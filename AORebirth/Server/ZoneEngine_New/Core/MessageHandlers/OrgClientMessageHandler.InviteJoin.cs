namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// /org invite (command 14): inviter must be the leader or at least 2 ranks above the lowest rank.
    /// Target must be online, not already in an organization, and on the same side.
    /// /org join (command 15): invited target accepts the invitation. If the target is under an invite lockout,
    /// the join is refused (client-side display; server sends no refusal packet).
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        /// <summary>
        /// Track pending invitations: key=invitee CharacterId, value=(inviter name, inviter id, timestamp).
        /// Used to recover inviter name on join when OrgClientMessage doesn't carry that info.
        /// </summary>
        static readonly Dictionary<int, (string InviterName, int InviterId, DateTime Timestamp)> PendingInvites
            = new Dictionary<int, (string, int, DateTime)>();

        /// <summary>Clean expired pending invitations (older than ~2 minutes).</summary>
        static void CleanupExpiredInvites()
        {
            var now = DateTime.UtcNow;
            var expired = PendingInvites.Where(kvp => (now - kvp.Value.Timestamp).TotalSeconds > 120).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
                PendingInvites.Remove(key);
        }
        void HandleInviteJoin(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (message.Command == OrgClientCommand.Invite)
            {
                HandleInvite(message, session, player);
            }
            else if (message.Command == OrgClientCommand.Join)
            {
                HandleJoin(message, session, player);
            }
        }

        void HandleInvite(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            // Target is identified by message.Target.Instance (selected in-game).
            if (message.Target.Instance == 0)
            {
                // No target selected; stay silent.
                return;
            }

            // Resolve target Player by instance id.
            if (!_playfieldManager.Value.FindPlayer(message.Target.Instance, out Player? target) || target is null)
            {
                // Target not found online; stay silent.
                return;
            }

            GoverningFormCatalog forms = _gameData.GoverningForms;
            if (!forms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? form))
                return;

            int actorRank = player.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base);
            int lowestRank = form.Ranks.Length - 1;

            // Invite permission: leader OR at least 2 ranks above the lowest.
            // (Lowest rank = form.Ranks.Length - 1; so inviter must have rank <= lowestRank - 2, i.e., be at least 2 above.)
            if (actorRank != 0 && actorRank > lowestRank - 2)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.InviteRankTooLow));
                return;
            }

            // Target must not be in another organization.
            if (target.Organization is not null)
            {
                SendRef(session, player, OrgChatText.InviteAlreadyInOrgRefusal(target.Name ?? string.Empty));
                return;
            }

            // Target must be on the same side.
            int inviterSide = player.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base);
            int targetSide = target.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base);
            if (inviterSide != targetSide)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.InviteOtherSide));
                return;
            }

            // All checks passed: send the invite to the target and record it for join resolution.
            lock (PendingInvites)
            {
                CleanupExpiredInvites();
                PendingInvites[target.Identity.Instance] = (player.Name ?? string.Empty, player.Identity.Instance, DateTime.UtcNow);
            }

            target.Session?.Send(new OrgInviteMessage
            {
                Identity = target.Identity,
                Organization = new Identity { Type = IdentityType.Organization, Instance = org.Id },
                OrganizationName = org.Name ?? string.Empty,
                Unknown3 = 0
            });
        }

        void HandleJoin(OrgClientMessage message, IZoneSession session, Player player)
        {
            // Extract organization id from the target identity (should be 0xDEAA:<org>)
            if (message.Target.Type != IdentityType.Organization || message.Target.Instance <= 0)
                return;

            int orgId = message.Target.Instance;
            Data.OrganizationRecord? org = _organizations.GetById(orgId);
            if (org is null)
                return;

            // Check if the player is under an invite lockout.
            PersistedOrgLockoutData? lockout = _organizations.GetLockout(player.Identity.Instance);
            if (lockout is not null && lockout.SecondsRemaining > 0)
            {
                long remaining = lockout.SecondsRemaining;
                ClientFeedback.SendFormattedInCategory(player, ClientFeedback.LockoutCategory, ClientFeedback.LockoutAbleIn,
                    (int)(remaining / 3600), (int)(remaining % 3600 / 60), (int)(remaining % 60));
                return;
            }

            // All checks passed: join the organization.
            GoverningFormCatalog forms = _gameData.GoverningForms;
            if (!forms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? form))
                return;

            int lowestRank = form.Ranks.Length - 1;

            // Recover the inviter name from pending invites.
            string inviterName = "Member";
            int inviterId = 0;
            lock (PendingInvites)
            {
                if (PendingInvites.TryGetValue(player.Identity.Instance, out var pending))
                {
                    inviterName = pending.InviterName;
                    inviterId = pending.InviterId;
                    PendingInvites.Remove(player.Identity.Instance);
                }
            }

            // Update player organization and rank. Both stats must be dirty, otherwise FlushDirtyStats never sends them.
            player.Organization = org;
            // Retail sends SocialStatus 4 to a freshly joined member (capture invite 20261108).
            const int OrgJoinSocialStatus = 4;
            void ApplyClan()
            {
                player.Stats.Set(CharacterStat.SocialStatus, OrgJoinSocialStatus, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.Clan, org.Id, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.ClanLevel, lowestRank, StatDetail.Base, dirty: true);
                player.FlushDirtyStats();
            }
            if (player.Playfield is { } playfield)
                playfield.DispatchPlayerProjection(player, ApplyClan);
            else
                ApplyClan();

            // Persist the changes.
            _organizations.SaveMembership(player.Identity.Instance, org.Id);
            _organizations.SaveRank(player.Identity.Instance, lowestRank);

            // Chat resolves the Clan stat to the organization channel only at login; join it now (also Org Msg).
            _chat.SendChatCommand(player.Identity.Instance, "#aorebirth-org-join " + org.Id);

            // Send join success
            session.Send(OrgChatText.Message(player.Identity, OrgChatText.Wrap(OrgChatRefs.JoinedOrganization)));
            session.Send(BuildOrgInfoPacket(player.Identity, org));
            NotifyInviterOfJoin(inviterId, player, org, lowestRank);

            // Broadcast to Org Msg: "<inviter> invited <joiner> to your organization."
            // Now using the recovered inviter name from PendingInvites (recorded at invite time).
            _chat.SendOrganizationMessage(org.Id, OrgChatText.InviteNotice(inviterName, player.Name ?? string.Empty));
        }

        // Retail sends OrgInfoPacket (org id and name, identity = joiner) after accepting, not the full OrgInfo (capture invite 20261108).
        static OrgInfoPacketMessage BuildOrgInfoPacket(Identity joiner, Data.OrganizationRecord org)
            => new OrgInfoPacketMessage
            {
                Identity = joiner,
                OrganizationId = org.Id,
                Name = org.Name ?? string.Empty
            };

        // Retail refreshes the joiner for the inviter: its org info and Clan stats are sent to the inviter's client.
        void NotifyInviterOfJoin(int inviterId, Player joiner, Data.OrganizationRecord org, int rank)
        {
            if (inviterId <= 0 || !_playfieldManager.Value.FindPlayer(inviterId, out Player? inviter) || inviter is null)
                return;

            inviter.Session?.Send(BuildOrgInfoPacket(joiner.Identity, org));
            inviter.Session?.Send(new StatMessage
            {
                Identity = joiner.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.Clan, Value2 = (uint)org.Id },
                    new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.ClanLevel, Value2 = (uint)rank }
                ]
            });
        }
    }
}
