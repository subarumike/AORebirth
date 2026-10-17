namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Text.Json.Nodes;
    using AORebirth.Communication.Messages;
    using AORebirth.Core.GameData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class TeamServiceTests
    {
        [TestMethod]
        public void InviteCreatesNoTeamUntilARealAcceptance()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Invite(a, b);
            Assert.IsNull(f.Teams.GetTeam(a));
            CharacterActionMessage invite = f.Session(b).Bodies.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(CharacterActionType.TeamRequestInvite, invite.Action);
            Assert.AreEqual(b.Identity, invite.Identity);
            Assert.AreEqual(a.Identity, invite.Target);
            Assert.AreEqual(0, invite.Parameter2);
            f.Accept(b, a);
            TeamSnapshot team = f.Teams.GetTeam(a)!;
            Assert.AreEqual(0x02800001, team.TeamId);
            Assert.AreEqual(1, team.LeaderId);
            CollectionAssert.AreEqual(new[] { 1, 2 }, team.MemberIds.ToArray());
            Assert.IsTrue(f.Teams.AreTeammates(a, b));
        }

        [TestMethod]
        public void JoinUsesLegacyRosterOrderAndExactActionFields()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Invite(a, b);
            f.Clear();
            f.Accept(b, a);
            CollectionAssert.AreEqual(new[]
            {
                "S:SocialStatus:7", "S:TeamSide:2", "S:SocialStatus:7", "A:TeamRequestReply",
                "S:SocialStatus:7", "M:1", "A:AcceptTeamRequest", "S:SocialStatus:7",
                "M:2", "V:2", "S:SocialStatus:7", "S:Team:41943041"
            }, f.Session(a).Bodies.Select(Describe).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "S:SocialStatus:5", "S:TeamSide:2", "S:SocialStatus:5", "M:2", "M:1",
                "V:1", "S:SocialStatus:5", "S:Team:41943041"
            }, f.Session(b).Bodies.Select(Describe).ToArray());
            CharacterActionMessage ack = f.Session(a).Bodies.OfType<CharacterActionMessage>().First();
            Assert.AreEqual(Identity.None, ack.Target);
            Assert.AreEqual(17, ack.Parameter2);
            CharacterActionMessage leader = f.Session(a).Bodies.OfType<CharacterActionMessage>().Last();
            Assert.AreEqual(a.Identity, leader.Target);
            Assert.AreEqual((int)IdentityType.TeamWindow, leader.Parameter1);
            Assert.AreEqual(f.Teams.GetTeam(a)!.TeamId, leader.Parameter2);
            foreach (TeamMemberMessage member in f.Session(a).Bodies.OfType<TeamMemberMessage>())
            {
                Assert.AreEqual(a.Identity, member.Identity);
                Assert.AreEqual(IdentityType.TeamWindow, member.Team.Type);
                Assert.AreEqual(-1, member.Unknown4);
                Assert.AreEqual(60, member.Level);
                Assert.AreEqual((short)3, member.Unknown5);
            }
        }

        [TestMethod]
        public void NoAndDeclineAreNotAcceptanceAndUseExistingThirtySecondCooldown()
        {
            foreach (int decline in new[] { 0, 20 })
            {
                var f = new Fixture();
                Player a = f.Player(1), b = f.Player(2);
                f.Invite(a, b);
                f.Clear();
                f.Action(b, CharacterActionType.ClientTeamInviteReply, a.Identity, decline);
                Assert.IsNull(f.Teams.GetTeam(b));
                CharacterActionMessage reply = f.Session(a).Bodies.OfType<CharacterActionMessage>().Single();
                Assert.AreEqual(b.Identity, reply.Target);
                Assert.AreEqual(20, reply.Parameter2);
                f.Clear();
                f.Invite(a, b);
                Assert.IsFalse(f.Session(b).Bodies.OfType<CharacterActionMessage>().Any());
                f.UtcNow = f.UtcNow.AddSeconds(30);
                f.Invite(a, b);
                f.Accept(b, a);
                Assert.IsTrue(f.Teams.AreTeammates(a, b));
            }
        }

        [TestMethod]
        public void ServerAcknowledgementsAndUnsolicitedAcceptCannotCreateMembership()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Accept(b, a);
            Assert.IsNull(f.Teams.GetTeam(b));
            f.Invite(a, b);
            f.Action(b, CharacterActionType.TeamRequestReply, a.Identity, 17);
            f.Action(b, CharacterActionType.AcceptTeamRequest, a.Identity, 1);
            Assert.IsNull(f.Teams.GetTeam(b));
            f.Accept(b, a);
            Assert.IsNotNull(f.Teams.GetTeam(b));
        }

        [TestMethod]
        public void PendingInviterCannotBeReplacedAndRequesterMustMatch()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Invite(a, b);
            f.Invite(c, b);
            f.Accept(b, c);
            Assert.IsNull(f.Teams.GetTeam(b));
            f.Accept(b, a);
            Assert.IsTrue(f.Teams.AreTeammates(a, b));
            Assert.IsNull(f.Teams.GetTeam(c));
        }

        [TestMethod]
        public void ReplayedAcceptDoesNotReannounceOrDuplicateRoster()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            f.Clear();
            Parallel.For(0, 12, _ => f.Accept(b, a));
            CollectionAssert.AreEqual(new[] { 1, 2 }, f.Teams.GetTeam(a)!.MemberIds.ToArray());
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
        }

        [TestMethod]
        public void LaterJoinAnnouncesOnlyTheJoinerToExistingMembers()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b);
            f.Invite(a, c);
            f.Clear();
            f.Accept(c, a);
            foreach (Player existing in new[] { a, b })
                CollectionAssert.AreEqual(new[] { 3 }, f.Session(existing).Bodies.OfType<TeamMemberMessage>()
                    .Select(m => m.Member.Instance).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, f.Session(c).Bodies.OfType<TeamMemberMessage>()
                .Select(m => m.Member.Instance).ToArray());
            Assert.AreEqual(3, a.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
        }

        [TestMethod]
        public void ConflictingMembershipNeverSilentlyLeavesOrMovesACharacter()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b);
            f.Join(c, d);
            int firstTeam = f.Teams.GetTeam(a)!.TeamId;
            int secondTeam = f.Teams.GetTeam(c)!.TeamId;
            f.Invite(c, b);
            f.Accept(b, c);
            Assert.AreEqual(firstTeam, f.Teams.GetTeam(b)!.TeamId);
            Assert.AreEqual(secondTeam, f.Teams.GetTeam(d)!.TeamId);
        }

        [TestMethod]
        public void SimultaneousInvitationsSurviveFirstTeamCreationAndMembersCanInvite()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Invite(a, b);
            f.Invite(a, c);
            f.Accept(b, a);
            f.Accept(c, a);
            f.Join(b, d);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, f.Teams.GetTeam(a)!.MemberIds.ToArray());
            Assert.AreEqual(1, f.Teams.GetTeam(d)!.LeaderId);
        }

        [TestMethod]
        public void ThirdAndFourthMembersOnlyAddNewRowsForEstablishedViewers()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b);
            f.Clear();

            f.Join(a, c);
            AssertRosterRows(f, a, 3);
            AssertRosterRows(f, b, 3);
            AssertRosterRows(f, c, 1, 2, 3);
            AssertAnnouncementsPrecedeVitals(f, a);
            AssertAnnouncementsPrecedeVitals(f, b);
            AssertInitialRosterOrder(f, c);
            Assert.IsFalse(f.AllBodies.OfType<CharacterActionMessage>()
                .Any(m => m.Action == CharacterActionType.TeamMemberLeft));
            Assert.IsFalse(f.Session(a).Bodies.OfType<CharacterActionMessage>()
                .Any(m => m.Action is CharacterActionType.TeamRequestReply or CharacterActionType.AcceptTeamRequest));

            f.Clear();
            f.Join(b, d);
            foreach (Player established in new[] { a, b, c })
            {
                AssertRosterRows(f, established, 4);
                AssertAnnouncementsPrecedeVitals(f, established);
            }
            AssertRosterRows(f, d, 1, 2, 3, 4);
            AssertInitialRosterOrder(f, d);
            Assert.AreEqual(4, a.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RapidInitialJoinsPublishEveryCurrentMemberOnce(bool reverseCallbacks)
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            pending.Deferred = true;

            f.Invite(a, b);
            f.Invite(a, c);
            f.Invite(a, d);
            f.Accept(b, a);
            f.Accept(c, a);
            f.Accept(d, a);
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any(), "Queued work is not delivered state.");
            pending.Drain(reverseCallbacks);

            foreach (Player viewer in new[] { a, b, c, d })
            {
                AssertRosterRows(f, viewer, 1, 2, 3, 4);
                AssertInitialRosterOrder(f, viewer);
                Assert.AreEqual(4, viewer.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ConcurrentQueuedAcceptsDoNotLoseOrRepeatAnEstablishedViewersDelta(bool reverseCallbacks)
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            a.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            b.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            f.Join(a, b);
            f.Invite(a, c);
            f.Invite(a, d);
            f.Clear();
            pending.Deferred = true;

            Parallel.Invoke(() => f.Accept(c, a), () => f.Accept(d, a));
            pending.Drain(reverseCallbacks);

            AssertRosterRows(f, a, 3, 4);
            AssertRosterRows(f, b, 3, 4);
            AssertRosterRows(f, c, 1, 2, 3, 4);
            AssertRosterRows(f, d, 1, 2, 3, 4);
            AssertAnnouncementsPrecedeVitals(f, a);
            AssertAnnouncementsPrecedeVitals(f, b);
            AssertInitialRosterOrder(f, c);
            AssertInitialRosterOrder(f, d);
            f.Clear();
            Parallel.Invoke(() => f.Accept(c, a), () => f.Accept(d, a));
            pending.Drain(reverseCallbacks);
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
        }

        [TestMethod]
        public void QueuedJoinThenLeaveDoesNotPublishAGhostMember()
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b);
            f.Clear();
            pending.Deferred = true;

            f.Join(a, c);
            f.Action(c, CharacterActionType.LeaveTeam);
            pending.Drain(reverse: true);

            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
            CollectionAssert.AreEqual(new[] { 1, 2 }, f.Teams.GetTeam(a)!.MemberIds.ToArray());
            Assert.IsNull(f.Teams.GetTeam(c));
            Assert.AreEqual(0, c.Stats.GetOrZero(CharacterStat.Team));
        }

        [TestMethod]
        public void PartialInitialPublicationCompletesMissingMemberAndTeamChatJoinOnce()
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2);
            pending.Deferred = true;
            f.Join(a, b);
            b.Stats.Set(CharacterStat.Level, 0);
            pending.Drain();
            AssertRosterRows(f, a, 1);

            b.Stats.Set(CharacterStat.Level, 60);
            f.Teams.RefreshPlayer(a, preserveRoster: true);
            pending.Drain();

            AssertRosterRows(f, a, 1, 2);
            string join = "#aorebirth-team-join " + f.Teams.GetTeam(a)!.TeamId;
            Assert.AreEqual(1, f.Chat.Commands.Count(c => c.CharacterId == a.Identity.Instance
                && c.ChatCommandString == join));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void QueuedLeaveAndRejoinRestoresChatWithoutRepeatingRetainedRows(bool raid)
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            if (raid)
            {
                Assert.IsTrue(f.Teams.ConvertToRaid(a));
            }
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            f.Chat.Commands.Clear();
            pending.Deferred = true;

            f.Action(c, CharacterActionType.LeaveTeam);
            f.Join(a, c);
            pending.Drain(reverse: true);

            foreach (Player viewer in new[] { a, b, c })
            {
                AssertRosterRows(f, viewer);
            }
            Assert.AreEqual(team, f.Teams.GetTeam(c)!.TeamId);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, f.Teams.GetTeam(c)!.MemberIds.ToArray());
            var expected = new List<string>
            {
                "#aorebirth-team-leave " + team, "#aorebirth-lft-remove", "#aorebirth-team-join " + team
            };
            if (raid)
            {
                expected.Add("#aorebirth-raid-convert " + team);
            }
            CollectionAssert.AreEqual(expected.ToArray(), f.Chat.Commands.Where(command => command.CharacterId == c.Identity.Instance)
                .Select(command => command.ChatCommandString).ToArray());
        }

        [TestMethod]
        public void QueuedRemovalsReachRemainingViewersWithoutReplayingTheirRows()
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b); f.Join(a, c); f.Join(a, d);
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            pending.Deferred = true;

            f.Action(c, CharacterActionType.LeaveTeam);
            f.Action(d, CharacterActionType.LeaveTeam);
            pending.Drain(reverse: true);

            foreach (Player viewer in new[] { a, b })
            {
                AssertRosterRows(f, viewer);
                AssertDepartures(f, viewer, team, 3, 4);
                Assert.AreEqual(2, viewer.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
            }
        }

        [TestMethod]
        public void LeadershipAndRaidChangesBeforeInitialDeliveryPreserveTheCurrentRoster()
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2);
            pending.Deferred = true;

            f.Join(a, b);
            f.Action(a, CharacterActionType.TransferLeader, b.Identity);
            Assert.IsTrue(f.Teams.ConvertToRaid(b));
            Assert.IsTrue(f.Teams.TryHandleRaidCmd(b, new RaidCmdMessage
            {
                Command = 4, TargetCharacterId = a.Identity.Instance, DestinationTeamIndex = 1
            }));
            pending.Drain(reverse: true);

            foreach (Player viewer in new[] { a, b })
            {
                AssertRosterRows(f, viewer, 1, 2);
                Assert.AreEqual(1, f.Session(viewer).Bodies.OfType<TeamMemberMessage>()
                    .Single(m => m.Member == a.Identity).Unknown4);
                Assert.AreEqual(1, f.Session(viewer).Bodies.OfType<RaidMessage>().Count());
            }
            Assert.AreEqual(b.Identity, f.Session(b).Bodies.OfType<CharacterActionMessage>()
                .Single(m => m.Action == CharacterActionType.AcceptTeamRequest).Target);
            Assert.AreEqual(13, a.Stats.GetOrZero(CharacterStat.SocialStatus));
            Assert.AreEqual(15, b.Stats.GetOrZero(CharacterStat.SocialStatus));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RepeatedQueuedRaidMovesOnlyReplaceTheMovedMemberAtItsFinalIndex(bool deferConversion)
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            if (!deferConversion)
            {
                Assert.IsTrue(f.Teams.ConvertToRaid(a));
            }
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            pending.Deferred = true;
            if (deferConversion)
            {
                Assert.IsTrue(f.Teams.ConvertToRaid(a));
            }

            foreach (int destination in new[] { 1, 2 })
            {
                Assert.IsTrue(f.Teams.TryHandleRaidCmd(a, new RaidCmdMessage
                {
                    Command = 4, TargetCharacterId = b.Identity.Instance, DestinationTeamIndex = destination
                }));
            }
            pending.Drain(reverse: true);

            foreach (Player viewer in new[] { a, b, c })
            {
                AssertRosterRows(f, viewer, 2);
                MessageBody[] rows = f.Session(viewer).Bodies.Where(m => m is TeamMemberMessage
                    || m is CharacterActionMessage { Action: CharacterActionType.TeamMemberLeft }).ToArray();
                Assert.AreEqual(2, rows.Length);
                var left = (CharacterActionMessage)rows[0];
                Assert.AreEqual(b.Identity, left.Target);
                Assert.AreEqual(team, left.Parameter1);
                Assert.AreEqual(0, left.Parameter2);
                Assert.AreEqual(2, ((TeamMemberMessage)rows[1]).Unknown4);
                if (deferConversion)
                {
                    RaidMessage converted = f.Session(viewer).Bodies.OfType<RaidMessage>().Single();
                    Assert.IsTrue(f.Session(viewer).Bodies.IndexOf(converted) < f.Session(viewer).Bodies.IndexOf(left),
                        "Raid conversion must precede the moved member's raid-slot update.");
                }
            }
        }

        [TestMethod]
        public void ZoningDuringAQueuedAdditionReconcilesOnlyTheUndeliveredMember()
        {
            var pending = new ProjectionQueue();
            var f = new Fixture(pending.Dispatch);
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b);
            f.Clear();
            pending.Deferred = true;
            f.Join(a, c);
            TeamSession old = f.BeginZoning(b);
            pending.Drain();
            Assert.IsFalse(old.Bodies.OfType<TeamMemberMessage>().Any());

            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);
            pending.Drain();
            AssertRosterRows(f, b, 3);
            f.Clear();
            f.Teams.RefreshPlayer(b, preserveRoster: true);
            pending.Drain();
            AssertRosterRows(f, b);
        }

        [TestMethod]
        public void ZoningReconcilesAddedAndRemovedMembersWithoutReplayingRetainedRows()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b); f.Join(a, c);
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            f.BeginZoning(b);
            f.Join(a, d);
            f.Action(c, CharacterActionType.LeaveTeam);

            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);

            AssertRosterRows(f, b, 4);
            AssertDepartures(f, b, team, 3);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, f.Teams.GetTeam(b)!.MemberIds.ToArray());
            Assert.AreEqual(3, b.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
        }

        [TestMethod]
        public void ZoningAcrossDisbandPublishesRetainedMemberRemovals()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            f.BeginZoning(b);
            f.Action(a, CharacterActionType.LeaveTeam);

            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);

            Assert.IsNull(f.Teams.GetTeam(b));
            AssertRosterRows(f, b);
            Assert.IsTrue(f.Session(b).Bodies.OfType<CharacterActionMessage>().Any(m =>
                m.Action == CharacterActionType.TeamMemberLeft && m.Target == b.Identity
                && m.Parameter1 == team && m.Parameter2 == -1));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
            Assert.AreEqual(4, b.Stats.GetOrZero(CharacterStat.SocialStatus));
        }

        [TestMethod]
        public void KickedZoningViewerReceivesSelfDepartureWithoutReplayingRows()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            TeamSession old = f.BeginZoning(b);
            f.Action(a, CharacterActionType.TeamKickMember, b.Identity);
            Assert.AreEqual(0, old.Bodies.Count);

            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);

            Assert.IsNull(f.Teams.GetTeam(b));
            CollectionAssert.AreEqual(new[] { 1, 3 }, f.Teams.GetTeam(a)!.MemberIds.ToArray());
            AssertRosterRows(f, b);
            CharacterActionMessage left = f.Session(b).Bodies.OfType<CharacterActionMessage>()
                .Single(m => m.Action == CharacterActionType.TeamMemberLeft && m.Target == b.Identity);
            Assert.AreEqual(team, left.Parameter1);
            Assert.AreEqual(-1, left.Parameter2);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void InactiveOrDisconnectedRemovalSilentlyResetsLocalTeamStats(bool disconnected)
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            Assert.AreEqual(2, b.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
            Assert.AreNotEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
            f.Clear();
            TeamSession inactive = f.Session(b);
            if (disconnected)
            {
                inactive.Close();
                f.Teams.OnTransportDisconnected(b, inactive);
                // A LinkDead member keeps its place; the LinkDead despawn takes it out.
                Assert.IsNotNull(f.Teams.GetTeam(b));
                f.Teams.DetachPlayer(b);
            }
            else
            {
                f.BeginZoning(b);
                f.Action(a, CharacterActionType.LeaveTeam);
            }

            Assert.IsNull(f.Teams.GetTeam(b));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.TeamSide));
            Assert.AreEqual(4, b.Stats.GetOrZero(CharacterStat.SocialStatus));
            Assert.AreEqual(0, inactive.Bodies.Count, "An inactive recipient must not receive UI packets.");
        }

        [TestMethod]
        public void ZoningReconcilesLeadershipAndRaidStateWithoutReplayingMembers()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            f.Clear();
            f.BeginZoning(b);
            f.Action(a, CharacterActionType.TransferLeader, c.Identity);
            Assert.IsTrue(f.Teams.ConvertToRaid(c));

            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);

            AssertRosterRows(f, b);
            Assert.AreEqual(c.Identity, f.Session(b).Bodies.OfType<CharacterActionMessage>()
                .Single(m => m.Action == CharacterActionType.AcceptTeamRequest).Target);
            Assert.AreEqual(1, f.Session(b).Bodies.OfType<RaidMessage>().Count());
            Assert.AreEqual(13, b.Stats.GetOrZero(CharacterStat.SocialStatus));
        }

        [TestMethod]
        public void FreshReconnectStillReceivesTheCompleteRoster()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            f.ReplaceSession(b);

            f.Teams.RefreshPlayer(b);

            AssertRosterRows(f, b, 1, 2, 3);
            AssertInitialRosterOrder(f, b);
            Assert.AreEqual(f.Teams.GetTeam(a)!.TeamId, b.Stats.GetOrZero(CharacterStat.Team));
        }

        [TestMethod]
        public void FreshReconnectAfterMissedChangesReceivesOnlyTheFinalCompleteRoster()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b); f.Join(a, c);
            f.BeginZoning(b);
            f.Join(a, d);
            f.Action(c, CharacterActionType.LeaveTeam);
            f.ReplaceSession(b);

            f.Teams.RefreshPlayer(b);

            AssertRosterRows(f, b, 1, 2, 4);
            AssertInitialRosterOrder(f, b);
            Assert.IsFalse(f.Session(b).Bodies.OfType<CharacterActionMessage>()
                .Any(m => m.Action == CharacterActionType.TeamMemberLeft));
            Assert.AreEqual(3, b.Stats.GetOrZero(CharacterStat.NumberOfTeamMembers));
        }

        [TestMethod]
        public void LeavePublishesRemovalNotAnotherRosterAndDissolvesLastMember()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            int teamId = f.Teams.GetTeam(a)!.TeamId;
            f.Clear();
            f.Action(b, CharacterActionType.LeaveTeam);
            Assert.IsNull(f.Teams.GetTeam(a));
            Assert.IsNull(f.Teams.GetTeam(b));
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
            CharacterActionMessage[] left = f.Session(a).Bodies.OfType<CharacterActionMessage>().ToArray();
            CollectionAssert.AreEqual(new[] { 2, 1 }, left.Select(m => m.Target.Instance).ToArray());
            Assert.IsTrue(left.All(m => m.Action == CharacterActionType.TeamMemberLeft
                && m.Parameter1 == teamId && m.Parameter2 == -1));
            Assert.AreEqual(0, a.Stats.GetOrZero(CharacterStat.Team));
            Assert.AreEqual(4, b.Stats.GetOrZero(CharacterStat.SocialStatus));
        }

        [TestMethod]
        public void OnlyLeaderCanKickAndLeavingLeaderPromotesOldestRemainingMember()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3), d = f.Player(4);
            f.Join(a, b); f.Join(a, c); f.Join(a, d);
            f.Action(b, CharacterActionType.TeamKickMember, c.Identity);
            Assert.IsNotNull(f.Teams.GetTeam(c));
            f.Action(a, CharacterActionType.TeamKickMember, d.Identity);
            Assert.IsNull(f.Teams.GetTeam(d));
            f.Clear();
            f.Action(a, CharacterActionType.LeaveTeam);
            Assert.AreEqual(2, f.Teams.GetTeam(b)!.LeaderId);
            CollectionAssert.AreEqual(new[] { 2, 3 }, f.Teams.GetTeam(c)!.MemberIds.ToArray());
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
        }

        [TestMethod]
        public void TransferPreservesRosterOrderAndCapturedSocialValuesOnRefresh()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Join(a, c);
            f.Action(b, CharacterActionType.TransferLeader, c.Identity);
            Assert.AreEqual(1, f.Teams.GetTeam(a)!.LeaderId);
            f.Clear();
            f.Action(a, CharacterActionType.TransferLeader, c.Identity);
            Assert.AreEqual(3, f.Teams.GetTeam(a)!.LeaderId);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, f.Teams.GetTeam(a)!.MemberIds.ToArray());
            Assert.AreEqual(13, a.Stats.GetOrZero(CharacterStat.SocialStatus));
            Assert.AreEqual(15, c.Stats.GetOrZero(CharacterStat.SocialStatus));
            Assert.IsTrue(f.AllBodies.OfType<CharacterActionMessage>().All(m => m.Target == c.Identity));
            f.Teams.RefreshPlayer(c);
            Assert.AreEqual(15, c.Stats.GetOrZero(CharacterStat.SocialStatus));
        }

        [TestMethod]
        public void LinkDeadKeepsMembershipReconnectResendsRosterAndDespawnCannotRestorePersistedTeamStat()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            int team = f.Teams.GetTeam(a)!.TeamId;
            f.Teams.OnTransportDisconnected(b, f.Session(b));
            Assert.AreEqual(team, f.Teams.GetTeam(a)!.TeamId);
            f.ReplaceSession(b);
            f.Chat.Commands.Clear();
            f.Teams.AttachPlayer(b);
            f.Teams.RefreshPlayer(b);
            Assert.IsTrue(f.Teams.AreTeammates(a, b));
            Assert.AreEqual(team, b.Stats.GetOrZero(CharacterStat.Team));
            CollectionAssert.AreEqual(new[] { 2, 1 }, f.Session(b).Bodies.OfType<TeamMemberMessage>()
                .Select(m => m.Member.Instance).ToArray());
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString == "#aorebirth-team-join " + team));
            f.Chat.Commands.Clear();
            Assert.IsTrue(f.Teams.TryHandleInboundChatCommand(2, "#aorebirth-chat-online"));
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString == "#aorebirth-team-join " + team));
            f.Teams.DetachPlayer(b);
            Assert.IsNull(f.Teams.GetTeam(a));
            f.Chat.Commands.Clear();
            Assert.IsTrue(f.Teams.TryHandleInboundChatCommand(2, "#aorebirth-chat-online"));
            Assert.IsFalse(f.Chat.Commands.Any());
            Player reloaded = f.Player(2, oldTeamStat: 0x02809999);
            Assert.IsNull(f.Teams.GetTeam(reloaded));
            Assert.AreEqual(0, reloaded.Stats.GetOrZero(CharacterStat.Team));
        }

        [TestMethod]
        public void SameActorZoningPreservesIdentityAndOldSessionCallbackCannotRemoveTeam()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            int team = f.Teams.GetTeam(a)!.TeamId;
            TeamSession old = f.Session(b);
            b.Session = null; // The established ZoneSession transfer clears this before old transport closes.
            old.UnbindPlayer();
            b.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            f.Teams.OnTransportDisconnected(b, old);
            f.ReplaceSession(b);
            f.Teams.RefreshPlayer(b, preserveRoster: true);
            f.Teams.OnTransportDisconnected(b, old);
            Assert.AreEqual(team, f.Teams.GetTeam(b)!.TeamId);
            AssertRosterRows(f, b);
        }

        [TestMethod]
        public void ReplacedActorCannotMutateNewOwnersMembership()
        {
            var f = new Fixture();
            Player a = f.Player(1), old = f.Player(2), c = f.Player(3);
            f.Join(a, old);
            Player current = f.Player(2);
            f.Join(c, current);
            f.Teams.DetachPlayer(old);
            f.Teams.OnTransportDisconnected(old, (TeamSession)old.Session!);
            f.Action(old, CharacterActionType.LeaveTeam);
            Assert.IsTrue(f.Teams.AreTeammates(c, current));
            Assert.IsNull(f.Teams.GetTeam(old));
            Assert.IsNull(f.Teams.GetTeam(a));
        }

        [TestMethod]
        public void StaleInvitationDiesWithInviterDisconnectOrSessionReplacement()
        {
            foreach (bool replaceSession in new[] { false, true })
            {
                var f = new Fixture();
                Player a = f.Player(1), b = f.Player(2);
                f.Invite(a, b);
                if (replaceSession) f.ReplaceSession(a);
                else f.Teams.OnTransportDisconnected(a, f.Session(a));
                f.Accept(b, a);
                Assert.IsNull(f.Teams.GetTeam(b));
            }
        }

        [TestMethod]
        public void MalformedIdentityParameterAndQuarantinedActorFailClosed()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Action(a, CharacterActionType.TeamRequestInvite, new Identity { Type = IdentityType.TeamWindow, Instance = 2 });
            f.Teams.TryHandle(a, new CharacterActionMessage { Identity = b.Identity,
                Action = CharacterActionType.TeamRequestInvite, Target = b.Identity });
            Assert.IsFalse(f.Session(b).Bodies.OfType<CharacterActionMessage>().Any());
            f.Invite(a, b);
            f.Action(b, CharacterActionType.ClientTeamInviteReply, a.Identity, -1);
            Assert.IsNull(f.Teams.GetTeam(b));
            b.QuarantinePersistence();
            f.Accept(b, a);
            Assert.IsNull(f.Teams.GetTeam(b));
        }

        [TestMethod]
        public void LevelWarningsUseSharedTableAndOnlyConfirmationDeliversInvite()
        {
            foreach (bool tooHigh in new[] { true, false })
            {
                var f = new Fixture();
                Player a = f.Player(1, tooHigh ? 64 : 200), b = f.Player(2, tooHigh ? 200 : 64);
                f.Invite(a, b);
                CharacterActionMessage warn = f.Session(a).Bodies.OfType<CharacterActionMessage>().Single();
                Assert.AreEqual(tooHigh ? CharacterActionType.TeamInviteAck : CharacterActionType.TeamInviteTooLow, warn.Action);
                Assert.AreEqual(b.Identity, warn.Target);
                ChatTextMessage text = f.Session(a).Bodies.OfType<ChatTextMessage>().Single();
                Assert.AreEqual(
                    tooHigh
                        ? "Member2 is too high for Member1."
                        : "Member2 is too low for Member1.",
                    text.Text);
                Assert.AreEqual(0, f.Session(b).Bodies.Count);
                f.Invite(a, b, confirmed: true);
                f.Accept(b, a);
                Assert.IsTrue(f.Teams.AreTeammates(a, b));
            }
        }

        [TestMethod]
        public void CrossPlayfieldInviteAndRaidReachEveryOwnerThroughChatBridge()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            a.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            b.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            f.Invite(a, b);
            TeamInviteMessage invite = f.Session(b).Bodies.OfType<TeamInviteMessage>().Single();
            Assert.AreEqual(a.Identity, invite.Inviter);
            Assert.AreEqual("Member1", invite.Name);
            Assert.AreEqual(1, invite.Unknown);
            f.Accept(b, a);
            Assert.IsFalse(f.Teams.ConvertToRaid(b));
            AssertFeedback(f.Session(b).Bodies.OfType<FeedbackMessage>().Single(), "Feedback_NeedToBeTeamLeader");
            f.Clear();
            Assert.IsTrue(f.Teams.ConvertToRaid(a));
            Assert.IsTrue(f.Teams.GetTeam(b)!.IsRaid);
            Assert.AreEqual(2, f.AllBodies.OfType<RaidMessage>().Count());
            AssertFeedback(f.Session(a).Bodies.OfType<FeedbackMessage>().Single(), "Feedback_RaidCreated");
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 1 && c.ChatCommandString.StartsWith("#aorebirth-raid-convert ", StringComparison.Ordinal)));
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString.StartsWith("#aorebirth-raid-convert ", StringComparison.Ordinal)));
            Assert.IsFalse(f.Teams.ConvertToRaid(a));
            Assert.AreEqual(
                "Your team is already a raid.",
                f.Session(a).Bodies.OfType<ChatTextMessage>().Last().Text);
        }

        [TestMethod]
        public void RaidLeaderMoveMemberPublishesTeamMemberLeftThenTeamMemberWithRaidIndex()
        {
            // Capture 20260924-213512: RaidCmd Command=4 Target=Zizion Dest=1 →
            // TeamMemberInfo, SocialStatus, TeamMemberLeft(param2=0), TeamMember(Unknown4=1), ...
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            Assert.IsTrue(f.Teams.ConvertToRaid(a));
            f.Clear();
            Assert.IsFalse(f.Teams.TryHandleRaidCmd(b, new RaidCmdMessage
            {
                Command = 4,
                TargetCharacterId = a.Identity.Instance,
                DestinationTeamIndex = 1
            }));
            AssertFeedback(f.Session(b).Bodies.OfType<FeedbackMessage>().Single(), "Feedback_NeedToBeTeamLeader");
            f.Clear();
            Assert.IsTrue(f.Teams.TryHandleRaidCmd(a, new RaidCmdMessage
            {
                Command = 4,
                TargetCharacterId = b.Identity.Instance,
                DestinationTeamIndex = 1
            }));
            CharacterActionMessage left = f.Session(a).Bodies.OfType<CharacterActionMessage>()
                .Single(m => m.Action == CharacterActionType.TeamMemberLeft);
            Assert.AreEqual(b.Identity, left.Target);
            Assert.AreEqual(f.Teams.GetTeam(a)!.TeamId, left.Parameter1);
            Assert.AreEqual(0, left.Parameter2);
            TeamMemberMessage moved = f.Session(a).Bodies.OfType<TeamMemberMessage>()
                .Single(m => m.Member.Instance == b.Identity.Instance);
            Assert.AreEqual(1, moved.Unknown4);
            TeamMemberMessage peerView = f.Session(b).Bodies.OfType<TeamMemberMessage>()
                .Single(m => m.Member.Instance == b.Identity.Instance);
            Assert.AreEqual(1, peerView.Unknown4);
        }

        [TestMethod]
        public void ShutdownClearsMembershipInvitationsRaidAndBothChatMemberships()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2), c = f.Player(3);
            f.Join(a, b); f.Teams.ConvertToRaid(a); f.Invite(a, c);
            f.Chat.Commands.Clear();
            f.Teams.Shutdown(); f.Teams.Shutdown();
            Assert.IsNull(f.Teams.GetTeam(a));
            Assert.IsNull(f.Teams.GetTeam(b));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
            Assert.AreEqual(2, f.Chat.Commands.Count(c => c.ChatCommandString.StartsWith("#aorebirth-team-leave ", StringComparison.Ordinal)));
            f.Accept(c, a);
            Assert.IsNull(f.Teams.GetTeam(c));
        }

        [TestMethod]
        public void UnsupportedVitalsAreNotInventedAndStatObserverRefreshesRealValues()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            b.Stats.Set(CharacterStat.MaxNanoEnergy, (int)CharacterStat.Unset);
            f.Join(a, b);
            Assert.IsFalse(f.Session(a).Bodies.OfType<TeamMemberInfoMessage>().Any());
            b.Stats.Set(CharacterStat.MaxNanoEnergy, 123);
            f.Clear(); f.Teams.RefreshPlayer(a);
            TeamMemberInfoMessage vitals = f.Session(a).Bodies.OfType<TeamMemberInfoMessage>().Single();
            Assert.AreEqual(123, vitals.Unknown5);
            Assert.AreEqual(123, vitals.Unknown6);
        }

        [TestMethod]
        public void DeferredProjectionCannotSendToReplacementSessionOrActor()
        {
            var pending = new Queue<Action>();
            var f = new Fixture((_, action) => pending.Enqueue(action));
            Player a = f.Player(1), b = f.Player(2);
            while (pending.TryDequeue(out Action? action)) action();
            f.Join(a, b);
            f.ReplaceSession(b);
            Player replacement = f.Player(1);
            while (pending.TryDequeue(out Action? action)) action();
            Assert.IsFalse(f.Session(b).Bodies.OfType<TeamMemberMessage>().Any());
            Assert.IsFalse(f.Session(replacement).Bodies.OfType<TeamMemberMessage>().Any());
            Assert.IsNull(f.Teams.GetTeam(replacement));
        }

        [TestMethod]
        public void NewerInlineRemovalInvalidatesAnOlderQueuedJoinProjection()
        {
            var pending = new Queue<Action>();
            bool deferred = true;
            var f = new Fixture((_, action) => { if (deferred) pending.Enqueue(action); else action(); });
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            deferred = false;
            f.Action(b, CharacterActionType.LeaveTeam);
            f.Clear();
            while (pending.TryDequeue(out Action? action)) action();
            Assert.IsFalse(f.AllBodies.OfType<TeamMemberMessage>().Any());
            Assert.AreEqual(0, a.Stats.GetOrZero(CharacterStat.Team));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
        }

        [TestMethod]
        public void TeamChatCommandsUseTheSameAuthorityAndNoInvitationTimeoutIsInvented()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            Assert.IsTrue(f.Teams.TryHandleChatCommand(a, ["team", "invite", "Member2"]));
            f.UtcNow = f.UtcNow.AddDays(2); // Legacy has cancellation, not an invitation-expiry duration.
            Assert.IsTrue(f.Teams.TryHandleChatCommand(b, ["team", "accept"]));
            int team = f.Teams.GetTeam(a)!.TeamId;
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString == "#aorebirth-lft-remove"));
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 1 && c.ChatCommandString == "#aorebirth-team-join " + team));
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString == "#aorebirth-team-join " + team));
            Assert.IsTrue(f.Teams.TryHandleChatCommand(b, ["team", "leave"]));
            Assert.IsNull(f.Teams.GetTeam(a));
            Assert.IsNull(f.Teams.GetTeam(b));
        }

        [TestMethod]
        public void ArrivalsTellChatEngineTheLivePlayfieldForLft()
        {
            var f = new Fixture();
            Player a = f.Player(1);
            Assert.IsFalse(f.Chat.Commands.Any(c => c.ChatCommandString.StartsWith("#aorebirth-pf", StringComparison.Ordinal)));

            a.Playfield = LftPlayfield(4310, shadowlands: true);
            f.Teams.AttachPlayer(a);
            f.Teams.AttachPlayer(a); // the login path attaches twice; one push per playfield
            a.Playfield = LftPlayfield(551, shadowlands: false);
            f.Teams.AttachPlayer(a);

            CollectionAssert.AreEqual(
                new[] { "#aorebirth-pf 4310 2", "#aorebirth-pf 551 1" },
                f.Chat.Commands.Where(c => c.CharacterId == 1).Select(c => c.ChatCommandString).ToArray());
        }

        [TestMethod]
        public void LftSeedRepeatsTheSearcherAndCandidateLocations()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            a.Playfield = LftPlayfield(551, shadowlands: false);
            b.Playfield = LftPlayfield(4310, shadowlands: true);
            f.Teams.AttachPlayer(a);
            f.Teams.AttachPlayer(b);
            f.Chat.Commands.Clear();

            Assert.IsTrue(f.Teams.TryHandleInboundChatCommand(1, "#aorebirth-lft-seed 2:Member2 3:Offline"));

            CollectionAssert.AreEquivalent(
                new[] { "1:#aorebirth-pf 551 1", "2:#aorebirth-pf 4310 2" },
                f.Chat.Commands.Select(c => c.CharacterId + ":" + c.ChatCommandString).ToArray());
        }

        private static Playfield LftPlayfield(int id, bool shadowlands)
        {
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new Identity { Type = IdentityType.Playfield, Instance = id });
            typeof(Playfield).GetField("<MetaData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new PlayfieldMetaData
                    { UnknownV9A = shadowlands ? PlayfieldEntryFlags.ShadowlandsMetaDataBit : 0 });
            return playfield;
        }

        [TestMethod]
        public void EveryExistingMemberParticipatesInTheXpRangeWarning()
        {
            var f = new Fixture();
            Player a = f.Player(1, 60), b = f.Player(2, 60), c = f.Player(3, 60);
            f.Join(a, b);
            b.Stats.Set(CharacterStat.Level, 200); // Updated only by b's owner-side stat event.
            f.Clear();
            f.Invite(a, c);
            CharacterActionMessage warning = f.Session(a).Bodies.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(CharacterActionType.TeamInviteTooLow, warning.Action);
            Assert.IsFalse(f.Session(c).Bodies.OfType<CharacterActionMessage>().Any());
        }

        [TestMethod]
        public void N3ChatCmdUsesExistingNormalizationAndRejectsOldSessionOrForgedOwner()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            var stuck = new StuckService(new GameplaySettings(), new Lazy<PlayfieldManager>(() =>
                throw new InvalidOperationException("Team command routing must not resolve stuck-service playfields.")));
            var handler = new TeamChatMessageHandler(f.Teams, stuck);
            var invite = new ChatCmdMessage { Identity = a.Identity, Command = "/command invite  Member2\0" };
            handler.Handle(invite, f.Session(a));
            handler.Handle(new ChatCmdMessage { Identity = b.Identity, Command = ".team accept" }, f.Session(b));
            Assert.IsTrue(f.Teams.AreTeammates(a, b));
            TeamSession old = f.Session(b);
            f.ReplaceSession(b);
            handler.Handle(new ChatCmdMessage { Identity = b.Identity, Command = "/team leave" }, old);
            handler.Handle(new ChatCmdMessage { Identity = a.Identity, Command = "/team leave" }, f.Session(b));
            Assert.IsTrue(f.Teams.AreTeammates(a, b));
            handler.Handle(new ChatCmdMessage { Identity = b.Identity, Command = "/team leave" }, f.Session(b));
            Assert.IsNull(f.Teams.GetTeam(b));
        }

        [TestMethod]
        public void CurrentNewCodecRoundTripsAllTeamPacketsWithHeaderAndIdentityPreserved()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            b.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            f.Join(a, b); f.Teams.ConvertToRaid(a); f.Action(a, CharacterActionType.TransferLeader, b.Identity);
            f.Action(b, CharacterActionType.LeaveTeam);
            var codec = new ZoneMessageCodec();
            foreach (MessageBody body in f.AllBodies)
            {
                byte[] bytes = codec.Serialize(body, 4582, 2);
                Message decoded = codec.Deserialize(bytes)!;
                Assert.AreEqual(body.GetType(), decoded.Body.GetType());
                Assert.AreEqual(4582, decoded.Header.Sender);
                Assert.AreEqual(2, decoded.Header.Receiver);
                CollectionAssert.AreEqual(bytes, codec.Serialize(decoded));
            }
        }

        [TestMethod]
        public void TeamBodiesMatchExistingContractFieldWidthsEndiannessAndConstants()
        {
            // Contract-derived fixtures for these explicit test actors, not mislabeled live captures.
            // N3 message IDs/fields come from the current shared AOtomation models and Legacy senders.
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            b.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            f.Join(a, b);
            AssertWire("4D2A313B0000C35000000002010000C3500000000100074D656D62657231",
                f.Session(b).Bodies.OfType<TeamInviteMessage>().Single());
            AssertWire("46312D2E0000C35000000001000000C350000000020000DEA902800001FFFFFFFF0000003C0003000000074D656D62657232",
                f.Session(a).Bodies.OfType<TeamMemberMessage>().Single(m => m.Member.Instance == 2));
            AssertWire("287842480000C35000000001000000C35000000002000001C2000001C2000000C8000000C8",
                f.Session(a).Bodies.OfType<TeamMemberInfoMessage>().Single());
            f.Action(b, CharacterActionType.LeaveTeam);
            AssertWire("5E4777700000C350000000010000000020000000000000C3500000000202800001FFFFFFFF0000",
                f.Session(a).Bodies.OfType<CharacterActionMessage>().First(m => m.Action == CharacterActionType.TeamMemberLeft));
        }

        [DataTestMethod]
        [DataRow(null, "")]
        [DataRow("", "")]
        [DataRow(" \t\r\n ", "")]
        [DataRow("\0\0", "")]
        [DataRow("/command invite  Member2\0", "invite|Member2")]
        [DataRow("...///TeAm   AcCePt", "TeAm|AcCePt")]
        [DataRow(".team  invite   Member2", "team|invite|Member2")]
        [DataRow("/.team accept", ".team|accept")]
        [DataRow(" /team leave", "/team|leave")]
        [DataRow("/team\taccept", "team\taccept")]
        [DataRow("/command\tinvite Member2", "command\tinvite|Member2")]
        [DataRow("command command invite", "command|invite")]
        [DataRow("\0 ", "\0")]
        public void CommandTokenizationPreservesExistingPrefixWhitespaceAndTerminatorOrder(string? input, string expected)
            => Assert.AreEqual(expected, string.Join("|", CommandInput.Tokenize(input)));

        [TestMethod]
        public void TeamLevelDataReloadUsesEditedRangesWithoutChangingAnExistingSnapshot()
        {
            string path = Path.GetTempFileName();
            try
            {
                string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "Teams", "LevelEligibility.json"));
                File.WriteAllText(path, source);
                var original = TeamLevelEligibility.Load(path);
                var document = JsonNode.Parse(source)!;
                var row = document["Ranges"]!.AsArray().Single(value => value!["Level"]!.GetValue<int>() == 60)!;
                row["Minimum"] = 43;
                row["Maximum"] = 85;
                File.WriteAllText(path, document.ToJsonString());
                var edited = TeamLevelEligibility.Load(path);
                Assert.AreEqual(42, original.ForLevel(60).Minimum);
                Assert.AreEqual(84, original.ForLevel(60).Maximum);
                Assert.AreEqual(43, edited.ForLevel(60).Minimum);
                Assert.AreEqual(85, edited.ForLevel(60).Maximum);
            }
            finally { File.Delete(path); }
        }

        [DataTestMethod]
        [DataRow("missing")]
        [DataRow("duplicate")]
        [DataRow("inverted")]
        [DataRow("outside")]
        public void MalformedTeamLevelDataCannotPublishUsableRanges(string corruption)
        {
            string path = Path.GetTempFileName();
            try
            {
                var document = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "Teams", "LevelEligibility.json")))!;
                var rows = document["Ranges"]!.AsArray();
                switch (corruption)
                {
                    case "missing": rows.RemoveAt(59); break;
                    case "duplicate": rows[59]!["Level"] = rows[58]!["Level"]!.GetValue<int>(); break;
                    case "inverted": rows[59]!["Minimum"] = rows[59]!["Maximum"]!.GetValue<int>() + 1; break;
                    case "outside": rows[59]!["Maximum"] = document["MaximumLevel"]!.GetValue<int>() + 1; break;
                }
                File.WriteAllText(path, document.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => TeamLevelEligibility.Load(path));
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void TeamLevelLookupClampsOutsideLevelsToTheExistingEndpoints()
        {
            var ranges = TeamLevelEligibility.Current;
            Assert.AreEqual(1, ranges.ForLevel(int.MinValue).Level);
            Assert.AreEqual(ranges.ForLevel(1).Minimum, ranges.ForLevel(0).Minimum);
            Assert.AreEqual(ranges.ForLevel(1).Maximum, ranges.ForLevel(0).Maximum);
            Assert.AreEqual(220, ranges.ForLevel(int.MaxValue).Level);
            Assert.AreEqual(ranges.ForLevel(220).Minimum, ranges.ForLevel(221).Minimum);
            Assert.AreEqual(ranges.ForLevel(220).Maximum, ranges.ForLevel(221).Maximum);
        }

        [DataTestMethod]
        [DataRow(42, true)]
        [DataRow(84, true)]
        [DataRow(41, false)]
        [DataRow(85, false)]
        public void TeamInviteUsesInclusiveRangeBoundaries(int candidateLevel, bool eligible)
        {
            var f = new Fixture();
            Player inviter = f.Player(1, 60), candidate = f.Player(2, candidateLevel);
            f.Invite(inviter, candidate);
            Assert.AreEqual(eligible, f.Session(candidate).Bodies.OfType<CharacterActionMessage>()
                .Any(message => message.Action == CharacterActionType.TeamRequestInvite));
            if (eligible)
            {
                f.Accept(candidate, inviter);
                Assert.IsTrue(f.Teams.AreTeammates(inviter, candidate));
            }
            else
            {
                Assert.IsNull(f.Teams.GetTeam(inviter));
                Assert.IsNull(f.Teams.GetTeam(candidate));
            }
        }

        private static void AssertWire(string hex, MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());
            using var stream = new MemoryStream();
            using var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(stream);
            serializer.Serialize(writer, new SerializationContext(resolver), body);
            CollectionAssert.AreEqual(Convert.FromHexString(hex), stream.ToArray(), body.GetType().Name);
            using var input = new MemoryStream(Convert.FromHexString(hex));
            using var reader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(input);
            MessageBody decoded = (MessageBody)serializer.Deserialize(reader, new SerializationContext(resolver));
            Assert.AreEqual(body.GetType(), decoded.GetType());
        }

        private static void AssertFeedback(FeedbackMessage message, string key)
        {
            Assert.AreEqual(0, message.Unknown1);
            Assert.AreEqual(ClientFeedback.CategoryId, message.CategoryId);
            Assert.AreEqual(unchecked((int)ClientFeedback.ElfHash(key)), message.MessageId);
        }

        private static string Describe(MessageBody body) => body switch
        {
            StatMessage stat => "S:" + stat.Stats.Single().Value1 + ":" + stat.Stats.Single().Value2,
            CharacterActionMessage action => "A:" + action.Action,
            TeamMemberMessage member => "M:" + member.Member.Instance,
            TeamMemberInfoMessage info => "V:" + info.Member.Instance,
            _ => body.GetType().Name
        };

        private static void AssertRosterRows(Fixture fixture, Player viewer, params int[] members)
        {
            TeamMemberMessage[] rows = fixture.Session(viewer).Bodies.OfType<TeamMemberMessage>().ToArray();
            CollectionAssert.AreEquivalent(members, rows.Select(m => m.Member.Instance).ToArray(),
                "Unexpected roster announcements for viewer " + viewer.Identity.Instance + ".");
            Assert.IsTrue(rows.All(m => m.Identity == viewer.Identity && m.Team.Type == IdentityType.TeamWindow
                && m.Team.Instance == fixture.Teams.GetTeam(viewer)!.TeamId));
        }

        private static void AssertDepartures(Fixture fixture, Player viewer, int team, params int[] members)
        {
            CharacterActionMessage[] departures = fixture.Session(viewer).Bodies.OfType<CharacterActionMessage>()
                .Where(m => m.Action == CharacterActionType.TeamMemberLeft).ToArray();
            CollectionAssert.AreEquivalent(members, departures.Select(m => m.Target.Instance).ToArray());
            Assert.IsTrue(departures.All(m => m.Identity == viewer.Identity && m.Parameter1 == team && m.Parameter2 == -1));
        }

        private static void AssertInitialRosterOrder(Fixture fixture, Player viewer)
        {
            Assert.AreEqual(viewer.Identity, fixture.Session(viewer).Bodies.OfType<TeamMemberMessage>().First().Member,
                "A fresh roster must announce its viewer before the other members.");
            AssertAnnouncementsPrecedeVitals(fixture, viewer);
        }

        private static void AssertAnnouncementsPrecedeVitals(Fixture fixture, Player viewer)
        {
            List<MessageBody> bodies = fixture.Session(viewer).Bodies;
            foreach (TeamMemberMessage row in bodies.OfType<TeamMemberMessage>().Where(m => m.Member != viewer.Identity))
            {
                TeamMemberInfoMessage vitals = bodies.OfType<TeamMemberInfoMessage>().Single(m => m.Member == row.Member);
                Assert.IsTrue(bodies.IndexOf(row) < bodies.IndexOf(vitals),
                    "Member " + row.Member.Instance + " must be announced before its vitals.");
            }
        }

        private sealed class ProjectionQueue
        {
            private readonly ConcurrentQueue<Action> _pending = new();
            public bool Deferred;
            public void Dispatch(Player _, Action action)
            {
                if (Deferred)
                {
                    _pending.Enqueue(action);
                }
                else
                {
                    action();
                }
            }
            public void Drain(bool reverse = false)
            {
                while (!_pending.IsEmpty)
                {
                    var batch = new List<Action>();
                    while (_pending.TryDequeue(out Action? action)) batch.Add(action);
                    if (reverse)
                    {
                        batch.Reverse();
                    }
                    foreach (Action action in batch) action();
                }
            }
        }

        private sealed class Fixture
        {
            public readonly TeamService Teams;
            public readonly TeamChat Chat = new();
            public DateTime UtcNow = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);
            private readonly Dictionary<int, TeamSession> _sessions = new();
            public IEnumerable<MessageBody> AllBodies => _sessions.Values.SelectMany(s => s.Bodies);
            public Fixture(Action<Player, Action>? dispatch = null)
                => Teams = new TeamService(Chat, () => UtcNow, dispatch ?? ((_, action) => action()));
            public Player Player(int id, int level = 60, int oldTeamStat = 0)
            {
                Player player = TestWorld.CreatePlayer(id);
                player.Name = "Member" + id;
                player.Stats.Set(CharacterStat.Level, level);
                player.Stats.Set(CharacterStat.Profession, 3);
                player.Stats.Set(CharacterStat.MaxHealth, 450);
                player.Stats.Set(CharacterStat.MaxNanoEnergy, 200);
                player.Stats.Set(CharacterStat.Team, oldTeamStat);
                ReplaceSession(player);
                Teams.AttachPlayer(player);
                return player;
            }
            public TeamSession Session(Player player) => _sessions[player.Identity.Instance];
            public void ReplaceSession(Player player)
            {
                var session = new TeamSession(); session.BindPlayer(player);
                player.Session = session; _sessions[player.Identity.Instance] = session;
            }
            public TeamSession BeginZoning(Player player)
            {
                TeamSession old = Session(player);
                player.Session = null;
                old.UnbindPlayer();
                Teams.OnTransportDisconnected(player, old);
                return old;
            }
            public void Action(Player player, CharacterActionType action, Identity target = default, int p2 = 0)
                => Teams.TryHandle(player, new CharacterActionMessage { Identity = player.Identity,
                    Action = action, Target = target, Parameter2 = p2 });
            public void Invite(Player inviter, Player invitee, bool confirmed = false)
                => Action(inviter, CharacterActionType.TeamRequestInvite, invitee.Identity, confirmed ? 1 : 0);
            public void Accept(Player invitee, Player inviter)
                => Action(invitee, CharacterActionType.TeamRequestReply, inviter.Identity, 1);
            public void Join(Player inviter, Player invitee) { Invite(inviter, invitee); Accept(invitee, inviter); }
            public void Clear() { foreach (TeamSession session in _sessions.Values) session.Bodies.Clear(); }
        }

        private sealed class TeamChat : IChatEngineLink
        {
            public readonly List<ChatCommand> Commands = new();
            public void Start() { }
            public bool TrySend(MessageBase message) { if (message is ChatCommand command) Commands.Add(command); return true; }
            public void Dispose() { }
        }

        private sealed class TeamSession : IZoneSession
        {
            public readonly List<MessageBody> Bodies = new();
            public SessionState State { get; set; } = SessionState.InPlay;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player;
            public void UnbindPlayer() => Player = null;
            public void Close() => State = SessionState.Closed;
            public void Send(byte[] packet) => throw new NotSupportedException();
            public void Send(Message message) => Send(message.Body);
            public void Send(MessageBody body) => Bodies.Add(body);
            public void Send(MessageBody body, int sender, int receiver) => Send(body);
            public void SendInitiateCompression() { }
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
        }
    }
}
