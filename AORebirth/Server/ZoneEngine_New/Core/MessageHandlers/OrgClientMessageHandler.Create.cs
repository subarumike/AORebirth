namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;

    /// <summary>Outcome of the retail name rules, listed in the order the captures show them being checked.</summary>
    enum OrgNameCheck
    {
        Valid,
        InvalidLength,
        TooFewLetters,
        InvalidCharacter,
        LowercaseInitial
    }

    /// <summary>
    /// /org create (command 1, name in CommandArgs): a full team of 6 led by the requester, all on the same side and
    /// without an organization, founds an organization. Replies and the order of checks come from the captures
    /// 20261031 (docs/generated/org_commands_scenario_capture_20261031.md); retail sends no Org Msg on creation.
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int CreateFeedbackCategory = 501;
        const int CreateInvalidLength = 89300600;
        const int CreateTooFewLetters = 24190273;
        const int CreateInvalidCharacter = 13952082;
        const int CreateLowercaseInitial = 13951557;
        const int CreateNameTaken = 167950692;
        const int CreateTeamRefused = 174731972;
        const int CreateSucceeded = 182257540;
        const int CreateTeamSize = 6;
        const int CreateLeaderRank = 0;
        const int CreateLeaderSocialStatus = 15;
        const string OrgJoinChatCommand = "#aorebirth-org-join ";

        void HandleCreate(OrgClientMessage message, IZoneSession session, Player player)
        {
            // Temporary live-test lock (Gameplay.json org.createGmOnly); no refusal text was captured for it.
            if (_settings.Org.CreateGmOnly && !player.IsGm)
            {
                _logger.Info("/org create refused (GM-only lock) character=" + player.Identity.Instance);
                return;
            }

            string name = (message.CommandArgs ?? string.Empty).Trim();
            int nameRefusal = CheckName(name) switch
            {
                OrgNameCheck.InvalidLength => CreateInvalidLength,
                OrgNameCheck.TooFewLetters => CreateTooFewLetters,
                OrgNameCheck.InvalidCharacter => CreateInvalidCharacter,
                OrgNameCheck.LowercaseInitial => CreateLowercaseInitial,
                _ => 0
            };
            if (nameRefusal != 0)
            {
                ClientFeedback.SendFormattedInCategory(player, CreateFeedbackCategory, nameRefusal);
                return;
            }

            try
            {
                CreateOrganization(player, name);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "/org create failed for character " + player.Identity.Instance);
            }
        }

        void CreateOrganization(Player leader, string name)
        {
            if (_organizations.IsNameTaken(name, 0))
            {
                ClientFeedback.SendFormattedInCategory(leader, CreateFeedbackCategory, CreateNameTaken);
                return;
            }

            // One refusal covers the leader, team size, raid, side and existing-organization conditions (capture 174731972).
            TeamSnapshot? team = _playfieldManager.Value.Teams.GetTeam(leader);
            if (leader.Organization is not null || team is null || team.IsRaid
                || team.LeaderId != leader.Identity.Instance || team.MemberIds.Count != CreateTeamSize)
            {
                ClientFeedback.SendFormattedInCategory(leader, CreateFeedbackCategory, CreateTeamRefused);
                return;
            }

            // Members on this engine are known live; the database check below covers everyone, wherever they are.
            var online = new List<Player> { leader };
            foreach (int memberId in team.MemberIds)
            {
                if (memberId == leader.Identity.Instance
                    || !_playfieldManager.Value.FindPlayer(memberId, out Player? member) || member is null)
                    continue;

                if (member.Organization is not null)
                {
                    ClientFeedback.SendFormattedInCategory(leader, CreateFeedbackCategory, CreateTeamRefused);
                    return;
                }

                online.Add(member);
            }

            if (!_gameData.GoverningForms.Forms.TryGetValue(0, out GoverningForm? form))
            {
                _logger.Error(new InvalidOperationException("Governing form 0 is missing."), "/org create cannot rank the members");
                return;
            }

            int lowestRank = Math.Max(0, form.Ranks.Length - 1);
            PersistedOrganizationCreateResult result = _organizations.Create(name, leader.Identity.Instance,
                team.MemberIds.ToList(), CreateLeaderRank, lowestRank);
            if (result.Outcome != OrganizationCreateOutcome.Created)
            {
                int refusal = result.Outcome == OrganizationCreateOutcome.NameTaken ? CreateNameTaken : CreateTeamRefused;
                ClientFeedback.SendFormattedInCategory(leader, CreateFeedbackCategory, refusal);
                return;
            }

            OrganizationRecord? org = _organizations.GetById(result.OrganizationId);
            if (org is null)
            {
                _logger.Error(new InvalidOperationException("Organization " + result.OrganizationId + " is missing after creation."),
                    "/org create could not read the new organization");
                return;
            }

            ClientFeedback.SendFormattedInCategory(leader, CreateFeedbackCategory, CreateSucceeded);
            ApplyCreatedOrganization(leader, online, org, lowestRank);

            // Chat resolves the Clan stat to the organization channel only at login: every member, wherever he is, joins it now.
            foreach (int memberId in team.MemberIds)
                _chat.SendChatCommand(memberId, OrgJoinChatCommand + org.Id);

            _logger.Info("/org create organization=" + org.Id + " leader=" + leader.Identity.Instance
                + " members=" + team.MemberIds.Count + " online=" + online.Count);
        }

        // The database already holds Clan and ClanLevel; the stats are set clean here and announced explicitly, in the
        // captured order (org info, then SocialStatus for the leader, Clan and ClanLevel as separate messages).
        void ApplyCreatedOrganization(Player leader, List<Player> online, OrganizationRecord org, int lowestRank)
        {
            foreach (Player member in online)
            {
                bool isLeader = ReferenceEquals(member, leader);
                int rank = isLeader ? CreateLeaderRank : lowestRank;
                member.Organization = org;
                ApplyOnTick(member, () =>
                {
                    if (isLeader)
                        member.Stats.Set(CharacterStat.SocialStatus, CreateLeaderSocialStatus, StatDetail.Base, dirty: false);
                    member.Stats.Set(CharacterStat.Clan, org.Id, StatDetail.Base, dirty: false);
                    member.Stats.Set(CharacterStat.ClanLevel, rank, StatDetail.Base, dirty: false);
                });
            }

            foreach (Player member in online)
            {
                bool isLeader = ReferenceEquals(member, leader);
                int rank = isLeader ? CreateLeaderRank : lowestRank;
                var clan = StatPacket(member, CharacterStat.Clan, org.Id);
                var clanLevel = StatPacket(member, CharacterStat.ClanLevel, rank);
                var neighborPackets = new List<MessageBody> { BuildOrgInfoPacket(member.Identity, org), clan, clanLevel };
                var packets = new List<MessageBody> { neighborPackets[0] };
                if (isLeader)
                    packets.Add(StatPacket(member, CharacterStat.SocialStatus, CreateLeaderSocialStatus));
                packets.Add(clan);
                packets.Add(clanLevel);

                foreach (Player recipient in online)
                    foreach (MessageBody packet in packets)
                        recipient.Session?.Send(packet);

                var locality = member.Playfield?.GetRequiredService<PlayfieldLocality>();
                foreach (MessageBody packet in neighborPackets)
                    locality?.Announce(member, packet, includeSelf: false);
            }
        }

        static StatMessage StatPacket(Player owner, CharacterStat stat, int value) => new()
        {
            Identity = owner.Identity,
            Stats = [new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = (uint)value }]
        };

        // Stats are only projected from the playfield tick; other paths run immediately.
        static void ApplyOnTick(Player player, Action action)
        {
            if (player.Playfield is { } playfield)
                playfield.DispatchPlayerProjection(player, action);
            else
                action();
        }
    }
}
