using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class HistoricalCatalog
{
    internal static readonly string[] Buckets =
    ["RubiKa", "NotumWars", "Shadowlands", "AlienInvasion", "LostEden", "Xan", "OtherContent", "MultipleExpansions", "Unknown"];

    static readonly JsonSerializerOptions InputOptions = new(Program.JsonOptions) { PropertyNameCaseInsensitive = true };
    static readonly UTF8Encoding Utf8 = new(false);
    const string EligibleChest = "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST";
    const string CurrentFieldsDifference = "EXPORTED_FIELDS_DIFFER_FROM_CURRENT_CANONICAL";

    internal static void Run(string gameDataRoot, string researchRoot, string reportPath, bool verify)
    {
        gameDataRoot = Path.GetFullPath(gameDataRoot);
        researchRoot = Path.GetFullPath(researchRoot);
        reportPath = Path.GetFullPath(reportPath);
        string classificationRoot = Path.Combine(gameDataRoot, "Loot", "ExpansionClassification");
        string historicalRoot = Path.Combine(classificationRoot, "HistoricalReconstruction");
        string destination = Path.Combine(historicalRoot, "catalogs");
        if (reportPath.StartsWith(classificationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The requested external report must not replace a classification or research input.");

        string itemsPath = Path.Combine(gameDataRoot, "items.dat");
        string familiesPath = Path.Combine(gameDataRoot, "ItemTemplates.json");
        string bundlePath = Path.Combine(researchRoot, "HistoricalEvidence.json");
        string observationsPath = Path.Combine(researchRoot, "ItemObservations.jsonl");
        string itemsHash = Program.HashFile(itemsPath);
        string? familiesHash = File.Exists(familiesPath) ? Program.HashFile(familiesPath) : null;
        string evidenceHash = Program.HashFile(bundlePath);
        string observationsHash = Program.HashFile(observationsPath);
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [itemsPath] = itemsHash, [bundlePath] = evidenceHash, [observationsPath] = observationsHash
        };
        if (familiesHash != null)
            inputs.Add(familiesPath, familiesHash);
        if (inputs.ContainsKey(reportPath))
            throw new ArgumentException("The external report cannot replace a selected source input.");

        BaselineState baseline = ReadBaseline(classificationRoot, itemsHash, familiesHash);
        HistoricalEvidenceBundle bundle;
        using (FileStream source = File.OpenRead(bundlePath))
            bundle = JsonSerializer.Deserialize<HistoricalEvidenceBundle>(source, InputOptions)
                ?? throw new InvalidDataException("Historical evidence bundle is empty.");
        if (bundle.FormatVersion != 1 || bundle.ItemsDatSha256 != itemsHash)
            throw new InvalidDataException("Historical evidence is not bound to the exact selected item database.");
        ValidateSources(bundle, researchRoot, inputs);
        var sourceUrls = bundle.Sources.ToDictionary(source => source.Id, source => source.Url, StringComparer.Ordinal);
        HistoricalAssertion[] resolvedAssertions = bundle.Assertions.Select(assertion =>
        {
            string[] urls = assertion.SourceUrls.Concat(assertion.SourceIds.Select(id => sourceUrls[id]))
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            return assertion with
            {
                SourceUrls = urls,
                AcquisitionSources = assertion.AcquisitionSources.Select(source => source with
                {
                    SourceUrls = source.SourceUrls.Length == 0 ? urls : source.SourceUrls
                }).ToArray()
            };
        }).ToArray();

        var observations = new List<HistoricalObservation>();
        foreach (string line in File.ReadLines(observationsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            HistoricalObservation observation = JsonSerializer.Deserialize<HistoricalObservation>(line, InputOptions)
                ?? throw new InvalidDataException("A historical observation is empty.");
            if (observation.TemplateId <= 0)
                throw new InvalidDataException("A historical observation has no positive original template identity.");
            observations.Add(observation);
        }
        var observationsById = observations.GroupBy(item => item.TemplateId)
            .ToDictionary(group => group.Key, group => SortObservations(group));
        var assertionsById = resolvedAssertions.GroupBy(item => item.TemplateId)
            .ToDictionary(group => group.Key, group => group.OrderBy(AssertionOrderKey, StringComparer.Ordinal).ToArray());

        List<DatItemTemplate> input = ItemsDatReader.Read(itemsPath);
        if (input.Count == 0)
            throw new InvalidDataException("Selected items.dat contains no definitions.");
        FamilyMetadata families = FamilyMetadata.Build(input,
            familiesHash == null ? null : File.ReadAllText(familiesPath));
        CatalogItem[] original = input.Select((item, index) => Program.BuildRecord(item, index, families)).ToArray();
        Program.ValidateCoverage(input, original);
        HistoricalRecord[] records = original.Select(item =>
            {
                HistoricalRecord record = BuildRecord(item,
                    assertionsById.GetValueOrDefault(item.TemplateId) ?? [],
                    observationsById.GetValueOrDefault(item.TemplateId) ?? [], itemsHash);
                int? canFlags = input[item.SourceRecordIndex].Stats.TryGetValue((int)CharacterStat.Can, out int value) ? value : null;
                bool? canCarry = canFlags.HasValue ? ((uint)canFlags.Value & (uint)CanFlags.Carry) != 0 : null;
                record = record with { CurrentCanFlags = canFlags, CurrentCanCarry = canCarry };
                if (record.ChestDisposition == EligibleChest && canCarry != true)
                    record = record with { ChestDisposition = "CURRENT_CARRY_CAPABILITY_UNVERIFIED_OR_UNAVAILABLE",
                        Unresolved = record.Unresolved.Append("DOCUMENTED_CHEST_SOURCE_RETAINED_BUT_CURRENT_CARRY_CAPABILITY_DOES_NOT_QUALIFY")
                            .Order(StringComparer.Ordinal).ToArray() };
                return record;
            })
            .OrderBy(item => item.TemplateId).ThenBy(item => item.StoredQuality)
            .ThenBy(item => item.SourceRecordIndex).ToArray();
        ValidateCoverage(input, records);

        var knownIds = input.Select(item => item.ID).ToHashSet();
        int[] historicalOnlyIds = observationsById.Keys.Concat(assertionsById.Keys).Distinct()
            .Where(id => !knownIds.Contains(id)).Order().ToArray();
        HistoricalPatch[] patches = bundle.Patches;
        Directory.CreateDirectory(historicalRoot);
        string stage = Path.Combine(historicalRoot, ".catalogs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var primaryFiles = new List<OutputIdentity>();
            foreach (string bucket in Buckets)
            {
                string name = bucket + ".json";
                HistoricalRecord[] selected = records.Where(item => item.PrimaryClassification == bucket).ToArray();
                WriteJson(Path.Combine(stage, name), selected);
                primaryFiles.Add(Identity(stage, name, selected.Length));
            }
            WriteJson(Path.Combine(stage, "ItemPatchHistory.json"), records.Select(item => new
            {
                item.SourceRecordIndex, item.TemplateId, item.Name, item.StoredQuality,
                item.MinimumSupportedQuality, item.MaximumSupportedQuality, item.QualityRangeEvidence,
                item.PrimaryClassification, item.ContentAssociations, item.RequiredExpansionAssociations,
                item.FirstKnownPatch, item.FirstRecordedAdditionPatch, item.ActualIntroductionStatus, item.DocumentedIntroductionPatch,
                item.PatchObservations, item.SourceUrls, item.Unresolved
            }));
            WriteJson(Path.Combine(stage, "ItemAcquisitionSources.json"), records.Select(item => new
            {
                item.SourceRecordIndex, item.TemplateId, item.Name, item.StoredQuality,
                item.MinimumSupportedQuality, item.MaximumSupportedQuality, item.PrimaryClassification,
                item.ContentAssociations, item.RequiredExpansionAssociations, item.AcquisitionSources,
                item.CurrentCanFlags, item.CurrentCanCarry,
                item.SpecialPurposeRoles, item.ChestDisposition, item.VerifiedSpecialPurposeExclusion,
                item.RetailChestAbsenceProven, item.VerifiedRkAccessible, item.SourceUrls, item.Unresolved
            }));
            WriteJson(Path.Combine(stage, "MissionChestEligibleRK.json"), records.Where(item => item.ChestDisposition == EligibleChest));
            WriteJson(Path.Combine(stage, "MissionChestExcluded.json"), records.Where(item => item.ChestDisposition != EligibleChest));
            WriteJson(Path.Combine(stage, "Sources.json"), bundle.Sources.OrderBy(source => source.Id, StringComparer.Ordinal));
            WriteJson(Path.Combine(stage, "PatchChronology.json"), patches);
            WriteJson(Path.Combine(stage, "ResearchGaps.json"), bundle.Gaps.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            WriteJson(Path.Combine(stage, "HistoricalOnlyItems.json"), historicalOnlyIds.Select(id => new
            {
                TemplateId = id,
                PatchObservations = observationsById.GetValueOrDefault(id) ?? [],
                Assertions = assertionsById.GetValueOrDefault(id) ?? [],
                Scope = "HISTORICAL_SOURCE_ID_ABSENT_FROM_CURRENT_INPUT_NOT_A_PRIMARY_ITEM_ASSIGNMENT"
            }));
            WriteJson(Path.Combine(stage, "BaselinePreservation.json"), baseline);
            var summary = new HistoricalSummary
            {
                ItemsDatSha256 = itemsHash, ItemTemplatesSha256 = familiesHash,
                EvidenceBundleSha256 = evidenceHash, ObservationsSha256 = observationsHash,
                ToolAssemblySha256 = Program.HashFile(Assembly.GetExecutingAssembly().Location),
                TotalInputRecords = records.Length, DistinctTemplateIds = knownIds.Count,
                BeforeClassificationCounts = baseline.ClassificationCounts,
                ContentClassificationCounts = Buckets.ToDictionary(bucket => bucket,
                    bucket => records.Count(item => item.PrimaryClassification == bucket), StringComparer.Ordinal),
                RequiredExpansionCounts = Program.Buckets.ToDictionary(bucket => bucket,
                    bucket => original.Count(item => item.ExpansionClassification == bucket), StringComparer.Ordinal),
                ItemsWithHistoricalObservations = records.Count(item => item.PatchObservations.Length > 0),
                ItemsWithRecordedAddition = records.Count(item => item.FirstRecordedAdditionPatch != null),
                HistoricalOnlyTemplateIds = historicalOnlyIds.Length,
                VerifiedRkAccessible = records.Count(item => item.VerifiedRkAccessible),
                MissionChestEligibleRk = records.Count(item => item.ChestDisposition == EligibleChest),
                VerifiedSpecialPurposeExcluded = records.Count(item => item.VerifiedSpecialPurposeExclusion),
                UnverifiedChestEligibility = records.Count(item => item.ChestDisposition != EligibleChest && !item.VerifiedSpecialPurposeExclusion),
                ObservedPatchLabels = patches.Select(item => item.Version).Concat(observations.Select(item => item.PatchVersion))
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Count(),
                AvailableSources = bundle.Sources.Count(source => source.Status.StartsWith("AVAILABLE", StringComparison.Ordinal)),
                UnavailableSourceRoutes = bundle.Sources.Count(source => !source.Status.StartsWith("AVAILABLE", StringComparison.Ordinal)),
                ObservationsByKind = observationsById.Values.SelectMany(values => values).Where(item => item.Kind != CurrentFieldsDifference)
                    .Concat(records.SelectMany(item => item.PatchObservations).Where(item => item.Kind == CurrentFieldsDifference))
                    .Concat(historicalOnlyIds.SelectMany(id => observationsById.GetValueOrDefault(id) ?? [])
                        .Where(item => item.Kind == CurrentFieldsDifference))
                    .GroupBy(item => item.Kind).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                PrimaryFiles = primaryFiles.ToArray()
            };
            WriteJson(Path.Combine(stage, "Summary.json"), summary);
            string report = HistoricalReport.Create(summary, records, bundle, baseline);
            File.WriteAllText(Path.Combine(stage, "ClassificationReport.md"), report, Utf8);

            if (!verify)
                AssertInputsUnchanged(inputs, familiesPath, familiesHash);
            AssertBaselineUnchanged(classificationRoot, baseline);
            Dictionary<string, string>? verifiedFiles = null;
            if (verify)
            {
                verifiedFiles = CompareDirectories(stage, destination);
                if (!File.Exists(reportPath) || !File.ReadAllBytes(reportPath).AsSpan().SequenceEqual(Utf8.GetBytes(report)))
                    throw new InvalidDataException("Historical reconstruction report differs during regeneration.");
                AssertInputsUnchanged(inputs, familiesPath, familiesHash);
            }
            else
            {
                Publish(stage, destination, historicalRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                string temporaryReport = reportPath + ".new-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporaryReport, report, Utf8);
                    File.Move(temporaryReport, reportPath, true);
                }
                finally
                {
                    if (File.Exists(temporaryReport))
                        File.Delete(temporaryReport);
                }
            }
            AssertBaselineUnchanged(classificationRoot, baseline);
            if (verify)
            {
                if (Program.HashFile(Assembly.GetExecutingAssembly().Location) != summary.ToolAssemblySha256)
                    throw new InvalidDataException("The catalog tool changed during historical verification.");
                string receiptPath = Path.Combine(historicalRoot, "historical.verify.json");
                string temporaryReceipt = receiptPath + ".new-" + Guid.NewGuid().ToString("N");
                try
                {
                    WriteJson(temporaryReceipt, new
                    {
                        FormatVersion = 1,
                        Result = "PASS",
                        Method = "Independent input reread, historical regeneration, exact output-set/hash and external report-byte comparison",
                        summary.ItemsDatSha256, summary.ItemTemplatesSha256, summary.EvidenceBundleSha256,
                        summary.ObservationsSha256, summary.ToolAssemblySha256, summary.TotalInputRecords,
                        BaselinePreservation = "PASS",
                        BaselineFiles = baseline.Files,
                        Files = verifiedFiles!.Select(pair => new
                        {
                            File = pair.Key, Sha256 = pair.Value,
                            Bytes = new FileInfo(Path.Combine(destination, pair.Key)).Length
                        }).ToArray(),
                        ReportSha256 = Program.HashFile(reportPath)
                    });
                    File.Move(temporaryReceipt, receiptPath, true);
                }
                finally
                {
                    if (File.Exists(temporaryReceipt))
                        File.Delete(temporaryReceipt);
                }
            }
            Console.WriteLine(verify ? "PASS historical reconstruction deterministic regeneration" : "PASS historical reconstruction catalog generation");
            Console.WriteLine("INPUT_RECORDS=" + records.Length);
            Console.WriteLine("CHEST_POSITIVE_DOCUMENTED=" + summary.MissionChestEligibleRk);
            Console.WriteLine("VERIFIED_SPECIAL_PURPOSE_EXCLUSIONS=" + summary.VerifiedSpecialPurposeExcluded);
            Console.WriteLine("RECONSTRUCTION_STATUS=" + summary.ReconstructionStatus);
            Console.WriteLine("OUTPUT=" + destination);
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                foreach (string file in Directory.GetFiles(stage))
                    File.Delete(file);
                Directory.Delete(stage);
            }
        }
    }

    internal static HistoricalRecord BuildRecord(CatalogItem item, HistoricalAssertion[] assertions,
        HistoricalObservation[] observations, string? currentItemsHash = null)
    {
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        HistoricalAssertion[] matchingAssertions = assertions.Where(assertion => assertion.TemplateId == item.TemplateId)
            .OrderBy(AssertionOrderKey, StringComparer.Ordinal).ToArray();
        HistoricalObservation[] matchingObservations = observations.Where(observation => observation.TemplateId == item.TemplateId).ToArray();
        if (matchingAssertions.Length != assertions.Length || matchingObservations.Length != observations.Length)
            unresolved.Add("FOREIGN_TEMPLATE_EVIDENCE_NOT_ATTACHED_OR_PROMOTED");
        HistoricalAssertion[] trusted = matchingAssertions.Where(IsTrustedAssertion)
            .OrderBy(AssertionOrderKey, StringComparer.Ordinal).ToArray();
        if (trusted.Length != matchingAssertions.Length)
            unresolved.Add("UNVERIFIED_OR_NONEXACT_ASSERTIONS_RETAINED_NOT_PROMOTED");
        string[] associations = trusted.SelectMany(assertion => assertion.ContentAssociations)
            .Select(NormalizeAssociation).Where(value => value != null).Cast<string>()
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (trusted.SelectMany(assertion => assertion.ContentAssociations).Any(value => NormalizeAssociation(value) == null))
            unresolved.Add("UNSUPPORTED_CONTENT_ASSOCIATION_NOT_PROMOTED");
        if (associations.Length == 0)
            unresolved.Add(item.VerifiedExpansions.Length > 0
                ? "ITEM_REQUIREMENTS_RETAINED_SEPARATELY_CONTENT_ORIGIN_UNVERIFIED"
                : "NO_VERIFIED_CONTENT_ORIGIN_EVIDENCE");
        if (trusted.Any(assertion => !QualityMatches(assertion, item.StoredQuality)))
            unresolved.Add("DOCUMENTED_SOURCE_QL_DOES_NOT_MATCH_CURRENT_RECORD_OR_EXPLICIT_RANGE");
        HistoricalAcquisition[] acquisition = matchingAssertions.SelectMany(assertion => assertion.AcquisitionSources
            .Select(source => source with
            {
                Confidence = IsTrustedAssertion(assertion) ? source.Confidence : "UNVERIFIED_PARENT_ASSERTION:" + source.Confidence,
                SourceUrls = source.SourceUrls.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            }))
            .DistinctBy(source => JsonSerializer.Serialize(source)).OrderBy(source => source.Kind, StringComparer.Ordinal)
            .ThenBy(source => source.Location, StringComparer.Ordinal).ThenBy(source => source.TemporalScope, StringComparer.Ordinal)
            .ThenBy(source => string.Join("|", source.SourceUrls), StringComparer.Ordinal).ToArray();
        string[] roles = trusted.SelectMany(assertion => assertion.Roles).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool specialExclusion = trusted.Any(assertion => assertion.ChestStatus == "VERIFIED_SPECIAL_PURPOSE_EXCLUSION");
        bool absenceProven = trusted.Any(assertion => assertion.RetailChestAbsenceProven);
        bool chestProof = trusted.Any(assertion => assertion.ChestStatus == "POSITIVE_DOCUMENTED"
            && assertion.ContentAssociations.Any(value => NormalizeAssociation(value) == "RubiKa")
            && assertion.AcquisitionSources.Any(source => source.Kind == "OrdinaryRkMissionChest" && IsTrustedConfidence(source.Confidence))
            && QualityMatches(assertion, item.StoredQuality));
        string chestDisposition = specialExclusion ? "VERIFIED_SPECIAL_PURPOSE_EXCLUSION"
            : absenceProven ? "VERIFIED_RETAIL_CHEST_ABSENCE"
            : chestProof ? EligibleChest
            : trusted.Any(assertion => assertion.ChestStatus == "HISTORICAL_DOCUMENTED_REPORT") ? "HISTORICAL_CHEST_REPORT_ONLY"
            : "NO_POSITIVE_CHEST_EVIDENCE";
        if (chestProof && (specialExclusion || absenceProven))
            unresolved.Add("POSITIVE_CHEST_ASSERTION_CONFLICTS_WITH_DOCUMENTED_EXCLUSION_REQUIRES_REVIEW");
        if (!chestProof && !specialExclusion && !absenceProven)
            unresolved.Add("ORDINARY_RK_CHEST_ELIGIBILITY_UNVERIFIED_FAIL_CLOSED_NOT_PROVEN_RETAIL_ABSENCE");

        HistoricalObservation? currentDifference = CompareCurrentFields(item, matchingObservations, currentItemsHash);
        HistoricalObservation[] sortedObservations = SortObservations(currentDifference == null
            ? matchingObservations : matchingObservations.Append(currentDifference));
        if (currentDifference != null)
            unresolved.Add("EXPORTED_SNAPSHOT_FIELD_DIFFERENCE_IS_NOT_AN_EXACT_RETAIL_RENAME_OR_QL_RANGE_CHANGE");
        HistoricalObservation[] presence = sortedObservations.Where(observation => IsTrustedConfidence(observation.Confidence)
            && IsPresenceKind(observation.Kind) && IsNumericVersion(EffectiveVersion(observation.DatabaseBuild, observation.PatchVersion))).ToArray();
        string? firstKnown = presence.FirstOrDefault()?.PatchVersion;
        string? firstAdded = presence.FirstOrDefault(observation => observation.Kind == "ADDED_IN_DATABASE_PATCH")?.PatchVersion;
        if (firstKnown == null)
            unresolved.Add("EARLIEST_VERIFIED_DATABASE_PRESENCE_UNRECOVERED");
        string[] introductionClaims = trusted.Select(assertion => assertion.DocumentedIntroductionPatch)
            .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (introductionClaims.Length > 1)
            unresolved.Add("CONFLICTING_DOCUMENTED_INTRODUCTION_PATCH_CLAIMS");
        unresolved.Add("COMPLETE_PATCH_AND_ACQUISITION_HISTORY_UNRECOVERED");
        return new HistoricalRecord
        {
            SourceRecordIndex = item.SourceRecordIndex, TemplateId = item.TemplateId,
            Name = item.Name, StoredQuality = item.StoredQuality,
            MinimumSupportedQuality = item.MinimumSupportedQuality, MaximumSupportedQuality = item.MaximumSupportedQuality,
            QualityRangeEvidence = item.QualityRangeEvidence, Category = item.Category,
            PrimaryClassification = associations.Length == 0 ? "Unknown" : associations.Length == 1 ? associations[0] : "MultipleExpansions",
            ContentAssociations = associations,
            ContentConfidence = associations.Length == 0 ? "UNVERIFIED"
                : trusted.All(assertion => assertion.Confidence.StartsWith("PROVEN_", StringComparison.Ordinal))
                    ? "PROVEN_EXACT_ID_SOURCE_ASSOCIATION" : "CORROBORATED_EXACT_ID_SOURCE_ASSOCIATION",
            RequiredExpansionAssociations = item.VerifiedExpansions,
            RequirementEvidence = item.ClassificationEvidence,
            FirstKnownPatch = firstKnown, FirstRecordedAdditionPatch = firstAdded,
            ActualIntroductionStatus = introductionClaims.Length == 1 ? "DOCUMENTED_GUIDE_INTRODUCTION_CLAIM_NOT_INFERRED_FROM_DATE"
                : introductionClaims.Length > 1 ? "CONFLICTING_DOCUMENTED_INTRODUCTION_CLAIMS"
                : firstKnown == null ? "UNVERIFIED" : "EARLIEST_RECOVERED_DATABASE_PRESENCE_NOT_PROVEN_FIRST_INTRODUCTION",
            DocumentedIntroductionPatch = introductionClaims.Length == 1 ? introductionClaims[0] : null,
            PatchObservations = sortedObservations, AcquisitionSources = acquisition,
            ClassificationEvidence = matchingAssertions, SpecialPurposeRoles = roles,
            ChestDisposition = chestDisposition, VerifiedSpecialPurposeExclusion = specialExclusion,
            RetailChestAbsenceProven = absenceProven,
            VerifiedRkAccessible = trusted.Any(assertion => assertion.ContentAssociations.Any(value => NormalizeAssociation(value) == "RubiKa")
                && assertion.AcquisitionSources.Any(source => IsTrustedConfidence(source.Confidence) && IsWorldAcquisition(source.Kind))),
            SourceUrls = matchingAssertions.SelectMany(assertion => assertion.SourceUrls)
                .Concat(acquisition.SelectMany(source => source.SourceUrls))
                .Concat(sortedObservations.SelectMany(observation => observation.SourceUrls))
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Unresolved = unresolved.ToArray()
        };
    }

    internal static void ValidateCoverage(IReadOnlyList<DatItemTemplate> input, HistoricalRecord[] records)
    {
        if (input.Count != records.Length || records.Select(item => item.SourceRecordIndex).Distinct().Count() != input.Count)
            throw new InvalidDataException("Historical primary coverage is not one-to-one with input records.");
        foreach (HistoricalRecord record in records)
        {
            if (record.SourceRecordIndex < 0 || record.SourceRecordIndex >= input.Count)
                throw new InvalidDataException("Historical record references an invalid source index.");
            DatItemTemplate item = input[record.SourceRecordIndex];
            if (record.TemplateId != item.ID || record.StoredQuality != item.Quality || record.Name != item.Name)
                throw new InvalidDataException("Historical reconstruction altered an original item identity, name or stored QL.");
            if (record.ClassificationEvidence.Any(assertion => assertion.TemplateId != record.TemplateId)
                || record.PatchObservations.Any(observation => observation.TemplateId != record.TemplateId))
                throw new InvalidDataException("Historical record contains evidence for a different item template identity.");
            string expected = record.ContentAssociations.Length == 0 ? "Unknown"
                : record.ContentAssociations.Length == 1 ? record.ContentAssociations[0] : "MultipleExpansions";
            if (!Buckets.Contains(record.PrimaryClassification, StringComparer.Ordinal) || record.PrimaryClassification != expected
                || record.ContentAssociations.Length != record.ContentAssociations.Distinct(StringComparer.Ordinal).Count())
                throw new InvalidDataException("Historical primary classification is invalid or duplicated.");
            if (record.PrimaryClassification != "Unknown" && !record.ContentAssociations.All(association =>
                    record.ClassificationEvidence.Any(assertion => IsTrustedAssertion(assertion)
                        && assertion.ContentAssociations.Any(value => NormalizeAssociation(value) == association))))
                throw new InvalidDataException("Historical content classification lacks exact-identity source evidence.");
            if (record.VerifiedSpecialPurposeExclusion && !record.ClassificationEvidence.Any(assertion =>
                    IsTrustedAssertion(assertion) && assertion.ChestStatus == "VERIFIED_SPECIAL_PURPOSE_EXCLUSION"))
                throw new InvalidDataException("A special-purpose chest exclusion lacks trusted exact-identity evidence.");
            if (record.RetailChestAbsenceProven && !record.ClassificationEvidence.Any(assertion =>
                    IsTrustedAssertion(assertion) && assertion.RetailChestAbsenceProven))
                throw new InvalidDataException("A retail chest-absence claim lacks trusted exact-identity evidence.");
            if (record.ChestDisposition == EligibleChest && (record.VerifiedSpecialPurposeExclusion || record.RetailChestAbsenceProven
                    || !record.ClassificationEvidence.Any(assertion => IsTrustedAssertion(assertion) && assertion.ChestStatus == "POSITIVE_DOCUMENTED"
                        && assertion.ContentAssociations.Any(value => NormalizeAssociation(value) == "RubiKa")
                        && QualityMatches(assertion, record.StoredQuality)
                        && assertion.AcquisitionSources.Any(source => source.Kind == "OrdinaryRkMissionChest" && IsTrustedConfidence(source.Confidence)))))
                throw new InvalidDataException("A chest allowlist record lacks positive, current, QL-matched RK chest evidence.");
        }
    }

    static bool IsTrustedAssertion(HistoricalAssertion assertion)
        => assertion.IdentityMatch.StartsWith("EXACT_", StringComparison.Ordinal)
            && IsTrustedConfidence(assertion.Confidence)
            && (assertion.SourceIds.Length > 0 || assertion.SourceUrls.Any(value => !string.IsNullOrWhiteSpace(value)));

    static bool IsTrustedConfidence(string confidence)
        => confidence.StartsWith("PROVEN_", StringComparison.Ordinal) || confidence.StartsWith("CORROBORATED_", StringComparison.Ordinal);

    static string? NormalizeAssociation(string value)
        => value is "OtherExpansionOrContent" or "PMVObsidianEdition" ? "OtherContent"
            : value is "RubiKa" or "NotumWars" or "Shadowlands" or "AlienInvasion" or "LostEden" or "Xan" or "OtherContent" ? value : null;

    static bool QualityMatches(HistoricalAssertion assertion, int quality)
    {
        bool hasMinimum = assertion.DocumentedMinimumQL.HasValue;
        bool hasMaximum = assertion.DocumentedMaximumQL.HasValue;
        if (hasMinimum != hasMaximum || (hasMinimum && assertion.DocumentedMinimumQL > assertion.DocumentedMaximumQL))
            return false;
        bool inRange = hasMinimum && quality >= assertion.DocumentedMinimumQL && quality <= assertion.DocumentedMaximumQL;
        if (assertion.DocumentedQuality.HasValue)
            return quality == assertion.DocumentedQuality || inRange;
        return !hasMinimum || inRange;
    }

    static bool IsPresenceKind(string kind)
        => kind is "ADDED_IN_DATABASE_PATCH" or "MODIFIED_IN_DATABASE_PATCH" or "PRESENT_IN_DATABASE_SNAPSHOT"
            or "NEWLY_PRESENT_BETWEEN_SNAPSHOTS" or "FIELD_CHANGED_BETWEEN_SNAPSHOTS" or "COMMUNITY_DATABASE_PRESENCE";

    static bool IsWorldAcquisition(string kind)
        => kind.StartsWith("Rk", StringComparison.Ordinal) || kind is "OrdinaryRkMissionChest" or "OrdinaryRkMissionReward"
            or "SpecialQuestReward" or "SeasonalEncounterCorpse";

    static HistoricalObservation? CompareCurrentFields(CatalogItem item,
        HistoricalObservation[] observations, string? currentItemsHash)
    {
        if (string.IsNullOrWhiteSpace(currentItemsHash))
            return null;
        var snapshot = observations.Where(observation => observation.Kind == "PRESENT_IN_DATABASE_SNAPSHOT"
                && IsTrustedConfidence(observation.Confidence) && observation.Detail?.ValueKind == JsonValueKind.Object
                && (observation.SourceUrls.Any(IsAoiaSource)
                    || (TryDetailProperty(observation.Detail.Value, "sourceUrl", out JsonElement sourceUrl)
                        && sourceUrl.ValueKind == JsonValueKind.String && IsAoiaSource(sourceUrl.GetString()!))))
            .Select(observation => new { Observation = observation, Bound = LatestSnapshotBound(observation) })
            .Where(value => value.Bound != null)
            .OrderByDescending(value => value.Bound!, PatchVersionComparer.Instance)
            .ThenBy(value => JsonSerializer.Serialize(value.Observation), StringComparer.Ordinal).FirstOrDefault();
        if (snapshot == null)
            return null;
        JsonElement detail = snapshot.Observation.Detail!.Value;
        bool hasName = TryDetailProperty(detail, "name", out JsonElement name)
            && name.ValueKind is JsonValueKind.String or JsonValueKind.Null;
        string? beforeName = hasName && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
        bool hasQuality = TryDetailProperty(detail, "quality", out JsonElement quality)
            && quality.ValueKind == JsonValueKind.Number && quality.TryGetInt32(out _);
        int? beforeQuality = hasQuality ? quality.GetInt32() : null;
        bool nameDiffers = hasName && !string.Equals(beforeName, item.Name, StringComparison.Ordinal);
        bool qualityDiffers = hasQuality && beforeQuality != item.StoredQuality;
        if (!nameDiffers && !qualityDiffers)
            return null;
        string[] fields = new[] { nameDiffers ? "Name" : null, qualityDiffers ? "StoredQuality" : null }
            .Where(value => value != null).Cast<string>().ToArray();
        return new HistoricalObservation
        {
            TemplateId = item.TemplateId,
            PatchVersion = "CURRENT_CANONICAL_SNAPSHOT",
            Kind = CurrentFieldsDifference,
            SourceUrls = snapshot.Observation.SourceUrls,
            SourceHashes = snapshot.Observation.SourceHashes.Append(currentItemsHash)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Confidence = "PROVEN_EXPORT_FIELD_DIFFERENCE_NOT_PROVEN_GAME_CHANGE",
            Detail = JsonSerializer.SerializeToElement(new
            {
                before = new
                {
                    TemplateId = snapshot.Observation.TemplateId, Name = beforeName, StoredQuality = beforeQuality,
                    NameFieldPresent = hasName, QualityFieldPresent = hasQuality,
                    SourcePatchLabel = snapshot.Observation.PatchVersion, snapshot.Observation.DatabaseBuild,
                    SourceUrls = snapshot.Observation.SourceUrls, SourceHashes = snapshot.Observation.SourceHashes
                },
                after = new
                {
                    item.TemplateId, item.Name, item.StoredQuality,
                    Source = "items.dat", Sha256 = currentItemsHash
                },
                fieldsChanged = fields,
                intervalStart = snapshot.Observation.PatchVersion,
                latestIdentityPresenceBound = snapshot.Bound,
                intervalStartQualification = "Before values come from their original snapshot source; later identity presence does not establish unchanged field values.",
                intervalEnd = "CURRENT_CANONICAL_SNAPSHOT",
                scope = "DifferentExporterMayAffectDisplay_NOT_PROVEN_EXACT_GAME_RENAME_OR_QL_RANGE_CHANGE_OR_PATCH",
                introductionOrAcquisitionInferred = false
            })
        };
    }

    static string? LatestSnapshotBound(HistoricalObservation observation)
    {
        var versions = new List<string>();
        string effective = EffectiveVersion(observation.DatabaseBuild, observation.PatchVersion);
        if (IsNumericVersion(effective))
            versions.Add(effective);
        if (observation.Detail is { ValueKind: JsonValueKind.Object } detail
            && TryDetailProperty(detail, "observedSnapshots", out JsonElement snapshots)
            && snapshots.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in snapshots.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String && IsNumericVersion(value.GetString()!))
                    versions.Add(value.GetString()!);
            }
        }
        return versions.OrderByDescending(version => version, PatchVersionComparer.Instance).FirstOrDefault();
    }

    static bool IsAoiaSource(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Host.Equals("sourceforge.net", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".sourceforge.net", StringComparison.OrdinalIgnoreCase))
            && uri.AbsolutePath.Contains("/aoia/", StringComparison.OrdinalIgnoreCase);

    static bool TryDetailProperty(JsonElement detail, string name, out JsonElement value)
    {
        foreach (JsonProperty property in detail.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    static string AssertionOrderKey(HistoricalAssertion assertion) => JsonSerializer.Serialize(assertion);

    static HistoricalObservation[] SortObservations(IEnumerable<HistoricalObservation> observations)
        => observations.Select(observation => observation with
            {
                SourceUrls = observation.SourceUrls.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                SourceHashes = observation.SourceHashes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            }).DistinctBy(observation => JsonSerializer.Serialize(observation))
            .OrderBy(observation => EffectiveVersion(observation.DatabaseBuild, observation.PatchVersion), PatchVersionComparer.Instance)
            .ThenBy(observation => observation.PatchVersion, StringComparer.Ordinal)
            .ThenBy(observation => observation.Kind, StringComparer.Ordinal)
            .ThenBy(observation => observation.DatabaseBuild, StringComparer.Ordinal)
            .ThenBy(observation => JsonSerializer.Serialize(observation), StringComparer.Ordinal).ToArray();

    static bool IsNumericVersion(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Split('.').All(part => int.TryParse(part, NumberStyles.None,
            CultureInfo.InvariantCulture, out int number) && number >= 0);

    static string EffectiveVersion(string? databaseBuild, string patchVersion)
        // Opaque build12.52 belongs to patch12.5, before patch12.8/build12.8.
        => patchVersion;

    static BaselineState ReadBaseline(string root, string itemsHash, string? familiesHash)
    {
        BaselineFile[] files = SnapshotBaselineFiles(root);
        string summaryPath = Path.Combine(root, "ClassificationSummary.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(summaryPath));
        JsonProperty countProperty = document.RootElement.EnumerateObject()
            .Single(property => property.Name.Equals("ClassificationCounts", StringComparison.OrdinalIgnoreCase));
        Dictionary<string, int> counts = countProperty.Value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);
        return new BaselineState(itemsHash, familiesHash, files, counts);
    }

    static BaselineFile[] SnapshotBaselineFiles(string root)
        => Directory.GetFiles(root).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(path => new BaselineFile(Path.GetFileName(path), Program.HashFile(path), new FileInfo(path).Length)).ToArray();

    static void AssertBaselineUnchanged(string root, BaselineState baseline)
    {
        if (!SnapshotBaselineFiles(root).SequenceEqual(baseline.Files))
            throw new InvalidDataException("Previous primary classification files changed; reconstruction is not accepted.");
    }

    static void ValidateSources(HistoricalEvidenceBundle bundle, string researchRoot, Dictionary<string, string> inputs)
    {
        if (bundle.Sources.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count() != bundle.Sources.Length)
            throw new InvalidDataException("Historical source IDs are duplicated.");
        var sourceIds = bundle.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        foreach (HistoricalAssertion assertion in bundle.Assertions)
        {
            if (assertion.TemplateId <= 0 || assertion.SourceIds.Any(id => !sourceIds.Contains(id)))
                throw new InvalidDataException("A historical assertion has an invalid item/source identity.");
        }
        foreach (HistoricalSource source in bundle.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.CachePath) || string.IsNullOrWhiteSpace(source.Sha256))
                continue;
            string path = Path.GetFullPath(source.CachePath, researchRoot);
            string actual = Program.HashFile(path);
            if (!actual.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Historical cached source hash differs: " + source.Id);
            inputs[path] = actual;
        }
    }

    static void AssertInputsUnchanged(Dictionary<string, string> inputs, string familiesPath, string? familiesHash)
    {
        foreach ((string path, string hash) in inputs)
        {
            if (Program.HashFile(path) != hash)
                throw new InvalidDataException("A selected item or historical source changed during reconstruction.");
        }
        if ((File.Exists(familiesPath) ? Program.HashFile(familiesPath) : null) != familiesHash)
            throw new InvalidDataException("Selected item family metadata changed during reconstruction.");
    }

    static void WriteJson<T>(string path, T value)
    {
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Program.JsonOptions);
        stream.WriteByte((byte)'\n');
    }

    static OutputIdentity Identity(string root, string name, int count)
        => new(name, Program.HashFile(Path.Combine(root, name)), new FileInfo(Path.Combine(root, name)).Length, count);

    static Dictionary<string, string> CompareDirectories(string actual, string expected)
    {
        if (!Directory.Exists(expected))
            throw new DirectoryNotFoundException("No historical catalog exists for deterministic verification.");
        string[] actualFiles = Directory.GetFiles(actual).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
        string[] previousFiles = Directory.GetFiles(expected).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
        if (!actualFiles.SequenceEqual(previousFiles, StringComparer.Ordinal) || Directory.GetDirectories(expected).Length != 0)
            throw new InvalidDataException("Historical catalog output set has missing or unexpected files/directories.");
        var verified = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in actualFiles)
        {
            string expectedHash = Program.HashFile(Path.Combine(expected, name));
            if (Program.HashFile(Path.Combine(actual, name)) != expectedHash)
                throw new InvalidDataException("Historical regeneration differs: " + name);
            verified.Add(name, expectedHash);
        }
        return verified;
    }

    static void Publish(string stage, string destination, string expectedParent)
    {
        string parent = Path.GetFullPath(expectedParent);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(stage)), parent, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(destination)), parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Historical catalog publish paths escaped the intended directory.");
        if (!Directory.Exists(destination))
        {
            Directory.Move(stage, destination);
            return;
        }
        string previous = destination + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.Move(destination, previous);
        try
        {
            Directory.Move(stage, destination);
        }
        catch
        {
            Directory.Move(previous, destination);
            throw;
        }
    }

    sealed class PatchVersionComparer : IComparer<string>
    {
        internal static readonly PatchVersionComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            left ??= "";
            right ??= "";
            if (!IsNumericVersion(left) || !IsNumericVersion(right))
                return StringComparer.Ordinal.Compare(left, right);
            int[] first = left.Split('.').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            int[] second = right.Split('.').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            for (int index = 0; index < Math.Max(first.Length, second.Length); index++)
            {
                int comparison = (index < first.Length ? first[index] : 0).CompareTo(index < second.Length ? second[index] : 0);
                if (comparison != 0)
                    return comparison;
            }
            return StringComparer.Ordinal.Compare(left, right);
        }
    }
}
