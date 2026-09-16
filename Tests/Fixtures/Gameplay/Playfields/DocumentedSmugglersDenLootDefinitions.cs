namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class SmugglersDenDocumentedDropDefinition
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

    internal static class DocumentedSmugglersDenLootDefinitions
    {
        internal const int PlayfieldInstance = 1862;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Smuggler%27s_Den";
        internal const string ExactProbabilitySourceUrl =
            "https://wiki.aodb.us/wiki/Unique_Encounters";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.smugglers-den.";

        internal const string NormalMantisKey = "smugglers-den.1862.enemy.den-mantis";
        internal const string MantisBreederKey = "smugglers-den.1862.enemy.den-mantis-breeder";
        internal const string SmugglerPilotKey = "smugglers-den.1862.named.den-smuggler-pilot";
        internal const string ForefatherKey = "smugglers-den.1862.boss.clawfinger-forefather";
        internal const string MantisQueenKey = "smugglers-den.1862.boss.den-mantis-queen";

        internal const int MantisEggItemId = 157947;

        private static readonly SmugglersDenDocumentedDropDefinition[] Drops =
            BuildDrops();

        internal static SmugglersDenDocumentedDropDefinition[] DocumentedDrops
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

        internal static string EnemyKeyForDisplayName(string displayName)
        {
            string value = (displayName ?? string.Empty).Trim();
            if (EqualsAny(
                value,
                "Den Mantis Digger",
                "Den Mantis Drone",
                "Den Mantis Earthmelder",
                "Den Mantis Forager",
                "Den Mantis Runner",
                "Den Mantis Scout",
                "Den Mantis Burrower",
                "Den Mantis Worker"))
            {
                return NormalMantisKey;
            }

            if (EqualsAny(value, "Den Mantis Breeder")) return MantisBreederKey;
            if (EqualsAny(value, "Den Smuggler Pilot")) return SmugglerPilotKey;
            if (EqualsAny(value, "Clawfinger Forefather", "Forefather")) return ForefatherKey;
            if (EqualsAny(value, "Den Mantis Queen", "Mantis Queen")) return MantisQueenKey;
            return null;
        }

        internal static SmugglersDenDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new SmugglersDenDocumentedDropDefinition[0];
            }

            string enemyKey = EnemyKeyForDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(enemyKey))
            {
                return new SmugglersDenDocumentedDropDefinition[0];
            }

            return Drops
                .Where(
                    value => string.Equals(
                        value.EnemyKey,
                        enemyKey,
                        StringComparison.Ordinal))
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

            SmugglersDenDocumentedDropDefinition[] active =
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
            foreach (SmugglersDenDocumentedDropDefinition drop in active)
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
            SmugglersDenDocumentedDropDefinition drop)
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
                        FixedQuality = drop.MinimumQuality,
                        MinimumQuality = drop.MinimumQuality,
                        MaximumQuality = drop.MaximumQuality,
                        MinimumQuantity = 1,
                        MaximumQuantity = 1,
                        Weight = 1,
                        DropChanceBasisPoints = drop.MinimumDropChanceBasisPoints,
                        UniquePerCorpse = true,
                        Semantics = LootSemantics.WeightedDocumented,
                        Evidence = LootEvidenceConfidence.CommunityDocumented,
                        EvidenceReference = ExactProbabilitySourceUrl,
                        ProbabilityEvidence = "documented-exact:" + drop.SourceProbability
                    }
                },
                Conditions = new string[0]
            };
        }

        private static SmugglersDenDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<SmugglersDenDocumentedDropDefinition>();

            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Abdomen", 164273, 164274, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Femur", 164275, 164276, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Head", 164271, 164272, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Tarsus", 164277, 164278, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Tibia", 164279, 164280, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Wing", 164281, 164282, 75, 140, "membership published; rate not published");
            AddInactiveFixed(values, NormalMantisKey, "Den Mantis", "Mantis Predator Blade", 164431, 120, "membership and maximum QL published; lower template bridge and rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Antennae", 164401, 164402, 75, 140, "membership published; rate not published");
            AddInactiveFixed(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Prothorax", 164432, 100, "fixed QL100 published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Mandibles", 164414, 164415, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Coxa", 164412, 164413, 75, 140, "membership published; rate not published");
            AddInactiveRange(values, NormalMantisKey, "Den Mantis", "Deformed Mantidae Eye", 164416, 164417, 75, 140, "membership published; rate not published");

            AddInactiveFixed(values, MantisBreederKey, "Den Mantis Breeder", "Mantis Scissors", 157760, 185, "membership published; rate not published");
            AddInactiveFixed(values, MantisBreederKey, "Den Mantis Breeder", "Mantis Egg", MantisEggItemId, 190, "membership published; rate not published");

            AddInactiveFixed(values, SmugglerPilotKey, "Den Smuggler Pilot", "Small Titan Message Container", 157222, 100, "membership published; rate not published");
            AddInactiveFixed(values, SmugglerPilotKey, "Den Smuggler Pilot", "FA Super 90 Pannikin", 123853, 159, "linked template membership published; rate and quality range not published");

            AddInactiveFixed(values, ForefatherKey, "Clawfinger Forefather", "Notum Focus", 158914, 1, "usually; not 100%; individual rate not published");
            AddInactiveFixed(values, ForefatherKey, "Clawfinger Forefather", "Spirit Focus", 158915, 1, "usually; not 100%; individual rate not published");
            AddInactiveFixed(values, ForefatherKey, "Clawfinger Forefather", "Fleshchopper", 158912, 1, "membership published; rate not published");
            AddInactiveFixed(values, ForefatherKey, "Clawfinger Forefather", "Toothpicker", 158913, 1, "membership published; rate not published");

            AddInactiveFixed(values, MantisQueenKey, "Den Mantis Queen", "Queen Blade", 157761, 200, "membership published; rate not published");
            AddActiveFixed(values, MantisQueenKey, "Den Mantis Queen", "Mantis Egg", MantisEggItemId, 190, "100%", 10000);
            AddInactiveFixed(values, MantisQueenKey, "Den Mantis Queen", "Peridotite", 287131, 200, "linked template membership published; rate and quality range not published");

            return values.ToArray();
        }

        private static void AddActiveFixed(
            ICollection<SmugglersDenDocumentedDropDefinition> values,
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
                itemTemplateId,
                quality,
                quality,
                dropChanceBasisPoints,
                dropChanceBasisPoints,
                sourceProbability,
                true);
        }

        private static void AddInactiveFixed(
            ICollection<SmugglersDenDocumentedDropDefinition> values,
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
            ICollection<SmugglersDenDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int highItemTemplateId,
            int minimumQuality,
            int maximumQuality,
            string sourceProbability)
        {
            Add(
                values,
                enemyKey,
                enemyDisplayName,
                itemName,
                itemTemplateId,
                highItemTemplateId,
                minimumQuality,
                maximumQuality,
                0,
                0,
                sourceProbability,
                false);
        }

        private static void Add(
            ICollection<SmugglersDenDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int highItemTemplateId,
            int minimumQuality,
            int maximumQuality,
            int minimumBasisPoints,
            int maximumBasisPoints,
            string sourceProbability,
            bool isActive)
        {
            values.Add(
                new SmugglersDenDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = itemName,
                    ItemTemplateId = itemTemplateId,
                    HighItemTemplateId = highItemTemplateId,
                    MinimumQuality = minimumQuality,
                    MaximumQuality = maximumQuality,
                    MinimumDropChanceBasisPoints = minimumBasisPoints,
                    MaximumDropChanceBasisPoints = maximumBasisPoints,
                    SourceProbability = sourceProbability,
                    IsActive = isActive
                });
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            return candidates.Any(
                candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
