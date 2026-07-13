namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum LootTableType
    {
        GlobalDefault, Family, EnemyType, SpawnOverride, Boss, DynaGlobal,
        DynaLevelBand, DynaFamily, Encounter, Mission, Dungeon, Event, Quest
    }

    internal enum LootRollMode { All, WeightedOne, WeightedMany, Independent, Guaranteed }
    internal enum LootEvidenceConfidence
    {
        ProvenRepository, ProvenCapture, ProvenPrimarySource, CommunityDocumented,
        ObservedAvailableLoot, Inferred, Unresolved
    }
    internal enum LootSemantics
    {
        GuaranteedProven, ObservedAvailable, WeightedDocumented, NoneProven, Unresolved
    }
    internal enum LootAssignmentTargetType
    {
        Global, Family, EnemyType, Spawn, Boss, DynaGlobal, DynaLevelBand,
        DynaFamily, Encounter, Mission, Dungeon, Event, Quest
    }
    internal enum CreditsPolicyMode { None, Fixed, Range, Unresolved }
    internal enum CorpseLootRightsPolicy { Public, OwnerOnly, Team, Personal, Scripted, Unresolved }

    internal sealed class CreditsPolicyDefinition
    {
        internal CreditsPolicyMode Mode { get; set; }
        internal int MinimumCredits { get; set; }
        internal int MaximumCredits { get; set; }
        internal LootEvidenceConfidence Evidence { get; set; }
    }

    internal sealed class LootEntryDefinition
    {
        internal string SelectionKey { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int HighItemTemplateId { get; set; }
        internal int? FixedQuality { get; set; }
        internal int MinimumQuality { get; set; }
        internal int MaximumQuality { get; set; }
        internal int MinimumQuantity { get; set; }
        internal int MaximumQuantity { get; set; }
        internal int Weight { get; set; }
        internal int DropChanceBasisPoints { get; set; }
        internal bool UniquePerCorpse { get; set; }
        internal LootSemantics Semantics { get; set; }
        internal LootEvidenceConfidence Evidence { get; set; }
        internal string EvidenceReference { get; set; }
    }

    internal sealed class LootGroupDefinition
    {
        internal string LootGroupKey { get; set; }
        internal LootRollMode RollMode { get; set; }
        internal int RollCount { get; set; }
        internal int EmptyWeight { get; set; }
        internal int DropChanceBasisPoints { get; set; }
        internal LootEntryDefinition[] Entries { get; set; }
        internal string[] Conditions { get; set; }
    }

    internal sealed class LootTableDefinition
    {
        internal string LootTableKey { get; set; }
        internal string DisplayName { get; set; }
        internal LootTableType TableType { get; set; }
        internal LootGroupDefinition[] RollGroups { get; set; }
        internal CreditsPolicyDefinition CreditsPolicy { get; set; }
        internal string QualityPolicy { get; set; }
        internal string Evidence { get; set; }
        internal LootEvidenceConfidence Confidence { get; set; }
        internal bool Enabled { get; set; }
    }

    internal sealed class LootAssignmentDefinition
    {
        internal string AssignmentKey { get; set; }
        internal LootAssignmentTargetType TargetType { get; set; }
        internal string TargetKey { get; set; }
        internal string LootTableKey { get; set; }
        internal int? PlayfieldId { get; set; }
        internal string EncounterKey { get; set; }
        internal int? MinimumLevel { get; set; }
        internal int? MaximumLevel { get; set; }
        internal int Priority { get; set; }
        internal string[] Conditions { get; set; }
        internal string Evidence { get; set; }
        internal LootEvidenceConfidence Confidence { get; set; }
        internal bool Enabled { get; set; }
    }

    internal sealed class LootGenerationContext
    {
        internal string EnemyProfileKey { get; set; }
        internal int EnemyIdentityInstance { get; set; }
        internal int MonsterData { get; set; }
        internal string FamilyKey { get; set; }
        internal int Level { get; set; }
        internal int PlayfieldId { get; set; }
        internal string SpawnKey { get; set; }
        internal string EncounterKey { get; set; }
        internal bool IsBoss { get; set; }
        internal bool IsDyna { get; set; }
        internal bool IsOwnedSummon { get; set; }
        internal string DynaLevelBandKey { get; set; }
        internal string DynaFamilyKey { get; set; }
        internal string EventKey { get; set; }
        internal int Seed { get; set; }
    }

    internal sealed class LootDefinitionValidationException : Exception
    {
        internal LootDefinitionValidationException(string message) : base(message) { }
    }

    internal sealed class LootTableRegistry
    {
        private readonly Dictionary<string, LootTableDefinition> tables =
            new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LootAssignmentDefinition> assignments =
            new Dictionary<string, LootAssignmentDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<int, bool> itemTemplateExists;

        internal LootTableRegistry(Func<int, bool> itemTemplateExists)
        {
            this.itemTemplateExists = itemTemplateExists ?? (value => value > 0);
        }

        internal string Version { get; private set; }

        internal void RegisterTable(LootTableDefinition table)
        {
            ValidateTable(table);
            if (this.tables.ContainsKey(table.LootTableKey))
            {
                throw new LootDefinitionValidationException("Duplicate loot table key: " + table.LootTableKey);
            }
            this.tables.Add(table.LootTableKey, table);
            this.RefreshVersion();
        }

        internal void RegisterAssignment(LootAssignmentDefinition assignment)
        {
            ValidateAssignment(assignment);
            if (this.assignments.ContainsKey(assignment.AssignmentKey))
            {
                throw new LootDefinitionValidationException("Duplicate loot assignment key: " + assignment.AssignmentKey);
            }
            if (!this.tables.ContainsKey(assignment.LootTableKey))
            {
                throw new LootDefinitionValidationException("Assignment references missing table: " + assignment.LootTableKey);
            }
            this.assignments.Add(assignment.AssignmentKey, assignment);
            this.RefreshVersion();
        }

        internal bool ContainsTable(string key) { return this.tables.ContainsKey(key); }
        internal bool ContainsAssignment(string key) { return this.assignments.ContainsKey(key); }
        internal LootTableDefinition GetTable(string key) { return this.tables[key]; }
        internal LootAssignmentDefinition[] Assignments() { return this.assignments.Values.ToArray(); }

        private void ValidateTable(LootTableDefinition table)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.LootTableKey))
            {
                throw new LootDefinitionValidationException("Loot table key is required.");
            }
            if (table.RollGroups == null) table.RollGroups = new LootGroupDefinition[0];
            if (table.CreditsPolicy == null)
            {
                throw new LootDefinitionValidationException("Credits policy is required for " + table.LootTableKey);
            }
            ValidateCredits(table.CreditsPolicy, table.LootTableKey);
            var groupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LootGroupDefinition group in table.RollGroups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.LootGroupKey) || !groupKeys.Add(group.LootGroupKey))
                {
                    throw new LootDefinitionValidationException("Invalid or duplicate loot group in " + table.LootTableKey);
                }
                if (group.RollCount < 0 || group.EmptyWeight < 0 || group.DropChanceBasisPoints < 0
                    || group.DropChanceBasisPoints > 10000 || group.Entries == null)
                {
                    throw new LootDefinitionValidationException("Invalid roll count or entries in " + group.LootGroupKey);
                }
                foreach (LootEntryDefinition entry in group.Entries) ValidateEntry(entry, group.LootGroupKey, table.Enabled);
            }
        }

        private void ValidateCredits(CreditsPolicyDefinition policy, string tableKey)
        {
            if (policy.MinimumCredits < 0 || policy.MaximumCredits < policy.MinimumCredits)
            {
                throw new LootDefinitionValidationException("Invalid credits range in " + tableKey);
            }
            if (policy.Mode == CreditsPolicyMode.Fixed && policy.MinimumCredits != policy.MaximumCredits)
            {
                throw new LootDefinitionValidationException("Fixed credits require equal bounds in " + tableKey);
            }
        }

        private void ValidateEntry(LootEntryDefinition entry, string groupKey, bool active)
        {
            if (entry == null || entry.ItemTemplateId <= 0)
                throw new LootDefinitionValidationException("Invalid item template in " + groupKey);
            if (active && !this.itemTemplateExists(entry.ItemTemplateId))
                throw new LootDefinitionValidationException("Unknown active item template: " + entry.ItemTemplateId);
            if (active && entry.HighItemTemplateId > 0 && !this.itemTemplateExists(entry.HighItemTemplateId))
                throw new LootDefinitionValidationException("Unknown active high item template: " + entry.HighItemTemplateId);
            if (entry.FixedQuality.HasValue && entry.FixedQuality.Value < 1)
                throw new LootDefinitionValidationException("Invalid fixed quality in " + groupKey);
            if (entry.MinimumQuality < 1 || entry.MaximumQuality < entry.MinimumQuality)
                throw new LootDefinitionValidationException("Invalid quality range in " + groupKey);
            if (entry.MinimumQuantity < 1 || entry.MaximumQuantity < entry.MinimumQuantity)
                throw new LootDefinitionValidationException("Invalid quantity range in " + groupKey);
            if (entry.Weight < 0 || entry.DropChanceBasisPoints < 0 || entry.DropChanceBasisPoints > 10000)
                throw new LootDefinitionValidationException("Invalid weight or probability in " + groupKey);
            if (entry.Semantics == LootSemantics.GuaranteedProven
                && entry.Evidence == LootEvidenceConfidence.Unresolved)
                throw new LootDefinitionValidationException("Unresolved item cannot be guaranteed in " + groupKey);
            if (entry.Semantics == LootSemantics.ObservedAvailable
                && entry.DropChanceBasisPoints >= 10000)
                throw new LootDefinitionValidationException("Observed-only item cannot become guaranteed in " + groupKey);
        }

        private static void ValidateAssignment(LootAssignmentDefinition assignment)
        {
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentKey)
                || string.IsNullOrWhiteSpace(assignment.LootTableKey))
                throw new LootDefinitionValidationException("Assignment key and table key are required.");
            if (assignment.MinimumLevel.HasValue && assignment.MaximumLevel.HasValue
                && assignment.MinimumLevel.Value > assignment.MaximumLevel.Value)
                throw new LootDefinitionValidationException("Invalid assignment level range: " + assignment.AssignmentKey);
            if (assignment.TargetType != LootAssignmentTargetType.Global
                && string.IsNullOrWhiteSpace(assignment.TargetKey))
                throw new LootDefinitionValidationException("Assignment target key is required: " + assignment.AssignmentKey);
        }

        private void RefreshVersion()
        {
            this.Version = string.Join("|", this.tables.Keys.OrderBy(x => x, StringComparer.Ordinal)
                .Concat(this.assignments.Keys.OrderBy(x => x, StringComparer.Ordinal)));
        }
    }
}
