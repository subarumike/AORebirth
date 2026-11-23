namespace ZoneEngine_New.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Teams;
    using WirePeer = DoorNetworkIntegrationTests.WirePeer;

    [TestClass]
    public sealed class HealthSynchronizationTests
    {
        const int DisplayedHealth = 1072;
        const int FullHealth = 4846;

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(60000)]
        public async Task FullServerHealthRepairsOwningSelectedTeammateAndNearbyClients(bool periodic)
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            using var world = new TriggerTestWorld(teams: teams);
            Player owner = world.First;
            Player teammate = world.Second;
            Player observer = world.AddPlayer(3);
            foreach (Player player in new[] { owner, teammate, observer })
            {
                player.Stats.Set(CharacterStat.Level, 160);
                player.Stats.Set(CharacterStat.MaxNanoEnergy, 100);
                player.Stats.Set(CharacterStat.CurrentNano, 100);
                player.Stats.Set(CharacterStat.MaxHealth, FullHealth);
                player.Stats.Set(CharacterStat.Health, DisplayedHealth, dirty: true);
                teams.AttachPlayer(player);
                world.Locality.ActivatePlayerVisibility(player);
            }
            Assert.IsTrue(teams.TryHandle(owner, new CharacterActionMessage
            {
                Identity = owner.Identity, Action = CharacterActionType.TeamRequestInvite, Target = teammate.Identity,
            }));
            Assert.IsTrue(teams.TryHandle(teammate, new CharacterActionMessage
            {
                Identity = teammate.Identity, Action = CharacterActionType.TeamRequestReply, Target = owner.Identity, Parameter2 = 1,
            }));
            TeamSnapshot? team = teams.GetTeam(owner);
            Assert.IsNotNull(team);
            CollectionAssert.Contains(team.MemberIds.ToArray(), teammate.Identity.Instance);
            Assert.IsNull(teams.GetTeam(observer));
            teammate.SetTarget(owner.Identity);

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            await using var ownerWire = await WirePeer.ConnectAsync(listener, owner);
            await using var teammateWire = await WirePeer.ConnectAsync(listener, teammate);
            await using var observerWire = await WirePeer.ConnectAsync(listener, observer);
            WirePeer[] peers = [ownerWire, teammateWire, observerWire];
            owner.FlushDirtyStats();
            await Task.WhenAll(peers.Select(peer => ReadHealth(peer, owner.Identity, DisplayedHealth)));

            // Model a lost update. Only the server knows the new HP until an attempted heal reconciles it.
            owner.Stats.Set(CharacterStat.Health, FullHealth);
            ApplyHeal(owner, periodic);
            owner.FlushDirtyStats();
            await Task.WhenAll(peers.Select(peer => ReadHealth(peer, owner.Identity, FullHealth)));
            Assert.AreEqual(FullHealth, owner.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(FullHealth, owner.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.IsTrue(peers.All(peer => peer.Log.Errors.IsEmpty));
        }

        [TestMethod]
        public void FullHealthRefreshCoalescesStackedHealsWithoutInventingAHeal()
        {
            using var world = new TriggerTestWorld();
            Player owner = world.First;
            owner.Stats.Set(CharacterStat.MaxHealth, FullHealth);
            owner.Stats.Set(CharacterStat.Health, FullHealth);
            owner.FlushDirtyStats();
            world.FirstSession.Sent.Clear();

            ApplyHeal(owner, periodic: false);
            ApplyHeal(owner, periodic: true);
            owner.FlushDirtyStats();
            ApplyHeal(owner, periodic: false);
            owner.FlushDirtyStats();

            var health = world.FirstSession.Sent.OfType<StatMessage>()
                .SelectMany(message => message.Stats).Where(value => value.Value1 == CharacterStat.Health).ToArray();
            Assert.AreEqual(1, health.Length);
            Assert.AreEqual((uint)FullHealth, health[0].Value2);
            Assert.IsFalse(world.FirstSession.Sent.OfType<HealthDamageMessage>().Any());
        }

        [TestMethod]
        public void DamageRealHealingAndPassiveRegenerationReachNearbyObservers()
        {
            using var world = new TriggerTestWorld();
            Player owner = world.First;
            Player observer = world.Second;
            owner.Stats.Set(CharacterStat.MaxHealth, 100);
            owner.Stats.Set(CharacterStat.Health, 100);
            owner.Stats.Set(CharacterStat.Breed, (int)Breed.Solitus);
            owner.Stats.Set(CharacterStat.HealDelta, 10);
            world.Locality.ActivatePlayerVisibility(owner);
            world.Locality.ActivatePlayerVisibility(observer);

            owner.ApplyDamage(owner, 50, HitType.Normal);
            owner.FlushDirtyStats();
            AssertLastHealth(world.FirstSession, owner, 50);
            AssertLastHealth(world.SecondSession, owner, 50);

            ApplyHeal(owner, periodic: false, amount: 10);
            foreach (RecordingZoneSession session in new[] { world.FirstSession, world.SecondSession })
            {
                HealthDamageMessage heal = session.Sent.OfType<HealthDamageMessage>().Single();
                Assert.AreEqual(owner.Identity, heal.Identity);
                Assert.AreEqual(60, heal.Unknown1);
                Assert.AreEqual(10, heal.Unknown2);
            }

            owner.Tick(30);
            Assert.AreEqual(73, owner.Stats.GetOrZero(CharacterStat.Health));
            AssertLastHealth(world.FirstSession, owner, 73);
            AssertLastHealth(world.SecondSession, owner, 73);
        }

        static void ApplyHeal(Player owner, bool periodic, int amount = 15)
        {
            var hit = new ItemSpell
            {
                FunctionType = (int)FunctionType.Hit, Target = 2,
                Arguments = [(int)CharacterStat.Health, amount, amount, 0],
            };
            if (!periodic)
            {
                var heal = new ItemTemplate { SpellList = new() { [EventType.OnUse] = [hit] } };
                Assert.IsTrue(heal.ExecuteHitProjection(owner, new StubInventoryRepository(), new StubItemBuilder()));
                return;
            }

            owner.Stats.Set(CharacterStat.MaxNCU, 100);
            hit.TickCount = 3;
            hit.TickInterval = 100;
            DateTime start = DateTime.UtcNow;
            Assert.AreEqual(BuffApplyDecision.Apply,
                owner.TryApplyBuff(TestNanos.Create(9001, modifiers: [hit]), owner.Identity, start, out _, out _));
            NanoRuntime.Tick(owner, start.AddSeconds(1));
        }

        static async Task ReadHealth(WirePeer peer, Identity owner, int expected)
        {
            var codec = new ZoneMessageCodec();
            for (int packet = 0; packet < 10; ++packet)
            {
                Message decoded = codec.Deserialize(await peer.ReadPacketAsync())!;
                Assert.AreEqual(peer.Player.Identity.Instance, decoded.Header.Receiver);
                Assert.IsNotInstanceOfType<HealthDamageMessage>(decoded.Body);
                if (decoded.Body is not StatMessage { Identity: var id } stat || id != owner)
                    continue;
                if (stat.Stats.Any(value => value.Value1 == CharacterStat.Health && value.Value2 == (uint)expected))
                    return;
            }
            Assert.Fail("The client did not receive the authoritative HP stat.");
        }

        static void AssertLastHealth(RecordingZoneSession session, Player owner, uint expected)
        {
            var health = session.Sent.OfType<StatMessage>().Where(message => message.Identity == owner.Identity)
                .SelectMany(message => message.Stats).Last(value => value.Value1 == CharacterStat.Health);
            Assert.AreEqual(expected, health.Value2);
        }
    }
}
