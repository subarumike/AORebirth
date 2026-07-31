using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.DbObjects;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Core;

namespace AOSharpLiveCapture
{
    internal sealed class Pf127GeometryCapture : IDisposable
    {
        private const int ResourcePlayfieldId = 127;
        private const int CapturePlayfieldObjectId = 122002;
        private const int VergilAeneidMonsterData = 203748;
        private const int DoorLinkSchemaVersion = 1;
        private const string DoorLinkUnavailableForClientSafety =
            "unavailable_not_read_for_client_safety";
        private const int GeometryStageWaitingForReadiness = 0;
        private const int GeometryStageReadinessObserved = 1;
        private const int GeometryStageSurfacesLoaded = 2;
        private const int GeometryStageComplete = 3;
        private const int GeometryStageCircuitBroken = 4;
        private static readonly TimeSpan GeometryRetryInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan PeriodicLineOfSightInterval = TimeSpan.FromSeconds(1);

        private readonly string sessionDirectory;
        private readonly string geometryPath;
        private readonly string lineOfSightPath;
        private readonly string doorStatePath;
        private readonly string captureErrorPath;
        private readonly Action<string, string> logEvent;
        private readonly bool residentSurfacesOnly;
        private readonly object captureErrorSync = new object();
        private readonly object vergilLosCoverageSync = new object();
        private readonly CaptureCombatRequestGate combatRequestGate = new CaptureCombatRequestGate();
        private readonly CaptureRuntimeCircuitBreaker runtimeBoundaryCircuitBreaker = new CaptureRuntimeCircuitBreaker();
        private readonly HashSet<string> vergilClearIdentityKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> vergilBlockedIdentityKeys = new HashSet<string>(StringComparer.Ordinal);

        private StreamWriter lineOfSightWriter;
        private StreamWriter doorStateWriter;
        private DateTime nextGeometryAttemptUtc = DateTime.MinValue;
        private DateTime nextPeriodicLineOfSightUtc = DateTime.MinValue;
        private DateTime nextDoorStateRetryUtc = DateTime.MinValue;
        private string stableGeometryCandidateSha256;
        private string lastDoorStateFingerprint = string.Empty;
        private string lastLoggedGeometryError = string.Empty;
        private string lastGeometryError = string.Empty;
        private string lastLineOfSightError = string.Empty;
        private string lastDoorStateError = string.Empty;
        private string lastRuntimeBoundaryError = string.Empty;
        private string lastTransientWrapperError = string.Empty;
        private string lastCaptureErrorWriteError = string.Empty;
        private int isPf127Active;
        private int pf127Observed;
        private int pf127CombatObserved;
        private int vergilCombatObserved;
        private int pendingActivationDoorSnapshot;
        private int combatTriggerCount;
        private int geometryWritten;
        private int disposed;
        private int geometryAttemptCount;
        private int geometryFailureCount;
        private int residentSurfaceIncompleteRetryCount;
        private int geometryStage;
        private int loadAllSurfacesCallCount;
        private int geometryRoomCount;
        private int geometryDoorCount;
        private int geometryMeshCount;
        private int geometryVertexCount;
        private int geometryTriangleCount;
        private int lineOfSightBatchCount;
        private int periodicLineOfSightBatchCount;
        private int combatLineOfSightBatchCount;
        private int lineOfSightRowCount;
        private int usableLineOfSightRowCount;
        private int combatLineOfSightRowCount;
        private int combatUsableLineOfSightRowCount;
        private int combatUsableRawVariantRowCount;
        private int combatUsablePlusOneVariantRowCount;
        private int lineOfSightProbeErrorCount;
        private int lineOfSightWriteErrorCount;
        private long nextEvidenceBatchId;
        private int doorStateRevision;
        private int doorStateBatchAttemptCount;
        private int usableDoorStateBatchCount;
        private int doorStateRowCount;
        private int doorStateReadErrorCount;
        private int doorStateWriteErrorCount;
        private int doorStateGenerationCircuitBroken;
        private int combatMatchedDoorAndLosBatchCount;
        private int vergilCombatMatchedDoorAndLosBatchCount;
        private int runtimeBoundaryErrorCount;
        private int transientWrapperSkipCount;
        private int captureErrorWriteErrorCount;

        public Pf127GeometryCapture(string sessionDirectory, Action<string, string> logEvent)
            : this(sessionDirectory, logEvent, false)
        {
        }

        internal Pf127GeometryCapture(
            string sessionDirectory,
            Action<string, string> logEvent,
            bool residentSurfacesOnly)
        {
            this.sessionDirectory = sessionDirectory;
            this.geometryPath = Path.Combine(sessionDirectory, "pf127-geometry.json");
            this.lineOfSightPath = Path.Combine(sessionDirectory, "pf127-line-of-sight.csv");
            this.doorStatePath = Path.Combine(sessionDirectory, "pf127-door-state.csv");
            this.captureErrorPath = Path.Combine(sessionDirectory, "pf127-capture-errors.log");
            this.logEvent = logEvent;
            this.residentSurfacesOnly = residentSurfacesOnly;
            this.EnsureLineOfSightWriter();
            this.EnsureDoorStateWriter();
        }

        public bool Pf127Observed
        {
            get { return Volatile.Read(ref this.pf127Observed) != 0; }
        }

        public bool Pf127CombatObserved
        {
            get { return Volatile.Read(ref this.pf127CombatObserved) != 0; }
        }

        public bool GeometryWritten
        {
            get { return Volatile.Read(ref this.geometryWritten) != 0; }
        }

        public bool RuntimeBoundaryCircuitBroken
        {
            get { return this.runtimeBoundaryCircuitBreaker.IsTripped; }
        }

        public bool VergilSameIdentityClearAndBlockedObserved
        {
            get
            {
                lock (this.vergilLosCoverageSync)
                {
                    return this.vergilClearIdentityKeys.Overlaps(this.vergilBlockedIdentityKeys);
                }
            }
        }

        public bool RecaptureRequired
        {
            get
            {
                return this.RuntimeBoundaryCircuitBroken
                       || Volatile.Read(ref this.captureErrorWriteErrorCount) != 0
                       || Volatile.Read(ref this.doorStateGenerationCircuitBroken) != 0
                       || (this.Pf127Observed
                           && (!this.GeometryWritten
                           || Volatile.Read(ref this.usableDoorStateBatchCount) == 0
                           || (this.Pf127CombatObserved
                               && (Volatile.Read(ref this.combatUsableRawVariantRowCount) == 0
                                   || Volatile.Read(ref this.combatUsablePlusOneVariantRowCount) == 0
                                   || Volatile.Read(ref this.combatMatchedDoorAndLosBatchCount) == 0
                                   || (Volatile.Read(ref this.vergilCombatObserved) != 0
                                       && (Volatile.Read(ref this.vergilCombatMatchedDoorAndLosBatchCount) == 0
                                           || !this.VergilSameIdentityClearAndBlockedObserved))))
                           || Volatile.Read(ref this.lineOfSightWriteErrorCount) > 0
                           || Volatile.Read(ref this.doorStateWriteErrorCount) > 0));
            }
        }

        public void NotifyPlayfieldChanged(bool isPf127)
        {
            int previous = Interlocked.Exchange(ref this.isPf127Active, isPf127 ? 1 : 0);
            if (!isPf127)
            {
                if (previous != 0)
                {
                    this.stableGeometryCandidateSha256 = null;
                    Interlocked.Exchange(ref this.doorStateGenerationCircuitBroken, 0);
                }

                this.combatRequestGate.Cancel();

                Interlocked.Exchange(ref this.pendingActivationDoorSnapshot, 0);
                return;
            }

            Interlocked.Exchange(ref this.pf127Observed, 1);
            if (previous == 0)
            {
                this.nextGeometryAttemptUtc = DateTime.MinValue;
                this.nextPeriodicLineOfSightUtc = DateTime.MinValue;
                this.nextDoorStateRetryUtc = DateTime.MinValue;
                Interlocked.Exchange(ref this.pendingActivationDoorSnapshot, 1);
            }
        }

        public void RequestCombatSample()
        {
            if (Volatile.Read(ref this.isPf127Active) == 0)
            {
                return;
            }

            Interlocked.Exchange(ref this.pf127CombatObserved, 1);
            Interlocked.Increment(ref this.combatTriggerCount);
            this.combatRequestGate.Request();
        }

        public void RequestImmediateUpdate()
        {
            this.nextGeometryAttemptUtc = DateTime.MinValue;
            this.nextPeriodicLineOfSightUtc = DateTime.MinValue;
            this.nextDoorStateRetryUtc = DateTime.MinValue;
            this.combatRequestGate.ResetRetryIfPending();
            if (Volatile.Read(ref this.isPf127Active) != 0)
            {
                Interlocked.Exchange(ref this.pendingActivationDoorSnapshot, 1);
            }
        }

        private void CompleteCombatRequest(long sampledGeneration)
        {
            this.combatRequestGate.Complete(sampledGeneration);
        }

        public bool ExecuteUpdateBoundary(
            DateTime capturedUtc,
            Func<bool> detectPf127,
            Func<string> detectRuntimePlayfieldId)
        {
            return this.runtimeBoundaryCircuitBreaker.TryExecute(
                () => this.Update(capturedUtc, detectPf127(), detectRuntimePlayfieldId()),
                ex => this.RecordRuntimeBoundaryException(
                    capturedUtc,
                    "Main.OnUpdate.Pf127GeometryCapture",
                    ex));
        }

        public void RecordRuntimeBoundaryException(DateTime capturedUtc, string phase, Exception ex)
        {
            try
            {
                string detail = ex == null
                                    ? "Unknown PF127 capture boundary exception."
                                    : ex.ToString();
                this.lastRuntimeBoundaryError = detail;
                Interlocked.Increment(ref this.runtimeBoundaryErrorCount);
                this.AppendCaptureError(capturedUtc, phase, detail);
            }
            catch
            {
            }
        }

        public void Update(DateTime nowUtc, bool isPf127, string runtimePlayfieldId)
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                return;
            }

            this.NotifyPlayfieldChanged(isPf127);
            if (!isPf127)
            {
                return;
            }

            if (!this.GeometryWritten
                && Volatile.Read(ref this.geometryStage) != GeometryStageCircuitBroken
                && nowUtc >= this.nextGeometryAttemptUtc)
            {
                this.nextGeometryAttemptUtc = nowUtc.Add(GeometryRetryInterval);
                this.TryWriteCanonicalGeometry();
            }

            if (Volatile.Read(ref this.pendingActivationDoorSnapshot) != 0
                && nowUtc >= this.nextDoorStateRetryUtc)
            {
                this.nextDoorStateRetryUtc = nowUtc.Add(GeometryRetryInterval);
                long activationBatchId = Interlocked.Increment(ref this.nextEvidenceBatchId);
                DoorStateBatchResult activationDoorState = this.CaptureDoorStateBatch(
                    nowUtc,
                    "playfield-activation",
                    activationBatchId,
                    runtimePlayfieldId);
                if (activationDoorState.Usable)
                {
                    Interlocked.Exchange(ref this.pendingActivationDoorSnapshot, 0);
                }
            }

            long sampledCombatGeneration;
            if (this.combatRequestGate.TryBegin(
                    nowUtc,
                    PeriodicLineOfSightInterval,
                    out sampledCombatGeneration))
            {
                this.SampleLineOfSight(
                    nowUtc,
                    "combat",
                    runtimePlayfieldId,
                    new CombatRequestSnapshot(sampledCombatGeneration));
            }

            if (nowUtc >= this.nextPeriodicLineOfSightUtc)
            {
                this.nextPeriodicLineOfSightUtc = nowUtc.Add(PeriodicLineOfSightInterval);
                this.SampleLineOfSight(
                    nowUtc,
                    "periodic",
                    runtimePlayfieldId,
                    CombatRequestSnapshot.Empty);
            }
        }

        public void AppendHealthJson(StringBuilder json, string indent)
        {
            bool complete = !this.Pf127Observed || !this.RecaptureRequired;
            json.Append(indent);
            json.Append("\"pf127GeometryAndLineOfSight\": {\n");
            AppendBooleanProperty(json, indent + "  ", "pf127Observed", this.Pf127Observed, true);
            AppendBooleanProperty(json, indent + "  ", "pf127CombatObserved", this.Pf127CombatObserved, true);
            AppendBooleanProperty(json, indent + "  ", "vergilCombatObserved", Volatile.Read(ref this.vergilCombatObserved) != 0, true);
            AppendBooleanProperty(json, indent + "  ", "vergilSameIdentityClearAndBlockedObserved", this.VergilSameIdentityClearAndBlockedObserved, true);
            AppendBooleanProperty(json, indent + "  ", "complete", complete, true);
            AppendBooleanProperty(json, indent + "  ", "recaptureRequired", this.RecaptureRequired, true);
            AppendStringProperty(json, indent + "  ", "geometryPath", this.geometryPath, true);
            AppendStringProperty(json, indent + "  ", "lineOfSightPath", this.lineOfSightPath, true);
            AppendStringProperty(json, indent + "  ", "doorStatePath", this.doorStatePath, true);
            AppendStringProperty(json, indent + "  ", "captureErrorPath", this.captureErrorPath, true);
            json.Append(indent);
            json.Append("  \"runtimeSafety\": {\n");
            AppendBooleanProperty(json, indent + "    ", "circuitBroken", this.RuntimeBoundaryCircuitBroken, true);
            AppendIntegerProperty(json, indent + "    ", "boundaryErrors", Volatile.Read(ref this.runtimeBoundaryErrorCount), true);
            AppendIntegerProperty(json, indent + "    ", "circuitBreakerFaults", this.runtimeBoundaryCircuitBreaker.FaultCount, true);
            AppendIntegerProperty(json, indent + "    ", "transientWrapperSkips", Volatile.Read(ref this.transientWrapperSkipCount), true);
            AppendIntegerProperty(json, indent + "    ", "errorLogWriteErrors", Volatile.Read(ref this.captureErrorWriteErrorCount), true);
            AppendStringProperty(json, indent + "    ", "lastBoundaryError", this.lastRuntimeBoundaryError, true);
            AppendStringProperty(json, indent + "    ", "lastTransientWrapperError", this.lastTransientWrapperError, true);
            AppendStringProperty(json, indent + "    ", "lastErrorLogWriteError", this.lastCaptureErrorWriteError, false);
            json.Append(indent);
            json.Append("  },\n");
            json.Append(indent);
            json.Append("  \"geometry\": {\n");
            AppendBooleanProperty(json, indent + "    ", "written", this.GeometryWritten, true);
            AppendBooleanProperty(json, indent + "    ", "residentSurfacesOnly", this.residentSurfacesOnly, true);
            AppendIntegerProperty(json, indent + "    ", "stage", Volatile.Read(ref this.geometryStage), true);
            AppendBooleanProperty(json, indent + "    ", "circuitBroken", Volatile.Read(ref this.geometryStage) == GeometryStageCircuitBroken, true);
            AppendIntegerProperty(json, indent + "    ", "loadAllSurfacesCalls", Volatile.Read(ref this.loadAllSurfacesCallCount), true);
            AppendIntegerProperty(json, indent + "    ", "attempts", Volatile.Read(ref this.geometryAttemptCount), true);
            AppendIntegerProperty(json, indent + "    ", "failures", Volatile.Read(ref this.geometryFailureCount), true);
            AppendIntegerProperty(json, indent + "    ", "residentSurfaceIncompleteRetries", Volatile.Read(ref this.residentSurfaceIncompleteRetryCount), true);
            AppendIntegerProperty(json, indent + "    ", "rooms", Volatile.Read(ref this.geometryRoomCount), true);
            AppendIntegerProperty(json, indent + "    ", "doors", Volatile.Read(ref this.geometryDoorCount), true);
            AppendIntegerProperty(json, indent + "    ", "meshes", Volatile.Read(ref this.geometryMeshCount), true);
            AppendIntegerProperty(json, indent + "    ", "vertices", Volatile.Read(ref this.geometryVertexCount), true);
            AppendIntegerProperty(json, indent + "    ", "triangles", Volatile.Read(ref this.geometryTriangleCount), true);
            AppendStringProperty(json, indent + "    ", "lastError", this.lastGeometryError, false);
            json.Append(indent);
            json.Append("  },\n");
            json.Append(indent);
            json.Append("  \"doorState\": {\n");
            AppendIntegerProperty(json, indent + "    ", "revision", Volatile.Read(ref this.doorStateRevision), true);
            AppendIntegerProperty(json, indent + "    ", "batchAttempts", Volatile.Read(ref this.doorStateBatchAttemptCount), true);
            AppendIntegerProperty(json, indent + "    ", "usableBatches", Volatile.Read(ref this.usableDoorStateBatchCount), true);
            AppendIntegerProperty(json, indent + "    ", "rows", Volatile.Read(ref this.doorStateRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "readErrors", Volatile.Read(ref this.doorStateReadErrorCount), true);
            AppendIntegerProperty(json, indent + "    ", "writeErrors", Volatile.Read(ref this.doorStateWriteErrorCount), true);
            AppendBooleanProperty(json, indent + "    ", "generationCircuitBroken", Volatile.Read(ref this.doorStateGenerationCircuitBroken) != 0, true);
            AppendIntegerProperty(json, indent + "    ", "combatMatchedLosBatches", Volatile.Read(ref this.combatMatchedDoorAndLosBatchCount), true);
            AppendStringProperty(json, indent + "    ", "lastError", this.lastDoorStateError, false);
            json.Append(indent);
            json.Append("  },\n");
            json.Append(indent);
            json.Append("  \"lineOfSight\": {\n");
            AppendIntegerProperty(json, indent + "    ", "combatTriggers", Volatile.Read(ref this.combatTriggerCount), true);
            AppendIntegerProperty(json, indent + "    ", "sampleBatches", Volatile.Read(ref this.lineOfSightBatchCount), true);
            AppendIntegerProperty(json, indent + "    ", "periodicBatches", Volatile.Read(ref this.periodicLineOfSightBatchCount), true);
            AppendIntegerProperty(json, indent + "    ", "combatBatches", Volatile.Read(ref this.combatLineOfSightBatchCount), true);
            AppendIntegerProperty(json, indent + "    ", "rows", Volatile.Read(ref this.lineOfSightRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "usableRows", Volatile.Read(ref this.usableLineOfSightRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "combatRows", Volatile.Read(ref this.combatLineOfSightRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "combatUsableRows", Volatile.Read(ref this.combatUsableLineOfSightRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "combatUsableRawVariantRows", Volatile.Read(ref this.combatUsableRawVariantRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "combatUsablePlusOneVariantRows", Volatile.Read(ref this.combatUsablePlusOneVariantRowCount), true);
            AppendIntegerProperty(json, indent + "    ", "vergilCombatMatchedDoorAndLosBatches", Volatile.Read(ref this.vergilCombatMatchedDoorAndLosBatchCount), true);
            AppendIntegerProperty(json, indent + "    ", "probeErrors", Volatile.Read(ref this.lineOfSightProbeErrorCount), true);
            AppendIntegerProperty(json, indent + "    ", "writeErrors", Volatile.Read(ref this.lineOfSightWriteErrorCount), true);
            AppendStringProperty(json, indent + "    ", "lastError", this.lastLineOfSightError, false);
            json.Append(indent);
            json.Append("  }\n");
            json.Append(indent);
            json.Append("}");
        }

        public void AppendValidation(List<string> issues, List<string> notes)
        {
            int boundaryErrors = Volatile.Read(ref this.runtimeBoundaryErrorCount);
            if (this.RuntimeBoundaryCircuitBroken)
            {
                issues.Add(
                    "PF127 capture runtime circuit breaker tripped; the collector is disabled for this session and required geometry/LOS coverage is incomplete. Full evidence: "
                    + this.captureErrorPath);
            }
            else if (boundaryErrors > 0)
            {
                issues.Add("PF127 capture recorded a runtime-boundary exception without a tripped circuit breaker.");
            }

            int transientWrapperSkips = Volatile.Read(ref this.transientWrapperSkipCount);
            if (transientWrapperSkips > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 capture safely skipped {0} transient AO character-wrapper read(s); successful later required coverage determines acceptance. Last error: {1}",
                        transientWrapperSkips,
                        string.IsNullOrEmpty(this.lastTransientWrapperError)
                            ? "none recorded"
                            : this.lastTransientWrapperError));
            }

            int captureErrorWriteErrors = Volatile.Read(ref this.captureErrorWriteErrorCount);
            if (captureErrorWriteErrors > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "pf127-capture-errors.log reported {0} append error(s); runtime failure evidence is incomplete. Last error: {1}",
                        captureErrorWriteErrors,
                        string.IsNullOrEmpty(this.lastCaptureErrorWriteError)
                            ? "none recorded"
                            : this.lastCaptureErrorWriteError));
            }

            if (!this.Pf127Observed)
            {
                notes.Add("Resource playfield 127 was not observed; PF127 geometry and line-of-sight requirements were not activated.");
                return;
            }

            if (!this.GeometryWritten)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Resource playfield 127 was observed, but deterministic pf127-geometry.json was not written after {0} attempt(s). Last error: {1}",
                        Volatile.Read(ref this.geometryAttemptCount),
                        string.IsNullOrEmpty(this.lastGeometryError) ? "none recorded" : this.lastGeometryError));
            }
            else
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 geometry complete: rooms={0}, doors={1}, meshes={2}, vertices={3}, triangles={4}.",
                        Volatile.Read(ref this.geometryRoomCount),
                        Volatile.Read(ref this.geometryDoorCount),
                        Volatile.Read(ref this.geometryMeshCount),
                        Volatile.Read(ref this.geometryVertexCount),
                        Volatile.Read(ref this.geometryTriangleCount)));
            }

            if (Volatile.Read(ref this.usableDoorStateBatchCount) == 0)
            {
                issues.Add(
                    "Resource playfield 127 was observed, but pf127-door-state.csv has no complete usable door snapshot.");
            }
            else
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 door-state capture complete: revision={0}, usableBatches={1}, rows={2}.",
                        Volatile.Read(ref this.doorStateRevision),
                        Volatile.Read(ref this.usableDoorStateBatchCount),
                        Volatile.Read(ref this.doorStateRowCount)));
            }

            if (this.Pf127CombatObserved
                && (Volatile.Read(ref this.combatUsableRawVariantRowCount) == 0
                    || Volatile.Read(ref this.combatUsablePlusOneVariantRowCount) == 0
                    || Volatile.Read(ref this.combatMatchedDoorAndLosBatchCount) == 0
                    || (Volatile.Read(ref this.vergilCombatObserved) != 0
                        && (Volatile.Read(ref this.vergilCombatMatchedDoorAndLosBatchCount) == 0
                            || !this.VergilSameIdentityClearAndBlockedObserved))))
            {
                issues.Add(
                    "PF127 combat was observed, but promotion coverage is incomplete. A same-batch combat-target match must contain a usable door-state snapshot plus raw and plus-one-Y LOS/Raycast variants; Vergil also requires clear and blocked native LOS samples for the same exact MonsterData 203748 identity.");
            }
            else if (this.Pf127CombatObserved)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 combat line-of-sight complete: triggers={0}, combatRows={1}, combatUsableRows={2}, rawUsableRows={3}, plusOneUsableRows={4}, matchedDoorBatches={5}.",
                        Volatile.Read(ref this.combatTriggerCount),
                        Volatile.Read(ref this.combatLineOfSightRowCount),
                        Volatile.Read(ref this.combatUsableLineOfSightRowCount),
                        Volatile.Read(ref this.combatUsableRawVariantRowCount),
                        Volatile.Read(ref this.combatUsablePlusOneVariantRowCount),
                        Volatile.Read(ref this.combatMatchedDoorAndLosBatchCount)));
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 Vergil Aeneid promotion-ready LOS batches (MonsterData {0}): {1}.",
                        VergilAeneidMonsterData,
                        Volatile.Read(ref this.vergilCombatMatchedDoorAndLosBatchCount)));
            }
            else
            {
                notes.Add(
                    "No PF127 combat context was observed; promotion does not require one when identity-proven periodic Vergil raw and plus-one-Y evidence includes matching usable door-state batches.");
            }

            int probeErrors = Volatile.Read(ref this.lineOfSightProbeErrorCount);
            if (probeErrors > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 line-of-sight API probes failed {0} time(s); these transient failures were retained as evidence and do not require recapture when required raw and plus-one-Y coverage later succeeds. Last error: {1}",
                        probeErrors,
                        string.IsNullOrEmpty(this.lastLineOfSightError) ? "none recorded" : this.lastLineOfSightError));
            }

            int writeErrors = Volatile.Read(ref this.lineOfSightWriteErrorCount);
            if (writeErrors > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "pf127-line-of-sight.csv reported {0} open/write/flush/close error(s); the LOS evidence stream is incomplete and recapture is required. Last error: {1}",
                        writeErrors,
                        string.IsNullOrEmpty(this.lastLineOfSightError) ? "none recorded" : this.lastLineOfSightError));
            }

            int doorReadErrors = Volatile.Read(ref this.doorStateReadErrorCount);
            if (doorReadErrors > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 door enumeration/state reads failed {0} time(s); the current playfield-generation door circuit breaker stopped all further native door reads. Re-enter PF127 after a completed teleport before another attempt. Last error: {1}",
                        doorReadErrors,
                        string.IsNullOrEmpty(this.lastDoorStateError) ? "none recorded" : this.lastDoorStateError));
            }

            int doorWriteErrors = Volatile.Read(ref this.doorStateWriteErrorCount);
            if (doorWriteErrors > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "pf127-door-state.csv reported {0} open/write/flush/close error(s); dynamic door evidence is incomplete and recapture is required. Last error: {1}",
                        doorWriteErrors,
                        string.IsNullOrEmpty(this.lastDoorStateError) ? "none recorded" : this.lastDoorStateError));
            }
        }

        public void Flush()
        {
            if (this.lineOfSightWriter != null)
            {
                try
                {
                    this.lineOfSightWriter.Flush();
                }
                catch (Exception ex)
                {
                    this.RecordLineOfSightWriteError(ex);
                }
            }

            if (this.doorStateWriter != null)
            {
                try
                {
                    this.doorStateWriter.Flush();
                }
                catch (Exception ex)
                {
                    this.RecordDoorStateWriteError(ex);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            StreamWriter writer = this.lineOfSightWriter;
            this.lineOfSightWriter = null;
            if (writer != null)
            {
                try
                {
                    writer.Flush();
                    writer.Dispose();
                }
                catch (Exception ex)
                {
                    this.RecordLineOfSightWriteError(ex);
                }
            }

            StreamWriter doorWriter = this.doorStateWriter;
            this.doorStateWriter = null;
            if (doorWriter != null)
            {
                try
                {
                    doorWriter.Flush();
                    doorWriter.Dispose();
                }
                catch (Exception ex)
                {
                    this.RecordDoorStateWriteError(ex);
                }
            }
        }

        private void TryWriteCanonicalGeometry()
        {
            int stage = Volatile.Read(ref this.geometryStage);
            if (stage == GeometryStageComplete || stage == GeometryStageCircuitBroken)
            {
                return;
            }

            Interlocked.Increment(ref this.geometryAttemptCount);
            string attemptPath = this.geometryPath + ".attempt";
            string candidatePath = this.geometryPath + ".candidate";
            try
            {
                if (stage == GeometryStageWaitingForReadiness)
                {
                    if (!IsCanonicalGeometryReady())
                    {
                        this.lastGeometryError = "PF127 geometry collections are not ready yet.";
                        return;
                    }

                    Interlocked.Exchange(ref this.geometryStage, GeometryStageReadinessObserved);
                    this.lastGeometryError = "PF127 geometry readiness observed; surface loading is deferred to the next update.";
                    return;
                }

                if (stage == GeometryStageReadinessObserved)
                {
                    if (this.residentSurfacesOnly)
                    {
                        Interlocked.Exchange(ref this.geometryStage, GeometryStageSurfacesLoaded);
                        this.lastGeometryError =
                            "PF127 resident surfaces are ready; canonical serialization is deferred to the next update."
                            + " DevExtras.LoadAllSurfaces is disabled in geometry-only safe mode.";
                        return;
                    }

                    Interlocked.Increment(ref this.loadAllSurfacesCallCount);
                    DevExtras.LoadAllSurfaces();
                    Interlocked.Exchange(ref this.geometryStage, GeometryStageSurfacesLoaded);
                    this.lastGeometryError = "PF127 surfaces loaded once; canonical serialization is deferred to the next update.";
                    return;
                }

                GeometryWriteResult writeResult = WriteCanonicalGeometryAttempt(attemptPath);
                string canonicalSha256 = ComputeFileSha256(attemptPath);
                bool candidateMatches = string.Equals(
                                            this.stableGeometryCandidateSha256,
                                            canonicalSha256,
                                            StringComparison.Ordinal)
                                        && File.Exists(candidatePath)
                                        && string.Equals(
                                            ComputeFileSha256(candidatePath),
                                            canonicalSha256,
                                            StringComparison.Ordinal);
                if (!candidateMatches)
                {
                    PromoteAttemptFile(attemptPath, candidatePath);
                    this.stableGeometryCandidateSha256 = canonicalSha256;
                    this.lastGeometryError = "PF127 geometry snapshot is waiting for one identical retry before promotion.";
                    return;
                }

                PromoteAttemptFile(attemptPath, this.geometryPath);
                DeleteFileNoThrow(candidatePath);
                this.geometryRoomCount = writeResult.RoomCount;
                this.geometryDoorCount = writeResult.DoorCount;
                this.geometryMeshCount = writeResult.MeshCount;
                this.geometryVertexCount = writeResult.VertexCount;
                this.geometryTriangleCount = writeResult.TriangleCount;
                this.lastGeometryError = string.Empty;
                Interlocked.Exchange(ref this.geometryWritten, 1);
                Interlocked.Exchange(ref this.geometryStage, GeometryStageComplete);
                this.stableGeometryCandidateSha256 = null;
                this.Log(
                    "PF127-GEOMETRY",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "written path={0} rooms={1} doors={2} meshes={3} vertices={4} triangles={5}",
                        this.geometryPath,
                        this.geometryRoomCount,
                        this.geometryDoorCount,
                        this.geometryMeshCount,
                        this.geometryVertexCount,
                        this.geometryTriangleCount));
            }
            catch (ResidentSurfaceIncompleteException ex)
            {
                DeleteFileNoThrow(attemptPath);
                DeleteFileNoThrow(candidatePath);
                this.stableGeometryCandidateSha256 = null;
                Interlocked.Increment(ref this.residentSurfaceIncompleteRetryCount);
                this.lastGeometryError = ex.Message;
                if (!string.Equals(this.lastLoggedGeometryError, this.lastGeometryError, StringComparison.Ordinal))
                {
                    this.lastLoggedGeometryError = this.lastGeometryError;
                    this.AppendCaptureError(
                        DateTime.UtcNow,
                        "TryWriteCanonicalGeometry.resident-surface-incomplete-retryable",
                        ex.ToString());
                    this.Log("PF127-GEOMETRY-RESIDENT-SURFACE-INCOMPLETE", this.lastGeometryError);
                }
            }
            catch (Exception ex)
            {
                DeleteFileNoThrow(attemptPath);
                DeleteFileNoThrow(candidatePath);
                Interlocked.Increment(ref this.geometryFailureCount);
                Interlocked.Exchange(ref this.geometryStage, GeometryStageCircuitBroken);
                this.stableGeometryCandidateSha256 = null;
                this.lastGeometryError = ex.ToString();
                this.AppendCaptureError(
                    DateTime.UtcNow,
                    "TryWriteCanonicalGeometry.circuit-breaker",
                    this.lastGeometryError);
                if (!string.Equals(this.lastLoggedGeometryError, this.lastGeometryError, StringComparison.Ordinal))
                {
                    this.lastLoggedGeometryError = this.lastGeometryError;
                    this.Log("PF127-GEOMETRY-CIRCUIT-BROKEN", this.lastGeometryError);
                }
            }
        }

        private static bool IsCanonicalGeometryReady()
        {
            if (!Playfield.IsDungeon || Playfield.ModelIdentity.Instance != ResourcePlayfieldId)
            {
                return false;
            }

            IEnumerable<Zone> liveZones = Playfield.Zones;
            IEnumerable<Room> liveRooms = Playfield.Rooms;
            IEnumerable<Door> liveDoors = Playfield.Doors;
            if (liveZones == null || liveRooms == null || liveDoors == null)
            {
                return false;
            }

            return SnapshotReferenceCollection(liveZones, "PF127 readiness zones").Length > 0
                   && SnapshotReferenceCollection(liveRooms, "PF127 readiness rooms").Length > 0
                   && SnapshotReferenceCollection(liveDoors, "PF127 readiness doors").Length > 0;
        }

        private void SampleLineOfSight(
            DateTime capturedUtc,
            string trigger,
            string runtimePlayfieldId,
            CombatRequestSnapshot combatRequest)
        {
            Interlocked.Increment(ref this.lineOfSightBatchCount);
            bool isCombat = string.Equals(trigger, "combat", StringComparison.Ordinal);
            if (isCombat)
            {
                Interlocked.Increment(ref this.combatLineOfSightBatchCount);
            }
            else
            {
                Interlocked.Increment(ref this.periodicLineOfSightBatchCount);
            }

            long evidenceBatchId = Interlocked.Increment(ref this.nextEvidenceBatchId);
            DoorStateBatchResult doorState = this.CaptureDoorStateBatch(
                capturedUtc,
                trigger,
                evidenceBatchId,
                runtimePlayfieldId);

            if (!this.EnsureLineOfSightWriter())
            {
                return;
            }

            Identity localIdentity;
            string localName;
            Vector3 localPosition;
            try
            {
                LocalPlayer localPlayer = DynelManager.LocalPlayer;
                if (localPlayer == null)
                {
                    throw new InvalidOperationException(
                        "Local player is unavailable during PF127 line-of-sight sampling.");
                }

                localIdentity = localPlayer.Identity;
                localName = localPlayer.Name;
                localPosition = localPlayer.Position;
            }
            catch (Exception ex)
            {
                this.RecordTransientWrapperFailure(
                    capturedUtc,
                    trigger,
                    "local-player",
                    ex,
                    isCombat);
                return;
            }

            List<LineOfSightTargetSnapshot> characterSnapshots;
            bool characterCollectionReadable = CaptureRuntimeSafety.TrySnapshot<SimpleChar, LineOfSightTargetSnapshot>(
                () => DynelManager.Characters,
                character =>
                {
                    Identity identity = character.Identity;
                    string name = character.Name;
                    bool isNpc = character.IsNpc;
                    Vector3 position = character.Position;
                    int monsterData;
                    string monsterDataError;
                    bool monsterDataSuccess = TryReadMonsterData(
                        character,
                        out monsterData,
                        out monsterDataError);
                    bool simpleCharLineOfSight;
                    string simpleCharLineOfSightError;
                    bool simpleCharLineOfSightSuccess = TryProbe(
                        () => character.IsInLineOfSight,
                        out simpleCharLineOfSight,
                        out simpleCharLineOfSightError);
                    return new LineOfSightTargetSnapshot(
                        identity,
                        name,
                        isNpc,
                        position,
                        monsterDataSuccess,
                        monsterData,
                        monsterDataError,
                        simpleCharLineOfSightSuccess,
                        simpleCharLineOfSight,
                        simpleCharLineOfSightError);
                },
                (phase, ex) => this.RecordTransientWrapperFailure(
                    capturedUtc,
                    trigger,
                    phase,
                    ex,
                    isCombat),
                out characterSnapshots);
            if (!characterCollectionReadable)
            {
                return;
            }

            characterSnapshots.RemoveAll(character => character.Identity == localIdentity);
            characterSnapshots.Sort(
                (left, right) =>
                {
                    int typeComparison = ((int)left.Identity.Type).CompareTo((int)right.Identity.Type);
                    return typeComparison != 0
                               ? typeComparison
                               : left.Identity.Instance.CompareTo(right.Identity.Instance);
                });

            bool batchHasUsableCombatTargetPair = false;
            bool batchHasUsableVergilPair = false;
            bool batchHasUnresolvedNpcMonsterData = false;
            bool batchHasOnlyUsableNpcPairs = true;
            foreach (LineOfSightTargetSnapshot target in characterSnapshots)
            {
                LineOfSightTargetBatchResult targetResult = this.WriteLineOfSightRows(
                    capturedUtc,
                    trigger,
                    runtimePlayfieldId,
                    localIdentity,
                    localName,
                    localPosition,
                    target,
                    isCombat,
                    doorState.Revision,
                    evidenceBatchId);
                bool isRequestedCombatTarget = isCombat && target.IsNpc;
                bool isVergil = targetResult.MonsterData.HasValue
                                && targetResult.MonsterData.Value == VergilAeneidMonsterData;
                if (isRequestedCombatTarget && !targetResult.MonsterData.HasValue)
                {
                    batchHasUnresolvedNpcMonsterData = true;
                }

                if (isRequestedCombatTarget && !targetResult.HasUsableVariantPair)
                {
                    batchHasOnlyUsableNpcPairs = false;
                }
                if (isRequestedCombatTarget && isVergil)
                {
                    Interlocked.Exchange(ref this.vergilCombatObserved, 1);
                }

                if (targetResult.HasUsableVariantPair && isRequestedCombatTarget)
                {
                    batchHasUsableCombatTargetPair = true;
                    if (isVergil)
                    {
                        batchHasUsableVergilPair = true;
                    }
                }

                if (targetResult.HasUsableVariantPair
                    && isVergil
                    && target.SimpleCharLineOfSightSuccess
                    && doorState.Usable)
                {
                    string identityKey = DoorIdentityKey(
                        (int)target.Identity.Type,
                        target.Identity.Instance);
                    lock (this.vergilLosCoverageSync)
                    {
                        if (target.SimpleCharLineOfSight)
                        {
                            this.vergilClearIdentityKeys.Add(identityKey);
                        }
                        else
                        {
                            this.vergilBlockedIdentityKeys.Add(identityKey);
                        }
                    }
                }
            }

            bool vergilPairRequired = Volatile.Read(ref this.vergilCombatObserved) != 0;
            if (isCombat
                && doorState.Usable
                && batchHasUsableCombatTargetPair
                && !batchHasUnresolvedNpcMonsterData
                && batchHasOnlyUsableNpcPairs
                && (!vergilPairRequired || batchHasUsableVergilPair))
            {
                Interlocked.Increment(ref this.combatMatchedDoorAndLosBatchCount);
                if (batchHasUsableVergilPair)
                {
                    Interlocked.Increment(ref this.vergilCombatMatchedDoorAndLosBatchCount);
                }

                this.CompleteCombatRequest(combatRequest.Generation);
            }
        }

        private LineOfSightTargetBatchResult WriteLineOfSightRows(
            DateTime capturedUtc,
            string trigger,
            string runtimePlayfieldId,
            Identity localIdentity,
            string localName,
            Vector3 localPosition,
            LineOfSightTargetSnapshot target,
            bool isCombat,
            int doorStateRevision,
            long evidenceBatchId)
        {
            try
            {
                Identity targetIdentity = target.Identity;
                string targetName = target.Name;
                bool targetIsNpc = target.IsNpc;
                Vector3 targetPosition = target.Position;
                int targetMonsterData = target.MonsterData;
                string targetMonsterDataError = target.MonsterDataError;
                bool targetMonsterDataSuccess = target.MonsterDataSuccess;
                bool simpleCharResult = target.SimpleCharLineOfSight;
                string simpleCharError = target.SimpleCharLineOfSightError;
                bool simpleCharSuccess = target.SimpleCharLineOfSightSuccess;
                bool rawUsable = this.WriteLineOfSightVariantRow(
                    capturedUtc,
                    trigger,
                    "raw",
                    0f,
                    runtimePlayfieldId,
                    localIdentity,
                    localName,
                    localPosition,
                    targetIdentity,
                    targetName,
                    targetIsNpc,
                    targetMonsterDataSuccess ? (int?)targetMonsterData : null,
                    targetMonsterDataError,
                    targetPosition,
                    simpleCharSuccess,
                    simpleCharResult,
                    simpleCharError,
                    isCombat,
                    doorStateRevision,
                    evidenceBatchId);
                bool plusOneUsable = this.WriteLineOfSightVariantRow(
                    capturedUtc,
                    trigger,
                    "plus-one-y",
                    1f,
                    runtimePlayfieldId,
                    localIdentity,
                    localName,
                    localPosition + new Vector3(0f, 1f, 0f),
                    targetIdentity,
                    targetName,
                    targetIsNpc,
                    targetMonsterDataSuccess ? (int?)targetMonsterData : null,
                    targetMonsterDataError,
                    targetPosition + new Vector3(0f, 1f, 0f),
                    simpleCharSuccess,
                    simpleCharResult,
                    simpleCharError,
                    isCombat,
                    doorStateRevision,
                    evidenceBatchId);
                return new LineOfSightTargetBatchResult(
                    rawUsable && plusOneUsable,
                    targetMonsterDataSuccess ? (int?)targetMonsterData : null,
                    target.Identity);
            }
            catch (Exception ex)
            {
                this.lastLineOfSightError = ex.GetType().Name + ": " + ex.Message;
                Interlocked.Increment(ref this.lineOfSightProbeErrorCount);
                this.Log("PF127-LOS-ERROR", this.lastLineOfSightError);
                return new LineOfSightTargetBatchResult(false, null, target.Identity);
            }
        }

        private bool WriteLineOfSightVariantRow(
            DateTime capturedUtc,
            string trigger,
            string probeVariant,
            float probeHeight,
            string runtimePlayfieldId,
            Identity localIdentity,
            string localName,
            Vector3 origin,
            Identity targetIdentity,
            string targetName,
            bool targetIsNpc,
            int? targetMonsterData,
            string targetMonsterDataError,
            Vector3 targetPosition,
            bool simpleCharSuccess,
            bool simpleCharResult,
            string simpleCharError,
            bool isCombat,
            int doorStateRevision,
            long evidenceBatchId)
        {
            bool playfieldResult = false;
            bool raycastResult = false;
            Vector3 hitPosition = Vector3.Zero;
            Vector3 hitNormal = Vector3.Zero;
            string playfieldError = string.Empty;
            string raycastError = string.Empty;
            bool endpointsFinite = IsFinite(origin) && IsFinite(targetPosition);
            bool playfieldSuccess = false;
            bool raycastSuccess = false;
            if (!endpointsFinite)
            {
                playfieldError = "Probe endpoints contain a non-finite coordinate.";
                raycastError = playfieldError;
            }
            else
            {
                playfieldSuccess = TryProbe(
                    () => Playfield.LineOfSight(origin, targetPosition, 1, false),
                    out playfieldResult,
                    out playfieldError);
                try
                {
                    raycastResult = Playfield.Raycast(
                        origin,
                        targetPosition,
                        out hitPosition,
                        out hitNormal);
                    raycastSuccess = !raycastResult
                                     || (IsFinite(hitPosition) && IsFinite(hitNormal));
                    if (!raycastSuccess)
                    {
                        raycastError = "Raycast returned a non-finite hit point or normal.";
                    }
                }
                catch (Exception ex)
                {
                    raycastSuccess = false;
                    raycastError = ex.GetType().Name + ": " + ex.Message;
                }
            }

            bool usable = targetMonsterData.HasValue
                          && simpleCharSuccess
                          && playfieldSuccess
                          && raycastSuccess;
            string error = JoinProbeErrors(
                targetMonsterDataError,
                simpleCharError,
                playfieldError,
                raycastError);
            string row = string.Join(
                ",",
                Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                Csv(trigger),
                Csv(probeVariant),
                FloatCsv(probeHeight),
                doorStateRevision.ToString(CultureInfo.InvariantCulture),
                evidenceBatchId.ToString(CultureInfo.InvariantCulture),
                ResourcePlayfieldId.ToString(CultureInfo.InvariantCulture),
                Csv(runtimePlayfieldId),
                Csv(localIdentity.ToString()),
                Csv(localName),
                FloatCsv(origin.X),
                FloatCsv(origin.Y),
                FloatCsv(origin.Z),
                Csv(targetIdentity.ToString()),
                ((int)targetIdentity.Type).ToString(CultureInfo.InvariantCulture),
                targetIdentity.Instance.ToString(CultureInfo.InvariantCulture),
                targetMonsterData.HasValue
                    ? targetMonsterData.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                Csv(targetName),
                targetIsNpc ? "true" : "false",
                FloatCsv(targetPosition.X),
                FloatCsv(targetPosition.Y),
                FloatCsv(targetPosition.Z),
                simpleCharSuccess ? (simpleCharResult ? "true" : "false") : string.Empty,
                playfieldSuccess ? (playfieldResult ? "true" : "false") : string.Empty,
                raycastSuccess ? (raycastResult ? "true" : "false") : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitPosition.X) : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitPosition.Y) : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitPosition.Z) : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitNormal.X) : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitNormal.Y) : string.Empty,
                raycastSuccess && raycastResult ? FloatCsv(hitNormal.Z) : string.Empty,
                usable ? "true" : "false",
                Csv(error));

            try
            {
                this.lineOfSightWriter.WriteLine(row);
            }
            catch (Exception ex)
            {
                this.RecordLineOfSightWriteError(ex);
                this.ResetLineOfSightWriter();
                return false;
            }

            Interlocked.Increment(ref this.lineOfSightRowCount);
            if (isCombat)
            {
                Interlocked.Increment(ref this.combatLineOfSightRowCount);
            }

            if (usable)
            {
                Interlocked.Increment(ref this.usableLineOfSightRowCount);
                if (isCombat)
                {
                    Interlocked.Increment(ref this.combatUsableLineOfSightRowCount);
                    if (string.Equals(probeVariant, "raw", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref this.combatUsableRawVariantRowCount);
                    }
                    else if (string.Equals(probeVariant, "plus-one-y", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref this.combatUsablePlusOneVariantRowCount);
                    }
                }
            }
            else
            {
                this.lastLineOfSightError = error;
                Interlocked.Increment(ref this.lineOfSightProbeErrorCount);
            }

            return usable;
        }

        private DoorStateBatchResult CaptureDoorStateBatch(
            DateTime capturedUtc,
            string trigger,
            long evidenceBatchId,
            string runtimePlayfieldId)
        {
            if (Volatile.Read(ref this.doorStateGenerationCircuitBroken) != 0)
            {
                return new DoorStateBatchResult(false, Volatile.Read(ref this.doorStateRevision));
            }

            Interlocked.Increment(ref this.doorStateBatchAttemptCount);
            try
            {
                if (!this.EnsureDoorStateWriter())
                {
                    return new DoorStateBatchResult(false, Volatile.Read(ref this.doorStateRevision));
                }

                IEnumerable<Door> liveDoors = Playfield.Doors;
                Door[] currentDoors = SnapshotReferenceCollection(liveDoors, "PF127 dynamic doors");
                if (currentDoors.Length == 0)
                {
                    throw new InvalidOperationException("PF127 door collection is empty during dynamic state sampling.");
                }

                List<DynamicDoorSnapshot> doors = new List<DynamicDoorSnapshot>(currentDoors.Length);
                HashSet<string> doorIdentities = new HashSet<string>(StringComparer.Ordinal);
                foreach (Door door in currentDoors)
                {
                    DynamicDoorSnapshot doorSnapshot = CaptureDynamicDoorSnapshot(door);
                    string doorIdentity = DoorIdentityKey(
                        doorSnapshot.IdentityType,
                        doorSnapshot.IdentityInstance);
                    if (!doorIdentities.Add(doorIdentity))
                    {
                        throw new InvalidOperationException(
                            "PF127 dynamic door collection contains duplicate identity " + doorIdentity + ".");
                    }

                    doors.Add(doorSnapshot);
                }

                doors = doors
                    .OrderBy(door => door.Position.X)
                    .ThenBy(door => door.Position.Y)
                    .ThenBy(door => door.Position.Z)
                    .ThenBy(door => door.IdentityType)
                    .ThenBy(door => door.IdentityInstance)
                    .ToList();

                string fingerprint = BuildDoorStateFingerprint(doors);
                if (!string.Equals(this.lastDoorStateFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    this.lastDoorStateFingerprint = fingerprint;
                    Interlocked.Increment(ref this.doorStateRevision);
                }

                int revision = Volatile.Read(ref this.doorStateRevision);
                foreach (DynamicDoorSnapshot door in doors)
                {
                    string row = string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(trigger),
                        revision.ToString(CultureInfo.InvariantCulture),
                        evidenceBatchId.ToString(CultureInfo.InvariantCulture),
                        ResourcePlayfieldId.ToString(CultureInfo.InvariantCulture),
                        Csv(runtimePlayfieldId),
                        door.IdentityType.ToString(CultureInfo.InvariantCulture),
                        door.IdentityInstance.ToString(CultureInfo.InvariantCulture),
                        Csv(door.Identity),
                        Csv(door.Name),
                        FloatCsv(door.Position.X),
                        FloatCsv(door.Position.Y),
                        FloatCsv(door.Position.Z),
                        FloatCsv(door.Rotation.X),
                        FloatCsv(door.Rotation.Y),
                        FloatCsv(door.Rotation.Z),
                        FloatCsv(door.Rotation.W),
                        DoorLinkSchemaVersion.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        Csv(door.Link1Resolution),
                        string.Empty,
                        string.Empty,
                        Csv(door.Link2Resolution),
                        string.Empty,
                        door.IsOpen ? "true" : "false",
                        door.IsLocked ? "true" : "false");
                    this.doorStateWriter.WriteLine(row);
                }

                Interlocked.Add(ref this.doorStateRowCount, doors.Count);
                Interlocked.Increment(ref this.usableDoorStateBatchCount);
                this.lastDoorStateError = string.Empty;
                return new DoorStateBatchResult(true, revision);
            }
            catch (IOException ex)
            {
                this.RecordDoorStateWriteError(ex);
                this.ResetDoorStateWriter();
                return new DoorStateBatchResult(false, Volatile.Read(ref this.doorStateRevision));
            }
            catch (UnauthorizedAccessException ex)
            {
                this.RecordDoorStateWriteError(ex);
                this.ResetDoorStateWriter();
                return new DoorStateBatchResult(false, Volatile.Read(ref this.doorStateRevision));
            }
            catch (Exception ex)
            {
                this.lastDoorStateError = ex.ToString();
                Interlocked.Increment(ref this.doorStateReadErrorCount);
                Interlocked.Exchange(ref this.doorStateGenerationCircuitBroken, 1);
                this.AppendCaptureError(
                    capturedUtc,
                    "CaptureDoorStateBatch.generation-circuit-breaker",
                    this.lastDoorStateError);
                this.Log("PF127-DOOR-STATE-CIRCUIT-BROKEN", this.lastDoorStateError);
                return new DoorStateBatchResult(false, Volatile.Read(ref this.doorStateRevision));
            }
        }

        private static DynamicDoorSnapshot CaptureDynamicDoorSnapshot(Door door)
        {
            try
            {
                Identity identity = door.Identity;
                string name = door.Name;
                Vector3 position = door.Position;
                Quaternion rotation = door.Rotation;
                bool isOpen = door.IsOpen;
                bool isLocked = door.IsLocked;
                if (!IsFinite(position) || !IsFinite(rotation))
                {
                    throw new InvalidOperationException(
                        "PF127 dynamic door "
                        + identity.ToString()
                        + " contains a non-finite position or rotation component.");
                }

                return new DynamicDoorSnapshot
                {
                    IdentityType = (int)identity.Type,
                    IdentityInstance = identity.Instance,
                    Identity = identity.ToString(),
                    Name = name,
                    Position = position,
                    Rotation = rotation,
                    Link1Resolution = DoorLinkUnavailableForClientSafety,
                    Link2Resolution = DoorLinkUnavailableForClientSafety,
                    IsOpen = isOpen,
                    IsLocked = isLocked
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PF127 dynamic door wrapper projection failed.", ex);
            }
        }

        private static string BuildDoorStateFingerprint(IEnumerable<DynamicDoorSnapshot> doors)
        {
            StringBuilder fingerprint = new StringBuilder();
            foreach (DynamicDoorSnapshot door in doors)
            {
                fingerprint.Append(door.IdentityType.ToString(CultureInfo.InvariantCulture));
                fingerprint.Append(':');
                fingerprint.Append(door.IdentityInstance.ToString(CultureInfo.InvariantCulture));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Position.X));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Position.Y));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Position.Z));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Rotation.X));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Rotation.Y));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Rotation.Z));
                fingerprint.Append('|');
                fingerprint.Append(FloatCsv(door.Rotation.W));
                fingerprint.Append('|');
                fingerprint.Append(door.Link1Resolution);
                fingerprint.Append('|');
                fingerprint.Append(door.Link2Resolution);
                fingerprint.Append('|');
                fingerprint.Append(door.IsOpen ? '1' : '0');
                fingerprint.Append('|');
                fingerprint.Append(door.IsLocked ? '1' : '0');
                fingerprint.Append('\n');
            }

            return fingerprint.ToString();
        }

        private bool EnsureDoorStateWriter()
        {
            if (this.doorStateWriter != null)
            {
                return true;
            }

            try
            {
                bool writeHeader = !File.Exists(this.doorStatePath)
                                   || new FileInfo(this.doorStatePath).Length == 0;
                this.doorStateWriter = new StreamWriter(
                    new FileStream(
                        this.doorStatePath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite),
                    new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
                if (writeHeader)
                {
                    this.doorStateWriter.WriteLine(
                        "CapturedUtc,Trigger,Revision,EvidenceBatchId,ResourcePlayfieldId,RuntimePlayfieldId,IdentityType,IdentityInstance,Identity,Name,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,RotationW,DoorLinkSchemaVersion,RawLink1Index,Link1Resolution,Room1Instance,RawLink2Index,Link2Resolution,Room2Instance,IsOpen,IsLocked");
                }

                return true;
            }
            catch (Exception ex)
            {
                this.RecordDoorStateWriteError(ex);
                this.ResetDoorStateWriter();
                return false;
            }
        }

        private void ResetDoorStateWriter()
        {
            StreamWriter writer = this.doorStateWriter;
            this.doorStateWriter = null;
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Dispose();
            }
            catch
            {
            }
        }

        private void RecordDoorStateWriteError(Exception ex)
        {
            this.lastDoorStateError = ex.GetType().Name + ": " + ex.Message;
            Interlocked.Increment(ref this.doorStateWriteErrorCount);
            this.Log("PF127-DOOR-STATE-WRITE-ERROR", this.lastDoorStateError);
        }

        private bool EnsureLineOfSightWriter()
        {
            if (this.lineOfSightWriter != null)
            {
                return true;
            }

            try
            {
                bool writeHeader = !File.Exists(this.lineOfSightPath)
                                   || new FileInfo(this.lineOfSightPath).Length == 0;
                this.lineOfSightWriter = new StreamWriter(
                    new FileStream(
                        this.lineOfSightPath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite),
                    new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
                if (writeHeader)
                {
                    this.lineOfSightWriter.WriteLine(
                        "CapturedUtc,Trigger,ProbeVariant,ProbeHeight,DoorStateRevision,EvidenceBatchId,ResourcePlayfieldId,RuntimePlayfieldId,LocalIdentity,LocalName,OriginX,OriginY,OriginZ,TargetIdentity,TargetIdentityType,TargetIdentityInstance,TargetMonsterData,TargetName,TargetIsNpc,TargetX,TargetY,TargetZ,SimpleCharIsInLineOfSight,PlayfieldLineOfSight,RaycastHit,RaycastHitX,RaycastHitY,RaycastHitZ,RaycastNormalX,RaycastNormalY,RaycastNormalZ,Usable,Error");
                }

                return true;
            }
            catch (Exception ex)
            {
                this.RecordLineOfSightWriteError(ex);
                this.ResetLineOfSightWriter();
                return false;
            }
        }

        private void ResetLineOfSightWriter()
        {
            StreamWriter writer = this.lineOfSightWriter;
            this.lineOfSightWriter = null;
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Dispose();
            }
            catch
            {
            }
        }

        private void RecordLineOfSightWriteError(Exception ex)
        {
            this.lastLineOfSightError = ex.GetType().Name + ": " + ex.Message;
            Interlocked.Increment(ref this.lineOfSightWriteErrorCount);
            this.Log("PF127-LOS-WRITE-ERROR", this.lastLineOfSightError);
        }

        private static bool TryProbe(Func<bool> probe, out bool result, out string error)
        {
            try
            {
                result = probe();
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                result = false;
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryReadMonsterData(SimpleChar target, out int monsterData, out string error)
        {
            try
            {
                monsterData = target.GetStat(Stat.MonsterData);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                monsterData = 0;
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string JoinProbeErrors(
            string monsterDataError,
            string simpleCharError,
            string playfieldError,
            string raycastError)
        {
            List<string> errors = new List<string>();
            if (!string.IsNullOrEmpty(monsterDataError))
            {
                errors.Add("TargetMonsterData=" + monsterDataError);
            }

            if (!string.IsNullOrEmpty(simpleCharError))
            {
                errors.Add("SimpleChar=" + simpleCharError);
            }

            if (!string.IsNullOrEmpty(playfieldError))
            {
                errors.Add("Playfield=" + playfieldError);
            }

            if (!string.IsNullOrEmpty(raycastError))
            {
                errors.Add("Raycast=" + raycastError);
            }

            return string.Join(" | ", errors.ToArray());
        }

        private static T[] SnapshotReferenceCollection<T>(IEnumerable<T> liveCollection, string label)
            where T : class
        {
            if (liveCollection == null)
            {
                throw new InvalidOperationException(label + " collection is unavailable.");
            }

            List<T> snapshot = new List<T>();
            try
            {
                foreach (T item in liveCollection)
                {
                    if (item == null)
                    {
                        throw new InvalidOperationException(label + " collection contains a null wrapper.");
                    }

                    snapshot.Add(item);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(label + " collection snapshot failed.", ex);
            }

            return snapshot.ToArray();
        }

        private static T[] SnapshotValueCollection<T>(IEnumerable<T> liveCollection, string label)
        {
            if (liveCollection == null)
            {
                throw new InvalidOperationException(label + " collection is unavailable.");
            }

            List<T> snapshot = new List<T>();
            try
            {
                foreach (T item in liveCollection)
                {
                    snapshot.Add(item);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(label + " collection snapshot failed.", ex);
            }

            return snapshot.ToArray();
        }

        private static int CaptureZoneInstance(Zone zone)
        {
            try
            {
                return zone.Instance;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PF127 zone wrapper projection failed.", ex);
            }
        }

        private static RoomGeometrySourceSnapshot CaptureRoomGeometrySourceSnapshot(Room room)
        {
            try
            {
                int instance = room.Instance;
                string name = room.Name;
                int floor = room.Floor;
                Vector3 position = room.Position;
                Vector3 center = room.Center;
                Vector3 templatePosition = room.TemplatePos;
                float rotationDegrees = room.Rotation;
                float templateRotationDegrees = room.TemplateRotation;
                float yOffset = room.YOffset;
                Rect worldRect = room.Rect;
                Rect localTileRect = room.LocalRect;
                IntPtr residentSurfacePointer = N3Zone_t.GetSurface(room.Pointer);
                if (residentSurfacePointer == IntPtr.Zero)
                {
                    throw new ResidentSurfaceIncompleteException(instance);
                }

                SurfaceResource surface = room.SurfaceResource;
                if (surface == null)
                {
                    throw new InvalidOperationException(
                        "Room " + instance.ToString(CultureInfo.InvariantCulture) + " has no surface resource.");
                }

                IEnumerable<Mesh> liveMeshes = surface.Meshes;
                Mesh[] meshWrappers = SnapshotReferenceCollection(
                    liveMeshes,
                    "PF127 room " + instance.ToString(CultureInfo.InvariantCulture) + " surface meshes");
                List<MeshGeometrySourceSnapshot> meshes = new List<MeshGeometrySourceSnapshot>(meshWrappers.Length);
                for (int meshIndex = 0; meshIndex < meshWrappers.Length; meshIndex++)
                {
                    meshes.Add(CaptureMeshGeometrySourceSnapshot(meshWrappers[meshIndex], instance, meshIndex));
                }

                return new RoomGeometrySourceSnapshot
                {
                    Instance = instance,
                    Name = name,
                    Floor = floor,
                    Position = position,
                    Center = center,
                    TemplatePosition = templatePosition,
                    RotationDegrees = rotationDegrees,
                    TemplateRotationDegrees = templateRotationDegrees,
                    YOffset = yOffset,
                    WorldRect = worldRect,
                    LocalTileRect = localTileRect,
                    Meshes = meshes
                };
            }
            catch (ResidentSurfaceIncompleteException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PF127 room geometry wrapper projection failed.", ex);
            }
        }

        private static MeshGeometrySourceSnapshot CaptureMeshGeometrySourceSnapshot(
            Mesh mesh,
            int roomInstance,
            int meshIndex)
        {
            try
            {
                IEnumerable<Vector3> liveVertices = mesh.Vertices;
                IEnumerable<int> liveTriangles = mesh.Triangles;
                Matrix4x4 localToWorld = mesh.LocalToWorldMatrix;
                return new MeshGeometrySourceSnapshot
                {
                    Index = meshIndex,
                    Vertices = SnapshotValueCollection(
                        liveVertices,
                        "PF127 room "
                        + roomInstance.ToString(CultureInfo.InvariantCulture)
                        + " mesh vertices"),
                    TriangleIndices = SnapshotValueCollection(
                        liveTriangles,
                        "PF127 room "
                        + roomInstance.ToString(CultureInfo.InvariantCulture)
                        + " mesh triangle indices"),
                    LocalToWorld = localToWorld
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "PF127 room "
                    + roomInstance.ToString(CultureInfo.InvariantCulture)
                    + " mesh "
                    + meshIndex.ToString(CultureInfo.InvariantCulture)
                    + " wrapper projection failed.",
                    ex);
            }
        }

        private static GeometryWriteResult WriteCanonicalGeometryAttempt(string path)
        {
            Identity modelIdentity = Playfield.ModelIdentity;
            if (modelIdentity.Instance != ResourcePlayfieldId)
            {
                throw new InvalidOperationException(
                    "PF127 geometry model identity is not resource playfield 127: "
                    + modelIdentity.ToString()
                    + ".");
            }

            IEnumerable<Zone> liveZones = Playfield.Zones;
            IEnumerable<Room> liveRooms = Playfield.Rooms;
            Zone[] zoneWrappers = SnapshotReferenceCollection(liveZones, "PF127 zones");
            Room[] roomWrappers = SnapshotReferenceCollection(liveRooms, "PF127 rooms");
            List<int> zoneInstanceList = new List<int>(zoneWrappers.Length);
            foreach (Zone zone in zoneWrappers)
            {
                zoneInstanceList.Add(CaptureZoneInstance(zone));
            }

            List<RoomGeometrySourceSnapshot> rooms = new List<RoomGeometrySourceSnapshot>(roomWrappers.Length);
            foreach (Room room in roomWrappers)
            {
                rooms.Add(CaptureRoomGeometrySourceSnapshot(room));
            }

            zoneInstanceList.Sort();
            rooms.Sort((left, right) => left.Instance.CompareTo(right.Instance));
            int[] zoneInstances = zoneInstanceList.ToArray();
            int[] roomInstances = rooms.Select(room => room.Instance).ToArray();
            if (zoneInstances.Length == 0 || roomInstances.Length == 0)
            {
                throw new InvalidOperationException("PF127 zone or room collection is empty.");
            }

            if (zoneInstances.Distinct().Count() != zoneInstances.Length
                || roomInstances.Distinct().Count() != roomInstances.Length)
            {
                throw new InvalidOperationException("PF127 zone or room collection contains duplicate instances.");
            }

            if (!zoneInstances.SequenceEqual(roomInstances))
            {
                throw new InvalidOperationException(
                    "PF127 zone/room instance sets are incomplete or disagree. zones=["
                    + string.Join(",", zoneInstances.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray())
                    + "] rooms=["
                    + string.Join(",", roomInstances.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray())
                    + "].");
            }

            if (rooms.Count == 0)
            {
                throw new InvalidOperationException("PF127 room collection is empty.");
            }

            List<DoorSnapshot> doors = CaptureStaticDoorSnapshots();
            if (doors.Count == 0)
            {
                throw new InvalidOperationException("PF127 door collection is empty.");
            }

            List<RoomSnapshot> roomSnapshots = new List<RoomSnapshot>(rooms.Count);
            GeometryWriteResult result = new GeometryWriteResult
            {
                RoomCount = rooms.Count,
                DoorCount = doors.Count
            };

            using (FileStream stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                65536,
                FileOptions.SequentialScan))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 65536))
            {
                writer.NewLine = "\n";
                writer.Write("{\n");
                writer.Write("  \"schemaVersion\": 1,\n");
                writer.Write("  \"doorLinkSchemaVersion\": 1,\n");
                writer.Write("  \"doorLinkCapturePolicy\": \"unavailable_not_read_for_client_safety\",\n");
                writer.Write("  \"playfieldResource\": 127,\n");
                writer.Write("  \"source\": \"AOSharpLiveCapture/AOSharp.Core room surface collision\",\n");
                writer.Write("  \"capturePlayfieldObject\": 122002,\n");
                writer.Write("  \"modelIdentity\": { \"type\": ");
                writer.Write(((int)modelIdentity.Type).ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"instance\": ");
                writer.Write(modelIdentity.Instance.ToString(CultureInfo.InvariantCulture));
                writer.Write(" },\n");
                writer.Write("  \"coordinateSystem\": { \"space\": \"ao-world\", \"x\": \"horizontal\", \"y\": \"up\", \"z\": \"horizontal\", \"units\": \"client-world-units\" },\n");
                writer.Write("  \"roomInstances\": [");
                for (int roomIndex = 0; roomIndex < roomInstances.Length; roomIndex++)
                {
                    if (roomIndex != 0)
                    {
                        writer.Write(", ");
                    }

                    writer.Write(roomInstances[roomIndex].ToString(CultureInfo.InvariantCulture));
                }

                writer.Write("],\n");
                writer.Write("  \"triangles\": [\n");

                bool firstTriangle = true;
                foreach (RoomGeometrySourceSnapshot room in rooms)
                {
                    RoomSnapshot roomSnapshot = new RoomSnapshot
                    {
                        Instance = room.Instance,
                        Name = room.Name,
                        Floor = room.Floor,
                        Position = room.Position,
                        Center = room.Center,
                        TemplatePosition = room.TemplatePosition,
                        RotationDegrees = room.RotationDegrees,
                        TemplateRotationDegrees = room.TemplateRotationDegrees,
                        YOffset = room.YOffset,
                        WorldRect = room.WorldRect,
                        LocalTileRect = room.LocalTileRect
                    };
                    if (room.Meshes.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Room "
                            + room.Instance.ToString(CultureInfo.InvariantCulture)
                            + " has no surface meshes.");
                    }

                    roomSnapshot.MeshCount = room.Meshes.Count;
                    result.MeshCount += room.Meshes.Count;
                    for (int meshIndex = 0; meshIndex < room.Meshes.Count; meshIndex++)
                    {
                        MeshGeometrySourceSnapshot mesh = room.Meshes[meshIndex];

                        if ((mesh.TriangleIndices.Length % 3) != 0)
                        {
                            throw new InvalidOperationException(
                                "Room surface triangle index count is not divisible by three.");
                        }

                        Matrix4x4 localToWorld = mesh.LocalToWorld;
                        MeshSnapshot meshSnapshot = new MeshSnapshot
                        {
                            Index = meshIndex,
                            SourceVertexCount = mesh.Vertices.Length,
                            SourceTriangleIndexCount = mesh.TriangleIndices.Length
                        };
                        roomSnapshot.Meshes.Add(meshSnapshot);
                        result.VertexCount += mesh.Vertices.Length;
                        roomSnapshot.VertexCount += mesh.Vertices.Length;
                        result.SourceTriangleIndexCount += mesh.TriangleIndices.Length;
                        roomSnapshot.SourceTriangleIndexCount += mesh.TriangleIndices.Length;
                        for (int triangleIndex = 0; triangleIndex < mesh.TriangleIndices.Length / 3; triangleIndex++)
                        {
                            int offset = triangleIndex * 3;
                            int indexA = mesh.TriangleIndices[offset];
                            int indexB = mesh.TriangleIndices[offset + 1];
                            int indexC = mesh.TriangleIndices[offset + 2];
                            ValidateVertexIndex(indexA, mesh.Vertices.Length);
                            ValidateVertexIndex(indexB, mesh.Vertices.Length);
                            ValidateVertexIndex(indexC, mesh.Vertices.Length);
                            Vector3 a = localToWorld.MultiplyPoint3x4(mesh.Vertices[indexA]);
                            Vector3 b = localToWorld.MultiplyPoint3x4(mesh.Vertices[indexB]);
                            Vector3 c = localToWorld.MultiplyPoint3x4(mesh.Vertices[indexC]);
                            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                            {
                                throw new InvalidOperationException(
                                    "PF127 room collision geometry contains a non-finite transformed vertex.");
                            }

                            if (IsDegenerateTriangle(a, b, c))
                            {
                                result.DegenerateTriangleCount++;
                                roomSnapshot.DegenerateTriangleCount++;
                                meshSnapshot.DegenerateTriangleCount++;
                                continue;
                            }

                            if (!firstTriangle)
                            {
                                writer.Write(",\n");
                            }

                            firstTriangle = false;
                            writer.Write("    { \"id\": ");
                            writer.Write(result.TriangleCount.ToString(CultureInfo.InvariantCulture));
                            writer.Write(", \"roomInstance\": ");
                            writer.Write(room.Instance.ToString(CultureInfo.InvariantCulture));
                            writer.Write(", \"meshIndex\": ");
                            writer.Write(meshIndex.ToString(CultureInfo.InvariantCulture));
                            writer.Write(", \"triangleIndex\": ");
                            writer.Write(triangleIndex.ToString(CultureInfo.InvariantCulture));
                            writer.Write(", \"a\": ");
                            WritePoint(writer, a);
                            writer.Write(", \"b\": ");
                            WritePoint(writer, b);
                            writer.Write(", \"c\": ");
                            WritePoint(writer, c);
                            writer.Write(" }");
                            result.TriangleCount++;
                            roomSnapshot.TriangleCount++;
                            meshSnapshot.TriangleCount++;
                        }
                    }

                    if (roomSnapshot.VertexCount == 0
                        || roomSnapshot.SourceTriangleIndexCount == 0
                        || roomSnapshot.TriangleCount == 0)
                    {
                        throw new InvalidOperationException(
                            "Room "
                            + room.Instance.ToString(CultureInfo.InvariantCulture)
                            + " has no source vertices, source triangle indices, or nondegenerate collision triangles.");
                    }

                    roomSnapshots.Add(roomSnapshot);
                }

                if (result.TriangleCount == 0)
                {
                    throw new InvalidOperationException("PF127 room collision surfaces contain no triangles.");
                }

                if (roomSnapshots.Sum(room => room.MeshCount) != result.MeshCount
                    || roomSnapshots.Sum(room => room.VertexCount) != result.VertexCount
                    || roomSnapshots.Sum(room => room.SourceTriangleIndexCount) != result.SourceTriangleIndexCount
                    || roomSnapshots.Sum(room => room.TriangleCount) != result.TriangleCount
                    || roomSnapshots.Sum(room => room.DegenerateTriangleCount) != result.DegenerateTriangleCount)
                {
                    throw new InvalidOperationException(
                        "PF127 per-room geometry counts do not equal the aggregate snapshot counts.");
                }

                writer.Write("\n  ],\n");
                writer.Write("  \"rooms\": [\n");
                for (int index = 0; index < roomSnapshots.Count; index++)
                {
                    WriteRoomJson(writer, roomSnapshots[index]);
                    writer.Write(index + 1 == roomSnapshots.Count ? "\n" : ",\n");
                }

                writer.Write("  ],\n");
                writer.Write("  \"doors\": [\n");
                for (int index = 0; index < doors.Count; index++)
                {
                    WriteDoorJson(writer, doors[index]);
                    writer.Write(index + 1 == doors.Count ? "\n" : ",\n");
                }

                writer.Write("  ],\n");
                writer.Write("  \"counts\": { \"rooms\": ");
                writer.Write(result.RoomCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"doors\": ");
                writer.Write(result.DoorCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"meshes\": ");
                writer.Write(result.MeshCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"vertices\": ");
                writer.Write(result.VertexCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"sourceTriangleIndices\": ");
                writer.Write(result.SourceTriangleIndexCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"triangles\": ");
                writer.Write(result.TriangleCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"degenerateTriangles\": ");
                writer.Write(result.DegenerateTriangleCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(" }\n");
                writer.Write("}\n");
            }

            return result;
        }

        private static List<DoorSnapshot> CaptureStaticDoorSnapshots()
        {
            IEnumerable<Door> liveDoors = Playfield.Doors;
            Door[] currentDoors = SnapshotReferenceCollection(liveDoors, "PF127 static doors");
            List<DoorSnapshot> doors = new List<DoorSnapshot>(currentDoors.Length);
            HashSet<string> doorIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (Door door in currentDoors)
            {
                DoorSnapshot doorSnapshot = CaptureStaticDoorSnapshot(door);

                string doorIdentity = DoorIdentityKey(
                    doorSnapshot.IdentityType,
                    doorSnapshot.IdentityInstance);
                if (!doorIdentities.Add(doorIdentity))
                {
                    throw new InvalidOperationException(
                        "PF127 door collection contains duplicate identity " + doorIdentity + ".");
                }

                doors.Add(doorSnapshot);
            }

            return doors
                .OrderBy(door => door.Position.X)
                .ThenBy(door => door.Position.Y)
                .ThenBy(door => door.Position.Z)
                .ThenBy(door => door.IdentityType)
                .ThenBy(door => door.IdentityInstance)
                .ToList();
        }

        private static DoorSnapshot CaptureStaticDoorSnapshot(Door door)
        {
            try
            {
                Identity identity = door.Identity;
                string name = door.Name;
                Vector3 position = door.Position;
                Quaternion rotation = door.Rotation;
                if (!IsFinite(position) || !IsFinite(rotation))
                {
                    throw new InvalidOperationException(
                        "PF127 door "
                        + identity.ToString()
                        + " contains a non-finite position or rotation component.");
                }

                return new DoorSnapshot
                {
                    IdentityType = (int)identity.Type,
                    IdentityInstance = identity.Instance,
                    Name = name,
                    Position = position,
                    Rotation = rotation,
                    Link1Resolution = DoorLinkUnavailableForClientSafety,
                    Link2Resolution = DoorLinkUnavailableForClientSafety
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PF127 static door wrapper projection failed.", ex);
            }
        }

        private static string DoorIdentityKey(int identityType, int identityInstance)
        {
            return identityType.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identityInstance.ToString(CultureInfo.InvariantCulture);
        }

        private static void WriteRoomJson(TextWriter writer, RoomSnapshot room)
        {
            writer.Write("    { \"instance\": ");
            writer.Write(room.Instance.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"name\": ");
            writer.Write(JsonString(room.Name));
            writer.Write(", \"floor\": ");
            writer.Write(room.Floor.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"position\": ");
            WriteVector(writer, room.Position);
            writer.Write(", \"center\": ");
            WriteVector(writer, room.Center);
            writer.Write(", \"templatePosition\": ");
            WriteVector(writer, room.TemplatePosition);
            writer.Write(", \"rotationDegrees\": ");
            writer.Write(Float(room.RotationDegrees));
            writer.Write(", \"templateRotationDegrees\": ");
            writer.Write(Float(room.TemplateRotationDegrees));
            writer.Write(", \"yOffset\": ");
            writer.Write(Float(room.YOffset));
            writer.Write(", \"worldRectXZ\": ");
            WriteRect(writer, room.WorldRect);
            writer.Write(", \"localTileRectXZ\": ");
            WriteRect(writer, room.LocalTileRect);
            writer.Write(", \"meshCount\": ");
            writer.Write(room.MeshCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"vertexCount\": ");
            writer.Write(room.VertexCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"sourceTriangleIndexCount\": ");
            writer.Write(room.SourceTriangleIndexCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"triangleCount\": ");
            writer.Write(room.TriangleCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"degenerateTriangleCount\": ");
            writer.Write(room.DegenerateTriangleCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"meshes\": [");
            for (int meshIndex = 0; meshIndex < room.Meshes.Count; meshIndex++)
            {
                MeshSnapshot mesh = room.Meshes[meshIndex];
                if (meshIndex != 0)
                {
                    writer.Write(", ");
                }

                writer.Write("{ \"index\": ");
                writer.Write(mesh.Index.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"sourceVertexCount\": ");
                writer.Write(mesh.SourceVertexCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"sourceTriangleIndexCount\": ");
                writer.Write(mesh.SourceTriangleIndexCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"triangleCount\": ");
                writer.Write(mesh.TriangleCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(", \"degenerateTriangleCount\": ");
                writer.Write(mesh.DegenerateTriangleCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(" }");
            }

            writer.Write("]");
            writer.Write(" }");
        }

        private static void WriteDoorJson(TextWriter writer, DoorSnapshot door)
        {
            writer.Write("    { \"identityType\": ");
            writer.Write(door.IdentityType.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"identityInstance\": ");
            writer.Write(door.IdentityInstance.ToString(CultureInfo.InvariantCulture));
            writer.Write(", \"name\": ");
            writer.Write(JsonString(door.Name));
            writer.Write(", \"position\": ");
            WriteVector(writer, door.Position);
            writer.Write(", \"rotation\": ");
            WriteQuaternion(writer, door.Rotation);
            writer.Write(", \"rawLink1Index\": ");
            WriteNullableInteger(writer, door.RawLink1Index);
            writer.Write(", \"link1Resolution\": ");
            writer.Write(JsonString(door.Link1Resolution));
            writer.Write(", \"room1Instance\": ");
            WriteNullableInteger(writer, door.Room1Instance);
            writer.Write(", \"rawLink2Index\": ");
            WriteNullableInteger(writer, door.RawLink2Index);
            writer.Write(", \"link2Resolution\": ");
            writer.Write(JsonString(door.Link2Resolution));
            writer.Write(", \"room2Instance\": ");
            WriteNullableInteger(writer, door.Room2Instance);
            writer.Write(" }");
        }

        private static void WriteVector(TextWriter writer, Vector3 value)
        {
            writer.Write("{ \"x\": ");
            writer.Write(Float(value.X));
            writer.Write(", \"y\": ");
            writer.Write(Float(value.Y));
            writer.Write(", \"z\": ");
            writer.Write(Float(value.Z));
            writer.Write(" }");
        }

        private static void WritePoint(TextWriter writer, Vector3 value)
        {
            WriteVector(writer, value);
        }

        private static void WriteQuaternion(TextWriter writer, Quaternion value)
        {
            writer.Write("{ \"x\": ");
            writer.Write(Float(value.X));
            writer.Write(", \"y\": ");
            writer.Write(Float(value.Y));
            writer.Write(", \"z\": ");
            writer.Write(Float(value.Z));
            writer.Write(", \"w\": ");
            writer.Write(Float(value.W));
            writer.Write(" }");
        }

        private static void WriteRect(TextWriter writer, Rect value)
        {
            writer.Write("{ \"minX\": ");
            writer.Write(Float(value.MinX));
            writer.Write(", \"minZ\": ");
            writer.Write(Float(value.MinY));
            writer.Write(", \"maxX\": ");
            writer.Write(Float(value.MaxX));
            writer.Write(", \"maxZ\": ");
            writer.Write(Float(value.MaxY));
            writer.Write(" }");
        }

        private static void WriteNullableInteger(TextWriter writer, int? value)
        {
            writer.Write(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null");
        }

        private static void ValidateVertexIndex(int index, int vertexCount)
        {
            if (index < 0 || index >= vertexCount)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Room surface triangle index {0} is outside vertex count {1}.",
                        index,
                        vertexCount));
            }
        }

        private static bool IsDegenerateTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            double edge1X = b.X - a.X;
            double edge1Y = b.Y - a.Y;
            double edge1Z = b.Z - a.Z;
            double edge2X = c.X - a.X;
            double edge2Y = c.Y - a.Y;
            double edge2Z = c.Z - a.Z;
            double normalX = (edge1Y * edge2Z) - (edge1Z * edge2Y);
            double normalY = (edge1Z * edge2X) - (edge1X * edge2Z);
            double normalZ = (edge1X * edge2Y) - (edge1Y * edge2X);
            double areaSquared = (normalX * normalX) + (normalY * normalY) + (normalZ * normalZ);
            return areaSquared <= 1.0e-20;
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65536,
                FileOptions.SequentialScan))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(stream);
                StringBuilder result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static void PromoteAttemptFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("PF127 geometry attempt file is missing.", sourcePath);
            }

            if (File.Exists(destinationPath))
            {
                File.Replace(sourcePath, destinationPath, null);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }

        private static void DeleteFileNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException("PF127 geometry contains a non-finite coordinate.");
            }

            return value == 0f ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FloatCsv(float value)
        {
            return value == 0f ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X)
                   && !float.IsInfinity(value.X)
                   && !float.IsNaN(value.Y)
                   && !float.IsInfinity(value.Y)
                   && !float.IsNaN(value.Z)
                   && !float.IsInfinity(value.Z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.X)
                   && !float.IsInfinity(value.X)
                   && !float.IsNaN(value.Y)
                   && !float.IsInfinity(value.Y)
                   && !float.IsNaN(value.Z)
                   && !float.IsInfinity(value.Z)
                   && !float.IsNaN(value.W)
                   && !float.IsInfinity(value.W);
        }

        private static string JsonString(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder escaped = new StringBuilder(value.Length + 2);
            escaped.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\b':
                        escaped.Append("\\b");
                        break;
                    case '\f':
                        escaped.Append("\\f");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            escaped.Append("\\u");
                            escaped.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            escaped.Append(character);
                        }

                        break;
                }
            }

            escaped.Append('"');
            return escaped.ToString();
        }

        private static string Csv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void AppendBooleanProperty(
            StringBuilder json,
            string indent,
            string name,
            bool value,
            bool comma)
        {
            json.Append(indent);
            json.Append(JsonString(name));
            json.Append(": ");
            json.Append(value ? "true" : "false");
            json.Append(comma ? ",\n" : "\n");
        }

        private static void AppendIntegerProperty(
            StringBuilder json,
            string indent,
            string name,
            int value,
            bool comma)
        {
            json.Append(indent);
            json.Append(JsonString(name));
            json.Append(": ");
            json.Append(value.ToString(CultureInfo.InvariantCulture));
            json.Append(comma ? ",\n" : "\n");
        }

        private static void AppendStringProperty(
            StringBuilder json,
            string indent,
            string name,
            string value,
            bool comma)
        {
            json.Append(indent);
            json.Append(JsonString(name));
            json.Append(": ");
            json.Append(JsonString(value ?? string.Empty));
            json.Append(comma ? ",\n" : "\n");
        }

        private void Log(string category, string message)
        {
            if (this.logEvent == null)
            {
                return;
            }

            try
            {
                this.logEvent(category, message);
            }
            catch
            {
            }
        }

        private void RecordTransientWrapperFailure(
            DateTime capturedUtc,
            string trigger,
            string phase,
            Exception ex,
            bool isCombat)
        {
            string detail;
            try
            {
                detail = ex == null
                             ? "Unknown transient AO character-wrapper failure."
                             : ex.ToString();
            }
            catch
            {
                detail = "Transient AO character-wrapper failure; exception formatting also failed.";
            }

            this.lastTransientWrapperError = detail;
            this.lastLineOfSightError = detail;
            Interlocked.Increment(ref this.transientWrapperSkipCount);
            Interlocked.Increment(ref this.lineOfSightProbeErrorCount);
            if (isCombat)
            {
                this.combatRequestGate.MarkRetryRequired();
            }

            this.AppendCaptureError(
                capturedUtc,
                "SampleLineOfSight." + trigger + "." + phase,
                detail);
        }

        private void AppendCaptureError(DateTime capturedUtc, string phase, string detail)
        {
            try
            {
                string record = capturedUtc.ToString("o", CultureInfo.InvariantCulture)
                                + " UTC phase="
                                + (phase ?? string.Empty)
                                + Environment.NewLine
                                + (detail ?? string.Empty)
                                + Environment.NewLine
                                + Environment.NewLine;
                lock (this.captureErrorSync)
                {
                    File.AppendAllText(this.captureErrorPath, record, new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    this.lastCaptureErrorWriteError = ex.GetType().Name + ": " + ex.Message;
                    Interlocked.Increment(ref this.captureErrorWriteErrorCount);
                }
                catch
                {
                }
            }
        }

        private sealed class GeometryWriteResult
        {
            public int RoomCount { get; set; }
            public int DoorCount { get; set; }
            public int MeshCount { get; set; }
            public int VertexCount { get; set; }
            public int SourceTriangleIndexCount { get; set; }
            public int TriangleCount { get; set; }
            public int DegenerateTriangleCount { get; set; }
        }

        private sealed class RoomGeometrySourceSnapshot
        {
            public int Instance { get; set; }
            public string Name { get; set; }
            public int Floor { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Center { get; set; }
            public Vector3 TemplatePosition { get; set; }
            public float RotationDegrees { get; set; }
            public float TemplateRotationDegrees { get; set; }
            public float YOffset { get; set; }
            public Rect WorldRect { get; set; }
            public Rect LocalTileRect { get; set; }
            public List<MeshGeometrySourceSnapshot> Meshes { get; set; }
        }

        private sealed class MeshGeometrySourceSnapshot
        {
            public int Index { get; set; }
            public Vector3[] Vertices { get; set; }
            public int[] TriangleIndices { get; set; }
            public Matrix4x4 LocalToWorld { get; set; }
        }

        private sealed class RoomSnapshot
        {
            public RoomSnapshot()
            {
                this.Meshes = new List<MeshSnapshot>();
            }

            public int Instance { get; set; }
            public string Name { get; set; }
            public int Floor { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Center { get; set; }
            public Vector3 TemplatePosition { get; set; }
            public float RotationDegrees { get; set; }
            public float TemplateRotationDegrees { get; set; }
            public float YOffset { get; set; }
            public Rect WorldRect { get; set; }
            public Rect LocalTileRect { get; set; }
            public int MeshCount { get; set; }
            public int VertexCount { get; set; }
            public int SourceTriangleIndexCount { get; set; }
            public int TriangleCount { get; set; }
            public int DegenerateTriangleCount { get; set; }
            public List<MeshSnapshot> Meshes { get; private set; }
        }

        private sealed class MeshSnapshot
        {
            public int Index { get; set; }
            public int SourceVertexCount { get; set; }
            public int SourceTriangleIndexCount { get; set; }
            public int TriangleCount { get; set; }
            public int DegenerateTriangleCount { get; set; }
        }

        private sealed class DoorSnapshot
        {
            public int IdentityType { get; set; }
            public int IdentityInstance { get; set; }
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public int? RawLink1Index { get; set; }
            public string Link1Resolution { get; set; }
            public int? Room1Instance { get; set; }
            public int? RawLink2Index { get; set; }
            public string Link2Resolution { get; set; }
            public int? Room2Instance { get; set; }
        }

        private sealed class DynamicDoorSnapshot
        {
            public int IdentityType { get; set; }
            public int IdentityInstance { get; set; }
            public string Identity { get; set; }
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public int? RawLink1Index { get; set; }
            public string Link1Resolution { get; set; }
            public int? Room1Instance { get; set; }
            public int? RawLink2Index { get; set; }
            public string Link2Resolution { get; set; }
            public int? Room2Instance { get; set; }
            public bool IsOpen { get; set; }
            public bool IsLocked { get; set; }
        }

        private sealed class ResidentSurfaceIncompleteException : Exception
        {
            public ResidentSurfaceIncompleteException(int roomInstance)
                : base(
                    "PF127 room "
                    + roomInstance.ToString(CultureInfo.InvariantCulture)
                    + " resident surface is incomplete (N3Zone_t.GetSurface returned IntPtr.Zero); retrying without dereferencing Room.SurfaceResource.")
            {
                this.RoomInstance = roomInstance;
            }

            public int RoomInstance { get; private set; }
        }

        private struct DoorStateBatchResult
        {
            public DoorStateBatchResult(bool usable, int revision)
            {
                this.Usable = usable;
                this.Revision = revision;
            }

            public bool Usable { get; private set; }
            public int Revision { get; private set; }
        }

        private struct LineOfSightTargetBatchResult
        {
            public LineOfSightTargetBatchResult(
                bool hasUsableVariantPair,
                int? monsterData,
                Identity identity)
            {
                this.HasUsableVariantPair = hasUsableVariantPair;
                this.MonsterData = monsterData;
                this.Identity = identity;
            }

            public bool HasUsableVariantPair { get; private set; }
            public int? MonsterData { get; private set; }
            public Identity Identity { get; private set; }
        }

        private struct CombatRequestSnapshot
        {
            public static readonly CombatRequestSnapshot Empty = new CombatRequestSnapshot(0);

            public CombatRequestSnapshot(long generation)
            {
                this.Generation = generation;
            }

            public long Generation { get; private set; }
        }

        private sealed class LineOfSightTargetSnapshot
        {
            public LineOfSightTargetSnapshot(
                Identity identity,
                string name,
                bool isNpc,
                Vector3 position,
                bool monsterDataSuccess,
                int monsterData,
                string monsterDataError,
                bool simpleCharLineOfSightSuccess,
                bool simpleCharLineOfSight,
                string simpleCharLineOfSightError)
            {
                this.Identity = identity;
                this.Name = name;
                this.IsNpc = isNpc;
                this.Position = position;
                this.MonsterDataSuccess = monsterDataSuccess;
                this.MonsterData = monsterData;
                this.MonsterDataError = monsterDataError;
                this.SimpleCharLineOfSightSuccess = simpleCharLineOfSightSuccess;
                this.SimpleCharLineOfSight = simpleCharLineOfSight;
                this.SimpleCharLineOfSightError = simpleCharLineOfSightError;
            }

            public Identity Identity { get; private set; }
            public string Name { get; private set; }
            public bool IsNpc { get; private set; }
            public Vector3 Position { get; private set; }
            public bool MonsterDataSuccess { get; private set; }
            public int MonsterData { get; private set; }
            public string MonsterDataError { get; private set; }
            public bool SimpleCharLineOfSightSuccess { get; private set; }
            public bool SimpleCharLineOfSight { get; private set; }
            public string SimpleCharLineOfSightError { get; private set; }
        }
    }
}
