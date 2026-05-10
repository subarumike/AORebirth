namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum CombatCorpseLootClass
    {
        CreditsOnly,
        ItemLoot,
        MajorBoss
    }

    public sealed class CombatLootTableEntry
    {
        public string ExactName { get; set; }

        public int MonsterData { get; set; }

        public int NpcFamily { get; set; }

        public int DropChancePercent { get; set; }

        public int Quality { get; set; }

        public int[] ItemTemplateIds { get; set; }

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
                        ItemTemplateIds = new[] { 0x4545F, 0x4545A }
                    });

                entries.Add(
                    new CombatLootTableEntry
                    {
                        ExactName = archetype.DisplayName,
                        MonsterData = archetype.MonsterData,
                        DropChancePercent = 100,
                        Quality = 1,
                        ItemTemplateIds = new[] { 27350, 85534, 85521, 273496, 273500 }
                    });

                entries.Add(
                    new CombatLootTableEntry
                    {
                        ExactName = archetype.DisplayName,
                        MonsterData = archetype.MonsterData,
                        DropChancePercent = 100,
                        Quality = 1,
                        ItemTemplateIds = new[] { 27350 }
                    });
            }

            return entries.ToArray();
        }
    }

    public static class CombatCorpseRules
    {
        public const int CorpseInventorySlots = 21;

        public const int MoveToInventoryPlacement = 0x6f;

        public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromMilliseconds(750);

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
