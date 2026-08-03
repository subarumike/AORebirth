namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    internal enum PhpRuntimeValidationFailure
    {
        None,
        MissingConfiguration,
        NonLocalPath,
        InvalidPath,
        MissingDirectory,
        MissingManifest,
        InvalidManifest,
        InvalidInventory,
        MissingExecutable,
        InvalidExecutable,
        ProbeFailed
    }

    internal sealed class PhpRuntimeValidationResult
    {
        private PhpRuntimeValidationResult(
            bool isValid,
            PhpRuntimeValidationFailure failure,
            string runtimeDirectory,
            string executablePath,
            string cliExecutablePath,
            string iniPath,
            string stateDirectory,
            string iniScanDirectory,
            string version,
            string manifestId,
            string message)
        {
            this.IsValid = isValid;
            this.Failure = failure;
            this.RuntimeDirectory = runtimeDirectory;
            this.ExecutablePath = executablePath;
            this.CliExecutablePath = cliExecutablePath;
            this.IniPath = iniPath;
            this.StateDirectory = stateDirectory;
            this.IniScanDirectory = iniScanDirectory;
            this.Version = version;
            this.ManifestId = manifestId;
            this.Message = message;
        }

        public string CliExecutablePath { get; private set; }

        public string ExecutablePath { get; private set; }

        public PhpRuntimeValidationFailure Failure { get; private set; }

        public string IniPath { get; private set; }

        public string IniScanDirectory { get; private set; }

        public bool IsValid { get; private set; }

        public string ManifestId { get; private set; }

        public string Message { get; private set; }

        public string RuntimeDirectory { get; private set; }

        public string StateDirectory { get; private set; }

        public string Version { get; private set; }

        public static PhpRuntimeValidationResult Failed(
            PhpRuntimeValidationFailure failure,
            string message)
        {
            return new PhpRuntimeValidationResult(
                false,
                failure,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                message);
        }

        internal static PhpRuntimeValidationResult Valid(
            string runtimeDirectory,
            string executablePath,
            string cliExecutablePath,
            string iniPath,
            string stateDirectory,
            string iniScanDirectory,
            string version,
            string manifestId,
            string message)
        {
            return new PhpRuntimeValidationResult(
                true,
                PhpRuntimeValidationFailure.None,
                runtimeDirectory,
                executablePath,
                cliExecutablePath,
                iniPath,
                stateDirectory,
                iniScanDirectory,
                version,
                manifestId,
                message);
        }
    }

    internal sealed class PhpRuntimeManifestEntry
    {
        public string Path { get; set; }

        public long Size { get; set; }

        public string Sha256 { get; set; }
    }

    internal sealed class PhpRuntimeManifest
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string ConfigurationSource { get; set; }

        public string ConfigurationInstalledPath { get; set; }

        public string ConfigurationSha256 { get; set; }

        public IList<PhpRuntimeManifestEntry> Files { get; set; }

        public ISet<string> Directories { get; set; }

        public long TotalUncompressedBytes { get; set; }
    }

    internal sealed class PhpCliProbeFacts
    {
        public int IntegerSize { get; set; }

        public string Sapi { get; set; }

        public string ThreadSafety { get; set; }

        public string Version { get; set; }
    }

    internal sealed class PhpCgiProbeFacts
    {
        public string Architecture { get; set; }

        public string Sapi { get; set; }

        public string ThreadSafety { get; set; }

        public string Version { get; set; }
    }

    internal sealed class PhpIniProbeFacts
    {
        public string AdditionalFiles { get; set; }

        public string LoadedConfigurationFile { get; set; }

        public string ScanDirectory { get; set; }
    }

    internal sealed class PhpRuntimeLease : IDisposable
    {
        private FileStream stream;

        public PhpRuntimeLease(FileStream stream)
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

    internal static class PhpRuntimeValidator
    {
        internal const string ManifestFileName = "PhpRuntime.manifest.xml";
        internal const string ConfigurationFileName = "WebEngine.php.ini";

        private const string RuntimeLeaseFileName = "PhpRuntime.runtime.lock";
        private const string PhpCgiExecutableName = "php-cgi.exe";
        private const string PhpCliExecutableName = "php.exe";
        private const string PhpDllName = "php8.dll";
        private const int MaximumProbeOutputCharacters = 1024 * 1024;
        private const int ProbeTimeoutMilliseconds = 15000;

        private const string ApprovedManifestId = "php-8.5.9-nts-win32-vs17-x64";
        private const string ApprovedAuthority = "The PHP Group official PHP for Windows archive";
        private const string ApprovedOfficialUrl = "https://downloads.php.net/~windows/releases/archives/php-8.5.9-nts-Win32-vs17-x64.zip";
        private const string ApprovedVersion = "8.5.9";
        private const string ApprovedArchitecture = "x64";
        private const string ApprovedThreadSafety = "NTS";
        private const string ApprovedToolchain = "VS17";
        private const string ApprovedArchiveFilename = "php-8.5.9-nts-Win32-vs17-x64.zip";
        private const long ApprovedArchiveSize = 36015210L;
        private const string ApprovedArchiveSha256 = "516c2d72231bd035c8a910120834add0ad208098b790b4909b2cbeb93ce135fc";
        private const string ApprovedManifestSha256 = "dc962aa41501a23d993cf667c546593ef36b122f8002d8ab3fc56d1a888cd735";
        private const string ApprovedArchiveRoot = "flat";
        private const int ApprovedFileCount = 78;
        private const int ApprovedDirectoryCount = 6;
        private const long ApprovedTotalUncompressedBytes = 101963340L;

        public static PhpRuntimeValidationResult Validate(string configuredPath, string baseDirectory)
        {
            return ValidateInternal(configuredPath, baseDirectory, true);
        }

        internal static PhpRuntimeValidationResult ValidateWithoutProbes(
            string configuredPath,
            string baseDirectory)
        {
            return ValidateInternal(configuredPath, baseDirectory, false);
        }

        public static PhpRuntimeValidationResult ValidateConfiguredManifest(string baseDirectory)
        {
            try
            {
                string canonicalBase = CanonicalDirectoryPath(baseDirectory);
                PhpRuntimeManifest manifest = LoadManifest(
                    Path.Combine(canonicalBase, ManifestFileName),
                    Path.Combine(canonicalBase, ConfigurationFileName));
                return PhpRuntimeValidationResult.Valid(
                    canonicalBase,
                    null,
                    null,
                    Path.Combine(canonicalBase, ConfigurationFileName),
                    null,
                    null,
                    manifest.Version,
                    manifest.Id,
                    "PHP runtime manifest PASS: manifest=" + manifest.Id
                    + " version=" + manifest.Version
                    + " files=" + manifest.Files.Count.ToString(CultureInfo.InvariantCulture)
                    + " bytes=" + manifest.TotalUncompressedBytes.ToString(CultureInfo.InvariantCulture));
            }
            catch (FileNotFoundException exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.MissingManifest,
                    "PHP runtime manifest FAIL: " + SafeMessage(exception));
            }
            catch (Exception exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.InvalidManifest,
                    "PHP runtime manifest FAIL: " + SafeMessage(exception));
            }
        }

        public static IDisposable AcquireRuntimeLease(string configuredPath, string baseDirectory)
        {
            string runtimeDirectory = ResolveRuntimeDirectory(configuredPath, baseDirectory);
            string parentDirectory = Path.GetDirectoryName(
                runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                throw new DirectoryNotFoundException("The PHP runtime parent directory is missing.");
            }

            EnsureExistingAncestorsHaveNoReparsePoints(parentDirectory);
            string leasePath = Path.Combine(parentDirectory, RuntimeLeaseFileName);
            FileStream stream = new FileStream(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return new PhpRuntimeLease(stream);
        }

        private static PhpRuntimeValidationResult ValidateInternal(
            string configuredPath,
            string baseDirectory,
            bool runProbes)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.MissingConfiguration,
                    "WebHostPhpPath is not configured.");
            }

            if (IsNetworkOrUriPath(configuredPath.Trim()))
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.NonLocalPath,
                    "WebHostPhpPath must be a local filesystem path.");
            }

            try
            {
                string canonicalBase = CanonicalDirectoryPath(baseDirectory);
                string runtimeDirectory = ResolveRuntimeDirectory(configuredPath, canonicalBase);
                if (!Directory.Exists(runtimeDirectory))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.MissingDirectory,
                        "The configured local PHP runtime directory does not exist.");
                }

                PhpRuntimeManifest manifest;
                try
                {
                    manifest = LoadManifest(
                        Path.Combine(canonicalBase, ManifestFileName),
                        Path.Combine(canonicalBase, ConfigurationFileName));
                }
                catch (InvalidDataException exception)
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidManifest,
                        "PHP runtime manifest FAIL: " + SafeMessage(exception));
                }
                catch (XmlException exception)
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidManifest,
                        "PHP runtime manifest FAIL: " + SafeMessage(exception));
                }
                catch (InvalidOperationException exception)
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidManifest,
                        "PHP runtime manifest FAIL: " + SafeMessage(exception));
                }
                catch (FormatException exception)
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidManifest,
                        "PHP runtime manifest FAIL: " + SafeMessage(exception));
                }
                catch (OverflowException exception)
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidManifest,
                        "PHP runtime manifest FAIL: " + SafeMessage(exception));
                }
                if (!File.Exists(Path.Combine(runtimeDirectory, PhpCgiExecutableName))
                    || !File.Exists(Path.Combine(runtimeDirectory, PhpCliExecutableName)))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.MissingExecutable,
                        "The configured PHP runtime is missing php-cgi.exe or php.exe.");
                }

                ValidateInstalledTree(runtimeDirectory, manifest);

                string executablePath = Path.Combine(runtimeDirectory, PhpCgiExecutableName);
                string cliExecutablePath = Path.Combine(runtimeDirectory, PhpCliExecutableName);
                string iniPath = Path.Combine(runtimeDirectory, manifest.ConfigurationInstalledPath);
                string stateDirectory = Path.Combine(canonicalBase, "WebEngineData");
                string iniScanDirectory = string.Empty;
                EnsureMutableStateDirectories(stateDirectory, iniScanDirectory);

                PhpRuntimeValidationResult result = PhpRuntimeValidationResult.Valid(
                    runtimeDirectory,
                    executablePath,
                    cliExecutablePath,
                    iniPath,
                    stateDirectory,
                    iniScanDirectory,
                    manifest.Version,
                    manifest.Id,
                    "Local PHP runtime PASS: manifest=" + manifest.Id
                    + " version=" + manifest.Version
                    + " architecture=x64 thread-safety=NTS");
                if (runProbes)
                {
                    ValidateRuntimeProbes(result, canonicalBase);
                }

                return result;
            }
            catch (FileNotFoundException exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.MissingManifest,
                    "PHP runtime validation FAIL: " + SafeMessage(exception));
            }
            catch (DirectoryNotFoundException exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.MissingDirectory,
                    "PHP runtime validation FAIL: " + SafeMessage(exception));
            }
            catch (InvalidDataException exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.InvalidInventory,
                    "PHP runtime validation FAIL: " + SafeMessage(exception));
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.ProbeFailed,
                    "PHP runtime probe FAIL: " + SafeMessage(exception));
            }
            catch (TimeoutException exception)
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.ProbeFailed,
                    "PHP runtime probe FAIL: " + SafeMessage(exception));
            }
            catch (ArgumentException)
            {
                return InvalidPath();
            }
            catch (NotSupportedException)
            {
                return InvalidPath();
            }
            catch (PathTooLongException)
            {
                return InvalidPath();
            }
            catch (SecurityException)
            {
                return InvalidPath();
            }
        }

        private static void ValidateRuntimeProbes(
            PhpRuntimeValidationResult runtime,
            string baseDirectory)
        {
            IDictionary<string, string> environment = ProbeEnvironment(runtime, baseDirectory);
            string facts = RunProbe(
                runtime.CliExecutablePath,
                "-c " + QuoteArgument(runtime.IniPath)
                + " -r \"echo PHP_VERSION,chr(124),PHP_INT_SIZE,chr(124),(PHP_ZTS?'TS':'NTS'),chr(124),PHP_SAPI;\"",
                runtime.RuntimeDirectory,
                environment);
            ParseAndValidateCliFacts(facts);

            string modulesOutput = RunProbe(
                runtime.CliExecutablePath,
                "-c " + QuoteArgument(runtime.IniPath) + " -m",
                runtime.RuntimeDirectory,
                environment);
            ParseAndValidateModuleList(modulesOutput);

            string iniOutput = RunProbe(
                runtime.CliExecutablePath,
                "-c " + QuoteArgument(runtime.IniPath) + " --ini",
                runtime.RuntimeDirectory,
                environment);
            ParseAndValidateIniOutput(iniOutput, Path.GetFullPath(runtime.IniPath));

            string cgiVersion = RunProbe(
                runtime.ExecutablePath,
                "-c " + QuoteArgument(runtime.IniPath) + " -v",
                runtime.RuntimeDirectory,
                environment);
            ParseAndValidateCgiVersion(cgiVersion);

            ValidateCgiRedirectContract(runtime, environment);
        }

        internal static PhpCliProbeFacts ParseAndValidateCliFacts(string output)
        {
            RequireBoundedProbeOutput(output, "PHP CLI identity");
            string normalized = output.Trim();
            if (normalized.IndexOf('\r') >= 0 || normalized.IndexOf('\n') >= 0)
            {
                throw new InvalidDataException("The PHP CLI identity probe output is malformed.");
            }

            string[] parts = normalized.Split('|');
            int integerSize;
            if (parts.Length != 4
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out integerSize))
            {
                throw new InvalidDataException("The PHP CLI identity probe output is malformed.");
            }

            var facts = new PhpCliProbeFacts
                        {
                            Version = parts[0],
                            IntegerSize = integerSize,
                            ThreadSafety = parts[2],
                            Sapi = parts[3]
                        };
            if (facts.Version != ApprovedVersion
                || facts.IntegerSize != 8
                || facts.ThreadSafety != ApprovedThreadSafety
                || !string.Equals(facts.Sapi, "cli", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The PHP CLI identity probe did not match PHP 8.5.9 x64 NTS.");
            }

            return facts;
        }

        internal static PhpCgiProbeFacts ParseAndValidateCgiVersion(string output)
        {
            RequireBoundedProbeOutput(output, "PHP CGI identity");
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                throw new InvalidDataException("The PHP CGI identity probe output is malformed.");
            }

            string identity = lines[0].Trim();
            if (!identity.StartsWith("PHP ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The PHP CGI identity probe output is malformed.");
            }

            int versionEnd = identity.IndexOf(' ', 4);
            if (versionEnd <= 4)
            {
                throw new InvalidDataException("The PHP CGI identity probe output is malformed.");
            }

            var facts = new PhpCgiProbeFacts
                        {
                            Version = identity.Substring(4, versionEnd - 4),
                            Sapi = identity.IndexOf("(cgi-fcgi)", StringComparison.OrdinalIgnoreCase) >= 0
                                       ? "cgi-fcgi"
                                       : string.Empty,
                            ThreadSafety = ContainsDelimitedToken(identity, ApprovedThreadSafety)
                                               ? ApprovedThreadSafety
                                               : string.Empty,
                            Architecture = ContainsDelimitedToken(identity, ApprovedArchitecture)
                                               ? ApprovedArchitecture
                                               : string.Empty
                        };
            if (facts.Version != ApprovedVersion
                || facts.Sapi != "cgi-fcgi"
                || facts.ThreadSafety != ApprovedThreadSafety
                || facts.Architecture != ApprovedArchitecture)
            {
                throw new InvalidDataException(
                    "The PHP CGI identity probe did not match PHP 8.5.9 x64 NTS CGI/FastCGI.");
            }

            return facts;
        }

        internal static ISet<string> ParseAndValidateModuleList(string output)
        {
            RequireBoundedProbeOutput(output, "PHP module list");
            HashSet<string> modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length != 0 && !(line.StartsWith("[", StringComparison.Ordinal)
                    && line.EndsWith("]", StringComparison.Ordinal)))
                {
                    modules.Add(line);
                }
            }

            string[] requiredModules =
            {
                "PDO", "pdo_mysql", "dom", "session", "hash", "json", "filter", "ctype"
            };
            foreach (string module in requiredModules)
            {
                if (!modules.Contains(module))
                {
                    throw new InvalidDataException("Required PHP module is not loaded: " + module);
                }
            }

            return modules;
        }

        internal static PhpIniProbeFacts ParseAndValidateIniOutput(string output, string expectedIniPath)
        {
            RequireBoundedProbeOutput(output, "PHP INI");
            if (string.IsNullOrWhiteSpace(expectedIniPath))
            {
                throw new ArgumentException("The approved PHP INI path is missing.");
            }

            const string LoadedPrefix = "Loaded Configuration File:";
            const string ScanPrefix = "Scan for additional .ini files in:";
            const string AdditionalPrefix = "Additional .ini files parsed:";
            var facts = new PhpIniProbeFacts();
            bool foundLoaded = false;
            bool foundScan = false;
            bool foundAdditional = false;
            foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.StartsWith(LoadedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundLoaded)
                    {
                        throw new InvalidDataException("The PHP INI probe contains duplicate loaded-file records.");
                    }

                    foundLoaded = true;
                    facts.LoadedConfigurationFile = NormalizeIniLoadedPath(
                        line.Substring(LoadedPrefix.Length).Trim());
                }
                else if (line.StartsWith(ScanPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundScan)
                    {
                        throw new InvalidDataException("The PHP INI probe contains duplicate scan-directory records.");
                    }

                    foundScan = true;
                    facts.ScanDirectory = line.Substring(ScanPrefix.Length).Trim();
                }
                else if (line.StartsWith(AdditionalPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundAdditional)
                    {
                        throw new InvalidDataException("The PHP INI probe contains duplicate additional-file records.");
                    }

                    foundAdditional = true;
                    facts.AdditionalFiles = line.Substring(AdditionalPrefix.Length).Trim();
                }
            }

            if (!foundLoaded
                || !foundScan
                || !foundAdditional
                || !string.Equals(
                    facts.LoadedConfigurationFile,
                    Path.GetFullPath(expectedIniPath),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(facts.ScanDirectory, "(none)", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(facts.AdditionalFiles, "(none)", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("PHP did not load only the approved php.ini.");
            }

            return facts;
        }

        private static string NormalizeIniLoadedPath(string value)
        {
            bool startsWithQuote = value.StartsWith("\"", StringComparison.Ordinal);
            bool endsWithQuote = value.EndsWith("\"", StringComparison.Ordinal);
            if (startsWithQuote != endsWithQuote || (startsWithQuote && value.Length < 2))
            {
                throw new InvalidDataException("The PHP INI loaded-file record has malformed quoting.");
            }

            string normalized = startsWithQuote
                ? value.Substring(1, value.Length - 2)
                : value;
            if (normalized.Length == 0 || normalized.IndexOf('\"') >= 0)
            {
                throw new InvalidDataException("The PHP INI loaded-file record is malformed.");
            }

            return normalized;
        }

        private static void ValidateCgiRedirectContract(
            PhpRuntimeValidationResult runtime,
            IDictionary<string, string> baseEnvironment)
        {
            string probePath = Path.Combine(
                runtime.StateDirectory,
                "tmp",
                "php-cgi-contract-probe-" + Guid.NewGuid().ToString("N") + ".php");
            try
            {
                File.WriteAllText(
                    probePath,
                    "<?php echo \"Content-Type: text/plain\\r\\n\\r\\nAOREBIRTH_CGI_PASS\";",
                    new UTF8Encoding(false));
                var environment = new Dictionary<string, string>(
                    baseEnvironment,
                    StringComparer.OrdinalIgnoreCase)
                {
                    { "REQUEST_METHOD", "GET" },
                    { "SCRIPT_FILENAME", probePath },
                    { "SCRIPT_NAME", "/internal/php-cgi-contract-probe.php" },
                    { "DOCUMENT_ROOT", runtime.StateDirectory },
                    { "SERVER_NAME", "localhost" },
                    { "SERVER_PORT", "80" },
                    { "SERVER_PROTOCOL", "HTTP/1.1" },
                    { "GATEWAY_INTERFACE", "CGI/1.1" },
                    { "CONTENT_LENGTH", "0" },
                    { "QUERY_STRING", string.Empty }
                };
                string output = RunProbe(
                    runtime.ExecutablePath,
                    "-c " + QuoteArgument(runtime.IniPath),
                    runtime.StateDirectory,
                    environment);
                if (output.IndexOf("Content-Type: text/plain", StringComparison.OrdinalIgnoreCase) < 0
                    || output.IndexOf("AOREBIRTH_CGI_PASS", StringComparison.Ordinal) < 0
                    || output.IndexOf("Security Alert", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidDataException(
                        "The PHP CGI redirect-status execution contract failed.");
                }
            }
            finally
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
        }

        private static bool ContainsDelimitedToken(string value, string token)
        {
            int start = 0;
            while (start < value.Length)
            {
                int index = value.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                int end = index + token.Length;
                bool leftDelimited = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                bool rightDelimited = end == value.Length || !char.IsLetterOrDigit(value[end]);
                if (leftDelimited && rightDelimited)
                {
                    return true;
                }

                start = end;
            }

            return false;
        }

        private static void RequireBoundedProbeOutput(string output, string label)
        {
            if (output == null || output.Length == 0 || output.Length > MaximumProbeOutputCharacters)
            {
                throw new InvalidDataException(label + " probe output is empty or exceeds its limit.");
            }
        }

        private static IDictionary<string, string> ProbeEnvironment(
            PhpRuntimeValidationResult runtime,
            string baseDirectory)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PHPRC", runtime.IniPath },
                { "PHP_INI_SCAN_DIR", runtime.IniScanDirectory },
                { "AOREBIRTH_PHP_STATE_DIR", runtime.StateDirectory },
                { "AOREBIRTH_WEBCORE_ROOT", Path.Combine(baseDirectory, "htdocs") },
                { "REDIRECT_STATUS", "200" }
            };
        }

        private static string RunProbe(
            string executablePath,
            string arguments,
            string workingDirectory,
            IDictionary<string, string> environment)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.EnvironmentVariables.Remove("PHPRC");
            startInfo.EnvironmentVariables.Remove("PHP_INI_SCAN_DIR");
            foreach (KeyValuePair<string, string> pair in environment)
            {
                startInfo.EnvironmentVariables[pair.Key] = pair.Value;
            }

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ProbeTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("The approved PHP runtime probe timed out.");
                }

                if (!Task.WaitAll(
                        new Task[] { stdoutTask, stderrTask },
                        ProbeTimeoutMilliseconds))
                {
                    throw new TimeoutException("The approved PHP runtime probe stream drain timed out.");
                }

                string stdout = stdoutTask.Result ?? string.Empty;
                string stderr = stderrTask.Result ?? string.Empty;
                if (stdout.Length > MaximumProbeOutputCharacters
                    || stderr.Length > MaximumProbeOutputCharacters)
                {
                    throw new InvalidDataException("The approved PHP runtime probe exceeded its output limit.");
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidDataException(
                        "The approved PHP runtime probe exited with code "
                        + process.ExitCode.ToString(CultureInfo.InvariantCulture)
                        + (string.IsNullOrWhiteSpace(stderr) ? "." : ": " + stderr.Trim()));
                }

                return stdout + (string.IsNullOrEmpty(stdout) ? stderr : string.Empty);
            }
        }

        private static PhpRuntimeManifest LoadManifest(
            string manifestPath,
            string configurationSourcePath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("The checked-in PHP runtime manifest is missing.", manifestPath);
            }

            EnsureNotReparsePoint(manifestPath, "The PHP runtime manifest is a reparse point.");
            EnsureExistingAncestorsHaveNoReparsePoints(Path.GetDirectoryName(Path.GetFullPath(manifestPath)));
            if (!string.Equals(
                    ComputeFileSha256(manifestPath),
                    ApprovedManifestSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The PHP runtime manifest SHA-256 is not approved.");
            }

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
            if (root == null || root.Name != "PhpRuntimeManifest")
            {
                throw new InvalidDataException("The PHP runtime manifest root is invalid.");
            }

            string[] rootAttributes =
            {
                "SchemaVersion", "Id", "Authority", "OfficialUrl", "Version",
                "Architecture", "ThreadSafety", "Toolchain", "ArchiveFilename",
                "ArchiveSize", "ArchiveSha256", "ArchiveRoot", "FileCount",
                "DirectoryCount", "TotalUncompressedBytes"
            };
            ValidateAttributeSet(root, rootAttributes, "PHP runtime manifest");
            if (RequiredAttribute(root, "SchemaVersion") != "1")
            {
                throw new InvalidDataException("The PHP runtime manifest schema version is unsupported.");
            }

            EnsureApprovedAuthority(root);
            int fileCount = ParseInt(root, "FileCount");
            int directoryCount = ParseInt(root, "DirectoryCount");
            long expectedTotal = ParseLong(root, "TotalUncompressedBytes");
            XElement configuration = root.Elements("Configuration").SingleOrDefault();
            if (configuration == null)
            {
                throw new InvalidDataException("The PHP runtime manifest configuration record is missing.");
            }

            ValidateAttributeSet(
                configuration,
                new[] { "Source", "InstalledPath", "Sha256" },
                "PHP runtime configuration record");
            EnsureElementHasNoContent(configuration, "PHP runtime configuration record");
            string configurationSource = ValidateRelativePath(RequiredAttribute(configuration, "Source"));
            string configurationInstalledPath = ValidateRelativePath(
                RequiredAttribute(configuration, "InstalledPath"));
            string configurationSha = RequiredHash(configuration, "Sha256");
            if (!string.Equals(configurationSource, ConfigurationFileName, StringComparison.Ordinal)
                || !string.Equals(configurationInstalledPath, "php.ini", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The PHP runtime configuration paths are not approved.");
            }

            List<PhpRuntimeManifestEntry> files = new List<PhpRuntimeManifestEntry>();
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (XElement element in root.Elements("File"))
            {
                ValidateAttributeSet(element, new[] { "Path", "Size", "Sha256" }, "PHP runtime file record");
                EnsureElementHasNoContent(element, "PHP runtime file record");
                string path = ValidateRelativePath(RequiredAttribute(element, "Path"));
                if (!seenPaths.Add(path))
                {
                    throw new InvalidDataException("The PHP runtime manifest contains a duplicate or case-colliding path.");
                }

                long size = ParseLong(element, "Size");
                if (size < 0 || size > 128L * 1024L * 1024L)
                {
                    throw new InvalidDataException("A PHP runtime file size is invalid.");
                }

                files.Add(new PhpRuntimeManifestEntry
                {
                    Path = path,
                    Size = size,
                    Sha256 = RequiredHash(element, "Sha256")
                });
                checked
                {
                    total += size;
                }
            }

            HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement element in root.Elements("Directory"))
            {
                ValidateAttributeSet(element, new[] { "Path" }, "PHP runtime directory record");
                EnsureElementHasNoContent(element, "PHP runtime directory record");
                string path = ValidateDirectoryPath(RequiredAttribute(element, "Path"));
                if (!directories.Add(path) || seenPaths.Contains(path))
                {
                    throw new InvalidDataException("The PHP runtime manifest contains a duplicate or colliding directory.");
                }
            }

            int expectedChildren = 1 + files.Count + directories.Count;
            if (root.Elements().Count() != expectedChildren
                || root.Elements().Any(element => element.Name != "Configuration"
                    && element.Name != "File"
                    && element.Name != "Directory"))
            {
                throw new InvalidDataException("The PHP runtime manifest contains an unexpected element.");
            }

            if (root.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value)))
            {
                throw new InvalidDataException("The PHP runtime manifest contains unexpected text.");
            }

            if (files.Count != fileCount
                || directories.Count != directoryCount
                || total != expectedTotal)
            {
                throw new InvalidDataException("The PHP runtime manifest inventory totals do not match its records.");
            }

            RequireManifestFile(files, PhpCgiExecutableName);
            RequireManifestFile(files, PhpCliExecutableName);
            RequireManifestFile(files, PhpDllName);
            RequireManifestFile(files, "ext/php_pdo_mysql.dll");
            ValidateConfigurationSource(configurationSourcePath, configurationSha);
            HashSet<string> installedDirectories = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);
            foreach (PhpRuntimeManifestEntry entry in files)
            {
                AddParentDirectories(entry.Path, installedDirectories);
            }

            return new PhpRuntimeManifest
            {
                Id = RequiredAttribute(root, "Id"),
                Version = RequiredAttribute(root, "Version"),
                ConfigurationSource = configurationSource,
                ConfigurationInstalledPath = configurationInstalledPath,
                ConfigurationSha256 = configurationSha,
                Files = files,
                Directories = installedDirectories,
                TotalUncompressedBytes = total
            };
        }

        private static void EnsureApprovedAuthority(XElement root)
        {
            if (RequiredAttribute(root, "Id") != ApprovedManifestId
                || RequiredAttribute(root, "Authority") != ApprovedAuthority
                || RequiredAttribute(root, "OfficialUrl") != ApprovedOfficialUrl
                || RequiredAttribute(root, "Version") != ApprovedVersion
                || RequiredAttribute(root, "Architecture") != ApprovedArchitecture
                || RequiredAttribute(root, "ThreadSafety") != ApprovedThreadSafety
                || RequiredAttribute(root, "Toolchain") != ApprovedToolchain
                || RequiredAttribute(root, "ArchiveFilename") != ApprovedArchiveFilename
                || ParseLong(root, "ArchiveSize") != ApprovedArchiveSize
                || RequiredHash(root, "ArchiveSha256") != ApprovedArchiveSha256
                || RequiredAttribute(root, "ArchiveRoot") != ApprovedArchiveRoot
                || ParseInt(root, "FileCount") != ApprovedFileCount
                || ParseInt(root, "DirectoryCount") != ApprovedDirectoryCount
                || ParseLong(root, "TotalUncompressedBytes") != ApprovedTotalUncompressedBytes)
            {
                throw new InvalidDataException("The PHP runtime manifest does not match the repository-approved authority.");
            }
        }

        private static void ValidateConfigurationSource(string path, string expectedHash)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The checked-in WebEngine.php.ini is missing.", path);
            }

            EnsureNotReparsePoint(path, "The checked-in WebEngine.php.ini is a reparse point.");
            if (!string.Equals(ComputeFileSha256(path), expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The checked-in WebEngine.php.ini hash does not match the manifest.");
            }

            string ini = File.ReadAllText(path);
            RequireIniDirective(ini, "extension", "php_pdo_mysql.dll");
            RequireIniDirective(ini, "cgi.force_redirect", "On");
            RequireIniDirective(ini, "cgi.fix_pathinfo", "Off");
            RequireIniDirective(ini, "expose_php", "Off");
            RequireIniDirective(ini, "display_errors", "Off");
            RequireIniDirective(ini, "display_startup_errors", "Off");
            RequireIniDirective(ini, "log_errors", "On");
            RequireIniDirective(ini, "allow_url_fopen", "Off");
            RequireIniDirective(ini, "allow_url_include", "Off");
            RequireIniDirective(ini, "file_uploads", "Off");
            RequireIniDirective(ini, "session.use_strict_mode", "On");
            RequireIniDirective(ini, "session.use_only_cookies", "On");
            RequireIniDirective(ini, "session.cookie_httponly", "On");
            RequireIniDirective(ini, "session.cookie_samesite", "Lax");
            RequireIniDirective(ini, "session.cookie_secure", "Off");
            RequireIniDirective(ini, "default_charset", "ISO-8859-1");
        }

        private static void RequireIniDirective(string ini, string name, string value)
        {
            string expected = value.Trim().Trim('"');
            foreach (string rawLine in ini.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string foundName = line.Substring(0, equals).Trim();
                string foundValue = line.Substring(equals + 1).Trim().Trim('"');
                if (string.Equals(foundName, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(foundValue, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new InvalidDataException("WebEngine.php.ini is missing approved directive " + name + ".");
        }

        private static void ValidateInstalledTree(string runtimeDirectory, PhpRuntimeManifest manifest)
        {
            string canonicalRoot = CanonicalDirectoryPath(runtimeDirectory);
            EnsureExistingAncestorsHaveNoReparsePoints(canonicalRoot);
            string rootPrefix = AddDirectorySeparator(canonicalRoot);
            Dictionary<string, PhpRuntimeManifestEntry> expectedFiles = manifest.Files.ToDictionary(
                entry => entry.Path,
                entry => entry,
                StringComparer.OrdinalIgnoreCase);
            expectedFiles.Add(
                manifest.ConfigurationInstalledPath,
                new PhpRuntimeManifestEntry
                {
                    Path = manifest.ConfigurationInstalledPath,
                    Size = -1,
                    Sha256 = manifest.ConfigurationSha256
                });

            HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<string> pending = new Queue<string>();
            pending.Enqueue(canonicalRoot);
            while (pending.Count > 0)
            {
                string directory = pending.Dequeue();
                foreach (string childDirectory in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    EnsureNotReparsePoint(childDirectory, "The installed PHP runtime contains a reparse-point directory.");
                    string relativeDirectory = GetContainedRelativePath(rootPrefix, childDirectory);
                    string approvedDirectory = manifest.Directories.FirstOrDefault(
                        path => string.Equals(path, relativeDirectory, StringComparison.OrdinalIgnoreCase));
                    if (approvedDirectory == null
                        || !string.Equals(approvedDirectory, relativeDirectory, StringComparison.Ordinal)
                        || !seenDirectories.Add(relativeDirectory))
                    {
                        throw new InvalidDataException("Unexpected PHP runtime directory: " + relativeDirectory);
                    }

                    pending.Enqueue(childDirectory);
                }

                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    EnsureNotReparsePoint(file, "The installed PHP runtime contains a reparse-point file.");
                    string relativePath = GetContainedRelativePath(rootPrefix, file);
                    PhpRuntimeManifestEntry expected;
                    if (!expectedFiles.TryGetValue(relativePath, out expected)
                        || !string.Equals(expected.Path, relativePath, StringComparison.Ordinal)
                        || !seenFiles.Add(relativePath))
                    {
                        throw new InvalidDataException("Unexpected PHP runtime file: " + relativePath);
                    }

                    FileInfo info = new FileInfo(file);
                    if (expected.Size >= 0 && info.Length != expected.Size)
                    {
                        throw new InvalidDataException("PHP runtime file size mismatch: " + relativePath);
                    }

                    if (!string.Equals(ComputeFileSha256(file), expected.Sha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("PHP runtime file SHA-256 mismatch: " + relativePath);
                    }
                }
            }

            if (seenFiles.Count != expectedFiles.Count || seenDirectories.Count != manifest.Directories.Count)
            {
                throw new InvalidDataException("The installed PHP runtime inventory is incomplete.");
            }

            ValidateX64PortableExecutable(Path.Combine(canonicalRoot, PhpCgiExecutableName));
            ValidateX64PortableExecutable(Path.Combine(canonicalRoot, PhpCliExecutableName));
            ValidateX64PortableExecutable(Path.Combine(canonicalRoot, PhpDllName));
        }

        private static void ValidateX64PortableExecutable(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("A required PHP runtime executable or DLL is missing.", path);
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, false))
            {
                if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d)
                {
                    throw new InvalidDataException("A required PHP runtime image is not a valid PE file.");
                }

                stream.Position = 0x3c;
                int peOffset = reader.ReadInt32();
                if (peOffset < 64 || peOffset > stream.Length - 6)
                {
                    throw new InvalidDataException("A required PHP runtime PE header is invalid.");
                }

                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x8664)
                {
                    throw new InvalidDataException("A required PHP runtime image is not x64.");
                }
            }
        }

        private static string ResolveRuntimeDirectory(string configuredPath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new ArgumentException("The PHP runtime path is empty.");
            }

            string trimmedPath = configuredPath.Trim();
            if (IsNetworkOrUriPath(trimmedPath))
            {
                throw new InvalidDataException("WebHostPhpPath must be a local filesystem path.");
            }

            string canonicalBase = CanonicalDirectoryPath(baseDirectory);
            string resolvedPath = Path.GetFullPath(
                Path.IsPathRooted(trimmedPath)
                    ? trimmedPath
                    : Path.Combine(canonicalBase, trimmedPath));
            if (IsNetworkOrUriPath(resolvedPath))
            {
                throw new InvalidDataException("WebHostPhpPath must resolve to a local filesystem path.");
            }

            string fileName = Path.GetFileName(resolvedPath);
            if (string.Equals(fileName, PhpCgiExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return CanonicalDirectoryPath(Path.GetDirectoryName(resolvedPath));
            }

            if (string.Equals(Path.GetExtension(resolvedPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("WebHostPhpPath may name only php-cgi.exe or its containing directory.");
            }

            return CanonicalDirectoryPath(resolvedPath);
        }

        private static void EnsureMutableStateDirectories(string stateDirectory, string iniScanDirectory)
        {
            Directory.CreateDirectory(stateDirectory);
            Directory.CreateDirectory(Path.Combine(stateDirectory, "log"));
            Directory.CreateDirectory(Path.Combine(stateDirectory, "tmp"));
            Directory.CreateDirectory(Path.Combine(stateDirectory, "sessions"));
            if (!string.IsNullOrEmpty(iniScanDirectory))
            {
                throw new InvalidDataException("Supplemental PHP INI scanning must be disabled.");
            }

            ValidateMutableStateDirectories(stateDirectory);
        }

        internal static void ValidateMutableStateDirectories(string stateDirectory)
        {
            string[] requiredDirectories =
            {
                stateDirectory,
                Path.Combine(stateDirectory, "log"),
                Path.Combine(stateDirectory, "tmp"),
                Path.Combine(stateDirectory, "sessions")
            };
            foreach (string directory in requiredDirectories)
            {
                if (!Directory.Exists(directory))
                {
                    throw new DirectoryNotFoundException("A required PHP mutable-state directory is missing.");
                }

                EnsureExistingAncestorsHaveNoReparsePoints(directory);
                EnsureNotReparsePoint(directory, "A PHP mutable-state directory is a reparse point.");
            }
        }

        private static void RequireManifestFile(IList<PhpRuntimeManifestEntry> files, string path)
        {
            if (!files.Any(entry => string.Equals(entry.Path, path, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("The PHP runtime manifest is missing required file: " + path);
            }
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

        private static string RequiredAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                throw new InvalidDataException("A required PHP runtime manifest attribute is missing: " + name);
            }

            return attribute.Value;
        }

        private static string RequiredHash(XElement element, string name)
        {
            string value = RequiredAttribute(element, name).ToLowerInvariant();
            if (value.Length != 64 || value.Any(character => !IsLowerHexCharacter(character)))
            {
                throw new InvalidDataException("A PHP runtime manifest SHA-256 value is invalid.");
            }

            return value;
        }

        private static int ParseInt(XElement element, string name)
        {
            int value;
            if (!int.TryParse(RequiredAttribute(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0)
            {
                throw new InvalidDataException("A PHP runtime manifest integer is invalid: " + name);
            }

            return value;
        }

        private static long ParseLong(XElement element, string name)
        {
            long value;
            if (!long.TryParse(RequiredAttribute(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0)
            {
                throw new InvalidDataException("A PHP runtime manifest integer is invalid: " + name);
            }

            return value;
        }

        private static void ValidateAttributeSet(XElement element, string[] allowed, string label)
        {
            HashSet<string> names = new HashSet<string>(allowed, StringComparer.Ordinal);
            if (element.Attributes().Any(attribute => !names.Contains(attribute.Name.LocalName))
                || element.Attributes().Count() != names.Count)
            {
                throw new InvalidDataException(label + " attributes are invalid.");
            }
        }

        private static void EnsureElementHasNoContent(XElement element, string label)
        {
            if (element.HasElements || element.Nodes().Any())
            {
                throw new InvalidDataException(label + " contains unexpected content.");
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
                throw new InvalidDataException("A PHP runtime manifest path is invalid.");
            }

            string[] parts = path.Split('/');
            foreach (string part in parts)
            {
                if (part.Length == 0
                    || part == "."
                    || part == ".."
                    || part.EndsWith(".", StringComparison.Ordinal)
                    || part.EndsWith(" ", StringComparison.Ordinal)
                    || part.Any(character => character < 0x20 || character == 0x7f))
                {
                    throw new InvalidDataException("A PHP runtime manifest path segment is invalid.");
                }
            }

            return path;
        }

        private static string ValidateDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("A PHP runtime manifest directory path is invalid.");
            }

            return ValidateRelativePath(path.Substring(0, path.Length - 1));
        }

        private static string CanonicalDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A required directory path is empty.");
            }

            string fullPath = Path.GetFullPath(path);
            if (IsNetworkOrUriPath(fullPath))
            {
                throw new InvalidDataException("Network and URI paths are not allowed.");
            }

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string AddDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string GetContainedRelativePath(string rootPrefix, string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A PHP runtime path escapes its approved root.");
            }

            return fullPath.Substring(rootPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void EnsureExistingAncestorsHaveNoReparsePoints(string path)
        {
            DirectoryInfo current = new DirectoryInfo(Path.GetFullPath(path));
            while (current != null)
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("A PHP runtime path contains a reparse point.");
                }

                current = current.Parent;
            }
        }

        private static void EnsureNotReparsePoint(string path, string message)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(message);
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(stream));
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

        private static bool IsLowerHexCharacter(char character)
        {
            return (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f');
        }

        private static bool IsNetworkOrUriPath(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            Uri uri;
            return Uri.TryCreate(path, UriKind.Absolute, out uri) && !uri.IsFile;
        }

        private static string QuoteArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        private static PhpRuntimeValidationResult InvalidPath()
        {
            return PhpRuntimeValidationResult.Failed(
                PhpRuntimeValidationFailure.InvalidPath,
                "WebHostPhpPath is not a valid local filesystem path.");
        }

        private static string SafeMessage(Exception exception)
        {
            return string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
        }
    }
}
