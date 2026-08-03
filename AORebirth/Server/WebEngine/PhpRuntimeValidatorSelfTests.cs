namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml.Linq;

    internal static class PhpRuntimeValidatorSelfTests
    {
        public static bool Run(TextWriter output)
        {
            int passed = 0;
            int total = 0;
            string sourceBase = AppDomain.CurrentDomain.BaseDirectory;

            RunCase(output, "approved manifest", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateConfiguredManifest(sourceBase);
                Require(result.IsValid, result.Message);
            });

            RunCase(output, "approved CLI identity fixture", ref passed, ref total, delegate
            {
                PhpCliProbeFacts facts = PhpRuntimeValidator.ParseAndValidateCliFacts(
                    "8.5.9|8|NTS|cli");
                Require(facts.Version == "8.5.9" && facts.IntegerSize == 8, "CLI facts changed.");
            });

            RunCase(output, "wrong PHP version fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateCliFacts("8.5.8|8|NTS|cli");
                }, "A wrong PHP version was accepted.");
            });

            RunCase(output, "wrong PHP architecture fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateCliFacts("8.5.9|4|NTS|cli");
                }, "A 32-bit PHP identity was accepted.");
            });

            RunCase(output, "wrong PHP thread-safety fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateCliFacts("8.5.9|8|TS|cli");
                }, "A thread-safe PHP identity was accepted.");
            });

            RunCase(output, "approved CGI build fixture", ref passed, ref total, delegate
            {
                PhpCgiProbeFacts facts = PhpRuntimeValidator.ParseAndValidateCgiVersion(
                    "PHP 8.5.9 (cgi-fcgi) (built: Jul 29 2026 17:12:39) (NTS Visual C++ 2022 x64)\r\n");
                Require(facts.Architecture == "x64" && facts.ThreadSafety == "NTS", "CGI facts changed.");
            });

            RunCase(output, "wrong CGI build architecture fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateCgiVersion(
                        "PHP 8.5.9 (cgi-fcgi) (built: Jul 29 2026 17:12:39) (NTS Visual C++ 2022 x86)\r\n");
                }, "An x86 PHP CGI build was accepted.");
            });

            RunCase(output, "approved module fixture", ref passed, ref total, delegate
            {
                ISet<string> modules = PhpRuntimeValidator.ParseAndValidateModuleList(
                    "[PHP Modules]\r\nPDO\r\npdo_mysql\r\ndom\r\nsession\r\nhash\r\njson\r\nfilter\r\nctype\r\n[Zend Modules]\r\n");
                Require(modules.Contains("pdo_mysql"), "The approved module list changed.");
            });

            RunCase(output, "missing module fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateModuleList(
                        "[PHP Modules]\r\nPDO\r\ndom\r\nsession\r\nhash\r\njson\r\nfilter\r\nctype\r\n");
                }, "A PHP module list without pdo_mysql was accepted.");
            });

            RunCase(output, "approved quoted INI fixture", ref passed, ref total, delegate
            {
                string iniPath = Path.Combine(sourceBase, "php", "php.ini");
                PhpIniProbeFacts facts = PhpRuntimeValidator.ParseAndValidateIniOutput(
                    BuildIniFixture(iniPath, "(none)"),
                    iniPath);
                Require(facts.AdditionalFiles == "(none)", "The approved INI facts changed.");
            });

            RunCase(output, "malformed quoted INI fixture", ref passed, ref total, delegate
            {
                string iniPath = Path.Combine(sourceBase, "php", "php.ini");
                string malformed = BuildIniFixture(iniPath, "(none)").Replace(
                    "\"" + Path.GetFullPath(iniPath) + "\"",
                    "\"" + Path.GetFullPath(iniPath));
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateIniOutput(malformed, iniPath);
                }, "A malformed quoted PHP INI path was accepted.");
            });

            RunCase(output, "additional INI fixture", ref passed, ref total, delegate
            {
                string iniPath = Path.Combine(sourceBase, "php", "php.ini");
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateIniOutput(
                        BuildIniFixture(iniPath, Path.Combine(sourceBase, "unapproved.ini")),
                        iniPath);
                }, "Additional PHP INI files were accepted.");
            });

            RunCase(output, "oversized probe fixture", ref passed, ref total, delegate
            {
                RequireProbeRejected(delegate
                {
                    PhpRuntimeValidator.ParseAndValidateCliFacts(new string('x', 1024 * 1024 + 1));
                }, "An oversized PHP probe result was accepted.");
            });

            RunCase(output, "missing manifest", ref passed, ref total, delegate
            {
                WithFixture(sourceBase, delegate(string root)
                {
                    File.Delete(Path.Combine(root, PhpRuntimeValidator.ManifestFileName));
                    PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateConfiguredManifest(root);
                    Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.MissingManifest,
                        "A missing manifest was accepted.");
                });
            });

            RunCase(output, "tampered ini", ref passed, ref total, delegate
            {
                WithFixture(sourceBase, delegate(string root)
                {
                    File.AppendAllText(Path.Combine(root, PhpRuntimeValidator.ConfigurationFileName), "\r\nexpose_php=On\r\n");
                    PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateConfiguredManifest(root);
                    Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.InvalidManifest,
                        "A tampered php.ini was accepted.");
                });
            });

            RunCase(output, "wrong authority", ref passed, ref total, delegate
            {
                WithFixture(sourceBase, delegate(string root)
                {
                    string path = Path.Combine(root, PhpRuntimeValidator.ManifestFileName);
                    string xml = File.ReadAllText(path);
                    File.WriteAllText(path, xml.Replace(
                        "php-8.5.9-nts-win32-vs17-x64",
                        "php-8.5.9-ts-win32-vs17-x64"), new UTF8Encoding(false));
                    PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateConfiguredManifest(root);
                    Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.InvalidManifest,
                        "A manifest with the wrong authority was accepted.");
                });
            });

            RunCase(output, "manifest hash authority", ref passed, ref total, delegate
            {
                WithFixture(sourceBase, delegate(string root)
                {
                    string manifestPath = Path.Combine(root, PhpRuntimeValidator.ManifestFileName);
                    XDocument document = XDocument.Load(manifestPath);
                    XElement file = document.Root.Elements("File").First();
                    byte[] payload = Encoding.ASCII.GetBytes("tampered-runtime-image");
                    file.SetAttributeValue("Size", payload.Length.ToString(CultureInfo.InvariantCulture));
                    file.SetAttributeValue("Sha256", ComputeSha256(payload));
                    document.Save(manifestPath);

                    string runtimeRoot = Path.Combine(root, "php");
                    string installedPath = Path.Combine(
                        runtimeRoot,
                        file.Attribute("Path").Value.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(installedPath));
                    File.WriteAllBytes(installedPath, payload);

                    PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes(
                        runtimeRoot,
                        root);
                    Require(
                        !result.IsValid
                        && result.Failure == PhpRuntimeValidationFailure.InvalidManifest
                        && result.Message.IndexOf("SHA-256", StringComparison.Ordinal) >= 0,
                        "A substituted runtime and matching edited manifest were accepted.");
                });
            });

            RunCase(output, "DTD rejection", ref passed, ref total, delegate
            {
                WithFixture(sourceBase, delegate(string root)
                {
                    string path = Path.Combine(root, PhpRuntimeValidator.ManifestFileName);
                    File.WriteAllText(path, "<!DOCTYPE x [<!ENTITY y SYSTEM 'file:///c:/windows/win.ini'>]><x>&y;</x>");
                    PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateConfiguredManifest(root);
                    Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.InvalidManifest,
                        "A manifest containing a DTD was accepted.");
                });
            });

            RunCase(output, "missing configuration", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes(null, sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.MissingConfiguration,
                    "A missing runtime configuration was accepted.");
            });

            RunCase(output, "UNC rejection", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes(
                    @"\\server\share\php",
                    sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.NonLocalPath,
                    "A UNC runtime was accepted.");
            });

            RunCase(output, "URI rejection", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes(
                    "https://example.invalid/php",
                    sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.NonLocalPath,
                    "A URI runtime was accepted.");
            });

            RunCase(output, "malformed path", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes("bad\0path", sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.InvalidPath,
                    "A malformed runtime path was accepted.");
            });

            RunCase(output, "wrong executable", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes("php.exe", sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.InvalidPath,
                    "An arbitrary executable path was accepted.");
            });

            RunCase(output, "missing directory", ref passed, ref total, delegate
            {
                PhpRuntimeValidationResult result = PhpRuntimeValidator.ValidateWithoutProbes(
                    "missing-php-runtime-for-self-test",
                    sourceBase);
                Require(!result.IsValid && result.Failure == PhpRuntimeValidationFailure.MissingDirectory,
                    "A missing runtime directory was accepted.");
            });

            RunCase(output, "exclusive lease", ref passed, ref total, delegate
            {
                string root = NewTemporaryDirectory();
                try
                {
                    using (IDisposable first = PhpRuntimeValidator.AcquireRuntimeLease("php", root))
                    {
                        bool rejected = false;
                        try
                        {
                            using (PhpRuntimeValidator.AcquireRuntimeLease("php", root))
                            {
                            }
                        }
                        catch (IOException)
                        {
                            rejected = true;
                        }

                        Require(rejected, "A concurrent PHP runtime lease was accepted.");
                    }
                }
                finally
                {
                    Directory.Delete(root, true);
                }
            });

            output.WriteLine(
                "PHP runtime self-tests: " + passed.ToString() + "/" + total.ToString() + " PASS");
            return passed == total;
        }

        private static string BuildIniFixture(string loadedIniPath, string additionalFiles)
        {
            return "Configuration File (php.ini) Path:\r\n"
                   + "Loaded Configuration File: \"" + Path.GetFullPath(loadedIniPath) + "\"\r\n"
                   + "Scan for additional .ini files in: (none)\r\n"
                   + "Additional .ini files parsed: " + additionalFiles + "\r\n";
        }

        private static void RequireProbeRejected(Action action, string message)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Require(rejected, message);
        }

        private static void WithFixture(string sourceBase, Action<string> action)
        {
            string root = NewTemporaryDirectory();
            try
            {
                File.Copy(
                    Path.Combine(sourceBase, PhpRuntimeValidator.ManifestFileName),
                    Path.Combine(root, PhpRuntimeValidator.ManifestFileName));
                File.Copy(
                    Path.Combine(sourceBase, PhpRuntimeValidator.ConfigurationFileName),
                    Path.Combine(root, PhpRuntimeValidator.ConfigurationFileName));
                action(root);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string NewTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "AORebirth-PhpRuntimeSelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(
                    sha256.ComputeHash(bytes).Select(
                        value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void RunCase(
            TextWriter output,
            string name,
            ref int passed,
            ref int total,
            Action action)
        {
            total++;
            try
            {
                action();
                passed++;
                output.WriteLine("[PASS] " + name);
            }
            catch (Exception exception)
            {
                output.WriteLine("[FAIL] " + name + ": " + exception.Message);
            }
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
