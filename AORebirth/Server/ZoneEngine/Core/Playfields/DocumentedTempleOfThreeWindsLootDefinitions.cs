namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class TempleOfThreeWindsDocumentedDropDefinition
    {
        internal string ProfileKey { get; set; }
        internal string BossDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int MinimumDropChanceBasisPoints { get; set; }
        internal int MaximumDropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
    }

    internal static class DocumentedTempleOfThreeWindsLootDefinitions
    {
        internal const int PlayfieldInstance = 1931;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Temple_of_Three_Winds";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.temple-of-three-winds.";

        private static readonly TempleOfThreeWindsDocumentedDropDefinition[] Drops =
            BuildDrops();

        internal static TempleOfThreeWindsDocumentedDropDefinition[] DocumentedDrops
        {
            get { return Drops.ToArray(); }
        }

        internal static int[] DocumentedSourceItemIds
        {
            get
            {
                return Drops
                    .Select(value => value.ItemTemplateId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
            }
        }

        internal static TempleOfThreeWindsDocumentedDropDefinition[] DropsForProfile(
            int playfieldId,
            string profileKey)
        {
            if (playfieldId != PlayfieldInstance || string.IsNullOrWhiteSpace(profileKey))
            {
                return new TempleOfThreeWindsDocumentedDropDefinition[0];
            }

            return Drops
                .Where(
                    value => string.Equals(
                        value.ProfileKey,
                        profileKey,
                        StringComparison.Ordinal))
                .ToArray();
        }

        internal static bool ApplyDocumentedBossLoot(
            LootTableDefinition table,
            int playfieldId,
            string profileKey)
        {
            if (table == null)
            {
                return false;
            }

            TempleOfThreeWindsDocumentedDropDefinition[] documented =
                DropsForProfile(playfieldId, profileKey);
            if (documented.Length == 0)
            {
                return false;
            }

            LootGroupDefinition[] existingGroups = table.RollGroups
                ?? new LootGroupDefinition[0];
            var existingItemIds = new HashSet<int>(
                existingGroups
                    .Where(value => value != null && value.Entries != null)
                    .SelectMany(value => value.Entries)
                    .Where(value => value != null)
                    .SelectMany(ItemIds)
                    .Concat(
                        (table.ObservedCorpseSnapshots
                         ?? new ObservedCorpseSnapshotDefinition[0])
                            .Where(value => value != null && value.Entries != null)
                            .SelectMany(value => value.Entries)
                            .Where(value => value != null)
                            .SelectMany(ItemIds))
                    .Where(value => value > 0));
            var groupKeys = new HashSet<string>(
                existingGroups
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (TempleOfThreeWindsDocumentedDropDefinition drop in documented)
            {
                string groupKey = DocumentedLootGroupPrefix
                                  + drop.ProfileKey
                                  + "."
                                  + drop.ItemTemplateId;
                if (existingItemIds.Contains(drop.ItemTemplateId)
                    || groupKeys.Contains(groupKey))
                {
                    continue;
                }

                additions.Add(DocumentedGroup(groupKey, drop));
                existingItemIds.Add(drop.ItemTemplateId);
                groupKeys.Add(groupKey);
            }

            if (additions.Count == 0)
            {
                return false;
            }

            table.RollGroups = existingGroups.Concat(additions).ToArray();
            table.AllowsDocumentedSupplement = true;
            return true;
        }

        private static IEnumerable<int> ItemIds(LootEntryDefinition value)
        {
            return new[] { value.ItemTemplateId, value.HighItemTemplateId };
        }

        private static LootGroupDefinition DocumentedGroup(
            string groupKey,
            TempleOfThreeWindsDocumentedDropDefinition drop)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = groupKey,
                RollMode = LootRollMode.Independent,
                RollCount = 1,
                EmptyWeight = 0,
                DropChanceBasisPoints = drop.MinimumDropChanceBasisPoints,
                Entries = new[]
                {
                    new LootEntryDefinition
                    {
                        ItemTemplateId = drop.ItemTemplateId,
                        HighItemTemplateId = drop.ItemTemplateId,
                        FixedQuality = drop.Quality,
                        MinimumQuality = drop.Quality,
                        MaximumQuality = drop.Quality,
                        MinimumQuantity = 1,
                        MaximumQuantity = 1,
                        Weight = 1,
                        DropChanceBasisPoints = 10000,
                        UniquePerCorpse = true,
                        Semantics = LootSemantics.WeightedDocumented,
                        Evidence = LootEvidenceConfidence.CommunityDocumented,
                        EvidenceReference = DocumentedLootSourceUrl,
                        ProbabilityEvidence = "documented-lower-bound:"
                                              + drop.SourceProbability
                    }
                },
                Conditions = new string[0]
            };
        }

        private static TempleOfThreeWindsDocumentedDropDefinition[] BuildDrops()
        {
            return new[]
            {
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey,
                    "Windcaller Yatila",
                    "Platinum Ring of the Three PM/SI",
                    204576,
                    600,
                    900,
                    "approximately 6-9%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey,
                    "The Curator",
                    "Notum Ring of the Three MM/BM",
                    204577,
                    200,
                    300,
                    "approximately 2-3%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey,
                    "The Curator",
                    "Notum Ring of the Three TS/MC",
                    204578,
                    600,
                    700,
                    "approximately 6-7%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey,
                    "The Curator",
                    "Platinum Ring of the Three TS/MC",
                    204575,
                    100,
                    100,
                    "approximately 1%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey,
                    "Nematet the Custodian of Time",
                    "Skull of the Ancient",
                    204647,
                    2000,
                    2000,
                    "approximately 20%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey,
                    "Nematet the Custodian of Time",
                    "Nematet's Inner Eye",
                    204613,
                    300,
                    500,
                    "approximately 3-5%"),
                Drop(
                    CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey,
                    "Guardian of Tomorrow",
                    "Guardian Tank Armor",
                    204748,
                    400,
                    500,
                    "approximately 4-5%")
            };
        }

        private static TempleOfThreeWindsDocumentedDropDefinition Drop(
            string profileKey,
            string bossDisplayName,
            string itemName,
            int itemTemplateId,
            int minimumDropChanceBasisPoints,
            int maximumDropChanceBasisPoints,
            string sourceProbability)
        {
            return new TempleOfThreeWindsDocumentedDropDefinition
            {
                ProfileKey = profileKey,
                BossDisplayName = bossDisplayName,
                ItemName = itemName,
                ItemTemplateId = itemTemplateId,
                Quality = 1,
                MinimumDropChanceBasisPoints = minimumDropChanceBasisPoints,
                MaximumDropChanceBasisPoints = maximumDropChanceBasisPoints,
                SourceProbability = sourceProbability
            };
        }
    }
}
