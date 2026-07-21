namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.IO;

    using AORebirth.Stats.SpecialStats;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    #endregion

    [TestClass]
    public class DailyMissionRewardRulesTests
    {
        [TestMethod]
        public void FullLevelXpUsesAuthoritativeRubikaTable()
        {
            Assert.AreEqual(1450, DailyMissionRewardRules.GetFullRubikaLevelXpReward(1));
            Assert.AreEqual(21500, DailyMissionRewardRules.GetFullRubikaLevelXpReward(25));
            Assert.AreEqual(203500, DailyMissionRewardRules.GetFullRubikaLevelXpReward(60));
            Assert.AreEqual(0, DailyMissionRewardRules.GetFullRubikaLevelXpReward(0));
            Assert.AreEqual(0, DailyMissionRewardRules.GetFullRubikaLevelXpReward(200));
        }

        [TestMethod]
        public void FullLevelRewardCarriesExistingProgressAcrossExactlyOneLevel()
        {
            foreach (int level in new[] { 25, 60 })
            {
                const int ExistingProgress = 1234;
                int floor = Convert.ToInt32(XPTable.TableRKXP[level - 1, 1]);
                int nextFloor = Convert.ToInt32(XPTable.TableRKXP[level, 1]);
                int reward = DailyMissionRewardRules.GetFullRubikaLevelXpReward(level);
                int cumulativeAfterReward = floor + ExistingProgress + reward;

                Assert.AreEqual(nextFloor, floor + reward);
                Assert.AreEqual(ExistingProgress, cumulativeAfterReward - nextFloor);
                Assert.IsTrue(
                    ExistingProgress < DailyMissionRewardRules.GetFullRubikaLevelXpReward(level + 1));
            }
        }

        [TestMethod]
        public void DailyRewardAppliesTwoTokensForEveryReachedLevelTier()
        {
            int[,] expectations =
            {
                { 1, 2 },
                { 14, 2 },
                { 15, 4 },
                { 25, 4 },
                { 49, 4 },
                { 50, 6 },
                { 60, 6 },
                { 74, 6 },
                { 75, 8 },
                { 99, 8 },
                { 100, 10 },
                { 124, 10 },
                { 125, 12 },
                { 149, 12 },
                { 150, 14 },
                { 174, 14 },
                { 175, 16 },
                { 189, 16 },
                { 190, 18 },
                { 220, 18 }
            };

            for (int row = 0; row < expectations.GetLength(0); row++)
            {
                int level = expectations[row, 0];
                int expectedReward = expectations[row, 1];
                Assert.AreEqual(expectedReward, DailyMissionRewardRules.GetSideTokenReward(level, 1));
                Assert.AreEqual(expectedReward, DailyMissionRewardRules.GetSideTokenReward(level, 2));
            }

            Assert.AreEqual(0, DailyMissionRewardRules.GetSideTokenReward(0, 1));
            Assert.AreEqual(0, DailyMissionRewardRules.GetSideTokenReward(221, 2));
            Assert.AreEqual(0, DailyMissionRewardRules.GetSideTokenReward(60, 0));
        }

        [TestMethod]
        public void SideSelectsTheCorrectTokenCounter()
        {
            int statId;
            Assert.IsTrue(DailyMissionRewardRules.TryGetSideTokenStatId(1, out statId));
            Assert.AreEqual(62, statId);
            Assert.IsTrue(DailyMissionRewardRules.TryGetSideTokenStatId(2, out statId));
            Assert.AreEqual(75, statId);
            Assert.IsFalse(DailyMissionRewardRules.TryGetSideTokenStatId(0, out statId));
            Assert.AreEqual(-1, statId);
        }

        [TestMethod]
        public void CompletionSnapshotFreezesTierAndNeutralEligibilityAcrossRetry()
        {
            DailyMissionRewardSnapshot levelFourteen;
            Assert.IsTrue(DailyMissionRewardRules.TryCreateCompletionSnapshot(14, 2, out levelFourteen));
            string serialized = DailyMissionRewardRules.SerializeCompletionSnapshot(levelFourteen);

            DailyMissionRewardSnapshot retry;
            Assert.IsTrue(DailyMissionRewardRules.TryParseCompletionSnapshot(serialized, out retry));
            Assert.AreEqual(14, retry.LevelBefore);
            Assert.AreEqual(75, retry.SideTokenStatId);
            Assert.AreEqual(2, retry.SideTokenReward);
            Assert.AreEqual(4, DailyMissionRewardRules.GetSideTokenReward(15, 2));

            DailyMissionRewardSnapshot neutral;
            Assert.IsTrue(DailyMissionRewardRules.TryCreateCompletionSnapshot(60, 0, out neutral));
            serialized = DailyMissionRewardRules.SerializeCompletionSnapshot(neutral);
            Assert.IsTrue(DailyMissionRewardRules.TryParseCompletionSnapshot(serialized, out retry));
            Assert.AreEqual(DailyMissionRewardRules.NoSideTokenStatId, retry.SideTokenStatId);
            Assert.AreEqual(0, retry.SideTokenReward);
            Assert.AreEqual(6, DailyMissionRewardRules.GetSideTokenReward(60, 1));
        }

        [TestMethod]
        public void CompletionSnapshotRejectsUnsupportedLevelSideAndTampering()
        {
            DailyMissionRewardSnapshot snapshot;
            Assert.IsFalse(DailyMissionRewardRules.TryCreateCompletionSnapshot(200, 2, out snapshot));
            Assert.IsFalse(DailyMissionRewardRules.TryCreateCompletionSnapshot(60, 3, out snapshot));
            Assert.IsFalse(
                DailyMissionRewardRules.TryParseCompletionSnapshot(
                    "item:285612:completion:v1:60:203500:75:8",
                    out snapshot));
        }

        [TestMethod]
        public void RewardEffectReferencesPreserveAppliedAmountsAndRecognizeLegacyOnlyAsPlusTwo()
        {
            string tokenReference = DailyMissionRewardRules.CreateSideTokenEffectReference(75, 6);
            int statId;
            int reward;
            Assert.IsTrue(
                DailyMissionRewardRules.TryResolveAppliedSideTokenEffectReference(
                    tokenReference,
                    out statId,
                    out reward));
            Assert.AreEqual(75, statId);
            Assert.AreEqual(6, reward);

            DailyMissionRewardSnapshot levelSixty;
            Assert.IsTrue(DailyMissionRewardRules.TryCreateCompletionSnapshot(60, 2, out levelSixty));
            Assert.IsTrue(
                DailyMissionRewardRules.TryResolveAppliedSideTokenForSnapshot(
                    levelSixty,
                    tokenReference,
                    out statId,
                    out reward));
            Assert.IsFalse(
                DailyMissionRewardRules.TryResolveAppliedSideTokenForSnapshot(
                    levelSixty,
                    DailyMissionRewardRules.CreateSideTokenEffectReference(75, 8),
                    out statId,
                    out reward));
            Assert.IsFalse(
                DailyMissionRewardRules.TryParseSideTokenEffectReference(
                    "item:285612:side-token:75:20",
                    out statId,
                    out reward));

            Assert.IsTrue(
                DailyMissionRewardRules.TryResolveAppliedSideTokenEffectReference(
                    DailyMissionRewardRules.LegacyOmniSideTokenEffectReference,
                    out statId,
                    out reward));
            Assert.AreEqual(75, statId);
            Assert.AreEqual(2, reward);

            tokenReference = DailyMissionRewardRules.CreateSideTokenEffectReference(
                DailyMissionRewardRules.NoSideTokenStatId,
                0);
            Assert.IsTrue(
                DailyMissionRewardRules.TryResolveAppliedSideTokenEffectReference(
                    tokenReference,
                    out statId,
                    out reward));
            Assert.AreEqual(DailyMissionRewardRules.NoSideTokenStatId, statId);
            Assert.AreEqual(0, reward);

            string xpReference = DailyMissionRewardRules.CreateFullLevelXpEffectReference(60, 203500);
            int level;
            Assert.IsTrue(
                DailyMissionRewardRules.TryParseFullLevelXpEffectReference(
                    xpReference,
                    out level,
                    out reward));
            Assert.AreEqual(60, level);
            Assert.AreEqual(203500, reward);
        }

        [TestMethod]
        public void LevelOneNinetyNineSnapshotCarriesMaximumProgressToLevelTwoHundred()
        {
            const int Level = 199;
            int reward = DailyMissionRewardRules.GetFullRubikaLevelXpReward(Level);
            int floor = Convert.ToInt32(XPTable.TableRKXP[Level - 1, 1]);
            int nextFloor = Convert.ToInt32(XPTable.TableRKXP[Level, 1]);
            long maximumLegalProgress = reward - 1L;
            long cumulativeAfterReward = floor + maximumLegalProgress + reward;

            Assert.AreEqual(nextFloor, floor + reward);
            Assert.AreEqual(maximumLegalProgress, cumulativeAfterReward - nextFloor);
            Assert.IsTrue(cumulativeAfterReward <= int.MaxValue);

            DailyMissionRewardSnapshot snapshot;
            Assert.IsTrue(DailyMissionRewardRules.TryCreateCompletionSnapshot(Level, 2, out snapshot));
            Assert.AreEqual(18, snapshot.SideTokenReward);
            string serialized = DailyMissionRewardRules.SerializeCompletionSnapshot(snapshot);
            DailyMissionRewardSnapshot parsed;
            Assert.IsTrue(DailyMissionRewardRules.TryParseCompletionSnapshot(serialized, out parsed));
            Assert.AreEqual(Level, parsed.LevelBefore);
            Assert.AreEqual(reward, parsed.XpReward);

            int parsedLevel;
            int parsedReward;
            Assert.IsTrue(
                DailyMissionRewardRules.TryParseFullLevelXpEffectReference(
                    DailyMissionRewardRules.CreateFullLevelXpEffectReference(Level, reward),
                    out parsedLevel,
                    out parsedReward));
            Assert.AreEqual(Level, parsedLevel);
            Assert.AreEqual(reward, parsedReward);
        }

        [TestMethod]
        public void SecondCompletionFixturePreservesBoundedRewardProvenanceAndExclusions()
        {
            string fixturePath = ResolveSecondCompletionFixturePath();
            Assert.IsTrue(File.Exists(fixturePath), fixturePath);

            string fixture = File.ReadAllText(fixturePath);
            StringAssert.Contains(fixture, "\"captureId\": \"20260721-023942\"");
            StringAssert.Contains(fixture, "\"runtimeSpecification\": false");
            StringAssert.Contains(
                fixture,
                "D4FF98AF8A6D1822D432208489E8E1AEA6E09B62B29E8B6FB041A7EC33212FC6");
            StringAssert.Contains(fixture, "\"source\": \"enemy-stat-updates.csv:1496\"");
            StringAssert.Contains(fixture, "\"source\": \"enemy-stat-updates.csv:1499\"");
            StringAssert.Contains(fixture, "\"unknown\": 1");
            StringAssert.Contains(fixture, "\"runtimeUse\": \"excluded\"");
            StringAssert.Contains(fixture, "\"captureProvidesDirectRewardDelta\": false");
            StringAssert.Contains(fixture, "The direct ordinary-XP wire sequence is not capture-proven");
        }

        private static string ResolveSecondCompletionFixturePath()
        {
            string deployedPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Content",
                "Captured",
                "Quests",
                "windcaller_karrec_completion_20260721_023942.json");
            if (File.Exists(deployedPath))
            {
                return deployedPath;
            }

            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                string repositoryPath = Path.Combine(
                    current.FullName,
                    "AORebirth",
                    "Server",
                    "ZoneEngine",
                    "Content",
                    "Captured",
                    "Quests",
                    "windcaller_karrec_completion_20260721_023942.json");
                if (File.Exists(repositoryPath))
                {
                    return repositoryPath;
                }

                current = current.Parent;
            }

            return deployedPath;
        }
    }
}
