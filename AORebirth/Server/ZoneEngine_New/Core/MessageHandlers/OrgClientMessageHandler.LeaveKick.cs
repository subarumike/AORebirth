namespace ZoneEngine_New.Core.MessageHandlers
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// /org leave (command 16), /org disband (command 6) and /org kick (command 13 by name, 12 on the selected target).
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        void HandleLeave(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            _memberships.Leave(player, org);
        }

        void HandleDisband(OrgClientMessage message, IZoneSession session, Player player)
        {
            // A GM disbands the organization of a targeted leader, even one he does not belong to.
            if (player.IsGm && message.Target.Instance > 0 && message.Target.Instance != player.Identity.Instance)
            {
                _memberships.DisbandByGm(player, message.Target.Instance);
                return;
            }

            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            // A member's disband is refused silently; only the leader's confirmed disband acts.
            if (OrgMembershipService.IsLeader(player, org))
                _memberships.DisbandAsLeader(player, org);
        }

        // A GM may kick from an organization he does not belong to, so the requester's membership is not checked here.
        void HandleKick(OrgClientMessage message, IZoneSession session, Player player)
        {
            // The kick-by-name form carries the name in CommandArgs; otherwise the selected character is the target.
            if (!string.IsNullOrEmpty(message.CommandArgs))
                _memberships.KickByName(player, message.CommandArgs);
            else if (message.Target.Instance > 0)
                _memberships.KickById(player, message.Target.Instance);
        }
    }
}
