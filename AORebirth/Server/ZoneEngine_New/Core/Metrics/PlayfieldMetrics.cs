namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Time-stamped duration samples for one live metric. Averages are computed on read for a window.
    /// </summary>
    public sealed class PlayfieldMetricSamples
    {
        // 30s at 32 Hz ≈ 960; pad for slight overrun / rate bumps.
        private const int Capacity = 1024;

        private readonly object _sync = new();
        private readonly long[] _timestamps = new long[Capacity];
        private readonly double[] _durationsMs = new double[Capacity];
        private int _writeIndex;
        private int _count;

        public void Record(double durationMs)
        {
            long now = Stopwatch.GetTimestamp();
            lock (_sync)
            {
                _timestamps[_writeIndex] = now;
                _durationsMs[_writeIndex] = durationMs;
                _writeIndex = (_writeIndex + 1) % Capacity;
                if (_count < Capacity)
                    _count++;
            }
        }

        public (double? averageMs, int sampleCount) Average(int windowSeconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

            long now = Stopwatch.GetTimestamp();
            long windowTicks = (long)(windowSeconds * (double)Stopwatch.Frequency);
            long cutoff = now - windowTicks;

            lock (_sync)
            {
                if (_count == 0)
                    return (null, 0);

                double sum = 0.0;
                int matched = 0;
                int start = (_writeIndex - _count + Capacity) % Capacity;
                for (int i = 0; i < _count; i++)
                {
                    int index = (start + i) % Capacity;
                    if (_timestamps[index] < cutoff)
                        continue;

                    sum += _durationsMs[index];
                    matched++;
                }

                if (matched == 0)
                    return (null, 0);

                return (sum / matched, matched);
            }
        }
    }

    /// <summary>Per-playfield build (once) and live tick metrics.</summary>
    public sealed class PlayfieldMetrics
    {
        private readonly object _sync = new();
        private double? _buildElapsedMs;

        public PlayfieldMetrics(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);
            PlayfieldId = playfieldId;
        }

        public int PlayfieldId { get; }

        public PlayfieldMetricSamples TickExecution { get; } = new();

        public PlayfieldMetricSamples WorldSimTick { get; } = new();

        public void RecordBuild(double elapsedMs)
        {
            lock (_sync)
            {
                if (_buildElapsedMs.HasValue)
                    return;

                _buildElapsedMs = elapsedMs;
            }
        }

        public PlayfieldMetricsSnapshot Snapshot(int windowSeconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

            double? buildMs;
            lock (_sync)
                buildMs = _buildElapsedMs;

            (double? tickAvg, int tickCount) = TickExecution.Average(windowSeconds);
            (double? worldAvg, int worldCount) = WorldSimTick.Average(windowSeconds);

            return new PlayfieldMetricsSnapshot(
                PlayfieldId,
                buildMs,
                windowSeconds,
                tickAvg,
                tickCount,
                worldAvg,
                worldCount);
        }
    }

    public sealed class PlayfieldMetricsSnapshot
    {
        public PlayfieldMetricsSnapshot(
            int playfieldId,
            double? buildElapsedMs,
            int windowSeconds,
            double? tickAverageMs,
            int tickSampleCount,
            double? worldSimAverageMs,
            int worldSimSampleCount)
        {
            PlayfieldId = playfieldId;
            BuildElapsedMs = buildElapsedMs;
            WindowSeconds = windowSeconds;
            TickAverageMs = tickAverageMs;
            TickSampleCount = tickSampleCount;
            WorldSimAverageMs = worldSimAverageMs;
            WorldSimSampleCount = worldSimSampleCount;
        }

        public int PlayfieldId { get; }

        public double? BuildElapsedMs { get; }

        public int WindowSeconds { get; }

        public double? TickAverageMs { get; }

        public int TickSampleCount { get; }

        public double? WorldSimAverageMs { get; }

        public int WorldSimSampleCount { get; }
    }

    public static class PlayfieldMetricsWindows
    {
        public static readonly int[] Allowed = [1, 3, 5, 10, 30];

        public const int DefaultSeconds = 1;

        public static bool IsAllowed(int windowSeconds)
        {
            for (int i = 0; i < Allowed.Length; i++)
            {
                if (Allowed[i] == windowSeconds)
                    return true;
            }

            return false;
        }
    }
}
