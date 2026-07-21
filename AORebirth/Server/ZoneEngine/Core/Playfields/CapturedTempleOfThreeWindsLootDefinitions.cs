namespace AORebirth.Core.Playfields
{
    internal static class CapturedTempleOfThreeWindsLootDefinitions
    {
        internal const int PlayfieldInstance = 647;
        internal const string DefenderProfileKey = "totw.647.boss.defender-of-the-three";
        internal const string DefenderEncounterKey = "totw.647.encounter.defender-of-the-three";
        internal const int DefenderCredits = 1450;
        internal const int DefenderFirstItem = 204750;
        internal const int DefenderSecondItem = 204649;

        private const string Evidence =
            "official-live captures 20260721-035526/040249/040324: two exact Defender of the Three "
            + "corpse snapshots with 1450 credits; first has 204750x1 plus 204649x1, second has "
            + "204750x2 plus 204649x1, all QL1; snapshot probabilities and wider pool unresolved";

        internal static bool TryRegister(
            LootTableRegistry registry,
            string profileKey,
            string encounterKey)
        {
            if (registry == null
                || profileKey != DefenderProfileKey)
            {
                return false;
            }

            string tableKey = "captured." + profileKey;
            if (!registry.ContainsTable(tableKey))
            {
                registry.RegisterTable(BuildDefenderLootTable());
                registry.RegisterAssignment(
                    new LootAssignmentDefinition
                    {
                        AssignmentKey = tableKey,
                        TargetType = LootAssignmentTargetType.Boss,
                        TargetKey = profileKey,
                        LootTableKey = tableKey,
                        PlayfieldId = PlayfieldInstance,
                        EncounterKey = encounterKey,
                        Priority = 0,
                        Conditions = new string[0],
                        Evidence = Evidence,
                        Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                        Enabled = true
                    });
            }

            return true;
        }

        internal static LootTableDefinition BuildDefenderLootTable()
        {
            string tableKey =
                "captured." + DefenderProfileKey;
            return new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = "Defender of the Three captured corpse snapshots",
                TableType = LootTableType.Boss,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = new[]
                {
                    Snapshot(
                        "capture.20260721-035526-040249",
                        Entry("capture.20260721-035526-040249", DefenderFirstItem, 1),
                        Entry("capture.20260721-035526-040249", DefenderSecondItem, 1)),
                    Snapshot(
                        "capture.20260721-040324",
                        Entry("capture.20260721-040324", DefenderFirstItem, 2),
                        Entry("capture.20260721-040324", DefenderSecondItem, 1))
                },
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                QualityPolicy = "captured-observed-corpse-snapshots",
                Evidence = Evidence,
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static ObservedCorpseSnapshotDefinition Snapshot(
            string key,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = key,
                Credits = DefenderCredits,
                Entries = entries,
                Evidence = LootEvidenceConfidence.ProvenCapture,
                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,
                EvidenceReference = Evidence + "; " + key
            };
        }

        private static LootEntryDefinition Entry(string snapshotKey, int itemId, int quantity)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = itemId,
                HighItemTemplateId = itemId,
                FixedQuality = 1,
                MinimumQuality = 1,
                MaximumQuality = 1,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = Evidence + "; " + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }
    }
}
