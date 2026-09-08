using AORebirth.World.Package;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--self-test" })) { PackageSelfTests.Run(); return 0; }
            if (args.Length == 0) throw new ArgumentException("Expected create, export, import, validate-current, or --self-test.");
            string operation = args[0];
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || args[i] is not ("--root" or "--manifest" or "--archive" or "--repository-root" or "--provenance-file")
                    || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Unknown, duplicate, or incomplete option.");
            }
            string root = Required(options, "--root");
            string manifest = Required(options, "--manifest");
            switch (operation)
            {
                case "create":
                    PackageOperations.Create(root, manifest, Required(options, "--archive"), Required(options, "--repository-root"), Required(options, "--provenance-file"));
                    break;
                case "export":
                    PackageOperations.Export(root, manifest, Required(options, "--archive"));
                    break;
                case "import":
                    PackageOperations.Import(root, manifest, Required(options, "--archive"));
                    break;
                case "validate-current":
                    PlayfieldPackageValidator.Validate(root, manifest);
                    break;
                default: throw new ArgumentException("Unsupported world package operation.");
            }
            PlayfieldPackageManifest approved = PlayfieldPackageValidator.Load(manifest);
            Console.WriteLine("PLAYFIELD_PACKAGE_FILES=" + approved.FileCount);
            Console.WriteLine("PLAYFIELD_PACKAGE_BYTES=" + approved.TotalBytes);
            Console.WriteLine("PLAYFIELD_PACKAGE_INDEX_SHA256=" + approved.FileIndexSha256);
            Console.WriteLine("PLAYFIELD_PACKAGE_ARCHIVE_SHA256=" + approved.ArchiveSha256);
            Console.WriteLine("PLAYFIELD_PACKAGE_AUTHORITY=" + approved.Authority);
            Console.WriteLine("PLAYFIELD_PACKAGE_" + operation.Replace('-', '_').ToUpperInvariant() + "=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("PLAYFIELD_PACKAGE=FAIL " + exception.Message);
            return 1;
        }
    }

    private static string Required(Dictionary<string, string> options, string name)
        => options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? Path.GetFullPath(value) : throw new ArgumentException("Missing " + name);
}

internal static class PackageOperations
{
    internal static void Create(string root, string manifestPath, string archivePath, string repository, string provenancePath)
    {
        RequireAbsentFile(manifestPath);
        RequireAbsentFile(archivePath);
        PackageFile[] files = PlayfieldPackageValidator.Inventory(root);
        PackageProvenance provenance = ReadProvenance(repository, root, files, provenancePath);
        WriteArchive(root, files, archivePath);
        PackageFile archive = PlayfieldPackageValidator.Describe(archivePath, "playfields.zip");
        var manifest = new PlayfieldPackageManifest(1, PlayfieldPackageValidator.Authority, "Playfields",
            files.Length, files.Sum(f => f.Bytes), PlayfieldPackageValidator.IndexHash(files), archive.Sha256,
            archive.Bytes, provenance, files);
        PlayfieldPackageValidator.ValidateManifest(manifest);
        // Recheck after archive generation: changed extraction inputs cannot produce an accepted manifest.
        PlayfieldPackageValidator.Validate(root, manifest);
        WriteManifest(manifestPath, manifest);
    }

    internal static void Export(string root, string manifestPath, string archivePath)
    {
        PlayfieldPackageManifest manifest = PlayfieldPackageValidator.Validate(root, manifestPath);
        RequireAbsentFile(archivePath);
        WriteArchive(root, manifest.Files, archivePath);
        ValidateArchiveIdentity(archivePath, manifest);
    }

    internal static void Import(string root, string manifestPath, string archivePath)
    {
        PlayfieldPackageManifest manifest = PlayfieldPackageValidator.Load(manifestPath);
        ValidateArchiveIdentity(archivePath, manifest);
        root = Path.GetFullPath(root);
        // Import has one managed target shape. It never merges, overwrites, or prunes user material.
        if (Path.GetFileName(root) != "Playfields" || Path.GetFileName(Path.GetDirectoryName(root)) != "GameData")
            throw new InvalidDataException("Import target must be a managed GameData/Playfields directory.");
        RequireEmptyOrAbsent(root);
        string parent = Path.GetDirectoryName(root)!;
        PlayfieldPackageValidator.RequireNoLinks(parent);
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".playfields-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            using (FileStream archiveStream = new(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read))
            {
                ValidateArchiveEntries(zip, manifest);
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(stage, entry.FullName));
                    if (!destination.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new InvalidDataException("Archive entry escapes staging root.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using Stream input = entry.Open();
                    using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    CopyExactly(input, output, entry.Length);
                }
            }
            PlayfieldPackageValidator.Validate(stage, manifest);
            RequireEmptyOrAbsent(root);
            if (Directory.Exists(root)) Directory.Delete(root); // Only an empty managed directory; never recursive.
            Directory.Move(stage, root);
        }
        finally
        {
            // This GUID staging directory contains only files this invocation created.
            if (Directory.Exists(stage))
            {
                PlayfieldPackageValidator.Inventory(stage); // Reject links before bounded recursive cleanup.
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    internal static void WriteArchive(string root, PackageFile[] files, string archivePath)
    {
        RequireAbsentFile(archivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
        {
            foreach (PackageFile file in files)
            {
                // Stored entries avoid OS/compressor-version differences.
                ZipArchiveEntry entry = zip.CreateEntry(file.Path, CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                entry.ExternalAttributes = unchecked((int)0x81A40000); // POSIX regular file 0644, fixed across host OSes.
                using FileStream input = new(Path.Combine(root, file.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
                using Stream output = entry.Open();
                CopyExactly(input, output, file.Bytes);
            }
        }
        NormalizeCreatorPlatform(archivePath);
    }

    // ZipArchive stamps its host OS into central-directory "version made by" even
    // when entry metadata is fixed. Normalize only that byte in our generated ZIP32
    // output; never rewrite supplied archives. See dotnet/runtime ZipArchiveEntry.
    internal static void NormalizeCreatorPlatform(string archivePath, byte platform = 0)
    {
        using FileStream archive = new(archivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (archive.Length < 22 || archive.Length >= uint.MaxValue)
            throw new InvalidDataException("Deterministic world package export requires ZIP32.");
        Span<byte> end = stackalloc byte[22];
        archive.Position = archive.Length - end.Length;
        archive.ReadExactly(end);
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(end[10..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(end) != 0x06054b50 || count == ushort.MaxValue
            || BinaryPrimitives.ReadUInt16LittleEndian(end[20..]) != 0)
            throw new InvalidDataException("Generated archive has an unsupported ZIP directory.");
        archive.Position = BinaryPrimitives.ReadUInt32LittleEndian(end[16..]);
        Span<byte> header = stackalloc byte[46];
        for (int i = 0; i < count; i++)
        {
            long start = archive.Position;
            archive.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x02014b50)
                throw new InvalidDataException("Generated archive central directory is invalid.");
            long next = start + header.Length + BinaryPrimitives.ReadUInt16LittleEndian(header[28..])
                + BinaryPrimitives.ReadUInt16LittleEndian(header[30..]) + BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            archive.Position = start + 5;
            archive.WriteByte(platform);
            archive.Position = next;
        }
        if (archive.Position != archive.Length - end.Length)
            throw new InvalidDataException("Generated ZIP32 directory has trailing data.");
    }

    internal static void WriteManifest(string path, PlayfieldPackageManifest manifest)
    {
        RequireAbsentFile(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, PlayfieldPackageValidator.JsonOptions) + "\n");
        output.Write(data);
    }

    private static void ValidateArchiveIdentity(string path, PlayfieldPackageManifest manifest)
    {
        PackageFile archive = PlayfieldPackageValidator.Describe(path, "playfields.zip");
        if (archive.Bytes != manifest.ArchiveBytes || archive.Sha256 != manifest.ArchiveSha256)
            throw new InvalidDataException("Archive size/SHA256 does not match the pinned world package manifest.");
    }

    private static void ValidateArchiveEntries(ZipArchive zip, PlayfieldPackageManifest manifest)
    {
        PlayfieldPackageValidator.ValidateIdentities(zip.Entries.Select(e => e.FullName));
        string[] expected = manifest.Files.Select(f => f.Path).ToArray();
        string[] actual = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidDataException("Archive has missing, extra, or case-drifted entries.");
        var files = manifest.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            uint attributes = unchecked((uint)entry.ExternalAttributes);
            uint unixType = (attributes >> 16) & 0xF000;
            if ((unixType != 0 && unixType != 0x8000) || (attributes & (0x10 | 0x400)) != 0)
                throw new InvalidDataException("Archive entries must be regular files, not directories or symbolic links.");
            if (entry.Length != files[entry.FullName].Bytes)
                throw new InvalidDataException("Archive entry length differs from its pinned descriptor.");
        }
    }

    private static void CopyExactly(Stream input, Stream output, long expected)
    {
        byte[] buffer = new byte[81920];
        long remaining = expected;
        while (remaining > 0)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) throw new InvalidDataException("Package input ended before its expected length.");
            output.Write(buffer, 0, read);
            remaining -= read;
        }
        if (input.ReadByte() != -1) throw new InvalidDataException("Package input exceeds its expected length.");
    }

    private static void RequireEmptyOrAbsent(string root)
    {
        PlayfieldPackageValidator.RequireNoLinks(root);
        if (File.Exists(root) || (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()))
            throw new InvalidDataException("Import refuses existing Playfields material; no merge, overwrite or prune is allowed.");
    }

    private static void RequireAbsentFile(string path)
    {
        PlayfieldPackageValidator.RequireNoLinks(path);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Output already exists; refusing replacement: " + path);
    }

    private static PackageProvenance ReadProvenance(string repository, string root, PackageFile[] files, string provenancePath)
    {
        repository = Path.GetFullPath(repository);
        string project = Path.Combine(repository, "Tools/RDBDataExtractor/RDBDataExtractor.csproj");
        var sourcePaths = Directory.GetFiles(Path.GetDirectoryName(project)!, "*.cs").Append(project)
            .Append(Path.Combine(repository, "Tools/extract-rdb-tilemaps.cmd")).ToList();
        foreach (XElement compile in XDocument.Load(project).Descendants("Compile"))
        {
            string include = (string?)compile.Attribute("Include") ?? throw new InvalidDataException("Extractor compile provenance is incomplete.");
            sourcePaths.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include.Replace('\\', '/'))));
        }
        PackageFile[] DescribeSources(IEnumerable<string> paths) => paths.Distinct(StringComparer.Ordinal)
            .Select(path => PlayfieldPackageValidator.Describe(path, Path.GetRelativePath(repository, path).Replace('\\', '/')))
            .OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
        string output = Path.Combine(repository, "Tools/RDBDataExtractor/bin/Debug/net10.0");
        PackageFile[] buildFiles = DescribeSources(Directory.GetFiles(output)
            .Where(p => p.EndsWith(".dll", StringComparison.Ordinal) || p.EndsWith(".deps.json", StringComparison.Ordinal)
                || p.EndsWith(".runtimeconfig.json", StringComparison.Ordinal)));
        var rawRecords = new List<RawRecordSource>();
        foreach (PackageFile file in files.Where(f => f.Path.EndsWith("/metadata.json", StringComparison.Ordinal)))
        {
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, file.Path)));
            JsonElement record = metadata.RootElement;
            rawRecords.Add(new RawRecordSource(file.Path, record.GetProperty("recordType").GetInt32(),
                record.GetProperty("tilemapResource").GetInt32(), record.GetProperty("rawRecordSha256").GetString()!));
        }
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllBytes(provenancePath));
        if (provenance.RootElement.GetProperty("authority").GetString() != PlayfieldPackageValidator.Authority
            || provenance.RootElement.GetProperty("installedClientVersion").GetString() != "unasserted")
            throw new InvalidDataException("Extraction provenance must preserve structural-only/unasserted version boundaries.");
        sourcePaths.Add(provenancePath);
        return new PackageProvenance("offline-installed-rdb-structural-export", "unasserted",
            provenance.RootElement.GetProperty("extractionCommand").GetString()!,
            DescribeSources(sourcePaths), buildFiles, rawRecords.ToArray());
    }
}
