using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1 && args.Length != 2 && args.Length != 5)
            {
                Console.Error.WriteLine(
                    "Usage: Stage7OfflineSmokeTests <repository-root> [LoginEngine-publish-directory [--structure-only <runtime-id> <package-kind>]]");
                return 2;
            }

            bool structureOnly = args.Length == 5 && string.Equals(args[2], "--structure-only", StringComparison.Ordinal);
            if (args.Length == 5 && !structureOnly)
            {
                Console.Error.WriteLine("The only supported publish-only mode is --structure-only <runtime-id> <package-kind>.");
                return 2;
            }

            Stage7ContractFingerprint.VerifyOffline(
                typeof(LoginEngine.CoreClient.Client).Assembly,
                typeof(AORebirth.Core.Components.IBus).Assembly);
            Stage7ContractFingerprint.VerifyRepository(args[0]);

            if (args.Length >= 2)
            {
                string runtimeIdentifier = structureOnly ? args[3] : "linux-x64";
                string packageKind = structureOnly ? args[4] : "framework-dependent";
                Stage7ContractFingerprint.VerifyPublish(args[0], args[1], runtimeIdentifier, packageKind);
                if (!structureOnly)
                {
                    VerifyPublishedValidationModes(args[1]);
                }
            }

            Console.WriteLine(
                structureOnly
                    ? "PASS: Stage 7 LoginEngine publish structure only (target runtime execution pending)"
                    : "PASS: Stage 7 offline LoginEngine runtime smoke");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + Unwrap(exception).Message);
            return 1;
        }
    }

    private static void VerifyPublishedValidationModes(string publishDirectory)
    {
        string publish = Path.GetFullPath(publishDirectory);
        string loginEngine = Path.Combine(publish, "LoginEngine.dll");
        string config = Path.Combine(publish, "Config.xml");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "AO_REBIRTH_CONFIG_PATH", config },
            {
                "AO_REBIRTH_MYSQL_CONNECTION",
                "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;Uid=stage7_offline;Pwd=stage7_offline"
            },
            { "AO_REBIRTH_EXPECTED_DATABASE", "aorebirth_chatengine_stage6" },
            { "AO_REBIRTH_REQUIRED_SQL_TYPE", "MySql" }
        };

        ProcessResult startup = RunDotNet(publish, loginEngine, new[] { "--validate-startup" }, environment);
        Assert(startup.ExitCode == 0, "Published LoginEngine --validate-startup failed: " + startup.StandardError);
        Assert(
            startup.StandardOutput.IndexOf(
                "LOGINENGINE_VALIDATION_OK mode=startup handlers=6 provider=MySql bindPolicy=Loopback address=127.0.0.1 nbug=disabled listeners=0",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation did not report the exact loopback listener-free contract.");

        var missingExpectedDatabaseEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_EXPECTED_DATABASE"] = string.Empty
        };
        ProcessResult missingExpectedDatabase = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            missingExpectedDatabaseEnvironment);
        Assert(
            missingExpectedDatabase.ExitCode != 0
            && missingExpectedDatabase.StandardError.IndexOf(
                "AO_REBIRTH_EXPECTED_DATABASE is required",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted a missing expected-database identity.");

        var mismatchedDatabaseEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_MYSQL_CONNECTION"] =
                "Server=127.0.0.1;Port=33067;Database=stage7_wrong_database;Uid=stage7_offline;Pwd=stage7_offline"
        };
        ProcessResult mismatchedDatabase = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            mismatchedDatabaseEnvironment);
        Assert(
            mismatchedDatabase.ExitCode != 0
            && mismatchedDatabase.StandardError.IndexOf(
                "The configured database does not match AO_REBIRTH_EXPECTED_DATABASE",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted a mismatched database identity.");

        var missingSecretEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_MYSQL_CONNECTION"] = string.Empty
        };
        ProcessResult missingSecret = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            missingSecretEnvironment);
        Assert(
            missingSecret.ExitCode != 0
            && missingSecret.StandardError.IndexOf(
                "AO_REBIRTH_MYSQL_CONNECTION is required",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted a missing database secret.");

        var explicitLoopbackEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_BIND_MODE"] = "Loopback"
        };
        ProcessResult explicitLoopback = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            explicitLoopbackEnvironment);
        Assert(
            explicitLoopback.ExitCode == 0
            && explicitLoopback.StandardOutput.IndexOf(
                "bindPolicy=Loopback address=127.0.0.1",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation rejected explicit Loopback mode.");

        var publicEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_BIND_MODE"] = "Public",
            ["AO_REBIRTH_CONFIG_PATH"] = CreateConfigWithZoneIp(config, "203.0.113.10")
        };
        try
        {
            ProcessResult publicStartup = RunDotNet(
                publish,
                loginEngine,
                new[] { "--validate-startup" },
                publicEnvironment);
            Assert(
                publicStartup.ExitCode == 0
                && publicStartup.StandardOutput.IndexOf(
                    "bindPolicy=Public address=0.0.0.0",
                    StringComparison.Ordinal) >= 0,
                "Published LoginEngine startup validation rejected explicit Public mode.");
        }
        finally
        {
            DeleteIfExists(publicEnvironment["AO_REBIRTH_CONFIG_PATH"]);
        }

        var publicLoopbackZoneEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_BIND_MODE"] = "Public"
        };
        ProcessResult publicLoopbackZone = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            publicLoopbackZoneEnvironment);
        Assert(
            publicLoopbackZone.ExitCode != 0
            && publicLoopbackZone.StandardError.IndexOf(
                "ZoneIP must be a concrete non-loopback IP address when AO_REBIRTH_BIND_MODE=Public",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted a loopback ZoneIP in Public mode.");

        var invalidBindModeEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_BIND_MODE"] = "Internet"
        };
        ProcessResult invalidBindMode = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            invalidBindModeEnvironment);
        Assert(
            invalidBindMode.ExitCode != 0
            && invalidBindMode.StandardError.IndexOf(
                "AO_REBIRTH_BIND_MODE must be Loopback or Public",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted an invalid bind mode.");

        var whitespaceBindModeEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["AO_REBIRTH_BIND_MODE"] = "   "
        };
        ProcessResult whitespaceBindMode = RunDotNet(
            publish,
            loginEngine,
            new[] { "--validate-startup" },
            whitespaceBindModeEnvironment);
        Assert(
            whitespaceBindMode.ExitCode != 0
            && whitespaceBindMode.StandardError.IndexOf(
                "AO_REBIRTH_BIND_MODE must be Loopback or Public",
                StringComparison.Ordinal) >= 0,
            "Published LoginEngine startup validation accepted a whitespace bind mode.");

        string lifecycleDirectory = Path.Combine(Path.GetTempPath(), "aorebirth-stage7-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lifecycleDirectory);
        try
        {
            string shutdownFile = Path.Combine(lifecycleDirectory, "shutdown.request");
            File.WriteAllText(shutdownFile, string.Empty);
            ProcessResult lifecycle = RunDotNet(
                publish,
                loginEngine,
                new[] { "--validate-lifecycle", "--shutdown-file", shutdownFile },
                environment);
            Assert(lifecycle.ExitCode == 0, "Published LoginEngine --validate-lifecycle failed: " + lifecycle.StandardError);
            Assert(
                lifecycle.StandardOutput.IndexOf(
                    "LOGINENGINE_VALIDATION_OK mode=lifecycle status=clean listeners=0",
                    StringComparison.Ordinal) >= 0,
                "Published LoginEngine lifecycle validation did not report clean listener-free shutdown.");
        }
        finally
        {
            if (Directory.Exists(lifecycleDirectory)) Directory.Delete(lifecycleDirectory, true);
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
            Arguments = Quote(assemblyPath) + " " + string.Join(" ", QuoteAll(arguments)),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (KeyValuePair<string, string> pair in environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using (Process process = Process.Start(startInfo))
        {
            if (process == null) throw new InvalidOperationException("Could not start dotnet for the published LoginEngine validation.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(true);
                process.WaitForExit();
                throw new TimeoutException("Published LoginEngine validation did not exit within 30 seconds.");
            }

            return new ProcessResult(
                process.ExitCode,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult());
        }
    }

    private static string CreateConfigWithZoneIp(string sourceConfig, string zoneIp)
    {
        string configText = File.ReadAllText(sourceConfig);
        string updatedConfig = configText.Replace("<ZoneIP>127.0.0.1</ZoneIP>", "<ZoneIP>" + zoneIp + "</ZoneIP>");
        if (string.Equals(configText, updatedConfig, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Could not create public-mode LoginEngine config fixture.");
        }

        string path = Path.Combine(Path.GetTempPath(), "aorebirth-stage7-login-public-" + Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, updatedConfig);
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static IEnumerable<string> QuoteAll(IEnumerable<string> values)
    {
        foreach (string value in values) yield return Quote(value);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
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
        internal string StandardOutput { get; private set; }
        internal string StandardError { get; private set; }
    }
}
