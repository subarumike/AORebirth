using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeRuntimeGraph
{
    private const string ProxyRoot = "Tools/AOClientRoomSpaceGuard/ProxyDll/";
    private static readonly (string Group, string Entry)[] Targets =
        [("proxy", "version_proxy.cpp"), ("deploy", "deploy_tool.cpp"), ("setup", "setup_tool.cpp")];
    private static readonly string[] LinkedInputs =
        ["Build-Package.cmd", "src/version_proxy.def", "src/deploy_tool.manifest", "src/setup_tool.manifest"];
    private static readonly string[] PackagedConfiguration =
        ["AORebirthServers.txt", "AORebirthAnarchyLauncher.url", "AORebirthDimensionServer.url"];

    internal sealed record TranslationUnit(string Target, string Source, string[] Includes, int ToolchainIncludes);
    internal sealed record Report(string SourceRevision, TranslationUnit[] TranslationUnits, string[] LinkedInputs,
        string[] PackageConfiguration);

    internal static Report? Read(string checkout, string buildRoot, string sourceRevision, Action<string> unresolved)
    {
        try
        {
            string graph = Path.Combine(buildRoot, "graph");
            string revision = File.ReadAllText(Path.Combine(graph, "source-revision.txt")).Trim();
            if (!string.Equals(sourceRevision, revision, StringComparison.Ordinal))
                throw new InvalidDataException("Native compiler graph was generated for a different source revision.");

            string[] toolchain = File.ReadAllLines(Path.Combine(graph, "toolchain.txt")).Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0] is "VCToolsInstallDir" or "WindowsSdkDir")
                .Select(parts => parts[1]).ToArray();
            if (toolchain.Length != 2 || toolchain.Any(path => !Path.IsPathFullyQualified(path) || !Directory.Exists(path)))
                throw new InvalidDataException("The native dependency graph is missing MSVC or Windows SDK provenance.");
            var units = new List<TranslationUnit>();
            foreach ((string group, string entry) in Targets)
            {
                string directory = Path.Combine(graph, group);
                var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                    JsonElement data = document.RootElement.GetProperty("Data");
                    string? source = EvaluatedRuntimeGraph.CheckoutPath(checkout, EvaluatedRuntimeGraph.Get(data, "Source"));
                    if (source == null || !sources.Add(source) || !DoaAdditionalInputs.IsNativeSource(source))
                        throw new InvalidDataException($"Invalid or duplicate native compiler input in {group}/{Path.GetFileName(file)}.");
                    var includes = new SortedSet<string>(StringComparer.Ordinal);
                    int external = 0;
                    foreach (JsonElement item in data.GetProperty("Includes").EnumerateArray())
                    {
                        string include = item.GetString() ?? "";
                        if (EvaluatedRuntimeGraph.CheckoutPath(checkout, include) is { } relative)
                            includes.Add(relative);
                        else if (toolchain.Any(root => EvaluatedRuntimeGraph.CheckoutPath(root, include) != null) && File.Exists(include))
                            external++;
                        else
                            throw new InvalidDataException($"Unrecognized external native include for {source}: {include}");
                    }
                    units.Add(new TranslationUnit(group, source, includes.ToArray(), external));
                }
                if (!sources.Contains(ProxyRoot + "src/" + entry))
                    throw new InvalidDataException($"Native {group} compiler evidence lacks its runtime entry source {entry}.");
            }

            string[] linked = LinkedInputs.Select(file => ProxyRoot + file).ToArray();
            string[] measured = units.Select(unit => unit.Source).Concat(units.SelectMany(unit => unit.Includes))
                .Concat(linked).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using JsonDocument hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(graph, "source-hashes.json")));
            foreach (string file in measured)
            {
                string actual = Hash(Path.Combine(checkout, file));
                if (!string.Equals(actual, EvaluatedRuntimeGraph.Get(hashes.RootElement, file), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Native dependency measurement is missing or stale for {file}.");
            }
            foreach (string file in PackagedConfiguration)
                if (Hash(Path.Combine(checkout, ProxyRoot, file)) != Hash(Path.Combine(buildRoot, "package", file)))
                    throw new InvalidDataException($"Validated native package configuration differs from the checkout: {file}");

            return new Report(revision, units.ToArray(), linked, PackagedConfiguration.Select(file => ProxyRoot + file).ToArray());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or JsonException or KeyNotFoundException)
        {
            unresolved("Native runtime graph unavailable or invalid: " + exception.Message);
            return null;
        }
    }

    private static string Hash(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
}
