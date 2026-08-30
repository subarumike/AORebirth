namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class StepsOfMadnessDocumentedDropDefinition
    {
        internal string EnemyKey { get; set; }
        internal string EnemyDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int MinimumDropChanceBasisPoints { get; set; }
        internal int MaximumDropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
        internal bool AppliesToEveryEnemy { get; set; }
        internal bool IsActive { get; set; }
    }

    internal static class DocumentedStepsOfMadnessLootDefinitions
    {
        internal const int PlayfieldInstance = 1933;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Neleb_the_Deranged";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.steps-of-madness.";

        internal const string EveryEnemyKey = "steps-of-madness.1933.all-enemies";
        internal const string UnrelentingFearKey = "steps-of-madness.1933.named.unrelenting-fear";
        internal const string ChildhoodHorrorKey = "steps-of-madness.1933.enemy.childhood-horror";
        internal const string FoundationOfSanityKey = "steps-of-madness.1933.enemy.foundation-of-sanity";
        internal const string FigmentOfImaginationKey = "steps-of-madness.1933.enemy.figment-of-imagination";
        internal const string FragmentOfSanityKey = "steps-of-madness.1933.enemy.fragment-of-sanity";
        internal const string RovingEyeKey = "steps-of-madness.1933.enemy.roving-eye";
        internal const string DetachedPsycheKey = "steps-of-madness.1933.enemy.detached-psyche";
        internal const string PulsingHatredKey = "steps-of-madness.1933.named.pulsing-hatred";
        internal const string SuppressedEmotionKey = "steps-of-madness.1933.named.suppressed-emotion";
        internal const string JealousyKey = "steps-of-madness.1933.named.jealousy";
        internal const string NotumHabitKey = "steps-of-madness.1933.named.notum-habit";
        internal const string MindShardKey = "steps-of-madness.1933.named.mind-shard";
        internal const string SanitysEdgeKey = "steps-of-madness.1933.named.sanitys-edge";
        internal const string ThiefOfReasonKey = "steps-of-madness.1933.named.thief-of-reason";
        internal const string BetrayerOfMemoryKey = "steps-of-madness.1933.named.betrayer-of-memory";
        internal const string NelebKey = "steps-of-madness.1933.boss.neleb-the-deranged";

        internal const int DarkDreamsItemId = 274971;

        private static readonly StepsOfMadnessDocumentedDropDefinition[] Drops =
            BuildDrops();

        internal static StepsOfMadnessDocumentedDropDefinition[] DocumentedDrops
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
            if (EqualsAny(value, "Unrelenting Fear")) return UnrelentingFearKey;
            if (EqualsAny(value, "Childhood Horror")) return ChildhoodHorrorKey;
            if (EqualsAny(value, "Foundation of Sanity")) return FoundationOfSanityKey;
            if (EqualsAny(value, "Figment of Imagination")) return FigmentOfImaginationKey;
            if (EqualsAny(value, "Fragment of Sanity")) return FragmentOfSanityKey;
            if (EqualsAny(value, "Roving Eye")) return RovingEyeKey;
            if (EqualsAny(value, "Detached Psyche")) return DetachedPsycheKey;
            if (EqualsAny(value, "Pulsing Hatred")) return PulsingHatredKey;
            if (EqualsAny(value, "Suppressed Emotion")) return SuppressedEmotionKey;
            if (EqualsAny(value, "Jealousy")) return JealousyKey;
            if (EqualsAny(value, "Notum Habit")) return NotumHabitKey;
            if (EqualsAny(value, "Mind Shard")) return MindShardKey;
            if (EqualsAny(value, "Sanity's Edge")) return SanitysEdgeKey;
            if (EqualsAny(value, "Thief of Reason")) return ThiefOfReasonKey;
            if (EqualsAny(value, "Betrayer of Memory")) return BetrayerOfMemoryKey;
            if (EqualsAny(value, "Neleb the Deranged")) return NelebKey;
            return null;
        }

        internal static StepsOfMadnessDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new StepsOfMadnessDocumentedDropDefinition[0];
            }

            string enemyKey = EnemyKeyForDisplayName(displayName);
            return Drops
                .Where(
                    value => value.AppliesToEveryEnemy
                             || (!string.IsNullOrWhiteSpace(enemyKey)
                                 && string.Equals(
                                     value.EnemyKey,
                                     enemyKey,
                                     StringComparison.Ordinal)))
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

            StepsOfMadnessDocumentedDropDefinition[] active =
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
            var groupKeys = new HashSet<string>(
                existing
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (StepsOfMadnessDocumentedDropDefinition drop in active)
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

                additions.Add(
                    IsNelebDarkDreams(drop)
                        ? NelebDarkDreamsGroup(groupKey, drop)
                        : DocumentedIndependentGroup(groupKey, drop));
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
            StepsOfMadnessDocumentedDropDefinition drop)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = groupKey,
                RollMode = LootRollMode.Independent,
                RollCount = 1,
                EmptyWeight = 0,
                DropChanceBasisPoints = drop.MinimumDropChanceBasisPoints,
                Entries = new[] { DocumentedEntry(drop, 1, 1, null) },
                Conditions = new string[0]
            };
        }

        private static LootGroupDefinition NelebDarkDreamsGroup(
            string groupKey,
            StepsOfMadnessDocumentedDropDefinition drop)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = groupKey,
                RollMode = LootRollMode.WeightedOne,
                RollCount = 1,
                EmptyWeight = 45,
                DropChanceBasisPoints = 10000,
                Entries = new[]
                {
                    DocumentedEntry(drop, 1, 40, "one-copy"),
                    DocumentedEntry(drop, 2, 15, "two-copies")
                },
                Conditions = new string[0]
            };
        }

        private static LootEntryDefinition DocumentedEntry(
            StepsOfMadnessDocumentedDropDefinition drop,
            int quantity,
            int weight,
            string selectionKey)
        {
            return new LootEntryDefinition
            {
                SelectionKey = selectionKey,
                ItemTemplateId = drop.ItemTemplateId,
                HighItemTemplateId = drop.ItemTemplateId,
                FixedQuality = drop.Quality,
                MinimumQuality = drop.Quality,
                MaximumQuality = drop.Quality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = weight,
                DropChanceBasisPoints = selectionKey == null ? 10000 : 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.WeightedDocumented,
                Evidence = LootEvidenceConfidence.CommunityDocumented,
                EvidenceReference = DocumentedLootSourceUrl,
                ProbabilityEvidence = "documented-exact:" + drop.SourceProbability
            };
        }

        private static bool IsNelebDarkDreams(
            StepsOfMadnessDocumentedDropDefinition drop)
        {
            return drop.ItemTemplateId == DarkDreamsItemId
                   && string.Equals(drop.EnemyKey, NelebKey, StringComparison.Ordinal);
        }

        private static StepsOfMadnessDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<StepsOfMadnessDocumentedDropDefinition>();

            AddUnresolvedRange(values, EveryEnemyKey, "Various Steps of Madness enemies", true,
                "various; rate and exact enemy coverage not published",
                275003, 275004, 274879, 274958, 274959, 274960, 274961);

            AddUnresolvedRange(values, UnrelentingFearKey, "Unrelenting Fear", false,
                "drop membership published; rate not published", 151903, 274958);
            AddUnresolvedRange(values, ChildhoodHorrorKey, "Childhood Horror", false,
                "reliable but not 100%; rate not published", 274958);
            AddUnresolvedRange(values, FoundationOfSanityKey, "Foundation of Sanity", false,
                "fairly reliable but may drop none; rate not published", 274879, 274960, 274961);
            AddUnresolvedRange(values, FigmentOfImaginationKey, "Figment of Imagination", false,
                "occasional Ether; type and rate not published",
                274879, 274958, 274959, 274960, 274961);
            AddUnresolvedRange(values, FragmentOfSanityKey, "Fragment of Sanity", false,
                "Ether membership published; type and rate not published",
                274879, 274958, 274959, 274960, 274961);
            AddUnresolvedRange(values, FragmentOfSanityKey, "Fragment of Sanity", false,
                "hands membership published; 100% Brutal Hands row is location-bound and same-name ambiguous",
                152021, 152022, 152023);
            AddUnresolvedRange(values, RovingEyeKey, "Roving Eye", false,
                "Ether membership published; type and rate not published",
                274879, 274958, 274959, 274960, 274961);
            AddUnresolvedRange(values, DetachedPsycheKey, "Detached Psyche", false,
                "Ether membership published; type and rate not published",
                274879, 274958, 274959, 274960, 274961);

            AddActive(values, PulsingHatredKey, "Pulsing Hatred", 152025, "100%", 10000);
            AddUnresolvedRange(values, PulsingHatredKey, "Pulsing Hatred", false,
                "drop membership published; rate not published", 274879);
            AddUnresolvedRange(values, SuppressedEmotionKey, "Suppressed Emotion", false,
                "high chance; numeric rate not published", 152031);
            AddUnresolvedRange(values, JealousyKey, "Jealousy", false,
                "drop membership published; rate not published", 152030);

            AddActive(values, NotumHabitKey, "Notum Habit", 152027, "100%", 10000);
            AddUnresolvedRange(values, NotumHabitKey, "Notum Habit", false,
                "Ether membership published; type and rate not published",
                274879, 274958, 274959, 274960, 274961);
            AddUnresolvedRange(values, NotumHabitKey, "Notum Habit", false,
                "sometimes; numeric rate not published", DarkDreamsItemId);
            AddUnresolvedRange(values, MindShardKey, "Mind Shard", false,
                "drop membership published; rate not published", 152024, DarkDreamsItemId);
            AddUnresolvedRange(values, SanitysEdgeKey, "Sanity's Edge", false,
                "hands membership published; per-item rates not published", 152021, 152022, 152023);
            AddUnresolvedRange(values, ThiefOfReasonKey, "Thief of Reason", false,
                "hands membership published; per-item rates not published", 152021, 152022, 152023);
            AddUnresolvedRange(values, BetrayerOfMemoryKey, "Betrayer of Memory", false,
                "drop membership published; rate not published", 152032);

            AddActive(values, NelebKey, "Neleb the Deranged", 151895, "100%", 10000);
            AddActive(values, NelebKey, "Neleb the Deranged", 151896, "100%", 10000);
            AddActive(values, NelebKey, "Neleb the Deranged", 152026, "100%", 10000);
            AddActive(values, NelebKey, "Neleb the Deranged", 274972, "100%", 10000);
            AddActive(
                values,
                NelebKey,
                "Neleb the Deranged",
                DarkDreamsItemId,
                "45% none; 40% one copy; 15% two copies",
                5500);

            return values.ToArray();
        }

        private static void AddActive(
            ICollection<StepsOfMadnessDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            int itemTemplateId,
            string sourceProbability,
            int dropChanceBasisPoints)
        {
            Add(
                values,
                enemyKey,
                enemyDisplayName,
                itemTemplateId,
                dropChanceBasisPoints,
                dropChanceBasisPoints,
                sourceProbability,
                false,
                true);
        }

        private static void AddUnresolvedRange(
            ICollection<StepsOfMadnessDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            bool appliesToEveryEnemy,
            string sourceProbability,
            params int[] itemTemplateIds)
        {
            foreach (int itemTemplateId in itemTemplateIds)
            {
                Add(
                    values,
                    enemyKey,
                    enemyDisplayName,
                    itemTemplateId,
                    0,
                    0,
                    sourceProbability,
                    appliesToEveryEnemy,
                    false);
            }
        }

        private static void Add(
            ICollection<StepsOfMadnessDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            int itemTemplateId,
            int minimumBasisPoints,
            int maximumBasisPoints,
            string sourceProbability,
            bool appliesToEveryEnemy,
            bool isActive)
        {
            values.Add(
                new StepsOfMadnessDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = ItemName(itemTemplateId),
                    ItemTemplateId = itemTemplateId,
                    Quality = ItemQuality(itemTemplateId),
                    MinimumDropChanceBasisPoints = minimumBasisPoints,
                    MaximumDropChanceBasisPoints = maximumBasisPoints,
                    SourceProbability = sourceProbability,
                    AppliesToEveryEnemy = appliesToEveryEnemy,
                    IsActive = isActive
                });
        }

        private static string ItemName(int itemTemplateId)
        {
            switch (itemTemplateId)
            {
                case 151895: return "Neleb's Nano-circuit Robe";
                case 151896: return "Fractured Sanity";
                case 151903: return "Fear-forged Blade";
                case 152021: return "Brutal Hands";
                case 152022: return "Loving Hands";
                case 152023: return "Gentle Hands";
                case 152024: return "Brainchopper";
                case 152025: return "Nervejolter";
                case 152026: return "Neleb's Notum Battlerod";
                case 152027: return "Neutrino Flash";
                case 152030: return "Essence of Pure Jealousy";
                case 152031: return "Emotional Sponge";
                case 152032: return "Cortex of the Executioner";
                case 274879: return "Nightmare Tidal Ether";
                case 274958: return "Nightmare Arachnid Ether";
                case 274959: return "Nightmare Spacial Ether";
                case 274960: return "Nightmare Burning Ether";
                case 274961: return "Nightmare Darkness Ether";
                case 274971: return "Dark Dreams";
                case 274972: return "Dream Mesh Circuit";
                case 275003: return "Sanity Recovery Medication";
                case 275004: return "Nightmare Therapy Pills";
                default: throw new ArgumentOutOfRangeException("itemTemplateId");
            }
        }

        private static int ItemQuality(int itemTemplateId)
        {
            switch (itemTemplateId)
            {
                case 151895:
                case 151896:
                case 151903:
                case 152025:
                case 152027:
                case 274879:
                case 274958:
                case 274959:
                case 274960:
                case 274961:
                case 275003:
                case 275004:
                    return 50;
                case 152021:
                case 152022:
                case 152023:
                    return 45;
                case 152024:
                case 152032:
                    return 44;
                case 152026:
                    return 53;
                case 152030:
                case 152031:
                    return 42;
                case 274971:
                case 274972:
                    return 1;
                default:
                    throw new ArgumentOutOfRangeException("itemTemplateId");
            }
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            return candidates.Any(
                candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
