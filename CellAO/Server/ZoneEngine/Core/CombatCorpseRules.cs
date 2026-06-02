namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using CellAO.Database.Dao;
    using CellAO.Database.Entities;

    public enum CombatCorpseLootClass
    {
        CreditsOnly,
        ItemLoot,
        MajorBoss
    }

    public sealed class CombatLootTableEntry
    {
        public string ExactName { get; set; }

        public string MobTemplateHash { get; set; }

        public int MonsterData { get; set; }

        public int NpcFamily { get; set; }

        public int Slot { get; set; }

        public int DropChancePercent { get; set; }

        public int DropChanceBasisPoints { get; set; }

        public int Quality { get; set; }

        public int[] ItemTemplateIds { get; set; }

        public CombatLootItemTemplate[] ItemTemplates { get; set; }

        public int EffectiveDropChanceBasisPoints
        {
            get
            {
                if (this.DropChanceBasisPoints > 0)
                {
                    return this.DropChanceBasisPoints;
                }

                return this.DropChancePercent * 100;
            }
        }

        public bool Matches(string targetName, int monsterData, int npcFamily)
        {
            if (!string.IsNullOrEmpty(this.ExactName)
                && !string.Equals(targetName, this.ExactName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (this.MonsterData != 0 && monsterData != this.MonsterData)
            {
                return false;
            }

            if (this.NpcFamily != 0 && npcFamily != this.NpcFamily)
            {
                return false;
            }

            return true;
        }
    }

    public sealed class CombatLootItemTemplate
    {
        public int LowId { get; set; }

        public int HighId { get; set; }

        public int MinQuality { get; set; }

        public int MaxQuality { get; set; }

        public int RangeCheck { get; set; }

        public string DropGroupHash { get; set; }
    }

    public static class CombatTestLootCatalog
    {
        public static CombatLootTableEntry[] BuildEntries()
        {
            var entries = new List<CombatLootTableEntry>();
            foreach (CombatTestMobArchetype.Entry archetype in CombatTestMobArchetype.All)
            {
                entries.Add(
                    new CombatLootTableEntry
                    {
                        ExactName = archetype.DisplayName,
                        MonsterData = archetype.MonsterData,
                        DropChancePercent = 100,
                        Quality = 1,
                        ItemTemplateIds = new[] { 27350 }
                    });

                entries.Add(
                    new CombatLootTableEntry
                    {
                        ExactName = archetype.DisplayName,
                        MonsterData = archetype.MonsterData,
                        DropChancePercent = 100,
                        Quality = 1,
                        ItemTemplateIds = new[] { 27351, 85534, 85521, 273496, 273500 }
                    });

                entries.Add(
                    new CombatLootTableEntry
                    {
                        ExactName = archetype.DisplayName,
                        MonsterData = archetype.MonsterData,
                        DropChancePercent = 100,
                        Quality = 1,
                        ItemTemplateIds = new[] { 27352 }
                    });
            }

            return entries.ToArray();
        }
    }

    public static class CombatMobLootCatalog
    {
        public static CombatLootTableEntry[] BuildEntries(
            IEnumerable<DBMobTemplate> mobTemplates,
            IEnumerable<DBMobDroptable> dropTable)
        {
            if (mobTemplates == null || dropTable == null)
            {
                return new CombatLootTableEntry[0];
            }

            Dictionary<string, List<DBMobDroptable>> dropsByHash =
                dropTable
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Hash))
                    .GroupBy(x => x.Hash.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            var entries = new List<CombatLootTableEntry>();
            foreach (DBMobTemplate template in mobTemplates.Where(HasDropHashes))
            {
                string[] hashExpressions = SplitLootField(template.DropHashes, ',');
                string[] slotValues = SplitLootField(template.DropSlots, ',');
                string[] rateValues = SplitLootField(template.DropRates, ',');

                for (int i = 0; i < hashExpressions.Length; i++)
                {
                    CombatLootItemTemplate[] itemTemplates =
                        ExpandDropHashExpression(hashExpressions[i], dropsByHash).ToArray();

                    if (itemTemplates.Length == 0)
                    {
                        continue;
                    }

                    int basisPoints = ParseDropRateBasisPoints(rateValues, i);
                    entries.Add(
                        new CombatLootTableEntry
                        {
                            ExactName = template.Name,
                            MobTemplateHash = template.Hash,
                            MonsterData = template.MonsterData,
                            NpcFamily = template.NPCFamily,
                            Slot = ParseIntAt(slotValues, i, i),
                            DropChanceBasisPoints = basisPoints,
                            DropChancePercent = basisPoints / 100,
                            ItemTemplates = itemTemplates
                        });
                }
            }

            return entries.ToArray();
        }

        private static bool HasDropHashes(DBMobTemplate template)
        {
            return template != null && !string.IsNullOrWhiteSpace(template.DropHashes);
        }

        private static IEnumerable<CombatLootItemTemplate> ExpandDropHashExpression(
            string expression,
            IDictionary<string, List<DBMobDroptable>> dropsByHash)
        {
            foreach (string dropHash in SplitLootField(expression, '+'))
            {
                List<DBMobDroptable> rows;
                if (!dropsByHash.TryGetValue(dropHash, out rows))
                {
                    continue;
                }

                foreach (DBMobDroptable row in rows)
                {
                    yield return new CombatLootItemTemplate
                    {
                        LowId = row.LowId,
                        HighId = row.HighId,
                        MinQuality = row.MinQl,
                        MaxQuality = row.MaxQl,
                        RangeCheck = row.RangeCheck,
                        DropGroupHash = row.Hash
                    };
                }
            }
        }

        private static string[] SplitLootField(string value, char separator)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new string[0];
            }

            return value
                .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();
        }

        private static int ParseDropRateBasisPoints(string[] values, int index)
        {
            int result = ParseIntAt(values, index, 10000);
            if (result < 0)
            {
                return 0;
            }

            return result > 10000 ? 10000 : result;
        }

        private static int ParseIntAt(string[] values, int index, int defaultValue)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return defaultValue;
            }

            int result;
            return int.TryParse(values[index], out result) ? result : defaultValue;
        }
    }

    public static class CombatCorpseRules
    {
        private static readonly ObservedCorpseCreditRule[] ObservedCreditRules =
        {
            new ObservedCorpseCreditRule("Beach Leet", 17655, 1, 1),
            new ObservedCorpseCreditRule("Island Reet", 30365, 5, 5),
            new ObservedCorpseCreditRule("Shore Snake", 30252, 5, 5),
            new ObservedCorpseCreditRule("Surf Lizard", 22794, 1, 1),
            new ObservedCorpseCreditRule("Cliff Malle", 17660, 3, 3),
            new ObservedCorpseCreditRule("Reef Salamander", 30354, 23, 29)
        };

        public const int CorpseInventorySlots = 21;

        public const int MoveToInventoryPlacement = 0x6f;

        public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(3);

        public static readonly TimeSpan CreditsOnlyCorpseLifetime = TimeSpan.FromSeconds(30);

        public static readonly TimeSpan ItemLootCorpseLifetime = TimeSpan.FromSeconds(60);

        public static readonly TimeSpan MajorBossCorpseLifetime = TimeSpan.FromMinutes(30);

        public static CombatCorpseLootClass LootClassFor(int unlootedItemCount, bool isMajorBoss)
        {
            if (isMajorBoss)
            {
                return CombatCorpseLootClass.MajorBoss;
            }

            return unlootedItemCount > 0
                       ? CombatCorpseLootClass.ItemLoot
                       : CombatCorpseLootClass.CreditsOnly;
        }

        public static TimeSpan LifetimeFor(CombatCorpseLootClass lootClass)
        {
            switch (lootClass)
            {
                case CombatCorpseLootClass.MajorBoss:
                    return MajorBossCorpseLifetime;

                case CombatCorpseLootClass.ItemLoot:
                    return ItemLootCorpseLifetime;

                default:
                    return CreditsOnlyCorpseLifetime;
            }
        }

        public static bool ShouldDrop(int dropChancePercent, Func<int, int> nextRandom)
        {
            if (dropChancePercent <= 0)
            {
                return false;
            }

            if (dropChancePercent >= 100)
            {
                return true;
            }

            if (nextRandom == null)
            {
                throw new ArgumentNullException("nextRandom");
            }

            return nextRandom(100) < dropChancePercent;
        }

        public static bool ShouldDropBasisPoints(int dropChanceBasisPoints, Func<int, int> nextRandom)
        {
            if (dropChanceBasisPoints <= 0)
            {
                return false;
            }

            if (dropChanceBasisPoints >= 10000)
            {
                return true;
            }

            if (nextRandom == null)
            {
                throw new ArgumentNullException("nextRandom");
            }

            return nextRandom(10000) < dropChanceBasisPoints;
        }

        public static T FindLootItem<T>(
            IEnumerable<T> lootItems,
            int requestedLootSlot,
            Func<T, int> slotSelector,
            Func<T, bool> lootedSelector) where T : class
        {
            if (lootItems == null)
            {
                return null;
            }

            List<T> remaining = lootItems.Where(x => !lootedSelector(x)).ToList();

            T exactMatch = remaining.FirstOrDefault(x => slotSelector(x) == requestedLootSlot);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            T oneBasedMatch = remaining.FirstOrDefault(x => slotSelector(x) + 1 == requestedLootSlot);
            if (oneBasedMatch != null)
            {
                return oneBasedMatch;
            }

            if (remaining.Count == 1 && requestedLootSlot <= 1)
            {
                return remaining[0];
            }

            return null;
        }

        public static short InventoryEntryCountFor(int multipleCount)
        {
            if (multipleCount <= 0 || multipleCount == 1234567890)
            {
                return 1;
            }

            return multipleCount > short.MaxValue ? short.MaxValue : (short)multipleCount;
        }

        public static int RollObservedCredits(string targetName, int monsterData, Func<int, int> nextRandom)
        {
            ObservedCorpseCreditRule rule = ObservedCreditRules.FirstOrDefault(
                x => x.Matches(targetName, monsterData));
            if (rule == null)
            {
                return 0;
            }

            if (rule.MaxCredits <= rule.MinCredits)
            {
                return rule.MinCredits;
            }

            if (nextRandom == null)
            {
                throw new ArgumentNullException("nextRandom");
            }

            return rule.MinCredits + nextRandom(rule.MaxCredits - rule.MinCredits + 1);
        }

        private sealed class ObservedCorpseCreditRule
        {
            public ObservedCorpseCreditRule(string name, int monsterData, int minCredits, int maxCredits)
            {
                this.Name = name;
                this.MonsterData = monsterData;
                this.MinCredits = minCredits;
                this.MaxCredits = maxCredits;
            }

            public string Name { get; private set; }

            public int MonsterData { get; private set; }

            public int MinCredits { get; private set; }

            public int MaxCredits { get; private set; }

            public bool Matches(string targetName, int monsterData)
            {
                if (monsterData != 0 && this.MonsterData == monsterData)
                {
                    return true;
                }

                return string.Equals(
                    NormalizeName(targetName),
                    this.Name,
                    StringComparison.OrdinalIgnoreCase);
            }

            private static string NormalizeName(string targetName)
            {
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    return string.Empty;
                }

                const string codexPrefix = "Codex Test ";
                string normalized = targetName.Trim();
                return normalized.StartsWith(codexPrefix, StringComparison.OrdinalIgnoreCase)
                           ? normalized.Substring(codexPrefix.Length)
                           : normalized;
            }
        }
    }

    public static class CombatCorpseVisuals
    {
        public static Dictionary<int, int> BuildMonsterDataToCorpseCatMeshMap()
        {
            var map = new Dictionary<int, int>
            {
                { 247831, 247826 },
                { 247832, 247821 },
                { 31114, 31102 }
            };

            foreach (KeyValuePair<int, int> mapping in CombatTestMobArchetype.CorpseVisualMappings())
            {
                map[mapping.Key] = mapping.Value;
            }

            return map;
        }

        public static int CorpseCatMeshFor(int catMesh, int monsterData, IDictionary<int, int> monsterDataToCorpseCatMesh)
        {
            if (IsUsableVisualId(catMesh))
            {
                return catMesh;
            }

            int mappedCatMesh;
            if (monsterDataToCorpseCatMesh != null
                && monsterDataToCorpseCatMesh.TryGetValue(monsterData, out mappedCatMesh))
            {
                return mappedCatMesh;
            }

            return monsterData;
        }

        public static int CorpseMonsterDataFor(int monsterData, int corpseCatMesh)
        {
            return IsUsableVisualId(monsterData) ? monsterData : corpseCatMesh;
        }

        public static int DeathAnimationKeyFor(int corpseAnimationKey, int itemAnimation, int defaultAnimationKey)
        {
            if (IsUsableVisualId(corpseAnimationKey))
            {
                return corpseAnimationKey;
            }

            if (IsUsableVisualId(itemAnimation))
            {
                return itemAnimation;
            }

            return defaultAnimationKey;
        }

        public static bool IsUsableVisualId(int value)
        {
            return value > 0 && value != 1234567890;
        }
    }
}
