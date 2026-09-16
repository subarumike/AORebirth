using System.Diagnostics;
using System.Text.Json;

// Public Windows acceptance owns source completeness. Private deployment tooling
// independently checks target-platform compilation, packaging and service defaults.
internal static class Program
{
    private const string Project = "AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj";
    private static int Main(string[] args)
    {
        try
        {
            int index = Array.IndexOf(args, "--repository-root");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing repository root");
            string root = Path.GetFullPath(args[index + 1]);
            string launcher = File.ReadAllText(Path.Combine(root, "start-engines.ps1"));
            Require(launcher.Contains("ZoneEngine_New") && !launcher.Contains("File = \"ZoneEngine.exe\""), "Windows must select only ZoneEngine_New");
            Require(!File.Exists(Path.Combine(root, "AORebirth/Server/ZoneEngine/ZoneEngine.csproj")), "Retired Legacy engine project must not return");
            foreach (string project in new[] { Project,
                "AORebirth/Libraries/Source/AORebirth.Database.Schema/AORebirth.Database.Schema.csproj",
                "AORebirth/Libraries/Source/AORebirth.World.Package/AORebirth.World.Package.csproj" })
                CheckSources(root, project);
            if (args.Contains("--self-test")) SelfTest();
            Console.WriteLine("WINDOWS_RUNTIME_SOURCE_GUARD=PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("WINDOWS_RUNTIME_SOURCE_GUARD=FAIL " + error.Message); return 1; }
    }

    private static void CheckSources(string root, string project)
    {
        string source = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, project)))!;
        string[] physical = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(source, p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
            .Select(Path.GetFullPath).ToArray();
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in new[] { "msbuild", project, "-nologo", "-p:RuntimeIdentifier=win-x64", "-getItem:Compile" }) info.ArgumentList.Add(arg);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start source evaluation");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("Source evaluation timed out"); }
        Require(process.ExitCode == 0, "Source evaluation failed: " + error.GetAwaiter().GetResult());
        using JsonDocument document = JsonDocument.Parse(output.GetAwaiter().GetResult());
        string[] evaluated = document.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
            .Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!))
            .Where(p => p.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
        EqualSet(physical, evaluated);
    }

    private static void SelfTest()
    {
        int rejected = 0;
        foreach (string[] actual in new[] { new[] { "a" }, new[] { "a", "b", "extra" }, new[] { "a", "B" }, new[] { "a", "a", "b" } })
            try { EqualSet(["a", "b"], actual); } catch (InvalidOperationException) { rejected++; }
        Require(rejected == 4, "Inventory negative fixtures did not fail closed");
        string root = Directory.CreateTempSubdirectory("aorebirth-source-guard-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "Included.cs"), "internal class Included {}");
            File.WriteAllText(Path.Combine(root, "Omitted.cs"), "internal class Omitted {}");
            File.WriteAllText(Path.Combine(root, "Fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include=\"Included.cs\" /></ItemGroup></Project>");
            bool omissionRejected = false;
            try { CheckSources(root, "Fixture.csproj"); } catch (InvalidOperationException) { omissionRejected = true; }
            Require(omissionRejected, "Actual source omission was accepted");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("SOURCE_INVENTORY_NEGATIVE_FIXTURES=PASS");
    }

    private static void EqualSet(string[] expected, string[] actual)
    {
        Require(actual.Length == actual.Distinct(StringComparer.Ordinal).Count(), "Duplicate source identities");
        Require(!expected.Except(actual, StringComparer.Ordinal).Any() && !actual.Except(expected, StringComparer.Ordinal).Any(), "Source inventory mismatch");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
