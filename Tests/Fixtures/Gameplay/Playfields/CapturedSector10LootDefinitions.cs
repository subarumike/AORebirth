namespace AORebirth.Core.Playfields
{
    using System;

    internal static class CapturedSector10LootDefinitions
    {
        internal const int MonsterData = 257313;
        internal const int BossLevel = 190;
        internal const int CorpseCredits = 35507;

        internal const string IlariProfileKey = "sector10.4474.boss.ilari-khazoh-ra";
        internal const string AnkariProfileKey = "sector10.4474.boss.ankari-khazoh-ra";
        internal const string ChaProfileKey = "sector10.4474.boss.cha-khazoh-ra";

        private const string IlariEvidence =
            "identity-linked PF4474 captures 20260830-041457, 20260830-041643, "
            + "20260830-041816, 20260830-041950, and 20260830-042105";
        private const string AnkariEvidence =
            "identity-linked PF4474 captures 20260830-042257, 20260830-042449, "
            + "20260830-042700, and 20260830-042926";
        private const string ChaEvidence =
            "identity-linked PF4474 capture 20260830-043059";

        internal static bool TryRegister(
            LootTableRegistry registry,
            string enemyName,
            int monsterData,
            int level,
            out string profileKey)
        {
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            LootTableDefinition table;
            if (!TryResolveProfile(enemyName, monsterData, level, out profileKey))
            {
                return false;
            }

            switch (profileKey)
            {
                case IlariProfileKey:
                    table = BuildIlariLootTable();
                    break;
                case AnkariProfileKey:
                    table = BuildAnkariLootTable();
                    break;
                case ChaProfileKey:
                    table = BuildChaLootTable();
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
                        Priority = 0,
                        Conditions = new string[0],
                        Evidence = table.Evidence,
                        Confidence = LootEvidenceConfidence.ProvenCapture,
                        Enabled = true
                    });
            }

            return true;
        }

        internal static bool TryResolveProfile(
            string enemyName,
            int monsterData,
            int level,
            out string profileKey)
        {
            profileKey = null;
            if (monsterData != MonsterData || level != BossLevel)
            {
                return false;
            }

            if (string.Equals(enemyName, "Ilari Khazoh Ra", StringComparison.OrdinalIgnoreCase))
            {
                profileKey = IlariProfileKey;
            }
            else if (string.Equals(enemyName, "Ankari Khazoh Ra", StringComparison.OrdinalIgnoreCase))
            {
                profileKey = AnkariProfileKey;
            }
            else if (string.Equals(enemyName, "Cha Khazoh Ra", StringComparison.OrdinalIgnoreCase))
            {
                profileKey = ChaProfileKey;
            }

            return profileKey != null;
        }

        internal static LootTableDefinition BuildIlariLootTable()
        {
            return Table(
                IlariProfileKey,
                "Ilari Khazoh Ra captured corpse snapshots",
                IlariEvidence,
                Snapshot(
                    "capture.20260830-041457.ilari",
                    IlariEvidence,
                    Entry("capture.20260830-041457.ilari", 287147, 287147, 200, 1, IlariEvidence),
                    Entry("capture.20260830-041457.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041457.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041457.ilari", 268494, 268494, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041457.ilari", 268510, 268510, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041457.ilari", 268477, 268477, 150, 1, IlariEvidence)),
                Snapshot(
                    "capture.20260830-041643.ilari",
                    IlariEvidence,
                    Entry("capture.20260830-041643.ilari", 287147, 287147, 200, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 268507, 268507, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 268499, 268499, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 268493, 268493, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041643.ilari", 268477, 268477, 150, 1, IlariEvidence)),
                Snapshot(
                    "capture.20260830-041816.ilari",
                    IlariEvidence,
                    Entry("capture.20260830-041816.ilari", 287147, 287147, 200, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 247140, 247141, 222, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 268510, 268510, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 268496, 268496, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041816.ilari", 268499, 268499, 150, 1, IlariEvidence)),
                Snapshot(
                    "capture.20260830-041950.ilari",
                    IlariEvidence,
                    Entry("capture.20260830-041950.ilari", 287147, 287147, 200, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 268507, 268507, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 268494, 268494, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 268493, 268493, 150, 1, IlariEvidence),
                    Entry("capture.20260830-041950.ilari", 268499, 268499, 150, 1, IlariEvidence)),
                Snapshot(
                    "capture.20260830-042105.ilari",
                    IlariEvidence,
                    Entry("capture.20260830-042105.ilari", 287147, 287147, 200, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 257968, 257968, 1, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 247140, 247141, 162, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 268494, 268494, 150, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 268494, 268494, 150, 1, IlariEvidence),
                    Entry("capture.20260830-042105.ilari", 268493, 268493, 150, 1, IlariEvidence)));
        }

        internal static LootTableDefinition BuildAnkariLootTable()
        {
            return Table(
                AnkariProfileKey,
                "Ankari Khazoh Ra captured corpse snapshots",
                AnkariEvidence,
                Snapshot(
                    "capture.20260830-042257.ankari",
                    AnkariEvidence,
                    Entry("capture.20260830-042257.ankari", 287147, 287147, 200, 1, AnkariEvidence),
                    Entry("capture.20260830-042257.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042257.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042257.ankari", 268477, 268477, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042257.ankari", 268493, 268493, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042257.ankari", 268510, 268510, 150, 1, AnkariEvidence)),
                Snapshot(
                    "capture.20260830-042449.ankari",
                    AnkariEvidence,
                    Entry("capture.20260830-042449.ankari", 287147, 287147, 200, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 247138, 247139, 190, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 268507, 268507, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 268496, 268496, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 268499, 268499, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042449.ankari", 268510, 268510, 150, 1, AnkariEvidence)),
                Snapshot(
                    "capture.20260830-042700.ankari",
                    AnkariEvidence,
                    Entry("capture.20260830-042700.ankari", 287147, 287147, 200, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 247144, 247145, 163, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 268507, 268507, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 268499, 268499, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 268499, 268499, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042700.ankari", 268496, 268496, 150, 1, AnkariEvidence)),
                Snapshot(
                    "capture.20260830-042926.ankari",
                    AnkariEvidence,
                    Entry("capture.20260830-042926.ankari", 287147, 287147, 200, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 257968, 257968, 1, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 247136, 247137, 157, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 268496, 268496, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 268493, 268493, 150, 1, AnkariEvidence),
                    Entry("capture.20260830-042926.ankari", 268499, 268499, 150, 1, AnkariEvidence)));
        }

        internal static LootTableDefinition BuildChaLootTable()
        {
            const string key = "capture.20260830-043059.cha";
            return Table(
                ChaProfileKey,
                "Cha Khazoh Ra captured corpse snapshot",
                ChaEvidence,
                Snapshot(
                    key,
                    ChaEvidence,
                    Entry(key, 287147, 287147, 200, 1, ChaEvidence),
                    Entry(key, 257968, 257968, 1, 1, ChaEvidence),
                    Entry(key, 257968, 257968, 1, 1, ChaEvidence),
                    Entry(key, 268507, 268507, 150, 1, ChaEvidence),
                    Entry(key, 268496, 268496, 150, 1, ChaEvidence),
                    Entry(key, 268496, 268496, 150, 1, ChaEvidence),
                    Entry(key, 268510, 268510, 150, 1, ChaEvidence)));
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
                Confidence = LootEvidenceConfidence.ProvenCapture,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static ObservedCorpseSnapshotDefinition Snapshot(
            string key,
            string evidence,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = key,
                Credits = CorpseCredits,
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
                UniquePerCorpse = false,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = evidence + "; " + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }
    }
}
