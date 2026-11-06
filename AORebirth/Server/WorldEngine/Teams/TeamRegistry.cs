namespace WorldEngine.Teams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.World.Contracts;
    using AORebirth.World.Contracts.V1;

    using Microsoft.Extensions.Logging;

    using WorldEngine.State;

    /// <summary>
    /// The one team authority for every zone instance: ids, membership, leader, raid layout and invitations. The rules
    /// are the zone TeamService's own (same checks, same messages); zones mirror each team that has one of their
    /// players and speak the client protocol. Level eligibility stays in the zone (GameData), which checks the range
    /// this registry hands back before confirming an invite.
    /// Lock order: this registry's lock, then <see cref="WorldState"/>'s, never the reverse.
    /// </summary>
    public sealed class TeamRegistry
    {
        const int FirstTeamId = 0x02800000;
        const int RaidTeamIdentityCount = 6;
        static readonly TimeSpan DeclineCooldown = TimeSpan.FromSeconds(30);
        static readonly TimeSpan InvitationLifetime = TimeSpan.FromMinutes(2);

        sealed class Team(int id, int leader)
        {
            public int Id { get; } = id;
            public int Leader = leader;
            public List<int> Members { get; } = [leader];
            public bool IsRaid;
            public bool ExplicitLeadershipTransfer;
            public Dictionary<int, int> RaidTeamIndex { get; } = new();
            public ulong Version;
        }

        sealed record Invitation(int Inviter, int Invitee, LeaseToken InviterToken, LeaseToken InviteeToken, int TeamId, long ExpiresMs);

        readonly object _sync = new();
        readonly WorldState _state;
        readonly ILogger<TeamRegistry> _log;
        readonly Func<long> _clock;
        readonly Dictionary<int, Team> _teams = new();
        readonly Dictionary<int, Team> _membership = new();
        readonly Dictionary<int, Invitation> _invitations = new();
        readonly Dictionary<int, long> _declinedUntilMs = new();
        int _nextTeamId = FirstTeamId;
        ulong _version;

        public TeamRegistry(WorldState state, ILogger<TeamRegistry> log, Func<long>? clock = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? (() => Environment.TickCount64);
        }

        long Now => _clock();

        public TeamCommandReply Command(ServiceInstance caller, TeamCommand command)
        {
            lock (_sync)
            {
                WorldState.MemberView? actor = _state.View(command.Actor);
                // Only the instance that owns the actor may speak for it.
                if (actor == null || !ReferenceEquals(actor.Value.Owner, caller))
                    return Refused(text: null);

                return command.Op switch
                {
                    TeamOp.TeamInvite => Invite(actor.Value, command),
                    TeamOp.TeamAccept => Reply(actor.Value, accept: true, command.Target),
                    TeamOp.TeamDecline => Reply(actor.Value, accept: false, command.Target),
                    TeamOp.TeamLeave => Leave(actor.Value.CharacterId, notifyLeaving: true),
                    TeamOp.TeamLogout => Leave(actor.Value.CharacterId, notifyLeaving: false),
                    TeamOp.TeamKick => Kick(actor.Value, command.Target),
                    TeamOp.TeamPromote => Promote(actor.Value, command.Target),
                    TeamOp.TeamConvertRaid => ConvertToRaid(actor.Value),
                    TeamOp.TeamMoveRaidMember => MoveRaidMember(actor.Value, command.Target, command.RaidTeam),
                    TeamOp.TeamSync => Sync(actor.Value),
                    TeamOp.TeamDisconnect => Disconnected(actor.Value.CharacterId),
                    _ => Refused("Unsupported team command.")
                };
            }
        }

        /// <summary>Characters whose lease ended (an instance died, a delete): they leave their teams as on logout.</summary>
        public void MembersGone(IReadOnlyList<int> characterIds)
        {
            if (characterIds.Count == 0) return;
            lock (_sync)
            {
                foreach (int id in characterIds)
                {
                    CancelInvitations(id);
                    _declinedUntilMs.Remove(id);
                    if (_membership.ContainsKey(id)) Remove(id, notifyLeaving: false);
                }

                foreach (KeyValuePair<int, Invitation> invitation in _invitations.Where(i => Now >= i.Value.ExpiresMs).ToList())
                    _invitations.Remove(invitation.Key);
                foreach (KeyValuePair<int, long> declined in _declinedUntilMs.Where(d => Now >= d.Value).ToList())
                    _declinedUntilMs.Remove(declined.Key);
            }
        }

        /// <summary>
        /// A zone (re)registered with the teams it mirrors. Right after a World restart those are taken back. Then
        /// every team with a member there is sent again (pushes sent while its stream was down are lost), and any
        /// mirror World no longer has a reason for is told to go.
        /// </summary>
        public void Reconcile(ServiceInstance instance, IReadOnlyList<TeamState> asserted, bool recovering)
        {
            lock (_sync)
            {
                if (recovering && asserted.Count > 0) Assert(asserted);
                var current = new HashSet<int>();
                foreach (Team team in _teams.Values)
                {
                    if (!team.Members.Any(id => _state.View(id) is { } member && ReferenceEquals(member.Owner, instance))) continue;
                    current.Add(team.Id);
                    instance.Send(new WorldToService { Team = new TeamEvent { Update = new TeamUpdate { Team = State(team), Sync = true } } });
                }

                foreach (TeamState stale in asserted.Where(team => !current.Contains(team.TeamId)))
                    instance.Send(new WorldToService
                    {
                        Team = new TeamEvent
                        {
                            Update = new TeamUpdate { Team = new TeamState { TeamId = stale.TeamId }, Disbanded = true }
                        }
                    });
            }
        }

        /// <summary>
        /// After a World restart: teams World does not know are adopted with members nobody else claims; known ones
        /// keep World's state. Caller holds the lock.
        /// </summary>
        void Assert(IEnumerable<TeamState> teams)
        {
            lock (_sync)
            {
                foreach (TeamState asserted in teams)
                {
                    if (asserted.TeamId <= 0 || _teams.ContainsKey(asserted.TeamId)) continue;
                    var members = asserted.Members.Select(m => m.CharacterId)
                        .Where(id => id > 0 && !_membership.ContainsKey(id) && _state.View(id) != null).Distinct().ToList();
                    if (members.Count < 2) continue;
                    int leader = members.Contains(asserted.Leader) ? asserted.Leader : members[0];
                    var team = new Team(asserted.TeamId, leader) { IsRaid = asserted.Raid, ExplicitLeadershipTransfer = asserted.ExplicitLeader };
                    team.Members.Clear();
                    team.Members.AddRange(members);
                    foreach (TeamMember member in asserted.Members)
                        if (members.Contains(member.CharacterId) && member.RaidTeam >= 0)
                            team.RaidTeamIndex[member.CharacterId] = member.RaidTeam;
                    _teams[team.Id] = team;
                    foreach (int id in members) _membership[id] = team;
                    _nextTeamId = Math.Max(_nextTeamId, team.Id <= int.MaxValue - (RaidTeamIdentityCount - 1)
                        ? team.Id + RaidTeamIdentityCount - 1 : int.MaxValue);
                    Publish(team, new TeamUpdate { Sync = true });
                }
            }
        }

        public object Snapshot()
        {
            lock (_sync)
            {
                return new
                {
                    teams = _teams.Values.Select(t => new
                    {
                        id = t.Id,
                        leader = t.Leader,
                        raid = t.IsRaid,
                        members = t.Members.ToList(),
                        version = t.Version
                    }).ToList(),
                    invitations = _invitations.Values.Select(i => new { inviter = i.Inviter, invitee = i.Invitee, team = i.TeamId }).ToList()
                };
            }
        }

        // ---------------------------------------------------------------------------------------------- operations

        TeamCommandReply Invite(WorldState.MemberView inviter, TeamCommand command)
        {
            int targetId = command.Target > 0 ? command.Target : _state.FindByName(command.TargetName);
            WorldState.MemberView? target = targetId > 0 ? _state.View(targetId) : null;
            if (target == null || target.Value.LinkDead || targetId == inviter.CharacterId)
                return Refused("Team invite target is not available.");
            if (_membership.ContainsKey(targetId)) return Feedback("Feedback_AlreadyTeamMember");
            _membership.TryGetValue(inviter.CharacterId, out Team? team);
            if (_invitations.TryGetValue(targetId, out Invitation? pending) && Now < pending.ExpiresMs)
                return Refused("Team invite already pending for " + target.Value.Card.Name + ".");
            if (_declinedUntilMs.TryGetValue(targetId, out long until) && Now < until)
                return Refused("That player declined recently. Wait a moment before inviting again.");
            if (target.Value.Card.Level is < 1 or > 220) return Refused("Team invite target level is unavailable.");

            if (!command.ConfirmedRange)
            {
                var range = new RangeCheck { Target = target.Value.Card };
                foreach (int id in team?.Members ?? [inviter.CharacterId])
                    if (_state.View(id) is { } member && !member.LinkDead)
                        range.Members.Add(member.Card);
                return new TeamCommandReply { Ok = false, Range = range };
            }

            _invitations[targetId] = new Invitation(inviter.CharacterId, targetId, inviter.Token, target.Value.Token,
                team?.Id ?? 0, Now + (long)InvitationLifetime.TotalMilliseconds);
            // The inviter learns who the target is (Recruit/LFT shows real names and levels).
            inviter.Owner.Send(new WorldToService
            {
                Team = new TeamEvent { Presence = new TeamPresenceCard { Viewer = inviter.CharacterId, Subject = target.Value.Card } }
            });
            target.Value.Owner.Send(new WorldToService
            {
                Team = new TeamEvent
                {
                    Invited = new TeamInvited
                    {
                        Invitee = targetId,
                        Inviter = inviter.Card,
                        SamePlayfield = ReferenceEquals(inviter.Owner, target.Value.Owner) && inviter.PlayfieldId == target.Value.PlayfieldId
                    }
                }
            });
            return Ok();
        }

        TeamCommandReply Reply(WorldState.MemberView invitee, bool accept, int requester)
        {
            int inviteeId = invitee.CharacterId;
            if (!_invitations.TryGetValue(inviteeId, out Invitation? invitation) || Now >= invitation.ExpiresMs)
            {
                _invitations.Remove(inviteeId);
                return Refused("No pending team invite.");
            }

            if (requester != 0 && requester != inviteeId && requester != invitation.Inviter)
                return Refused("Team invitation requester does not match.");
            WorldState.MemberView? inviter = _state.View(invitation.Inviter);
            _membership.TryGetValue(invitation.Inviter, out Team? team);
            if (inviter == null || inviter.Value.LinkDead
                || !LeaseTokens.SameAs(inviter.Value.Token, invitation.InviterToken)
                || !LeaseTokens.SameAs(invitee.Token, invitation.InviteeToken)
                || (team?.Id ?? 0) != invitation.TeamId
                || _membership.ContainsKey(inviteeId))
            {
                _invitations.Remove(inviteeId);
                return Refused("The team invitation is no longer valid.");
            }

            _invitations.Remove(inviteeId);
            if (!accept)
            {
                _declinedUntilMs[inviteeId] = Now + (long)DeclineCooldown.TotalMilliseconds;
                inviter.Value.Owner.Send(new WorldToService
                {
                    Team = new TeamEvent { Declined = new TeamDeclined { Inviter = invitation.Inviter, Invitee = inviteeId } }
                });
                return Ok();
            }

            if (team == null)
            {
                if (_nextTeamId > int.MaxValue - RaidTeamIdentityCount) return Refused("Team identity space is exhausted.");
                // Live raid subgroups use consecutive team identities; reserve all six before a possible conversion.
                team = new Team(_nextTeamId + 1, invitation.Inviter);
                _nextTeamId += RaidTeamIdentityCount;
                _teams[team.Id] = team;
                _membership[invitation.Inviter] = team;
                foreach (int id in _invitations.Where(p => p.Value.Inviter == invitation.Inviter && p.Value.TeamId == 0)
                    .Select(p => p.Key).ToArray())
                    _invitations[id] = _invitations[id] with { TeamId = team.Id };
            }

            team.Members.Add(inviteeId);
            _membership[inviteeId] = team;
            // Any outstanding invite sent/received under pre-join ownership is now stale.
            CancelInvitations(inviteeId);
            _invitations.Remove(invitation.Inviter);
            Publish(team, new TeamUpdate { Joined = inviteeId });
            _log.LogInformation("Team {Team} joined by {Character}", team.Id, inviteeId);
            return Ok();
        }

        TeamCommandReply Leave(int memberId, bool notifyLeaving)
        {
            CancelInvitations(memberId);
            Remove(memberId, notifyLeaving);
            return Ok();
        }

        TeamCommandReply Kick(WorldState.MemberView leader, int targetId)
        {
            if (targetId <= 0 || targetId == leader.CharacterId
                || !_membership.TryGetValue(leader.CharacterId, out Team? team)
                || team.Leader != leader.CharacterId || !team.Members.Contains(targetId))
                return Feedback("Feedback_NeedToBeTeamLeader");
            CancelInvitations(targetId);
            Remove(targetId, notifyLeaving: true);
            return Ok();
        }

        TeamCommandReply Promote(WorldState.MemberView leader, int targetId)
        {
            if (targetId <= 0 || targetId == leader.CharacterId
                || !_membership.TryGetValue(leader.CharacterId, out Team? team)
                || team.Leader != leader.CharacterId || !team.Members.Contains(targetId)
                || _state.View(targetId) is not { LinkDead: false })
                return Refused("Team leadership transfer target is not available or you are not the leader.");
            if (team.IsRaid && team.RaidTeamIndex.GetValueOrDefault(targetId, 0) > 0)
                return Refused("Only a member of Team 1 can be promoted to raid leader.");
            team.Leader = targetId;
            team.ExplicitLeadershipTransfer = true;
            Publish(team, new TeamUpdate());
            return Ok();
        }

        TeamCommandReply ConvertToRaid(WorldState.MemberView leader)
        {
            if (!_membership.TryGetValue(leader.CharacterId, out Team? team)) return Feedback("Feedback_YouAreNotMemberOfTeam");
            if (team.Leader != leader.CharacterId) return Feedback("Feedback_NeedToBeTeamLeader");
            if (team.IsRaid) return Refused("Your team is already a raid.");
            if (team.Members.Count < 2) return Refused("You need at least one other team member to convert to raid.");
            team.IsRaid = true;
            Publish(team, new TeamUpdate());
            return new TeamCommandReply { Ok = true, Feedback = "Feedback_RaidCreated" };
        }

        TeamCommandReply MoveRaidMember(WorldState.MemberView leader, int targetId, int raidTeam)
        {
            if (!_membership.TryGetValue(leader.CharacterId, out Team? team)) return Feedback("Feedback_YouAreNotMemberOfTeam");
            if (!team.IsRaid) return Refused("Your team is not a raid.");
            if (team.Leader != leader.CharacterId) return Feedback("Feedback_NeedToBeTeamLeader");
            if (targetId == team.Leader) return Refused("The raid leader cannot be moved to another team.");
            if (_state.View(targetId) is not { LinkDead: false } || !_membership.TryGetValue(targetId, out Team? targetTeam)
                || !ReferenceEquals(targetTeam, team))
                return Refused("That player is not in your raid.");
            team.RaidTeamIndex[targetId] = raidTeam;
            Publish(team, new TeamUpdate());
            return Ok();
        }

        /// <summary>The member arrived on another instance: that instance mirrors its team now.</summary>
        TeamCommandReply Sync(WorldState.MemberView member)
        {
            if (_membership.TryGetValue(member.CharacterId, out Team? team))
                member.Owner.Send(new WorldToService { Team = new TeamEvent { Update = new TeamUpdate { Team = State(team), Sync = true } } });
            return Ok();
        }

        TeamCommandReply Disconnected(int memberId)
        {
            CancelInvitations(memberId);
            return Ok();
        }

        void Remove(int memberId, bool notifyLeaving)
        {
            if (!_membership.Remove(memberId, out Team? team))
            {
                // Not in a team: its own instance still refreshes its team stats.
                SendTo(memberId, new TeamUpdate { Departed = memberId, NotifyLeaving = notifyLeaving });
                return;
            }

            team.Members.Remove(memberId);
            team.RaidTeamIndex.Remove(memberId);
            if (team.Members.Count == 1)
            {
                int last = team.Members[0];
                _membership.Remove(last);
                team.Members.Clear();
                CancelInvitations(last);
                _teams.Remove(team.Id);
                var update = new TeamUpdate
                {
                    Team = State(team),
                    Departed = memberId,
                    NotifyLeaving = notifyLeaving,
                    Disbanded = true,
                    LastMember = last
                };
                SendTo(memberId, update);
                SendTo(last, update);
                _log.LogInformation("Team {Team} disbanded", team.Id);
                return;
            }

            if (team.Members.Count > 1)
            {
                if (team.Leader == memberId) team.Leader = team.Members[0];
                team.ExplicitLeadershipTransfer = false;
            }

            var change = new TeamUpdate { Departed = memberId, NotifyLeaving = notifyLeaving };
            Publish(team, change, extra: memberId);
        }

        // ---------------------------------------------------------------------------------------------- delivery

        /// <summary>Sends the team's new state to every instance with a member (and the departed member's).</summary>
        void Publish(Team team, TeamUpdate update, int extra = 0)
        {
            team.Version = ++_version;
            update.Team = State(team);
            var sent = new HashSet<ServiceInstance>(ReferenceEqualityComparer.Instance);
            foreach (int id in extra > 0 ? team.Members.Append(extra) : team.Members)
            {
                if (_state.View(id) is { } member && sent.Add(member.Owner))
                    member.Owner.Send(new WorldToService { Team = new TeamEvent { Update = update } });
            }
        }

        void SendTo(int characterId, TeamUpdate update)
        {
            if (_state.View(characterId) is { } member)
                member.Owner.Send(new WorldToService { Team = new TeamEvent { Update = update } });
        }

        TeamState State(Team team)
        {
            var state = new TeamState
            {
                TeamId = team.Id,
                Version = team.Version,
                Leader = team.Leader,
                Raid = team.IsRaid,
                ExplicitLeader = team.ExplicitLeadershipTransfer
            };
            foreach (int id in team.Members)
            {
                PlayerCard card = _state.View(id)?.Card ?? new PlayerCard { CharacterId = id };
                state.Members.Add(new TeamMember
                {
                    CharacterId = id,
                    Card = card,
                    RaidTeam = team.RaidTeamIndex.GetValueOrDefault(id, -1)
                });
            }

            return state;
        }

        void CancelInvitations(int id)
        {
            foreach (int target in _invitations.Where(p => p.Key == id || p.Value.Inviter == id).Select(p => p.Key).ToArray())
                _invitations.Remove(target);
        }

        static TeamCommandReply Ok() => new() { Ok = true };

        static TeamCommandReply Refused(string? text) => new() { Ok = false, Text = text ?? string.Empty };

        static TeamCommandReply Feedback(string key) => new() { Ok = false, Feedback = key };
    }
}
