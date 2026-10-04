namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    using AORebirth.World.Pathfinding;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// GM-driven tick profiler fed by <see cref="TickStallWatch"/> stage segments. Two parts:
    /// <list type="bullet">
    /// <item>The slow-tick log is always on: one comparison per tick, a record only for ticks longer than
    /// the playfield's configured heartbeat interval (or a GM override, <see cref="OverrideThresholdMs"/>).</item>
    /// <item>Stage and per-dynel aggregates only collect while recording, which stops itself after the
    /// requested duration so a forgotten session cannot leave the overhead on.</item>
    /// </list>
    /// Each playfield's tick thread is the only writer of its own rows; GM readers copy under the row lock.
    /// </summary>
    internal static class TickProfiler
    {
        public const int DefaultRecordSeconds = 60;
        public const int MaxRecordSeconds = 600;
        /// <summary>Limit when a playfield has no heartbeat rate yet (the default 32 ticks/s).</summary>
        const double FallbackBudgetMs = 1000.0 / 32;
        const int SlowTickCapacity = 64;

        static readonly ConcurrentDictionary<(int PlayfieldId, string Stage), SegmentStats> Stages = new();
        static readonly ConcurrentDictionary<(int PlayfieldId, ulong Dynel), DynelStats> Dynels = new();
        static readonly ConcurrentDictionary<int, SegmentStats> Ticks = new();
        static readonly ConcurrentDictionary<(int PlayfieldId, ulong Dynel, string Stage), PathStats> Paths = new();
        /// <summary>Stage row for GC pause time inside ticks. It overlaps the stage that was interrupted.</summary>
        public const string GcPauseStage = "gc.pause";

        static readonly object SlowSync = new();
        static readonly SlowTick[] SlowRing = new SlowTick[SlowTickCapacity];
        static int slowWrite;
        static int slowCount;

        static volatile bool recording;
        static long startedTicks;
        static long stopAtTicks;
        static long stoppedTicks;
        static int overrideThresholdMs;

        public static bool IsRecording => recording;

        /// <summary>GM override of the slow-tick limit in ms; 0 uses each playfield's tick interval.</summary>
        public static int OverrideThresholdMs
        {
            get => Volatile.Read(ref overrideThresholdMs);
            set => Volatile.Write(ref overrideThresholdMs, Math.Clamp(value, 0, 60_000));
        }

        public static string ThresholdDescription
            => OverrideThresholdMs > 0
                ? OverrideThresholdMs.ToString(CultureInfo.InvariantCulture) + "ms (override)"
                : "configured tick interval";

        static double LimitMs(double budgetMs)
        {
            int overrideMs = OverrideThresholdMs;
            if (overrideMs > 0)
                return overrideMs;
            return budgetMs > 0 ? budgetMs : FallbackBudgetMs;
        }

        /// <summary>Seconds of data behind the aggregates: up to now while recording, else up to the stop.</summary>
        public static double RecordedSeconds
        {
            get
            {
                long start = Interlocked.Read(ref startedTicks);
                if (start == 0)
                    return 0;
                long end = recording ? Stopwatch.GetTimestamp() : Interlocked.Read(ref stoppedTicks);
                return Math.Max(0, end - start) / (double)Stopwatch.Frequency;
            }
        }

        public static double RemainingSeconds
            => recording ? Math.Max(0, Interlocked.Read(ref stopAtTicks) - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency : 0;

        /// <summary>Clears the aggregates and records for <paramref name="seconds"/>.</summary>
        public static void Start(int seconds)
        {
            seconds = Math.Clamp(seconds, 1, MaxRecordSeconds);
            recording = false;
            ClearAggregates();
            long now = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref startedTicks, now);
            Interlocked.Exchange(ref stopAtTicks, now + seconds * Stopwatch.Frequency);
            recording = true;
        }

        public static void Stop()
        {
            if (!recording)
                return;
            recording = false;
            Interlocked.Exchange(ref stoppedTicks, Stopwatch.GetTimestamp());
        }

        public static void Reset()
        {
            ClearAggregates();
            lock (SlowSync)
            {
                Array.Clear(SlowRing);
                slowWrite = 0;
                slowCount = 0;
            }

            long now = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref startedTicks, recording ? now : 0);
            Interlocked.Exchange(ref stoppedTicks, now);
        }

        internal static void StopIfDue(long now)
        {
            if (recording && now >= Interlocked.Read(ref stopAtTicks))
                Stop();
        }

        internal static void RecordSegment(int playfieldId, string stage, Dynel? dynel, long elapsedTicks, long allocatedBytes)
        {
            Stages.GetOrAdd((playfieldId, stage), static _ => new SegmentStats()).Add(elapsedTicks, allocatedBytes);
            if (dynel != null)
                DynelRow(playfieldId, dynel).AddStage(stage, elapsedTicks, allocatedBytes);
        }

        /// <summary>One navmesh search on this tick thread, charged to the dynel whose tick ran it.</summary>
        internal static void RecordPathSearch(string stage, PathSearchOutcome outcome, int iterations, long elapsedTicks)
            => PathRow(stage).AddSearch(outcome, iterations, elapsedTicks);

        /// <summary>A reachability answer served from cache instead of a search.</summary>
        internal static void RecordPathCacheHit(string stage) => PathRow(stage).AddCacheHit();

        /// <summary>Path rows with the most search time first.</summary>
        public static IReadOnlyList<PathSummary> PathSummaries(int? playfieldId)
        {
            var all = new List<PathSummary>();
            foreach (KeyValuePair<(int PlayfieldId, ulong Dynel, string Stage), PathStats> pair in Paths)
            {
                if (playfieldId.HasValue && pair.Key.PlayfieldId != playfieldId.Value)
                    continue;
                all.Add(pair.Value.Read());
            }

            all.Sort(static (a, b) => b.TotalMs.CompareTo(a.TotalMs));
            return all;
        }

        static PathStats PathRow(string stage)
        {
            int playfieldId = TickStallWatch.CurrentPlayfieldId;
            Dynel? dynel = TickStallWatch.CurrentDynel;
            return Paths.GetOrAdd(
                (playfieldId, dynel?.Identity.Long() ?? 0, stage),
                static (key, d) => new PathStats(
                    key.PlayfieldId,
                    d?.Identity.Instance ?? 0,
                    d == null ? "(playfield)" : Describe(d),
                    key.Stage),
                dynel);
        }

        internal static void RecordDynel(int playfieldId, Dynel dynel, long elapsedTicks)
        {
            if (recording)
                DynelRow(playfieldId, dynel).AddTick(elapsedTicks);
        }

        internal static void RecordTick(
            int playfieldId,
            double budgetMs,
            double gcPauseMs,
            int gcCollections,
            int gen1Collections,
            int gen2Collections,
            long allocatedBytes,
            long tickTicks,
            string? worstStage,
            int worstDetail,
            Dynel? worstDynel,
            long worstSegmentTicks,
            long stages)
        {
            if (recording)
            {
                Ticks.GetOrAdd(playfieldId, static _ => new SegmentStats()).Add(tickTicks, allocatedBytes);

                // GC pauses overlap whatever stage was running; kept as its own row so they can be told apart.
                if (gcPauseMs > 0)
                    Stages.GetOrAdd((playfieldId, GcPauseStage), static _ => new SegmentStats())
                        .Add((long)(gcPauseMs * Stopwatch.Frequency / 1000.0));
            }

            double tickMs = TickStallWatch.Milliseconds(tickTicks);
            double limitMs = LimitMs(budgetMs);
            if (tickMs <= limitMs)
                return;

            var slow = new SlowTick(
                DateTime.UtcNow,
                playfieldId,
                tickMs,
                limitMs,
                worstStage ?? "(none)",
                worstDetail,
                worstDynel == null ? null : Describe(worstDynel) + " #" + worstDynel.Identity.Instance.ToString(CultureInfo.InvariantCulture),
                TickStallWatch.Milliseconds(worstSegmentTicks),
                stages)
            {
                GcPauseMs = gcPauseMs,
                GcCollections = gcCollections,
                GcGen1 = gen1Collections,
                GcGen2 = gen2Collections
            };

            lock (SlowSync)
            {
                SlowRing[slowWrite] = slow;
                slowWrite = (slowWrite + 1) % SlowTickCapacity;
                if (slowCount < SlowTickCapacity)
                    slowCount++;
            }

            WatchdogFeed.PublishSlowTick(slow);
        }

        /// <summary>Newest first.</summary>
        public static IReadOnlyList<SlowTick> SlowTicks()
        {
            lock (SlowSync)
            {
                var result = new List<SlowTick>(slowCount);
                for (int i = 1; i <= slowCount; i++)
                    result.Add(SlowRing[(slowWrite - i + SlowTickCapacity) % SlowTickCapacity]);
                return result;
            }
        }

        public static IReadOnlyList<StageSummary> StageSummaries(int? playfieldId) => StageSummaries(playfieldId, byAllocation: false);

        /// <summary>Stage rows, most total time (or most bytes allocated) first.</summary>
        public static IReadOnlyList<StageSummary> StageSummaries(int? playfieldId, bool byAllocation)
        {
            var result = new List<StageSummary>();
            foreach (KeyValuePair<(int PlayfieldId, string Stage), SegmentStats> pair in Stages)
            {
                if (playfieldId.HasValue && pair.Key.PlayfieldId != playfieldId.Value)
                    continue;
                result.Add(new StageSummary(pair.Key.PlayfieldId, pair.Key.Stage, pair.Value.Read()));
            }

            if (byAllocation)
                result.Sort(static (a, b) => b.Stats.AllocatedBytes.CompareTo(a.Stats.AllocatedBytes));
            else
                result.Sort(static (a, b) => b.Stats.TotalMs.CompareTo(a.Stats.TotalMs));
            return result;
        }

        public static IReadOnlyList<TickSummary> TickSummaries()
        {
            var result = new List<TickSummary>();
            foreach (KeyValuePair<int, SegmentStats> pair in Ticks)
                result.Add(new TickSummary(pair.Key, pair.Value.Read()));
            result.Sort(static (a, b) => b.Stats.TotalMs.CompareTo(a.Stats.TotalMs));
            return result;
        }

        /// <summary>The <paramref name="count"/> dynels with the most total tick time, worst first.</summary>
        public static IReadOnlyList<DynelSummary> TopDynels(int count, int? playfieldId)
        {
            var all = new List<DynelSummary>();
            foreach (KeyValuePair<(int PlayfieldId, ulong Dynel), DynelStats> pair in Dynels)
            {
                if (playfieldId.HasValue && pair.Key.PlayfieldId != playfieldId.Value)
                    continue;
                all.Add(pair.Value.Read());
            }

            all.Sort(static (a, b) => b.Tick.TotalMs.CompareTo(a.Tick.TotalMs));
            if (all.Count > count)
                all.RemoveRange(count, all.Count - count);
            return all;
        }

        static DynelStats DynelRow(int playfieldId, Dynel dynel)
            => Dynels.GetOrAdd(
                (playfieldId, dynel.Identity.Long()),
                static (key, d) => new DynelStats(key.PlayfieldId, d.Identity.Instance, Describe(d)),
                dynel);

        static string Describe(Dynel dynel)
        {
            string kind = dynel switch
            {
                Player => "Player",
                NpcCharacter => "NPC",
                Character => "Char",
                _ => dynel.GetType().Name
            };
            string? name = (dynel as Character)?.Name;
            return string.IsNullOrWhiteSpace(name) ? kind : kind + " " + name;
        }

        static void ClearAggregates()
        {
            Stages.Clear();
            Dynels.Clear();
            Ticks.Clear();
            Paths.Clear();
        }

        internal sealed class SegmentStats
        {
            long count;
            long totalTicks;
            long maxTicks;
            long allocatedBytes;

            internal void Add(long elapsedTicks, long bytes = 0)
            {
                lock (this)
                {
                    count++;
                    totalTicks += elapsedTicks;
                    allocatedBytes += bytes;
                    if (elapsedTicks > maxTicks)
                        maxTicks = elapsedTicks;
                }
            }

            internal TimingStats Read()
            {
                lock (this)
                    return new TimingStats(count, TickStallWatch.Milliseconds(totalTicks), TickStallWatch.Milliseconds(maxTicks), allocatedBytes);
            }
        }

        sealed class PathStats
        {
            static readonly int OutcomeCount = Enum.GetValues<PathSearchOutcome>().Length;

            readonly int playfieldId;
            readonly int instance;
            readonly string label;
            readonly string stage;
            readonly long[] outcomes = new long[OutcomeCount];
            long searches;
            long cacheHits;
            long totalTicks;
            long maxTicks;
            int maxIterations;

            internal PathStats(int playfieldId, int instance, string label, string stage)
            {
                this.playfieldId = playfieldId;
                this.instance = instance;
                this.label = label;
                this.stage = stage;
            }

            internal void AddSearch(PathSearchOutcome outcome, int iterations, long elapsedTicks)
            {
                lock (this)
                {
                    searches++;
                    outcomes[(int)outcome]++;
                    totalTicks += elapsedTicks;
                    if (elapsedTicks > maxTicks)
                        maxTicks = elapsedTicks;
                    if (iterations > maxIterations)
                        maxIterations = iterations;
                }
            }

            internal void AddCacheHit()
            {
                lock (this)
                    cacheHits++;
            }

            internal PathSummary Read()
            {
                lock (this)
                {
                    return new PathSummary(
                        playfieldId,
                        instance,
                        label,
                        stage,
                        searches,
                        cacheHits,
                        (long[])outcomes.Clone(),
                        TickStallWatch.Milliseconds(totalTicks),
                        TickStallWatch.Milliseconds(maxTicks),
                        maxIterations);
                }
            }
        }

        sealed class DynelStats
        {
            readonly int playfieldId;
            readonly int instance;
            readonly string label;
            readonly SegmentStats tick = new();
            readonly Dictionary<string, SegmentStats> stages = new(StringComparer.Ordinal);

            internal DynelStats(int playfieldId, int instance, string label)
            {
                this.playfieldId = playfieldId;
                this.instance = instance;
                this.label = label;
            }

            internal void AddTick(long elapsedTicks) => tick.Add(elapsedTicks);

            internal void AddStage(string stage, long elapsedTicks, long allocatedBytes)
            {
                SegmentStats? stats;
                lock (stages)
                {
                    if (!stages.TryGetValue(stage, out stats))
                        stages[stage] = stats = new SegmentStats();
                }

                stats.Add(elapsedTicks, allocatedBytes);
            }

            internal DynelSummary Read()
            {
                var parts = new List<StageSummary>();
                lock (stages)
                {
                    foreach (KeyValuePair<string, SegmentStats> pair in stages)
                        parts.Add(new StageSummary(playfieldId, pair.Key, pair.Value.Read()));
                }

                parts.Sort(static (a, b) => b.Stats.TotalMs.CompareTo(a.Stats.TotalMs));
                return new DynelSummary(playfieldId, instance, label, tick.Read(), parts);
            }
        }
    }

    /// <summary>Count, time and (while recording) bytes allocated on the tick thread.</summary>
    internal readonly record struct TimingStats(long Count, double TotalMs, double MaxMs, long AllocatedBytes = 0)
    {
        public double AverageMs => Count == 0 ? 0 : TotalMs / Count;

        public double AllocatedBytesPerCall => Count == 0 ? 0 : AllocatedBytes / (double)Count;
    }

    internal sealed record StageSummary(int PlayfieldId, string Stage, TimingStats Stats);

    internal sealed record TickSummary(int PlayfieldId, TimingStats Stats);

    internal sealed record DynelSummary(int PlayfieldId, int Instance, string Label, TimingStats Tick, IReadOnlyList<StageSummary> Stages);

    internal sealed record PathSummary(
        int PlayfieldId,
        int Instance,
        string Label,
        string Stage,
        long Searches,
        long CacheHits,
        long[] Outcomes,
        double TotalMs,
        double MaxMs,
        int MaxIterations)
    {
        public long Count(PathSearchOutcome outcome) => Outcomes[(int)outcome];

        public long Failures => Searches - Count(PathSearchOutcome.Found);

        public double AverageMs => Searches == 0 ? 0 : TotalMs / Searches;
    }

    internal sealed record SlowTick(
        DateTime AtUtc,
        int PlayfieldId,
        double TickMs,
        double LimitMs,
        string WorstStage,
        int WorstDetail,
        string? WorstDynel,
        double WorstStageMs,
        long Stages)
    {
        /// <summary>Garbage-collector pause inside this tick (all threads are paused).</summary>
        public double GcPauseMs { get; init; }

        /// <summary>Collections that started during this tick (every generation counts as a gen0 one).</summary>
        public int GcCollections { get; init; }

        /// <summary>Of those, gen1 or gen2 collections.</summary>
        public int GcGen1 { get; init; }

        /// <summary>Of those, full (gen2) collections.</summary>
        public int GcGen2 { get; init; }

        /// <summary>Deepest generation collected in this tick, or -1 when none ran.</summary>
        public int GcDeepestGeneration => GcGen2 > 0 ? 2 : GcGen1 > 0 ? 1 : GcCollections > 0 ? 0 : -1;
    }
}
