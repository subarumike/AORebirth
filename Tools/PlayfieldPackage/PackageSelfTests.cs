using AORebirth.World.Package;
using System.IO.Compression;
using System.Text;

internal static class PackageSelfTests
{
    internal static void Run()
    {
        string temp = Directory.CreateTempSubdirectory("aorebirth-playfield-package-test-").FullName;
        int passed = 0;
        try
        {
            string source = Path.Combine(temp, "source");
            Directory.CreateDirectory(Path.Combine(source, "1"));
            File.WriteAllText(Path.Combine(source, "1", "A.dat"), "fixture-one", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(source, "1", "B.dat"), "fixture-two", new UTF8Encoding(false));
            PackageFile[] files = PlayfieldPackageValidator.Inventory(source);
            string archive = Path.Combine(temp, "source.zip");
            PackageOperations.WriteArchive(source, files, archive);
            PackageFile zipDescriptor = PlayfieldPackageValidator.Describe(archive, "source.zip");
            var provenance = new PackageProvenance("offline-installed-rdb-structural-export", "unasserted",
                "synthetic self-test fixture; not game content", files.Take(1).ToArray(), [], []);
            var manifest = new PlayfieldPackageManifest(1, PlayfieldPackageValidator.Authority, "Playfields", files.Length,
                files.Sum(f => f.Bytes), PlayfieldPackageValidator.IndexHash(files), zipDescriptor.Sha256, zipDescriptor.Bytes, provenance, files);
            string manifestPath = Path.Combine(temp, "manifest.json");
            PackageOperations.WriteManifest(manifestPath, manifest);
            string destination = Path.Combine(temp, "valid", "GameData", "Playfields");
            PackageOperations.Import(destination, manifestPath, archive);
            PlayfieldPackageValidator.Validate(destination, manifestPath);
            passed++;
            string empty = Path.Combine(temp, "empty", "GameData", "Playfields");
            Directory.CreateDirectory(empty);
            PackageOperations.Import(empty, manifestPath, archive);
            PlayfieldPackageValidator.Validate(empty, manifestPath);
            passed++;
            Reject(() => PackageOperations.Import(destination, manifestPath, archive));
            PlayfieldPackageValidator.Validate(destination, manifestPath); // Existing material remains untouched.
            passed++;
            string secondArchive = Path.Combine(temp, "second.zip");
            PackageOperations.Export(source, manifestPath, secondArchive);
            if (PlayfieldPackageValidator.Describe(secondArchive, "second.zip").Sha256 != manifest.ArchiveSha256)
                throw new InvalidOperationException("Deterministic archive reproduction failed.");
            passed++;
            PackageOperations.NormalizeCreatorPlatform(secondArchive, platform: 3); // Unix creator-header fixture.
            PackageOperations.NormalizeCreatorPlatform(secondArchive);
            if (PlayfieldPackageValidator.Describe(secondArchive, "second.zip").Sha256 != manifest.ArchiveSha256)
                throw new InvalidOperationException("Cross-platform creator header normalization failed.");
            passed++;
            File.AppendAllText(secondArchive, "tampered");
            string rejectedDestination = Path.Combine(temp, "rejected", "GameData", "Playfields");
            Reject(() => PackageOperations.Import(rejectedDestination, manifestPath, secondArchive));
            if (Directory.Exists(rejectedDestination)) throw new InvalidOperationException("Tampered package created its target.");
            passed++;

            foreach (string fault in new[] { "missing", "extra", "case", "duplicate", "traversal", "symlink", "payload", "directorycase" })
            {
                string badArchive = Path.Combine(temp, fault + ".zip");
                using (var zip = ZipFile.Open(badArchive, ZipArchiveMode.Create))
                {
                    Add(zip, fault == "case" ? "1/a.dat" : "1/A.dat", "fixture-one", fault == "symlink");
                    if (fault != "missing") Add(zip, fault == "directorycase" ? "ONE/B.dat" : "1/B.dat", fault == "payload" ? "tampered!!!" : "fixture-two");
                    if (fault == "extra") Add(zip, "1/Unexpected.dat", "extra");
                    if (fault == "duplicate") Add(zip, "1/A.dat", "fixture-one");
                    if (fault == "traversal") Add(zip, "../escaped.dat", "unsafe");
                }
                PackageFile badDescriptor = PlayfieldPackageValidator.Describe(badArchive, fault + ".zip");
                // Re-pin only archive identity in this synthetic fixture to test entry defenses independently.
                PlayfieldPackageManifest adversarial = manifest with { ArchiveSha256 = badDescriptor.Sha256, ArchiveBytes = badDescriptor.Bytes };
                string badManifest = Path.Combine(temp, fault + ".json");
                PackageOperations.WriteManifest(badManifest, adversarial);
                Reject(() => PackageOperations.Import(rejectedDestination, badManifest, badArchive));
                if (Directory.Exists(rejectedDestination)) throw new InvalidOperationException("Invalid entries published a target.");
                passed++;
            }
            File.WriteAllText(Path.Combine(destination, "1", "Extra.dat"), "extra");
            Reject(() => PlayfieldPackageValidator.Validate(destination, manifestPath));
            passed++;
            Reject(() => PlayfieldPackageValidator.ValidateIdentities(new[] { "One/A.dat", "one/B.dat" }));
            Reject(() => PlayfieldPackageValidator.ValidateIdentities(new[] { "one", "one/B.dat" }));
            passed += 2;
            Console.WriteLine("PLAYFIELD_PACKAGE_SELF_TESTS=PASS (" + passed + "/" + passed + ")");
        }
        finally
        {
            PlayfieldPackageValidator.RequireNoLinks(temp);
            Directory.Delete(temp, recursive: true); // Exact private fixture tree created by this method.
        }
    }

    private static void Add(ZipArchive zip, string name, string contents, bool symlink = false)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        entry.ExternalAttributes = unchecked((int)(symlink ? 0xA1FF0000 : 0x81A40000));
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid package was accepted.");
    }
}
