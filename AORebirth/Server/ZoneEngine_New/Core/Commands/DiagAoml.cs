namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Colors and layout for <c>.diag</c> popups and watchdog chat. Numbers are colored by severity
    /// (green fine, yellow watch, red problem) against thresholds the caller picks, usually fractions of
    /// the playfield's tick interval.
    /// </summary>
    internal static class DiagAoml
    {
        public const string Gold = "#FFD700";
        public const string Cyan = "#9CD6E4";
        public const string White = "#FFFFFF";
        public const string Grey = "#8A8A8A";
        public const string Green = "#63E163";
        public const string Yellow = "#FFD040";
        public const string Red = "#FF5050";
        public const string Orange = "#FFA040";

        public static string Color(string color, string text) => "<font color=" + color + ">" + text + "</font>";

        public static string Title(string text) => Color(Gold, text);

        /// <summary>A section heading on its own line, with a blank line above unless it opens the popup.</summary>
        public static string Section(string text) => "<br>" + Color(Gold, text);

        public static string Label(string text) => Color(Cyan, text);

        public static string Muted(string text) => Color(Grey, text);

        public static string Name(string text) => Color(White, text);

        /// <summary><paramref name="text"/> in green, yellow or red as <paramref name="value"/> passes warn / bad.</summary>
        public static string Severity(double value, double warn, double bad, string text)
            => Color(value >= bad ? Red : value >= warn ? Yellow : Green, text);

        /// <summary>Same, for values where lower is worse (achieved tick rate).</summary>
        public static string SeverityLow(double value, double warn, double bad, string text)
            => Color(value <= bad ? Red : value <= warn ? Yellow : Green, text);

        public static string Ms(double value) => value.ToString(value >= 100 ? "F0" : value >= 10 ? "F1" : "F2", CultureInfo.InvariantCulture);

        /// <summary>"label value" with the label cyan, followed by two spaces.</summary>
        public static string Field(string label, string coloredValue) => Label(label) + " " + coloredValue + "&#160;&#160;";

        public static string Indent => "&#160;&#160;&#160;";

        public static string Link(string command, string label)
            => "<a href='chatcmd:///say " + command + "'>" + label + "</a>";

        /// <summary>Keeps names from breaking out of the popup's AOML.</summary>
        public static string Safe(string text)
            => text.Replace('"', '\'').Replace("'", "`", StringComparison.Ordinal).Replace('<', '(').Replace('>', ')');

        /// <summary>
        /// Packs blocks into popup links of at most <paramref name="maxBodyLength"/> characters. A block is never split.
        /// <paramref name="header"/> opens the first page. <paramref name="pageLabel"/> names a multi-page link from the
        /// first and last block index it holds; by default pages are numbered.
        /// </summary>
        public static IReadOnlyList<string> Pages(
            string title,
            string? header,
            IReadOnlyList<string> blocks,
            string separator = "<br>",
            Func<int, int, string>? pageLabel = null,
            int maxBodyLength = GetStatsAomlBuilder.DefaultMaxBodyLength)
        {
            // Every page repeats the title inside the popup; keep the whole body within budget.
            maxBodyLength -= Title(title).Length + "<br>".Length;
            var bodies = new List<(string Body, int First, int Last)>();
            var body = new StringBuilder();
            int first = 0;
            if (!string.IsNullOrEmpty(header))
                body.Append(header);

            for (int i = 0; i < blocks.Count; i++)
            {
                string addition = body.Length == 0 ? blocks[i] : separator + blocks[i];
                if (body.Length > 0 && body.Length + addition.Length > maxBodyLength && i > first)
                {
                    bodies.Add((body.ToString(), first, i - 1));
                    body.Clear();
                    first = i;
                    addition = blocks[i];
                }

                body.Append(addition);
            }

            if (body.Length > 0 || bodies.Count == 0)
                bodies.Add((body.Length == 0 ? Muted("(empty)") : body.ToString(), first, Math.Max(first, blocks.Count - 1)));

            var links = new List<string>(bodies.Count);
            for (int i = 0; i < bodies.Count; i++)
            {
                string label = bodies.Count == 1
                    ? title
                    : pageLabel != null
                        ? title + " " + pageLabel(bodies[i].First, bodies[i].Last)
                        : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, bodies.Count);
                links.Add(GetStatsAomlBuilder.BuildLink(Title(title) + "<br>" + bodies[i].Body, label));
            }

            return links;
        }
    }
}
