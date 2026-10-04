namespace ZoneEngine_New.Core.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NLog.Targets.Wrappers;

    /// <summary>
    /// Puts every configured log target (console, file, debug ring) behind NLog's background writer, so a log call
    /// on a playfield tick thread only queues the event: console and disk writes, and console stalls, happen on the
    /// writer thread. Event time, message and exception are captured at the call, so timestamps stay accurate.
    /// The queue is bounded; past the limit new events are dropped (and counted) rather than blocking a tick or
    /// growing memory without bound. Pending events are flushed on shutdown, process exit and unhandled exceptions.
    /// </summary>
    public static class AsyncLogging
    {
        /// <summary>Events waiting to be written before new ones are dropped. Roughly tens of MB at the limit.</summary>
        public const int QueueLimit = 100_000;

        /// <summary>Events written per batch by the background writer.</summary>
        const int BatchSize = 500;

        static readonly TimeSpan ExitFlushTimeout = TimeSpan.FromSeconds(5);

        static long droppedEvents;
        static int installed;

        /// <summary>Events discarded because the queue was full since start.</summary>
        public static long DroppedEvents => Interlocked.Read(ref droppedEvents);

        /// <summary>Wraps every target referenced by the current rules. Call once, after all targets are added.</summary>
        public static void Install()
        {
            if (Interlocked.Exchange(ref installed, 1) != 0)
                return;

            LoggingConfiguration? config = LogManager.Configuration;
            if (config == null)
                return;

            var wrappers = new Dictionary<Target, AsyncTargetWrapper>(ReferenceEqualityComparer.Instance);
            foreach (LoggingRule rule in config.LoggingRules)
            {
                for (int i = 0; i < rule.Targets.Count; i++)
                {
                    Target target = rule.Targets[i];
                    if (target is AsyncTargetWrapper)
                        continue;

                    if (!wrappers.TryGetValue(target, out AsyncTargetWrapper? wrapper))
                    {
                        wrapper = new AsyncTargetWrapper((target.Name ?? "target") + "-async", target)
                        {
                            QueueLimit = QueueLimit,
                            OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
                            BatchSize = BatchSize,
                            TimeToSleepBetweenBatches = 1
                        };
                        wrapper.LogEventDropped += static (_, _) => Interlocked.Increment(ref droppedEvents);
                        config.AddTarget(wrapper);
                        wrappers[target] = wrapper;
                    }

                    rule.Targets[i] = wrapper;
                }
            }

            LogManager.Configuration = config;

            AppDomain.CurrentDomain.UnhandledException += static (_, _) => Flush();
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => Flush();
        }

        /// <summary>Writes out everything queued, waiting at most a few seconds.</summary>
        public static void Flush()
        {
            try
            {
                LogManager.Flush(ExitFlushTimeout);
            }
            catch (Exception)
            {
                // Best effort while the process is going down.
            }
        }
    }
}
