namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;

    #endregion

    [TestClass]
    public class QuestRuntimePersistenceTests
    {
        private const string B18C = "Mission:5514B18C";
        private const string B18D = "Mission:5514B18D";
        private const string B18E = "Mission:5514B18E";
        private const string B18F = "Mission:5514B18F";
        private const string B194 = "Mission:5514B194";
        private const string Karrec = "Mission:55579381";

        private const string B18CObjective = "mission_5514B18C_objective_questfullupdate";
        private const string B18DObjective = "mission_5514B18D_objective_questfullupdate";
        private const string B18EObjective = "mission_5514B18E_objective_questfullupdate";
        private const string KarrecObjective = "mission_55579381_deliver_offerings";

        [TestMethod]
        public void B18CProgressIsIsolatedAcrossCharacters()
        {
            var repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            Activate(service, 1201, B18C);
            Activate(service, 1202, B18C);

            for (int index = 1; index <= 4; index++)
            {
                Assert.AreEqual(
                    MissionOperationStatus.Applied,
                    Observe(service, 1201, B18C, B18CObjective, "cleaning-robot:" + index).Status);
            }

            Assert.AreEqual(4, service.GetObjective(1201, B18C, B18CObjective).Progress);
            Assert.AreEqual(0, service.GetObjective(1202, B18C, B18CObjective).Progress);
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1201, B18C).State);
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1202, B18C).State);
        }

        [TestMethod]
        public void B18CFinalDuplicateAfterRestartCompletesAndActivatesB18DOnce()
        {
            var state = new InMemoryMissionRepositoryState();
            var firstRepository = new InMemoryMissionRepository(state);
            PersistentMissionService first = Service(firstRepository);
            Activate(first, 1210, B18C);
            for (int index = 1; index <= 5; index++)
            {
                Assert.AreEqual(
                    MissionOperationStatus.Applied,
                    Observe(first, 1210, B18C, B18CObjective, "cleaning-robot:" + index).Status);
            }

            var reconstructedRepository = new InMemoryMissionRepository(state);
            PersistentMissionService reconstructed = Service(reconstructedRepository);
            Assert.AreEqual(
                MissionOperationStatus.AlreadyApplied,
                Observe(reconstructed, 1210, B18C, B18CObjective, "cleaning-robot:5").Status);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                reconstructed.CompleteAndActivateNextMission(1210, B18C, B18D).Status);
            Assert.AreEqual(
                MissionOperationStatus.AlreadyApplied,
                reconstructed.CompleteAndActivateNextMission(1210, B18C, B18D).Status);

            MissionCharacterSnapshot snapshot = reconstructedRepository.ReadCharacter(1210);
            Assert.AreEqual(MissionLifecycleState.Completed, snapshot.Missions.Single(value => value.QuestId == B18C).State);
            Assert.AreEqual(MissionLifecycleState.Active, snapshot.Missions.Single(value => value.QuestId == B18D).State);
            Assert.AreEqual(2, snapshot.Missions.Count);
            Assert.AreEqual(1, snapshot.Objectives.Count(value => value.QuestId == B18D));
        }

        [TestMethod]
        public void RexB18ECompletionRewardsXpAndCreditsOnceAndActivatesB18F()
        {
            var state = new InMemoryMissionRepositoryState();
            var repository = new InMemoryMissionRepository(state);
            PersistentMissionService service = Service(repository);
            ActivateThroughB18E(service, 1301);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                Observe(service, 1301, B18E, B18EObjective, "rex-return:captured").Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.CompleteMission(1301, B18E).Status);

            var coordinator = new MissionRewardCoordinator(repository);
            MissionRewardDefinition rewards = RexStatRewards();
            MissionRewardExecutionResult first = coordinator.ExecuteAtomicCharacterStats(
                1301,
                B18E,
                rewards,
                "capture:rex-b18e-xp-credits");
            MissionRewardExecutionResult duplicate = coordinator.ExecuteAtomicCharacterStats(
                1301,
                B18E,
                rewards,
                "capture:rex-b18e-xp-credits");

            Assert.AreEqual(MissionRewardExecutionStatus.Applied, first.Status);
            Assert.AreEqual(MissionRewardExecutionStatus.AlreadyApplied, duplicate.Status);
            Assert.AreEqual(1040, first.StatValues.Single(value => value.StatId == 61).Value);
            Assert.AreEqual(290, first.StatValues.Single(value => value.StatId == 52).Value);
            Assert.AreEqual(290, first.StatValues.Single(value => value.StatId == 592).Value);
            Assert.AreEqual(290, first.StatValues.Single(value => value.StatId == 57).Value);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(1301, B18E, B18F).Status);
            Assert.AreEqual(
                MissionOperationStatus.AlreadyApplied,
                service.CompleteAndActivateNextMission(1301, B18E, B18F).Status);
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1301, B18F).State);
            Assert.AreEqual(1, repository.ReadCharacter(1301).Rewards.Count);
        }

        [TestMethod]
        public void MarcusB18FInventoryFailureRetriesThenGrantsItemAndB194Once()
        {
            var repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            ActivateThroughB18F(service, 1401);
            Assert.AreEqual(MissionOperationStatus.Applied, service.CompleteMission(1401, B18F).Status);

            var effect = new SequencedEffect(
                MissionRewardEffectResult.RetryableFailure("inventory persistence failed"),
                MissionRewardEffectResult.Applied("inventory-item:296780:character:1401"));
            var coordinator = new MissionRewardCoordinator(repository);
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "compact-fire-suppressant-296780",
                                 RewardType = "inventory-item",
                                 IsResolved = true
                             };

            Assert.AreEqual(
                MissionRewardExecutionStatus.RetryableFailure,
                coordinator.ExecuteExternal(1401, B18F, definition, effect).Status);
            Assert.AreEqual(MissionRewardStatus.Failed, repository.ReadCharacter(1401).Rewards.Single().Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.Applied,
                coordinator.ExecuteExternal(1401, B18F, definition, effect).Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.AlreadyApplied,
                coordinator.ExecuteExternal(1401, B18F, definition, effect).Status);
            Assert.AreEqual(2, effect.CallCount);

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(1401, B18F, B194).Status);
            Assert.AreEqual(
                MissionOperationStatus.AlreadyApplied,
                service.CompleteAndActivateNextMission(1401, B18F, B194).Status);
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1401, B194).State);
            Assert.AreEqual(1, repository.ReadCharacter(1401).Rewards.Count);
        }

        [TestMethod]
        public void KarrecProgressRewardsAndAccountAccessAreScopedAndRetrySafe()
        {
            var repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            Activate(service, 1501, Karrec);
            Activate(service, 1502, Karrec);

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                Observe(service, 1501, Karrec, KarrecObjective, "trade-offering:297042").Status);
            Assert.AreEqual(
                MissionOperationStatus.DuplicateObservation,
                Observe(service, 1501, Karrec, KarrecObjective, "trade-offering:297042").Status);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                Observe(service, 1501, Karrec, KarrecObjective, "trade-offering:297043").Status);
            Assert.AreEqual(0, service.GetObjective(1502, Karrec, KarrecObjective).Progress);
            Assert.AreEqual(MissionOperationStatus.Applied, service.CompleteMission(1501, Karrec).Status);

            var coordinator = new MissionRewardCoordinator(repository);
            MissionRewardExecutionResult sideTokens = coordinator.ExecuteAtomicCharacterStats(
                1501,
                Karrec,
                SideTokenReward(),
                "capture:karrec-side-token-plus-two");
            var researchEffect = new SequencedEffect(
                MissionRewardEffectResult.RetryableFailure("research persistence unavailable"),
                MissionRewardEffectResult.Applied("personal-research-allocation:5000"));
            MissionRewardDefinition researchReward = ResearchReward();
            Assert.AreEqual(
                MissionRewardExecutionStatus.RetryableFailure,
                coordinator.ExecuteExternal(1501, Karrec, researchReward, researchEffect).Status);
            Assert.IsNull(service.GetAccountFlag("account-1501", "totw-wall-access"));

            Assert.AreEqual(
                MissionRewardExecutionStatus.AlreadyApplied,
                coordinator.ExecuteAtomicCharacterStats(
                    1501,
                    Karrec,
                    SideTokenReward(),
                    "capture:karrec-side-token-plus-two").Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.Applied,
                coordinator.ExecuteExternal(1501, Karrec, researchReward, researchEffect).Status);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.SetAccountFlag(1501, "account-1501", Karrec, "totw-wall-access", "completed:" + Karrec).Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.AlreadyApplied,
                coordinator.ExecuteExternal(1501, Karrec, researchReward, researchEffect).Status);

            Assert.AreEqual(2, sideTokens.StatValues.Single(value => value.StatId == 75).Value);
            Assert.AreEqual(2, researchEffect.CallCount);
            Assert.IsNotNull(service.GetAccountFlag("account-1501", "totw-wall-access"));
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1502, Karrec).State);
            Assert.AreEqual(2, repository.ReadCharacter(1501).Rewards.Count);
        }

        [TestMethod]
        public void AtomicHandoffRejectsTerminalNextMissionWithoutCompletingCurrentMission()
        {
            var repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            Activate(service, 1601, B18C);
            for (int index = 1; index <= 5; index++)
            {
                Observe(service, 1601, B18C, B18CObjective, "cleaning-robot:" + index);
            }

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(1601, B18C, B18D).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.OfferMission(1601, Karrec).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.AbandonMission(1601, Karrec).Status);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                Observe(service, 1601, B18D, B18DObjective, "cargo-box:captured").Status);

            Assert.AreEqual(
                MissionOperationStatus.Rejected,
                service.CompleteAndActivateNextMission(1601, B18D, Karrec).Status);
            Assert.AreEqual(MissionLifecycleState.Active, service.GetMission(1601, B18D).State);
            Assert.AreEqual(MissionLifecycleState.Abandoned, service.GetMission(1601, Karrec).State);
        }

        [TestMethod]
        public void B18CPerKillClientFeedbackPolicyRemainsFailClosed()
        {
            Assert.IsFalse(RexB18CFeedbackPolicy.ShouldSendPerKillFeedback(1, 5));
            Assert.IsFalse(RexB18CFeedbackPolicy.ShouldSendPerKillFeedback(5, 5));
        }

        private static PersistentMissionService Service(IMissionRepository repository)
        {
            return new PersistentMissionService(repository, Definitions(), () => DateTime.UtcNow.Ticks);
        }

        private static IEnumerable<MissionDefinition> Definitions()
        {
            return new[]
                   {
                       Definition(B18C, "kill", B18CObjective, 5),
                       Definition(B18D, "use", B18DObjective, 1, B18C),
                       Definition(B18E, "talk", B18EObjective, 1, B18D),
                       Definition(B18F, "talk-to-marcus", null, 0, B18E),
                       Definition(B194, "captured-preview", null, 0, B18F),
                       Definition(Karrec, "deliver", KarrecObjective, 2)
                   };
        }

        private static MissionDefinition Definition(
            string questId,
            string stepId,
            string objectiveId,
            int requiredCount,
            params string[] prerequisites)
        {
            return new MissionDefinition
                   {
                       QuestId = questId,
                       InitialStepId = stepId,
                       IsResolved = true,
                       StepIds = new[] { stepId },
                       PrerequisiteQuestIds = prerequisites ?? new string[0],
                       Objectives = string.IsNullOrWhiteSpace(objectiveId)
                                        ? new MissionObjectiveDefinition[0]
                                        : new[]
                                          {
                                              new MissionObjectiveDefinition
                                              {
                                                  ObjectiveId = objectiveId,
                                                  StepId = stepId,
                                                  RequiredCount = requiredCount,
                                                  IsResolved = true
                                              }
                                          }
                   };
        }

        private static void Activate(PersistentMissionService service, int characterId, string questId)
        {
            Assert.AreEqual(MissionOperationStatus.Applied, service.OfferMission(characterId, questId).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.AcceptMission(characterId, questId).Status);
        }

        private static MissionOperationResult Observe(
            PersistentMissionService service,
            int characterId,
            string questId,
            string objectiveId,
            string observationKey)
        {
            return service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = objectiveId,
                    ObservationKey = observationKey,
                    Amount = 1,
                    EventType = "capture-backed-test",
                    SourceIdentity = "character:" + characterId,
                    TargetIdentity = observationKey
                });
        }

        private static void ActivateThroughB18E(PersistentMissionService service, int characterId)
        {
            Activate(service, characterId, B18C);
            for (int index = 1; index <= 5; index++)
            {
                Observe(service, characterId, B18C, B18CObjective, "cleaning-robot:" + index);
            }

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(characterId, B18C, B18D).Status);
            Observe(service, characterId, B18D, B18DObjective, "cargo-box:captured");
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(characterId, B18D, B18E).Status);
        }

        private static void ActivateThroughB18F(PersistentMissionService service, int characterId)
        {
            ActivateThroughB18E(service, characterId);
            Observe(service, characterId, B18E, B18EObjective, "rex-return:captured");
            Assert.AreEqual(MissionOperationStatus.Applied, service.CompleteMission(characterId, B18E).Status);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteAndActivateNextMission(characterId, B18E, B18F).Status);
        }

        private static MissionRewardDefinition RexStatRewards()
        {
            return new MissionRewardDefinition
                   {
                       RewardKey = "rex-b18e-xp-credits",
                       RewardType = "character-stats",
                       IsResolved = true,
                       StatMutations = new[]
                                       {
                                           AddStat(61, 1040),
                                           AddStat(52, 290),
                                           AddStat(592, 290),
                                           SetStat(57, 290)
                                       }
                   };
        }

        private static MissionRewardDefinition SideTokenReward()
        {
            return new MissionRewardDefinition
                   {
                       RewardKey = "side-tokens-2",
                       RewardType = "character-stats",
                       IsResolved = true,
                       StatMutations = new[] { AddStat(75, 2) }
                   };
        }

        private static MissionRewardDefinition ResearchReward()
        {
            return new MissionRewardDefinition
                   {
                       RewardKey = "personal-research-xp-5000",
                       RewardType = "personal-research-allocation",
                       IsResolved = true
                   };
        }

        private static MissionCharacterStatMutation AddStat(int statId, long value)
        {
            return new MissionCharacterStatMutation
                   {
                       StatIdentityType = 50000,
                       StatId = statId,
                       Kind = MissionStatMutationKind.AddClamped,
                       Value = value,
                       MinimumValue = 0,
                       MaximumValue = uint.MaxValue
                   };
        }

        private static MissionCharacterStatMutation SetStat(int statId, long value)
        {
            MissionCharacterStatMutation mutation = AddStat(statId, value);
            mutation.Kind = MissionStatMutationKind.Set;
            return mutation;
        }

        private sealed class SequencedEffect : IMissionRewardEffect
        {
            private readonly Queue<MissionRewardEffectResult> results;

            public SequencedEffect(params MissionRewardEffectResult[] results)
            {
                this.results = new Queue<MissionRewardEffectResult>(results);
            }

            public int CallCount { get; private set; }

            public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
            {
                this.CallCount++;
                return this.results.Dequeue();
            }
        }
    }
}
