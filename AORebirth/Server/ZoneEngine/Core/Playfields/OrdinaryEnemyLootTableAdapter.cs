namespace AORebirth.Core.Playfields
{
    using System;
    using System.Globalization;
    using System.Linq;

    internal sealed class OrdinaryEnemyLootTableAdapterResult
    {
        internal OrdinaryEnemyLootTableAdapterResult(
            LootTableDefinition table,
            LootAssignmentDefinition assignment)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (assignment == null) throw new ArgumentNullException("assignment");
            this.Table = table;
            this.Assignment = assignment;
        }

        internal LootTableDefinition Table { get; private set; }

        internal LootAssignmentDefinition Assignment { get; private set; }
    }

    internal static class OrdinaryEnemyLootTableAdapter
    {
        internal static OrdinaryEnemyLootTableAdapterResult Build(
            OrdinaryEnemyProfile profile,
            string tableKey,
            string assignmentKey)
        {
            return Build(profile, 0, tableKey, assignmentKey);
        }

        internal static OrdinaryEnemyLootTableAdapterResult Build(
            OrdinaryEnemyProfile profile,
            int targetLevel,
            string tableKey,
            string assignmentKey)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (string.IsNullOrWhiteSpace(tableKey)) throw new ArgumentException("Table key is required.", "tableKey");
            if (string.IsNullOrWhiteSpace(assignmentKey)) throw new ArgumentException("Assignment key is required.", "assignmentKey");

            OrdinaryEnemyProfileValidator.ValidateLootProfile(profile.ProfileKey, profile.Loot);
            LootGroupDefinition[] groups = BuildGroups(profile.Loot);
            bool levelSpecificCredits = profile.Loot.LevelCreditRules.Length > 0 && targetLevel > 0;
            string evidence = string.Join(
                ",",
                new[]
                    {
                        profile.Loot.ItemEvidenceReference,
                        profile.Loot.CreditEvidenceReference
                    }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(evidence))
            {
                evidence = profile.Loot.Evidence.ToString();
            }
            LootEvidenceConfidence confidence = ConfidenceFor(profile.Loot);
            var table = new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = profile.DisplayName,
                TableType = LootTableType.EnemyType,
                RollGroups = groups,
                CreditsPolicy = CreditsFor(profile.Loot, targetLevel),
                QualityPolicy = "captured-fixed",
                Evidence = evidence,
                Confidence = confidence,
                ItemPoolUnresolved = !profile.Loot.ItemPoolComplete,
                Enabled = true
            };
            var assignment = new LootAssignmentDefinition
            {
                AssignmentKey = assignmentKey,
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = profile.ProfileKey,
                LootTableKey = tableKey,
                MinimumLevel = levelSpecificCredits ? (int?)targetLevel : null,
                MaximumLevel = levelSpecificCredits ? (int?)targetLevel : null,
                Priority = 0,
                Evidence = evidence,
                Confidence = confidence,
                Enabled = true,
                Conditions = new string[0]
            };
            return new OrdinaryEnemyLootTableAdapterResult(table, assignment);
        }

        private static LootGroupDefinition[] BuildGroups(OrdinaryEnemyLootProfile loot)
        {
            OrdinaryEnemyLootEntry[] entries = loot.Entries ?? new OrdinaryEnemyLootEntry[0];
            if (entries.Length == 0)
            {
                return new LootGroupDefinition[0];
            }

            if (loot.PoolMode == OrdinaryEnemyLootPoolMode.WeightedOne)
            {
                return new[]
                {
                    new LootGroupDefinition
                    {
                        LootGroupKey = "weighted-one",
                        RollMode = LootRollMode.WeightedOne,
                        RollCount = 1,
                        EmptyWeight = loot.EmptyWeight,
                        DropChanceBasisPoints = 10000,
                        Entries = entries.Select(AdaptEntry).ToArray(),
                        Conditions = new string[0]
                    }
                };
            }

            if (loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries)
            {
                throw new LootDefinitionValidationException(
                    "Unsupported ordinary enemy loot pool mode: " + loot.PoolMode);
            }

            return entries.Select(
                (entry, index) =>
                    new LootGroupDefinition
                    {
                        LootGroupKey = "entry." + index.ToString(CultureInfo.InvariantCulture),
                        RollMode = entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven
                            ? LootRollMode.Guaranteed
                            : LootRollMode.Independent,
                        RollCount = 1,
                        EmptyWeight = 0,
                        DropChanceBasisPoints = 10000,
                        Entries = new[] { AdaptEntry(entry) },
                        Conditions = new string[0]
                    })
                .ToArray();
        }

        private static LootEntryDefinition AdaptEntry(OrdinaryEnemyLootEntry entry)
        {
            bool guaranteed = entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven;
            return new LootEntryDefinition
            {
                SelectionKey = string.Format(
                    CultureInfo.InvariantCulture,
                    "slot.{0}.item.{1}.{2}.ql.{3}",
                    entry.Slot,
                    entry.LowId,
                    entry.HighId,
                    entry.QualityLevel),
                ItemTemplateId = entry.LowId,
                HighItemTemplateId = entry.HighId,
                FixedQuality = entry.QualityLevel,
                MinimumQuality = entry.QualityLevel,
                MaximumQuality = entry.QualityLevel,
                MinimumQuantity = entry.Quantity,
                MaximumQuantity = entry.Quantity,
                Weight = entry.Weight,
                DropChanceBasisPoints = entry.DropChanceBasisPoints,
                UniquePerCorpse = true,
                Semantics = guaranteed
                    ? LootSemantics.GuaranteedProven
                    : LootSemantics.ObservedAvailable,
                Evidence = entry.LinkageEvidence
                    == OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence
                        ? LootEvidenceConfidence.ObservedAvailableLoot
                        : LootEvidenceConfidence.ProvenCapture,
                EvidenceReference = entry.EvidenceReference,
                LinkageEvidence = entry.LinkageEvidence.ToString(),
                ProbabilityEvidence = entry.ProbabilityEvidence.ToString()
            };
        }

        private static CreditsPolicyDefinition CreditsFor(
            OrdinaryEnemyLootProfile loot,
            int targetLevel)
        {
            OrdinaryEnemyLevelCreditRule level = targetLevel > 0
                ? loot.LevelCreditRules.FirstOrDefault(value => value.EnemyLevel == targetLevel)
                : null;
            if (level != null)
            {
                return CreditsRange(
                    level.MinimumCredits,
                    level.MaximumCredits,
                    LootEvidenceConfidence.ProvenCapture);
            }

            if (loot.ObservedCreditOutcomes.Length > 0)
            {
                int[] outcomes = (int[])loot.ObservedCreditOutcomes.Clone();
                return new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.ObservedSamples,
                    MinimumCredits = outcomes.Min(),
                    MaximumCredits = outcomes.Max(),
                    ObservedCredits = outcomes,
                    Evidence = LootEvidenceConfidence.ObservedAvailableLoot
                };
            }

            if ((loot.CreditEvidence == OrdinaryEnemyEvidenceState.Observed
                 || loot.CreditEvidence == OrdinaryEnemyEvidenceState.Policy)
                && loot.MinimumCredits.HasValue
                && loot.MaximumCredits.HasValue)
            {
                return CreditsRange(
                    loot.MinimumCredits.Value,
                    loot.MaximumCredits.Value,
                    loot.CreditEvidence == OrdinaryEnemyEvidenceState.Observed
                        ? LootEvidenceConfidence.ProvenCapture
                        : LootEvidenceConfidence.Inferred);
            }

            return new CreditsPolicyDefinition
            {
                Mode = CreditsPolicyMode.Unresolved,
                Evidence = LootEvidenceConfidence.Unresolved
            };
        }

        private static CreditsPolicyDefinition CreditsRange(
            int minimum,
            int maximum,
            LootEvidenceConfidence evidence)
        {
            return new CreditsPolicyDefinition
            {
                Mode = minimum == maximum ? CreditsPolicyMode.Fixed : CreditsPolicyMode.Range,
                MinimumCredits = minimum,
                MaximumCredits = maximum,
                Evidence = evidence
            };
        }

        private static LootEvidenceConfidence ConfidenceFor(OrdinaryEnemyLootProfile loot)
        {
            if (loot.Entries.Length > 0
                && loot.Entries.All(
                    value => value.LinkageEvidence
                                 == OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem
                             || value.LinkageEvidence
                                 == OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem))
            {
                return LootEvidenceConfidence.ProvenCapture;
            }
            if (loot.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot)
            {
                return LootEvidenceConfidence.ObservedAvailableLoot;
            }
            return LootEvidenceConfidence.Unresolved;
        }
    }
}
