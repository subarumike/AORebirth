namespace ZoneEngine_New.Core.Teams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Communication.Messages;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed record TeamSnapshot(int TeamId, int LeaderId, IReadOnlyList<int> MemberIds, bool IsRaid);

    /// <summary>
    /// One process-local team authority, independent of playfield and transport ownership.
    /// Only the accepted Player object may mutate its membership. No team database or capture input.
    /// Wire fields follow the compiled Legacy TeamRuntime and current AOtomation contracts.
    /// </summary>
    public sealed class TeamService
    {
        private sealed class Team(int id, int leader)
        {
            public int Id { get; } = id;
            public int Leader = leader;
            public List<int> Members { get; } = [leader];
            public bool IsRaid;
            public bool ExplicitLeadershipTransfer;
        }

        private sealed record Invitation(Player Inviter, Player Invitee, IZoneSession InviterSession,
            IZoneSession InviteeSession, int TeamId);

        // Updated only from that player's attach/refresh/stat-change callback on its owner tick.
        // Team operations on another playfield never enumerate or read this player's mutable Stats.
        private sealed record MemberData(string Name, int Level, int Profession, int MaxHealth,
            int MaxNano, bool HasVitals, InfoPacketMessage Info);

        private readonly object _sync = new();
        private readonly Dictionary<int, Player> _players = new();
        private readonly Dictionary<int, MemberData> _data = new();
        private readonly Dictionary<int, Action<CharacterStat, int, int, bool>> _statObservers = new();
        private readonly Dictionary<int, long> _projectionEpochs = new();
        // A character has exactly one membership entry; Team.Members is its ordered projection.
        private readonly Dictionary<int, Team> _membership = new();
        private readonly Dictionary<int, Invitation> _invitations = new();
        private readonly Dictionary<int, DateTime> _declinedUntil = new();
        private readonly IChatEngineLink? _chat;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<Player, Action>? _dispatchOnOwner;
        private int _nextTeamId = 0x02800000;
        private bool _stopped;

        public TeamService(IChatEngineLink? chatLink = null, Func<DateTime>? utcNow = null,
            Action<Player, Action>? dispatchOnOwner = null)
        {
            _chat = chatLink;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _dispatchOnOwner = dispatchOnOwner;
        }

        public void AttachPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (_sync)
            {
                if (_stopped) return;
                if (player.Identity.Type != IdentityType.CanbeAffected || player.Identity.Instance <= 0)
                    throw new ArgumentException("A team owner must be a character identity.", nameof(player));
                if (_players.TryGetValue(player.Identity.Instance, out Player? previous))
                {
                    if (ReferenceEquals(previous, player)) return;
                    Remove(previous, notifyLeaving: false);
                    CancelInvitations(previous.Identity.Instance);
                    previous.Stats.StatChanged -= _statObservers[player.Identity.Instance];
                }
                _players[player.Identity.Instance] = player;
                _projectionEpochs[player.Identity.Instance] = _projectionEpochs.GetValueOrDefault(player.Identity.Instance) + 1;
                CaptureData(player);
                Action<CharacterStat, int, int, bool> observer = (stat, _, _, _) =>
                {
                    if (stat is not (CharacterStat.Level or CharacterStat.Profession or CharacterStat.MaxHealth
                        or CharacterStat.MaxNanoEnergy or CharacterStat.Health)) return;
                    lock (_sync) { if (Owns(player)) CaptureData(player); }
                };
                _statObservers[player.Identity.Instance] = observer;
                player.Stats.StatChanged += observer;
                // Persisted stat values never reconstitute a process-local team.
                ResetLocalStats(player);
            }
        }

        public void RefreshPlayer(Player player)
        {
            lock (_sync)
            {
                if (!IsActive(player)) return;
                CaptureData(player);
                if (_membership.TryGetValue(player.Identity.Instance, out Team? team))
                    SendRoster(player, team);
                else
                    ApplyStats(player, null, 4, send: true);
            }
        }

        public void OnTransportDisconnected(Player player, IZoneSession disconnectedSession)
        {
            lock (_sync)
            {
                // Zoning clears Player.Session before transport closure. Superseded transport callbacks
                // must also be ignored; neither is the accepted Legacy leave-game transition.
                if (!Owns(player) || !ReferenceEquals(player.Session, disconnectedSession)) return;
                CancelInvitations(player.Identity.Instance);
                Remove(player, notifyLeaving: false);
            }
        }

        public void DetachPlayer(Player player)
        {
            lock (_sync)
            {
                if (!Owns(player)) return;
                CancelInvitations(player.Identity.Instance);
                Remove(player, notifyLeaving: false);
                ResetLocalStats(player);
                player.Stats.StatChanged -= _statObservers[player.Identity.Instance];
                _statObservers.Remove(player.Identity.Instance);
                _players.Remove(player.Identity.Instance);
                _data.Remove(player.Identity.Instance);
                _projectionEpochs.Remove(player.Identity.Instance);
                _declinedUntil.Remove(player.Identity.Instance);
            }
        }

        public void Shutdown()
        {
            lock (_sync)
            {
                if (_stopped) return;
                foreach (Player player in _players.Values.ToArray()) Remove(player, notifyLeaving: true);
                foreach (Player player in _players.Values)
                    player.Stats.StatChanged -= _statObservers[player.Identity.Instance];
                _statObservers.Clear();
                _data.Clear();
                _projectionEpochs.Clear();
                _invitations.Clear();
                _declinedUntil.Clear();
                _players.Clear();
                _stopped = true;
            }
        }

        public TeamSnapshot? GetTeam(Player player)
        {
            lock (_sync)
                return Owns(player) && _membership.TryGetValue(player.Identity.Instance, out Team? team)
                    ? new TeamSnapshot(team.Id, team.Leader, Array.AsReadOnly(team.Members.ToArray()), team.IsRaid)
                    : null;
        }

        public bool AreTeammates(Player first, Player second)
        {
            lock (_sync)
                return Owns(first) && Owns(second)
                    && _membership.TryGetValue(first.Identity.Instance, out Team? team)
                    && _membership.TryGetValue(second.Identity.Instance, out Team? other)
                    && ReferenceEquals(team, other);
        }

        public bool TryHandle(Player player, CharacterActionMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (message.Action is not (CharacterActionType.TeamRequestInvite or CharacterActionType.TeamRequestReply
                or CharacterActionType.ClientTeamInviteReply or CharacterActionType.LeaveTeam
                or CharacterActionType.TeamKickMember or CharacterActionType.TransferLeader
                or CharacterActionType.AcceptTeamRequest)) return false;
            lock (_sync)
            {
                if (!IsActive(player) || message.Identity != player.Identity) return true;
                switch (message.Action)
                {
                    case CharacterActionType.TeamRequestInvite:
                        Identity target = message.Target;
                        if (target == Identity.None)
                        {
                            int id = message.Parameter2 > 1 ? message.Parameter2 : message.Parameter1;
                            target = CharacterIdentity(id);
                        }
                        Invite(player, target, message.Parameter2 == 1);
                        break;
                    case CharacterActionType.TeamRequestReply:
                    case CharacterActionType.ClientTeamInviteReply:
                        if (message.Parameter2 is 1 or 0 or 20)
                            Reply(player, message.Parameter2 == 1, message.Target);
                        break;
                    case CharacterActionType.LeaveTeam:
                        CancelInvitations(player.Identity.Instance);
                        Remove(player, notifyLeaving: true);
                        break;
                    case CharacterActionType.TeamKickMember:
                        Kick(player, message.Target);
                        break;
                    case CharacterActionType.TransferLeader:
                        TransferLeadership(player, message.Target);
                        break;
                    // This is server-to-client leadership state, never invite acceptance.
                    case CharacterActionType.AcceptTeamRequest:
                        break;
                }
                return true;
            }
        }

        public bool TryHandleChatCommand(Player player, string[] args)
        {
            lock (_sync)
            {
                if (!IsActive(player)) return false;
                if (args == null || args.Length < 2) return Error(player, "Team commands: /team invite <name>, /team accept, /team decline, /team leave.");
                switch (args[1].ToLowerInvariant())
                {
                    case "accept": return Reply(player, true, Identity.None);
                    case "decline":
                    case "reject": return Reply(player, false, Identity.None);
                    case "leave": CancelInvitations(player.Identity.Instance); return Remove(player, true);
                    case "invite" when args.Length == 3:
                        Player[] matches = _players.Values.Where(p => IsActive(p)
                            && string.Equals(p.Name, args[2], StringComparison.OrdinalIgnoreCase)).ToArray();
                        return matches.Length == 1 ? Invite(player, matches[0].Identity, false)
                            : Error(player, "Team invite target is not available.");
                    default: return Error(player, "Unknown team command.");
                }
            }
        }

        public bool ConvertToRaid(Player leader)
        {
            lock (_sync)
            {
                if (!IsActive(leader)) return false;
                if (!_membership.TryGetValue(leader.Identity.Instance, out Team? team)
                    || team.Leader != leader.Identity.Instance || team.Members.Count < 2)
                    return Error(leader, "Only the leader of a team can convert to raid.");
                if (team.IsRaid) return Error(leader, "Your team is already a raid.");
                team.IsRaid = true;
                foreach (int id in team.Members)
                {
                    Player member = _players[id];
                    Send(member, new RaidMessage { Identity = member.Identity, Unknown = 0, Unknown1 = 0 });
                    ChatCommand(id, "#aorebirth-raid-convert " + team.Id);
                }
                return true;
            }
        }

        private bool Invite(Player inviter, Identity targetIdentity, bool confirmedRange)
        {
            if (targetIdentity.Type != IdentityType.CanbeAffected || targetIdentity.Instance <= 0
                || !_players.TryGetValue(targetIdentity.Instance, out Player? target) || !IsActive(target)
                || ReferenceEquals(target, inviter)) return Error(inviter, "Team invite target is not available.");
            if (_membership.ContainsKey(target.Identity.Instance)) return Error(inviter, "That player is already in a team.");
            _membership.TryGetValue(inviter.Identity.Instance, out Team? team);
            if (_invitations.ContainsKey(target.Identity.Instance))
                return Error(inviter, "Team invite already pending for " + target.Name + ".");
            if (_declinedUntil.TryGetValue(target.Identity.Instance, out DateTime until) && _utcNow() < until)
                return Error(inviter, "That player declined recently. Wait a moment before inviting again.");
            if (!TryLevel(target, out int targetLevel)) return Error(inviter, "Team invite target level is unavailable.");
            if (!confirmedRange)
            {
                bool tooLow = false;
                foreach (int id in team?.Members ?? [inviter.Identity.Instance])
                {
                    if (!TryLevel(_players[id], out int level)) return Error(inviter, "Team member level is unavailable.");
                    if (ZoneEngine.Core.TeamXpShareWindow.IsTooHighForXpShare(level, targetLevel))
                    {
                        Send(inviter, Action(inviter, CharacterActionType.TeamInviteAck, target.Identity));
                        return true;
                    }
                    tooLow |= ZoneEngine.Core.TeamXpShareWindow.IsTooLowForXpShare(level, targetLevel);
                }
                if (tooLow)
                {
                    Send(inviter, Action(inviter, CharacterActionType.TeamInviteTooLow, target.Identity));
                    return true;
                }
            }
            _invitations[target.Identity.Instance] = new Invitation(inviter, target,
                inviter.Session!, target.Session!, team?.Id ?? 0);
            // Names/levels come from current actors; no off-map SCFU ghosts or historical capture reads.
            Send(inviter, _data[target.Identity.Instance].Info);
            Send(inviter, new StatMessage { Identity = target.Identity, Stats =
                [new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.Level, Value2 = (uint)targetLevel }] });
            if (!ReferenceEquals(inviter.Playfield, target.Playfield))
                Send(target, new TeamInviteMessage { Identity = target.Identity, Unknown = 1,
                    Inviter = inviter.Identity, Name = _data[inviter.Identity.Instance].Name });
            Send(target, Action(target, CharacterActionType.TeamRequestInvite, inviter.Identity));
            return true;
        }

        private bool Reply(Player invitee, bool accept, Identity requester)
        {
            if (!_invitations.TryGetValue(invitee.Identity.Instance, out Invitation? invitation))
                return Error(invitee, "No pending team invite.");
            Player inviter = invitation.Inviter;
            if (requester != Identity.None && requester != invitee.Identity && requester != inviter.Identity)
                return Error(invitee, "Team invitation requester does not match.");
            _membership.TryGetValue(inviter.Identity.Instance, out Team? team);
            if (!ReferenceEquals(invitation.Invitee, invitee) || !IsActive(inviter)
                || !ReferenceEquals(inviter.Session, invitation.InviterSession)
                || !ReferenceEquals(invitee.Session, invitation.InviteeSession)
                || (team?.Id ?? 0) != invitation.TeamId
                || !TryLevel(inviter, out _) || !TryLevel(invitee, out _)
                || _membership.ContainsKey(invitee.Identity.Instance))
            {
                _invitations.Remove(invitee.Identity.Instance);
                return Error(invitee, "The team invitation is no longer valid.");
            }
            _invitations.Remove(invitee.Identity.Instance);
            if (!accept)
            {
                _declinedUntil[invitee.Identity.Instance] = _utcNow().AddSeconds(30);
                Send(inviter, Action(inviter, CharacterActionType.TeamRequestReply, invitee.Identity, 0, 20));
                return true;
            }
            if (team == null)
            {
                if (_nextTeamId == int.MaxValue) return Error(invitee, "Team identity space is exhausted.");
                team = new Team(++_nextTeamId, inviter.Identity.Instance);
                _membership.Add(inviter.Identity.Instance, team);
                foreach (int id in _invitations.Where(p => ReferenceEquals(p.Value.Inviter, inviter)
                    && p.Value.TeamId == 0).Select(p => p.Key).ToArray())
                    _invitations[id] = _invitations[id] with { TeamId = team.Id };
            }
            team.Members.Add(invitee.Identity.Instance);
            _membership.Add(invitee.Identity.Instance, team);
            // Any outstanding invite sent/received under pre-join ownership is now stale.
            CancelInvitations(invitee.Identity.Instance);
            _invitations.Remove(inviter.Identity.Instance);
            ChatCommand(invitee.Identity.Instance, "#aorebirth-lft-remove");
            foreach (int id in team.Members) SendRoster(_players[id], team);
            return true;
        }

        private bool Kick(Player leader, Identity target)
        {
            if (target.Type != IdentityType.CanbeAffected || target == leader.Identity
                || !_membership.TryGetValue(leader.Identity.Instance, out Team? team)
                || team.Leader != leader.Identity.Instance || !team.Members.Contains(target.Instance))
                return Error(leader, "Only the team leader can kick another member of this team.");
            CancelInvitations(target.Instance);
            return Remove(_players[target.Instance], true);
        }

        private bool TransferLeadership(Player leader, Identity target)
        {
            if (target.Type != IdentityType.CanbeAffected || target == leader.Identity
                || !_membership.TryGetValue(leader.Identity.Instance, out Team? team)
                || team.Leader != leader.Identity.Instance || !team.Members.Contains(target.Instance)
                || !IsActive(_players[target.Instance]))
                return Error(leader, "Team leadership transfer target is not available or you are not the leader.");
            team.Leader = target.Instance;
            team.ExplicitLeadershipTransfer = true;
            foreach (int id in team.Members)
            {
                Player member = _players[id];
                ApplyStats(member, team, id == target.Instance ? 15 : 13, send: true);
                Send(member, Action(member, CharacterActionType.AcceptTeamRequest, target,
                    (int)IdentityType.TeamWindow, team.Id));
            }
            return true;
        }

        private bool Remove(Player player, bool notifyLeaving)
        {
            if (!_membership.Remove(player.Identity.Instance, out Team? team))
            {
                ApplyStats(player, null, 4, send: notifyLeaving);
                if (notifyLeaving)
                    Send(player, Action(player, CharacterActionType.TeamMemberLeft, player.Identity, 0, -1));
                return true;
            }
            team.Members.Remove(player.Identity.Instance);
            ApplyStats(player, null, 4, send: notifyLeaving);
            ChatCommand(player.Identity.Instance, "#aorebirth-team-leave " + team.Id);
            if (notifyLeaving) Send(player, Action(player, CharacterActionType.TeamMemberLeft, player.Identity, team.Id, -1));
            foreach (int id in team.Members)
                Send(_players[id], Action(_players[id], CharacterActionType.TeamMemberLeft, player.Identity, team.Id, -1));
            if (team.Members.Count == 1)
            {
                Player last = _players[team.Members[0]];
                _membership.Remove(last.Identity.Instance);
                team.Members.Clear();
                CancelInvitations(last.Identity.Instance);
                ApplyStats(last, null, 4, send: true);
                Send(last, Action(last, CharacterActionType.TeamMemberLeft, last.Identity, team.Id, -1));
                ChatCommand(last.Identity.Instance, "#aorebirth-team-leave " + team.Id);
            }
            else if (team.Members.Count > 1)
            {
                if (team.Leader == player.Identity.Instance) team.Leader = team.Members[0];
                team.ExplicitLeadershipTransfer = false;
                foreach (int id in team.Members)
                {
                    Player member = _players[id];
                    ApplyStats(member, team, id == team.Leader ? 7 : 5, send: true);
                    if (id == team.Leader)
                    {
                        Send(member, Action(member, CharacterActionType.TeamRequestReply, Identity.None, 0, 17));
                        Send(member, Action(member, CharacterActionType.AcceptTeamRequest, member.Identity,
                            (int)IdentityType.TeamWindow, team.Id));
                    }
                }
            }
            return true;
        }

        private void SendRoster(Player viewer, Team team)
        {
            if (!IsActive(viewer)) return;
            int social = viewer.Identity.Instance == team.Leader
                ? (team.ExplicitLeadershipTransfer ? 15 : 7)
                : (team.ExplicitLeadershipTransfer ? 13 : 5);
            ApplyStats(viewer, team, social, send: false);
            // Keep the captured Legacy ordering: social/team-side precede ack/self roster,
            // leadership precedes other members, and channel identity follows the roster.
            SetStat(viewer, CharacterStat.SocialStatus, social, true);
            SetStat(viewer, CharacterStat.TeamSide, 2, true);
            SetStat(viewer, CharacterStat.SocialStatus, social, true);
            if (viewer.Identity.Instance == team.Leader)
            {
                Send(viewer, Action(viewer, CharacterActionType.TeamRequestReply, Identity.None, 0, 17));
                SetStat(viewer, CharacterStat.SocialStatus, social, true);
            }
            Announce(viewer, viewer, team);
            if (viewer.Identity.Instance == team.Leader)
            {
                Send(viewer, Action(viewer, CharacterActionType.AcceptTeamRequest, viewer.Identity,
                    (int)IdentityType.TeamWindow, team.Id));
                SetStat(viewer, CharacterStat.SocialStatus, social, true);
            }
            foreach (int id in team.Members)
            {
                if (id == viewer.Identity.Instance) continue;
                Player member = _players[id];
                Announce(viewer, member, team);
                // Legacy paired max values; unresolved vitals are omitted, never synthesized as 469.
                MemberData data = _data[id];
                if (data.HasVitals)
                    Send(viewer, new TeamMemberInfoMessage { Identity = viewer.Identity, Unknown = 0, Member = member.Identity,
                        Unknown3 = data.MaxHealth, Unknown4 = data.MaxHealth,
                        Unknown5 = data.MaxNano, Unknown6 = data.MaxNano });
                SetStat(viewer, CharacterStat.SocialStatus, social, true);
            }
            SetStat(viewer, CharacterStat.Team, team.Id, true);
            ChatCommand(viewer.Identity.Instance, "#aorebirth-team-join " + team.Id);
            if (team.IsRaid)
            {
                Send(viewer, new RaidMessage { Identity = viewer.Identity, Unknown = 0, Unknown1 = 0 });
                ChatCommand(viewer.Identity.Instance, "#aorebirth-raid-convert " + team.Id);
            }
        }

        private void Announce(Player viewer, Player member, Team team)
        {
            if (!TryLevel(member, out int level)) return;
            MemberData data = _data[member.Identity.Instance];
            Send(viewer, new TeamMemberMessage { Identity = viewer.Identity, Unknown = 0, Member = member.Identity,
                Team = new Identity { Type = IdentityType.TeamWindow, Instance = team.Id }, Unknown4 = -1,
                Level = level, Unknown5 = (short)data.Profession, Name = data.Name });
        }

        private void ApplyStats(Player player, Team? team, int social, bool send)
        {
            // A newer projection may execute inline on the owner tick while an older remote
            // projection is still queued. Never allow that delayed projection to restore old UI/stats.
            _projectionEpochs[player.Identity.Instance] = _projectionEpochs.GetValueOrDefault(player.Identity.Instance) + 1;
            SetStat(player, CharacterStat.NumberOfTeamMembers, team?.Members.Count ?? 0, send);
            SetStat(player, CharacterStat.TeamSide, team == null ? 0 : 2, send);
            SetStat(player, CharacterStat.SocialStatus, social, send);
            SetStat(player, CharacterStat.Team, team?.Id ?? 0, send);
        }

        // Lifecycle callers are already on this actor's owner tick. Clear transient values before
        // the existing snapshot/despawn path; queued projections are invalidated by Remove/Detach.
        private static void ResetLocalStats(Player player)
        {
            player.Stats.Set(CharacterStat.NumberOfTeamMembers, 0);
            player.Stats.Set(CharacterStat.TeamSide, 0);
            player.Stats.Set(CharacterStat.SocialStatus, 4);
            player.Stats.Set(CharacterStat.Team, 0);
        }

        private void SetStat(Player player, CharacterStat stat, int value, bool send)
        {
            Dispatch(player, () =>
            {
                player.Stats.Set(stat, value);
                if (send) SendNow(player, new StatMessage { Identity = player.Identity, Stats =
                    [new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = (uint)value }] });
            });
        }

        private static CharacterActionMessage Action(Player viewer, CharacterActionType action, Identity target,
            int parameter1 = 0, int parameter2 = 0) => new()
            { Identity = viewer.Identity, Unknown = 0, Action = action, Target = target, Parameter1 = parameter1, Parameter2 = parameter2 };

        private bool Owns(Player player) => !_stopped && _players.TryGetValue(player.Identity.Instance, out Player? owner)
            && ReferenceEquals(player, owner);
        private bool IsActive(Player player) => Owns(player) && !player.IsPersistenceQuarantined
            && player.Session?.State == SessionState.InPlay && ReferenceEquals(player.Session.Player, player);
        private bool TryLevel(Player player, out int level)
        {
            MemberData data = _data[player.Identity.Instance];
            level = data.Level;
            return level is >= 1 and <= 220 && data.Profession is >= 0 and <= 15;
        }
        private void CaptureData(Player player)
        {
            bool hasVitals = player.Stats.TryGetValue(CharacterStat.MaxHealth, out int health) && health > 0;
            hasVitals &= player.Stats.TryGetValue(CharacterStat.MaxNanoEnergy, out int nano)
                && nano >= 0 && !StatCollection.IsUnset(nano);
            _data[player.Identity.Instance] = new MemberData(player.Name ?? player.FirstName,
                player.Stats.GetOrZero(CharacterStat.Level), player.Stats.GetOrZero(CharacterStat.Profession),
                health, nano, hasVitals, player.BuildInfoPacket());
        }
        private static Identity CharacterIdentity(int id) => new() { Type = IdentityType.CanbeAffected, Instance = id };
        private void CancelInvitations(int id)
        {
            foreach (int target in _invitations.Where(p => p.Key == id || p.Value.Inviter.Identity.Instance == id)
                .Select(p => p.Key).ToArray()) _invitations.Remove(target);
        }
        private void ChatCommand(int id, string command)
            => _chat?.TrySend(new ChatCommand { CharacterId = id, ChatCommandString = command });
        private void Send(Player player, MessageBody message) => Dispatch(player, () => SendNow(player, message));

        private void Dispatch(Player player, Action action)
        {
            IZoneSession? session = player.Session;
            long epoch = _projectionEpochs.GetValueOrDefault(player.Identity.Instance);
            void Fenced()
            {
                lock (_sync)
                {
                    if (!Owns(player) || !ReferenceEquals(player.Session, session)
                        || _projectionEpochs.GetValueOrDefault(player.Identity.Instance) != epoch) return;
                    action();
                }
            }
            if (_dispatchOnOwner != null) _dispatchOnOwner(player, Fenced);
            else if (player.Playfield == null) Fenced();
            else throw new InvalidOperationException("Team projections require the owning playfield dispatcher.");
        }

        private static void SendNow(Player player, MessageBody message)
        {
            if (player.Session?.State == SessionState.InPlay && ReferenceEquals(player.Session.Player, player))
                player.Session.Send(message);
        }
        private bool Error(Player player, string text)
        {
            Send(player, new ChatTextMessage { Identity = player.Identity, Text = text });
            return false;
        }
    }
}
