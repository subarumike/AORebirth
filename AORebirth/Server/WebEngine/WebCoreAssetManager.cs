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
    using System.Xml;
    using System.Xml.Linq;

    using _config = Utility.Config.ConfigReadWrite;

    public sealed class WebCoreAssetResult
    {
        internal WebCoreAssetResult(bool isValid, string message, string assetRoot, string manifestId)
        {
            this.IsValid = isValid;
            this.Message = message;
            this.AssetRoot = assetRoot;
            this.ManifestId = manifestId;
        }

        public bool IsValid { get; private set; }

        public string Message { get; private set; }

        public string AssetRoot { get; private set; }

        public string ManifestId { get; private set; }
    }

    internal sealed class WebCoreManifestEntry
    {
        public string Path { get; set; }

        public long Size { get; set; }

        public string Sha256 { get; set; }
    }

    internal sealed class WebCoreManifest
    {
        public string Id { get; set; }

        public string UpstreamRepository { get; set; }

        public string UpstreamCommit { get; set; }

        public string ArchiveSha256 { get; set; }

        public string ArchiveRoot { get; set; }

        public string LicenseStatus { get; set; }

        public IList<WebCoreManifestEntry> Entries { get; set; }

        public IDictionary<string, WebCoreManifestEntry> EntryByPath { get; set; }

        public ISet<string> ExpectedDirectories { get; set; }

        public long TotalSize { get; set; }
    }

    internal sealed class WebCoreArchivePlan
    {
        public ZipArchiveEntry ArchiveEntry { get; set; }

        public WebCoreManifestEntry ManifestEntry { get; set; }
    }

    internal sealed class WebCoreAssetLease : IDisposable
    {
        private FileStream stream;

        public WebCoreAssetLease(FileStream stream)
        {
            this.stream = stream;
        }

        public void Dispose()
        {
            if (this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }
        }
    }

    public static class WebCoreAssetManager
    {
        internal const string ManifestFileName = "WebCoreAssets.manifest.xml";

        private const int MaximumFileCount = 100000;
        private const long MaximumFileSize = 128L * 1024L * 1024L;
        private const long MaximumTotalSize = 1024L * 1024L * 1024L;
        private const long MaximumArchiveSize = 512L * 1024L * 1024L;
        private const string RuntimeLeaseFileName = "WebCoreAssets.runtime.lock";
        private const string ApprovedManifestId = "cellao-webcore-765c3850767b";
        private const string ApprovedUpstreamRepository = "https://github.com/CellAO/CellAO-WebCore";
        private const string ApprovedUpstreamCommit = "765c3850767b63af1cd259bab7f2f7ca3e97adf9";
        private const string ApprovedArchiveSha256 = "ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab";
        private const string ApprovedArchiveRoot = "CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9";
        private const string ApprovedLicenseStatus = "unresolved-no-upstream-license-file";
        private const int ApprovedFileCount = 7140;
        private const long ApprovedTotalSize = 26648501L;

        public static WebCoreAssetResult ValidateConfiguredAssets()
        {
            try
            {
                string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                string assetRoot = ResolveLocalAssetRoot(
                    _config.Instance.CurrentConfig.WebHostRoot,
                    baseDirectory);
                WebCoreManifest manifest = WebCoreCompatibilityManager.LoadValidatedAuthority(baseDirectory);
                string canonicalRoot = CanonicalDirectoryPath(assetRoot);
                ValidateAssetTree(canonicalRoot, manifest);
                return Success(
                    "WebCore assets PASS: root=" + canonicalRoot + " manifest=" + manifest.Id,
                    canonicalRoot,
                    manifest.Id);
            }
            catch (Exception exception)
            {
                return Failure("WebCore assets FAIL: " + SafeMessage(exception), null, null);
            }
        }

        public static WebCoreAssetResult ImportConfiguredArchive(
            string archivePath,
            string expectedVersion,
            string pythonExecutable,
            string compatibilityToolPath)
        {
            try
            {
                string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                using (IDisposable lease = AcquireExclusiveLease(baseDirectory))
                {
                    string assetRoot = ResolveLocalAssetRoot(
                        _config.Instance.CurrentConfig.WebHostRoot,
                        baseDirectory);
                    string manifestPath = Path.Combine(baseDirectory, ManifestFileName);
                    return ImportArchive(
                        archivePath,
                        expectedVersion,
                        assetRoot,
                        manifestPath,
                        null,
                        true,
                        pythonExecutable,
                        compatibilityToolPath);
                }
            }
            catch (Exception exception)
            {
                return Failure("WebCore import FAIL: " + SafeMessage(exception), null, null);
            }
        }

        public static IDisposable AcquireRuntimeLease()
        {
            return AcquireExclusiveLease(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory));
        }

        public static WebCoreAssetResult ValidateConfiguredManifest()
        {
            try
            {
                string manifestPath = Path.Combine(
                    Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                    ManifestFileName);
                WebCoreManifest manifest = LoadManifest(manifestPath);
                EnsureApprovedManifestAuthority(manifest);
                return Success(
                    "WebCore manifest PASS: manifest=" + manifest.Id
                    + " commit=" + manifest.UpstreamCommit
                    + " files=" + manifest.Entries.Count.ToString(CultureInfo.InvariantCulture)
                    + " bytes=" + manifest.TotalSize.ToString(CultureInfo.InvariantCulture),
                    null,
                    manifest.Id);
            }
            catch (Exception exception)
            {
                return Failure("WebCore manifest FAIL: " + SafeMessage(exception), null, null);
            }
        }

        internal static WebCoreAssetResult ValidateAssets(
            string assetRoot,
            string manifestPath,
            bool requireApprovedAuthority = false)
        {
            try
            {
                WebCoreManifest manifest = LoadManifest(manifestPath);
                if (requireApprovedAuthority)
                {
                    EnsureApprovedManifestAuthority(manifest);
                }

                string canonicalRoot = CanonicalDirectoryPath(assetRoot);
                ValidateAssetTree(canonicalRoot, manifest);
                return Success(
                    "WebCore assets PASS: root=" + canonicalRoot + " manifest=" + manifest.Id,
                    canonicalRoot,
                    manifest.Id);
            }
            catch (Exception exception)
            {
                return Failure("WebCore assets FAIL: " + SafeMessage(exception), assetRoot, null);
            }
        }

        internal static WebCoreAssetResult ImportArchive(
            string archivePath,
            string expectedVersion,
            string assetRoot,
            string manifestPath,
            Action beforeActivation,
            bool requireApprovedAuthority = false,
            string pythonExecutable = null,
            string compatibilityToolPath = null)
        {
            string stagingDirectory = null;
            string backupDirectory = null;
            string canonicalRoot = null;
            bool previousTreeMoved = false;

            try
            {
                WebCoreManifest manifest = LoadManifest(manifestPath);
                if (requireApprovedAuthority)
                {
                    EnsureApprovedManifestAuthority(manifest);
                }

                if (!string.Equals(expectedVersion, manifest.UpstreamCommit, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The requested WebCore version does not match the approved manifest.");
                }

                canonicalRoot = CanonicalDirectoryPath(assetRoot);
                string canonicalArchive = ResolveLocalArchivePath(archivePath, canonicalRoot);
                string parentDirectory = Path.GetDirectoryName(canonicalRoot.TrimEnd(Path.DirectorySeparatorChar));
                string targetName = Path.GetFileName(canonicalRoot.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentDirectory) || string.IsNullOrEmpty(targetName))
                {
                    throw new InvalidDataException("The configured WebCore asset root cannot be replaced safely.");
                }

                EnsureExistingAncestorsHaveNoReparsePoints(parentDirectory);
                Directory.CreateDirectory(parentDirectory);
                EnsureExistingAncestorsHaveNoReparsePoints(parentDirectory);

                string operationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                stagingDirectory = Path.Combine(parentDirectory, targetName + ".staging-" + operationId);
                backupDirectory = Path.Combine(parentDirectory, targetName + ".backup-" + operationId);

                using (FileStream archiveStream = new FileStream(
                    canonicalArchive,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    if (archiveStream.Length > MaximumArchiveSize)
                    {
                        throw new InvalidDataException("The local WebCore archive exceeds the bounded archive size.");
                    }

                    string archiveHash = ComputeStreamSha256(archiveStream);
                    if (!string.Equals(archiveHash, manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("The local WebCore archive SHA-256 does not match the approved manifest.");
                    }

                    archiveStream.Position = 0;
                    using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true))
                    {
                        IList<WebCoreArchivePlan> plans = ValidateArchiveInventory(archive, manifest);
                        Directory.CreateDirectory(stagingDirectory);
                        ExtractPlans(plans, stagingDirectory);
                    }
                }

                ValidateAssetTree(CanonicalDirectoryPath(stagingDirectory), manifest);

                if (requireApprovedAuthority)
                {
                    if (string.IsNullOrWhiteSpace(pythonExecutable)
                        || string.IsNullOrWhiteSpace(compatibilityToolPath))
                    {
                        throw new InvalidDataException(
                            "Production WebCore import requires the approved compatibility tool and a local Python interpreter.");
                    }

                    WebCoreCompatibilityManager.ApplyWithApprovedTool(
                        stagingDirectory,
                        pythonExecutable,
                        compatibilityToolPath,
                        Path.GetDirectoryName(Path.GetFullPath(manifestPath)));
                    WebCoreManifest finalManifest = WebCoreCompatibilityManager.LoadValidatedAuthority(
                        Path.GetDirectoryName(Path.GetFullPath(manifestPath)));
                    ValidateAssetTree(CanonicalDirectoryPath(stagingDirectory), finalManifest);
                    manifest = finalManifest;
                }

                if (Directory.Exists(canonicalRoot))
                {
                    EnsureTreeHasNoReparsePoints(canonicalRoot);
                    Directory.Move(canonicalRoot, backupDirectory);
                    previousTreeMoved = true;
                }

                if (beforeActivation != null)
                {
                    beforeActivation();
                }

                Directory.Move(stagingDirectory, canonicalRoot);
                stagingDirectory = null;

                if (previousTreeMoved)
                {
                    try
                    {
                        Directory.Delete(backupDirectory, true);
                        backupDirectory = null;
                    }
                    catch
                    {
                        // Recursive deletion can fail after partially removing the backup.
                        // The fully validated new root is already atomically active, so never
                        // replace it with a possibly partial backup.
                        return Failure(
                            "WebCore import FAIL: validated new assets are active, but prior-tree cleanup failed; a retained backup requires manual review.",
                            canonicalRoot,
                            manifest.Id);
                    }
                }

                return Success(
                    "WebCore import PASS: root=" + canonicalRoot + " manifest=" + manifest.Id,
                    canonicalRoot,
                    manifest.Id);
            }
            catch (Exception exception)
            {
                string rollbackError = null;
                try
                {
                    if (previousTreeMoved
                        && !string.IsNullOrEmpty(canonicalRoot)
                        && !Directory.Exists(canonicalRoot)
                        && Directory.Exists(backupDirectory))
                    {
                        Directory.Move(backupDirectory, canonicalRoot);
                        backupDirectory = null;
                    }
                }
                catch (Exception rollbackException)
                {
                    rollbackError = SafeMessage(rollbackException);
                }

                DeleteTemporaryDirectory(stagingDirectory);

                string message = "WebCore import FAIL: " + SafeMessage(exception);
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    message += " Rollback failed: " + rollbackError;
                }

                return Failure(message, assetRoot, null);
            }
        }

        private static void EnsureApprovedManifestAuthority(WebCoreManifest manifest)
        {
            if (!string.Equals(manifest.Id, ApprovedManifestId, StringComparison.Ordinal)
                || !string.Equals(manifest.UpstreamRepository, ApprovedUpstreamRepository, StringComparison.Ordinal)
                || !string.Equals(manifest.UpstreamCommit, ApprovedUpstreamCommit, StringComparison.Ordinal)
                || !string.Equals(manifest.ArchiveSha256, ApprovedArchiveSha256, StringComparison.Ordinal)
                || !string.Equals(manifest.ArchiveRoot, ApprovedArchiveRoot, StringComparison.Ordinal)
                || !string.Equals(manifest.LicenseStatus, ApprovedLicenseStatus, StringComparison.Ordinal)
                || manifest.Entries.Count != ApprovedFileCount
                || manifest.TotalSize != ApprovedTotalSize)
            {
                throw new InvalidDataException("The WebCore manifest does not match the repository-approved authority.");
            }
        }

        internal static WebCoreManifest LoadManifest(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                throw new FileNotFoundException("The checked-in WebCore asset manifest is missing.");
            }

            EnsureNotReparsePoint(manifestPath, "The WebCore asset manifest is a reparse point.");
            EnsureExistingAncestorsHaveNoReparsePoints(Path.GetDirectoryName(Path.GetFullPath(manifestPath)));

            XDocument document;
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (XmlReader reader = XmlReader.Create(manifestPath, settings))
            {
                document = XDocument.Load(reader, LoadOptions.None);
            }

            XElement root = document.Root;
            if (root == null || root.Name != "WebCoreAssetManifest")
            {
                throw new InvalidDataException("The WebCore manifest root is invalid.");
            }

            string[] allowedRootAttributes =
            {
                "SchemaVersion", "Id", "UpstreamRepository", "UpstreamCommit",
                "ArchiveSha256", "ArchiveRoot", "LicenseStatus"
            };
            ValidateAttributeSet(root, allowedRootAttributes, "WebCore manifest");
            if (RequiredAttribute(root, "SchemaVersion") != "1")
            {
                throw new InvalidDataException("The WebCore manifest schema version is unsupported.");
            }

            WebCoreManifest manifest = new WebCoreManifest
            {
                Id = RequiredAttribute(root, "Id"),
                UpstreamRepository = RequiredAttribute(root, "UpstreamRepository"),
                UpstreamCommit = RequiredAttribute(root, "UpstreamCommit"),
                ArchiveSha256 = RequiredAttribute(root, "ArchiveSha256").ToLowerInvariant(),
                ArchiveRoot = RequiredAttribute(root, "ArchiveRoot"),
                LicenseStatus = RequiredAttribute(root, "LicenseStatus"),
                Entries = new List<WebCoreManifestEntry>(),
                EntryByPath = new Dictionary<string, WebCoreManifestEntry>(StringComparer.OrdinalIgnoreCase),
                ExpectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            if (!IsSafeIdentifier(manifest.Id)
                || !IsLowerHex(manifest.UpstreamCommit, 40)
                || !IsLowerHex(manifest.ArchiveSha256, 64)
                || string.IsNullOrWhiteSpace(manifest.UpstreamRepository)
                || string.IsNullOrWhiteSpace(manifest.LicenseStatus))
            {
                throw new InvalidDataException("The WebCore manifest provenance fields are invalid.");
            }

            string normalizedArchiveRoot = ValidateRelativePath(manifest.ArchiveRoot);
            if (normalizedArchiveRoot.IndexOf('/') >= 0)
            {
                throw new InvalidDataException("The WebCore manifest archive root must be one directory.");
            }

            string previousPath = null;
            foreach (XElement fileElement in root.Elements())
            {
                if (fileElement.Name != "File"
                    || fileElement.HasElements
                    || !string.IsNullOrWhiteSpace(fileElement.Value))
                {
                    throw new InvalidDataException("The WebCore manifest contains an unexpected element.");
                }

                ValidateAttributeSet(fileElement, new[] { "Path", "Size", "Sha256" }, "WebCore file entry");
                string path = ValidateRelativePath(RequiredAttribute(fileElement, "Path"));
                if (previousPath != null && string.CompareOrdinal(previousPath, path) >= 0)
                {
                    throw new InvalidDataException("The WebCore manifest file inventory is not uniquely ordinal-sorted.");
                }

                long size;
                if (!long.TryParse(
                    RequiredAttribute(fileElement, "Size"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out size)
                    || size < 0
                    || size > MaximumFileSize)
                {
                    throw new InvalidDataException("A WebCore manifest file size is invalid.");
                }

                string sha256 = RequiredAttribute(fileElement, "Sha256").ToLowerInvariant();
                if (!IsLowerHex(sha256, 64))
                {
                    throw new InvalidDataException("A WebCore manifest file SHA-256 is invalid.");
                }

                WebCoreManifestEntry entry = new WebCoreManifestEntry
                {
                    Path = path,
                    Size = size,
                    Sha256 = sha256
                };
                if (manifest.EntryByPath.ContainsKey(path))
                {
                    throw new InvalidDataException("The WebCore manifest contains a duplicate or case-colliding path.");
                }

                manifest.Entries.Add(entry);
                manifest.EntryByPath.Add(path, entry);
                AddExpectedDirectories(manifest.ExpectedDirectories, path);
                checked
                {
                    manifest.TotalSize += size;
                }

                if (manifest.TotalSize > MaximumTotalSize || manifest.Entries.Count > MaximumFileCount)
                {
                    throw new InvalidDataException("The WebCore manifest exceeds the bounded inventory policy.");
                }

                previousPath = path;
            }

            if (manifest.Entries.Count == 0)
            {
                throw new InvalidDataException("The WebCore manifest contains no approved files.");
            }

            if (manifest.Entries.Any(entry => manifest.ExpectedDirectories.Contains(entry.Path)))
            {
                throw new InvalidDataException("The WebCore manifest contains a file/directory collision.");
            }

            if (root.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value)))
            {
                throw new InvalidDataException("The WebCore manifest contains unexpected text.");
            }

            return manifest;
        }

        internal static void ValidateAssetTree(string canonicalRoot, WebCoreManifest manifest)
        {
            EnsureExistingAncestorsHaveNoReparsePoints(canonicalRoot);
            if (!Directory.Exists(canonicalRoot))
            {
                throw new DirectoryNotFoundException("The configured WebCore asset root is missing.");
            }

            string rootPrefix = AddDirectorySeparator(canonicalRoot);
            HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IList<string> directories = EnumerateDirectoriesWithoutReparsePoints(canonicalRoot);

            foreach (string directory in directories)
            {
                string relativeDirectory = GetContainedRelativePath(rootPrefix, directory);
                string normalizedDirectory = ValidateRelativePath(relativeDirectory);
                if (!manifest.ExpectedDirectories.Contains(normalizedDirectory))
                {
                    throw new InvalidDataException("Unexpected WebCore directory: " + normalizedDirectory);
                }
            }

            IEnumerable<string> files = new[] { canonicalRoot }
                .Concat(directories)
                .SelectMany(directory => Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly));
            foreach (string file in files)
            {
                EnsureNotReparsePoint(file, "A WebCore asset file is a reparse point.");
                string relativePath = ValidateRelativePath(GetContainedRelativePath(rootPrefix, file));
                WebCoreManifestEntry expected;
                if (!manifest.EntryByPath.TryGetValue(relativePath, out expected))
                {
                    throw new InvalidDataException("Unexpected WebCore file: " + relativePath);
                }

                if (!string.Equals(relativePath, expected.Path, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A WebCore file path differs by case from the approved manifest: " + relativePath);
                }

                if (!seenFiles.Add(relativePath))
                {
                    throw new InvalidDataException("The WebCore asset tree contains a duplicate or case-colliding file.");
                }

                FileInfo info = new FileInfo(file);
                if (info.Length != expected.Size)
                {
                    throw new InvalidDataException("WebCore file size mismatch: " + relativePath);
                }

                string sha256 = ComputeFileSha256(file);
                if (!string.Equals(sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("WebCore file SHA-256 mismatch: " + relativePath);
                }
            }

            if (seenFiles.Count != manifest.Entries.Count)
            {
                WebCoreManifestEntry missing = manifest.Entries.First(entry => !seenFiles.Contains(entry.Path));
                throw new InvalidDataException("Missing WebCore file: " + missing.Path);
            }
        }

        private static IList<WebCoreArchivePlan> ValidateArchiveInventory(
            ZipArchive archive,
            WebCoreManifest manifest)
        {
            List<WebCoreArchivePlan> plans = new List<WebCoreArchivePlan>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string rootPrefix = manifest.ArchiveRoot + "/";
            bool sawRootDirectory = false;

            foreach (ZipArchiveEntry archiveEntry in archive.Entries)
            {
                RejectArchiveLinkOrSpecialEntry(archiveEntry);
                string fullName = archiveEntry.FullName;
                if (string.Equals(fullName, rootPrefix, StringComparison.Ordinal))
                {
                    if (sawRootDirectory)
                    {
                        throw new InvalidDataException("The WebCore archive contains a duplicate root directory.");
                    }

                    sawRootDirectory = true;
                    continue;
                }

                if (string.IsNullOrEmpty(fullName)
                    || fullName.IndexOf('\\') >= 0
                    || !fullName.StartsWith(rootPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The WebCore archive contains an unsafe or unexpected root path.");
                }

                string relativePath = fullName.Substring(rootPrefix.Length);
                bool isDirectory = relativePath.EndsWith("/", StringComparison.Ordinal);
                if (isDirectory)
                {
                    relativePath = relativePath.Substring(0, relativePath.Length - 1);
                    if (relativePath.Length == 0)
                    {
                        continue;
                    }

                    string normalizedDirectory = ValidateRelativePath(relativePath);
                    if (!manifest.ExpectedDirectories.Contains(normalizedDirectory)
                        || !seenDirectories.Add(normalizedDirectory))
                    {
                        throw new InvalidDataException("The WebCore archive contains an unexpected or duplicate directory.");
                    }

                    continue;
                }

                string normalizedPath = ValidateRelativePath(relativePath);
                WebCoreManifestEntry expected;
                if (!manifest.EntryByPath.TryGetValue(normalizedPath, out expected))
                {
                    throw new InvalidDataException("The WebCore archive contains an unexpected file: " + normalizedPath);
                }

                if (!string.Equals(normalizedPath, expected.Path, StringComparison.Ordinal)
                    || !seen.Add(normalizedPath))
                {
                    throw new InvalidDataException("The WebCore archive contains a duplicate, case-colliding, or mismatched file path.");
                }

                if (archiveEntry.Length != expected.Size || archiveEntry.Length > MaximumFileSize)
                {
                    throw new InvalidDataException("A WebCore archive entry size does not match the approved manifest.");
                }

                plans.Add(new WebCoreArchivePlan
                {
                    ArchiveEntry = archiveEntry,
                    ManifestEntry = expected
                });
            }

            if (plans.Count != manifest.Entries.Count)
            {
                throw new InvalidDataException("The WebCore archive is missing one or more approved files.");
            }

            return plans;
        }

        private static void ExtractPlans(IList<WebCoreArchivePlan> plans, string stagingDirectory)
        {
            string stagingRoot = AddDirectorySeparator(Path.GetFullPath(stagingDirectory));
            byte[] buffer = new byte[81920];

            foreach (WebCoreArchivePlan plan in plans)
            {
                string localRelativePath = plan.ManifestEntry.Path.Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.GetFullPath(Path.Combine(stagingRoot, localRelativePath));
                if (!destinationPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("A WebCore archive entry escapes the staging root.");
                }

                string destinationParent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationParent))
                {
                    Directory.CreateDirectory(destinationParent);
                }

                long written = 0;
                using (Stream input = plan.ArchiveEntry.Open())
                using (FileStream output = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (SHA256 sha256 = SHA256.Create())
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        written += read;
                        if (written > plan.ManifestEntry.Size)
                        {
                            throw new InvalidDataException("A WebCore archive entry expanded beyond its approved size.");
                        }

                        output.Write(buffer, 0, read);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                    }

                    sha256.TransformFinalBlock(new byte[0], 0, 0);
                    string extractedHash = ToLowerHex(sha256.Hash);
                    if (written != plan.ManifestEntry.Size
                        || !string.Equals(extractedHash, plan.ManifestEntry.Sha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("A WebCore archive entry failed size or SHA-256 validation.");
                    }
                }
            }
        }

        private static string ResolveLocalAssetRoot(string configuredRoot, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot)
                || LooksLikeUri(configuredRoot)
                || IsUncOrDevicePath(configuredRoot))
            {
                throw new InvalidDataException("The configured WebCore asset root must be a local filesystem path.");
            }

            string combined = Path.IsPathRooted(configuredRoot)
                                  ? configuredRoot
                                  : Path.Combine(baseDirectory, configuredRoot);
            string canonicalBase = CanonicalDirectoryPath(baseDirectory);
            string canonicalRoot = CanonicalDirectoryPath(combined);
            if (!canonicalRoot.StartsWith(AddDirectorySeparator(canonicalBase), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The configured WebCore asset root must remain below the WebEngine directory.");
            }

            return canonicalRoot;
        }

        private static string ResolveLocalArchivePath(string archivePath, string assetRoot)
        {
            if (string.IsNullOrWhiteSpace(archivePath)
                || LooksLikeUri(archivePath)
                || IsUncOrDevicePath(archivePath)
                || !string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("WebCore import requires a local ZIP file.");
            }

            string canonicalArchive = Path.GetFullPath(archivePath);
            if (!File.Exists(canonicalArchive))
            {
                throw new FileNotFoundException("The local WebCore archive is missing.");
            }

            EnsureNotReparsePoint(canonicalArchive, "The local WebCore archive is a reparse point.");
            EnsureExistingAncestorsHaveNoReparsePoints(Path.GetDirectoryName(canonicalArchive));
            string rootPrefix = AddDirectorySeparator(CanonicalDirectoryPath(assetRoot));
            if (canonicalArchive.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The local WebCore archive cannot be stored inside the live asset root.");
            }

            return canonicalArchive;
        }

        private static string CanonicalDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || LooksLikeUri(path) || IsUncOrDevicePath(path))
            {
                throw new InvalidDataException("A WebCore path is not a supported local directory.");
            }

            string canonical = Path.GetFullPath(path);
            string volumeRoot = Path.GetPathRoot(canonical);
            if (string.Equals(
                canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                volumeRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A WebCore asset root cannot be a filesystem volume root.");
            }

            return canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string ValidateRelativePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)
                || rawPath.IndexOf('\\') >= 0
                || rawPath.IndexOf('%') >= 0
                || rawPath.IndexOf(':') >= 0
                || rawPath.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(rawPath)
                || !rawPath.IsNormalized(NormalizationForm.FormC))
            {
                throw new InvalidDataException("A WebCore relative path is unsafe.");
            }

            string[] segments = rawPath.Split('/');
            if (segments.Length == 0)
            {
                throw new InvalidDataException("A WebCore relative path is empty.");
            }

            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment)
                    || segment == "."
                    || segment == ".."
                    || segment.EndsWith(" ", StringComparison.Ordinal)
                    || segment.EndsWith(".", StringComparison.Ordinal)
                    || IsReservedWindowsName(segment))
                {
                    throw new InvalidDataException("A WebCore relative path contains an unsafe segment.");
                }

                foreach (char character in segment)
                {
                    if (char.IsControl(character) || Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0)
                    {
                        throw new InvalidDataException("A WebCore relative path contains an invalid character.");
                    }
                }
            }

            return string.Join("/", segments);
        }

        private static void RejectArchiveLinkOrSpecialEntry(ZipArchiveEntry entry)
        {
            int unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
            if (unixMode == 0xA000
                || (unixMode != 0 && unixMode != 0x8000 && unixMode != 0x4000)
                || ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                || (directory && unixMode == 0x8000)
                || (!directory && unixMode == 0x4000))
            {
                throw new InvalidDataException("The WebCore archive contains a link or special entry.");
            }
        }

        private static void EnsureTreeHasNoReparsePoints(string root)
        {
            EnumerateDirectoriesWithoutReparsePoints(root);
        }

        private static IList<string> EnumerateDirectoriesWithoutReparsePoints(string root)
        {
            EnsureNotReparsePoint(root, "The WebCore asset root is a reparse point.");
            List<string> discovered = new List<string>();
            Queue<string> pending = new Queue<string>();
            pending.Enqueue(root);
            while (pending.Count != 0)
            {
                string parent = pending.Dequeue();
                foreach (string directory in Directory.GetDirectories(parent, "*", SearchOption.TopDirectoryOnly))
                {
                    EnsureNotReparsePoint(directory, "A WebCore asset directory is a reparse point.");
                    discovered.Add(directory);
                    pending.Enqueue(directory);
                }
            }

            return discovered;
        }

        private static void EnsureNotReparsePoint(string path, string message)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(message);
            }
        }

        private static void EnsureExistingAncestorsHaveNoReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current))
                {
                    EnsureNotReparsePoint(current, "A WebCore local path contains a reparse-point ancestor.");
                }

                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }

        private static string GetContainedRelativePath(string rootPrefix, string path)
        {
            string canonicalPath = Path.GetFullPath(path);
            if (!canonicalPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A WebCore asset path escapes the configured root.");
            }

            return canonicalPath.Substring(rootPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void AddExpectedDirectories(ISet<string> directories, string filePath)
        {
            int separator = filePath.IndexOf('/');
            while (separator >= 0)
            {
                directories.Add(filePath.Substring(0, separator));
                separator = filePath.IndexOf('/', separator + 1);
            }
        }

        private static void ValidateAttributeSet(XElement element, IEnumerable<string> allowed, string context)
        {
            HashSet<string> expected = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (XAttribute attribute in element.Attributes())
            {
                if (!expected.Remove(attribute.Name.LocalName) || attribute.Name.Namespace != XNamespace.None)
                {
                    throw new InvalidDataException(context + " contains an unexpected or duplicate attribute.");
                }
            }

            if (expected.Count != 0)
            {
                throw new InvalidDataException(context + " is missing a required attribute.");
            }
        }

        private static string RequiredAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                throw new InvalidDataException("The WebCore manifest is missing a required value.");
            }

            return attribute.Value;
        }

        private static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!(character >= 'a' && character <= 'z')
                    && !(character >= '0' && character <= '9')
                    && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsReservedWindowsName(string segment)
        {
            string stem = segment.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL")
            {
                return true;
            }

            if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)))
            {
                return stem[3] >= '1' && stem[3] <= '9';
            }

            return false;
        }

        private static bool LooksLikeUri(string value)
        {
            Uri uri;
            return value.IndexOf("://", StringComparison.Ordinal) >= 0
                   || (Uri.TryCreate(value, UriKind.Absolute, out uri) && !uri.IsFile);
        }

        private static bool IsUncOrDevicePath(string value)
        {
            return value.StartsWith("\\\\", StringComparison.Ordinal)
                   || value.StartsWith("//", StringComparison.Ordinal)
                   || value.StartsWith("\\\\?\\", StringComparison.Ordinal)
                   || value.StartsWith("\\\\.\\", StringComparison.Ordinal);
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(stream));
            }
        }

        private static string ComputeStreamSha256(Stream stream)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(stream));
            }
        }

        private static IDisposable AcquireExclusiveLease(string baseDirectory)
        {
            string canonicalBase = CanonicalDirectoryPath(baseDirectory);
            EnsureExistingAncestorsHaveNoReparsePoints(canonicalBase);
            string leasePath = Path.Combine(canonicalBase, RuntimeLeaseFileName);
            try
            {
                FileStream stream = new FileStream(
                    leasePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
                return new WebCoreAssetLease(stream);
            }
            catch (IOException exception)
            {
                throw new InvalidDataException(
                    "WebCore assets are in use by another runtime or import operation.",
                    exception);
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

        private static string AddDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                       ? path
                       : path + Path.DirectorySeparatorChar;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch
                {
                    // The original import failure remains authoritative.
                }
            }
        }

        private static string SafeMessage(Exception exception)
        {
            if (exception is InvalidDataException
                || exception is IOException
                || exception is UnauthorizedAccessException
                || exception is XmlException
                || exception is CryptographicException)
            {
                return exception.Message;
            }

            return "The local WebCore asset operation failed its security contract.";
        }

        private static WebCoreAssetResult Success(string message, string assetRoot, string manifestId)
        {
            return new WebCoreAssetResult(true, message, assetRoot, manifestId);
        }

        private static WebCoreAssetResult Failure(string message, string assetRoot, string manifestId)
        {
            return new WebCoreAssetResult(false, message, assetRoot, manifestId);
        }
    }
}
