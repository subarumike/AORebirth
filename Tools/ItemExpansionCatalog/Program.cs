using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class Program
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static readonly string[] Buckets =
    ["RubiKa", "Shadowlands", "AlienInvasion", "LostEden", "Xan", "NotumWars", "PMVObsidianEdition", "MultipleExpansions", "Unknown"];

    static int Main(string[] args)
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            if (args.Length == 1 && args[0] == "--self-test")
                return CatalogChecks.Run();
            if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
            {
                Console.WriteLine("ItemExpansionCatalog --gamedata-root <private GameData> [--verify]");
                Console.WriteLine("--verify independently regenerates and compares without changing the catalog.");
                Console.WriteLine("--self-test runs focused in-memory catalog checks; no database or engine access.");
                return args.Length == 0 ? 1 : 0;
            }
            string? root = null;
            bool verify = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--gamedata-root" && ++i < args.Length)
                    root = Path.GetFullPath(args[i]);
                else if (args[i] == "--verify")
                    verify = true;
                else
                    throw new ArgumentException("Unknown or incomplete argument: " + args[i]);
            }
            if (root == null)
                throw new ArgumentException("An explicit private --gamedata-root is required.");
            Run(root, verify);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    static void Run(string root, bool verify)
    {
        string itemsPath = Path.Combine(root, "items.dat");
        string familiesPath = Path.Combine(root, "ItemTemplates.json");
        string itemsHash = HashFile(itemsPath);
        string? familyHash = File.Exists(familiesPath) ? HashFile(familiesPath) : null;
        List<DatItemTemplate> input = ItemsDatReader.Read(itemsPath);
        if (input.Count == 0)
            throw new InvalidDataException("Selected items.dat contains no definitions.");
        FamilyMetadata families = FamilyMetadata.Build(input,
            familyHash == null ? null : File.ReadAllText(familiesPath));
        CatalogItem[] records = input.Select((item, index) => BuildRecord(item, index, families))
            .OrderBy(item => item.TemplateId).ThenBy(item => item.StoredQuality)
            .ThenBy(item => item.SourceRecordIndex).ToArray();
        ValidateCoverage(input, records);

        string destination = Path.Combine(root, "Loot", "ExpansionClassification");
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".ExpansionClassification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        bool completed = false;
        try
        {
            var generated = new List<OutputIdentity>();
            foreach (string bucket in Buckets)
            {
                string name = bucket + ".json";
                CatalogItem[] selected = records.Where(item => item.ExpansionClassification == bucket).ToArray();
                WriteJson(Path.Combine(stage, name), selected);
                generated.Add(Identity(stage, name, selected.Length));
            }

            var summary = new CatalogSummary(
                1, "ITEM_EXPANSION_REQUIREMENTS_V1", itemsHash, familyHash,
                HashFile(Assembly.GetExecutingAssembly().Location), records.Length,
                records.Select(item => item.TemplateId).Distinct().Count(),
                records.GroupBy(item => (item.TemplateId, item.StoredQuality)).Count(group => group.Count() > 1),
                Buckets.ToDictionary(bucket => bucket,
                    bucket => records.Count(item => item.ExpansionClassification == bucket), StringComparer.Ordinal),
                records.Where(item => item.ExpansionClassification == "Unknown").GroupBy(item => item.ClassificationReason)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                records.Count(item => !item.Evidence.HasExpansionInformation),
                records.Count(item => item.Evidence.HasUnsupportedValue),
                records.Count(item => item.Evidence.HasConflictingEvidence),
                records.Count(item => item.Evidence.HasIncompleteRdb),
                input.Count(item => item.Rdb != null), input.Count(item => string.IsNullOrEmpty(item.Name)),
                families.FamilyCount, families.RangeFamilyCount, families.LinkedTemplateCount,
                families.MissingTemplateIds, families.Warnings, generated.ToArray(),
                "PASS", "PASS", "UNVERIFIED", "NOT_READY");
            WriteJson(Path.Combine(stage, "ClassificationSummary.json"), summary);
            File.WriteAllText(Path.Combine(stage, "ClassificationReport.md"),
                CatalogReport.Create(summary, records), new UTF8Encoding(false));

            // Detect a selected-source change before any catalog is replaced.
            if (HashFile(itemsPath) != itemsHash ||
                (File.Exists(familiesPath) ? HashFile(familiesPath) : null) != familyHash)
                throw new InvalidDataException("Selected item inputs changed during generation; catalog not published.");

            if (verify)
            {
                CompareDirectories(stage, destination);
                // A separate receipt records the actual second parse/comparison without changing catalog bytes.
                var verifiedFiles = Directory.GetFiles(destination).Select(path => new
                {
                    File = Path.GetFileName(path), Sha256 = HashFile(path), Bytes = new FileInfo(path).Length
                }).OrderBy(file => file.File, StringComparer.Ordinal).ToArray();
                WriteJson(destination + ".verify.json", new
                {
                    Result = "PASS", Method = "Independent input reread, regeneration and exact complete output comparison",
                    summary.ItemsDatSha256, summary.ItemTemplatesSha256, summary.ToolAssemblySha256,
                    summary.TotalInputRecords, Files = verifiedFiles
                });
            }
            else
                Publish(stage, destination);
            completed = true;
            Console.WriteLine(verify ? "PASS deterministic regeneration and complete coverage" : "PASS catalog generated and complete coverage");
            Console.WriteLine("INPUT_RECORDS=" + records.Length);
            foreach ((string bucket, int count) in summary.ClassificationCounts)
                Console.WriteLine(bucket + "=" + count);
            Console.WriteLine("INPUT_SHA256=" + itemsHash);
            Console.WriteLine("RK_ONLY_FILTERING=NOT_READY");
            Console.WriteLine("OUTPUT=" + destination);
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                // Only files created in this invocation's exact staging directory are removed.
                foreach (string file in Directory.GetFiles(stage))
                    File.Delete(file);
                Directory.Delete(stage);
            }
            if (!completed)
                Console.Error.WriteLine("Existing primary classification files were not accepted as a new generation.");
        }
    }

    internal static CatalogItem BuildRecord(DatItemTemplate item, int index, FamilyMetadata families)
    {
        ItemEvidence evidence = ExpansionEvidenceInspector.Inspect(item);
        ExpansionClassification classification = RequirementExpansionClassifier.Classify(item, evidence);
        FamilyQualityEvidence[] links = families.ForTemplate(item.ID);
        FamilyQualityEvidence[] ranges = links.Where(link => link.MinimumQuality.HasValue && link.MaximumQuality.HasValue).ToArray();
        int? fileClass = item.Stats.TryGetValue((int)CharacterStat.ItemClass, out int value) ? value : null;
        int? liveClass = item.Rdb?.LiveStats.TryGetValue((int)CharacterStat.ItemClass, out int liveValue) == true ? liveValue : null;
        return new CatalogItem(index, item.ID, item.Name, item.Quality,
            ranges.Length == 0 ? null : ranges.Min(link => link.MinimumQuality),
            ranges.Length == 0 ? null : ranges.Max(link => link.MaximumQuality),
            links.Select(link => new CatalogQualityEvidence(link.FamilyHash, link.MinimumQuality,
                link.MaximumQuality, link.Status, link.SourceField)).ToArray(),
            classification.PrimaryClassification, classification.Associations,
            classification.OriginalValue, classification.SourceField, classification.Confidence,
            classification.Reason, classification.Evidence,
            new CategoryMetadata(item.ItemType, item.DynelType, item.Flags, fileClass,
                fileClass.HasValue && Enum.IsDefined(typeof(ItemClass), fileClass.Value)
                    ? ((ItemClass)fileClass.Value).ToString() : null,
                liveClass, liveClass.HasValue && Enum.IsDefined(typeof(ItemClass), liveClass.Value)
                    ? ((ItemClass)liveClass.Value).ToString() : null), evidence);
    }

    internal static void ValidateCoverage(IReadOnlyList<DatItemTemplate> input, CatalogItem[] records)
    {
        if (input.Count != records.Length || records.Select(item => item.SourceRecordIndex).Distinct().Count() != input.Count)
            throw new InvalidDataException("Input/output record coverage is not one-to-one.");
        foreach (CatalogItem record in records)
        {
            DatItemTemplate original = input[record.SourceRecordIndex];
            if (record.TemplateId != original.ID || record.StoredQuality != original.Quality || record.Name != original.Name)
                throw new InvalidDataException("Catalog identity differs from original input record.");
            if (!Buckets.Contains(record.ExpansionClassification, StringComparer.Ordinal))
                throw new InvalidDataException("Unsupported primary classification.");
            if (record.ExpansionClassification == "Unknown")
            {
                if (record.VerifiedExpansions.Length != 0 || record.ClassificationEvidence.Length != 0)
                    throw new InvalidDataException("Unknown evidence cannot be promoted to verified associations.");
            }
            else if (record.VerifiedExpansions.Length == 0 || record.ClassificationEvidence.Length == 0
                || string.IsNullOrWhiteSpace(record.SourceField)
                || !record.ClassificationConfidence.StartsWith("PROVEN", StringComparison.Ordinal)
                || (record.ExpansionClassification == "MultipleExpansions"
                    ? record.VerifiedExpansions.Distinct(StringComparer.Ordinal).Count() < 2
                    : record.VerifiedExpansions.Length != 1 || record.VerifiedExpansions[0] != record.ExpansionClassification))
                throw new InvalidDataException("Verified expansion assignment lacks traceable item requirement evidence.");
        }
    }

    internal static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    static OutputIdentity Identity(string root, string name, int count)
        => new(name, HashFile(Path.Combine(root, name)), new FileInfo(Path.Combine(root, name)).Length, count);

    static void WriteJson<T>(string path, T value)
    {
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, JsonOptions);
        stream.WriteByte((byte)'\n');
    }

    static void CompareDirectories(string actual, string expected)
    {
        if (!Directory.Exists(expected))
            throw new DirectoryNotFoundException("No existing catalog to verify: " + expected);
        string[] files = Directory.GetFiles(actual).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
        string[] previousFiles = Directory.GetFiles(expected).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
        if (!files.SequenceEqual(previousFiles, StringComparer.Ordinal) || Directory.GetDirectories(expected).Length != 0)
            throw new InvalidDataException("Existing catalog has missing or unexpected files/directories.");
        foreach (string name in files)
        {
            string previous = Path.Combine(expected, name);
            if (!File.Exists(previous) || HashFile(previous) != HashFile(Path.Combine(actual, name)))
                throw new InvalidDataException("Regeneration differs: " + name);
        }
    }

    static void Publish(string stage, string destination)
    {
        if (!Directory.Exists(destination))
        {
            Directory.Move(stage, destination);
            return;
        }
        // Retain the previous whole catalog; never recursively delete user files or old evidence.
        string backup = destination + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.Move(destination, backup);
        try
        {
            Directory.Move(stage, destination);
        }
        catch
        {
            Directory.Move(backup, destination);
            throw;
        }
    }
}

internal sealed record CatalogItem(int SourceRecordIndex, int TemplateId, string? Name, int StoredQuality,
    int? MinimumSupportedQuality, int? MaximumSupportedQuality, CatalogQualityEvidence[] QualityRangeEvidence,
    string ExpansionClassification, string[] VerifiedExpansions, int? OriginalClassificationValue,
    string? SourceField, string ClassificationConfidence, string ClassificationReason,
    ClassificationEvidence[] ClassificationEvidence, CategoryMetadata Category, ItemEvidence Evidence);

internal sealed record CatalogQualityEvidence(string FamilyHash, int? MinimumQuality, int? MaximumQuality,
    string Status, string SourceField);

internal sealed record CategoryMetadata(int RawItemType, int DynelType, int ItemFlags,
    int? FileItemClass, string? FileItemClassName, int? LiveItemClass, string? LiveItemClassName);

internal sealed record OutputIdentity(string File, string Sha256, long Bytes, int ItemCount);

internal sealed record CatalogSummary(int FormatVersion, string RuleSet, string ItemsDatSha256,
    string? ItemTemplatesSha256, string ToolAssemblySha256, int TotalInputRecords, int DistinctTemplateIds,
    int RepeatedTemplateQualityKeys, Dictionary<string, int> ClassificationCounts,
    Dictionary<string, int> PrimaryUnknownReasons, int MissingExpansionInformation,
    int UnsupportedExpansionValues, int ConflictingExpansionEvidence, int IncompleteRdbMetadata,
    int RecordsWithRdb, int RecordsWithMissingName, int FamilyCount, int RangeFamilyCount,
    int FamilyLinkedTemplateCount, int[] MissingFamilyTemplateIds, string[] FamilyWarnings,
    OutputIdentity[] PrimaryFiles, string CoverageValidation, string IdentityValidation,
    string GardenSanctuaryKeyIdentification, string RkOnlyMissionChestIntegration);
