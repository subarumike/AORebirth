namespace ZoneEngine_New.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class MissionModuleDispatchTests
{
    [TestMethod]
    public void AcceptancePacketForwardsTheDecodedOfferAndSession()
    {
        var module = new AcceptanceProbe();
        var handler = new CreateQuestMessageHandler(module);
        var session = new RecordingZoneSession();
        MessageBody packet = new CreateQuestMessage
        {
            QuestIdentity = new Identity { Type = IdentityType.Mission, Instance = 41 }
        };

        handler.Handle(packet, session);

        Assert.AreSame(packet, module.Packet);
        Assert.AreSame(session, module.Session);
    }

    [TestMethod]
    public void JournalDeleteDispatchesOnlyTheSessionsOwnMissionPacket()
    {
        var player = TestWorld.CreatePlayer(41);
        var other = TestWorld.CreatePlayer(42);
        var session = new RecordingZoneSession { State = SessionState.InPlay };
        session.BindPlayer(player);
        var module = new LifecycleProbe();
        var handler = new QuestMessageHandler(module);
        var packet = new QuestMessage
        {
            Identity = other.Identity,
            Mission = new Identity { Type = IdentityType.Mission, Instance = 43 },
            Action = QuestAction.Delete
        };

        handler.Handle((MessageBody)packet, session);
        Assert.AreEqual(0, module.AbandonCalls);

        packet.Identity = player.Identity;
        handler.Handle((MessageBody)packet, session);
        Assert.AreEqual(1, module.AbandonCalls);
        Assert.AreSame(player, module.Player);
        Assert.AreEqual(43, module.JournalInstance);
    }

    private sealed class AcceptanceProbe : IMissionAcceptanceModule
    {
        public CreateQuestMessage? Packet { get; private set; }
        public IZoneSession? Session { get; private set; }

        public void Accept(CreateQuestMessage message, IZoneSession session)
        {
            Packet = message;
            Session = session;
        }
    }

    private sealed class LifecycleProbe : IGeneratedMissionLifecycleModule
    {
        public int AbandonCalls { get; private set; }
        public Player? Player { get; private set; }
        public int JournalInstance { get; private set; }

        public bool TryAbandon(Player player, int journalInstance)
        {
            AbandonCalls++;
            Player = player;
            JournalInstance = journalInstance;
            return true;
        }

        public bool TryEnter(Player player, Identity entrance) => throw new NotSupportedException();
        public bool TryDuplicateKey(Player player, Identity sourceSlot, Identity targetSlot) => throw new NotSupportedException();
        public void RetryPendingRewards(Player player) => throw new NotSupportedException();
        public void PrepareLogin(int characterId) => throw new NotSupportedException();
        public (Playfield? Playfield, Vector3? Position, IDisposable? Admission)? ResolveLogin(int characterId, int storedPlayfield)
            => throw new NotSupportedException();
        public void Restore(Player player) => throw new NotSupportedException();
    }
}
