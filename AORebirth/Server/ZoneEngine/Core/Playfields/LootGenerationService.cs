namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal interface ILootRandomSource
    {
        int Next(int maximumExclusive);
    }

    internal sealed class SeededLootRandomSource : ILootRandomSource
    {
        private readonly Random random;
        internal SeededLootRandomSource(int seed) { this.random = new Random(seed); }
        public int Next(int maximumExclusive) { return this.random.Next(maximumExclusive); }
    }

    internal sealed class GeneratedLootItem
    {
        internal LootEntryDefinition Definition { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int HighItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int Quantity { get; set; }
        internal string TableKey { get; set; }
        internal string GroupKey { get; set; }
    }

    internal sealed class LootRollEvidence
    {
        internal string TableKey { get; set; }
        internal string GroupKey { get; set; }
        internal int EntryTemplateId { get; set; }
        internal string Outcome { get; set; }
    }

    internal sealed class LootGenerationResult
    {
        internal LootGenerationResult()
        {
            this.Items = new List<GeneratedLootItem>();
            this.AppliedTableKeys = new List<string>();
            this.AppliedAssignmentKeys = new List<string>();
            this.RollEvidence = new List<LootRollEvidence>();
            this.SkippedEntries = new List<string>();
        }

        internal List<GeneratedLootItem> Items { get; private set; }
        internal int Credits { get; set; }
        internal bool CreditsUnresolved { get; set; }
        internal bool LootUnresolved { get; set; }
        internal List<string> AppliedTableKeys { get; private set; }
        internal List<string> AppliedAssignmentKeys { get; private set; }
        internal List<LootRollEvidence> RollEvidence { get; private set; }
        internal List<string> SkippedEntries { get; private set; }
        internal int Seed { get; set; }
        internal string RegistryVersion { get; set; }
    }

    internal sealed class ResolvedLootAssignment
    {
        internal LootAssignmentDefinition Assignment { get; set; }
        internal LootTableDefinition Table { get; set; }
        internal int Specificity { get; set; }
    }

    internal sealed class LootAssignmentResolver
    {
        internal ResolvedLootAssignment[] Resolve(LootTableRegistry registry, LootGenerationContext context)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (context == null) throw new ArgumentNullException("context");
            if (context.IsOwnedSummon) return new ResolvedLootAssignment[0];

            return registry.Assignments()
                .Where(x => x.Enabled && Matches(x, context))
                .Select(x => new ResolvedLootAssignment
                {
                    Assignment = x,
                    Table = registry.GetTable(x.LootTableKey),
                    Specificity = SpecificityFor(x.TargetType)
                })
                .Where(x => x.Table.Enabled)
                .OrderBy(x => x.Specificity)
                .ThenBy(x => x.Assignment.Priority)
                .ThenBy(x => x.Assignment.AssignmentKey, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool Matches(LootAssignmentDefinition assignment, LootGenerationContext context)
        {
            if (assignment.PlayfieldId.HasValue && assignment.PlayfieldId.Value != context.PlayfieldId) return false;
            if (assignment.MinimumLevel.HasValue && context.Level < assignment.MinimumLevel.Value) return false;
            if (assignment.MaximumLevel.HasValue && context.Level > assignment.MaximumLevel.Value) return false;
            switch (assignment.TargetType)
            {
                case LootAssignmentTargetType.Global: return true;
                case LootAssignmentTargetType.Family: return Same(assignment.TargetKey, context.FamilyKey);
                case LootAssignmentTargetType.EnemyType: return Same(assignment.TargetKey, context.EnemyProfileKey);
                case LootAssignmentTargetType.Spawn: return Same(assignment.TargetKey, context.SpawnKey);
                case LootAssignmentTargetType.Boss: return context.IsBoss && Same(assignment.TargetKey, context.EnemyProfileKey);
                case LootAssignmentTargetType.DynaGlobal: return context.IsDyna;
                case LootAssignmentTargetType.DynaLevelBand: return context.IsDyna && Same(assignment.TargetKey, context.DynaLevelBandKey);
                case LootAssignmentTargetType.DynaFamily: return context.IsDyna && Same(assignment.TargetKey, context.DynaFamilyKey);
                case LootAssignmentTargetType.Encounter: return Same(assignment.TargetKey, context.EncounterKey);
                case LootAssignmentTargetType.Event: return Same(assignment.TargetKey, context.EventKey);
                default: return false;
            }
        }

        private static bool Same(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int SpecificityFor(LootAssignmentTargetType type)
        {
            switch (type)
            {
                case LootAssignmentTargetType.Global: return 0;
                case LootAssignmentTargetType.Family: return 10;
                case LootAssignmentTargetType.EnemyType: return 20;
                case LootAssignmentTargetType.Dungeon:
                case LootAssignmentTargetType.Mission: return 25;
                case LootAssignmentTargetType.DynaGlobal: return 30;
                case LootAssignmentTargetType.DynaLevelBand: return 35;
                case LootAssignmentTargetType.DynaFamily: return 40;
                case LootAssignmentTargetType.Boss: return 50;
                case LootAssignmentTargetType.Spawn: return 60;
                case LootAssignmentTargetType.Encounter: return 70;
                case LootAssignmentTargetType.Event: return 80;
                default: return 25;
            }
        }
    }

    internal sealed class LootGenerationService
    {
        private readonly LootTableRegistry registry;
        private readonly LootAssignmentResolver resolver;
        internal LootGenerationService(
            LootTableRegistry registry,
            LootAssignmentResolver resolver)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (resolver == null) throw new ArgumentNullException("resolver");
            this.registry = registry;
            this.resolver = resolver;
        }

        internal LootGenerationResult Generate(LootGenerationContext context, ILootRandomSource random)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (random == null) throw new ArgumentNullException("random");
            var result = new LootGenerationResult { Seed = context.Seed, RegistryVersion = this.registry.Version };
            ResolvedLootAssignment[] resolved = this.resolver.Resolve(this.registry, context);
            if (resolved.Length == 0)
            {
                result.LootUnresolved = !context.IsOwnedSummon;
                return result;
            }

            foreach (ResolvedLootAssignment resolvedAssignment in resolved)
            {
                LootTableDefinition table = resolvedAssignment.Table;
                result.AppliedAssignmentKeys.Add(resolvedAssignment.Assignment.AssignmentKey);
                result.AppliedTableKeys.Add(table.LootTableKey);
                result.LootUnresolved |= table.ItemPoolUnresolved;
                foreach (LootGroupDefinition group in table.RollGroups.OrderBy(x => x.LootGroupKey, StringComparer.Ordinal))
                {
                    this.RollGroup(result, table, group, context, random);
                }
                ApplyCredits(result, table.CreditsPolicy, random);
            }
            return result;
        }

        private void RollGroup(
            LootGenerationResult result,
            LootTableDefinition table,
            LootGroupDefinition group,
            LootGenerationContext context,
            ILootRandomSource random)
        {
            if (group.DropChanceBasisPoints > 0 && group.DropChanceBasisPoints < 10000
                && random.Next(10000) >= group.DropChanceBasisPoints)
            {
                result.RollEvidence.Add(new LootRollEvidence
                {
                    TableKey = table.LootTableKey,
                    GroupKey = group.LootGroupKey,
                    EntryTemplateId = 0,
                    Outcome = "group-not-selected"
                });
                return;
            }
            LootEntryDefinition[] entries = group.Entries
                .OrderBy(x => x.ItemTemplateId)
                .ThenBy(x => x.MinimumQuality)
                .ToArray();
            switch (group.RollMode)
            {
                case LootRollMode.Guaranteed:
                case LootRollMode.All:
                    foreach (LootEntryDefinition entry in entries) this.TryGenerate(result, table, group, entry, context, random, true);
                    break;
                case LootRollMode.ObservedSnapshot:
                    result.LootUnresolved = true;
                    foreach (LootEntryDefinition entry in entries) this.TryGenerate(result, table, group, entry, context, random, true);
                    break;
                case LootRollMode.Independent:
                    foreach (LootEntryDefinition entry in entries) this.TryGenerate(result, table, group, entry, context, random, false);
                    break;
                case LootRollMode.WeightedOne:
                    this.RollWeighted(result, table, group, entries, context, random, 1);
                    break;
                case LootRollMode.WeightedMany:
                    this.RollWeighted(result, table, group, entries, context, random, Math.Max(0, group.RollCount));
                    break;
            }
        }

        private void RollWeighted(
            LootGenerationResult result,
            LootTableDefinition table,
            LootGroupDefinition group,
            LootEntryDefinition[] entries,
            LootGenerationContext context,
            ILootRandomSource random,
            int rolls)
        {
            for (int roll = 0; roll < rolls; roll++)
            {
                var selections = entries.Where(CanRoll)
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.SelectionKey)
                        ? "item:" + x.ItemTemplateId
                        : x.SelectionKey, StringComparer.Ordinal)
                    .Select(x => new { Key = x.Key, Weight = x.Max(y => y.Weight), Entries = x.ToArray() })
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToArray();
                int totalWeight = group.EmptyWeight + selections.Sum(x => x.Weight);
                if (totalWeight <= 0) return;
                int selection = random.Next(totalWeight);
                if (selection < group.EmptyWeight)
                {
                    result.RollEvidence.Add(new LootRollEvidence
                    {
                        TableKey = table.LootTableKey,
                        GroupKey = group.LootGroupKey,
                        EntryTemplateId = 0,
                        Outcome = "empty"
                    });
                    continue;
                }
                selection -= group.EmptyWeight;
                foreach (var candidate in selections)
                {
                    if (selection < candidate.Weight)
                    {
                        foreach (LootEntryDefinition entry in candidate.Entries)
                            this.TryGenerate(result, table, group, entry, context, random, true);
                        break;
                    }
                    selection -= candidate.Weight;
                }
            }
        }

        private void TryGenerate(
            LootGenerationResult result,
            LootTableDefinition table,
            LootGroupDefinition group,
            LootEntryDefinition entry,
            LootGenerationContext context,
            ILootRandomSource random,
            bool selected)
        {
            if (!CanRoll(entry))
            {
                result.LootUnresolved |= entry.Semantics == LootSemantics.Unresolved;
                result.SkippedEntries.Add(table.LootTableKey + ":" + group.LootGroupKey + ":" + entry.ItemTemplateId);
                return;
            }
            bool dropped = selected || entry.DropChanceBasisPoints >= 10000
                || (entry.DropChanceBasisPoints > 0 && random.Next(10000) < entry.DropChanceBasisPoints);
            result.RollEvidence.Add(new LootRollEvidence
            {
                TableKey = table.LootTableKey,
                GroupKey = group.LootGroupKey,
                EntryTemplateId = entry.ItemTemplateId,
                Outcome = dropped ? "generated" : "not-selected"
            });
            if (!dropped) return;
            if (entry.UniquePerCorpse && result.Items.Any(x => x.Definition.ItemTemplateId == entry.ItemTemplateId)) return;

            int quality = entry.FixedQuality ?? NextInclusive(random, entry.MinimumQuality, entry.MaximumQuality);
            int quantity = NextInclusive(random, entry.MinimumQuantity, entry.MaximumQuantity);
            result.Items.Add(new GeneratedLootItem
            {
                Definition = entry,
                ItemTemplateId = entry.ItemTemplateId,
                HighItemTemplateId = entry.HighItemTemplateId,
                Quality = quality,
                Quantity = quantity,
                TableKey = table.LootTableKey,
                GroupKey = group.LootGroupKey
            });
        }

        private static bool CanRoll(LootEntryDefinition entry)
        {
            return entry.Semantics != LootSemantics.Unresolved && entry.Semantics != LootSemantics.NoneProven;
        }

        private static void ApplyCredits(LootGenerationResult result, CreditsPolicyDefinition policy, ILootRandomSource random)
        {
            switch (policy.Mode)
            {
                case CreditsPolicyMode.None: result.Credits = 0; result.CreditsUnresolved = false; break;
                case CreditsPolicyMode.Fixed: result.Credits = policy.MinimumCredits; result.CreditsUnresolved = false; break;
                case CreditsPolicyMode.Range: result.Credits = NextInclusive(random, policy.MinimumCredits, policy.MaximumCredits); result.CreditsUnresolved = false; break;
                case CreditsPolicyMode.ObservedSet:
                {
                    int[] observed = policy.ObservedCredits ?? new int[0];
                    result.Credits = observed.Length == 0 ? 0 : observed[random.Next(observed.Length)];
                    result.CreditsUnresolved = true;
                    break;
                }
                case CreditsPolicyMode.Unresolved: result.CreditsUnresolved = true; break;
            }
        }

        private static int NextInclusive(ILootRandomSource random, int minimum, int maximum)
        {
            return maximum <= minimum ? minimum : minimum + random.Next(maximum - minimum + 1);
        }
    }
}
