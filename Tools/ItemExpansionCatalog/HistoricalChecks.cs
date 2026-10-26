using System.Text.Json;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class HistoricalChecks
{
    internal static int Run()
    {
        int count = 0;
        var template = new DatItemTemplate { ID = 1001, Name = "Synthetic fixture", Quality = 40 };
        FamilyMetadata families = FamilyMetadata.Build([template], null);
        CatalogItem item = Program.BuildRecord(template, 0, families);

        HistoricalRecord noEvidence = HistoricalCatalog.BuildRecord(item, [], []);
        Require(noEvidence.PrimaryClassification == "Unknown" && !noEvidence.RetailChestAbsenceProven,
            "Missing historical acquisition is neither RK origin nor proven retail absence.");
        count++;

        var requiredTemplate = new DatItemTemplate { ID = 1002, Name = "Synthetic entitlement fixture", Quality = 1 };
        requiredTemplate.Actions.Add(new DatAction { ActionType = ActionType.ToWear, Requirements =
            [new DatRequirement { Statnumber = (int)CharacterStat.Expansion, Value = (int)Expansions.LostEden,
                Operator = Operator.BitAnd, Target = ItemTarget.User }] });
        CatalogItem requiredItem = Program.BuildRecord(requiredTemplate, 0, FamilyMetadata.Build([requiredTemplate], null));
        HistoricalRecord required = HistoricalCatalog.BuildRecord(requiredItem, [], []);
        Require(required.PrimaryClassification == "Unknown" && required.RequiredExpansionAssociations.SequenceEqual(["LostEden"]),
            "Historical content origin and current entitlement requirements are separate axes.");
        count++;

        HistoricalObservation lateAddition = Observation(1001, "18.0", "ADDED_IN_DATABASE_PATCH");
        HistoricalRecord dated = HistoricalCatalog.BuildRecord(item, [], [lateAddition]);
        Require(dated.PrimaryClassification == "Unknown" && dated.FirstKnownPatch == "18.0",
            "An expansion release-era patch cannot assign content origin by date/version.");
        count++;

        HistoricalAssertion rk = Assertion(1001, "RubiKa");
        HistoricalRecord documentedIntroduction = HistoricalCatalog.BuildRecord(item,
            [rk with { DocumentedIntroductionPatch = "17.5", DocumentedIntroductionScope = "Explicit synthetic guide statement" }], [lateAddition]);
        Require(documentedIntroduction.DocumentedIntroductionPatch == "17.5"
            && documentedIntroduction.FirstKnownPatch == "18.0", "A documented introduction claim remains separate from recovered database appearances.");
        HistoricalRecord conflictingIntroductions = HistoricalCatalog.BuildRecord(item,
            [rk with { DocumentedIntroductionPatch = "17.5" }, rk with { DocumentedIntroductionPatch = "18.0" }], []);
        Require(conflictingIntroductions.DocumentedIntroductionPatch == null
            && conflictingIntroductions.Unresolved.Contains("CONFLICTING_DOCUMENTED_INTRODUCTION_PATCH_CLAIMS"),
            "Conflicting introduction claims remain unresolved rather than being arbitrarily selected.");
        count++;
        HistoricalRecord rkOnly = HistoricalCatalog.BuildRecord(item, [rk], []);
        Require(rkOnly.PrimaryClassification == "RubiKa" && !rkOnly.VerifiedRkAccessible
            && rkOnly.ChestDisposition != "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST",
            "Base-client membership alone proves neither RK access nor ordinary chest acquisition.");
        count++;

        HistoricalAssertion chest = rk with { ChestStatus = "POSITIVE_DOCUMENTED", DocumentedQuality = 40,
            AcquisitionSources = [new HistoricalAcquisition { Kind = "OrdinaryRkMissionChest",
                Location = "Synthetic RK mission", Confidence = "PROVEN_DOCUMENTED_ASSOCIATION",
                SourceUrls = ["https://example.invalid/chest-source"] }] };
        HistoricalRecord eligible = HistoricalCatalog.BuildRecord(item, [chest], []);
        Require(eligible.ChestDisposition == "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST" && eligible.VerifiedRkAccessible,
            "Exact positive RK chest evidence is required for the allowlist.");
        count++;

        HistoricalRecord wrongQl = HistoricalCatalog.BuildRecord(item, [chest with { DocumentedQuality = 41 }], []);
        Require(wrongQl.ChestDisposition != "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST",
            "A point-quality source cannot silently establish another QL.");
        count++;

        HistoricalRecord range = HistoricalCatalog.BuildRecord(item,
            [chest with { DocumentedQuality = null, DocumentedMinimumQL = 1, DocumentedMaximumQL = 200 }], []);
        Require(range.ChestDisposition == "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST",
            "Explicit source QL ranges can support a matching quality.");
        Require(HistoricalCatalog.BuildRecord(item,
            [chest with { DocumentedQuality = null, DocumentedMinimumQL = 1, DocumentedMaximumQL = null }], [])
                .ChestDisposition != "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST", "A half-specified range is not invented.");
        count++;

        HistoricalRecord historicalReport = HistoricalCatalog.BuildRecord(item,
            [chest with { ChestStatus = "HISTORICAL_DOCUMENTED_REPORT" }], []);
        Require(historicalReport.ChestDisposition != "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST"
            && historicalReport.AcquisitionSources.Length == 1, "A historical report is retained without becoming current allowlist proof.");
        count++;

        HistoricalAssertion key = Assertion(1001, "Shadowlands") with { Roles = ["GardenKey"],
            ChestStatus = "VERIFIED_SPECIAL_PURPOSE_EXCLUSION" };
        HistoricalRecord excluded = HistoricalCatalog.BuildRecord(item, [key, chest], []);
        Require(excluded.VerifiedSpecialPurposeExclusion
            && excluded.ChestDisposition != "ELIGIBLE_POSITIVE_DOCUMENTED_RK_CHEST"
            && excluded.Unresolved.Any(reason => reason.Contains("CONFLICT", StringComparison.Ordinal)),
            "Contradictory chest and documented special-role evidence fails closed.");
        count++;

        var gardenName = template;
        gardenName.Name = "The Key to a Garden";
        HistoricalRecord onlyName = HistoricalCatalog.BuildRecord(Program.BuildRecord(gardenName, 0, families), [], []);
        Require(!onlyName.VerifiedSpecialPurposeExclusion, "An item name does not establish a special-purpose role.");
        gardenName.Name = "Synthetic fixture";
        count++;

        HistoricalRecord multiple = HistoricalCatalog.BuildRecord(item, [rk, Assertion(1001, "Shadowlands")], []);
        Require(multiple.PrimaryClassification == "MultipleExpansions"
            && multiple.ContentAssociations.SequenceEqual(["RubiKa", "Shadowlands"]), "Multiple proven content associations remain explicit.");
        count++;

        HistoricalRecord other = HistoricalCatalog.BuildRecord(item, [Assertion(1001, "OtherExpansionOrContent")], []);
        Require(other.PrimaryClassification == "OtherContent", "Additional content uses its own primary file.");
        count++;

        HistoricalAssertion uncertain = chest with { Confidence = "UNVERIFIED", IdentityMatch = "UNVERIFIED" };
        HistoricalRecord unverified = HistoricalCatalog.BuildRecord(item, [uncertain], []);
        Require(unverified.PrimaryClassification == "Unknown" && unverified.AcquisitionSources.Length == 1
            && unverified.AcquisitionSources[0].Confidence.StartsWith("UNVERIFIED", StringComparison.Ordinal),
            "Unverified acquisition claims remain queryable without being promoted.");
        count++;

        HistoricalRecord differentIdentity = HistoricalCatalog.BuildRecord(item,
            [chest with { TemplateId = 9999 }], [Observation(9999, "11.0", "ADDED_IN_DATABASE_PATCH")]);
        Require(differentIdentity.PrimaryClassification == "Unknown" && differentIdentity.FirstKnownPatch == null,
            "Historical facts cannot cross exact item identities.");
        count++;

        HistoricalRecord snapshot = HistoricalCatalog.BuildRecord(item, [],
            [Observation(1001, "17.9.1", "ADDED_IN_DATABASE_PATCH"), Observation(1001, "17.7.2", "PRESENT_IN_DATABASE_SNAPSHOT")]);
        Require(snapshot.FirstKnownPatch == "17.7.2" && snapshot.FirstRecordedAdditionPatch == "17.9.1"
            && snapshot.ActualIntroductionStatus != "PROVEN_FIRST_INTRODUCTION", "Snapshot upper bounds do not invent first-introduction dates.");
        count++;

        HistoricalRecord onlyDeletion = HistoricalCatalog.BuildRecord(item, [], [Observation(1001, "18.7.0", "DELETED_FROM_DATABASE_PATCH")]);
        Require(onlyDeletion.FirstKnownPatch == null, "Deletion at a patch is not presence in that patch.");
        count++;

        HistoricalRecord oldLabels = HistoricalCatalog.BuildRecord(item, [],
            [Observation(1001, "12.8", "MODIFIED_IN_DATABASE_PATCH") with { DatabaseBuild = "12.8" },
             Observation(1001, "12.5", "ADDED_IN_DATABASE_PATCH") with { DatabaseBuild = "12.52" }]);
        Require(oldLabels.FirstKnownPatch == "12.5", "Opaque build12.52 must not move patch12.5 after patch12.8.");
        count++;

        using JsonDocument oldFields = JsonDocument.Parse("""
            {"name":"Old exported label","quality":41,"observedSnapshots":["17.7.2","17.9.1"]}
            """);
        HistoricalObservation oldExport = Observation(1001, "17.7.2", "PRESENT_IN_DATABASE_SNAPSHOT") with
        {
            SourceUrls = ["https://downloads.sourceforge.net/project/aoia/OldFiles/synthetic.zip"],
            SourceHashes = [new string('b', 64)], Detail = oldFields.RootElement.Clone()
        };
        HistoricalRecord currentDiff = HistoricalCatalog.BuildRecord(item, [], [oldExport], new string('a', 64));
        Require(currentDiff.PatchObservations.Any(observation => observation.Kind == "EXPORTED_FIELDS_DIFFER_FROM_CURRENT_CANONICAL")
            && currentDiff.PrimaryClassification == "Unknown" && currentDiff.FirstKnownPatch == "17.7.2",
            "Qualified exported-field differences do not invent game patches or content origin.");
        Require(currentDiff.PatchObservations.Single(observation => observation.Kind == "EXPORTED_FIELDS_DIFFER_FROM_CURRENT_CANONICAL")
            .Detail!.Value.GetProperty("intervalStart").GetString() == "17.7.2",
            "Later identity presence cannot move the before-value source date forward.");
        count++;

        HistoricalRecord truncatedList = HistoricalCatalog.BuildRecord(item, [],
            [oldExport with { Kind = "MODIFIED_IN_DATABASE_PATCH", SourceUrls = ["https://aoitems.example.invalid/history"] }], new string('a', 64));
        Require(!truncatedList.PatchObservations.Any(observation => observation.Kind == "EXPORTED_FIELDS_DIFFER_FROM_CURRENT_CANONICAL"),
            "Truncated archived list labels are not evidence of an actual old item name.");
        count++;

        HistoricalCatalog.ValidateCoverage([template], [eligible]);
        MustReject(() => HistoricalCatalog.ValidateCoverage([template], []));
        MustReject(() => HistoricalCatalog.ValidateCoverage([template], [eligible with { TemplateId = 9999 }]));
        MustReject(() => HistoricalCatalog.ValidateCoverage([template], [eligible with { ClassificationEvidence = [chest with { TemplateId = 9999 }] }]));
        count++;

        byte[] first = JsonSerializer.SerializeToUtf8Bytes(HistoricalCatalog.BuildRecord(item, [chest], [lateAddition]), Program.JsonOptions);
        byte[] repeated = JsonSerializer.SerializeToUtf8Bytes(HistoricalCatalog.BuildRecord(item, [chest], [lateAddition]), Program.JsonOptions);
        Require(first.AsSpan().SequenceEqual(repeated), "Repeated historical record generation is byte-identical.");
        count++;
        Console.WriteLine("PASS " + count + " historical evidence, separation, acquisition and coverage checks");
        return 0;
    }

    static HistoricalAssertion Assertion(int id, string association) => new()
    {
        TemplateId = id, ContentAssociations = [association], IdentityMatch = "EXACT_TEMPLATE_ID_IN_SOURCE_LINK",
        Confidence = "PROVEN_DOCUMENTED_ASSOCIATION", SourceUrls = ["https://example.invalid/source"]
    };

    static HistoricalObservation Observation(int id, string patch, string kind) => new()
    {
        TemplateId = id, PatchVersion = patch, Kind = kind, Confidence = "PROVEN_DATABASE_TAG",
        SourceUrls = ["https://example.invalid/history"]
    };

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    static void MustReject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidDataException("Invalid historical coverage/evidence was accepted.");
    }
}
