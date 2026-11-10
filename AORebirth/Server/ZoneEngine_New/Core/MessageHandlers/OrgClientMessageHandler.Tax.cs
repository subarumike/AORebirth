namespace ZoneEngine_New.Core.MessageHandlers
{
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// /org tax X by the leader: zero or positive whole credits only. Replies are Text.json ids in the member category.
    /// The stored tax is per organization; the per-member deduction at taxTick is handled elsewhere.
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int TaxNotLeader = 192685736;
        const int TaxInvalid = 198384040;
        const int TaxChangedLeader = 165930356;

        void HandleTaxChange(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            if (!OrgMembershipService.IsLeader(player, org))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, TaxNotLeader);
                return;
            }

            if (!TryParseTax(message.CommandArgs, out int newTax))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, TaxInvalid);
                return;
            }

            int oldTax = (int)org.Tax;
            _organizations.SaveTax(org.Id, newTax);

            OrganizationRecord updated = org with { Tax = newTax };
            foreach (Player member in OnlineMembers(org.Id))
                member.Organization = updated;

            _chat.SendOrganizationMessage(org.Id,
                OrgChatText.TaxChangedOrgMessage(player.Name ?? string.Empty, newTax, oldTax));
            ClientFeedback.SendFormattedInCategory(player, MemberCategory, TaxChangedLeader, oldTax, newTax);

            if (newTax > oldTax)
                SendTaxIncreaseDialogs(org.Id, player.Identity.Instance, newTax);
        }

        // Online members other than the leader are asked whether to leave. Yes sends the normal /org leave; No does nothing.
        void SendTaxIncreaseDialogs(int orgId, int leaderId, int newTax)
        {
            foreach (Player member in OnlineMembers(orgId))
            {
                if (member.Identity.Instance == leaderId || member.Session is null)
                    continue;

                member.Session.Send(new OrgTaxIncreaseMessage
                {
                    Identity = member.Identity,
                    Organization = new Identity { Type = IdentityType.Organization, Instance = orgId },
                    NewTax = newTax,
                });
            }
        }

        // Whole, non-negative amounts only: anything else (sign, decimals, letters, overflow) is the invalid-tax reply.
        static bool TryParseTax(string args, out int tax)
            => int.TryParse(args, NumberStyles.None, CultureInfo.InvariantCulture, out tax) && tax >= 0;
    }
}
