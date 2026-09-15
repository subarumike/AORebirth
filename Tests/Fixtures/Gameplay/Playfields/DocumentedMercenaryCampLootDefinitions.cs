namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class MercenaryCampDocumentedDropDefinition
    {
        internal string EnemyKey { get; set; }
        internal string EnemyDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int DropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
        internal bool IsActive { get; set; }
    }

    internal static class DocumentedMercenaryCampLootDefinitions
    {
        internal const int PlayfieldInstance = 620;
        internal const int BreastplateOfAzureReveriesItemId = 165304;
        internal const int NellyJohnsonsLittleBlackDressItemId = 165214;
        internal const int FancyStethoscopicGlassesItemId = 165176;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Unique_Encounters";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.mercenary-camp.";

        internal const string IanWarrKey = "mercenary-camp.620.boss.ian-warr";
        internal const string NellyJohnsonKey = "mercenary-camp.620.boss.nelly-johnson";
        internal const string PatriciaJohnsonKey = "mercenary-camp.620.boss.patricia-johnson";
        internal const string PeterLeeKey = "mercenary-camp.620.boss.peter-lee";
        internal const string RisLeeKey = "mercenary-camp.620.boss.ris-lee";

        private static readonly MercenaryCampDocumentedDropDefinition[] Drops = BuildDrops();

        internal static MercenaryCampDocumentedDropDefinition[] DocumentedDrops
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

        internal static MercenaryCampDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new MercenaryCampDocumentedDropDefinition[0];
            }

            string enemyKey = EnemyKeyForDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(enemyKey))
            {
                return new MercenaryCampDocumentedDropDefinition[0];
            }

            return Drops
                .Where(value => string.Equals(value.EnemyKey, enemyKey, StringComparison.Ordinal))
                .ToArray();
        }

        internal static bool ApplyDocumentedLoot(
            LootTableDefinition table,
            int playfieldId,
            string displayName)
        {
            if (table == null)
            {
                return false;
            }

            MercenaryCampDocumentedDropDefinition[] active =
                DropsForDisplayName(playfieldId, displayName)
                    .Where(value => value.IsActive)
                    .ToArray();
            if (active.Length == 0)
            {
                return false;
            }

            LootGroupDefinition[] existing = table.RollGroups
                ?? new LootGroupDefinition[0];
            var existingItemIds = new HashSet<int>(
                existing
                    .Where(value => value != null && value.Entries != null)
                    .SelectMany(value => value.Entries)
                    .Where(value => value != null)
                    .Select(value => value.ItemTemplateId)
                    .Concat(
                        (table.ObservedCorpseSnapshots
                         ?? new ObservedCorpseSnapshotDefinition[0])
                            .Where(value => value != null && value.Entries != null)
                            .SelectMany(value => value.Entries)
                            .Where(value => value != null)
                            .Select(value => value.ItemTemplateId))
                    .Where(value => value > 0));
            var groupKeys = new HashSet<string>(
                existing
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (MercenaryCampDocumentedDropDefinition drop in active)
            {
                string groupKey = DocumentedLootGroupPrefix
                                  + drop.EnemyKey
                                  + "."
                                  + drop.ItemTemplateId;
                if (existingItemIds.Contains(drop.ItemTemplateId)
                    || groupKeys.Contains(groupKey))
                {
                    continue;
                }

                additions.Add(DocumentedIndependentGroup(groupKey, drop));
                existingItemIds.Add(drop.ItemTemplateId);
                groupKeys.Add(groupKey);
            }

            if (additions.Count == 0)
            {
                return false;
            }

            table.RollGroups = existing.Concat(additions).ToArray();
            table.AllowsDocumentedSupplement = true;
            return true;
        }

        private static LootGroupDefinition DocumentedIndependentGroup(
            string groupKey,
            MercenaryCampDocumentedDropDefinition drop)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = groupKey,
                RollMode = LootRollMode.Independent,
                RollCount = 1,
                EmptyWeight = 0,
                DropChanceBasisPoints = drop.DropChanceBasisPoints,
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
                        DropChanceBasisPoints = drop.DropChanceBasisPoints,
                        UniquePerCorpse = true,
                        Semantics = LootSemantics.WeightedDocumented,
                        Evidence = LootEvidenceConfidence.CommunityDocumented,
                        EvidenceReference = DocumentedLootSourceUrl,
                        ProbabilityEvidence = "documented-exact:" + drop.SourceProbability
                    }
                },
                Conditions = new string[0]
            };
        }

        private static MercenaryCampDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<MercenaryCampDocumentedDropDefinition>();

            AddInactive(values, IanWarrKey, "Ian Warr", "Average Gloves", 165207, 100, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Blood Bat", 165130, 200, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Blood Mace", 165127, 200, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Flaxen Notum Pants", 165206, 200, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Pioneer of Jobe Cloak", 165203, 1, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Rust-pitted Ring", 156772, 100, "one random extra is published; per-item probability is not published");
            AddInactive(values, IanWarrKey, "Ian Warr", "Stone Samurai Boots", 165205, 200, "one random extra is published; per-item probability is not published");
            AddAzureMemberships(
                values,
                IanWarrKey,
                "Ian Warr",
                "one random Azure piece is published; per-item probability is not published",
                BreastplateOfAzureReveriesItemId,
                "explicit 100% breastplate; the separate random Azure slot remains unresolved");

            AddInactive(values, NellyJohnsonKey, "Nelly Johnson", "Anything", 165215, 200, "membership published; rate not published");
            AddAzureMemberships(values, NellyJohnsonKey, "Nelly Johnson", "membership published; rate not published", 0, null);
            AddActive(values, NellyJohnsonKey, "Nelly Johnson", "Nelly Johnsons Little Black Dress", NellyJohnsonsLittleBlackDressItemId, 200, "explicit 100%", 10000);

            AddAzureMemberships(values, PatriciaJohnsonKey, "Patricia Johnson", "approximately 70% for one Azure piece; per-item probability is not published", 0, null);
            AddInactive(values, PatriciaJohnsonKey, "Patricia Johnson", "Luxurious Rubber Pants", 168670, 200, "membership published; rate not published");
            AddInactive(values, PatriciaJohnsonKey, "Patricia Johnson", "Luxurious Rubber Shirt", 168672, 200, "membership published; rate not published");
            AddInactive(values, PatriciaJohnsonKey, "Patricia Johnson", "Luxurious Rubber Sleeves", 168671, 200, "membership published; rate not published");
            AddInactive(values, PatriciaJohnsonKey, "Patricia Johnson", "Pain of Patricia", 168675, 200, "membership published; rate not published");
            AddInactive(values, PatriciaJohnsonKey, "Patricia Johnson", "Reign of Patricia", 212995, 200, "membership published; rate not published");

            AddAzureMemberships(values, PeterLeeKey, "Peter Lee", "one random Azure piece is published; per-item probability is not published", 0, null);
            AddInactive(values, PeterLeeKey, "Peter Lee", "Heavy Notum Tank Armor", 165213, 200, "one random Notum Tank Armor is published; per-item probability is not published");
            AddInactive(values, PeterLeeKey, "Peter Lee", "Light Notum Tank Armor", 165208, 200, "one random Notum Tank Armor is published; per-item probability is not published");
            AddInactive(values, PeterLeeKey, "Peter Lee", "Medium Notum Tank Armor", 165209, 200, "one random Notum Tank Armor is published; per-item probability is not published");

            AddAzureMemberships(values, RisLeeKey, "Ris Lee", "two random Azure pieces are published; per-item probability and duplicate behavior are not published", 0, null);
            AddActive(values, RisLeeKey, "Ris Lee", "Fancy Stethoscopic Glasses", FancyStethoscopicGlassesItemId, 1, "explicit 100%", 10000);

            return values.ToArray();
        }

        private static void AddAzureMemberships(
            ICollection<MercenaryCampDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string unresolvedProbability,
            int activeItemId,
            string activeProbability)
        {
            AddAzure(values, enemyKey, enemyDisplayName, "Boots of Azure Reveries", 165305, unresolvedProbability, activeItemId, activeProbability);
            AddAzure(values, enemyKey, enemyDisplayName, "Breastplate of Azure Reveries", BreastplateOfAzureReveriesItemId, unresolvedProbability, activeItemId, activeProbability);
            AddAzure(values, enemyKey, enemyDisplayName, "Gloves of Azure Reveries", 165306, unresolvedProbability, activeItemId, activeProbability);
            AddAzure(values, enemyKey, enemyDisplayName, "Helmet of Azure Reveries", 165307, unresolvedProbability, activeItemId, activeProbability);
            AddAzure(values, enemyKey, enemyDisplayName, "Pants of Azure Reveries", 165308, unresolvedProbability, activeItemId, activeProbability);
            AddAzure(values, enemyKey, enemyDisplayName, "Sleeves of Azure Reveries", 165303, unresolvedProbability, activeItemId, activeProbability);
        }

        private static void AddAzure(
            ICollection<MercenaryCampDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            string unresolvedProbability,
            int activeItemId,
            string activeProbability)
        {
            if (itemTemplateId == activeItemId)
            {
                AddActive(values, enemyKey, enemyDisplayName, itemName, itemTemplateId, 200, activeProbability, 10000);
                return;
            }

            AddInactive(values, enemyKey, enemyDisplayName, itemName, itemTemplateId, 200, unresolvedProbability);
        }

        private static void AddInactive(
            ICollection<MercenaryCampDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability)
        {
            Add(values, enemyKey, enemyDisplayName, itemName, itemTemplateId, quality, sourceProbability, 0, false);
        }

        private static void AddActive(
            ICollection<MercenaryCampDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability,
            int dropChanceBasisPoints)
        {
            Add(values, enemyKey, enemyDisplayName, itemName, itemTemplateId, quality, sourceProbability, dropChanceBasisPoints, true);
        }

        private static void Add(
            ICollection<MercenaryCampDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability,
            int dropChanceBasisPoints,
            bool isActive)
        {
            values.Add(
                new MercenaryCampDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = itemName,
                    ItemTemplateId = itemTemplateId,
                    Quality = quality,
                    DropChanceBasisPoints = dropChanceBasisPoints,
                    SourceProbability = sourceProbability,
                    IsActive = isActive
                });
        }

        private static string EnemyKeyForDisplayName(string displayName)
        {
            string value = (displayName ?? string.Empty).Trim();
            if (EqualsName(value, "Ian Warr")) return IanWarrKey;
            if (EqualsName(value, "Nelly Johnson")) return NellyJohnsonKey;
            if (EqualsName(value, "Patricia Johnson")) return PatriciaJohnsonKey;
            if (EqualsName(value, "Peter Lee")) return PeterLeeKey;
            if (EqualsName(value, "Ris Lee")) return RisLeeKey;
            return null;
        }

        private static bool EqualsName(string value, string candidate)
        {
            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
