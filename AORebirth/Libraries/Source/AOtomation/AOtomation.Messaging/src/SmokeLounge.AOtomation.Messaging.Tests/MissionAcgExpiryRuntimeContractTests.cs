namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgExpiryRuntimeContractTests
    {
        [TestMethod]
        public void SchedulerUsesOneSecondProcessWideTimer()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(source, "ScanPeriodMilliseconds = 1000");
            StringAssert.Contains(source, "Interlocked.Exchange(ref scanRunning, 1)");
            StringAssert.Contains(source, "new Timer(");
        }

        [TestMethod]
        public void ZoneLifecycleStartsAndStopsExpiryScheduler()
        {
            string source = ReadZoneSource("Program.cs");
            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(source, "MissionAcgExpiryRuntime.Start();");
            Assert.IsTrue(
                Count(source, "MissionAcgExpiryRuntime.Stop();") >= 3,
                "Shutdown, console cancellation, and server stop must halt the scheduler.");
            StringAssert.Contains(runtime, "new ManualResetEvent(false)");
            StringAssert.Contains(runtime, "current.Dispose(drained)");
            StringAssert.Contains(runtime, "drained.WaitOne();");
        }

        [TestMethod]
        public void SchedulerUsesPersistedAbsoluteExpiryWithoutExtension()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(source, "binding.Binding.ExpiryUtc");
            StringAssert.Contains(source, "MissionAcgExpiryPolicy.IsDue(");
            Assert.IsFalse(source.Contains("AddHours(48)"));
            Assert.IsFalse(source.Contains("ExpiryUtc = DateTime.UtcNow"));
        }

        [TestMethod]
        public void StartupRestoresJournalBeforeLiveScanning()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            int load = source.IndexOf("expiryStore.LoadAll(bindings)", StringComparison.Ordinal);
            int start = source.IndexOf("internal static void Start()", StringComparison.Ordinal);
            Assert.IsTrue(load >= 0 && start > load);
            StringAssert.Contains(source, "ProcessAllDue(DateTime.UtcNow);");
            StringAssert.Contains(source, "PendingBindingUpdates");
            StringAssert.Contains(source, "PendingBindingUpdates.Clear();");
            StringAssert.Contains(
                source,
                "lifecycle == MissionAcgLifecycleState.Expired");
            StringAssert.Contains(source, "SetRetryBackoff(");
            StringAssert.Contains(source, "RetryAfterUtc");
        }

        [TestMethod]
        public void NewAcceptanceWaitsForDurableExpiryRecovery()
        {
            string binding =
                ReadMissionSource("MissionAcgBindingRuntime.cs");
            int expiryInitialization =
                binding.IndexOf(
                    "MissionAcgExpiryRuntime.Initialize(",
                    StringComparison.Ordinal);
            int allocationReady =
                binding.IndexOf(
                    "expiryRestorationComplete = true",
                    StringComparison.Ordinal);
            Assert.IsTrue(
                expiryInitialization >= 0
                && allocationReady > expiryInitialization);
            StringAssert.Contains(
                binding,
                "AllocatorDuringExpiryRecovery");
            StringAssert.Contains(
                binding,
                "if (!expiryRestorationComplete)");
            string expiry =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(
                expiry,
                "TryRestoreReleasePendingJournalConfirmation(");
            StringAssert.Contains(
                expiry,
                "MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted");
        }

        [TestMethod]
        public void ObjectiveAndCompletionEntryPointsUseCentralExpiryGate()
        {
            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            string interaction =
                ReadMissionSource("MissionAcgObjectiveInteractionService.cs");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.TryClaimObjectiveVerification(");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.ReleaseObjectiveVerificationClaim(");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.CanContinueCompletion(");
            StringAssert.Contains(
                interaction,
                "MissionAcgExpiryRuntime.CanBeginObjectiveAction(");
        }

        [TestMethod]
        public void RewardClaimBoundaryIsSharedWithExpiry()
        {
            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.TryClaimCompletionReward(");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.ConfirmCompletionRewardClaim(");
            StringAssert.Contains(
                completion,
                "MissionAcgExpiryRuntime.ReleaseCompletionRewardClaim(");
        }

        [TestMethod]
        public void CompletionStartedMayExpireBeforeRewardClaim()
        {
            string state = ReadMissionSource("MissionAcgInstanceState.cs");
            string runtime = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(
                state,
                "case MissionAcgLifecycleState.CompletionStarted:");
            StringAssert.Contains(
                state,
                "next == MissionAcgLifecycleState.Expired");
            StringAssert.Contains(
                runtime,
                "MissionAcgCompletionPhase.RewardClaimStarted");
        }

        [TestMethod]
        public void ExpiredObjectiveCannotBeResurrected()
        {
            string source =
                ReadMissionSource("MissionAcgObjectiveContracts.cs");
            StringAssert.Contains(
                source,
                "Expired or abandoned objective state cannot be resurrected.");
            StringAssert.Contains(
                source,
                "Terminal objective state cannot be replaced.");
        }

        [TestMethod]
        public void ExpiryEvacuationUsesExactExteriorAndSafeFallback()
        {
            string source = ReadMissionSource("MissionInstanceService.cs");
            int start =
                source.IndexOf(
                    "TryEvacuateExpiredMissionOccupant(",
                    StringComparison.Ordinal);
            int end =
                source.IndexOf(
                    "ClearGeneratedInstanceProcessState(",
                    start,
                    StringComparison.Ordinal);
            string method = source.Substring(start, end - start);
            StringAssert.Contains(method, "binding.ExteriorEntranceIdentity.Instance");
            StringAssert.Contains(method, "binding.ExteriorX");
            StringAssert.Contains(method, "ApplySideHubFallback(");
            StringAssert.Contains(method, "OutdoorExitMarkerStandoff");
            Assert.IsFalse(method.Contains("ResolveOutdoorExitDestination("));

            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            int process =
                runtime.IndexOf(
                    "private static void ProcessAcceptedCore",
                    StringComparison.Ordinal);
            int evacuation =
                runtime.IndexOf(
                    "TryAdvanceOccupantEvacuation(",
                    process,
                    StringComparison.Ordinal);
            int objectiveExpiry =
                runtime.IndexOf(
                    "MissionAcgObjectiveRuntime.TrySetLifecycle(",
                    process,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                process >= 0
                && evacuation > process
                && objectiveExpiry > evacuation,
                "Connected occupants must be evacuated before runtime teardown begins.");
        }

        [TestMethod]
        public void ExpirySendsQuestDeleteWithoutCompletionAction()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(source, "MissionCompleteService.SendQuestDelete(");
            Assert.IsFalse(source.Contains("SendMissionCompleteAction("));
            Assert.IsFalse(source.Contains("GrantCredits("));
            Assert.IsFalse(source.Contains("AwardDirectXp("));
        }

        [TestMethod]
        public void ExactRuntimeAndTokenCleanupPrecedeRelease()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            int runtime =
                source.IndexOf(
                    "TryCompleteRuntimeCleanup(",
                    StringComparison.Ordinal);
            int process =
                source.IndexOf(
                    "ClearGeneratedInstanceProcessState(",
                    StringComparison.Ordinal);
            int release =
                source.IndexOf(
                    "TryReleaseAfterDurableCleanup(",
                    StringComparison.Ordinal);
            Assert.IsTrue(runtime >= 0 && process > runtime && release > process);
            string instanceService =
                ReadMissionSource("MissionInstanceService.cs");
            StringAssert.Contains(
                instanceService,
                "MissionTokenProgressTracker.ClearPlayfield(livePlayfield);");
            StringAssert.Contains(
                instanceService,
                "MissionAcgOutdoorReturnStamp");
            StringAssert.Contains(instanceService, "outdoorReturn.Matches(binding)");
        }

        [TestMethod]
        public void Pf2ReleaseRequiresJournalOccupancyResidualAndExactOwnerProof()
        {
            string source = ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(
                source,
                "MissionAcgExpiryPolicy.CanReleasePlayfield(");
            StringAssert.Contains(source, "HasConnectedOccupant(");
            StringAssert.Contains(
                source,
                "AllocatorDuringExpiryRecovery.IsReservedBy(");
            StringAssert.Contains(
                source,
                "MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(");
            StringAssert.Contains(
                source,
                "MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted");
            StringAssert.Contains(
                source,
                "MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed");
            int prerequisites =
                source.IndexOf(
                    "HasReleasePrerequisites(context.Journal.State)",
                    StringComparison.Ordinal);
            int attempted =
                source.IndexOf(
                    "MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted",
                    prerequisites,
                    StringComparison.Ordinal);
            int cleaned =
                source.IndexOf(
                    "MissionAcgLifecycleState.Cleaned",
                    attempted,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                prerequisites >= 0 && attempted > prerequisites && cleaned > attempted,
                "Durable release intent must precede the binding's Cleaned transition.");
        }

        [TestMethod]
        public void AcceptanceAndReleaseUseExactAllocatorOwner()
        {
            string acceptance =
                ReadMissionSource("MissionAcgAcceptanceCoordinator.cs");
            string binding =
                ReadMissionSource("MissionAcgBindingRuntime.cs");
            StringAssert.Contains(
                acceptance,
                "allocator.TryReservePlayfield(");
            StringAssert.Contains(
                acceptance,
                "acceptedIdentity,");
            StringAssert.Contains(
                binding,
                "if (!allocator.ReleaseAfterCleanup(");
            StringAssert.Contains(
                binding,
                "holdForDurableJournalConfirmation");
            StringAssert.Contains(
                binding,
                "PF2 allocator rejected exact-owner cleanup release.");
            string allocator =
                ReadMissionSource("MissionAcgAllocationService.cs");
            StringAssert.Contains(
                allocator,
                "!this.releaseConfirmationPendingPlayfields.Contains(");
            StringAssert.Contains(
                allocator,
                "ConfirmReleaseAfterDurableJournal(");
            StringAssert.Contains(
                ReadMissionSource("MissionAcgExpiryRuntime.cs"),
                "ConfirmReleaseAfterDurableJournal(current)");
        }

        [TestMethod]
        public void ReleasedAllocatorRangeCombatFailsClosed()
        {
            string source = ReadMissionSource("MissionAcgSpatialRuntime.cs");
            StringAssert.Contains(
                source,
                "MissionAcgAllocationService.IsAllocatableRange(firstPf)");
            StringAssert.Contains(
                source,
                "MissionAcgAllocationService.IsAllocatableRange(secondPf)");
            string normalized = source.Replace("\r\n", "\n");
            Assert.IsFalse(
                normalized.Contains(
                    "if (!firstBound && !secondBound)\n            {\n                return true;"));
        }

        [TestMethod]
        public void ExactInventoryRemovalRollsBackOnPersistenceFailure()
        {
            string source = ReadMissionSource("MissionKeyGrantService.cs");
            StringAssert.Contains(source, "TryRestoreRemovedInventoryItem(");
            StringAssert.Contains(source, "BaseInventory.Write");
            StringAssert.Contains(
                source,
                "if (!persisted)");
            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            StringAssert.Contains(completion, "bool keyPresent");
            StringAssert.Contains(
                completion,
                "&& !MissionKeyGrantService.TryRemoveMissionKey(");
        }

        [TestMethod]
        public void AcceptedMissionRemovalUsesAtomicExactPersistence()
        {
            string source = ReadMissionSource("MissionAcceptedStore.cs");
            StringAssert.Contains(source, "TryRemoveExactPersisted(");
            StringAssert.Contains(source, "TryWriteSidecarAtomic(");
            StringAssert.Contains(
                source,
                "TryReadSidecarForExactRemoval(");
            StringAssert.Contains(source, "FindExactIndex_NoLock(");
            StringAssert.Contains(
                source,
                "Exact accepted mission remains after durable removal.");
        }

        [TestMethod]
        public void ReconnectProcessesExpiryBeforeCompletionAndMissionResend()
        {
            string lifecycle =
                ReadMissionSource("MissionAcgLifecycleService.cs");
            int expiry =
                lifecycle.IndexOf(
                    "MissionAcgExpiryRuntime.ProcessForCharacter(",
                    StringComparison.Ordinal);
            int completion =
                lifecycle.IndexOf(
                    "MissionAcgCompletionJournalService.ResumeForCharacter(",
                    StringComparison.Ordinal);
            Assert.IsTrue(expiry >= 0 && completion > expiry);

            string connected =
                ReadZoneSource("Core/PacketHandlers/ClientConnected.cs");
            int cleanup =
                connected.IndexOf(
                    "MissionAcgLifecycleService.TryCleanupPendingForCharacter(",
                    StringComparison.Ordinal);
            int resend =
                connected.IndexOf(
                    "MissionAcceptService.TryResendForLogin(",
                    StringComparison.Ordinal);
            Assert.IsTrue(cleanup >= 0 && resend > cleanup);
        }

        [TestMethod]
        public void OfflineOwnerRetainsReconciliationRequirementDurably()
        {
            string state = ReadMissionSource("MissionAcgExpiryState.cs");
            string store = ReadMissionSource("MissionAcgExpiryStateStore.cs");
            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(state, "RequiresOwnerReconciliation");
            StringAssert.Contains(store, "RequiresOwnerReconciliation");
            StringAssert.Contains(
                runtime,
                "Exact inventory and client journal cleanup awaits owner reconnect.");
            StringAssert.Contains(runtime, "inventoryArtifactsRemoved");
            StringAssert.Contains(runtime, "clientMissionRemoved");
            StringAssert.Contains(runtime, "occupantsAlreadyEvacuated");
            StringAssert.Contains(
                runtime,
                "(!inventoryArtifactsRemoved || !clientMissionRemoved)");
        }

        [TestMethod]
        public void DurableCompletionWinnerIsRemovedFromExpiryScheduling()
        {
            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(
                runtime,
                "MissionAcgExpiryPolicy.IsCompletionOwned(");
            StringAssert.Contains(
                runtime,
                "CompletionOwned.Add(accepted)");
            StringAssert.Contains(
                runtime,
                "CompletionOwned.Contains(accepted)");
            Assert.IsTrue(
                Count(runtime, "CompletionOwned.Contains(accepted)") >= 2,
                "Both expiry and duplicate completion claims must reject durable completion ownership.");
            StringAssert.Contains(
                runtime,
                "CompletionOwned.Contains(acceptedQuestInstance)");
        }

        [TestMethod]
        public void AbandonmentAndExpiryUseOneAtomicOwnerGate()
        {
            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            StringAssert.Contains(runtime, "AbandonmentClaims");
            StringAssert.Contains(runtime, "AbandonmentOwned");
            StringAssert.Contains(runtime, "CompletionTransitionClaims");
            StringAssert.Contains(runtime, "TryClaimAbandonment(");
            StringAssert.Contains(
                runtime,
                "MissionAcgExpiryPolicy.CanBeginAbandonment(");

            string handler =
                ReadZoneSource(
                    "Core/MessageHandlers/QuestMessageHandler.cs");
            int claim =
                handler.IndexOf(
                    "MissionAcgExpiryRuntime.TryClaimAbandonment(",
                    StringComparison.Ordinal);
            int refresh =
                handler.IndexOf(
                    "MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(",
                    claim,
                    StringComparison.Ordinal);
            int transition =
                handler.IndexOf(
                    "MissionAcgLifecycleState.Abandoned",
                    refresh,
                    StringComparison.Ordinal);
            int confirm =
                handler.IndexOf(
                    "MissionAcgExpiryRuntime.ConfirmAbandonmentClaim(",
                    transition,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                claim >= 0
                && refresh > claim
                && transition > refresh
                && confirm > transition);
            StringAssert.Contains(
                handler,
                "MissionAcgExpiryRuntime.ReleaseAbandonmentClaim(");

            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            int completionClaim =
                completion.IndexOf(
                    "MissionAcgExpiryRuntime.TryClaimCompletionTransition(",
                    StringComparison.Ordinal);
            int completionTransition =
                completion.IndexOf(
                    "MissionAcgLifecycleState.CompletionStarted",
                    completionClaim,
                    StringComparison.Ordinal);
            int completionRelease =
                completion.IndexOf(
                    "MissionAcgExpiryRuntime.ReleaseCompletionTransitionClaim(",
                    completionTransition,
                    StringComparison.Ordinal);
            int objectiveTransition =
                completion.IndexOf(
                    "MissionAcgCompletionPhase.CompletionStarted",
                    completionTransition,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                completionClaim >= 0
                && completionTransition > completionClaim
                && objectiveTransition > completionTransition
                && completionRelease > objectiveTransition
                && completionRelease > completionTransition);
        }

        [TestMethod]
        public void InterruptedCompletionTransitionResumesItsSecondDurableWrite()
        {
            string runtime =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            int claimStart =
                runtime.IndexOf(
                    "internal static bool TryClaimCompletionTransition(",
                    StringComparison.Ordinal);
            int claimEnd =
                runtime.IndexOf(
                    "internal static void ReleaseCompletionTransitionClaim(",
                    claimStart,
                    StringComparison.Ordinal);
            Assert.IsTrue(claimStart >= 0 && claimEnd > claimStart);
            string claim = runtime.Substring(claimStart, claimEnd - claimStart);
            StringAssert.Contains(
                claim,
                "MissionAcgLifecycleState.CompletionStarted");

            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            int persistedCheck =
                completion.IndexOf(
                    "bool bindingTransitionPersisted",
                    StringComparison.Ordinal);
            int objectiveWrite =
                completion.IndexOf(
                    "MissionAcgCompletionPhase.CompletionStarted",
                    persistedCheck,
                    StringComparison.Ordinal);
            int release =
                completion.IndexOf(
                    "MissionAcgExpiryRuntime.ReleaseCompletionTransitionClaim(",
                    objectiveWrite,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                persistedCheck >= 0
                && objectiveWrite > persistedCheck
                && release > objectiveWrite);
        }

        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            DirectoryInfo current =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return File.ReadAllText(
                        Path.Combine(
                            current.FullName,
                            "AORebirth/Server/ZoneEngine",
                            relativePath));
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
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
