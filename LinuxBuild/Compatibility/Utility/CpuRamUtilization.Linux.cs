using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Utility
{
    public class CpuRamUtilization : IDisposable
    {
        private static readonly object InstanceLock = new object();
        private static readonly object SampleLock = new object();
        private static CpuRamUtilization instance;
        private static ulong previousCpuTotal;
        private static ulong previousCpuIdle;
        private static bool hasCpuSample;
        private static DateTime previousProcessSampleTime;
        private static TimeSpan previousProcessCpuTime;

        private bool disposed;

        private CpuRamUtilization()
        {
        }

        public static CpuRamUtilization Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    if (instance == null)
                    {
                        instance = new CpuRamUtilization();
                    }

                    return instance;
                }
            }
        }

        public static float GetCpuLoad()
        {
            Instance.ThrowIfDisposed();
            float value = ReadCpuLoad();
            if (value == 0.0f)
            {
                Thread.Sleep(100);
                value = ReadCpuLoad();
            }

            return Math.Max(0.0f, Math.Min(100.0f, value));
        }

        public static float GetRamLoad()
        {
            Instance.ThrowIfDisposed();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length >= 2
                        && double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double kilobytes))
                    {
                        return (float)(kilobytes / 1024.0d);
                    }
                }
            }

            GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
            long availableBytes = memoryInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false);
            return (float)(Math.Max(0L, availableBytes) / (1024.0d * 1024.0d));
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.disposed = true;
        }

        private static float ReadCpuLoad()
        {
            lock (SampleLock)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string firstLine;
                    using (var reader = new StreamReader("/proc/stat"))
                    {
                        firstLine = reader.ReadLine();
                    }

                    if (TryParseLinuxCpuSnapshot(firstLine, out ulong total, out ulong idle))
                    {
                        if (!hasCpuSample)
                        {
                            previousCpuTotal = total;
                            previousCpuIdle = idle;
                            hasCpuSample = true;
                            return 0.0f;
                        }

                        if (total < previousCpuTotal || idle < previousCpuIdle)
                        {
                            previousCpuTotal = total;
                            previousCpuIdle = idle;
                            return 0.0f;
                        }

                        ulong totalDelta = total - previousCpuTotal;
                        ulong idleDelta = idle - previousCpuIdle;
                        previousCpuTotal = total;
                        previousCpuIdle = idle;
                        return totalDelta == 0
                            ? 0.0f
                            : (float)((totalDelta - Math.Min(totalDelta, idleDelta)) * 100.0d / totalDelta);
                    }
                }

                using (Process process = Process.GetCurrentProcess())
                {
                    DateTime currentTime = DateTime.UtcNow;
                    TimeSpan currentCpuTime = process.TotalProcessorTime;
                    if (previousProcessSampleTime == default(DateTime))
                    {
                        previousProcessSampleTime = currentTime;
                        previousProcessCpuTime = currentCpuTime;
                        return 0.0f;
                    }

                    double elapsedMilliseconds = (currentTime - previousProcessSampleTime).TotalMilliseconds;
                    double cpuMilliseconds = (currentCpuTime - previousProcessCpuTime).TotalMilliseconds;
                    previousProcessSampleTime = currentTime;
                    previousProcessCpuTime = currentCpuTime;
                    return elapsedMilliseconds <= 0.0d
                        ? 0.0f
                        : (float)(cpuMilliseconds * 100.0d / (elapsedMilliseconds * Environment.ProcessorCount));
                }
            }
        }

        private static bool TryParseLinuxCpuSnapshot(string line, out ulong total, out ulong idle)
        {
            total = 0UL;
            idle = 0UL;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 || !string.Equals(fields[0], "cpu", StringComparison.Ordinal))
            {
                return false;
            }

            var values = new ulong[fields.Length - 1];
            for (int index = 1; index < fields.Length; index++)
            {
                if (!ulong.TryParse(
                    fields[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out values[index - 1]))
                {
                    return false;
                }
            }

            // Linux reports guest and guest_nice after steal, but those values
            // are already included in user and nice. Sum only user through steal.
            int totalFieldCount = Math.Min(values.Length, 8);
            for (int index = 0; index < totalFieldCount; index++)
            {
                total += values[index];
            }

            idle = values[3] + (values.Length > 4 ? values[4] : 0UL);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(typeof(CpuRamUtilization).FullName);
            }
        }
    }
}
