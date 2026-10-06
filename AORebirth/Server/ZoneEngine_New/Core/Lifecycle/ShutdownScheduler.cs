namespace ZoneEngine_New.Core.Lifecycle
{
    using System;
    using System.Globalization;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>Stops the whole engine the normal way: players saved, services stopped, process exits.</summary>
    public interface IEngineShutdown
    {
        void Shutdown(string reason);
    }

    /// <summary>
    /// GM <c>.shutdown &lt;seconds&gt;</c>: counts down on its own timer thread (never a playfield tick, which the
    /// shutdown itself stops), tells every online player at the usual checkpoints, then shuts the engine down.
    /// <c>.shutdown abort</c> cancels it. One countdown at a time; scheduling again replaces it.
    /// </summary>
    public sealed class ShutdownScheduler : IDisposable
    {
        /// <summary>Remaining seconds at which players are told how long is left.</summary>
        static readonly int[] Checkpoints =
            [3600, 1800, 1200, 900, 600, 300, 240, 180, 120, 60, 30, 15, 10, 5, 4, 3, 2, 1];

        public const int MaxSeconds = 24 * 60 * 60;

        readonly Lazy<PlayfieldManager> _playfields;
        readonly IEngineShutdown _shutdown;
        readonly IZoneLogger _logger;
        readonly object _gate = new();

        Timer? _timer;
        DateTime _dueUtc;
        int _lastAnnounced;
        int _generation;

        public ShutdownScheduler(Lazy<PlayfieldManager> playfields, IEngineShutdown shutdown, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(shutdown);
            ArgumentNullException.ThrowIfNull(logger);
            _playfields = playfields;
            _shutdown = shutdown;
            _logger = logger;
        }

        /// <summary>Seconds until the scheduled shutdown, or null when none is pending.</summary>
        public int? SecondsRemaining
        {
            get
            {
                lock (_gate)
                    return _timer == null ? null : Math.Max(0, (int)Math.Ceiling((_dueUtc - DateTime.UtcNow).TotalSeconds));
            }
        }

        /// <summary>Starts (or replaces) a countdown of <paramref name="seconds"/> and announces it.</summary>
        public void Schedule(int seconds, string requestedBy)
        {
            if (seconds < 1 || seconds > MaxSeconds)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            lock (_gate)
            {
                _timer?.Dispose();
                _generation++;
                int generation = _generation;
                _dueUtc = DateTime.UtcNow.AddSeconds(seconds);
                _lastAnnounced = seconds;
                _timer = new Timer(_ => Tick(generation), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Shutdown scheduled in {0}s by {1}", seconds, requestedBy));
            Announce("The server will shut down in " + FormatDuration(seconds) + ".");
        }

        /// <summary>Cancels a pending countdown. False when none was pending.</summary>
        public bool Abort(string requestedBy)
        {
            lock (_gate)
            {
                if (_timer == null)
                    return false;

                _timer.Dispose();
                _timer = null;
                _generation++;
            }

            _logger.Info("Shutdown aborted by " + requestedBy);
            Announce("The scheduled server shutdown has been cancelled.");
            return true;
        }

        void Tick(int generation)
        {
            int remaining;
            int? checkpoint = null;
            lock (_gate)
            {
                if (_timer == null || generation != _generation)
                    return;

                remaining = Math.Max(0, (int)Math.Ceiling((_dueUtc - DateTime.UtcNow).TotalSeconds));
                if (remaining <= 0)
                {
                    _timer.Dispose();
                    _timer = null;
                    _generation++;
                }
                else
                {
                    // The largest checkpoint reached since the last announcement, so a late tick never skips one.
                    foreach (int point in Checkpoints)
                    {
                        if (point < _lastAnnounced && remaining <= point)
                        {
                            checkpoint = point;
                            break;
                        }
                    }

                    if (checkpoint != null)
                        _lastAnnounced = checkpoint.Value;
                }
            }

            if (remaining <= 0)
            {
                Announce("The server is shutting down now.");
                // Give the final line a moment to reach clients before the sessions close.
                Thread.Sleep(1000);
                _shutdown.Shutdown("scheduled shutdown");
                return;
            }

            if (checkpoint != null)
                Announce("The server will shut down in " + FormatDuration(checkpoint.Value) + ".");
        }

        void Announce(string text)
        {
            foreach (Player player in _playfields.Value.SnapshotPlayers())
            {
                Playfield? playfield = player.Playfield;
                if (player.Session == null || playfield == null)
                    continue;

                playfield.DispatchPlayerProjection(player, () =>
                    player.Session?.Send(new ChatTextMessage
                    {
                        Identity = player.Identity,
                        Text = text,
                        Unknown1 = 0,
                        Unknown2 = 0,
                        Unknown3 = 0
                    }));
            }
        }

        static string FormatDuration(int seconds)
        {
            if (seconds >= 3600 && seconds % 3600 == 0)
                return Plural(seconds / 3600, "hour");
            if (seconds >= 60 && seconds % 60 == 0)
                return Plural(seconds / 60, "minute");
            if (seconds >= 60)
                return Plural(seconds / 60, "minute") + " " + Plural(seconds % 60, "second");
            return Plural(seconds, "second");
        }

        static string Plural(int value, string unit)
            => value.ToString(CultureInfo.InvariantCulture) + " " + unit + (value == 1 ? string.Empty : "s");

        public void Dispose()
        {
            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
                _generation++;
            }
        }
    }
}
