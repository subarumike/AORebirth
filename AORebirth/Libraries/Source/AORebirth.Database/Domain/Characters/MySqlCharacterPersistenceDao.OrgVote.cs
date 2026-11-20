namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using AORebirth.Interfaces.Persistence.Characters;

    /// <summary>Organization votes: tables org_vote, org_vote_choice and org_vote_member.</summary>
    public sealed partial class MySqlCharacterPersistenceDao
    {
        public PersistedOrgVoteStartResult StartOrganizationVote(int organizationId, int starterId, string description,
            int durationMinutes, IList<string> choices, IList<int> electorateIds, long startedUnix, long endsUnix, long cooldownSeconds)
        {
            if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
            if (starterId <= 0) throw new ArgumentOutOfRangeException(nameof(starterId));
            if (choices == null || choices.Count < 2) throw new ArgumentOutOfRangeException(nameof(choices));
            if (electorateIds == null) throw new ArgumentNullException(nameof(electorateIds));

            return Transaction((c, t) =>
            {
                // The organizations row lock serialises votes of one organization, as the kick and bank paths do.
                IList<int> locked = QueryWithin(c, t, "SELECT Id FROM organizations WHERE Id=@Id FOR UPDATE",
                    r => r.GetInt32(0), "@Id", organizationId);
                if (locked.Count == 0)
                    return new PersistedOrgVoteStartResult { Outcome = OrgVoteStartOutcome.Missing };

                IList<int> active = QueryWithin(c, t, "SELECT vote_id FROM org_vote WHERE org_id=@Org AND status='Active' LIMIT 1",
                    r => r.GetInt32(0), "@Org", organizationId);
                if (active.Count > 0)
                    return new PersistedOrgVoteStartResult { Outcome = OrgVoteStartOutcome.AlreadyActive };

                IList<long> lastClosed = QueryWithin(c, t,
                    "SELECT closed_utc FROM org_vote WHERE org_id=@Org AND status IN ('Finished','Stopped') "
                    + "ORDER BY closed_utc DESC LIMIT 1",
                    r => r.IsDBNull(0) ? 0L : r.GetInt64(0), "@Org", organizationId);
                if (lastClosed.Count > 0 && lastClosed[0] + cooldownSeconds > startedUnix)
                    return new PersistedOrgVoteStartResult
                    {
                        Outcome = OrgVoteStartOutcome.Cooldown,
                        CooldownSecondsLeft = lastClosed[0] + cooldownSeconds - startedUnix
                    };

                Execute(c, t, "INSERT INTO org_vote (org_id,description,duration_minutes,started_utc,ends_utc,closed_utc,status,"
                    + "stop_reason,started_by_id,last_updated) VALUES (@Org,@Desc,@Minutes,@Start,@Ends,NULL,'Active',NULL,@Starter,@Now)",
                    "@Org", organizationId, "@Desc", description ?? string.Empty, "@Minutes", durationMinutes,
                    "@Start", startedUnix, "@Ends", endsUnix, "@Starter", starterId, "@Now", startedUnix);

                long voteId;
                using (IDbCommand command = Command(c, t, "SELECT LAST_INSERT_ID()"))
                    voteId = Convert.ToInt64(command.ExecuteScalar());

                for (int i = 0; i < choices.Count; i++)
                    Execute(c, t, "INSERT INTO org_vote_choice (vote_id,choice_no,label) VALUES (@Vote,@No,@Label)",
                        "@Vote", voteId, "@No", i + 1, "@Label", choices[i] ?? string.Empty);

                foreach (int memberId in electorateIds)
                    Execute(c, t, "INSERT INTO org_vote_member (vote_id,char_id,char_name,choice_no,cast_at) "
                        + "VALUES (@Vote,@Char,NULL,NULL,NULL)", "@Vote", voteId, "@Char", memberId);

                return new PersistedOrgVoteStartResult { Outcome = OrgVoteStartOutcome.Started, VoteId = (int)voteId };
            });
        }

        public PersistedOrgVoteData? LoadShownOrganizationVote(int organizationId)
        {
            if (organizationId <= 0) return null;
            IList<int> ids = Query("SELECT vote_id FROM org_vote WHERE org_id=@Org AND status IN ('Active','Finished') "
                + "ORDER BY (status='Active') DESC, vote_id DESC LIMIT 1", r => r.GetInt32(0), "@Org", organizationId);
            return ids.Count == 0 ? null : LoadOrganizationVote(ids[0]);
        }

        public PersistedOrgVoteData? LoadOrganizationVote(int voteId)
        {
            if (voteId <= 0) return null;
            IList<PersistedOrgVoteData> headers = Query("SELECT vote_id,org_id,description,duration_minutes,started_utc,ends_utc,"
                + "closed_utc,status,stop_reason,started_by_id FROM org_vote WHERE vote_id=@Id LIMIT 1",
                r => new PersistedOrgVoteData
                {
                    VoteId = r.GetInt32(0),
                    OrganizationId = r.GetInt32(1),
                    Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                    DurationMinutes = r.GetInt32(3),
                    StartedUnix = r.GetInt64(4),
                    EndsUnix = r.GetInt64(5),
                    ClosedUnix = r.IsDBNull(6) ? (long?)null : r.GetInt64(6),
                    Status = r.GetString(7),
                    StopReason = r.IsDBNull(8) ? null : r.GetString(8),
                    StartedById = r.GetInt32(9)
                }, "@Id", voteId);
            if (headers.Count == 0) return null;

            PersistedOrgVoteData vote = headers[0];
            vote.Choices = Query("SELECT choice_no,label FROM org_vote_choice WHERE vote_id=@Id ORDER BY choice_no",
                r => new PersistedOrgVoteChoice { ChoiceNo = r.GetInt32(0), Label = r.GetString(1) }, "@Id", voteId);
            vote.Members = Query("SELECT char_id,choice_no,cast_at FROM org_vote_member WHERE vote_id=@Id ORDER BY char_id",
                r => new PersistedOrgVoteMember
                {
                    CharacterId = r.GetInt32(0),
                    ChoiceNo = r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
                    CastUnix = r.IsDBNull(2) ? (long?)null : r.GetInt64(2)
                }, "@Id", voteId);
            return vote;
        }

        public IList<int> LoadDueActiveVoteIds(long nowUnix)
            => Query("SELECT vote_id FROM org_vote WHERE status='Active' AND ends_utc<=@Now", r => r.GetInt32(0), "@Now", nowUnix);

        public OrgVoteCastOutcome CastOrganizationVote(int voteId, int characterId, int choiceNo, long nowUnix)
        {
            if (voteId <= 0) throw new ArgumentOutOfRangeException(nameof(voteId));
            if (characterId <= 0) throw new ArgumentOutOfRangeException(nameof(characterId));
            if (choiceNo < 1 || choiceNo > 10) throw new ArgumentOutOfRangeException(nameof(choiceNo));

            return Transaction((c, t) =>
            {
                // The vote row lock serialises casts, so one member can never cast twice.
                IList<(string Status, long EndsUnix)> votes = QueryWithin(c, t,
                    "SELECT status,ends_utc FROM org_vote WHERE vote_id=@Id FOR UPDATE",
                    r => (r.GetString(0), r.GetInt64(1)), "@Id", voteId);
                if (votes.Count == 0 || votes[0].Status != "Active" || nowUnix >= votes[0].EndsUnix)
                    return votes.Count == 0 ? OrgVoteCastOutcome.Missing : OrgVoteCastOutcome.Closed;

                IList<int?> rows = QueryWithin(c, t,
                    "SELECT choice_no FROM org_vote_member WHERE vote_id=@Id AND char_id=@Char FOR UPDATE",
                    r => r.IsDBNull(0) ? (int?)null : r.GetInt32(0), "@Id", voteId, "@Char", characterId);
                if (rows.Count == 0)
                    return OrgVoteCastOutcome.NotEligible;
                if (rows[0].HasValue)
                    return OrgVoteCastOutcome.AlreadyVoted;

                Execute(c, t, "UPDATE org_vote_member SET choice_no=@Choice,cast_at=@Now "
                    + "WHERE vote_id=@Id AND char_id=@Char AND choice_no IS NULL",
                    "@Choice", choiceNo, "@Now", nowUnix, "@Id", voteId, "@Char", characterId);
                return OrgVoteCastOutcome.Cast;
            });
        }

        public bool CloseOrganizationVote(int voteId, string status, string? stopReason, long nowUnix)
        {
            if (voteId <= 0) throw new ArgumentOutOfRangeException(nameof(voteId));
            if (status != "Finished" && status != "Stopped") throw new ArgumentOutOfRangeException(nameof(status));

            return Transaction((c, t) =>
            {
                int updated = Execute(c, t,
                    "UPDATE org_vote SET status=@Status,stop_reason=@Reason,closed_utc=@Now,last_updated=@Now "
                    + "WHERE vote_id=@Id AND status='Active'",
                    "@Status", status, "@Reason", stopReason, "@Now", nowUnix, "@Id", voteId);

                // A stopped vote is never shown again; only its org_vote header is kept for the cooldown.
                if (updated > 0 && status == "Stopped")
                {
                    Execute(c, t, "DELETE FROM org_vote_member WHERE vote_id=@Id", "@Id", voteId);
                    Execute(c, t, "DELETE FROM org_vote_choice WHERE vote_id=@Id", "@Id", voteId);
                }

                return updated > 0;
            });
        }
    }
}
