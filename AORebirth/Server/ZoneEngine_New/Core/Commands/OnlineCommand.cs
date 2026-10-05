namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// <c>.online</c>: every player online, grouped by real profession (not VisualProfession), highest level first.
    /// Open to everyone, so each caller is held to a short cooldown.
    /// </summary>
    public sealed class OnlineCommand : IGmCommand
    {
        static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

        readonly Lazy<PlayfieldManager> _playfields;
        readonly ConcurrentDictionary<int, DateTime> _lastUseUtc = new();

        public OnlineCommand(Lazy<PlayfieldManager> playfields)
        {
            ArgumentNullException.ThrowIfNull(playfields);
            _playfields = playfields;
        }

        public string Name => "online";

        public int RequiredGmLevel => 0;

        public string Usage => ".online";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            DateTime nowUtc = DateTime.UtcNow;
            int callerId = context.Player.Identity.Instance;
            if (_lastUseUtc.TryGetValue(callerId, out DateTime lastUtc) && nowUtc - lastUtc < Cooldown)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Please wait a moment before using .online again.");
                return;
            }

            _lastUseUtc[callerId] = nowUtc;

            var entries = _playfields.Value.SnapshotPlayers()
                .Select(player => new
                {
                    Name = string.IsNullOrEmpty(player.Name)
                        ? player.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                        : player.Name,
                    Level = player.Stats.GetOrZero(CharacterStat.Level),
                    Profession = player.Stats.GetOrZero(CharacterStat.Profession)
                })
                .ToList();

            // One block per profession so a page break never separates a heading from its players.
            var blocks = new List<string>();
            foreach (var group in entries
                .GroupBy(entry => entry.Profession)
                .OrderBy(group => ProfessionName(group.Key), StringComparer.Ordinal))
            {
                var block = new System.Text.StringBuilder();
                block.Append(DiagAoml.Section(ProfessionName(group.Key)))
                    .Append(' ')
                    .Append(DiagAoml.Muted("(" + group.Count().ToString(CultureInfo.InvariantCulture) + ")"));

                foreach (var entry in group
                    .OrderByDescending(entry => entry.Level)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
                {
                    block.Append("<br>")
                        .Append(DiagAoml.Indent)
                        .Append(DiagAoml.Name(DiagAoml.Safe(entry.Name)))
                        .Append("&#160;&#160;")
                        .Append(DiagAoml.Label("Lvl"))
                        .Append(' ')
                        .Append(DiagAoml.Color(LevelColor(entry.Level), entry.Level.ToString(CultureInfo.InvariantCulture)));
                }

                blocks.Add(block.ToString());
            }

            string title = string.Format(CultureInfo.InvariantCulture, "Players Online ({0})", entries.Count);
            GmCommandFeedback.SendLines(context.Session, context.Player, DiagAoml.Pages(title, header: null, blocks));
        }

        /// <summary>Level tiers at a glance: grey under 50, green under 100, cyan under 150, yellow under 200, orange 200+.</summary>
        static string LevelColor(int level) => level switch
        {
            >= 200 => DiagAoml.Orange,
            >= 150 => DiagAoml.Yellow,
            >= 100 => DiagAoml.Cyan,
            >= 50 => DiagAoml.Green,
            _ => DiagAoml.Grey
        };

        static string ProfessionName(int profession)
        {
            if (profession <= 0 || !Enum.IsDefined(typeof(Profession), profession))
                return string.Format(CultureInfo.InvariantCulture, "Unknown ({0})", profession);

            string raw = ((Profession)profession).ToString();
            var spaced = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]))
                    spaced.Append(' ');
                spaced.Append(raw[i]);
            }

            return spaced.ToString();
        }
    }
}
