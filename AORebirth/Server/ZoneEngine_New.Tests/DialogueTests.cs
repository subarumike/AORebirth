namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Doja;
using ZoneEngine_New.Core.Dialogue;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Teams;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

[TestClass]
public sealed class DialogueTests
{
    [TestMethod]
    public void PackagedDialogueUsesAllExistingManifestDataWithoutMakingDataAnActivationCapability()
    {
        var catalog = DialogueCatalog.Load(AppContext.BaseDirectory);
        Assert.AreEqual(44, catalog.Registry.NpcCount);
        Assert.IsTrue(catalog.TryGet(DialogueActionRouter.Scarlett, out var scarlett));
        Assert.AreEqual("scarlett_001", scarlett.RootNodeId);
        Assert.IsTrue(catalog.TryGet("SimpleChar:78CCD541", out _)); // Prince Creehan data is not registered activation.
        using var w = new World(DialogueActionRouter.Scarlett, 7010, accepted: false);
        w.Npc.Name = "Scarlett Dalquist";
        Assert.IsFalse(w.Service.Open(w.State.Session, w.Npc.Identity));
        Assert.AreEqual(0, w.State.Session.Messages.Count);
    }

    [TestMethod]
    public void AcceptedActorOpensExactContentAndDuplicateOpenCannotResetConversation()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        Assert.IsTrue(w.Open());
        var open = w.State.Session.Messages.OfType<KnuBotOpenChatWindowMessage>().Single();
        Assert.AreEqual(w.State.Player.Identity, open.Identity); Assert.AreEqual(w.Npc.Identity, open.Target);
        Assert.AreEqual(2, open.Unknown1); Assert.AreEqual(1, open.Unknown2);
        Assert.IsFalse(w.Open());
        Assert.IsFalse(w.Service.Answer(w.State.Session, w.Npc.Identity, 0)); // The initial answer list is not sent yet.
        w.Drain();
        Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotOpenChatWindowMessage>().Count());
        Assert.AreEqual(3, w.State.Session.Messages.OfType<KnuBotAnswerListMessage>().Single().DialogOptions.Length);
        Assert.IsTrue(w.State.Session.Messages.OfType<KnuBotAppendTextMessage>().Single().Text.Contains("\n\n", StringComparison.Ordinal));
        Assert.AreEqual(0, w.State.Dao.Calls);
    }

    [TestMethod]
    public void StanAcceptedNodeTriggersExistingQuestTransactionExactlyOnceBeforeNextDialogue()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        Assert.IsTrue(w.Open()); w.Drain();
        Assert.IsTrue(w.Answer(0)); w.Drain(); // stan_goldman_002
        Assert.IsTrue(w.Answer(0)); w.Drain(); // stan_goldman_003
        w.State.Session.Messages.Clear(); int before = w.State.Dao.Calls;
        w.State.Dao.BeforeCommit = pending =>
        {
            Assert.AreEqual(0, w.State.Session.Messages.Count);
            Assert.AreEqual(DaoState.Active, pending.GetMission(new(111, AuthoredQuestService.BuyLockpick)).State);
        };
        Assert.IsTrue(w.Answer(0));
        Assert.IsFalse(w.Answer(0)); // Duplicate while the next node is queued cannot rerun acceptance.
        Assert.AreEqual(before + 1, w.State.Dao.Calls);
        Assert.AreEqual(DaoState.Active, w.State.Dao.GetMission(new(111, AuthoredQuestService.BuyLockpick)).State);
        Assert.AreEqual(1, w.State.Session.Messages.OfType<QuestFullUpdateMessage>().Count());
    }

    [TestMethod]
    public void CloseOptionAndExplicitCloseDiscardTheSessionWithoutInventedOptions()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010);
        Assert.IsTrue(w.Open()); w.Drain(); w.State.Session.Messages.Clear();
        Assert.IsTrue(w.Answer(1)); w.Drain();
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
        Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotCloseChatWindowMessage>().Count());
        Assert.AreEqual(0, w.State.Session.Messages.OfType<KnuBotAnswerListMessage>().Count());
        Assert.IsFalse(w.Answer(0));
        Assert.IsTrue(w.Open());
        Assert.IsTrue(w.Service.Close(w.State.Session, w.Npc.Identity));
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void DisconnectAndTransportReplacementCannotInheritDialogueOwnership()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        Assert.IsTrue(w.Open()); w.Drain();
        var replacement = new AuthoredQuestTests.Session(); replacement.BindPlayer(w.State.Player);
        w.State.Player.Session = replacement;
        Assert.IsFalse(w.Service.Answer(w.State.Session, w.Npc.Identity, 0));
        w.Service.Tick(w.State.Player.Playfield!);
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
        Assert.IsFalse(w.Service.Answer(replacement, w.Npc.Identity, 0));
        Assert.IsTrue(w.Service.Open(replacement, w.Npc.Identity));
        replacement.State = SessionState.Closed;
        w.Service.Tick(w.State.Player.Playfield!);
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void ZoningAndShutdownRemoveQueuedPacketsAndStagedOwnership()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        using var destination = new AuthoredQuestTests.World(7010);
        Assert.IsTrue(w.Open()); var source = w.State.Player.Playfield!;
        w.State.Player.Playfield = destination.Player.Playfield;
        w.State.Session.Messages.Clear(); w.Advance(); w.Service.Tick(source);
        Assert.IsFalse(w.Service.HasSession(w.State.Player)); Assert.AreEqual(0, w.State.Session.Messages.Count);
        w.State.Player.Playfield = source;
        Assert.IsTrue(w.Open()); w.State.Session.Messages.Clear(); w.Service.Shutdown(source); w.Drain();
        Assert.IsFalse(w.Service.HasSession(w.State.Player)); Assert.AreEqual(0, w.State.Session.Messages.Count);
    }

    [TestMethod]
    public void NpcDespawnAndSameIdentityReplacementDoNotTransferCapabilityOrSession()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        Assert.IsTrue(w.Open()); w.State.Session.Messages.Clear();
        w.State.Registry.Unregister(w.Npc.Identity); w.Drain();
        Assert.IsFalse(w.Service.HasSession(w.State.Player)); Assert.AreEqual(0, w.State.Session.Messages.Count);
        var replacement = w.State.AddNpc(DialogueActionRouter.Stan, accepted: false);
        replacement.Name = w.Npc.Name;
        Assert.IsFalse(w.Service.Open(w.State.Session, replacement.Identity));
        Assert.IsFalse(w.Answer(0));
    }

    [TestMethod]
    public void ForeignPlayerCannotAnswerOrCloseEvenWhenBothAreNearTheSameAcceptedNpc()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553);
        Assert.IsTrue(w.Open()); w.Drain();
        var other = TestWorld.CreatePlayer(333); var session = new AuthoredQuestTests.Session();
        other.Playfield = w.State.Player.Playfield; other.Session = session; session.BindPlayer(other); w.State.Registry.Register(other);
        Assert.IsFalse(w.Service.Answer(session, w.Npc.Identity, 0));
        Assert.IsFalse(w.Service.Close(session, w.Npc.Identity));
        Assert.IsTrue(w.Service.HasSession(w.State.Player)); Assert.AreEqual(0, session.Messages.Count);
    }

    [TestMethod]
    public void RealTeamJoinLeaveAndRejoinCannotTransferDialogueOrStagedRewardOwnership()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010);
        var chip = w.PrepareDoja(); w.BeginDoja(); Assert.IsTrue(w.Stage());
        var other = TestWorld.CreatePlayer(333);
        var session = new AuthoredQuestTests.Session();
        other.Playfield = w.State.Player.Playfield; other.Position = w.State.Player.Position;
        other.Session = session; session.BindPlayer(other); w.State.Registry.Register(other);
        other.Stats.Set(CharacterStat.Level, 2); other.Stats.Set(CharacterStat.Profession, 1);
        w.State.Player.Stats.Set(CharacterStat.Profession, 1);
        var otherChip = TestWorld.CreateItem(lowId: 284954, highId: 284954, instanceId: 33301);
        other.Inventory.Inventory.Content[64] = otherChip;
        // Both players and all callbacks run on this test's single owner thread.
        var teams = new TeamService(dispatchOnOwner: (_, action) => action());
        try
        {
            teams.AttachPlayer(w.State.Player); teams.AttachPlayer(other);
            void TeamAction(Player player, CharacterActionType action, Identity target, int reply = 0)
                => Assert.IsTrue(teams.TryHandle(player, new CharacterActionMessage
                { Identity = player.Identity, Action = action, Target = target, Parameter2 = reply }));
            void Join()
            {
                TeamAction(w.State.Player, CharacterActionType.TeamRequestInvite, other.Identity);
                TeamAction(other, CharacterActionType.TeamRequestReply, w.State.Player.Identity, 1);
                Assert.IsTrue(teams.AreTeammates(w.State.Player, other));
            }
            void AssertForeignCannotInherit()
            {
                session.Messages.Clear(); int calls = w.State.Dao.Calls;
                Assert.IsFalse(w.Service.Answer(session, w.Npc.Identity, 0));
                Assert.IsFalse(w.Service.Close(session, w.Npc.Identity));
                Assert.IsFalse(w.Service.StageTrade(session, new() { Target = w.Npc.Identity,
                    Container = new() { Type = IdentityType.Inventory, Instance = 64 } }));
                Assert.IsFalse(w.Service.FinishTrade(session, new() { Target = w.Npc.Identity, Decline = 0 }));
                Assert.AreEqual(calls, w.State.Dao.Calls); Assert.AreEqual(0, session.Messages.Count);
                Assert.IsTrue(w.Service.HasSession(w.State.Player)); Assert.IsFalse(w.Service.HasSession(other));
                Assert.AreSame(chip, w.State.Player.Inventory.Inventory.Content[64]);
                Assert.AreSame(otherChip, other.Inventory.Inventory.Content[64]);
            }
            Join(); AssertForeignCannotInherit();
            TeamAction(other, CharacterActionType.LeaveTeam, Identity.None);
            Assert.IsFalse(teams.AreTeammates(w.State.Player, other)); AssertForeignCannotInherit();
            Join(); AssertForeignCannotInherit();
            w.State.Session.Messages.Clear(); session.Messages.Clear();
            Assert.IsTrue(w.Finish(), w.State.Logger.LastError);
            Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotRejectedItemsMessage>().Count());
            Assert.AreEqual(0, session.Messages.OfType<KnuBotRejectedItemsMessage>().Count());
            Assert.AreSame(otherChip, other.Inventory.Inventory.Content[64]);
            Assert.IsFalse(w.State.Player.Inventory.Inventory.Content.ContainsKey(64));
            Assert.AreEqual(3, w.State.Player.Stats.GetOrOne(CharacterStat.Level));
            Assert.AreEqual(2, other.Stats.GetOrOne(CharacterStat.Level));
            int committedCalls = w.State.Dao.Calls;
            Assert.IsFalse(w.Finish()); Assert.AreEqual(committedCalls, w.State.Dao.Calls);
        }
        finally { teams.Shutdown(); }
    }

    [TestMethod]
    public void DojaTradePinsStagedItemAndDeclineLeavesInventoryAndMissionUntouched()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010); var chip = w.PrepareDoja();
        w.BeginDoja();
        Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotStartTradeMessage>().Single().NumberOfItemSlotsInTradeWindow);
        Assert.AreEqual(0, w.State.Session.Messages.OfType<KnuBotAnswerListMessage>().Count());
        Assert.IsTrue(w.Stage()); int calls = w.State.Dao.Calls;
        Assert.IsTrue(w.Service.FinishTrade(w.State.Session, new() { Target = w.Npc.Identity, Decline = 1 }));
        Assert.AreSame(chip, w.State.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(DaoState.Active, w.State.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.AreEqual(calls, w.State.Dao.Calls); Assert.AreEqual(0, w.State.Dao.Rewards.Count);
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void DojaTradeCommitPublishesAcceptedOnceAndReplayCannotAwardAgain()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010); w.PrepareDoja(); w.BeginDoja(); Assert.IsTrue(w.Stage());
        w.State.Session.Messages.Clear();
        w.State.Dao.BeforeCommit = pending => Assert.AreEqual(0, w.State.Session.Messages.Count);
        Assert.IsTrue(w.Finish(), w.State.Logger.LastError);
        Assert.IsTrue(w.State.Session.Messages[0] is KnuBotRejectedItemsMessage { Unknown1: 2, Unknown2: 0, Items.Length: 0 });
        Assert.AreEqual(3, w.State.Player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(2, w.State.Dao.Rewards.Count); Assert.IsFalse(w.State.Player.Inventory.Inventory.Content.ContainsKey(64));
        int calls = w.State.Dao.Calls; Assert.IsFalse(w.Finish()); Assert.AreEqual(calls, w.State.Dao.Calls);
        w.Drain(); Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotRejectedItemsMessage>().Count());
    }

    [TestMethod]
    public void DojaTradeStaleItemAndLateFailureDoNotConsumeOrAcknowledge()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010); var chip = w.PrepareDoja(); w.BeginDoja(); Assert.IsTrue(w.Stage());
        w.State.Session.Messages.Clear(); w.State.Player.Inventory.Inventory.Content[64] = TestWorld.CreateItem(lowId: 284954, highId: 284954, instanceId: 99);
        Assert.IsFalse(w.Finish()); Assert.AreEqual(0, w.State.Session.Messages.Count);
        w.State.Player.Inventory.Inventory.Content[64] = chip; w.State.Dao.Failure = new InvalidOperationException("late durable failure");
        Assert.IsFalse(w.Finish()); Assert.AreEqual(0, w.State.Session.Messages.Count);
        Assert.AreSame(chip, w.State.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(0, w.State.Dao.Rewards.Count);
        Assert.IsFalse(w.State.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void DojaTradeUnknownCommitQuarantinesAndDropsTheSessionWithoutRetryOrAcknowledgement()
    {
        using var w = new World(DialogueActionRouter.Scarlett, 7010); w.PrepareDoja(); w.BeginDoja(); Assert.IsTrue(w.Stage());
        w.State.Session.Messages.Clear(); w.State.Dao.UnknownCommit = true;
        Assert.IsFalse(w.Finish()); Assert.IsTrue(w.State.Player.IsPersistenceQuarantined);
        Assert.AreEqual(0, w.State.Session.Messages.Count); Assert.IsFalse(w.Service.HasSession(w.State.Player));
        int calls = w.State.Dao.Calls; Assert.IsFalse(w.Finish()); Assert.AreEqual(calls, w.State.Dao.Calls);
    }

    [TestMethod]
    public void StanFactoryTradeUsesFourSlotsAndDurableRewardBeforeAcceptedContinuation()
    {
        using var w = new World(DialogueActionRouter.Stan, 6553); var factory = w.State.Add(248306);
        w.State.Activate(AuthoredQuestService.DeliverFactory);
        Assert.IsTrue(w.Open()); w.Drain(); w.State.Session.Messages.Clear();
        Assert.IsTrue(w.Answer(0)); w.Drain();
        var start = w.State.Session.Messages.OfType<KnuBotStartTradeMessage>().Single();
        Assert.AreEqual(4, start.NumberOfItemSlotsInTradeWindow); Assert.IsTrue(start.Message.Contains("Stanley Goodman", StringComparison.Ordinal));
        Assert.IsTrue(w.Stage()); w.State.Session.Messages.Clear();
        w.State.Dao.BeforeCommit = pending =>
        {
            Assert.AreSame(factory, w.State.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(0, w.State.Session.Messages.Count); Assert.AreEqual(0, pending.Items[10].ContainerType);
            Assert.AreEqual(DaoState.Completed, pending.GetMission(new(111, AuthoredQuestService.DeliverFactory)).State);
            Assert.AreEqual(DaoState.Active, pending.GetMission(new(111, AuthoredQuestService.TalkSarah)).State);
        };
        Assert.IsTrue(w.Finish(), w.State.Logger.LastError);
        Assert.IsTrue(w.State.Session.Messages[0] is CharacterActionMessage { Action: CharacterActionType.DeleteItem });
        Assert.IsTrue(w.State.Session.Messages[1] is KnuBotRejectedItemsMessage { Items.Length: 0 });
        Assert.AreEqual(1240, w.State.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(1, w.State.Player.Inventory.Inventory.Content.Values.Count(item => item.LowId == 296572));
        Assert.IsFalse(w.Finish()); w.Drain();
        Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotRejectedItemsMessage>().Count());
    }

    [TestMethod]
    public void SarahAcceptedDialogueRecoveryTradeAndVernonHandoffUseOneTransactionPerAction()
    {
        using var w = new World(DialogueActionRouter.Sarah, 6553); w.State.Activate(AuthoredQuestService.TalkSarah);
        Assert.IsTrue(w.Open()); w.Drain(); w.State.Session.Messages.Clear();
        w.State.Dao.BeforeCommit = pending => Assert.AreEqual(0, w.State.Session.Messages.Count);
        Assert.IsTrue(w.Answer(0), w.State.Logger.LastError); w.Drain();
        Assert.AreEqual(DaoState.Completed, w.State.Dao.GetMission(new(111, AuthoredQuestService.TalkSarah)).State);
        Assert.AreEqual(DaoState.Active, w.State.Dao.GetMission(new(111, AuthoredQuestService.FindThief)).State);
        Assert.AreEqual(1, w.State.Session.Messages.OfType<QuestFullUpdateMessage>().Count());
        Assert.IsTrue(w.Service.Close(w.State.Session, w.Npc.Identity)); w.State.Session.Messages.Clear();
        int ack = 0;
        w.State.Dao.BeforeCommit = pending =>
        {
            Assert.AreEqual(0, ack); Assert.AreEqual(0, w.State.Session.Messages.Count);
            Assert.AreEqual(0, w.State.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(200, pending.Items.Values.Single(item => item.LowId == 295618).Quality);
        };
        Assert.IsTrue(w.State.Service.TryUseShopThiefRemains(w.State.Player, () => ack++), w.State.Logger.LastError);
        Assert.AreEqual(1, ack); Assert.AreEqual(200, w.State.Player.Inventory.Inventory.Content[64].Quality);
        Assert.AreEqual(DaoState.Completed, w.State.Dao.GetMission(new(111, AuthoredQuestService.FindThief)).State);
        Assert.AreEqual(DaoState.Active, w.State.Dao.GetMission(new(111, AuthoredQuestService.DeliverArmor)).State);
        w.State.Dao.BeforeCommit = null; w.State.Session.Messages.Clear();
        Assert.IsTrue(w.Open()); w.Drain(); w.State.Session.Messages.Clear();
        Assert.IsTrue(w.Answer(0)); w.Drain();
        var start = w.State.Session.Messages.OfType<KnuBotStartTradeMessage>().Single();
        Assert.AreEqual(1, start.NumberOfItemSlotsInTradeWindow); Assert.IsTrue(start.Message.Contains("Sarah Greene", StringComparison.Ordinal));
        Assert.IsTrue(w.Stage()); w.State.Session.Messages.Clear();
        w.State.Dao.BeforeCommit = pending =>
        {
            Assert.AreEqual(0, w.State.Session.Messages.Count);
            Assert.AreEqual(295618, w.State.Player.Inventory.Inventory.Content[64].LowId);
            Assert.AreEqual(DaoState.Completed, pending.GetMission(new(111, AuthoredQuestService.DeliverArmor)).State);
            Assert.AreEqual(DaoState.Active, pending.GetMission(new(111, AuthoredQuestService.TalkVernon)).State);
            Assert.AreEqual(1280L, pending.Stats[(int)CharacterStat.Cash]);
        };
        Assert.IsTrue(w.Finish(), w.State.Logger.LastError);
        Assert.AreEqual(2229, w.State.Player.Stats.GetOrZero(CharacterStat.XP));
        Assert.AreEqual(1280, w.State.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsTrue(w.State.Player.Inventory.Inventory.Content.Values.Any(item => item.LowId == 296574 && item.Quality == 1));
        Assert.AreEqual(1, w.State.Dao.Rewards.Count); Assert.IsFalse(w.Finish());
        var tip = w.State.Session.Messages.OfType<QuestFullUpdateMessage>().Single().Quests.Single();
        Assert.AreEqual(unchecked((int)0x555BE9F7), tip.QuestId.Instance);
        Assert.AreEqual(unchecked((int)0x78E0FC68), tip.UnknownId1.Instance);
        w.Drain(); Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotRejectedItemsMessage>().Count());
    }

    [TestMethod]
    public void SarahTradeRollbackPreservesExactArmorAndHasNoAcceptedFrame()
    {
        using var w = new World(DialogueActionRouter.Sarah, 6553); var armor = w.State.Add(295618);
        w.State.Activate(AuthoredQuestService.DeliverArmor);
        Assert.IsTrue(w.Open()); w.Drain(); Assert.IsTrue(w.Answer(0)); w.Drain(); Assert.IsTrue(w.Stage());
        w.State.Session.Messages.Clear(); w.State.Dao.Failure = new InvalidOperationException("late Sarah handoff failure");
        Assert.IsFalse(w.Finish()); Assert.AreSame(armor, w.State.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(104, w.State.Dao.Items[10].ContainerType); Assert.AreEqual(0, w.State.Dao.Rewards.Count);
        Assert.IsNull(w.State.Dao.GetMission(new(111, AuthoredQuestService.TalkVernon)));
        Assert.AreEqual(0, w.State.Session.Messages.Count); Assert.IsFalse(w.State.Player.IsPersistenceQuarantined);
        w.State.Dao.Failure = null; Assert.IsTrue(w.Finish(), w.State.Logger.LastError);
    }

    [TestMethod]
    public void SarahTradeUnknownCommitKeepsStaleMemoryQuarantinedAndNeverAcknowledgesOrRetries()
    {
        using var w = new World(DialogueActionRouter.Sarah, 6553); var armor = w.State.Add(295618);
        w.State.Activate(AuthoredQuestService.DeliverArmor);
        Assert.IsTrue(w.Open()); w.Drain(); Assert.IsTrue(w.Answer(0)); w.Drain(); Assert.IsTrue(w.Stage());
        w.State.Session.Messages.Clear(); w.State.Dao.UnknownCommit = true;
        Assert.IsFalse(w.Finish()); Assert.IsTrue(w.State.Player.IsPersistenceQuarantined);
        Assert.AreSame(armor, w.State.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(0, w.State.Dao.Items[10].ContainerType);
        Assert.AreEqual(1, w.State.Dao.Rewards.Count); Assert.AreEqual(0, w.State.Session.Messages.Count);
        int calls = w.State.Dao.Calls; Assert.IsFalse(w.Finish()); Assert.AreEqual(calls, w.State.Dao.Calls);
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void ZyvaniaCannotClaimSuccessfulDialogueWithoutItsRequiredTransportAdapter()
    {
        using var w = new World(DialogueActionRouter.Zyvania, 655);
        Assert.IsTrue(w.State.Npcs.TryGetBinding(w.Npc, out _));
        Assert.IsFalse(w.Open()); Assert.IsFalse(w.Answer(0));
        Assert.AreEqual(0, w.State.Session.Messages.Count); Assert.AreEqual(0, w.State.Dao.Calls);
        Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void MarcoPreservesAcceptedBlankPromptAndTwoChoicesWithoutInventingNanoExplanation()
    {
        using var w = new World(DialogueActionRouter.Marco, 6553);
        Assert.IsTrue(w.Open()); w.Drain();
        Assert.AreEqual(0, w.State.Session.Messages.OfType<KnuBotAppendTextMessage>().Count());
        var choices = w.State.Session.Messages.OfType<KnuBotAnswerListMessage>().Single().DialogOptions;
        CollectionAssert.AreEqual(new[] { "What can you tell me about Nano Programs?", "Goodbye" }, choices.Select(option => option.Text).ToArray());
        w.State.Session.Messages.Clear(); Assert.IsTrue(w.Answer(0)); w.Drain();
        Assert.AreEqual(1, w.State.Session.Messages.OfType<KnuBotCloseChatWindowMessage>().Count());
        Assert.AreEqual(0, w.State.Session.Messages.OfType<KnuBotAppendTextMessage>().Count());
        Assert.AreEqual(0, w.State.Dao.Calls); Assert.IsFalse(w.Service.HasSession(w.State.Player));
    }

    [TestMethod]
    public void TailorMeasurementUsesExactAcceptedItemAndExistingAtomicGrantBoundary()
    {
        using var w = new AuthoredQuestTests.World(127);
        w.Dao.BeforeCommit = pending => { Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count); };
        Assert.IsTrue(w.Service.TryGrantTailorMeasurement(w.Player, 7));
        Assert.AreEqual(256422, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
        Assert.IsTrue(w.Session.Messages[0] is TemplateActionMessage { Unknown2: 87, Quality: 1 });
        Assert.IsTrue(w.Session.Messages[1] is ContainerAddItemMessage { TargetPlacement: 0x6f });
        int calls = w.Dao.Calls; Assert.IsFalse(w.Service.TryGrantTailorMeasurement(w.Player, 8)); Assert.AreEqual(calls, w.Dao.Calls);
    }

    sealed class World : IDisposable
    {
        public readonly AuthoredQuestTests.World State;
        public readonly NpcCharacter Npc;
        public readonly DialogueService Service;
        long _now;
        public World(string content, int playfield, bool accepted = true)
        {
            State = new(playfield); Npc = State.AddNpc(content, accepted);
            Service = new(DialogueCatalog.Load(AppContext.BaseDirectory), new DialogueActionRouter(State.Service), () => _now);
        }
        public bool Open() => Service.Open(State.Session, Npc.Identity);
        public bool Answer(int value) => Service.Answer(State.Session, Npc.Identity, value);
        public void Advance() => _now += 20;
        public void Drain() { for (int i = 0; i < 10; i++) { Advance(); Service.Tick(State.Player.Playfield!); } }
        public ZoneEngine_New.Core.Inventory.Item PrepareDoja()
        {
            var chip = State.Add(284954); State.Activate(DojaChipInteractionRules.QuestTurnIn);
            State.Player.Stats.Set(CharacterStat.Level, 2); State.Player.Stats.Set(CharacterStat.XP, 1500); State.Player.Stats.Set(CharacterStat.Side, 1);
            return chip;
        }
        public void BeginDoja() { Assert.IsTrue(Open()); Drain(); State.Session.Messages.Clear(); Assert.IsTrue(Answer(0)); Drain(); }
        public bool Stage() => Service.StageTrade(State.Session, new() { Target = Npc.Identity, Container = new() { Type = IdentityType.Inventory, Instance = 64 } });
        public bool Finish() => Service.FinishTrade(State.Session, new() { Target = Npc.Identity, Decline = 0 });
        public void Dispose() { Service.Shutdown(State.Player.Playfield!); State.Dispose(); }
    }
}
