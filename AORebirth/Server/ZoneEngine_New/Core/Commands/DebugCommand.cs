namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;

    /// <summary>
    /// <c>.debug &lt;channel&gt; [on|off]</c>: toggles one of your own debug channels, stored as a bit of
    /// <see cref="CharacterStat.DebugFlags"/> so it survives relog. <c>.debug</c> alone lists the channels.
    /// </summary>
    public sealed class DebugCommand : IGmCommand
    {
        static readonly (string Name, PlayerDebugFlags Flag, string Description)[] Channels =
        [
            ("watchdog", PlayerDebugFlags.Watchdog, "slow tick, stall and recovery notices in chat")
        ];

        public string Name => "debug";

        public int RequiredGmLevel => 1;

        public string Usage => ".debug [watchdog] [on|off]";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Player player = context.Player;
            var flags = (PlayerDebugFlags)player.Stats.GetOrZero(CharacterStat.DebugFlags);

            if (context.Args.Length == 0)
            {
                var rows = new List<string>();
                foreach ((string name, PlayerDebugFlags flag, string description) in Channels)
                {
                    bool on = (flags & flag) != 0;
                    rows.Add(DiagAoml.Label("[debug] ") + DiagAoml.Link(".debug " + name, name) + " "
                        + (on ? DiagAoml.Color(DiagAoml.Green, "ON") : DiagAoml.Muted("off")) + " " + DiagAoml.Muted(description));
                }

                GmCommandFeedback.SendLines(context.Session, player, rows);
                return;
            }

            int index = Array.FindIndex(Channels, c => string.Equals(c.Name, context.Args[0], StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                GmCommandFeedback.Send(context.Session, player, "Unknown debug channel. Usage: " + Usage);
                return;
            }

            (string channel, PlayerDebugFlags channelFlag, _) = Channels[index];
            bool enable = (flags & channelFlag) == 0;
            if (context.Args.Length > 1)
            {
                if (string.Equals(context.Args[1], "on", StringComparison.OrdinalIgnoreCase))
                    enable = true;
                else if (string.Equals(context.Args[1], "off", StringComparison.OrdinalIgnoreCase))
                    enable = false;
                else
                {
                    GmCommandFeedback.Send(context.Session, player, "Usage: " + Usage);
                    return;
                }
            }

            flags = enable ? flags | channelFlag : flags & ~channelFlag;
            player.Stats.Set(CharacterStat.DebugFlags, (int)flags, StatDetail.Base);
            WatchdogFeed.Sync(player);

            GmCommandFeedback.Send(context.Session, player, DiagAoml.Label("[debug] ") + channel + " "
                + (enable ? DiagAoml.Color(DiagAoml.Green, "ON") : DiagAoml.Muted("off")));
        }
    }
}
