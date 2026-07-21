namespace AORebirth.Core.Playfields
{
    using System;

    internal static class CapturedTempleOfThreeWindsLootDefinitions
    {
        internal const int PlayfieldInstance = 647;

        internal const string DefenderProfileKey = "totw.647.boss.defender-of-the-three";
        internal const string DefenderEncounterKey = "totw.647.encounter.defender-of-the-three";
        internal const string YatilaProfileKey = "totw.647.named.windcaller-yatila";
        internal const string YatilaEncounterKey = "totw.647.encounter.windcaller-yatila";
        internal const string GulardProfileKey = "totw.647.named.reverend-gulard";
        internal const string GulardEncounterKey = "totw.647.encounter.reverend-gulard";
        internal const string ReAnimatorProfileKey = "totw.647.boss.the-re-animator";
        internal const string ReAnimatorEncounterKey = "totw.647.encounter.the-re-animator";
        internal const string BetanyProfileKey = "totw.647.named.acolyte-betany";
        internal const string BetanyEncounterKey = "totw.647.encounter.acolyte-betany";

        internal const int DefenderCredits = 1450;
        internal const int DefenderFirstItem = 204750;
        internal const int DefenderSecondItem = 204649;

        private const string DefenderEvidence =
            "official-live captures 20260721-035526/040249/040324: two exact Defender of the Three "
            + "corpse snapshots with 1450 credits; first has 204750x1 plus 204649x1, second has "
            + "204750x2 plus 204649x1, all QL1; snapshot probabilities and wider pool unresolved";
        private const string YatilaEvidence =
            "official-live capture 20260721-041439: exact Windcaller Yatila corpse snapshot with "
            + "424 credits and 275083 QL1, 204595 QL1, 204829 QL390, 204653 QL1, 204596 QL1";
        private const string GulardEvidence =
            "official-live capture 20260721-042139: two exact Reverend Gulard corpse snapshots, "
            + "each with 776 credits and 204750 QL1 x1";
        private const string ReAnimatorEvidence =
            "official-live capture 20260721-043204: exact The Re-Animator corpse snapshot with "
            + "2357 credits and 275083, 204598, 204708, 204698, all QL1 x1";
        private const string BetanyEvidence =
            "official-live capture 20260721-044256: exact Acolyte Betany corpse snapshot with "
            + "634 credits, 291082/291083 QL32 x50, 291043/291044 QL32 x25, and 204572 QL1 x1";

        internal static bool TryRegister(
            LootTableRegistry registry,
            string profileKey,
            string encounterKey)
        {
            if (registry == null)
            {
                return false;
            }

            LootTableDefinition table;
            string evidence;
            switch (profileKey)
            {
                case DefenderProfileKey:
                    table = BuildDefenderLootTable();
                    evidence = DefenderEvidence;
                    break;
                case YatilaProfileKey:
                    table = BuildYatilaLootTable();
                    evidence = YatilaEvidence;
                    break;
                case GulardProfileKey:
                    table = BuildGulardLootTable();
                    evidence = GulardEvidence;
                    break;
                case ReAnimatorProfileKey:
                    table = BuildReAnimatorLootTable();
                    evidence = ReAnimatorEvidence;
                    break;
                case BetanyProfileKey:
                    table = BuildBetanyLootTable();
                    evidence = BetanyEvidence;
                    break;
                default:
                    return false;
            }

            if (!registry.ContainsTable(table.LootTableKey))
            {
                registry.RegisterTable(table);
                registry.RegisterAssignment(
                    new LootAssignmentDefinition
                    {
                        AssignmentKey = table.LootTableKey,
                        TargetType = LootAssignmentTargetType.Boss,
                        TargetKey = profileKey,
                        LootTableKey = table.LootTableKey,
                        PlayfieldId = PlayfieldInstance,
                        EncounterKey = encounterKey,
                        Priority = 0,
                        Conditions = new string[0],
                        Evidence = evidence,
                        Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                        Enabled = true
                    });
            }

            return true;
        }

        internal static LootTableDefinition BuildDefenderLootTable()
        {
            return Table(
                DefenderProfileKey,
                "Defender of the Three captured corpse snapshots",
                DefenderEvidence,
                Snapshot(
                    "capture.20260721-035526-040249",
                    DefenderCredits,
                    DefenderEvidence,
                    Entry("capture.20260721-035526-040249", DefenderFirstItem, DefenderFirstItem, 1, 1, DefenderEvidence),
                    Entry("capture.20260721-035526-040249", DefenderSecondItem, DefenderSecondItem, 1, 1, DefenderEvidence)),
                Snapshot(
                    "capture.20260721-040324",
                    DefenderCredits,
                    DefenderEvidence,
                    Entry("capture.20260721-040324", DefenderFirstItem, DefenderFirstItem, 1, 2, DefenderEvidence),
                    Entry("capture.20260721-040324", DefenderSecondItem, DefenderSecondItem, 1, 1, DefenderEvidence)));
        }

        internal static LootTableDefinition BuildYatilaLootTable()
        {
            const string key = "capture.20260721-041439.yatila";
            return Table(
                YatilaProfileKey,
                "Windcaller Yatila captured corpse snapshot",
                YatilaEvidence,
                Snapshot(
                    key,
                    424,
                    YatilaEvidence,
                    Entry(key, 275083, 275083, 1, 1, YatilaEvidence),
                    Entry(key, 204595, 204595, 1, 1, YatilaEvidence),
                    Entry(key, 204829, 204829, 390, 1, YatilaEvidence),
                    Entry(key, 204653, 204653, 1, 1, YatilaEvidence),
                    Entry(key, 204596, 204596, 1, 1, YatilaEvidence)));
        }

        internal static LootTableDefinition BuildGulardLootTable()
        {
            return Table(
                GulardProfileKey,
                "Reverend Gulard captured corpse snapshots",
                GulardEvidence,
                Snapshot(
                    "capture.20260721-042139.gulard.1",
                    776,
                    GulardEvidence,
                    Entry("capture.20260721-042139.gulard.1", 204750, 204750, 1, 1, GulardEvidence)),
                Snapshot(
                    "capture.20260721-042139.gulard.2",
                    776,
                    GulardEvidence,
                    Entry("capture.20260721-042139.gulard.2", 204750, 204750, 1, 1, GulardEvidence)));
        }

        internal static LootTableDefinition BuildReAnimatorLootTable()
        {
            const string key = "capture.20260721-043204.re-animator";
            return Table(
                ReAnimatorProfileKey,
                "The Re-Animator captured corpse snapshot",
                ReAnimatorEvidence,
                Snapshot(
                    key,
                    2357,
                    ReAnimatorEvidence,
                    Entry(key, 275083, 275083, 1, 1, ReAnimatorEvidence),
                    Entry(key, 204598, 204598, 1, 1, ReAnimatorEvidence),
                    Entry(key, 204708, 204708, 1, 1, ReAnimatorEvidence),
                    Entry(key, 204698, 204698, 1, 1, ReAnimatorEvidence)));
        }

        internal static LootTableDefinition BuildBetanyLootTable()
        {
            const string key = "capture.20260721-044256.betany";
            return Table(
                BetanyProfileKey,
                "Acolyte Betany captured corpse snapshot",
                BetanyEvidence,
                Snapshot(
                    key,
                    634,
                    BetanyEvidence,
                    Entry(key, 291082, 291083, 32, 50, BetanyEvidence),
                    Entry(key, 291043, 291044, 32, 25, BetanyEvidence),
                    Entry(key, 204572, 204572, 1, 1, BetanyEvidence)));
        }

        private static LootTableDefinition Table(
            string profileKey,
            string displayName,
            string evidence,
            params ObservedCorpseSnapshotDefinition[] snapshots)
        {
            return new LootTableDefinition
            {
                LootTableKey = "captured." + profileKey,
                DisplayName = displayName,
                TableType = LootTableType.Boss,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = snapshots,
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                QualityPolicy = "captured-observed-corpse-snapshots",
                Evidence = evidence,
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static ObservedCorpseSnapshotDefinition Snapshot(
            string key,
            int credits,
            string evidence,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = key,
                Credits = credits,
                Entries = entries,
                Evidence = LootEvidenceConfidence.ProvenCapture,
                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,
                EvidenceReference = evidence + "; " + key
            };
        }

        private static LootEntryDefinition Entry(
            string snapshotKey,
            int lowItemId,
            int highItemId,
            int quality,
            int quantity,
            string evidence)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = lowItemId,
                HighItemTemplateId = highItemId,
                FixedQuality = quality,
                MinimumQuality = quality,
                MaximumQuality = quality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = evidence + "; " + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }
    }
}
