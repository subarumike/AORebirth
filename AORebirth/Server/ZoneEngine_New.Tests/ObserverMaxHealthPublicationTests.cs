namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Teams;

    using WirePeer = DoorNetworkIntegrationTests.WirePeer;

    [TestClass]
    public sealed class ObserverMaxHealthPublicationTests
    {
        const int MongosGolem = 95705;
        const int LifeChanneler = 96247;
        const int NanomageEnhancedAlienTankArmor = 268506;
        const int TestBatchEnd = 913647;

        static readonly Lazy<ItemBuilder> AuthoredItems = new(() => new ItemBuilder(
            new ItemTemplateCatalog(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: GameDataPaths.ResolveRuntimeRoot()),
                new StubLogger()), new StubLogger()));

        [TestMethod]
        [DataRow(MongosGolem, 1500)]
        [DataRow(LifeChanneler, 1344)]
        [Timeout(60000)]
        public async Task EquipmentAndBuffChangesKeepConnectedObserversOnEffectiveVitals(int nanoId, int maxHealthBonus)
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            ItemBuilder items = AuthoredItems.Value;
            using var world = new TriggerTestWorld(teams: teams, items: items, trackPlayers: true);
            Player subject = world.First;
            Player teammate = world.Second;
            Player selectedViewer = world.AddPlayer(3);
            Player bystander = world.AddPlayer(4);
            ((StubGameData)world.Playfield.GetRequiredService<IGameData>()).AllowMissingXpLevels = true;
            foreach (Player player in new[] { subject, teammate, selectedViewer, bystander })
            {
                player.Stats.Set(CharacterStat.Flags, 0);
                player.Stats.Set(CharacterStat.Level, 220);
                player.Stats.Set(CharacterStat.Breed, (int)Breed.Nanomage);
                player.Stats.Set(CharacterStat.Profession, (int)Profession.Enforcer);
                player.Stats.Set(CharacterStat.Stamina, 1200);
                player.Stats.Set(CharacterStat.Intelligence, 1200);
                player.Stats.Set(CharacterStat.Psychic, 1200);
                player.Stats.Set(CharacterStat.BodyDevelopment, 400);
                player.Stats.Set(CharacterStat.MaxNCU, 200);
                player.Stats.Set(CharacterStat.PercentRemainingHealth, 100);
                player.RebaseStats();
                teams.AttachPlayer(player);
            }

            int nakedBase = subject.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base);
            int backSlot = subject.Inventory.Armor.Offset + (int)ArmorSlots.Back - 1;
            Item armor = items.Create(NanomageEnhancedAlienTankArmor, NanomageEnhancedAlienTankArmor, 150,
                ItemSource.Command, instanceId: 293);
            Assert.IsTrue(subject.Inventory.Armor.Add(backSlot, armor));
            subject.RebaseStats();
            int armoredBase = subject.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base);
            int armoredMaximum = subject.Stats.GetOrZero(CharacterStat.MaxHealth);
            Assert.IsTrue(armoredBase > nakedBase, "The authored armor's Body Development must change base MaxHealth.");
            Assert.AreEqual(armoredBase + 500, armoredMaximum, "The authored armor also adds a separate MaxHealth bonus.");
            subject.Stats.Set(CharacterStat.Health, armoredMaximum - 220, dirty: true);
            subject.FlushDirtyStats();

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            await using WirePeer ownerWire = await WirePeer.ConnectAsync(listener, subject);
            await using WirePeer teammateWire = await WirePeer.ConnectAsync(listener, teammate);
            await using WirePeer selectedWire = await WirePeer.ConnectAsync(listener, selectedViewer);
            await using WirePeer bystanderWire = await WirePeer.ConnectAsync(listener, bystander);
            WirePeer[] peers = [ownerWire, teammateWire, selectedWire, bystanderWire];
            foreach (WirePeer peer in peers)
                world.Locality.ActivatePlayerVisibility(peer.Player);

            Assert.IsTrue(teams.TryHandle(subject, new CharacterActionMessage
            {
                Identity = subject.Identity,
                Action = CharacterActionType.TeamRequestInvite,
                Target = teammate.Identity
            }));
            Assert.IsTrue(teams.TryHandle(teammate, new CharacterActionMessage
            {
                Identity = teammate.Identity,
                Action = CharacterActionType.TeamRequestReply,
                Target = subject.Identity,
                Parameter2 = 1
            }));
            Assert.IsNotNull(teams.GetTeam(teammate));
            var lookAt = new LookAtMessageHandler();
            lookAt.Handle(new LookAtMessage { Target = subject.Identity }, selectedWire.Session);
            MessageBody[][] initial = await ReadBatch(peers);
            foreach (MessageBody[] observer in initial.Skip(1))
            {
                SimpleCharFullUpdateMessage spawn = observer.OfType<SimpleCharFullUpdateMessage>()
                    .Single(packet => packet.Identity == subject.Identity);
                Assert.AreEqual(armoredMaximum, spawn.Health);
                Assert.AreEqual(220, spawn.HealthDamage);
            }
            Assert.AreEqual(armoredMaximum, ((CharacterInfoPacket)initial[2].OfType<InfoPacketMessage>()
                .Single(packet => packet.Identity == subject.Identity).Info).MaxHealth);

            DateTime now = DateTime.UtcNow;
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(subject, subject, nanoId, items, new StubInventoryRepository(), now));
            Assert.IsTrue(subject.TryGetBuff(nanoId, out Buff? activeBuff));
            Assert.IsNotNull(activeBuff);
            world.PublishPendingStats(subject);
            Assert.IsTrue(MaxHealthCalculator.TryCompute(subject.Stats, out int buffedArmoredBase));
            int buffedArmoredMaximum = buffedArmoredBase + 500 + maxHealthBonus;
            Assert.AreEqual(buffedArmoredMaximum, subject.Stats.GetOrZero(CharacterStat.MaxHealth));
            MessageBody[][] afterBuff = await ReadBatch(peers);

            // The buff remains active. Removing the armor changes both the derived base and the effective maximum.
            Assert.AreSame(armor, subject.Inventory.Armor.Remove(backSlot));
            world.Playfield.QueueRebase(subject);
            world.PublishPendingStats(subject);
            Assert.IsTrue(MaxHealthCalculator.TryCompute(subject.Stats, out int buffedNakedBase));
            Assert.AreEqual(buffedNakedBase, subject.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.IsTrue(buffedNakedBase < buffedArmoredBase);
            Assert.AreEqual(buffedNakedBase + maxHealthBonus, subject.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.IsTrue(subject.TryGetBuff(nanoId, out _));
            MessageBody[][] afterUnequip = await ReadBatch(peers);

            TeamMemberInfoMessage teamMaximum = afterUnequip[1].OfType<TeamMemberInfoMessage>()
                .Single(packet => packet.Member == subject.Identity);
            Assert.AreEqual(buffedNakedBase + maxHealthBonus, teamMaximum.Unknown5);
            Assert.AreEqual(buffedNakedBase, WireStats(afterUnequip[0], subject, CharacterStat.MaxHealth).Single());
            AssertRemoteVitals(afterUnequip, subject, "Armor removal with the buff still active");

            foreach (MessageBody[] remote in afterBuff.Skip(1))
                Assert.IsTrue(PublishedVitals(remote, subject).Any(value => value.Maximum == buffedArmoredMaximum),
                    "The buffed MaxHealth must reach each observer.");

            NanoRuntime.Tick(subject, activeBuff.ExpiresAtUtc.AddTicks(1));
            world.PublishPendingStats(subject);
            Assert.AreEqual(nakedBase, subject.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.IsFalse(subject.TryGetBuff(nanoId, out _));
            lookAt.Handle(new LookAtMessage { Target = subject.Identity }, selectedWire.Session);
            MessageBody[][] afterExpiry = await ReadBatch(peers);
            AssertRemoteVitals(afterExpiry, subject, "Buff expiry");
            var inspection = (CharacterInfoPacket)afterExpiry[2].OfType<InfoPacketMessage>()
                .Single(packet => packet.Identity == subject.Identity).Info;
            Assert.AreEqual(nakedBase, inspection.MaxHealth);
            Assert.AreEqual(subject.Stats.GetOrZero(CharacterStat.Health), inspection.Health);

            Assert.IsTrue(subject.Inventory.Armor.Add(backSlot, armor));
            subject.RebaseStats();
            int reequippedHealth = subject.Stats.GetOrZero(CharacterStat.Health);
            subject.Stats.Set(CharacterStat.Health, reequippedHealth - 29, dirty: true);
            subject.FlushDirtyStats();
            MessageBody[][] afterDamage = await ReadBatch(peers);
            Assert.AreEqual(reequippedHealth, afterDamage[1].OfType<TeamMemberInfoMessage>()
                .Single(packet => packet.Member == subject.Identity).Unknown6);
            AssertRemoteVitals(afterDamage, subject, "Current HP changed after the team update");

            world.Playfield.QueueRebase(subject);
            world.PublishPendingStats(subject);
            MessageBody[][] unchanged = await ReadBatch(peers);
            Assert.IsTrue(unchanged.All(messages => !PublishedVitals(messages, subject).Any()),
                "An unchanged rebase must not repeat vitals publications.");
            Assert.IsTrue(peers.All(peer => peer.Log.Errors.IsEmpty));
        }

        static void AssertRemoteVitals(MessageBody[][] frames, Player subject, string stage)
        {
            int expectedMaximum = subject.Stats.GetOrZero(CharacterStat.MaxHealth);
            int expectedCurrent = subject.Stats.GetOrZero(CharacterStat.Health);
            string[] roles = ["owner", "teammate", "selected viewer", "vicinity observer"];
            for (int i = 1; i < frames.Length; i++)
            {
                var updates = PublishedVitals(frames[i], subject).ToArray();
                Assert.AreEqual(1, updates.Count(update => update.Maximum.HasValue), $"{stage}, {roles[i]} maximum count");
                int? lastMaximum = updates.Select(update => update.Maximum).LastOrDefault(value => value.HasValue);
                int? lastCurrent = updates.Select(update => update.Current).LastOrDefault(value => value.HasValue);
                Assert.AreEqual(expectedMaximum, lastMaximum, $"{stage}, {roles[i]}");
                Assert.AreEqual(expectedCurrent, lastCurrent, $"{stage}, {roles[i]}");
            }
        }

        static IEnumerable<int> WireStats(IEnumerable<MessageBody> frames, Player subject, CharacterStat stat) =>
            frames.OfType<StatMessage>().Where(packet => packet.Identity == subject.Identity)
                .SelectMany(packet => packet.Stats).Where(pair => pair.Value1 == stat)
                .Select(pair => unchecked((int)pair.Value2));

        static IEnumerable<(int? Maximum, int? Current)> PublishedVitals(IEnumerable<MessageBody> frames, Player subject)
        {
            foreach (MessageBody frame in frames)
            {
                if (frame is TeamMemberInfoMessage team && team.Member == subject.Identity)
                    yield return (team.Unknown5, team.Unknown6);
                if (frame is HealthDamageMessage health && health.Identity == subject.Identity)
                    yield return (null, health.Unknown1);
                if (frame is not StatMessage stat || stat.Identity != subject.Identity)
                    continue;
                foreach (var pair in stat.Stats)
                {
                    if (pair.Value1 == CharacterStat.MaxHealth)
                        yield return (unchecked((int)pair.Value2), null);
                    if (pair.Value1 == CharacterStat.Health)
                        yield return (null, unchecked((int)pair.Value2));
                }
            }
        }

        static Task<MessageBody[][]> ReadBatch(WirePeer[] peers)
        {
            foreach (WirePeer peer in peers)
                peer.Session.Send(new StatMessage
                {
                    Identity = peer.Player.Identity,
                    Stats = [new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.Level, Value2 = TestBatchEnd }]
                });
            return Task.WhenAll(peers.Select(ReadUntilEnd));
        }

        static async Task<MessageBody[]> ReadUntilEnd(WirePeer peer)
        {
            var codec = new ZoneMessageCodec();
            var frames = new List<MessageBody>();
            for (int i = 0; i < 128; i++)
            {
                Message decoded = codec.Deserialize(await peer.ReadPacketAsync())!;
                Assert.AreEqual(peer.Player.Identity.Instance, decoded.Header.Receiver);
                if (decoded.Body is StatMessage stat && stat.Identity == peer.Player.Identity
                    && stat.Stats.Any(pair => pair.Value1 == CharacterStat.Level && pair.Value2 == TestBatchEnd))
                    return frames.ToArray();
                frames.Add(decoded.Body);
            }
            Assert.Fail("The test batch did not reach its final frame.");
            return [];
        }
    }
}
