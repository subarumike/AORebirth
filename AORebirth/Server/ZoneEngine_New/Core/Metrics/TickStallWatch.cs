namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    using NLog;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Diagnostic for ticks that never return. Each tick thread publishes the stage it is currently
    /// inside; a watcher thread logs any stage that outlives <see cref="StallSeconds"/> and logs
    /// again if the same tick is still stuck. Observation only: nothing here interrupts, aborts or
    /// skips a tick, so a stalled playfield still looks exactly as broken as it is.
    /// Every stage change also closes a timed segment: the tick's worst segment feeds the slow-tick
    /// log and, while <see cref="TickProfiler"/> is recording, every segment feeds its aggregates.
    /// </summary>
    internal static class TickStallWatch
    {
        const double StallSeconds = 5.0;
        const double RepeatSeconds = 15.0;
        const int PollMilliseconds = 1000;

        static readonly ConcurrentDictionary<int, ThreadSlot> SlotsByThread = new();
        static readonly Logger Log = LogManager.GetLogger("TickStall");

        [ThreadStatic]
        static ThreadSlot? threadSlot;

        static int watcherStarted;

        /// <summary>
        /// Opens a tick on the calling thread. Must be paired with <see cref="EndTick"/>.
        /// <paramref name="budgetMs"/> is the heartbeat interval; a tick longer than it is slow.
        /// </summary>
        internal static void BeginTick(int playfieldId, double budgetMs)
        {
            EnsureWatcher();

            ThreadSlot slot = threadSlot ??= SlotsByThread.GetOrAdd(
                Environment.CurrentManagedThreadId,
                threadId => new ThreadSlot(threadId));

            long now = Stopwatch.GetTimestamp();
            TickProfiler.StopIfDue(now);

            slot.PlayfieldId = playfieldId;
            slot.BudgetMs = budgetMs;
            slot.GcPauseAtStart = GC.GetTotalPauseDuration();
            slot.GcCountAtStart = GC.CollectionCount(0);
            slot.Gen1AtStart = GC.CollectionCount(1);
            slot.Gen2AtStart = GC.CollectionCount(2);

            // Allocation is measured only while recording; a tick that started before recording reports none.
            slot.AllocTracking = TickProfiler.IsRecording;
            if (slot.AllocTracking)
            {
                slot.AllocMark = GC.GetAllocatedBytesForCurrentThread();
                slot.TickAllocStart = slot.AllocMark;
            }
            slot.CurrentDynel = null;
            slot.WorstSegmentTicks = 0;
            slot.WorstStage = null;
            slot.WorstDetail = 0;
            slot.WorstDynel = null;
            Volatile.Write(ref slot.Stage, null);
            Volatile.Write(ref slot.Sequence, Volatile.Read(ref slot.Sequence) + 1);
            Volatile.Write(ref slot.TickStartedTicks, now);
            Volatile.Write(ref slot.Transitions, 0);
            Stage("tick.begin");
        }

        internal static void Stage(string stage) => Stage(stage, 0);

        /// <summary>
        /// Publishes the stage the tick just entered. <paramref name="detail"/> carries the id that
        /// makes the stage actionable (cell id, dynel instance). No-op off a tick thread.
        /// </summary>
        internal static void Stage(string stage, int detail)
        {
            ThreadSlot? slot = threadSlot;
            if (slot == null)
                return;

            long now = Stopwatch.GetTimestamp();
            CloseSegment(slot, now);
            slot.Detail = detail;
            Volatile.Write(ref slot.Transitions, Volatile.Read(ref slot.Transitions) + 1);
            Volatile.Write(ref slot.StageStartedTicks, now);
            Volatile.Write(ref slot.Stage, stage);
        }

        /// <summary>
        /// Enters <paramref name="stage"/> for a nested piece of work (a raycast, a path search) and returns to the
        /// enclosing stage when disposed, so the caller's remaining work is still charged to the caller's stage.
        /// </summary>
        internal static StageScope Enter(string stage, int detail)
        {
            ThreadSlot? slot = threadSlot;
            if (slot?.Stage is not string previous)
                return default;

            int previousDetail = slot.Detail;
            Stage(stage, detail);
            return new StageScope(previous, previousDetail);
        }

        internal readonly struct StageScope : IDisposable
        {
            readonly string? _previous;
            readonly int _previousDetail;

            internal StageScope(string previous, int previousDetail)
            {
                _previous = previous;
                _previousDetail = previousDetail;
            }

            public void Dispose()
            {
                if (_previous != null)
                    Stage(_previous, _previousDetail);
            }
        }

        /// <summary>The dynel whose tick is running on this thread, if any.</summary>
        internal static Dynel? CurrentDynel => threadSlot?.CurrentDynel;

        /// <summary>The playfield ticking on this thread; 0 off a tick thread.</summary>
        internal static int CurrentPlayfieldId => threadSlot?.Stage != null ? threadSlot.PlayfieldId : 0;

        /// <summary>
        /// Enters <paramref name="stage"/> for one dynel's tick. Segments until <see cref="EndDynel"/> are
        /// charged to that dynel as well as to the playfield.
        /// </summary>
        internal static void BeginDynel(string stage, Dynel dynel)
        {
            Stage(stage, dynel.Identity.Instance);
            ThreadSlot? slot = threadSlot;
            if (slot == null)
                return;

            slot.CurrentDynel = dynel;
            slot.DynelStartedTicks = slot.StageStartedTicks;
        }

        internal static void EndDynel()
        {
            ThreadSlot? slot = threadSlot;
            if (slot?.CurrentDynel is not Dynel dynel)
                return;

            long now = Stopwatch.GetTimestamp();
            CloseSegment(slot, now);
            TickProfiler.RecordDynel(slot.PlayfieldId, dynel, now - slot.DynelStartedTicks);
            slot.CurrentDynel = null;

            // The stage name stays published for the stall watcher; only its clock restarts.
            Volatile.Write(ref slot.StageStartedTicks, now);
        }

        internal static void EndTick()
        {
            ThreadSlot? slot = threadSlot;
            if (slot == null)
                return;

            long now = Stopwatch.GetTimestamp();
            CloseSegment(slot, now);
            slot.CurrentDynel = null;

            long sequence = Volatile.Read(ref slot.Sequence);
            string? lastStage = Volatile.Read(ref slot.Stage);
            Volatile.Write(ref slot.Stage, null);

            TickProfiler.RecordTick(
                slot.PlayfieldId,
                slot.BudgetMs,
                (GC.GetTotalPauseDuration() - slot.GcPauseAtStart).TotalMilliseconds,
                GC.CollectionCount(0) - slot.GcCountAtStart,
                GC.CollectionCount(1) - slot.Gen1AtStart,
                GC.CollectionCount(2) - slot.Gen2AtStart,
                slot.AllocTracking ? GC.GetAllocatedBytesForCurrentThread() - slot.TickAllocStart : 0,
                now - Volatile.Read(ref slot.TickStartedTicks),
                slot.WorstStage,
                slot.WorstDetail,
                slot.WorstDynel,
                slot.WorstSegmentTicks,
                Volatile.Read(ref slot.Transitions));
            slot.WorstDynel = null;

            if (Volatile.Read(ref slot.ReportedSequence) != sequence)
                return;

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "[pf={0}] tick #{1} recovered after {2:F1}s, last stage={3} detail={4} stages={5}",
                slot.PlayfieldId,
                sequence,
                Seconds(now - Volatile.Read(ref slot.TickStartedTicks)),
                lastStage ?? "(none)",
                slot.Detail,
                Volatile.Read(ref slot.Transitions));
            Log.Warn(message);
            WatchdogFeed.PublishRecovered(message);
        }

        internal static double Milliseconds(long elapsedTicks) => elapsedTicks * 1000.0 / Stopwatch.Frequency;

        /// <summary>Charges the time since the current stage began to that stage. Tick thread only.</summary>
        static void CloseSegment(ThreadSlot slot, long now)
        {
            string? stage = slot.Stage;
            if (stage == null)
                return;

            long elapsed = now - slot.StageStartedTicks;
            if (elapsed > slot.WorstSegmentTicks)
            {
                slot.WorstSegmentTicks = elapsed;
                slot.WorstStage = stage;
                slot.WorstDetail = slot.Detail;
                slot.WorstDynel = slot.CurrentDynel;
            }

            if (!TickProfiler.IsRecording)
                return;

            long allocated = 0;
            if (slot.AllocTracking)
            {
                long mark = GC.GetAllocatedBytesForCurrentThread();
                allocated = mark - slot.AllocMark;
                slot.AllocMark = mark;
            }

            TickProfiler.RecordSegment(slot.PlayfieldId, stage, slot.CurrentDynel, elapsed, allocated);
        }

        static void EnsureWatcher()
        {
            if (Interlocked.CompareExchange(ref watcherStarted, 1, 0) != 0)
                return;

            Thread thread = new(Watch)
            {
                IsBackground = true,
                Name = "TickStallWatch"
            };
            thread.Start();
        }

        static void Watch()
        {
            while (true)
            {
                Thread.Sleep(PollMilliseconds);

                long now = Stopwatch.GetTimestamp();
                foreach (ThreadSlot slot in SlotsByThread.Values)
                    ReportIfStalled(slot, now);
            }
        }

        static void ReportIfStalled(ThreadSlot slot, long now)
        {
            string? stage = Volatile.Read(ref slot.Stage);
            if (stage == null)
                return;

            // Trip on the tick, not the stage: a tick spinning between stages resets the stage clock
            // on every pass and would otherwise never look stuck.
            double tickSeconds = Seconds(now - Volatile.Read(ref slot.TickStartedTicks));
            if (tickSeconds < StallSeconds)
                return;

            long sequence = Volatile.Read(ref slot.Sequence);
            double stageSeconds = Seconds(now - Volatile.Read(ref slot.StageStartedTicks));
            bool alreadyReported = Volatile.Read(ref slot.ReportedSequence) == sequence;
            if (alreadyReported && tickSeconds - slot.ReportedTickSeconds < RepeatSeconds)
                return;

            slot.ReportedTickSeconds = tickSeconds;
            Volatile.Write(ref slot.ReportedSequence, sequence);

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "[pf={0}] tick #{1} stalled {2:F1}s, stage={3} detail={4} for {5:F1}s, stages={6}, thread={7}",
                slot.PlayfieldId,
                sequence,
                tickSeconds,
                stage,
                slot.Detail,
                stageSeconds,
                Volatile.Read(ref slot.Transitions),
                slot.ThreadId);
            Log.Warn(message);
            WatchdogFeed.PublishStall(message);
        }

        static double Seconds(long elapsedTicks) => elapsedTicks / (double)Stopwatch.Frequency;

        sealed class ThreadSlot
        {
            internal ThreadSlot(int threadId)
            {
                ThreadId = threadId;
                ReportedSequence = -1;
            }

            internal int ThreadId { get; }

            internal int PlayfieldId;

            internal long Sequence;

            internal long TickStartedTicks;

            internal long StageStartedTicks;

            /// <summary>Null between ticks; the watcher only reports threads inside a tick.</summary>
            internal string? Stage;

            internal int Detail;

            /// <summary>Stage changes in the current tick. A huge count means a spin, not a block.</summary>
            internal long Transitions;

            internal long ReportedSequence;

            internal double ReportedTickSeconds;

            // Owned by the tick thread; the watcher never reads these.
            internal double BudgetMs;

            internal TimeSpan GcPauseAtStart;

            internal int GcCountAtStart;

            internal int Gen1AtStart;

            internal int Gen2AtStart;

            /// <summary>True when this tick began while recording, so the allocation marks are valid.</summary>
            internal bool AllocTracking;

            /// <summary>Bytes this thread had allocated when the current stage began.</summary>
            internal long AllocMark;

            internal long TickAllocStart;

            internal Dynel? CurrentDynel;

            internal long DynelStartedTicks;

            internal long WorstSegmentTicks;

            internal string? WorstStage;

            internal int WorstDetail;

            internal Dynel? WorstDynel;
        }
    }
}
