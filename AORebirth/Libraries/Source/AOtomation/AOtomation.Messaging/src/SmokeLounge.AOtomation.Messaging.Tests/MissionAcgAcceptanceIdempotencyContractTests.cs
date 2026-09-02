namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgAcceptanceIdempotencyContractTests
    {
        [TestMethod]
        public void DurableOwnerOfferClaimPrecedesAllocation()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string accept = ReadMember(coordinator, "internal static bool TryAccept(");

            AssertOrdered(
                accept,
                "MissionAcgAcceptedProjectionRuntime.TryGetByOwnerOffer(",
                "MissionAcgBindingRuntime.TryGetByOwnerOffer(",
                "MissionOfferStore.TryClaimForAcceptance(",
                "TryReserveAcceptedQuestIdentity(",
                "MissionAcgAcceptedProjectionRuntime.TryCreate(",
                "MissionOfferStore.TryMarkAccepted(",
                "TryResumeAcceptance(");
        }

        [TestMethod]
        public void AcceptanceTreatsHighBitTerminalInstancesAsConcrete()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string accept = ReadMember(coordinator, "internal static bool TryAccept(");
            string validate = ReadMember(coordinator, "private static bool ValidateOffer(");

            StringAssert.Contains(accept, "ToRecord(offer.Unknown5)");
            StringAssert.Contains(validate, "offer.Unknown5.Instance == 0");
            Assert.IsFalse(
                validate.IndexOf("offer.Unknown5.Instance <= 0", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void DurableOfferAuthorityRestoresBeforeAllocatorAndNewIdentityPublication()
        {
            string runtime = ReadMissionSource("MissionAcgBindingRuntime.cs");
            string initialize = ReadMember(runtime, "internal static void Initialize()");

            AssertOrdered(
                initialize,
                "MissionAcgAcceptedProjectionRuntime.Initialize(",
                "MissionOfferStore.Initialize(missionStateDirectory);",
                "RestoreGeneratedOfferAuthority(DateTime.UtcNow);",
                "allocator.TryRestore(",
                "MissionRollService.SetOfferIdentityCollisionValidator(");

            string restore =
                ReadMember(runtime, "private static void RestoreGeneratedOfferAuthority(");
            AssertOrdered(
                restore,
                "MissionOfferStore.DiscardPreparedOnRestoration(nowUtc);",
                "MissionOfferStore.Snapshot();",
                "MissionAcgAcceptedProjectionRuntime.TryGetByOwnerOffer(",
                "MissionOfferStore.TryReconcileAccepted(",
                "MissionOfferStore.TryRestoreUnprojectedClaim(",
                "MissionOfferStore.ExpirePending(nowUtc);");
        }

        [TestMethod]
        public void RollPublishesExactDurableBatchAndDiscardsOnlyThatBatchOnFeeFailure()
        {
            string handler =
                ReadZoneSource("Core/MessageHandlers/QuestAlternativeMessageHandler.cs");
            string read = ReadMember(handler, "protected override void Read(");
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string accept = ReadMember(coordinator, "internal static bool TryAccept(");

            AssertOrdered(
                read,
                "lock (MissionOfferStore.AuthorityGate)",
                "out rollSeed",
                "MissionOfferStore.TryStoreRoll(",
                "out storedBatch",
                "MissionOfferStore.TryBeginFeeCharge(",
                "MissionRollFeeService.TryChargeRollFee(",
                "MissionOfferStore.TryDiscardBatch(",
                "storedBatch,",
                "rollFeeWasCharged = true;",
                "MissionOfferStore.TryPublishBatch(",
                "client.SendCompressed(response);");
            StringAssert.Contains(accept, "lock (MissionOfferStore.AuthorityGate)");
            Assert.IsFalse(
                read.IndexOf("MissionOfferStore.DiscardRoll(", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void LoginRecoversTheDurableFeeClaimBeforeMissionWindowResend()
        {
            string connected = ReadZoneSource("Core/PacketHandlers/ClientConnected.cs");
            AssertOrdered(
                connected,
                "MissionAcgLifecycleService.TryCleanupPendingForCharacter(",
                "MissionRollFeeService.TryRecoverAndSendForLogin(",
                "MissionAcceptService.TryResendForLogin(");

            string feeService = ReadMissionSource("MissionRollFeeService.cs");
            string recover = ReadMember(
                feeService,
                "internal static bool TryRecoverAndSendForLogin(");
            AssertOrdered(
                recover,
                "MissionOfferStore.TryGetFeeChargePending(",
                "TryChargeRollFee(",
                "MissionOfferStore.TryPublishBatch(",
                "MissionOfferStore.TryGetPendingRollForLogin(",
                "client.SendCompressed(pendingResponse);");
        }

        [TestMethod]
        public void RollFeeDebitAndBatchClaimShareOneExistingDatabaseTransaction()
        {
            string feeService = ReadMissionSource("MissionRollFeeService.cs");
            string charge = ReadMember(
                feeService,
                "public static bool TryChargeRollFee(");
            StringAssert.Contains(charge, "dao.TryChargeRollFee(");
            Assert.IsFalse(
                charge.IndexOf("BeginTransaction", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(
                charge.IndexOf("SELECT ", StringComparison.OrdinalIgnoreCase) >= 0);

            string missionDao = ReadRepositoryFile(
                "AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/MySqlMissionDao.cs");
            string apply = ReadMember(
                missionDao,
                "public MissionRollFeeResult TryChargeRollFee(");
            AssertOrdered(
                apply,
                "connection.BeginTransaction()",
                "ReadCash(",
                "SELECT RewardType, Status, EffectReference FROM missionrewardledger",
                "INSERT INTO stats",
                "INSERT INTO missionrewardledger",
                "transaction.Commit();");
            string readCash = ReadMember(
                missionDao,
                "private static int ReadCash(");
            StringAssert.Contains(readCash, "SELECT StatValue FROM stats");
            StringAssert.Contains(readCash, "FOR UPDATE");
            Assert.IsFalse(
                apply.IndexOf("INSERT INTO missionstate", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(
                apply.IndexOf("CREATE TABLE", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        public void EveryAcceptanceSideEffectHasADurablePendingCheckpoint()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string resume = ReadMember(coordinator, "private static bool TryResumeAcceptance(");

            AssertOrdered(
                resume,
                "MissionAcgAcceptancePhase.KeyGrantPending",
                "TryGrantReservedMissionKey(",
                "MissionAcgAcceptancePhase.KeyGranted",
                "MissionAcgAcceptancePhase.ArtifactGrantPending",
                "TryGrantReservedRepairItem(",
                "MissionAcgAcceptancePhase.ArtifactsGranted");
            AssertOrdered(
                resume,
                "MissionAcgLifecycleState.Accepted",
                "MissionAcgAcceptancePhase.AcceptanceCommitted",
                "MissionAcgAcceptancePhase.QfuPending",
                "SendAcceptedGeneratedMission(",
                "MissionAcgAcceptancePhase.QfuSent");
        }

        [TestMethod]
        public void DuplicateCreateQuestDoesNotRequireTheProcessLocalOffer()
        {
            string handler =
                ReadZoneSource("Core/MessageHandlers/CreateQuestMessageHandler.cs");
            string read = ReadMember(handler, "protected override void Read(");

            StringAssert.Contains(read, "message.QuestIdentity");
            StringAssert.Contains(read, "MissionAcgAcceptanceCoordinator.TryAccept(");
            Assert.IsFalse(
                read.IndexOf("MissionOfferStore.TryGetOffer(", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void ReconnectRecoversPendingAcceptanceBeforeMissionListProjection()
        {
            string service = ReadMissionSource("MissionAcceptService.cs");
            string reconnect = ReadMember(service, "public static bool TryResendForLogin(");

            AssertOrdered(
                reconnect,
                "MissionAcgBindingRuntime.Initialize();",
                "MissionAcgAcceptanceCoordinator.TryRecoverOwned(",
                "MissionAcceptedStore.GetAll(",
                "SendOneMissionWindow(");
        }

        [TestMethod]
        public void ReconnectSuppressesTheExactQfuAlreadyDeliveredByRecovery()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string recover = ReadMember(
                coordinator,
                "internal static bool TryRecoverOwned(");
            StringAssert.Contains(
                recover,
                "out ISet<int> deliveredAcceptedQuestInstances");
            AssertOrdered(
                recover,
                "deliveredAcceptedQuestInstances = new HashSet<int>();",
                "TryResumeAcceptance(",
                "deliveredAcceptedQuestInstances.Add(");

            string service = ReadMissionSource("MissionAcceptService.cs");
            string reconnect = ReadMember(
                service,
                "public static bool TryResendForLogin(");
            AssertOrdered(
                reconnect,
                "ISet<int> deliveredAcceptedQuestInstances",
                "MissionAcgAcceptanceCoordinator.TryRecoverOwned(",
                "deliveredAcceptedQuestInstances.Contains(",
                "entry.QuestIdentity.Instance",
                "continue;",
                "SendOneMissionWindow(");
        }

        [TestMethod]
        public void GeneratedCompletionUsesFrozenProjectionRewardsBeforeFallbacks()
        {
            string completion = ReadMissionSource("MissionCompleteService.cs");
            string cash = ReadMember(completion, "internal static int ResolveCashReward(");
            string xp = ReadMember(completion, "internal static int ResolveXpReward(");

            AssertOrdered(cash, "HasFrozenAcceptedRewards", "BaseCashForMissionQl(");
            AssertOrdered(xp, "HasFrozenAcceptedRewards", "BaseXpForMissionQl(");
            StringAssert.Contains(completion, "FrozenItemRewardCount");
        }

        [TestMethod]
        public void CompletionRecoveryResolvesFrozenProjectionOutsideActiveMissionList()
        {
            string journal = ReadMissionSource("MissionAcgCompletionJournalService.cs");
            string resolver = ReadMember(
                journal,
                "private static bool TryResolveAcceptedMission(");
            AssertOrdered(
                resolver,
                "MissionAcceptedStore.TryResolve(",
                "MissionAcceptedStore.TryResolveGeneratedProjection(");

            string acceptedStore = ReadMissionSource("MissionAcceptedStore.cs");
            string projectionResolver = ReadMember(
                acceptedStore,
                "internal static bool TryResolveGeneratedProjection(");
            AssertOrdered(
                projectionResolver,
                "MissionAcgAcceptedProjectionRuntime.TryGetByAcceptedQuest(",
                "MissionAcgAcceptancePhase.AcceptanceCommitted",
                "BuildProjectionEntry(projection)");
        }

        [TestMethod]
        public void IrrecoverableAcceptanceUsesDurableExactCleanupOwner()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string cleanup = ReadMember(
                coordinator,
                "private static void CleanupIrrecoverableAcceptance(");
            AssertOrdered(
                cleanup,
                "MissionAcgLifecycleState.CleanupPending",
                "MissionAcgLifecycleService.TryCleanupOwnedRecord(");
            Assert.IsFalse(
                cleanup.IndexOf("TryRemoveMissionKey(", StringComparison.Ordinal) >= 0);
            Assert.IsFalse(
                cleanup.IndexOf(
                    "TryReleaseFailedAcceptanceAfterCleanup(",
                    StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void RestartRestoresProjectionReservationsBeforeNewAllocatorUse()
        {
            string runtime = ReadMissionSource("MissionAcgBindingRuntime.cs");
            string initialize = ReadMember(runtime, "internal static void Initialize()");

            AssertOrdered(
                initialize,
                "MissionAcgAcceptedProjectionRuntime.Initialize(",
                "allocator.TryRestore(",
                "MissionAcgObjectiveRuntime.Initialize(",
                "MissionAcgAcceptedProjectionRuntime.ReconcileObjectiveArtifacts();",
                "initialized = true;");
        }

        [TestMethod]
        public void TerminalLifecycleRejectsBeforeAnyArtifactRecovery()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string resume = ReadMember(coordinator, "private static bool TryResumeAcceptance(");

            AssertOrdered(
                resume,
                "projection.LifecycleState != MissionAcgLifecycleState.Reserved",
                "projection.CleanupState != MissionAcgCleanupState.None",
                "MissionAcgAcceptancePhase.KeyGrantPending",
                "TryFindReservedMissionArtifact(",
                "TryGrantReservedMissionKey(");
        }

        [TestMethod]
        public void RecoveryFindsReservedArtifactsByItemIdentityNotBagCoordinates()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string resume = ReadMember(coordinator, "private static bool TryResumeAcceptance(");
            StringAssert.Contains(resume, "TryFindReservedMissionArtifact(");
            Assert.IsFalse(
                resume.IndexOf("TryGetExactInventoryItem(", StringComparison.Ordinal) >= 0);

            string grants = ReadMissionSource("MissionKeyGrantService.cs");
            string lookup = ReadMember(
                grants,
                "internal static bool TryFindReservedMissionArtifact(");
            AssertOrdered(
                lookup,
                "character.BaseInventory.Pages",
                "candidate.Identity.Instance != itemIdentityInstance",
                "candidate.LowID == MissionKeyTemplateId",
                "item = candidate;");
        }

        [TestMethod]
        public void OfferExpiryAndAcceptedMissionExpiryRemainIndependent()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string accept = ReadMember(coordinator, "internal static bool TryAccept(");
            AssertOrdered(
                accept,
                "acceptedUtc.AddSeconds(",
                "MissionAcceptService.MissionDurationSeconds");
            StringAssert.Contains(accept, "offerRecord.ExpiresUtc,");
        }

        [TestMethod]
        public void RepairTemplateIsFrozenBeforeDurableProjectionAndReusedOnGrant()
        {
            string coordinator = ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string accept = ReadMember(coordinator, "internal static bool TryAccept(");
            AssertOrdered(
                accept,
                "TryResolveRepairTemplateIds(",
                "MissionAcgAcceptedProjection.Create(",
                "repairArtifactLowId",
                "repairArtifactHighId");

            string resume = ReadMember(
                coordinator,
                "private static bool TryResumeAcceptance(");
            AssertOrdered(
                resume,
                "TryGrantReservedRepairItem(",
                "projection.RepairArtifactLowId",
                "projection.RepairArtifactHighId");
        }

        [TestMethod]
        public void StartupCleansExpiredPreBindingReservationWithoutOwnerReconnect()
        {
            string runtime = ReadMissionSource("MissionAcgBindingRuntime.cs");
            string initialize = ReadMember(runtime, "internal static void Initialize()");
            AssertOrdered(
                initialize,
                "allocator.TryRestore(",
                "pending.Binding.ExpiryUtc > restorationUtc",
                "projection.WithLifecycle(",
                "MissionAcgLifecycleState.Cleaned",
                "allocator.RollbackUnpersisted(",
                "MissionAcgRuntimeManager.Initialize(");
        }

        private static void AssertOrdered(string source, params string[] tokens)
        {
            int cursor = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                int next = source.IndexOf(tokens[i], cursor + 1, StringComparison.Ordinal);
                Assert.IsTrue(next > cursor, "Missing or out-of-order token: " + tokens[i]);
                cursor = next;
            }
        }

        private static string ReadMember(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Member signature not found: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.IsTrue(brace >= 0, "Member body not found: " + signature);
            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail("Member body was not balanced: " + signature);
            return string.Empty;
        }

        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            return ReadRepositoryFile(
                "AORebirth/Server/ZoneEngine/" + relativePath.Replace('\\', '/'));
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                string gitEntry = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
                {
                    return File.ReadAllText(
                        Path.Combine(
                            current.FullName,
                            relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }

                current = current.Parent;
            }

            Assert.Fail("Repository root could not be located.");
            return string.Empty;
        }
    }
}
