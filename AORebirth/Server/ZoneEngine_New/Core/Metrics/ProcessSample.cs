namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Threading;

    /// <summary>
    /// Point-in-time process counters (CPU, GC, locks, thread pool). Two samples give per-interval rates;
    /// each reader keeps its own previous sample.
    /// </summary>
    internal sealed record ProcessSample(
        DateTime Wall,
        TimeSpan Cpu,
        TimeSpan Uptime,
        int Threads,
        long WorkingSetBytes,
        int Gen0,
        int Gen1,
        int Gen2,
        TimeSpan GcPause,
        long LockContentions,
        long PoolCompleted,
        long TotalAllocatedBytes)
    {
        internal static ProcessSample Take()
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
            return new ProcessSample(
                DateTime.UtcNow,
                process.TotalProcessorTime,
                DateTime.Now - process.StartTime,
                process.Threads.Count,
                process.WorkingSet64,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2),
                GC.GetTotalPauseDuration(),
                Monitor.LockContentionCount,
                ThreadPool.CompletedWorkItemCount,
                GC.GetTotalAllocatedBytes(precise: false));
        }

        /// <summary>Pause of the most recent GC (a background GC reports two).</summary>
        internal static double LastPauseMs(GCMemoryInfo gc)
        {
            ReadOnlySpan<TimeSpan> pauses = gc.PauseDurations;
            double total = 0;
            foreach (TimeSpan pause in pauses)
                total += pause.TotalMilliseconds;
            return total;
        }
    }
}
