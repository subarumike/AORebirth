namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    [DeploymentItem(@".\XML Data\MissionLevels.csv", @"XML Data")]
    public class MissionLevelGraphTests
    {
        private const string CanonicalSourceSha256 =
            "393308fe4ac80f7513743aaedabaaaf5c372d081f15f9afc489b3c4df8c03b6a";

        private const string UpstreamOdsSha256 =
            "5efdba9a2e8310253246d82a9e733d90b32bb4b360a035c157f9d81832f4a0e7";

        [TestMethod]
        public void ValidCompleteGraphLoadsAndResolvesEveryLevelAndDifficulty()
        {
            MissionLevelGraph graph = LoadCanonical();

            for (int level = MissionLevelGraph.MinimumLevel;
                 level <= MissionLevelGraph.MaximumLevel;
                 level++)
            {
                for (int difficultyIndex = 0;
                     difficultyIndex < MissionLevelGraph.DifficultyCount;
                     difficultyIndex++)
                {
                    int missionQuality;
                    Assert.IsTrue(
                        graph.TryGetMissionQuality(
                            level,
                            difficultyIndex,
                            out missionQuality),
                        "Missing level "
                        + level
                        + " difficulty "
                        + difficultyIndex
                        + ".");
                    Assert.IsTrue(
                        missionQuality >= MissionLevelGraph.MinimumMissionQuality
                        && missionQuality
                           <= MissionLevelGraph.MaximumMissionQuality);
                }

                int neutralQuality;
                Assert.IsTrue(
                    graph.TryGetMissionQuality(level, 5, out neutralQuality));
                Assert.AreEqual(level, neutralQuality, "level " + level);

                int tokenCount;
                Assert.IsTrue(graph.TryGetTokenCount(level, out tokenCount));
                Assert.IsTrue(
                    tokenCount >= MissionLevelGraph.MinimumTokenCount
                    && tokenCount <= MissionLevelGraph.MaximumTokenCount);
            }

            int ignored;
            Assert.IsFalse(graph.TryGetMissionQuality(0, 0, out ignored));
            Assert.IsFalse(graph.TryGetMissionQuality(221, 0, out ignored));
            Assert.IsFalse(graph.TryGetMissionQuality(1, -1, out ignored));
            Assert.IsFalse(graph.TryGetMissionQuality(1, 11, out ignored));
        }

        [TestMethod]
        public void HelpbotAnchorsAndDerivedDetentsRemainExact()
        {
            MissionLevelGraphPublication publication = PublishCanonical();
            int[] levelFour =
            {
                2,
                3,
                3,
                3,
                3,
                4,
                4,
                4,
                5,
                6,
                7
            };

            for (int index = 0; index < levelFour.Length; index++)
            {
                Assert.AreEqual(
                    levelFour[index],
                    MissionLevelTable.GetRequiredMissionQualityForRoll(
                        publication,
                        4,
                        index + 1),
                    "level 4 wire difficulty " + (index + 1));
            }

            Assert.AreEqual(
                42,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    60,
                    1));
            Assert.AreEqual(
                18,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    10,
                    11));
            Assert.AreEqual(
                108,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    60,
                    11));
            Assert.AreEqual(
                60,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    77,
                    3));
            Assert.AreEqual(
                64,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    77,
                    4));
            Assert.AreEqual(
                91,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    77,
                    8));
            Assert.AreEqual(
                117,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    90,
                    9));
            Assert.AreEqual(
                122,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    112,
                    7));
            Assert.AreEqual(
                212,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    142,
                    10));
            Assert.AreEqual(
                250,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    149,
                    11));
        }

        [TestMethod]
        public void PublishedLevelOneHundredThreeMaximumDecreaseIsPreserved()
        {
            MissionLevelGraphPublication publication = PublishCanonical();
            Assert.AreEqual(
                186,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    102,
                    11));
            Assert.AreEqual(
                185,
                MissionLevelTable.GetRequiredMissionQualityForRoll(
                    publication,
                    103,
                    11));
        }

        [TestMethod]
        public void MissingLevelFailsClosed()
        {
            List<string> lines = CanonicalLines();
            lines.RemoveAt(60);

            AssertRejected(
                JoinCanonicalLines(lines),
                "missing one or more level rows");
        }

        [TestMethod]
        public void MissingDifficultyPositionAndMissingCellFailClosed()
        {
            List<string> lines = CanonicalLines();
            var header = new List<string>(lines[0].Split(','));
            header.RemoveAt(11);
            lines[0] = string.Join(",", header.ToArray());
            AssertRejected(
                JoinCanonicalLines(lines),
                "header is missing a difficulty column");

            lines = CanonicalLines();
            var row = new List<string>(lines[60].Split(','));
            row.RemoveAt(11);
            lines[60] = string.Join(",", row.ToArray());
            AssertRejected(
                JoinCanonicalLines(lines),
                "row with missing columns");
        }

        [TestMethod]
        public void DuplicateDifficultyCellFailsClosed()
        {
            List<string> lines = CanonicalLines();
            string[] header = lines[0].Split(',');
            header[11] = "Q9";
            lines[0] = string.Join(",", header);

            AssertRejected(
                JoinCanonicalLines(lines),
                "duplicate difficulty cell");
        }

        [TestMethod]
        public void DuplicateAndConflictingLevelRowsFailClosed()
        {
            List<string> duplicate = CanonicalLines();
            duplicate[220] = duplicate[219];
            AssertRejected(
                JoinCanonicalLines(duplicate),
                "duplicate level row");

            List<string> conflicting = CanonicalLines();
            string[] row = conflicting[220].Split(',');
            row[0] = "219";
            conflicting[220] = string.Join(",", row);
            AssertRejected(
                JoinCanonicalLines(conflicting),
                "conflicting duplicate level row");
        }

        [TestMethod]
        public void MalformedNumericTokensFailClosed()
        {
            AssertRejected(
                ReplaceCell(60, 1, "042"),
                "malformed mission-quality token");
            AssertRejected(
                ReplaceCell(60, 12, "3x"),
                "malformed token-count value");
            AssertRejected(
                ReplaceCell(60, 0, "+60"),
                "malformed level token");
        }

        [TestMethod]
        public void OutOfRangeLevelIndexQualityAndTokenFailClosed()
        {
            AssertRejected(
                ReplaceCell(1, 0, "0"),
                "out-of-range level");
            AssertRejected(
                ReplaceHeaderCell(11, "Q11"),
                "difficulty index is out of range");
            AssertRejected(
                ReplaceCell(60, 1, "0"),
                "out-of-range mission quality");
            AssertRejected(
                ReplaceCell(60, 11, "251"),
                "out-of-range mission quality");
            AssertRejected(
                ReplaceCell(60, 12, "0"),
                "out-of-range token count");
            AssertRejected(
                ReplaceCell(60, 12, "10"),
                "out-of-range token count");
        }

        [TestMethod]
        public void UnexpectedExtraRowAndColumnsFailClosed()
        {
            List<string> extraRow = CanonicalLines();
            extraRow.Add(extraRow[220]);
            AssertRejected(
                JoinCanonicalLines(extraRow),
                "unexpected extra row");

            List<string> extraHeaderColumn = CanonicalLines();
            extraHeaderColumn[0] += ",Unexpected";
            AssertRejected(
                JoinCanonicalLines(extraHeaderColumn),
                "unexpected extra column");

            List<string> extraRowColumn = CanonicalLines();
            extraRowColumn[60] += ",0";
            AssertRejected(
                JoinCanonicalLines(extraRowColumn),
                "unexpected extra column");
        }

        [TestMethod]
        public void MalformedHeaderFailsClosed()
        {
            AssertRejected(
                ReplaceHeaderCell(0, "level"),
                "header is malformed");
            AssertRejected(
                ReplaceHeaderCell(12, "Token"),
                "header is malformed");
            AssertRejected(
                ReplaceHeaderCell(1, "Difficulty0"),
                "difficulty header is malformed");
        }

        [TestMethod]
        public void TruncatedAndEmptyPayloadsFailClosed()
        {
            string truncated = MissionLevelGraphData.CanonicalCsv.Substring(
                0,
                MissionLevelGraphData.CanonicalCsv.Length - 1);
            AssertRejected(
                truncated,
                "truncated or lacks its terminal newline");

            AssertRejectedWithKnownHash(
                string.Empty,
                MissionLevelGraphData.CanonicalPayloadSha256,
                "payload is empty");
            AssertRejectedWithKnownHash(
                null,
                MissionLevelGraphData.CanonicalPayloadSha256,
                "payload is empty");
        }

        [TestMethod]
        public void PayloadAndMetadataHashValidationFailsClosed()
        {
            AssertRejectedWithKnownHash(
                MissionLevelGraphData.CanonicalCsv,
                new string('0', 64),
                "payload SHA-256 does not match");

            MissionLevelGraph graph;
            string failure;
            Assert.IsFalse(
                MissionLevelGraphLoader.TryLoad(
                    MissionLevelGraphData.CanonicalCsv,
                    MissionLevelGraphData.CanonicalPayloadSha256,
                    "not-a-sha256",
                    "test source",
                    out graph,
                    out failure));
            Assert.IsNull(graph);
            StringAssert.Contains(failure, "source hash metadata is malformed");
        }

        [TestMethod]
        public void ImpossibleRowSemanticValuesFailClosed()
        {
            AssertRejected(
                ReplaceCell(60, 2, "41"),
                "decreases across difficulty positions");
            AssertRejected(
                ReplaceCell(60, 6, "61"),
                "impossible neutral-difficulty value");
            AssertRejected(
                ReplaceCell(77, 12, "3"),
                "token counts decrease between levels");
        }

        [TestMethod]
        public void CanonicalSerializationAndGenerationAreDeterministic()
        {
            MissionLevelGraph first = LoadCanonical();
            MissionLevelGraph second = LoadCanonical();

            Assert.AreEqual(
                MissionLevelGraphData.CanonicalCsv,
                first.SerializeCanonicalCsv());
            Assert.AreEqual(
                first.SerializeCanonicalCsv(),
                second.SerializeCanonicalCsv());
            Assert.AreEqual(
                MissionLevelGraphData.CanonicalPayloadSha256,
                MissionLevelGraphLoader.ComputeSha256(
                    first.SerializeCanonicalCsv()));
            Assert.AreEqual(first.PayloadSha256, second.PayloadSha256);
            Assert.AreEqual(first.SourceSha256, second.SourceSha256);
        }

        [TestMethod]
        public void CanonicalSourceHashAndDeployedCsvNormalizationRemainStable()
        {
            Assert.AreEqual(
                CanonicalSourceSha256,
                MissionLevelGraphData.SourceSha256);
            Assert.AreEqual(
                CanonicalSourceSha256,
                MissionLevelGraphData.CanonicalPayloadSha256);
            Assert.AreEqual(
                CanonicalSourceSha256,
                MissionLevelGraphLoader.ComputeSha256(
                    MissionLevelGraphData.CanonicalCsv));
            Assert.AreEqual(
                UpstreamOdsSha256,
                MissionLevelGraphData.UpstreamOdsSha256);
            Assert.AreEqual(
                "docs/evidence/data/helpbot-mission-ql-levels-1-149.json",
                MissionLevelGraphData.HelpbotReferenceRepositoryPath);
            Assert.AreEqual(
                "f8841253af7ed9b63aa2d9d1a2d48e487239b4f8e44e57b225cc7b3855c04488",
                MissionLevelGraphData.HelpbotReferenceRawSha256);

            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "XML Data",
                "MissionLevels.csv");
            Assert.IsTrue(File.Exists(path), path);
            string source = File.ReadAllText(path);
            string normalized = source
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            if (normalized.Length > 0 && normalized[0] == '\uFEFF')
            {
                normalized = normalized.Substring(1);
            }

            Assert.AreEqual(MissionLevelGraphData.CanonicalCsv, normalized);
            Assert.AreEqual(
                CanonicalSourceSha256,
                MissionLevelGraphLoader.ComputeSha256(normalized));
        }

        [TestMethod]
        public void FailedPublicationNeverExposesPartialGraph()
        {
            var emptyPublication = new MissionLevelGraphPublication();
            string failure;
            Assert.IsFalse(
                emptyPublication.TryPublish(
                    ReplaceCell(60, 1, "0"),
                    MissionLevelGraphLoader.ComputeSha256(
                        ReplaceCell(60, 1, "0")),
                    MissionLevelGraphData.SourceSha256,
                    "invalid test graph",
                    out failure));

            MissionLevelGraph graph;
            Assert.IsFalse(emptyPublication.TryGet(out graph, out failure));
            Assert.IsNull(graph);

            MissionLevelGraphPublication publication = PublishCanonical();
            MissionLevelGraph before;
            Assert.IsTrue(publication.TryGet(out before, out failure), failure);

            string invalid = ReplaceCell(60, 1, "0");
            Assert.IsFalse(
                publication.TryPublish(
                    invalid,
                    MissionLevelGraphLoader.ComputeSha256(invalid),
                    MissionLevelGraphData.SourceSha256,
                    "invalid replacement",
                    out failure));

            MissionLevelGraph after;
            Assert.IsTrue(publication.TryGet(out after, out failure), failure);
            Assert.AreSame(before, after);
            int missionQuality;
            Assert.IsTrue(after.TryGetMissionQuality(60, 0, out missionQuality));
            Assert.AreEqual(42, missionQuality);
        }

        [TestMethod]
        public void ConcurrentReadersRetainValidSnapshotDuringFailedReload()
        {
            MissionLevelGraphPublication publication = PublishCanonical();
            string invalid = ReplaceCell(60, 1, "0");
            string invalidHash =
                MissionLevelGraphLoader.ComputeSha256(invalid);
            int readFailures = 0;
            var start = new ManualResetEvent(false);
            var readers = new Thread[4];

            for (int readerIndex = 0;
                 readerIndex < readers.Length;
                 readerIndex++)
            {
                readers[readerIndex] =
                    new Thread(
                        new ThreadStart(
                        delegate
                        {
                            start.WaitOne();
                            for (int iteration = 0;
                                 iteration < 2000;
                                 iteration++)
                            {
                                MissionLevelGraph snapshot;
                                string readFailure;
                                int missionQuality;
                                if (!publication.TryGet(
                                        out snapshot,
                                        out readFailure)
                                    || !snapshot.TryGetMissionQuality(
                                        60,
                                        0,
                                        out missionQuality)
                                    || missionQuality != 42)
                                {
                                    Interlocked.Increment(
                                        ref readFailures);
                                }
                            }
                        }));
                readers[readerIndex].IsBackground = true;
                readers[readerIndex].Start();
            }

            start.Set();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                string failure;
                Assert.IsFalse(
                    publication.TryPublish(
                        invalid,
                        invalidHash,
                        MissionLevelGraphData.SourceSha256,
                        "invalid concurrent replacement",
                        out failure));
            }

            for (int readerIndex = 0;
                 readerIndex < readers.Length;
                 readerIndex++)
            {
                Assert.IsTrue(readers[readerIndex].Join(10000));
            }

            Assert.AreEqual(0, readFailures);
        }

        [TestMethod]
        public void MissionRollFailsExplicitlyWithoutAValidGraph()
        {
            var publication = new MissionLevelGraphPublication();
            try
            {
                MissionRollService.ResolveMissionQualityForRoll(
                    publication,
                    60,
                    1);
                Assert.Fail(
                    "A mission roll without a valid graph must fail closed.");
            }
            catch (InvalidOperationException exception)
            {
                StringAssert.Contains(
                    exception.Message,
                    "official mission-level graph is unavailable or invalid");
            }
        }

        private static MissionLevelGraph LoadCanonical()
        {
            MissionLevelGraph graph;
            string failure;
            Assert.IsTrue(
                MissionLevelGraphLoader.TryLoad(
                    MissionLevelGraphData.CanonicalCsv,
                    MissionLevelGraphData.CanonicalPayloadSha256,
                    MissionLevelGraphData.SourceSha256,
                    MissionLevelGraphData.SourceRepositoryPath,
                    out graph,
                    out failure),
                failure);
            Assert.IsNotNull(graph);
            return graph;
        }

        private static MissionLevelGraphPublication PublishCanonical()
        {
            var publication = new MissionLevelGraphPublication();
            string failure;
            Assert.IsTrue(
                publication.TryPublish(
                    MissionLevelGraphData.CanonicalCsv,
                    MissionLevelGraphData.CanonicalPayloadSha256,
                    MissionLevelGraphData.SourceSha256,
                    MissionLevelGraphData.SourceRepositoryPath,
                    out failure),
                failure);
            return publication;
        }

        private static void AssertRejected(
            string canonicalCsv,
            string expectedFailure)
        {
            AssertRejectedWithKnownHash(
                canonicalCsv,
                MissionLevelGraphLoader.ComputeSha256(canonicalCsv),
                expectedFailure);
        }

        private static void AssertRejectedWithKnownHash(
            string canonicalCsv,
            string expectedHash,
            string expectedFailure)
        {
            MissionLevelGraph graph;
            string failure;
            Assert.IsFalse(
                MissionLevelGraphLoader.TryLoad(
                    canonicalCsv,
                    expectedHash,
                    MissionLevelGraphData.SourceSha256,
                    "malformed test graph",
                    out graph,
                    out failure));
            Assert.IsNull(graph);
            StringAssert.Contains(failure, expectedFailure);
        }

        private static string ReplaceCell(
            int levelLine,
            int column,
            string value)
        {
            List<string> lines = CanonicalLines();
            string[] cells = lines[levelLine].Split(',');
            cells[column] = value;
            lines[levelLine] = string.Join(",", cells);
            return JoinCanonicalLines(lines);
        }

        private static string ReplaceHeaderCell(int column, string value)
        {
            List<string> lines = CanonicalLines();
            string[] cells = lines[0].Split(',');
            cells[column] = value;
            lines[0] = string.Join(",", cells);
            return JoinCanonicalLines(lines);
        }

        private static List<string> CanonicalLines()
        {
            string csv = MissionLevelGraphData.CanonicalCsv;
            return new List<string>(
                csv.Substring(0, csv.Length - 1).Split('\n'));
        }

        private static string JoinCanonicalLines(IList<string> lines)
        {
            var copy = new string[lines.Count];
            for (int index = 0; index < lines.Count; index++)
            {
                copy[index] = lines[index];
            }

            return string.Join("\n", copy) + "\n";
        }
    }
}
