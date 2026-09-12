namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using AORebirth.Communication.Messages;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using ZoneEngine_New.Core.Chat;
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
        public void DisconnectDropsMembershipAndReconnectCannotRestorePersistedTeamStat()
        {
            var f = new Fixture();
            Player a = f.Player(1), b = f.Player(2);
            f.Join(a, b);
            f.Teams.OnTransportDisconnected(b, f.Session(b));
            Assert.IsNull(f.Teams.GetTeam(a));
            f.ReplaceSession(b);
            f.Teams.AttachPlayer(b);
            f.Teams.RefreshPlayer(b);
            Assert.IsNull(f.Teams.GetTeam(b));
            Assert.AreEqual(0, b.Stats.GetOrZero(CharacterStat.Team));
            f.Teams.DetachPlayer(b);
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
            f.Teams.RefreshPlayer(b);
            f.Teams.OnTransportDisconnected(b, old);
            Assert.AreEqual(team, f.Teams.GetTeam(b)!.TeamId);
            CollectionAssert.AreEqual(new[] { 2, 1 }, f.Session(b).Bodies.OfType<TeamMemberMessage>()
                .Select(m => m.Member.Instance).ToArray());
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
            f.Clear();
            Assert.IsTrue(f.Teams.ConvertToRaid(a));
            Assert.IsTrue(f.Teams.GetTeam(b)!.IsRaid);
            Assert.AreEqual(2, f.AllBodies.OfType<RaidMessage>().Count());
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 1 && c.ChatCommandString.StartsWith("#aorebirth-raid-convert ", StringComparison.Ordinal)));
            Assert.IsTrue(f.Chat.Commands.Any(c => c.CharacterId == 2 && c.ChatCommandString.StartsWith("#aorebirth-raid-convert ", StringComparison.Ordinal)));
            Assert.IsFalse(f.Teams.ConvertToRaid(a));
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
            var handler = new TeamChatMessageHandler(f.Teams);
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

        private static string Describe(MessageBody body) => body switch
        {
            StatMessage stat => "S:" + stat.Stats.Single().Value1 + ":" + stat.Stats.Single().Value2,
            CharacterActionMessage action => "A:" + action.Action,
            TeamMemberMessage member => "M:" + member.Member.Instance,
            TeamMemberInfoMessage info => "V:" + info.Member.Instance,
            _ => body.GetType().Name
        };

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
