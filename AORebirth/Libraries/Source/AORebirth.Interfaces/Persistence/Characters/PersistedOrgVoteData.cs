namespace AORebirth.Interfaces.Persistence.Characters
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One organization vote with its choices and electorate snapshot. Stopped votes are stored only to enforce the
    /// cooldown; callers never display them.
    /// </summary>
    public sealed class PersistedOrgVoteData
    {
        public int VoteId { get; set; }
        public int OrganizationId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public long StartedUnix { get; set; }
        public long EndsUnix { get; set; }
        public long? ClosedUnix { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StopReason { get; set; }
        public int StartedById { get; set; }
        public IList<PersistedOrgVoteChoice> Choices { get; set; } = Array.Empty<PersistedOrgVoteChoice>();
        public IList<PersistedOrgVoteMember> Members { get; set; } = Array.Empty<PersistedOrgVoteMember>();
    }

    public sealed class PersistedOrgVoteChoice
    {
        public int ChoiceNo { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class PersistedOrgVoteMember
    {
        public int CharacterId { get; set; }
        /// <summary>Choice cast by the member, or null while the member has not voted.</summary>
        public int? ChoiceNo { get; set; }
        public long? CastUnix { get; set; }
    }

    public enum OrgVoteStartOutcome
    {
        Started,
        AlreadyActive,
        Cooldown,
        Missing
    }

    public sealed class PersistedOrgVoteStartResult
    {
        public OrgVoteStartOutcome Outcome { get; set; }
        public int VoteId { get; set; }
        /// <summary>Seconds left in the cooldown when <see cref="Outcome"/> is Cooldown.</summary>
        public long CooldownSecondsLeft { get; set; }
    }

    public enum OrgVoteCastOutcome
    {
        Cast,
        AlreadyVoted,
        NotEligible,
        Closed,
        Missing
    }
}
