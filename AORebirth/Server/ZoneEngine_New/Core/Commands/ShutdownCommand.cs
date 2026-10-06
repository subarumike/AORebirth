namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using ZoneEngine_New.Core.Lifecycle;

    /// <summary><c>.shutdown &lt;seconds&gt;</c> schedules a timed engine shutdown with a chat countdown; <c>.shutdown abort</c> cancels it.</summary>
    public sealed class ShutdownCommand : IGmCommand
    {
        readonly ShutdownScheduler _scheduler;

        public ShutdownCommand(ShutdownScheduler scheduler)
        {
            ArgumentNullException.ThrowIfNull(scheduler);
            _scheduler = scheduler;
        }

        public string Name => "shutdown";

        public int RequiredGmLevel => 1;

        public string Usage => ".shutdown <seconds> | .shutdown abort";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string issuer = string.IsNullOrEmpty(context.Player.Name)
                ? context.Player.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                : context.Player.Name;

            if (context.Args.Length == 1 && string.Equals(context.Args[0], "abort", StringComparison.OrdinalIgnoreCase))
            {
                if (!_scheduler.Abort(issuer))
                    GmCommandFeedback.Send(context.Session, context.Player, "No shutdown is scheduled.");
                return;
            }

            if (context.Args.Length != 1
                || !int.TryParse(context.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
                || seconds < 1
                || seconds > ShutdownScheduler.MaxSeconds)
            {
                GmCommandFeedback.Send(context.Session, context.Player, string.Format(CultureInfo.InvariantCulture,
                    "Usage: {0} (1 to {1} seconds)", Usage, ShutdownScheduler.MaxSeconds));
                return;
            }

            _scheduler.Schedule(seconds, issuer);
        }
    }
}
