namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class GetCommand : IGmCommand
    {
        public string Name => "get";

        public int RequiredGmLevel => 1;

        public string Usage => ".get stat <statName|statId> | .get stats";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            string verb = context.Args[0];
            if (string.Equals(verb, "stat", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStat(context);
                return;
            }

            if (string.Equals(verb, "stats", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStats(context);
                return;
            }

            GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
        }

        static void ExecuteStat(GmCommandContext context)
        {
            if (context.Args.Length < 2 || !CharacterStatParser.TryParse(context.Args[1], out CharacterStat stat))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: .get stat <statName|statId>");
                return;
            }

            Player subject = context.ResolveSubject();
            if (!subject.Stats.TryGetValue(stat, out _))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ({1}) = <unset> [{2}]",
                        stat,
                        (int)stat,
                        subject.Name ?? string.Empty));
                return;
            }

            int full = subject.Stats.Get(stat, StatDetail.Full);
            int bas = subject.Stats.Get(stat, StatDetail.Base);
            int bonus = subject.Stats.Get(stat, StatDetail.Bonus);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ({1}) = {2} (base={3} bonus={4}) [{5}]",
                    stat,
                    (int)stat,
                    full,
                    bas,
                    bonus,
                    subject.Name ?? string.Empty));
        }

        static void ExecuteStats(GmCommandContext context)
        {
            Player subject = context.ResolveSubject();
            IReadOnlyList<string> lines = GetStatsAomlBuilder.BuildChatLines(
                subject.Name ?? string.Empty,
                subject.Stats,
                Player.FullCharacterStatSets);

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }
    }

    /// <summary>Builds AOML <c>text://</c> popup links for FullCharacter stat sets.</summary>
    internal static class GetStatsAomlBuilder
    {
        public const int DefaultMaxBodyLength = 900;

        public static IReadOnlyList<string> BuildChatLines(
            string subjectName,
            StatCollection stats,
            IReadOnlyList<CharacterStat[]> sets,
            int maxBodyLength = DefaultMaxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(sets);
            if (maxBodyLength < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBodyLength));

            string title = string.IsNullOrWhiteSpace(subjectName) ? "Stats" : subjectName + " Stats";
            var lines = new List<string>();

            for (int setIndex = 0; setIndex < sets.Count; setIndex++)
            {
                CharacterStat[] set = sets[setIndex];
                if (set == null || set.Length == 0)
                    continue;

                IReadOnlyList<string> rows = BuildRows(stats, set);
                IReadOnlyList<string> chunks = ChunkRows(rows, maxBodyLength);
                int setNumber = setIndex + 1;

                for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    string label = chunks.Count == 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0} — Set {1}", title, setNumber)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} — Set {1} ({2}/{3})",
                            title,
                            setNumber,
                            chunkIndex + 1,
                            chunks.Count);

                    lines.Add(BuildLink(chunks[chunkIndex], label));
                }
            }

            if (lines.Count == 0)
                lines.Add(BuildLink("(no stats)", title));

            return lines;
        }

        public static string BuildLink(string body, string label)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(label);
            return string.Format(
                CultureInfo.InvariantCulture,
                "<a href=\"text://{0}\">{1}</a>",
                body,
                label);
        }

        public static IReadOnlyList<string> BuildRows(StatCollection stats, CharacterStat[] set)
        {
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(set);

            var rows = new List<string>(set.Length);
            for (int i = 0; i < set.Length; i++)
            {
                CharacterStat stat = set[i];
                string valueText;
                if (!stats.TryGetValue(stat, out _))
                    valueText = "<unset>";
                else
                {
                    int full = stats.Get(stat, StatDetail.Full);
                    int bas = stats.Get(stat, StatDetail.Base);
                    int bonus = stats.Get(stat, StatDetail.Bonus);
                    valueText = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} (b={1} +{2})",
                        full,
                        bas,
                        bonus);
                }

                rows.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ({1}) = {2}",
                        stat,
                        (int)stat,
                        valueText));
            }

            return rows;
        }

        public static IReadOnlyList<string> ChunkRows(IReadOnlyList<string> rows, int maxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Count == 0)
                return ["(empty)"];

            var chunks = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                string addition = current.Length == 0 ? row : "<br>" + row;
                if (current.Length > 0 && current.Length + addition.Length > maxBodyLength)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                    addition = row;
                }

                // Single row longer than budget: still emit it alone (client may truncate).
                if (current.Length == 0 && addition.Length > maxBodyLength && chunks.Count > 0)
                {
                    chunks.Add(addition);
                    continue;
                }

                current.Append(addition);
            }

            if (current.Length > 0)
                chunks.Add(current.ToString());

            return chunks;
        }
    }
}
