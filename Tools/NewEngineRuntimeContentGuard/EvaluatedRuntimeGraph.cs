using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

internal static class EvaluatedRuntimeGraph
{
    internal static readonly string[] EntryProjects =
    [
        "AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj",
        "AORebirth/Server/WorldEngine/WorldEngine.csproj",
        "WindowsBuildNet10/Projects/ChatEngine.WinNet10.csproj",
        "WindowsBuildNet10/Projects/LoginEngine.WinNet10.csproj"
    ];

    private const int EvaluationTimeoutMilliseconds = 60_000;
    private const string ItemKinds = "Compile,ProjectReference,Content,None,EmbeddedResource,AdditionalFiles,Protobuf,Analyzer";
    private const string Properties = "Configuration,Platform,TargetFramework,TargetFrameworks,RuntimeIdentifier,RuntimeIdentifiers,DefineConstants,LangVersion,NETCoreSdkVersion,MSBuildToolsPath";
    private static readonly HashSet<string> RuntimeItems = new(ItemKinds.Split(','), StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SdkSymbolTargets = new(
        ["AddImplicitDefineConstants", "GenerateTargetFrameworkDefineConstants", "GenerateTargetPlatformDefineConstants",
            "GenerateNETCompatibleDefineConstants", "GeneratePlatformCompatibleDefineConstants", "_DisableDiagnosticTracing"],
        StringComparer.OrdinalIgnoreCase);

    internal sealed record Input(string Kind, string Path);
    internal sealed record Project(string Path, string Configuration, string Platform, string Framework,
        string LanguageVersion, string SdkVersion, string[] Defines, string[] ProjectReferences, Input[] Inputs,
        string[] SdkAnalyzers, string[] RepositoryBuildInputs);

    internal static Project[] Read(string root, IEnumerable<string> roots, Action<string> unresolved,
        IReadOnlySet<string>? tracked = null)
    {
        var result = new List<Project>();
        var auditedDocuments = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string configuration in new[] { "Debug", "Release" })
        {
            var pending = new Queue<string>(roots);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.TryDequeue(out string? path))
            {
                if (!visited.Add(path)) continue;
                try
                {
                    Project project = ReadProject(root, path, configuration, unresolved, tracked, auditedDocuments);
                    result.Add(project);
                    foreach (string reference in project.ProjectReferences)
                        pending.Enqueue(reference);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException
                    or KeyNotFoundException or JsonException or XmlException or TimeoutException or Win32Exception)
                {
                    unresolved($"MSBuild evaluation failed for {path} ({configuration}/AnyCPU): {exception.Message}");
                }
            }
        }
        return result.ToArray();
    }

    private static Project ReadProject(string root, string path, string configuration, Action<string> unresolved,
        IReadOnlySet<string>? tracked, Dictionary<string, bool> auditedDocuments)
    {
        string fullPath = Path.Combine(root, path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Project input is unavailable.", path);
        string[] buildInputs = ReadBuildInputs(root, fullPath, configuration, tracked);
        foreach (string input in buildInputs)
        {
            if (!auditedDocuments.TryGetValue(input, out bool safeSdkSymbolEvaluation))
            {
                safeSdkSymbolEvaluation = AuditBuildInput(root, input, unresolved);
                auditedDocuments.Add(input, safeSdkSymbolEvaluation);
            }
            if (!safeSdkSymbolEvaluation)
                throw new InvalidDataException($"Repository build input modifies SDK symbol evaluation: {input}");
        }
        // This SDK target supplies framework symbols. It does not execute PrepareForBuild or CoreCompile.
        using JsonDocument evaluation = JsonDocument.Parse(RunMsBuild(root, fullPath, configuration,
            "-target:AddImplicitDefineConstants", "-getProperty:" + Properties, "-getItem:" + ItemKinds));
        JsonElement properties = evaluation.RootElement.GetProperty("Properties");
        string Property(string name) => properties.GetProperty(name).GetString() ?? throw new InvalidDataException(name);
        string framework = Property("TargetFramework"), platform = Property("Platform");
        if (Property("Configuration") != configuration || platform != "AnyCPU" || framework.Length == 0
            || Property("TargetFrameworks").Length != 0 || Property("RuntimeIdentifier").Length != 0
            || Property("RuntimeIdentifiers").Length != 0)
            unresolved($"Unhandled build configuration dimensions for {path} ({configuration}/{platform}/{framework}).");

        var inputs = new HashSet<Input>();
        var references = new SortedSet<string>(StringComparer.Ordinal);
        var sdkAnalyzers = new SortedSet<string>(StringComparer.Ordinal);
        string sdkDirectory = Path.Combine(Property("MSBuildToolsPath"), "Sdks");
        JsonElement items = evaluation.RootElement.GetProperty("Items");
        foreach (string kind in ItemKinds.Split(','))
        foreach (JsonElement item in items.GetProperty(kind).EnumerateArray())
        {
            if (kind == "None" && !IsCopied(item)) continue;
            string itemPath = Get(item, "FullPath");
            string? relative = CheckoutPath(root, itemPath);
            if (relative == null)
            {
                if (kind == "Analyzer" && CheckoutPath(sdkDirectory, itemPath) is { } sdkPath && File.Exists(itemPath))
                    sdkAnalyzers.Add(sdkPath);
                else
                    unresolved($"Evaluated {kind} input escapes the checkout: {path} -> {itemPath}");
                continue;
            }
            if (!File.Exists(itemPath))
                unresolved($"Missing evaluated {kind} input: {path} -> {relative}");
            if (kind == "Compile" && !relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                unresolved($"Unsupported evaluated C# compiler input: {path} -> {relative}");
            if (kind == "Analyzer" || kind == "ProjectReference"
                && Get(item, "OutputItemType").Equals("Analyzer", StringComparison.OrdinalIgnoreCase))
                unresolved($"Repository source generator or analyzer requires audited generated-source coverage: {path} -> {relative}");
            if (kind == "ProjectReference")
            {
                if (new[] { "SetConfiguration", "SetPlatform", "SetTargetFramework", "AdditionalProperties" }
                        .Any(name => Get(item, name).Length != 0)
                    || new[] { "GlobalPropertiesToRemove", "UndefineProperties" }.SelectMany(name => Get(item, name).Split(';'))
                        .Any(name => name.Equals("Configuration", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("Platform", StringComparison.OrdinalIgnoreCase)))
                    unresolved($"Project reference requires distinct evaluation properties: {path} -> {relative}");
                references.Add(relative);
            }
            else inputs.Add(new Input(kind, relative));
        }

        string[] defines = Property("DefineConstants").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new Project(path, configuration, platform, framework, Property("LangVersion"), Property("NETCoreSdkVersion"),
            defines, references.ToArray(), inputs.OrderBy(input => input.Kind, StringComparer.Ordinal)
                .ThenBy(input => input.Path, StringComparer.Ordinal).ToArray(), sdkAnalyzers.ToArray(), buildInputs);
    }

    private static bool IsCopied(JsonElement item) => IsCopy(Get(item, "CopyToOutputDirectory"))
        || IsCopy(Get(item, "CopyToPublishDirectory"));

    private static bool IsCopy(string value) => value.Length != 0 && !value.Equals("Never", StringComparison.OrdinalIgnoreCase);

    internal static string Get(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

    internal static string? CheckoutPath(string root, string path)
    {
        if (!Path.IsPathFullyQualified(path)) return null;
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative is "." or ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null : relative;
    }

    private static string[] ReadBuildInputs(string root, string project, string configuration, IReadOnlySet<string>? tracked)
    {
        XDocument flattened = XDocument.Parse(RunMsBuild(root, project, configuration, "-preprocess"));
        var inputs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { CheckoutPath(root, project)! };
        foreach (XComment comment in flattened.DescendantNodes().OfType<XComment>())
        {
            // /preprocess brackets each resolved import with a separator comment and its absolute path.
            string[] lines = comment.Value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3 || !lines[0].All(character => character == '=') || !lines[^1].All(character => character == '='))
                continue;
            foreach (string line in lines)
            {
                if (CheckoutPath(root, line) is not { } relative || !File.Exists(line)) continue;
                bool generatedNugetImport = relative.EndsWith(".nuget.g.props", StringComparison.OrdinalIgnoreCase)
                    || relative.EndsWith(".nuget.g.targets", StringComparison.OrdinalIgnoreCase);
                if (!generatedNugetImport || tracked?.Contains(relative) == true)
                    inputs.Add(relative);
            }
        }
        return inputs.ToArray();
    }

    private static bool AuditBuildInput(string root, string path, Action<string> unresolved)
    {
        XDocument document = XDocument.Load(Path.Combine(root, path));
        foreach (XElement task in document.Descendants().Where(element => element.Name.LocalName == "UsingTask"))
            unresolved($"Repository build task requires audited source-generation coverage: {path} -> {(string?)task.Attribute("TaskName")}");

        bool safeSdkSymbolEvaluation = true;
        foreach (XElement target in document.Descendants().Where(element => element.Name.LocalName == "Target"))
        {
            if (new[] { "Name", "BeforeTargets", "AfterTargets" }
                .SelectMany(attribute => ((string?)target.Attribute(attribute) ?? "").Split(';', StringSplitOptions.TrimEntries))
                .Any(SdkSymbolTargets.Contains))
                safeSdkSymbolEvaluation = false;
            foreach (XElement step in target.Descendants().Where(IsTargetTimeSourceInput))
                unresolved($"Target-time runtime input requires audited build coverage: {path} -> {(string?)target.Attribute("Name")}/{step.Name.LocalName}");
        }
        return safeSdkSymbolEvaluation;
    }

    private static bool IsTargetTimeSourceInput(XElement element)
    {
        string name = element.Name.LocalName;
        return name == "Csc"
            || element.Parent?.Name.LocalName == "ItemGroup" && RuntimeItems.Contains(name)
            || element.Parent?.Name.LocalName == "PropertyGroup" && name == "DefineConstants"
            || name == "Output" && (RuntimeItems.Contains((string?)element.Attribute("ItemName") ?? "")
                || (string?)element.Attribute("PropertyName") == "DefineConstants");
    }

    internal static void ValidateConfiguration(string root, IEnumerable<Input> inputs, Action<string> unresolved)
    {
        foreach (string path in inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal)
            .Where(path => path.StartsWith("AORebirth/Config/", StringComparison.OrdinalIgnoreCase)))
        {
            if (!File.Exists(Path.Combine(root, path))) continue;
            try
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".json":
                        using (FileStream stream = File.OpenRead(Path.Combine(root, path)))
                        using (JsonDocument.Parse(stream)) { }
                        break;
                    case ".xml":
                        using (XmlReader reader = XmlReader.Create(Path.Combine(root, path)))
                            while (reader.Read()) { }
                        break;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or XmlException)
            {
                unresolved($"Invalid evaluated runtime configuration {path}: {exception.Message}");
            }
        }
    }

    private static string RunMsBuild(string root, string project, string configuration, params string[] query)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        string[] arguments = ["msbuild", project, "-nologo", "-nodeReuse:false", $"-p:Configuration={configuration}",
            "-p:Platform=AnyCPU", .. query];
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start MSBuild.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(EvaluationTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"MSBuild exceeded the {EvaluationTimeoutMilliseconds / 1000}-second evaluation bound.");
        }
        string output = stdout.GetAwaiter().GetResult();
        string errors = stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"MSBuild exited {process.ExitCode}. {errors}{output}");
        return output;
    }
}
