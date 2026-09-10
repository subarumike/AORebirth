using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

/// <summary>Owns only an explicitly supplied binary, fixture environment, loopback listener and shutdown file.</summary>
sealed class ConnectedEngineProcess : IDisposable
{
    readonly Process process;
    readonly string shutdown;
    readonly StringBuilder output = new();
    readonly DisposableSchemaDatabase fixture;
    public int Id => process.Id;
    public string BinarySha256 { get; }
    public ConnectedEngineProcess(string assembly, bool login, DisposableSchemaDatabase fixture)
    {
        this.fixture = fixture;
        assembly = Path.GetFullPath(assembly);
        BinarySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly)));
        shutdown = Path.Combine(fixture.DirectoryPath, "connected-stop-" + Guid.NewGuid().ToString("N"));
        bool managed = Path.GetExtension(assembly).Equals(".dll", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo(managed ? "dotnet" : assembly) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(assembly)! };
        FixtureEnvironment.ClearInheritedRuntimeSettings(start);
        if (managed) start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(login ? "/headless" : "--headless");
        start.ArgumentList.Add(login ? "/shutdown-file" : "--shutdown-file");
        start.ArgumentList.Add(shutdown);
        start.Environment["AO_REBIRTH_MYSQL_CONNECTION"] = fixture.ConnectionString;
        start.Environment["AO_REBIRTH_CONFIG_PATH"] = fixture.ConfigPath;
        start.Environment["AO_REBIRTH_BIND_MODE"] = "Loopback";
        start.Environment["AO_REBIRTH_EXPECTED_DATABASE"] = DisposableSchemaDatabase.DatabaseName;
        start.Environment["AO_REBIRTH_REQUIRED_SQL_TYPE"] = "MySql";
        process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            var elapsed = Stopwatch.StartNew();
            int port = login ? fixture.LoginPort : fixture.ZonePort;
            while (elapsed.Elapsed < TimeSpan.FromSeconds(60))
            {
                if (process.HasExited) throw new FixtureFailure("connected-engine-exited-before-ready-" + (login ? "login" : "zone"));
                bool marker;
                lock (output) marker = login ? output.ToString().Contains("Starting LoginEngine in headless mode.")
                    : output.ToString().Contains("ZONEENGINE_NEW_READY");
                if (marker)
                {
                    try { using var socket = new TcpClient(); socket.Connect(IPAddress.Loopback, port); return; }
                    catch (SocketException) { }
                }
                Thread.Sleep(100);
            }
            throw new FixtureFailure("connected-engine-readiness-timeout-" + (login ? "login" : "zone"));
        }
        catch { Dispose(); throw; }
    }
    public void Stop()
    {
        File.WriteAllText(shutdown, "stop");
        if (!process.WaitForExit(30000)) throw new FixtureFailure("connected-clean-shutdown-timeout");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new FixtureFailure("connected-clean-shutdown-exit");
    }
    public void Dispose()
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        string text;
        lock (output) text = output.ToString();
        // Persist local diagnostics without ephemeral credentials.
        string secret = new MySqlConnector.MySqlConnectionStringBuilder(fixture.ConnectionString).Password;
        text = text.Replace(fixture.ConnectionString, "<fixture-connection>").Replace(secret, "<fixture-secret>");
        File.WriteAllText(Path.Combine(ConnectedAcceptanceSmoke.RepositoryRoot(), "build-verify",
            "connected-engine-" + process.Id + ".log"), text);
        process.Dispose();
        if (File.Exists(shutdown)) File.Delete(shutdown);
    }
}
