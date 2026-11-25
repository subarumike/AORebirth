namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Teams;

[TestClass]
public sealed class CampRefusalTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RefusedCampClearsTheClientTimerAndAllowsAnotherCamp(bool attacking)
    {
        using var world = new TriggerTestWorld();
        var handler = new CharacterActionMessageHandler(null!, new TeamService());
        Player camper = world.First;
        var session = world.FirstSession;
        foreach (Player player in new[] { camper, world.Second })
        {
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 100);
        }

        Player attacker = attacking ? camper : world.Second;
        attacker.SetFightingTarget((attacking ? world.Second : camper).Identity);
        Assert.IsTrue(camper.IsInCombat);
        Assert.AreEqual(attacking, camper.FightingTarget.Instance != 0);
        session.Sent.Clear();

        var camp = new CharacterActionMessage { Identity = camper.Identity, Action = CharacterActionType.Logout };
        handler.Handle(camp, session);
        handler.Handle(camp, session);

        Assert.IsFalse(session.IsClosed);
        Assert.AreSame(world.Playfield, camper.Playfield);
        Assert.IsTrue(world.Registry.TryGet(camper.Identity, out Dynel? stillRegistered));
        Assert.AreSame(camper, stillRegistered);
        Assert.IsFalse(session.Sent.OfType<StartLogoutMessage>().Any());
        var feedback = session.Sent.OfType<FeedbackMessage>().ToArray();
        Assert.AreEqual(2, feedback.Length);
        Assert.IsTrue(feedback.All(message => message.MessageId == ClientFeedback.CantLogoutInFight));
        var cancellations = session.Sent.OfType<CharacterActionMessage>().ToArray();
        Assert.AreEqual(2, cancellations.Length);
        for (int i = 0; i < cancellations.Length; ++i)
        {
            CharacterActionMessage message = cancellations[i];
            Assert.AreEqual(CharacterActionType.StopLogout, message.Action);
            Assert.AreEqual(camper.Identity, message.Identity);
            Assert.AreEqual(Identity.None, message.Target);
            Assert.AreEqual(0, message.Unknown1);
            Assert.AreEqual(0, message.Parameter1);
            Assert.AreEqual(0, message.Parameter2);
            Assert.AreEqual((short)0, message.Unknown2);
            Assert.AreSame(feedback[i], session.Sent[session.Sent.IndexOf(message) - 1]);
        }

        using (var bytes = new MemoryStream())
        using (var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(bytes))
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            resolver.GetSerializer(typeof(CharacterActionMessage)).Serialize(writer, new SerializationContext(resolver), cancellations[0]);
            CollectionAssert.AreEqual(Convert.FromHexString("5E4777700000C35000000001010000007A00000000000000000000000000000000000000000000"), bytes.ToArray());
        }

        attacker.SetFightingTarget(Identity.None);
        Assert.IsFalse(camper.IsInCombat);
        camper.Playfield = null;
        handler.Handle(camp, session);
        Assert.AreEqual(camper.Identity, session.Sent.OfType<StartLogoutMessage>().Single().Identity);
        Assert.IsTrue(session.IsClosed);
    }

    [TestMethod]
    public void OrdinaryCampStillStartsLogoutAfterSessionRebind()
    {
        var handler = new CharacterActionMessageHandler(null!, new TeamService());
        Player player = TestWorld.CreatePlayer(7);
        var prior = new RecordingZoneSession { State = SessionState.InPlay };
        player.EnterOnline(prior);
        var camp = new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.Logout };
        handler.Handle(camp, prior);
        Assert.IsTrue(prior.IsClosed);
        Assert.AreEqual(player.Identity, prior.Sent.OfType<StartLogoutMessage>().Single().Identity);

        prior.UnbindPlayer();
        var reconnect = new RecordingZoneSession { State = SessionState.InPlay };
        player.EnterOnline(reconnect);
        handler.Handle(camp, prior);
        Assert.IsFalse(reconnect.IsClosed);
        handler.Handle(camp, reconnect);
        Assert.IsTrue(reconnect.IsClosed);
        Assert.AreEqual(player.Identity, reconnect.Sent.OfType<StartLogoutMessage>().Single().Identity);
        Assert.IsFalse(reconnect.Sent.OfType<CharacterActionMessage>().Any());
    }
}
