namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Teams;

    [TestClass]
    public sealed class TeamVitalSynchronizationTests
    {
        [TestMethod]
        public void VisibleJoinPublishesFourAuthoritativeVitalsInTheOriginalWireOrder()
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            using var world = new TriggerTestWorld(teams: teams);
            Attach(teams, world.First);
            Attach(teams, world.Second);
            world.Locality.ActivatePlayerVisibility(world.First);
            world.Locality.ActivatePlayerVisibility(world.Second);

            Join(teams, world.First, world.Second);

            var sent = world.FirstSession.Sent;
            TeamMemberMessage row = sent.OfType<TeamMemberMessage>().Single(message => message.Member == world.Second.Identity);
            TeamMemberInfoMessage info = sent.OfType<TeamMemberInfoMessage>().Single();
            Assert.IsTrue(sent.IndexOf(row) < sent.IndexOf(info));
            Assert.IsTrue(sent.Skip(sent.IndexOf(info) + 1).OfType<StatMessage>()
                .Any(message => message.Stats.Any(stat => stat.Value1 == CharacterStat.SocialStatus)));
            AssertVitals(info, world.Second, 1839, 1843, 4846, 1072);
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            using var output = new MemoryStream();
            using var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(output);
            resolver.GetSerializer(typeof(TeamMemberInfoMessage)).Serialize(writer, new SerializationContext(resolver), info);
            CollectionAssert.AreEqual(Convert.FromHexString(
                "287842480000C35000000001000000C350000000020000072F00000733000012EE00000430"), output.ToArray());
        }

        [TestMethod]
        public void RetainedRosterReceivesVitalsOnlyAfterTheTeammateDynelEntersVisibility()
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            using var world = new TriggerTestWorld(teams: teams);
            Player viewer = world.First;
            Player member = world.Second;
            Player bystander = world.AddPlayer(3);
            var bystanderSession = (RecordingZoneSession)bystander.Session!;
            Attach(teams, viewer);
            Attach(teams, member);
            Attach(teams, bystander);
            Join(teams, viewer, member);
            Assert.IsFalse(world.FirstSession.Sent.OfType<TeamMemberInfoMessage>().Any());
            world.FirstSession.Sent.Clear();

            world.Locality.ActivatePlayerVisibility(viewer);
            world.Locality.ActivatePlayerVisibility(bystander);

            var sent = world.FirstSession.Sent;
            CharInPlayMessage visible = sent.OfType<CharInPlayMessage>().Single(message => message.Identity == member.Identity);
            TeamMemberInfoMessage info = sent.OfType<TeamMemberInfoMessage>().Single();
            Assert.IsTrue(sent.IndexOf(visible) < sent.IndexOf(info), "The remote character must exist before the vitals packet.");
            AssertVitals(info, member, 1839, 1843, 4846, 1072);
            Assert.IsFalse(sent.OfType<TeamMemberMessage>().Any());
            Assert.IsFalse(bystanderSession.Sent.OfType<TeamMemberInfoMessage>().Any());

            world.Locality.DeactivatePlayerVisibility(viewer);
            sent.Clear();
            member.Stats.Set(CharacterStat.MaxHealth, 6000);
            member.Stats.Set(CharacterStat.Health, 4200);
            teams.RefreshPlayer(viewer, preserveRoster: true);
            Assert.IsFalse(sent.OfType<TeamMemberInfoMessage>().Any());
            world.Locality.ActivatePlayerVisibility(viewer);
            world.Locality.ActivatePlayerVisibility(viewer);

            AssertVitals(sent.OfType<TeamMemberInfoMessage>().Single(), member, 1839, 1843, 6000, 4200);
            Assert.IsFalse(sent.OfType<TeamMemberMessage>().Any());
        }

        [TestMethod]
        public void RaidMovePublishesActualVitalsOnBothSidesOfTheExistingRowChange()
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            using var world = new TriggerTestWorld(teams: teams);
            Player leader = world.First;
            Player member = world.Second;
            Player other = world.AddPlayer(3);
            foreach (Player player in new[] { leader, member, other })
            {
                Attach(teams, player);
                world.Locality.ActivatePlayerVisibility(player);
            }
            Join(teams, leader, member);
            Join(teams, leader, other);
            Assert.IsTrue(teams.ConvertToRaid(leader));
            var sent = world.FirstSession.Sent;
            sent.Clear();
            var movedViewer = world.SecondSession.Sent;
            movedViewer.Clear();
            member.Stats.Set(CharacterStat.Health, 2098);

            Assert.IsTrue(teams.MoveRaidMember(leader, member.Identity.Instance, 1));

            TeamMemberMessage moved = sent.OfType<TeamMemberMessage>().Single();
            Assert.AreEqual(member.Identity, moved.Member);
            TeamMemberInfoMessage[] vitals = sent.OfType<TeamMemberInfoMessage>().ToArray();
            Assert.AreEqual(2, vitals.Length);
            Assert.IsTrue(sent.IndexOf(vitals[0]) < sent.IndexOf(moved) && sent.IndexOf(moved) < sent.LastIndexOf(vitals[1]));
            foreach (TeamMemberInfoMessage info in vitals)
                AssertVitals(info, member, 1839, 1843, 4846, 2098);

            foreach (Player peer in new[] { leader, other })
            {
                TeamMemberMessage peerRow = movedViewer.OfType<TeamMemberMessage>().Single(message => message.Member == peer.Identity);
                TeamMemberInfoMessage peerVitals = movedViewer.OfType<TeamMemberInfoMessage>().Single(message => message.Member == peer.Identity);
                Assert.IsTrue(movedViewer.IndexOf(peerVitals) < movedViewer.IndexOf(peerRow));
            }
        }

        [TestMethod]
        public void ChangedMaximumRefreshesVisibleTeammateOnceAfterBuffAndExpiry()
        {
            var teams = new TeamService(dispatchOnOwner: (_, action) => action());
            using var world = new TriggerTestWorld(teams: teams);
            ((StubGameData)world.Playfield.GetRequiredService<IGameData>()).AllowMissingXpLevels = true;
            Player member = world.Second;
            Attach(teams, world.First);
            Attach(teams, member);
            member.Stats.Set(CharacterStat.MaxNCU, 100);
            member.RebaseStats();
            int baseline = member.Stats.GetOrZero(CharacterStat.MaxHealth);
            member.Stats.Set(CharacterStat.Health, baseline);
            world.Locality.ActivatePlayerVisibility(world.First);
            world.Locality.ActivatePlayerVisibility(member);
            Join(teams, world.First, member);

            var sent = world.FirstSession.Sent;
            sent.Clear();
            DateTime start = DateTime.UtcNow;
            var buff = TestNanos.Create(9002, durationCentiseconds: 200,
                modifiers: [TestNanos.Modify(CharacterStat.MaxHealth, 200)]);
            Assert.AreEqual(BuffApplyDecision.Apply, member.TryApplyBuff(buff, member.Identity, start, out _, out _));
            member.RebaseStats();
            member.RebaseStats();
            TeamMemberInfoMessage boosted = sent.OfType<TeamMemberInfoMessage>().Single();
            Assert.AreEqual(baseline + 200, boosted.Unknown5);
            Assert.AreEqual(baseline + 200, boosted.Unknown6);
            Assert.IsFalse(sent.OfType<TeamMemberMessage>().Any());

            sent.Clear();
            member.Stats.Set(CharacterStat.Health, baseline + 150, dirty: true);
            member.FlushDirtyStats();
            Assert.IsFalse(sent.OfType<TeamMemberInfoMessage>().Any(), "Ordinary HP changes use the existing Stat packet.");
            NanoRuntime.Tick(member, start.AddSeconds(3));
            member.RebaseStats();
            TeamMemberInfoMessage expired = sent.OfType<TeamMemberInfoMessage>().Single();
            Assert.AreEqual(baseline, expired.Unknown5);
            Assert.AreEqual(member.Stats.GetOrZero(CharacterStat.Health), expired.Unknown6);
            Assert.IsFalse(sent.OfType<TeamMemberMessage>().Any());
        }

        private static void Attach(TeamService teams, Player player)
        {
            player.Name = "Member" + player.Identity.Instance;
            player.Stats.Set(CharacterStat.Level, 60);
            player.Stats.Set(CharacterStat.Profession, (int)Profession.Enforcer);
            player.Stats.Set(CharacterStat.MaxHealth, 4846);
            player.Stats.Set(CharacterStat.Health, 1072);
            player.Stats.Set(CharacterStat.MaxNanoEnergy, 1843);
            player.Stats.Set(CharacterStat.CurrentNano, 1839);
            teams.AttachPlayer(player);
        }

        private static void Join(TeamService teams, Player inviter, Player invitee)
        {
            teams.TryHandle(inviter, new CharacterActionMessage
            {
                Identity = inviter.Identity,
                Action = CharacterActionType.TeamRequestInvite,
                Target = invitee.Identity,
                Parameter2 = 1
            });
            teams.TryHandle(invitee, new CharacterActionMessage
            {
                Identity = invitee.Identity,
                Action = CharacterActionType.TeamRequestReply,
                Target = inviter.Identity,
                Parameter2 = 1
            });
            Assert.IsNotNull(teams.GetTeam(invitee));
        }

        private static void AssertVitals(TeamMemberInfoMessage info, Player member, int currentNano, int maxNano, int maxHp, int currentHp)
        {
            Assert.AreEqual(member.Identity, info.Member);
            Assert.AreEqual(currentNano, info.Unknown3);
            Assert.AreEqual(maxNano, info.Unknown4);
            Assert.AreEqual(maxHp, info.Unknown5);
            Assert.AreEqual(currentHp, info.Unknown6);
        }
    }
}
