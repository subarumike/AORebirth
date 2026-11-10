namespace ZoneEngine_New.Core.MessageHandlers
{
    using System.Globalization;

    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// /org bank add and remove. Replies are Text.json ids in the member category, sent as plain text (no captured ref).
    /// The bank cap and the leader's cash cap come from rules.txt.
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int MemberCategory = 508;
        const int BankNotLeader = 222804051;
        const int BankInvalidAmount = 5429092;
        const int BankInsufficientCash = 89364632;
        const int BankInsufficientBank = 210489620;
        const int BankViewTooLow = 237299844;
        const long BankCap = 3_999_999_999;

        void HandleBankTransfer(OrgClientMessage message, IZoneSession session, Player player, bool deposit)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            if (!deposit && !OrgMembershipService.IsLeader(player, org))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, BankNotLeader);
                return;
            }

            if (!TryParseBankAmount(message.CommandArgs, out long amount))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, BankInvalidAmount);
                return;
            }

            // Pending in-memory credits reach the database first, so the transfer works on the current balance.
            player.FlushDirtyStats();
            PersistedOrganizationBankResult result = _organizations.TransferBank(org.Id, player.Identity.Instance, deposit,
                amount, BankCap, CashLimit.Max - 1);

            switch (result.Outcome)
            {
                case OrganizationBankOutcome.InsufficientCash:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, BankInsufficientCash);
                    return;
                case OrganizationBankOutcome.BankFull:
                case OrganizationBankOutcome.CashFull:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, BankInvalidAmount);
                    return;
                case OrganizationBankOutcome.InsufficientBank:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, BankInsufficientBank, unchecked((int)result.Bank));
                    return;
                case OrganizationBankOutcome.Done:
                    break;
                default:
                    return;
            }

            // Silent on success: the new balances are stored, so only the in-memory copies are refreshed.
            OrganizationRecord updated = org with { Bank = result.Bank };
            foreach (Player member in OnlineMembers(org.Id))
                member.Organization = updated;

            player.Stats.Set(CharacterStat.Cash, (int)result.Cash, StatDetail.Base, dirty: false);
            player.Session?.Send(new StatMessage
            {
                Identity = player.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = CharacterStat.Cash,
                        Value2 = (uint)result.Cash
                    }
                ]
            });
        }

        // Whole, positive amounts only: anything else (sign, decimals, letters, overflow) is the invalid-amount reply.
        static bool TryParseBankAmount(string args, out long amount)
            => long.TryParse(args, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }
}
