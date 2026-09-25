namespace ZoneEngine_New.Tests;

using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class SetStatMessageHandlerTests
{
    [TestMethod]
    public void InRangeAggDefIsStoredAndEchoed()
    {
        (Player player, RecordingZoneSession session) = Playing();
        var handler = new SetStatMessageHandler();

        handler.Handle(Request(player, CharacterStat.AggDef, 40), session);

        Assert.AreEqual(40, player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base));
        AssertEcho(session, player, 40u);
    }

    [TestMethod]
    public void AggDefIsClampedToSliderRange()
    {
        (Player player, RecordingZoneSession session) = Playing();
        var handler = new SetStatMessageHandler();

        handler.Handle(Request(player, CharacterStat.AggDef, 250), session);
        Assert.AreEqual(100, player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base));
        AssertEcho(session, player, 100u);

        session.Sent.Clear();
        handler.Handle(Request(player, CharacterStat.AggDef, -400), session);
        Assert.AreEqual(-100, player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base));
        AssertEcho(session, player, unchecked((uint)-100));
    }

    [TestMethod]
    public void AlreadyClampedValueIsEchoedWhenTheRequestIsOutOfRange()
    {
        (Player player, RecordingZoneSession session) = Playing();
        player.Stats.Set(CharacterStat.AggDef, 100, StatDetail.Base);
        var handler = new SetStatMessageHandler();

        handler.Handle(Request(player, CharacterStat.AggDef, 180), session);

        Assert.AreEqual(100, player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base));
        AssertEcho(session, player, 100u);
    }

    [TestMethod]
    public void OtherStatsIdentitiesAndSessionsAreIgnored()
    {
        (Player player, RecordingZoneSession session) = Playing();
        player.Stats.Set(CharacterStat.Cash, 50, StatDetail.Base);
        player.Stats.Set(CharacterStat.AggDef, 10, StatDetail.Base);
        var handler = new SetStatMessageHandler();

        handler.Handle(Request(player, CharacterStat.Cash, 999), session);
        handler.Handle(new SetStatMessage(CharacterStat.AggDef, 80)
        {
            Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = player.Identity.Instance + 1 }
        }, session);
        session.State = SessionState.Connected;
        handler.Handle(Request(player, CharacterStat.AggDef, -20), session);

        Assert.AreEqual(50, player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base));
        Assert.AreEqual(10, player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base));
        Assert.AreEqual(0, session.Sent.Count);
    }

    static (Player Player, RecordingZoneSession Session) Playing()
    {
        Player player = TestWorld.CreatePlayer(8101);
        var session = new RecordingZoneSession { State = SessionState.InPlay };
        session.BindPlayer(player);
        player.Session = session;
        return (player, session);
    }

    static SetStatMessage Request(Player player, CharacterStat stat, int value)
        => new(stat, value) { Identity = player.Identity };

    static void AssertEcho(RecordingZoneSession session, Player player, uint value)
    {
        var message = (StatMessage)session.Sent.Single();
        Assert.AreEqual(player.Identity, message.Identity);
        Assert.AreEqual(1, message.Stats.Length);
        Assert.AreEqual(CharacterStat.AggDef, message.Stats[0].Value1);
        Assert.AreEqual(value, message.Stats[0].Value2);
    }
}
