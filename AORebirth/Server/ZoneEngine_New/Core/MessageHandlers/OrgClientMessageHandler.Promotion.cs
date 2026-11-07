namespace ZoneEngine_New.Core.MessageHandlers
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// /org promote and demote (rules.txt lines 44-47): any member moves a same-organization member of lower rank by one
    /// rank. A promotion stops one rank below the actor's own rank; promoting the member just below the leader starts the
    /// leadership transfer (rules.txt lines 49-50).
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        void HandleRankChange(OrgClientMessage message, IZoneSession session, Player player, bool promote)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(promote ? OrgChatRefs.PromoteInvalidTarget : OrgChatRefs.DemoteInvalidTarget));
                return;
            }

            Player? target = FindRankTarget(message, player);
            if (target is null || target.Organization?.Id != org.Id)
            {
                SendRef(session, player, OrgChatText.Wrap(promote ? OrgChatRefs.PromoteInvalidTarget : OrgChatRefs.DemoteInvalidTarget));
                return;
            }

            if (!_gameData.GoverningForms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? form))
                return;

            int actorRank = player.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base);
            int targetRank = target.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base);
            int lowestRank = form.Ranks.Length - 1;

            // Only members of lower rank (higher number) than the actor can be moved.
            if (targetRank <= actorRank)
            {
                SendRef(session, player, OrgChatText.Wrap(promote ? OrgChatRefs.PromoteInvalidTarget : OrgChatRefs.DemoteInvalidTarget));
                return;
            }

            int newRank = promote ? targetRank - 1 : targetRank + 1;
            if (promote && actorRank == 0 && targetRank == 1)
            {
                HandleLeadershipTransfer(message, session, player, target, org, targetRank);
                return;
            }

            if (promote && newRank <= actorRank)
            {
                // The refusal ref for this case was not captured, so it is only logged for now.
                _logger.Info("/org promote refused (above allowed rank) org=" + org.Id);
                return;
            }

            if (!promote && targetRank >= lowestRank)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.DemoteAtLowestRank + OrgChatText.TextArg(target.Name ?? string.Empty)));
                return;
            }

            ResetRank(target, newRank);
            _organizations.SaveRank(target.Identity.Instance, newRank);

            string rankName = form.Ranks[newRank];
            session.Send(OrgChatText.Feedback(player.Identity,
                (promote ? OrgChatRefs.PromoteActor : OrgChatRefs.DemoteActor)
                + OrgChatText.TextArg(target.Name ?? string.Empty) + OrgChatText.TextArg(rankName)));
            target.Session?.Send(OrgChatText.Feedback(target.Identity,
                (promote ? OrgChatRefs.PromoteTarget : OrgChatRefs.DemoteTarget)
                + OrgChatText.TextArg(player.Name ?? string.Empty) + OrgChatText.TextArg(rankName)));
        }

        // Unknown1 == 0 marks the request the client sends after the leader confirmed the dialog (captures).
        void HandleLeadershipTransfer(OrgClientMessage message, IZoneSession session, Player leader, Player target,
            Data.OrganizationRecord org, int leaderNewRank)
        {
            if (message.Unknown1 != 0)
            {
                session.Send(new OrgLeadershipTransferMessage
                {
                    Identity = leader.Identity,
                    Unknown1 = (int)target.Identity.Type,
                    Unknown2 = target.Identity.Instance,
                    Organization = new Identity { Type = IdentityType.Organization, Instance = org.Id }
                });
                return;
            }

            _organizations.SaveRank(target.Identity.Instance, 0);
            _organizations.SaveRank(leader.Identity.Instance, leaderNewRank);
            _organizations.SaveLeader(org.Id, target.Identity.Instance);
            ResetRank(target, 0);
            ResetRank(leader, leaderNewRank);

            Data.OrganizationRecord updated = org with { LeaderId = target.Identity.Instance, LeaderName = target.Name ?? string.Empty };
            foreach (Player member in OnlineMembers(org.Id))
                member.Organization = updated;

            _chat.SendOrganizationMessage(org.Id,
                OrgChatText.Wrap(OrgChatRefs.LeadershipGivenOrg + OrgChatText.TextArg(target.Name ?? string.Empty)));
            target.Session?.Send(new FormatFeedbackMessage
            {
                Identity = target.Identity,
                Unknown = 1,
                Unknown1 = 0,
                Unknown2 = 0,
                FormattedMessage = "~&!!!\":" + OrgChatRefs.LeadershipGivenTarget + OrgChatText.TextArg(leader.Name ?? string.Empty) + "~"
            });
        }

        Player? FindRankTarget(OrgClientMessage message, Player player)
        {
            if (message.Target.Instance == 0)
                return null;
            if (message.Target.Instance == player.Identity.Instance)
                return player;
            return _playfieldManager.Value.FindPlayer(message.Target.Instance, out Player target) ? target : null;
        }
    }
}
