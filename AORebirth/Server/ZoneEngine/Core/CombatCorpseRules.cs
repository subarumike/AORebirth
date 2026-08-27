namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    public enum CombatCorpseLootClass
    {
        Empty,
        RegularLoot,
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

        public static bool TryGetObservedCreditRange(
            string name,
            int monsterData,
            out int minimumCredits,
            out int maximumCredits)
        {
            ObservedCorpseCreditRule rule = ObservedCreditRules.FirstOrDefault(
                value => value.Matches(name, monsterData));
            if (rule == null)
            {
                minimumCredits = 0;
                maximumCredits = 0;
                return false;
            }

            minimumCredits = rule.MinCredits;
            maximumCredits = rule.MaxCredits;
            return true;
        }

        // Fully emptied corpses despawn immediately after last item/credits leave.
        public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.Zero;

        public static readonly TimeSpan EmptyCorpseLifetime = TimeSpan.Zero;

        // Unlooted corpses despawn after 60 seconds.
        public static readonly TimeSpan RegularLootCorpseLifetime = TimeSpan.FromSeconds(60);

        public static readonly TimeSpan MajorBossCorpseLifetime = TimeSpan.FromMinutes(30);

        public static CombatCorpseLootClass LootClassFor(int unlootedItemCount, int unlootedCredits, bool isMajorBoss)
        {
            if (unlootedItemCount <= 0 && unlootedCredits <= 0)
            {
                return CombatCorpseLootClass.Empty;
            }

            return isMajorBoss
                       ? CombatCorpseLootClass.MajorBoss
                       : CombatCorpseLootClass.RegularLoot;
        }

        public static TimeSpan LifetimeFor(CombatCorpseLootClass lootClass)
        {
            switch (lootClass)
            {
                case CombatCorpseLootClass.MajorBoss:
                    return MajorBossCorpseLifetime;

                case CombatCorpseLootClass.RegularLoot:
                    return RegularLootCorpseLifetime;

                default:
                    return EmptyCorpseLifetime;
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
                { 31114, 31102 },
                { 17649, 15215 },
                // Capture 20260722-cap-mob-drop-cred corpse-full-updates: Waste Collector / Supreme.
                { 17714, 17316 },
                // Capture 20260722-cap-mob-drop-cred: Garbage Flea corpse CATMesh.
                { 17657, 15231 },
                { 30379, 26978 },
                { 203748, 5921 },
                // Capture 20260723-221330: Barking Chimera / Yuttos corpse CATMesh.
                { 209173, 208966 },
                // Capture 20260723-221330: Swift/Dreaming Silvertail corpse CATMesh.
                { 208922, 208937 },
                // Capture 20260822-070136: Hiathlin / Hiathlin Prime corpse CATMesh.
                { 209196, 208982 },
                // Capture 20260822-082554: Papagena corpse CATMesh (CorpseFullUpdate FCE016).
                { 236640, 236637 },
                // Capture 20260823-112044: Papageno Omni corpse CATMesh (CorpseFullUpdate FCE011).
                { 208640, 208356 },
                // Capture 20260823-103458: Nascence Spirit Hunter / Soul Dredge corpse CATMesh.
                { 209215, 214776 },
                // Capture 20260823-103458: Cascading Spirit corpse CATMesh.
                { 217008, 216891 },
                // Capture 20260823-112044: Disease-Ridden Rafter corpse CATMesh (Shadowlands Rafter family).
                { 212186, 210952 },
                // Nascence D1 Coral Rafter / Havaris (MD 212846): same Rafter family corpse mesh.
                // Unmapped MD-as-CATMesh crashes the client (CorpseFullUpdate generic path).
                { 212846, 210952 },
                // Capture 20260823-112044: Tempterus corpse CATMesh.
                { 209189, 208978 },
                // Capture 20260823-112044: Predator Striker corpse CATMesh.
                { 209022, 208940 },
                // Capture 20260825-202932: The Demonic Subjugator corpse CATMesh.
                { 223690, 216837 },
                // Capture 20260823-112044: Crippler of Growth corpse CATMesh.
                { 209333, 209275 },
                // Nascence D1 Crippler of Destiny — same Crippler family corpse mesh.
                { 209340, 209275 },
                // Capture 20260823-171238: Wailing Spirit corpse CATMesh (513B ExtTex tail).
                { 217022, 214925 },
                // Capture 20260823-171238: Croaker of Desolation/Solitude corpse CATMesh.
                { 209319, 209264 },
                { 209326, 209264 },
                // Capture 20260823-171238: Smelly Weaver corpse CATMesh (460B Material tail).
                { 209347, 209288 },
                // Capture 20260823-182854: Nascence Dungeon 2 corpse CATMesh.
                { 209082, 208950 }, // Bound Dryad
                { 209458, 209423 }, // Infernal Vortexoid
                { 209252, 209046 }, // Malah-Fama
                // Capture 20260826-051307: Malah-Ana / Spinetooth Hatchling outdoor corpse CATMesh.
                { 209229, 209046 }, // Malah-Ana
                { 226557, 302574 }, // Spinetooth Hatchling
                { 209354, 209288 }, // Weaver of Malice
                { 209136, 208955 }, // Burning Shadow
                { 209125, 208955 }, // Icy Shadow
                // L7 gold 20260725-002423 / Find Person 20260725-185432 mission trash corpses.
                { 26159, 17909 },
                { 26139, 5914 },
                { 26155, 23370 },
                { 26137, 5934 },
                { 26076, 17530 },
                { 26101, 23366 },
                { 26088, 17534 },
                { 26103, 23366 },
                { 26135, 5934 },
                { 26074, 23366 },
                { 26090, 5934 },
                { 26092, 17530 },
                { 26097, 23366 },
                { 26123, 17530 }
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

            // Never use living MonsterData as CATMesh — crashes the current client renderer
            // (see CorpseFullUpdate). Caller falls back to a known-safe mesh.
            return 0;
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
