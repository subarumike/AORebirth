namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// /org startvote, vote, vote info and stopvote (rules.txt step 5). Replies are Text.json ids in the member category.
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int VoteStartNotLeader = 258658757;
        const int VoteStartUsage = 111800288;
        const int VoteUsage = 179467989;
        const int VoteInvalidDuration = 68628373;
        const int VoteInvalidChoices = 98440303;
        const int VoteDuplicateChoice = 76010659;
        const int VoteChoiceTooHigh = 99189171;
        const int VoteAlreadyActive = 168300623;
        const int VoteCooldown = 114148739;
        const int VoteLateJoin = 140104245;
        const int VoteCast = 213809944;
        const int VoteAlreadyCast = 259056068;
        const int VoteNoneInProgress = 179440135;
        const int VoteLeaderInformed = 123361268;
        const int VoteLeaderCanBegin = 65798404;
        const int VoteStoppedLeader = 131953188;

        void HandleStartVote(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            if (!player.IsGm && !OrgMembershipService.IsLeader(player, org))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteStartNotLeader);
                return;
            }

            if (!TrySplitStartVote(message.CommandArgs, out string description, out string[] tokens) || tokens.Length == 0
                || !int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteStartUsage);
                return;
            }

            if (minutes < OrgVoteService.MinDurationMinutes || minutes > OrgVoteService.MaxDurationMinutes)
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteInvalidDuration);
                return;
            }

            int choiceError = ParseChoices(tokens.Skip(1).ToArray(), out List<string> labels, out string? choiceArgument);
            if (choiceError != 0)
            {
                if (choiceArgument is null)
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, choiceError);
                else
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, choiceError, choiceArgument);
                return;
            }

            PersistedOrgVoteStartResult result = _votes.Start(org.Id, player.Identity.Instance, description, minutes, labels, DateTime.UtcNow);
            switch (result.Outcome)
            {
                case OrgVoteStartOutcome.AlreadyActive:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteAlreadyActive);
                    return;
                case OrgVoteStartOutcome.Cooldown:
                    int minutesLeft = (int)((result.CooldownSecondsLeft + 59) / 60);
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteCooldown,
                        _votes.CooldownSeconds / 60, minutesLeft);
                    return;
                case OrgVoteStartOutcome.Missing:
                    SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                    return;
                case OrgVoteStartOutcome.Started:
                    SendRef(session, player, OrgChatText.Wrap(OrgChatText.RefId(VoteLeaderInformed)));
                    SendRef(session, player, OrgChatText.Wrap(OrgChatText.RefId(VoteLeaderCanBegin)));
                    return;
            }
        }

        void HandleVote(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            if (!int.TryParse((message.CommandArgs ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int choiceNo))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteUsage);
                return;
            }

            OrgVoteService.CastResult result = _votes.Cast(org.Id, player.Identity.Instance, choiceNo, DateTime.UtcNow);
            switch (result.Status)
            {
                case OrgVoteService.CastStatus.Cast:
                    session.Send(OrgChatText.VoteCastConfirmation(player.Identity, OrgChatText.RefId(VoteCast)
                        + OrgChatText.TextArg(choiceNo.ToString(CultureInfo.InvariantCulture) + ":" + result.ChoiceLabel)));
                    return;
                case OrgVoteService.CastStatus.AlreadyVoted:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteAlreadyCast);
                    return;
                case OrgVoteService.CastStatus.NotEligible:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteLateJoin);
                    return;
                case OrgVoteService.CastStatus.InvalidChoice:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteUsage);
                    return;
                case OrgVoteService.CastStatus.ChoiceTooHigh:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteChoiceTooHigh,
                        choiceNo.ToString(CultureInfo.InvariantCulture));
                    return;
                default:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteNoneInProgress);
                    return;
            }
        }

        void HandleVoteInfo(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            string? text = _votes.Describe(org.Id, player.Identity.Instance, DateTime.UtcNow);
            if (text is null)
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteNoneInProgress);
            else
                SendRef(session, player, text);
        }

        void HandleStopVote(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            if (!player.IsGm && !OrgMembershipService.IsLeader(player, org))
            {
                ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteStartNotLeader);
                return;
            }

            string reason = (message.CommandArgs ?? string.Empty).Trim();
            switch (_votes.Stop(org.Id, reason, DateTime.UtcNow))
            {
                case OrgVoteService.StopOutcome.Stopped:
                    // Captured retail: only the leader also receives "Voting stopped." before the organization notice.
                    SendRef(session, player, OrgChatText.Wrap(OrgChatText.RefId(VoteStoppedLeader)));
                    return;
                default:
                    ClientFeedback.SendFormattedInCategory(player, MemberCategory, VoteNoneInProgress);
                    return;
            }
        }

        /// <summary>Splits '"description" rest': the description is the quoted text, the rest is whitespace separated.</summary>
        static bool TrySplitStartVote(string? args, out string description, out string[] tokens)
        {
            description = string.Empty;
            tokens = Array.Empty<string>();

            string text = (args ?? string.Empty).Trim();
            if (text.Length < 2 || text[0] != '"')
                return false;

            int close = text.IndexOf('"', 1);
            if (close < 0)
                return false;

            description = text.Substring(1, close - 1).Trim();
            tokens = text.Substring(close + 1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return description.Length > 0 && description.Length <= OrgVoteService.MaxDescriptionLength;
        }

        /// <summary>
        /// Parses "N:label" entries. A token without a leading "N:" continues the previous label, so labels may contain
        /// spaces. Numbers must be 1..N in order. Returns 0 on success, else the Text.json id to answer with;
        /// <paramref name="argument"/> is the typed number for the "exceeds the maximum" text.
        /// </summary>
        static int ParseChoices(string[] tokens, out List<string> labels, out string? argument)
        {
            labels = new List<string>();
            argument = null;

            var entries = new List<(string Number, string Label)>();
            foreach (string token in tokens)
            {
                int colon = token.IndexOf(':');
                if (colon > 0 && int.TryParse(token.Substring(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    entries.Add((token.Substring(0, colon), token.Substring(colon + 1)));
                }
                else if (entries.Count > 0)
                {
                    (string number, string label) = entries[^1];
                    entries[^1] = (number, label.Length == 0 ? token : label + " " + token);
                }
                else
                {
                    return VoteStartUsage;
                }
            }

            var numbers = new List<int>();
            foreach ((string numberText, _) in entries)
            {
                if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                    return VoteStartUsage;
                if (number > OrgVoteService.MaxChoices)
                {
                    argument = numberText;
                    return VoteChoiceTooHigh;
                }
                numbers.Add(number);
            }

            if (entries.Count < OrgVoteService.MinChoices)
                return VoteInvalidChoices;
            if (numbers.Distinct().Count() != numbers.Count
                || entries.Select(e => e.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
                return VoteDuplicateChoice;

            for (int index = 0; index < entries.Count; index++)
            {
                if (numbers[index] != index + 1 || entries[index].Label.Trim().Length == 0
                    || entries[index].Label.Trim().Length > OrgVoteService.MaxChoiceLabelLength)
                    return VoteStartUsage;

                labels.Add(entries[index].Label.Trim());
            }

            return 0;
        }
    }
}
