using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

// No database driver, server reference, network listener or migration execution.
// Evaluate the shared SDK project on both RIDs so new sources cannot be silently
// absent from one platform, then compare exact packaged asset identities/hashes.
internal static class Program
{
    private const string Project = "AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj";
    private static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(Value(args, "--repository-root"));
            CheckDefaults(root);
            CheckSources(root);
            if (args.Contains("--publish"))
            {
                string publish = Path.GetFullPath(Value(args, "--publish"));
                CheckPackage(root, publish);
                if (args.Contains("--source-sha")) CheckOfflineStartup(publish, Value(args, "--source-sha"), Value(args, "--build-platform"));
            }
            if (args.Contains("--self-test")) SelfTest();
            Console.WriteLine("BACKEND_INTEGRATION_GUARD=PASS");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("BACKEND_INTEGRATION_GUARD=FAIL " + e.Message); return 1; }
    }

    private static void CheckDefaults(string root)
    {
        string shell = File.ReadAllText(Path.Combine(root, "LinuxBuild/publish-zoneengine.sh"));
        string cmd = File.ReadAllText(Path.Combine(root, "LinuxBuild/publish-zoneengine.cmd"));
        string unit = File.ReadAllText(Path.Combine(root, "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service"));
        string rollback = File.ReadAllText(Path.Combine(root, "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine-legacy.service"));
        Require(shell.Contains("engine=\"${3:-new}\"") && shell.Contains(Project), "Linux default publish must select the shared New engine project.");
        Require(cmd.Contains("if \"%ENGINE%\"==\"\" set ENGINE=new") && cmd.Contains(Project.Replace('/', '\\')), "Windows-hosted Linux publish default must select New.");
        Require(unit.Contains("ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine_New --headless --shutdown-file"), "Linux default service must launch ZoneEngine_New.");
        Require(unit.Contains("Type=notify") && unit.Contains("ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine_New --validate-database"), "Linux service must require database preflight and readiness.");
        Require(!unit.Contains("--recover-stale-online") && !unit.Contains("--migrate"), "Default runtime service must not mutate schema or perform startup recovery SQL.");
        Require(rollback.Contains("/ZoneEngine --headless") && rollback.Contains("Conflicts=ao-rebirth-zoneengine.service"), "Legacy rollback must be explicit and mutually exclusive.");
        string windows = File.ReadAllText(Path.Combine(root, "start-engines.ps1"));
        Require(windows.Contains("LegacyZoneEngine") && windows.Contains("ZoneEngine_New"), "Windows launcher lacks explicit legacy rollback selection.");
        Console.WriteLine("DEFAULT_ENGINE_PARITY=PASS");
    }

    private static void CheckSources(string root)
    {
        string[] windows = Evaluate(root, "win-x64", "Compile");
        string[] linux = Evaluate(root, "linux-x64", "Compile");
        EqualSet(windows, linux, "Windows/Linux evaluated C# source");
        string runtime = Path.GetFullPath(Path.Combine(root, "AORebirth/Server/ZoneEngine_New"));
        string[] physical = Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(runtime, p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
            .Select(Path.GetFullPath).ToArray();
        EqualSet(physical, windows.Where(p => p.StartsWith(runtime + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray(), "New runtime source omission");
        foreach (string relative in new[]
        {
            "AORebirth/Libraries/Source/AORebirth.Database.Schema/AORebirth.Database.Schema.csproj",
            "AORebirth/Libraries/Source/AORebirth.World.Package/AORebirth.World.Package.csproj"
        })
        {
            Require(File.Exists(Path.Combine(root, relative)), "Required shared runtime library is missing.");
            string[] winLibrary = Evaluate(root, "win-x64", "Compile", relative);
            string[] linuxLibrary = Evaluate(root, "linux-x64", "Compile", relative);
            EqualSet(winLibrary, linuxLibrary, "Shared runtime library platform parity");
            string libraryRoot = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, relative)))!;
            string[] librarySources = Directory.GetFiles(libraryRoot, "*.cs", SearchOption.AllDirectories)
                .Where(p => !Path.GetRelativePath(libraryRoot, p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
                .Select(Path.GetFullPath).ToArray();
            EqualSet(librarySources, winLibrary.Where(p => p.StartsWith(libraryRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray(),
                "Shared runtime library source omission");
        }
        Console.WriteLine("LINUX_SOURCE_INVENTORY_GUARD=PASS");
    }

    private static string[] Evaluate(string root, string rid, string item, string project = Project)
    {
        string json = Run(root, "dotnet", "msbuild", project, "-nologo", "-p:RuntimeIdentifier=" + rid, "-getItem:" + item);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Items").GetProperty(item).EnumerateArray()
            .Select(i => Path.GetFullPath(i.GetProperty("FullPath").GetString()!))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static void CheckPackage(string root, string publish)
    {
        foreach (string name in new[] { "ZoneEngine_New.dll", "ZoneEngine_New.deps.json", "ZoneEngine_New.runtimeconfig.json", "Config.xml" })
            Require(File.Exists(Path.Combine(publish, name)), "Missing New engine package asset: " + name);
        Require(!File.Exists(Path.Combine(publish, "ZoneEngine.dll")), "Default package accidentally contains legacy ZoneEngine.");
        byte[] apphost = File.ReadAllBytes(Path.Combine(publish, "ZoneEngine_New"));
        Require(apphost.Length > 20 && apphost[0] == 0x7f && apphost[1] == (byte)'E' && apphost[2] == (byte)'L' && apphost[3] == (byte)'F', "New engine package lacks a Linux ELF apphost.");
        Require(apphost.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes("ZoneEngine_New.dll")) >= 0, "Linux apphost is not bound to ZoneEngine_New.dll.");
        string database = Path.Combine(root, "AORebirth/Libraries/Source/AORebirth.Database");
        var expected = Directory.GetFiles(Path.Combine(database, "SqlTables"), "*.sql").Select(p => (Source: p, Relative: "SqlTables/" + Path.GetFileName(p)))
            .Concat(Directory.GetFiles(Path.Combine(database, "Migrations"), "*.sql").Where(p => !Path.GetFileName(p).Contains("account", StringComparison.OrdinalIgnoreCase)).Select(p => (Source: p, Relative: "Migrations/" + Path.GetFileName(p)))).ToArray();
        string[] actual = Directory.GetFiles(publish, "*.sql", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(publish, p).Replace('\\', '/')).ToArray();
        EqualSet(expected.Select(p => p.Relative).ToArray(), actual, "SQL/migration package identity");
        foreach (var asset in expected)
            Require(SHA256.HashData(File.ReadAllBytes(asset.Source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(publish, asset.Relative)))), "SQL package hash mismatch: " + asset.Relative);
        Console.WriteLine("SQL_PACKAGE_PARITY_GUARD=PASS");
    }

    private static void SelfTest()
    {
        int rejected = 0;
        foreach (string[] actual in new[] { new[] { "a" }, new[] { "a", "b", "unexpected" }, new[] { "a", "B" }, new[] { "a", "a", "b" } })
            try { EqualSet(new[] { "a", "b" }, actual, "fixture"); } catch (InvalidOperationException) { rejected++; }
        Require(rejected == 4, "Set guard did not reject omission, addition, case drift and duplicate fixtures.");
        Console.WriteLine("PACKAGE_PARITY_NEGATIVE_FIXTURES=PASS (4/4)");
        string temporary = Directory.CreateTempSubdirectory("aorebirth-backend-inventory-").FullName;
        try
        {
            string runtime = Path.GetFullPath(Path.Combine(temporary, Path.GetDirectoryName(Project)!));
            Directory.CreateDirectory(runtime);
            File.WriteAllText(Path.Combine(runtime, "Included.cs"), "internal class Included {}");
            File.WriteAllText(Path.Combine(runtime, "Omitted.cs"), "internal class Omitted {}");
            string fixtureProject = Path.Combine(temporary, Project);
            File.WriteAllText(fixtureProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Remove=\"Omitted.cs\" /></ItemGroup></Project>");
            RequireRejected(() => CheckSources(temporary), "physical C# omitted from both platforms");
            File.WriteAllText(fixtureProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup Condition=\"'$(RuntimeIdentifier)' == 'linux-x64'\"><Compile Remove=\"Omitted.cs\" /></ItemGroup></Project>");
            RequireRejected(() => CheckSources(temporary), "Linux-only C# omission");
            Console.WriteLine("SOURCE_OMISSION_NEGATIVE_FIXTURES=PASS (2/2)");
        }
        finally { Directory.Delete(temporary, true); }
    }

    private static void RequireRejected(Action action, string label)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Guard accepted " + label + ".");
    }

    private static void CheckOfflineStartup(string publish, string sha, string platform)
    {
        string corpus = Path.Combine(publish, "Content/Official/PlayfieldPlacements");
        RunEngine(publish, null, true, "--validate-official-placements", "--source-sha", sha, "--build-platform", platform,
            "--placement-manifest-output", Path.Combine(corpus, "official-placement-build-manifest.json"),
            "--placement-provenance-output", Path.Combine(corpus, "PLACEMENT_PROVENANCE.env"));
        Require(File.Exists(Path.Combine(corpus, "PLACEMENT_PROVENANCE.env")), "Placement validator did not produce provenance.");
        RunEngine(publish, null, true, "--validate-startup");
        RunEngine(publish, "NotPublic", false, "--validate-startup");
        Console.WriteLine("LINUX_NEWENGINE_OFFLINE_STARTUP=PASS");
    }

    private static void RunEngine(string publish, string? bind, bool success, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = publish, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(typeof(Program).Assembly.Location, ".runtimeconfig.json"), "--depsfile", Path.Combine(publish, "ZoneEngine_New.deps.json"), Path.Combine(publish, "ZoneEngine_New.dll") }.Concat(args)) info.ArgumentList.Add(arg);
        info.Environment["AO_REBIRTH_CONFIG_PATH"] = Path.Combine(publish, "Config.xml");
        info.Environment["AO_REBIRTH_MYSQL_CONNECTION"] = "Server=127.0.0.1;Port=33067;Database=aorebirth_offline;Uid=offline;Pwd=REPLACE_WITH_OFFLINE_TEST_VALUE;SslMode=None";
        info.Environment["AO_REBIRTH_REQUIRED_SQL_TYPE"] = "MySql";
        info.Environment["AO_REBIRTH_EXPECTED_DATABASE"] = "aorebirth_offline";
        info.Environment["AO_REBIRTH_BIND_MODE"] = bind ?? "Loopback";
        info.Environment["AO_REBIRTH_CHAT_LISTEN_IP"] = "127.0.0.1";
        info.Environment.Remove("NOTIFY_SOCKET");
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start packaged offline validator.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("Packaged offline validator timed out."); }
        string messages = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Require(!messages.Contains("REPLACE_WITH_OFFLINE_TEST_VALUE"), "Offline validator disclosed connection secret.");
        Require((process.ExitCode == 0) == success, "Packaged offline validator returned unexpected status: " + messages);
    }

    private static void EqualSet(string[] expected, string[] actual, string label)
    {
        Require(actual.Length == actual.Distinct(StringComparer.Ordinal).Count(), label + " contains duplicate identities.");
        string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        string[] extra = actual.Except(expected, StringComparer.Ordinal).ToArray();
        Require(missing.Length == 0 && extra.Length == 0, label + " mismatch. Missing (" + missing.Length + "): " + string.Join(", ", missing.Take(5)) + "; unexpected (" + extra.Length + "): " + string.Join(", ", extra.Take(5)));
    }

    private static string Run(string root, string file, params string[] args)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start source evaluator.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("Source evaluator timed out."); }
        Require(process.ExitCode == 0, "Source evaluator failed: " + error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    private static string Value(string[] args, string name) => Array.IndexOf(args, name) is int i && i >= 0 && i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing " + name);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
