namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldCharacterHeartbeatHealthRulesTests
    {
        [TestMethod]
        public void PlayfieldCharacterHeartbeatHealthRulesCoverRuntimeModelStates()
        {
            Assert.IsTrue(PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(1));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(0));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(-1));

            Assert.IsTrue(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(1, 2));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(0, 2));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(-1, 2));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(1, 0));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(1, -1));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(2, 2));
            Assert.IsFalse(PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(3, 2));

            Func<int[], int> readSingleHealth = values => values.Single(value => true);
            int nonTargetHealthReads = 0;
            Assert.IsFalse(
                PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    new int[0],
                    false,
                    values =>
                        {
                            nonTargetHealthReads++;
                            return values.Single(value => true);
                        }));
            Assert.AreEqual(0, nonTargetHealthReads);

            AssertInvalidOperation(
                () =>
                    PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                        new int[0],
                        true,
                        readSingleHealth),
                "A relevant candidate with missing health must surface its cardinality failure.");
            AssertInvalidOperation(
                () =>
                    PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                        new[] { 27, 27 },
                        true,
                        readSingleHealth),
                "A relevant candidate with duplicate health must surface its cardinality failure.");

            Assert.IsFalse(
                PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    new[] { 0 },
                    true,
                    readSingleHealth));
            Assert.IsFalse(
                PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    new[] { -1 },
                    true,
                    readSingleHealth));
            Assert.IsTrue(
                PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    new[] { 25 },
                    true,
                    readSingleHealth));

            int[][] scannedCandidates = { new int[0], new[] { 0 }, new[] { 25 } };
            bool[] scannedTargetStates = { false, true, true };
            bool laterLivingCandidateWasProcessed = false;
            for (int index = 0; index < scannedCandidates.Length; index++)
            {
                if (PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    scannedCandidates[index],
                    scannedTargetStates[index],
                    readSingleHealth))
                {
                    laterLivingCandidateWasProcessed = true;
                    break;
                }
            }

            Assert.IsTrue(laterLivingCandidateWasProcessed);

            int[] healthyCandidate = { 1 };
            int[] unrelatedStats = { 41 };
            Assert.IsTrue(
                PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    healthyCandidate,
                    true,
                    readSingleHealth));
            Assert.AreEqual(1, healthyCandidate[0]);
            Assert.AreEqual(41, unrelatedStats[0]);
        }

        [TestMethod]
        public void PlayfieldCharacterHeartbeatStatsContractSurfacesMissingOrDuplicateHealth()
        {
            string statsSource = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Stats",
                    "Stats.cs"));

            StringAssert.Contains(
                statsSource,
                "this.health = new StatHitPoints(this, 27, 1, true, false, true);");
            StringAssert.Contains(
                statsSource,
                "this.life = new StatLife(this, 1, 1, true, false, true);");
            Assert.AreEqual(1, CountOccurrences(statsSource, "this.all.Add(this.health);"));
            Assert.AreEqual(1, CountOccurrences(statsSource, "this.all.Add(this.life);"));
            StringAssert.Contains(
                statsSource,
                "return this.all.Single(x => x.StatId == (int)i);");

            string heartbeatSource = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "AORebirth",
                    "Server",
                    "ZoneEngine",
                    "Core",
                    "Playfields",
                    "PlayfieldCharacterHeartbeatRuntimeService.cs"));
            int targetGuardIndex = heartbeatSource.IndexOf(
                "bool targetsNpc = character.FightingTarget.Instance == targetInstance",
                StringComparison.Ordinal);
            Assert.IsTrue(targetGuardIndex >= 0);
            int candidateRuleIndex = heartbeatSource.IndexOf(
                "PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(",
                targetGuardIndex,
                StringComparison.Ordinal);
            Assert.IsTrue(candidateRuleIndex > targetGuardIndex);
            StringAssert.Contains(
                heartbeatSource,
                "candidate => candidate.Stats[StatIds.health].Value");
            Assert.IsFalse(heartbeatSource.Contains("catch (InvalidOperationException)"));
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int searchFrom = 0;
            while ((searchFrom = source.IndexOf(value, searchFrom, StringComparison.Ordinal)) >= 0)
            {
                count++;
                searchFrom += value.Length;
            }

            return count;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            DirectoryInfo current = new FileInfo(sourcePath).Directory;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            Assert.Fail("Unable to locate the repository root from the test source path.");
            return string.Empty;
        }

        private static void AssertInvalidOperation(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Assert.Fail(message);
        }
    }
}
