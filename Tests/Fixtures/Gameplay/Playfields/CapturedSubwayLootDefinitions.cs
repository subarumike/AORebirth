namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class CapturedSubwayLootDefinitions
    {
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Condemned_Subway#Loot";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.subway.loot-membership.";
        internal const string AbmouthProfileKey =
            "subway.127.boss.abmouth-supremus";
        internal const string VergilProfileKey =
            "subway.127.boss.vergil-aeneid";
        internal const string EumenidesProfileKey =
            "subway.127.named.eumenides";
        internal const string StrikeForemanProfileKey =
            "subway.127.named.strike-foreman";
        internal const string AbmouthInfectorProfileKey =
            "subway.127.encounter.abmouth-infector";

        private static readonly int[] AnyMobItemIds =
        {
            234877,
            234875,
            234876,
            234874,
            202731,
            202743,
            301707,
            301716,
            301714,
            301717,
            301715,
            301708,
            301709,
            301718,
            301712,
            301711,
            301713,
            301710
        };

        private static readonly int[] LivingCyberArmorItemIds =
        {
            163432,
            163430,
            163426,
            160051
        };

        internal static int[] DocumentedSourceItemIds
        {
            get
            {
                return AnyMobItemIds
                    .Concat(
                        new[]
                        {
                            204397,
                            258543,
                            292256,
                            291043,
                            202720,
                            202756,
                            202723,
                            204396
                        })
                    .Concat(LivingCyberArmorItemIds)
                    .Concat(new[] { 287146, 202717, 202733, 202741 })
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
            }
        }

        internal static bool ApplyDocumentedMembership(
            LootTableDefinition table,
            string profileKey,
            string displayName)
        {
            if (table == null || string.IsNullOrWhiteSpace(profileKey))
            {
                return false;
            }

            int[] documentedIds = DocumentedItemIdsForProfile(profileKey, displayName);
            if (documentedIds.Length == 0)
            {
                return false;
            }

            string groupKey = DocumentedLootGroupPrefix + profileKey;
            LootGroupDefinition[] existingGroups = table.RollGroups
                ?? new LootGroupDefinition[0];
            if (existingGroups.Any(
                    value => string.Equals(
                        value.LootGroupKey,
                        groupKey,
                        StringComparison.Ordinal)))
            {
                return true;
            }

            var existingItemIds = new HashSet<int>(
                existingGroups
                    .Where(value => value != null && value.Entries != null)
                    .SelectMany(value => value.Entries)
                    .Where(value => value != null)
                    .SelectMany(
                        value => new[]
                        {
                            value.ItemTemplateId,
                            value.HighItemTemplateId
                        })
                    .Concat(
                        (table.ObservedCorpseSnapshots
                         ?? new ObservedCorpseSnapshotDefinition[0])
                            .Where(value => value != null && value.Entries != null)
                            .SelectMany(value => value.Entries)
                            .Where(value => value != null)
                            .SelectMany(
                                value => new[]
                                {
                                    value.ItemTemplateId,
                                    value.HighItemTemplateId
                                }))
                    .Where(value => value > 0));
            int[] missingIds = documentedIds
                .Where(value => !existingItemIds.Contains(value))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (missingIds.Length == 0)
            {
                return false;
            }

            table.RollGroups = existingGroups.Concat(
                new[]
                {
                    new LootGroupDefinition
                    {
                        LootGroupKey = groupKey,
                        RollMode = LootRollMode.WeightedOne,
                        RollCount = 1,
                        EmptyWeight = 0,
                        DropChanceBasisPoints = 10000,
                        Entries = missingIds.Select(DocumentedEntry).ToArray(),
                        Conditions = new string[0]
                    }
                }).ToArray();
            table.AllowsDocumentedSupplement = true;
            return true;
        }

        internal static int[] DocumentedItemIdsForProfile(
            string profileKey,
            string displayName)
        {
            if (!IsDocumentedSubwayEnemy(profileKey))
            {
                return new int[0];
            }

            var itemIds = new List<int>(AnyMobItemIds);
            switch (displayName ?? string.Empty)
            {
                case "Violent Vagabond":
                    itemIds.AddRange(new[] { 204397, 258543 });
                    break;
                case "Stim Fiend":
                    itemIds.AddRange(new[] { 292256, 291043 });
                    break;
                case "Workman Striker":
                    itemIds.Add(202720);
                    break;
                case "Architect Striker":
                    itemIds.Add(202756);
                    break;
                case "Premature Pattern":
                    itemIds.Add(204396);
                    break;
            }

            if (IsLivingCyberArmorDropper(displayName)
                || string.Equals(
                    profileKey,
                    EumenidesProfileKey,
                    StringComparison.Ordinal)
                || string.Equals(
                    profileKey,
                    VergilProfileKey,
                    StringComparison.Ordinal))
            {
                itemIds.AddRange(LivingCyberArmorItemIds);
            }

            if (string.Equals(
                    profileKey,
                    EumenidesProfileKey,
                    StringComparison.Ordinal))
            {
                itemIds.AddRange(new[] { 287146, 202717 });
            }
            else if (string.Equals(
                    profileKey,
                    VergilProfileKey,
                    StringComparison.Ordinal))
            {
                itemIds.AddRange(new[] { 287146, 202733 });
            }
            else if (string.Equals(
                    profileKey,
                    AbmouthProfileKey,
                    StringComparison.Ordinal))
            {
                itemIds.AddRange(new[] { 287146, 202717, 202733, 202741 });
            }
            else if (string.Equals(
                    profileKey,
                    StrikeForemanProfileKey,
                    StringComparison.Ordinal))
            {
                itemIds.Add(202723);
            }

            return itemIds.Distinct().OrderBy(value => value).ToArray();
        }

        private static bool IsDocumentedSubwayEnemy(string profileKey)
        {
            return profileKey.StartsWith("subway.supported.", StringComparison.Ordinal)
                   || profileKey.StartsWith("subway.ordinary.", StringComparison.Ordinal)
                   || string.Equals(
                       profileKey,
                       AbmouthProfileKey,
                       StringComparison.Ordinal)
                   || string.Equals(
                       profileKey,
                       VergilProfileKey,
                       StringComparison.Ordinal)
                   || string.Equals(
                       profileKey,
                       EumenidesProfileKey,
                       StringComparison.Ordinal)
                   || string.Equals(
                       profileKey,
                       StrikeForemanProfileKey,
                       StringComparison.Ordinal);
        }

        private static bool IsLivingCyberArmorDropper(string displayName)
        {
            switch (displayName ?? string.Empty)
            {
                case "Neural Burnout":
                case "Premature Pattern":
                case "Incomplete Rebuild":
                case "Fragmented Soul":
                case "Empty Shell":
                case "Melded Patterns":
                    return true;
                default:
                    return false;
            }
        }

        private static LootEntryDefinition DocumentedEntry(int itemId)
        {
            return new LootEntryDefinition
            {
                ItemTemplateId = itemId,
                HighItemTemplateId = itemId,
                FixedQuality = 1,
                MinimumQuality = 1,
                MaximumQuality = 1,
                MinimumQuantity = 1,
                MaximumQuantity = 1,
                Weight = 1,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.WeightedDocumented,
                Evidence = LootEvidenceConfidence.CommunityDocumented,
                EvidenceReference = DocumentedLootSourceUrl,
                ProbabilityEvidence = "unresolved-membership-only"
            };
        }
    }
}
