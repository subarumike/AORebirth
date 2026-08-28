using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using AORebirth.CaptureProtocol;
using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Core;

using SmokeLounge.AOtomation.Messaging.Messages;

namespace AOSharpLiveCapture
{
    /// <summary>
    /// Records a fail-closed, epoch-scoped view of NPC identity evidence.
    ///
    /// This component only calls AOSharp/native read accessors. It never uses
    /// Dynel setters, ResourceDatabase mutation, client-memory writes, or packet
    /// send APIs. Pointer values are retained as diagnostics and are never part
    /// of an identity key.
    /// </summary>
    internal sealed class NpcIdentityBridgeCapture : IDisposable
    {
        internal const int SchemaVersion = 1;
        internal const string ArtifactFileName = "npc-identity-bridge-live.jsonl";
        internal const string SummaryFileName = "npc-identity-bridge-summary.json";

        private const int PlayfieldDistrictInfoType = 1000014;
        private const int UnsetStatSentinel = 1234567890;
        private const int MaximumIncompleteRetries = 3;
        private static readonly TimeSpan IncompleteRetryInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PositionRefreshInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ArtifactFlushInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ModelIdentityRetryInterval = TimeSpan.FromSeconds(2);

        // These are the only client stats consumed by the bridge serializer.
        // Enumerating all 626 Stat values for every nearby NPC made the failed
        // Arete capture perform 1,378,452 main-thread GetStat calls.
        private static readonly Stat[] ClientVisibleStats =
        {
            Stat.MonsterData,
            Stat.StaticInstance,
            Stat.CATMesh,
            Stat.HeadMesh,
            Stat.Breed,
            Stat.Sex,
            Stat.Profession,
            Stat.Level,
            Stat.OwnerInstance,
            Stat.VisualFlags
        };

        private static readonly HashSet<string> BridgeEnvelopeMessageNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SimpleCharFullUpdate",
                "Stat",
                "SetStat",
                "CharInPlay",
                "Despawn",
                "CorpseFullUpdate",
                "AppearanceUpdate",
                "SetPos",
                "FollowTarget",
                "StopMovingCmd",
                "N3Teleport"
            };

        private readonly object syncRoot = new object();
        private readonly string captureId;
        private readonly List<ZoneEpochRecord> epochs = new List<ZoneEpochRecord>();
        private readonly List<NpcSnapshotRecord> snapshots = new List<NpcSnapshotRecord>();
        private readonly List<PacketEventRecord> packetEvents = new List<PacketEventRecord>();
        private readonly Dictionary<string, PacketScfuRecord> latestScfuByEpochIdentity =
            new Dictionary<string, PacketScfuRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, PacketStatRecord> latestStatByEpochIdentity =
            new Dictionary<string, PacketStatRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, LineageState> lineageByEpochIdentity =
            new Dictionary<string, LineageState>(StringComparer.Ordinal);
        private readonly Dictionary<string, NpcEvidenceState> evidenceStateByEpochIdentity =
            new Dictionary<string, NpcEvidenceState>(StringComparer.Ordinal);
        private readonly Dictionary<long, int> emittedSnapshotsBySecond =
            new Dictionary<long, int>();

        private ZoneEpochRecord currentEpoch;
        private long lastGlobalOrdinal;
        private long observationSequence;
        private long packetEventSequence;
        private int nextEpochOrdinal;
        private bool transitionInProgress;
        private bool completed;
        private bool disposed;
        private DateTime lastArtifactWriteUtc = DateTime.MinValue;
        private long redundantSnapshotsSuppressed;
        private long retriesTotal;
        private long deferredWorkCount;
        private long droppedWorkCount = 0;
        private long snapshotCallbackCount;
        private long snapshotCallbackElapsedTicks;
        private long snapshotCallbackMaxTicks;
        private int maxSnapshotsPerSecond;
        private long serializedBytesTotal;
        private long maxSerializedBytesPerSecond;
        private long artifactWriteCount;

        internal NpcIdentityBridgeCapture(string sessionDirectory, string captureId)
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory))
            {
                throw new ArgumentException("A bridge capture session directory is required.", "sessionDirectory");
            }

            if (string.IsNullOrWhiteSpace(captureId))
            {
                throw new ArgumentException("A bridge capture id is required.", "captureId");
            }

            Directory.CreateDirectory(sessionDirectory);
            this.captureId = captureId.Trim();
            this.ArtifactPath = Path.Combine(sessionDirectory, ArtifactFileName);
            this.SummaryPath = Path.Combine(sessionDirectory, SummaryFileName);
        }

        internal string ArtifactPath { get; private set; }
        internal string SummaryPath { get; private set; }

        internal void Start(DateTime capturedUtc, long currentGlobalOrdinal)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                if (this.currentEpoch == null)
                {
                    this.BeginPendingEpochNoLock(
                        NormalizeUtc(capturedUtc),
                        currentGlobalOrdinal,
                        "capture-start",
                        null);
                }

                this.EnsureStableEpochNoLock(
                    NormalizeUtc(capturedUtc),
                    currentGlobalOrdinal,
                    "capture-start-stable-sample");
                this.WriteArtifactNoLock();
            }
        }

        internal void OnTeleportStarted(DateTime capturedUtc, long currentGlobalOrdinal)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                this.CloseCurrentEpochNoLock(utc, currentGlobalOrdinal, "teleport-started");
                this.transitionInProgress = true;
                this.WriteArtifactNoLock();
            }
        }

        internal void OnTeleportEnded(DateTime capturedUtc, long currentGlobalOrdinal)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                this.transitionInProgress = false;
                if (this.currentEpoch == null)
                {
                    this.BeginPendingEpochNoLock(utc, currentGlobalOrdinal, "teleport-ended", null);
                }

                this.EnsureStableEpochNoLock(utc, currentGlobalOrdinal, "teleport-ended-stable-sample");
                this.WriteArtifactNoLock();
            }
        }

        internal void OnTeleportFailed(DateTime capturedUtc, long currentGlobalOrdinal)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                this.transitionInProgress = false;
                if (this.currentEpoch == null)
                {
                    this.BeginPendingEpochNoLock(utc, currentGlobalOrdinal, "teleport-failed", null);
                }

                this.EnsureStableEpochNoLock(utc, currentGlobalOrdinal, "teleport-failed-stable-sample");
                this.WriteArtifactNoLock();
            }
        }

        internal void OnPlayfieldInit(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            uint runtimePlayfieldId)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                this.CloseCurrentEpochNoLock(utc, currentGlobalOrdinal, "playfield-init-replaced-epoch");
                this.BeginPendingEpochNoLock(
                    utc,
                    currentGlobalOrdinal,
                    "playfield-init",
                    unchecked((int)runtimePlayfieldId));

                // PlayfieldInit can arrive while Game.IsZoning is still true.
                // The hint is retained, but the epoch cannot become valid until
                // the complete runtime/model/local-player sample is stable.
                this.EnsureStableEpochNoLock(utc, currentGlobalOrdinal, "playfield-init-stable-sample");
                this.WriteArtifactNoLock();
            }
        }

        internal void OnDynelSpawned(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            Dynel dynel)
        {
            this.ObserveNpcLifecycleBoundary(capturedUtc, currentGlobalOrdinal, dynel);
        }

        internal void OnCharInPlay(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            SimpleChar character)
        {
            this.ObserveNpcLifecycleBoundary(capturedUtc, currentGlobalOrdinal, character);
        }

        internal int ObserveNearbyNpcs(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger)
        {
            Stopwatch callback = Stopwatch.StartNew();
            try
            {
                lock (this.syncRoot)
                {
                    this.ThrowIfCompletedOrDisposed();
                    DateTime utc = NormalizeUtc(capturedUtc);
                    this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                    if (!this.EnsureStableEpochNoLock(utc, currentGlobalOrdinal, trigger))
                    {
                        return 0;
                    }

                    SimpleChar[] npcs;
                    try
                    {
                        npcs = (DynelManager.NPCs ?? new SimpleChar[0])
                            .Where(value => value != null)
                            .OrderBy(value => SafeIdentityType(value))
                            .ThenBy(value => SafeIdentityInstance(value))
                            .ToArray();
                    }
                    catch
                    {
                        return 0;
                    }

                    int captured = 0;
                    foreach (SimpleChar npc in npcs)
                    {
                        if (this.ObserveNpcNoLock(utc, currentGlobalOrdinal, trigger, npc))
                        {
                            captured++;
                        }
                    }

                    return captured;
                }
            }
            finally
            {
                callback.Stop();
                this.RecordSnapshotCallback(callback.ElapsedTicks);
            }
        }

        internal bool ObserveNpc(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger,
            Dynel dynel)
        {
            Stopwatch callback = Stopwatch.StartNew();
            try
            {
                lock (this.syncRoot)
                {
                    this.ThrowIfCompletedOrDisposed();
                    DateTime utc = NormalizeUtc(capturedUtc);
                    this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                    if (!this.EnsureStableEpochNoLock(utc, currentGlobalOrdinal, trigger))
                    {
                        return false;
                    }

                    return this.ObserveNpcNoLock(utc, currentGlobalOrdinal, trigger, dynel);
                }
            }
            finally
            {
                callback.Stop();
                this.RecordSnapshotCallback(callback.ElapsedTicks);
            }
        }

        private void ObserveNpcLifecycleBoundary(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            Dynel dynel)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);

                Identity identity;
                SimpleChar character;
                if (!TryResolveNpc(dynel, out identity, out character))
                {
                    return;
                }

                ZoneEpochRecord boundaryEpoch = this.currentEpoch;
                if (boundaryEpoch != null
                    && currentGlobalOrdinal >= boundaryEpoch.StartGlobalOrdinal)
                {
                    string identityKey = EpochIdentityKey(
                        boundaryEpoch.ZoneEpochId,
                        (int)identity.Type,
                        identity.Instance);
                    long boundaryOrdinal = Math.Max(
                        boundaryEpoch.StartGlobalOrdinal,
                        Math.Max(currentGlobalOrdinal, this.lastGlobalOrdinal));
                    LineageState priorLineage;
                    if (this.lineageByEpochIdentity.TryGetValue(identityKey, out priorLineage))
                    {
                        this.BeginLifecycleBoundaryNoLock(
                            boundaryEpoch,
                            (int)identity.Type,
                            identity.Instance,
                            character.Pointer,
                            boundaryOrdinal);
                    }
                    else
                    {
                        // An initial Spawn/CharInPlay is discovery, not proof
                        // that same-epoch packets already received belong to a
                        // prior object. Preserve them for direct identity link.
                        this.MarkEvidenceDirtyNoLock(identityKey, "initial-lifecycle-discovery");
                    }
                }
            }
        }

        internal void ObserveRawPacket(
            DateTime capturedUtc,
            string direction,
            long globalOrdinal,
            int sequence,
            int n3TypeValue,
            int identityType,
            int identityInstance)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                this.ObserveGlobalOrdinalNoLock(globalOrdinal);

                string messageName = Enum.IsDefined(typeof(N3MessageType), n3TypeValue)
                                         ? ((N3MessageType)n3TypeValue).ToString()
                                         : n3TypeValue.ToString(CultureInfo.InvariantCulture);
                if (!BridgeEnvelopeMessageNames.Contains(messageName))
                {
                    return;
                }

                ZoneEpochRecord epoch = this.SelectStableEpochNoLock(globalOrdinal);
                this.packetEvents.Add(
                    new PacketEnvelopeRecord
                    {
                        EventSequence = ++this.packetEventSequence,
                        Epoch = epoch,
                        CapturedUtc = NormalizeUtc(capturedUtc),
                        Direction = direction ?? string.Empty,
                        GlobalOrdinal = globalOrdinal,
                        Sequence = sequence,
                        DecodeError = string.Empty,
                        N3TypeValue = n3TypeValue,
                        N3TypeName = messageName,
                        IdentityType = identityType,
                        IdentityInstance = identityInstance
                    });

                if (epoch != null
                    && messageName == "Despawn"
                    && identityType == (int)IdentityType.SimpleChar)
                {
                    this.BeginLifecycleBoundaryNoLock(
                        epoch,
                        identityType,
                        identityInstance,
                        IntPtr.Zero,
                        globalOrdinal);
                }
            }
        }

        internal void ObserveRawSimpleCharFullUpdate(
            DateTime capturedUtc,
            string direction,
            long globalOrdinal,
            int sequence,
            RawSimpleCharFullUpdate message,
            string decodeError)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(globalOrdinal);

                ZoneEpochRecord stableEpoch = this.SelectStableEpochNoLock(globalOrdinal);
                bool decodeTrusted = message != null
                                     && string.IsNullOrWhiteSpace(decodeError)
                                     && message.DecodeFullyConsumed;
                bool npcPayloadEligible = decodeTrusted
                                          && message.Npc != null
                                          && message.Identity.Type == (int)IdentityType.SimpleChar;
                ZoneEpochRecord epoch = npcPayloadEligible
                                             ? this.SelectEpochForScfuNoLock(message, globalOrdinal)
                                             : null;
                var record = new PacketScfuRecord
                {
                    EventSequence = ++this.packetEventSequence,
                    Epoch = epoch,
                    BridgeLinkEligible = epoch != null,
                    CapturedUtc = utc,
                    Direction = direction ?? string.Empty,
                    GlobalOrdinal = globalOrdinal,
                    Sequence = sequence,
                    DecodeError = decodeError ?? string.Empty,
                    Message = message
                };
                this.packetEvents.Add(record);

                if (!decodeTrusted)
                {
                    this.ClearCachedEvidenceNoLock(stableEpoch, null, null);
                }
                else if (npcPayloadEligible && epoch == null)
                {
                    this.ClearCachedEvidenceNoLock(
                        stableEpoch,
                        message.Identity.Type,
                        message.Identity.Instance);
                }
                else if (epoch != null)
                {
                    string key = EpochIdentityKey(epoch.ZoneEpochId, message.Identity.Type, message.Identity.Instance);
                    this.latestScfuByEpochIdentity[key] = record;
                    this.MarkEvidenceDirtyNoLock(key, "scfu-received");
                }
            }
        }

        internal void ObserveRawStat(
            DateTime capturedUtc,
            string direction,
            long globalOrdinal,
            int sequence,
            RawStatMessage message,
            string decodeError)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfCompletedOrDisposed();
                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(globalOrdinal);

                // Stat packets do not carry a playfield. During a transition
                // there is no safe basis for attaching them to either side.
                ZoneEpochRecord stableEpoch = this.SelectStableEpochNoLock(globalOrdinal);
                bool decodeTrusted = message != null
                                     && string.IsNullOrWhiteSpace(decodeError)
                                     && message.DecodeFullyConsumed;
                bool linkEligible = decodeTrusted
                                    && stableEpoch != null
                                    && message.Identity.Type == (int)IdentityType.SimpleChar;
                ZoneEpochRecord epoch = linkEligible ? stableEpoch : null;
                var record = new PacketStatRecord
                {
                    EventSequence = ++this.packetEventSequence,
                    Epoch = epoch,
                    BridgeLinkEligible = linkEligible,
                    CapturedUtc = utc,
                    Direction = direction ?? string.Empty,
                    GlobalOrdinal = globalOrdinal,
                    Sequence = sequence,
                    DecodeError = decodeError ?? string.Empty,
                    Message = message
                };
                this.packetEvents.Add(record);

                if (!decodeTrusted)
                {
                    this.ClearCachedEvidenceNoLock(stableEpoch, null, null);
                }
                else if (epoch != null)
                {
                    string key = EpochIdentityKey(epoch.ZoneEpochId, message.Identity.Type, message.Identity.Instance);
                    this.latestStatByEpochIdentity[key] = record;
                    this.MarkEvidenceDirtyNoLock(key, "stat-received");
                }
            }
        }

        internal void Flush()
        {
            lock (this.syncRoot)
            {
                this.ThrowIfDisposed();
                this.WriteArtifactNoLock();
            }
        }

        internal void Complete(DateTime capturedUtc, long currentGlobalOrdinal)
        {
            lock (this.syncRoot)
            {
                this.ThrowIfDisposed();
                if (this.completed)
                {
                    return;
                }

                DateTime utc = NormalizeUtc(capturedUtc);
                this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
                this.CloseCurrentEpochNoLock(utc, currentGlobalOrdinal, "capture-complete");
                this.completed = true;
                this.WriteArtifactNoLock();
            }
        }

        public void Dispose()
        {
            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    return;
                }

                if (!this.completed)
                {
                    DateTime utc = DateTime.UtcNow;
                    this.CloseCurrentEpochNoLock(utc, this.lastGlobalOrdinal, "capture-disposed");
                    this.completed = true;
                    this.WriteArtifactNoLock();
                }

                this.disposed = true;
            }
        }

        private bool ObserveNpcNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger,
            Dynel dynel)
        {
            if (this.currentEpoch == null || this.currentEpoch.Validity != "valid" || dynel == null)
            {
                return false;
            }

            Identity identityBefore;
            SimpleChar character;
            if (!TryResolveNpc(dynel, out identityBefore, out character))
            {
                return false;
            }

            string identityKey = EpochIdentityKey(
                this.currentEpoch.ZoneEpochId,
                (int)identityBefore.Type,
                identityBefore.Instance);
            NpcEvidenceState evidenceState = this.GetOrCreateEvidenceStateNoLock(
                identityKey,
                capturedUtc,
                character.Pointer);
            if (!this.ShouldCaptureNpcNoLock(evidenceState, capturedUtc, trigger, character.Pointer))
            {
                this.redundantSnapshotsSuppressed++;
                return false;
            }

            WorldIdentitySample worldBefore = this.currentEpoch.World;
            NpcSnapshotRecord snapshot = this.CaptureNpcSnapshotNoLock(
                capturedUtc,
                currentGlobalOrdinal,
                trigger,
                character,
                identityBefore);

            // Reading every visible stat is a bounded but non-atomic operation.
            // Re-read both the dynel identity and complete world identity after
            // it. If either changed, discard the partially collected row rather
            // than allowing evidence from different epochs/objects to combine.
            WorldIdentitySample worldAfter;
            string worldError;
            Identity identityAfter;
            bool validAfter;
            try
            {
                identityAfter = character.Identity;
                validAfter = character.IsValid;
            }
            catch
            {
                return false;
            }

            if (!TryCaptureWorldIdentity(out worldAfter, out worldError)
                || !worldBefore.SameRuntimeContext(worldAfter)
                || identityBefore != identityAfter
                || !validAfter)
            {
                this.currentEpoch.Validity = "invalid";
                this.currentEpoch.State = "identity-changed-during-snapshot";
                this.currentEpoch.SamplingError = string.IsNullOrWhiteSpace(worldError)
                                                      ? "World or dynel identity changed during the bounded snapshot."
                                                      : worldError;
                this.CloseCurrentEpochNoLock(
                    capturedUtc,
                    currentGlobalOrdinal,
                    "identity-changed-during-snapshot");
                this.transitionInProgress = true;
                return false;
            }

            evidenceState.ClientIdentityCaptured = true;
            evidenceState.ClientSnapshotComplete = string.IsNullOrWhiteSpace(snapshot.PositionError)
                                                   && string.IsNullOrWhiteSpace(snapshot.RotationError)
                                                   && snapshot.CellId.HasValue
                                                   && snapshot.Stats.All(value => string.IsNullOrWhiteSpace(value.Error));
            evidenceState.Dirty = false;
            evidenceState.LastSeenUtc = capturedUtc;
            evidenceState.LastCaptureUtc = capturedUtc;
            evidenceState.NextPositionRefreshUtc = capturedUtc.Add(PositionRefreshInterval);
            if (!evidenceState.ClientSnapshotComplete)
            {
                evidenceState.NextRetryUtc = capturedUtc.Add(IncompleteRetryInterval);
            }

            string fingerprint = SnapshotFingerprint(snapshot);
            if (string.Equals(evidenceState.LastFingerprint, fingerprint, StringComparison.Ordinal))
            {
                this.redundantSnapshotsSuppressed++;
                return false;
            }

            evidenceState.LastFingerprint = fingerprint;
            snapshot.ObservationSequence = ++this.observationSequence;
            this.snapshots.Add(snapshot);
            this.RecordEmittedSnapshotNoLock(snapshot.CapturedUtc);
            return true;
        }

        private static bool TryResolveNpc(
            Dynel dynel,
            out Identity identity,
            out SimpleChar character)
        {
            identity = Identity.None;
            character = null;
            if (dynel == null)
            {
                return false;
            }

            try
            {
                identity = dynel.Identity;
                if (identity.Type != IdentityType.SimpleChar)
                {
                    return false;
                }

                character = dynel as SimpleChar ?? new SimpleChar(dynel);
                return character.IsNpc && !character.IsPet && character.IsValid;
            }
            catch
            {
                identity = Identity.None;
                character = null;
                return false;
            }
        }

        private NpcSnapshotRecord CaptureNpcSnapshotNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger,
            SimpleChar character,
            Identity identity)
        {
            int? cellId = null;
            string cellError = string.Empty;
            try
            {
                IntPtr zonePointer = N3Dynel_t.GetZone(character.Pointer);
                if (zonePointer != IntPtr.Zero)
                {
                    cellId = N3Zone_t.GetInstance(zonePointer);
                }
            }
            catch (Exception exception)
            {
                cellError = exception.GetType().Name + ": " + exception.Message;
            }

            Vector3 position = new Vector3();
            Quaternion rotation = new Quaternion();
            string positionError = string.Empty;
            string rotationError = string.Empty;
            try
            {
                position = character.Position;
            }
            catch (Exception exception)
            {
                positionError = exception.GetType().Name + ": " + exception.Message;
            }

            try
            {
                rotation = character.Rotation;
            }
            catch (Exception exception)
            {
                rotationError = exception.GetType().Name + ": " + exception.Message;
            }

            var stats = new List<ClientStatRecord>(ClientVisibleStats.Length);
            foreach (Stat stat in ClientVisibleStats)
            {
                var record = new ClientStatRecord
                {
                    Name = stat.ToString(),
                    StatId = (int)stat
                };
                try
                {
                    int value = character.GetStat(stat);
                    record.RawValue = value;
                    if (value == UnsetStatSentinel)
                    {
                        record.Provenance = "sentinel/default";
                        record.Observed = false;
                    }
                    else
                    {
                        record.Provenance = "client-state-observed";
                        record.Observed = true;
                        record.Value = value;
                    }
                }
                catch (Exception exception)
                {
                    record.Provenance = "not-observed";
                    record.Observed = false;
                    record.Error = exception.GetType().Name + ": " + exception.Message;
                }

                stats.Add(record);
            }

            string identityKey = EpochIdentityKey(
                this.currentEpoch.ZoneEpochId,
                (int)identity.Type,
                identity.Instance);
            long observationOrdinal = Math.Max(
                this.currentEpoch.StartGlobalOrdinal,
                Math.Max(currentGlobalOrdinal, this.lastGlobalOrdinal));
            LineageState lineage;
            bool priorLineageFound = this.lineageByEpochIdentity.TryGetValue(
                identityKey,
                out lineage);
            bool lineageReplaced = priorLineageFound && lineage.Pointer != character.Pointer;
            if (!priorLineageFound || lineageReplaced)
            {
                int nextLineage = lineage == null ? 1 : lineage.Ordinal + 1;
                // A packet can legitimately arrive before AOSharp exposes the
                // corresponding Dynel. First discovery therefore inherits
                // direct same-epoch packet evidence. Only a proven object
                // replacement advances the evidence floor and clears caches.
                long evidenceFloor = lineageReplaced
                                         ? observationOrdinal
                                         : this.currentEpoch.StartGlobalOrdinal - 1;
                lineage = new LineageState
                {
                    Ordinal = nextLineage,
                    Pointer = character.Pointer,
                    EvidenceAfterGlobalOrdinal = evidenceFloor,
                    LastObservationGlobalOrdinal = observationOrdinal
                };
                this.lineageByEpochIdentity[identityKey] = lineage;
                if (lineageReplaced)
                {
                    this.latestScfuByEpochIdentity.Remove(identityKey);
                    this.latestStatByEpochIdentity.Remove(identityKey);
                }
            }

            PacketScfuRecord scfu;
            PacketStatRecord statPacket;
            this.latestScfuByEpochIdentity.TryGetValue(identityKey, out scfu);
            this.latestStatByEpochIdentity.TryGetValue(identityKey, out statPacket);
            if (scfu != null && scfu.GlobalOrdinal <= lineage.EvidenceAfterGlobalOrdinal)
            {
                scfu = null;
            }
            if (statPacket != null
                && (statPacket.GlobalOrdinal <= lineage.EvidenceAfterGlobalOrdinal
                    || (lineageReplaced && scfu == null)))
            {
                statPacket = null;
            }
            lineage.LastObservationGlobalOrdinal = observationOrdinal;
            return new NpcSnapshotRecord
            {
                Epoch = this.currentEpoch,
                CapturedUtc = capturedUtc,
                ObservationGlobalOrdinal = observationOrdinal,
                EvidenceWindowStartGlobalOrdinal = lineage.EvidenceAfterGlobalOrdinal + 1,
                Trigger = trigger ?? string.Empty,
                IdentityType = (int)identity.Type,
                IdentityInstance = identity.Instance,
                EpochScopedIdentityKey = identityKey,
                LifecycleLineage = identityKey
                                   + "|lineage:"
                                   + lineage.Ordinal.ToString("D4", CultureInfo.InvariantCulture),
                Name = SafeString(() => character.Name),
                Pointer = character.Pointer,
                Position = position,
                PositionError = positionError,
                Rotation = rotation,
                RotationError = rotationError,
                CellId = cellId,
                CellError = cellError,
                Stats = stats,
                Scfu = scfu,
                LatestStatPacket = statPacket
            };
        }

        private bool EnsureStableEpochNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger)
        {
            this.ObserveGlobalOrdinalNoLock(currentGlobalOrdinal);
            if (this.transitionInProgress)
            {
                if (this.currentEpoch != null)
                {
                    this.currentEpoch.Validity = "pending";
                    this.currentEpoch.State = "awaiting-transition-end";
                }

                return false;
            }

            WorldIdentitySample sample;
            string error;
            if (!TryCaptureWorldIdentity(out sample, out error))
            {
                if (this.currentEpoch != null)
                {
                    this.currentEpoch.SamplingError = error;
                    if (this.currentEpoch.Validity != "valid")
                    {
                        this.currentEpoch.Validity = "pending";
                        this.currentEpoch.State = "awaiting-stable-world";
                    }
                }

                return false;
            }

            if (this.currentEpoch == null)
            {
                this.BeginEpochWithWorldNoLock(
                    capturedUtc,
                    currentGlobalOrdinal,
                    trigger + "-new-epoch",
                    sample);
            }
            else if (this.currentEpoch.World == null)
            {
                if (this.currentEpoch.RuntimePlayfieldIdHint.HasValue
                    && this.currentEpoch.RuntimePlayfieldIdHint.Value != sample.RuntimePlayfield.Instance)
                {
                    this.currentEpoch.Validity = "invalid";
                    this.currentEpoch.State = "playfield-init-hint-conflict";
                    this.currentEpoch.SamplingError = string.Format(
                        CultureInfo.InvariantCulture,
                        "PlayfieldInit hint {0} conflicts with stable runtime playfield {1}.",
                        this.currentEpoch.RuntimePlayfieldIdHint.Value,
                        sample.RuntimePlayfield.Instance);
                    this.CloseCurrentEpochNoLock(
                        capturedUtc,
                        currentGlobalOrdinal,
                        "playfield-init-hint-conflict");
                    this.BeginEpochWithWorldNoLock(
                        capturedUtc,
                        currentGlobalOrdinal,
                        trigger + "-hint-conflict-recovery",
                        sample);
                }
                else
                {
                    this.currentEpoch.World = sample;
                    this.currentEpoch.Validity = "valid";
                    this.currentEpoch.State = "stable";
                    this.currentEpoch.SamplingError = string.Empty;
                }
            }
            else if (!this.currentEpoch.World.SameRuntimeContext(sample))
            {
                this.CloseCurrentEpochNoLock(
                    capturedUtc,
                    currentGlobalOrdinal,
                    "runtime-world-identity-changed");
                this.BeginEpochWithWorldNoLock(
                    capturedUtc,
                    currentGlobalOrdinal,
                    trigger + "-world-identity-change",
                    sample);
            }
            else
            {
                this.currentEpoch.Validity = "valid";
                this.currentEpoch.State = "stable";
                this.currentEpoch.SamplingError = string.Empty;
            }

            this.ObservePlayfieldModelNoLock(
                this.currentEpoch,
                sample,
                capturedUtc,
                currentGlobalOrdinal);

            return this.currentEpoch != null
                   && this.currentEpoch.Validity == "valid"
                   && this.lastGlobalOrdinal >= this.currentEpoch.StartGlobalOrdinal;
        }

        private ZoneEpochRecord SelectStableEpochNoLock(long globalOrdinal)
        {
            return this.currentEpoch != null
                   && !this.transitionInProgress
                   && this.currentEpoch.Validity == "valid"
                   && this.currentEpoch.World != null
                   && globalOrdinal >= this.currentEpoch.StartGlobalOrdinal
                       ? this.currentEpoch
                       : null;
        }

        private void BeginLifecycleBoundaryNoLock(
            ZoneEpochRecord epoch,
            int identityType,
            int identityInstance,
            IntPtr pointer,
            long boundaryGlobalOrdinal)
        {
            if (epoch == null)
            {
                return;
            }

            string identityKey = EpochIdentityKey(
                epoch.ZoneEpochId,
                identityType,
                identityInstance);
            LineageState prior;
            this.lineageByEpochIdentity.TryGetValue(identityKey, out prior);
            int nextLineage = prior == null ? 1 : prior.Ordinal + 1;
            long evidenceFloor = Math.Max(
                epoch.StartGlobalOrdinal,
                Math.Max(boundaryGlobalOrdinal, this.lastGlobalOrdinal));
            this.lineageByEpochIdentity[identityKey] = new LineageState
            {
                Ordinal = nextLineage,
                Pointer = pointer,
                EvidenceAfterGlobalOrdinal = evidenceFloor,
                LastObservationGlobalOrdinal = evidenceFloor
            };
            this.latestScfuByEpochIdentity.Remove(identityKey);
            this.latestStatByEpochIdentity.Remove(identityKey);
            this.evidenceStateByEpochIdentity.Remove(identityKey);
        }

        private void ClearCachedEvidenceNoLock(
            ZoneEpochRecord epoch,
            int? identityType,
            int? identityInstance)
        {
            if (epoch == null)
            {
                return;
            }

            if (identityType.HasValue && identityInstance.HasValue)
            {
                string identityKey = EpochIdentityKey(
                    epoch.ZoneEpochId,
                    identityType.Value,
                    identityInstance.Value);
                this.latestScfuByEpochIdentity.Remove(identityKey);
                this.latestStatByEpochIdentity.Remove(identityKey);
                return;
            }

            string prefix = epoch.ZoneEpochId + "|";
            foreach (string key in this.latestScfuByEpochIdentity.Keys
                .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray())
            {
                this.latestScfuByEpochIdentity.Remove(key);
            }
            foreach (string key in this.latestStatByEpochIdentity.Keys
                .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray())
            {
                this.latestStatByEpochIdentity.Remove(key);
            }
        }

        private NpcEvidenceState GetOrCreateEvidenceStateNoLock(
            string identityKey,
            DateTime capturedUtc,
            IntPtr pointer)
        {
            NpcEvidenceState state;
            if (!this.evidenceStateByEpochIdentity.TryGetValue(identityKey, out state))
            {
                state = new NpcEvidenceState
                {
                    FirstSeenUtc = capturedUtc,
                    LastSeenUtc = capturedUtc,
                    Pointer = pointer,
                    Dirty = true,
                    DirtyReason = "first-seen",
                    NextRetryUtc = capturedUtc,
                    NextPositionRefreshUtc = capturedUtc
                };
                this.evidenceStateByEpochIdentity[identityKey] = state;
            }
            else
            {
                if (state.FirstSeenUtc == DateTime.MinValue)
                {
                    state.FirstSeenUtc = capturedUtc;
                }
                state.LastSeenUtc = capturedUtc;
                if (state.Pointer == IntPtr.Zero)
                {
                    state.Pointer = pointer;
                }
            }

            return state;
        }

        private bool ShouldCaptureNpcNoLock(
            NpcEvidenceState state,
            DateTime capturedUtc,
            string trigger,
            IntPtr pointer)
        {
            bool pointerChanged = state.Pointer != IntPtr.Zero && state.Pointer != pointer;
            bool firstCapture = !state.ClientIdentityCaptured;
            bool eventTriggered = state.Dirty
                                  || !string.Equals(
                                      trigger,
                                      "periodic-nearby-scan",
                                      StringComparison.Ordinal);
            bool retryDue = !state.ClientSnapshotComplete
                            && state.ClientIdentityCaptured
                            && state.RetryCount < MaximumIncompleteRetries
                            && capturedUtc >= state.NextRetryUtc;
            bool positionRefreshDue = state.ClientIdentityCaptured
                                      && capturedUtc >= state.NextPositionRefreshUtc;
            if (retryDue)
            {
                state.RetryCount++;
                this.retriesTotal++;
            }
            else if (!state.ClientSnapshotComplete
                     && state.ClientIdentityCaptured
                     && state.RetryCount < MaximumIncompleteRetries)
            {
                this.deferredWorkCount++;
            }

            if (pointerChanged)
            {
                state.Pointer = pointer;
                state.Dirty = true;
                state.DirtyReason = "client-object-pointer-replaced";
            }

            return firstCapture || eventTriggered || retryDue || positionRefreshDue || pointerChanged;
        }

        private void MarkEvidenceDirtyNoLock(string identityKey, string reason)
        {
            NpcEvidenceState state;
            if (!this.evidenceStateByEpochIdentity.TryGetValue(identityKey, out state))
            {
                state = new NpcEvidenceState
                {
                    FirstSeenUtc = DateTime.MinValue,
                    LastSeenUtc = DateTime.MinValue,
                    NextRetryUtc = DateTime.MinValue,
                    NextPositionRefreshUtc = DateTime.MinValue
                };
                this.evidenceStateByEpochIdentity[identityKey] = state;
            }

            state.Dirty = true;
            state.DirtyReason = reason ?? "packet-evidence-received";
        }

        private static string SnapshotFingerprint(NpcSnapshotRecord snapshot)
        {
            var value = new StringBuilder();
            value.Append(snapshot.IdentityType).Append('|').Append(snapshot.IdentityInstance).Append('|');
            value.Append(snapshot.Pointer.ToInt64()).Append('|');
            value.Append(snapshot.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Position.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Rotation.X.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Rotation.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Rotation.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.Rotation.W.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            value.Append(snapshot.CellId.HasValue ? snapshot.CellId.Value.ToString(CultureInfo.InvariantCulture) : "null");
            foreach (ClientStatRecord stat in snapshot.Stats)
            {
                value.Append('|').Append(stat.StatId).Append(':');
                value.Append(stat.RawValue.HasValue ? stat.RawValue.Value.ToString(CultureInfo.InvariantCulture) : "null");
                value.Append(':').Append(stat.Error ?? string.Empty);
            }
            value.Append("|scfu:").Append(snapshot.Scfu == null ? 0 : snapshot.Scfu.GlobalOrdinal);
            value.Append("|stat:").Append(snapshot.LatestStatPacket == null ? 0 : snapshot.LatestStatPacket.GlobalOrdinal);
            return value.ToString();
        }

        private void RecordSnapshotCallback(long elapsedTicks)
        {
            lock (this.syncRoot)
            {
                this.snapshotCallbackCount++;
                this.snapshotCallbackElapsedTicks += elapsedTicks;
                this.snapshotCallbackMaxTicks = Math.Max(this.snapshotCallbackMaxTicks, elapsedTicks);
            }
        }

        private void RecordEmittedSnapshotNoLock(DateTime capturedUtc)
        {
            long second = NormalizeUtc(capturedUtc).Ticks / TimeSpan.TicksPerSecond;
            int count;
            this.emittedSnapshotsBySecond.TryGetValue(second, out count);
            count++;
            this.emittedSnapshotsBySecond[second] = count;
            this.maxSnapshotsPerSecond = Math.Max(this.maxSnapshotsPerSecond, count);
        }

        private void ObservePlayfieldModelNoLock(
            ZoneEpochRecord epoch,
            WorldIdentitySample sample,
            DateTime capturedUtc,
            long globalOrdinal)
        {
            if (epoch == null || sample == null)
            {
                return;
            }

            if (epoch.PlayfieldModelState == "observed-direct-resource")
            {
                if (sample.ModelPlayfield != null
                    && sample.ModelPlayfield.Type == PlayfieldDistrictInfoType
                    && !sample.ModelPlayfield.Equals(epoch.World.ModelPlayfield))
                {
                    epoch.PlayfieldModelState = "conflict";
                    epoch.PlayfieldModelFinalReason = "A different type-1000014 ModelIdentity appeared in the same epoch.";
                }
                return;
            }

            if (capturedUtc < epoch.NextModelIdentityRetryUtc)
            {
                return;
            }

            epoch.ModelIdentityRetryCount++;
            epoch.NextModelIdentityRetryUtc = capturedUtc.Add(ModelIdentityRetryInterval);
            if (sample.ModelPlayfield == null)
            {
                epoch.PlayfieldModelState = sample.ModelSampleState == "changed-during-sample"
                                                ? "late"
                                                : "default";
                epoch.PlayfieldModelFinalReason = "Playfield.ModelIdentity was not stably exposed at this sample.";
                return;
            }

            if (!epoch.ModelIdentityFirstObservedUtc.HasValue)
            {
                epoch.ModelIdentityFirstObservedUtc = capturedUtc;
                epoch.ModelIdentityFirstObservedGlobalOrdinal = globalOrdinal;
            }
            epoch.World.ModelPlayfield = sample.ModelPlayfield;
            if (sample.ModelPlayfield.Type == PlayfieldDistrictInfoType)
            {
                epoch.PlayfieldModelState = "observed-direct-resource";
                epoch.ModelIdentityFirstValidUtc = capturedUtc;
                epoch.ModelIdentityFirstValidGlobalOrdinal = globalOrdinal;
                epoch.PlayfieldModelFinalReason = string.Empty;
            }
            else
            {
                epoch.PlayfieldModelState = "observed-non-resource-type";
                epoch.PlayfieldModelFinalReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield.ModelIdentity exposed type {0}; only type {1} is direct base-playfield proof.",
                    sample.ModelPlayfield.Type,
                    PlayfieldDistrictInfoType);
            }
        }

        private static bool TryCaptureWorldIdentity(
            out WorldIdentitySample sample,
            out string error)
        {
            sample = null;
            error = string.Empty;
            try
            {
                if (Game.IsZoning)
                {
                    error = "Game.IsZoning was true before the world identity sample.";
                    return false;
                }

                LocalPlayer localPlayer = DynelManager.LocalPlayer;
                if (localPlayer == null)
                {
                    error = "DynelManager.LocalPlayer was unavailable.";
                    return false;
                }

                Identity localIdentity1 = localPlayer.Identity;
                Identity runtimeIdentity1 = Playfield.Identity;
                Identity modelIdentity1 = Playfield.ModelIdentity;

                if (Game.IsZoning)
                {
                    error = "Game.IsZoning became true during the world identity sample.";
                    return false;
                }

                LocalPlayer localPlayer2 = DynelManager.LocalPlayer;
                if (localPlayer2 == null)
                {
                    error = "DynelManager.LocalPlayer disappeared during the world identity sample.";
                    return false;
                }

                Identity localIdentity2 = localPlayer2.Identity;
                Identity runtimeIdentity2 = Playfield.Identity;
                Identity modelIdentity2 = Playfield.ModelIdentity;
                if (Game.IsZoning
                    || localIdentity1 != localIdentity2
                    || runtimeIdentity1 != runtimeIdentity2)
                {
                    error = "Runtime or local-player identity changed during the atomic sample.";
                    return false;
                }

                if (runtimeIdentity1 == Identity.None || localIdentity1 == Identity.None)
                {
                    error = "Runtime playfield or local-player identity was None.";
                    return false;
                }

                int? nativeZoneInstance = null;
                try
                {
                    IntPtr nativeZone = N3Dynel_t.GetZone(localPlayer.Pointer);
                    if (nativeZone != IntPtr.Zero)
                    {
                        nativeZoneInstance = N3Zone_t.GetInstance(nativeZone);
                    }
                }
                catch
                {
                    nativeZoneInstance = null;
                }

                sample = new WorldIdentitySample
                {
                    RuntimePlayfield = IdentityValue.FromIdentity(runtimeIdentity1),
                    ModelPlayfield = modelIdentity1 == Identity.None
                                         || modelIdentity1 != modelIdentity2
                                             ? null
                                             : IdentityValue.FromIdentity(modelIdentity1),
                    ModelSampleState = modelIdentity1 == Identity.None
                                           ? "default"
                                           : modelIdentity1 != modelIdentity2
                                                 ? "changed-during-sample"
                                                 : "observed",
                    LocalPlayer = IdentityValue.FromIdentity(localIdentity1),
                    NativeZoneInstance = nativeZoneInstance
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private ZoneEpochRecord SelectEpochForScfuNoLock(
            RawSimpleCharFullUpdate message,
            long globalOrdinal)
        {
            ZoneEpochRecord epoch = this.SelectStableEpochNoLock(globalOrdinal);
            if (epoch == null || message == null || !message.PlayfieldId.HasValue)
            {
                return null;
            }

            int expectedRuntime = epoch.World.RuntimePlayfield.Instance;
            if (message.PlayfieldId.Value != expectedRuntime)
            {
                return null;
            }

            return epoch;
        }

        private void BeginPendingEpochNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger,
            int? runtimePlayfieldIdHint)
        {
            long startGlobalOrdinal = Math.Max(0, currentGlobalOrdinal);
            ZoneEpochRecord priorEpoch = this.epochs.Count == 0
                                             ? null
                                             : this.epochs[this.epochs.Count - 1];
            if (priorEpoch != null && priorEpoch.EndGlobalOrdinal.HasValue)
            {
                startGlobalOrdinal = Math.Max(
                    startGlobalOrdinal,
                    priorEpoch.EndGlobalOrdinal.Value + 1);
            }

            var epoch = new ZoneEpochRecord
            {
                EpochOrdinal = ++this.nextEpochOrdinal,
                ZoneEpochId = this.captureId
                              + "-zone-"
                              + this.nextEpochOrdinal.ToString("D4", CultureInfo.InvariantCulture),
                StartGlobalOrdinal = startGlobalOrdinal,
                StartedUtc = capturedUtc,
                Trigger = trigger ?? string.Empty,
                Validity = "pending",
                State = "awaiting-stable-world",
                RuntimePlayfieldIdHint = runtimePlayfieldIdHint,
                PlayfieldModelState = "default",
                PlayfieldModelFinalReason = "Playfield.ModelIdentity has not yet been sampled.",
                NextModelIdentityRetryUtc = capturedUtc
            };
            this.epochs.Add(epoch);
            this.currentEpoch = epoch;
        }

        private void BeginEpochWithWorldNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string trigger,
            WorldIdentitySample world)
        {
            this.BeginPendingEpochNoLock(
                capturedUtc,
                currentGlobalOrdinal,
                trigger,
                world == null ? (int?)null : world.RuntimePlayfield.Instance);
            this.currentEpoch.World = world;
            this.currentEpoch.Validity = world == null ? "pending" : "valid";
            this.currentEpoch.State = world == null ? "awaiting-stable-world" : "stable";
        }

        private void CloseCurrentEpochNoLock(
            DateTime capturedUtc,
            long currentGlobalOrdinal,
            string state)
        {
            if (this.currentEpoch == null)
            {
                return;
            }

            this.currentEpoch.EndGlobalOrdinal = Math.Max(
                this.currentEpoch.StartGlobalOrdinal,
                Math.Max(currentGlobalOrdinal, this.lastGlobalOrdinal));
            this.currentEpoch.EndedUtc = capturedUtc;
            if (this.currentEpoch.Validity == "pending")
            {
                this.currentEpoch.Validity = "invalid";
            }

            if (this.currentEpoch.PlayfieldModelState == "default"
                || this.currentEpoch.PlayfieldModelState == "late")
            {
                this.currentEpoch.PlayfieldModelState = "not-exposed";
                this.currentEpoch.PlayfieldModelFinalReason =
                    "No stable type-1000014 Playfield.ModelIdentity was exposed before epoch close.";
            }

            this.currentEpoch.State = state ?? "closed";
            this.currentEpoch = null;
        }

        private void ObserveGlobalOrdinalNoLock(long globalOrdinal)
        {
            if (globalOrdinal > this.lastGlobalOrdinal)
            {
                this.lastGlobalOrdinal = globalOrdinal;
            }
        }

        private void WriteArtifactNoLock()
        {
            DateTime writeStartedUtc = DateTime.UtcNow;
            if (!this.completed
                && this.lastArtifactWriteUtc != DateTime.MinValue
                && writeStartedUtc - this.lastArtifactWriteUtc < ArtifactFlushInterval)
            {
                return;
            }

            string temporaryPath = this.ArtifactPath + ".tmp";
            using (var output = new StreamWriter(
                new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false)))
            {
                foreach (ZoneEpochRecord epoch in this.epochs.OrderBy(value => value.EpochOrdinal))
                {
                    output.WriteLine(this.SerializeZoneEpoch(epoch));
                }

                foreach (NpcSnapshotRecord snapshot in this.snapshots
                    .OrderBy(value => value.ObservationSequence))
                {
                    output.WriteLine(this.SerializeNpcSnapshot(snapshot));
                }

                foreach (PacketEventRecord packetEvent in this.packetEvents
                    .OrderBy(value => value.GlobalOrdinal)
                    .ThenBy(value => value.EventSequence))
                {
                    output.WriteLine(packetEvent.Serialize(this.captureId));
                }
            }

            if (File.Exists(this.ArtifactPath))
            {
                File.Replace(temporaryPath, this.ArtifactPath, null);
            }
            else
            {
                File.Move(temporaryPath, this.ArtifactPath);
            }

            long serializedBytes = new FileInfo(this.ArtifactPath).Length;
            double seconds = this.lastArtifactWriteUtc == DateTime.MinValue
                                 ? 1.0
                                 : Math.Max(1.0, (writeStartedUtc - this.lastArtifactWriteUtc).TotalSeconds);
            this.serializedBytesTotal += serializedBytes;
            this.maxSerializedBytesPerSecond = Math.Max(
                this.maxSerializedBytesPerSecond,
                (long)Math.Ceiling(serializedBytes / seconds));
            this.artifactWriteCount++;
            this.lastArtifactWriteUtc = writeStartedUtc;
            if (this.completed)
            {
                this.WriteSummaryNoLock();
            }
        }

        private void WriteSummaryNoLock()
        {
            NpcSnapshotRecord[] latestSnapshots = this.snapshots
                .GroupBy(value => value.EpochScopedIdentityKey, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(value => value.ObservationSequence).First())
                .ToArray();
            int npcsTotal = latestSnapshots.Length;
            int npcsWithScfu = latestSnapshots.Count(value => value.Scfu != null);
            int npcsWithStatPacket = latestSnapshots.Count(value => value.LatestStatPacket != null);
            int rawScfuTotal = this.packetEvents.OfType<PacketScfuRecord>().Count();
            int rawStatTotal = this.packetEvents.OfType<PacketStatRecord>().Count();
            int rawScfuNpcIdentities = this.packetEvents
                .OfType<PacketScfuRecord>()
                .Where(value => value.Message != null)
                .Select(value => value.Message.Identity.Type.ToString(CultureInfo.InvariantCulture)
                                 + ":"
                                 + value.Message.Identity.Instance.ToString(CultureInfo.InvariantCulture))
                .Distinct(StringComparer.Ordinal)
                .Count();
            int rawStatNpcIdentities = this.packetEvents
                .OfType<PacketStatRecord>()
                .Where(value => value.Message != null)
                .Select(value => value.Message.Identity.Type.ToString(CultureInfo.InvariantCulture)
                                 + ":"
                                 + value.Message.Identity.Instance.ToString(CultureInfo.InvariantCulture))
                .Distinct(StringComparer.Ordinal)
                .Count();
            double averageCallbackMs = this.snapshotCallbackCount == 0
                                           ? 0.0
                                           : this.snapshotCallbackElapsedTicks * 1000.0
                                             / Stopwatch.Frequency
                                             / this.snapshotCallbackCount;
            double maximumCallbackMs = this.snapshotCallbackMaxTicks * 1000.0 / Stopwatch.Frequency;

            var json = new StringBuilder();
            json.Append("{");
            AppendJsonNumber(json, "schema_version", SchemaVersion, false);
            AppendJsonString(json, "capture_id", this.captureId, true);
            AppendJsonNumber(json, "npcs_total", npcsTotal, true);
            AppendJsonNumber(json, "npcs_with_client_identity", npcsTotal, true);
            AppendJsonNumber(json, "npcs_with_world_position", latestSnapshots.Count(value => string.IsNullOrWhiteSpace(value.PositionError)), true);
            AppendJsonNumber(json, "npcs_with_orientation", latestSnapshots.Count(value => string.IsNullOrWhiteSpace(value.RotationError)), true);
            AppendJsonNumber(json, "npcs_with_native_cell", latestSnapshots.Count(value => value.CellId.HasValue), true);
            AppendJsonNumber(json, "npcs_with_scfu", npcsWithScfu, true);
            AppendJsonNumber(json, "npcs_with_scfu_position", npcsWithScfu, true);
            AppendJsonNumber(json, "npcs_with_full_appearance", latestSnapshots.Count(value => value.Scfu != null && value.Scfu.Message != null), true);
            AppendJsonNumber(json, "npcs_with_stat_packet", npcsWithStatPacket, true);
            AppendJsonNumber(json, "npcs_with_client_stats", latestSnapshots.Count(value => value.Stats != null && value.Stats.Count == ClientVisibleStats.Length && value.Stats.All(stat => string.IsNullOrWhiteSpace(stat.Error))), true);
            AppendJsonNumber(json, "epochs_total", this.epochs.Count, true);
            AppendJsonNumber(json, "epochs_with_playfield_id", this.epochs.Count(value => value.World != null && value.World.RuntimePlayfield != null), true);
            AppendJsonNumber(json, "epochs_with_playfield_model_id", this.epochs.Count(value => value.World != null && value.World.ModelPlayfield != null), true);
            AppendJsonNumber(json, "epochs_with_direct_base_playfield_id", this.epochs.Count(value => value.World != null && value.World.ModelPlayfield != null && value.World.ModelPlayfield.Type == PlayfieldDistrictInfoType), true);
            AppendJsonNumber(json, "snapshots_total", this.snapshots.Count, true);
            AppendJsonNumber(json, "snapshots_redundant_suppressed", this.redundantSnapshotsSuppressed, true);
            AppendJsonNumber(json, "retries_total", this.retriesTotal, true);
            AppendJsonNumber(json, "raw_scfu_total", rawScfuTotal, true);
            AppendJsonNumber(json, "raw_scfu_npc_identities", rawScfuNpcIdentities, true);
            AppendJsonNumber(json, "scfu_direct_linked", npcsWithScfu, true);
            AppendJsonNumber(json, "scfu_unlinked", Math.Max(0, rawScfuNpcIdentities - npcsWithScfu), true);
            AppendJsonNumber(json, "scfu_decode_failed", this.packetEvents.OfType<PacketScfuRecord>().Count(value => !string.IsNullOrWhiteSpace(value.DecodeError)), true);
            AppendJsonNumber(json, "scfu_outside_epoch", this.packetEvents.OfType<PacketScfuRecord>().Count(value => value.Epoch == null), true);
            AppendJsonNumber(json, "raw_stat_total", rawStatTotal, true);
            AppendJsonNumber(json, "raw_stat_npc_identities", rawStatNpcIdentities, true);
            AppendJsonNumber(json, "stat_direct_linked", npcsWithStatPacket, true);
            AppendJsonNumber(json, "stat_unlinked", Math.Max(0, rawStatNpcIdentities - npcsWithStatPacket), true);
            AppendJsonNumber(json, "stat_decode_failed", this.packetEvents.OfType<PacketStatRecord>().Count(value => !string.IsNullOrWhiteSpace(value.DecodeError)), true);
            AppendJsonNumber(json, "stat_outside_epoch", this.packetEvents.OfType<PacketStatRecord>().Count(value => value.Epoch == null), true);
            AppendJsonFloat(json, "snapshot_callback_average_ms", (float)averageCallbackMs, true);
            AppendJsonFloat(json, "snapshot_callback_max_ms", (float)maximumCallbackMs, true);
            AppendJsonNumber(json, "max_snapshots_per_second", this.maxSnapshotsPerSecond, true);
            AppendJsonNumber(json, "serialized_bytes_total", this.serializedBytesTotal, true);
            AppendJsonNumber(json, "max_serialized_bytes_per_second", this.maxSerializedBytesPerSecond, true);
            AppendJsonNumber(json, "artifact_write_count", this.artifactWriteCount, true);
            AppendJsonNumber(json, "queue_depth_high_water", 0, true);
            AppendJsonNumber(json, "raw_packet_processing_backlog", 0, true);
            AppendJsonNumber(json, "dropped_work_count", this.droppedWorkCount, true);
            AppendJsonNumber(json, "deferred_work_count", this.deferredWorkCount, true);
            AppendJsonString(json, "client_stats_scope", "10 identity-relevant stats; full enumeration skipped for performance", true);
            json.Append("}");
            File.WriteAllText(this.SummaryPath, json.ToString() + Environment.NewLine, new UTF8Encoding(false));
        }

        private string SerializeZoneEpoch(ZoneEpochRecord epoch)
        {
            string publishedValidity = epoch.Validity == "valid"
                                       && !epoch.EndGlobalOrdinal.HasValue
                                           ? "pending"
                                           : epoch.Validity;
            var json = new StringBuilder();
            json.Append("{");
            AppendJsonString(json, "record_type", "zone_epoch", false);
            AppendJsonNumber(json, "schema_version", SchemaVersion, true);
            AppendJsonString(json, "capture_id", this.captureId, true);
            AppendJsonString(json, "zone_epoch_id", epoch.ZoneEpochId, true);
            AppendJsonNumber(json, "epoch_ordinal", epoch.EpochOrdinal, true);
            AppendJsonNumber(json, "start_global_ordinal", epoch.StartGlobalOrdinal, true);
            AppendJsonNullableNumber(json, "end_global_ordinal", epoch.EndGlobalOrdinal, true);
            AppendJsonString(json, "validity", publishedValidity, true);
            AppendJsonString(json, "state", epoch.State, true);
            AppendJsonString(json, "trigger", epoch.Trigger, true);
            AppendJsonString(json, "started_utc", FormatUtc(epoch.StartedUtc), true);
            AppendJsonNullableString(json, "ended_utc", epoch.EndedUtc.HasValue ? FormatUtc(epoch.EndedUtc.Value) : null, true);
            AppendJsonNullableNumber(json, "runtime_playfield_id_hint", epoch.RuntimePlayfieldIdHint, true);
            AppendJsonIdentity(json, "runtime_playfield_identity", epoch.World == null ? null : epoch.World.RuntimePlayfield, true);
            AppendJsonIdentity(json, "model_playfield_identity", epoch.World == null ? null : epoch.World.ModelPlayfield, true);
            AppendJsonString(json, "playfield_model_id_state", epoch.PlayfieldModelState ?? "not-exposed", true);
            AppendJsonNullableNumber(
                json,
                "playfield_model_type",
                epoch.World == null || epoch.World.ModelPlayfield == null
                    ? (int?)null
                    : epoch.World.ModelPlayfield.Type,
                true);
            AppendJsonNullableNumber(
                json,
                "playfield_model_instance",
                epoch.World == null || epoch.World.ModelPlayfield == null
                    ? (int?)null
                    : epoch.World.ModelPlayfield.Instance,
                true);
            AppendJsonNullableNumber(
                json,
                "native_zone_instance",
                epoch.World == null ? (int?)null : epoch.World.NativeZoneInstance,
                true);
            AppendJsonNumber(json, "model_identity_retry_count", epoch.ModelIdentityRetryCount, true);
            AppendJsonNullableString(
                json,
                "model_identity_first_observed_utc",
                epoch.ModelIdentityFirstObservedUtc.HasValue
                    ? FormatUtc(epoch.ModelIdentityFirstObservedUtc.Value)
                    : null,
                true);
            AppendJsonNullableNumber(
                json,
                "model_identity_first_observed_global_ordinal",
                epoch.ModelIdentityFirstObservedGlobalOrdinal,
                true);
            AppendJsonNullableString(
                json,
                "model_identity_first_valid_utc",
                epoch.ModelIdentityFirstValidUtc.HasValue
                    ? FormatUtc(epoch.ModelIdentityFirstValidUtc.Value)
                    : null,
                true);
            AppendJsonNullableNumber(
                json,
                "model_identity_first_valid_global_ordinal",
                epoch.ModelIdentityFirstValidGlobalOrdinal,
                true);
            AppendJsonString(
                json,
                "playfield_model_final_reason",
                epoch.PlayfieldModelFinalReason ?? string.Empty,
                true);
            AppendJsonIdentityWrapper(
                json,
                "runtime_playfield",
                epoch.World == null ? null : epoch.World.RuntimePlayfield,
                epoch.World == null ? "not-observed" : "client-state-observed",
                "Playfield.Identity",
                true);
            AppendJsonIdentityWrapper(
                json,
                "base_playfield_direct",
                epoch.World != null
                    && epoch.World.ModelPlayfield != null
                    && epoch.World.ModelPlayfield.Type == PlayfieldDistrictInfoType
                    ? epoch.World.ModelPlayfield
                    : null,
                epoch.World != null
                    && epoch.World.ModelPlayfield != null
                    && epoch.World.ModelPlayfield.Type == PlayfieldDistrictInfoType
                    ? "client-state-observed"
                    : "not-observed",
                "Playfield.ModelIdentity",
                true);
            AppendJsonNullableNumberWrapper(
                json,
                "district_id_direct",
                null,
                "not-observed",
                "No checked-in direct district-id accessor is exposed.",
                true);
            AppendJsonNullableNumberWrapper(
                json,
                "cell_id_direct",
                null,
                "not-observed",
                "Zone epochs do not identify an NPC-specific native zone/cell.",
                true);
            AppendJsonBoolean(
                json,
                "model_identity_is_playfield_district_info",
                epoch.World != null
                    && epoch.World.ModelPlayfield != null
                    && epoch.World.ModelPlayfield.Type == PlayfieldDistrictInfoType,
                true);
            AppendJsonIdentity(json, "local_player_identity", epoch.World == null ? null : epoch.World.LocalPlayer, true);
            AppendJsonString(json, "sampling_error", epoch.SamplingError ?? string.Empty, true);
            json.Append("}");
            return json.ToString();
        }

        private string SerializeNpcSnapshot(NpcSnapshotRecord snapshot)
        {
            ZoneEpochRecord epoch = snapshot.Epoch;
            WorldIdentitySample world = epoch == null ? null : epoch.World;
            bool validEpoch = epoch != null
                              && epoch.Validity == "valid"
                              && epoch.EndGlobalOrdinal.HasValue;
            bool directModel = validEpoch
                               && world != null
                               && world.ModelPlayfield != null
                               && world.ModelPlayfield.Type == PlayfieldDistrictInfoType;
            ClientStatRecord monsterData = FindStat(snapshot.Stats, Stat.MonsterData);
            ClientStatRecord staticInstance = FindStat(snapshot.Stats, Stat.StaticInstance);
            ClientStatRecord catMesh = FindStat(snapshot.Stats, Stat.CATMesh);
            ClientStatRecord headMesh = FindStat(snapshot.Stats, Stat.HeadMesh);
            ClientStatRecord breed = FindStat(snapshot.Stats, Stat.Breed);
            ClientStatRecord gender = FindStat(snapshot.Stats, Stat.Sex);
            ClientStatRecord profession = FindStat(snapshot.Stats, Stat.Profession);
            ClientStatRecord level = FindStat(snapshot.Stats, Stat.Level);
            ClientStatRecord ownerInstance = FindStat(snapshot.Stats, Stat.OwnerInstance);
            ClientStatRecord visualFlags = FindStat(snapshot.Stats, Stat.VisualFlags);
            var blockers = new List<string>();
            if (!validEpoch)
            {
                blockers.Add("invalid-or-unfinalized-zone-epoch");
            }
            if (!directModel)
            {
                blockers.Add("playfield-model-identity-type-1000014-not-directly-observed");
            }
            if (snapshot.Scfu == null)
            {
                blockers.Add("same-epoch-scfu-not-observed");
            }
            if (!snapshot.CellId.HasValue)
            {
                blockers.Add("native-zone-cell-not-observed");
            }
            blockers.Add("npc-specific-official-placement-identity-not-exposed");

            string bridgeState = !validEpoch
                                     ? "invalid-epoch"
                                     : !directModel
                                           ? "not-exposed"
                                           : snapshot.Scfu == null
                                                 ? "partial"
                                                 : "direct-candidate";

            var json = new StringBuilder();
            json.Append("{");
            AppendJsonString(json, "record_type", "npc_snapshot", false);
            AppendJsonNumber(json, "schema_version", SchemaVersion, true);
            AppendJsonString(json, "capture_id", this.captureId, true);
            AppendJsonString(json, "zone_epoch_id", epoch == null ? string.Empty : epoch.ZoneEpochId, true);
            AppendJsonBoolean(json, "zone_epoch_valid", validEpoch, true);
            AppendJsonNumber(json, "observation_sequence", snapshot.ObservationSequence, true);
            AppendJsonNumber(json, "observation_global_ordinal", snapshot.ObservationGlobalOrdinal, true);
            AppendJsonNumber(
                json,
                "evidence_window_start_global_ordinal",
                snapshot.EvidenceWindowStartGlobalOrdinal,
                true);
            AppendJsonString(json, "timestamp", FormatUtc(snapshot.CapturedUtc), true);
            AppendJsonString(json, "trigger", snapshot.Trigger, true);
            AppendJsonNumber(json, "runtime_identity_type", snapshot.IdentityType, true);
            AppendJsonNumber(json, "runtime_identity_instance", snapshot.IdentityInstance, true);
            AppendJsonIdentityWrapper(
                json,
                "npc_runtime_identity",
                new IdentityValue(snapshot.IdentityType, snapshot.IdentityInstance),
                "client-state-observed",
                "Dynel.Identity",
                true);
            AppendJsonIdentityWrapper(
                json,
                "dynel_identity",
                new IdentityValue(snapshot.IdentityType, snapshot.IdentityInstance),
                "client-state-observed",
                "Dynel.Identity",
                true);
            AppendJsonString(json, "epoch_scoped_identity_key", snapshot.EpochScopedIdentityKey, true);
            AppendJsonString(json, "lifecycle_lineage", snapshot.LifecycleLineage, true);
            AppendJsonIdentityWrapper(
                json,
                "runtime_playfield",
                world == null ? null : world.RuntimePlayfield,
                world == null ? "not-observed" : "client-state-observed",
                "Playfield.Identity",
                true);
            AppendJsonNullableNumber(
                json,
                "runtime_playfield_id",
                world == null ? (int?)null : world.RuntimePlayfield.Instance,
                true);
            AppendJsonIdentityWrapper(
                json,
                "base_playfield_direct",
                directModel ? world.ModelPlayfield : null,
                directModel ? "client-state-observed" : "not-observed",
                "Playfield.ModelIdentity",
                true);
            AppendJsonNullableNumber(
                json,
                "base_playfield_id_if_proven",
                directModel ? (int?)world.ModelPlayfield.Instance : null,
                true);
            AppendJsonNullableNumberWrapper(
                json,
                "district_id_direct",
                null,
                "not-observed",
                "No checked-in direct district-id accessor is exposed.",
                true);
            AppendJsonNullableNumberWrapper(
                json,
                "cell_id_direct",
                snapshot.CellId,
                snapshot.CellId.HasValue ? "client-state-observed" : "not-observed",
                "N3Dynel_t.GetZone -> N3Zone_t.GetInstance",
                true);
            AppendJsonBoolean(json, "cell_to_official_district_relation_proven", false, true);
            AppendJsonNullableNumber(json, "full_model_type_direct", directModel ? (int?)world.ModelPlayfield.Type : null, true);
            AppendJsonNullableNumber(json, "full_model_instance_direct", directModel ? (int?)world.ModelPlayfield.Instance : null, true);
            AppendJsonEvidenceFromPacketOrStat(
                json,
                "monster_data",
                snapshot.Scfu == null ? (uint?)null : snapshot.Scfu.Message.MonsterData,
                "raw SimpleCharFullUpdate.MonsterData",
                monsterData,
                true);
            AppendJsonNullableNumber(json, "template_id_direct", (int?)null, true);
            AppendJsonStatEvidence(json, "template_id_client_state_candidate", staticInstance, true);
            AppendJsonPositions(json, snapshot, true);
            if (snapshot.Scfu == null)
            {
                AppendJsonNullEvidence(json, "packet_scfu_heading", "No same-epoch SCFU was observed.", true);
                AppendJsonNullEvidence(json, "packet_scfu_level", "No same-epoch SCFU was observed.", true);
                AppendJsonNullEvidence(json, "packet_scfu_breed_derived", "No same-epoch SCFU was observed.", true);
                AppendJsonNullEvidence(json, "packet_scfu_gender_derived", "No same-epoch SCFU was observed.", true);
            }
            else
            {
                if ((snapshot.Scfu.Message.Flags & 0x00000200) != 0)
                {
                    AppendJsonRawQuaternionEvidence(
                        json,
                        "packet_scfu_heading",
                        snapshot.Scfu.Message.Heading,
                        "packet-observed",
                        "raw SimpleCharFullUpdate.Heading",
                        true);
                }
                else
                {
                    AppendJsonNullEvidence(
                        json,
                        "packet_scfu_heading",
                        "Same-epoch SCFU did not carry the heading flag.",
                        true);
                }
                AppendJsonObservedNumber(
                    json,
                    "packet_scfu_level",
                    snapshot.Scfu.Message.Level,
                    "packet-observed",
                    "raw SimpleCharFullUpdate.Level",
                    true);
                AppendJsonObservedNumber(
                    json,
                    "packet_scfu_breed_derived",
                    snapshot.Scfu.Message.AppearanceBreed,
                    "derived",
                    "raw SimpleCharFullUpdate.AppearanceValue bitfield",
                    true);
                AppendJsonObservedNumber(
                    json,
                    "packet_scfu_gender_derived",
                    snapshot.Scfu.Message.AppearanceGender,
                    "derived",
                    "raw SimpleCharFullUpdate.AppearanceValue bitfield",
                    true);
            }
            AppendJsonQuaternionEvidence(
                json,
                "heading",
                snapshot.Rotation,
                snapshot.RotationError,
                "Dynel.Rotation",
                "client-state-observed",
                true);
            AppendJsonQuaternionEvidence(
                json,
                "orientation",
                snapshot.Rotation,
                snapshot.RotationError,
                "Dynel.Rotation",
                "client-state-observed",
                true);
            if (snapshot.Scfu != null && snapshot.Scfu.Message.HeadMesh.HasValue)
            {
                AppendJsonObservedNumber(
                    json,
                    "head_mesh",
                    snapshot.Scfu.Message.HeadMesh.Value,
                    "packet-observed",
                    "raw SimpleCharFullUpdate.HeadMesh",
                    true);
            }
            else
            {
                AppendJsonStatEvidence(json, "head_mesh", headMesh, true);
            }

            AppendJsonStringEvidence(
                json,
                "textures",
                snapshot.Scfu == null
                    ? null
                    : RawScfuFormatting.FormatTextures(snapshot.Scfu.Message.Textures),
                snapshot.Scfu == null ? "not-observed" : "packet-observed",
                "raw SimpleCharFullUpdate.Textures",
                true);
            AppendJsonStringEvidence(
                json,
                "meshes",
                snapshot.Scfu == null
                    ? null
                    : RawScfuFormatting.FormatMeshes(snapshot.Scfu.Message.Meshes),
                snapshot.Scfu == null ? "not-observed" : "packet-observed",
                "raw SimpleCharFullUpdate.Meshes",
                true);
            AppendJsonStatEvidence(json, "cat_mesh", catMesh, true);
            AppendJsonStatEvidence(json, "breed", breed, true);
            AppendJsonStatEvidence(json, "gender", gender, true);
            AppendJsonStatEvidence(json, "profession", profession, true);
            AppendJsonStatEvidence(json, "level", level, true);
            AppendJsonOwner(json, snapshot, ownerInstance, true);
            if (snapshot.Scfu != null)
            {
                AppendJsonObservedNumber(
                    json,
                    "visual_flags",
                    snapshot.Scfu.Message.VisualFlags,
                    "packet-observed",
                    "raw SimpleCharFullUpdate.VisualFlags",
                    true);
            }
            else
            {
                AppendJsonStatEvidence(json, "visual_flags", visualFlags, true);
            }

            AppendJsonString(json, "name_corroborating_only", snapshot.Name, true);
            AppendJsonPointerDiagnostic(json, snapshot.Pointer, true);
            AppendJsonPacketProvenance(json, snapshot, true);
            AppendJsonClientStateProvenance(json, snapshot, true);
            AppendJsonClientStats(json, snapshot.Stats, true);
            AppendJsonString(json, "bridge_state", bridgeState, true);
            AppendJsonStringArray(json, "bridge_blockers", blockers, true);
            json.Append("}");
            return json.ToString();
        }

        private static void AppendJsonPositions(
            StringBuilder json,
            NpcSnapshotRecord snapshot,
            bool comma)
        {
            AppendPropertyPrefix(json, "positions", comma);
            json.Append("{");
            AppendJsonVectorEvidence(
                json,
                "world",
                snapshot.Position,
                snapshot.PositionError,
                "Dynel.Position/Vehicle.Position",
                "client-state-observed",
                false);
            AppendJsonNullEvidence(
                json,
                "local",
                "No checked-in Dynel local-position accessor is exposed.",
                true);
            AppendJsonNullEvidence(
                json,
                "district",
                "No checked-in Dynel district-relative position accessor is exposed.",
                true);
            AppendJsonNullEvidence(
                json,
                "cell",
                "A native zone/cell id is exposed, but no cell-relative NPC position accessor is exposed.",
                true);
            if (snapshot.Scfu == null)
            {
                AppendJsonNullEvidence(json, "packet_scfu", "No same-epoch SCFU was observed.", true);
            }
            else
            {
                AppendPropertyPrefix(json, "packet_scfu", true);
                json.Append("{");
                AppendJsonString(json, "state", "observed", false);
                AppendJsonString(json, "provenance", "packet-observed", true);
                AppendJsonString(json, "source", "raw SimpleCharFullUpdate.Position", true);
                AppendJsonRawVector(json, "value", snapshot.Scfu.Message.Position, true);
                json.Append("}");
            }
            json.Append("}");
        }

        private static void AppendJsonPacketProvenance(
            StringBuilder json,
            NpcSnapshotRecord snapshot,
            bool comma)
        {
            AppendPropertyPrefix(json, "packet_provenance", comma);
            json.Append("[");
            bool itemComma = false;
            if (snapshot.Scfu != null)
            {
                AppendPacketReference(json, snapshot.Scfu, "scfu", itemComma);
                itemComma = true;
            }
            if (snapshot.LatestStatPacket != null)
            {
                AppendPacketReference(json, snapshot.LatestStatPacket, "stat", itemComma);
            }
            json.Append("]");
        }

        private static void AppendPacketReference(
            StringBuilder json,
            PacketEventRecord packet,
            string source,
            bool comma)
        {
            if (comma)
            {
                json.Append(",");
            }
            json.Append("{");
            AppendJsonString(json, "kind", source, false);
            AppendJsonString(json, "source", source == "scfu" ? "SimpleCharFullUpdate" : "Stat", true);
            AppendJsonString(json, "direction", packet.Direction, true);
            AppendJsonNumber(json, "sequence", packet.Sequence, true);
            AppendJsonNumber(json, "global_ordinal", packet.GlobalOrdinal, true);
            AppendJsonString(json, "captured_utc", FormatUtc(packet.CapturedUtc), true);
            json.Append("}");
        }

        private static void AppendJsonClientStateProvenance(
            StringBuilder json,
            NpcSnapshotRecord snapshot,
            bool comma)
        {
            AppendPropertyPrefix(json, "client_state_provenance", comma);
            json.Append("{");
            AppendJsonString(json, "runtime_identity", "Dynel.Identity", false);
            AppendJsonString(json, "position", "Dynel.Position/Vehicle.Position", true);
            AppendJsonString(json, "rotation", "Dynel.Rotation/Vehicle.Rotation", true);
            AppendJsonString(json, "cell", "N3Dynel_t.GetZone -> N3Zone_t.GetInstance", true);
            AppendJsonString(json, "stats", "Dynel.GetStat", true);
            AppendJsonString(json, "pointer", "Dynel.Pointer diagnostic only", true);
            AppendJsonString(json, "position_error", snapshot.PositionError, true);
            AppendJsonString(json, "rotation_error", snapshot.RotationError, true);
            AppendJsonString(json, "cell_error", snapshot.CellError, true);
            json.Append("}");
        }

        private static void AppendJsonClientStats(
            StringBuilder json,
            IEnumerable<ClientStatRecord> stats,
            bool comma)
        {
            AppendPropertyPrefix(json, "client_visible_stats", comma);
            json.Append("[");
            bool rowComma = false;
            foreach (ClientStatRecord stat in stats ?? new ClientStatRecord[0])
            {
                if (rowComma)
                {
                    json.Append(",");
                }
                rowComma = true;
                json.Append("{");
                AppendJsonString(json, "stat", stat.Name, false);
                AppendJsonNumber(json, "stat_id", stat.StatId, true);
                AppendJsonNullableNumber(json, "value", stat.Value, true);
                AppendJsonNullableNumber(json, "raw_value", stat.RawValue, true);
                AppendJsonString(json, "provenance", stat.Provenance, true);
                AppendJsonString(json, "error", stat.Error ?? string.Empty, true);
                json.Append("}");
            }
            json.Append("]");
        }

        private static void AppendJsonOwner(
            StringBuilder json,
            NpcSnapshotRecord snapshot,
            ClientStatRecord ownerInstance,
            bool comma)
        {
            AppendPropertyPrefix(json, "owner", comma);
            json.Append("{");
            if (snapshot.Scfu != null && snapshot.Scfu.Message.Owner.HasValue)
            {
                RawScfuIdentity owner = snapshot.Scfu.Message.Owner.Value;
                AppendJsonString(json, "state", "observed", false);
                AppendJsonString(json, "provenance", "packet-observed", true);
                AppendJsonString(json, "source", "raw SimpleCharFullUpdate.Owner", true);
                AppendJsonNumber(json, "type", owner.Type, true);
                AppendJsonNumber(json, "instance", owner.Instance, true);
            }
            else
            {
                AppendJsonString(
                    json,
                    "state",
                    ownerInstance != null && ownerInstance.Observed ? "partial" : "not-observed",
                    false);
                AppendJsonString(
                    json,
                    "provenance",
                    ownerInstance == null ? "not-observed" : ownerInstance.Provenance,
                    true);
                AppendJsonString(json, "source", "Stat.OwnerInstance (owner type unavailable)", true);
                AppendJsonNullableNumber(
                    json,
                    "instance",
                    ownerInstance == null ? (int?)null : ownerInstance.Value,
                    true);
                AppendJsonNullableNumber(json, "type", (int?)null, true);
            }
            json.Append("}");
        }

        private static void AppendJsonEvidenceFromPacketOrStat(
            StringBuilder json,
            string name,
            uint? packetValue,
            string packetSource,
            ClientStatRecord stat,
            bool comma)
        {
            if (packetValue.HasValue)
            {
                AppendPropertyPrefix(json, name, comma);
                json.Append("{");
                AppendJsonString(json, "state", "observed", false);
                AppendJsonString(json, "provenance", "packet-observed", true);
                AppendJsonString(json, "source", packetSource, true);
                AppendJsonUnsignedNumber(json, "value", packetValue.Value, true);
                if (stat != null)
                {
                    AppendJsonNullableNumber(json, "client_state_value", stat.Value, true);
                    AppendJsonString(json, "client_state_provenance", stat.Provenance, true);
                }
                json.Append("}");
                return;
            }

            AppendJsonStatEvidence(json, name, stat, comma);
        }

        private static void AppendJsonStatEvidence(
            StringBuilder json,
            string name,
            ClientStatRecord stat,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            if (stat == null)
            {
                AppendJsonString(json, "state", "not-observed", false);
                AppendJsonString(json, "provenance", "not-observed", true);
                AppendJsonNullableNumber(json, "value", (int?)null, true);
            }
            else
            {
                AppendJsonString(
                    json,
                    "state",
                    stat.Observed ? "observed" : stat.Provenance,
                    false);
                AppendJsonString(json, "provenance", stat.Provenance, true);
                AppendJsonString(json, "source", "Dynel.GetStat(" + stat.Name + ")", true);
                AppendJsonNullableNumber(json, "value", stat.Value, true);
                AppendJsonNullableNumber(json, "raw_value", stat.RawValue, true);
                AppendJsonString(json, "error", stat.Error ?? string.Empty, true);
            }
            json.Append("}");
        }

        private static void AppendJsonObservedNumber(
            StringBuilder json,
            string name,
            long value,
            string provenance,
            string source,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", "observed", false);
            AppendJsonString(json, "provenance", provenance, true);
            AppendJsonString(json, "source", source, true);
            AppendJsonNumber(json, "value", value, true);
            json.Append("}");
        }

        private static void AppendJsonStringEvidence(
            StringBuilder json,
            string name,
            string value,
            string provenance,
            string source,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", value == null ? "not-observed" : "observed", false);
            AppendJsonString(json, "provenance", provenance, true);
            AppendJsonString(json, "source", source, true);
            AppendJsonNullableString(json, "value", value, true);
            json.Append("}");
        }

        private static void AppendJsonVectorEvidence(
            StringBuilder json,
            string name,
            Vector3 value,
            string error,
            string source,
            string provenance,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", string.IsNullOrWhiteSpace(error) ? "observed" : "not-observed", false);
            AppendJsonString(json, "provenance", string.IsNullOrWhiteSpace(error) ? provenance : "not-observed", true);
            AppendJsonString(json, "source", source, true);
            if (string.IsNullOrWhiteSpace(error))
            {
                AppendJsonVector(json, "value", value, true);
            }
            else
            {
                AppendJsonNull(json, "value", true);
            }
            AppendJsonString(json, "error", error ?? string.Empty, true);
            AppendJsonString(
                json,
                "space_semantics",
                "AOSharp exposes this as Position; no checked-in local/district transform is implied.",
                true);
            json.Append("}");
        }

        private static void AppendJsonQuaternionEvidence(
            StringBuilder json,
            string name,
            Quaternion value,
            string error,
            string source,
            string provenance,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", string.IsNullOrWhiteSpace(error) ? "observed" : "not-observed", false);
            AppendJsonString(json, "provenance", string.IsNullOrWhiteSpace(error) ? provenance : "not-observed", true);
            AppendJsonString(json, "source", source, true);
            if (string.IsNullOrWhiteSpace(error))
            {
                AppendJsonQuaternion(json, "value", value, true);
            }
            else
            {
                AppendJsonNull(json, "value", true);
            }
            AppendJsonString(json, "error", error ?? string.Empty, true);
            json.Append("}");
        }

        private static void AppendJsonNullEvidence(
            StringBuilder json,
            string name,
            string reason,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", "not-observed", false);
            AppendJsonString(json, "provenance", "not-observed", true);
            AppendJsonNull(json, "value", true);
            AppendJsonString(json, "reason", reason, true);
            json.Append("}");
        }

        private static void AppendJsonPointerDiagnostic(
            StringBuilder json,
            IntPtr pointer,
            bool comma)
        {
            AppendPropertyPrefix(json, "client_object_pointer_diagnostic", comma);
            json.Append("{");
            AppendJsonString(
                json,
                "value",
                "0x" + pointer.ToInt64().ToString("X", CultureInfo.InvariantCulture),
                false);
            AppendJsonString(json, "provenance", "client-state-observed", true);
            AppendJsonBoolean(json, "authoritative", false, true);
            AppendJsonBoolean(json, "stable_across_runs", false, true);
            json.Append("}");
        }

        private static ClientStatRecord FindStat(
            IEnumerable<ClientStatRecord> stats,
            Stat stat)
        {
            int statId = (int)stat;
            return (stats ?? new ClientStatRecord[0]).FirstOrDefault(value => value.StatId == statId);
        }

        private static string EpochIdentityKey(string epochId, int type, int instance)
        {
            return (epochId ?? string.Empty)
                   + "|type:"
                   + type.ToString(CultureInfo.InvariantCulture)
                   + "|instance:"
                   + instance.ToString(CultureInfo.InvariantCulture);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }
            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string FormatUtc(DateTime value)
        {
            return NormalizeUtc(value).ToString("o", CultureInfo.InvariantCulture);
        }

        private static int SafeIdentityType(Dynel dynel)
        {
            try
            {
                return (int)dynel.Identity.Type;
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static int SafeIdentityInstance(Dynel dynel)
        {
            try
            {
                return dynel.Identity.Instance;
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static string SafeString(Func<string> read)
        {
            try
            {
                return read() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ThrowIfCompletedOrDisposed()
        {
            this.ThrowIfDisposed();
            if (this.completed)
            {
                throw new InvalidOperationException("The NPC identity bridge capture is already complete.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("NpcIdentityBridgeCapture");
            }
        }

        private static void AppendPropertyPrefix(StringBuilder json, string name, bool comma)
        {
            if (comma)
            {
                json.Append(",");
            }
            json.Append(Json(name));
            json.Append(":");
        }

        private static void AppendJsonString(
            StringBuilder json,
            string name,
            string value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(Json(value ?? string.Empty));
        }

        private static void AppendJsonNullableString(
            StringBuilder json,
            string name,
            string value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value == null ? "null" : Json(value));
        }

        private static void AppendJsonNumber(
            StringBuilder json,
            string name,
            long value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonUnsignedNumber(
            StringBuilder json,
            string name,
            uint value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonNullableNumber(
            StringBuilder json,
            string name,
            int? value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null");
        }

        private static void AppendJsonNullableNumber(
            StringBuilder json,
            string name,
            long? value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null");
        }

        private static void AppendJsonBoolean(
            StringBuilder json,
            string name,
            bool value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append(value ? "true" : "false");
        }

        private static void AppendJsonNull(StringBuilder json, string name, bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("null");
        }

        private static void AppendJsonIdentity(
            StringBuilder json,
            string name,
            IdentityValue identity,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            if (identity == null)
            {
                json.Append("null");
                return;
            }
            json.Append("{");
            AppendJsonNumber(json, "type", identity.Type, false);
            AppendJsonNumber(json, "instance", identity.Instance, true);
            json.Append("}");
        }

        private static void AppendJsonIdentityWrapper(
            StringBuilder json,
            string name,
            IdentityValue identity,
            string classification,
            string provenance,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonIdentity(json, "value", identity, false);
            AppendJsonString(json, "classification", classification, true);
            AppendJsonString(json, "provenance", provenance, true);
            json.Append("}");
        }

        private static void AppendJsonNullableNumberWrapper(
            StringBuilder json,
            string name,
            int? value,
            string classification,
            string provenance,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonNullableNumber(json, "value", value, false);
            AppendJsonString(json, "classification", classification, true);
            AppendJsonString(json, "provenance", provenance, true);
            json.Append("}");
        }

        private static void AppendJsonVector(
            StringBuilder json,
            string name,
            Vector3 value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonFloat(json, "x", value.X, false);
            AppendJsonFloat(json, "y", value.Y, true);
            AppendJsonFloat(json, "z", value.Z, true);
            json.Append("}");
        }

        private static void AppendJsonRawVector(
            StringBuilder json,
            string name,
            RawScfuVector3 value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonFloat(json, "x", value.X, false);
            AppendJsonFloat(json, "y", value.Y, true);
            AppendJsonFloat(json, "z", value.Z, true);
            json.Append("}");
        }

        private static void AppendJsonQuaternion(
            StringBuilder json,
            string name,
            Quaternion value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonFloat(json, "x", value.X, false);
            AppendJsonFloat(json, "y", value.Y, true);
            AppendJsonFloat(json, "z", value.Z, true);
            AppendJsonFloat(json, "w", value.W, true);
            json.Append("}");
        }

        private static void AppendJsonRawQuaternion(
            StringBuilder json,
            string name,
            RawScfuQuaternion value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonFloat(json, "x", value.X, false);
            AppendJsonFloat(json, "y", value.Y, true);
            AppendJsonFloat(json, "z", value.Z, true);
            AppendJsonFloat(json, "w", value.W, true);
            json.Append("}");
        }

        private static void AppendJsonRawQuaternionEvidence(
            StringBuilder json,
            string name,
            RawScfuQuaternion value,
            string classification,
            string source,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("{");
            AppendJsonString(json, "state", "observed", false);
            AppendJsonString(json, "provenance", classification, true);
            AppendJsonString(json, "source", source, true);
            AppendJsonRawQuaternion(json, "value", value, true);
            json.Append("}");
        }

        private static void AppendJsonFloat(
            StringBuilder json,
            string name,
            float value,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                json.Append("null");
            }
            else
            {
                json.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static void AppendJsonStringArray(
            StringBuilder json,
            string name,
            IEnumerable<string> values,
            bool comma)
        {
            AppendPropertyPrefix(json, name, comma);
            json.Append("[");
            bool valueComma = false;
            foreach (string value in values ?? new string[0])
            {
                if (valueComma)
                {
                    json.Append(",");
                }
                valueComma = true;
                json.Append(Json(value ?? string.Empty));
            }
            json.Append("]");
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

        private sealed class IdentityValue : IEquatable<IdentityValue>
        {
            internal IdentityValue(int type, int instance)
            {
                this.Type = type;
                this.Instance = instance;
            }

            internal int Type { get; private set; }
            internal int Instance { get; private set; }

            internal static IdentityValue FromIdentity(Identity identity)
            {
                return new IdentityValue((int)identity.Type, identity.Instance);
            }

            public bool Equals(IdentityValue other)
            {
                return other != null && this.Type == other.Type && this.Instance == other.Instance;
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as IdentityValue);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.Type * 397) ^ this.Instance;
                }
            }
        }

        private sealed class WorldIdentitySample : IEquatable<WorldIdentitySample>
        {
            internal IdentityValue RuntimePlayfield { get; set; }
            internal IdentityValue ModelPlayfield { get; set; }
            internal IdentityValue LocalPlayer { get; set; }
            internal int? NativeZoneInstance { get; set; }
            internal string ModelSampleState { get; set; }

            internal bool SameRuntimeContext(WorldIdentitySample other)
            {
                return other != null
                       && object.Equals(this.RuntimePlayfield, other.RuntimePlayfield)
                       && object.Equals(this.LocalPlayer, other.LocalPlayer);
            }

            public bool Equals(WorldIdentitySample other)
            {
                return this.SameRuntimeContext(other)
                       && object.Equals(this.ModelPlayfield, other.ModelPlayfield)
                       && this.NativeZoneInstance == other.NativeZoneInstance;
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as WorldIdentitySample);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = this.RuntimePlayfield == null ? 0 : this.RuntimePlayfield.GetHashCode();
                    hash = (hash * 397) ^ (this.ModelPlayfield == null ? 0 : this.ModelPlayfield.GetHashCode());
                    hash = (hash * 397) ^ (this.LocalPlayer == null ? 0 : this.LocalPlayer.GetHashCode());
                    hash = (hash * 397) ^ (this.NativeZoneInstance.HasValue ? this.NativeZoneInstance.Value : 0);
                    return hash;
                }
            }
        }

        private sealed class ZoneEpochRecord
        {
            internal int EpochOrdinal { get; set; }
            internal string ZoneEpochId { get; set; }
            internal long StartGlobalOrdinal { get; set; }
            internal long? EndGlobalOrdinal { get; set; }
            internal DateTime StartedUtc { get; set; }
            internal DateTime? EndedUtc { get; set; }
            internal string Trigger { get; set; }
            internal string Validity { get; set; }
            internal string State { get; set; }
            internal int? RuntimePlayfieldIdHint { get; set; }
            internal WorldIdentitySample World { get; set; }
            internal string SamplingError { get; set; }
            internal string PlayfieldModelState { get; set; }
            internal string PlayfieldModelFinalReason { get; set; }
            internal int ModelIdentityRetryCount { get; set; }
            internal DateTime NextModelIdentityRetryUtc { get; set; }
            internal DateTime? ModelIdentityFirstObservedUtc { get; set; }
            internal long? ModelIdentityFirstObservedGlobalOrdinal { get; set; }
            internal DateTime? ModelIdentityFirstValidUtc { get; set; }
            internal long? ModelIdentityFirstValidGlobalOrdinal { get; set; }
        }

        private sealed class ClientStatRecord
        {
            internal string Name { get; set; }
            internal int StatId { get; set; }
            internal int? Value { get; set; }
            internal int? RawValue { get; set; }
            internal bool Observed { get; set; }
            internal string Provenance { get; set; }
            internal string Error { get; set; }
        }

        private sealed class LineageState
        {
            internal int Ordinal { get; set; }
            internal IntPtr Pointer { get; set; }
            internal long EvidenceAfterGlobalOrdinal { get; set; }
            internal long LastObservationGlobalOrdinal { get; set; }
        }

        private sealed class NpcEvidenceState
        {
            internal DateTime FirstSeenUtc { get; set; }
            internal DateTime LastSeenUtc { get; set; }
            internal DateTime LastCaptureUtc { get; set; }
            internal DateTime NextRetryUtc { get; set; }
            internal DateTime NextPositionRefreshUtc { get; set; }
            internal IntPtr Pointer { get; set; }
            internal bool ClientIdentityCaptured { get; set; }
            internal bool ClientSnapshotComplete { get; set; }
            internal bool Dirty { get; set; }
            internal string DirtyReason { get; set; }
            internal int RetryCount { get; set; }
            internal string LastFingerprint { get; set; }
        }

        private sealed class NpcSnapshotRecord
        {
            internal ZoneEpochRecord Epoch { get; set; }
            internal DateTime CapturedUtc { get; set; }
            internal long ObservationSequence { get; set; }
            internal long ObservationGlobalOrdinal { get; set; }
            internal long EvidenceWindowStartGlobalOrdinal { get; set; }
            internal string Trigger { get; set; }
            internal int IdentityType { get; set; }
            internal int IdentityInstance { get; set; }
            internal string EpochScopedIdentityKey { get; set; }
            internal string LifecycleLineage { get; set; }
            internal string Name { get; set; }
            internal IntPtr Pointer { get; set; }
            internal Vector3 Position { get; set; }
            internal string PositionError { get; set; }
            internal Quaternion Rotation { get; set; }
            internal string RotationError { get; set; }
            internal int? CellId { get; set; }
            internal string CellError { get; set; }
            internal List<ClientStatRecord> Stats { get; set; }
            internal PacketScfuRecord Scfu { get; set; }
            internal PacketStatRecord LatestStatPacket { get; set; }
        }

        private abstract class PacketEventRecord
        {
            internal long EventSequence { get; set; }
            internal ZoneEpochRecord Epoch { get; set; }
            internal bool BridgeLinkEligible { get; set; }
            internal DateTime CapturedUtc { get; set; }
            internal string Direction { get; set; }
            internal long GlobalOrdinal { get; set; }
            internal int Sequence { get; set; }
            internal string DecodeError { get; set; }

            internal abstract string Serialize(string captureId);

            protected void AppendCommon(StringBuilder json, string captureId, string recordType)
            {
                AppendJsonString(json, "record_type", recordType, false);
                AppendJsonNumber(json, "schema_version", SchemaVersion, true);
                AppendJsonString(json, "capture_id", captureId, true);
                AppendJsonNullableString(
                    json,
                    "zone_epoch_id",
                    this.Epoch == null ? null : this.Epoch.ZoneEpochId,
                    true);
                AppendJsonBoolean(
                    json,
                    "zone_epoch_valid",
                    this.Epoch != null
                    && this.Epoch.Validity == "valid"
                    && this.Epoch.EndGlobalOrdinal.HasValue,
                    true);
                AppendJsonBoolean(json, "bridge_link_eligible", this.BridgeLinkEligible, true);
                AppendJsonString(json, "captured_utc", FormatUtc(this.CapturedUtc), true);
                AppendJsonString(json, "direction", this.Direction, true);
                AppendJsonNumber(json, "global_ordinal", this.GlobalOrdinal, true);
                AppendJsonNumber(json, "sequence", this.Sequence, true);
                AppendJsonString(json, "decode_error", this.DecodeError ?? string.Empty, true);
            }
        }

        private sealed class PacketEnvelopeRecord : PacketEventRecord
        {
            internal int N3TypeValue { get; set; }
            internal string N3TypeName { get; set; }
            internal int IdentityType { get; set; }
            internal int IdentityInstance { get; set; }

            internal override string Serialize(string captureId)
            {
                var json = new StringBuilder();
                json.Append("{");
                this.AppendCommon(json, captureId, "packet_event");
                AppendJsonNumber(json, "n3_type_value", this.N3TypeValue, true);
                AppendJsonString(json, "n3_type_name", this.N3TypeName ?? string.Empty, true);
                AppendJsonNumber(json, "runtime_identity_type", this.IdentityType, true);
                AppendJsonNumber(json, "runtime_identity_instance", this.IdentityInstance, true);
                AppendJsonString(json, "provenance", "raw-packets.csv envelope", true);
                json.Append("}");
                return json.ToString();
            }
        }

        private sealed class PacketScfuRecord : PacketEventRecord
        {
            internal RawSimpleCharFullUpdate Message { get; set; }

            internal override string Serialize(string captureId)
            {
                var json = new StringBuilder();
                json.Append("{");
                this.AppendCommon(json, captureId, "packet_scfu");
                if (this.Message == null)
                {
                    AppendJsonNullableNumber(json, "runtime_identity_type", (int?)null, true);
                    AppendJsonNullableNumber(json, "runtime_identity_instance", (int?)null, true);
                    AppendJsonString(json, "decode_state", "not-observed", true);
                }
                else
                {
                    AppendJsonNumber(json, "runtime_identity_type", this.Message.Identity.Type, true);
                    AppendJsonNumber(json, "runtime_identity_instance", this.Message.Identity.Instance, true);
                    AppendJsonString(
                        json,
                        "epoch_scoped_identity_key",
                        this.Epoch == null
                            ? string.Empty
                            : EpochIdentityKey(
                                this.Epoch.ZoneEpochId,
                                this.Message.Identity.Type,
                                this.Message.Identity.Instance),
                        true);
                    AppendJsonNullableNumber(json, "runtime_playfield_id", this.Message.PlayfieldId, true);
                    AppendJsonRawVector(json, "position", this.Message.Position, true);
                    if ((this.Message.Flags & 0x00000200) != 0)
                    {
                        AppendJsonRawQuaternion(json, "heading", this.Message.Heading, true);
                    }
                    else
                    {
                        AppendJsonNull(json, "heading", true);
                    }
                    AppendJsonUnsignedNumber(json, "monster_data", this.Message.MonsterData, true);
                    AppendJsonNullableNumber(json, "head_mesh", this.Message.HeadMesh, true);
                    AppendJsonString(
                        json,
                        "textures",
                        RawScfuFormatting.FormatTextures(this.Message.Textures),
                        true);
                    AppendJsonString(
                        json,
                        "meshes",
                        RawScfuFormatting.FormatMeshes(this.Message.Meshes),
                        true);
                    AppendJsonNumber(json, "visual_flags", this.Message.VisualFlags, true);
                    AppendJsonNumber(json, "appearance_value", this.Message.AppearanceValue, true);
                    AppendJsonNumber(json, "breed", this.Message.AppearanceBreed, true);
                    AppendJsonNumber(json, "gender", this.Message.AppearanceGender, true);
                    AppendJsonUnsignedNumber(json, "race", this.Message.AppearanceRace, true);
                    AppendJsonString(json, "name_corroborating_only", this.Message.Name ?? string.Empty, true);
                    if (this.Message.Owner.HasValue)
                    {
                        RawScfuIdentity owner = this.Message.Owner.Value;
                        AppendPropertyPrefix(json, "owner", true);
                        json.Append("{");
                        AppendJsonNumber(json, "type", owner.Type, false);
                        AppendJsonNumber(json, "instance", owner.Instance, true);
                        json.Append("}");
                    }
                    else
                    {
                        AppendJsonNull(json, "owner", true);
                    }
                    AppendJsonBoolean(json, "decode_fully_consumed", this.Message.DecodeFullyConsumed, true);
                    AppendJsonString(json, "provenance", "packet-observed", true);
                    AppendJsonNullableNumber(json, "full_model_type_direct", (int?)null, true);
                    AppendJsonNullableNumber(json, "full_model_instance_direct", (int?)null, true);
                }
                json.Append("}");
                return json.ToString();
            }
        }

        private sealed class PacketStatRecord : PacketEventRecord
        {
            internal RawStatMessage Message { get; set; }

            internal override string Serialize(string captureId)
            {
                var json = new StringBuilder();
                json.Append("{");
                this.AppendCommon(json, captureId, "packet_stat");
                if (this.Message == null)
                {
                    AppendJsonNullableNumber(json, "runtime_identity_type", (int?)null, true);
                    AppendJsonNullableNumber(json, "runtime_identity_instance", (int?)null, true);
                    AppendJsonString(json, "decode_state", "not-observed", true);
                    AppendPropertyPrefix(json, "stats", true);
                    json.Append("[]");
                }
                else
                {
                    AppendJsonNumber(json, "runtime_identity_type", this.Message.Identity.Type, true);
                    AppendJsonNumber(json, "runtime_identity_instance", this.Message.Identity.Instance, true);
                    AppendJsonString(
                        json,
                        "epoch_scoped_identity_key",
                        this.Epoch == null
                            ? string.Empty
                            : EpochIdentityKey(
                                this.Epoch.ZoneEpochId,
                                this.Message.Identity.Type,
                                this.Message.Identity.Instance),
                        true);
                    AppendJsonBoolean(json, "decode_fully_consumed", this.Message.DecodeFullyConsumed, true);
                    AppendJsonString(json, "provenance", "packet-observed", true);
                    AppendPropertyPrefix(json, "stats", true);
                    json.Append("[");
                    RawStatValue[] stats = this.Message.Stats ?? new RawStatValue[0];
                    for (int index = 0; index < stats.Length; index++)
                    {
                        if (index > 0)
                        {
                            json.Append(",");
                        }
                        RawStatValue stat = stats[index];
                        bool sentinel = stat.Value == unchecked((uint)UnsetStatSentinel);
                        json.Append("{");
                        AppendJsonNumber(json, "stat_ordinal", index, false);
                        AppendJsonNumber(json, "stat_id", stat.StatId, true);
                        if (sentinel)
                        {
                            AppendJsonNull(json, "value", true);
                            AppendJsonUnsignedNumber(json, "raw_value", stat.Value, true);
                            AppendJsonString(json, "provenance", "sentinel/default", true);
                        }
                        else
                        {
                            AppendJsonUnsignedNumber(json, "value", stat.Value, true);
                            AppendJsonUnsignedNumber(json, "raw_value", stat.Value, true);
                            AppendJsonString(json, "provenance", "packet-observed", true);
                        }
                        json.Append("}");
                    }
                    json.Append("]");
                }
                json.Append("}");
                return json.ToString();
            }
        }
    }
}
