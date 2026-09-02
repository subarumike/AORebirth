namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    using AORebirth.Interfaces.Persistence.Missions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionDaoArchitectureTests
    {
        [TestMethod]
        public void AdapterPreservesMissionFieldsOrderingAndEmptyResults()
        {
            var dao = new FakeMissionDao
                      {
                          Missions = new List<MissionStateData>
                                     {
                                         CreateMission(71, "quest.z", null, 9),
                                         CreateMission(71, "quest.a", "optional-step", 4)
                                     }
                      };
            var adapter = new MissionDaoRepositoryAdapter(dao);

            IList<MissionStateRecord> missions = adapter.GetMissions(71);

            Assert.AreEqual(2, missions.Count);
            Assert.AreEqual("quest.z", missions[0].QuestId);
            Assert.IsNull(missions[0].CurrentStepId);
            Assert.AreEqual(9L, missions[0].Version);
            Assert.AreEqual("quest.a", missions[1].QuestId);
            Assert.AreEqual("optional-step", missions[1].CurrentStepId);
            Assert.AreEqual(0, new MissionDaoRepositoryAdapter(new FakeMissionDao()).GetMissions(71).Count);
        }

        [TestMethod]
        public void AdapterCopiesOptimisticVersionAndNormalizedKeysBackToDomainRecord()
        {
            var dao = new FakeMissionDao();
            var adapter = new MissionDaoRepositoryAdapter(dao);
            var key = new MissionKey(91, "quest.alpha");
            var record = new MissionStateRecord
                         {
                             CharacterId = 91,
                             QuestId = "quest.alpha",
                             State = ZoneEngine.Core.Missions.MissionLifecycleState.Active,
                             CurrentStepId = "step.one",
                             CreatedAtUtcTicks = 10,
                             UpdatedAtUtcTicks = 11,
                             Version = 3
                         };

            adapter.Execute(
                91,
                transaction =>
                {
                    transaction.SaveMission(key, record);
                    return true;
                });

            Assert.AreEqual(4L, record.Version);
            Assert.AreEqual("quest.alpha", record.QuestId);
            Assert.AreEqual("step.one", record.CurrentStepId);
            Assert.AreEqual(91, dao.LastTransactionCharacterId);
        }

        [TestMethod]
        public void AdapterPropagatesDatabaseFailures()
        {
            var adapter = new MissionDaoRepositoryAdapter(
                new FakeMissionDao { Failure = new InvalidOperationException("database failure") });

            try
            {
                adapter.GetMissions(33);
                Assert.Fail("DAO failure was not propagated.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual("database failure", exception.Message);
            }
        }

        [TestMethod]
        public void MissionRuntimeBoundaryContainsNoSqlOrProviderConstruction()
        {
            string root = FindRepositoryRoot();
            string missionRoot = Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Missions");
            string[] forbidden =
            {
                "System.Data",
                "Dapper",
                "Connector.GetConnection",
                "IDbConnection",
                "MySqlConnection",
                "MySqlMissionRepository",
                "MissionRollFeeClaimRepository"
            };

            foreach (string path in Directory.GetFiles(missionRoot, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string source = File.ReadAllText(path);
                foreach (string token in forbidden)
                {
                    Assert.IsFalse(source.Contains(token), Path.GetFileName(path) + " contains " + token);
                }
            }

            Assert.IsFalse(File.Exists(Path.Combine(missionRoot, "MySqlMissionRepository.cs")));
            string implementation = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Libraries\Source\AORebirth.Database\Domain\Missions\MySqlMissionDao.cs"));
            StringAssert.Contains(implementation, "INSERT INTO missionstates");
            StringAssert.Contains(implementation, "FOR UPDATE");
            StringAssert.Contains(implementation, "connection.BeginTransaction()");

            string contract = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Libraries\Source\AORebirth.Interfaces\Persistence\Missions\IMissionDao.cs"));
            Assert.IsFalse(contract.Contains("System.Data"));
            Assert.IsFalse(contract.Contains("IDbConnection"));
            Assert.IsFalse(contract.Contains("MySql"));
            Assert.IsFalse(contract.Contains("SELECT "));
        }

        private static MissionStateData CreateMission(
            int characterId,
            string questId,
            string currentStepId,
            long version)
        {
            return new MissionStateData
                   {
                       CharacterId = characterId,
                       QuestId = questId,
                       State = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState.Active,
                       CurrentStepId = currentStepId,
                       CreatedAtUtcTicks = 1,
                       UpdatedAtUtcTicks = 2,
                       Version = version
                   };
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            DirectoryInfo directory = new FileInfo(sourcePath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "AORebirth"))
                    && File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }

        private sealed class FakeMissionDao : IMissionDao
        {
            internal FakeMissionDao()
            {
                this.Missions = new List<MissionStateData>();
            }

            internal IList<MissionStateData> Missions { get; set; }
            internal Exception Failure { get; set; }
            internal int LastTransactionCharacterId { get; private set; }

            public MissionStateData GetMission(MissionKeyData key)
            {
                this.ThrowIfRequested();
                foreach (MissionStateData mission in this.Missions)
                {
                    if (mission.CharacterId == key.CharacterId
                        && string.Equals(mission.QuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
                    {
                        return mission.Clone();
                    }
                }

                return null;
            }

            public IList<MissionStateData> GetMissions(int characterId)
            {
                this.ThrowIfRequested();
                var result = new List<MissionStateData>();
                foreach (MissionStateData mission in this.Missions)
                {
                    if (mission.CharacterId == characterId)
                    {
                        result.Add(mission.Clone());
                    }
                }

                return result;
            }

            public MissionCharacterSnapshotData ReadCharacter(int characterId)
            {
                return new MissionCharacterSnapshotData(
                    characterId,
                    this.GetMissions(characterId),
                    null,
                    null,
                    null);
            }

            public string ResolveCharacterAccountKey(int characterId)
            {
                return characterId > 0 ? "account" : null;
            }

            public MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey)
            {
                return null;
            }

            public IList<MissionAccountFlagData> GetAccountFlags(string accountKey)
            {
                return new MissionAccountFlagData[0];
            }

            public MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request)
            {
                throw new NotSupportedException();
            }

            public bool MarkStartAreaSelectionPending(int characterId)
            {
                return characterId > 0;
            }

            public string GetStartAreaSelectionState(int characterId)
            {
                return null;
            }

            public bool TryCompleteStartAreaSelection(int characterId, string selectedState)
            {
                return false;
            }

            public T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation)
            {
                return this.Execute(characterId, null, operation);
            }

            public T Execute<T>(
                int characterId,
                string accountKey,
                Func<IMissionDaoTransaction, T> operation)
            {
                this.ThrowIfRequested();
                this.LastTransactionCharacterId = characterId;
                return operation(new FakeTransaction(characterId, accountKey));
            }

            private void ThrowIfRequested()
            {
                if (this.Failure != null)
                {
                    throw this.Failure;
                }
            }
        }

        private sealed class FakeTransaction : IMissionDaoTransaction
        {
            internal FakeTransaction(int characterId, string accountKey)
            {
                this.CharacterId = characterId;
                this.AccountKey = accountKey;
            }

            public int CharacterId { get; private set; }
            public string AccountKey { get; private set; }

            public MissionStateData GetMission(MissionKeyData key) { return null; }
            public IList<MissionStateData> GetMissions(int characterId) { return new MissionStateData[0]; }

            public void SaveMission(MissionKeyData key, MissionStateData record)
            {
                record.Version = record.Version <= 0 ? 1 : record.Version + 1;
            }

            public MissionObjectiveProgressData GetObjective(MissionObjectiveKeyData key) { return null; }
            public void SaveObjective(MissionObjectiveKeyData key, MissionObjectiveProgressData record) { throw new NotSupportedException(); }
            public bool TryAddObservation(MissionObjectiveObservationData observation) { throw new NotSupportedException(); }
            public MissionFlagData GetFlag(MissionKeyData key, string flagKey) { return null; }
            public void SaveFlag(MissionKeyData key, MissionFlagData flag) { throw new NotSupportedException(); }
            public MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey) { return null; }
            public void SaveAccountFlag(string accountKey, MissionAccountFlagData flag) { throw new NotSupportedException(); }
            public MissionRewardStageData GetReward(MissionRewardKeyData key) { return null; }

            public MissionRewardClaimResultData TryClaimReward(
                MissionRewardKeyData key,
                string rewardType,
                string claimToken,
                long claimedAtUtcTicks,
                long claimExpiresAtUtcTicks)
            {
                throw new NotSupportedException();
            }

            public bool TryMarkRewardApplied(
                MissionRewardKeyData key,
                string claimToken,
                long expectedVersion,
                string effectReference,
                long appliedAtUtcTicks,
                out MissionRewardStageData stage)
            {
                throw new NotSupportedException();
            }

            public bool TryMarkRewardFailed(
                MissionRewardKeyData key,
                string claimToken,
                long expectedVersion,
                string error,
                long failedAtUtcTicks,
                out MissionRewardStageData stage)
            {
                throw new NotSupportedException();
            }

            public MissionAtomicStatRewardResultData TryApplyCharacterStatReward(
                MissionRewardKeyData key,
                string rewardType,
                IList<MissionStatMutationData> mutations,
                string effectReference,
                long appliedAtUtcTicks)
            {
                throw new NotSupportedException();
            }
        }
    }
}
