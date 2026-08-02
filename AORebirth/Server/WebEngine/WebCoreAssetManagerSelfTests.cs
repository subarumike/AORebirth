namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml.Linq;

    internal sealed class WebCoreFixtureFile
    {
        public string Path { get; set; }

        public byte[] Content { get; set; }
    }

    internal sealed class WebCoreFixtureArchiveEntry
    {
        public string Path { get; set; }

        public byte[] Content { get; set; }

        public int? ExternalAttributes { get; set; }
    }

    public static class WebCoreAssetManagerSelfTests
    {
        private const string FixtureId = "fixture-webcore-v1";
        private const string FixtureCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string FixtureRoot = "fixture-root";

        public static bool Run(TextWriter output)
        {
            int passed = 0;
            int total = 0;
            string testRoot = Path.Combine(
                Path.GetTempPath(),
                "AORebirth-WebCoreSelfTest-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

            try
            {
                Directory.CreateDirectory(testRoot);
                RunCase(output, "valid-tree", testRoot, TestValidTree, ref passed, ref total);
                RunCase(output, "missing-manifest", testRoot, TestMissingManifest, ref passed, ref total);
                RunCase(output, "missing-file", testRoot, TestMissingFile, ref passed, ref total);
                RunCase(output, "modified-file", testRoot, TestModifiedFile, ref passed, ref total);
                RunCase(output, "unexpected-file", testRoot, TestUnexpectedFile, ref passed, ref total);
                RunCase(output, "manifest-traversal", testRoot, TestManifestTraversal, ref passed, ref total);
                RunCase(output, "manifest-case-collision", testRoot, TestManifestCaseCollision, ref passed, ref total);
                RunCase(output, "manifest-file-directory-collision", testRoot, TestManifestFileDirectoryCollision, ref passed, ref total);
                RunCase(output, "manifest-file-size-bound", testRoot, TestManifestFileSizeBound, ref passed, ref total);
                RunCase(output, "valid-offline-import", testRoot, TestValidImport, ref passed, ref total);
                RunCase(output, "exclusive-runtime-lease", testRoot, TestExclusiveRuntimeLease, ref passed, ref total);
                RunCase(output, "wrong-version-preserves-tree", testRoot, TestWrongVersionPreservesTree, ref passed, ref total);
                RunCase(output, "manifest-id-version-preserves-tree", testRoot, TestManifestIdVersionPreservesTree, ref passed, ref total);
                RunCase(output, "archive-hash-preserves-tree", testRoot, TestArchiveHashPreservesTree, ref passed, ref total);
                RunCase(output, "remote-archive-path", testRoot, TestRemoteArchivePath, ref passed, ref total);
                RunCase(output, "parent-traversal", testRoot, delegate(string root) { TestUnsafeArchivePath(root, FixtureRoot + "/../escape.php"); }, ref passed, ref total);
                RunCase(output, "absolute-windows-path", testRoot, delegate(string root) { TestUnsafeArchivePath(root, "C:/absolute.php"); }, ref passed, ref total);
                RunCase(output, "unc-path", testRoot, delegate(string root) { TestUnsafeArchivePath(root, "//server/share/evil.php"); }, ref passed, ref total);
                RunCase(output, "drive-qualified-path", testRoot, delegate(string root) { TestUnsafeArchivePath(root, FixtureRoot + "/C:/evil.php"); }, ref passed, ref total);
                RunCase(output, "mixed-slash-traversal", testRoot, delegate(string root) { TestUnsafeArchivePath(root, FixtureRoot + "/..\\evil.php"); }, ref passed, ref total);
                RunCase(output, "encoded-traversal", testRoot, delegate(string root) { TestUnsafeArchivePath(root, FixtureRoot + "/%2e%2e/evil.php"); }, ref passed, ref total);
                RunCase(output, "unicode-normalization", testRoot, delegate(string root) { TestUnsafeArchivePath(root, FixtureRoot + "/cafe\u0301.php"); }, ref passed, ref total);
                RunCase(output, "duplicate-entry", testRoot, TestDuplicateArchiveEntry, ref passed, ref total);
                RunCase(output, "case-collision-entry", testRoot, TestCaseCollisionArchiveEntry, ref passed, ref total);
                RunCase(output, "directory-case-collision-entry", testRoot, TestDirectoryCaseCollisionArchiveEntry, ref passed, ref total);
                RunCase(output, "archive-file-directory-collision", testRoot, TestArchiveFileDirectoryCollision, ref passed, ref total);
                RunCase(output, "oversized-entry", testRoot, TestOversizedEntry, ref passed, ref total);
                RunCase(output, "unexpected-entry", testRoot, TestUnexpectedEntry, ref passed, ref total);
                RunCase(output, "missing-entry", testRoot, TestMissingEntry, ref passed, ref total);
                RunCase(output, "entry-hash-mismatch", testRoot, TestEntryHashMismatch, ref passed, ref total);
                RunCase(output, "corrupt-archive", testRoot, TestCorruptArchive, ref passed, ref total);
                RunCase(output, "symlink-entry", testRoot, TestSymlinkEntry, ref passed, ref total);
                RunCase(output, "activation-failure-preserves-tree", testRoot, TestActivationFailurePreservesTree, ref passed, ref total);
                RunCase(output, "activation-collision-retains-backup", testRoot, TestActivationCollisionRetainsBackup, ref passed, ref total);
                RunCase(output, "backup-cleanup-failure-reported", testRoot, TestBackupCleanupFailureReported, ref passed, ref total);
                RunCase(output, "failed-import-leaves-no-temporary-tree", testRoot, TestFailedImportLeavesNoTemporaryTree, ref passed, ref total);

                output.WriteLine("[WebCore Asset Self-Test] PASS " + passed + "/" + total);
                return true;
            }
            catch (Exception exception)
            {
                output.WriteLine("[WebCore Asset Self-Test] FAIL: " + exception.Message);
                return false;
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, true);
                }
            }
        }

        private static void RunCase(
            TextWriter output,
            string name,
            string root,
            Action<string> test,
            ref int passed,
            ref int total)
        {
            total++;
            string caseRoot = Path.Combine(root, total.ToString("D2", CultureInfo.InvariantCulture) + "-" + name);
            Directory.CreateDirectory(caseRoot);
            test(caseRoot);
            passed++;
            output.WriteLine("[WebCore Asset Self-Test] PASS case=" + name);
        }

        private static void TestValidTree(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string manifest = Path.Combine(root, "manifest.xml");
            string assets = Path.Combine(root, "assets");
            WriteAssetTree(assets, files);
            WriteManifest(manifest, new string('0', 64), files);
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid, "approved local tree was rejected");
        }

        private static void TestMissingManifest(string root)
        {
            Require(!WebCoreAssetManager.ValidateAssets(Path.Combine(root, "assets"), Path.Combine(root, "missing.xml")).IsValid,
                "missing manifest was accepted");
        }

        private static void TestMissingFile(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string manifest = Path.Combine(root, "manifest.xml");
            string assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(assets);
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid, "missing expected file was accepted");
        }

        private static void TestModifiedFile(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string manifest = Path.Combine(root, "manifest.xml");
            string assets = Path.Combine(root, "assets");
            WriteAssetTree(assets, files);
            File.WriteAllText(Path.Combine(assets, "index.php"), "changed");
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid, "modified file was accepted");
        }

        private static void TestUnexpectedFile(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string manifest = Path.Combine(root, "manifest.xml");
            string assets = Path.Combine(root, "assets");
            WriteAssetTree(assets, files);
            File.WriteAllText(Path.Combine(assets, "unexpected.php"), "unexpected");
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid, "unexpected file was accepted");
        }

        private static void TestManifestTraversal(string root)
        {
            IList<WebCoreFixtureFile> files = new[] { FixtureFile("../escape.php", "x") };
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(Path.Combine(root, "assets"), manifest).IsValid,
                "manifest traversal was accepted");
        }

        private static void TestManifestCaseCollision(string root)
        {
            IList<WebCoreFixtureFile> files = new[]
            {
                FixtureFile("Index.php", "a"),
                FixtureFile("index.php", "b")
            };
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(Path.Combine(root, "assets"), manifest).IsValid,
                "manifest case collision was accepted");
        }

        private static void TestManifestFileDirectoryCollision(string root)
        {
            IList<WebCoreFixtureFile> files = new[]
            {
                FixtureFile("admin", "a"),
                FixtureFile("admin/index.php", "b")
            };
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, new string('0', 64), files);
            Require(!WebCoreAssetManager.ValidateAssets(Path.Combine(root, "assets"), manifest).IsValid,
                "manifest file/directory collision was accepted");
        }

        private static void TestManifestFileSizeBound(string root)
        {
            string manifest = Path.Combine(root, "manifest.xml");
            XDocument document = new XDocument(
                new XElement(
                    "WebCoreAssetManifest",
                    new XAttribute("SchemaVersion", "1"),
                    new XAttribute("Id", FixtureId),
                    new XAttribute("UpstreamRepository", "https://example.invalid/fixture"),
                    new XAttribute("UpstreamCommit", FixtureCommit),
                    new XAttribute("ArchiveSha256", new string('0', 64)),
                    new XAttribute("ArchiveRoot", FixtureRoot),
                    new XAttribute("LicenseStatus", "test-only"),
                    new XElement(
                        "File",
                        new XAttribute("Path", "index.php"),
                        new XAttribute("Size", "134217729"),
                        new XAttribute("Sha256", new string('0', 64)))));
            document.Save(manifest);
            Require(!WebCoreAssetManager.ValidateAssets(Path.Combine(root, "assets"), manifest).IsValid,
                "manifest entry beyond the 128 MiB bound was accepted");
        }

        private static void TestValidImport(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(archive, ArchiveEntries(files));
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);

            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive, FixtureCommit, assets, manifest, null);
            Require(result.IsValid, "valid offline import failed: " + result.Message);
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid, "imported tree failed validation");
            Require(!HasTemporarySibling(assets), "successful import left a staging or backup tree");
        }

        private static void TestExclusiveRuntimeLease(string root)
        {
            bool secondLeaseBlocked = false;
            using (IDisposable firstLease = WebCoreAssetManager.AcquireRuntimeLease())
            {
                try
                {
                    using (IDisposable secondLease = WebCoreAssetManager.AcquireRuntimeLease())
                    {
                    }
                }
                catch (InvalidDataException)
                {
                    secondLeaseBlocked = true;
                }
            }

            Require(secondLeaseBlocked, "a concurrent WebCore runtime/import lease was accepted");
            Require(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebCoreAssets.runtime.lock")),
                "runtime lease file remained after disposal");
        }

        private static void TestWrongVersionPreservesTree(string root)
        {
            ImportFailurePreservesTree(root, "wrong-version", null, null);
        }

        private static void TestManifestIdVersionPreservesTree(string root)
        {
            ImportFailurePreservesTree(root, FixtureId, null, null);
        }

        private static void TestArchiveHashPreservesTree(string root)
        {
            ImportFailurePreservesTree(root, FixtureCommit, new string('f', 64), null);
        }

        private static void TestRemoteArchivePath(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, new string('0', 64), files);
            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                "https://example.invalid/assets.zip",
                FixtureCommit,
                Path.Combine(root, "live"),
                manifest,
                null);
            Require(!result.IsValid, "remote archive path was accepted");
        }

        private static void TestUnsafeArchivePath(string root, string unsafePath)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(unsafePath, "bad"));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestDuplicateArchiveEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(FixtureRoot + "/index.php", "fixture-index"));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestCaseCollisionArchiveEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(FixtureRoot + "/INDEX.PHP", "fixture-index"));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestDirectoryCaseCollisionArchiveEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(FixtureRoot + "/ADMIN/", string.Empty));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestArchiveFileDirectoryCollision(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(FixtureRoot + "/index.php/child", "bad"));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestOversizedEntry(string root)
        {
            IList<WebCoreFixtureFile> files = new[] { FixtureFile("index.php", "a") };
            IList<WebCoreFixtureArchiveEntry> entries = new[]
            {
                ArchiveEntry(FixtureRoot + "/index.php", "ab")
            };
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestUnexpectedEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries.Add(ArchiveEntry(FixtureRoot + "/extra.php", "extra"));
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestMissingEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = new[]
            {
                ArchiveEntry(FixtureRoot + "/index.php", "fixture-index")
            };
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestEntryHashMismatch(string root)
        {
            IList<WebCoreFixtureFile> files = new[] { FixtureFile("index.php", "a") };
            IList<WebCoreFixtureArchiveEntry> entries = new[]
            {
                ArchiveEntry(FixtureRoot + "/index.php", "b")
            };
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestCorruptArchive(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string archive = Path.Combine(root, "assets.zip");
            File.WriteAllBytes(archive, Encoding.ASCII.GetBytes("not-a-zip"));
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive, FixtureCommit, Path.Combine(root, "live"), manifest, null);
            Require(!result.IsValid, "corrupt archive was accepted");
        }

        private static void TestSymlinkEntry(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            entries[0].ExternalAttributes = unchecked((int)0xA1FF0000);
            RequireRejectedArchive(root, files, entries, null);
        }

        private static void TestActivationFailurePreservesTree(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(archive, ArchiveEntries(files));
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);

            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive,
                FixtureCommit,
                assets,
                manifest,
                delegate { throw new IOException("injected activation failure"); });
            Require(!result.IsValid, "injected activation failure was accepted");
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid,
                "activation failure did not preserve the prior valid tree");
            Require(!HasTemporarySibling(assets), "activation rollback left a staging or backup tree");
        }

        private static void TestFailedImportLeavesNoTemporaryTree(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            IList<WebCoreFixtureArchiveEntry> entries = ArchiveEntries(files);
            WebCoreFixtureArchiveEntry indexEntry = entries.First(
                entry => entry.Path.EndsWith("/index.php", StringComparison.Ordinal));
            indexEntry.Content = Enumerable.Repeat((byte)'x', indexEntry.Content.Length).ToArray();
            RequireRejectedArchive(root, files, entries, null);
            Require(!HasTemporarySibling(Path.Combine(root, "live")),
                "failed archive validation left a staging or backup tree");
        }

        private static void TestActivationCollisionRetainsBackup(string root)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(archive, ArchiveEntries(files));
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);

            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive,
                FixtureCommit,
                assets,
                manifest,
                delegate
                {
                    Directory.CreateDirectory(assets);
                    File.WriteAllText(Path.Combine(assets, "collision.txt"), "external collision");
                });
            Require(!result.IsValid, "activation collision was accepted");
            string[] backups = Directory.GetDirectories(
                root,
                "live.backup-*",
                SearchOption.TopDirectoryOnly);
            Require(backups.Length == 1, "activation collision did not retain exactly one recoverable prior backup");
            Require(WebCoreAssetManager.ValidateAssets(backups[0], manifest).IsValid,
                "retained activation backup is not the prior valid tree");
        }

        private static void TestBackupCleanupFailureReported(string root)
        {
            IList<WebCoreFixtureFile> files = new[] { FixtureFile("index.php", "content") };
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(
                archive,
                new[]
                {
                    ArchiveEntry(FixtureRoot + "/", string.Empty),
                    ArchiveEntry(FixtureRoot + "/index.php", "content")
                });
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);
            FileStream lockedBackupFile = null;

            WebCoreAssetResult result;
            try
            {
                result = WebCoreAssetManager.ImportArchive(
                    archive,
                    FixtureCommit,
                    assets,
                    manifest,
                    delegate
                    {
                        string backup = Directory.GetDirectories(
                            root,
                            "live.backup-*",
                            SearchOption.TopDirectoryOnly).Single();
                        lockedBackupFile = new FileStream(
                            Path.Combine(backup, "index.php"),
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read);
                    });
            }
            finally
            {
                if (lockedBackupFile != null)
                {
                    lockedBackupFile.Dispose();
                }
            }

            Require(!result.IsValid, "incomplete prior-tree cleanup was reported as PASS");
            Require(result.Message.IndexOf("prior-tree cleanup failed", StringComparison.Ordinal) >= 0,
                "cleanup failure did not report the retained-backup boundary: " + result.Message);
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid,
                "cleanup failure invalidated the newly active tree");
            string[] backups = Directory.GetDirectories(root, "live.backup-*", SearchOption.TopDirectoryOnly);
            Require(backups.Length == 1, "cleanup failure did not retain one recoverable backup");
            Require(WebCoreAssetManager.ValidateAssets(backups[0], manifest).IsValid,
                "cleanup failure did not retain the prior valid tree");
            Directory.Delete(backups[0], true);
            Require(!HasTemporarySibling(assets), "cleanup-failure test left a temporary backup");
        }

        private static void ImportFailurePreservesTree(
            string root,
            string requestedVersion,
            string manifestArchiveHash,
            Action beforeActivation)
        {
            IList<WebCoreFixtureFile> files = DefaultFiles();
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(archive, ArchiveEntries(files));
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, manifestArchiveHash ?? ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);

            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive, requestedVersion, assets, manifest, beforeActivation);
            Require(!result.IsValid, "invalid import was accepted");
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid,
                "failed import changed the prior valid asset tree");
        }

        private static void RequireRejectedArchive(
            string root,
            IList<WebCoreFixtureFile> files,
            IList<WebCoreFixtureArchiveEntry> entries,
            Action beforeActivation)
        {
            string archive = Path.Combine(root, "assets.zip");
            CreateArchive(archive, entries);
            string manifest = Path.Combine(root, "manifest.xml");
            WriteManifest(manifest, ComputeSha256(archive), files);
            string assets = Path.Combine(root, "live");
            WriteAssetTree(assets, files);

            WebCoreAssetResult result = WebCoreAssetManager.ImportArchive(
                archive, FixtureCommit, assets, manifest, beforeActivation);
            Require(!result.IsValid, "unsafe archive was accepted");
            Require(WebCoreAssetManager.ValidateAssets(assets, manifest).IsValid,
                "rejected archive changed the existing valid asset tree");
        }

        private static IList<WebCoreFixtureFile> DefaultFiles()
        {
            return new[]
            {
                FixtureFile("admin/panel.php", "fixture-admin"),
                FixtureFile("index.php", "fixture-index")
            };
        }

        private static WebCoreFixtureFile FixtureFile(string path, string content)
        {
            return new WebCoreFixtureFile
            {
                Path = path,
                Content = Encoding.UTF8.GetBytes(content)
            };
        }

        private static WebCoreFixtureArchiveEntry ArchiveEntry(string path, string content)
        {
            return new WebCoreFixtureArchiveEntry
            {
                Path = path,
                Content = Encoding.UTF8.GetBytes(content)
            };
        }

        private static IList<WebCoreFixtureArchiveEntry> ArchiveEntries(IEnumerable<WebCoreFixtureFile> files)
        {
            List<WebCoreFixtureArchiveEntry> entries = new List<WebCoreFixtureArchiveEntry>
            {
                ArchiveEntry(FixtureRoot + "/", string.Empty),
                ArchiveEntry(FixtureRoot + "/admin/", string.Empty)
            };
            entries.AddRange(
                files.Select(
                    file => new WebCoreFixtureArchiveEntry
                    {
                        Path = FixtureRoot + "/" + file.Path,
                        Content = file.Content
                    }));
            return entries;
        }

        private static void WriteAssetTree(string root, IEnumerable<WebCoreFixtureFile> files)
        {
            Directory.CreateDirectory(root);
            foreach (WebCoreFixtureFile file in files)
            {
                string path = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, file.Content);
            }
        }

        private static void CreateArchive(string archivePath, IEnumerable<WebCoreFixtureArchiveEntry> entries)
        {
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (WebCoreFixtureArchiveEntry fixtureEntry in entries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(fixtureEntry.Path, CompressionLevel.Optimal);
                    if (fixtureEntry.ExternalAttributes.HasValue)
                    {
                        entry.ExternalAttributes = fixtureEntry.ExternalAttributes.Value;
                    }

                    using (Stream stream = entry.Open())
                    {
                        stream.Write(fixtureEntry.Content, 0, fixtureEntry.Content.Length);
                    }
                }
            }
        }

        private static void WriteManifest(
            string manifestPath,
            string archiveSha256,
            IEnumerable<WebCoreFixtureFile> fixtureFiles)
        {
            IEnumerable<XElement> files = fixtureFiles
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(
                    file => new XElement(
                        "File",
                        new XAttribute("Path", file.Path),
                        new XAttribute("Size", file.Content.LongLength.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("Sha256", ComputeSha256(file.Content))));
            XDocument document = new XDocument(
                new XElement(
                    "WebCoreAssetManifest",
                    new XAttribute("SchemaVersion", "1"),
                    new XAttribute("Id", FixtureId),
                    new XAttribute("UpstreamRepository", "https://example.invalid/fixture"),
                    new XAttribute("UpstreamCommit", FixtureCommit),
                    new XAttribute("ArchiveSha256", archiveSha256),
                    new XAttribute("ArchiveRoot", FixtureRoot),
                    new XAttribute("LicenseStatus", "test-only"),
                    files));
            document.Save(manifestPath);
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(stream));
            }
        }

        private static string ComputeSha256(byte[] content)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(content));
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static bool HasTemporarySibling(string assetRoot)
        {
            string parent = Path.GetDirectoryName(assetRoot);
            string name = Path.GetFileName(assetRoot);
            return Directory.GetDirectories(parent, name + ".staging-*", SearchOption.TopDirectoryOnly).Length != 0
                   || Directory.GetDirectories(parent, name + ".backup-*", SearchOption.TopDirectoryOnly).Length != 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
