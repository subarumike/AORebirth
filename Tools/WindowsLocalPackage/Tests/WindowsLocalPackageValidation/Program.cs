namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;
    using System.Xml.Linq;
    using Microsoft.CSharp;

    internal static class OfflineTests
    {
        private static readonly string[] EngineNames = { "WorldEngine", "LoginEngine", "ZoneEngine_New", "ChatEngine" };
        private static readonly string[] DailyPackageFiles =
        {
            "php/php.exe", "php/php8.dll", "config/PhpRuntime.manifest.xml", "web/daily-router.php", "web/daily.php.ini",
            "web/daily/index.app", "web/daily/index.html", "web/daily/rewards.json", "web/daily/claim.php",
            "web/daily/css/icc.css", "web/daily/js/daily.js"
        };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private const string ReadyWorld = "{\"world\":{\"epoch\":1,\"recovering\":false,\"instances\":[{\"name\":\"chat\",\"role\":\"RoleChat\",\"connected\":true,\"heartbeatAgeMs\":100},{\"name\":\"zone-7501\",\"role\":\"RoleZone\",\"endpoint\":\"127.0.0.1:7501\",\"connected\":true,\"heartbeatAgeMs\":100}]}}";

        private static int Main(string[] args)
        {
            try
            {
                var options = ReadOptions(args);
                string launcher = RequiredFile(options, "--launcher");
                string databaseSetup = RequiredFile(options, "--database-setup");
                string dotnet = RequiredFile(options, "--dotnet");
                Type controller = Assembly.LoadFrom(launcher).GetType("AORebirth.WindowsLocalLauncher.PackageController", true);
                Type state = controller.Assembly.GetType("AORebirth.WindowsLocalLauncher.PackageState", true);
                MethodInfo verify = controller.GetMethod("VerifyPackage", BindingFlags.Static | BindingFlags.NonPublic);
                if (verify == null)
                    throw new InvalidOperationException("The actual launcher package verifier is absent.");

                var tests = new KeyValuePair<string, Action>[]
                {
                    Test("FOUR_ENGINE_LAUNCH", FourEngineLaunch),
                    Test("WORLDENGINE_FIRST", WorldEngineFirst),
                    Test("WORLD_FAILURE_BLOCKS_DEPENDENTS", WorldFailureBlocksDependents),
                    Test("FOUR_ENGINE_STOP", FourEngineStop),
                    Test("WORLDENGINE_LAST", WorldEngineLast),
                    Test("DAILY_PHP_LOOPBACK_TOPOLOGY", DailyPhpLoopbackTopology),
                    Test("DAILY_PHP_STOPS_BEFORE_CORE_ENGINES", DailyPhpStopsBeforeCoreEngines),
                    Test("STOP_REJECTS_UNKNOWN_OR_DUPLICATE_COMPONENTS", StopRejectsUnknownComponents),
                    Test("WORLD_REGISTRATION_READINESS", WorldRegistrationReadiness),
                    Test("CURRENT_MIGRATION_DISCOVERY", () => MigrationDiscovery(dotnet, databaseSetup)),
                    Test("LEGACY_CREDENTIAL_ROLLBACK_COMPATIBILITY", () => LegacyCredentialRoundTrip(state)),
                    Test("STARTUP_WAITER_PRESERVES_GUI_EXIT", StartupWaiterPreservesGuiExit),
                    Test("COMPLETE_PACKAGE_INVENTORY", () => WithFixture(fixture => Verify(verify, fixture.Root))),
                    Test("DAILY_PHP_PACKAGE_FILES_REQUIRED", () => DailyPhpPackageFilesRequired(verify)),
                    Test("LEGACY_PACKAGE_SCHEMA_REJECTED", () => WithFixture(fixture =>
                    {
                        fixture.WriteInventory(schemaVersion: 1);
                        MustReject(() => Verify(verify, fixture.Root));
                    })),
                    Test("MISSING_WORLDENGINE_REJECTED", () => WithFixture(fixture =>
                    {
                        fixture.Remove("server/WorldEngine/WorldEngine.dll");
                        fixture.WriteInventory();
                        MustReject(() => Verify(verify, fixture.Root));
                    })),
                    Test("OMITTED_CANONICAL_CONTENT_REJECTED", () => WithFixture(fixture =>
                    {
                        fixture.Remove("GameData/Playfields/1/Spawns.json");
                        fixture.WriteInventory();
                        MustReject(() => Verify(verify, fixture.Root));
                    })),
                    Test("UNADMITTED_CONTENT_REJECTED", () => WithFixture(fixture =>
                    {
                        fixture.Add("GameData/Unreviewed.json", "{}");
                        fixture.WriteInventory();
                        MustReject(() => Verify(verify, fixture.Root));
                    })),
                    Test("CORRUPT_CONTENT_REJECTED", () => WithFixture(fixture =>
                    {
                        File.AppendAllText(fixture.PathOf("GameData/NpcTemplates.json"), " ");
                        MustReject(() => Verify(verify, fixture.Root));
                    })),
                    Test("DUPLICATE_INVENTORY_PATH_REJECTED", () => WithFixture(fixture =>
                    {
                        fixture.WriteInventory(true);
                        MustReject(() => Verify(verify, fixture.Root));
                    }))
                };

                int failed = 0;
                foreach (var test in tests)
                {
                    try
                    {
                        test.Value();
                        Console.WriteLine(test.Key + "=PASS");
                    }
                    catch (Exception exception)
                    {
                        failed++;
                        Console.Error.WriteLine(test.Key + "=FAIL " + exception.Message);
                    }
                }

                Console.WriteLine("OFFLINE_TESTS=" + (failed == 0 ? "PASS" : "FAIL") + " total=" + tests.Length + " failed=" + failed);
                return failed == 0 ? 0 : 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("OFFLINE_TESTS=FAIL " + exception.Message);
                return 1;
            }
        }

        private static void FourEngineLaunch()
        {
            var launched = new Dictionary<string, int[]>(StringComparer.Ordinal);
            PackageTopology.StartEngines((name, ports) => launched.Add(name, ports));
            Assert(launched.Count == 4 && EngineNames.All(launched.ContainsKey), "The launch plan must invoke all four engines exactly once.");
            Assert(launched["WorldEngine"].SequenceEqual(new[] { 7600, 7601 }), "World readiness must cover RPC and diagnostics listeners.");
            Assert(launched["ChatEngine"].SequenceEqual(new[] { 7012 })
                && launched["LoginEngine"].SequenceEqual(new[] { 7500 })
                && launched["ZoneEngine_New"].SequenceEqual(new[] { 7501 }), "Dependent engine readiness ports changed.");
        }

        private static void WorldEngineFirst()
        {
            var launched = new List<string>();
            PackageTopology.StartEngines((name, ports) => launched.Add(name));
            Assert(launched.First() == "WorldEngine", "World must start before every dependent engine.");
        }

        private static void WorldFailureBlocksDependents()
        {
            var launched = new List<string>();
            try
            {
                PackageTopology.StartEngines((name, ports) =>
                {
                    launched.Add(name);
                    throw new InvalidOperationException("Synthetic World startup failure.");
                });
            }
            catch (InvalidOperationException)
            {
                Assert(launched.SequenceEqual(new[] { "WorldEngine" }), "Dependents ran after World startup failed.");
                return;
            }

            throw new InvalidOperationException("A failed World startup was swallowed.");
        }

        private static void FourEngineStop()
        {
            string[] stopped = PackageTopology.StopOrder(EngineNames);
            Assert(stopped.Length == 4 && EngineNames.All(name => stopped.Count(value => value == name) == 1),
                "The stop plan must stop all four engines exactly once.");
        }

        private static void WorldEngineLast()
        {
            string[] stopped = PackageTopology.StopOrder(new[] { "WorldEngine", "MySQL", "ChatEngine", "AccountBroker", "ZoneEngine_New", "LoginEngine" });
            int world = Array.IndexOf(stopped, "WorldEngine");
            Assert(EngineNames.Where(name => name != "WorldEngine").All(name => Array.IndexOf(stopped, name) < world),
                "World stopped before a dependent engine.");
            Assert(Array.IndexOf(stopped, "AccountBroker") < world && Array.IndexOf(stopped, "MySQL") > world,
                "Owned account service and database stop order is unsafe.");
        }

        private static void StopRejectsUnknownComponents()
        {
            MustReject(() => PackageTopology.StopOrder(new[] { "WorldEngine", "UnownedProcess" }));
            MustReject(() => PackageTopology.StopOrder(new[] { "WorldEngine", "WorldEngine" }));
        }

        private static void DailyPhpLoopbackTopology()
        {
            var engines = new List<string>();
            PackageTopology.StartEngines((name, ports) => engines.Add(name));
            Assert(engines.Count == 4 && !engines.Contains(PackageTopology.DailyPhp) && !PackageTopology.IsEngine(PackageTopology.DailyPhp),
                "The Daily PHP service must remain separate from the four game engines.");
            Assert(PackageTopology.DailyBindAddress == "127.0.0.1" && PackageTopology.DailyHost == "uwg.daily.icc-rk"
                && PackageTopology.DailyPort == 80, "Daily PHP no longer matches the native loopback route.");
            Assert(PackageTopology.Ports(PackageTopology.DailyPhp, 3308).SequenceEqual(new[] { 80 })
                && PackageTopology.FixedPorts.Contains(80), "Daily PHP port 80 is missing from owned-listener or fixed-port checks.");
        }

        private static void DailyPhpStopsBeforeCoreEngines()
        {
            string[] stopped = PackageTopology.StopOrder(new[]
            {
                "WorldEngine", "MySQL", "ChatEngine", PackageTopology.DailyPhp, "AccountBroker", "ZoneEngine_New", "LoginEngine"
            });
            int daily = Array.IndexOf(stopped, PackageTopology.DailyPhp);
            Assert(daily >= 0 && EngineNames.All(name => Array.IndexOf(stopped, name) > daily),
                "The Daily claim service must stop before the game engines.");
            string[] core = stopped.Where(PackageTopology.IsEngine).ToArray();
            Assert(core.Length == 4 && core.Last() == "WorldEngine" && stopped.Last() == "MySQL",
                "Adding Daily PHP must preserve World as the last game engine and MySQL as the last service to stop.");
        }

        private static void DailyPhpPackageFilesRequired(MethodInfo verify)
        {
            foreach (string path in DailyPackageFiles)
            {
                WithFixture(fixture =>
                {
                    fixture.Remove(path);
                    fixture.WriteInventory();
                    try { Verify(verify, fixture.Root); }
                    catch (InvalidDataException) { return; }
                    throw new InvalidOperationException("Inventory accepted a package missing Daily PHP content: " + path);
                });
            }
        }

        private static void WorldRegistrationReadiness()
        {
            Assert(PackageTopology.IsWorldReady(ReadyWorld, true), "Fresh registered World was rejected.");
            Assert(!PackageTopology.IsWorldReady(ReadyWorld.Replace("\"recovering\":false", "\"recovering\":true"), true), "Recovering World was treated as ready.");
            Assert(!PackageTopology.IsWorldReady(ReadyWorld.Replace("\"heartbeatAgeMs\":100", "\"heartbeatAgeMs\":15001"), true), "Stale registration was treated as ready.");
            Assert(!PackageTopology.IsWorldReady(ReadyWorld.Replace("127.0.0.1:7501", "127.0.0.1:9999"), true), "Wrong Zone endpoint was treated as ready.");
            Assert(!PackageTopology.IsWorldReady("{\"world\":{\"epoch\":1,\"recovering\":false,\"instances\":[]}}", true), "Missing dependent registrations were accepted.");
            Assert(PackageTopology.IsWorldReady("{\"world\":{\"epoch\":1,\"recovering\":true,\"instances\":[]}}", false), "Early World listener check incorrectly requires dependents.");
        }

        private static void MigrationDiscovery(string dotnet, string databaseSetup)
        {
            var start = new ProcessStartInfo(dotnet, Quote(databaseSetup) + " --verify-migrations")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(databaseSetup)
            };
            start.EnvironmentVariables.Remove("AO_REBIRTH_MYSQL_CONNECTION");
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                // Resource-only discovery should finish immediately; allow 30 seconds on slow machines.
                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    process.WaitForExit();
                    throw new TimeoutException("Read-only migration discovery exceeded its 30-second timeout.");
                }

                string stdout = output.GetAwaiter().GetResult();
                string stderr = errors.GetAwaiter().GetResult();
                Assert(process.ExitCode == 0, "Migration discovery failed without a database connection: " + stderr.Trim());
                string[] migrations = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("MIGRATION=", StringComparison.Ordinal)).Select(line => line.Substring(10)).ToArray();
                Assert(migrations.Length >= 14 && migrations.Distinct(StringComparer.Ordinal).Count() == migrations.Length,
                    "Current migration discovery was incomplete or duplicated.");
                foreach (string migration in new[] { "20261025_character_pet_names.sql", "20261029_daily_login_claims.sql", "20261030_daily_login_character_scope.sql", "20261102_account_sanctions.sql" })
                    Assert(migrations.Contains(migration, StringComparer.Ordinal), "Missing required current migration: " + migration);

                Assert(stdout.Contains("PACKAGED_MIGRATIONS_VERIFIED count=" + migrations.Length.ToString(CultureInfo.InvariantCulture)),
                    "Migration resources were listed without a successful verification result.");
            }
        }

        private static void LegacyCredentialRoundTrip(Type stateType)
        {
            WithFixture(fixture =>
            {
                string admin = new string('A', 40);
                string runtime = new string('R', 40);
                string expectedCredentials = admin + "\n" + runtime;
                byte[] legacyClear = Encoding.UTF8.GetBytes(expectedCredentials);
                string secrets = fixture.PathOf("data/launcher.secrets");
                Directory.CreateDirectory(Path.GetDirectoryName(secrets));
                try { File.WriteAllBytes(secrets, ProtectedData.Protect(legacyClear, null, DataProtectionScope.CurrentUser)); }
                finally { Array.Clear(legacyClear, 0, legacyClear.Length); }

                var original = new XElement("Launcher",
                    new XAttribute("clientFolder", "C:\\Offline\\Client"),
                    new XAttribute("ownsDataDirectory", true),
                    new XAttribute("setupComplete", true),
                    new XAttribute("databasePort", 3309));
                original.Save(fixture.PathOf("data/launcher.xml"));
                MethodInfo load = stateType.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo save = stateType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(load != null && save != null, "The actual package state persistence methods are absent.");
                object state = load.Invoke(null, new object[] { fixture.Root });
                Assert((string)StateField(stateType, state, "AdminPassword") == admin
                    && (string)StateField(stateType, state, "RuntimePassword") == runtime,
                    "Existing legacy credentials were replaced during load.");
                Assert(StateField(stateType, state, "WorldToken") == null, "Legacy credentials unexpectedly contained a World token.");
                save.Invoke(state, new object[] { fixture.Root });

                byte[] savedClear = ProtectedData.Unprotect(File.ReadAllBytes(secrets), null, DataProtectionScope.CurrentUser);
                try
                {
                    string saved = Encoding.UTF8.GetString(savedClear);
                    Assert(saved == expectedCredentials && saved.Split('\n').Length == 2,
                        "Saving current state changed the legacy two-field secret format.");
                }
                finally { Array.Clear(savedClear, 0, savedClear.Length); }

                byte[] worldClear = ProtectedData.Unprotect(File.ReadAllBytes(fixture.PathOf("data/world.secrets")), null, DataProtectionScope.CurrentUser);
                string world;
                try
                {
                    world = Encoding.UTF8.GetString(worldClear);
                    Assert(world.IndexOf('\n') < 0 && world.IndexOf('\r') < 0 && Convert.FromBase64String(world).Length == 32,
                        "The separate World secret is not a valid 32-byte protected token.");
                }
                finally { Array.Clear(worldClear, 0, worldClear.Length); }

                XElement savedSettings = XElement.Load(fixture.PathOf("data/launcher.xml"));
                Assert(original.Attributes().All(attribute => (string)savedSettings.Attribute(attribute.Name) == attribute.Value),
                    "Existing installation settings changed during the World-token upgrade.");
                object reloaded = load.Invoke(null, new object[] { fixture.Root });
                Assert((string)StateField(stateType, reloaded, "AdminPassword") == admin
                    && (string)StateField(stateType, reloaded, "RuntimePassword") == runtime
                    && (string)StateField(stateType, reloaded, "WorldToken") == world,
                    "Saved legacy credentials and the separate World token did not round-trip.");
            });
        }

        private static object StateField(Type type, object state, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("Actual package state field is absent: " + name);
            return field.GetValue(state);
        }

        private static void StartupWaiterPreservesGuiExit()
        {
            WithFixture(fixture =>
            {
                string executable = fixture.PathOf("AORebirth.exe");
                using (var compiler = new CSharpCodeProvider())
                {
                    var parameters = new CompilerParameters
                    {
                        GenerateExecutable = true,
                        OutputAssembly = executable,
                        CompilerOptions = "/target:winexe /platform:x64"
                    };
                    const string source = "internal static class Fixture { public static int Main(string[] args) { return args.Length == 1 && args[0] == \"--verify-startup\" ? 7 : 19; } }";
                    CompilerResults compiled = compiler.CompileAssemblyFromSource(parameters, source);
                    Assert(!compiled.Errors.HasErrors, "The harmless GUI fixture did not compile: "
                        + string.Join(", ", compiled.Errors.Cast<CompilerError>().Where(error => !error.IsWarning).Select(error => error.ErrorNumber)));
                }

                using (var reader = new BinaryReader(File.OpenRead(executable)))
                {
                    reader.BaseStream.Seek(0x3c, SeekOrigin.Begin);
                    reader.BaseStream.Seek(reader.ReadInt32(), SeekOrigin.Begin);
                    Assert(reader.ReadUInt32() == 0x00004550, "The fixture is not a Windows executable.");
                    reader.BaseStream.Seek(20 + 68, SeekOrigin.Current);
                    Assert(reader.ReadUInt16() == 2, "The fixture must use the Windows GUI subsystem.");
                }
                fixture.WriteInventory();

                string repository = Directory.GetCurrentDirectory();
                string wrapper = Path.Combine(repository, "Tools", "WindowsLocalPackage", "verify-startup.cmd");
                Assert(File.Exists(wrapper), "Run the offline tests from the owning repository wrapper.");
                string command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                var start = new ProcessStartInfo(command, "/d /s /c \"" + Quote(wrapper) + " -PackageRoot " + Quote(fixture.Root) + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = repository
                };
                using (var process = Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    // The fixture returns immediately. Allow 30 seconds for the CMD and PowerShell startup.
                    if (!process.WaitForExit(30000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        throw new TimeoutException("The startup waiter did not return the GUI fixture's exit code within 30 seconds.");
                    }

                    string stdout = output.GetAwaiter().GetResult();
                    string stderr = errors.GetAwaiter().GetResult();
                    Assert(process.ExitCode == 7, "The startup waiter did not propagate the GUI fixture's exit 7: " + stderr.Trim());
                    const string prefix = "PACKAGE_STARTUP_LOGS=";
                    string logLine = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Single(line => line.StartsWith(prefix, StringComparison.Ordinal));
                    string logRoot = Path.GetFullPath(logLine.Substring(prefix.Length));
                    string expectedRoot = Path.Combine(repository, "build-verify", "windows-local-package-startup") + Path.DirectorySeparatorChar;
                    Assert(logRoot.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase), "The startup waiter reported an unexpected log directory.");
                    var receipt = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(logRoot, "result.json")));
                    var verification = (Dictionary<string, object>)receipt["verification"];
                    Assert(Convert.ToInt32(receipt["exitCode"], CultureInfo.InvariantCulture) == 7
                        && Convert.ToInt32(verification["actualExitCode"], CultureInfo.InvariantCulture) == 7
                        && (string)verification["argument"] == "--verify-startup"
                        && !(bool)verification["timedOut"] && receipt["emergencyCleanup"] == null
                        && (string)receipt["result"] == "FAIL", "The startup waiter receipt did not record the actual fixture failure.");
                }
            });
        }

        private static void Verify(MethodInfo method, string root)
        {
            try { method.Invoke(null, new object[] { root }); }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void WithFixture(Action<PackageFixture> action)
        {
            using (var fixture = new PackageFixture())
                action(fixture);
        }

        private static void MustReject(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Invalid package or process topology was accepted.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static KeyValuePair<string, Action> Test(string name, Action action) { return new KeyValuePair<string, Action>(name, action); }

        private static Dictionary<string, string> ReadOptions(string[] args)
        {
            if (args.Length != 6)
                throw new ArgumentException("Expected --launcher <exe> --database-setup <dll> --dotnet <exe>.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < args.Length; index += 2)
                result.Add(args[index], Path.GetFullPath(args[index + 1]));
            return result;
        }

        private static string RequiredFile(Dictionary<string, string> options, string name)
        {
            string value;
            if (!options.TryGetValue(name, out value) || !File.Exists(value))
                throw new FileNotFoundException("Required test input is missing: " + name);
            return value;
        }

        private static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new ArgumentException("Unsafe executable argument.");
            return "\"" + value + "\"";
        }

        private sealed class PackageFixture : IDisposable
        {
            private readonly HashSet<string> files = new HashSet<string>(StringComparer.Ordinal);
            internal string Root { get; private set; }

            internal PackageFixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "AORebirthPackageOfflineTests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
                foreach (string path in new[]
                {
                    "AORebirth.exe", "runtime/dotnet.exe", "mysql/bin/mysqld.exe", "mysql/bin/mysql.exe",
                    "server/LoginEngine/LoginEngine.dll", "server/ChatEngine/ChatEngine.dll", "server/WorldEngine/WorldEngine.dll",
                    "server/ZoneEngine_New/ZoneEngine_New.dll", "tools/DatabaseSetup/DatabaseSetup.dll",
                    "tools/DatabaseMigrationTool/DatabaseMigrationTool.dll", "tools/AccountBroker/AORebirth.AccountBroker.Service.exe",
                    "prerequisites/vc_redist.x64.exe"
                })
                    Add(path, "Synthetic offline fixture: " + path);

                foreach (string path in DailyPackageFiles)
                    Add(path, "Synthetic offline Daily PHP fixture: " + path);

                Add("config/Config.xml", "<Config><SQLType>MySql</SQLType><ChatIP>127.0.0.1</ChatIP><ZoneIP>127.0.0.1</ZoneIP><ChatPort>7012</ChatPort><ZonePort>7501</ZonePort><LoginPort>7500</LoginPort><CommPort>6996</CommPort></Config>");
                Add("config/client.xml", "<Client downloadUrl=\"https://ao-rebirth.com/offline-fixture.zip\" executable=\"Anarchy.exe\" executableSha256=\"" + new string('a', 64) + "\" archiveLength=\"1\" />");
                Add("GameData/NpcTemplates.json", "{}");
                Add("GameData/Playfields/1/Spawns.json", "{}");
                var admitted = files.Where(path => path.StartsWith("GameData/", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new { blob = "offline-fixture", path = path.Substring(9), sha256 = Hash(PathOf(path)), size = new FileInfo(PathOf(path)).Length }).ToArray();
                Add("runtime-content-manifest.json", Json.Serialize(admitted));
                WriteInventory();
            }

            internal string PathOf(string path) { return Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)); }

            internal void Add(string path, string content)
            {
                string file = PathOf(path);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, content, new UTF8Encoding(false));
                files.Add(path);
            }

            internal void Remove(string path)
            {
                files.Remove(path);
                File.Delete(PathOf(path));
            }

            internal void WriteInventory(bool duplicate = false, int schemaVersion = 2)
            {
                string[] payload = files.Where(path => path != "inventory.csv" && path != "package-manifest.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
                string inventory = "\"path\",\"length\",\"sha256\"\r\n" + string.Join("\r\n", payload.Select(path => "\"" + path + "\",\"" + new FileInfo(PathOf(path)).Length + "\",\"" + Hash(PathOf(path)) + "\"")) + "\r\n";
                Add("inventory.csv", inventory);
                var entries = files.Where(path => path != "package-manifest.json").OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new { path, length = new FileInfo(PathOf(path)).Length, sha256 = Hash(PathOf(path)) }).ToList();
                if (duplicate)
                    entries.Add(entries[0]);
                var manifest = new
                {
                    schemaVersion,
                    architecture = "win-x64",
                    publicMasterSha = new string('a', 40),
                    windowsGameDataManifestSha256 = Hash(PathOf("runtime-content-manifest.json")),
                    files = entries
                };
                File.WriteAllText(PathOf("package-manifest.json"), Json.Serialize(manifest), new UTF8Encoding(false));
            }

            public void Dispose()
            {
                string expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string resolved = Path.GetFullPath(Root);
                if (!resolved.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(resolved).StartsWith("AORebirthPackageOfflineTests-", StringComparison.Ordinal)
                    || (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
                Directory.Delete(resolved, true);
            }

            private static string Hash(string path)
            {
                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(path))
                    return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
