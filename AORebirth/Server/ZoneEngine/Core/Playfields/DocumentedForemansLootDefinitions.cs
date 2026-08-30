namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class ForemansDocumentedDropDefinition
    {
        internal string EnemyKey { get; set; }
        internal string EnemyDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int HighItemTemplateId { get; set; }
        internal int MinimumQuality { get; set; }
        internal int MaximumQuality { get; set; }
        internal bool AppliesToEveryEnemy { get; set; }
    }

    internal static class DocumentedForemansLootDefinitions
    {
        internal const int PlayfieldInstance = 1941;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Biomare#Loot_&_Items";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.foremans.loot-membership.";

        internal const string EveryEnemyKey = "foremans.1941.all-enemies";
        internal const string GunbeetleKey = "foremans.1941.enemy.gunbeetle";
        internal const string BodyguardKey = "foremans.1941.enemy.bodyguard";
        internal const string LabDirectorKey = "foremans.1941.boss.lab-director";
        internal const string NeutralizerKey = "foremans.1941.enemy.neutralizer";
        internal const string RikRakKey = "foremans.1941.named.captain-rik-rak-jones";
        internal const string TriPlumboKey = "foremans.1941.boss.tri-plumbo";
        internal const string SecurityOfficerKey = "foremans.1941.enemy.security-officer";
        internal const string ResearchTechnicianKey = "foremans.1941.enemy.research-technician";
        internal const string TimKey = "foremans.1941.boss.tim";
        internal const string ExecutiveProtectorKey = "foremans.1941.enemy.executive-protector";
        internal const string ChiefBaseProtectorKey = "foremans.1941.enemy.chief-base-protector";

        private static readonly ForemansDocumentedDropDefinition[] Drops = BuildDrops();

        internal static ForemansDocumentedDropDefinition[] DocumentedDrops
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
            if (EqualsAny(value, "Gunbeetle")) return GunbeetleKey;
            if (EqualsAny(value, "Bodyguard")) return BodyguardKey;
            if (EqualsAny(value, "Lab Director")) return LabDirectorKey;
            if (EqualsAny(value, "Neutralizer")) return NeutralizerKey;
            if (EqualsAny(value, "Captain Rik-Rak Jones", "Rik-Rak")) return RikRakKey;
            if (EqualsAny(value, "Tri Plumbo", "Tri-Plumbo")) return TriPlumboKey;
            if (EqualsAny(value, "Security Officer")) return SecurityOfficerKey;
            if (EqualsAny(value, "Research Technician")) return ResearchTechnicianKey;
            if (EqualsAny(value, "T.I.M.")) return TimKey;
            if (EqualsAny(value, "Executive Protector")) return ExecutiveProtectorKey;
            if (EqualsAny(value, "Chief Base Protector")) return ChiefBaseProtectorKey;
            return null;
        }

        internal static ForemansDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new ForemansDocumentedDropDefinition[0];
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

        internal static bool ApplyDocumentedMembership(
            LootTableDefinition table,
            int playfieldId,
            string displayName)
        {
            if (table == null)
            {
                return false;
            }

            ForemansDocumentedDropDefinition[] documented =
                DropsForDisplayName(playfieldId, displayName);
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
            foreach (IGrouping<string, ForemansDocumentedDropDefinition> scope in
                     documented.GroupBy(value => value.EnemyKey))
            {
                string groupKey = DocumentedLootGroupPrefix + scope.Key;
                if (groupKeys.Contains(groupKey))
                {
                    continue;
                }

                ForemansDocumentedDropDefinition[] missing = scope
                    .Where(
                        value => !existingItemIds.Contains(value.ItemTemplateId)
                                 && !existingItemIds.Contains(value.HighItemTemplateId))
                    .ToArray();
                if (missing.Length == 0)
                {
                    continue;
                }

                additions.Add(
                    new LootGroupDefinition
                    {
                        LootGroupKey = groupKey,
                        RollMode = LootRollMode.WeightedOne,
                        RollCount = 1,
                        EmptyWeight = 0,
                        DropChanceBasisPoints = 10000,
                        Entries = missing.Select(DocumentedEntry).ToArray(),
                        Conditions = new string[0]
                    });
                foreach (ForemansDocumentedDropDefinition drop in missing)
                {
                    existingItemIds.Add(drop.ItemTemplateId);
                    existingItemIds.Add(drop.HighItemTemplateId);
                }
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

        private static LootEntryDefinition DocumentedEntry(
            ForemansDocumentedDropDefinition drop)
        {
            bool fixedQuality = drop.ItemTemplateId == drop.HighItemTemplateId;
            return new LootEntryDefinition
            {
                ItemTemplateId = drop.ItemTemplateId,
                HighItemTemplateId = drop.HighItemTemplateId,
                FixedQuality = fixedQuality ? drop.MinimumQuality : 0,
                MinimumQuality = drop.MinimumQuality,
                MaximumQuality = drop.MaximumQuality,
                MinimumQuantity = 1,
                MaximumQuantity = 1,
                Weight = 1,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.WeightedDocumented,
                Evidence = LootEvidenceConfidence.CommunityDocumented,
                EvidenceReference = DocumentedLootSourceUrl,
                ProbabilityEvidence = "unresolved-membership-only"
            };
        }

        private static ForemansDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<ForemansDocumentedDropDefinition>();

            AddRange(values, EveryEnemyKey, "All Biomare mobs", "Notum Chip", 136622, 136623, 30, 100, true);
            AddRange(values, EveryEnemyKey, "All Biomare mobs", "Notum Fragment", 136624, 136625, 30, 100, true);
            AddRange(values, EveryEnemyKey, "All Biomare mobs", "Enriched Notum Nugget", 136636, 136637, 30, 100, true);

            AddFixed(values, GunbeetleKey, "Gunbeetle", "Salvaged Beetle Blaster", 156769, 48);
            AddFixed(values, BodyguardKey, "Bodyguard", "Gamma Ejector", 156770, 68);
            AddFixed(values, BodyguardKey, "Bodyguard", "Assault-class Tank Armor", 156576, 68);
            AddFixed(values, LabDirectorKey, "Lab Director", "Customized IMI Desert Reet 1000", 156771, 80);
            AddFixed(values, LabDirectorKey, "Lab Director", "Sealed Order FPGA-202", 156332, 80);
            AddFixed(values, LabDirectorKey, "Lab Director", "Sealed Order XITL-0127", 156328, 80);
            AddFixed(values, LabDirectorKey, "Lab Director", "Sealed Order BLCG-7791", 156330, 80);
            AddFixed(values, NeutralizerKey, "Neutralizer", "Combat Medic's Light Tank Armor", 156575, 54);
            AddFixed(values, RikRakKey, "Captain Rik-Rak Jones", "Assault-class Tank Armor", 156576, 68);
            AddFixed(values, TriPlumboKey, "Tri Plumbo", "Corroded Ring", 200818, 100);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Plate Arms", 208253, 208254, 60, 100, false);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Breastplate", 208255, 208256, 60, 100, false);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Plate Boots", 208257, 208258, 60, 100, false);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Plate Gloves", 208259, 208260, 60, 100, false);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Plate Helmet", 208261, 208262, 60, 100, false);
            AddRange(values, TriPlumboKey, "Tri Plumbo", "Storm Carbonum Plate Legs", 208263, 208264, 60, 100, false);
            AddFixed(values, SecurityOfficerKey, "Security Officer", "Personal Safe", 156695, 100);
            AddFixed(values, ResearchTechnicianKey, "Research Technician", "Personal Safe", 156695, 100);
            AddFixed(values, TimKey, "T.I.M.", "HUD Upgrade: Enhanced Target", 156773, 100);
            AddFixed(values, TimKey, "T.I.M.", "HUD Upgrade: Personal S.T.M", 156774, 100);
            AddFixed(values, ExecutiveProtectorKey, "Executive Protector", "Enhanced NCU Chip with Recompiling Core", 156693, 50);
            AddFixed(values, ChiefBaseProtectorKey, "Chief Base Protector", "Aged Brandy", 156697, 1);

            return values.ToArray();
        }

        private static void AddFixed(
            ICollection<ForemansDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality)
        {
            AddRange(
                values,
                enemyKey,
                enemyDisplayName,
                itemName,
                itemTemplateId,
                itemTemplateId,
                quality,
                quality,
                false);
        }

        private static void AddRange(
            ICollection<ForemansDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int highItemTemplateId,
            int minimumQuality,
            int maximumQuality,
            bool appliesToEveryEnemy)
        {
            values.Add(
                new ForemansDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = itemName,
                    ItemTemplateId = itemTemplateId,
                    HighItemTemplateId = highItemTemplateId,
                    MinimumQuality = minimumQuality,
                    MaximumQuality = maximumQuality,
                    AppliesToEveryEnemy = appliesToEveryEnemy
                });
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            return candidates.Any(
                candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
