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

            var lines = new List<string>(entries.Count + 16)
            {
                string.Format(CultureInfo.InvariantCulture, "Players online: {0}", entries.Count)
            };

            foreach (var group in entries
                .GroupBy(entry => entry.Profession)
                .OrderBy(group => ProfessionName(group.Key), StringComparer.Ordinal))
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1})", ProfessionName(group.Key), group.Count()));
                foreach (var entry in group
                    .OrderByDescending(entry => entry.Level)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0} - level {1}", entry.Name, entry.Level));
                }
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

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
