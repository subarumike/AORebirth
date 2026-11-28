namespace ZoneEngine_New.Tests;

using System;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Quests;

[TestClass]
public sealed class AuthoredRewardModuleTests
{
    [TestMethod]
    public void AuthoredRewardReplayRollsBackOtherMutationsInTheSameAction()
    {
        var player = TestWorld.CreatePlayer(111);
        player.Stats.Set(CharacterStat.Cash, 100);
        player.Stats.Set(CharacterStat.XP, 200);
        var dao = new AuthoredMissionTestDao();
        var module = new RewardExecutionModule(dao, new StubItemBuilder(), new StubLogger());
        var quest = new MissionKeyData(111, "authored-reward");
        dao.SaveMission(quest, new MissionStateData
        {
            CharacterId = 111, QuestId = quest.QuestId, State = MissionLifecycleState.Completed
        });

        void RunAction(int itemId)
        {
            dao.Execute(111, transaction =>
            {
                ((IMissionInventoryMutationTransaction)transaction).ApplyInventoryMutation(
                    [new MissionItemInstanceData
                    {
                        InstanceId = itemId, LowId = 30, HighId = 30, Quality = 1, StackCount = 1,
                        ContainerType = 104, ContainerInstance = 111, ContainerPlacement = 64
                    }], []);
                return module.ApplyAuthoredStats(transaction, player, quest.QuestId, "payout", 23, 17, "fixture", 100);
            });
        }

        lock (player.PersistenceGate)
        {
            RunAction(900001);
            Assert.ThrowsExactly<InvalidOperationException>(() => RunAction(900002));
        }

        Assert.AreEqual(117L, dao.Stats[(int)CharacterStat.Cash]);
        Assert.AreEqual(223L, dao.Stats[(int)CharacterStat.XP]);
        Assert.IsTrue(dao.Items.ContainsKey(900001));
        Assert.IsFalse(dao.Items.ContainsKey(900002));
        Assert.HasCount(1, dao.Rewards);
    }
}
