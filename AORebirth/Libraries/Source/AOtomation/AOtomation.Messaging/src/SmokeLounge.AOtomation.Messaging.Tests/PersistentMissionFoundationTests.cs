namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    #endregion

    [TestClass]
    public class PersistentMissionFoundationTests
    {
        private const string FirstQuest = "Mission:FOUNDATION-A";
        private const string SecondQuest = "Mission:FOUNDATION-B";

        [TestMethod]
        public void MissionStateDirectoryUsesConfiguredWritableStateDirectory()
        {
            string previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR");
            string previousZone = Environment.GetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR");
            string configured = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-mission-state",
                Guid.NewGuid().ToString("N"));

            try
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", configured);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", null);

                Assert.AreEqual(
                    Path.GetFullPath(configured),
                    MissionStateDirectory.Resolve());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", previous);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", previousZone);
            }
        }

        [TestMethod]
        public void MissionStateDirectoryUsesExistingZoneStateDirectoryConfiguration()
        {
            string previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR");
            string previousZone = Environment.GetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR");
            string configured = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-zone-state",
                Guid.NewGuid().ToString("N"));

            try
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", null);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", configured);

                Assert.AreEqual(
                    Path.GetFullPath(configured),
                    MissionStateDirectory.Resolve());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", previous);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", previousZone);
            }
        }

        [TestMethod]
        public void MissionStateDirectoryFallsBackToReleaseBaseMissionStateDirectory()
        {
            string previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR");
            string previousZone = Environment.GetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR");

            try
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", null);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", null);

                Assert.AreEqual(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-state"),
                    MissionStateDirectory.Resolve());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MISSION_STATE_DIR", previous);
                Environment.SetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR", previousZone);
            }
        }

        [TestMethod]
        public void SameQuestProgressIsIsolatedByStableCharacterIdentity()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);

            Activate(service, 1001, FirstQuest);
            Activate(service, 1002, FirstQuest);

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.ObserveObjective(Observation(1001, "death:robot:1")).Status);

            MissionCharacterSnapshot first = repository.ReadCharacter(1001);
            MissionCharacterSnapshot second = repository.ReadCharacter(1002);
            Assert.AreEqual(1, first.Objectives.Single().Progress);
            Assert.AreEqual(0, second.Objectives.Single().Progress);
            Assert.AreEqual(MissionLifecycleState.Active, first.Missions.Single().State);
            Assert.AreEqual(MissionLifecycleState.Active, second.Missions.Single().State);
        }

        [TestMethod]
        public void DurableObservationKeyPreventsDuplicateProgressAcrossRepositoryReconstruction()
        {
            var durableState = new InMemoryMissionRepositoryState();
            var firstRepository = new InMemoryMissionRepository(durableState);
            PersistentMissionService firstService = Service(firstRepository);
            Activate(firstService, 1010, FirstQuest);

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                firstService.ObserveObjective(Observation(1010, "death:robot:generation-7")).Status);

            var reconstructedRepository = new InMemoryMissionRepository(durableState);
            PersistentMissionService reconstructedService = Service(reconstructedRepository);
            Assert.AreEqual(
                MissionOperationStatus.DuplicateObservation,
                reconstructedService.ObserveObjective(Observation(1010, "death:robot:generation-7")).Status);
            Assert.AreEqual(1, reconstructedRepository.ReadCharacter(1010).Objectives.Single().Progress);
        }

        [TestMethod]
        public void ServiceAndRepositoryReconstructionReloadsMissionObjectivesFlagsAndLifecycle()
        {
            var durableState = new InMemoryMissionRepositoryState();
            var firstRepository = new InMemoryMissionRepository(durableState);
            PersistentMissionService firstService = Service(firstRepository);
            Activate(firstService, 1020, FirstQuest);
            firstService.ObserveObjective(Observation(1020, "death:robot:1"));
            firstService.SetFlag(1020, FirstQuest, "handoff", "pending");

            var reconstructedRepository = new InMemoryMissionRepository(durableState);
            PersistentMissionService reconstructedService = Service(reconstructedRepository);
            MissionReloadResult reload = reconstructedService.ReloadAfterZoneEngineRestart(1020);

            Assert.AreEqual(MissionReloadReason.ZoneEngineRestart, reload.Reason);
            Assert.IsFalse(reload.ClientJournalReconciliationSupported);
            Assert.AreEqual(MissionLifecycleState.Active, reload.Snapshot.Missions.Single().State);
            Assert.AreEqual(1, reload.Snapshot.Objectives.Single().Progress);
            Assert.AreEqual("pending", reload.Snapshot.Flags.Single().Value);
        }

        [TestMethod]
        public void PrerequisitesAndInvalidTransitionsAreEnforcedPerCharacter()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);

            Assert.AreEqual(MissionOperationStatus.NotFound, service.AcceptMission(1030, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.OfferMission(1030, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Rejected, service.CompleteMission(1030, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Rejected, service.FailMission(1030, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Rejected, service.OfferMission(1030, SecondQuest).Status);

            Assert.AreEqual(MissionOperationStatus.Applied, service.AcceptMission(1030, FirstQuest).Status);
            service.ObserveObjective(Observation(1030, "death:robot:1"));
            service.ObserveObjective(Observation(1030, "death:robot:2"));
            Assert.AreEqual(MissionOperationStatus.Applied, service.CompleteMission(1030, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.OfferMission(1030, SecondQuest).Status);

            Assert.AreEqual(
                MissionOperationStatus.Rejected,
                service.OfferMission(1031, SecondQuest).Status,
                "Another character must satisfy the prerequisite independently.");
            Assert.AreEqual(MissionLifecycleState.Completed, service.GetMission(1030, FirstQuest).State);
            Assert.IsNull(service.GetMission(1031, FirstQuest));
        }

        [TestMethod]
        public void TerminalMissionDoesNotBecomeImplicitlyRepeatable()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);

            Assert.AreEqual(MissionOperationStatus.Applied, service.OfferMission(1040, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Applied, service.AbandonMission(1040, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.AlreadyApplied, service.AbandonMission(1040, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Rejected, service.OfferMission(1040, FirstQuest).Status);
            Assert.AreEqual(MissionOperationStatus.Rejected, service.AcceptMission(1040, FirstQuest).Status);
        }

        [TestMethod]
        public void FailedExternalRewardRemainsRetryableAndSuccessfulRetryIsNotDuplicated()
        {
            var clock = new DeterministicClock();
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository, clock);
            CompleteNoObjectiveQuest(service, 1050, SecondQuest, 1050);

            var effect = new SequencedEffect(
                MissionRewardEffectResult.RetryableFailure("inventory persistence failed"),
                MissionRewardEffectResult.Applied("inventory:item:296780"));
            var coordinator = new MissionRewardCoordinator(
                repository,
                clock.Next,
                new DeterministicTokenFactory().Next,
                TimeSpan.FromMinutes(1));
            MissionRewardDefinition reward = ExternalReward();

            MissionRewardExecutionResult failed = coordinator.ExecuteExternal(1050, SecondQuest, reward, effect);
            MissionRewardExecutionResult retried = coordinator.ExecuteExternal(1050, SecondQuest, reward, effect);
            MissionRewardExecutionResult duplicate = coordinator.ExecuteExternal(1050, SecondQuest, reward, effect);

            Assert.AreEqual(MissionRewardExecutionStatus.RetryableFailure, failed.Status);
            Assert.AreEqual(MissionRewardExecutionStatus.Applied, retried.Status);
            Assert.AreEqual(MissionRewardExecutionStatus.AlreadyApplied, duplicate.Status);
            Assert.AreEqual(2, effect.CallCount, "Applied ledger state must suppress a duplicate external effect.");
            MissionRewardStageRecord stage = repository.ReadCharacter(1050).Rewards.Single();
            Assert.AreEqual(MissionRewardStatus.Applied, stage.Status);
            Assert.AreEqual(2, stage.Attempts);
            Assert.AreEqual("inventory:item:296780", stage.EffectReference);
        }

        [TestMethod]
        public void AtomicCharacterStatRewardAndLedgerApplyOnceInOneRepositoryTransaction()
        {
            var clock = new DeterministicClock();
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository, clock);
            CompleteNoObjectiveQuest(service, 1060, SecondQuest, 1060);
            repository.SeedCharacterStat(1060, 50000, 61, 1000);

            var coordinator = new MissionRewardCoordinator(
                repository,
                clock.Next,
                new DeterministicTokenFactory().Next,
                TimeSpan.FromMinutes(1));
            var reward = new MissionRewardDefinition
                         {
                             RewardKey = "credits",
                             RewardType = "CharacterStats",
                             IsResolved = true,
                             StatMutations = new[]
                                             {
                                                 new MissionCharacterStatMutation
                                                 {
                                                     StatIdentityType = 50000,
                                                     StatId = 61,
                                                     Kind = MissionStatMutationKind.AddClamped,
                                                     Value = 1040,
                                                     MinimumValue = 0,
                                                     MaximumValue = 999999999
                                                 }
                                             }
                         };

            Assert.AreEqual(
                MissionRewardExecutionStatus.Applied,
                coordinator.ExecuteAtomicCharacterStats(1060, SecondQuest, reward, "cash:+1040").Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.AlreadyApplied,
                coordinator.ExecuteAtomicCharacterStats(1060, SecondQuest, reward, "cash:+1040").Status);
            Assert.AreEqual(2040, repository.GetCharacterStat(1060, 50000, 61));
            Assert.AreEqual(1, repository.ReadCharacter(1060).Rewards.Single().Attempts);
        }

        [TestMethod]
        public void RewardsFailClosedWhenDefinitionEffectOrMissionCompletionIsUnresolved()
        {
            var clock = new DeterministicClock();
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository, clock);
            Activate(service, 1070, FirstQuest);
            var coordinator = new MissionRewardCoordinator(
                repository,
                clock.Next,
                new DeterministicTokenFactory().Next,
                TimeSpan.FromMinutes(1));

            MissionRewardDefinition unresolved = ExternalReward();
            unresolved.IsResolved = false;
            Assert.AreEqual(
                MissionRewardExecutionStatus.Unresolved,
                coordinator.ExecuteExternal(1070, FirstQuest, unresolved, new SequencedEffect()).Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.Unresolved,
                coordinator.ExecuteExternal(1070, FirstQuest, ExternalReward(), null).Status);
            Assert.AreEqual(
                MissionRewardExecutionStatus.Rejected,
                coordinator.ExecuteExternal(
                    1070,
                    FirstQuest,
                    ExternalReward(),
                    new SequencedEffect(MissionRewardEffectResult.Applied("must-not-run"))).Status);
            Assert.AreEqual(0, repository.ReadCharacter(1070).Rewards.Count);
        }

        [TestMethod]
        public void AccountAccessFlagRequiresExplicitStableAccountScopeAndIsSharedAcrossCharacters()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            ActivateNoObjectiveQuest(service, 1080, SecondQuest, 1080);

            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteMissionWithAccountFlag(
                    1080,
                    "account-user",
                    SecondQuest,
                    "subway.wall-picture-access",
                    "granted").Status);
            MissionAccountFlagRecord flag = service.GetAccountFlag(
                "account-user",
                "subway.wall-picture-access");
            Assert.IsNotNull(flag);
            Assert.AreEqual(SecondQuest, flag.SourceQuestId);
            Assert.AreEqual("granted", flag.Value);

            AssertSameAccountFlag(
                flag,
                repository.GetAccountFlag("ACCOUNT-USER", "subway.wall-picture-access"));
            Assert.IsNull(repository.GetAccountFlag("different-account", "subway.wall-picture-access"));
            AssertThrows<InvalidOperationException>(
                () => repository.Execute(
                    1081,
                    transaction =>
                    {
                        transaction.SaveAccountFlag(
                            "account-user",
                            new MissionAccountFlagRecord
                            {
                                AccountKey = "account-user",
                                FlagKey = "illegal-character-only-write"
                            });
                        return true;
                    }));
        }

        [TestMethod]
        public void CombinedAccountFlagCompletionRollsBackOnConflictingAccountOwnership()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            PersistentMissionService service = Service(repository);
            ActivateNoObjectiveQuest(service, 1090, SecondQuest, 1090);
            Assert.AreEqual(
                MissionOperationStatus.Applied,
                service.CompleteMissionWithAccountFlag(1090, "user-a", SecondQuest, "access", "yes").Status);

            ActivateNoObjectiveQuest(service, 1091, SecondQuest, 1091);
            Assert.AreEqual(
                MissionOperationStatus.Rejected,
                service.CompleteMissionWithAccountFlag(1091, "user-a", SecondQuest, "access", "no").Status);
            Assert.AreEqual(
                MissionLifecycleState.Active,
                service.GetMission(1091, SecondQuest).State,
                "A conflicting account flag must not partially commit mission completion.");
        }

        [TestMethod]
        public void RepositoryTransactionExceptionDoesNotCommitPartialMissionState()
        {
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            AssertThrows<InvalidOperationException>(
                () => repository.Execute<bool>(
                    1100,
                    transaction =>
                    {
                        var key = new MissionKey(1100, "Mission:ROLLBACK");
                        transaction.SaveMission(
                            key,
                            new MissionStateRecord
                            {
                                CharacterId = 1100,
                                QuestId = "Mission:ROLLBACK",
                                State = MissionLifecycleState.Offered,
                                CreatedAtUtcTicks = 1,
                                UpdatedAtUtcTicks = 1
                            });
                        throw new InvalidOperationException("rollback");
                    }));
            Assert.IsNull(repository.GetMission(new MissionKey(1100, "Mission:ROLLBACK")));
        }

        [TestMethod]
        public void UnresolvedObjectiveAndInvalidStableIdentityFailClosed()
        {
            var unresolvedQuest = new MissionDefinition
                                  {
                                      QuestId = "Mission:UNRESOLVED-OBJECTIVE",
                                      InitialStepId = "step",
                                      IsResolved = true,
                                      StepIds = new[] { "step" },
                                      Objectives = new[]
                                                   {
                                                       new MissionObjectiveDefinition
                                                       {
                                                           ObjectiveId = "unknown",
                                                           StepId = "step",
                                                           RequiredCount = 0,
                                                           IsResolved = false
                                                       }
                                                   }
                                  };
            InMemoryMissionRepository repository = new InMemoryMissionRepository();
            var service = new PersistentMissionService(repository, new[] { unresolvedQuest }, () => 100L);
            Activate(service, 1110, unresolvedQuest.QuestId);

            Assert.AreEqual(
                MissionOperationStatus.Unresolved,
                service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = 1110,
                        QuestId = unresolvedQuest.QuestId,
                        ObjectiveId = "unknown",
                        ObservationKey = "unknown:1",
                        EventType = "Unproven"
                    }).Status);
            Assert.AreEqual(MissionOperationStatus.Unresolved, service.OfferMission(0, unresolvedQuest.QuestId).Status);
            Assert.AreEqual(MissionOperationStatus.Unresolved, service.SetFlag(1110, null, "x", "y").Status);
        }

        private static PersistentMissionService Service(
            IMissionRepository repository,
            DeterministicClock clock = null)
        {
            return new PersistentMissionService(
                repository,
                Definitions(),
                clock == null ? (Func<long>)(() => 100L) : clock.Next);
        }

        private static IEnumerable<MissionDefinition> Definitions()
        {
            return new[]
                   {
                       new MissionDefinition
                       {
                           QuestId = FirstQuest,
                           InitialStepId = "kill",
                           IsResolved = true,
                           StepIds = new[] { "kill" },
                           Objectives = new[]
                                        {
                                            new MissionObjectiveDefinition
                                            {
                                                ObjectiveId = "kill-two",
                                                StepId = "kill",
                                                RequiredCount = 2,
                                                IsResolved = true
                                            }
                                        }
                       },
                       new MissionDefinition
                       {
                           QuestId = SecondQuest,
                           InitialStepId = "talk",
                           IsResolved = true,
                           StepIds = new[] { "talk" },
                           PrerequisiteQuestIds = new[] { FirstQuest }
                       }
                   };
        }

        private static MissionObjectiveObservation Observation(int characterId, string key)
        {
            return new MissionObjectiveObservation
                   {
                       CharacterId = characterId,
                       QuestId = FirstQuest,
                       ObjectiveId = "kill-two",
                       ObservationKey = key,
                       Amount = 1,
                       EventType = "Kill",
                       SourceIdentity = "SimpleChar:" + characterId,
                       TargetIdentity = "SimpleChar:ROBOT"
                   };
        }

        private static void Activate(PersistentMissionService service, int characterId, string questId)
        {
            Assert.IsTrue(service.OfferMission(characterId, questId).Succeeded);
            Assert.IsTrue(service.AcceptMission(characterId, questId).Succeeded);
        }

        private static void CompleteNoObjectiveQuest(
            PersistentMissionService service,
            int characterId,
            string questId,
            int prerequisiteCharacterId)
        {
            ActivateNoObjectiveQuest(service, characterId, questId, prerequisiteCharacterId);
            Assert.IsTrue(service.CompleteMission(characterId, questId).Succeeded);
        }

        private static void ActivateNoObjectiveQuest(
            PersistentMissionService service,
            int characterId,
            string questId,
            int prerequisiteCharacterId)
        {
            Activate(service, prerequisiteCharacterId, FirstQuest);
            service.ObserveObjective(Observation(prerequisiteCharacterId, "prerequisite:1"));
            service.ObserveObjective(Observation(prerequisiteCharacterId, "prerequisite:2"));
            Assert.IsTrue(service.CompleteMission(prerequisiteCharacterId, FirstQuest).Succeeded);
            Activate(service, characterId, questId);
        }

        private static void AssertSameAccountFlag(
            MissionAccountFlagRecord expected,
            MissionAccountFlagRecord actual)
        {
            Assert.IsNotNull(actual);
            Assert.IsTrue(string.Equals(expected.AccountKey, actual.AccountKey, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(expected.FlagKey, actual.FlagKey, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(expected.Value, actual.Value);
            Assert.AreEqual(expected.SourceQuestId, actual.SourceQuestId);
        }

        private static MissionRewardDefinition ExternalReward()
        {
            return new MissionRewardDefinition
                   {
                       RewardKey = "item-296780",
                       RewardType = "InventoryItem",
                       IsResolved = true
                   };
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected " + typeof(TException).Name + ".");
            }
            catch (TException)
            {
            }
        }

        private sealed class DeterministicClock
        {
            private long value = 1000;

            public long Next()
            {
                return ++this.value;
            }
        }

        private sealed class DeterministicTokenFactory
        {
            private int value;

            public string Next()
            {
                return "claim-" + (++this.value);
            }
        }

        private sealed class SequencedEffect : IMissionRewardEffect
        {
            private readonly Queue<MissionRewardEffectResult> results;

            public SequencedEffect(params MissionRewardEffectResult[] results)
            {
                this.results = new Queue<MissionRewardEffectResult>(results ?? new MissionRewardEffectResult[0]);
            }

            public int CallCount { get; private set; }

            public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
            {
                this.CallCount++;
                return this.results.Count == 0
                           ? MissionRewardEffectResult.RetryableFailure("no configured effect result")
                           : this.results.Dequeue();
            }
        }
    }

}
