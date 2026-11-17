namespace ZoneEngine_New.Core.Organizations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Membership ends outside the join path: leave, disband, kick (GM, rank, anarchist quorum) and side change.
    /// Notice ids are Text.json ids; the anarchist quorum is persisted in org_anar_pend_kick.
    /// </summary>
    public sealed class OrgMembershipService
    {
        const int MemberCategory = 508;
        const int SideChangeCategory = 501;
        const int KickCategory = 110;
        const int LeaveNotice = 45846567;
        const int MembersDisbandedNotice = 143442663;
        const int AnarchismKicksRemaining = 233638763;
        const int GmDisbandedNotice = 28601677;
        const int LeaderDisbandedBySideChange = 35204276;
        const int MemberKickedBySideChange = 181448347;
        const int GmKickedMember = 33183701;
        const int KickedByMember = 169766805;
        const int QuorumLeaderKickedLeader = 230985621;
        const int CannotKickLeader = 211;
        const int CannotKickSelf = 186172214;
        const int KickRankRefused = 167722847;
        const int NoActiveCharacter = 53724837;
        const int MemberQuorum = 2;
        const int LeaderQuorum = 3;
        const int RankKickGap = 3;
        const string LeaveCommand = "#aorebirth-org-leave ";

        sealed record MemberRef(int Id, string Name, Player? Online);

        readonly IOrganizationRepository _organizations;
        readonly OrgLockoutClock _lockouts;
        readonly IChatEngineLink _chat;
        readonly ICharacterDao _characters;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IGameData _gameData;
        readonly GameplaySettings _settings;
        readonly OrgTaxTickService _tax;

        // Lazy: PlayfieldManager holds this service, so eager resolution would be a cycle.
        public OrgMembershipService(IOrganizationRepository organizations, OrgLockoutClock lockouts, IChatEngineLink chat,
            ICharacterDao characters, Lazy<PlayfieldManager> playfields, IGameData gameData, GameplaySettings settings,
            OrgTaxTickService tax)
        {
            _organizations = organizations;
            _lockouts = lockouts;
            _chat = chat;
            _characters = characters;
            _playfields = playfields;
            _gameData = gameData;
            _settings = settings;
            _tax = tax;
        }

        public static bool IsLeader(Player player, OrganizationRecord org) => org.LeaderId == player.Identity.Instance;

        public void Attach(Player player)
        {
            player.SideChanged -= OnSideChanged;
            player.SideChanged += OnSideChanged;
            _lockouts.Start(player.Identity.Instance, player.Name ?? string.Empty);
        }

        public void Detach(Player player)
        {
            player.SideChanged -= OnSideChanged;
            _lockouts.Stop(player.Identity.Instance);
        }

        public void Leave(Player player, OrganizationRecord org)
        {
            if (IsLeader(player, org))
            {
                DisbandAsLeader(player, org);
                return;
            }

            _lockouts.Add(player.Identity.Instance, player.Name ?? string.Empty, _settings.Org.LeaveLockoutSeconds);
            _organizations.ClearMembership(player.Identity.Instance);
            _tax.Forget(player.Identity.Instance);
            ClientFeedback.SendFormattedInCategory(player, MemberCategory, LeaveNotice);
            ClearOnline(player, org.Id);
        }

        public void DisbandAsLeader(Player leader, OrganizationRecord org)
        {
            leader.Session?.Send(OrgChatText.Message(leader.Identity, OrgChatText.Wrap(OrgChatRefs.OrganizationDisbanded)));

            string leaderName = leader.Name ?? string.Empty;
            Disband(org, orgMessage: null, notifyOnlineMember: member =>
            {
                if (member.Identity.Instance != org.LeaderId)
                    ClientFeedback.SendFormattedInCategory(member, MemberCategory, MembersDisbandedNotice, leaderName);
            });
        }

        // A GM dissolves the organization of a targeted leader; a non-leader target is refused silently.
        public void DisbandByGm(Player gm, int targetId)
        {
            MemberRef target = ResolveMember(targetId);
            if (OrganizationOf(target) is not { } org || org.LeaderId != target.Id)
                return;

            Disband(org, OrgChatText.GmDisbandedOrgMessage(gm.Name ?? string.Empty), notifyOnlineMember: NotifyGmDisbanded,
                lockoutExemptId: gm.Identity.Instance);
        }

        static void NotifyGmDisbanded(Player member)
            => ClientFeedback.SendFormattedInCategory(member, MemberCategory, GmDisbandedNotice);

        public void KickById(Player actor, int targetId)
            => Kick(actor, ResolveMember(targetId));

        public void KickByName(Player actor, string name)
        {
            MemberRef? target = FindByName(name);
            if (target is null)
            {
                NotifyNoActiveCharacter(actor, name);
                return;
            }

            Kick(actor, target);
        }

        void Disband(OrganizationRecord org, string? orgMessage, Action<Player>? notifyOnlineMember, int lockoutExemptId = 0)
        {
            List<MemberRef> members = ResolveMembers(org.Id);

            if (orgMessage is not null)
                _chat.SendOrganizationMessage(org.Id, orgMessage);

            _organizations.Disband(org.Id);

            foreach (MemberRef member in members)
            {
                _tax.Forget(member.Id);
                if (member.Id != lockoutExemptId)
                    _lockouts.Add(member.Id, member.Name, _settings.Org.DisbandLockoutSeconds);

                if (member.Online is not { } online)
                    continue;

                notifyOnlineMember?.Invoke(online);
                ClearOnline(online, org.Id);
            }
        }

        void Kick(Player actor, MemberRef target)
        {
            if (target.Id == actor.Identity.Instance)
            {
                ClientFeedback.SendFormattedInCategory(actor, MemberCategory, CannotKickSelf);
                return;
            }

            OrganizationRecord? org = OrganizationOf(target);
            if (org is null)
            {
                NotifyNoActiveCharacter(actor, target.Name);
                return;
            }

            // A GM kicks
            if (actor.IsGm)
            {
                if (target.Id == org.LeaderId)
                {
                    // Kicking the leader dissolves the organization; the leader gets the GM notice first.
                    Disband(org, OrgChatText.GmDisbandedOrgMessage(actor.Name ?? string.Empty), notifyOnlineMember: member =>
                    {
                        if (member.Identity.Instance == target.Id)
                            ClientFeedback.SendFormattedInCategory(member, KickCategory, GmKickedMember);

                        NotifyGmDisbanded(member);
                    }, lockoutExemptId: actor.Identity.Instance);
                    return;
                }

                _chat.SendOrganizationMessage(org.Id, OrgChatText.GmKickedOrgMessage(target.Name));
                KickMember(org, target, _settings.Org.KickLockoutSeconds, KickCategory, GmKickedMember);
                return;
            }

            if (actor.Organization is not { } actorOrg || actorOrg.Id != org.Id)
            {
                NotifyNoActiveCharacter(actor, target.Name);
                return;
            }

            if (IsAnarchism(org))
            {
                RegisterAnarchismRequest(actor, org, target);
                return;
            }

            if (target.Id == org.LeaderId)
            {
                ClientFeedback.SendFormattedInCategory(actor, MemberCategory, CannotKickLeader);
                return;
            }

            // The leader kicks anyone; other ranks only kick members at least RankKickGap ranks below their own.
            bool actorIsLeader = actor.Identity.Instance == org.LeaderId;
            if (!actorIsLeader && RankOf(target) - RankOf(Ref(actor)) < RankKickGap)
            {
                ClientFeedback.SendFormattedInCategory(actor, MemberCategory, KickRankRefused);
                return;
            }

            string actorName = actor.Name ?? string.Empty;
            _chat.SendOrganizationMessage(org.Id, OrgChatText.RankKickedOrgMessage(actorName, target.Name));
            KickMember(org, target, _settings.Org.KickLockoutSeconds, KickCategory, KickedByMember, actorName);
        }

        void RegisterAnarchismRequest(Player actor, OrganizationRecord org, MemberRef target)
        {
            // The repository decides under the organization lock; a repeat request by the same kicker is a no-op.
            PersistedAnarchistKickResult result = _organizations.RecordAnarchistKick(org.Id, target.Id, target.Name,
                actor.Identity.Instance, actor.Name ?? string.Empty, MemberQuorum, LeaderQuorum);
            if (result.Outcome == AnarchistKickOutcome.Recorded)
            {
                int required = result.TargetIsLeader ? LeaderQuorum : MemberQuorum;
                ClientFeedback.SendFormattedInCategory(actor, MemberCategory, AnarchismKicksRemaining,
                    required - result.Requesters.Count);
                return;
            }

            if (result.Outcome != AnarchistKickOutcome.Reached)
                return;

            if (result.TargetIsLeader)
                KickLeaderByQuorum(org, target, result.Requesters);
            else
            {
                string last = result.Requesters[result.Requesters.Count - 1].KickerName;
                string kickers = result.Requesters[0].KickerName + " and " + last;
                _chat.SendOrganizationMessage(org.Id, OrgChatText.QuorumMemberKickedOrgMessage(kickers, target.Name));
                KickMember(org, target, _settings.Org.KickLockoutSeconds, KickCategory, KickedByMember, last);
            }
        }

        void KickLeaderByQuorum(OrganizationRecord org, MemberRef leader, IList<PersistedAnarchistKickerData> requesters)
        {
            string first = requesters[0].KickerName;
            string second = requesters[1].KickerName;
            string third = requesters[2].KickerName;

            _chat.SendOrganizationMessage(org.Id, OrgChatText.QuorumLeaderKickedOrgMessage(first, second, third, leader.Name));
            KickMember(org, leader, _settings.Org.AnarchistLeaderKickLockoutSeconds, MemberCategory,
                QuorumLeaderKickedLeader, first, second, third);

            // The new leader is drawn among the three members who requested the kick.
            PersistedAnarchistKickerData successor = requesters[Random.Shared.Next(LeaderQuorum)];
            _organizations.SaveLeader(org.Id, successor.KickerId);
            _chat.SendOrganizationMessage(org.Id,
                OrgChatText.Wrap(OrgChatRefs.LeadershipGivenOrg + OrgChatText.TextArg(successor.KickerName)));

            OrganizationRecord updated = org with { LeaderId = successor.KickerId, LeaderName = successor.KickerName };
            foreach (Player member in _playfields.Value.SnapshotPlayers())
            {
                if (member.Organization?.Id == org.Id)
                    member.Organization = updated;
            }
        }

        void KickMember(OrganizationRecord org, MemberRef target, long lockoutSeconds, int category, int messageId,
            params object[] args)
        {
            _lockouts.Add(target.Id, target.Name, lockoutSeconds);
            _organizations.ClearMembership(target.Id);
            _tax.Forget(target.Id);

            if (target.Online is not { } online)
                return;

            ClientFeedback.SendFormattedInCategory(online, category, messageId, args);
            ClearOnline(online, org.Id);
        }

        void OnSideChanged(Player player)
        {
            // Runs inside the stat update that changed the side; the membership change is deferred to the tick.
            if (player.Playfield is { } playfield && playfield.PostAction(() => HandleSideChange(player)))
                return;

            HandleSideChange(player);
        }

        void HandleSideChange(Player player)
        {
            if (player.Organization is not { } org)
                return;

            if (IsLeader(player, org))
            {
                string leaderName = player.Name ?? string.Empty;
                Disband(org, orgMessage: null, notifyOnlineMember: member =>
                {
                    if (member.Identity.Instance == org.LeaderId)
                        ClientFeedback.SendFormattedInCategory(member, SideChangeCategory, LeaderDisbandedBySideChange, leaderName);
                    else
                        ClientFeedback.SendFormattedInCategory(member, MemberCategory, MembersDisbandedNotice, leaderName);
                });
                return;
            }

            string kickedName = player.Name ?? string.Empty;
            _chat.SendOrganizationMessage(org.Id, OrgChatText.SideChangeKickedOrgMessage(kickedName));
            KickMember(org, Ref(player), _settings.Org.SideChangeLockoutSeconds, SideChangeCategory,
                MemberKickedBySideChange, kickedName);
        }

        // Notifying the leaver and the characters around at once, as the invite does for the inviter, so the org name clears without waiting for the tick.
        void ClearOnline(Player player, int orgId)
        {
            player.Organization = null;
            AnnounceOrganizationCleared(player);
            // Removed rather than zeroed: a saved rank 0 would read back as leader on the next login.
            OnPlayerTick(player, () =>
            {
                player.Stats.Remove(CharacterStat.Clan);
                player.Stats.Remove(CharacterStat.ClanLevel);
                player.FlushDirtyStats();
            });

            _chat.SendChatCommand(player.Identity.Instance, LeaveCommand + orgId);
        }

        // Retail sends an empty OrgInfoPacket (id 0, no name) before the Clan reset; without it the client keeps the old org name.
        void AnnounceOrganizationCleared(Player player)
        {
            var orgInfo = new OrgInfoPacketMessage
            {
                Identity = player.Identity,
                OrganizationId = 0,
                Name = string.Empty
            };

            var clan = new StatMessage
            {
                Identity = player.Identity,
                Stats = [new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.Clan, Value2 = 0 }]
            };

            var locality = player.Playfield?.GetRequiredService<PlayfieldLocality>();

            player.Session?.Send(orgInfo);
            locality?.Announce(player, orgInfo, includeSelf: false);

            player.Session?.Send(clan);
            locality?.Announce(player, clan, includeSelf: false);
        }

        // Only a leader or a GM is told that the named character cannot be kicked; other requests stay silent.
        void NotifyNoActiveCharacter(Player actor, string name)
        {
            if (actor.IsGm || (actor.Organization is { } org && IsLeader(actor, org)))
                ClientFeedback.SendFormattedInCategory(actor, MemberCategory, NoActiveCharacter, name);
        }

        // Stats are only projected from the playfield tick; other paths run immediately.
        static void OnPlayerTick(Player player, Action action)
        {
            if (player.Playfield is { } playfield)
                playfield.DispatchPlayerProjection(player, action);
            else
                action();
        }

        bool IsAnarchism(OrganizationRecord org)
            => _gameData.GoverningForms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? form) && form.Ranks.Length == 1;

        OrganizationRecord? OrganizationOf(MemberRef target)
        {
            if (target.Online is { } online)
                return online.Organization;

            int orgId = _organizations.GetOrganizationIdOf(target.Id);
            return orgId > 0 ? _organizations.GetById(orgId) : null;
        }

        int RankOf(MemberRef member)
            => member.Online is { } online
                ? online.Stats.GetOrZero(CharacterStat.ClanLevel)
                : _organizations.GetStoredRank(member.Id);

        List<MemberRef> ResolveMembers(int orgId)
        {
            var members = new List<MemberRef>();
            foreach (int id in _organizations.GetMemberIds(orgId))
                members.Add(ResolveMember(id));

            return members;
        }

        MemberRef ResolveMember(int characterId)
        {
            if (_playfields.Value.FindPlayer(characterId, out Player? online) && online is not null)
                return Ref(online);

            return new MemberRef(characterId, _characters.LoadById(characterId)?.Name ?? string.Empty, null);
        }

        MemberRef? FindByName(string name)
        {
            foreach (Player player in _playfields.Value.SnapshotPlayers())
            {
                if (string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase))
                    return Ref(player);
            }

            CharacterDirectoryData? data = _characters.LoadByName(name);
            return data is null ? null : new MemberRef(data.CharacterId, data.Name, null);
        }

        static MemberRef Ref(Player player) => new(player.Identity.Instance, player.Name ?? string.Empty, player);
    }
}
