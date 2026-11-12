namespace ZoneEngine_New.Core.Organizations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// One place of a vote result: every choice sharing the same vote count. Several choices share a place on a tie.
    /// </summary>
    public readonly struct OrgVoteStanding
    {
        public OrgVoteStanding(int place, int votes, IReadOnlyList<int> choiceNumbers)
        {
            Place = place;
            Votes = votes;
            ChoiceNumbers = choiceNumbers;
        }

        /// <summary>0-based place; indexes the ordinal catalog texts (category 508, id 0 = "First place" .. 9 = "Tenth place").</summary>
        public int Place { get; }

        public int Votes { get; }

        public IReadOnlyList<int> ChoiceNumbers { get; }
    }

    /// <summary>
    /// Orders vote results like retail (capture votes\Adonis City, 20261112): descending vote count, choices with the same
    /// count share one place, choice numbers ascending inside a place. Choices with zero votes are still ranked.
    /// </summary>
    public static class OrgVoteRanking
    {
        public const int MaxChoices = 10;

        public static IReadOnlyList<OrgVoteStanding> Rank(IEnumerable<(int ChoiceNumber, int Votes)> tallies)
        {
            var ordered = tallies
                .OrderByDescending(tally => tally.Votes)
                .ThenBy(tally => tally.ChoiceNumber)
                .ToList();

            if (ordered.Count > MaxChoices)
            {
                throw new ArgumentOutOfRangeException(nameof(tallies), ordered.Count, "A vote cannot have more than 10 choices.");
            }

            var standings = new List<OrgVoteStanding>();
            int index = 0;
            while (index < ordered.Count)
            {
                int votes = ordered[index].Votes;
                var choiceNumbers = new List<int>();
                while (index < ordered.Count && ordered[index].Votes == votes)
                {
                    choiceNumbers.Add(ordered[index].ChoiceNumber);
                    index++;
                }

                standings.Add(new OrgVoteStanding(standings.Count, votes, choiceNumbers));
            }

            return standings;
        }
    }
}
