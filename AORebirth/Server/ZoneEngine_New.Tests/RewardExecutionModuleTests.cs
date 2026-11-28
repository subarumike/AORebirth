namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.Text.Json;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests;

[TestClass]
public sealed partial class RewardExecutionModuleTests
{
    [TestMethod]
    public void GeneratedRewardPlanUsesEarnedDefinitionAndKeepsInventoryAndStatsUnpublished()
    {
        var player = TestWorld.CreatePlayer(70);
        player.Stats.Set(CharacterStat.Cash, 100);
        var stale = new QuestTemplate { Reward = new QuestReward { Credits = 900 } };
        var stored = new QuestTemplate
        {
            Reward = new QuestReward
            {
                Credits = 17,
                Records = [[new QuestRewardItem { Id = 123, HighId = 124, Quality = 20, Count = 2 }]]
            }
        };
        var quest = AssignedQuest(stale, QuestSource.Generated);
        var earned = new CharacterQuestCompletionSnapshotData
        {
            QuestId = quest.QuestId, AssignedAtUtcTicks = quest.AssignedAtUtc.Ticks,
            DefinitionJson = JsonSerializer.Serialize(stored, QuestTemplate.JsonOptions)
        };
        var module = new RewardExecutionModule(null, new StubItemBuilder(), new StubLogger());

        QuestRewardExecutionPlan? plan;
        lock (player.PersistenceGate)
            plan = module.Plan(player, quest, earned, null, earned.AssignedAtUtcTicks + 1);

        Assert.IsNotNull(plan);
        Assert.AreEqual(117, plan.Stats[CharacterStat.Cash]);
        Assert.AreEqual(100, player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsEmpty(player.Inventory.Inventory.Content);
        Assert.AreEqual(earned.AssignedAtUtcTicks, plan.Request.AssignedAtUtcTicks);
        Assert.HasCount(1, plan.Request.Grants);
        var item = plan.Request.Grants[0];
        Assert.AreEqual(123, item.LowId);
        Assert.AreEqual(124, item.HighId);
        Assert.AreEqual(20, item.Quality);
        Assert.AreEqual(2, item.StackCount);
        Assert.AreEqual(player.Identity.Instance, item.ContainerInstance);
    }

    [TestMethod]
    public void InvalidSelectedDefinitionCannotBecomeAnEmptySuccessfulReward()
    {
        var player = TestWorld.CreatePlayer(71);
        var template = new QuestTemplate
        {
            Reward = new QuestReward { Records = [[new QuestRewardItem { Id = 0 }]] }
        };
        var quest = AssignedQuest(template, QuestSource.Generated);
        var earned = new CharacterQuestCompletionSnapshotData
        {
            QuestId = quest.QuestId, AssignedAtUtcTicks = quest.AssignedAtUtc.Ticks,
            DefinitionJson = JsonSerializer.Serialize(template, QuestTemplate.JsonOptions)
        };
        var module = new RewardExecutionModule(null, new StubItemBuilder(), new StubLogger());

        lock (player.PersistenceGate)
            Assert.ThrowsExactly<InvalidOperationException>(
                () => module.Plan(player, quest, earned, null, earned.AssignedAtUtcTicks + 1));
        Assert.IsEmpty(player.Inventory.Inventory.Content);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VirtualRewardOnlyJoinsTransactionWhenItsWholeSequenceIsSupported(bool mixed)
    {
        var player = TestWorld.CreatePlayer(72);
        player.Stats.Set(CharacterStat.AlignmentClanTokens, 10);
        var tokens = new ItemTemplate { Id = 50, Flags = (int)ItemFlags.Null };
        tokens.SpellList[EventType.OnUse] = [
            new ItemSpell
            {
                FunctionType = (int)FunctionType.Hit, Target = (int)ItemTarget.User,
                Arguments = [(int)CharacterStat.AlignmentClanTokens, 3, 3]
            }
        ];
        var unsupported = new ItemTemplate { Id = 51, Flags = (int)ItemFlags.Null };
        unsupported.SpellList[EventType.OnUse] = [
            new ItemSpell { FunctionType = (int)FunctionType.SpawnItem, Target = (int)ItemTarget.User }
        ];
        var template = new QuestTemplate
        {
            Reward = new QuestReward
            {
                Records = mixed
                    ? [[new QuestRewardItem { Id = 50 }, new QuestRewardItem { Id = 51 }]]
                    : [[new QuestRewardItem { Id = 50 }]]
            }
        };
        var quest = AssignedQuest(template, QuestSource.Generated);
        var earned = new CharacterQuestCompletionSnapshotData
        {
            QuestId = quest.QuestId, AssignedAtUtcTicks = quest.AssignedAtUtc.Ticks,
            DefinitionJson = JsonSerializer.Serialize(template, QuestTemplate.JsonOptions)
        };
        var module = new RewardExecutionModule(null, new StubItemBuilder().Add(tokens).Add(unsupported), new StubLogger());
        lock (player.PersistenceGate)
        {
            var plan = module.Plan(player, quest, earned, null, earned.AssignedAtUtcTicks + 1);
            Assert.IsNotNull(plan);
            var mutation = plan.Request.StatMutations.SingleOrDefault(stat => stat.StatId == (int)CharacterStat.AlignmentClanTokens);
            Assert.IsEmpty(plan.Request.Grants);
            Assert.AreEqual(10, player.Stats.GetOrZero(CharacterStat.AlignmentClanTokens));
            if (mixed)
            {
                Assert.IsNull(mutation);
                Assert.HasCount(2, plan.ImmediateVirtualItems);
                Assert.IsEmpty(plan.VirtualPublications);
            }
            else
            {
                Assert.IsNotNull(mutation);
                Assert.AreEqual(13L, mutation.Value);
                Assert.IsEmpty(plan.ImmediateVirtualItems);
                var publish = plan.VirtualPublications.Single();
                publish();
                publish();
                Assert.AreEqual(13, player.Stats.GetOrZero(CharacterStat.AlignmentClanTokens));
            }
        }
    }

    static PlayerQuest AssignedQuest(QuestTemplate template, QuestSource source) => new()
    {
        QuestId = "reward-fixture",
        Source = source,
        Template = template,
        AssignedAtUtc = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)
    };
}
