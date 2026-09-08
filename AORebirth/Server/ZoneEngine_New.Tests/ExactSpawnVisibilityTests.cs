namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class ExactSpawnVisibilityTests
{
    [TestMethod]
    public void ExactSpawnIsDeliveredWithoutCallingTheIncompatibleTypedCodec()
    {
        var (player, session, locality) = Create();
        var source = new ExactDynel();
        locality.RegisterDynel(source); locality.ActivatePlayerVisibility(player);
        Assert.AreEqual(player.Identity, source.Receiver);
        Assert.AreEqual(1, session.Packets.Count);
        CollectionAssert.AreEqual(source.Bytes, session.Packets[0]);
        Assert.AreEqual(0, session.Bodies.Count);
    }

    [TestMethod]
    public void OrdinaryDynelStillUsesItsExistingTypedSpawn()
    {
        var (player, session, locality) = Create();
        var source = new TypedDynel();
        locality.RegisterDynel(source); locality.ActivatePlayerVisibility(player);
        Assert.AreEqual(0, session.Packets.Count);
        Assert.AreEqual(1, session.Bodies.Count); Assert.AreSame(source.Body, session.Bodies[0]);
    }

    [TestMethod]
    public void FailedExactSpawnDoesNotLeaveAFalseVisibleMarkThatSuppressesRetry()
    {
        var (player, session, locality) = Create();
        locality.RegisterDynel(new ExactDynel()); session.FailNext = true;
        Assert.ThrowsException<InvalidOperationException>(() => locality.ActivatePlayerVisibility(player));
        Assert.AreEqual(0, session.Packets.Count);
        locality.ActivatePlayerVisibility(player);
        Assert.AreEqual(1, session.Packets.Count);
        locality.ActivatePlayerVisibility(player);
        Assert.AreEqual(1, session.Packets.Count, "Successful spawn remains de-duplicated.");
    }

    static (Player, Session, PlayfieldLocality) Create()
    {
        var player = TestWorld.CreatePlayer(700);
        var session = new Session(); player.Session = session; session.BindPlayer(player);
        var locality = new PlayfieldLocality(0x160001, null);
        locality.RegisterDynel(player);
        return (player, session, locality);
    }
    sealed class ExactDynel() : Dynel(new() { Type = IdentityType.Corpse, Instance = 1000001 })
    {
        internal byte[] Bytes = [1, 2, 3, 4];
        internal Identity Receiver;
        public override byte[] BuildSpawnPacket(Identity receiver) { Receiver = receiver; return Bytes; }
        public override MessageBody BuildSpawnMessage() => throw new AssertFailedException("Exact accepted wire must not be decoded or re-encoded.");
    }
    sealed class TypedDynel() : Dynel(new() { Type = IdentityType.Corpse, Instance = 1000002 })
    {
        internal MessageBody Body = new DespawnMessage();
        public override MessageBody BuildSpawnMessage() => Body;
    }
    sealed class Session : IZoneSession
    {
        internal readonly List<byte[]> Packets = [];
        internal readonly List<MessageBody> Bodies = [];
        internal bool FailNext;
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("Injected transport failure."); }
            Packets.Add((byte[])packet.Clone());
        }
        public void Send(Message message) => Bodies.Add(message.Body);
        public void Send(MessageBody body) => Bodies.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
