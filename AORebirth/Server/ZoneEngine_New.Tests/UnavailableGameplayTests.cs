namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class UnavailableGameplayTests
{
    [TestMethod]
    public void EveryRemovedNetworkFeatureRejectsBeforeItemsCreditsRewardsOrPersistenceChange()
    {
        using var world = new InventoryActionTests.World();
        var item = world.Add(42, 7);
        world.Player.Stats.Set(CharacterStat.Cash, 1234);
        world.Session.Messages.Clear();
        var stats = world.Player.Stats.GetEntries().Select(e => (e.Stat, e.Base, e.Bonus)).ToArray();
        Action[] requests = [
            () => new AttackMessageHandler().Handle(new AttackMessage(), world.Session),
            () => new QuestAlternativeMessageHandler().Handle(new QuestAlternativeMessage(), world.Session),
            () => new CreateQuestMessageHandler().Handle(new CreateQuestMessage(), world.Session),
            () => new QuestMessageHandler().Handle(new QuestMessage(), world.Session),
            () => new KnuBotOpenChatWindowMessageHandler().Handle(new KnuBotOpenChatWindowMessage(), world.Session),
            () => new KnuBotAnswerMessageHandler().Handle(new KnuBotAnswerMessage(), world.Session),
            () => new KnuBotCloseChatWindowMessageHandler().Handle(new KnuBotCloseChatWindowMessage(), world.Session),
            () => new KnuBotTradeMessageHandler().Handle(new KnuBotTradeMessage(), world.Session),
            () => new KnuBotFinishTradeMessageHandler().Handle(new KnuBotFinishTradeMessage(), world.Session)
        ];
        foreach (var request in requests)
        {
            request(); request(); // Repeat cannot double-charge or double-grant.
            Assert.AreEqual(0, world.Persistence.Calls);
            Assert.AreEqual(7, item.StackCount);
            Assert.AreSame(item, world.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(1, world.Player.Inventory.Inventory.Content.Count);
            CollectionAssert.AreEqual(stats, world.Player.Stats.GetEntries().Select(e => (e.Stat, e.Base, e.Bonus)).ToArray());
        }
        Assert.AreEqual(18, world.Session.Messages.Count);
        Assert.IsTrue(world.Session.Messages.All(m => m is ChatTextMessage));
    }

    [TestMethod]
    public void RejectionDoesNotSendPacketsToPreAdmissionOrStaleSession()
    {
        using var world = new InventoryActionTests.World();
        world.Session.Messages.Clear();
        world.Session.State = SessionState.Connected;
        new CreateQuestMessageHandler().Handle(new CreateQuestMessage(), world.Session);
        Assert.AreEqual(0, world.Session.Messages.Count);
        world.Session.State = SessionState.InPlay;
        world.Player.Session = null;
        new KnuBotFinishTradeMessageHandler().Handle(new KnuBotFinishTradeMessage(), world.Session);
        Assert.AreEqual(0, world.Session.Messages.Count);
        Assert.AreEqual(0, world.Persistence.Calls);
    }
}
