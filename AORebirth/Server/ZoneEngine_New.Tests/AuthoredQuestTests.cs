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
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

[TestClass]
public sealed class AuthoredQuestTests
{
    [TestMethod]
    public void PackagedAcceptedAuthoredCatalogPreservesExactObjectives()
    {
        var catalog = LoadCatalog();
        Assert.IsTrue(catalog.Definitions.Single(value => value.QuestId == AuthoredQuestService.BuyLockpick).Objectives
            .Any(value => value.ObjectiveId == "mission_555BD124_buy_lockpick" && value.RequiredCount == 1 && value.IsResolved));
        Assert.IsTrue(catalog.Definitions.Single(value => value.QuestId == DojaChipInteractionRules.QuestTurnIn).Objectives
            .Any(value => value.ObjectiveId == "mission_55AA2421_turnin" && value.RequiredCount == 1 && value.IsResolved));
    }

    [TestMethod]
    public void LockpickGrantRetirementAndMissionHandoffShareOneCommit()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyLockpick); var sealedItem = w.Add(295999);
        bool checkedBefore = false, isolated = false, fullPlan = false;
        w.Dao.BeforeCommit = working => { checkedBefore = true; isolated = w.Player.Inventory.Inventory.Content[64] == sealedItem && w.Session.Messages.Count == 0;
            fullPlan = working.Items[10].ContainerType == 0 && working.Items.Values.Any(value => value.LowId == 95577)
                && working.GetMission(new(111, AuthoredQuestService.BuyLockpick)).State == DaoState.Completed
                && working.GetMission(new(111, AuthoredQuestService.Strongbox)).State == DaoState.Active; };
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, sealedItem));
        Assert.IsTrue(checkedBefore && isolated && fullPlan); Assert.AreEqual(1, w.Dao.Calls);
        Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
        Assert.AreEqual(95577, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
        Assert.IsTrue(w.Session.Messages[0] is TemplateActionMessage { Unknown2: 87 });
        Assert.IsTrue(w.Session.Messages[1] is ContainerAddItemMessage);
        Assert.IsTrue(w.Session.Messages[2] is TemplateActionMessage { Unknown2: 3 });
        Assert.IsTrue(w.Session.Messages[3] is CharacterActionMessage { Action: CharacterActionType.DeleteItem });
        Assert.AreEqual(2, w.Session.Messages.OfType<byte[]>().Count());
        Assert.IsTrue(w.Session.Messages.OfType<QuestFullUpdateMessage>().Single().Quests.Single().QuestId.Instance == unchecked((int)0x555BE9C5));
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, sealedItem));
    }

    [TestMethod]
    public void FailedLockpickCommitPreservesSourceMissionInventoryAndNoAcknowledgement()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyLockpick); var item = w.Add(295999);
        w.Dao.Failure = new InvalidOperationException("late mission write failure");
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item));
        Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(104, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(1, w.Dao.Items.Count); Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.BuyLockpick)).State);
        Assert.IsNull(w.Dao.GetMission(new(111, AuthoredQuestService.Strongbox))); Assert.AreEqual(0, w.Session.Messages.Count);
        Assert.IsFalse(w.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void UnknownAuthoredCommitQuarantinesWithoutMemoryReplayOrSuccessFrames()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyLockpick); var item = w.Add(295999); w.Dao.UnknownCommit = true;
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.IsTrue(w.Player.IsPersistenceQuarantined);
        Assert.AreEqual(SessionState.Closed, w.Session.State); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(0, w.Dao.Items[10].ContainerType);
        int calls = w.Dao.Calls; Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(calls, w.Dao.Calls);
    }

    [TestMethod]
    public void MarcoContentsTipRewardAndLiveStatsCommitTogetherWithoutLosingUnsnapshottedValues()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyNano); var item = w.Add(248258);
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
            w.Session.Messages.OfType<TemplateActionMessage>().Where(value => value.Unknown2 == 87).Select(value => value.ItemLowId).ToArray());
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestService.BuyNano)).State);
        Assert.AreEqual(1, w.Dao.Calls);
    }

    [TestMethod]
    public void LateMarcoFailureRollsBackEveryGrantSourceMissionAndRewardStat()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyNano); var item = w.Add(248258);
        w.Player.Stats.Set(CharacterStat.Cash, 123); w.Dao.Stats[(int)CharacterStat.Cash] = 123;
        w.Dao.Failure = new InvalidOperationException("late insert failure");
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(1, w.Dao.Items.Count);
        Assert.AreEqual(0, w.Dao.Rewards.Count); Assert.AreEqual(123L, w.Dao.Stats[(int)CharacterStat.Cash]);
        Assert.AreEqual(123, w.Player.Stats.GetOrZero(CharacterStat.Cash)); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.BuyNano)).State); Assert.AreEqual(0, w.Session.Messages.Count);
    }

    [TestMethod]
    public void FullInventoryAndStaleRowsCannotConsumeOrAdvanceQuest()
    {
        using var w = new World(); w.Activate(AuthoredQuestService.BuyLockpick); var item = w.Add(295999); TestWorld.FillInventory(w.Player);
        Assert.IsFalse(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreEqual(0, w.Session.Messages.Count);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.BuyLockpick)).State);
        using var stale = new World(); stale.Activate(AuthoredQuestService.BuyLockpick); var moved = stale.Add(295999);
        stale.Dao.Items[10].ContainerPlacement = 65;
        Assert.IsFalse(stale.Service.TryUseItem(stale.Player, Slot, moved)); Assert.AreEqual(0, stale.Session.Messages.Count);
        Assert.IsNull(stale.Dao.GetMission(new(111, AuthoredQuestService.Strongbox)));
    }

    [TestMethod]
    public void DojaUseAcceptsWithoutConsumingAndReplayDoesNotDuplicateJournal()
    {
        using var w = new World(); var item = w.Add(284954); w.Player.Stats.Set(CharacterStat.Level, 10);
        Assert.IsTrue(w.Service.TryUseItem(w.Player, Slot, item)); Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(104, w.Dao.Items[10].ContainerType); Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, DojaChipInteractionRules.QuestTurnIn)).State);
        Assert.IsTrue(w.Session.Messages[0] is TemplateActionMessage { Unknown2: 3 }); Assert.IsTrue(w.Session.Messages[1] is byte[]);
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
    public void StrongboxPreservesLockpickAndFactoryTurnInPublishesOnlyAfterCommit()
    {
        using var w = new World(); var lockpick = w.Add(95577); w.Activate(AuthoredQuestService.Strongbox);
        Assert.IsTrue(w.Service.TryUseLockpickOnStrongbox(w.Player, Slot, lockpick));
        Assert.AreSame(lockpick, w.Player.Inventory.Inventory.Content[64]); Assert.AreEqual(104, w.Dao.Items[10].ContainerType);
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestService.Strongbox)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.DeliverFactory)).State);
        var factory = w.Player.Inventory.Inventory.Content.Single(pair => pair.Value.LowId == 248306);
        w.Session.Messages.Clear(); w.Player.Stats.Set(CharacterStat.Cash, 20); int accepted = 0;
        w.Dao.BeforeCommit = pending => { Assert.AreEqual(0, accepted); Assert.AreEqual(0, w.Session.Messages.Count); Assert.AreEqual(20, w.Player.Stats.GetOrZero(CharacterStat.Cash)); };
        Assert.IsTrue(w.Service.TryTurnInFactory(w.Player, new() { Type = IdentityType.Inventory, Instance = factory.Key }, factory.Value, () => accepted++));
        Assert.AreEqual(1, accepted); Assert.AreEqual(1260, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(DaoState.Completed, w.Dao.GetMission(new(111, AuthoredQuestService.DeliverFactory)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.TalkSarah)).State);
        Assert.AreEqual(DaoState.Active, w.Dao.GetMission(new(111, AuthoredQuestService.BuyNano)).State);
        Assert.IsTrue(w.Player.Inventory.Inventory.Content.Values.Any(item => item.LowId == 296572));
    }

    static Identity Slot => new() { Type = IdentityType.Inventory, Instance = 64 };
    static AuthoredQuestCatalog LoadCatalog() => AuthoredQuestCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Content"));

    sealed class World : IDisposable
    {
        internal readonly Player Player = TestWorld.CreatePlayer(111);
        internal readonly Session Session = new();
        internal readonly AuthoredMissionTestDao Dao = new();
        internal readonly AuthoredQuestCatalog Catalog = LoadCatalog();
        internal readonly AuthoredQuestService Service;
        internal readonly ErrorLogger Logger = new();
        internal DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        readonly InventoryFlushService _flush;
        readonly ServiceProvider _services;
        internal World(int playfield = 6553)
        {
            Player.Session = Session; Session.BindPlayer(Player);
            Player.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, new Identity { Type = IdentityType.Playfield, Instance = playfield });
            _services = new ServiceCollection().AddSingleton(new PlayfieldLocality(playfield, null)).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, _services);
            var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
            typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Dictionary<int, Player>());
            _flush = new(new Lazy<PlayfieldManager>(() => manager), new NoIndependentFlush(), new StubLogger());
            Service = new(Dao, _flush, new StubItemBuilder(), new TemplateCatalog(), new Ids(), Catalog, Logger, () => Now);
        }
        internal void Activate(string quest)
        {
            var service = new PersistentMissionService(new MissionDaoRepositoryAdapter(Dao), Catalog.Definitions);
            Assert.IsTrue(service.OfferMission(111, quest).Succeeded); Assert.IsTrue(service.AcceptMission(111, quest).Succeeded); Dao.Calls = 0;
        }
        internal Item Add(int template)
        {
            var item = TestWorld.CreateItem(lowId: template, highId: template, instanceId: 10); item.StackCount = 1;
            Player.Inventory.Inventory.Add(64, item);
            Dao.Items.Add(10, new() { InstanceId = 10, ContainerType = 104, ContainerInstance = 111, ContainerPlacement = 64,
                ItemType = item.Identity.Type != IdentityType.None ? (int)item.Identity.Type : item.Definition.ItemType,
                LowId = template, HighId = template, Quality = 1, StackCount = 1, Source = (byte)item.Source });
            return item;
        }
        public void Dispose() { _flush.Dispose(); _services.Dispose(); }
    }
    sealed class Ids : IItemInstanceIdAllocator { int _next = 1000; public int Allocate() => _next++; }
    sealed class ErrorLogger : ZoneEngine_New.Core.Logging.IZoneLogger
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
    { public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int owner, IReadOnlyList<int> nanos) => throw new InvalidOperationException("No independent item transaction expected."); }
    sealed class Session : IZoneSession
    {
        internal readonly List<object> Messages = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) => Messages.Add(packet);
        public void Send(Message message) => Messages.Add(message.Body);
        public void Send(MessageBody body) => Messages.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
