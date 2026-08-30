namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class CamelotDocumentedDropDefinition
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

    internal static class DocumentedCamelotLootDefinitions
    {
        internal const int PlayfieldInstance = 120;
        internal const int NanobotInfusionDeviceItemId = 275382;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Camelot";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.camelot.";

        internal const string MorganKey = "camelot.120.boss.morgan-le-faye";
        internal const string GhasapKey = "camelot.120.boss.lord-ghasap";
        internal const string TarasqueKey = "camelot.120.boss.tarasque";
        internal const string DeValosKey = "camelot.120.boss.administrator-devalos";

        private static readonly CamelotDocumentedDropDefinition[] Drops = BuildDrops();

        internal static CamelotDocumentedDropDefinition[] DocumentedDrops
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

        internal static CamelotDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new CamelotDocumentedDropDefinition[0];
            }

            string enemyKey = EnemyKeyForDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(enemyKey))
            {
                return new CamelotDocumentedDropDefinition[0];
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

            CamelotDocumentedDropDefinition[] active =
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
            foreach (CamelotDocumentedDropDefinition drop in active)
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
            CamelotDocumentedDropDefinition drop)
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

        private static CamelotDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<CamelotDocumentedDropDefinition>();

            AddInactive(values, MorganKey, "Morgan Le Faye", "A Well Shaped Tool of Torturing", 157900, 200, "membership published; rate not published");
            AddInactive(values, MorganKey, "Morgan Le Faye", "Extra Curved Gutting Hook", 157903, 200, "membership published; rate not published");

            AddInactive(values, GhasapKey, "Lord Ghasap", "Fork of Ghasap (Lord Version)", 158403, 1, "membership published; phase and rate not published");
            AddInactive(values, GhasapKey, "Lord Ghasap", "Fork of Ghasap", 158321, 1, "membership published; phase and rate not published");
            AddInactive(values, GhasapKey, "Lord Ghasap", "Dreadful Pitchfork", 158298, 1, "membership published; phase and rate not published");
            AddInactive(values, GhasapKey, "Lord Ghasap", "Pattern of Imminent Death", 255553, 200, "membership published; phase and rate not published");
            AddInactive(values, GhasapKey, "Lord Ghasap", "Pattern of Inevitable Death", 255552, 200, "membership published; phase and rate not published");
            AddInactive(values, GhasapKey, "Lord Ghasap", "Corroded Ring", 200818, 100, "membership published; phase and rate not published");

            AddInactive(values, TarasqueKey, "Tarasque", "The Edge of the Tarasque", 157856, 200, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Typical Dragon Tooth Poker", 158842, 199, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Heavy-Headed Hardwood Staff", 159136, 200, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Gaily Painted Hood", 158795, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Smelly Butcher Gloves", 158844, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Aura Magnifier", 158798, 100, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Heavily Padded Overcoat", 158800, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Robust Backpack", 158790, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Smart Hood of the Wanderer", 158789, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Cloak of the Wandering Knight", 158788, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Hollow Bone Bracer of Merlin Ambrosius", 158891, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Signet Ring of the Green Knight", 158801, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Globe of Clarity", 158797, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Globe of Sufferance", 158796, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Heart of Tarasque", 158787, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Sinew of Tarasque", 158764, 1, "likely; not 100%; outcome rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Shard of Living Dragon Skull", 158896, 100, "membership published; rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Chunk of Living Dragon Flesh", 158892, 100, "membership published; rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Living Dragon Claws", 301127, 100, "membership published; rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Piece of Living Dragon Wing", 158894, 100, "membership published; rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Patch of Living Dragon Skin", 158893, 100, "membership published; rate not published");
            AddInactive(values, TarasqueKey, "Tarasque", "Lump of Living Dragon Marrow", 158895, 100, "membership published; rate not published");

            AddActive(values, DeValosKey, "Administrator DeValos", "Nanobot Infusion Device", NanobotInfusionDeviceItemId, 1, "exclusive, 100%", 10000);

            return values.ToArray();
        }

        private static void AddInactive(
            ICollection<CamelotDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability)
        {
            Add(
                values,
                enemyKey,
                enemyDisplayName,
                itemName,
                itemTemplateId,
                quality,
                sourceProbability,
                0,
                false);
        }

        private static void AddActive(
            ICollection<CamelotDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability,
            int dropChanceBasisPoints)
        {
            Add(
                values,
                enemyKey,
                enemyDisplayName,
                itemName,
                itemTemplateId,
                quality,
                sourceProbability,
                dropChanceBasisPoints,
                true);
        }

        private static void Add(
            ICollection<CamelotDocumentedDropDefinition> values,
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
                new CamelotDocumentedDropDefinition
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
            if (EqualsName(value, "Morgan Le Faye")) return MorganKey;
            if (EqualsName(value, "Lord Ghasap")
                || EqualsName(value, "Reborn Lord Ghasap")) return GhasapKey;
            if (EqualsName(value, "Tarasque")) return TarasqueKey;
            if (EqualsName(value, "Administrator DeValos")) return DeValosKey;
            return null;
        }

        private static bool EqualsName(string value, string candidate)
        {
            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
