namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal static class PhpRuntimeValidatorSelfTests
    {
        public static bool Run(TextWriter output)
        {
            var failures = new List<string>();
            RunCase("missing configuration fails closed", MissingConfigurationFailsClosed, failures);
            RunCase("missing runtime fails closed", MissingRuntimeFailsClosed, failures);
            RunCase("missing executable fails closed", MissingExecutableFailsClosed, failures);
            RunCase("relative local runtime is accepted", RelativeLocalRuntimeIsAccepted, failures);
            RunCase("explicit local executable is accepted", ExplicitLocalExecutableIsAccepted, failures);
            RunCase("network locations are rejected", NetworkLocationsAreRejected, failures);
            RunCase("malformed paths are rejected", MalformedPathsAreRejected, failures);

            if (failures.Count == 0)
            {
                output.WriteLine("[WebEngine PHP self-test] PASS 7/7");
                return true;
            }

            foreach (string failure in failures)
            {
                output.WriteLine("[WebEngine PHP self-test] FAIL " + failure);
            }

            return false;
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-webengine-php-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void ExplicitLocalExecutableIsAccepted()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        string executablePath = Path.Combine(root, "php-cgi.exe");
                        File.WriteAllText(executablePath, "test marker only");

                        PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate(executablePath, root);
                        Require(result.IsValid, "explicit php-cgi.exe path was rejected");
                        Require(
                            string.Equals(result.RuntimeDirectory, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase),
                            "runtime directory was not canonicalized");
                        Require(
                            string.Equals(result.ExecutablePath, Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase),
                            "executable path was not canonicalized");
                    });
        }

        private static void MalformedPathsAreRejected()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        PhpRuntimeValidationResult malformed = PhpRuntimeValidator.Validate("bad\0path", root);
                        PhpRuntimeValidationResult wrongExecutable = PhpRuntimeValidator.Validate(
                            Path.Combine(root, "php.exe"),
                            root);

                        Require(
                            !malformed.IsValid && malformed.Failure == PhpRuntimeValidationFailure.InvalidPath,
                            "malformed path was not rejected");
                        Require(
                            !wrongExecutable.IsValid && wrongExecutable.Failure == PhpRuntimeValidationFailure.InvalidPath,
                            "unexpected executable name was not rejected");
                    });
        }

        private static void MissingExecutableFailsClosed()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        string runtimeDirectory = Path.Combine(root, "php");
                        Directory.CreateDirectory(runtimeDirectory);

                        PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate(runtimeDirectory, root);
                        Require(!result.IsValid, "runtime without php-cgi.exe was accepted");
                        Require(
                            result.Failure == PhpRuntimeValidationFailure.MissingExecutable,
                            "runtime without php-cgi.exe returned the wrong failure");
                    });
        }

        private static void MissingConfigurationFailsClosed()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate(null, root);
                        Require(!result.IsValid, "missing configuration was accepted");
                        Require(
                            result.Failure == PhpRuntimeValidationFailure.MissingConfiguration,
                            "missing configuration returned the wrong failure");
                    });
        }

        private static void MissingRuntimeFailsClosed()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate("php", root);
                        Require(!result.IsValid, "missing runtime was accepted");
                        Require(
                            result.Failure == PhpRuntimeValidationFailure.MissingDirectory,
                            "missing runtime returned the wrong failure");
                    });
        }

        private static void NetworkLocationsAreRejected()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        string[] invalidPaths =
                        {
                            "http://127.0.0.1/php",
                            "https://127.0.0.1/php",
                            "file:///C:/php",
                            @"\\server\share\php",
                            "//server/share/php"
                        };

                        foreach (string invalidPath in invalidPaths)
                        {
                            PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate(invalidPath, root);
                            Require(!result.IsValid, "network or URI path was accepted");
                            Require(
                                result.Failure == PhpRuntimeValidationFailure.NonLocalPath,
                                "network or URI path returned the wrong failure");
                        }
                    });
        }

        private static void RelativeLocalRuntimeIsAccepted()
        {
            WithTemporaryDirectory(
                root =>
                    {
                        string runtimeDirectory = Path.Combine(root, "runtime", "php");
                        Directory.CreateDirectory(runtimeDirectory);
                        string executablePath = Path.Combine(runtimeDirectory, "php-cgi.exe");
                        File.WriteAllText(executablePath, "test marker only");

                        PhpRuntimeValidationResult result = PhpRuntimeValidator.Validate(
                            Path.Combine("runtime", "php"),
                            root);
                        Require(result.IsValid, "relative local runtime was rejected");
                        Require(
                            string.Equals(result.RuntimeDirectory, Path.GetFullPath(runtimeDirectory), StringComparison.OrdinalIgnoreCase),
                            "relative runtime directory was not canonicalized");
                        Require(
                            string.Equals(result.ExecutablePath, Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase),
                            "relative executable path was not canonicalized");
                    });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void RunCase(string name, Action test, ICollection<string> failures)
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failures.Add(name + " (" + ex.GetType().Name + ")");
            }
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            string root = CreateTemporaryDirectory();
            try
            {
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
