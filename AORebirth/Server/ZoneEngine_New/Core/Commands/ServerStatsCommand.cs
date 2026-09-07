namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;

    public sealed class ServerStatsCommand : IGmCommand
    {
        private readonly IPlayfieldMetricsRegistry _metricsRegistry;

        public ServerStatsCommand(IPlayfieldMetricsRegistry metricsRegistry)
        {
            ArgumentNullException.ThrowIfNull(metricsRegistry);
            _metricsRegistry = metricsRegistry;
        }

        public string Name => "serverstats";

        public int RequiredGmLevel => 1;

        public string Usage => ".serverstats [1|3|5|10|30]";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            int windowSeconds = PlayfieldMetricsWindows.DefaultSeconds;
            if (context.Args.Length > 0)
            {
                if (!int.TryParse(context.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out windowSeconds)
                    || !PlayfieldMetricsWindows.IsAllowed(windowSeconds))
                {
                    GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                    return;
                }
            }

            Playfield? playfield = context.Player.Playfield;
            if (playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            if (!_metricsRegistry.TryGetSnapshot(playfield.Identity.Instance, windowSeconds, out PlayfieldMetricsSnapshot? snapshot)
                || snapshot == null)
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "No metrics for playfield {0}.",
                        playfield.Identity.Instance));
                return;
            }

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Server stats PF {0} window={1}s",
                    snapshot.PlayfieldId,
                    snapshot.WindowSeconds));

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "build={0} tickAvg={1} worldSimAvg={2}",
                    FormatMs(snapshot.BuildElapsedMs, sampleCount: null),
                    FormatMs(snapshot.TickAverageMs, snapshot.TickSampleCount),
                    FormatMs(snapshot.WorldSimAverageMs, snapshot.WorldSimSampleCount)));
        }

        private static string FormatMs(double? valueMs, int? sampleCount)
        {
            if (!valueMs.HasValue)
                return "n/a";

            if (sampleCount.HasValue)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F2}ms (n={1})",
                    valueMs.Value,
                    sampleCount.Value);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:F1}ms", valueMs.Value);
        }
    }
}
