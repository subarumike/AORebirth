namespace ZoneEngine_New.Core.Organizations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;

    using AORebirth.Interfaces.Persistence.Characters;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Guild votes (rules.txt step 5). The leader or a GM starts a vote; every member of the org at start, online or not,
    /// may cast one vote. A vote closes at its deadline and the results go to the org. The electorate is snapshotted at
    /// start, so a member who joins later cannot vote, and a member who leaves keeps a vote cast before leaving.
    /// A stopped vote is hidden from display but keeps the cooldown.
    /// </summary>
    public sealed class OrgVoteService : IDisposable
    {
        public const int MinChoices = 2;
        public const int MaxChoices = OrgVoteRanking.MaxChoices;
        public const int MinDurationMinutes = 2;
        public const int MaxDurationMinutes = 10080;
        public const int MaxDescriptionLength = 255;
        public const int MaxChoiceLabelLength = 64;

        static readonly TimeSpan ClosePeriod = TimeSpan.FromSeconds(5);

        // Text.json ids. Ordinals 0..9 are "First place" .. "Tenth place", so a place index is its own id.
        const int ResultsHeaderTextId = 16378820;
        const int NoVotesTextId = 88959635;
        const int MembersVotedTextId = 112284196;
        const int SinglePlaceTextId = 5931833;
        const int TiedPlaceTextId = 54457652;
        const int StartNoticeTextId = 206008469;
        const int StopReasonTextId = 166906756;
        const int NoVotingTextId = 179440135;
        const int PreviousResultsTextId = 147575765;
        const int InfoTextId = 89144094;
        const int StatusTextId = 95062979;
        const int MinutesRemainingTextId = 67317284;
        const int VotingFinishedTextId = 184602788;
        const int NotVotedTextId = 97773172;
        const int VotedTextId = 181849080;

        // Captured retail status field of a finished vote: two spaces after the results header.
        const string FinishedStatusField = "  ";

        readonly IOrganizationRepository _organizations;
        readonly IChatEngineLink _chat;
        readonly IZoneLogger _logger;
        readonly GameplaySettings _settings;
        readonly Timer _timer;
        int _closing;

        public OrgVoteService(IOrganizationRepository organizations, IChatEngineLink chat, IZoneLogger logger,
            GameplaySettings settings)
        {
            _organizations = organizations;
            _chat = chat;
            _logger = logger;
            _settings = settings;
            _timer = new Timer(_ => CloseDueVotes(DateTime.UtcNow), null, ClosePeriod, ClosePeriod);
        }

        public void Dispose() => _timer.Dispose();

        /// <summary>Real-time seconds between a vote ending or being stopped and the next start (GameplaySettings Org).</summary>
        public int CooldownSeconds => _settings.Org.VoteCooldownSeconds;

        public enum StopOutcome
        {
            Stopped,
            NoActiveVote
        }

        public enum CastStatus
        {
            Cast,
            AlreadyVoted,
            NotEligible,
            Closed,
                NoVote,
                InvalidChoice,
                ChoiceTooHigh
            }

        public readonly record struct CastResult(CastStatus Status, string? ChoiceLabel);

        /// <summary>Starts a vote for the organization. Choices are numbered 1..N in the order given.</summary>
        public PersistedOrgVoteStartResult Start(int organizationId, int starterId, string description, int durationMinutes,
            IList<string> labels, DateTime nowUtc)
        {
            long startedUnix = ToUnix(nowUtc);
            long endsUnix = startedUnix + durationMinutes * 60L;
            IList<int> electorate = _organizations.GetMemberIds(organizationId);

            PersistedOrgVoteStartResult result = _organizations.StartVote(organizationId, starterId, description, durationMinutes,
                labels, electorate, startedUnix, endsUnix, CooldownSeconds);

            if (result.Outcome == OrgVoteStartOutcome.Started)
            {
                string candidates = string.Join(" ", labels.Select((label, index) => (index + 1).ToString(CultureInfo.InvariantCulture) + ":" + label));
                _chat.SendOrganizationMessage(organizationId, OrgChatText.Wrap(OrgChatText.RefId(StartNoticeTextId)
                    + OrgChatText.TextArg(description) + "u" + OrgChatText.EncodeInt((uint)durationMinutes)
                    + OrgChatText.TextArg(candidates)));
            }

            return result;
        }

        /// <summary>Stops the Active vote of the organization. The stopped vote is never displayed again.</summary>
        public StopOutcome Stop(int organizationId, string reason, DateTime nowUtc)
        {
            PersistedOrgVoteData vote = _organizations.GetShownVote(organizationId);
            if (vote == null || vote.Status != "Active")
                return StopOutcome.NoActiveVote;

            string stopReason = reason ?? string.Empty;
            if (!_organizations.CloseVote(vote.VoteId, "Stopped", stopReason, ToUnix(nowUtc)))
                return StopOutcome.NoActiveVote;

            // Captured retail: the reason is always passed, so an empty reason shows as "" in the notice.
            _chat.SendOrganizationMessage(organizationId,
                OrgChatText.Wrap(OrgChatText.RefId(StopReasonTextId) + OrgChatText.TextArg(stopReason)));
            return StopOutcome.Stopped;
        }

        /// <summary>Records one vote. The choice must exist in the vote; the member must be in the electorate.</summary>
        public CastResult Cast(int organizationId, int voterId, int choiceNo, DateTime nowUtc)
        {
            PersistedOrgVoteData vote = _organizations.GetShownVote(organizationId);
            if (vote == null || vote.Status != "Active")
                return new CastResult(CastStatus.NoVote, null);

            PersistedOrgVoteChoice choice = vote.Choices.FirstOrDefault(c => c.ChoiceNo == choiceNo);
            if (choice == null)
                return new CastResult(choiceNo > vote.Choices.Count ? CastStatus.ChoiceTooHigh : CastStatus.InvalidChoice, null);

            switch (_organizations.CastVote(vote.VoteId, voterId, choiceNo, ToUnix(nowUtc)))
            {
                case OrgVoteCastOutcome.Cast:
                    return new CastResult(CastStatus.Cast, choice.Label);
                case OrgVoteCastOutcome.AlreadyVoted:
                    return new CastResult(CastStatus.AlreadyVoted, null);
                case OrgVoteCastOutcome.NotEligible:
                    return new CastResult(CastStatus.NotEligible, null);
                case OrgVoteCastOutcome.Closed:
                    return new CastResult(CastStatus.Closed, null);
                default:
                    return new CastResult(CastStatus.NoVote, null);
            }
        }

        /// <summary>
        /// Text of /org vote info: the Active vote with the viewer's own status, or the latest Finished vote's results.
        /// Null when the organization has nothing to show.
        /// </summary>
        public string? Describe(int organizationId, int viewerId, DateTime nowUtc)
        {
            PersistedOrgVoteData vote = _organizations.GetShownVote(organizationId);
            if (vote == null)
                return null;

            if (vote.Status == "Finished")
                return OrgChatText.Wrap(OrgChatText.RefId(NoVotingTextId)) + OrgChatText.Wrap(OrgChatText.RefId(PreviousResultsTextId))
                    + BuildResults(vote);

            long remainingSeconds = Math.Max(0, vote.EndsUnix - ToUnix(nowUtc));
            long remainingMinutes = (remainingSeconds + 59) / 60;
            var text = new StringBuilder();
            text.Append(OrgChatText.Wrap(OrgChatText.RefId(InfoTextId) + OrgChatText.TextArg(vote.Description)));
            text.Append(Candidates(vote)).Append(' ');
            text.Append(OrgChatText.Wrap(OrgChatText.RefId(StatusTextId)));
            text.Append(remainingSeconds == 0
                ? OrgChatText.Wrap(OrgChatText.RefId(VotingFinishedTextId))
                : OrgChatText.Wrap(OrgChatText.RefId(MinutesRemainingTextId) + "u" + OrgChatText.EncodeInt((uint)remainingMinutes)));

            PersistedOrgVoteMember viewer = vote.Members.FirstOrDefault(m => m.CharacterId == viewerId);
            if (viewer == null)
                return text.ToString();

            if (viewer.ChoiceNo is { } choiceNo)
            {
                string label = vote.Choices.First(c => c.ChoiceNo == choiceNo).Label;
                text.Append(OrgChatText.Wrap(OrgChatText.RefId(VotedTextId)
                    + OrgChatText.TextArg(choiceNo.ToString(CultureInfo.InvariantCulture) + ":" + label)));
            }
            else
            {
                text.Append(OrgChatText.Wrap(OrgChatText.RefId(NotVotedTextId)));
            }

            return text.ToString();
        }

        /// <summary>Closes every Active vote past its deadline and sends its results to the org.</summary>
        void CloseDueVotes(DateTime nowUtc)
        {
            if (Interlocked.Exchange(ref _closing, 1) == 1)
                return;

            try
            {
                long nowUnix = ToUnix(nowUtc);
                foreach (int voteId in _organizations.GetDueVoteIds(nowUnix))
                {
                    // Close before reading: a cast committed before the deadline is then included in the results.
                    if (!_organizations.CloseVote(voteId, "Finished", null, nowUnix))
                        continue;

                    PersistedOrgVoteData vote = _organizations.GetVote(voteId);
                    if (vote != null)
                        _chat.SendOrganizationMessage(vote.OrganizationId, BuildResults(vote));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Organization vote close failed.");
            }
            finally
            {
                Interlocked.Exchange(ref _closing, 0);
            }
        }

        /// <summary>Results block: "No votes were cast." or one line per place, ties sharing a place.</summary>
        static string BuildResults(PersistedOrgVoteData vote)
        {
            var text = new StringBuilder();
            text.Append(OrgChatText.Wrap(OrgChatText.RefId(ResultsHeaderTextId) + OrgChatText.TextArg(vote.Description)));

            int voters = vote.Members.Count(m => m.ChoiceNo.HasValue);
            if (voters == 0)
            {
                text.Append(OrgChatText.Wrap(OrgChatText.RefId(NoVotesTextId)));
                return text.ToString();
            }

            text.Append(FinishedStatusField);
            text.Append(OrgChatText.Wrap(OrgChatText.RefId(MembersVotedTextId) + "u" + OrgChatText.EncodeInt((uint)voters)));

            var tallies = vote.Choices.Select(choice =>
                (choice.ChoiceNo, Votes: vote.Members.Count(m => m.ChoiceNo == choice.ChoiceNo)));
            foreach (OrgVoteStanding standing in OrgVoteRanking.Rank(tallies))
            {
                string labels = string.Join(", ", standing.ChoiceNumbers.Select(number =>
                    number.ToString(CultureInfo.InvariantCulture) + ":" + vote.Choices.First(c => c.ChoiceNo == number).Label));
                int lineTextId = standing.ChoiceNumbers.Count > 1 ? TiedPlaceTextId : SinglePlaceTextId;
                // Place index 0..9 is the ordinal (0 = First place); lines are separated by a line feed, none after the last.
                text.Append('\n').Append(OrgChatText.Wrap(OrgChatText.RefId(lineTextId) + OrgChatText.NestedOrdinal(standing.Place)
                    + "u" + OrgChatText.EncodeInt((uint)standing.Votes)));
                text.Append(labels).Append('.');
            }

            return text.ToString();
        }

        static string Candidates(PersistedOrgVoteData vote)
            => string.Join(" ", vote.Choices.Select(c => c.ChoiceNo.ToString(CultureInfo.InvariantCulture) + ":" + c.Label));

        static long ToUnix(DateTime utc) => (long)(utc - DateTime.UnixEpoch).TotalSeconds;
    }
}
