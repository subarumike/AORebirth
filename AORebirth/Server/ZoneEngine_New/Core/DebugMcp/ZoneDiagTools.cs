namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime;
    using System.Text.Json;
    using System.Threading;

    using AORebirth.World.Pathfinding;

    using ModelContextProtocol.Server;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Tick-performance tools: the same data as the <c>.diag</c> GM command, as JSON. All are read-only except
    /// <c>diag_control</c>, which only starts, stops or clears the self-stopping profiler.
    /// </summary>
    [McpServerToolType]
    public sealed class ZoneDiagTools
    {
        const int DefaultTickRate = 32;
        const int MaxRows = 100;

        static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        static readonly object ProcessSync = new();
        static ProcessSample? lastProcess;

        readonly PlayfieldManager _playfields;
        readonly IPlayfieldMetricsRegistry _metrics;

        public ZoneDiagTools(PlayfieldManager playfields, IPlayfieldMetricsRegistry metrics)
        {
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(metrics);
            _playfields = playfields;
            _metrics = metrics;
        }

        [McpServerTool(Name = "diag_status")]
        [System.ComponentModel.Description("Tick profiler state (recording, seconds recorded/left), the slow-tick limit, and how many slow ticks are logged. Read-only.")]
        public string Status()
            => Json(new
            {
                recording = TickProfiler.IsRecording,
                recordedSeconds = R(TickProfiler.RecordedSeconds),
                remainingSeconds = R(TickProfiler.RemainingSeconds),
                slowTickLimit = TickProfiler.ThresholdDescription,
                slowTicksLogged = TickProfiler.SlowTicks().Count,
                watchdogSubscribers = WatchdogFeed.HasSubscribers,
                pathIterationCap = NavMeshPathfinder.SearchIterationCap
            });

        [McpServerTool(Name = "diag_control")]
        [System.ComponentModel.Description("Profiler control. action: start (clears data, records for seconds, max 600, stops itself), stop (keeps data), reset (clears data and slow-tick log). Changes diagnostics only.")]
        public string Control(string action, int seconds = TickProfiler.DefaultRecordSeconds)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "start":
                    TickProfiler.Start(Math.Clamp(seconds, 1, TickProfiler.MaxRecordSeconds));
                    break;
                case "stop":
                    TickProfiler.Stop();
                    break;
                case "reset":
                    TickProfiler.Reset();
                    break;
                default:
                    return Error("action must be start, stop or reset.");
            }

            return Status();
        }

        [McpServerTool(Name = "diag_playfields")]
        [System.ComponentModel.Description("Every loaded playfield's tick health over windowSeconds (1, 3, 5, 10, 30): avg/max tick ms, ticks run per second vs configured rate, players and dynels. Always on. Worst first.")]
        public string Playfields(int windowSeconds = 5)
        {
            if (!PlayfieldMetricsWindows.IsAllowed(windowSeconds))
                return Error("windowSeconds must be 1, 3, 5, 10, or 30.");

            var byId = new Dictionary<int, Playfield>();
            foreach (Playfield playfield in _playfields.SnapshotPlayfields())
                byId[playfield.Identity.Instance] = playfield;

            var rows = new List<object>();
            foreach (PlayfieldMetricsSnapshot snapshot in _metrics.GetAllSnapshots(windowSeconds)
                .OrderByDescending(static s => s.TickAverageMs ?? 0))
            {
                int tickRate = DefaultTickRate;
                int? players = null;
                int? dynels = null;
                if (byId.TryGetValue(snapshot.PlayfieldId, out Playfield? playfield))
                {
                    players = 0;
                    dynels = 0;
                    foreach (Dynel dynel in playfield.GetRequiredService<DynelRegistry>().Dynels())
                    {
                        dynels++;
                        if (dynel is Player)
                            players++;
                    }

                    if (playfield.TickRate > 0)
                        tickRate = playfield.TickRate;
                }

                rows.Add(new
                {
                    playfieldId = snapshot.PlayfieldId,
                    loaded = playfield != null,
                    tickAvgMs = R(snapshot.TickAverageMs),
                    tickMaxMs = R(snapshot.TickMaxMs),
                    budgetMs = R(1000.0 / tickRate),
                    ticksPerSecond = R(snapshot.TickSampleCount / (double)windowSeconds),
                    configuredTickRate = tickRate,
                    worldSimAvgMs = R(snapshot.WorldSimAverageMs),
                    buildMs = R(snapshot.BuildElapsedMs),
                    players,
                    dynels
                });
            }

            return Json(new { windowSeconds, count = rows.Count, playfields = rows });
        }

        [McpServerTool(Name = "diag_top")]
        [System.ComponentModel.Description("Profiler: dynels with the most tick time (needs diag_control start). Each row: tick count, avg/max/total ms, ms per real second, and the most expensive stages with avg/max/total. playfieldId 0 = all.")]
        public string Top(int count = 20, int playfieldId = 0, int stagesPerDynel = 6)
        {
            count = Math.Clamp(count, 1, MaxRows);
            stagesPerDynel = Math.Clamp(stagesPerDynel, 0, 32);
            double seconds = Seconds();
            var rows = new List<object>();
            foreach (DynelSummary dynel in TickProfiler.TopDynels(count, Scope(playfieldId)))
            {
                rows.Add(new
                {
                    playfieldId = dynel.PlayfieldId,
                    instance = dynel.Instance,
                    label = dynel.Label,
                    ticks = dynel.Tick.Count,
                    avgMs = R(dynel.Tick.AverageMs),
                    maxMs = R(dynel.Tick.MaxMs),
                    totalMs = R(dynel.Tick.TotalMs),
                    msPerSecond = R(dynel.Tick.TotalMs / seconds),
                    stages = dynel.Stages.Take(stagesPerDynel).Select(static s => Timing(s.Stage, s.Stats)).ToList()
                });
            }

            return Json(new { profiler = ProfilerHeader(), count = rows.Count, dynels = rows });
        }

        [McpServerTool(Name = "diag_stages")]
        [System.ComponentModel.Description("Profiler: whole-tick timing per playfield and every stage's count, avg/max/total ms, ms per real second, and bytes allocated on the tick thread (KB per real second, bytes per call). sortBy: time (default) or alloc. Stage time excludes nested los/path stages. playfieldId 0 = all.")]
        public string Stages(int playfieldId = 0, int count = 60, string sortBy = "time")
        {
            bool byAlloc = string.Equals(sortBy, "alloc", StringComparison.OrdinalIgnoreCase);
            count = Math.Clamp(count, 1, MaxRows);
            int? scope = Scope(playfieldId);
            double seconds = Seconds();
            var ticks = TickProfiler.TickSummaries()
                .Where(t => scope == null || t.PlayfieldId == scope)
                .Select(t => new
                {
                    playfieldId = t.PlayfieldId,
                    ticks = t.Stats.Count,
                    avgMs = R(t.Stats.AverageMs),
                    maxMs = R(t.Stats.MaxMs),
                    msPerSecond = R(t.Stats.TotalMs / seconds),
                    allocKbPerSecond = R(t.Stats.AllocatedBytes / 1024.0 / seconds),
                    allocBytesPerTick = R(t.Stats.AllocatedBytesPerCall)
                })
                .ToList();
            var stages = TickProfiler.StageSummaries(scope, byAlloc)
                .Take(count)
                .Select(s => new
                {
                    playfieldId = s.PlayfieldId,
                    stage = s.Stage,
                    count = s.Stats.Count,
                    avgMs = R(s.Stats.AverageMs),
                    maxMs = R(s.Stats.MaxMs),
                    totalMs = R(s.Stats.TotalMs),
                    msPerSecond = R(s.Stats.TotalMs / seconds),
                    allocKbPerSecond = R(s.Stats.AllocatedBytes / 1024.0 / seconds),
                    allocBytesPerCall = R(s.Stats.AllocatedBytesPerCall)
                })
                .ToList();
            return Json(new { profiler = ProfilerHeader(), sortedBy = byAlloc ? "alloc" : "time", ticks, stages });
        }

        [McpServerTool(Name = "diag_path")]
        [System.ComponentModel.Description("Profiler: navmesh searches by purpose (path.reach/route/walk/pet/motor) and per dynel: searches, cache hits, outcomes (Found, NoEnds, EndOffMesh, SearchFailed, IterationCap, Partial, NoStraightPath), avg/max/total ms, max iterations. playfieldId 0 = all.")]
        public string Path(int count = 20, int playfieldId = 0)
        {
            count = Math.Clamp(count, 1, MaxRows);
            double seconds = Seconds();
            IReadOnlyList<PathSummary> rows = TickProfiler.PathSummaries(Scope(playfieldId));
            var purposes = rows
                .GroupBy(static r => r.Stage)
                .Select(g => new
                {
                    purpose = g.Key,
                    searches = g.Sum(static r => r.Searches),
                    cacheHits = g.Sum(static r => r.CacheHits),
                    totalMs = R(g.Sum(static r => r.TotalMs)),
                    msPerSecond = R(g.Sum(static r => r.TotalMs) / seconds),
                    maxMs = R(g.Max(static r => r.MaxMs)),
                    maxIterations = g.Max(static r => r.MaxIterations),
                    outcomes = Outcomes(g.SelectMany(static r => Enumerable.Range(0, r.Outcomes.Length).Select(i => (i, r.Outcomes[i]))))
                })
                .OrderByDescending(static p => p.totalMs)
                .ToList();
            var dynels = rows.Take(count).Select(r => new
            {
                playfieldId = r.PlayfieldId,
                instance = r.Instance,
                label = r.Label,
                purpose = r.Stage,
                searches = r.Searches,
                searchesPerSecond = R(r.Searches / seconds),
                cacheHits = r.CacheHits,
                avgMs = R(r.AverageMs),
                maxMs = R(r.MaxMs),
                totalMs = R(r.TotalMs),
                maxIterations = r.MaxIterations,
                outcomes = Outcomes(Enumerable.Range(0, r.Outcomes.Length).Select(i => (i, r.Outcomes[i])))
            }).ToList();
            return Json(new { profiler = ProfilerHeader(), purposes, dynels });
        }

        [McpServerTool(Name = "diag_slow")]
        [System.ComponentModel.Description("Always-on slow-tick log, newest first: UTC time, playfield, tick ms vs limit, the single worst stage with its dynel/detail and ms, GC pause ms and collections inside the tick, and stage count.")]
        public string Slow(int count = 64, int playfieldId = 0)
        {
            int? scope = Scope(playfieldId);
            var rows = TickProfiler.SlowTicks()
                .Where(t => scope == null || t.PlayfieldId == scope)
                .Take(Math.Clamp(count, 1, MaxRows))
                .Select(t => new
                {
                    atUtc = t.AtUtc.ToString("o", CultureInfo.InvariantCulture),
                    playfieldId = t.PlayfieldId,
                    tickMs = R(t.TickMs),
                    limitMs = R(t.LimitMs),
                    worstStage = t.WorstStage,
                    worstDynel = t.WorstDynel,
                    worstDetail = t.WorstDetail,
                    worstStageMs = R(t.WorstStageMs),
                    gcPauseMs = R(t.GcPauseMs),
                    gcCollections = t.GcCollections,
                    gcGen1Collections = t.GcGen1,
                    gcGen2Collections = t.GcGen2,
                    stages = t.Stages
                })
                .ToList();
            return Json(new { slowTickLimit = TickProfiler.ThresholdDescription, count = rows.Count, ticks = rows });
        }

        [McpServerTool(Name = "diag_process")]
        [System.ComponentModel.Description("Process health: cores, threads, memory, GC mode/heap/pause %, collections, thread pool backlog, lock contentions, plus CPU % and deltas since the previous diag_process call.")]
        public string Process()
        {
            ProcessSample now = ProcessSample.Take();
            ProcessSample? last;
            lock (ProcessSync)
            {
                last = lastProcess;
                lastProcess = now;
            }

            GCMemoryInfo gc = GC.GetGCMemoryInfo();
            ThreadPool.GetAvailableThreads(out int workerFree, out int ioFree);
            ThreadPool.GetMaxThreads(out int workerMax, out int ioMax);

            object? interval = null;
            if (last != null)
            {
                double wall = Math.Max((now.Wall - last.Wall).TotalSeconds, 0.001);
                double cpu = (now.Cpu - last.Cpu).TotalSeconds;
                interval = new
                {
                    seconds = R(wall),
                    cpuPercentOfOneCore = R(100.0 * cpu / wall),
                    cpuPercentOfMachine = R(100.0 * cpu / wall / Environment.ProcessorCount),
                    gen0 = now.Gen0 - last.Gen0,
                    gen1 = now.Gen1 - last.Gen1,
                    gen2 = now.Gen2 - last.Gen2,
                    gcPauseMs = R((now.GcPause - last.GcPause).TotalMilliseconds),
                    allocMbPerSecond = R((now.TotalAllocatedBytes - last.TotalAllocatedBytes) / 1048576.0 / wall),
                    lockContentionsPerSecond = R((now.LockContentions - last.LockContentions) / wall),
                    threadPoolItems = now.PoolCompleted - last.PoolCompleted
                };
            }

            return Json(new
            {
                cores = Environment.ProcessorCount,
                playfields = _playfields.SnapshotPlayfields().Count,
                players = _playfields.SnapshotPlayers().Count,
                threads = now.Threads,
                uptime = now.Uptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture),
                workingSetMb = R(now.WorkingSetBytes / 1048576.0),
                gc = new
                {
                    serverGc = GCSettings.IsServerGC,
                    latencyMode = GCSettings.LatencyMode.ToString(),
                    heapMb = R(gc.HeapSizeBytes / 1048576.0),
                    committedMb = R(gc.TotalCommittedBytes / 1048576.0),
                    pauseTimePercent = R(gc.PauseTimePercentage),
                    lastPauseMs = R(ProcessSample.LastPauseMs(gc)),
                    totalPauseMs = R(now.GcPause.TotalMilliseconds),
                    totalAllocatedMb = R(now.TotalAllocatedBytes / 1048576.0),
                    gen0 = now.Gen0,
                    gen1 = now.Gen1,
                    gen2 = now.Gen2
                },
                threadPool = new
                {
                    threads = ThreadPool.ThreadCount,
                    queued = ThreadPool.PendingWorkItemCount,
                    workersBusy = workerMax - workerFree,
                    ioBusy = ioMax - ioFree
                },
                lockContentions = now.LockContentions,
                logging = new
                {
                    async = true,
                    queueLimit = Logging.AsyncLogging.QueueLimit,
                    droppedEvents = Logging.AsyncLogging.DroppedEvents
                },
                sincePreviousCall = interval
            });
        }

        static object ProfilerHeader()
            => new
            {
                recording = TickProfiler.IsRecording,
                recordedSeconds = R(TickProfiler.RecordedSeconds),
                remainingSeconds = R(TickProfiler.RemainingSeconds)
            };

        static object Timing(string stage, TimingStats stats)
            => new
            {
                stage,
                count = stats.Count,
                avgMs = R(stats.AverageMs),
                maxMs = R(stats.MaxMs),
                totalMs = R(stats.TotalMs),
                allocKb = R(stats.AllocatedBytes / 1024.0)
            };

        static Dictionary<string, long> Outcomes(IEnumerable<(int Index, long Count)> counts)
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach ((int index, long n) in counts)
            {
                if (n == 0)
                    continue;
                string name = ((PathSearchOutcome)index).ToString();
                result[name] = result.TryGetValue(name, out long existing) ? existing + n : n;
            }

            return result;
        }

        static int? Scope(int playfieldId) => playfieldId > 0 ? playfieldId : null;

        static double Seconds() => Math.Max(TickProfiler.RecordedSeconds, 0.001);

        static double R(double value) => Math.Round(value, 3);

        static double? R(double? value) => value.HasValue ? Math.Round(value.Value, 3) : null;

        static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

        static string Error(string message) => Json(new { error = message });
    }
}
