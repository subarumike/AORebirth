namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Doja;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Trade;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

[TestClass]
public sealed class AuthoredQuestTests
{
    [TestMethod]
    public void PackagedAcceptedAuthoredCatalogPreservesExactObjectives()
    {
        var catalog = LoadCatalog();
        Assert.IsTrue(catalog.Definitions.Single(value => value.QuestId == AuthoredQuestFixture.BuyLockpick).Objectives
            .Any(value => value.ObjectiveId == "mission_555BD124_buy_lockpick" && value.RequiredCount == 1 && value.IsResolved));
        Assert.IsTrue(catalog.Definitions.Single(value => value.QuestId == DojaChipInteractionRules.QuestTurnIn).Objectives
            .Any(value => value.ObjectiveId == "mission_55AA2421_turnin" && value.RequiredCount == 1 && value.IsResolved));
    }

    [TestMethod]
    public void LockpickGrantRetirementAndMissionHandoffShareOneCommit()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyLockpick); var sealedItem = w.Add(295999);
        bool checkedBefore = false, isolated = false, fullPlan = false;
        w.Dao.BeforeCommit = working => { checkedBefore = true; isolated = w.Player.Inventory.Inventory.Content[64] == sealedItem && w.Session.Messages.Count == 0;
            fullPlan = working.Items[10].ContainerType == 0 && working.Items.Values.Any(value => value.LowId == 95577)
                && working.GetMission(new(111, AuthoredQuestFixture.BuyLockpick)).State == DaoState.Completed
                && working.GetMission(new(111, AuthoredQuestFixture.Strongbox)).State == DaoState.Active; };
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, sealedItem));
        Assert.IsTrue(checkedBefore && isolated && fullPlan); Assert.AreEqual(1, w.Dao.Calls);
        Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
        Assert.AreEqual(95577, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
        Assert.IsTrue(w.Session.Messages[0] is TemplateActionMessage { Action: TemplateActionType.Overflow });
        Assert.IsTrue(w.Session.Messages[1] is ContainerAddItemMessage);
        Assert.IsTrue(w.Session.Messages[2] is TemplateActionMessage { Action: TemplateActionType.Use });
        Assert.IsTrue(w.Session.Messages[3] is CharacterActionMessage { Action: CharacterActionType.DeleteItem });
        Assert.AreEqual(2, w.Session.Messages.OfType<byte[]>().Count());
        Assert.IsTrue(w.Session.Messages.OfType<QuestFullUpdateMessage>().Single().Quests.Single().QuestId.Instance == unchecked((int)0x555BE9C5));
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, sealedItem));
    }

    [TestMethod]
    public void FailedLockpickCommitPreservesSourceMissionInventoryAndNoAcknowledgement()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyLockpick); var item = w.Add(295999);
        w.Dao.Failure = new InvalidOperationException("late mission write failure");
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item));
        Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(104, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(1, w.Dao.Items.Count); Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyLockpick)).State);
        Assert.IsNull(w.Dao.GetMission(new(111, AuthoredQuestFixture.Strongbox))); Assert.AreEqual(0, w.Session.Messages.Count);
        Assert.IsFalse(w.Player.IsPersistenceQuarantined);
    }

    [DataTestMethod]
    [DataRow("accepted")]
    [DataRow("overflow")]
    [DataRow("rollback")]
    [DataRow("unknown")]
    public void InventoryUsePacketRoutesAuthoredItemsWithoutFallingThroughOnFailure(string outcome)
    {
        using var w = new World();
        w.Activate(AuthoredQuestFixture.BuyLockpick);
        var item = w.Add(295999);
        // A full inventory is no refusal: the reward waits in overflow.
        if (outcome == "overflow") TestWorld.FillInventory(w.Player);
        item.Definition.Stats[CharacterStat.Can] = (int)(CanFlags.Use | CanFlags.Consume);
        item.SpellList[EventType.OnUse] = [new ItemSpell
        {
            FunctionType = (int)FunctionType.Set, Target = (int)ItemTarget.User,
            Arguments = [(int)CharacterStat.Cash, 777]
        }];
        if (outcome == "rollback") w.Dao.Failure = new InvalidOperationException("late mission write failure");
        w.Dao.UnknownCommit = outcome == "unknown";
        w.Dao.BeforeCommit = _ =>
        {
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(0, w.Session.Messages.Count);
        };
        var handler = new GenericCmdMessageHandler(new StubInventoryRepository(), new StubItemBuilder(), w.Service);
        handler.Handle(new GenericCmdMessage
        {
            Identity = w.Player.Identity, Action = GenericCmdAction.Use, User = w.Player.Identity, Target = [Slot]
        }, w.Session);

        Assert.AreNotEqual(777, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        if (outcome == "accepted")
        {
            Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
            Assert.AreEqual(95577, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
            Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyLockpick)).State);
            Assert.AreEqual(1, w.Session.Messages.OfType<GenericCmdMessage>().Single().Temp1);
        }
        else if (outcome == "overflow")
        {
            Assert.IsFalse(w.Player.Inventory.Inventory.Content.Values.Contains(item));
            Assert.AreEqual(95577, w.Player.Inventory.Overflow.Content.Values.Single().LowId);
            Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyLockpick)).State);
            Assert.AreEqual(1, w.Session.Messages.OfType<GenericCmdMessage>().Single().Temp1);
        }
        else
        {
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(1, item.StackCount);
            if (outcome == "unknown")
            {
                Assert.IsTrue(w.Player.IsPersistenceQuarantined);
                Assert.AreEqual(SessionState.Closed, w.Session.State);
                Assert.AreEqual(0, w.Session.Messages.Count);
            }
            else Assert.AreEqual(2, w.Session.Messages.OfType<GenericCmdMessage>().Single().Temp1);
        }
    }

    [TestMethod]
    public void UnknownAuthoredCommitQuarantinesWithoutMemoryReplayOrSuccessFrames()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyLockpick); var item = w.Add(295999); w.Dao.UnknownCommit = true;
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.IsTrue(w.Player.IsPersistenceQuarantined);
        Assert.AreEqual(SessionState.Closed, w.Session.State); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Items[10].ContainerType);
        int calls = w.Dao.Calls; Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(calls, w.Dao.Calls);
    }

    [TestMethod]
    public void MarcoContentsTipRewardAndLiveStatsCommitTogetherWithoutLosingUnsnapshottedValues()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyNano); var item = w.Add(248258);
        w.Player.Stats.Set(CharacterStat.Cash, 123); w.Player.Stats.Set(CharacterStat.XP, 300);
        w.Dao.Stats[(int)CharacterStat.Cash] = 1; w.Dao.Stats[(int)CharacterStat.XP] = 2;
        bool isolated = false;
        w.Dao.BeforeCommit = working => isolated = w.Player.Stats.GetOrZero(CharacterStat.Cash) == 123
            && w.Session.Messages.Count == 0 && working.Items.Values.Count(value => value.ContainerType == 104) == 6
            && working.Stats[(int)CharacterStat.Cash] == 1363 && working.Rewards.Count == 1;
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, item)); Assert.IsTrue(isolated);
        Assert.AreEqual(6, w.Player.Inventory.Inventory.Content.Count); Assert.AreEqual(1363, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(2869, w.Player.Stats.GetOrZero(CharacterStat.XP));
        CollectionAssert.AreEqual(new[] { 43384, 42423, 99589, 43960, 43978, 223373 },
            w.Session.Messages.OfType<TemplateActionMessage>().Where(value => value.Action == TemplateActionType.Overflow).Select(value => value.ItemLowId).ToArray());
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyNano)).State);
        Assert.AreEqual(1, w.Dao.Calls);
    }

    [TestMethod]
    public void LateMarcoFailureRollsBackEveryGrantSourceMissionAndRewardStat()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyNano); var item = w.Add(248258);
        w.Player.Stats.Set(CharacterStat.Cash, 123); w.Dao.Stats[(int)CharacterStat.Cash] = 123;
        w.Dao.Failure = new InvalidOperationException("late insert failure");
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(1, w.Dao.Items.Count);
        Assert.AreEqual(0, w.Dao.Rewards.Count); Assert.AreEqual(123L, w.Dao.Stats[(int)CharacterStat.Cash]);
        Assert.AreEqual(123, w.Player.Stats.GetOrZero(CharacterStat.Cash)); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyNano)).State); Assert.AreEqual(0, w.Session.Messages.Count);
    }

    [TestMethod]
    public void FullInventoryRewardIsSavedInOverflow()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.BuyLockpick); var item = w.Add(295999); TestWorld.FillInventory(w.Player);
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, item));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyLockpick)).State);
        Item reward = w.Player.Inventory.Overflow.Content.Values.Single();
        Assert.AreEqual(95577, reward.LowId);
        Assert.IsTrue(reward.IsPersisted);
        Assert.IsTrue(w.Dao.Items.Values.Any(row => row.LowId == 95577 && row.ContainerType == (int)IdentityType.OverflowWindow));
    }

    [TestMethod]
    public void StaleRowsCannotConsumeOrAdvanceQuest()
    {
        using var stale = new World(); stale.Activate(AuthoredQuestFixture.BuyLockpick); var moved = stale.Add(295999);
        stale.Dao.Items[10].ContainerPlacement = 65;
        Assert.IsFalse(stale.Service.TryUseItem(stale.Player, Slot, moved)); Assert.AreEqual(0, stale.Session.Messages.Count);
        Assert.IsNull(stale.Dao.GetMission(new(111, AuthoredQuestFixture.Strongbox)));
    }

    [TestMethod]
    public void DojaUseAcceptsWithoutConsumingAndReplayDoesNotDuplicateJournal()
    {
        using var w = new World(); var item = w.Add(284954); w.Player.Stats.Set(CharacterStat.Level, 10);
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(104, w.Dao.Items[10].ContainerType); Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.IsTrue(w.Session.Messages[0] is TemplateActionMessage { Action: TemplateActionType.Use }); Assert.IsTrue(w.Session.Messages[1] is byte[]);
        int sent = w.Session.Messages.Count; Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(sent, w.Session.Messages.Count);
        Assert.AreEqual(0, w.Session.Messages.OfType<CharacterActionMessage>().Count());
    }

    [TestMethod]
    public void DojaTurnInCommitsChipFullLevelTokensAndAccountCooldownBeforePublication()
    {
        using var w = new World(7010); var chip = w.Add(284954); w.Activate(DojaChipInteractionRules.QuestTurnIn);
        w.Player.Stats.Set(CharacterStat.Level, 2); w.Player.Stats.Set(CharacterStat.XP, 1500);
        w.Player.Stats.Set(CharacterStat.Side, 1); w.Player.Stats.Set((CharacterStat)62, 7);
        w.Player.Stats.Set(CharacterStat.UnsavedXP, 5000); w.Player.Stats.Set(CharacterStat.IP, 1500);
        int accepted = 0;
        w.Dao.BeforeCommit = pending =>
        {
            Assert.AreEqual(0, accepted); Assert.AreEqual(0, w.Session.Messages.Count);
            Assert.AreSame(chip, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(2, w.Player.Stats.GetOrOne(CharacterStat.Level));
            Assert.AreEqual(0, pending.Items[10].ContainerType); Assert.AreEqual(4100L, pending.Stats[(int)CharacterStat.XP]);
            Assert.AreEqual(9L, pending.Stats[62]); Assert.AreEqual(2, pending.Rewards.Count);
        };
        Assert.IsTrue(w.Service.TryTurnInDoja(w.Player, Slot, chip, () => { Assert.AreEqual(0, w.Session.Messages.Count); accepted++; }), w.Logger.LastError);
        Assert.AreEqual(1, accepted); Assert.AreEqual(3, w.Player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(4100, w.Player.Stats.GetOrZero(CharacterStat.XP)); Assert.AreEqual(5000, w.Player.Stats.GetOrZero(CharacterStat.UnsavedXP));
        Assert.AreEqual(9, w.Player.Stats.GetOrZero((CharacterStat)62)); Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestCooldown)).State);
        var flag = w.Dao.GetAccountFlag("account", DojaChipInteractionRules.CooldownFlag);
        Assert.AreEqual(w.Now.AddHours(18), DateTime.Parse(flag.Value, null, System.Globalization.DateTimeStyles.RoundtripKind));
        int sent = w.Session.Messages.Count; int calls = w.Dao.Calls;
        Assert.IsFalse(w.Service.TryTurnInDoja(w.Player, Slot, chip, () => accepted++));
        Assert.AreEqual(1, accepted); Assert.AreEqual(sent, w.Session.Messages.Count); Assert.AreEqual(calls + 1, w.Dao.Calls);
    }

    [TestMethod]
    [DataRow("accepted")]
    [DataRow("declined")]
    [DataRow("wrong-item")]
    [DataRow("credits")]
    [DataRow("stale-row")]
    [DataRow("rollback")]
    [DataRow("unknown-commit")]
    public void DojaScarlettHandlersCommitOrReturnTheExactOfferedChip(string result)
    {
        using var w = new World(DojaChipInteractionRules.ScarlettPlayfieldId, allowOfferParking: true);
        Item chip = w.Add(DojaChipInteractionRules.NascenseChipItemId);
        w.Activate(DojaChipInteractionRules.QuestTurnIn);
        w.Player.Stats.Set(CharacterStat.Level, 2);
        w.Player.Stats.Set(CharacterStat.XP, 1500);
        w.Player.Stats.Set(CharacterStat.Side, 1);
        NpcCharacter npc = w.AddNpc(DojaChipInteractionRules.ScarlettIdentityText, instance: DojaChipInteractionRules.ScarlettInstance);
        NpcCharacter otherActor = w.AddNpc("fixture:other-actor");
        var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: Path.Combine(AppContext.BaseDirectory, "DojaGameData"));
        var catalog = new TemplateCatalog();
        var items = new StubItemBuilder();
        var minter = new HashItemMinter(data, catalog, items);
        var ids = new Ids();
        var persistence = new NoIndependentTrade();
        var trades = new TradeService(w.Logger, data, catalog, minter, ids, w.Flush, persistence);
        var actions = new InventoryActionService(persistence, w.Flush, ids, w.Logger);
        var quests = new QuestCatalog(data, w.Logger);
        // This dialogue uses the authored ledger. Accidental use of the ordinary quest service fails the fixture.
        var questService = (QuestService)RuntimeHelpers.GetUninitializedObject(typeof(QuestService));
        long time = 100;
        var knubot = new KnubotService(new KnubotCatalog(data, quests, catalog, w.Logger),
            new KnubotEffectServices(questService, minter, w.Flush, trades, actions, w.Service), w.Logger, () => time);
        void TickDialogue()
        {
            for (int i = 0; i < 5; i++)
            {
                time += 20;
                knubot.Tick(w.Player.Playfield!);
            }
        }
        new KnuBotStartTradeMessageHandler(knubot).Handle(new KnuBotStartTradeMessage { Target = npc.Identity }, w.Session);
        Assert.IsFalse(w.Session.Messages.OfType<KnuBotStartTradeMessage>().Any());
        var open = new KnuBotOpenChatWindowMessageHandler(knubot);
        open.Handle(new KnuBotOpenChatWindowMessage { Target = otherActor.Identity }, w.Session);
        Assert.AreEqual(0, w.Session.Messages.Count);
        open.Handle(new KnuBotOpenChatWindowMessage { Target = npc.Identity }, w.Session);
        TickDialogue();
        var reply = w.Session.Messages.OfType<KnuBotAnswerListMessage>().Single();
        Assert.AreEqual("I have found a DOJA Chip in Nascense.", reply.DialogOptions[0].Text);
        w.Session.Messages.Clear();
        new KnuBotAnswerMessageHandler(knubot).Handle(new KnuBotAnswerMessage { Target = npc.Identity, Answer = 0 }, w.Session);
        TickDialogue();
        var start = w.Session.Messages.OfType<KnuBotStartTradeMessage>().Single();
        Assert.AreEqual(1, start.NumberOfItemSlotsInTradeWindow);
        Assert.AreEqual("Drag and drop the item(s) you want to give to Scarlett Dalquist into one of the slots available and press \"accept\"",
            start.Message);
        Assert.IsFalse(w.Session.Messages.OfType<KnuBotAnswerListMessage>().Any());

        Item offered = chip;
        Identity offeredSlot = Slot;
        if (result == "wrong-item")
        {
            offered = w.Add(95577, placement: 65, instanceId: 11);
            offeredSlot = new Identity { Type = IdentityType.Inventory, Instance = 65 };
        }
        var offer = new KnuBotTradeMessageHandler(knubot);
        var add = new KnuBotTradeMessage { Target = npc.Identity, Action = KnuBotTradeAction.Add, Container = offeredSlot };
        Identity otherSlot = new() { Type = IdentityType.Inventory, Instance = 65 };
        if (result == "accepted")
        {
            // An item with no durable row cannot occupy the offer.
            var other = TestWorld.CreateItem(instanceId: 0, persisted: false);
            Assert.IsTrue(w.Player.Inventory.Inventory.Add(otherSlot.Instance, other));
            offer.Handle(new KnuBotTradeMessage { Target = npc.Identity, Action = KnuBotTradeAction.Add, Container = otherSlot }, w.Session);
            Assert.IsFalse(other.Locked);
            Assert.IsTrue(trades.TryGetSession(w.Player, out TradeSession openTrade));
            Assert.AreEqual(0, openTrade.InitiatorOffer.Count);
            Assert.AreSame(other, w.Player.Inventory.Inventory.Remove(otherSlot.Instance));
        }
        offer.Handle(add, w.Session);
        Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(offeredSlot.Instance));
        Assert.IsTrue(offered.Locked);
        w.Flush.HardFlush(w.Player);
        Assert.AreEqual((int)IdentityType.OverflowWindow, w.Dao.Items[offered.InstanceId].ContainerType);
        Assert.AreEqual(offered.InstanceId, w.Dao.Items[offered.InstanceId].ContainerPlacement);
        Item? interleavedGrant = null;
        if (result is "accepted" or "rollback")
        {
            interleavedGrant = TestWorld.CreateItem(lowId: 95577, highId: 95577, instanceId: 11, persisted: false);
            lock (w.Player.PersistenceGate)
            {
                Assert.IsTrue(InventoryGrantPlan.TryCreate(w.Player, [interleavedGrant], out var grant));
                var row = grant.Rows.Single();
                Assert.AreEqual(Slot.Instance, row.ContainerPlacement, "The normal grant reuses the slot freed by the offer.");
                w.Dao.Execute(111, tx =>
                {
                    ((IMissionInventoryMutationTransaction)tx).ApplyInventoryMutation([new()
                    {
                        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
                        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
                        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
                    }], []);
                    return true;
                });
                Assert.IsTrue(grant.PublishAfterCommit());
            }
            Assert.AreEqual(interleavedGrant.LowId, w.Session.Messages.OfType<AddTemplateMessage>().Single().LowId);
        }

        w.Session.Messages.Clear();
        w.Dao.BeforeCommit = pending =>
        {
            Assert.AreEqual(0, w.Session.Messages.Count);
            Assert.AreEqual(0, pending.Items[chip.InstanceId].ContainerType);
        };
        if (result == "rollback")
            w.Dao.Failure = new InvalidOperationException("Injected authored trade failure.");
        if (result == "stale-row")
            w.Dao.Items[chip.InstanceId].ContainerPlacement++;
        w.Dao.UnknownCommit = result == "unknown-commit";
        var finish = new KnuBotFinishTradeMessageHandler(knubot);
        var message = new KnuBotFinishTradeMessage
            { Target = npc.Identity, Decline = result == "declined" ? 1 : 0, Amount = result == "credits" ? 1 : 0 };
        finish.Handle(new KnuBotFinishTradeMessage { Target = otherActor.Identity }, w.Session);
        Assert.AreEqual(0, w.Session.Messages.Count);
        Assert.IsTrue(offered.Locked);
        finish.Handle(message, w.Session);
        Assert.IsFalse(trades.TryGetSession(w.Player, out _));
        Assert.IsFalse(offered.Locked);
        int messages = w.Session.Messages.Count;
        int calls = w.Dao.Calls;
        finish.Handle(message, w.Session);
        Assert.AreEqual(messages, w.Session.Messages.Count);
        Assert.AreEqual(calls, w.Dao.Calls);

        if (result == "accepted")
        {
            Assert.AreEqual(0, ((KnuBotRejectedItemsMessage)w.Session.Messages[0]).Items.Length);
            Assert.IsFalse(w.Session.Messages.OfType<CharacterActionMessage>().Any(packet => packet.Action == CharacterActionType.DeleteItem));
            Assert.AreSame(interleavedGrant, w.Player.Inventory.Inventory.Content[Slot.Instance]);
            Assert.AreEqual(104, w.Dao.Items[interleavedGrant!.InstanceId].ContainerType);
            Assert.AreEqual(Slot.Instance, w.Dao.Items[interleavedGrant.InstanceId].ContainerPlacement);
            Assert.AreEqual(0, w.Dao.Items[chip.InstanceId].ContainerType);
            Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
            Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestCooldown)).State);
            Assert.AreEqual(2, w.Dao.Rewards.Count);
            TickDialogue();
            Assert.AreEqual("Goodbye", w.Session.Messages.OfType<KnuBotAnswerListMessage>().Single().DialogOptions[0].Text);
        }
        else if (result == "unknown-commit")
        {
            Assert.IsTrue(w.Player.IsPersistenceQuarantined);
            Assert.AreEqual(SessionState.Closed, w.Session.State);
            Assert.AreEqual(0, messages);
            Assert.AreEqual(0, w.Dao.Items[chip.InstanceId].ContainerType);
        }
        else
        {
            var returned = w.Session.Messages.OfType<KnuBotRejectedItemsMessage>().Single().Items.Single();
            Assert.AreEqual(offered.LowId, returned.LowId);
            int returnedSlot = interleavedGrant == null ? offeredSlot.Instance : otherSlot.Instance;
            Assert.AreSame(interleavedGrant ?? chip, w.Player.Inventory.Inventory.Content[Slot.Instance]);
            Assert.AreSame(offered, w.Player.Inventory.Inventory.Content[returnedSlot]);
            w.Flush.HardFlush(w.Player);
            Assert.AreEqual(104, w.Dao.Items[offered.InstanceId].ContainerType);
            Assert.AreEqual(returnedSlot, w.Dao.Items[offered.InstanceId].ContainerPlacement);
            Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
            Assert.AreEqual(0, w.Dao.Rewards.Count);
        }
    }

    [TestMethod]
    public void DojaLateFailureRollsBackChipProgressionBothLedgersAndAccountFlags()
    {
        using var w = new World(7010); var chip = w.Add(284954); w.Activate(DojaChipInteractionRules.QuestTurnIn);
        w.Player.Stats.Set(CharacterStat.Level, 2); w.Player.Stats.Set(CharacterStat.XP, 1500); w.Player.Stats.Set(CharacterStat.Side, 1);
        w.Dao.Failure = new InvalidOperationException("late commit failure"); int accepted = 0;
        Assert.IsFalse(w.Service.TryTurnInDoja(w.Player, Slot, chip, () => accepted++));
        Assert.AreEqual(0, accepted); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Rewards.Count);
        Assert.AreEqual(0, w.Dao.AccountFlags.Count); Assert.AreEqual(104, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.IsNull(w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestCooldown)));
        Assert.AreSame(chip, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(2, w.Player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(1500, w.Player.Stats.GetOrZero(CharacterStat.XP)); Assert.IsFalse(w.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void DojaUnknownCommitQuarantinesWithoutPublishingOrRetryingRewards()
    {
        using var w = new World(7010); var chip = w.Add(284954); w.Activate(DojaChipInteractionRules.QuestTurnIn);
        w.Player.Stats.Set(CharacterStat.Level, 1); w.Player.Stats.Set(CharacterStat.XP, 0);
        w.Dao.UnknownCommit = true; int accepted = 0;
        Assert.IsFalse(w.Service.TryTurnInDoja(w.Player, Slot, chip, () => accepted++));
        Assert.IsTrue(w.Player.IsPersistenceQuarantined, w.Logger.LastError); Assert.AreEqual(SessionState.Closed, w.Session.State);
        Assert.AreEqual(0, accepted); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.AreEqual(1, w.Player.Stats.GetOrOne(CharacterStat.Level)); Assert.AreSame(chip, w.Player.Inventory.Inventory.Content[64]);
        int calls = w.Dao.Calls; Assert.IsFalse(w.Service.TryTurnInDoja(w.Player, Slot, chip, () => accepted++)); Assert.AreEqual(calls, w.Dao.Calls);
    }

    [TestMethod]
    public void DojaAccountCooldownBlocksUseAndRestoreRetainsExactRemainingExpiry()
    {
        using var w = new World(7010); var chip = w.Add(284954); w.Player.Stats.Set(CharacterStat.Level, 2);
        w.Dao.AccountFlags["account|" + DojaChipInteractionRules.CooldownFlag] = new() { AccountKey = "account",
            FlagKey = DojaChipInteractionRules.CooldownFlag, Value = w.Now.AddHours(1).ToString("o"), Version = 1 };
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, chip)); Assert.IsNull(w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)));
        Assert.AreSame(chip, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(1, w.Session.Messages.OfType<ChatTextMessage>().Count());
        w.Activate(DojaChipInteractionRules.QuestCooldown);
        var key = new MissionKeyData(111, DojaChipInteractionRules.QuestCooldown);
        var flag = new MissionFlagData { CharacterId = 111, QuestId = key.QuestId, FlagKey = DojaChipInteractionRules.CooldownFlag,
            Value = w.Now.AddHours(1).ToString("o"), Version = 1 };
        w.Dao.Flags[key + "|" + flag.FlagKey] = flag;
        w.Session.Messages.Clear(); w.Service.Restore(w.Player);
        Assert.AreEqual(1, w.Session.Messages.Count);
        CollectionAssert.AreEqual(DojaChipPacketSender.CreateJournalPacket(111, key.QuestId, 3600, 0), (byte[])w.Session.Messages[0]);
        Assert.AreEqual(flag.Value, w.Dao.GetFlag(key, flag.FlagKey).Value);
    }

    [TestMethod]
    public void CommonMissionServiceDoesNotReactivateCompletedDojaAfterCooldown()
    {
        using var w = new World(7010); w.Activate(DojaChipInteractionRules.QuestTurnIn);
        var key = new MissionKeyData(111, DojaChipInteractionRules.QuestTurnIn);
        var completed = w.Dao.Missions[key]; completed.State = DaoState.Completed;
        completed.CompletedAtUtcTicks = w.Now.AddDays(-2).Ticks;
        long version = completed.Version, acceptedAt = completed.AcceptedAtUtcTicks, updatedAt = completed.UpdatedAtUtcTicks;
        var definition = w.Catalog.Definitions.Single(value => value.QuestId == key.QuestId);
        Assert.IsFalse(w.Dao.Execute(111, tx => AuthoredMissionProgression.Offer(tx, definition, w.Now.Ticks)));
        Assert.IsFalse(w.Dao.Execute(111, tx => AuthoredMissionProgression.Accept(tx, definition, w.Now.Ticks)));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(key).State);
        var after = w.Dao.GetMission(key);
        Assert.AreEqual(version, after.Version); Assert.AreEqual(acceptedAt, after.AcceptedAtUtcTicks);
        Assert.AreEqual(updatedAt, after.UpdatedAtUtcTicks); Assert.AreEqual(completed.CompletedAtUtcTicks, after.CompletedAtUtcTicks);
        Assert.AreEqual(0, w.Dao.Rewards.Count); Assert.AreEqual(0, w.Session.Messages.Count);
    }

    [TestMethod]
    public void CompletedDojaCycleCannotPublishFalseFreshAcceptanceOrConsumeAnotherChip()
    {
        using var w = new World(7010); var chip = w.Add(284954); w.Player.Stats.Set(CharacterStat.Level, 2);
        w.Activate(DojaChipInteractionRules.QuestTurnIn);
        var key = new MissionKeyData(111, DojaChipInteractionRules.QuestTurnIn);
        w.Dao.Missions[key].State = DaoState.Completed;
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, chip));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(key).State);
        Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Rewards.Count);
        Assert.AreEqual(104, w.Dao.Items[10].ContainerType); Assert.AreSame(chip, w.Player.Inventory.Inventory.Content[64]);
        Assert.IsFalse(w.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void StrongboxAndThiefFailureNeverAcknowledgeOrPublishPartialGrant()
    {
        using (var w = new World())
        {
            var lockpick = w.Add(95577); w.Activate(AuthoredQuestFixture.Strongbox); int ack = 0;
            w.Dao.Failure = new InvalidOperationException("late Strongbox handoff failure");
            Assert.IsFalse(w.Service.TryUseLockpickOnStrongbox(w.Player, Slot, lockpick, () => ack++));
            Assert.AreEqual(0, ack); Assert.AreEqual(0, w.Session.Messages.Count);
            Assert.AreSame(lockpick, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(1, w.Dao.Items.Count);
            Assert.IsNull(w.Dao.GetMission(new(111, AuthoredQuestFixture.DeliverFactory)));
            w.Dao.Failure = null;
            w.Dao.BeforeCommit = pending => Assert.AreEqual(0, ack);
            Assert.IsTrue(w.Service.TryUseLockpickOnStrongbox(w.Player, Slot, lockpick, () => ack++), w.Logger.LastError);
            Assert.AreEqual(1, ack); Assert.AreEqual(2, w.Dao.Items.Count);
        }
        using (var w = new World())
        {
            w.Activate(AuthoredQuestFixture.FindThief); int ack = 0;
            w.Dao.Failure = new InvalidOperationException("late thief handoff failure");
            Assert.IsFalse(w.Service.TryUseShopThiefRemains(w.Player, () => ack++));
            Assert.AreEqual(0, ack); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Items.Count);
            Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.FindThief)).State);
            Assert.IsNull(w.Dao.GetMission(new(111, AuthoredQuestFixture.DeliverArmor)));
            Assert.IsFalse(w.Player.IsPersistenceQuarantined);
        }
    }

    [TestMethod]
    public void CompletedFactoryAndArmorCannotRegenerateUnfinishableQuestItems()
    {
        using (var w = new World())
        {
            var lockpick = w.Add(95577); w.Activate(AuthoredQuestFixture.DeliverFactory);
            w.Dao.Missions[new(111, AuthoredQuestFixture.DeliverFactory)].State = DaoState.Completed;
            int ack = 0;
            Assert.IsFalse(w.Service.TryUseLockpickOnStrongbox(w.Player, Slot, lockpick, () => ack++));
            Assert.AreEqual(0, ack); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(1, w.Dao.Items.Count);
            Assert.AreSame(lockpick, w.Player.Inventory.Inventory.Content[64]);
        }
        using (var w = new World())
        {
            w.Activate(AuthoredQuestFixture.DeliverArmor);
            w.Dao.Missions[new(111, AuthoredQuestFixture.DeliverArmor)].State = DaoState.Completed;
            int ack = 0;
            Assert.IsFalse(w.Service.TryUseShopThiefRemains(w.Player, () => ack++));
            Assert.AreEqual(0, ack); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Items.Count);
            Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.DeliverArmor)).State);
        }
    }

    [TestMethod]
    public void SarahRestoreUsesExistingExactJournalBuildersForOnlyActiveMissions()
    {
        using var w = new World(); w.Activate(AuthoredQuestFixture.DeliverArmor);
        w.Service.Restore(w.Player);
        var tip = w.Session.Messages.OfType<QuestFullUpdateMessage>().Single().Quests.Single();
        Assert.AreEqual(unchecked((int)0x555BE9F6), tip.QuestId.Instance); Assert.AreEqual(158429, tip.MissionIconId);
        Assert.AreEqual(unchecked((int)0x78E0FC69), tip.UnknownId1.Instance);
        w.Session.Messages.Clear(); w.Dao.Missions[new(111, AuthoredQuestFixture.DeliverArmor)].State = DaoState.Completed;
        w.Service.Restore(w.Player); Assert.AreEqual(0, w.Session.Messages.Count);
    }

    [TestMethod]
    public void StrongboxPreservesLockpickAndFactoryTurnInPublishesOnlyAfterCommit()
    {
        using var w = new World(); var lockpick = w.Add(95577); w.Activate(AuthoredQuestFixture.Strongbox);
        Assert.IsTrue(w.Service.TryUseLockpickOnStrongbox(w.Player, Slot, lockpick));
        Assert.AreSame(lockpick, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(104, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.Strongbox)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.DeliverFactory)).State);
        var factory = w.Player.Inventory.Inventory.Content.Single(pair => pair.Value.LowId == 248306);
        w.Session.Messages.Clear(); w.Player.Stats.Set(CharacterStat.Cash, 20); int accepted = 0;
        w.Dao.BeforeCommit = pending => { Assert.AreEqual(0, accepted); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(20, w.Player.Stats.GetOrZero(CharacterStat.Cash)); };
        Assert.IsTrue(w.Service.TryTurnInFactory(w.Player, new() { Type = IdentityType.Inventory, Instance = factory.Key }, factory.Value, () => accepted++));
        Assert.AreEqual(1, accepted); Assert.AreEqual(1260, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestFixture.DeliverFactory)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.TalkSarah)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestFixture.BuyNano)).State);
        Assert.IsTrue(w.Player.Inventory.Inventory.Content.Values.Any(item => item.LowId == 296572));
    }

    static Identity Slot => new() { Type = IdentityType.Inventory, Instance = 64 };
    static AuthoredQuestCatalog LoadCatalog() => AuthoredQuestCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Content"));

    internal sealed class World : IDisposable
    {
        internal readonly Player Player = TestWorld.CreatePlayer(111);
        internal readonly Session Session = new();
        internal readonly AuthoredMissionTestDao Dao = new();
        internal readonly AuthoredQuestCatalog Catalog;
        internal readonly AuthoredQuestService Service;
        internal readonly ErrorLogger Logger = new();
        internal readonly DynelRegistry Registry = new();
        internal readonly NpcContentActivationService Npcs;
        internal DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        readonly InventoryFlushService _flush;
        internal InventoryFlushService Flush => _flush;
        readonly ServiceProvider _services;
        internal World(int playfield = 6553, InteractionContent? content = null, bool allowOfferParking = false)
        {
            Catalog = content == null ? LoadCatalog() : new(content);
            Player.Session = Session; Session.BindPlayer(Player);
            Player.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, new Identity { Type = IdentityType.Playfield, Instance = playfield });
            typeof(Playfield).GetField("<ResourceId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, playfield);
            var locality = new PlayfieldLocality(playfield, null);
            Npcs = new(Player.Playfield, Registry, locality, new StubItemBuilder(), new TemplateCatalog());
            _services = new ServiceCollection().AddSingleton(locality).AddSingleton(Registry).AddSingleton(Npcs).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, _services);
            Registry.Register(Player);
            var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
            typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Dictionary<int, Player>());
            _flush = new(new Lazy<PlayfieldManager>(() => manager),
                allowOfferParking ? new OfferParkingFlush(Dao) : new NoIndependentFlush(), new StubLogger());
            Service = new(Dao, _flush, new StubItemBuilder(), new TemplateCatalog(), new Ids(), Catalog, Logger, () => Now);
        }
        internal void Activate(string quest)
        {
            var definition = Catalog.Definitions.Single(value => value.QuestId == quest);
            Assert.IsTrue(Dao.Execute(111, tx => AuthoredMissionProgression.Offer(tx, definition, Now.Ticks)));
            Assert.IsTrue(Dao.Execute(111, tx => AuthoredMissionProgression.Accept(tx, definition, Now.Ticks))); Dao.Calls = 0;
        }
        internal Item Add(int template, int placement = 64, int instanceId = 10)
        {
            var item = TestWorld.CreateItem(lowId: template, highId: template, instanceId: instanceId);
            Player.Inventory.Inventory.Add(placement, item);
            Dao.Items.Add(instanceId, new() { InstanceId = instanceId, ContainerType = 104, ContainerInstance = 111, ContainerPlacement = placement,
                ItemType = item.Identity.Type != IdentityType.None ? (int)item.Identity.Type : item.Definition.ItemType,
                LowId = template, HighId = template, Quality = 1, StackCount = 1, Source = (byte)item.Source });
            return item;
        }
        internal NpcCharacter AddNpc(string contentIdentity, bool accepted = true, bool vendor = false, int instance = 222)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new StubItemBuilder())
            { Playfield = Player.Playfield, Position = Player.Position, Name = "Fixture accepted actor" };
            Registry.Register(npc);
            if (accepted) Npcs.Bind(npc, new("fixture:" + instance, "accepted-test-contract", contentIdentity,
                Player.Playfield!.Identity.Instance, vendor));
            return npc;
        }
        public void Dispose() { _flush.Dispose(); _services.Dispose(); }
    }
    sealed class Ids : IItemInstanceIdAllocator { int _next = 1000; public int Allocate() => _next++; }
    internal sealed class ErrorLogger : ZoneEngine_New.Core.Logging.IZoneLogger
    {
        internal string LastError = string.Empty;
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => LastError = message;
        public void Error(Exception exception, string message) => LastError = message + " " + exception;
        public ZoneEngine_New.Core.Logging.IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }
    sealed class TemplateCatalog : IItemTemplateCatalog
    {
        public bool TryGet(int id, out ItemTemplate template) { template = Require(id); return true; }
        public ItemTemplate Require(int id) => new() { Id = id, Quality = 1 };
    }
    sealed class NoIndependentFlush : ICharacterCoalesceCommit
    { public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int owner, IReadOnlyList<int> nanos, IReadOnlyList<ActiveNanoRecord>? activeNanos, IReadOnlyList<SkillLockRecord>? skillLocks) => throw new InvalidOperationException("No independent item transaction expected."); }
    sealed class OfferParkingFlush(AuthoredMissionTestDao dao) : ICharacterCoalesceCommit
    {
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int owner,
            IReadOnlyList<int> nanos, IReadOnlyList<ActiveNanoRecord>? activeNanos, IReadOnlyList<SkillLockRecord>? skillLocks)
        {
            Assert.AreEqual(0, inserts.Count);
            foreach (ItemLocationUpdate location in updates)
            {
                Assert.AreEqual(owner, location.ContainerInstance);
                var row = dao.Items[location.InstanceId];
                row.ContainerType = location.ContainerType;
                row.ContainerInstance = location.ContainerInstance;
                row.ContainerPlacement = location.ContainerPlacement;
                row.StackCount = location.StackCount ?? row.StackCount;
            }
        }
    }
    sealed class NoIndependentTrade : ITradePersistence, IInventoryMutationPersistence
    {
        public void Persist(TradePersistenceBatch batch) => throw new InvalidOperationException("The authored mission owns this trade.");
        public void Persist(InventoryMutationBatch batch) => throw new InvalidOperationException("No independent inventory mutation expected.");
    }
    internal sealed class Session : IZoneSession
    {
        internal readonly List<object> Messages = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public bool TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) => Messages.Add(packet);
        public void Send(Message message) => Messages.Add(message.Body);
        public void Send(MessageBody body) => Messages.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
