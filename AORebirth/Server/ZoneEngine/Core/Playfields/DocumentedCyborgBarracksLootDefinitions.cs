namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class CyborgBarracksDocumentedDropDefinition
    {
        internal string EnemyKey { get; set; }
        internal string EnemyDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int HighItemTemplateId { get; set; }
        internal int MinimumQuality { get; set; }
        internal int MaximumQuality { get; set; }
        internal int MinimumDropChanceBasisPoints { get; set; }
        internal int MaximumDropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
        internal bool IsActive { get; set; }
    }

    internal static class DocumentedCyborgBarracksLootDefinitions
    {
        internal const int PlayfieldInstance = 1833;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Cyborg_Barracks";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.cyborg-barracks.";

        internal const string CyborgKey = "cyborg-barracks.1833.enemy.cyborg";
        internal const string AugmentedHellfuryKey =
            "cyborg-barracks.1833.enemy.augmented-cyborg-hellfury";
        internal const string DeimosKey = "cyborg-barracks.1833.boss.eradicator-deimos";
        internal const string SeverusKey = "cyborg-barracks.1833.boss.general-severus";
        internal const string JocastaKey = "cyborg-barracks.1833.boss.commander-jocasta";
        internal const string InfernoKey = "cyborg-barracks.1833.boss.prototype-inferno";

        private static readonly CyborgBarracksDocumentedDropDefinition[] Drops =
            BuildDrops();

        internal static CyborgBarracksDocumentedDropDefinition[] DocumentedDrops
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

        internal static CyborgBarracksDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new CyborgBarracksDocumentedDropDefinition[0];
            }

            string value = (displayName ?? string.Empty).Trim();
            var enemyKeys = new HashSet<string>(StringComparer.Ordinal);
            if (value.IndexOf("Cyborg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enemyKeys.Add(CyborgKey);
            }

            if (EqualsName(value, "Augmented Cyborg Hellfury")) enemyKeys.Add(AugmentedHellfuryKey);
            if (EqualsName(value, "Eradicator Deimos")) enemyKeys.Add(DeimosKey);
            if (EqualsName(value, "General Severus")) enemyKeys.Add(SeverusKey);
            if (EqualsName(value, "Commander Jocasta")) enemyKeys.Add(JocastaKey);
            if (EqualsName(value, "Prototype Inferno")) enemyKeys.Add(InfernoKey);
            if (enemyKeys.Count == 0)
            {
                return new CyborgBarracksDocumentedDropDefinition[0];
            }

            return Drops
                .Where(valueDrop => enemyKeys.Contains(valueDrop.EnemyKey))
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

            CyborgBarracksDocumentedDropDefinition[] active =
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
                existing
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (CyborgBarracksDocumentedDropDefinition drop in active)
            {
                string groupKey = DocumentedLootGroupPrefix
                                  + drop.EnemyKey
                                  + "."
                                  + drop.ItemTemplateId;
                if (existingItemIds.Contains(drop.ItemTemplateId)
                    || existingItemIds.Contains(drop.HighItemTemplateId)
                    || groupKeys.Contains(groupKey))
                {
                    continue;
                }

                additions.Add(DocumentedIndependentGroup(groupKey, drop));
                existingItemIds.Add(drop.ItemTemplateId);
                existingItemIds.Add(drop.HighItemTemplateId);
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

        private static IEnumerable<int> ItemIds(LootEntryDefinition value)
        {
            return new[] { value.ItemTemplateId, value.HighItemTemplateId };
        }

        private static LootGroupDefinition DocumentedIndependentGroup(
            string groupKey,
            CyborgBarracksDocumentedDropDefinition drop)
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
                        HighItemTemplateId = drop.HighItemTemplateId,
                        FixedQuality = drop.MinimumQuality == drop.MaximumQuality
                            ? drop.MinimumQuality
                            : 0,
                        MinimumQuality = drop.MinimumQuality,
                        MaximumQuality = drop.MaximumQuality,
                        MinimumQuantity = 1,
                        MaximumQuantity = 1,
                        Weight = 1,
                        DropChanceBasisPoints = drop.MinimumDropChanceBasisPoints,
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

        private static CyborgBarracksDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<CyborgBarracksDocumentedDropDefinition>();

            AddInactiveRange(values, CyborgKey, "Cyborg", "Library of Foul Language", 165110, 165111, 1, 200, "perhaps over 10%; no exact numeric rate published");
            AddInactiveFixed(values, AugmentedHellfuryKey, "Augmented Cyborg Hellfury", "Hellfury Assault Cannon", 153977, 80, "rare; no numeric rate published");

            AddInactiveFixed(values, DeimosKey, "Eradicator Deimos", "Bastion of Deimos", 153982, 78, "membership published; rate not published");
            AddInactiveFixed(values, DeimosKey, "Eradicator Deimos", "Deimos' Bio-Enhanced Feedback Rifle", 153981, 78, "membership published; rate not published");

            AddInactiveFixed(values, SeverusKey, "General Severus", "Severus' Fusion Sprayer", 153979, 83, "membership published; rate not published");
            AddInactiveFixed(values, SeverusKey, "General Severus", "Severus' Void Spinner", 153980, 83, "membership published; rate not published");

            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Immortal Katana", 154505, 200, "membership published; rate not published");
            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Bonehammer", 153975, 90, "membership published; rate not published");
            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Internal Anti-Matter Powerplant", 154408, 85, "membership published; rate not published");
            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Augmented Cyborg Arm Armor", 154405, 77, "membership published; rate not published");
            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Sub-Dermal Vengeance Screen (Right Wrist)", 154407, 85, "membership published; rate not published");
            AddInactiveFixed(values, JocastaKey, "Commander Jocasta", "Sub-Dermal Vengeance Screen (Left Wrist)", 154406, 85, "membership published; rate not published");

            AddInactiveFixed(values, InfernoKey, "Prototype Inferno", "Hellspinner Shock Cannon", 153976, 84, "rare; no numeric rate published");

            return values.ToArray();
        }

        private static void AddInactiveFixed(
            ICollection<CyborgBarracksDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability)
        {
            AddInactiveRange(
                values,
                enemyKey,
                enemyDisplayName,
                itemName,
                itemTemplateId,
                itemTemplateId,
                quality,
                quality,
                sourceProbability);
        }

        private static void AddInactiveRange(
            ICollection<CyborgBarracksDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int highItemTemplateId,
            int minimumQuality,
            int maximumQuality,
            string sourceProbability)
        {
            values.Add(
                new CyborgBarracksDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = itemName,
                    ItemTemplateId = itemTemplateId,
                    HighItemTemplateId = highItemTemplateId,
                    MinimumQuality = minimumQuality,
                    MaximumQuality = maximumQuality,
                    MinimumDropChanceBasisPoints = 0,
                    MaximumDropChanceBasisPoints = 0,
                    SourceProbability = sourceProbability,
                    IsActive = false
                });
        }

        private static bool EqualsName(string value, string candidate)
        {
            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
