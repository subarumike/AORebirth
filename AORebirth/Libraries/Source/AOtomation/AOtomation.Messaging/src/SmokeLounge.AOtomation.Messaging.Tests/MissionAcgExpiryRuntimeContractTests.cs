namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgExpiryRuntimeContractTests
    {














        [TestMethod]
        public void CompletionStartedMayExpireBeforeRewardClaim()
        {
            string state = LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"AORebirth\Server\ZoneEngine_New\SharedGameplay\Missions\MissionAcgInstanceState.cs"));
            StringAssert.Contains(
                state,
                "case MissionAcgLifecycleState.CompletionStarted:");
            StringAssert.Contains(
                state,
                "next == MissionAcgLifecycleState.Expired");
        }

        [TestMethod]
        public void ExpiredObjectiveCannotBeResurrected()
        {
            string source =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgObjectiveContracts.cs"));
            StringAssert.Contains(
                source,
                "Expired or abandoned objective state cannot be resurrected.");
            StringAssert.Contains(
                source,
                "Terminal objective state cannot be replaced.");
        }









        [TestMethod]
        public void AcceptanceAndReleaseUseExactAllocatorOwner()
        {
            string allocator =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgAllocationService.cs"));
            StringAssert.Contains(
                allocator,
                "!this.releaseConfirmationPendingPlayfields.Contains(");
            StringAssert.Contains(
                allocator,
                "ConfirmReleaseAfterDurableJournal(");
        }









        [TestMethod]
        public void OfflineOwnerRetainsReconciliationRequirementDurably()
        {
            string state = LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgExpiryState.cs"));
            string store = LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgExpiryStateStore.cs"));
            StringAssert.Contains(state, "RequiresOwnerReconciliation");
            StringAssert.Contains(store, "RequiresOwnerReconciliation");
        }







        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "AORebirth/Server/ZoneEngine",
                    relativePath));
        }

        private static int Count(string value, string fragment)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(
                       fragment,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += fragment.Length;
            }

            return count;
        }
    }
}
