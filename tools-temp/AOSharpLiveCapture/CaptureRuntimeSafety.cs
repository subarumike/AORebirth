using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AOSharpLiveCapture
{
    internal static class CaptureRuntimeSafety
    {
        public static bool ExecuteBoundary(
            Action action,
            Action<Exception> recordError,
            Action recordRecovery)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                InvokeFailSafe(() => recordError?.Invoke(ex));
                return false;
            }

            InvokeFailSafe(recordRecovery);
            return true;
        }

        public static bool TrySnapshot<TSource, TSnapshot>(
            Func<IEnumerable<TSource>> getSources,
            Func<TSource, TSnapshot> createSnapshot,
            Action<string, Exception> recordError,
            out List<TSnapshot> snapshots)
            where TSource : class
        {
            snapshots = new List<TSnapshot>();
            List<TSource> sources = new List<TSource>();
            try
            {
                IEnumerable<TSource> sourceCollection = getSources();
                if (sourceCollection == null)
                {
                    throw new InvalidOperationException("Capture source collection is unavailable.");
                }

                foreach (TSource source in sourceCollection)
                {
                    sources.Add(source);
                }
            }
            catch (Exception ex)
            {
                InvokeFailSafe(() => recordError?.Invoke("collection", ex));
                snapshots.Clear();
                return false;
            }

            foreach (TSource source in sources)
            {
                if (source == null)
                {
                    InvokeFailSafe(
                        () => recordError?.Invoke(
                            "character",
                            new InvalidOperationException("Capture source collection contains a null character wrapper.")));
                    continue;
                }

                try
                {
                    snapshots.Add(createSnapshot(source));
                }
                catch (Exception ex)
                {
                    InvokeFailSafe(() => recordError?.Invoke("character", ex));
                }
            }

            return true;
        }

        internal static void InvokeFailSafe(Action action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch
            {
            }
        }
    }

    internal sealed class CaptureCallbackBoundary
    {
        private readonly object counterSyncRoot = new object();
        private readonly object appendSyncRoot = new object();
        private readonly Dictionary<string, MutableCallbackCounter> counters =
            new Dictionary<string, MutableCallbackCounter>(StringComparer.Ordinal);
        private string errorLogPath = string.Empty;
        private string fallbackErrorLogPath = string.Empty;
        private long totalInvocationCount;
        private long totalErrorCount;
        private long errorLogWriteFailureCount;

        public void ConfigureFallback(string fallbackPath)
        {
            try
            {
                lock (this.counterSyncRoot)
                {
                    this.fallbackErrorLogPath = fallbackPath ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(this.errorLogPath))
                    {
                        this.errorLogPath = this.fallbackErrorLogPath;
                    }
                }
            }
            catch
            {
            }
        }

        public void BeginSession(string primaryPath, string fallbackPath)
        {
            try
            {
                lock (this.counterSyncRoot)
                {
                    this.counters.Clear();
                    this.totalInvocationCount = 0;
                    this.totalErrorCount = 0;
                    this.errorLogWriteFailureCount = 0;
                    this.errorLogPath = primaryPath ?? string.Empty;
                    this.fallbackErrorLogPath = fallbackPath ?? string.Empty;
                }
            }
            catch
            {
            }
        }

        public bool Dispatch(string callbackName, Action callback)
        {
            string safeCallbackName = NormalizeCallbackName(callbackName);
            this.IncrementInvocation(safeCallbackName);

            try
            {
                if (callback == null)
                {
                    throw new ArgumentNullException("callback");
                }

                callback();
                return true;
            }
            catch (Exception ex)
            {
                this.RecordError(safeCallbackName, ex);
                return false;
            }
        }

        public CaptureCallbackBoundarySnapshot Snapshot()
        {
            try
            {
                lock (this.counterSyncRoot)
                {
                    return new CaptureCallbackBoundarySnapshot(
                        this.totalInvocationCount,
                        this.totalErrorCount,
                        this.errorLogWriteFailureCount,
                        this.errorLogPath,
                        this.counters
                            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                            .Select(
                                entry => new CaptureCallbackCounterSnapshot(
                                    entry.Key,
                                    entry.Value.InvocationCount,
                                    entry.Value.ErrorCount))
                            .ToArray());
                }
            }
            catch
            {
                return CaptureCallbackBoundarySnapshot.Empty;
            }
        }

        private void IncrementInvocation(string callbackName)
        {
            try
            {
                lock (this.counterSyncRoot)
                {
                    MutableCallbackCounter counter = this.GetOrCreateCounter(callbackName);
                    counter.InvocationCount++;
                    this.totalInvocationCount++;
                }
            }
            catch
            {
            }
        }

        private void RecordError(string callbackName, Exception exception)
        {
            try
            {
                lock (this.counterSyncRoot)
                {
                    MutableCallbackCounter counter = this.GetOrCreateCounter(callbackName);
                    counter.ErrorCount++;
                    this.totalErrorCount++;
                }
            }
            catch
            {
            }

            this.AppendErrorNoThrow(callbackName, exception);
        }

        private MutableCallbackCounter GetOrCreateCounter(string callbackName)
        {
            MutableCallbackCounter counter;
            if (!this.counters.TryGetValue(callbackName, out counter))
            {
                counter = new MutableCallbackCounter();
                this.counters.Add(callbackName, counter);
            }

            return counter;
        }

        private void AppendErrorNoThrow(string callbackName, Exception exception)
        {
            try
            {
                string primaryPath;
                string fallbackPath;
                lock (this.counterSyncRoot)
                {
                    primaryPath = this.errorLogPath;
                    fallbackPath = this.fallbackErrorLogPath;
                }

                StringBuilder evidence = new StringBuilder();
                evidence.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                evidence.Append(" callback=");
                evidence.Append(callbackName);
                evidence.Append(" thread=");
                evidence.Append(Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
                evidence.AppendLine();
                evidence.AppendLine(exception == null ? "Unknown callback failure." : exception.ToString());

                bool appended = false;
                lock (this.appendSyncRoot)
                {
                    appended = TryAppend(primaryPath, evidence.ToString());
                    if (!appended
                        && !string.Equals(primaryPath, fallbackPath, StringComparison.OrdinalIgnoreCase))
                    {
                        appended = TryAppend(fallbackPath, evidence.ToString());
                    }
                }

                if (!appended)
                {
                    lock (this.counterSyncRoot)
                    {
                        this.errorLogWriteFailureCount++;
                    }
                }
            }
            catch
            {
                try
                {
                    lock (this.counterSyncRoot)
                    {
                        this.errorLogWriteFailureCount++;
                    }
                }
                catch
                {
                }
            }
        }

        private static bool TryAppend(string path, string evidence)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, evidence, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeCallbackName(string callbackName)
        {
            if (string.IsNullOrWhiteSpace(callbackName))
            {
                return "unknown";
            }

            return callbackName.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private sealed class MutableCallbackCounter
        {
            public long InvocationCount { get; set; }

            public long ErrorCount { get; set; }
        }
    }

    internal sealed class CaptureCallbackBoundarySnapshot
    {
        public static readonly CaptureCallbackBoundarySnapshot Empty =
            new CaptureCallbackBoundarySnapshot(
                0,
                0,
                0,
                string.Empty,
                new CaptureCallbackCounterSnapshot[0]);

        public CaptureCallbackBoundarySnapshot(
            long totalInvocationCount,
            long totalErrorCount,
            long errorLogWriteFailureCount,
            string errorLogPath,
            CaptureCallbackCounterSnapshot[] counters)
        {
            this.TotalInvocationCount = totalInvocationCount;
            this.TotalErrorCount = totalErrorCount;
            this.ErrorLogWriteFailureCount = errorLogWriteFailureCount;
            this.ErrorLogPath = errorLogPath ?? string.Empty;
            this.Counters = counters ?? new CaptureCallbackCounterSnapshot[0];
        }

        public long TotalInvocationCount { get; private set; }

        public long TotalErrorCount { get; private set; }

        public long ErrorLogWriteFailureCount { get; private set; }

        public string ErrorLogPath { get; private set; }

        public CaptureCallbackCounterSnapshot[] Counters { get; private set; }
    }

    internal sealed class CaptureCallbackCounterSnapshot
    {
        public CaptureCallbackCounterSnapshot(string callbackName, long invocationCount, long errorCount)
        {
            this.CallbackName = callbackName;
            this.InvocationCount = invocationCount;
            this.ErrorCount = errorCount;
        }

        public string CallbackName { get; private set; }

        public long InvocationCount { get; private set; }

        public long ErrorCount { get; private set; }
    }

    internal sealed class CaptureRuntimeCircuitBreaker
    {
        private int isTripped;
        private int faultCount;

        public bool IsTripped
        {
            get { return Volatile.Read(ref this.isTripped) != 0; }
        }

        public int FaultCount
        {
            get { return Volatile.Read(ref this.faultCount); }
        }

        public bool TryExecute(Action action, Action<Exception> recordError)
        {
            if (this.IsTripped)
            {
                return false;
            }

            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                if (Interlocked.CompareExchange(ref this.isTripped, 1, 0) == 0)
                {
                    Interlocked.Increment(ref this.faultCount);
                    CaptureRuntimeSafety.InvokeFailSafe(() => recordError?.Invoke(ex));
                }

                return false;
            }
        }
    }

    internal sealed class CaptureCombatRequestGate
    {
        private readonly object syncRoot = new object();
        private long generation;
        private bool pending;
        private DateTime nextRetryUtc = DateTime.MinValue;

        public bool IsPending
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.pending;
                }
            }
        }

        public void Request()
        {
            lock (this.syncRoot)
            {
                this.generation++;
                this.pending = true;
                this.nextRetryUtc = DateTime.MinValue;
            }
        }

        public void Cancel()
        {
            lock (this.syncRoot)
            {
                this.generation++;
                this.pending = false;
                this.nextRetryUtc = DateTime.MinValue;
            }
        }

        public void ResetRetryIfPending()
        {
            lock (this.syncRoot)
            {
                if (this.pending)
                {
                    this.nextRetryUtc = DateTime.MinValue;
                }
            }
        }

        public void MarkRetryRequired()
        {
            lock (this.syncRoot)
            {
                this.generation++;
                this.pending = true;
            }
        }

        public bool TryBegin(
            DateTime nowUtc,
            TimeSpan retryInterval,
            out long sampledGeneration)
        {
            lock (this.syncRoot)
            {
                sampledGeneration = this.generation;
                if (!this.pending || nowUtc < this.nextRetryUtc)
                {
                    return false;
                }

                this.nextRetryUtc = nowUtc.Add(retryInterval);
                return true;
            }
        }

        public bool Complete(long sampledGeneration)
        {
            lock (this.syncRoot)
            {
                if (!this.pending || this.generation != sampledGeneration)
                {
                    return false;
                }

                this.pending = false;
                return true;
            }
        }
    }
}
