namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    #endregion

    [TestClass]
    public class TempleAcceptanceMatrixTests
    {
        private const string MatrixPath =
            @"docs\evidence\TEMPLE_FULL_CORPUS_COMPLETION_20260801.md";

        [TestMethod]
        public void MatrixPreservesAcceptedTotalsAndExactFailClosedBoundaries()
        {
            string matrix = Read(MatrixPath);

            AssertContainsAll(
                matrix,
                "Temple full-corpus acceptance matrix",
                "current Temple status authority",
                "Capture sessions discovered | `381`",
                "Canonical-valid sessions | `365`",
                "Complete combat chains | `3,269`",
                "`temple-ordinary` | `167` | `167` | `0`",
                "`temple-named-encounters` | `12` | `12` | `0`",
                "`temple-reanimated-corpse-adds` | `2` | `2` | `0`",
                "**PF1931 total** | **181** | **181** | **0**",
                "**Accepted - 30/30 rooms**",
                "**Accepted - 43/43 internal**",
                "**Accepted absence - no synthetic vendor**",
                "**Accepted absence - no invented dialogue**",
                "**Accepted absence - no invented quest/mission**",
                @"tools\run_temple_acceptance_tests.cmd");

            AssertContainsAll(
                matrix,
                "Temple fail-closed contracts",
                "Attack-skill versus Nano Resist selection",
                "hostile AreaCast recipients",
                "stun/RestrictAction behavior",
                "Uklesh proc probability",
                "Murial ally selector/cadence",
                "Murial and Reanimated Corpse loot outcomes",
                "Official loot probabilities and unseen wider pools",
                "No valid Temple observation was rejected");
        }

        [TestMethod]
        public void DeterministicRunnerOwnsEveryTempleAcceptanceSurface()
        {
            string runner = Read(@"tools\run_temple_acceptance_tests.cmd");

            AssertContainsAll(
                runner,
                "Temple acceptance runner",
                "TempleAcceptanceMatrixTests",
                "Pf1931CoverageIncludesEveryOrdinaryNamedSuccessorAndOwnedAdd",
                "TempleOfThreeWindsOrdinaryContentTests",
                "DungeonNamedEncounterCompletionTests",
                "DungeonNamedLifecycleCompletionTests",
                "TempleDoorStatusRuntimeTests",
                "PlayfieldCollisionGeometryTests",
                "NpcChaseNavigationTests",
                "OfficialDungeonNavigationTests",
                "N3RecoveredContractTests",
                "PlayfieldRuntimeOwnershipTests",
                "GlobalLootFoundationTests");
        }

        [TestMethod]
        public void EarlierTempleReportsDeferCurrentStatusToTheMatrix()
        {
            string[] evidenceDocuments =
            {
                "FINAL_ORDINARY_DUNGEON_COMBAT_COMPLETION_20260728.md",
                "DUNGEON_GAMEPLAY_COMPLETION_20260728.md",
                "DUNGEON_NAMED_ENCOUNTER_COMPLETION_20260728.md",
                "DUNGEON_NAMED_LIFECYCLE_COMPLETION_20260729.md",
                "DUNGEON_NANO_LOOT_CONTRACT_COMPLETION_20260730.md",
                "TEMPLE_ORDINARY_COMBAT_COMPLETION_20260728.md",
                "TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md",
                "TEMPLE_OF_THREE_WINDS_20260721_ENTRANCE_TO_FIRST_BOSS.md",
                "TEMPLE_OF_THREE_WINDS_20260721_GUARDIAN_GARTUA_MAIN_ROOM.md",
                "TEMPLE_OF_THREE_WINDS_20260721_MURIAL_PATROL.md",
                "TEMPLE_OF_THREE_WINDS_20260721_YATILA_TO_BETANY.md",
                "TEMPLE_OF_THREE_WINDS_20260721_CURATOR_AND_NEMATET.md",
                "TEMPLE_OF_THREE_WINDS_20260721_DEFENDER_OF_THE_THREE.md",
                "PF1931_TEMPLE_NANO_GAMEPLAY_COMPLETION_20260730.md",
                "PF1931_TEMPLE_EXISTING_CORPUS_CONTINUATION_20260731.md",
                "PF1931_TEMPLE_DYNAMIC_DOORS_20260731.md",
                "PF1931_TEMPLE_WORLD_INTERACTIONS_20260731.md"
            };

            foreach (string document in evidenceDocuments)
            {
                string report = Read(Path.Combine("docs", "evidence", document));
                StringAssert.Contains(report, "PF1931 status authority (2026-08-01)", document);
                StringAssert.Contains(report, "TEMPLE_FULL_CORPUS_COMPLETION_20260801.md", document);
            }
        }

        private static string Read(string relativePath)
        {
            string path = Path.Combine(FindRepositoryRoot(), relativePath);
            Assert.IsTrue(File.Exists(path), "Missing PF1931 acceptance artifact: " + relativePath);
            return File.ReadAllText(path);
        }

        private static void AssertContainsAll(string source, string owner, params string[] values)
        {
            foreach (string value in values)
            {
                StringAssert.Contains(source, value, owner + " is missing " + value + ".");
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
