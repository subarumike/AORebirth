namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    using NLog;

    /// <summary>
    /// Diagnostic for ticks that never return. Each tick thread publishes the stage it is currently
    /// inside; a watcher thread logs any stage that outlives <see cref="StallSeconds"/> and logs
    /// again if the same tick is still stuck. Observation only: nothing here interrupts, aborts or
    /// skips a tick, so a stalled playfield still looks exactly as broken as it is.
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

        /// <summary>Opens a tick on the calling thread. Must be paired with <see cref="EndTick"/>.</summary>
        internal static void BeginTick(int playfieldId)
        {
            EnsureWatcher();

            ThreadSlot slot = threadSlot ??= SlotsByThread.GetOrAdd(
                Environment.CurrentManagedThreadId,
                threadId => new ThreadSlot(threadId));

            slot.PlayfieldId = playfieldId;
            Volatile.Write(ref slot.Sequence, Volatile.Read(ref slot.Sequence) + 1);
            Volatile.Write(ref slot.TickStartedTicks, Stopwatch.GetTimestamp());
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

            slot.Detail = detail;
            Volatile.Write(ref slot.Transitions, Volatile.Read(ref slot.Transitions) + 1);
            Volatile.Write(ref slot.StageStartedTicks, Stopwatch.GetTimestamp());
            Volatile.Write(ref slot.Stage, stage);
        }

        internal static void EndTick()
        {
            ThreadSlot? slot = threadSlot;
            if (slot == null)
                return;

            long sequence = Volatile.Read(ref slot.Sequence);
            string? lastStage = Volatile.Read(ref slot.Stage);
            Volatile.Write(ref slot.Stage, null);

            if (Volatile.Read(ref slot.ReportedSequence) != sequence)
                return;

            Log.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[pf={0}] tick #{1} recovered after {2:F1}s, last stage={3} detail={4} stages={5}",
                    slot.PlayfieldId,
                    sequence,
                    Seconds(Stopwatch.GetTimestamp() - Volatile.Read(ref slot.TickStartedTicks)),
                    lastStage ?? "(none)",
                    slot.Detail,
                    Volatile.Read(ref slot.Transitions)));
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

            Log.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[pf={0}] tick #{1} stalled {2:F1}s, stage={3} detail={4} for {5:F1}s, stages={6}, thread={7}",
                    slot.PlayfieldId,
                    sequence,
                    tickSeconds,
                    stage,
                    slot.Detail,
                    stageSeconds,
                    Volatile.Read(ref slot.Transitions),
                    slot.ThreadId));
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
        }
    }
}
