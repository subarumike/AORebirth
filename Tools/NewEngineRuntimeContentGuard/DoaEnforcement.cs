using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class DoaEnforcement
{
    private const int ProcessTimeoutMilliseconds = 180_000;

    internal static int Run(string root, string[] args)
    {
        if (args.Length < 3 || !Regex.IsMatch(args[2], "^[0-9a-fA-F]{40}$"))
        {
            Console.Error.WriteLine("Usage: --enforce <checkout> <exact-base-sha> [--catalog-root <GameData>] [--output <report>]");
            return 64;
        }

        string? catalogRoot = null, output = null;
        for (int index = 3; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length) return 64;
            switch (args[index])
            {
                case "--catalog-root" when catalogRoot == null: catalogRoot = Path.GetFullPath(args[index + 1]); break;
                case "--output" when output == null: output = Path.GetFullPath(args[index + 1]); break;
                default: return 64;
            }
        }

        string baselinePath = Path.GetTempFileName();
        string proposedPath = Path.GetTempFileName();
        try
        {
            string baseline = args[2].ToLowerInvariant();
            RunChild(root, "git", ["merge-base", "--is-ancestor", baseline, "HEAD"], [0]);
            string guard = typeof(DoaEnforcement).Assembly.Location;
            RunChild(root, "dotnet", [guard, "--baseline", root, baseline, "--doa-inventory", "--output", baselinePath], [0]);
            RunChild(root, "dotnet", [guard, "--write", root, "--doa-inventory", "--output", proposedPath], [0, 1]);

            using JsonDocument before = JsonDocument.Parse(File.ReadAllText(baselinePath));
            using JsonDocument after = JsonDocument.Parse(File.ReadAllText(proposedPath));
            Finding[] baselineFindings = ReadFindings(before.RootElement);
            Finding[] proposedFindings = ReadFindings(after.RootElement);
            (Finding[] inherited, Finding[] introduced) = Compare(baselineFindings, proposedFindings);
            string[] baselineUnresolved = ReadUnresolved(before.RootElement);
            string[] unresolved = ReadUnresolved(after.RootElement);
            string[] introducedProjectInputs = unresolved.Except(baselineUnresolved, StringComparer.Ordinal).ToArray();
            Finding[] authoritative = catalogRoot == null ? [] : DoaRules.AuthoritativeReferenceIssues(catalogRoot).ToArray();
            bool passed = introduced.Length == 0 && introducedProjectInputs.Length == 0 && authoritative.Length == 0;
            var report = new
            {
                status = passed ? "PASS_PROPOSED_CHANGES" : "FAIL_ARCHITECTURE",
                baselineRevision = baseline,
                sourceGraph = after.RootElement.GetProperty("sourceGraphMethod").GetString(),
                runtimeCSharpFilesAudited = after.RootElement.GetProperty("runtimeCSharpFilesAudited").GetInt32(),
                authoritativeGameDataChecked = catalogRoot != null,
                inheritedOpenFindings = inherited,
                introducedFindings = introduced,
                unresolvedProjectInputs = unresolved,
                introducedProjectInputs,
                authoritativeReferenceFindings = authoritative
            };
            if (output != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
            Console.WriteLine($"DOA_BASELINE={baseline}");
            Console.WriteLine($"DOA_INHERITED_OPEN_FINDINGS={inherited.Length}");
            Console.WriteLine($"DOA_INTRODUCED_FINDINGS={introduced.Length}");
            Console.WriteLine($"DOA_AUTHORITATIVE_GAMEDATA={(catalogRoot == null ? "NOT_SUPPLIED" : "CHECKED")}");
            foreach (Finding finding in inherited)
                Console.WriteLine($"OPEN {finding.Path}:{finding.Line} {finding.Rule} {finding.Symbol}: {finding.Basis}");
            foreach (Finding finding in introduced.Concat(authoritative))
                Console.Error.WriteLine($"BLOCK {finding.Path}:{finding.Line} {finding.Rule} {finding.Symbol}: {finding.Basis}");
            foreach (string finding in introducedProjectInputs)
                Console.Error.WriteLine("BLOCK unresolved project input: " + finding);
            Console.WriteLine("DOA_ENFORCEMENT=" + (passed ? "PASS_PROPOSED_CHANGES" : "FAIL"));
            return passed ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException or TimeoutException)
        {
            Console.Error.WriteLine("DOA_ENFORCEMENT_ERROR=" + exception.Message);
            return 2;
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(proposedPath);
        }
    }

    internal static (Finding[] Inherited, Finding[] Introduced) Compare(IEnumerable<Finding> baseline, IEnumerable<Finding> proposed)
    {
        static (string, string, string, string?) Key(Finding finding)
            => (finding.Path, finding.Rule, finding.Symbol, finding.Fingerprint);
        var remaining = baseline.GroupBy(Key).ToDictionary(group => group.Key, group => group.Count());
        var inherited = new List<Finding>();
        var introduced = new List<Finding>();
        foreach (Finding finding in proposed)
        {
            if (finding.Fingerprint != null && remaining.TryGetValue(Key(finding), out int count) && count > 0)
            {
                inherited.Add(finding);
                remaining[Key(finding)] = count - 1;
            }
            else introduced.Add(finding);
        }
        return (inherited.ToArray(), introduced.ToArray());
    }

    private static Finding[] ReadFindings(JsonElement report)
        => report.GetProperty("findings").Deserialize<Finding[]>() ?? throw new InvalidDataException("Missing guard findings.");

    private static string[] ReadUnresolved(JsonElement report)
        => report.GetProperty("unresolvedProjectInputs").Deserialize<string[]>() ?? throw new InvalidDataException("Missing project inputs.");

    private static void RunChild(string root, string executable, string[] arguments, int[] allowedCodes)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start " + executable);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"{executable} exceeded the {ProcessTimeoutMilliseconds / 1000}-second audit bound.");
        }
        if (!allowedCodes.Contains(process.ExitCode))
            throw new InvalidOperationException($"{executable} exited {process.ExitCode}. " + stderr.GetAwaiter().GetResult() + stdout.GetAwaiter().GetResult());
    }
}
