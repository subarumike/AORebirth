namespace AORebirth.World.Package;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record PackageFile(string Path, long Bytes, string Sha256);
public sealed record RawRecordSource(string MetadataPath, int RecordType, int ResourceId, string Sha256);
public sealed record PackageProvenance(string Kind, string InstalledClientVersion, string ExtractionCommand,
    PackageFile[] ExtractorSources, PackageFile[] ExtractorBuildFiles, RawRecordSource[] RawRecords);

public sealed record PlayfieldPackageManifest(int SchemaVersion, string Authority, string ContentRoot,
    int FileCount, long TotalBytes, string FileIndexSha256, string ArchiveSha256, long ArchiveBytes,
    PackageProvenance Provenance, PackageFile[] Files);

/// <summary>Offline read-only byte identity validation. This never grants NPC spawn/behavior authority.</summary>
public static class PlayfieldPackageValidator
{
    public const string Authority = "structural-only-no-runtime-activation";
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static PlayfieldPackageManifest Load(string manifestPath)
    {
        RequireNoLinks(manifestPath);
        using FileStream stream = File.OpenRead(manifestPath);
        using JsonDocument json = JsonDocument.Parse(stream);
        RejectDuplicateProperties(json.RootElement);
        PlayfieldPackageManifest manifest = json.RootElement.Deserialize<PlayfieldPackageManifest>(JsonOptions)
            ?? throw new InvalidDataException("World package manifest is empty.");
        ValidateManifest(manifest);
        return manifest;
    }

    public static PlayfieldPackageManifest Validate(string playfieldsRoot, string manifestPath)
    {
        PlayfieldPackageManifest manifest = Load(manifestPath);
        Validate(playfieldsRoot, manifest);
        return manifest;
    }

    public static void Validate(string playfieldsRoot, PlayfieldPackageManifest manifest)
    {
        ValidateManifest(manifest);
        PackageFile[] actual = Inventory(playfieldsRoot);
        if (!actual.SequenceEqual(manifest.Files))
            throw new InvalidDataException("Playfield package differs from its pinned manifest (missing, extra, case, size or hash mismatch).");
    }

    public static PackageFile[] Inventory(string root)
    {
        root = Path.GetFullPath(root);
        RequireNoLinks(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Playfield package root is missing.");
        var paths = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? current))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(current))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Playfield package may not contain symbolic links or reparse points.");
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                ValidateRelativePath(relative);
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                else paths.Add(relative);
            }
        }
        ValidateIdentities(paths);
        return paths.Order(StringComparer.Ordinal).Select(relative => Describe(Path.Combine(root, relative), relative)).ToArray();
    }

    public static PackageFile Describe(string file, string relative)
    {
        RequireNoLinks(file);
        using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = stream.Length;
        return new PackageFile(relative, length, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    public static string IndexHash(IEnumerable<PackageFile> files)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(files.Select(
            f => f.Path + "\0" + f.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + f.Sha256 + "\n")))));

    public static void ValidateManifest(PlayfieldPackageManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.Authority != Authority || manifest.ContentRoot != "Playfields"
            || manifest.Files == null || manifest.FileCount < 1 || manifest.FileCount != manifest.Files.Length
            || manifest.ArchiveBytes < 1 || !IsHash(manifest.ArchiveSha256) || !IsHash(manifest.FileIndexSha256)
            || manifest.Provenance == null || manifest.Provenance.Kind != "offline-installed-rdb-structural-export"
            || manifest.Provenance.InstalledClientVersion != "unasserted")
            throw new InvalidDataException("Unsupported or incomplete world package manifest.");
        ValidateFiles(manifest.Files);
        if (manifest.TotalBytes != manifest.Files.Sum(f => f.Bytes) || manifest.FileIndexSha256 != IndexHash(manifest.Files))
            throw new InvalidDataException("World package inventory totals/digest are invalid.");
        ValidateFiles(manifest.Provenance.ExtractorSources);
        ValidateFiles(manifest.Provenance.ExtractorBuildFiles);
        if (manifest.Provenance.ExtractorSources.Length == 0 || manifest.Provenance.RawRecords == null
            || string.IsNullOrWhiteSpace(manifest.Provenance.ExtractionCommand))
            throw new InvalidDataException("World package extraction provenance is missing.");
        var metadata = manifest.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        ValidateIdentities(manifest.Provenance.RawRecords.Select(r => r.MetadataPath));
        foreach (RawRecordSource raw in manifest.Provenance.RawRecords)
            if (!metadata.ContainsKey(raw.MetadataPath) || raw.RecordType <= 0 || raw.ResourceId <= 0 || !IsHash(raw.Sha256))
                throw new InvalidDataException("World package raw-resource provenance is invalid.");
    }

    private static void ValidateFiles(PackageFile[] files)
    {
        if (files == null || files.Any(f => f == null || f.Bytes < 0 || !IsHash(f.Sha256)))
            throw new InvalidDataException("World package file descriptor is invalid.");
        ValidateIdentities(files.Select(f => f.Path));
        if (!files.Select(f => f.Path).SequenceEqual(files.Select(f => f.Path).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("World package inventory must be ordinally sorted.");
    }

    public static void ValidateIdentities(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            ValidateRelativePath(path);
            if (!seen.Add(path)) throw new InvalidDataException("World package contains duplicate or case-colliding paths.");
            string prefix = "";
            foreach (string part in path.Split('/'))
            {
                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                if (prefixes.TryGetValue(prefix, out string? existing) && existing != prefix)
                    throw new InvalidDataException("World package directory casing is inconsistent.");
                prefixes[prefix] = prefix;
            }
        }
        foreach (string path in seen)
        {
            int separator = path.IndexOf('/');
            while (separator >= 0)
            {
                if (seen.Contains(path[..separator]))
                    throw new InvalidDataException("World package file conflicts with a directory identity.");
                separator = path.IndexOf('/', separator + 1);
            }
        }
    }

    public static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.StartsWith('/') || path.EndsWith('/'))
            throw new InvalidDataException("World package path is not canonical relative POSIX form.");
        foreach (string part in path.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.Any(c => c < 33 || c > 126 || "<>:\"|?*".Contains(c)))
                throw new InvalidDataException("World package path is unsafe or not portable.");
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4
                && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
                throw new InvalidDataException("World package path uses a reserved Windows name.");
        }
    }

    public static void RequireNoLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current != null)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("World package paths must not traverse symbolic links/reparse points.");
            current = Path.GetDirectoryName(current);
        }
    }

    private static bool IsHash(string? text) => text is { Length: 64 } && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new InvalidDataException("Manifest JSON contains a duplicate property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
}
