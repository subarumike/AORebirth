namespace AORebirth.Tools.AccountDaoValidation
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using MySqlConnector;

    internal sealed class DisposableMySql : IDisposable
    {
        private const string Container = "aorebirth-account-dao-validation";
        private const string Network = "aorebirth_account_dao_validation_internal";
        private const string Volume = "aorebirth_account_dao_validation_data";
        private const string Database = "aorebirth_account_dao_validation";
        private const string User = "aorebirth_account_validation";
        private const string Label = "org.aorebirth.account-dao-run";
        private const string Image = "mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d";
        private const uint Port = 33070;
        private readonly string run = Guid.NewGuid().ToString("N");
        private string environmentFile;
        private bool containerCreated;
        private bool networkCreated;
        private bool volumeCreated;
        private bool disposed;

        internal string ApplicationConnectionString { get; private set; }
        internal string RootConnectionString { get; private set; }

        internal static DisposableMySql Create()
        {
            var fixture = new DisposableMySql();
            try
            {
                // A missing resource is not permission to reuse any existing database.
                Docker("info", "--format", "{{.ServerVersion}}");
                Docker("image", "inspect", Image);
                RequireAbsent("container", Container);
                RequireAbsent("network", Network);
                RequireAbsent("volume", Volume);
                var listener = new TcpListener(IPAddress.Loopback, checked((int)Port));
                try { listener.Start(); } finally { listener.Stop(); }
                string rootPassword = Secret();
                string password = Secret();
                fixture.environmentFile = Path.Combine(Path.GetTempPath(), "aorebirth-account-dao-" + fixture.run + ".env");
                File.WriteAllLines(fixture.environmentFile, new[]
                {
                    "MYSQL_ROOT_PASSWORD=" + rootPassword,
                    "MYSQL_DATABASE=" + Database,
                    "MYSQL_USER=" + User,
                    "MYSQL_PASSWORD=" + password
                });
                File.SetAttributes(fixture.environmentFile, FileAttributes.Hidden | FileAttributes.Temporary);
                // Host-loopback publication uses the proven mission fixture bridge pattern.
                // Docker internal networks suppress the required published-port mapping here.
                Docker("network", "create", "--label", Label + "=" + fixture.run, Network);
                fixture.networkCreated = true;
                Docker("volume", "create", "--label", Label + "=" + fixture.run, Volume);
                fixture.volumeCreated = true;
                Docker("run", "--detach", "--name", Container, "--label", Label + "=" + fixture.run,
                    "--restart", "no", "--network", Network, "--publish", "127.0.0.1:" + Port + ":3306",
                    "--env-file", fixture.environmentFile, "--volume", Volume + ":/var/lib/mysql", Image);
                fixture.containerCreated = true;
                fixture.RootConnectionString = Connection("root", rootPassword);
                fixture.ApplicationConnectionString = Connection(User, password);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal MySqlConnection WaitForReady()
        {
            var elapsed = Stopwatch.StartNew();
            Exception last = null;
            while (elapsed.Elapsed < TimeSpan.FromSeconds(60))
            {
                var connection = new MySqlConnection(this.RootConnectionString);
                try { connection.Open(); return connection; }
                catch (Exception exception) { last = exception; connection.Dispose(); Thread.Sleep(500); }
            }
            Console.Error.WriteLine("ACCOUNT_DAO_STARTUP_LAST_ERROR=" + (last == null ? "none" : last.GetType().Name));
            if (last is MySqlException) Console.Error.WriteLine("ACCOUNT_DAO_STARTUP_MYSQL_NUMBER=" + ((MySqlException)last).Number);
            throw new InvalidOperationException("disposable-mysql-startup-timeout");
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
            bool clean = true;
            clean &= this.RemoveOwned("container", Container, this.containerCreated);
            clean &= this.RemoveOwned("volume", Volume, this.volumeCreated);
            clean &= this.RemoveOwned("network", Network, this.networkCreated);
            if (!string.IsNullOrEmpty(this.environmentFile) && File.Exists(this.environmentFile))
            {
                try
                {
                    File.SetAttributes(this.environmentFile, FileAttributes.Normal);
                    File.Delete(this.environmentFile);
                }
                catch { clean = false; }
            }
            Console.WriteLine("ACCOUNT_DAO_DISPOSABLE_CLEANUP=" + (clean ? "PASS" : "FAIL"));
            if (!clean) throw new InvalidOperationException("disposable-cleanup-needs-attention");
        }

        private bool RemoveOwned(string kind, string name, bool created)
        {
            if (!created) return true;
            try
            {
                string format = kind == "container"
                    ? "{{index .Config.Labels \"" + Label + "\"}}"
                    : "{{index .Labels \"" + Label + "\"}}";
                Result label = RunDocker(kind, "inspect", "--format", format, name);
                if (label.Code != 0 || label.Output.Trim() != this.run)
                    return false; // Never delete a foreign or unverified resource.
                if (kind == "container") Docker("rm", "--force", name);
                else Docker(kind, "rm", name);
                RequireAbsent(kind, name);
                return true;
            }
            catch { return false; }
        }

        private static string Connection(string user, string password)
        {
            return new MySqlConnectionStringBuilder
            {
                Server = IPAddress.Loopback.ToString(), Port = Port, Database = Database, UserID = user,
                Password = password, SslMode = MySqlSslMode.None, ConnectionTimeout = 3,
                DefaultCommandTimeout = 15, Pooling = false
            }.ConnectionString;
        }

        private static string Secret()
        {
            byte[] bytes = new byte[36];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static void RequireAbsent(string kind, string name)
        {
            Result result = RunDocker(kind, "inspect", name);
            if (result.Code == 0) throw new InvalidOperationException("disposable-resource-already-exists");
        }

        private static void Docker(params string[] args)
        {
            if (RunDocker(args).Code != 0) throw new InvalidOperationException("docker-operation-failed");
        }

        private static Result RunDocker(params string[] args)
        {
            var start = new ProcessStartInfo("docker")
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            foreach (string argument in args) start.ArgumentList.Add(argument);
            using (Process process = Process.Start(start))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill(true);
                    throw new InvalidOperationException("docker-operation-timeout");
                }
                Task.WaitAll(output, errors);
                // Never print Docker stderr, environment files, connection strings or credential data.
                return new Result { Code = process.ExitCode, Output = output.Result };
            }
        }

        private sealed class Result { internal int Code; internal string Output; }
    }
}
