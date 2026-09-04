namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using Config = Utility.Config.ConfigReadWrite;

    /// <summary>
    /// Dedicated-thread playfield clock. Targets <c>PlayfieldTickRate</c> with a Stopwatch
    /// cadence and hybrid sleep/spin wait so ticks do not share the thread pool with timers.
    /// </summary>
    internal sealed class PlayfieldHeartbeat : IDisposable
    {
        private readonly Identity _playfieldIdentity;
        private readonly Action<double> _tick;
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);
        private readonly double _targetIntervalSeconds;
        private readonly long _targetIntervalTicks;
        private bool _disposed;

        internal PlayfieldHeartbeat(Identity playfieldIdentity, Action<double> tick)
        {
            ArgumentNullException.ThrowIfNull(tick);

            _playfieldIdentity = playfieldIdentity;
            _tick = tick;

            int configuredTickRate =
                Config.Instance.CurrentConfig == null ? 0 : Config.Instance.CurrentConfig.PlayfieldTickRate;
            TickRate = configuredTickRate > 0 ? configuredTickRate : 32;
            _targetIntervalSeconds = 1.0 / TickRate;
            _targetIntervalTicks = Math.Max(1L, (long)Math.Round(Stopwatch.Frequency * _targetIntervalSeconds));

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "PlayfieldHeartbeat-" + playfieldIdentity.Instance,
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} heartbeat thread tick rate={1}/s interval={2:F3}ms (config PlayfieldTickRate={3}, stopwatch)",
                    playfieldIdentity,
                    TickRate,
                    _targetIntervalSeconds * 1000.0,
                    configuredTickRate));
        }

        internal int TickRate { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopEvent.Set();
            if (!_thread.Join(TimeSpan.FromSeconds(2)))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Playfield {0} heartbeat thread did not exit within 2s",
                        _playfieldIdentity));
            }

            _stopEvent.Dispose();
        }

        private void Run()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long nextDueTicks = stopwatch.ElapsedTicks + _targetIntervalTicks;
            long lastTickTicks = stopwatch.ElapsedTicks;

            while (!_stopEvent.IsSet)
            {
                WaitUntil(stopwatch, nextDueTicks);
                if (_stopEvent.IsSet)
                {
                    break;
                }

                long nowTicks = stopwatch.ElapsedTicks;
                double deltaTime = (nowTicks - lastTickTicks) / (double)Stopwatch.Frequency;
                if (deltaTime <= 0.0)
                {
                    deltaTime = _targetIntervalSeconds;
                }

                lastTickTicks = nowTicks;

                try
                {
                    _tick(deltaTime);
                }
                catch (Exception e)
                {
                    LogUtil.ErrorException(e, false, "Playfield heartbeat failed for {0}", _playfieldIdentity);
                }

                nextDueTicks += _targetIntervalTicks;

                // If the tick overran, skip missed slots so we do not burst catch-up ticks.
                long behind = stopwatch.ElapsedTicks - nextDueTicks;
                if (behind > _targetIntervalTicks)
                {
                    long skippedIntervals = behind / _targetIntervalTicks;
                    nextDueTicks += skippedIntervals * _targetIntervalTicks;
                }
            }
        }

        private void WaitUntil(Stopwatch stopwatch, long dueTicks)
        {
            while (!_stopEvent.IsSet)
            {
                long remainingTicks = dueTicks - stopwatch.ElapsedTicks;
                if (remainingTicks <= 0)
                {
                    return;
                }

                double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;

                // Sleep the coarse remainder; spin the last ~1.5ms for tighter cadence.
                if (remainingMs > 1.5)
                {
                    _stopEvent.Wait(Math.Max(1, (int)(remainingMs - 1.0)));
                    continue;
                }

                Thread.SpinWait(64);
            }
        }
    }
}
