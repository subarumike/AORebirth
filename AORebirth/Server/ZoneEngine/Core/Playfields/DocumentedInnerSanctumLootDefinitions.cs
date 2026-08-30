namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class InnerSanctumDocumentedDropDefinition
    {
        internal string BossKey { get; set; }
        internal string BossDisplayName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int MinimumDropChanceBasisPoints { get; set; }
        internal int MaximumDropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
        internal bool IsActive { get; set; }
    }

    internal static class DocumentedInnerSanctumLootDefinitions
    {
        internal const int PlayfieldInstance = 1943;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Inner_Sanctum";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.inner-sanctum.";

        internal const string ReIncarnatorBossKey =
            "inner-sanctum.1943.boss.re-incarnator";
        internal const string JeuruBossKey =
            "inner-sanctum.1943.boss.jeuru-the-defiler";
        internal const string IskopBossKey =
            "inner-sanctum.1943.boss.iskop-the-idolator";
        internal const string UmmohBossKey =
            "inner-sanctum.1943.boss.dominus-ummoh";
        internal const string FacutBossKey =
            "inner-sanctum.1943.boss.dominus-facut";
        internal const string JiannuBossKey =
            "inner-sanctum.1943.boss.dominus-jiannu";
        internal const string InobakBossKey =
            "inner-sanctum.1943.boss.inobak-the-gelid";
        internal const string HezakBossKey =
            "inner-sanctum.1943.boss.hezak-the-immortal";

        private static readonly InnerSanctumDocumentedDropDefinition[] Drops =
            BuildDrops();

        internal static InnerSanctumDocumentedDropDefinition[] DocumentedDrops
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

        internal static string BossKeyForDisplayName(string displayName)
        {
            string value = (displayName ?? string.Empty).Trim();
            if (EqualsAny(value, "The Re-Incarnator", "The Re-Animator"))
            {
                return ReIncarnatorBossKey;
            }
            if (EqualsAny(value, "Jeuru the Defiler"))
            {
                return JeuruBossKey;
            }
            if (EqualsAny(value, "Iskop the Idolator"))
            {
                return IskopBossKey;
            }
            if (StartsWith(value, "Dominus Ummoh"))
            {
                return UmmohBossKey;
            }
            if (StartsWith(value, "Dominus Facut"))
            {
                return FacutBossKey;
            }
            if (StartsWith(value, "Dominus Jiannu"))
            {
                return JiannuBossKey;
            }
            if (EqualsAny(value, "Inobak the Gelid"))
            {
                return InobakBossKey;
            }
            if (EqualsAny(value, "Hezak the Immortal"))
            {
                return HezakBossKey;
            }

            return null;
        }

        internal static InnerSanctumDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new InnerSanctumDocumentedDropDefinition[0];
            }

            string bossKey = BossKeyForDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(bossKey))
            {
                return new InnerSanctumDocumentedDropDefinition[0];
            }

            return Drops
                .Where(value => string.Equals(value.BossKey, bossKey, StringComparison.Ordinal))
                .ToArray();
        }

        internal static bool ApplyDocumentedBossLoot(
            LootTableDefinition table,
            int playfieldId,
            string displayName)
        {
            if (table == null)
            {
                return false;
            }

            InnerSanctumDocumentedDropDefinition[] active =
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
                    .SelectMany(
                        value => new[]
                        {
                            value.ItemTemplateId,
                            value.HighItemTemplateId
                        })
                    .Where(value => value > 0));
            var groupKeys = new HashSet<string>(
                existing
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (InnerSanctumDocumentedDropDefinition drop in active)
            {
                string groupKey = DocumentedLootGroupPrefix
                                  + drop.BossKey
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

            table.RollGroups = existing.Concat(additions).ToArray();
            table.AllowsDocumentedSupplement = true;
            return true;
        }

        private static LootGroupDefinition DocumentedGroup(
            string groupKey,
            InnerSanctumDocumentedDropDefinition drop)
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
                        ProbabilityEvidence = "documented-lower-bound:" + drop.SourceProbability
                    }
                },
                Conditions = new string[0]
            };
        }

        private static InnerSanctumDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<InnerSanctumDocumentedDropDefinition>();

            Add(values, ReIncarnatorBossKey, "The Re-Incarnator", 204829, 10000, 10000, "100%", true, 390);
            Add(values, ReIncarnatorBossKey, "The Re-Incarnator", 206253, 10000, 10000, "100%", true);
            AddUnresolved(values, ReIncarnatorBossKey, "The Re-Incarnator", "uncommon",
                206237, 206238, 206239, 206055, 206056, 206057, 206061, 206047, 206049, 206053);

            Add(values, JeuruBossKey, "Jeuru the Defiler", 206049, 3000, 3500, "30-35%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206056, 1500, 2000, "15-20%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206061, 2500, 3000, "25-30%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206053, 2000, 2500, "20-25%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206192, 900, 1000, "9-10%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206248, 600, 700, "6-7%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206064, 10000, 10000, "100%", true);
            AddRange(values, JeuruBossKey, "Jeuru the Defiler", 300, 400, "3-4%", true,
                205957, 205958, 205960, 206196, 206204);
            AddRange(values, JeuruBossKey, "Jeuru the Defiler", 700, 800, "7-8%", true,
                206232, 206235, 206236);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206257, 10000, 10000, "100%", true);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206477, 600, 700, "6-7%", true, 140);
            Add(values, JeuruBossKey, "Jeuru the Defiler", 206253, 1500, 2000, "15-20%", true);

            Add(values, IskopBossKey, "Iskop the Idolator", 206049, 1200, 1300, "12-13%", true);
            Add(values, IskopBossKey, "Iskop the Idolator", 206056, 2000, 2500, "20-25%", true);
            AddRange(values, IskopBossKey, "Iskop the Idolator", 3000, 3500, "30-35%", true,
                206061, 206053);
            Add(values, IskopBossKey, "Iskop the Idolator", 206192, 900, 1000, "9-10%", true);
            Add(values, IskopBossKey, "Iskop the Idolator", 206248, 2500, 3000, "25-30%", true);
            AddRange(values, IskopBossKey, "Iskop the Idolator", 600, 700, "6-7%", true,
                206136, 205961, 205959, 205956, 206196);
            Add(values, IskopBossKey, "Iskop the Idolator", 206204, 4000, 4500, "40-45%", true);
            Add(values, IskopBossKey, "Iskop the Idolator", 206063, 0, 399, "<4% if it drops at all", false);
            AddRange(values, IskopBossKey, "Iskop the Idolator", 900, 1000, "9-10%", true,
                206232, 206235, 206236, 206253);
            Add(values, IskopBossKey, "Iskop the Idolator", 206258, 10000, 10000, "100%", true);
            Add(values, IskopBossKey, "Iskop the Idolator", 206477, 2500, 3000, "25-30%", true, 140);

            AddRange(values, UmmohBossKey, "Dominus Ummoh", 1000, 1100, "10-11%", true,
                206047, 206013, 206015);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 1800, 2000, "18-20%", true,
                206055, 206008);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 300, 400, "3-4%", true,
                206059, 206011, 206201, 206203);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206060, 700, 800, "7-8%", true);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 2000, 2500, "20-25%", true,
                206018, 206192, 206254, 206017);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206136, 1400, 1500, "14-15%", true);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 800, 900, "8-9%", true,
                205961, 205959, 205956, 206196);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 500, 600, "5-6%", true,
                205950, 205953, 205955);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206202, 1400, 1500, "14-15%", true);
            AddRange(values, UmmohBossKey, "Dominus Ummoh", 1300, 1400, "13-14%", true,
                206237, 206238, 206239);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206246, 0, 399, "<4% if it drops at all", false);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206242, 700, 800, "7-8%", true);
            Add(values, UmmohBossKey, "Dominus Ummoh", 206068, 100, 200, "1-2%", true);

            AddUnresolved(values, FacutBossKey, "Dominus Facut", "rate not published",
                206059, 206060, 206018, 206192, 206013, 206015, 206008, 206011,
                206196, 205951, 205954, 205952, 206203, 206242, 206254);

            AddRange(values, JiannuBossKey, "Dominus Jiannu", 800, 900, "8-9%", true,
                206047, 206055, 205961, 205959, 205956, 206196);
            AddRange(values, JiannuBossKey, "Dominus Jiannu", 900, 1000, "9-10%", true,
                206192, 206155, 206156);
            AddRange(values, JiannuBossKey, "Dominus Jiannu", 400, 500, "4-5%", true,
                206013, 206008, 206011, 206136, 206202, 206242);
            Add(values, JiannuBossKey, "Dominus Jiannu", 206015, 1000, 1100, "10-11%", true);
            AddRange(values, JiannuBossKey, "Dominus Jiannu", 200, 300, "2-3%", true,
                205950, 205953, 205955);
            Add(values, JiannuBossKey, "Dominus Jiannu", 206201, 1100, 1200, "11-12%", true);
            Add(values, JiannuBossKey, "Dominus Jiannu", 206203, 700, 800, "7-8%", true);
            AddRange(values, JiannuBossKey, "Dominus Jiannu", 300, 400, "3-4%", true,
                206237, 206238, 206239, 206254);
            Add(values, JiannuBossKey, "Dominus Jiannu", 287145, 10000, 10000, "100%", true, 200);

            Add(values, InobakBossKey, "Inobak the Gelid", 206047, 5000, 5500, "50-55%", true);
            Add(values, InobakBossKey, "Inobak the Gelid", 206055, 4500, 5000, "45-50%", true);
            Add(values, InobakBossKey, "Inobak the Gelid", 206057, 10000, 10000, "100%", true);
            Add(values, InobakBossKey, "Inobak the Gelid", 206136, 300, 400, "3-4%", true);
            Add(values, InobakBossKey, "Inobak the Gelid", 206155, 100, 200, "1-2%", true);
            Add(values, InobakBossKey, "Inobak the Gelid", 206156, 600, 700, "6-7%", true);
            AddRange(values, InobakBossKey, "Inobak the Gelid", 300, 400, "3-4%", true,
                205957, 205958, 205960, 206196, 206203);
            AddRange(values, InobakBossKey, "Inobak the Gelid", 500, 600, "5-6%", true,
                205951, 205954, 205952, 206017);
            AddRange(values, InobakBossKey, "Inobak the Gelid", 100, 200, "1-2%", true,
                206201, 206247, 206068);
            AddRange(values, InobakBossKey, "Inobak the Gelid", 200, 300, "2-3%", true,
                206237, 206238, 206239, 206246);
            Add(values, InobakBossKey, "Inobak the Gelid", 287145, 10000, 10000, "100%", true, 200);

            AddUnresolved(values, HezakBossKey, "Hezak the Immortal", "rate not published",
                206058, 206052, 206062, 206054, 206016, 206018, 206196, 206136,
                206156, 206237, 206238, 206239, 206246, 206068, 206017, 206067);
            Add(values, HezakBossKey, "Hezak the Immortal", 255550, 0, 0, "rate not published", false, 200);
            Add(values, HezakBossKey, "Hezak the Immortal", 255551, 0, 0, "rate not published", false, 200);

            return values.ToArray();
        }

        private static void AddRange(
            ICollection<InnerSanctumDocumentedDropDefinition> values,
            string bossKey,
            string bossDisplayName,
            int minimumBasisPoints,
            int maximumBasisPoints,
            string sourceProbability,
            bool isActive,
            params int[] itemTemplateIds)
        {
            foreach (int itemTemplateId in itemTemplateIds)
            {
                Add(
                    values,
                    bossKey,
                    bossDisplayName,
                    itemTemplateId,
                    minimumBasisPoints,
                    maximumBasisPoints,
                    sourceProbability,
                    isActive);
            }
        }

        private static void AddUnresolved(
            ICollection<InnerSanctumDocumentedDropDefinition> values,
            string bossKey,
            string bossDisplayName,
            string sourceProbability,
            params int[] itemTemplateIds)
        {
            AddRange(
                values,
                bossKey,
                bossDisplayName,
                0,
                0,
                sourceProbability,
                false,
                itemTemplateIds);
        }

        private static void Add(
            ICollection<InnerSanctumDocumentedDropDefinition> values,
            string bossKey,
            string bossDisplayName,
            int itemTemplateId,
            int minimumBasisPoints,
            int maximumBasisPoints,
            string sourceProbability,
            bool isActive,
            int quality = 1)
        {
            values.Add(
                new InnerSanctumDocumentedDropDefinition
                {
                    BossKey = bossKey,
                    BossDisplayName = bossDisplayName,
                    ItemTemplateId = itemTemplateId,
                    Quality = quality,
                    MinimumDropChanceBasisPoints = minimumBasisPoints,
                    MaximumDropChanceBasisPoints = maximumBasisPoints,
                    SourceProbability = sourceProbability,
                    IsActive = isActive
                });
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            return candidates.Any(
                candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
