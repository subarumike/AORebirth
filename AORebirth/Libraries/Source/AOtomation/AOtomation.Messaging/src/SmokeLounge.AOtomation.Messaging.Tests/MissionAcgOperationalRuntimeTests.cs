namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgOperationalRuntimeTests
    {
        private MissionAcgLayoutCatalog catalog;
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-stage5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.temporaryRoot))
            {
                Directory.Delete(this.temporaryRoot, true);
            }
        }

        [TestMethod]
        public void OperationalFormatIsVersionThreeWithExplicitLegacySupport()
        {
            Assert.AreEqual(1, MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion);
            Assert.AreEqual(2, MissionAcgOperationalState.LegacyDeathWitnessFormatVersion);
            Assert.AreEqual(3, MissionAcgOperationalState.CurrentFormatVersion);
            Assert.AreEqual(1, MissionAcgRuntimeState.CurrentFormatVersion);
            Assert.AreEqual(2, MissionAcgInstanceBinding.CurrentFormatVersion);
        }

        [TestMethod]
        public void BoundMissionContentRegistrationReachesOperationalNpcSpawning()
        {
            string module =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MissionInstanceContentModule.cs");
            string npcRuntime =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            string operational =
                ReadSource(
                    @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgOperationalRuntime.cs");

            Assert.IsTrue(
                module.Contains(
                    "return MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance);"));
            Assert.IsFalse(
                module.Contains("!MissionAcgBindingRuntime.IsBoundLivePlayfield"));
            Assert.IsTrue(module.Contains("registration.RegisterCapturedNpcSpawns();"));
            Assert.IsTrue(
                npcRuntime.Contains("MissionAcgOperationalRuntime.TrySpawnForPlayfield"));
            Assert.IsTrue(
                operational.Contains("MissionInstanceMobCombat.RegisterAggressive(mob.Identity);"));
            Assert.IsTrue(operational.Contains("npcState.Level"));
            Assert.IsTrue(
                operational.Contains("MissionNpcDifficultyPolicy.ResolveLevel"));
        }

        [TestMethod]
        public void LegacyCapturedDifficultyMigratesWithoutLosingMutableState()
        {
            MissionAcgBindingRecord record = this.CreateBinding(20, this.FirstPf());
            MissionAcgOperationalState template = this.CreateState(record, false, true);
            MissionAcgOperationalState legacyExpected =
                WithDifficulty(
                    template,
                    MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion,
                    38,
                    1221,
                    1221);
            MissionAcgOperationalState legacyPersisted =
                WithDifficulty(
                    template,
                    MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion,
                    38,
                    1221,
                    610);
            MissionAcgOperationalState currentExpected =
                WithDifficulty(
                    template,
                    MissionAcgOperationalState.CurrentFormatVersion,
                    2,
                    50,
                    50);
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(store.TryWrite(legacyPersisted, false, out failure), failure);

            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            Assert.AreEqual(
                MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion,
                restored.FormatVersion);

            MissionAcgOperationalState migrated;
            Assert.IsTrue(
                MissionAcgOperationalStateMigration.TryUpgradeLegacyDifficulty(
                    restored,
                    legacyExpected,
                    currentExpected,
                    DateTime.UtcNow,
                    out migrated,
                    out failure),
                failure);
            Assert.AreEqual(MissionAcgOperationalState.CurrentFormatVersion, migrated.FormatVersion);
            Assert.AreEqual(2, migrated.Npcs[0].Level);
            Assert.AreEqual(50, migrated.Npcs[0].MaximumHealth);
            Assert.AreEqual(24, migrated.Npcs[0].CurrentHealth);
            Assert.IsTrue(migrated.Chests[0].IsOpen);
            Assert.IsTrue(migrated.Chests[0].IsExhausted);
            Assert.IsTrue(store.TryWrite(migrated, true, out failure), failure);

            MissionAcgOperationalState roundTrip;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out roundTrip, out exists, out failure),
                failure);
            Assert.AreEqual(MissionAcgOperationalState.CurrentFormatVersion, roundTrip.FormatVersion);
            Assert.AreEqual(24, roundTrip.Npcs[0].CurrentHealth);
        }

        [TestMethod]
        public void NpcRuntimeIdentityRoundTripsWithoutCapturedPfLeakage()
        {
            MissionAcgBindingRecord record = this.CreateBinding(1, this.FirstPf());
            MissionAcgOperationalState initial = this.CreateState(record, false, false);
            MissionAcgOperationalState restored = this.RoundTrip(record, initial);
            Assert.AreEqual(initial.Npcs[0].RuntimeIdentity, restored.Npcs[0].RuntimeIdentity);
            Assert.AreEqual(record.Binding.AllocatedLivePlayfield2, restored.AllocatedLivePlayfield2);
            Assert.AreNotEqual(
                this.catalog.FindByLayoutId(record.Binding.SelectedBundleId).SourcePlayfield2,
                restored.AllocatedLivePlayfield2);
        }

        [TestMethod]
        public void SameCapturedSlotInTwoPf2InstancesHasDifferentRuntimeIdentity()
        {
            int firstPf = this.FirstPf();
            int secondPf = firstPf + 1;
            int firstRuntime = RuntimeIdentity(firstPf, 1);
            int secondRuntime = RuntimeIdentity(secondPf, 1);
            Assert.AreNotEqual(firstRuntime, secondRuntime);
            Assert.AreEqual(1, firstRuntime & 0xFF);
            Assert.AreEqual(1, secondRuntime & 0xFF);
        }

        [TestMethod]
        public void DeadNpcAndCorpseOwnershipSurviveRestart()
        {
            MissionAcgBindingRecord record = this.CreateBinding(2, this.FirstPf());
            MissionAcgOperationalState restored =
                this.RoundTrip(record, this.CreateState(record, true, false));
            Assert.AreEqual(MissionAcgNpcLifeState.Dead, restored.Npcs[0].LifeState);
            Assert.AreEqual(0, restored.Npcs[0].CurrentHealth);
            Assert.AreEqual(MissionAcgCorpseState.Available, restored.Npcs[0].CorpseState);
            Assert.AreEqual(
                restored.Npcs[0].RuntimeIdentity.Instance,
                restored.Npcs[0].CorpseIdentity.Instance);
        }

        [TestMethod]
        public void ExactDeathWitnessAndHookCheckpointSurviveRestart()
        {
            MissionAcgBindingRecord record = this.CreateBinding(201, this.FirstPf());
            MissionAcgOperationalState witnessed =
                this.CreateWitnessedDeathState(
                    record,
                    MissionAcgNpcDeathHookCheckpoint.RewardHooksCompleted,
                    record.Binding.OwnerIdentity);
            MissionAcgOperationalState restored = this.RoundTrip(record, witnessed);
            MissionAcgNpcRuntimeState target = restored.Npcs[0];
            Assert.AreEqual(
                record.Binding.OwnerIdentity,
                target.DeathCreditedAttackerIdentity);
            Assert.AreEqual(
                record.Binding.OwnerIdentity,
                target.DeathCreditedOwnerIdentity);
            Assert.AreEqual(1, target.DeathSpawnGeneration);
            Assert.IsTrue(target.DiedAtUtc.HasValue);
            Assert.AreEqual(
                MissionAcgNpcDeathHookCheckpoint.RewardHooksCompleted,
                target.DeathHookCheckpoint);
        }

        [TestMethod]
        public void LegacyVersionTwoDeadStateMigratesWithoutInventingDeathWitness()
        {
            MissionAcgBindingRecord record = this.CreateBinding(202, this.FirstPf());
            MissionAcgOperationalState current = this.CreateState(record, true, false);
            MissionAcgOperationalState legacy =
                WithDifficulty(
                    current,
                    MissionAcgOperationalState.LegacyDeathWitnessFormatVersion,
                    current.Npcs[0].Level,
                    current.Npcs[0].MaximumHealth,
                    current.Npcs[0].CurrentHealth);
            MissionAcgOperationalState expected = this.CreateState(record, false, false);
            MissionAcgOperationalState migrated;
            string failure;
            Assert.IsTrue(
                MissionAcgOperationalStateMigration.TryUpgradeLegacyDeathWitness(
                    legacy,
                    expected,
                    DateTime.UtcNow,
                    out migrated,
                    out failure),
                failure);
            Assert.AreEqual(MissionAcgNpcLifeState.Dead, migrated.Npcs[0].LifeState);
            Assert.AreEqual(
                MissionAcgNpcDeathHookCheckpoint.None,
                migrated.Npcs[0].DeathHookCheckpoint);
            Assert.IsNull(migrated.Npcs[0].DeathCreditedAttackerIdentity);

            MissionAcgObjectiveRecord objective =
                CreateKillObjective(
                    record,
                    migrated.Npcs[0],
                    MissionAcgObjectiveLifecycle.Exposed,
                    MissionAcgCompletionPhase.None);
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsPersistedKillDeathWitnessEligible(
                    record,
                    migrated,
                    objective,
                    migrated.Npcs[0]));
        }

        [TestMethod]
        public void CorpseIdentityIsIsolatedBetweenSimultaneousInstances()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateBinding(3, firstPf);
            MissionAcgBindingRecord second = this.CreateBinding(4, firstPf + 1);
            MissionAcgOperationalState firstState = this.CreateState(first, true, false);
            MissionAcgOperationalState secondState = this.CreateState(second, true, false);
            Assert.AreNotEqual(
                firstState.Npcs[0].CorpseIdentity.Instance,
                secondState.Npcs[0].CorpseIdentity.Instance);
        }

        [TestMethod]
        public void CapturedCorpseCreditRangeIsInclusiveAndNamedAsCurrency()
        {
            Assert.IsFalse(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(20));
            Assert.IsTrue(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(21));
            Assert.IsTrue(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(44));
            Assert.IsTrue(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(87));
            Assert.IsFalse(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(88));
            Assert.AreEqual(21, MissionAcgCorpsePolicy.MinimumCapturedCorpseCredits);
            Assert.AreEqual(87, MissionAcgCorpsePolicy.MaximumCapturedCorpseCredits);
        }

        [TestMethod]
        public void CapturedCorpseCreditsAreDeterministicAndAlwaysWithinRange()
        {
            int livePf2 = this.FirstPf();
            for (int ordinal = 1; ordinal <= 64; ordinal++)
            {
                int runtimeNpc = RuntimeIdentity(livePf2, ordinal);
                int first;
                int second;
                Assert.IsTrue(
                    MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                        runtimeNpc,
                        livePf2,
                        out first));
                Assert.IsTrue(
                    MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                        runtimeNpc,
                        livePf2,
                        out second));
                Assert.AreEqual(first, second);
                Assert.IsTrue(MissionAcgCorpsePolicy.IsCapturedCorpseCreditAmount(first));
            }
        }

        [TestMethod]
        public void CapturedCorpseCreditArithmeticRejectsInvalidAndExtremeIdentities()
        {
            int credits;
            int livePf2 = this.FirstPf();
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                    0,
                    livePf2,
                    out credits));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                    -1,
                    livePf2,
                    out credits));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                    int.MinValue,
                    livePf2,
                    out credits));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                    int.MaxValue,
                    livePf2,
                    out credits));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveCapturedCorpseCredits(
                    RuntimeIdentity(livePf2, 1),
                    MissionAcgAllocationService.LegacySharedPlayfield2,
                    out credits));
        }

        [TestMethod]
        public void CapturedCorpseHashingWidensBeforeMultiplication()
        {
            int mixed;
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveLegacySignedSalt(
                    int.MinValue,
                    int.MaxValue,
                    uint.MaxValue,
                    out mixed));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryResolveLegacySignedSalt(
                    1,
                    0,
                    0x80000000u,
                    out mixed));

            int left = 123456;
            int right = 789;
            uint multiplier = 397u;
            int expected =
                unchecked(
                    (int)(
                        (uint)(((ulong)(uint)left * multiplier) & uint.MaxValue)
                        ^ (uint)right));
            Assert.IsTrue(
                MissionAcgCorpsePolicy.TryResolveLegacySignedSalt(
                    left,
                    right,
                    multiplier,
                    out mixed));
            Assert.AreEqual(expected, mixed);
            Assert.IsTrue(
                MissionAcgCorpsePolicy.TryResolveLegacySignedSalt(
                    int.MaxValue,
                    int.MaxValue,
                    uint.MaxValue,
                    out mixed));
            Assert.AreEqual(
                (int)(Math.Abs((long)mixed) % 67L),
                MissionAcgCorpsePolicy.StableBucket(mixed, 67));
        }

        [TestMethod]
        public void CorpseInteractionDistanceFailsClosedForNonFiniteAndExtremeValues()
        {
            Assert.IsTrue(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(0.0, 5.0));
            Assert.IsTrue(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(5.0, 5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(5.0001, 5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(-1.0, 5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(double.NaN, 5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(
                    double.PositiveInfinity,
                    5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(
                    double.NegativeInfinity,
                    5.0));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsInteractionDistanceAllowed(
                    double.MaxValue,
                    5.0));
        }

        [TestMethod]
        public void ExactGeneratedMissionCorpseAccessRequiresFullOwnershipChain()
        {
            MissionAcgBindingRecord record = this.CreateBinding(31, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, true, false);
            MissionAcgNpcRuntimeState npc = state.Npcs[0];
            string failure;
            Assert.IsTrue(
                MissionAcgCorpsePolicy.TryValidateAccess(
                    state,
                    state.AcceptedQuestIdentity,
                    state.OwnerIdentity,
                    state.AllocatedLivePlayfield2,
                    npc.RuntimeIdentity,
                    npc.CorpseIdentity,
                    state.OwnerIdentity.Instance,
                    true,
                    true,
                    2.0,
                    5.0,
                    out failure),
                failure);

            AssertAccessRejected(
                state,
                new MissionAcgIdentityRecord(
                    state.AcceptedQuestIdentity.Type,
                    state.AcceptedQuestIdentity.Instance + 1),
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                true,
                2.0);
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                new MissionAcgIdentityRecord(
                    state.OwnerIdentity.Type,
                    state.OwnerIdentity.Instance + 1),
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                true,
                2.0);
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2 + 1,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                true,
                2.0);
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance + 1,
                true,
                2.0);
        }

        [TestMethod]
        public void GeneratedMissionCorpseAccessRejectsStaleLifecycleAndIdentity()
        {
            MissionAcgBindingRecord record = this.CreateBinding(32, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, true, false);
            MissionAcgNpcRuntimeState npc = state.Npcs[0];
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                new MissionAcgIdentityRecord(
                    npc.RuntimeIdentity.Type,
                    npc.RuntimeIdentity.Instance + 1),
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                true,
                2.0);
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                new MissionAcgIdentityRecord(
                    npc.CorpseIdentity.Type,
                    npc.CorpseIdentity.Instance + 1),
                state.OwnerIdentity.Instance,
                true,
                2.0);
            AssertAccessRejected(
                state,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                false,
                2.0);
            AssertAccessRejected(
                state.BeginCleanup(DateTime.UtcNow),
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                npc.RuntimeIdentity,
                npc.CorpseIdentity,
                state.OwnerIdentity.Instance,
                true,
                2.0);
        }

        [TestMethod]
        public void GeneratedMissionCorpseAccessSurvivesRestartWithoutReroll()
        {
            MissionAcgBindingRecord record = this.CreateBinding(33, this.FirstPf());
            MissionAcgOperationalState restored =
                this.RoundTrip(record, this.CreateState(record, true, false));
            MissionAcgNpcRuntimeState npc = restored.Npcs[0];
            string failure;
            Assert.IsTrue(
                MissionAcgCorpsePolicy.TryValidateAccess(
                    restored,
                    restored.AcceptedQuestIdentity,
                    restored.OwnerIdentity,
                    restored.AllocatedLivePlayfield2,
                    npc.RuntimeIdentity,
                    npc.CorpseIdentity,
                    restored.OwnerIdentity.Instance,
                    true,
                    true,
                    1.0,
                    5.0,
                    out failure),
                failure);
        }

        [TestMethod]
        public void PersistedKillDeathRequiresExactWitnessOrDurableVerificationToResume()
        {
            MissionAcgBindingRecord record = this.CreateBinding(34, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, true, false);
            MissionAcgNpcRuntimeState target = state.Npcs[0];
            MissionAcgObjectiveRecord objective =
                CreateKillObjective(
                    record,
                    target,
                    MissionAcgObjectiveLifecycle.Exposed,
                    MissionAcgCompletionPhase.None);
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsVerifiedKillDeathRecoveryEligible(
                    objective,
                    target));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsPersistedKillDeathWitnessEligible(
                    record,
                    state,
                    objective,
                    target));

            MissionAcgOperationalState witnessed =
                this.CreateWitnessedDeathState(
                    record,
                    MissionAcgNpcDeathHookCheckpoint.DeathPersisted,
                    record.Binding.OwnerIdentity);
            MissionAcgNpcRuntimeState witnessedTarget = witnessed.Npcs[0];
            MissionAcgObjectiveRecord unverifiedWitnessed =
                CreateKillObjective(
                    record,
                    witnessedTarget,
                    MissionAcgObjectiveLifecycle.Exposed,
                    MissionAcgCompletionPhase.None);
            Assert.IsTrue(
                MissionAcgCorpsePolicy.IsPersistedKillDeathWitnessEligible(
                    record,
                    witnessed,
                    unverifiedWitnessed,
                    witnessedTarget));

            MissionAcgOperationalState wrongAttacker =
                this.CreateWitnessedDeathState(
                    record,
                    MissionAcgNpcDeathHookCheckpoint.DeathPersisted,
                    new MissionAcgIdentityRecord(0xC350, 999999));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsPersistedKillDeathWitnessEligible(
                    record,
                    wrongAttacker,
                    CreateKillObjective(
                        record,
                        wrongAttacker.Npcs[0],
                        MissionAcgObjectiveLifecycle.Exposed,
                        MissionAcgCompletionPhase.None),
                    wrongAttacker.Npcs[0]));

            MissionAcgObjectiveRecord verified =
                CreateKillObjective(
                    record,
                    target,
                    MissionAcgObjectiveLifecycle.Verified,
                    MissionAcgCompletionPhase.ObjectiveVerified);
            Assert.IsTrue(
                MissionAcgCorpsePolicy.IsVerifiedKillDeathRecoveryEligible(
                    verified,
                    target));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsVerifiedKillDeathRecoveryEligible(
                    verified,
                    target.WithCleanup()));

            var expired =
                new MissionAcgBindingRecord(
                    record.Binding,
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.Expired,
                        MissionAcgCleanupState.None,
                        DateTime.UtcNow,
                        null),
                    record.RecordPath);
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsPersistedKillDeathWitnessEligible(
                    expired,
                    witnessed,
                    unverifiedWitnessed,
                    witnessedTarget));
        }

        [TestMethod]
        public void ExactLiveKillCorpseLeaseDefersOnlySuccessfulCompletionCleanup()
        {
            MissionAcgBindingRecord record = this.CreateBinding(35, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, true, false);
            MissionAcgNpcRuntimeState target = state.Npcs[0];
            MissionAcgObjectiveRecord objective =
                CreateKillObjective(
                    record,
                    target,
                    MissionAcgObjectiveLifecycle.CompletionStarted,
                    MissionAcgCompletionPhase.QuestDeleteSent);
            Assert.IsTrue(
                MissionAcgCorpsePolicy.ShouldDeferKillCompletionCleanup(
                    state,
                    objective,
                    record.Binding.AcceptedQuestIdentity,
                    record.Binding.AllocatedLivePlayfield2,
                    true));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.ShouldDeferKillCompletionCleanup(
                    state,
                    objective,
                    record.Binding.AcceptedQuestIdentity,
                    record.Binding.AllocatedLivePlayfield2,
                    false));

            MissionAcgOperationalState despawned =
                state.ReplaceNpc(
                    target.WithCorpseState(MissionAcgCorpseState.Despawned),
                    DateTime.UtcNow);
            Assert.IsFalse(
                MissionAcgCorpsePolicy.ShouldDeferKillCompletionCleanup(
                    despawned,
                    objective,
                    record.Binding.AcceptedQuestIdentity,
                    record.Binding.AllocatedLivePlayfield2,
                    true));
        }

        [TestMethod]
        public void CompletionStartedCorpseAccessRequiresReservedPf2AndNoCleanup()
        {
            Assert.IsTrue(
                MissionAcgCorpsePolicy.IsBindingAccessibleForCorpse(
                    false,
                    true,
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCleanupState.None,
                    true));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsBindingAccessibleForCorpse(
                    false,
                    true,
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    true));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsBindingAccessibleForCorpse(
                    false,
                    true,
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCleanupState.None,
                    false));
            Assert.IsFalse(
                MissionAcgCorpsePolicy.IsBindingAccessibleForCorpse(
                    false,
                    false,
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCleanupState.None,
                    true));
        }

        [TestMethod]
        public void OnlyExactObjectiveCorpseRetirementCanResumeCompletion()
        {
            MissionAcgBindingRecord record = this.CreateBinding(36, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, true, false);
            MissionAcgNpcRuntimeState target = state.Npcs[0];
            MissionAcgObjectiveRecord objective =
                CreateKillObjective(
                    record,
                    target,
                    MissionAcgObjectiveLifecycle.CompletionStarted,
                    MissionAcgCompletionPhase.QuestDeleteSent);
            Assert.IsTrue(
                MissionAcgCorpsePolicy
                    .ShouldResumeCompletionAfterCorpseRetirement(
                        objective,
                        state.AcceptedQuestIdentity,
                        state.OwnerIdentity,
                        state.AllocatedLivePlayfield2,
                        target.RuntimeIdentity));
            Assert.IsFalse(
                MissionAcgCorpsePolicy
                    .ShouldResumeCompletionAfterCorpseRetirement(
                        objective,
                        state.AcceptedQuestIdentity,
                        state.OwnerIdentity,
                        state.AllocatedLivePlayfield2,
                        new MissionAcgIdentityRecord(
                            target.RuntimeIdentity.Type,
                            target.RuntimeIdentity.Instance + 1)));
        }

        [TestMethod]
        public void GeneratedMissionCorpsePolicyIsWiredWithoutChangingOrdinaryCorpseAccess()
        {
            string playfield = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string operational = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgOperationalRuntime.cs");
            string npcRuntime = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            string lifecycle = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectLifecycleRuntimeService.cs");
            string catalog = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs");
            string completion = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgCompletionJournalService.cs");

            StringAssert.Contains(playfield, "TryAuthorizeGeneratedMissionCorpse");
            StringAssert.Contains(playfield, "TryResolveCapturedCorpseCredits");
            StringAssert.Contains(playfield, "CorpseLootRightsPolicy.OwnerOnly");
            StringAssert.Contains(playfield, "if (operationalMissionNpc)");
            StringAssert.Contains(playfield, "lootItems.Clear();");
            StringAssert.Contains(playfield, "generatedLoot.LootUnresolved");
            StringAssert.Contains(playfield, "if (!corpse.IsGeneratedMissionCorpse)");
            StringAssert.Contains(playfield, "CorpseLootRightsPolicy.Public");
            StringAssert.Contains(playfield, "HasExactCorpseLease");
            StringAssert.Contains(playfield, "ResumeForAccepted");
            StringAssert.Contains(playfield, "HandleCorpseSpawnFailed");
            StringAssert.Contains(
                playfield,
                "pendingMissionCorpseCompletionResumes");
            StringAssert.Contains(operational, "TryValidateCorpseAccess");
            StringAssert.Contains(operational, "concrete.DespawnCorpses");
            StringAssert.Contains(
                completion,
                "ShouldDeferKillCompletionCleanup");
            StringAssert.Contains(
                operational,
                "A generated mission PF2 may never fall through to the ordinary");
            StringAssert.Contains(npcRuntime, "operationalDeathAlreadyPersisted");
            StringAssert.Contains(npcRuntime, "action=corpse-and-combat-reward-suppressed");
            Assert.IsTrue(
                npcRuntime.IndexOf(
                    "this.ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);",
                    StringComparison.Ordinal)
                < npcRuntime.IndexOf(
                    "this.rewards.RunNpcDeathRewardHooks",
                    StringComparison.Ordinal));
            StringAssert.Contains(lifecycle, "if (!registerCorpse(target, corpseId))");
            Assert.IsFalse(playfield.Contains("Math.Abs(salt)"));
            Assert.IsFalse(playfield.Contains("credits = 20 +"));
            Assert.IsFalse(catalog.Contains("Math.Abs(salt)"));
        }

        [TestMethod]
        public void BoundMissionPfRejectsEveryUnregisteredDeathAndGenericCorpseFallback()
        {
            string operational = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgOperationalRuntime.cs");
            string playfield = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");

            StringAssert.Contains(
                operational,
                "A generated mission PF2 may never fall through to the ordinary");
            StringAssert.Contains(
                operational,
                "if (!state.TryGetNpc(target.Identity.Instance, out npc))");
            StringAssert.Contains(
                playfield,
                "MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(");
        }

        [TestMethod]
        public void PersistedKillDeathReconcilesOnlyExactCompletionWithoutDuplicateCombatRewards()
        {
            string npcRuntime = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            string objective = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgObjectiveInteractionService.cs");
            string instance = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceService.cs");

            StringAssert.Contains(
                npcRuntime,
                "MissionAcgObjectiveInteractionService");
            StringAssert.Contains(
                npcRuntime,
                "action=corpse-and-combat-reward-suppressed");
            StringAssert.Contains(
                objective,
                "TryResumePersistedTargetDeath");
            StringAssert.Contains(
                objective,
                "IsPersistedKillDeathWitnessEligible");
            StringAssert.Contains(
                objective,
                "\"KillTargetPersistedDeathRecovery\"");
            StringAssert.Contains(
                npcRuntime,
                "persistedDeathWitnessMatchesAttacker");
            StringAssert.Contains(
                npcRuntime,
                "RewardHooksStarted");
            StringAssert.Contains(
                instance,
                "MissionAcgObjectiveInteractionService.TryResumePersistedTargetDeath(");
            StringAssert.Contains(instance, "ENTRY-RECOVER-KILL");
        }

        [TestMethod]
        public void UnresolvedChestIsExplicitlyEmptyAndCannotRefillOnRestart()
        {
            MissionAcgBindingRecord record = this.CreateBinding(5, this.FirstPf());
            MissionAcgOperationalState restored =
                this.RoundTrip(record, this.CreateState(record, false, true));
            Assert.AreEqual(
                MissionAcgLootAuthority.UnresolvedEmpty,
                restored.Chests[0].LootAuthority);
            Assert.IsTrue(restored.Chests[0].IsOpen);
            Assert.IsTrue(restored.Chests[0].IsExhausted);
            Assert.AreEqual(0, restored.Chests[0].TransferredItemCount);
        }

        [TestMethod]
        public void AtomicReplacementPreservesOnlyLatestDurableNpcState()
        {
            MissionAcgBindingRecord record = this.CreateBinding(6, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            MissionAcgOperationalState alive = this.CreateState(record, false, false);
            string failure;
            Assert.IsTrue(store.TryWrite(alive, false, out failure), failure);
            MissionAcgOperationalState dead = this.CreateState(record, true, false);
            Assert.IsTrue(store.TryWrite(dead, true, out failure), failure);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            Assert.AreEqual(MissionAcgNpcLifeState.Dead, restored.Npcs[0].LifeState);
            Assert.IsFalse(File.Exists(store.PathFor(record.Binding.AcceptedQuestIdentity) + ".bak"));
        }

        [TestMethod]
        public void TamperedOperationalSidecarFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(7, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(record, false, false), false, out failure),
                failure);
            string path = store.PathFor(record.Binding.AcceptedQuestIdentity);
            File.AppendAllText(path, "tamper=1\r\n");
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void TruncatedOperationalSidecarFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(8, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(store.PathFor(record.Binding.AcceptedQuestIdentity)));
            File.WriteAllText(
                store.PathFor(record.Binding.AcceptedQuestIdentity),
                "AORebirth.MissionAcgOperationalState\r\n");
            MissionAcgOperationalState restored;
            bool exists;
            string failure;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
            StringAssert.Contains(failure, "truncated");
        }

        [TestMethod]
        public void UnknownOperationalFormatFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(9, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(record, false, false), false, out failure),
                failure);
            string path = store.PathFor(record.Binding.AcceptedQuestIdentity);
            string contents =
                File.ReadAllText(path).Replace(
                    "FormatVersion="
                    + MissionAcgOperationalState.CurrentFormatVersion,
                    "FormatVersion=99");
            File.WriteAllText(path, contents);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void BindingMismatchCannotRedirectOperationalState()
        {
            MissionAcgBindingRecord first = this.CreateBinding(10, this.FirstPf());
            MissionAcgBindingRecord second = this.CreateBinding(11, this.FirstPf() + 1);
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(first, false, false), false, out failure),
                failure);
            string firstPath = store.PathFor(first.Binding.AcceptedQuestIdentity);
            string secondPath = store.PathFor(second.Binding.AcceptedQuestIdentity);
            Directory.CreateDirectory(Path.GetDirectoryName(secondPath));
            File.Copy(firstPath, secondPath);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(second.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void DuplicateNpcRuntimeIdentityIsRejected()
        {
            MissionAcgBindingRecord record = this.CreateBinding(12, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, false, false);
            bool rejected = false;
            try
            {
                new MissionAcgOperationalState(
                    MissionAcgOperationalState.CurrentFormatVersion,
                    state.AcceptedQuestIdentity,
                    state.OwnerIdentity,
                    state.AllocatedLivePlayfield2,
                    state.BundleId,
                    state.BundlePayloadSha256,
                    state.BuildingIdentity,
                    new[] { state.Npcs[0], state.Npcs[0] },
                    state.Chests,
                    MissionAcgOperationalCleanupState.Active,
                    DateTime.UtcNow);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
        }

        [TestMethod]
        public void CleanupIsExactAndIdempotentAtTheStateBoundary()
        {
            MissionAcgBindingRecord record = this.CreateBinding(13, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, false, false);
            MissionAcgOperationalState pending = state.BeginCleanup(DateTime.UtcNow);
            MissionAcgOperationalState completed = pending.CompleteCleanup(DateTime.UtcNow);
            MissionAcgOperationalState repeated = completed.CompleteCleanup(DateTime.UtcNow);
            Assert.AreEqual(MissionAcgOperationalCleanupState.Completed, repeated.CleanupState);
            Assert.IsTrue(repeated.Npcs.All(x => x.CleanupCompleted));
            Assert.IsTrue(repeated.Chests.All(x => x.CleanupCompleted));
        }

        [TestMethod]
        public void AllSelectableBundlesHaveFiniteCapturedNpcCoordinatesAndAttributes()
        {
            Assert.AreEqual(5, this.catalog.SelectableLayouts.Count);
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(bundle.EntryPoint));
                Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(bundle.Exit.Position));
                Assert.IsTrue(bundle.NpcSlots.Count > 0);
                foreach (MissionAcgNpcSlotRecord npc in bundle.NpcSlots)
                {
                    Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(npc.Position));
                    Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(npc.Heading));
                    Assert.IsTrue(npc.TemplateId > 0);
                    Assert.IsTrue(npc.MonsterData > 0);
                    Assert.IsTrue(npc.CapturedLevel > 0);
                    Assert.IsTrue(npc.CapturedHealth > 0);
                }
            }
        }

        [TestMethod]
        public void SharedAndCapturedPf2ValuesRemainBlockedForLiveAllocation()
        {
            Assert.IsFalse(
                MissionAcgAllocationService.IsAllocatableRange(
                    MissionAcgAllocationService.LegacySharedPlayfield2));
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.AreNotEqual(
                    bundle.SourcePlayfield2,
                    this.CreateBinding(20 + bundle.SourcePlayfield2, this.FirstPf()).Binding
                        .AllocatedLivePlayfield2);
            }
        }

        [TestMethod]
        public void IncompleteShape1441804RemainsExcluded()
        {
            MissionAcgLayoutBundle incomplete = this.catalog.FindBySourcePlayfield2(1441804);
            Assert.IsTrue(incomplete == null || !incomplete.IsSelectable);
            Assert.IsFalse(
                this.catalog.SelectableLayouts.Any(x => x.SourcePlayfield2 == 1441804));
        }

        private static MissionAcgObjectiveRecord CreateKillObjective(
            MissionAcgBindingRecord record,
            MissionAcgNpcRuntimeState target,
            MissionAcgObjectiveLifecycle lifecycle,
            MissionAcgCompletionPhase phase)
        {
            var binding =
                new MissionAcgObjectiveBinding(
                    MissionAcgObjectiveBinding.CurrentFormatVersion,
                    record.Binding.AcceptedQuestIdentity,
                    record.Binding.OwnerIdentity,
                    null,
                    true,
                    MissionRollType.KillPerson,
                    record.Binding.AllocatedLivePlayfield2,
                    record.Binding.SelectedBundleId,
                    record.Binding.SelectedBundlePayloadSha256,
                    record.Binding.AcgBuildingIdentity,
                    target.CapturedSlot,
                    target.CapturedIdentity,
                    target.RuntimeIdentity,
                    target.TemplateId,
                    target.Name,
                    MissionAcgObjectiveInteraction.TargetDeath,
                    null,
                    0,
                    0);
            var state =
                new MissionAcgObjectiveState(
                    lifecycle,
                    phase,
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
                    phase >= MissionAcgCompletionPhase.Action59Sent,
                    phase >= MissionAcgCompletionPhase.QuestDeleteSent,
                    phase >= MissionAcgCompletionPhase.ObjectiveCleanupCompleted,
                    phase >= MissionAcgCompletionPhase.MissionCleanupCompleted,
                    DateTime.UtcNow);
            return new MissionAcgObjectiveRecord(binding, state, string.Empty);
        }

        private static void AssertAccessRejected(
            MissionAcgOperationalState state,
            MissionAcgIdentityRecord acceptedQuest,
            MissionAcgIdentityRecord owner,
            int livePlayfield2,
            MissionAcgIdentityRecord runtimeNpc,
            MissionAcgIdentityRecord corpse,
            int looterInstance,
            bool bindingAccessible,
            double distance)
        {
            string failure;
            Assert.IsFalse(
                MissionAcgCorpsePolicy.TryValidateAccess(
                    state,
                    acceptedQuest,
                    owner,
                    livePlayfield2,
                    runtimeNpc,
                    corpse,
                    looterInstance,
                    bindingAccessible,
                    true,
                    distance,
                    5.0,
                    out failure));
            Assert.IsFalse(string.IsNullOrWhiteSpace(failure));
        }

        private MissionAcgOperationalState RoundTrip(
            MissionAcgBindingRecord record,
            MissionAcgOperationalState state)
        {
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(store.TryWrite(state, false, out failure), failure);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            return restored;
        }

        private MissionAcgOperationalState CreateState(
            MissionAcgBindingRecord record,
            bool dead,
            bool openedChest)
        {
            int runtimeInstance =
                RuntimeIdentity(record.Binding.AllocatedLivePlayfield2, 1);
            MissionAcgIdentityRecord runtimeNpc =
                new MissionAcgIdentityRecord(0xC350, runtimeInstance);
            MissionAcgIdentityRecord corpse =
                dead ? new MissionAcgIdentityRecord(0xC76A, runtimeInstance) : null;
            var npc =
                new MissionAcgNpcRuntimeState(
                    0,
                    new MissionAcgIdentityRecord(0xC350, 700001),
                    runtimeNpc,
                    new MissionAcgPointRecord(10.0f, 5.0f, 20.0f),
                    new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                    30369,
                    30369,
                    42,
                    1773,
                    dead ? 0 : 1773,
                    104,
                    null,
                    "Captured Mission NPC",
                    MissionAcgNpcRole.KillTarget,
                    dead ? MissionAcgNpcLifeState.Dead : MissionAcgNpcLifeState.Alive,
                    dead ? MissionAcgNpcCombatState.Dead : MissionAcgNpcCombatState.Stationary,
                    corpse,
                    dead ? MissionAcgCorpseState.Available : MissionAcgCorpseState.None,
                    1,
                    false);
            var chest =
                new MissionAcgChestRuntimeState(
                    0,
                    new MissionAcgIdentityRecord(0xC74F, 800001),
                    new MissionAcgIdentityRecord(
                        0xC74F,
                        RuntimeIdentity(record.Binding.AllocatedLivePlayfield2, 2)),
                    MissionAcgLootAuthority.UnresolvedEmpty,
                    openedChest,
                    openedChest,
                    0,
                    false);
            return new MissionAcgOperationalState(
                MissionAcgOperationalState.CurrentFormatVersion,
                record.Binding.AcceptedQuestIdentity,
                record.Binding.OwnerIdentity,
                record.Binding.AllocatedLivePlayfield2,
                record.Binding.SelectedBundleId,
                record.Binding.SelectedBundlePayloadSha256,
                record.Binding.AcgBuildingIdentity,
                new[] { npc },
                new[] { chest },
                MissionAcgOperationalCleanupState.Active,
                new DateTime(2026, 7, 28, 18, 0, 0, DateTimeKind.Utc));
        }

        private MissionAcgOperationalState CreateWitnessedDeathState(
            MissionAcgBindingRecord record,
            MissionAcgNpcDeathHookCheckpoint checkpoint,
            MissionAcgIdentityRecord creditedAttacker)
        {
            MissionAcgOperationalState state = this.CreateState(record, false, false);
            MissionAcgNpcRuntimeState npc = state.Npcs[0].WithDeath(
                new MissionAcgIdentityRecord(
                    0xC76A,
                    state.Npcs[0].RuntimeIdentity.Instance),
                creditedAttacker,
                record.Binding.OwnerIdentity,
                new DateTime(2026, 7, 28, 18, 30, 0, DateTimeKind.Utc));
            while (npc.DeathHookCheckpoint < checkpoint)
            {
                npc =
                    npc.WithDeathHookCheckpoint(
                        (MissionAcgNpcDeathHookCheckpoint)((int)npc.DeathHookCheckpoint + 1));
            }

            npc = npc.WithCorpseState(MissionAcgCorpseState.Available);
            return state.ReplaceNpc(
                npc,
                new DateTime(2026, 7, 28, 18, 31, 0, DateTimeKind.Utc));
        }

        private static MissionAcgOperationalState WithDifficulty(
            MissionAcgOperationalState state,
            int formatVersion,
            int level,
            int maximumHealth,
            int currentHealth)
        {
            MissionAcgNpcRuntimeState source = state.Npcs[0];
            var npc =
                new MissionAcgNpcRuntimeState(
                    source.CapturedSlot,
                    source.CapturedIdentity,
                    source.RuntimeIdentity,
                    source.Position,
                    source.Heading,
                    source.TemplateId,
                    source.MonsterData,
                    level,
                    maximumHealth,
                    currentHealth,
                    source.MonsterScale,
                    source.HeadMesh,
                    source.Name,
                    source.Role,
                    source.LifeState,
                    source.CombatState,
                    source.CorpseIdentity,
                    source.CorpseState,
                    source.SpawnGeneration,
                    source.CleanupCompleted);
            return new MissionAcgOperationalState(
                formatVersion,
                state.AcceptedQuestIdentity,
                state.OwnerIdentity,
                state.AllocatedLivePlayfield2,
                state.BundleId,
                state.BundlePayloadSha256,
                state.BuildingIdentity,
                new[] { npc },
                state.Chests,
                state.CleanupState,
                DateTime.UtcNow);
        }

        private MissionAcgBindingRecord CreateBinding(int salt, int livePf)
        {
            var owner = new MissionAcgIdentityRecord(0xC350, 10000 + salt);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        2000 + salt,
                        MissionRollType.KillPerson,
                        42,
                        owner));
            DateTime accepted =
                new DateTime(2026, 7, 28, 17, 0, 0, DateTimeKind.Utc).AddSeconds(salt);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x50000000 + salt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x01000000 + salt),
                    owner,
                    null,
                    MissionRollType.KillPerson,
                    42,
                    2000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x60000000 + salt),
                    new MissionAcgIdentityRecord(0x9C50, 710),
                    43308,
                    27595,
                    229.605f,
                    6.504f,
                    452.042f,
                    new MissionAcgIdentityRecord(0xDAC1, 0x1000 + salt),
                    bundle,
                    livePf,
                    accepted,
                    accepted.AddHours(48));
            return new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCleanupState.None,
                    accepted,
                    null),
                string.Empty);
        }

        private int FirstPf()
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private static int RuntimeIdentity(int livePf, int ordinal)
        {
            return unchecked((int)0x60000000)
                   | ((livePf - MissionAcgAllocationService.MinimumLivePlayfield2) << 8)
                   | ordinal;
        }

        private static string ReadSource(string relativePath)
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        }
    }
}
