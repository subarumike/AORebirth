using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args.Length == 3 || args.Length == 4 || args.Length > 5)
            {
                Console.Error.WriteLine(
                    "Usage: Stage5OfflineSmokeTests <repository-root> [ChatEngine-publish-directory [--structure-only <runtime-id> <package-kind>]]");
                return 2;
            }

            bool structureOnly = args.Length == 5
                                 && string.Equals(args[2], "--structure-only", StringComparison.Ordinal);
            if (args.Length == 5 && !structureOnly)
            {
                Console.Error.WriteLine("The only supported publish-only mode is --structure-only <runtime-id> <package-kind>.");
                return 2;
            }

            Stage5ContractFingerprint.VerifyOffline(
                typeof(ChatEngine.PacketWriter).Assembly,
                typeof(AO.Core.Encryption.BigInteger).Assembly);
            Stage5RepositoryChecks.VerifyRepository(args[0]);
            if (args.Length >= 2)
            {
                if (structureOnly)
                {
                    Stage5RepositoryChecks.VerifyPublish(args[0], args[1], args[3], args[4]);
                }
                else
                {
                    Stage5RepositoryChecks.VerifyPublish(args[0], args[1]);
                }
                if (!structureOnly)
                {
                    VerifyPublishedValidationModes(args[1]);
                }
            }

            Console.WriteLine(
                structureOnly
                    ? "PASS: Stage 5 publish structure only (target runtime execution pending)"
                    : "PASS: Stage 5 offline ChatEngine runtime smoke");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void VerifyPublishedValidationModes(string publishDirectory)
    {
        string publish = Path.GetFullPath(publishDirectory);
        string chatEngine = Path.Combine(publish, "ChatEngine.dll");
        string config = Path.Combine(publish, "Config.xml");
        var startupEnvironment = new Dictionary<string, string>
        {
            { "AO_REBIRTH_CONFIG_PATH", config },
            {
                "AO_REBIRTH_MYSQL_CONNECTION",
                "Server=127.0.0.1;Database=offline;Uid=offline;Pwd=offline"
            },
            { "AO_REBIRTH_CHAT_LISTEN_IP", "127.0.0.1" },
            { "AO_REBIRTH_ISCOM_LISTEN_IP", "127.0.0.1" },
            { "AO_REBIRTH_REQUIRED_SQL_TYPE", "MySql" }
        };

        ProcessResult startup = RunDotNet(
            publish,
            chatEngine,
            new[] { "--validate-startup" },
            startupEnvironment);
        Assert(startup.ExitCode == 0, "Published --validate-startup failed: " + startup.StandardError);
        Assert(
            startup.StandardOutput.Contains("CHATENGINE_VALIDATION_OK mode=startup", StringComparison.Ordinal)
            && startup.StandardOutput.Contains("listeners=0", StringComparison.Ordinal),
            "Published --validate-startup did not report its listener-free contract.");

        var missingSecretEnvironment = new Dictionary<string, string>(startupEnvironment)
        {
            ["AO_REBIRTH_MYSQL_CONNECTION"] = string.Empty
        };
        ProcessResult missingSecret = RunDotNet(
            publish,
            chatEngine,
            new[] { "--validate-startup" },
            missingSecretEnvironment);
        Assert(
            missingSecret.ExitCode != 0
            && missingSecret.StandardError.Contains(
                "AO_REBIRTH_MYSQL_CONNECTION is required",
                StringComparison.Ordinal),
            "Published startup validation accepted a missing deployment database secret.");

        string configurationDirectory = Path.Combine(
            Path.GetTempPath(),
            "aorebirth-stage5-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configurationDirectory);
        try
        {
            string sourceConfiguration = File.ReadAllText(config);
            string secretConfiguration = Path.Combine(configurationDirectory, "Config.xml");
            string secretText = sourceConfiguration.Replace(
                "REPLACE_WITH_LOCAL_PASSWORD",
                "not-allowed-in-config");
            Assert(!string.Equals(secretText, sourceConfiguration, StringComparison.Ordinal),
                "Published Config.xml no longer contains the expected credential placeholder.");
            File.WriteAllText(secretConfiguration, secretText);
            var secretConfigurationEnvironment = new Dictionary<string, string>(startupEnvironment)
            {
                ["AO_REBIRTH_CONFIG_PATH"] = secretConfiguration
            };
            ProcessResult embeddedSecret = RunDotNet(
                publish,
                chatEngine,
                new[] { "--validate-startup" },
                secretConfigurationEnvironment);
            Assert(
                embeddedSecret.ExitCode != 0
                && embeddedSecret.StandardError.Contains(
                    "Config.xml must contain only a placeholder MySQL connection",
                    StringComparison.Ordinal),
                "Published startup validation accepted an operational database secret in Config.xml.");

            string alternateProviderConfiguration = Path.Combine(
                configurationDirectory,
                "AlternateProvider.xml");
            string alternateProviderText = sourceConfiguration.Replace(
                "<SQLType>MySql</SQLType>",
                "<SQLType>PostgreSQL</SQLType>");
            Assert(!string.Equals(alternateProviderText, sourceConfiguration, StringComparison.Ordinal),
                "Published Config.xml no longer contains the expected MySql provider selection.");
            File.WriteAllText(alternateProviderConfiguration, alternateProviderText);
            var alternateProviderEnvironment = new Dictionary<string, string>(startupEnvironment)
            {
                ["AO_REBIRTH_CONFIG_PATH"] = alternateProviderConfiguration
            };
            ProcessResult alternateProvider = RunDotNet(
                publish,
                chatEngine,
                new[] { "--validate-startup" },
                alternateProviderEnvironment);
            Assert(
                alternateProvider.ExitCode != 0
                && alternateProvider.StandardError.Contains(
                    "supports only the MySql provider",
                    StringComparison.Ordinal),
                "Published startup validation accepted a provider outside the Linux deployment boundary.");
        }
        finally
        {
            if (Directory.Exists(configurationDirectory))
            {
                Directory.Delete(configurationDirectory, true);
            }
        }

        string lifecycleDirectory = Path.Combine(
            Path.GetTempPath(),
            "aorebirth-stage5-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lifecycleDirectory);
        try
        {
            string shutdownFile = Path.Combine(lifecycleDirectory, "shutdown.request");
            File.WriteAllText(shutdownFile, string.Empty);
            ProcessResult lifecycle = RunDotNet(
                publish,
                chatEngine,
                new[] { "--validate-lifecycle", "--shutdown-file", shutdownFile },
                new Dictionary<string, string>());
            Assert(lifecycle.ExitCode == 0, "Published --validate-lifecycle failed: " + lifecycle.StandardError);
            Assert(
                lifecycle.StandardOutput.Contains("CHATENGINE_LIFECYCLE_READY listeners=0", StringComparison.Ordinal)
                && lifecycle.StandardOutput.Contains("CHATENGINE_LIFECYCLE_STOPPED status=clean", StringComparison.Ordinal),
                "Published --validate-lifecycle did not report a clean listener-free stop.");
            Assert(!File.Exists(shutdownFile), "Published lifecycle validation did not consume its shutdown file.");
        }
        finally
        {
            if (Directory.Exists(lifecycleDirectory))
            {
                Directory.Delete(lifecycleDirectory, true);
            }
        }
    }

    private static ProcessResult RunDotNet(
        string workingDirectory,
        string assemblyPath,
        IEnumerable<string> arguments,
        IDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assemblyPath);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (string inheritedVariable in new[]
        {
            "AO_REBIRTH_CONFIG_PATH",
            "AO_REBIRTH_MYSQL_CONNECTION",
            "AO_REBIRTH_CHAT_LISTEN_IP",
            "AO_REBIRTH_ISCOM_LISTEN_IP",
            "AO_REBIRTH_REQUIRED_SQL_TYPE",
            "NOTIFY_SOCKET"
        })
        {
            startInfo.Environment.Remove(inheritedVariable);
        }

        foreach (KeyValuePair<string, string> entry in environment)
        {
            startInfo.Environment[entry.Key] = entry.Value;
        }

        using (var process = new Process { StartInfo = startInfo })
        {
            Assert(process.Start(), "Unable to start published ChatEngine validation.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill(true);
                process.WaitForExit();
                throw new InvalidOperationException("Published ChatEngine validation timed out.");
            }

            return new ProcessResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ProcessResult
    {
        internal ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            this.ExitCode = exitCode;
            this.StandardOutput = standardOutput;
            this.StandardError = standardError;
        }

        internal int ExitCode { get; private set; }

        internal string StandardError { get; private set; }

        internal string StandardOutput { get; private set; }
    }
}
