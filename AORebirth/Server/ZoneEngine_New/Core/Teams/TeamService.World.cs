namespace ZoneEngine_New.Core.Teams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using AORebirth.World.Contracts.V1;

    using Grpc.Core;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    using Identity = SmokeLounge.AOtomation.Messaging.GameData.Identity;

    /// <summary>
    /// World mode: every team change is a World command; World pushes each team's new state to every instance with a
    /// member, and this instance turns it into the same client messages the process-local path sends.
    /// </summary>
    public sealed partial class TeamService
    {
        static readonly TimeSpan WorldCallTimeout = TimeSpan.FromSeconds(3);

        // ------------------------------------------------------------------------------------------------ commands

        void HandleWithWorld(Player player, CharacterActionMessage message)
        {
            switch (message.Action)
            {
                case CharacterActionType.TeamRequestInvite:
                    Identity target = message.Target;
                    if (target == Identity.None)
                    {
                        int id = message.Parameter2 > 1 ? message.Parameter2 : message.Parameter1;
                        target = CharacterIdentity(id);
                    }

                    if (target.Type != IdentityType.CanbeAffected || target.Instance <= 0 || target == player.Identity)
                    {
                        Error(player, "Team invite target is not available.");
                        return;
                    }

                    WorldInvite(player, target.Instance, null, confirmed: message.Parameter2 == 1);
                    break;
                case CharacterActionType.TeamRequestReply:
                case CharacterActionType.ClientTeamInviteReply:
                    if (message.Parameter2 is 1 or 0 or 20)
                        WorldCommand(player, message.Parameter2 == 1 ? TeamOp.TeamAccept : TeamOp.TeamDecline,
                            message.Target == Identity.None ? 0 : message.Target.Instance);
                    break;
                case CharacterActionType.LeaveTeam:
                    WorldCommand(player, TeamOp.TeamLeave);
                    break;
                case CharacterActionType.TeamKickMember:
                    if (message.Target.Type != IdentityType.CanbeAffected)
                    {
                        Feedback(player, "Feedback_NeedToBeTeamLeader");
                        return;
                    }

                    WorldCommand(player, TeamOp.TeamKick, message.Target.Instance);
                    break;
                case CharacterActionType.TransferLeader:
                    if (message.Target.Type != IdentityType.CanbeAffected)
                    {
                        Error(player, "Team leadership transfer target is not available or you are not the leader.");
                        return;
                    }

                    WorldCommand(player, TeamOp.TeamPromote, message.Target.Instance);
                    break;
                // This is server-to-client leadership state, never invite acceptance.
                case CharacterActionType.AcceptTeamRequest:
                    break;
            }
        }

        /// <summary>Sends a team command for <paramref name="player"/>; World's answer (feedback, text) goes back to it.</summary>
        void WorldCommand(Player player, TeamOp op, int target = 0, int raidTeam = 0)
        {
            var command = new TeamCommand { Op = op, Actor = player.Identity.Instance, Target = target, RaidTeam = raidTeam };
            _ = Task.Run(async () =>
            {
                TeamCommandReply? reply = await CallWorld(command).ConfigureAwait(false);
                if (op is TeamOp.TeamLogout or TeamOp.TeamSync or TeamOp.TeamDisconnect) return;
                lock (_sync)
                {
                    if (!Owns(player)) return;
                    if (reply == null) Error(player, "Teams are unavailable right now.");
                    else Answer(player, reply);
                }
            });
        }

        /// <summary>
        /// An invite goes to World twice when needed: first World checks everything but the level range and returns
        /// the levels; this instance checks them against GameData eligibility (showing the too-high/too-low choice as
        /// the local path does) and confirms.
        /// </summary>
        void WorldInvite(Player inviter, int targetId, string? targetName, bool confirmed)
        {
            var command = new TeamCommand
            {
                Op = TeamOp.TeamInvite,
                Actor = inviter.Identity.Instance,
                Target = targetId,
                TargetName = targetName ?? string.Empty,
                ConfirmedRange = confirmed
            };
            _ = Task.Run(async () =>
            {
                TeamCommandReply? reply = await CallWorld(command).ConfigureAwait(false);
                lock (_sync)
                {
                    if (!IsActive(inviter)) return;
                    if (reply == null)
                    {
                        Error(inviter, "Teams are unavailable right now.");
                        return;
                    }

                    if (reply.Range == null)
                    {
                        Answer(inviter, reply);
                        return;
                    }

                    PlayerCard target = reply.Range.Target;
                    if (FindLevelConflict(reply.Range.Members, target.Level, out PlayerCard? conflict, out bool tooHigh))
                    {
                        Identity targetIdentity = CharacterIdentity(target.CharacterId);
                        string conflictName = conflict != null && !string.IsNullOrEmpty(conflict.Name) ? conflict.Name : "the team";
                        Send(inviter, Action(inviter, tooHigh ? CharacterActionType.TeamInviteAck : CharacterActionType.TeamInviteTooLow,
                            targetIdentity));
                        Send(inviter, new ChatTextMessage
                        {
                            Identity = inviter.Identity,
                            Text = target.Name + (tooHigh ? " is too high for " : " is too low for ") + conflictName + "."
                        });
                        return;
                    }

                    WorldInvite(inviter, target.CharacterId, null, confirmed: true);
                }
            });
        }

        /// <summary>The local rule (<see cref="TryFindLevelConflict"/>) over the levels World sent.</summary>
        bool FindLevelConflict(IEnumerable<PlayerCard> members, int inviteeLevel, out PlayerCard? conflict, out bool tooHigh)
        {
            conflict = null;
            tooHigh = false;
            PlayerCard? tooHighMember = null;
            PlayerCard? tooLowMember = null;
            foreach (PlayerCard member in members)
            {
                if (member.Level is < 1 or > 220) continue;
                if (_eligibility.IsTooHighForMember(member.Level, inviteeLevel))
                {
                    if (tooHighMember == null || member.Level < tooHighMember.Level) tooHighMember = member;
                }
                else if (_eligibility.IsTooLowForMember(member.Level, inviteeLevel))
                {
                    if (tooLowMember == null || member.Level > tooLowMember.Level) tooLowMember = member;
                }
            }

            if (tooHighMember != null)
            {
                conflict = tooHighMember;
                tooHigh = true;
                return true;
            }

            conflict = tooLowMember;
            return tooLowMember != null;
        }

        void Answer(Player player, TeamCommandReply reply)
        {
            if (!string.IsNullOrEmpty(reply.Feedback))
                Feedback(player, reply.Feedback);
            if (!string.IsNullOrEmpty(reply.Text))
                Error(player, reply.Text);
        }

        async Task<TeamCommandReply?> CallWorld(TeamCommand command)
        {
            if (_world == null || !_world.IsServing) return null;
            try
            {
                return await _world.Channel.Teams.CommandAsync(command, _world.Channel.Call(WorldCallTimeout)).ResponseAsync
                    .ConfigureAwait(false);
            }
            catch (RpcException)
            {
                return null;
            }
        }

        /// <summary>Every team this instance mirrors, for World's registration.</summary>
        public IReadOnlyList<TeamState> SnapshotMirrors()
        {
            lock (_sync)
            {
                var teams = new List<TeamState>();
                foreach (Team team in _mirrors.Values)
                {
                    var state = new TeamState
                    {
                        TeamId = team.Id,
                        Leader = team.Leader,
                        Raid = team.IsRaid,
                        ExplicitLeader = team.ExplicitLeadershipTransfer
                    };
                    foreach (int id in team.Members)
                        state.Members.Add(new TeamMember
                        {
                            CharacterId = id,
                            RaidTeam = team.RaidTeamIndex.GetValueOrDefault(id, -1),
                            Card = new PlayerCard { CharacterId = id }
                        });
                    teams.Add(state);
                }

                return teams;
            }
        }

        // ------------------------------------------------------------------------------------------------ pushes

        void OnWorldMessage(WorldToService message)
        {
            if (message.BodyCase != WorldToService.BodyOneofCase.Team) return;
            TeamEvent team = message.Team;
            switch (team.BodyCase)
            {
                case TeamEvent.BodyOneofCase.Update:
                    ApplyUpdate(team.Update);
                    break;
                case TeamEvent.BodyOneofCase.Invited:
                    OnInvited(team.Invited);
                    break;
                case TeamEvent.BodyOneofCase.Presence:
                    OnPresenceCard(team.Presence);
                    break;
                case TeamEvent.BodyOneofCase.Declined:
                    OnDeclined(team.Declined);
                    break;
            }
        }

        void ApplyUpdate(TeamUpdate update)
        {
            lock (_sync)
            {
                if (_stopped) return;
                int departed = update.Departed;
                TeamState? state = update.Team;
                if (departed > 0)
                {
                    _membership.Remove(departed, out Team? old);
                    _remoteData.Remove(departed);
                    if (_players.TryGetValue(departed, out Player? leaving))
                    {
                        QueueRoster(leaving, departed, update.NotifyLeaving);
                        if (old != null) LeaveChat(departed, old.Id);
                    }
                }

                if (state == null) return;
                if (update.Disbanded)
                {
                    int last = update.LastMember;
                    _membership.Remove(last);
                    _remoteData.Remove(last);
                    if (_players.TryGetValue(last, out Player? alone))
                    {
                        CancelInvitations(last);
                        QueueRoster(alone, departed);
                        LeaveChat(last, state.TeamId);
                    }

                    // Everyone this instance still shows in the team (World's resync of a team it no longer has).
                    if (_mirrors.Remove(state.TeamId, out Team? gone))
                        foreach (int id in _membership.Where(p => ReferenceEquals(p.Value, gone)).Select(p => p.Key).ToList())
                        {
                            _membership.Remove(id);
                            _remoteData.Remove(id);
                            if (id == last || id == departed || !_players.TryGetValue(id, out Player? member)) continue;
                            QueueRoster(member, departed > 0 ? departed : null);
                            LeaveChat(id, state.TeamId);
                        }
                    return;
                }

                Team mirror = Mirror(state);
                if (update.Joined > 0 && _players.ContainsKey(update.Joined))
                    ChatCommand(update.Joined, "#aorebirth-lft-remove");
                foreach (int id in mirror.Members)
                {
                    if (_players.TryGetValue(id, out Player? viewer))
                        QueueRoster(viewer, departed > 0 ? departed : null);
                }

                PruneMirror(mirror);
            }
        }

        /// <summary>World's state for a team, into its one local mirror object. Caller holds _sync.</summary>
        Team Mirror(TeamState state)
        {
            if (!_mirrors.TryGetValue(state.TeamId, out Team? team))
            {
                team = new Team(state.TeamId, state.Leader);
                team.Members.Clear();
                _mirrors[state.TeamId] = team;
            }

            team.Leader = state.Leader;
            team.IsRaid = state.Raid;
            team.ExplicitLeadershipTransfer = state.ExplicitLeader;
            List<int> previous = [.. team.Members];
            team.Members.Clear();
            team.RaidTeamIndex.Clear();
            foreach (TeamMember member in state.Members)
            {
                int id = member.CharacterId;
                if (id <= 0) continue;
                team.Members.Add(id);
                if (member.RaidTeam >= 0) team.RaidTeamIndex[id] = member.RaidTeam;
                if (!_players.ContainsKey(id) && member.Card != null)
                    _remoteData[id] = new MemberData(member.Card.Name, member.Card.Level, member.Card.Profession,
                        member.Card.MaxHealth, member.Card.MaxNano, member.Card.HasVitals, member.Card.Side);
                _membership[id] = team;
            }

            foreach (int id in previous.Where(id => !team.Members.Contains(id)))
            {
                if (_membership.TryGetValue(id, out Team? current) && ReferenceEquals(current, team)) _membership.Remove(id);
                _remoteData.Remove(id);
            }

            return team;
        }

        /// <summary>A mirror no local player is in any more is dropped. Caller holds _sync.</summary>
        void PruneMirror(Team team)
        {
            if (_world == null || team.Members.Any(_players.ContainsKey)) return;
            foreach (int id in team.Members)
            {
                if (_membership.TryGetValue(id, out Team? current) && ReferenceEquals(current, team)) _membership.Remove(id);
                _remoteData.Remove(id);
            }

            _mirrors.Remove(team.Id);
        }

        void OnInvited(TeamInvited invited)
        {
            lock (_sync)
            {
                if (!_players.TryGetValue(invited.Invitee, out Player? invitee) || !IsActive(invitee) || invited.Inviter == null) return;
                Identity inviter = CharacterIdentity(invited.Inviter.CharacterId);
                if (!invited.SamePlayfield)
                    Send(invitee, new TeamInviteMessage { Identity = invitee.Identity, Unknown = 1, Inviter = inviter, Name = invited.Inviter.Name });
                Send(invitee, Action(invitee, CharacterActionType.TeamRequestInvite, inviter));
            }
        }

        void OnPresenceCard(TeamPresenceCard presence)
        {
            lock (_sync)
            {
                if (!_players.TryGetValue(presence.Viewer, out Player? viewer) || !IsActive(viewer) || presence.Subject == null) return;
                PlayerCard subject = presence.Subject;
                Send(viewer, InfoPacket(subject));
                Send(viewer, new StatMessage
                {
                    Identity = CharacterIdentity(subject.CharacterId),
                    Stats =
                    [
                        new GameTuple<CharacterStat, uint>
                        {
                            Value1 = CharacterStat.Level,
                            Value2 = (uint)Math.Clamp(subject.Level, 1, 220)
                        }
                    ]
                });
            }
        }

        void OnDeclined(TeamDeclined declined)
        {
            lock (_sync)
            {
                if (!_players.TryGetValue(declined.Inviter, out Player? inviter) || !Owns(inviter)) return;
                Send(inviter, Action(inviter, CharacterActionType.TeamRequestReply, CharacterIdentity(declined.Invitee), 0, 20));
            }
        }

        /// <summary>The character inspect packet a player on this instance would build, from another instance's card.</summary>
        static InfoPacketMessage InfoPacket(PlayerCard card)
        {
            int visual = Math.Clamp(card.VisualProfession > 0 ? card.VisualProfession : card.Profession, 0, (int)Profession.Shade);
            int maxHealth = Math.Max(1, card.MaxHealth);
            return new InfoPacketMessage
            {
                Identity = CharacterIdentity(card.CharacterId),
                Unknown = 0,
                Type = InfoPacketType.Character,
                Info = new CharacterInfoPacket
                {
                    Unknown1 = 0x01,
                    Profession = (Profession)visual,
                    Level = (byte)Math.Clamp(card.Level, 0, byte.MaxValue),
                    TitleLevel = (byte)Math.Clamp(Math.Max(1, card.TitleLevel), 0, byte.MaxValue),
                    VisualProfession = (Profession)visual,
                    SideXp = (short)Math.Clamp(card.SideXp, short.MinValue, short.MaxValue),
                    Health = maxHealth,
                    MaxHealth = maxHealth,
                    BreedHostility = 0,
                    FirstName = card.FirstName ?? string.Empty,
                    LastName = card.LastName ?? string.Empty,
                    LegacyTitle = string.Empty,
                    PvpTitle = string.Empty,
                    CityPlayfieldId = 0,
                    InvadersKilled = card.InvadersKilled,
                    KilledByInvaders = card.KilledByInvaders,
                    AiLevel = card.AiLevel,
                    PvpDuelWins = 0,
                    PvpDuelLoses = 0,
                    PvpProfessionDuelLoses = 0,
                    PvpSoloKills = 0,
                    PvpTeamKills = 0,
                    PvpSoloScore = 0,
                    PvpTeamScore = 0,
                    PvpDuelScore = 0
                }
            };
        }
    }
}
