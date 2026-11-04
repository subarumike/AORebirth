namespace ZoneEngine_New.Core.Teams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    public sealed record TeamSnapshot(int TeamId, int LeaderId, IReadOnlyList<int> MemberIds, bool IsRaid);

    /// <summary>
    /// Teams for this zone instance's players, independent of playfield and transport ownership. With a World link,
    /// World owns membership (ids, members, leader, raid layout, invitations): every change goes to World, and this
    /// service mirrors each team that has a local member and speaks the client protocol for it. Without one (tests,
    /// tools) it is the process-local authority. Only the accepted Player object may act for its character.
    /// No team database or capture input. Wire fields follow the compiled Legacy TeamRuntime and current AOtomation
    /// contracts.
    /// </summary>
    public sealed partial class TeamService
    {
        private sealed class Team(int id, int leader)
        {
            public int Id { get; } = id;
            public int Leader = leader;
            public List<int> Members { get; } = [leader];
            public bool IsRaid;
            public bool ExplicitLeadershipTransfer;
            /// <summary>Per-member raid sub-team index; missing / default = -1 (capture 20260924-213512).</summary>
            public Dictionary<int, int> RaidTeamIndex { get; } = new();
        }

        private sealed record Invitation(Player Inviter, Player Invitee, IZoneSession InviterSession,
            IZoneSession InviteeSession, int TeamId);

        // Updated only from that player's attach/refresh/stat-change callback on its owner tick.
        // Team operations on another playfield never enumerate or read this player's mutable Stats.
        private sealed record MemberData(string Name, int Level, int Profession, int MaxHealth,
            int MaxNano, bool HasVitals);

        // Rows submitted through the accepted session on its owner tick, not queued projection intentions.
        private sealed class PublishedRoster(int teamId)
        {
            public int TeamId { get; } = teamId;
            public Dictionary<int, int> Members { get; } = new();
            public int Leader;
            public bool ExplicitLeadershipTransfer;
            public bool IsRaid;
            public bool ChatJoined;
            public bool RaidChatJoined;
        }

        private readonly object _sync = new();
        private readonly Dictionary<int, Player> _players = new();
        private readonly Dictionary<int, MemberData> _data = new();
        private readonly Dictionary<int, Action<CharacterStat, int, int, bool>> _statObservers = new();
        private readonly Dictionary<int, long> _projectionEpochs = new();
        private readonly Dictionary<int, PublishedRoster> _publishedRosters = new();
        private readonly Dictionary<int, IZoneSession> _rosterResetSessions = new();
        // A character has exactly one membership entry; Team.Members is its ordered projection.
        private readonly Dictionary<int, Team> _membership = new();
        private readonly Dictionary<int, Invitation> _invitations = new();
        private readonly Dictionary<int, DateTime> _declinedUntil = new();
        // Last playfield told to ChatEngine for its LFT location filter, captured on the owner tick.
        private readonly Dictionary<int, (int Playfield, int Expansion)> _lftLocations = new();
        private readonly IChatEngineLink? _chat;
        private readonly TeamLevelEligibility _eligibility;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<Player, Action>? _dispatchOnOwner;
        private readonly ZoneEngine_New.Core.World.WorldLink? _world;
        // World mode: one mirror object per team id, shared by every member entry in _membership.
        private readonly Dictionary<int, Team> _mirrors = new();
        // World mode: remote members' rows, from World (local members use _data from CaptureData).
        private readonly Dictionary<int, MemberData> _remoteData = new();
        private int _nextTeamId = 0x02800000;
        private bool _stopped;

        public TeamService(IChatEngineLink? chatLink = null, Func<DateTime>? utcNow = null,
            Action<Player, Action>? dispatchOnOwner = null, ZoneEngine_New.Core.World.WorldLink? world = null)
        {
            _eligibility = TeamLevelEligibility.Current;
            _chat = chatLink;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _dispatchOnOwner = dispatchOnOwner;
            _world = world;
            if (_world != null) _world.WorldMessageReceived += OnWorldMessage;
        }

        public void AttachPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (_sync)
            {
                if (_stopped) return;
                if (player.Identity.Type != IdentityType.CanbeAffected || player.Identity.Instance <= 0)
                    throw new ArgumentException("A team owner must be a character identity.", nameof(player));
                // Every arrival (login, zone change, reconnect) attaches, so this tracks the live playfield.
                PublishLftLocation(player);
                if (_players.TryGetValue(player.Identity.Instance, out Player? previous))
                {
                    if (ReferenceEquals(previous, player)) return;
                    Remove(previous, notifyLeaving: false);
                    CancelInvitations(previous.Identity.Instance);
                    previous.Stats.StatChanged -= _statObservers[player.Identity.Instance];
                }
                _players[player.Identity.Instance] = player;
                _publishedRosters.Remove(player.Identity.Instance);
                _rosterResetSessions.Remove(player.Identity.Instance);
                _projectionEpochs[player.Identity.Instance] = _projectionEpochs.GetValueOrDefault(player.Identity.Instance) + 1;
                CaptureData(player);
                Action<CharacterStat, int, int, bool> observer = (stat, _, _, _) =>
                {
                    if (stat is not (CharacterStat.Level or CharacterStat.Profession or CharacterStat.MaxHealth
                        or CharacterStat.MaxNanoEnergy)) return;
                    lock (_sync) { if (Owns(player)) CaptureData(player); }
                };
                _statObservers[player.Identity.Instance] = observer;
                player.Stats.StatChanged += observer;
                // Persisted stat values never reconstitute a process-local team.
                ResetLocalStats(player);
                if (_world != null)
                {
                    // Others see this player's card soon; World sends its team here if it is in one.
                    _world.SyncPresenceSoon();
                    WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamSync);
                }
            }
        }

        /// <summary>
        /// Arriving from another zone instance with a team: mirror it before the first FullCharacter so the client's
        /// team stats stay as they were, and treat the roster the client already shows as published.
        /// </summary>
        public void ApplyArrivedTeam(Player player, AORebirth.World.Contracts.V1.TeamState team)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(team);
            lock (_sync)
            {
                if (_world == null || !Owns(player) || team.TeamId <= 0) return;
                Team mirror = Mirror(team);
                if (!_membership.ContainsKey(player.Identity.Instance)) return;
                var published = new PublishedRoster(mirror.Id)
                {
                    Leader = mirror.Leader,
                    ExplicitLeadershipTransfer = mirror.ExplicitLeadershipTransfer,
                    IsRaid = mirror.IsRaid,
                    ChatJoined = true,
                    RaidChatJoined = mirror.IsRaid
                };
                foreach (int id in mirror.Members) published.Members[id] = mirror.IsRaid ? RaidListOf(mirror, id) : -1;
                _publishedRosters[player.Identity.Instance] = published;
                int viewerId = player.Identity.Instance;
                int social = viewerId == mirror.Leader
                    ? (mirror.ExplicitLeadershipTransfer ? 15 : 7)
                    : (mirror.ExplicitLeadershipTransfer ? 13 : 5);
                ApplyStatsNow(player, mirror, social, send: false);
            }
        }

        /// <summary>World mode: what the source of a cross-instance move tells the destination about the team.</summary>
        public AORebirth.World.Contracts.V1.TeamState? SnapshotTeamState(Player player)
        {
            lock (_sync)
            {
                if (!Owns(player) || !_membership.TryGetValue(player.Identity.Instance, out Team? team)) return null;
                var state = new AORebirth.World.Contracts.V1.TeamState
                {
                    TeamId = team.Id,
                    Leader = team.Leader,
                    Raid = team.IsRaid,
                    ExplicitLeader = team.ExplicitLeadershipTransfer
                };
                foreach (int id in team.Members)
                {
                    MemberData? data = DataOf(id);
                    state.Members.Add(new AORebirth.World.Contracts.V1.TeamMember
                    {
                        CharacterId = id,
                        RaidTeam = team.RaidTeamIndex.GetValueOrDefault(id, -1),
                        Card = data == null ? new AORebirth.World.Contracts.V1.PlayerCard { CharacterId = id } : new AORebirth.World.Contracts.V1.PlayerCard
                        {
                            CharacterId = id,
                            Name = data.Name,
                            Level = data.Level,
                            Profession = data.Profession,
                            MaxHealth = data.MaxHealth,
                            MaxNano = data.MaxNano,
                            HasVitals = data.HasVitals
                        }
                    });
                }

                return state;
            }
        }

        /// <summary>The player moved to another zone instance: its team goes on there; only local tracking ends here.</summary>
        public void DetachForTransfer(Player player)
        {
            lock (_sync)
            {
                if (_world == null) { DetachPlayerCore(player); return; }
                if (!Owns(player)) return;
                int id = player.Identity.Instance;
                _membership.TryGetValue(id, out Team? team);
                ForgetLocal(player);
                if (team != null) PruneMirror(team);
            }
        }

        public void RefreshPlayer(Player player, bool preserveRoster = false)
        {
            lock (_sync)
            {
                if (!IsActive(player)) return;
                CaptureData(player);
                if (!preserveRoster)
                {
                    _rosterResetSessions[player.Identity.Instance] = player.Session!;
                }
                else
                {
                    _rosterResetSessions.Remove(player.Identity.Instance);
                }
                QueueRoster(player, refreshVitals: !preserveRoster);
            }
        }

        public void OnTransportDisconnected(Player player, IZoneSession disconnectedSession)
        {
            lock (_sync)
            {
                // Zoning clears Player.Session before transport closure. Superseded transport callbacks
                // must also be ignored; neither is the accepted Legacy leave-game transition.
                if (!Owns(player) || !ReferenceEquals(player.Session, disconnectedSession)) return;
                // A LinkDead member keeps its place: a reconnect resends the roster (RefreshPlayer),
                // and only the LinkDead despawn (DetachPlayer) takes it out of the team.
                if (_world != null) { WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamDisconnect); return; }
                CancelInvitations(player.Identity.Instance);
            }
        }

        public void DetachPlayer(Player player)
        {
            lock (_sync) DetachPlayerCore(player);
        }

        private void DetachPlayerCore(Player player)
        {
            if (!Owns(player)) return;
            if (_world != null)
            {
                // Logging out for good: World takes it out of its team and tells the others.
                WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamLogout);
                _membership.TryGetValue(player.Identity.Instance, out Team? team);
                ResetLocalStats(player);
                ForgetLocal(player);
                if (team != null) PruneMirror(team);
                return;
            }

            CancelInvitations(player.Identity.Instance);
            Remove(player, notifyLeaving: false);
            ResetLocalStats(player);
            ForgetLocal(player);
        }

        private void ForgetLocal(Player player)
        {
            int id = player.Identity.Instance;
            player.Stats.StatChanged -= _statObservers[id];
            _statObservers.Remove(id);
            _players.Remove(id);
            _data.Remove(id);
            _projectionEpochs.Remove(id);
            _publishedRosters.Remove(id);
            _rosterResetSessions.Remove(id);
            _declinedUntil.Remove(id);
            _lftLocations.Remove(id);
        }

        public void Shutdown()
        {
            lock (_sync)
            {
                if (_stopped) return;
                // With World, teams outlive this instance; its players already logged out one by one.
                if (_world == null)
                    foreach (Player player in _players.Values.ToArray()) Remove(player, notifyLeaving: true);
                foreach (Player player in _players.Values)
                    player.Stats.StatChanged -= _statObservers[player.Identity.Instance];
                _statObservers.Clear();
                _data.Clear();
                _projectionEpochs.Clear();
                _publishedRosters.Clear();
                _rosterResetSessions.Clear();
                _invitations.Clear();
                _declinedUntil.Clear();
                _lftLocations.Clear();
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

        /// <summary>The player's team id and whether it leads it, without copying the roster; false when not in a team.</summary>
        public bool TryGetTeamId(Player player, out int teamId, out bool isLeader)
        {
            lock (_sync)
            {
                if (Owns(player) && _membership.TryGetValue(player.Identity.Instance, out Team? team))
                {
                    teamId = team.Id;
                    isLeader = team.Leader == player.Identity.Instance;
                    return true;
                }
            }

            teamId = 0;
            isLeader = false;
            return false;
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
                if (_world != null)
                {
                    HandleWithWorld(player, message);
                    return true;
                }

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
                if (_world != null)
                {
                    switch (args[1].ToLowerInvariant())
                    {
                        case "accept": WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamAccept); return true;
                        case "decline":
                        case "reject": WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamDecline); return true;
                        case "leave": WorldCommand(player, AORebirth.World.Contracts.V1.TeamOp.TeamLeave); return true;
                        case "invite" when args.Length == 3: WorldInvite(player, 0, args[2], confirmed: false); return true;
                        default: return Error(player, "Unknown team command.");
                    }
                }

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
                if (_world != null) { WorldCommand(leader, AORebirth.World.Contracts.V1.TeamOp.TeamConvertRaid); return true; }
                if (!_membership.TryGetValue(leader.Identity.Instance, out Team? team))
                    return Feedback(leader, "Feedback_YouAreNotMemberOfTeam");
                if (team.Leader != leader.Identity.Instance)
                    return Feedback(leader, "Feedback_NeedToBeTeamLeader");
                if (team.IsRaid)
                    return Error(leader, "Your team is already a raid.");
                if (team.Members.Count < 2)
                    return Error(leader, "You need at least one other team member to convert to raid.");

                team.IsRaid = true;
                foreach (int id in team.Members)
                {
                    QueueRoster(_players[id]);
                }

                Send(leader, ClientFeedback.Create(leader.Identity, "Feedback_RaidCreated"));
                return true;
            }
        }

        /// <summary>
        /// Capture-backed RaidCmd: Command=1 convert (20260902-073932);
        /// Command=4 move member TargetCharacterId → DestinationTeamIndex (20260924-213512).
        /// </summary>
        public bool TryHandleRaidCmd(Player player, RaidCmdMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            return message.Command switch
            {
                1 => ConvertToRaid(player),
                4 => MoveRaidMember(player, message.TargetCharacterId, message.DestinationTeamIndex),
                _ => Error(player, "Unsupported raid command.")
            };
        }

        /// <summary>
        /// Capture 20260924-213512: OUT RaidCmd Command=4 Target=Zizion DestTeam=1 →
        /// IN TeamMemberInfo, SocialStatus, TeamMemberLeft(param2=0), TeamMember(Unknown4=1),
        /// TeamMemberInfo, SocialStatus for each raid viewer.
        /// </summary>
        public bool MoveRaidMember(Player leader, int targetCharacterId, int destinationTeamIndex)
        {
            lock (_sync)
            {
                if (!IsActive(leader)) return false;
                if (_world != null)
                {
                    WorldCommand(leader, AORebirth.World.Contracts.V1.TeamOp.TeamMoveRaidMember, targetCharacterId, destinationTeamIndex);
                    return true;
                }

                if (!_membership.TryGetValue(leader.Identity.Instance, out Team? team))
                    return Feedback(leader, "Feedback_YouAreNotMemberOfTeam");
                if (!team.IsRaid)
                    return Error(leader, "Your team is not a raid.");
                if (team.Leader != leader.Identity.Instance)
                    return Feedback(leader, "Feedback_NeedToBeTeamLeader");
                if (targetCharacterId == team.Leader)
                    return Error(leader, "The raid leader cannot be moved to another team.");
                if (!_players.TryGetValue(targetCharacterId, out Player? target) || !IsActive(target)
                    || !_membership.TryGetValue(targetCharacterId, out Team? targetTeam)
                    || !ReferenceEquals(targetTeam, team))
                    return Error(leader, "That player is not in your raid.");

                team.RaidTeamIndex[targetCharacterId] = destinationTeamIndex;
                foreach (int viewerId in team.Members)
                {
                    QueueRoster(_players[viewerId]);
                }
                return true;
            }
        }

        private bool Invite(Player inviter, Identity targetIdentity, bool confirmedRange)
        {
            if (targetIdentity.Type != IdentityType.CanbeAffected || targetIdentity.Instance <= 0
                || !_players.TryGetValue(targetIdentity.Instance, out Player? target) || !IsActive(target)
                || ReferenceEquals(target, inviter)) return Error(inviter, "Team invite target is not available.");
            if (_membership.ContainsKey(target.Identity.Instance)) return Feedback(inviter, "Feedback_AlreadyTeamMember");
            _membership.TryGetValue(inviter.Identity.Instance, out Team? team);
            if (_invitations.ContainsKey(target.Identity.Instance))
                return Error(inviter, "Team invite already pending for " + target.Name + ".");
            if (_declinedUntil.TryGetValue(target.Identity.Instance, out DateTime until) && _utcNow() < until)
                return Error(inviter, "That player declined recently. Wait a moment before inviting again.");
            if (!TryLevel(target, out int targetLevel)) return Error(inviter, "Team invite target level is unavailable.");
            if (!confirmedRange)
            {
                if (TryFindLevelConflict(
                        inviter,
                        team,
                        targetLevel,
                        out Player? conflictMember,
                        out bool tooHigh))
                {
                    string conflictName = conflictMember != null && !string.IsNullOrEmpty(conflictMember.Name)
                        ? conflictMember.Name
                        : "the team";
                    if (tooHigh)
                    {
                        Send(inviter, Action(inviter, CharacterActionType.TeamInviteAck, target.Identity));
                        Send(inviter, new ChatTextMessage
                        {
                            Identity = inviter.Identity,
                            Text = target.Name + " is too high for " + conflictName + "."
                        });
                    }
                    else
                    {
                        Send(inviter, Action(inviter, CharacterActionType.TeamInviteTooLow, target.Identity));
                        Send(inviter, new ChatTextMessage
                        {
                            Identity = inviter.Identity,
                            Text = target.Name + " is too low for " + conflictName + "."
                        });
                    }

                    return true;
                }
            }

            _invitations[target.Identity.Instance] = new Invitation(inviter, target,
                inviter.Session!, target.Session!, team?.Id ?? 0);
            // Real name+level for Recruit/LFT — never invent levels (avoids fake too high/low).
            WireInvitePresence(inviter, target, targetLevel);
            if (!ReferenceEquals(inviter.Playfield, target.Playfield))
                Send(target, new TeamInviteMessage { Identity = target.Identity, Unknown = 1,
                    Inviter = inviter.Identity, Name = _data[inviter.Identity.Instance].Name });
            Send(target, Action(target, CharacterActionType.TeamRequestInvite, inviter.Identity));
            return true;
        }

        /// <summary>
        /// Invitee must be in XP range of every current team member (solo = inviter).
        /// TooHigh uses the lowest conflicting member; TooLow uses the highest.
        /// </summary>
        bool TryFindLevelConflict(
            Player inviter,
            Team? team,
            int inviteeLevel,
            out Player? conflictMember,
            out bool tooHigh)
        {
            conflictMember = null;
            tooHigh = false;

            Player? tooHighMember = null;
            int tooHighMemberLevel = int.MaxValue;
            Player? tooLowMember = null;
            int tooLowMemberLevel = int.MinValue;

            foreach (int id in team?.Members ?? [inviter.Identity.Instance])
            {
                if (!_players.TryGetValue(id, out Player? member) || !IsActive(member))
                    continue;
                if (!TryLevel(member, out int memberLevel))
                    continue;

                if (_eligibility.IsTooHighForMember(memberLevel, inviteeLevel))
                {
                    if (memberLevel < tooHighMemberLevel)
                    {
                        tooHighMember = member;
                        tooHighMemberLevel = memberLevel;
                    }
                }
                else if (_eligibility.IsTooLowForMember(memberLevel, inviteeLevel))
                {
                    if (memberLevel > tooLowMemberLevel)
                    {
                        tooLowMember = member;
                        tooLowMemberLevel = memberLevel;
                    }
                }
            }

            if (tooHighMember != null)
            {
                conflictMember = tooHighMember;
                tooHigh = true;
                return true;
            }

            if (tooLowMember != null)
            {
                conflictMember = tooLowMember;
                tooHigh = false;
                return true;
            }

            return false;
        }

        void WireInvitePresence(Player viewer, Player subject, int subjectLevel)
        {
            Send(viewer, subject.BuildInfoPacket());
            Send(viewer, new StatMessage
            {
                Identity = subject.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = CharacterStat.Level,
                        Value2 = (uint)Math.Clamp(subjectLevel, 1, 220)
                    }
                ]
            });
        }

        /// <summary>
        /// ChatEngine LFT search → seed real name+level for matches (anti "NoName is too high").
        /// No off-map SCFU ghosts — InfoPacket + Level StatMessage only.
        /// </summary>
        public bool TryHandleInboundChatCommand(int characterId, string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;

            string text = command.Trim();
            if (text.Equals("#aorebirth-lft-remove", StringComparison.OrdinalIgnoreCase))
                return true;

            // Chat (re)connected after the roster's team join was sent; rejoin its team/raid channel.
            if (text.Equals("#aorebirth-chat-online", StringComparison.Ordinal))
            {
                lock (_sync)
                {
                    if (!_stopped && _membership.TryGetValue(characterId, out Team? team))
                    {
                        ChatCommand(characterId, "#aorebirth-team-join " + team.Id);
                        if (team.IsRaid) ChatCommand(characterId, "#aorebirth-raid-convert " + team.Id);
                    }
                }
                return true;
            }

            const string seedPrefix = "#aorebirth-lft-seed";
            if (!text.StartsWith(seedPrefix, StringComparison.Ordinal))
                return false;

            lock (_sync)
            {
                if (_stopped || !_players.TryGetValue(characterId, out Player? searcher) || !IsActive(searcher))
                    return true;

                // ChatEngine resolves "This playfield" / RK / SL right after this; it may have restarted since
                // the arrivals, so repeat the searcher's and each candidate's location.
                SendLftLocation(characterId);
                string[] tokens = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < tokens.Length; i++)
                {
                    string token = tokens[i];
                    int colon = token.IndexOf(':');
                    string idPart = colon >= 0 ? token[..colon] : token;
                    if (!int.TryParse(idPart, out int candidateId) || candidateId == 0
                        || candidateId == characterId)
                        continue;
                    if (!_players.TryGetValue(candidateId, out Player? candidate) || !IsActive(candidate))
                        continue;
                    SendLftLocation(candidateId);
                    if (!TryLevel(candidate, out int level))
                        continue;
                    WireInvitePresence(searcher, candidate, level);
                }
            }

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
            bool created = team == null;
            if (team == null)
            {
                if (_nextTeamId > int.MaxValue - RaidTeamLists) return Error(invitee, "Team identity space is exhausted.");
                team = new Team(_nextTeamId + 1, inviter.Identity.Instance);
                _nextTeamId += RaidTeamLists;
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
            foreach (int id in team.Members)
            {
                QueueRoster(_players[id]);
            }
            return true;
        }

        private bool Kick(Player leader, Identity target)
        {
            if (target.Type != IdentityType.CanbeAffected || target == leader.Identity
                || !_membership.TryGetValue(leader.Identity.Instance, out Team? team)
                || team.Leader != leader.Identity.Instance || !team.Members.Contains(target.Instance))
                return Feedback(leader, "Feedback_NeedToBeTeamLeader");
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
            if (team.IsRaid && team.RaidTeamIndex.GetValueOrDefault(target.Instance, 0) > 0)
                return Error(leader, "Only a member of Team 1 can be promoted to raid leader.");
            team.Leader = target.Instance;
            team.ExplicitLeadershipTransfer = true;
            foreach (int id in team.Members)
            {
                QueueRoster(_players[id]);
            }
            return true;
        }

        private bool Remove(Player player, bool notifyLeaving)
        {
            if (!_membership.Remove(player.Identity.Instance, out Team? team))
            {
                QueueRoster(player, player.Identity.Instance, notifyLeaving);
                return true;
            }
            team.Members.Remove(player.Identity.Instance);
            team.RaidTeamIndex.Remove(player.Identity.Instance);
            LeaveChat(player.Identity.Instance, team.Id);
            QueueRoster(player, player.Identity.Instance, notifyLeaving);
            if (team.Members.Count == 1)
            {
                Player last = _players[team.Members[0]];
                _membership.Remove(last.Identity.Instance);
                team.Members.Clear();
                CancelInvitations(last.Identity.Instance);
                QueueRoster(last, player.Identity.Instance);
                LeaveChat(last.Identity.Instance, team.Id);
            }
            else if (team.Members.Count > 1)
            {
                if (team.Leader == player.Identity.Instance) team.Leader = team.Members[0];
                team.ExplicitLeadershipTransfer = false;
                foreach (int id in team.Members)
                {
                    QueueRoster(_players[id], player.Identity.Instance);
                }
            }
            return true;
        }

        private void QueueRoster(Player viewer, int? departingMember = null, bool notifyLeaving = true,
            bool refreshVitals = false)
        {
            _projectionEpochs[viewer.Identity.Instance] = _projectionEpochs.GetValueOrDefault(viewer.Identity.Instance) + 1;
            Dispatch(viewer, () => PublishRosterNow(viewer, departingMember, notifyLeaving, refreshVitals));
        }

        private void PublishRosterNow(Player viewer, int? departingMember, bool notifyLeaving, bool refreshVitals)
        {
            int viewerId = viewer.Identity.Instance;
            _membership.TryGetValue(viewerId, out Team? team);
            int social = team == null ? 4 : viewerId == team.Leader
                ? (team.ExplicitLeadershipTransfer ? 15 : 7)
                : (team.ExplicitLeadershipTransfer ? 13 : 5);
            if (!IsActive(viewer))
            {
                ApplyStatsNow(viewer, team, social, send: false);
                return;
            }
            if (_rosterResetSessions.TryGetValue(viewerId, out IZoneSession? resetSession)
                && ReferenceEquals(resetSession, viewer.Session))
            {
                _publishedRosters.Remove(viewerId);
                _rosterResetSessions.Remove(viewerId);
            }
            _publishedRosters.TryGetValue(viewerId, out PublishedRoster? published);
            if (published != null && published.TeamId != (team?.Id ?? 0))
            {
                // A surviving member sees departed rows before the final self-leave on disband.
                if (departingMember != viewerId)
                {
                    foreach (int memberId in published.Members.Keys.Where(id => id != viewerId).ToArray())
                    {
                        if (!RemovePublishedMemberNow(viewer, published, memberId)) return;
                    }
                }
                ApplyStatsNow(viewer, null, 4, send: notifyLeaving);
                if (!notifyLeaving || !RemovePublishedMemberNow(viewer, published, viewerId)) return;
                _publishedRosters.Remove(viewerId);
                published = null;
                if (team == null) return;
            }
            if (team == null)
            {
                ApplyStatsNow(viewer, null, 4, send: notifyLeaving);
                return;
            }

            bool raidRosterReset = published is { IsRaid: true } && team.IsRaid
                && published.Members.TryGetValue(viewerId, out int previousRaidIndex)
                && previousRaidIndex != RaidListOf(team, viewerId);
            if (raidRosterReset)
            {
                // Live moved-viewer capture 20261104T005758Z resets SocialStatus before the self-leave.
                SetStatNow(viewer, CharacterStat.SocialStatus, 4, true);
                SetStatNow(viewer, CharacterStat.SocialStatus, 4, true);
                // Original client Gamecode clears all six roster groups on a self-removal, including a raid move.
                if (!RemovePublishedMemberNow(viewer, published!, viewerId)) return;
                published!.Members.Clear();
            }

            bool initialRoster = published == null;
            bool membershipChanged = published == null || published.Members.Count != team.Members.Count
                || team.Members.Any(id => !published.Members.ContainsKey(id));
            bool leadershipChanged = published == null || published.Leader != team.Leader
                || published.ExplicitLeadershipTransfer != team.ExplicitLeadershipTransfer;

            if (published != null)
            {
                foreach (int memberId in published.Members.Keys.Where(id => !team.Members.Contains(id)).ToArray())
                {
                    if (!RemovePublishedMemberNow(viewer, published, memberId)) return;
                }
            }

            ApplyStatsNow(viewer, team, social, send: !initialRoster && !raidRosterReset
                && (membershipChanged || leadershipChanged || refreshVitals));
            if (raidRosterReset)
            {
                // Live lets TeamMember set the new team identity; it sends no preceding Team stat or leader reply.
                SetStatNow(viewer, CharacterStat.TeamSide, 2, true);
                SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                // Original RaidView ignores joins until the viewer's own row restores raid membership.
                if (!AnnounceNow(viewer, viewerId, team)) return;
            }
            if (initialRoster)
            {
                // Preserve the captured initial handshake; existing viewers receive only row deltas.
                SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                SetStatNow(viewer, CharacterStat.TeamSide, 2, true);
                SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                if (viewerId == team.Leader)
                {
                    if (!SendNow(viewer, Action(viewer, CharacterActionType.TeamRequestReply, Identity.None, 0, 17))) return;
                    SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                }
                if (TryLevel(viewer, out _) && !AnnounceNow(viewer, viewerId, team)) return;
                _publishedRosters.TryGetValue(viewerId, out published);
                if (viewerId == team.Leader)
                {
                    if (!SendNow(viewer, Action(viewer, CharacterActionType.AcceptTeamRequest, viewer.Identity,
                        (int)IdentityType.TeamWindow, team.Id))) return;
                    SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                }
            }
            else if (leadershipChanged && !raidRosterReset)
            {
                if (team.ExplicitLeadershipTransfer)
                {
                    if (!SendNow(viewer, Action(viewer, CharacterActionType.AcceptTeamRequest, CharacterIdentity(team.Leader),
                        (int)IdentityType.TeamWindow, team.Id))) return;
                }
                else if (viewerId == team.Leader)
                {
                    if (!SendNow(viewer, Action(viewer, CharacterActionType.TeamRequestReply, Identity.None, 0, 17))) return;
                    if (!SendNow(viewer, Action(viewer, CharacterActionType.AcceptTeamRequest, viewer.Identity,
                        (int)IdentityType.TeamWindow, team.Id))) return;
                }
            }
            // Established viewers must enter raid mode before an accumulated subgroup reassignment.
            if (!initialRoster && team.IsRaid && !published!.IsRaid)
            {
                if (!SendNow(viewer, new RaidMessage { Identity = viewer.Identity, Unknown = 0, Unknown1 = 0 })) return;
                published.IsRaid = true;
                // Original RaidIIR converts the existing ordinary-team rows to raid group zero in place.
                foreach (int id in published.Members.Keys.ToArray())
                    published.Members[id] = Math.Max(0, published.Members[id]);
            }
            foreach (int id in team.Members)
            {
                if (initialRoster && id == viewerId) continue;
                int raidIndex = team.IsRaid ? RaidListOf(team, id) : -1;
                bool hasRow = published != null && published.Members.TryGetValue(id, out _);
                if (hasRow && published!.Members[id] != raidIndex)
                {
                    PublishRaidMemberMoveNow(viewer, id, team);
                    continue;
                }
                if (hasRow && !initialRoster && !refreshVitals) continue;
                if (!hasRow && !TryLevel(id, out _)) continue;
                if (id != viewerId)
                {
                    // Live moved-viewer capture 20261104T005758Z: visible peer vitals, SocialStatus, then its row.
                    // The cross-zone part omits vitals; the original receiver requires the peer's dynel to exist.
                    MemberData? data = DataOf(id);
                    if (data is { HasVitals: true } && IsMemberVisibleTo(viewer, id))
                    {
                        SendNow(viewer, new TeamMemberInfoMessage { Identity = viewer.Identity, Unknown = 0, Member = CharacterIdentity(id),
                            Unknown3 = data.MaxNano, Unknown4 = data.MaxNano,
                            Unknown5 = data.MaxHealth, Unknown6 = data.MaxHealth });
                    }
                    SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
                }
                if (!AnnounceNow(viewer, id, team)) return;
                _publishedRosters.TryGetValue(viewerId, out published);
            }
            if (!IsActive(viewer)) return;
            if (initialRoster)
            {
                SetStatNow(viewer, CharacterStat.Team, ClientTeamId(team, viewerId), true);
            }
            if (published == null) return;
            published.Leader = team.Leader;
            published.ExplicitLeadershipTransfer = team.ExplicitLeadershipTransfer;
            if (!published.ChatJoined)
            {
                ChatCommand(viewerId, "#aorebirth-team-join " + team.Id);
                published.ChatJoined = true;
            }
            if (team.IsRaid && !published.IsRaid
                && SendNow(viewer, new RaidMessage { Identity = viewer.Identity, Unknown = 0, Unknown1 = 0 }))
            {
                published.IsRaid = true;
            }
            if (published.IsRaid && !published.RaidChatJoined)
            {
                ChatCommand(viewerId, "#aorebirth-raid-convert " + team.Id);
                published.RaidChatJoined = true;
            }
        }

        private bool AnnounceNow(Player viewer, int memberId, Team team)
        {
            int viewerId = viewer.Identity.Instance;
            _publishedRosters.TryGetValue(viewerId, out PublishedRoster? published);
            if (published?.TeamId == team.Id && published.Members.ContainsKey(memberId)) return true;
            if (!TryLevel(memberId, out int level)) return false;
            MemberData data = DataOf(memberId)!;
            // Original RaidView interprets -1 as the viewer's current subgroup when handling a join signal.
            int raidTeamIndex = team.IsRaid ? RaidListOf(team, memberId) : -1;
            if (!SendNow(viewer, new TeamMemberMessage { Identity = viewer.Identity, Unknown = 0, Member = CharacterIdentity(memberId),
                Team = new Identity { Type = IdentityType.TeamWindow, Instance = ClientTeamId(team, memberId) }, Unknown4 = raidTeamIndex,
                Level = level, Unknown5 = (short)data.Profession, Name = data.Name })) return false;
            if (published == null)
            {
                published = new PublishedRoster(team.Id);
                _publishedRosters[viewerId] = published;
            }
            published.Members[memberId] = raidTeamIndex;
            return true;
        }

        private bool RemovePublishedMemberNow(Player viewer, PublishedRoster published, int memberId)
        {
            // Original client CharacterAction 0x20 uses Parameter2 to index the OLD subgroup, not as a reason code.
            // Conversion puts the original team in raid group zero; its previously announced rows still have -1.
            int raidTeamIndex = published.IsRaid ? Math.Max(0, published.Members.GetValueOrDefault(memberId, -1)) : -1;
            if (!SendNow(viewer, Action(viewer, CharacterActionType.TeamMemberLeft, CharacterIdentity(memberId),
                published.TeamId + (published.IsRaid ? raidTeamIndex : 0), raidTeamIndex))) return false;
            published.Members.Remove(memberId);
            return true;
        }

        private void PublishRaidMemberMoveNow(Player viewer, int targetId, Team team)
        {
            if (!IsActive(viewer)) return;
            int social = viewer.Identity.Instance == team.Leader
                ? (team.ExplicitLeadershipTransfer ? 15 : 7)
                : (team.ExplicitLeadershipTransfer ? 13 : 5);
            MemberData? data = DataOf(targetId);
            if (data == null) return;
            Identity target = CharacterIdentity(targetId);
            bool sendVitals = data.HasVitals && IsMemberVisibleTo(viewer, targetId);
            if (sendVitals)
                SendNow(viewer, new TeamMemberInfoMessage { Identity = viewer.Identity, Unknown = 0, Member = target,
                    Unknown3 = data.MaxNano, Unknown4 = data.MaxNano,
                    Unknown5 = data.MaxHealth, Unknown6 = data.MaxHealth });
            SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
            // The captured move left group zero; later moves must remove from the member's actual old group.
            if (!RemovePublishedMemberNow(viewer, _publishedRosters[viewer.Identity.Instance], targetId)) return;
            if (!AnnounceNow(viewer, targetId, team)) return;
            if (sendVitals)
                SendNow(viewer, new TeamMemberInfoMessage { Identity = viewer.Identity, Unknown = 0, Member = target,
                    Unknown3 = data.MaxNano, Unknown4 = data.MaxNano,
                    Unknown5 = data.MaxHealth, Unknown6 = data.MaxHealth });
            SetStatNow(viewer, CharacterStat.SocialStatus, social, true);
        }

        private static void ApplyStatsNow(Player player, Team? team, int social, bool send)
        {
            SetStatNow(player, CharacterStat.NumberOfTeamMembers, team?.Members.Count ?? 0, send);
            SetStatNow(player, CharacterStat.TeamSide, team == null ? 0 : 2, send);
            SetStatNow(player, CharacterStat.SocialStatus, social, send);
            SetStatNow(player, CharacterStat.Team, team == null ? 0 : ClientTeamId(team, player.Identity.Instance), send);
        }

        // Live capture 20261104T005758Z: raid group N has team instance base + N, including remote members.
        private static int ClientTeamId(Team team, int memberId) => team.Id + (team.IsRaid ? RaidListOf(team, memberId) : 0);

        private bool IsMemberVisibleTo(Player viewer, int memberId)
            => _players.TryGetValue(memberId, out Player? member)
                && viewer.Playfield?.GetService<PlayfieldLocality>()?.IsVisibleTo(member, viewer) == true;

        // Lifecycle callers are already on this actor's owner tick. Clear transient values before
        // the existing snapshot/despawn path; queued projections are invalidated by Remove/Detach.
        private static void ResetLocalStats(Player player)
        {
            player.Stats.Set(CharacterStat.NumberOfTeamMembers, 0);
            player.Stats.Set(CharacterStat.TeamSide, 0);
            player.Stats.Set(CharacterStat.SocialStatus, 4);
            player.Stats.Set(CharacterStat.Team, 0);
        }

        private static void SetStatNow(Player player, CharacterStat stat, int value, bool send)
        {
            player.Stats.Set(stat, value);
            if (send) SendNow(player, new StatMessage { Identity = player.Identity, Stats =
                [new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = (uint)value }] });
        }

        private static CharacterActionMessage Action(Player viewer, CharacterActionType action, Identity target,
            int parameter1 = 0, int parameter2 = 0) => new()
            { Identity = viewer.Identity, Unknown = 0, Action = action, Target = target, Parameter1 = parameter1, Parameter2 = parameter2 };

        private bool Owns(Player player) => !_stopped && _players.TryGetValue(player.Identity.Instance, out Player? owner)
            && ReferenceEquals(player, owner);
        private bool IsActive(Player player) => Owns(player) && !player.IsPersistenceQuarantined
            && player.Session?.State == SessionState.InPlay && ReferenceEquals(player.Session.Player, player);
        private bool TryLevel(Player player, out int level) => TryLevel(player.Identity.Instance, out level);

        private bool TryLevel(int characterId, out int level)
        {
            level = 0;
            if (DataOf(characterId) is not { } data) return false;
            level = data.Level;
            return level is >= 1 and <= 220 && data.Profession is >= 0 and <= 15;
        }

        /// <summary>A local member's captured row, else the row World sent for a member on another instance.</summary>
        private MemberData? DataOf(int characterId)
            => _data.TryGetValue(characterId, out MemberData? local) ? local
                : _remoteData.TryGetValue(characterId, out MemberData? remote) ? remote : null;
        private void CaptureData(Player player)
        {
            bool hasVitals = player.Stats.TryGetValue(CharacterStat.MaxHealth, out int health) && health > 0;
            hasVitals &= player.Stats.TryGetValue(CharacterStat.MaxNanoEnergy, out int nano)
                && nano >= 0 && !StatCollection.IsUnset(nano);
            _data[player.Identity.Instance] = new MemberData(player.Name ?? player.FirstName,
                player.Stats.GetOrZero(CharacterStat.Level), player.Stats.GetOrZero(CharacterStat.Profession),
                health, nano, hasVitals);
        }
        private static Identity CharacterIdentity(int id) => new() { Type = IdentityType.CanbeAffected, Instance = id };
        private void CancelInvitations(int id)
        {
            foreach (int target in _invitations.Where(p => p.Key == id || p.Value.Inviter.Identity.Instance == id)
                .Select(p => p.Key).ToArray()) _invitations.Remove(target);
        }
        private void PublishLftLocation(Player player)
        {
            if (player.Playfield is not { } playfield || playfield.Identity.Instance <= 0) return;
            // ChatEngine.Lists.LftPlayfieldRegistry: expansion 1 = Rubi-Ka, 2 = Shadowlands.
            int expansion = PlayfieldEntryFlags.ExpansionPlayfield(playfield.MetaData) == 1 ? 2 : 1;
            // Other players see this in LFT: the playfield the client knows, never a proxy instance's own id.
            var location = (playfield.ClientPlayfieldId, expansion);
            int id = player.Identity.Instance;
            if (_lftLocations.TryGetValue(id, out var sent) && sent == location) return;
            _lftLocations[id] = location;
            SendLftLocation(id);
        }
        private void SendLftLocation(int id)
        {
            if (_lftLocations.TryGetValue(id, out var location))
                ChatCommand(id, "#aorebirth-pf " + location.Playfield + " " + location.Expansion);
        }
        private void ChatCommand(int id, string command)
            => _chat?.SendChatCommand(id, command);
        private void LeaveChat(int id, int teamId)
        {
            ChatCommand(id, "#aorebirth-team-leave " + teamId);
            if (_publishedRosters.TryGetValue(id, out PublishedRoster? published) && published.TeamId == teamId)
            {
                published.ChatJoined = false;
                published.RaidChatJoined = false;
            }
        }
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

        private static bool SendNow(Player player, MessageBody message)
        {
            if (player.Session?.State == SessionState.InPlay && ReferenceEquals(player.Session.Player, player))
            {
                player.Session.Send(message);
                return true;
            }
            return false;
        }
        private bool Error(Player player, string text)
        {
            Send(player, new ChatTextMessage { Identity = player.Identity, Text = text });
            return false;
        }

        private bool Feedback(Player player, string key)
        {
            Send(player, ClientFeedback.Create(player.Identity, key));
            return false;
        }
    }
}
