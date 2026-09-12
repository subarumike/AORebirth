namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class GeneratedMissionCorpseInteractionTests
{
    [TestMethod]
    public void OpenCloseReopenPreservesWireOrderSingleClaimAndEachDelayedAcknowledgement()
    {
        using var w = new World();
        Assert.IsTrue(w.Open("open"));
        var inventory = (InventoryUpdateMessage)w.Session.Messages[0];
        Assert.AreEqual(21, inventory.NumberOfSlots); Assert.AreEqual(2, inventory.Unknown1);
        Assert.AreEqual(1, inventory.Unknown2); Assert.AreEqual(0, inventory.Entries.Length);
        Assert.AreEqual(1, inventory.SlotnumberInMainInventory);
        w.At(100); Assert.IsTrue(w.Open("close"));
        Assert.AreEqual(3, w.Session.Messages.Count, "Close must not send another InventoryUpdate.");
        var close = (ActionMessage)w.Session.Messages[1];
        Assert.AreEqual(w.Corpse.Identity, close.Identity); Assert.AreEqual(w.Player.Identity, close.Target);
        Assert.AreEqual(1, close.Unknown); Assert.AreEqual(1, close.ActionCode); Assert.AreEqual(0x66, close.ActionIdentity);
        var finished = (CharacterActionMessage)w.Session.Messages[2];
        Assert.AreEqual(CharacterActionType.UseActionFinished, finished.Action); Assert.AreEqual(w.Player.Identity, finished.Identity);
        Assert.AreEqual(Identity.None, finished.Target); Assert.AreEqual(0, finished.Parameter1); Assert.AreEqual(0, finished.Parameter2);
        w.At(200); Assert.IsTrue(w.Open("reopen"));
        Assert.AreEqual(2, ((InventoryUpdateMessage)w.Session.Messages[3]).SlotnumberInMainInventory);
        w.At(499); w.Corpse.Tick(0); Assert.AreEqual(0, w.Claims);
        w.At(500); w.Corpse.Tick(0); Assert.AreEqual(1, w.Claims); Assert.IsTrue(w.State.CorpseClaimed);
        Assert.AreEqual(0, w.Acknowledged.Count);
        w.At(550); w.Corpse.Tick(0); CollectionAssert.AreEqual(new[] { "open" }, w.Acknowledged);
        Assert.IsNull(w.Corpse.Playfield, "Repeated requests must not extend corpse visibility.");
        w.At(650); w.Corpse.FlushPendingAcknowledgements();
        CollectionAssert.AreEqual(new[] { "open", "close" }, w.Acknowledged);
        w.At(750); w.Corpse.FlushPendingAcknowledgements();
        CollectionAssert.AreEqual(new[] { "open", "close", "reopen" }, w.Acknowledged);
        Assert.IsFalse(w.Corpse.HasPendingAcknowledgements); Assert.AreEqual(1, w.Claims);
    }

    [TestMethod]
    public void ReconnectedSessionCannotCloseInheritClaimOrReceiveDelayedAcknowledgements()
    {
        foreach (bool committed in new[] { false, true })
        {
            using var w = new World(); Assert.IsTrue(w.Open("open"));
            if (committed) { w.At(500); w.Corpse.Tick(0); }
            w.Player.Session = new Session(); w.Player.Session.BindPlayer(w.Player);
            Assert.IsFalse(w.Open("stale-close"));
            w.At(550); w.Corpse.Tick(0); w.Corpse.FlushPendingAcknowledgements();
            Assert.AreEqual(committed ? 1 : 0, w.Claims);
            Assert.AreEqual(0, w.Acknowledged.Count); Assert.AreEqual(0, w.Denied);
            Assert.IsFalse(w.Corpse.HasPendingAcknowledgements);
        }
    }

    [TestMethod]
    public void KnownClaimFailureCancelsAllQueuedAcknowledgementsWithoutAutomaticRetry()
    {
        using var w = new World(); w.ClaimSucceeds = false;
        Assert.IsTrue(w.Open("open")); w.At(100); Assert.IsTrue(w.Open("close"));
        w.At(500); w.Corpse.Tick(0); w.At(750); w.Corpse.Tick(0);
        Assert.AreEqual(1, w.Claims); Assert.AreEqual(1, w.Denied);
        Assert.AreEqual(0, w.Acknowledged.Count); Assert.IsFalse(w.State.CorpseClaimed);
        Assert.IsFalse(w.Corpse.HasPendingAcknowledgements);
    }

    [TestMethod]
    public void ClosedInitiatingSessionCannotClaimEvenBeforePlayerReferenceIsUnbound()
    {
        using var w = new World(); Assert.IsTrue(w.Open("open"));
        w.Session.Close(); w.At(550); w.Corpse.Tick(0);
        Assert.AreEqual(0, w.Claims); Assert.AreEqual(0, w.Acknowledged.Count);
        Assert.IsFalse(w.Corpse.HasPendingAcknowledgements);
    }

    [TestMethod]
    public void CorpseReplyForcesAcceptedTemp4WithoutChangingOrdinaryRepliesOrRequestTargets()
    {
        Identity original = new() { Type = IdentityType.CanbeAffected, Instance = 77 };
        Identity corpse = new() { Type = IdentityType.Corpse, Instance = 77 };
        var request = new GenericCmdMessage { Identity = original, Temp4 = 987654, Target = [original] };
        var reply = GenericCmdMessageHandler.Reply(request, corpse, 1, corpseUse: true);
        Assert.AreEqual(1, reply.Temp4); Assert.AreEqual(corpse, reply.Target[0]);
        Assert.AreEqual(987654, request.Temp4); Assert.AreEqual(original, request.Target[0]);
        Assert.AreNotSame(request.Target, reply.Target);
        Assert.AreEqual(987654, GenericCmdMessageHandler.Reply(request, corpse, 1).Temp4);
    }

    sealed class World : IDisposable
    {
        const long Start = 1000000000;
        long _now = Start;
        readonly ServiceProvider _services;
        internal Player Player = TestWorld.CreatePlayer(77);
        internal Session Session = new();
        internal GeneratedMissionObject State;
        internal GeneratedMissionCorpseDynel Corpse;
        internal List<string> Acknowledged = [];
        internal int Claims, Denied;
        internal bool ClaimSucceeds = true;
        internal World()
        {
            // No runtime build, world-content load, database, listener or heartbeat.
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            _services = new ServiceCollection().AddSingleton(new DynelRegistry())
                .AddSingleton(new PlayfieldLocality(0x160001, null)).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, _services);
            typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, 1);
            Player.Playfield = playfield; Player.Session = Session; Session.BindPlayer(Player);
            Player.Stats.Set(CharacterStat.Health, 100);
            State = new() { OwnerId = 77, QuestType = 0xDAC3, QuestInstance = 99, RuntimeInstance = 7001,
                CorpseCredits = 21, CorpseExpiresAtUtcTicks = Start + TimeSpan.TicksPerSecond * 60 };
            Corpse = new(null!, State, 0x160001, () => _now) { Playfield = playfield };
        }
        internal void At(int milliseconds) => _now = Start + TimeSpan.TicksPerMillisecond * milliseconds;
        internal bool Open(string label) => Corpse.Open(Player, _ => { Claims++; return ClaimSucceeds; },
            identity => { Assert.AreEqual(Corpse.Identity, identity); Acknowledged.Add(label); }, () => Denied++);
        public void Dispose() => _services.Dispose();
    }
    sealed class Session : IZoneSession
    {
        internal List<MessageBody> Messages = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) => throw new NotSupportedException();
        public void Send(Message message) => Messages.Add(message.Body);
        public void Send(MessageBody body) => Messages.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
