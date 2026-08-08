using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace AORebirth.LinuxBuild.SourceInventoryGuard;

internal static class Program
{
    private const string RepositoryRootProperty = "$(AORebirthRepositoryRoot)";

    public static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            IReadOnlySet<string> trackedPaths = LoadTrackedPaths(options.RepositoryRoot);
            foreach (InventoryJob job in options.GetJobs())
            {
                string expected = Generate(
                    options.RepositoryRoot,
                    job.LegacyProjectPath,
                    trackedPaths,
                    job.ContentOnly);

                if (options.Write)
                {
                    string? directory = Path.GetDirectoryName(job.OutputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(job.OutputPath, expected, new UTF8Encoding(false));
                    Console.WriteLine($"WROTE: {job.OutputPath}");
                    continue;
                }

                if (!File.Exists(job.OutputPath))
                {
                    Console.Error.WriteLine($"STALE: source inventory is missing: {job.OutputPath}");
                    return 1;
                }

                string actual = File.ReadAllText(job.OutputPath, Encoding.UTF8);
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"STALE: source inventory does not match {job.LegacyProjectPath}");
                    return 1;
                }

                Console.WriteLine($"PASS: {job.OutputPath}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
    }

    private static string Generate(
        string repositoryRoot,
        string legacyProjectPath,
        IReadOnlySet<string> trackedPaths,
        bool contentOnly)
    {
        XDocument legacyProject = XDocument.Load(legacyProjectPath, LoadOptions.PreserveWhitespace);
        XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
        string legacyDirectory = Path.GetDirectoryName(legacyProjectPath)
            ?? throw new InvalidOperationException("Legacy project has no parent directory.");

        var projectItems = legacyProject
            .Descendants()
            .Where(
                element => contentOnly
                    ? element.Name == msbuild + "Content"
                    : element.Name == msbuild + "Compile" || element.Name == msbuild + "EmbeddedResource")
            .Select(
                element => contentOnly
                    ? CreateContentProjectItem(element, legacyDirectory, repositoryRoot, trackedPaths)
                    : CreateProjectItem(element, legacyDirectory, repositoryRoot, trackedPaths))
            .ToArray();

        if (projectItems.Length == 0)
        {
            throw new InvalidOperationException(
                contentOnly
                    ? "Legacy project contains no Content items."
                    : "Legacy project contains no Compile or EmbeddedResource items.");
        }

        var seenItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (XElement projectItem in projectItems)
        {
            string key = projectItem.Name.LocalName + "|" + projectItem.Attribute("Include")!.Value;
            if (!seenItems.Add(key))
            {
                throw new InvalidOperationException($"Duplicate or case-colliding project item: {key}");
            }
        }

        var output = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project", new XElement("ItemGroup", projectItems)));

        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (XmlWriter writer = XmlWriter.Create(builder, settings))
        {
            output.Save(writer);
        }

        return builder.ToString() + "\n";
    }

    private static XElement CreateContentProjectItem(
        XElement legacyElement,
        string legacyDirectory,
        string repositoryRoot,
        IReadOnlySet<string> trackedPaths)
    {
        XElement validatedItem = CreateProjectItem(
            legacyElement,
            legacyDirectory,
            repositoryRoot,
            trackedPaths);
        XElement[] legacyMetadata = legacyElement.Elements().ToArray();
        if (legacyMetadata.Length != 1
            || legacyMetadata[0].Name.LocalName != "CopyToOutputDirectory"
            || legacyMetadata[0].Value != "PreserveNewest")
        {
            string include = legacyElement.Attribute("Include")?.Value ?? "<missing>";
            throw new InvalidOperationException(
                $"Content item must have only CopyToOutputDirectory=PreserveNewest metadata: {include}");
        }

        string legacyInclude = legacyElement.Attribute("Include")!.Value;
        return new XElement(
            "Content",
            new XAttribute("Include", validatedItem.Attribute("Include")!.Value),
            new XAttribute("Link", legacyInclude.Replace('\\', '/')),
            new XAttribute("CopyToOutputDirectory", "PreserveNewest"),
            new XAttribute("CopyToPublishDirectory", "PreserveNewest"));
    }

    private static XElement CreateProjectItem(
        XElement legacyElement,
        string legacyDirectory,
        string repositoryRoot,
        IReadOnlySet<string> trackedPaths)
    {
        if (legacyElement.Attribute("Condition") != null
            || legacyElement.Ancestors(legacyElement.Name.Namespace + "ItemGroup").Any(group => group.Attribute("Condition") != null))
        {
            throw new InvalidOperationException($"Conditional {legacyElement.Name.LocalName} items are not supported.");
        }

        string include = legacyElement.Attribute("Include")?.Value
            ?? throw new InvalidOperationException($"{legacyElement.Name.LocalName} item is missing Include.");
        if (include.IndexOfAny(new[] { '*', '?' }) >= 0 || include.Contains("$(", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Wildcard or evaluated project item is not supported: {include}");
        }

        string platformPath = include
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(legacyDirectory, platformPath));
        string repositoryRelative = Path.GetRelativePath(repositoryRoot, fullPath);

        if (repositoryRelative == ".."
            || repositoryRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Project item escapes the repository: {include}");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Project item does not exist: {include}", fullPath);
        }

        ValidateExactPathCasing(repositoryRoot, fullPath, include);

        string portablePath = repositoryRelative.Replace(Path.DirectorySeparatorChar, '/');
        if (!trackedPaths.Contains(portablePath))
        {
            throw new InvalidOperationException($"Project item is not tracked by Git: {portablePath}");
        }

        var result = new XElement(
            legacyElement.Name.LocalName,
            new XAttribute("Include", RepositoryRootProperty + "/" + portablePath));
        foreach (XElement metadata in legacyElement.Elements())
        {
            string value = metadata.Name.LocalName == "Link"
                ? metadata.Value.Replace('\\', '/')
                : metadata.Value;
            result.Add(new XElement(metadata.Name.LocalName, value));
        }

        return result;
    }

    private static IReadOnlySet<string> LoadTrackedPaths(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryRoot);
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Git for source inventory validation.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Unable to read tracked repository files: " + error.Trim());
        }

        return output
            .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateExactPathCasing(string repositoryRoot, string fullPath, string include)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
        string currentPath = repositoryRoot;
        foreach (string segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            string? exactName = Directory
                .EnumerateFileSystemEntries(currentPath)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, segment, StringComparison.Ordinal));
            if (exactName == null)
            {
                string? caseInsensitiveName = Directory
                    .EnumerateFileSystemEntries(currentPath)
                    .Select(Path.GetFileName)
                    .FirstOrDefault(name => string.Equals(name, segment, StringComparison.OrdinalIgnoreCase));
                if (caseInsensitiveName != null)
                {
                    throw new InvalidOperationException(
                        $"Project item casing does not match the filesystem: {include} (expected {caseInsensitiveName}).");
                }

                throw new FileNotFoundException($"Project item path segment does not exist: {include}");
            }

            currentPath = Path.Combine(currentPath, exactName);
        }
    }

    private sealed record InventoryJob(string LegacyProjectPath, string OutputPath, bool ContentOnly);

    private sealed record Options(
        string RepositoryRoot,
        string? LegacyProjectPath,
        string? OutputPath,
        string? ManifestPath,
        bool Write)
    {
        public IReadOnlyList<InventoryJob> GetJobs()
        {
            if (ManifestPath == null)
            {
                return new[] { new InventoryJob(LegacyProjectPath!, OutputPath!, false) };
            }

            using FileStream stream = File.OpenRead(ManifestPath);
            InventoryManifest manifest = JsonSerializer.Deserialize<InventoryManifest>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Inventory manifest is empty.");
            if (manifest.Projects == null || manifest.Projects.Length == 0)
            {
                throw new InvalidOperationException("Inventory manifest contains no projects.");
            }

            var jobs = new List<InventoryJob>();
            foreach (InventoryManifestEntry entry in manifest.Projects)
            {
                string legacyProjectPath = Path.GetFullPath(
                    Path.Combine(RepositoryRoot, entry.LegacyProject));
                jobs.Add(
                    new InventoryJob(
                        legacyProjectPath,
                        Path.GetFullPath(Path.Combine(RepositoryRoot, entry.Output)),
                        false));
                if (entry.ContentOutput != null)
                {
                    if (string.IsNullOrWhiteSpace(entry.ContentOutput))
                    {
                        throw new InvalidOperationException("Inventory contentOutput cannot be empty.");
                    }

                    jobs.Add(
                        new InventoryJob(
                            legacyProjectPath,
                            Path.GetFullPath(Path.Combine(RepositoryRoot, entry.ContentOutput)),
                            true));
                }
            }

            return jobs;
        }

        public static Options Parse(string[] args)
        {
            string? repositoryRoot = null;
            string? legacyProject = null;
            string? output = null;
            string? manifest = null;
            bool? write = null;

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--repository-root":
                        repositoryRoot = ReadValue(args, ref index);
                        break;
                    case "--legacy-project":
                        legacyProject = ReadValue(args, ref index);
                        break;
                    case "--output":
                        output = ReadValue(args, ref index);
                        break;
                    case "--manifest":
                        manifest = ReadValue(args, ref index);
                        break;
                    case "--write":
                        write = true;
                        break;
                    case "--check":
                        write = false;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {args[index]}");
                }
            }

            bool hasSingleProject = legacyProject != null && output != null && manifest == null;
            bool hasManifest = manifest != null && legacyProject == null && output == null;
            if (repositoryRoot == null || write == null || (!hasSingleProject && !hasManifest))
            {
                throw new ArgumentException(
                    "Required: --repository-root <path> (--manifest <path> | --legacy-project <path> --output <path>) (--check|--write)");
            }

            return new Options(
                Path.GetFullPath(repositoryRoot),
                legacyProject == null ? null : Path.GetFullPath(legacyProject),
                output == null ? null : Path.GetFullPath(output),
                manifest == null ? null : Path.GetFullPath(manifest),
                write.Value);
        }

        private static string ReadValue(string[] args, ref int index)
        {
            index++;
            if (index >= args.Length)
            {
                throw new ArgumentException("Missing option value.");
            }

            return args[index];
        }
    }

    private sealed record InventoryManifest(InventoryManifestEntry[] Projects);

    private sealed record InventoryManifestEntry(string LegacyProject, string Output, string? ContentOutput);
}
