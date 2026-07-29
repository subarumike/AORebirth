using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using AOSharp.Common.GameData;
using AOSharp.Core;

namespace AOSharpLiveCapture
{
    internal sealed class MinimalPf127Capture : IDisposable
    {
        internal const string RequestFileName = "pf127-geometry-only.request";

        private const int ResourcePlayfieldId = 127;
        private const int RequiredStableTicks = 20;

        private static readonly TimeSpan RequiredStableDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan StatusWriteInterval = TimeSpan.FromSeconds(1);

        private readonly object logSyncRoot = new object();
        private readonly CaptureRuntimeCircuitBreaker updateCircuitBreaker =
            new CaptureRuntimeCircuitBreaker();
        private readonly string statusPath;
        private readonly StreamWriter modeLog;
        private readonly Pf127GeometryCapture geometryCapture;

        private DateTime stableSinceUtc = DateTime.MinValue;
        private DateTime nextStatusWriteUtc = DateTime.MinValue;
        private StablePlayfieldSignal stableSignal;
        private int stableTickCount;
        private int stableReady;
        private int zoningObserved;
        private int signalFailureCount;
        private int disposed;
        private string lastSignalError = string.Empty;
        private string lastStabilityReason = "The explicit PF127 geometry-only request has not armed yet.";
        private string lastUpdateError = string.Empty;

        private MinimalPf127Capture(string sessionDirectory)
        {
            this.SessionDirectory = sessionDirectory;
            this.statusPath = Path.Combine(sessionDirectory, "capture_info.json");
            this.modeLog = new StreamWriter(
                new FileStream(
                    Path.Combine(sessionDirectory, "pf127-safe-mode.log"),
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite),
                new UTF8Encoding(false))
            {
                AutoFlush = true
            };
            this.geometryCapture = new Pf127GeometryCapture(
                sessionDirectory,
                this.Log,
                true);
            this.Log(
                "SAFE-MODE",
                "PF127 geometry-only capture initialized. Native collection access is gated until the client is not zoning and PF127 identity remains stable for at least five seconds and twenty updates. DevExtras.LoadAllSurfaces is disabled.");
            this.WriteStatusNoThrow(false);
        }

        public string SessionDirectory { get; private set; }

        public static bool ConsumeRequestNoThrow(string pluginDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pluginDirectory))
                {
                    return false;
                }

                string requestPath = Path.Combine(pluginDirectory, RequestFileName);
                if (!File.Exists(requestPath))
                {
                    return false;
                }

                try
                {
                    File.Delete(requestPath);
                }
                catch
                {
                    // The mode is already selected. A stale marker is harmless and
                    // the approved launcher removes it before arming a later request.
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryCreate(
            string pluginDirectory,
            out MinimalPf127Capture capture,
            out string error)
        {
            capture = null;
            error = string.Empty;
            try
            {
                string sessionDirectory = CreateSessionDirectory(pluginDirectory);
                capture = new MinimalPf127Capture(sessionDirectory);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        public void UpdateNoThrow(DateTime capturedUtc)
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                return;
            }

            this.updateCircuitBreaker.TryExecute(
                () => this.UpdateCore(capturedUtc),
                ex =>
                {
                    this.lastUpdateError = ex.ToString();
                    this.Log("SAFE-MODE-UPDATE-CIRCUIT-BROKEN", this.lastUpdateError);
                    this.WriteStatusNoThrow(false);
                });
        }

        public void Dispose()
        {
            this.DisposeNoThrow();
        }

        public void DisposeNoThrow()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            CaptureRuntimeSafety.InvokeFailSafe(() => this.WriteStatusNoThrow(true));
            CaptureRuntimeSafety.InvokeFailSafe(() => this.geometryCapture.Flush());
            CaptureRuntimeSafety.InvokeFailSafe(() => this.geometryCapture.Dispose());
            CaptureRuntimeSafety.InvokeFailSafe(
                () =>
                {
                    lock (this.logSyncRoot)
                    {
                        this.modeLog.WriteLine(
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                            + "\tSAFE-MODE\tPF127 geometry-only capture stopped.");
                        this.modeLog.Flush();
                        this.modeLog.Dispose();
                    }
                });
        }

        private void UpdateCore(DateTime capturedUtc)
        {
            if (Game.IsZoning)
            {
                this.lastStabilityReason =
                    "Game.IsZoning is true; the collector is blocked until zoning ends and a new stable interval completes.";
                if (Interlocked.Exchange(ref this.zoningObserved, 1) == 0)
                {
                    this.Log(
                        "STABILITY-GATE",
                        "Game.IsZoning is true. No PF127 rooms, doors, zones, characters, or LOS APIs will be read until zoning ends and a new stable interval completes.");
                }

                this.ResetStabilityGate();
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            StablePlayfieldSignal currentSignal;
            string signalError;
            if (!TryCaptureStableSignal(out currentSignal, out signalError))
            {
                this.lastSignalError = signalError;
                this.lastStabilityReason = signalError;
                Interlocked.Increment(ref this.signalFailureCount);
                this.ResetStabilityGate();
                this.Log("STABILITY-GATE-WAIT", signalError);
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            if (!currentSignal.IsPf127)
            {
                this.lastStabilityReason =
                    "Current playfield model resource is "
                    + currentSignal.ModelResourceId.ToString(CultureInfo.InvariantCulture)
                    + ", not PF127.";
                this.ResetStabilityGate();
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            if (!currentSignal.Equals(this.stableSignal))
            {
                this.geometryCapture.NotifyPlayfieldChanged(false);
                this.stableSignal = currentSignal;
                this.stableSinceUtc = capturedUtc;
                this.stableTickCount = 1;
                this.lastStabilityReason =
                    "PF127 startup signal is stable but has not yet reached five seconds and twenty updates.";
                Interlocked.Exchange(ref this.stableReady, 0);
                this.Log(
                    "STABILITY-GATE",
                    "PF127 candidate observed; waiting for five seconds and twenty unchanged updates before native collection access. runtime="
                    + currentSignal.RuntimePlayfieldIdentity
                    + " local="
                    + currentSignal.LocalPlayerIdentity);
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            this.stableTickCount++;
            if ((capturedUtc - this.stableSinceUtc) < RequiredStableDuration
                || this.stableTickCount < RequiredStableTicks)
            {
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            if (Game.IsZoning)
            {
                this.ResetStabilityGate();
                this.WriteStatusIfDue(capturedUtc);
                return;
            }

            if (Interlocked.Exchange(ref this.stableReady, 1) == 0)
            {
                this.lastStabilityReason =
                    "PF127 startup stability gate is armed; resident collection is active.";
                this.Log(
                    "STABILITY-GATE-OPEN",
                    "PF127 identity and local player remained stable. Resident geometry, door, and LOS collection may begin.");
            }

            this.geometryCapture.ExecuteUpdateBoundary(
                capturedUtc,
                () => true,
                () => currentSignal.RuntimePlayfieldIdentity);
            this.geometryCapture.Flush();
            this.WriteStatusIfDue(capturedUtc);
        }

        private void ResetStabilityGate()
        {
            this.stableSignal = default(StablePlayfieldSignal);
            this.stableSinceUtc = DateTime.MinValue;
            this.stableTickCount = 0;
            Interlocked.Exchange(ref this.stableReady, 0);
            this.geometryCapture.NotifyPlayfieldChanged(false);
        }

        private static bool TryCaptureStableSignal(
            out StablePlayfieldSignal signal,
            out string error)
        {
            signal = default(StablePlayfieldSignal);
            error = string.Empty;
            try
            {
                if (Game.IsZoning)
                {
                    error = "Game.IsZoning became true before the stability signal could be read.";
                    return false;
                }

                LocalPlayer localPlayer = DynelManager.LocalPlayer;
                if (localPlayer == null)
                {
                    error = "Local player is unavailable.";
                    return false;
                }

                Identity localIdentity = localPlayer.Identity;
                Identity modelIdentity = Playfield.ModelIdentity;
                Identity runtimeIdentity = Playfield.Identity;
                if (Game.IsZoning)
                {
                    error = "Game.IsZoning became true while the stability signal was being read.";
                    return false;
                }

                signal = new StablePlayfieldSignal(
                    modelIdentity.Instance,
                    runtimeIdentity.ToString(),
                    localIdentity.ToString());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private void WriteStatusIfDue(DateTime capturedUtc)
        {
            if (capturedUtc < this.nextStatusWriteUtc)
            {
                return;
            }

            this.nextStatusWriteUtc = capturedUtc.Add(StatusWriteInterval);
            this.WriteStatusNoThrow(false);
        }

        private void WriteStatusNoThrow(bool finalized)
        {
            try
            {
                var issues = new List<string>();
                var notes = new List<string>();
                this.geometryCapture.AppendValidation(issues, notes);
                if (Volatile.Read(ref this.stableReady) == 0)
                {
                    issues.Add(
                        "PF127 geometry-only safe mode was explicitly requested but never armed. "
                        + this.lastStabilityReason);
                }
                else if (!this.geometryCapture.VergilSameIdentityClearAndBlockedObserved)
                {
                    issues.Add(
                        "Vergil Aeneid promotion evidence is incomplete. The same exact MonsterData 203748 identity must have both clear and blocked usable raw and plus-one-Y native LOS/Raycast pairs with matching usable door-state batches; a combat trigger is not required.");
                }
                else
                {
                    notes.Add(
                        "Vergil Aeneid promotion coverage is complete for the same exact identity with clear and blocked raw and plus-one-Y native LOS/Raycast evidence; local-player FightingTarget state is not an acceptance requirement.");
                }

                if (this.updateCircuitBreaker.IsTripped)
                {
                    issues.Add("PF127 geometry-only safe-mode update boundary circuit breaker tripped.");
                }

                bool complete = Volatile.Read(ref this.stableReady) != 0
                                && this.geometryCapture.GeometryWritten
                                && this.geometryCapture.VergilSameIdentityClearAndBlockedObserved
                                && !this.geometryCapture.RecaptureRequired
                                && !this.updateCircuitBreaker.IsTripped;
                var json = new StringBuilder();
                json.Append("{\n");
                json.Append("  \"captureMode\": \"pf127-geometry-only\",\n");
                json.Append("  \"requested\": true,\n");
                json.Append("  \"armed\": ");
                json.Append(Volatile.Read(ref this.stableReady) != 0 ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"complete\": ");
                json.Append(complete ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"recaptureRequired\": ");
                json.Append(complete ? "false" : "true");
                json.Append(",\n");
                json.Append("  \"finalized\": ");
                json.Append(finalized ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"stableGateReady\": ");
                json.Append(Volatile.Read(ref this.stableReady) != 0 ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"zoningObserved\": ");
                json.Append(Volatile.Read(ref this.zoningObserved) != 0 ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"stableTicks\": ");
                json.Append(this.stableTickCount.ToString(CultureInfo.InvariantCulture));
                json.Append(",\n");
                json.Append("  \"signalFailures\": ");
                json.Append(Volatile.Read(ref this.signalFailureCount).ToString(CultureInfo.InvariantCulture));
                json.Append(",\n");
                json.Append("  \"updateBoundaryCircuitBroken\": ");
                json.Append(this.updateCircuitBreaker.IsTripped ? "true" : "false");
                json.Append(",\n");
                json.Append("  \"lastSignalError\": ");
                json.Append(Json(this.lastSignalError));
                json.Append(",\n");
                json.Append("  \"lastStabilityReason\": ");
                json.Append(Json(this.lastStabilityReason));
                json.Append(",\n");
                json.Append("  \"lastUpdateError\": ");
                json.Append(Json(this.lastUpdateError));
                json.Append(",\n");
                json.Append("  \"issues\": [");
                AppendStringArray(json, issues);
                json.Append("],\n");
                json.Append("  \"notes\": [");
                AppendStringArray(json, notes);
                json.Append("],\n");
                this.geometryCapture.AppendHealthJson(json, "  ");
                json.Append("\n}\n");
                File.WriteAllText(this.statusPath, json.ToString(), new UTF8Encoding(false));
                this.Log(
                    "STATUS",
                    "armed="
                    + (Volatile.Read(ref this.stableReady) != 0 ? "true" : "false")
                    + " complete="
                    + (complete ? "true" : "false"));
            }
            catch (Exception ex)
            {
                this.Log("SAFE-MODE-STATUS-WRITE-ERROR", ex.ToString());
            }
        }

        private void Log(string category, string message)
        {
            CaptureRuntimeSafety.InvokeFailSafe(
                () =>
                {
                    lock (this.logSyncRoot)
                    {
                        this.modeLog.WriteLine(
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                            + "\t"
                            + (category ?? string.Empty)
                            + "\t"
                            + (message ?? string.Empty).Replace("\r", " ").Replace("\n", " | "));
                    }
                });
        }

        private static string CreateSessionDirectory(string pluginDirectory)
        {
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                throw new ArgumentException("Plugin directory is required.", "pluginDirectory");
            }

            string captureRoot = Path.Combine(pluginDirectory, "captures");
            Directory.CreateDirectory(captureRoot);
            string prefix = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (int suffix = 0; suffix < 1000; suffix++)
            {
                string name = suffix == 0
                                  ? prefix
                                  : prefix + "-" + suffix.ToString("000", CultureInfo.InvariantCulture);
                string candidate = Path.Combine(captureRoot, name);
                if (Directory.Exists(candidate))
                {
                    continue;
                }

                Directory.CreateDirectory(candidate);
                return candidate;
            }

            throw new IOException("Could not allocate a unique PF127 geometry-only capture directory.");
        }

        private static void AppendStringArray(StringBuilder json, IList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (index != 0)
                {
                    json.Append(", ");
                }

                json.Append(Json(values[index]));
            }
        }

        private static string Json(string value)
        {
            if (value == null)
            {
                return "null";
            }

            return "\""
                   + value.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t")
                   + "\"";
        }

        private struct StablePlayfieldSignal : IEquatable<StablePlayfieldSignal>
        {
            public StablePlayfieldSignal(
                int modelResourceId,
                string runtimePlayfieldIdentity,
                string localPlayerIdentity)
            {
                this.ModelResourceId = modelResourceId;
                this.RuntimePlayfieldIdentity = runtimePlayfieldIdentity ?? string.Empty;
                this.LocalPlayerIdentity = localPlayerIdentity ?? string.Empty;
            }

            public int ModelResourceId { get; private set; }

            public bool IsPf127
            {
                get { return this.ModelResourceId == ResourcePlayfieldId; }
            }

            public string RuntimePlayfieldIdentity { get; private set; }

            public string LocalPlayerIdentity { get; private set; }

            public bool Equals(StablePlayfieldSignal other)
            {
                return this.ModelResourceId == other.ModelResourceId
                       && string.Equals(
                           this.RuntimePlayfieldIdentity,
                           other.RuntimePlayfieldIdentity,
                           StringComparison.Ordinal)
                       && string.Equals(
                           this.LocalPlayerIdentity,
                           other.LocalPlayerIdentity,
                           StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is StablePlayfieldSignal
                       && this.Equals((StablePlayfieldSignal)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = this.ModelResourceId;
                    hash = (hash * 397) ^ this.RuntimePlayfieldIdentity.GetHashCode();
                    hash = (hash * 397) ^ this.LocalPlayerIdentity.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
