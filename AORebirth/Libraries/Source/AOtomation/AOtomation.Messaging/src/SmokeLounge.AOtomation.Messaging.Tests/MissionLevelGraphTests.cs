namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine.Core.Missions;

    // Behavioral checks exercise NewEngine's editable CSV reader; compiled hashes and publication objects are retired.
    [TestClass]
    public class MissionLevelGraphTests
    {
        static string SourcePath => Path.Combine(AppContext.BaseDirectory, "GameData", "Missions", "Source", "MissionLevels.csv");

        [TestMethod]
        public void ValidCompleteGraphLoadsAndResolvesEveryLevelAndDifficulty()
        {
            var data = MissionLevelData.Load(SourcePath);
            for (int level = 1; level <= 220; level++)
            {
                for (int wire = 1; wire <= 11; wire++)
                {
                    int quality = data.Quality(level, wire);
                    Assert.IsTrue(quality >= 1 && quality <= 250);
                }
                Assert.AreEqual(level, data.Quality(level, 6));
                Assert.IsTrue(data.Tokens(level) >= 1 && data.Tokens(level) <= 9);
            }
            Assert.AreEqual(data.Quality(1, 1), data.Quality(0, 1));
            Assert.AreEqual(data.Quality(220, 1), data.Quality(221, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => data.Quality(1, 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => data.Quality(1, 12));
        }

        [TestMethod]
        public void LevelFourAndCapturedLevelSixtyRowsRemainExact()
        {
            var data = MissionLevelData.Load(SourcePath);
            int[] expected = { 2, 3, 3, 3, 3, 4, 4, 4, 5, 6, 7 };
            for (int index = 0; index < expected.Length; index++) Assert.AreEqual(expected[index], data.Quality(4, index + 1));
            Assert.AreEqual(42, data.Quality(60, 1));
        }

        [TestMethod]
        public void MissingLevelFailsClosed()
        {
            var lines = CanonicalLines();
            lines.RemoveAt(60);
            AssertRejected(JoinCanonicalLines(lines));
        }

        [TestMethod]
        public void MissingDifficultyPositionAndMissingCellFailClosed()
        {
            var lines = CanonicalLines();
            var header = new List<string>(lines[0].Split(','));
            header.RemoveAt(11);
            lines[0] = string.Join(",", header);
            AssertRejected(JoinCanonicalLines(lines));
            lines = CanonicalLines();
            var row = new List<string>(lines[60].Split(','));
            row.RemoveAt(11);
            lines[60] = string.Join(",", row);
            AssertRejected(JoinCanonicalLines(lines));
        }

        [TestMethod]
        public void DuplicateDifficultyCellFailsClosed() => AssertRejected(ReplaceCell(0, 11, "Q9"));

        [TestMethod]
        public void DuplicateAndConflictingLevelRowsFailClosed()
        {
            var lines = CanonicalLines();
            lines[220] = lines[219];
            AssertRejected(JoinCanonicalLines(lines));
            AssertRejected(ReplaceCell(220, 0, "219"));
        }

        [TestMethod]
        public void MalformedNumericTokensFailClosed()
        {
            AssertRejected(ReplaceCell(60, 1, "042"));
            AssertRejected(ReplaceCell(60, 12, "3x"));
            AssertRejected(ReplaceCell(60, 0, "+60"));
        }

        [TestMethod]
        public void OutOfRangeLevelIndexQualityAndTokenFailClosed()
        {
            AssertRejected(ReplaceCell(1, 0, "0"));
            AssertRejected(ReplaceCell(0, 11, "Q11"));
            AssertRejected(ReplaceCell(60, 1, "0"));
            AssertRejected(ReplaceCell(60, 11, "251"));
            AssertRejected(ReplaceCell(60, 12, "0"));
            AssertRejected(ReplaceCell(60, 12, "10"));
        }

        [TestMethod]
        public void UnexpectedExtraRowAndColumnsFailClosed()
        {
            var lines = CanonicalLines();
            lines.Add(lines[220]);
            AssertRejected(JoinCanonicalLines(lines));
            lines = CanonicalLines();
            lines[0] += ",Unexpected";
            AssertRejected(JoinCanonicalLines(lines));
            lines = CanonicalLines();
            lines[60] += ",0";
            AssertRejected(JoinCanonicalLines(lines));
        }

        [TestMethod]
        public void MalformedHeaderFailsClosed()
        {
            AssertRejected(ReplaceCell(0, 0, "level"));
            AssertRejected(ReplaceCell(0, 12, "Token"));
            AssertRejected(ReplaceCell(0, 1, "Difficulty0"));
        }

        [TestMethod]
        public void TruncatedAndEmptyPayloadsFailClosed()
        {
            string complete = JoinCanonicalLines(CanonicalLines());
            AssertRejected(complete.Substring(0, complete.LastIndexOf(',')));
            AssertRejected(string.Empty);
        }

        [TestMethod]
        public void ImpossibleSemanticValuesFailClosed()
        {
            AssertRejected(ReplaceCell(60, 2, "41"));
            AssertRejected(ReplaceCell(60, 6, "61"));
            AssertRejected(ReplaceCell(61, 1, "41"));
            AssertRejected(ReplaceCell(77, 12, "3"));
        }

        [TestMethod]
        public void MissionRollFailsExplicitlyWithoutAValidGraph()
        {
            // No quality can be resolved when the source fails validation.
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, string.Empty);
                Assert.ThrowsExactly<InvalidDataException>(() => MissionLevelData.Load(path).Quality(60, 1));
            }
            finally { File.Delete(path); }
        }

        static void AssertRejected(string csv)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, csv);
                try { MissionLevelData.Load(path); }
                catch (Exception error) when (error is InvalidDataException || error is FormatException || error is OverflowException) { return; }
                Assert.Fail("Malformed mission-level content was accepted.");
            }
            finally { File.Delete(path); }
        }

        static string ReplaceCell(int line, int column, string value)
        {
            var lines = CanonicalLines();
            var cells = lines[line].Split(',');
            cells[column] = value;
            lines[line] = string.Join(",", cells);
            return JoinCanonicalLines(lines);
        }
        static List<string> CanonicalLines() => new List<string>(File.ReadAllLines(SourcePath));
        static string JoinCanonicalLines(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";
    }
}
