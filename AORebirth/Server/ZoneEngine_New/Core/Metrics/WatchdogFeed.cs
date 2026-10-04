namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>Bits of <see cref="CharacterStat.DebugFlags"/>; each one opts a GM into a debug channel.</summary>
    [Flags]
    public enum PlayerDebugFlags
    {
        None = 0,

        /// <summary>Slow and stalled playfield ticks, as chat.</summary>
        Watchdog = 1 << 0
    }

    /// <summary>
    /// Sends watchdog notices (slow ticks, stalls, recoveries) to online players with
    /// <see cref="PlayerDebugFlags.Watchdog"/> set. Subscribers are kept here, not read from stats at publish
    /// time: notices come from tick and watcher threads that do not own the subscriber's stats.
    /// </summary>
    internal static class WatchdogFeed
    {
        /// <summary>At most one slow-tick notice per playfield per interval; the rest are counted.</summary>
        const double SlowNoticeIntervalSeconds = 2.0;

        static readonly ConcurrentDictionary<int, Player> Subscribers = new();
        static readonly ConcurrentDictionary<int, Throttle> ThrottleByPlayfield = new();

        public static bool HasSubscribers => !Subscribers.IsEmpty;

        public static bool IsSubscribed(Player player) => Subscribers.TryGetValue(player.Identity.Instance, out Player? current)
            && ReferenceEquals(current, player);

        /// <summary>Matches the subscription to the player's stored flag. Call on the player's owning thread.</summary>
        public static void Sync(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            var flags = (PlayerDebugFlags)player.Stats.GetOrZero(CharacterStat.DebugFlags);
            if ((flags & PlayerDebugFlags.Watchdog) != 0)
                Subscribers[player.Identity.Instance] = player;
            else
                Remove(player);
        }

        public static void Remove(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            Subscribers.TryRemove(new System.Collections.Generic.KeyValuePair<int, Player>(player.Identity.Instance, player));
        }

        public static void Publish(string text)
        {
            if (Subscribers.IsEmpty)
                return;

            string line = DiagAoml.Color(DiagAoml.Orange, "[watchdog] ") + text;
            foreach (Player player in Subscribers.Values)
            {
                IZoneSession? session = player.Session;
                if (session == null)
                    continue;

                try
                {
                    GmCommandFeedback.Send(session, player, line);
                }
                catch (Exception)
                {
                    // A notice must never break the tick or watcher thread that raised it.
                }
            }
        }

        internal static void PublishSlowTick(SlowTick slow)
        {
            if (Subscribers.IsEmpty)
                return;

            Throttle throttle = ThrottleByPlayfield.GetOrAdd(slow.PlayfieldId, static _ => new Throttle());
            long now = Stopwatch.GetTimestamp();
            int suppressed;
            lock (throttle)
            {
                if (throttle.LastSentTicks != 0
                    && now - throttle.LastSentTicks < SlowNoticeIntervalSeconds * Stopwatch.Frequency)
                {
                    throttle.Suppressed++;
                    return;
                }

                suppressed = throttle.Suppressed;
                throttle.Suppressed = 0;
                throttle.LastSentTicks = now;
            }

            string where = slow.WorstDynel != null
                ? " " + DiagAoml.Name(DiagAoml.Safe(slow.WorstDynel))
                : slow.WorstDetail != 0 ? DiagAoml.Muted(" #" + slow.WorstDetail.ToString(CultureInfo.InvariantCulture)) : string.Empty;
            Publish(
                DiagAoml.Name("pf " + slow.PlayfieldId.ToString(CultureInfo.InvariantCulture)) + " slow tick "
                + DiagAoml.Severity(slow.TickMs, slow.LimitMs * 2, slow.LimitMs * 4, DiagAoml.Ms(slow.TickMs) + "ms")
                + DiagAoml.Muted(" / " + DiagAoml.Ms(slow.LimitMs) + "ms")
                + DiagAoml.Label("  worst ") + slow.WorstStage + where + " "
                + DiagAoml.Severity(slow.WorstStageMs, slow.LimitMs * 0.5, slow.LimitMs, DiagAoml.Ms(slow.WorstStageMs) + "ms")
                + (slow.GcPauseMs > 0
                    ? DiagAoml.Label("  gc ") + DiagAoml.Color(DiagAoml.Red, DiagAoml.Ms(slow.GcPauseMs) + "ms")
                        + DiagAoml.Muted(" gen" + Math.Max(0, slow.GcDeepestGeneration).ToString(CultureInfo.InvariantCulture))
                    : string.Empty)
                + (suppressed > 0
                    ? DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "  +{0} more", suppressed))
                    : string.Empty));
        }

        /// <summary>A tick stuck past the stall limit; always red.</summary>
        public static void PublishStall(string text) => Publish(DiagAoml.Color(DiagAoml.Red, text));

        /// <summary>A stalled tick that finally returned.</summary>
        public static void PublishRecovered(string text) => Publish(DiagAoml.Color(DiagAoml.Yellow, text));

        sealed class Throttle
        {
            internal long LastSentTicks;

            internal int Suppressed;
        }
    }
}
