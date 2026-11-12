using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class DoaEnforcement
{
    private const int ProcessTimeoutMilliseconds = 300_000;

    internal static int Run(string root, string[] args)
    {
        bool structureOnly = args[0] == "--enforce-structure";
        if (args.Length < 3 || !Regex.IsMatch(args[2], "^[0-9a-fA-F]{40}$"))
        {
            Console.Error.WriteLine("Usage: --enforce <checkout> <exact-base-sha> --catalog-root <GameData> --native-graph <build-dir> [--output <report>]");
            Console.Error.WriteLine("       --enforce-structure <checkout> <exact-base-sha> --native-graph <build-dir> [--output <report>]");
            return 64;
        }

        string? catalogRoot = null, output = null, nativeGraph = null;
        for (int index = 3; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1])
                || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("DOA_INVALID_VALIDATION_OPTION=" + args[index]);
                return 64;
            }
            switch (args[index])
            {
                case "--catalog-root" when !structureOnly && catalogRoot == null: catalogRoot = Path.GetFullPath(args[index + 1]); break;
                case "--native-graph" when nativeGraph == null: nativeGraph = Path.GetFullPath(args[index + 1]); break;
                case "--output" when output == null: output = Path.GetFullPath(args[index + 1]); break;
                default:
                    Console.Error.WriteLine("DOA_INVALID_VALIDATION_OPTION=" + args[index]);
                    return 64;
            }
        }
        if (!structureOnly && catalogRoot == null)
        {
            Console.Error.WriteLine("DOA_MISSING_REQUIRED_INPUT=--catalog-root <GameData>");
            return 1;
        }

        string baselinePath = Path.GetTempFileName();
        string proposedPath = Path.GetTempFileName();
        try
        {
            string baseline = args[2].ToLowerInvariant();
            RunChild(root, "git", ["merge-base", "--is-ancestor", baseline, "HEAD"], [0]);
            string guard = typeof(DoaEnforcement).Assembly.Location;
            RunChild(root, "dotnet", [guard, "--baseline", root, baseline, "--doa-inventory", "--output", baselinePath], [0]);
            var proposedArguments = new List<string> { guard, "--write", root, "--doa-inventory", "--evaluated-graph", "--output", proposedPath };
            if (nativeGraph != null) proposedArguments.AddRange(["--native-graph", nativeGraph]);
            RunChild(root, "dotnet", proposedArguments.ToArray(), [0, 1]);

            using JsonDocument before = JsonDocument.Parse(File.ReadAllText(baselinePath));
            using JsonDocument after = JsonDocument.Parse(File.ReadAllText(proposedPath));
            Finding[] baselineFindings = ReadFindings(before.RootElement);
            Finding[] proposedFindings = ReadFindings(after.RootElement);
            (Finding[] inherited, Finding[] introduced) = Compare(baselineFindings, proposedFindings);
            string[] baselineUnresolved = ReadUnresolved(before.RootElement);
            string[] unresolved = ReadUnresolved(after.RootElement);
            string[] introducedProjectInputs = unresolved.Except(baselineUnresolved, StringComparer.Ordinal).ToArray();
            Finding[] authoritative = catalogRoot == null ? [] : ReadAuthorityFindings(root, catalogRoot).ToArray();
            // The baseline classifies findings for the report. Acceptance uses the proposed inventory directly.
            string status = AcceptanceStatus(structureOnly, catalogRoot != null, proposedFindings.Length,
                unresolved.Length, authoritative.Length);
            var report = new
            {
                status,
                validationScope = structureOnly ? "PUBLIC_STRUCTURE" : "STRUCTURE_AND_AUTHORITATIVE_REFERENCES",
                baselineRevision = baseline,
                proposedRevision = after.RootElement.GetProperty("sourceRevision").GetString(),
                sourceGraph = after.RootElement.GetProperty("sourceGraphMethod").GetString(),
                runtimeCSharpFilesAudited = after.RootElement.GetProperty("runtimeCSharpFilesAudited").GetInt32(),
                nativeFilesAudited = after.RootElement.GetProperty("nativeFilesAudited").GetInt32(),
                sourceGraphLimitations = after.RootElement.GetProperty("sourceGraphLimitations").Clone(),
                evaluatedBuildGraph = after.RootElement.GetProperty("evaluatedBuildGraph").Clone(),
                nativeBuildGraph = after.RootElement.GetProperty("nativeBuildGraph").Clone(),
                authoritativeReferencesEvaluated = catalogRoot != null,
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
            Console.WriteLine($"DOA_TOTAL_CURRENT_FINDINGS={proposedFindings.Length}");
            Console.WriteLine($"DOA_UNRESOLVED_PROJECT_INPUTS={unresolved.Length}");
            Console.WriteLine($"DOA_AUTHORITATIVE_REFERENCES={(structureOnly ? "NOT_EVALUATED" : authoritative.Length == 0 ? "PASS" : "FAIL")}");
            Console.WriteLine($"DAO_EXISTING_VIOLATIONS={inherited.Length}");
            Console.WriteLine($"DAO_NEW_VIOLATIONS={introduced.Length}");
            Console.WriteLine($"DAO_PUBLIC_STRUCTURE={(proposedFindings.Length == 0 && unresolved.Length == 0 ? "PASS" : "FAIL")}");
            foreach (Finding finding in inherited.Concat(introduced).Concat(authoritative))
                Console.Error.WriteLine($"BLOCK {finding.Path}:{finding.Line} {finding.Rule} {finding.Symbol}: {finding.Basis}");
            foreach (string finding in unresolved)
                Console.Error.WriteLine("BLOCK unresolved project input: " + finding);
            Console.WriteLine("DOA_ENFORCEMENT=" + status);
            return status == "FAIL_ARCHITECTURE" ? 1 : 0;
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

    private static IEnumerable<Finding> ReadAuthorityFindings(string checkout, string root)
    {
        foreach (Finding finding in DoaRules.AuthoritativeReferenceIssues(root))
            yield return finding;

        string policyPath = Path.Combine(root, DoaAdditionalInputs.MonsterPolicy);
        foreach (Finding finding in DoaAdditionalInputs.MonsterPolicyIssues(DoaAdditionalInputs.MonsterPolicy,
                     File.Exists(policyPath) ? File.ReadAllText(policyPath) : null))
            yield return finding;

        string lftPolicyPath = Path.Combine(root, DoaAdditionalInputs.LftPolicy);
        string? selected = File.Exists(lftPolicyPath) ? File.ReadAllText(lftPolicyPath) : null;
        foreach (Finding finding in DoaAdditionalInputs.LftPolicyIssues(DoaAdditionalInputs.LftPolicy, selected,
                     id => DoaAdditionalInputs.LftMetadataMatches(root, id)))
            yield return finding;

        string proposedRelative = "AORebirth/GameData/" + DoaAdditionalInputs.LftPolicy;
        string proposedPath = Path.Combine(checkout, proposedRelative);
        if (!File.Exists(proposedPath)) yield break;
        string proposed = File.ReadAllText(proposedPath);
        if (string.Equals(proposed, selected, StringComparison.Ordinal)) yield break;
        foreach (Finding finding in DoaAdditionalInputs.LftPolicyIssues(proposedRelative, proposed,
                     id => DoaAdditionalInputs.LftMetadataMatches(root, id)))
            yield return finding;
    }

    internal static string AcceptanceStatus(bool structureOnly, bool catalogSupplied, int currentFindings,
        int unresolvedInputs, int authoritativeFindings)
    {
        if ((!structureOnly && !catalogSupplied) || currentFindings != 0
            || unresolvedInputs != 0 || authoritativeFindings != 0)
            return "FAIL_ARCHITECTURE";

        return structureOnly ? "PASS_STRUCTURE_ONLY" : "PASS_ARCHITECTURE_AND_REFERENCES";
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
