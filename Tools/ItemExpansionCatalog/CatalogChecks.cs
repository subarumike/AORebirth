using System.Text.Json;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class CatalogChecks
{
    internal static int Run()
    {
        int checks = 0;
        FamilyMetadata empty = FamilyMetadata.Build([], null);

        CatalogItem missing = Record(Item(101, 1), 0, empty);
        RequireUnknown(missing, "Absent expansion metadata must not become Rubi-Ka.");
        Require(!missing.Evidence.HasExpansionInformation, "Missing metadata must be distinguishable from an explicit value.");
        checks++;

        DatItemTemplate zeroTemplate = Item(102, 1);
        zeroTemplate.Stats[(int)CharacterStat.Expansion] = 0;
        CatalogItem zero = Record(zeroTemplate, 0, empty);
        RequireUnknown(zero, "An explicit zero entitlement mask does not prove base-game origin.");
        checks++;

        DatItemTemplate shadowlandsTemplate = Item(103, 200);
        shadowlandsTemplate.Stats[(int)CharacterStat.Expansion] = (int)Expansions.ShadowLands;
        CatalogItem shadowlands = Record(shadowlandsTemplate, 0, empty);
        RequireUnknown(shadowlands, "A direct template stat alone does not establish a positive item requirement.");
        Require(shadowlands.Evidence.HasExpansionInformation, "An observed expansion stat must not be discarded.");
        checks++;

        DatItemTemplate multipleTemplate = Item(104, 100);
        multipleTemplate.Stats[(int)CharacterStat.Expansion] = (int)(Expansions.ShadowLands | Expansions.AlienInvasion);
        RequireUnknown(Record(multipleTemplate, 0, empty), "A multi-bit entitlement mask must not imply MultipleExpansions origin.");
        checks++;

        (Expansions Flag, string Classification)[] singleRequirements =
        [
            (Expansions.ShadowLands, "Shadowlands"),
            (Expansions.AlienInvasion, "AlienInvasion"),
            (Expansions.LostEden, "LostEden"),
            (Expansions.LegacyOfTheXan, "Xan"),
            (Expansions.NotumWars, "NotumWars")
        ];
        foreach ((Expansions flag, string classification) in singleRequirements)
        {
            CatalogItem required = Record(ActionItem(105, ExpansionRequirement(flag)), 0, empty);
            Require(required.ExpansionClassification == classification
                && required.VerifiedExpansions.SequenceEqual([classification]),
                "An explicit positive item requirement must classify " + classification + ".");
            Require(required.OriginalClassificationValue == (int)flag && required.SourceField != null,
                "A verified expansion requirement must retain its original value and source field.");
            checks++;
        }

        DatRequirement skill = new()
        {
            Statnumber = (int)CharacterStat.Pistol, Value = 100,
            Operator = Operator.GreaterThan, Target = ItemTarget.User
        };
        CatalogItem conjunction = Record(ActionItem(109, skill,
            ExpansionRequirement(Expansions.LostEden), Boolean(Operator.And)), 0, empty);
        Require(conjunction.ExpansionClassification == "LostEden",
            "An unrelated skill requirement joined by AND must preserve a positive expansion requirement.");
        checks++;

        CatalogItem legacyConjunction = Record(ActionItem(115, skill,
            ExpansionRequirement(Expansions.LostEden)), 0, empty);
        Require(legacyConjunction.ExpansionClassification == "LostEden",
            "A legacy implicit-AND requirement list must retain a positive expansion requirement.");
        checks++;

        DatItemTemplate mirroredTemplate = ActionItem(116, ExpansionRequirement(Expansions.LostEden));
        mirroredTemplate.Actions[0].Requirements[0].Target = ItemTarget.Self;
        mirroredTemplate.Rdb = new DatRdbTemplate
        {
            ReadComplete = true,
            ActionCriteria = new Dictionary<int, List<DatCriterion>>
            {
                [(int)ActionType.ToWear] =
                [
                    new DatCriterion
                    {
                        Value1 = (int)CharacterStat.Expansion, Value2 = (int)Expansions.LostEden,
                        Operator = (int)Operator.BitAnd
                    }
                ]
            }
        };
        CatalogItem mirrored = Record(mirroredTemplate, 0, empty);
        Require(mirrored.ExpansionClassification == "LostEden" && !mirrored.Evidence.HasConflictingEvidence,
            "A complete consistent raw/mapped item requirement must classify without a mirror conflict.");
        mirroredTemplate.Rdb.ReadComplete = false;
        RequireUnknown(Record(mirroredTemplate, 0, empty),
            "An incomplete present RDB record must not establish a verified expansion requirement.");
        checks++;

        CatalogItem alternative = Record(ActionItem(110, skill,
            ExpansionRequirement(Expansions.LostEden), Boolean(Operator.Or)), 0, empty);
        RequireUnknown(alternative, "A non-expansion alternative joined by OR cannot prove a mandatory expansion association.");
        Require(alternative.Evidence.HasExpansionInformation, "Ambiguous expansion-related action criteria must remain traceable.");
        checks++;

        DatRequirement negated = ExpansionRequirement(Expansions.ShadowLands);
        negated.Operator = Operator.NotBitAnd;
        RequireUnknown(Record(ActionItem(111, negated), 0, empty),
            "A negated expansion requirement must not assign that expansion.");
        RequireUnknown(Record(ActionItem(112, ExpansionRequirement(Expansions.ShadowLands), Boolean(Operator.Not)), 0, empty),
            "A postfix NOT must not turn an excluded expansion into a positive association.");
        checks++;

        Expansions combinedFlags = Expansions.ShadowLands | Expansions.AlienInvasion;
        CatalogItem combined = Record(ActionItem(113, ExpansionRequirement(combinedFlags)), 0, empty);
        Require(combined.ExpansionClassification == "MultipleExpansions"
            && combined.VerifiedExpansions.Order(StringComparer.Ordinal).SequenceEqual(["AlienInvasion", "Shadowlands"])
            && combined.OriginalClassificationValue == (int)combinedFlags,
            "A verified multi-bit expansion requirement must preserve both associations and the original mask.");
        checks++;

        CatalogItem twoRequirements = Record(ActionItem(114, ExpansionRequirement(Expansions.ShadowLands),
            ExpansionRequirement(Expansions.AlienInvasion), Boolean(Operator.And)), 0, empty);
        Require(twoRequirements.ExpansionClassification == "MultipleExpansions"
            && twoRequirements.VerifiedExpansions.Order(StringComparer.Ordinal).SequenceEqual(["AlienInvasion", "Shadowlands"]),
            "Two positive expansion requirements joined by AND must preserve both associations.");
        checks++;

        CatalogItem positiveAlternatives = Record(ActionItem(117, ExpansionRequirement(Expansions.ShadowLands),
            ExpansionRequirement(Expansions.AlienInvasion), Boolean(Operator.Or)), 0, empty);
        Require(positiveAlternatives.ExpansionClassification == "MultipleExpansions"
            && positiveAlternatives.VerifiedExpansions.Order(StringComparer.Ordinal).SequenceEqual(["AlienInvasion", "Shadowlands"]),
            "An OR whose every branch has a positive expansion requirement must preserve the explicit alternatives.");
        RequireUnknown(Record(ActionItem(118, ExpansionRequirement(Expansions.Mail)), 0, empty),
            "Mail entitlement alone must not become an expansion or base-game item classification.");
        checks++;

        CatalogItem equality = Record(ActionItem(119, EqualityRequirement(Expansions.ShadowLands)), 0, empty);
        Require(equality.ExpansionClassification == "Shadowlands"
            && equality.VerifiedExpansions.SequenceEqual(["Shadowlands"]),
            "A satisfiable positive expansion equality must establish its explicit association.");
        checks++;

        RequireUnknown(Record(ActionItem(120, EqualityRequirement(Expansions.ShadowLands),
            EqualityRequirement(Expansions.AlienInvasion), Boolean(Operator.And)), 0, empty),
            "Contradictory equalities in one action cannot establish any expansion association.");
        checks++;

        RequireUnknown(Record(ActionItem(121, EqualityRequirement(Expansions.AlienInvasion),
            ExpansionRequirement(Expansions.ShadowLands), Boolean(Operator.And)), 0, empty),
            "An exact expansion equality joined with an incompatible positive bit check is unsatisfiable.");
        checks++;

        CatalogItem equalityAlternatives = Record(ActionItem(122, EqualityRequirement(Expansions.ShadowLands),
            EqualityRequirement(Expansions.AlienInvasion), Boolean(Operator.Or)), 0, empty);
        Require(equalityAlternatives.ExpansionClassification == "MultipleExpansions"
            && equalityAlternatives.VerifiedExpansions.Order(StringComparer.Ordinal).SequenceEqual(["AlienInvasion", "Shadowlands"]),
            "Satisfiable alternative expansion equalities must preserve their explicit associations.");
        checks++;

        CatalogItem constrainedAlternatives = Record(ActionItem(123,
            ExpansionRequirement(Expansions.ShadowLands | Expansions.AlienInvasion),
            EqualityRequirement(Expansions.AlienInvasion), Boolean(Operator.And)), 0, empty);
        Require(constrainedAlternatives.ExpansionClassification == "AlienInvasion"
            && constrainedAlternatives.VerifiedExpansions.SequenceEqual(["AlienInvasion"]),
            "An exact mask must remove impossible individual bits from an ANY-bit alternative.");
        checks++;

        CatalogItem constrainedBranches = Record(ActionItem(124,
            EqualityRequirement(Expansions.ShadowLands), EqualityRequirement(Expansions.AlienInvasion),
            Boolean(Operator.Or), EqualityRequirement(Expansions.AlienInvasion), Boolean(Operator.And)), 0, empty);
        Require(constrainedBranches.ExpansionClassification == "AlienInvasion"
            && constrainedBranches.VerifiedExpansions.SequenceEqual(["AlienInvasion"]),
            "An enclosing exact mask must remove an unreachable positive OR branch.");
        checks++;

        DatItemTemplate named = Item(106, 1);
        named.Name = "The Key to a Garden and Sanctuary";
        CatalogItem gardenName = Record(named, 0, empty);
        named.Name = "Unrelated item name";
        CatalogItem otherName = Record(named, 0, empty);
        RequireUnknown(gardenName, "Garden/sanctuary words cannot classify an item.");
        Require(JsonSerializer.Serialize(gardenName.Evidence, Program.JsonOptions)
            == JsonSerializer.Serialize(otherName.Evidence, Program.JsonOptions), "Name-only changes must not alter classification evidence.");
        checks++;

        DatItemTemplate unsupportedTemplate = Item(107, 1);
        const int unsupportedMask = 1 << 20;
        unsupportedTemplate.Stats[(int)CharacterStat.Expansion] = unsupportedMask;
        CatalogItem unsupported = Record(unsupportedTemplate, 0, empty);
        RequireUnknown(unsupported, "Unsupported entitlement bits must remain Unknown.");
        Require(unsupported.Evidence.HasUnsupportedValue, "Unsupported entitlement bits must be flagged.");
        Require(JsonSerializer.Serialize(unsupported.Evidence, Program.JsonOptions)
            .Contains(unsupportedMask.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
            "Unsupported raw values must remain in the evidence.");
        checks++;

        DatItemTemplate conflictingTemplate = Item(108, 1);
        conflictingTemplate.Stats[(int)CharacterStat.Expansion] = (int)Expansions.ShadowLands;
        conflictingTemplate.Rdb = new DatRdbTemplate
        {
            ReadComplete = true,
            Stats =
            [
                new DatIntPair { Key = (int)CharacterStat.Expansion, Value = (int)Expansions.ShadowLands },
                new DatIntPair { Key = (int)CharacterStat.Expansion, Value = (int)Expansions.AlienInvasion }
            ]
        };
        CatalogItem conflicting = Record(conflictingTemplate, 0, empty);
        RequireUnknown(conflicting, "Conflicting raw values must not become an expansion assignment.");
        Require(conflicting.Evidence.HasConflictingEvidence, "Conflicting direct raw values must be visible despite merged stat overwrite.");
        checks++;

        DatItemTemplate[] familyInput =
        [
            Item(201, 1), Item(202, 200), Item(203, 50), Item(204, 50),
            Item(205, 20), Item(206, 1), Item(206, 100), Item(207, 10)
        ];
        const string familyJson = """
            {
              "GOOD": { "Templates": [201, 202] },
              "SAME": { "Templates": [203, 204] },
              "MISS": { "Templates": [205, 999] },
              "AMBG": { "Templates": [206, 207] },
              "SOLO": { "Templates": [207] }
            }
            """;
        FamilyMetadata families = FamilyMetadata.Build(familyInput, familyJson);
        CatalogItem firstEndpoint = Record(familyInput[0], 0, families);
        CatalogItem lastEndpoint = Record(familyInput[1], 1, families);
        Require(firstEndpoint.MinimumSupportedQuality == 1 && firstEndpoint.MaximumSupportedQuality == 200
            && lastEndpoint.MinimumSupportedQuality == 1 && lastEndpoint.MaximumSupportedQuality == 200,
            "An explicit interpolation family must retain both supported QL endpoints.");
        Require(firstEndpoint.TemplateId == 201 && firstEndpoint.StoredQuality == 1
            && lastEndpoint.TemplateId == 202 && lastEndpoint.StoredQuality == 200,
            "Family ranges must preserve original identities and stored quality variants.");
        checks++;

        Require(families.ForTemplate(203).All(link => link.MinimumQuality == null && link.MaximumQuality == null),
            "Same-QL variants do not prove an interpolation range.");
        Require(families.ForTemplate(205).All(link => link.MinimumQuality == null && link.MaximumQuality == null)
            && families.MissingTemplateIds.SequenceEqual([999]), "An incomplete family cannot invent supported QLs.");
        Require(families.ForTemplate(206).All(link => link.MinimumQuality == null && link.MaximumQuality == null),
            "A duplicated template ID with distinct stored QLs makes its endpoint range ambiguous.");
        Require(families.ForTemplate(207).Single(link => link.FamilyHash == "SOLO").MinimumQuality == null,
            "A single explicit link does not establish a variable QL range.");
        checks++;

        CatalogItem[] familyRecords = familyInput.Select((item, index) => Record(item, index, families)).ToArray();
        Program.ValidateCoverage(familyInput, familyRecords);
        Require(familyRecords.Count(record => record.TemplateId == 206) == 2
            && familyRecords.Where(record => record.TemplateId == 206).Select(record => record.StoredQuality).SequenceEqual([1, 100]),
            "Coverage must preserve repeated IDs with distinct stored QLs.");
        checks++;

        MustReject(() => Program.ValidateCoverage(familyInput, familyRecords[..^1]), "Missing primary assignments must fail validation.");
        CatalogItem[] duplicatedAssignment = (CatalogItem[])familyRecords.Clone();
        duplicatedAssignment[1] = duplicatedAssignment[0];
        MustReject(() => Program.ValidateCoverage(familyInput, duplicatedAssignment), "Duplicate primary assignments must fail validation.");
        CatalogItem[] changedIdentity = (CatalogItem[])familyRecords.Clone();
        changedIdentity[0] = changedIdentity[0] with { TemplateId = 9999 };
        MustReject(() => Program.ValidateCoverage(familyInput, changedIdentity), "Altered item identities must fail validation.");
        checks++;

        CatalogItem[] fabricated = (CatalogItem[])familyRecords.Clone();
        fabricated[0] = fabricated[0] with { ExpansionClassification = "RubiKa" };
        MustReject(() => Program.ValidateCoverage(familyInput, fabricated), "Unsupported expansion assignments must fail validation.");
        checks++;

        byte[] originalBytes = JsonSerializer.SerializeToUtf8Bytes(familyRecords, Program.JsonOptions);
        CatalogItem[] repeatedRecords = familyInput.Select((item, index) => Record(item, index,
            FamilyMetadata.Build(familyInput, familyJson))).ToArray();
        byte[] repeatedBytes = JsonSerializer.SerializeToUtf8Bytes(repeatedRecords, Program.JsonOptions);
        Require(originalBytes.AsSpan().SequenceEqual(repeatedBytes), "Repeated generation must serialize to identical bytes.");
        checks++;

        Console.WriteLine("PASS " + checks + " focused classification, family, coverage and determinism checks");
        return 0;
    }

    static DatItemTemplate Item(int id, int quality)
        => new() { ID = id, Quality = quality, Name = "Fixture item" };

    static DatItemTemplate ActionItem(int id, params DatRequirement[] requirements)
    {
        DatItemTemplate item = Item(id, 1);
        item.Actions.Add(new DatAction { ActionType = ActionType.ToWear, Requirements = requirements.ToList() });
        return item;
    }

    static DatRequirement ExpansionRequirement(Expansions flag)
        => new()
        {
            Statnumber = (int)CharacterStat.Expansion, Value = (int)flag,
            Operator = Operator.BitAnd, Target = ItemTarget.User
        };

    static DatRequirement Boolean(Operator operation)
        => new() { Statnumber = (int)CharacterStat.Flags, Operator = operation, Target = ItemTarget.User };

    static DatRequirement EqualityRequirement(Expansions flag)
    {
        DatRequirement requirement = ExpansionRequirement(flag);
        requirement.Operator = Operator.EqualTo;
        return requirement;
    }

    static CatalogItem Record(DatItemTemplate item, int index, FamilyMetadata families)
        => Program.BuildRecord(item, index, families);

    static void RequireUnknown(CatalogItem item, string message)
        => Require(item.ExpansionClassification == "Unknown" && item.VerifiedExpansions.Length == 0, message);

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    static void MustReject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidDataException(message);
    }
}
