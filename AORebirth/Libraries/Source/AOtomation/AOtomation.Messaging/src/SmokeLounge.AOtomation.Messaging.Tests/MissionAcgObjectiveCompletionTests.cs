namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgObjectiveCompletionTests
    {
        private MissionAcgLayoutCatalog catalog;

        private string root;

        [TestInitialize]
        public void Initialize()
        {
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
            this.root =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-stage4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.root))
            {
                Directory.Delete(this.root, true);
            }
        }

        [TestMethod]
        public void AllFiveContractsBindOneCapturedSlotToOneRuntimeIdentity()
        {
            MissionRollType[] types =
            {
                MissionRollType.KillPerson,
                MissionRollType.FindPerson,
                MissionRollType.FindItem,
                MissionRollType.FindItemReturn,
                MissionRollType.RepairMachine
            };
            for (int i = 0; i < types.Length; i++)
            {
                MissionAcgObjectiveRecord record = this.CreateObjective(types[i], i + 1, 100 + i);
                Assert.AreEqual(types[i], record.Binding.MissionType);
                Assert.AreEqual(
                    MissionAcgObjectiveContract.InteractionFor(types[i]),
                    record.Binding.RequiredInteraction);
                Assert.IsTrue(record.Binding.RuntimeObjectiveIdentity.Instance != 0);
                Assert.AreEqual(record.Binding.AllocatedLivePlayfield2, ReversePf(record));
                MissionAcgLayoutBundle bundle = this.catalog.FindByLayoutId(record.Binding.BundleId);
                Assert.AreEqual(1, bundle.ObjectiveSlots.Count);
                Assert.AreEqual(
                    bundle.ObjectiveSlots[0].CapturedIdentity,
                    record.Binding.CapturedObjectiveIdentity);
            }
        }

        [TestMethod]
        public void AbandonedExpiredAndCleanedObjectivesCannotResumeCompletion()
        {
            MissionAcgObjectiveState verified =
                StateAtPhase(
                    NewState(DateTime.UtcNow),
                    MissionAcgCompletionPhase.ObjectiveVerified);
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(verified));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    verified.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.Abandoned)));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    verified.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.Expired)));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    verified.Copy(
                        lifecycle:
                            MissionAcgObjectiveLifecycle.CleanupCompleted)));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    verified.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.Invalid)));

            MissionAcgObjectiveState completionOwned =
                StateAtPhase(
                    NewState(DateTime.UtcNow),
                    MissionAcgCompletionPhase.RewardClaimStarted);
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    completionOwned));
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    completionOwned.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.Completed)));
        }

        [TestMethod]
        public void CleanupCompletionRequiresBothDurableOwners()
        {
            DateTime now = DateTime.UtcNow;
            var cleanedBinding =
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    now,
                    now);
            MissionAcgObjectiveState incompleteObjective =
                NewState(now).Copy(
                    lifecycle:
                        MissionAcgObjectiveLifecycle.CleanupCompleted,
                    objectiveCleanupCompleted: true,
                    missionCleanupCompleted: false);
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCleanupComplete(
                    cleanedBinding,
                    incompleteObjective));

            MissionAcgObjectiveState cleanedObjective =
                incompleteObjective.Copy(missionCleanupCompleted: true);
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.IsCleanupComplete(
                    cleanedBinding,
                    cleanedObjective));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsCleanupComplete(
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.CleanupPending,
                        MissionAcgCleanupState.InstanceReleasePending,
                        now,
                        now),
                    cleanedObjective));
        }

        [TestMethod]
        public void PfReleaseGateRequiresVerifiedTerminalCleanup()
        {
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.RequiresVerifiedRuntimeCleanup(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.RequiresVerifiedRuntimeCleanup(
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.RequiresVerifiedRuntimeCleanup(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Failed));
        }

        [TestMethod]
        public void BindingTransitionsRejectStaleStateVersions()
        {
            DateTime now = DateTime.UtcNow;
            var current =
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Accepted,
                    MissionAcgCleanupState.None,
                    now,
                    null);
            var sameVersion =
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Accepted,
                    MissionAcgCleanupState.None,
                    now,
                    null);
            Assert.IsTrue(
                MissionAcgLifecyclePolicy.IsSameBindingStateVersion(
                    current,
                    sameVersion));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsSameBindingStateVersion(
                    current,
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.Active,
                        MissionAcgCleanupState.None,
                        now,
                        null)));
            Assert.IsFalse(
                MissionAcgLifecyclePolicy.IsSameBindingStateVersion(
                    current,
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.Accepted,
                        MissionAcgCleanupState.None,
                        now.AddTicks(1),
                        null)));
        }

        [TestMethod]
        public void ExactKeyLookupNeverConsumesAnotherAcceptedMissionsKey()
        {
            int characterInstance = Guid.NewGuid().GetHashCode();
            var firstMission =
                new Identity
                {
                    Type =
                        (IdentityType)
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                    Instance = 101
                };
            var secondMission =
                new Identity
                {
                    Type = firstMission.Type,
                    Instance = 102
                };
            var missingMission =
                new Identity
                {
                    Type = firstMission.Type,
                    Instance = 103
                };
            MissionKeyStore.Register(characterInstance, firstMission, 7001);
            MissionKeyStore.Register(characterInstance, secondMission, 7002);

            int key;
            Assert.IsFalse(
                MissionKeyStore.TryTakeExact(
                    characterInstance,
                    missingMission,
                    out key));
            Assert.AreEqual(0, key);
            Assert.IsTrue(
                MissionKeyStore.TryTakeExact(
                    characterInstance,
                    firstMission,
                    out key));
            Assert.AreEqual(7001, key);
            Assert.IsTrue(
                MissionKeyStore.TryTakeExact(
                    characterInstance,
                    secondMission,
                    out key));
            Assert.AreEqual(7002, key);
        }

        [TestMethod]
        public void ProductionAbandonmentUsesExactOwnedCleanupWithoutNewestFallback()
        {
            string handler =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\QuestMessageHandler.cs");
            int ownedLookup =
                handler.IndexOf(
                    "TryGetOwnedByAcceptedQuest",
                    StringComparison.Ordinal);
            int acknowledgement =
                handler.IndexOf(
                    "SendDeleteAcknowledgement(client, character, deleteMission)",
                    ownedLookup,
                    StringComparison.Ordinal);
            Assert.IsTrue(ownedLookup >= 0);
            Assert.IsTrue(acknowledgement > ownedLookup);
            StringAssert.Contains(handler, "TryAbandonGeneratedMission");
            StringAssert.Contains(handler, "TryCleanupOwnedRecord");
            StringAssert.Contains(handler, "TryTakeExact");
            Assert.IsFalse(handler.Contains("TryRemoveAnyMissionKey"));
            Assert.IsFalse(handler.Contains("TryRemoveAnyRepairItem"));
            Assert.IsFalse(
                handler.Contains("MissionTokenProgressTracker.ClearCharacter"));
            Assert.IsFalse(
                handler.Contains("MissionFindItemService.ClearCharacter"));

            string bindingRuntime =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgBindingRuntime.cs");
            int cleanupGate =
                bindingRuntime.IndexOf(
                    "TryVerifyRuntimeCleanup",
                    StringComparison.Ordinal);
            int release =
                bindingRuntime.IndexOf(
                    "allocator.ReleaseAfterCleanup",
                    StringComparison.Ordinal);
            Assert.IsTrue(cleanupGate >= 0);
            Assert.IsTrue(release > cleanupGate);
            StringAssert.Contains(
                bindingRuntime,
                "MissionAcgLifecyclePolicy.IsSameBindingStateVersion");
            StringAssert.Contains(
                bindingRuntime,
                "TryReleaseAfterDurableCleanup");
            StringAssert.Contains(
                bindingRuntime,
                "TryReleaseFailedAcceptanceAfterCleanup");

            string objectiveRuntime =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgObjectiveRuntime.cs");
            StringAssert.Contains(
                objectiveRuntime,
                "MissionAcgLifecyclePolicy.IsCompletionResumeEligible");

            string completion =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgCompletionJournalService.cs");
            int objectiveFinal =
                completion.IndexOf(
                    "missionCleanupCompleted: true",
                    StringComparison.Ordinal);
            int completionRelease =
                completion.IndexOf(
                    "TryReleaseAfterDurableCleanup",
                    StringComparison.Ordinal);
            Assert.IsTrue(objectiveFinal >= 0);
            Assert.IsTrue(completionRelease > objectiveFinal);

            string lifecycle =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgLifecycleService.cs");
            int lifecycleObjectiveFinal =
                lifecycle.IndexOf(
                    "missionCleanupCompleted: true",
                    StringComparison.Ordinal);
            int cleanedTransition =
                lifecycle.IndexOf(
                    "MissionAcgLifecycleState.Cleaned",
                    lifecycleObjectiveFinal,
                    StringComparison.Ordinal);
            int lifecycleRelease =
                lifecycle.IndexOf(
                    "TryReleaseAfterDurableCleanup",
                    lifecycleObjectiveFinal,
                    StringComparison.Ordinal);
            Assert.IsTrue(lifecycleObjectiveFinal >= 0);
            Assert.IsTrue(cleanedTransition > lifecycleObjectiveFinal);
            Assert.IsTrue(lifecycleRelease > cleanedTransition);

            string acceptance =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgAcceptanceCoordinator.cs");
            StringAssert.Contains(
                acceptance,
                "TryReleaseFailedAcceptanceAfterCleanup");
            Assert.IsFalse(acceptance.Contains("MissionKeyStore.TryTake("));
        }

        [TestMethod]
        public void ExactRoutingRejectsWrongOwnerTeamPfRuntimeTemplateNameAndReplay()
        {
            MissionAcgObjectiveRecord record =
                this.CreateObjective(MissionRollType.KillPerson, 10, 200);
            MissionAcgObjectiveEvent valid = EventFor(record);
            string failure;
            Assert.IsTrue(MissionAcgObjectiveContract.TryVerify(record, valid, out failure), failure);

            valid.OwnerInstance++;
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));
            valid = EventFor(record);
            valid.TeamIdentity = new MissionAcgIdentityRecord(1, 2);
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));
            valid = EventFor(record);
            valid.AllocatedLivePlayfield2++;
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));
            valid = EventFor(record);
            valid.RuntimeObjectiveIdentity =
                new MissionAcgIdentityRecord(
                    record.Binding.RuntimeObjectiveIdentity.Type,
                    record.Binding.RuntimeObjectiveIdentity.Instance + 1);
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));
            valid = EventFor(record);
            valid.ObjectiveTemplateId++;
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));
            valid = EventFor(record);
            valid.ObjectiveName = record.Binding.ObjectiveName + " other";
            Assert.IsFalse(MissionAcgObjectiveContract.TryVerify(record, valid, out failure));

            MissionAcgObjectiveRecord replay =
                record.WithState(
                    record.State.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.Verified,
                        phase: MissionAcgCompletionPhase.ObjectiveVerified));
            Assert.IsFalse(
                MissionAcgObjectiveContract.TryVerify(
                    replay,
                    EventFor(replay),
                    out failure));
        }

        [TestMethod]
        public void SameTemplateTargetsAndSameTypeMissionsRemainIndependent()
        {
            MissionAcgObjectiveRecord older =
                this.CreateObjective(MissionRollType.KillPerson, 20, 300);
            MissionAcgObjectiveRecord newer =
                this.CreateObjective(MissionRollType.KillPerson, 21, 300);
            string failure;
            Assert.AreNotEqual(
                older.Binding.AcceptedQuestIdentity,
                newer.Binding.AcceptedQuestIdentity);
            Assert.AreNotEqual(
                older.Binding.AllocatedLivePlayfield2,
                newer.Binding.AllocatedLivePlayfield2);
            Assert.AreNotEqual(
                older.Binding.RuntimeObjectiveIdentity,
                newer.Binding.RuntimeObjectiveIdentity);
            Assert.IsTrue(
                MissionAcgObjectiveContract.TryVerify(
                    older,
                    EventFor(older),
                    out failure),
                failure);
            MissionAcgObjectiveEvent crossed = EventFor(older);
            crossed.RuntimeObjectiveIdentity = newer.Binding.RuntimeObjectiveIdentity;
            Assert.IsFalse(
                MissionAcgObjectiveContract.TryVerify(
                    older,
                    crossed,
                    out failure));
        }

        [TestMethod]
        public void FindPersonFindItemReturnAndRepairUseDistinctCapturedContracts()
        {
            MissionAcgObjectiveRecord person =
                this.CreateObjective(MissionRollType.FindPerson, 30, 400);
            MissionAcgObjectiveRecord find =
                this.CreateObjective(MissionRollType.FindItem, 31, 400);
            MissionAcgObjectiveRecord returned =
                this.CreateObjective(MissionRollType.FindItemReturn, 32, 400);
            MissionAcgObjectiveRecord repair =
                this.CreateObjective(MissionRollType.RepairMachine, 33, 400);
            Assert.AreEqual(0xC350, person.Binding.CapturedObjectiveIdentity.Type);
            Assert.AreEqual(0xC73D, find.Binding.CapturedObjectiveIdentity.Type);
            Assert.AreEqual(0xC74A, returned.Binding.CapturedObjectiveIdentity.Type);
            Assert.AreEqual(
                MissionAcgObjectiveContract.RepairComponentTemplateId,
                repair.Binding.RequiredMissionItemTemplateId);
            Assert.AreEqual(
                MissionAcgObjectiveContract.RepairMachineTemplateId,
                repair.Binding.RequiredMachineTemplateId);
            Assert.AreEqual(
                MissionAcgObjectiveContract.RepairMachineTemplateId,
                repair.Binding.ObjectiveTemplateId);
            Assert.AreEqual(16, MissionAcgObjectiveContract.AcceptedQfuVersionFor(MissionRollType.FindPerson));
            Assert.AreEqual(64, MissionAcgObjectiveContract.AcceptedQfuQuestIdentityFlagFor(MissionRollType.FindPerson));
            Assert.AreEqual(15, MissionAcgObjectiveContract.AcceptedQfuVersionFor(MissionRollType.FindItem));
            Assert.AreEqual(8, MissionAcgObjectiveContract.AcceptedQfuVersionFor(MissionRollType.FindItemReturn));
        }

        [TestMethod]
        public void ReturnAndRepairRequireExactInventoryInstanceAndTerminalOrMachine()
        {
            MissionAcgObjectiveRecord returned =
                WithMissionItem(
                    this.CreateObjective(MissionRollType.FindItemReturn, 40, 500),
                    0x700001);
            MissionAcgObjectiveRecord repair =
                WithMissionItem(
                    this.CreateObjective(MissionRollType.RepairMachine, 41, 500),
                    0x700002);
            string failure;
            Assert.IsTrue(
                MissionAcgObjectiveContract.TryVerify(
                    returned,
                    EventFor(returned),
                    out failure),
                failure);
            MissionAcgObjectiveEvent wrongItem = EventFor(returned);
            wrongItem.MissionItemIdentity =
                new MissionAcgIdentityRecord(0xC76D, 0x700003);
            Assert.IsFalse(
                MissionAcgObjectiveContract.TryVerify(
                    returned,
                    wrongItem,
                    out failure));
            MissionAcgObjectiveEvent wrongTerminal = EventFor(returned);
            wrongTerminal.IssuingTerminalIdentity =
                new MissionAcgIdentityRecord(
                    returned.Binding.IssuingTerminalIdentity.Type,
                    returned.Binding.IssuingTerminalIdentity.Instance + 1);
            Assert.IsFalse(
                MissionAcgObjectiveContract.TryVerify(
                    returned,
                    wrongTerminal,
                    out failure));
            Assert.IsTrue(
                MissionAcgObjectiveContract.TryVerify(
                    repair,
                    EventFor(repair),
                    out failure),
                failure);
            MissionAcgObjectiveEvent wrongComponent = EventFor(repair);
            wrongComponent.MissionItemTemplateId++;
            Assert.IsFalse(
                MissionAcgObjectiveContract.TryVerify(
                    repair,
                    wrongComponent,
                    out failure));
        }

        [TestMethod]
        public void ObjectiveAndCompletionJournalRoundTripPreservesEveryIdentityAndFrozenClaim()
        {
            MissionAcgObjectiveRecord source =
                WithMissionItem(
                    this.CreateObjective(MissionRollType.FindItemReturn, 50, 600),
                    0x710001);
            source =
                source.WithState(
                    source.State.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.CompletionStarted,
                        phase: MissionAcgCompletionPhase.RewardCalculationFrozen,
                        frozenCredits: 1234,
                        frozenXp: 5678,
                        frozenItemLowId: 10,
                        frozenItemHighId: 11,
                        frozenItemQuality: 42,
                        frozenItemCount: 1,
                        creditsClaimId: "credits-claim",
                        xpClaimId: "xp-claim",
                        itemClaimId: "item-claim"));
            var store =
                new MissionAcgObjectiveStore(
                    Path.Combine(this.root, "mission-state"),
                    this.catalog);
            MissionAcgObjectiveRecord persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(source, out persisted, out failure), failure);
            MissionAcgObjectiveLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join(" | ", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Records.Count);
            MissionAcgObjectiveRecord actual = loaded.Records[0];
            Assert.AreEqual(source.Binding.AcceptedQuestIdentity, actual.Binding.AcceptedQuestIdentity);
            Assert.AreEqual(source.Binding.RuntimeObjectiveIdentity, actual.Binding.RuntimeObjectiveIdentity);
            Assert.AreEqual(source.Binding.BuildingIdentity, actual.Binding.BuildingIdentity);
            Assert.AreEqual(source.State.MissionItemIdentity, actual.State.MissionItemIdentity);
            Assert.AreEqual(1234, actual.State.FrozenCredits);
            Assert.AreEqual(5678, actual.State.FrozenXp);
            Assert.AreEqual("credits-claim", actual.State.CreditsClaimId);
            Assert.AreEqual("xp-claim", actual.State.XpClaimId);
            Assert.AreEqual("item-claim", actual.State.ItemClaimId);
        }

        [TestMethod]
        public void JournalRejectsTruncationUnknownVersionIntegrityMismatchAndIgnoresTemp()
        {
            MissionAcgObjectiveRecord source =
                this.CreateObjective(MissionRollType.FindItem, 60, 700);
            var store =
                new MissionAcgObjectiveStore(
                    Path.Combine(this.root, "mission-state"),
                    this.catalog);
            MissionAcgObjectiveRecord persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(source, out persisted, out failure), failure);
            string path = Directory.GetFiles(store.DirectoryPath, "*.objective")[0];
            File.WriteAllText(path + ".partial.tmp", "partial");
            Assert.IsTrue(store.LoadAll().IsValid);

            string valid = File.ReadAllText(path);
            File.WriteAllText(path, "AORebirth-MissionAcgObjective\r\nFormatVersion=1\r\n");
            Assert.IsFalse(store.LoadAll().IsValid);
            File.WriteAllText(path, valid.Replace("ObjectiveTemplateId=", "ObjectiveTemplateId=9"));
            Assert.IsFalse(store.LoadAll().IsValid);
            File.WriteAllText(path, Rehash(valid.Replace("FormatVersion=1", "FormatVersion=99")));
            Assert.IsFalse(store.LoadAll().IsValid);
        }

        [TestMethod]
        public void DurablePhasesAndGrantStatesCannotRegressOrDuplicate()
        {
            MissionAcgObjectiveState state =
                this.CreateObjective(MissionRollType.FindItem, 70, 800).State;
            string failure;
            MissionAcgObjectiveState verified =
                state.Copy(phase: MissionAcgCompletionPhase.ObjectiveVerified);
            Assert.IsTrue(
                MissionAcgCompletionRules.CanReplace(state, verified, out failure),
                failure);
            MissionAcgObjectiveState started =
                verified.Copy(phase: MissionAcgCompletionPhase.CompletionStarted);
            Assert.IsTrue(
                MissionAcgCompletionRules.CanReplace(verified, started, out failure),
                failure);
            MissionAcgObjectiveState frozen =
                started.Copy(
                    phase: MissionAcgCompletionPhase.RewardCalculationFrozen,
                    frozenCredits: 1,
                    frozenXp: 1,
                    itemState: MissionAcgGrantState.ExplicitNone,
                    creditsClaimId: "credits",
                    xpClaimId: "xp",
                    itemClaimId: "item");
            Assert.IsTrue(
                MissionAcgCompletionRules.CanReplace(started, frozen, out failure),
                failure);
            MissionAcgObjectiveState claim =
                frozen.Copy(phase: MissionAcgCompletionPhase.RewardClaimStarted);
            Assert.IsTrue(
                MissionAcgCompletionRules.CanReplace(frozen, claim, out failure),
                failure);
            MissionAcgObjectiveState pending =
                claim.Copy(creditsState: MissionAcgGrantState.Pending);
            Assert.IsTrue(
                MissionAcgCompletionRules.CanReplace(claim, pending, out failure),
                failure);
            MissionAcgObjectiveState granted =
                pending.Copy(
                    phase: MissionAcgCompletionPhase.CreditsGranted,
                    creditsState: MissionAcgGrantState.Granted);
            Assert.IsTrue(MissionAcgCompletionRules.CanReplace(pending, granted, out failure), failure);
            Assert.IsFalse(MissionAcgCompletionRules.CanReplace(granted, pending, out failure));
            Assert.IsFalse(
                MissionAcgCompletionRules.CanReplace(
                    granted,
                    granted.Copy(phase: MissionAcgCompletionPhase.None),
                    out failure));
            Assert.IsFalse(
                MissionAcgCompletionRules.CanReplace(
                    state,
                    state.Copy(
                        phase: MissionAcgCompletionPhase.ObjectiveVerified,
                        creditsState: MissionAcgGrantState.Pending),
                    out failure));
        }

        [TestMethod]
        public void EveryDurableCompletionPhaseRoundTripsForRestartRecovery()
        {
            MissionAcgObjectiveRecord source =
                this.CreateObjective(MissionRollType.FindItem, 71, 801);
            foreach (MissionAcgCompletionPhase phase in
                Enum.GetValues(typeof(MissionAcgCompletionPhase)))
            {
                MissionAcgObjectiveState phaseState =
                    StateAtPhase(source.State, phase);
                string phaseRoot =
                    Path.Combine(this.root, "phase-" + (int)phase);
                var store =
                    new MissionAcgObjectiveStore(phaseRoot, this.catalog);
                MissionAcgObjectiveRecord persisted;
                string failure;
                Assert.IsTrue(
                    store.TryCreate(
                        source.WithState(phaseState),
                        out persisted,
                        out failure),
                    phase + ": " + failure);
                MissionAcgObjectiveLoadResult loaded = store.LoadAll();
                Assert.IsTrue(
                    loaded.IsValid,
                    phase + ": " + string.Join(" | ", loaded.Diagnostics));
                Assert.AreEqual(phase, loaded.Records[0].State.Phase);
                Assert.AreEqual(
                    phase == MissionAcgCompletionPhase.RewardClaimStarted
                        ? MissionAcgGrantState.Pending
                        : phase >= MissionAcgCompletionPhase.CreditsGranted
                          ? MissionAcgGrantState.Granted
                          : phase >= MissionAcgCompletionPhase.RewardCalculationFrozen
                            ? MissionAcgGrantState.NotStarted
                            : MissionAcgGrantState.NotStarted,
                    loaded.Records[0].State.CreditsState);
            }
        }

        [TestMethod]
        public void StageOneTwoThreeAndAllocationRegressionsRemainExact()
        {
            Assert.AreEqual(5, this.catalog.SelectableLayouts.Count);
            Assert.AreEqual(2, MissionAcgInstanceBinding.CurrentFormatVersion);
            Assert.AreEqual(1, MissionAcgObjectiveBinding.CurrentFormatVersion);
            Assert.IsNull(
                this.catalog.FindBySourcePlayfield2(
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2));
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.AreEqual(
                    bundle.ExpectedGeneratorPayloadSha256.ToLowerInvariant(),
                    bundle.GeneratorPayloadSha256.ToLowerInvariant());
                Assert.AreNotEqual(
                    MissionAcgAllocationService.LegacySharedPlayfield2,
                    bundle.SourcePlayfield2);
            }
        }

        private MissionAcgObjectiveRecord CreateObjective(
            MissionRollType type,
            int salt,
            int owner)
        {
            MissionAcgIdentityRecord ownerIdentity =
                new MissionAcgIdentityRecord(0xC350, owner);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(12000 + salt, type, 42, ownerIdentity));
            int livePf = FirstPf(this.catalog) + salt;
            DateTime accepted =
                new DateTime(2026, 7, 28, 20, 0, 0, DateTimeKind.Utc)
                    .AddMinutes(salt);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x51000000 + salt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x11000000 + salt),
                    ownerIdentity,
                    null,
                    type,
                    42,
                    12000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x61000000 + salt),
                    new MissionAcgIdentityRecord(0x9C50, 635),
                    1,
                    2,
                    10,
                    0,
                    20,
                    new MissionAcgIdentityRecord(0xC350, 0x1000 + salt),
                    bundle,
                    livePf,
                    accepted,
                    accepted.AddHours(48));
            var bindingRecord =
                new MissionAcgBindingRecord(
                    binding,
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.Accepted,
                        MissionAcgCleanupState.None,
                        accepted,
                        null),
                    string.Empty);
            MissionAcgMaterializedInstance instance;
            string failure;
            Assert.IsTrue(
                MissionAcgRuntimeMaterializer.TryMaterialize(
                    bindingRecord,
                    bundle,
                    null,
                    accepted,
                    out instance,
                    out failure),
                failure);
            MissionAcgObjectiveSlotRecord slot = bundle.ObjectiveSlots[0];
            MissionAcgRuntimeObject runtime =
                instance.Objects.Single(
                    x => x.Identity.CapturedIdentity.Equals(slot.CapturedIdentity));
            var objectiveBinding =
                new MissionAcgObjectiveBinding(
                    1,
                    binding.AcceptedQuestIdentity,
                    binding.OwnerIdentity,
                    null,
                    true,
                    type,
                    livePf,
                    bundle.LayoutId,
                    bundle.GeneratorPayloadSha256,
                    bundle.BuildingIdentity,
                    slot.Slot,
                    slot.CapturedIdentity,
                    runtime.Identity.RuntimeIdentity,
                    slot.TemplateId,
                    slot.Name,
                    MissionAcgObjectiveContract.InteractionFor(type),
                    type == MissionRollType.FindItemReturn
                        ? binding.IssuingTerminalIdentity
                        : null,
                    type == MissionRollType.RepairMachine
                        ? MissionAcgObjectiveContract.RepairComponentTemplateId
                        : type == MissionRollType.FindItemReturn ? slot.TemplateId : 0,
                    type == MissionRollType.RepairMachine
                        ? MissionAcgObjectiveContract.RepairMachineTemplateId
                        : 0);
            return new MissionAcgObjectiveRecord(
                objectiveBinding,
                NewState(accepted),
                string.Empty);
        }

        private static MissionAcgObjectiveRecord WithMissionItem(
            MissionAcgObjectiveRecord record,
            int instance)
        {
            int type =
                record.Binding.MissionType == MissionRollType.RepairMachine
                    ? 0xC73D
                    : 0xC76D;
            return record.WithState(
                record.State.Copy(
                    lifecycle: MissionAcgObjectiveLifecycle.ItemPossessed,
                    missionItemIdentity:
                        new MissionAcgIdentityRecord(type, instance)));
        }

        private static MissionAcgObjectiveState NewState(DateTime now)
        {
            return new MissionAcgObjectiveState(
                MissionAcgObjectiveLifecycle.Exposed,
                MissionAcgCompletionPhase.None,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                MissionAcgGrantState.NotStarted,
                MissionAcgGrantState.NotStarted,
                MissionAcgGrantState.NotStarted,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                false,
                false,
                false,
                false,
                false,
                now);
        }

        private static MissionAcgObjectiveState StateAtPhase(
            MissionAcgObjectiveState initial,
            MissionAcgCompletionPhase phase)
        {
            if (phase < MissionAcgCompletionPhase.RewardCalculationFrozen)
            {
                return initial.Copy(
                    lifecycle:
                        phase == MissionAcgCompletionPhase.ObjectiveVerified
                            ? MissionAcgObjectiveLifecycle.Verified
                            : phase == MissionAcgCompletionPhase.CompletionStarted
                              ? MissionAcgObjectiveLifecycle.CompletionStarted
                              : initial.Lifecycle,
                    phase: phase);
            }

            MissionAcgGrantState credits =
                phase == MissionAcgCompletionPhase.RewardClaimStarted
                    ? MissionAcgGrantState.Pending
                    : phase >= MissionAcgCompletionPhase.CreditsGranted
                      ? MissionAcgGrantState.Granted
                      : MissionAcgGrantState.NotStarted;
            MissionAcgGrantState xp =
                phase >= MissionAcgCompletionPhase.XpGranted
                    ? MissionAcgGrantState.Granted
                    : MissionAcgGrantState.NotStarted;
            return initial.Copy(
                lifecycle:
                    phase == MissionAcgCompletionPhase.MissionCleanupCompleted
                        ? MissionAcgObjectiveLifecycle.CleanupCompleted
                        : MissionAcgObjectiveLifecycle.CompletionStarted,
                phase: phase,
                frozenCredits: 10,
                frozenXp: 20,
                frozenItemCount: 0,
                creditsState: credits,
                xpState: xp,
                itemState: MissionAcgGrantState.ExplicitNone,
                creditsClaimId: "credits",
                xpClaimId: "xp",
                itemClaimId: "item",
                artifactsRemoved:
                    phase >= MissionAcgCompletionPhase.MissionArtifactsRemoved,
                action59Sent:
                    phase >= MissionAcgCompletionPhase.Action59Sent,
                questDeleteSent:
                    phase >= MissionAcgCompletionPhase.QuestDeleteSent,
                objectiveCleanupCompleted:
                    phase >= MissionAcgCompletionPhase.ObjectiveCleanupCompleted,
                missionCleanupCompleted:
                    phase >= MissionAcgCompletionPhase.MissionCleanupCompleted);
        }

        private static MissionAcgObjectiveEvent EventFor(
            MissionAcgObjectiveRecord record)
        {
            return new MissionAcgObjectiveEvent
                   {
                       OwnerInstance = record.Binding.OwnerIdentity.Instance,
                       TeamIdentity = null,
                       AcceptedQuestInstance =
                           record.Binding.AcceptedQuestIdentity.Instance,
                       AllocatedLivePlayfield2 =
                           record.Binding.AllocatedLivePlayfield2,
                       RuntimeObjectiveIdentity =
                           record.Binding.RuntimeObjectiveIdentity,
                       Interaction = record.Binding.RequiredInteraction,
                       ObjectiveTemplateId =
                           record.Binding.ObjectiveTemplateId,
                       ObjectiveName = record.Binding.ObjectiveName,
                       MissionItemIdentity = record.State.MissionItemIdentity,
                       MissionItemTemplateId =
                           record.Binding.RequiredMissionItemTemplateId,
                       IssuingTerminalIdentity =
                           record.Binding.IssuingTerminalIdentity,
                       ObservationId = "test"
                   };
        }

        private static int ReversePf(MissionAcgObjectiveRecord record)
        {
            int pf;
            int ordinal;
            Assert.IsTrue(
                MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                    record.Binding.RuntimeObjectiveIdentity.Instance,
                    out pf,
                    out ordinal));
            return pf;
        }

        private static int FirstPf(MissionAcgLayoutCatalog catalog)
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private static string ReadSource(string relativePath)
        {
            DirectoryInfo current =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return File.ReadAllText(
                        Path.Combine(current.FullName, relativePath));
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }

        private static string Rehash(string content)
        {
            string[] lines =
                content.Replace("\r\n", "\n")
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var fields =
                lines.Skip(1)
                    .Where(x => !x.StartsWith("RecordSha256=", StringComparison.Ordinal))
                    .OrderBy(x => x.Substring(0, x.IndexOf('=')), StringComparer.Ordinal)
                    .ToArray();
            string canonical = string.Join("\r\n", fields) + "\r\n";
            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                hash = string.Concat(bytes.Select(x => x.ToString("x2")));
            }

            return lines[0]
                   + "\r\n"
                   + canonical
                   + "RecordSha256="
                   + hash
                   + "\r\n";
        }
    }
}
