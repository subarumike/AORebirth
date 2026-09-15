namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    [TestClass]
    public sealed class ReconnectVisibilityTests
    {
        [TestMethod]
        public void ActivatePlayerVisibilityResendsNeighborhoodToANewSession()
        {
            var locality = new PlayfieldLocality(954, metaData: null);
            Player player = TestWorld.CreatePlayer(18);
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 },
                new StubItemBuilder());

            var first = new RecordingZoneSession();
            player.EnterOnline(first);
            locality.RegisterDynel(npc);
            locality.RegisterDynel(player);
            locality.ActivatePlayerVisibility(player);

            Assert.IsTrue(
                first.Sent.Exists(body => body is SimpleCharFullUpdateMessage scfu
                    && scfu.Identity.Instance == npc.Identity.Instance));

            var second = new RecordingZoneSession { State = SessionState.Loading };
            first.UnbindPlayer();
            player.EnterLinkDead(System.TimeSpan.FromSeconds(60));
            player.EnterOnline(second);
            second.State = SessionState.InPlay;
            locality.ActivatePlayerVisibility(player);

            Assert.IsTrue(
                second.Sent.Exists(body => body is SimpleCharFullUpdateMessage scfu
                    && scfu.Identity.Instance == npc.Identity.Instance),
                "Reconnect must re-send nearby dynels to the new session.");
        }
    }
}
