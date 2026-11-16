namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// Org debt and suspension. While suspended only /org debt, /org info and /org leave are accepted; the other
    /// commands get the captured refusal naming the action and the minutes left. Action texts are Text.json wording.
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int SuspendedRefusal = 153045876;
        const int InviteSuspendedRefusal = 33524638;
        const int DebtStatus = 182058216;
        const string SuspendedInviteAction = "invite a player to your organization";

        // Returns true when the command was refused because the member is suspended.
        bool RefuseWhileSuspended(OrgClientMessage message, Player player)
        {
            // Bank remove keeps its leader-only refusal (222804051) for non-leaders, suspended or not.
            if (message.Command is OrgClientCommand.Debt or OrgClientCommand.Info or OrgClientCommand.Leave
                or OrgClientCommand.BankRemove)
                return false;

            bool invite = message.Command == OrgClientCommand.Invite;
            string? action = invite ? SuspendedInviteAction : SuspendedActionText(message);
            if (action is null)
                return false;

            if (_tax.SuspensionOf(player, DateTime.UtcNow) is not { } suspension)
                return false;

            if (invite)
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, InviteSuspendedRefusal, action);
            else
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, SuspendedRefusal, action,
                    OrgTaxTickService.MessageInt((suspension.SecondsLeft + 59) / 60),
                    OrgTaxTickService.MessageInt(suspension.Debt));
            return true;
        }

        void HandleDebt(Player player)
            => ClientFeedback.SendFormattedInCategory(player, MemberCategory, DebtStatus,
                OrgTaxTickService.MessageInt(_tax.DebtOf(player)));

        static string? SuspendedActionText(OrgClientMessage message) => message.Command switch
        {
            OrgClientCommand.Bank => "to show amount of cash in the organization bank",
            OrgClientCommand.BankAdd => "to add money to the organization bank",
            OrgClientCommand.Ranks => "to get organization rank info",
            OrgClientCommand.Promote => "to promote a member",
            OrgClientCommand.Demote => "to demote a member",
            OrgClientCommand.Tax => "to show organization tax",
            OrgClientCommand.Kick or OrgClientCommand.Unknown2 => string.IsNullOrEmpty(message.CommandArgs)
                ? "to kick selected member"
                : "to kick member by name",
            _ => null,
        };
    }
}
