namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime;
    using System.Text;
    using System.Threading;

    using AORebirth.World.Pathfinding;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;

    using static ZoneEngine_New.Core.Commands.DiagAoml;

    /// <summary>
    /// <c>.diag</c>: tick-performance diagnostics, one subcommand each. <c>pfs</c>, <c>slow</c> and <c>proc</c> read
    /// always-on data; <c>top</c> and <c>stages</c> read the <see cref="TickProfiler"/> recording that <c>on</c> starts.
    /// Severity colors are judged against the tick interval (the per-tick budget).
    /// </summary>
    public sealed class DiagCommand : IGmCommand
    {
        const int DefaultTopCount = 20;
        const int MaxTopCount = 50;
        const int StagesPerDynel = 3;
        const int DefaultTickRate = 32;

        readonly IPlayfieldMetricsRegistry _metrics;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly object _procSync = new();
        ProcessSample? _lastProc;

        public DiagCommand(IPlayfieldMetricsRegistry metrics, Lazy<PlayfieldManager> playfields)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
        }

        public string Name => "diag";

        public int RequiredGmLevel => 1;

        public string Usage => ".diag [on [sec] | off | reset | top [n] [here|pfId] | stages [here|all|pfId] | path [n] [here|pfId] | alloc [here|all|pfId] | pfs [1|3|5|10|30] | slow | proc | threshold <ms|auto>]";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string sub = context.Args.Length > 0 ? context.Args[0].ToLowerInvariant() : "help";
            switch (sub)
            {
                case "on":
                case "start":
                    Start(context);
                    break;
                case "off":
                case "stop":
                    TickProfiler.Stop();
                    Say(context, Label("[diag] ") + string.Format(CultureInfo.InvariantCulture,
                        "Profiler stopped after {0:F0}s; results kept. ", TickProfiler.RecordedSeconds) + ResultLinks());
                    break;
                case "reset":
                    TickProfiler.Reset();
                    Say(context, Label("[diag] ") + "Profiler data and slow-tick log cleared.");
                    break;
                case "top":
                    Top(context);
                    break;
                case "stages":
                    Stages(context, byAllocation: false);
                    break;
                case "path":
                    Path(context);
                    break;
                case "alloc":
                    Stages(context, byAllocation: true);
                    break;
                case "pfs":
                    Playfields(context);
                    break;
                case "slow":
                    Slow(context);
                    break;
                case "proc":
                    Process(context);
                    break;
                case "threshold":
                    Threshold(context);
                    break;
                default:
                    Help(context);
                    break;
            }
        }

        void Help(GmCommandContext context)
        {
            bool watchdog = WatchdogFeed.IsSubscribed(context.Player);
            // One block per section so a page break never splits a section.
            var sections = new List<string>
            {
                Status()
                    + "<br>" + Field("Slow-tick limit", Name(TickProfiler.ThresholdDescription))
                    + "<br>" + Field("Watchdog chat", watchdog ? Color(Green, "ON") : Muted("off")) + Link(".debug watchdog", "[toggle]"),

                Section("Recording") + Muted(" (small tick cost while on)")
                    + "<br>" + Command(".diag on 60", "on [sec]", "record timings, stops itself (max 600s)")
                    + "<br>" + Command(".diag off", "off", "stop, keep results")
                    + "<br>" + Command(".diag reset", "reset", "clear results and slow log"),

                Section("Recorded results")
                    + "<br>" + Command(".diag top", "top [n] [here|pf]", "worst dynels, avg/max per part")
                    + "<br>" + Command(".diag stages", "stages [here|all|pf]", "where the tick time goes")
                    + "<br>" + Command(".diag path", "path [n] [here|pf]", "navmesh searches: who, why, how they end")
                    + "<br>" + Command(".diag alloc", "alloc [here|all|pf]", "who allocates (feeds GC pauses)"),

                Section("Always on")
                    + "<br>" + Command(".diag pfs", "pfs [secs]", "every playfield's tick health")
                    + "<br>" + Command(".diag slow", "slow", "recent slow ticks, worst part")
                    + "<br>" + Command(".diag proc", "proc", "CPU, GC, locks, thread pool")
                    + "<br>" + Indent + Name("threshold <ms|auto>") + " " + Muted("override slow limit"),

                Section("Colors")
                    + "<br>" + Indent + Color(Green, "fine") + "  " + Color(Yellow, "watch") + "  " + Color(Red, "problem")
                    + Muted("  vs the tick interval")
            };
            SendPopup(context, "Tick diagnostics", sections);
        }

        void Start(GmCommandContext context)
        {
            int seconds = TickProfiler.DefaultRecordSeconds;
            if (context.Args.Length > 1
                && (!int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) || seconds <= 0))
            {
                Say(context, "Usage: .diag on [1-" + TickProfiler.MaxRecordSeconds.ToString(CultureInfo.InvariantCulture) + "]");
                return;
            }

            seconds = Math.Min(seconds, TickProfiler.MaxRecordSeconds);
            TickProfiler.Start(seconds);
            Say(context, Label("[diag] ") + Color(Green, "Profiler recording") + string.Format(CultureInfo.InvariantCulture,
                " for {0}s (stops itself). ", seconds) + ResultLinks());
        }

        void Top(GmCommandContext context)
        {
            if (!TryParseCountAndScope(context, ".diag top [n] [here|pfId]", out int count, out int? playfieldId))
                return;

            IReadOnlyList<DynelSummary> top = TickProfiler.TopDynels(count, playfieldId);
            if (top.Count == 0)
            {
                Say(context, NoDataHint());
                return;
            }

            double budget = BudgetMs(context);
            double seconds = Math.Max(TickProfiler.RecordedSeconds, 0.001);
            string header = Status() + "<br>"
                + Muted("avg / max ms per tick of that dynel; load = ms of each real second") + "<br>";

            var blocks = new List<string>(top.Count);
            for (int i = 0; i < top.Count; i++)
            {
                DynelSummary dynel = top[i];
                double load = dynel.Tick.TotalMs / seconds;
                var block = new StringBuilder();
                block.Append(Color(Orange, (i + 1).ToString(CultureInfo.InvariantCulture) + ". "))
                    .Append(Name(Safe(dynel.Label)))
                    .Append(Muted(string.Format(CultureInfo.InvariantCulture, " #{0} pf {1}", dynel.Instance, dynel.PlayfieldId)))
                    .Append("<br>").Append(Indent)
                    .Append(Field("avg", Severity(dynel.Tick.AverageMs, budget * 0.02, budget * 0.1, Ms(dynel.Tick.AverageMs))))
                    .Append(Field("max", Severity(dynel.Tick.MaxMs, budget * 0.25, budget, Ms(dynel.Tick.MaxMs))))
                    .Append(Field("load", Severity(load, 10, 50, load.ToString("F1", CultureInfo.InvariantCulture) + "ms/s")))
                    .Append(Muted("n " + dynel.Tick.Count.ToString(CultureInfo.InvariantCulture)));

                int stages = Math.Min(StagesPerDynel, dynel.Stages.Count);
                if (stages > 0)
                    block.Append("<br>").Append(Indent);
                for (int s = 0; s < stages; s++)
                {
                    StageSummary stage = dynel.Stages[s];
                    block.Append(Label(stage.Stage)).Append(' ')
                        .Append(Severity(stage.Stats.AverageMs, budget * 0.02, budget * 0.1, Ms(stage.Stats.AverageMs)))
                        .Append(Muted("/"))
                        .Append(Severity(stage.Stats.MaxMs, budget * 0.25, budget, Ms(stage.Stats.MaxMs)))
                        .Append("&#160;&#160;");
                }

                blocks.Add(block.ToString());
            }

            string title = string.Format(CultureInfo.InvariantCulture, "Top {0} tick wasters", top.Count);
            GmCommandFeedback.SendLines(context.Session, context.Player, Pages(
                title,
                header,
                blocks,
                separator: "<br><br>",
                pageLabel: static (first, last) => string.Format(CultureInfo.InvariantCulture, "#{0}-{1}", first + 1, last + 1)));
        }

        /// <summary>Parses <c>[n] [here|pfId]</c> after the subcommand. False after sending usage.</summary>
        static bool TryParseCountAndScope(GmCommandContext context, string usage, out int count, out int? playfieldId)
        {
            count = DefaultTopCount;
            playfieldId = null;
            for (int i = 1; i < context.Args.Length; i++)
            {
                string arg = context.Args[i];
                if (string.Equals(arg, "here", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryCurrentPlayfield(context, out int here))
                        return false;
                    playfieldId = here;
                }
                else if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
                {
                    // Small numbers are a count, playfield ids are not.
                    if (value <= MaxTopCount)
                        count = value;
                    else
                        playfieldId = value;
                }
                else
                {
                    Say(context, "Usage: " + usage);
                    return false;
                }
            }

            return true;
        }

        void Path(GmCommandContext context)
        {
            if (!TryParseCountAndScope(context, ".diag path [n] [here|pfId]", out int count, out int? playfieldId))
                return;

            IReadOnlyList<PathSummary> rows = TickProfiler.PathSummaries(playfieldId);
            if (rows.Count == 0)
            {
                Say(context, NoDataHint());
                return;
            }

            double budget = BudgetMs(context);
            double seconds = Math.Max(TickProfiler.RecordedSeconds, 0.001);
            int cap = NavMeshPathfinder.SearchIterationCap;

            // Totals per purpose, so "reach checks are 90% of path time" is visible at a glance.
            var purposes = new SortedDictionary<string, PathSummary>(StringComparer.Ordinal);
            foreach (PathSummary row in rows)
            {
                if (!purposes.TryGetValue(row.Stage, out PathSummary? sum))
                {
                    purposes[row.Stage] = row with { Outcomes = (long[])row.Outcomes.Clone() };
                    continue;
                }

                long[] outcomes = sum.Outcomes;
                for (int i = 0; i < outcomes.Length; i++)
                    outcomes[i] += row.Outcomes[i];
                purposes[row.Stage] = sum with
                {
                    Searches = sum.Searches + row.Searches,
                    CacheHits = sum.CacheHits + row.CacheHits,
                    TotalMs = sum.TotalMs + row.TotalMs,
                    MaxMs = Math.Max(sum.MaxMs, row.MaxMs),
                    MaxIterations = Math.Max(sum.MaxIterations, row.MaxIterations)
                };
            }

            var header = new StringBuilder();
            header.Append(Status()).Append("<br>")
                .Append(Muted("load = search ms per real second; cache = answers reused without a search")).Append("<br>")
                .Append(Section("By purpose"));
            foreach (PathSummary sum in purposes.Values)
                header.Append("<br>").Append(Indent).Append(Label(sum.Stage)).Append(' ').Append(PathNumbers(sum, seconds, budget, cap));
            header.Append("<br>").Append(Section("Worst searchers")).Append("<br>");

            var blocks = new List<string>();
            for (int i = 0; i < rows.Count && i < count; i++)
            {
                PathSummary row = rows[i];
                blocks.Add(
                    Color(Orange, (i + 1).ToString(CultureInfo.InvariantCulture) + ". ")
                    + Name(Safe(row.Label))
                    + Muted(string.Format(CultureInfo.InvariantCulture, " #{0} pf {1} ", row.Instance, row.PlayfieldId))
                    + Label(row.Stage)
                    + "<br>" + Indent + PathNumbers(row, seconds, budget, cap));
            }

            string title = playfieldId.HasValue
                ? "Path searches pf " + playfieldId.Value.ToString(CultureInfo.InvariantCulture)
                : "Path searches";
            GmCommandFeedback.SendLines(context.Session, context.Player, Pages(
                title,
                header.ToString(),
                blocks,
                separator: "<br>",
                pageLabel: static (first, last) => string.Format(CultureInfo.InvariantCulture, "#{0}-{1}", first + 1, last + 1)));
        }

        /// <summary>Rate, cache share, time and how searches ended, colored for trouble.</summary>
        static string PathNumbers(PathSummary row, double seconds, double budget, int cap)
        {
            double rate = row.Searches / seconds;
            long asked = row.Searches + row.CacheHits;
            double hitPercent = asked == 0 ? 0 : 100.0 * row.CacheHits / asked;
            double load = row.TotalMs / seconds;
            double failPercent = row.Searches == 0 ? 0 : 100.0 * row.Failures / row.Searches;
            long capped = row.Count(PathSearchOutcome.IterationCap);

            var text = new StringBuilder();
            text.Append(Field("n", Severity(rate, 10, 50, rate.ToString("F1", CultureInfo.InvariantCulture) + "/s")))
                .Append(Field("cache", SeverityLow(hitPercent, 50, 10, hitPercent.ToString("F0", CultureInfo.InvariantCulture) + "%")))
                .Append(Field("avg", Severity(row.AverageMs, budget * 0.05, budget * 0.25, Ms(row.AverageMs))))
                .Append(Field("max", Severity(row.MaxMs, budget * 0.25, budget, Ms(row.MaxMs))))
                .Append(Field("load", Severity(load, 20, 100, load.ToString("F1", CultureInfo.InvariantCulture) + "ms/s")))
                .Append("<br>").Append(Indent)
                .Append(Field("failed", Severity(failPercent, 25, 60,
                    row.Failures.ToString(CultureInfo.InvariantCulture) + " (" + failPercent.ToString("F0", CultureInfo.InvariantCulture) + "%)")));

            AppendOutcome(text, row, PathSearchOutcome.Partial, "partial");
            AppendOutcome(text, row, PathSearchOutcome.NoEnds, "no-mesh");
            AppendOutcome(text, row, PathSearchOutcome.EndOffMesh, "off-mesh");
            AppendOutcome(text, row, PathSearchOutcome.SearchFailed, "search-fail");
            AppendOutcome(text, row, PathSearchOutcome.NoStraightPath, "no-straight");
            AppendOutcome(text, row, PathSearchOutcome.Disconnected, "disconnected");
            text.Append(Field("hit cap", capped > 0 ? Color(Red, capped.ToString(CultureInfo.InvariantCulture)) : Color(Green, "0")))
                .Append(Field("iters", Severity(row.MaxIterations, cap * 0.5, cap,
                    row.MaxIterations.ToString(CultureInfo.InvariantCulture)) + Muted("/" + cap.ToString(CultureInfo.InvariantCulture))));
            return text.ToString();
        }

        static void AppendOutcome(StringBuilder text, PathSummary row, PathSearchOutcome outcome, string label)
        {
            long n = row.Count(outcome);
            if (n > 0)
                text.Append(Field(label, Color(Yellow, n.ToString(CultureInfo.InvariantCulture))));
        }

        void Stages(GmCommandContext context, bool byAllocation)
        {
            int? playfieldId;
            string scope = context.Args.Length > 1 ? context.Args[1] : "here";
            if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
                playfieldId = null;
            else if (string.Equals(scope, "here", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryCurrentPlayfield(context, out int here))
                    return;
                playfieldId = here;
            }
            else if (int.TryParse(scope, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id > 0)
                playfieldId = id;
            else
            {
                Say(context, "Usage: .diag " + (byAllocation ? "alloc" : "stages") + " [here|all|pfId]");
                return;
            }

            IReadOnlyList<StageSummary> stages = TickProfiler.StageSummaries(playfieldId, byAllocation);
            if (stages.Count == 0)
            {
                Say(context, NoDataHint());
                return;
            }

            double budget = BudgetMs(context);
            double seconds = Math.Max(TickProfiler.RecordedSeconds, 0.001);
            var rows = new List<string>
            {
                Status(),
                Muted("load = ms of each real second (1000 = a whole core); avg / max ms per pass; alloc = KB allocated per real second")
            };

            foreach (TickSummary tick in TickProfiler.TickSummaries())
            {
                if (playfieldId.HasValue && tick.PlayfieldId != playfieldId.Value)
                    continue;
                double load = tick.Stats.TotalMs / seconds;
                rows.Add(Section("pf " + tick.PlayfieldId.ToString(CultureInfo.InvariantCulture) + " whole tick")
                    + "<br>" + Indent
                    + Field("avg", Severity(tick.Stats.AverageMs, budget * 0.5, budget, Ms(tick.Stats.AverageMs)))
                    + Field("max", Severity(tick.Stats.MaxMs, budget, budget * 3, Ms(tick.Stats.MaxMs)))
                    + Field("load", Severity(load, 250, 600, load.ToString("F0", CultureInfo.InvariantCulture) + "ms/s"))
                    + Field("alloc", AllocRate(tick.Stats.AllocatedBytes, seconds, 4096, 16384))
                    + Muted("n " + tick.Stats.Count.ToString(CultureInfo.InvariantCulture)));
            }

            rows.Add(Section(byAllocation ? "Stages, most bytes allocated first" : "Stages, most total time first"));
            foreach (StageSummary stage in stages)
            {
                double load = stage.Stats.TotalMs / seconds;
                rows.Add(
                    (playfieldId.HasValue ? string.Empty : Muted("pf " + stage.PlayfieldId.ToString(CultureInfo.InvariantCulture) + " "))
                    + Label(stage.Stage) + " "
                    + Severity(load, 50, 200, load.ToString("F1", CultureInfo.InvariantCulture) + "ms/s") + "  "
                    + Severity(stage.Stats.AverageMs, budget * 0.1, budget * 0.5, Ms(stage.Stats.AverageMs))
                    + Muted("/")
                    + Severity(stage.Stats.MaxMs, budget * 0.5, budget, Ms(stage.Stats.MaxMs))
                    + "  " + AllocRate(stage.Stats.AllocatedBytes, seconds, 512, 2048)
                    + Muted(" n " + stage.Stats.Count.ToString(CultureInfo.InvariantCulture)));
            }

            string what = byAllocation ? "Allocations" : "Tick stages";
            string title = playfieldId.HasValue
                ? what + " pf " + playfieldId.Value.ToString(CultureInfo.InvariantCulture)
                : what + ", all playfields";
            SendPopup(context, title, rows);
        }

        void Playfields(GmCommandContext context)
        {
            int window = 5;
            if (context.Args.Length > 1
                && (!int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out window)
                    || !PlayfieldMetricsWindows.IsAllowed(window)))
            {
                Say(context, "Usage: .diag pfs [1|3|5|10|30]");
                return;
            }

            var byId = new Dictionary<int, Playfield>();
            foreach (Playfield playfield in _playfields.Value.SnapshotPlayfields())
                byId[playfield.Identity.Instance] = playfield;

            var snapshots = new List<PlayfieldMetricsSnapshot>(_metrics.GetAllSnapshots(window));
            snapshots.Sort(static (a, b) => (b.TickAverageMs ?? 0).CompareTo(a.TickAverageMs ?? 0));

            var rows = new List<string>
            {
                Muted(string.Format(CultureInfo.InvariantCulture,
                    "last {0}s, worst first; rate = ticks run per second / configured", window))
            };
            foreach (PlayfieldMetricsSnapshot snapshot in snapshots)
            {
                int tickRate = DefaultTickRate;
                string load = Muted("not loaded");
                if (byId.TryGetValue(snapshot.PlayfieldId, out Playfield? playfield))
                {
                    int players = 0;
                    int dynels = 0;
                    foreach (Dynel dynel in playfield.GetRequiredService<DynelRegistry>().Dynels())
                    {
                        dynels++;
                        if (dynel is Player)
                            players++;
                    }

                    load = Field("players", Name(players.ToString(CultureInfo.InvariantCulture)))
                        + Field("dynels", Name(dynels.ToString(CultureInfo.InvariantCulture)));
                    if (playfield.TickRate > 0)
                        tickRate = playfield.TickRate;
                }

                double budget = 1000.0 / tickRate;
                double rate = snapshot.TickSampleCount / (double)window;
                rows.Add(Name("pf " + snapshot.PlayfieldId.ToString(CultureInfo.InvariantCulture))
                    + "<br>" + Indent
                    + Field("avg", MsOrNa(snapshot.TickAverageMs, budget * 0.5, budget))
                    + Field("max", MsOrNa(snapshot.TickMaxMs, budget, budget * 3))
                    + Field("rate", SeverityLow(rate, tickRate * 0.9, tickRate * 0.5, rate.ToString("F1", CultureInfo.InvariantCulture))
                        + Muted("/" + tickRate.ToString(CultureInfo.InvariantCulture)))
                    + "<br>" + Indent + load);
            }

            SendPopup(context, string.Format(CultureInfo.InvariantCulture, "Playfield ticks ({0})", snapshots.Count), rows);
        }

        void Slow(GmCommandContext context)
        {
            IReadOnlyList<SlowTick> slow = TickProfiler.SlowTicks();
            if (slow.Count == 0)
            {
                Say(context, Label("[diag] ") + Color(Green, "No slow ticks logged") + " (limit: " + TickProfiler.ThresholdDescription + ").");
                return;
            }

            string header = Muted("newest first, UTC; limit " + TickProfiler.ThresholdDescription) + "<br>";
            var blocks = new List<string>(slow.Count);
            foreach (SlowTick tick in slow)
            {
                string where = tick.WorstDynel == null
                    ? (tick.WorstDetail != 0 ? Muted(" #" + tick.WorstDetail.ToString(CultureInfo.InvariantCulture)) : string.Empty)
                    : " " + Name(Safe(tick.WorstDynel));
                blocks.Add(
                    Muted(tick.AtUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture)) + " "
                    + Name("pf " + tick.PlayfieldId.ToString(CultureInfo.InvariantCulture)) + " "
                    + Severity(tick.TickMs, tick.LimitMs * 2, tick.LimitMs * 4, Ms(tick.TickMs) + "ms")
                    + Muted(" / " + Ms(tick.LimitMs))
                    + "<br>" + Indent + Label("worst ") + tick.WorstStage + where + " "
                    + Severity(tick.WorstStageMs, tick.LimitMs * 0.5, tick.LimitMs, Ms(tick.WorstStageMs) + "ms")
                    + (tick.GcPauseMs > 0
                        ? Label(" gc ") + Color(Red, Ms(tick.GcPauseMs) + "ms")
                            + Muted(" gen" + Math.Max(0, tick.GcDeepestGeneration).ToString(CultureInfo.InvariantCulture)
                                + " x" + tick.GcCollections.ToString(CultureInfo.InvariantCulture))
                        : string.Empty)
                    + Muted(" stages " + tick.Stages.ToString(CultureInfo.InvariantCulture)));
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, Pages(
                string.Format(CultureInfo.InvariantCulture, "Slow ticks ({0})", slow.Count), header, blocks));
        }

        void Process(GmCommandContext context)
        {
            var now = ProcessSample.Take();
            ProcessSample? last;
            lock (_procSync)
            {
                last = _lastProc;
                _lastProc = now;
            }

            GCMemoryInfo gc = GC.GetGCMemoryInfo();
            ThreadPool.GetAvailableThreads(out int workerFree, out int ioFree);
            ThreadPool.GetMaxThreads(out int workerMax, out int ioMax);
            long queued = ThreadPool.PendingWorkItemCount;
            double lastPause = ProcessSample.LastPauseMs(gc);

            var rows = new List<string>
            {
                Section("Process").Substring("<br>".Length),
                Indent + Field("cores", Name(Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)))
                    + Field("playfields", Name(_playfields.Value.SnapshotPlayfields().Count.ToString(CultureInfo.InvariantCulture)))
                    + Field("threads", Name(now.Threads.ToString(CultureInfo.InvariantCulture))),
                Indent + Field("uptime", Name(now.Uptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)))
                    + Field("memory", Name(Mb(now.WorkingSetBytes))),
                Indent + Field("log lines dropped", Severity(Logging.AsyncLogging.DroppedEvents, 1, 1000,
                    Logging.AsyncLogging.DroppedEvents.ToString(CultureInfo.InvariantCulture)))
                    + Muted("(background log queue, limit " + Logging.AsyncLogging.QueueLimit.ToString(CultureInfo.InvariantCulture) + ")"),

                Section("Garbage collector"),
                Indent + Field("mode", Name((GCSettings.IsServerGC ? "server" : "workstation") + ", " + GCSettings.LatencyMode)),
                Indent + Field("heap", Name(Mb(gc.HeapSizeBytes))) + Field("committed", Name(Mb(gc.TotalCommittedBytes))),
                Indent + Field("pause time", Severity(gc.PauseTimePercentage, 2, 5, gc.PauseTimePercentage.ToString("F2", CultureInfo.InvariantCulture) + "%"))
                    + Field("last pause", Severity(lastPause, 10, 50, Ms(lastPause) + "ms")),
                Indent + Field("collections", Name(string.Format(CultureInfo.InvariantCulture, "{0} / {1} / {2}", now.Gen0, now.Gen1, now.Gen2)))
                    + Muted("gen0/1/2"),

                Section("Thread pool"),
                Indent + Field("threads", Name(ThreadPool.ThreadCount.ToString(CultureInfo.InvariantCulture)))
                    + Field("queued", Severity(queued, 10, 100, queued.ToString(CultureInfo.InvariantCulture)))
                    + Field("busy", Name(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", workerMax - workerFree, workerMax))),
                Indent + Field("io busy", Name(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", ioMax - ioFree, ioMax)))
                    + Field("lock contentions", Name(now.LockContentions.ToString(CultureInfo.InvariantCulture)))
            };

            if (last != null)
            {
                double wall = Math.Max((now.Wall - last.Wall).TotalSeconds, 0.001);
                double cpu = (now.Cpu - last.Cpu).TotalSeconds;
                double machine = 100.0 * cpu / wall / Environment.ProcessorCount;
                double pause = (now.GcPause - last.GcPause).TotalMilliseconds;
                double contentions = (now.LockContentions - last.LockContentions) / wall;
                rows.Add(Section(string.Format(CultureInfo.InvariantCulture, "Last {0:F0}s (since previous .diag proc)", wall)));
                rows.Add(Indent + Field("CPU", Severity(machine, 70, 90, machine.ToString("F0", CultureInfo.InvariantCulture) + "%"))
                    + Muted(string.Format(CultureInfo.InvariantCulture, "of machine ({0:F0}% of one core)", 100.0 * cpu / wall)));
                double allocMb = (now.TotalAllocatedBytes - last.TotalAllocatedBytes) / 1048576.0 / wall;
                rows.Add(Indent + Field("allocated", Severity(allocMb, 50, 200, allocMb.ToString("F1", CultureInfo.InvariantCulture) + " MB/s")));
                rows.Add(Indent + Field("GC pause", Severity(pause / wall, 20, 50, Ms(pause) + "ms"))
                    + Field("GCs", Name(string.Format(CultureInfo.InvariantCulture, "+{0} / +{1} / +{2}",
                        now.Gen0 - last.Gen0, now.Gen1 - last.Gen1, now.Gen2 - last.Gen2))));
                rows.Add(Indent + Field("lock contentions", Severity(contentions, 100, 1000, contentions.ToString("F0", CultureInfo.InvariantCulture) + "/s"))
                    + Field("pool items", Name((now.PoolCompleted - last.PoolCompleted).ToString(CultureInfo.InvariantCulture))));
            }
            else
                rows.Add("<br>" + Muted("Run ") + Link(".diag proc", ".diag proc") + Muted(" again for CPU % and per-interval rates."));

            SendPopup(context, "Process health", rows);
        }

        void Threshold(GmCommandContext context)
        {
            if (context.Args.Length >= 2 && string.Equals(context.Args[1], "auto", StringComparison.OrdinalIgnoreCase))
            {
                TickProfiler.OverrideThresholdMs = 0;
                Say(context, Label("[diag] ") + "Slow-tick limit is the configured tick interval again.");
                return;
            }

            if (context.Args.Length < 2
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms)
                || ms <= 0)
            {
                Say(context, Label("[diag] ") + "Slow-tick limit is " + TickProfiler.ThresholdDescription + ". Usage: .diag threshold <ms|auto>");
                return;
            }

            TickProfiler.OverrideThresholdMs = ms;
            Say(context, Label("[diag] ") + "Slow-tick limit set to " + Name(TickProfiler.ThresholdDescription)
                + " until restart; " + Link(".diag threshold auto", "[auto]") + " reverts.");
        }

        /// <summary>The issuer's playfield tick interval, the budget severity colors are judged against.</summary>
        static double BudgetMs(GmCommandContext context)
        {
            int rate = context.Player.Playfield?.TickRate ?? 0;
            return 1000.0 / (rate > 0 ? rate : DefaultTickRate);
        }

        static string Status()
        {
            if (TickProfiler.IsRecording)
                return Field("Profiler", Color(Green, "ON") + Muted(string.Format(CultureInfo.InvariantCulture,
                    " {0:F0}s recorded, {1:F0}s left", TickProfiler.RecordedSeconds, TickProfiler.RemainingSeconds)));
            return Field("Profiler", Muted("off") + Muted(string.Format(CultureInfo.InvariantCulture,
                " ({0:F0}s of data)", TickProfiler.RecordedSeconds)) + " " + Link(".diag on 60", "[start 60s]"));
        }

        static string Command(string command, string syntax, string description)
            => Indent + Link(command, syntax) + " " + Muted(description);

        static string ResultLinks()
            => Link(".diag top", "[top]") + " " + Link(".diag stages", "[stages]") + " " + Link(".diag stages all", "[stages all]")
                + " " + Link(".diag path", "[path]");

        static string NoDataHint()
            => TickProfiler.IsRecording
                ? Label("[diag] ") + "No samples yet; wait a moment and try again."
                : Label("[diag] ") + "No profiler data. " + Link(".diag on 60", "[start 60s]");

        static bool TryCurrentPlayfield(GmCommandContext context, out int playfieldId)
        {
            playfieldId = context.Player.Playfield?.Identity.Instance ?? 0;
            if (playfieldId > 0)
                return true;
            Say(context, "Not on a playfield.");
            return false;
        }

        /// <summary>KB allocated per real second, colored against <paramref name="warnKb"/> / <paramref name="badKb"/>.</summary>
        static string AllocRate(long bytes, double seconds, double warnKb, double badKb)
        {
            double kbPerSecond = bytes / 1024.0 / seconds;
            string text = kbPerSecond >= 1024
                ? (kbPerSecond / 1024.0).ToString("F1", CultureInfo.InvariantCulture) + "MB/s"
                : kbPerSecond.ToString("F0", CultureInfo.InvariantCulture) + "KB/s";
            return Severity(kbPerSecond, warnKb, badKb, text);
        }

        static string MsOrNa(double? value, double warn, double bad)
            => value.HasValue ? Severity(value.Value, warn, bad, Ms(value.Value) + "ms") : Muted("n/a");

        static string Mb(long bytes) => (bytes / 1048576.0).ToString("F0", CultureInfo.InvariantCulture) + " MB";

        static void Say(GmCommandContext context, string text) => GmCommandFeedback.Send(context.Session, context.Player, text);

        static void SendPopup(GmCommandContext context, string title, List<string> rows)
            => GmCommandFeedback.SendLines(context.Session, context.Player, Pages(title, null, rows));
    }
}
