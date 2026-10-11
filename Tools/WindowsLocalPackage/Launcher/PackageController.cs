namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;
    using System.Xml.Linq;

    internal sealed class PackageController
    {
        internal const int DatabasePort = 3308;
        private readonly string root;
        private readonly PackageState state;
        private readonly Action<string> status;
        private readonly List<OwnedProcess> owned = new List<OwnedProcess>();
        private string adminPasswordInUse;
        private bool verified;
        private volatile bool downloading;
        private volatile bool downloadCancelled;
        private volatile HttpWebRequest downloadRequest;

        internal PackageController(string root, Action<string> status)
        {
            this.root = root;
            this.status = status;
            state = PackageState.Load(root);
            RecoverOwnedProcesses();
        }

        internal string ClientFolder { get { return state.ClientFolder; } }
        internal bool SetupComplete { get { return state.SetupComplete; } }
        internal bool IsRunning { get { return owned.Any(item => !item.Process.HasExited); } }

        internal static void VerifyPackage(string root)
        {
            string manifest = SafePath(root, "package-manifest.json");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            var document = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifest));
            if (Convert.ToInt32(document["schemaVersion"], CultureInfo.InvariantCulture) != 1
                || (string)document["architecture"] != "win-x64")
                throw new InvalidDataException("This package requires a supported Windows x64 manifest.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> entry in (IEnumerable)document["files"])
            {
                string relative = (string)entry["path"];
                if (!seen.Add(relative.Replace('/', '\\')))
                    throw new InvalidDataException("The package inventory contains a duplicate file.");

                string file = SafePath(root, relative);
                if (!File.Exists(file) || new FileInfo(file).Length != Convert.ToInt64(entry["length"], CultureInfo.InvariantCulture))
                    throw new InvalidDataException("Missing or incomplete package file: " + relative);

                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(file))
                {
                    string hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!hash.Equals((string)entry["sha256"], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Package file verification failed: " + relative);
                }
            }

            foreach (string required in new[]
            {
                "AORebirth.exe", "runtime/dotnet.exe", "mysql/bin/mysqld.exe", "mysql/bin/mysql.exe",
                "server/LoginEngine/LoginEngine.dll", "server/ChatEngine/ChatEngine.dll",
                "server/ZoneEngine_New/ZoneEngine_New.dll", "tools/DatabaseSetup/DatabaseSetup.dll",
                "tools/DatabaseMigrationTool/DatabaseMigrationTool.dll",
                "tools/AccountBroker/AORebirth.AccountBroker.Service.exe", "config/Config.xml", "config/client.xml",
                "prerequisites/vc_redist.x64.exe"
            })
            {
                if (!seen.Contains(required.Replace('/', '\\')))
                    throw new InvalidDataException("Required component missing from inventory: " + required);
            }

            if (!Directory.Exists(SafePath(root, "GameData")) || !seen.Any(file => file.StartsWith("GameData\\", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The complete GameData package is required.");

            ValidateConfig(PackageState.ReadXml(SafePath(root, "config/Config.xml")));
            ReadClient(root);
        }

        internal void SetClientFolder(string folder)
        {
            XElement client = ReadClient(root);
            string executable = SafePath(Path.GetFullPath(folder), (string)client.Attribute("executable"));
            if (!File.Exists(executable))
                throw new InvalidDataException("Choose the extracted AORebirth client folder containing " + (string)client.Attribute("executable") + ".");

            VerifyClientExecutable(executable, client);
            state.ClientFolder = Path.GetFullPath(folder);
            state.Save(root);
        }

        internal void DownloadClient()
        {
            downloadCancelled = false;
            downloading = true;
            try
            {
                DownloadClientCore();
            }
            catch (Exception) when (downloadCancelled)
            {
                throw new OperationCanceledException("Client download cancelled. Partial files were preserved; no incomplete client was selected.");
            }
            finally
            {
                downloadRequest = null;
                downloading = false;
            }
        }

        internal void CancelDownload()
        {
            if (!downloading)
                return;

            downloadCancelled = true;
            HttpWebRequest request = downloadRequest;
            if (request != null)
            {
                request.Abort();
            }

            status("Cancelling the client download...");
        }

        private void CheckDownloadCancellation()
        {
            if (downloadCancelled)
                throw new OperationCanceledException("Client download cancelled. Partial files were preserved; no incomplete client was selected.");
        }

        private void DownloadClientCore()
        {
            EnsurePackage();
            CheckDownloadCancellation();
            InitializeUserFiles();
            XElement client = ReadClient(root);
            string expectedHash = (string)client.Attribute("archiveSha256");
            string downloadKey = expectedHash ?? (string)client.Attribute("executableSha256");
            string destination = PathOf("client/" + downloadKey.Substring(0, 12));
            if (Directory.Exists(destination))
            {
                SetExtractedClient(destination, client);
                status("The verified client is already installed.");
                return;
            }

            Directory.CreateDirectory(PathOf("data/downloads"));
            Directory.CreateDirectory(PathOf("client"));
            string archive = PathOf("data/downloads/client-" + downloadKey + ".zip");
            if (!File.Exists(archive))
            {
                string partial = archive + "." + Guid.NewGuid().ToString("N") + ".partial";
                status("Downloading the AORebirth client...");
                var request = (HttpWebRequest)WebRequest.Create((string)client.Attribute("downloadUrl"));
                request.AllowAutoRedirect = false;
                request.Timeout = 30000;
                request.ReadWriteTimeout = 60000;
                request.UserAgent = "AORebirth-WindowsLocalLauncher/1";
                downloadRequest = request;
                CheckDownloadCancellation();
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new InvalidOperationException("The approved client download did not return the expected file.");

                    long pinnedLength = (long)client.Attribute("archiveLength");
                    string pinnedEtag = (string)client.Attribute("etag");
                    if (response.ContentLength != pinnedLength
                        || (!string.IsNullOrEmpty(pinnedEtag) && response.Headers["ETag"] != pinnedEtag))
                        throw new InvalidDataException("The website client download metadata changed. Install an updated AORebirth server package before downloading this client.");

                    byte[] buffer = new byte[1024 * 1024];
                    long downloaded = 0;
                    long lastReport = 0;
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        CheckDownloadCancellation();
                        output.Write(buffer, 0, count);
                        downloaded += count;
                        if (downloaded > 20L * 1024 * 1024 * 1024)
                            throw new InvalidDataException("The client download exceeded the expected package size limit.");

                        if (downloaded - lastReport >= 16 * 1024 * 1024)
                        {
                            status("Downloading client: " + (downloaded / (1024 * 1024)) + " MB" +
                                (response.ContentLength > 0 ? " of " + (response.ContentLength / (1024 * 1024)) + " MB" : string.Empty));
                            lastReport = downloaded;
                        }
                    }

                    if (response.ContentLength >= 0 && response.ContentLength != downloaded)
                        throw new InvalidDataException("The client download was interrupted. The partial download was preserved.");

                    output.Flush(true);
                }

                status("Verifying the downloaded client...");
                VerifyArchive(partial, client);
                CheckDownloadCancellation();
                File.Move(partial, archive);
            }

            VerifyArchive(archive, client);
            CheckDownloadCancellation();
            string staging = PathOf("client/.extract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            status("Extracting the verified client...");
            long extracted = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var zip = ZipFile.OpenRead(archive))
            {
                if (zip.Entries.Count > 100000)
                    throw new InvalidDataException("The client archive contains too many entries.");

                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    CheckDownloadCancellation();
                    string path = SafePath(staging, entry.FullName);
                    if (!seen.Add(path) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("The client archive contains a duplicate path or filesystem link.");

                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(path);
                        continue;
                    }

                    extracted += entry.Length;
                    if (entry.Length < 0 || extracted > 40L * 1024 * 1024 * 1024)
                        throw new InvalidDataException("The client archive exceeded the extraction size limit.");

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (Stream input = entry.Open())
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[1024 * 1024];
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            CheckDownloadCancellation();
                            output.Write(buffer, 0, count);
                        }

                        if (output.Length != entry.Length)
                            throw new InvalidDataException("A client file was not fully extracted.");
                    }
                }
            }

            string folder = FindClientFolder(staging, client);
            VerifyClientExecutable(SafePath(folder, (string)client.Attribute("executable")), client);
            CheckDownloadCancellation();
            Directory.Move(staging, destination);
            SetExtractedClient(destination, client);
            status("Client downloaded, verified and installed. Create your account, then press Play.");
        }

        internal void InstallPrerequisites()
        {
            EnsurePackage();
            string installer = PathOf("prerequisites/vc_redist.x64.exe");
            using (Process process = Process.Start(new ProcessStartInfo(installer, "/install /passive /norestart")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                process.WaitForExit();
                if (process.ExitCode != 0 && process.ExitCode != 3010 && process.ExitCode != 1638)
                    throw new InvalidOperationException("Microsoft runtime setup failed (exit " + process.ExitCode + ").");

                status(process.ExitCode == 3010 ? "Microsoft runtime installed. Restart Windows before first-time setup." : "Microsoft runtime is installed. Continue with first-time setup.");
            }
        }

        internal void Prepare()
        {
            EnsurePackage();
            if (IsRunning)
                throw new InvalidOperationException("Stop the local server before running setup.");

            InitializeUserFiles();
            AccountUrlReservation.EnsureForCurrentUser();
            RunDotnet("server/ZoneEngine_New/ZoneEngine_New.dll", "--validate-startup", 180000, "world-check");
            RequireFreePorts(3308, 6996, 7012, 7500, 7501, 7510);
            try
            {
                StartDatabase();
                status("Preparing the local database...");
                var environment = BaseEnvironment();
                environment["AO_REBIRTH_MIGRATION_CONNECTION"] = AdminConnection(adminPasswordInUse);
                environment["AO_REBIRTH_MYSQL_ADMIN_PASSWORD"] = state.AdminPassword;
                RunExecutable("runtime/dotnet.exe", Quote(PathOf("tools/DatabaseSetup/DatabaseSetup.dll"))
                    + " prepare --root " + Quote(root) + " --port 3308", 300000, "database-setup", environment, null);
                adminPasswordInUse = state.AdminPassword;
                state.SetupComplete = true;
                state.Save(root);
                status("Local server setup is complete.");
            }
            finally
            {
                Stop();
            }
        }

        internal void Start()
        {
            EnsurePackage();
            if (!state.SetupComplete)
                throw new InvalidOperationException("Run First-time setup before starting the local server.");

            if (IsRunning)
            {
                EnsureServerReady();
                return;
            }

            InitializeUserFiles();
            RequireFreePorts(3308, 6996, 7012, 7500, 7501, 7510);
            try
            {
                StartDatabase();
                status("Checking local database readiness...");
                RunDotnet("server/ZoneEngine_New/ZoneEngine_New.dll", "--validate-database", 60000, "database-check");
                RunDotnet("server/ZoneEngine_New/ZoneEngine_New.dll", "--validate-startup", 180000, "world-check");
                StartEngine("ChatEngine", new[] { 6996, 7012 });
                StartEngine("LoginEngine", new[] { 7500 });
                StartEngine("ZoneEngine_New", new[] { 7501 });
                StartBroker();
                status("Local server is ready. Create an account or press Play.");
            }
            catch
            {
                Stop();
                throw;
            }
        }

        internal void CreateAccount()
        {
            Start();
            Process.Start(new ProcessStartInfo("http://127.0.0.1:7510/register") { UseShellExecute = true });
        }

        internal void Play()
        {
            XElement client = ReadClient(root);
            if (string.IsNullOrWhiteSpace(state.ClientFolder))
                throw new InvalidOperationException("Download and extract the AORebirth client, then select its folder.");

            string executable = SafePath(state.ClientFolder, (string)client.Attribute("executable"));
            if (!File.Exists(executable))
                throw new InvalidDataException("The selected client executable is missing. Select the extracted client folder again.");

            VerifyClientExecutable(executable, client);
            Start();
            Process.Start(new ProcessStartInfo(executable, (string)client.Attribute("arguments") ?? string.Empty)
            {
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = true
            });
            status("Client launched. Keep this window open while playing.");
        }

        internal void OpenLogs()
        {
            string logs = PathOf("logs");
            Directory.CreateDirectory(logs);
            Process.Start(new ProcessStartInfo(logs) { UseShellExecute = true });
        }

        internal void CheckStatus()
        {
            if (!IsRunning)
            {
                status("Local server is stopped.");
                return;
            }

            EnsureServerReady();
            status("Local server and account registration are ready.");
        }

        private void EnsureServerReady()
        {
            foreach (string name in new[] { "MySQL", "ChatEngine", "LoginEngine", "ZoneEngine_New", "AccountBroker" })
            {
                OwnedProcess item = owned.SingleOrDefault(process => process.Name == name);
                if (item == null || item.Process.HasExited)
                    throw new InvalidOperationException("The local server is only partially running (" + name + " is unavailable). Press Stop server, inspect Logs, then start again.");

                VerifyOwnership(item);
                if (name != "AccountBroker")
                {
                    int[] ports = name == "MySQL" ? new[] { 3308 } : name == "ChatEngine" ? new[] { 6996, 7012 }
                        : name == "LoginEngine" ? new[] { 7500 } : new[] { 7501 };
                    WaitForPorts(item, ports, 2000);
                }
            }

            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:7510/health");
            request.Proxy = null;
            request.Timeout = 2000;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException("Local account registration is unavailable. Stop the local server and inspect Logs.");
            }
        }

        internal void Stop()
        {
            Exception failure = null;
            bool forced = false;
            bool abnormalExit = false;
            foreach (OwnedProcess item in owned.ToArray().Reverse())
            {
                try
                {
                    if (item.Process.HasExited)
                    {
                        abnormalExit |= item.Name != "AccountBroker" && item.Process.ExitCode != 0;
                        owned.Remove(item);
                        SaveOwnedProcesses();
                        item.Process.Dispose();
                        continue;
                    }

                    VerifyOwnership(item);
                    status("Stopping " + item.Name + "...");
                    if (item.Name == "MySQL")
                    {
                        WaitForPorts(item, new[] { 3308 }, 2000);
                        if (CanConnectAdmin(state.AdminPassword))
                        {
                            adminPasswordInUse = state.AdminPassword;
                        }
                        else if (!state.SetupComplete && state.OwnsDataDirectory)
                        {
                            WaitForPorts(item, new[] { 3308 }, 2000);
                            if (!CanConnectAdmin(string.Empty))
                                throw new InvalidOperationException("The recovered local MySQL credentials could not be verified. Its ownership record was preserved.");

                            adminPasswordInUse = string.Empty;
                        }
                        else
                        {
                            throw new InvalidOperationException("The recovered local MySQL credentials could not be verified. MySQL was left running and its ownership record was preserved.");
                        }

                        var environment = BaseEnvironment();
                        environment["MYSQL_PWD"] = adminPasswordInUse ?? state.AdminPassword;
                        WaitForPorts(item, new[] { 3308 }, 2000);
                        RunExecutable("mysql/bin/mysql.exe", MysqlArguments(), 30000, "database-stop", environment, "SHUTDOWN;\n");
                        if (!item.Process.WaitForExit(30000))
                            throw new InvalidOperationException("MySQL did not stop cleanly. Keep the launcher open and inspect the logs.");
                    }
                    else
                    {
                        if (item.ShutdownFile != null)
                        {
                            File.WriteAllText(item.ShutdownFile, "stop");
                        }
                        else
                        {
                            item.Process.CloseMainWindow();
                        }

                        if (!item.Process.WaitForExit(item.ShutdownFile == null ? 5000 : 45000))
                        {
                            VerifyOwnership(item);
                            forced = true;
                            item.Process.Kill();
                            if (!item.Process.WaitForExit(10000))
                                throw new InvalidOperationException(item.Name + " has not exited.");
                        }
                    }

                    abnormalExit |= item.Name != "AccountBroker" && item.Process.ExitCode != 0;
                    owned.Remove(item);
                    SaveOwnedProcesses();
                    item.Process.Dispose();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    // Do not shut down the database while a gameplay process could still save.
                    break;
                }
            }

            if (failure != null)
                throw failure;

            status(forced || abnormalExit ? "Local server stopped. A service did not shut down cleanly; inspect Logs before starting again."
                : "Local server stopped. Your local database remains in data.");
        }

        private void EnsurePackage()
        {
            if (verified)
                return;

            status("Verifying the Windows package...");
            VerifyPackage(root);
            verified = true;
        }

        private void InitializeUserFiles()
        {
            SafePath(root, "data");
            SafePath(root, "logs");
            state.Save(root);
            Directory.CreateDirectory(PathOf("logs"));
            Directory.CreateDirectory(PathOf("data/config"));
            string config = PathOf("data/config/Config.xml");
            if (!File.Exists(config))
            {
                File.Copy(PathOf("config/Config.xml"), config);
            }

            ValidateConfig(PackageState.ReadXml(config));
        }

        private void StartDatabase()
        {
            status("Starting the private local MySQL instance...");
            string data = PathOf("data/mysql");
            bool fresh = !Directory.Exists(data) || !Directory.EnumerateFileSystemEntries(data).Any();
            if (fresh)
            {
                if (state.SetupComplete)
                    throw new InvalidDataException("The local database files are missing. Restore your data backup before continuing.");

                // An initial native-loader failure can leave an empty directory; retry only that empty state.
                state.OwnsDataDirectory = true;
                state.Save(root);
                Directory.CreateDirectory(data);
            }
            else if (!state.OwnsDataDirectory || !File.Exists(Path.Combine(data, "auto.cnf")))
            {
                throw new InvalidDataException("The data/mysql directory is unrecognized or incompletely initialized. No existing files were changed.");
            }

            string options = PathOf("data/mysql.ini");
            File.WriteAllText(options, "[mysqld]\r\nbasedir=\"" + PathOf("mysql").Replace('\\', '/')
                + "\"\r\ndatadir=\"" + data.Replace('\\', '/') + "\"\r\nport=3308\r\nbind-address=127.0.0.1\r\nmysqlx=0\r\nskip-log-bin\r\nlocal-infile=0\r\n");
            string arguments = "--defaults-file=" + Quote(options);
            if (fresh)
            {
                status("Initializing your dedicated local database...");
                RunExecutable("mysql/bin/mysqld.exe", arguments + " --initialize-insecure --console", 180000,
                    "database-initialize", BaseEnvironment(), null);
            }

            OwnedProcess process = Launch("MySQL", "mysql/bin/mysqld.exe", arguments + " --console", null, BaseEnvironment());
            WaitForPorts(process, new[] { 3308 }, 60000);
            adminPasswordInUse = state.AdminPassword;
            if (!CanConnectAdmin(state.AdminPassword))
            {
                if (state.SetupComplete || !state.OwnsDataDirectory || !CanConnectAdmin(string.Empty))
                    throw new InvalidOperationException("The local database credentials could not be verified. Restore matching data and credentials from backup.");

                adminPasswordInUse = string.Empty;
            }
        }

        private bool CanConnectAdmin(string password)
        {
            var environment = BaseEnvironment();
            environment["MYSQL_PWD"] = password;
            return RunExecutable("mysql/bin/mysql.exe", MysqlArguments(), 15000, "database-connect",
                environment, "SELECT 1;\n", false) == 0;
        }

        private static string MysqlArguments()
        {
            return "--no-defaults --protocol=TCP --host=127.0.0.1 --port=3308 --user=root --batch --skip-column-names --connect-timeout=5";
        }

        private void StartEngine(string name, int[] ports)
        {
            status("Starting " + name + "...");
            string signal = PathOf("data/stop-" + name + "-" + Guid.NewGuid().ToString("N") + ".signal");
            string dll = PathOf("server/" + name + "/" + name + ".dll");
            OwnedProcess process = Launch(name, "runtime/dotnet.exe",
                Quote(dll) + " /autostart /headless /shutdown-file " + Quote(signal), signal, BaseEnvironment());
            WaitForPorts(process, ports, 120000);
        }

        private void StartBroker()
        {
            status("Starting local account registration...");
            AccountUrlReservation.EnsureForCurrentUser();
            var environment = BaseEnvironment();
            environment["AOREBIRTH_ACCOUNT_BROKER_URL"] = "http://127.0.0.1:7510/";
            OwnedProcess process = Launch("AccountBroker", "tools/AccountBroker/AORebirth.AccountBroker.Service.exe",
                string.Empty, null, environment);
            // HttpListener uses HTTP.sys, whose listener PID is System rather than the broker PID.
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 30000)
            {
                if (process.Process.HasExited)
                    throw new InvalidOperationException("Local account registration exited. Open Logs for details.");

                VerifyOwnership(process);
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:7510/health");
                    request.Proxy = null;
                    request.Timeout = 1000;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                            return;
                    }
                }
                catch (WebException)
                {
                    Thread.Sleep(250);
                }
            }

            throw new InvalidOperationException("Local account registration did not become ready.");
        }

        private void RunDotnet(string dll, string arguments, int timeout, string log)
        {
            RunExecutable("runtime/dotnet.exe", Quote(PathOf(dll)) + " " + arguments, timeout, log, BaseEnvironment(), null);
        }

        private int RunExecutable(string relative, string arguments, int timeout, string log,
            Dictionary<string, string> environment, string input, bool throwOnFailure = true)
        {
            using (Process process = NewProcess(relative, arguments, environment))
            {
                process.StartInfo.RedirectStandardInput = input != null;
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start " + log + ".");

                var identity = new OwnedProcess(log, process, PathOf(relative), null);
                CaptureOutput(process, log);
                if (input != null)
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.Close();
                }

                if (!process.WaitForExit(timeout))
                {
                    VerifyOwnership(identity);
                    process.Kill();
                    process.WaitForExit(10000);
                    throw new InvalidOperationException(log + " timed out. Inspect logs before retrying.");
                }

                process.WaitForExit();
                if (throwOnFailure && process.ExitCode != 0)
                    throw new InvalidOperationException(log + " failed (exit " + process.ExitCode + "). Open Logs for details.");

                return process.ExitCode;
            }
        }

        private OwnedProcess Launch(string name, string executable, string arguments, string signal,
            Dictionary<string, string> environment)
        {
            Process process = NewProcess(executable, arguments, environment);
            if (!process.Start())
                throw new InvalidOperationException("Unable to start " + name + ".");

            var item = new OwnedProcess(name, process, PathOf(executable), signal);
            owned.Add(item);
            SaveOwnedProcesses();
            CaptureOutput(process, name);
            return item;
        }

        private void RecoverOwnedProcesses()
        {
            string recordPath = PathOf("data/owned-processes.xml");
            if (!File.Exists(recordPath))
                return;

            XElement document = PackageState.ReadXml(recordPath);
            if (document.Name != "OwnedProcesses")
                throw new InvalidDataException("The saved process ownership document is invalid. No processes were stopped.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            var pids = new HashSet<int>();
            foreach (XElement entry in document.Elements())
            {
                string name = (string)entry.Attribute("name");
                if (entry.Name != "Process" || name == null || !names.Add(name))
                    throw new InvalidDataException("The saved process ownership document contains an unknown or duplicate component.");

                string expected = ExpectedExecutable(name);
                string executable = (string)entry.Attribute("executable");
                if (executable == null || !Path.GetFullPath(executable).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A saved process executable is outside the expected package component. No processes were stopped.");

                int pid = (int?)entry.Attribute("pid") ?? 0;
                long ticks = (long?)entry.Attribute("startedUtcTicks") ?? 0;
                if (pid <= 0 || !pids.Add(pid) || ticks <= 0)
                    throw new InvalidDataException("A saved process identity is invalid.");

                string signal = (string)entry.Attribute("shutdownFile");
                bool engine = name == "ChatEngine" || name == "LoginEngine" || name == "ZoneEngine_New";
                if (engine)
                {
                    string prefix = PathOf("data/stop-" + name + "-");
                    if (signal == null || !Path.GetFullPath(signal).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        || !Path.GetDirectoryName(Path.GetFullPath(signal)).Equals(PathOf("data"), StringComparison.OrdinalIgnoreCase)
                        || !signal.EndsWith(".signal", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("A saved shutdown signal does not belong to this package's protected data directory.");

                    string relative = Path.GetFullPath(signal).Substring(root.TrimEnd('\\').Length + 1);
                    signal = PathOf(relative);
                }
                else if (!string.IsNullOrEmpty(signal))
                {
                    throw new InvalidDataException("A saved non-engine process has an unexpected shutdown signal.");
                }

                Process process;
                try
                {
                    process = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    // The original process has exited. A stale record has no authority over another process.
                    continue;
                }

                try
                {
                    if (process.Handle == IntPtr.Zero)
                        throw new InvalidOperationException("Windows could not retain the saved process identity.");

                    if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != ticks)
                    {
                        process.Dispose();
                        continue;
                    }

                    var item = new OwnedProcess(name, process, expected, signal);
                    VerifyOwnership(item);
                    owned.Add(item);
                }
                catch (Exception exception) when ((exception is Win32Exception || exception is InvalidOperationException)
                    && IsRecordedProcessGone(pid, ticks))
                {
                    process.Dispose();
                    // A bounded second lookup proves exit or PID reuse; it never adopts or stops the replacement.
                }
                catch
                {
                    process.Dispose();
                    throw;
                }
            }

            if (owned.Count > 0 && state.AdminPassword == null)
                throw new InvalidDataException("Local credentials for recovered services are missing. Restore the matching protected data backup before continuing.");

            SaveOwnedProcesses();
        }

        private static bool IsRecordedProcessGone(int pid, long expectedTicks)
        {
            try
            {
                using (Process current = Process.GetProcessById(pid))
                {
                    return current.HasExited || current.StartTime.ToUniversalTime().Ticks != expectedTicks;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private string ExpectedExecutable(string name)
        {
            if (name == "MySQL")
                return PathOf("mysql/bin/mysqld.exe");

            if (name == "AccountBroker")
                return PathOf("tools/AccountBroker/AORebirth.AccountBroker.Service.exe");

            if (name == "ChatEngine" || name == "LoginEngine" || name == "ZoneEngine_New")
                return PathOf("runtime/dotnet.exe");

            throw new InvalidDataException("The saved process ownership document contains an unknown component.");
        }

        private void SaveOwnedProcesses()
        {
            string path = PathOf("data/owned-processes.xml");
            if (owned.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            var document = new XElement("OwnedProcesses", owned.Select(item => new XElement("Process",
                new XAttribute("name", item.Name),
                new XAttribute("pid", item.Process.Id),
                new XAttribute("startedUtcTicks", item.StartedUtc.Ticks),
                new XAttribute("executable", item.Executable),
                item.ShutdownFile == null ? null : new XAttribute("shutdownFile", item.ShutdownFile))));
            PackageState.AtomicWrite(path, Encoding.UTF8.GetBytes(document.ToString()));
        }

        private Process NewProcess(string relative, string arguments, Dictionary<string, string> environment)
        {
            string executable = PathOf(relative);
            var info = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string key in info.EnvironmentVariables.Keys.Cast<string>().ToArray())
            {
                if (key.StartsWith("AO_REBIRTH_", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("AOREBIRTH_", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("MYSQL_", StringComparison.OrdinalIgnoreCase))
                {
                    info.EnvironmentVariables.Remove(key);
                }
            }

            foreach (var entry in environment)
            {
                info.EnvironmentVariables[entry.Key] = entry.Value;
            }

            return new Process { StartInfo = info };
        }

        private Dictionary<string, string> BaseEnvironment()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOTNET_ROOT"] = PathOf("runtime"),
                ["DOTNET_ROOT_X64"] = PathOf("runtime"),
                ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                ["AO_REBIRTH_MYSQL_CONNECTION"] = "Server=127.0.0.1;Port=3308;Database=cellao_codex_clean;User ID=aorebirth_local;Password=" + state.RuntimePassword + ";AllowPublicKeyRetrieval=True;",
                ["AO_REBIRTH_EXPECTED_DATABASE"] = "cellao_codex_clean",
                ["AO_REBIRTH_CONFIG_PATH"] = PathOf("data/config/Config.xml"),
                ["AO_REBIRTH_GAMEDATA_PATH"] = PathOf("GameData"),
                ["AO_REBIRTH_BIND_MODE"] = "Loopback",
                ["AO_REBIRTH_ISCOM_LISTEN_IP"] = "127.0.0.1",
                ["AO_REBIRTH_CHAT_LISTEN_IP"] = "127.0.0.1"
            };
        }

        private static string AdminConnection(string password)
        {
            return "Server=127.0.0.1;Port=3308;Database=cellao_codex_clean;User ID=root;Password=" + password + ";AllowPublicKeyRetrieval=True;";
        }

        private void CaptureOutput(Process process, string name)
        {
            object gate = new object();
            bool logFailed = false;
            DataReceivedEventHandler write = (sender, args) =>
            {
                if (args.Data == null)
                    return;

                string line = args.Data;
                foreach (string secret in new[] { state.AdminPassword, state.RuntimePassword })
                {
                    if (!string.IsNullOrEmpty(secret))
                    {
                        line = line.Replace(secret, "[redacted]");
                    }
                }

                lock (gate)
                {
                    if (logFailed)
                        return;

                    try
                    {
                        File.AppendAllText(PathOf("logs/" + name + ".log"), DateTime.UtcNow.ToString("O") + " " + line + Environment.NewLine);
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        logFailed = true;
                        status("Log output could not be saved. Check available disk space and folder permissions.");
                    }
                }
            };
            process.OutputDataReceived += write;
            process.ErrorDataReceived += write;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private static void RequireFreePorts(params int[] ports)
        {
            foreach (IPEndPoint listener in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                if (ports.Contains(listener.Port))
                    throw new InvalidOperationException("Local port " + listener.Port + " is already in use. Close the other server first. No processes were stopped.");
            }
        }

        private static void WaitForPorts(OwnedProcess item, int[] ports, int timeout)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeout)
            {
                if (item.Process.HasExited)
                    throw new InvalidOperationException(item.Name + " exited during startup. Open Logs for details.");

                VerifyOwnership(item);
                IDictionary<int, int> listeners = ReadListenerOwners(ports);
                if (ports.All(port => listeners.ContainsKey(port) && listeners[port] == item.Process.Id))
                    return;

                if (ports.Any(port => listeners.ContainsKey(port) && listeners[port] != item.Process.Id))
                    throw new InvalidOperationException("A required port belongs to another process. No unrelated process was stopped.");

                Thread.Sleep(250);
            }

            throw new InvalidOperationException(item.Name + " did not become ready. Open Logs for details.");
        }

        private static IDictionary<int, int> ReadListenerOwners(int[] ports)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, false, 2, 3, 0) != 0)
                    throw new InvalidOperationException("Windows could not verify local listener ownership.");

                int count = Marshal.ReadInt32(buffer);
                var owners = new Dictionary<int, int>();
                for (int index = 0; index < count; index++)
                {
                    IntPtr row = IntPtr.Add(buffer, 4 + index * 24);
                    byte first = Marshal.ReadByte(row, 8);
                    byte second = Marshal.ReadByte(row, 9);
                    int port = first * 256 + second;
                    if (!ports.Contains(port))
                        continue;

                    int pid = Marshal.ReadInt32(row, 20);
                    if (owners.ContainsKey(port) && owners[port] != pid)
                        throw new InvalidOperationException("Multiple processes own a local port.");

                    owners[port] = pid;
                }

                return owners;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

        private static void VerifyOwnership(OwnedProcess item)
        {
            if (item.Process.HasExited)
                return;

            if (item.Process.StartTime.ToUniversalTime() != item.StartedUtc
                || !Path.GetFullPath(item.Process.MainModule.FileName).Equals(item.Executable, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Process ownership could not be verified for " + item.Name + ". No stop was attempted.");
        }

        private static XElement ReadClient(string root)
        {
            XElement client = PackageState.ReadXml(SafePath(root, "config/client.xml"));
            Uri url;
            if (client.Name != "Client" || !Uri.TryCreate((string)client.Attribute("downloadUrl"), UriKind.Absolute, out url)
                || url.Scheme != Uri.UriSchemeHttps || url.Host != "ao-rebirth.com"
                || string.IsNullOrWhiteSpace((string)client.Attribute("executable"))
                || (client.Attribute("archiveSha256") != null && !IsHash((string)client.Attribute("archiveSha256")))
                || ((long?)client.Attribute("archiveLength") ?? 0) <= 0
                || !IsHash((string)client.Attribute("executableSha256")))
                throw new InvalidDataException("The approved client download configuration is missing or invalid.");

            SafePath(root, (string)client.Attribute("executable"));
            return client;
        }

        private static bool IsHash(string value)
        {
            return value != null && value.Length == 64 && value.All(character => Uri.IsHexDigit(character));
        }

        private static void VerifyHash(string path, string expected, string description)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                if (!BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(description + " does not match the approved AORebirth download.");
            }
        }

        private static void VerifyClientExecutable(string executable, XElement client)
        {
            VerifyHash(executable, (string)client.Attribute("executableSha256"), "Client executable");
        }

        private static void VerifyArchive(string archive, XElement client)
        {
            if (new FileInfo(archive).Length != (long)client.Attribute("archiveLength"))
                throw new InvalidDataException("The client archive is incomplete or does not match the approved download length.");

            string hash = (string)client.Attribute("archiveSha256");
            if (hash != null)
            {
                VerifyHash(archive, hash, "Client archive");
            }
        }

        private static string FindClientFolder(string directory, XElement client)
        {
            string relative = ((string)client.Attribute("executable")).Replace('/', '\\');
            var matches = Directory.EnumerateFiles(directory, Path.GetFileName(relative), SearchOption.AllDirectories)
                .Where(file => file.EndsWith("\\" + relative, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException("The verified archive does not contain one unambiguous client executable.");

            return matches[0].Substring(0, matches[0].Length - relative.Length).TrimEnd('\\');
        }

        private void SetExtractedClient(string directory, XElement client)
        {
            SetClientFolder(FindClientFolder(directory, client));
        }

        private static void ValidateConfig(XElement config)
        {
            foreach (var value in new Dictionary<string, string>
            {
                ["SQLType"] = "MySql", ["ChatIP"] = "127.0.0.1", ["ZoneIP"] = "127.0.0.1",
                ["ChatPort"] = "7012", ["ZonePort"] = "7501", ["LoginPort"] = "7500", ["CommPort"] = "6996"
            })
            {
                if (!string.Equals((string)config.Element(value.Key), value.Value, StringComparison.Ordinal))
                    throw new InvalidDataException("Local package configuration must retain " + value.Key + "=" + value.Value + ".");
            }
        }

        private string PathOf(string relative) { return SafePath(root, relative); }

        private static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0)
                throw new InvalidDataException("A package path is invalid.");

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', '\\')));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A package path leaves the installation folder.");

            string current = path;
            while (current.Length >= fullRoot.Length - 1)
            {
                if ((File.Exists(current) || Directory.Exists(current))
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Package and client folders must not contain filesystem links.");

                current = Path.GetDirectoryName(current);
                if (current == null)
                    break;
            }

            return path;
        }

        private static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new InvalidDataException("The installation path contains unsupported characters.");

            return "\"" + value.TrimEnd('\\') + "\"";
        }

        private sealed class OwnedProcess
        {
            internal readonly string Name;
            internal readonly Process Process;
            internal readonly string Executable;
            internal readonly string ShutdownFile;
            internal readonly DateTime StartedUtc;

            internal OwnedProcess(string name, Process process, string executable, string shutdownFile)
            {
                Name = name;
                Process = process;
                Executable = executable;
                ShutdownFile = shutdownFile;
                StartedUtc = process.StartTime.ToUniversalTime();
            }
        }
    }
}
