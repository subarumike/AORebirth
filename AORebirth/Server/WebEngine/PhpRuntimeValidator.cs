namespace WebEngine
{
    using System;
    using System.IO;
    using System.Security;

    internal enum PhpRuntimeValidationFailure
    {
        None,
        MissingConfiguration,
        NonLocalPath,
        InvalidPath,
        MissingDirectory,
        MissingExecutable
    }

    internal sealed class PhpRuntimeValidationResult
    {
        private PhpRuntimeValidationResult(
            bool isValid,
            PhpRuntimeValidationFailure failure,
            string runtimeDirectory,
            string executablePath,
            string message)
        {
            this.IsValid = isValid;
            this.Failure = failure;
            this.RuntimeDirectory = runtimeDirectory;
            this.ExecutablePath = executablePath;
            this.Message = message;
        }

        public string ExecutablePath { get; private set; }

        public PhpRuntimeValidationFailure Failure { get; private set; }

        public bool IsValid { get; private set; }

        public string Message { get; private set; }

        public string RuntimeDirectory { get; private set; }

        public static PhpRuntimeValidationResult Failed(
            PhpRuntimeValidationFailure failure,
            string message)
        {
            return new PhpRuntimeValidationResult(false, failure, null, null, message);
        }

        public static PhpRuntimeValidationResult Valid(string runtimeDirectory, string executablePath)
        {
            return new PhpRuntimeValidationResult(
                true,
                PhpRuntimeValidationFailure.None,
                runtimeDirectory,
                executablePath,
                "Local PHP runtime validated.");
        }
    }

    internal static class PhpRuntimeValidator
    {
        private const string PhpCgiExecutableName = "php-cgi.exe";

        public static PhpRuntimeValidationResult Validate(string configuredPath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.MissingConfiguration,
                    "WebHostPhpPath is not configured. Configure a local directory containing php-cgi.exe.");
            }

            string trimmedPath = configuredPath.Trim();
            if (IsNetworkOrUriPath(trimmedPath))
            {
                return PhpRuntimeValidationResult.Failed(
                    PhpRuntimeValidationFailure.NonLocalPath,
                    "WebHostPhpPath must be a local filesystem path. URLs and network paths are not allowed.");
            }

            try
            {
                string resolutionRoot = Path.GetFullPath(
                    string.IsNullOrWhiteSpace(baseDirectory)
                        ? AppDomain.CurrentDomain.BaseDirectory
                        : baseDirectory);
                string resolvedPath = Path.GetFullPath(
                    Path.IsPathRooted(trimmedPath)
                        ? trimmedPath
                        : Path.Combine(resolutionRoot, trimmedPath));

                if (IsNetworkOrUriPath(resolvedPath))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.NonLocalPath,
                        "WebHostPhpPath must resolve to a local filesystem path.");
                }

                bool executableWasConfigured = string.Equals(
                    Path.GetFileName(resolvedPath),
                    PhpCgiExecutableName,
                    StringComparison.OrdinalIgnoreCase);
                if (!executableWasConfigured && string.Equals(
                    Path.GetExtension(resolvedPath),
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.InvalidPath,
                        "WebHostPhpPath may name only php-cgi.exe or its containing directory.");
                }

                string runtimeDirectory = executableWasConfigured
                    ? Path.GetDirectoryName(resolvedPath)
                    : resolvedPath;
                string executablePath = executableWasConfigured
                    ? resolvedPath
                    : Path.Combine(runtimeDirectory, PhpCgiExecutableName);

                if (string.IsNullOrEmpty(runtimeDirectory) || !Directory.Exists(runtimeDirectory))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.MissingDirectory,
                        "The configured local PHP runtime directory does not exist.");
                }

                if (!File.Exists(executablePath))
                {
                    return PhpRuntimeValidationResult.Failed(
                        PhpRuntimeValidationFailure.MissingExecutable,
                        "The configured local PHP runtime does not contain php-cgi.exe.");
                }

                return PhpRuntimeValidationResult.Valid(runtimeDirectory, executablePath);
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

        private static PhpRuntimeValidationResult InvalidPath()
        {
            return PhpRuntimeValidationResult.Failed(
                PhpRuntimeValidationFailure.InvalidPath,
                "WebHostPhpPath is not a valid local filesystem path.");
        }

        private static bool IsNetworkOrUriPath(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            Uri uri;
            return Uri.TryCreate(path, UriKind.Absolute, out uri)
                   && !IsWindowsDrivePath(path);
        }

        private static bool IsWindowsDrivePath(string path)
        {
            return path.Length >= 3
                   && char.IsLetter(path[0])
                   && path[1] == ':'
                   && (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar);
        }
    }
}
