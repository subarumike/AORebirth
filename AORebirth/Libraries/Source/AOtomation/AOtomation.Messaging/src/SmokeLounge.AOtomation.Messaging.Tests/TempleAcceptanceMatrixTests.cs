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
            @"docs\evidence\PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md";

        [TestMethod]
        public void MatrixPreservesAcceptedTotalsAndExactFailClosedBoundaries()
        {
            string matrix = Read(MatrixPath);

            AssertContainsAll(
                matrix,
                "PF1931 acceptance totals",
                "sole authoritative status document",
                "`167/167` ordinary actor slots; `14/14` named lifecycle/combat domains",
                "`167/167` PF1931 actors resolve exact active contracts",
                "`14/14` PF1931 domains",
                "`3` gameplay contracts; `3` explicit active-domain no-nano classifications",
                "`20` actor/family contracts",
                "`21` observed outcome families: `9` ordinary plus `12` named",
                "`2` domains have no proven outcome",
                "`30/30` official rooms",
                "`43/43` internal doors plus exterior EntryHall statel `C024078B`",
                "PF647 `C0080287` entry and PF1931 `C024078B` exit",
                "PF1931 Temple of Three Winds is complete for the existing evidence corpus.");

            AssertContainsAll(
                matrix,
                "PF1931 fail-closed contracts",
                "Authoritative attack-skill versus Nano Resist resolution",
                "Hostile AreaCast recipients",
                "generic stun/action-lock semantics",
                "Proven landed-hit proc probability",
                "Categorical missing-buff ally/self selector and safe cadence",
                "Generation selector and resist resolution for hostile damage",
                "Reanimated Corpse adds and Murial",
                "`Weight=0`, `DropChanceBasisPoints=0`, and",
                "No fail-closed row creates a nano");
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
                StringAssert.Contains(report, "PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md", document);
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
