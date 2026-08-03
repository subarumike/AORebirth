namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    internal sealed class WebCoreCompatibilityResult
    {
        private WebCoreCompatibilityResult(bool isValid, string message, WebCoreManifest manifest)
        {
            this.IsValid = isValid;
            this.Message = message;
            this.Manifest = manifest;
        }

        public bool IsValid { get; private set; }

        public WebCoreManifest Manifest { get; private set; }

        public string Message { get; private set; }

        public static WebCoreCompatibilityResult Failed(string message)
        {
            return new WebCoreCompatibilityResult(false, message, null);
        }

        public static WebCoreCompatibilityResult Valid(string message, WebCoreManifest manifest)
        {
            return new WebCoreCompatibilityResult(true, message, manifest);
        }
    }

    internal sealed class WebCoreCompatibilityPatch
    {
        public string OperationId { get; set; }

        public string Path { get; set; }

        public long InputSize { get; set; }

        public string InputSha256 { get; set; }

        public long OutputSize { get; set; }

        public string OutputSha256 { get; set; }
    }

    internal static class WebCoreCompatibilityManager
    {
        internal const string CompatibilityManifestFileName = "WebCoreCompatibility.manifest.xml";
        internal const string PatchedManifestFileName = "WebCorePatchedAssets.manifest.xml";

        private const string PatchSetId = "cellao-webcore-php85-compatibility-v1";
        private const string PatchSetVersion = "1";
        private const string UpstreamCommit = "765c3850767b63af1cd259bab7f2f7ca3e97adf9";
        private const string BaseManifestSha256 = "85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463";
        private const string CompatibilityManifestSha256 = "4bd7c613e1f232419737c0f14fcff94cdca3a9f2fa136e0fda9a0a05790ca31a";
        private const string PatchedManifestSha256 = "f07f6b2ce58fa025e93baa49241dbe71a8d7482a10dfd437b2e1d50c418c45c8";
        private const string CompatibilityToolSha256 = "e0d68a8d1c5c6577fe5582f0ecfae5f45cdf6394a69a0c638976d4f16f512da7";
        private const int FileCount = 7140;
        private const int PatchFileCount = 7;
        private const long TotalBytes = 26649619L;
        private const int ToolTimeoutMilliseconds = 300000;
        private const int MaximumToolOutputCharacters = 1024 * 1024;

        public static WebCoreCompatibilityResult ValidateConfiguredAuthority(string baseDirectory)
        {
            try
            {
                string canonicalBase = Path.GetFullPath(baseDirectory);
                WebCoreManifest manifest = LoadValidatedAuthority(canonicalBase);
                return WebCoreCompatibilityResult.Valid(
                    "WebCore compatibility PASS: patch-set=" + PatchSetId
                    + " files=" + manifest.Entries.Count.ToString(CultureInfo.InvariantCulture)
                    + " patched=" + PatchFileCount.ToString(CultureInfo.InvariantCulture)
                    + " bytes=" + manifest.TotalSize.ToString(CultureInfo.InvariantCulture),
                    manifest);
            }
            catch (Exception exception)
            {
                return WebCoreCompatibilityResult.Failed(
                    "WebCore compatibility FAIL: " + SafeMessage(exception));
            }
        }

        internal static WebCoreManifest LoadValidatedAuthority(string baseDirectory)
        {
            string canonicalBase = Path.GetFullPath(baseDirectory);
            string baseManifestPath = Path.Combine(canonicalBase, WebCoreAssetManager.ManifestFileName);
            string compatibilityPath = Path.Combine(canonicalBase, CompatibilityManifestFileName);
            string patchedPath = Path.Combine(canonicalBase, PatchedManifestFileName);
            RequireFileHash(baseManifestPath, BaseManifestSha256, "base WebCore manifest");
            RequireFileHash(compatibilityPath, CompatibilityManifestSha256, "WebCore compatibility manifest");
            RequireFileHash(patchedPath, PatchedManifestSha256, "patched WebCore manifest");

            WebCoreManifest baseManifest = WebCoreAssetManager.LoadManifest(baseManifestPath);
            WebCoreManifest patchedManifest = LoadPatchedManifest(patchedPath);
            IDictionary<string, WebCoreCompatibilityPatch> patches = LoadCompatibilityManifest(compatibilityPath);
            CrossValidate(baseManifest, patchedManifest, patches);
            return patchedManifest;
        }

        internal static void ApplyWithApprovedTool(
            string stagingDirectory,
            string pythonExecutable,
            string compatibilityToolPath,
            string baseDirectory)
        {
            LoadValidatedAuthority(baseDirectory);
            string canonicalStaging = Path.GetFullPath(stagingDirectory);
            string canonicalPython = ResolveLocalFile(pythonExecutable, "Python executable");
            string canonicalTool = ResolveLocalFile(compatibilityToolPath, "WebCore compatibility tool");
            RequireFileHash(canonicalTool, CompatibilityToolSha256, "WebCore compatibility tool");

            string parent = Path.GetDirectoryName(canonicalStaging.TrimEnd(Path.DirectorySeparatorChar));
            string outputDirectory = Path.Combine(
                parent,
                Path.GetFileName(canonicalStaging) + ".patched-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            {
                throw new InvalidDataException("The WebCore compatibility staging target already exists.");
            }

            try
            {
                RunCompatibilityTool(
                    canonicalPython,
                    canonicalTool,
                    canonicalStaging,
                    outputDirectory);
                WebCoreManifest finalManifest = LoadValidatedAuthority(baseDirectory);
                WebCoreAssetManager.ValidateAssetTree(Path.GetFullPath(outputDirectory), finalManifest);
                Directory.Delete(canonicalStaging, true);
                Directory.Move(outputDirectory, canonicalStaging);
            }
            catch
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }

                throw;
            }
        }

        private static void RunCompatibilityTool(
            string pythonExecutable,
            string toolPath,
            string sourceRoot,
            string outputRoot)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = QuoteArgument(toolPath) + " apply "
                    + QuoteArgument(sourceRoot) + " " + QuoteArgument(outputRoot),
                WorkingDirectory = Path.GetDirectoryName(toolPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ToolTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("The WebCore compatibility tool timed out.");
                }

                if (!Task.WaitAll(
                        new Task[] { stdoutTask, stderrTask },
                        ToolTimeoutMilliseconds))
                {
                    throw new TimeoutException("The WebCore compatibility tool stream drain timed out.");
                }

                string stdout = stdoutTask.Result ?? string.Empty;
                string stderr = stderrTask.Result ?? string.Empty;
                if (stdout.Length > MaximumToolOutputCharacters
                    || stderr.Length > MaximumToolOutputCharacters)
                {
                    throw new InvalidDataException("The WebCore compatibility tool exceeded its output limit.");
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidDataException(
                        "The WebCore compatibility tool failed with code "
                        + process.ExitCode.ToString(CultureInfo.InvariantCulture)
                        + (string.IsNullOrWhiteSpace(stderr) ? "." : ": " + stderr.Trim()));
                }
            }
        }

        private static WebCoreManifest LoadPatchedManifest(string path)
        {
            XDocument document = LoadXml(path);
            XElement root = document.Root;
            if (root == null || root.Name != "WebCorePatchedAssetManifest")
            {
                throw new InvalidDataException("The patched WebCore manifest root is invalid.");
            }

            ValidateAttributeSet(
                root,
                new[]
                {
                    "SchemaVersion", "Id", "PatchSetVersion", "UpstreamCommit",
                    "BaseManifestSha256", "FileCount", "TotalBytes"
                },
                "patched WebCore manifest");
            if (RequiredAttribute(root, "SchemaVersion") != "1"
                || RequiredAttribute(root, "Id") != PatchSetId
                || RequiredAttribute(root, "PatchSetVersion") != PatchSetVersion
                || RequiredAttribute(root, "UpstreamCommit") != UpstreamCommit
                || RequiredHash(root, "BaseManifestSha256") != BaseManifestSha256
                || ParseInt(root, "FileCount") != FileCount
                || ParseLong(root, "TotalBytes") != TotalBytes)
            {
                throw new InvalidDataException("The patched WebCore manifest authority is invalid.");
            }

            var entries = new List<WebCoreManifestEntry>();
            var byPath = new Dictionary<string, WebCoreManifestEntry>(StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (XElement element in root.Elements())
            {
                if (element.Name != "File")
                {
                    throw new InvalidDataException("The patched WebCore manifest contains an unexpected element.");
                }

                ValidateAttributeSet(element, new[] { "Path", "Size", "Sha256" }, "patched WebCore file");
                EnsureNoContent(element, "patched WebCore file");
                string relativePath = ValidateRelativePath(RequiredAttribute(element, "Path"));
                long size = ParseLong(element, "Size");
                string hash = RequiredHash(element, "Sha256");
                if (size > 128L * 1024L * 1024L || byPath.ContainsKey(relativePath))
                {
                    throw new InvalidDataException("A patched WebCore file record is invalid or duplicated.");
                }

                var entry = new WebCoreManifestEntry { Path = relativePath, Size = size, Sha256 = hash };
                entries.Add(entry);
                byPath.Add(relativePath, entry);
                total += size;
                AddParentDirectories(relativePath, directories);
            }

            if (entries.Count != FileCount || total != TotalBytes)
            {
                throw new InvalidDataException("The patched WebCore manifest totals are invalid.");
            }

            return new WebCoreManifest
            {
                Id = PatchSetId,
                UpstreamCommit = UpstreamCommit,
                Entries = entries,
                EntryByPath = byPath,
                ExpectedDirectories = directories,
                TotalSize = total
            };
        }

        private static IDictionary<string, WebCoreCompatibilityPatch> LoadCompatibilityManifest(string path)
        {
            XDocument document = LoadXml(path);
            XElement root = document.Root;
            if (root == null || root.Name != "WebCoreCompatibilityManifest")
            {
                throw new InvalidDataException("The WebCore compatibility manifest root is invalid.");
            }

            ValidateAttributeSet(
                root,
                new[]
                {
                    "SchemaVersion", "Id", "PatchSetVersion", "UpstreamCommit",
                    "BaseManifestSha256", "FinalManifestSha256", "FileCount", "PatchFileCount"
                },
                "WebCore compatibility manifest");
            if (RequiredAttribute(root, "SchemaVersion") != "1"
                || RequiredAttribute(root, "Id") != PatchSetId
                || RequiredAttribute(root, "PatchSetVersion") != PatchSetVersion
                || RequiredAttribute(root, "UpstreamCommit") != UpstreamCommit
                || RequiredHash(root, "BaseManifestSha256") != BaseManifestSha256
                || RequiredHash(root, "FinalManifestSha256") != PatchedManifestSha256
                || ParseInt(root, "FileCount") != FileCount
                || ParseInt(root, "PatchFileCount") != PatchFileCount)
            {
                throw new InvalidDataException("The WebCore compatibility manifest authority is invalid.");
            }

            var patches = new Dictionary<string, WebCoreCompatibilityPatch>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement element in root.Elements())
            {
                if (element.Name != "Patch")
                {
                    throw new InvalidDataException("The WebCore compatibility manifest contains an unexpected element.");
                }

                ValidateAttributeSet(
                    element,
                    new[]
                    {
                        "OperationId", "Path", "InputSize", "InputSha256", "OutputSize", "OutputSha256"
                    },
                    "WebCore compatibility patch");
                EnsureNoContent(element, "WebCore compatibility patch");
                var patch = new WebCoreCompatibilityPatch
                {
                    OperationId = RequiredAttribute(element, "OperationId"),
                    Path = ValidateRelativePath(RequiredAttribute(element, "Path")),
                    InputSize = ParseLong(element, "InputSize"),
                    InputSha256 = RequiredHash(element, "InputSha256"),
                    OutputSize = ParseLong(element, "OutputSize"),
                    OutputSha256 = RequiredHash(element, "OutputSha256")
                };
                if (!patches.ContainsKey(patch.Path))
                {
                    patches.Add(patch.Path, patch);
                }
                else
                {
                    throw new InvalidDataException("The WebCore compatibility manifest contains a duplicate patch path.");
                }
            }

            if (patches.Count != PatchFileCount)
            {
                throw new InvalidDataException("The WebCore compatibility patch count is invalid.");
            }

            return patches;
        }

        private static void CrossValidate(
            WebCoreManifest baseManifest,
            WebCoreManifest patchedManifest,
            IDictionary<string, WebCoreCompatibilityPatch> patches)
        {
            if (baseManifest.Entries.Count != FileCount
                || patchedManifest.Entries.Count != FileCount
                || baseManifest.EntryByPath.Count != patchedManifest.EntryByPath.Count)
            {
                throw new InvalidDataException("The WebCore base and patched inventories are not aligned.");
            }

            foreach (WebCoreManifestEntry baseEntry in baseManifest.Entries)
            {
                WebCoreManifestEntry finalEntry;
                if (!patchedManifest.EntryByPath.TryGetValue(baseEntry.Path, out finalEntry)
                    || !string.Equals(baseEntry.Path, finalEntry.Path, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A patched WebCore path does not match the base manifest.");
                }

                WebCoreCompatibilityPatch patch;
                if (patches.TryGetValue(baseEntry.Path, out patch))
                {
                    if (patch.InputSize != baseEntry.Size
                        || patch.InputSha256 != baseEntry.Sha256
                        || patch.OutputSize != finalEntry.Size
                        || patch.OutputSha256 != finalEntry.Sha256
                        || string.IsNullOrWhiteSpace(patch.OperationId))
                    {
                        throw new InvalidDataException("A WebCore compatibility patch is not cross-linked to both manifests.");
                    }
                }
                else if (baseEntry.Size != finalEntry.Size || baseEntry.Sha256 != finalEntry.Sha256)
                {
                    throw new InvalidDataException("An unpatched WebCore file changed in the final manifest.");
                }
            }
        }

        private static XDocument LoadXml(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("A required WebCore compatibility authority file is missing.", path);
            }

            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                return XDocument.Load(reader, LoadOptions.None);
            }
        }

        private static void RequireFileHash(string path, string expected, string label)
        {
            if (!File.Exists(path)
                || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
                || ComputeFileSha256(path) != expected)
            {
                throw new InvalidDataException("The " + label + " SHA-256 does not match the approved authority.");
            }
        }

        private static string ResolveLocalFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.StartsWith(@"\\", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal))
            {
                throw new InvalidDataException(label + " must be a local file.");
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new FileNotFoundException(label + " is missing or is a reparse point.", fullPath);
            }

            return fullPath;
        }

        private static void AddParentDirectories(string path, ISet<string> directories)
        {
            int separator = path.LastIndexOf('/');
            while (separator > 0)
            {
                string directory = path.Substring(0, separator);
                directories.Add(directory);
                separator = directory.LastIndexOf('/');
            }
        }

        private static string ValidateRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.StartsWith("/", StringComparison.Ordinal)
                || path.EndsWith("/", StringComparison.Ordinal)
                || path.Contains("\\")
                || path.Contains(":")
                || path.Contains("//")
                || Path.IsPathRooted(path))
            {
                throw new InvalidDataException("A WebCore compatibility path is invalid.");
            }

            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0
                    || part == "."
                    || part == ".."
                    || part.EndsWith(".", StringComparison.Ordinal)
                    || part.EndsWith(" ", StringComparison.Ordinal)
                    || part.Any(character => character < 0x20 || character == 0x7f))
                {
                    throw new InvalidDataException("A WebCore compatibility path segment is invalid.");
                }
            }

            return path;
        }

        private static void ValidateAttributeSet(XElement element, string[] names, string label)
        {
            var expected = new HashSet<string>(names, StringComparer.Ordinal);
            if (element.Attributes().Count() != expected.Count
                || element.Attributes().Any(attribute => !expected.Contains(attribute.Name.LocalName)))
            {
                throw new InvalidDataException("The " + label + " attributes are invalid.");
            }
        }

        private static void EnsureNoContent(XElement element, string label)
        {
            if (element.HasElements || element.Nodes().Any())
            {
                throw new InvalidDataException("The " + label + " contains unexpected content.");
            }
        }

        private static string RequiredAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                throw new InvalidDataException("A required WebCore compatibility attribute is missing: " + name);
            }

            return attribute.Value;
        }

        private static string RequiredHash(XElement element, string name)
        {
            string value = RequiredAttribute(element, name).ToLowerInvariant();
            if (value.Length != 64
                || value.Any(character => !((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f'))))
            {
                throw new InvalidDataException("A WebCore compatibility SHA-256 value is invalid.");
            }

            return value;
        }

        private static int ParseInt(XElement element, string name)
        {
            int value;
            if (!int.TryParse(RequiredAttribute(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0)
            {
                throw new InvalidDataException("A WebCore compatibility integer is invalid: " + name);
            }

            return value;
        }

        private static long ParseLong(XElement element, string name)
        {
            long value;
            if (!long.TryParse(RequiredAttribute(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0)
            {
                throw new InvalidDataException("A WebCore compatibility integer is invalid: " + name);
            }

            return value;
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                StringBuilder builder = new StringBuilder(64);
                foreach (byte value in sha256.ComputeHash(stream))
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string SafeMessage(Exception exception)
        {
            return string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
        }
    }
}
