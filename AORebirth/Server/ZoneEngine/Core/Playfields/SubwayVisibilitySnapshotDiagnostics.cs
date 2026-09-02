namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    #endregion

    internal enum SubwayVisibilityDiagnosticPacketKind
    {
        SimpleCharFullUpdate,
        WeaponDefinition,
        CharInPlay
    }

    internal static class SubwayVisibilitySnapshotDiagnostics
    {
        private static readonly object PendingSync = new object();

        private static readonly Dictionary<byte[], SubwayVisibilityDiagnosticPacketRecord> PendingPackets =
            new Dictionary<byte[], SubwayVisibilityDiagnosticPacketRecord>(new ByteArrayReferenceComparer());

        [ThreadStatic]
        private static SubwayVisibilityDiagnosticPacketContext currentPacket;

        internal static SubwayVisibilityDiagnosticSnapshot TryBeginSnapshot(ICharacter recipient, int candidateCharacters)
        {
            SubwayVisibilityDiagnosticConfiguration configuration =
                SubwayVisibilityDiagnosticSelection.Configuration;
            if (!configuration.Enabled
                || recipient == null
                || recipient.Playfield == null
                || recipient.Playfield.Identity.Instance != CapturedSubwayContentProvider.SubwayPlayfieldInstance)
            {
                return null;
            }

            return new SubwayVisibilityDiagnosticSnapshot(configuration, recipient, candidateCharacters);
        }

        internal static IDisposable BeginPacket(
            SubwayVisibilityDiagnosticSnapshot snapshot,
            SubwayVisibilityDiagnosticEnemy enemy,
            SubwayVisibilityDiagnosticPacketKind kind,
            int weaponIndex)
        {
            if (snapshot == null || enemy == null)
            {
                return EmptyScope.Instance;
            }

            SubwayVisibilityDiagnosticPacketContext previous = currentPacket;
            currentPacket = new SubwayVisibilityDiagnosticPacketContext(snapshot, enemy, kind, weaponIndex);
            return new PacketScope(previous);
        }

        internal static void OnSerializationStarted(MessageBody body)
        {
            SubwayVisibilityDiagnosticPacketContext context = currentPacket;
            if (context == null)
            {
                return;
            }

            context.Snapshot.RecordPacketEvent(context, "SERIALIZATION_STARTED", body, 0, string.Empty);
        }

        internal static void OnSerializationCompleted(MessageBody body, byte[] buffer)
        {
            SubwayVisibilityDiagnosticPacketContext context = currentPacket;
            if (context == null)
            {
                return;
            }

            var record = new SubwayVisibilityDiagnosticPacketRecord(
                context.Snapshot,
                context.Enemy,
                context.Kind,
                context.WeaponIndex,
                SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(buffer));
            context.Snapshot.RecordSerializedPacket(record, body);
            if (buffer != null)
            {
                lock (PendingSync)
                {
                    PendingPackets[buffer] = record;
                }
            }
        }

        internal static void OnSerializationFailed(MessageBody body, Exception exception)
        {
            SubwayVisibilityDiagnosticPacketContext context = currentPacket;
            if (context != null)
            {
                context.Snapshot.RecordPacketEvent(
                    context,
                    "SERIALIZATION_FAILED",
                    body,
                    0,
                    exception == null ? string.Empty : exception.ToString());
                context.Snapshot.RecordFailure(context.Enemy, "serialization", exception);
            }
        }

        internal static void OnTransportUnavailable(byte[] buffer, string reason)
        {
            SubwayVisibilityDiagnosticPacketRecord record = TakePacket(buffer);
            if (record == null)
            {
                return;
            }

            record.Snapshot.RecordTransportEvent(record, "SEND_FAILED", reason);
            record.Snapshot.RecordFailure(record.Enemy, "transport", new IOException(reason));
        }

        internal static void OnTransportStarted(byte[] buffer)
        {
            SubwayVisibilityDiagnosticPacketRecord record = FindPacket(buffer);
            if (record != null)
            {
                record.Snapshot.RecordTransportEvent(record, "SEND_STARTED", string.Empty);
            }
        }

        internal static void OnTransportCompleted(byte[] buffer)
        {
            SubwayVisibilityDiagnosticPacketRecord record = TakePacket(buffer);
            if (record == null)
            {
                return;
            }

            record.Snapshot.RecordTransportEvent(record, "SEND_COMPLETED", string.Empty);
            record.Snapshot.RecordPacketTransportCompleted(record);
        }

        internal static void OnTransportFailed(byte[] buffer, Exception exception)
        {
            SubwayVisibilityDiagnosticPacketRecord record = TakePacket(buffer);
            if (record == null)
            {
                return;
            }

            record.Snapshot.RecordTransportEvent(
                record,
                "SEND_FAILED",
                exception == null ? string.Empty : exception.ToString());
            record.Snapshot.RecordFailure(record.Enemy, "transport", exception);
        }

        private static SubwayVisibilityDiagnosticPacketRecord FindPacket(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            lock (PendingSync)
            {
                SubwayVisibilityDiagnosticPacketRecord record;
                PendingPackets.TryGetValue(buffer, out record);
                return record;
            }
        }

        private static SubwayVisibilityDiagnosticPacketRecord TakePacket(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            lock (PendingSync)
            {
                SubwayVisibilityDiagnosticPacketRecord record;
                if (!PendingPackets.TryGetValue(buffer, out record))
                {
                    return null;
                }

                PendingPackets.Remove(buffer);
                return record;
            }
        }

        private sealed class PacketScope : IDisposable
        {
            private readonly SubwayVisibilityDiagnosticPacketContext previous;
            private bool disposed;

            internal PacketScope(SubwayVisibilityDiagnosticPacketContext previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                currentPacket = this.previous;
                this.disposed = true;
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();
            public void Dispose()
            {
            }
        }

        private sealed class ByteArrayReferenceComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] left, byte[] right)
            {
                return object.ReferenceEquals(left, right);
            }

            public int GetHashCode(byte[] value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }

    internal sealed class SubwayVisibilityDiagnosticSnapshot
    {
        private readonly object sync = new object();
        private readonly SubwayVisibilityDiagnosticConfiguration configuration;
        private readonly Identity playerIdentity;
        private readonly int playfieldId;
        private readonly DateTime startedUtc;
        private readonly string snapshotId;
        private readonly string eventPath;
        private readonly string ledgerPath;
        private readonly string summaryPath;

        private int sendOrdinal;
        private int totalCandidateNpcs;
        private SubwayVisibilitySpatialInterestMetrics spatialInterestMetrics;
        private int totalNpcsSent;
        private int totalPackets;
        private long totalBytes;
        private int completedEnemies;
        private int largestScfu;
        private int largestEnemyTotal;
        private int lastCompletedOrdinal;
        private string lastCompletedIdentity = string.Empty;
        private DateTime? firstSendUtc;
        private DateTime? lastSendUtc;
        private bool enqueueCompleted;
        private bool failed;
        private bool finalized;

        internal SubwayVisibilityDiagnosticSnapshot(
            SubwayVisibilityDiagnosticConfiguration configuration,
            ICharacter recipient,
            int candidateCharacters)
        {
            this.configuration = configuration;
            this.playerIdentity = recipient.Identity;
            this.playfieldId = recipient.Playfield.Identity.Instance;
            this.startedUtc = DateTime.UtcNow;
            this.snapshotId = string.Format(
                CultureInfo.InvariantCulture,
                "{0}-{1:X8}-{2:yyyyMMddTHHmmssfff}",
                configuration.SessionId,
                recipient.Identity.Instance,
                this.startedUtc);
            this.eventPath = Path.Combine(configuration.ArtifactDirectory, "runtime-events.jsonl");
            this.ledgerPath = Path.Combine(configuration.ArtifactDirectory, "per-enemy-send-ledger.csv");
            this.summaryPath = Path.Combine(configuration.ArtifactDirectory, "snapshot-summary.jsonl");
            Directory.CreateDirectory(configuration.ArtifactDirectory);
            this.WriteEvent(
                "SNAPSHOT_STARTED",
                null,
                string.Empty,
                0,
                "candidate_characters=" + candidateCharacters.ToString(CultureInfo.InvariantCulture));
        }

        internal SubwayVisibilityDiagnosticEnemy BeginEnemy(Character character)
        {
            if (character == null || (character.Controller != null && character.Controller.Client != null))
            {
                return null;
            }

            SubwayVisibilityDiagnosticManifestEntry entry;
            SubwayVisibilityDiagnosticSelection.TryGetRuntimeEntry(character.Identity.Instance, out entry);
            Coordinate position = character.CalculatePredictedPosition();
            int ordinal;
            lock (this.sync)
            {
                ordinal = ++this.sendOrdinal;
            }

            int level = 0;
            try
            {
                level = character.Stats[StatIds.level].Value;
            }
            catch
            {
                level = 0;
            }

            var enemy = new SubwayVisibilityDiagnosticEnemy(
                ordinal,
                character.Identity,
                character.Name,
                entry == null ? character.Name : entry.Family,
                entry == null ? "BASELINE" : entry.Classification,
                entry == null ? 0 : entry.Ordinal,
                entry == null ? 0 : entry.SourceInstance,
                entry == null ? string.Empty : entry.SourceCapture,
                position.x,
                position.y,
                position.z,
                level,
                DateTime.UtcNow);
            this.WriteEvent("ENEMY_SEQUENCE_STARTED", enemy, string.Empty, 0, string.Empty);
            return enemy;
        }

        internal void MarkEnemyQueued(SubwayVisibilityDiagnosticEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            lock (this.sync)
            {
                this.totalNpcsSent++;
                enemy.CumulativeNpcCount = this.totalNpcsSent;
                enemy.CumulativePacketCount = this.totalPackets;
                enemy.CumulativeBytes = this.totalBytes;
            }

            this.WriteEvent("ENEMY_SEQUENCE_QUEUED", enemy, string.Empty, enemy.TotalBytes, string.Empty);
        }

        internal void MarkWeaponPhaseStarted(SubwayVisibilityDiagnosticEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            this.WriteEvent("WEAPON_ENUMERATION_STARTED", enemy, string.Empty, 0, string.Empty);
        }

        internal void MarkWeaponPhaseCompleted(SubwayVisibilityDiagnosticEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            this.WriteEvent(
                "WEAPON_ENUMERATION_COMPLETED",
                enemy,
                string.Empty,
                enemy.WeaponBytes,
                "weapon_packet_count=" + enemy.WeaponCount.ToString(CultureInfo.InvariantCulture));
        }

        internal void MarkSnapshotEnqueueCompleted()
        {
            lock (this.sync)
            {
                this.enqueueCompleted = true;
            }

            this.WriteEvent("SNAPSHOT_ENQUEUE_COMPLETED", null, string.Empty, 0, string.Empty);
            this.TryFinalize();
        }

        internal void SetTotalCandidateNpcs(int value)
        {
            lock (this.sync)
            {
                this.totalCandidateNpcs = Math.Max(0, value);
            }

            this.WriteEvent(
                "SNAPSHOT_CANDIDATES_COUNTED",
                null,
                string.Empty,
                0,
                "total_candidate_npcs=" + this.totalCandidateNpcs.ToString(CultureInfo.InvariantCulture));
        }

        internal void RecordSpatialInterestSelection(
            SubwayVisibilitySpatialInterestMetrics metrics)
        {
            if (metrics == null)
            {
                throw new ArgumentNullException("metrics");
            }

            lock (this.sync)
            {
                this.spatialInterestMetrics = metrics;
            }

            this.SetTotalCandidateNpcs(metrics.TotalPlayfieldNpcs);
            this.WriteEvent(
                "SNAPSHOT_SPATIAL_INTEREST_SELECTED",
                null,
                string.Empty,
                0,
                string.Join(
                    " ",
                    "total_playfield_characters=" + metrics.TotalPlayfieldCharacters.ToString(CultureInfo.InvariantCulture),
                    "total_playfield_npcs=" + metrics.TotalPlayfieldNpcs.ToString(CultureInfo.InvariantCulture),
                    "spatial_query_inspected_candidates=" + metrics.SpatialQueryInspectedCandidates.ToString(CultureInfo.InvariantCulture),
                    "within_enter_radius_count=" + metrics.WithinEnterRadiusCount.ToString(CultureInfo.InvariantCulture),
                    "already_visible_count=" + metrics.AlreadyVisibleCount.ToString(CultureInfo.InvariantCulture),
                    "newly_visible_count=" + metrics.NewlyVisibleCount.ToString(CultureInfo.InvariantCulture),
                    "leaving_visible_count=" + metrics.LeavingVisibleCount.ToString(CultureInfo.InvariantCulture),
                    "filtered_out_count=" + metrics.FilteredOutCount.ToString(CultureInfo.InvariantCulture)));
        }

        internal void RecordPacketEvent(
            SubwayVisibilityDiagnosticPacketContext context,
            string suffix,
            MessageBody body,
            int size,
            string detail)
        {
            this.WriteEvent(
                PacketPrefix(context.Kind) + "_" + suffix,
                context.Enemy,
                body == null ? string.Empty : body.GetType().Name,
                size,
                detail);
        }

        internal void RecordSerializedPacket(SubwayVisibilityDiagnosticPacketRecord record, MessageBody body)
        {
            record.Enemy.AddPacket(record.Kind, record.SerializedSize);
            lock (this.sync)
            {
                this.totalPackets++;
                this.totalBytes += record.SerializedSize;
                if (record.Kind == SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate
                    && record.SerializedSize > this.largestScfu)
                {
                    this.largestScfu = record.SerializedSize;
                }
            }

            this.WriteEvent(
                PacketPrefix(record.Kind) + "_SERIALIZATION_COMPLETED",
                record.Enemy,
                body == null ? string.Empty : body.GetType().Name,
                record.SerializedSize,
                "weapon_index=" + record.WeaponIndex.ToString(CultureInfo.InvariantCulture));
        }

        internal void RecordTransportEvent(
            SubwayVisibilityDiagnosticPacketRecord record,
            string suffix,
            string detail)
        {
            DateTime now = DateTime.UtcNow;
            lock (this.sync)
            {
                if (!this.firstSendUtc.HasValue)
                {
                    this.firstSendUtc = now;
                }

                this.lastSendUtc = now;
            }

            this.WriteEvent(
                PacketPrefix(record.Kind) + "_" + suffix,
                record.Enemy,
                string.Empty,
                record.SerializedSize,
                detail);
        }

        internal void RecordPacketTransportCompleted(SubwayVisibilityDiagnosticPacketRecord record)
        {
            if (record.Kind != SubwayVisibilityDiagnosticPacketKind.CharInPlay)
            {
                return;
            }

            record.Enemy.CompletedUtc = DateTime.UtcNow;
            lock (this.sync)
            {
                this.completedEnemies++;
                this.lastCompletedOrdinal = record.Enemy.SendOrdinal;
                this.lastCompletedIdentity = record.Enemy.RuntimeIdentity.ToString();
                if (record.Enemy.TotalBytes > this.largestEnemyTotal)
                {
                    this.largestEnemyTotal = record.Enemy.TotalBytes;
                }
            }

            this.WriteLedger(record.Enemy);
            this.WriteEvent("ENEMY_SEQUENCE_COMPLETED", record.Enemy, string.Empty, record.Enemy.TotalBytes, string.Empty);
            this.TryFinalize();
        }

        internal void RecordFailure(
            SubwayVisibilityDiagnosticEnemy enemy,
            string phase,
            Exception exception)
        {
            bool firstFailure;
            lock (this.sync)
            {
                firstFailure = !this.failed;
                this.failed = true;
            }

            if (!firstFailure)
            {
                return;
            }

            if (enemy != null)
            {
                this.WriteLedger(enemy, false, phase);
            }

            this.WriteEvent(
                "SNAPSHOT_FAILURE",
                enemy,
                string.Empty,
                0,
                "phase=" + phase + " exception=" + (exception == null ? string.Empty : exception.ToString()));
            this.WriteSummary(false, "FAILED");
        }

        private void TryFinalize()
        {
            bool shouldFinalize;
            lock (this.sync)
            {
                shouldFinalize =
                    !this.finalized
                    && !this.failed
                    && this.enqueueCompleted
                    && this.completedEnemies == this.totalNpcsSent;
                if (shouldFinalize)
                {
                    this.finalized = true;
                }
            }

            if (shouldFinalize)
            {
                this.WriteEvent("SNAPSHOT_COMPLETED", null, string.Empty, 0, string.Empty);
                this.WriteSummary(true, "COMPLETED");
            }
        }

        private void WriteEvent(
            string eventName,
            SubwayVisibilityDiagnosticEnemy enemy,
            string messageType,
            int size,
            string detail)
        {
            DateTime now = DateTime.UtcNow;
            int cumulativeNpcCount;
            int cumulativePacketCount;
            long cumulativeByteCount;
            lock (this.sync)
            {
                cumulativeNpcCount = enemy == null ? this.totalNpcsSent : enemy.CumulativeNpcCount;
                cumulativePacketCount = enemy == null ? this.totalPackets : enemy.CumulativePacketCount;
                cumulativeByteCount = enemy == null ? this.totalBytes : enemy.CumulativeBytes;
            }

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJson(builder, "timestamp_utc", now.ToString("o", CultureInfo.InvariantCulture), true);
            AppendJson(builder, "session_id", this.configuration.SessionId, false);
            AppendJson(builder, "snapshot_id", this.snapshotId, false);
            AppendJson(builder, "player_identity", this.playerIdentity.ToString(), false);
            AppendJson(builder, "playfield_id", this.playfieldId, false);
            AppendJson(builder, "snapshot_phase", "initial_visibility", false);
            AppendJson(builder, "selected_slice", this.configuration.Slice, false);
            AppendJson(builder, "event", eventName, false);
            AppendJson(builder, "send_ordinal", enemy == null ? 0 : enemy.SendOrdinal, false);
            AppendJson(builder, "manifest_ordinal", enemy == null ? 0 : enemy.ManifestOrdinal, false);
            AppendJson(builder, "enemy_identity", enemy == null ? string.Empty : enemy.RuntimeIdentity.ToString(), false);
            AppendJson(builder, "enemy_type", enemy == null ? string.Empty : enemy.RuntimeIdentity.Type.ToString(), false);
            AppendJson(builder, "enemy_instance", enemy == null ? 0 : enemy.RuntimeIdentity.Instance, false);
            AppendJson(builder, "source_identity", enemy == null || enemy.SourceInstance == 0 ? string.Empty : "SimpleChar:" + enemy.SourceInstance.ToString("X8", CultureInfo.InvariantCulture), false);
            AppendJson(builder, "enemy_name", enemy == null ? string.Empty : enemy.Name, false);
            AppendJson(builder, "enemy_family", enemy == null ? string.Empty : enemy.Family, false);
            AppendJson(builder, "source_population_group", enemy == null ? string.Empty : enemy.SourceGroup, false);
            AppendJson(builder, "source_capture", enemy == null ? string.Empty : enemy.SourceCapture, false);
            AppendJson(builder, "quarantine_slice", this.configuration.Slice, false);
            AppendJson(builder, "position", enemy == null ? string.Empty : enemy.PositionText, false);
            AppendJson(builder, "level", enemy == null ? 0 : enemy.Level, false);
            AppendJson(builder, "message_type", messageType, false);
            AppendJson(builder, "serialized_size", size, false);
            AppendJson(builder, "cumulative_npc_count", cumulativeNpcCount, false);
            AppendJson(builder, "cumulative_packet_count", cumulativePacketCount, false);
            AppendJson(builder, "cumulative_byte_count", cumulativeByteCount, false);
            AppendJson(builder, "elapsed_ms", (long)(now - this.startedUtc).TotalMilliseconds, false);
            AppendJson(builder, "detail", detail, false);
            builder.Append('}');
            AppendLine(this.eventPath, builder.ToString());
        }

        private void WriteLedger(SubwayVisibilityDiagnosticEnemy enemy)
        {
            this.WriteLedger(enemy, true, string.Empty);
        }

        private void WriteLedger(SubwayVisibilityDiagnosticEnemy enemy, bool completed, string failureState)
        {
            lock (this.sync)
            {
                if (enemy.LedgerWritten)
                {
                    return;
                }

                enemy.LedgerWritten = true;
                if (!File.Exists(this.ledgerPath))
                {
                    File.AppendAllText(
                        this.ledgerPath,
                        "SessionId,SnapshotId,PlayerIdentity,PlayfieldId,SendOrdinal,ManifestOrdinal,RuntimeIdentity,EnemyType,EnemyInstance,SourceIdentity,Name,Family,SourcePopulationGroup,SourceCapture,SelectedSlice,Position,Level,ScfuBytes,WeaponPacketCount,WeaponBytes,CharInPlayBytes,PacketCount,TotalBytes,CumulativeNpcCount,CumulativePacketCount,CumulativeBytes,SequenceCompleted,FailureState,ElapsedMs\r\n",
                        Encoding.UTF8);
                }

                string line = string.Join(
                    ",",
                    Csv(this.configuration.SessionId),
                    Csv(this.snapshotId),
                    Csv(this.playerIdentity.ToString()),
                    this.playfieldId.ToString(CultureInfo.InvariantCulture),
                    enemy.SendOrdinal.ToString(CultureInfo.InvariantCulture),
                    enemy.ManifestOrdinal.ToString(CultureInfo.InvariantCulture),
                    Csv(enemy.RuntimeIdentity.ToString()),
                    Csv(enemy.RuntimeIdentity.Type.ToString()),
                    enemy.RuntimeIdentity.Instance.ToString(CultureInfo.InvariantCulture),
                    Csv(enemy.SourceInstance == 0 ? string.Empty : "SimpleChar:" + enemy.SourceInstance.ToString("X8", CultureInfo.InvariantCulture)),
                    Csv(enemy.Name),
                    Csv(enemy.Family),
                    Csv(enemy.SourceGroup),
                    Csv(enemy.SourceCapture),
                    Csv(this.configuration.Slice),
                    Csv(enemy.PositionText),
                    enemy.Level.ToString(CultureInfo.InvariantCulture),
                    enemy.ScfuBytes.ToString(CultureInfo.InvariantCulture),
                    enemy.WeaponCount.ToString(CultureInfo.InvariantCulture),
                    enemy.WeaponBytes.ToString(CultureInfo.InvariantCulture),
                    enemy.CharInPlayBytes.ToString(CultureInfo.InvariantCulture),
                    enemy.PacketCount.ToString(CultureInfo.InvariantCulture),
                    enemy.TotalBytes.ToString(CultureInfo.InvariantCulture),
                    enemy.CumulativeNpcCount.ToString(CultureInfo.InvariantCulture),
                    enemy.CumulativePacketCount.ToString(CultureInfo.InvariantCulture),
                    enemy.CumulativeBytes.ToString(CultureInfo.InvariantCulture),
                    completed ? "1" : "0",
                    Csv(failureState),
                    ((long)((enemy.CompletedUtc ?? DateTime.UtcNow) - this.startedUtc).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
                File.AppendAllText(this.ledgerPath, line + "\r\n", Encoding.UTF8);
            }
        }

        private void WriteSummary(bool completed, string status)
        {
            lock (this.sync)
            {
                var builder = new StringBuilder();
                builder.Append('{');
                AppendJson(builder, "session_id", this.configuration.SessionId, true);
                AppendJson(builder, "snapshot_id", this.snapshotId, false);
                AppendJson(builder, "player_identity", this.playerIdentity.ToString(), false);
                AppendJson(builder, "playfield_id", this.playfieldId, false);
                AppendJson(builder, "total_candidate_npcs", this.totalCandidateNpcs, false);
                (this.spatialInterestMetrics
                 ?? SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(0, 0, 0, 0, 0))
                    .AppendJsonFields(builder, false);
                AppendJson(builder, "total_npcs_sent", this.totalNpcsSent, false);
                AppendJson(builder, "total_packet_count", this.totalPackets, false);
                AppendJson(builder, "total_serialized_bytes", this.totalBytes, false);
                AppendJson(builder, "largest_scfu_size", this.largestScfu, false);
                AppendJson(builder, "largest_per_enemy_total_size", this.largestEnemyTotal, false);
                AppendJson(builder, "first_send_timestamp", this.firstSendUtc.HasValue ? this.firstSendUtc.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty, false);
                AppendJson(builder, "last_send_timestamp", this.lastSendUtc.HasValue ? this.lastSendUtc.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty, false);
                AppendJson(builder, "total_duration_ms", (long)(DateTime.UtcNow - this.startedUtc).TotalMilliseconds, false);
                AppendJson(builder, "last_completed_ordinal", this.lastCompletedOrdinal, false);
                AppendJson(builder, "last_completed_enemy_identity", this.lastCompletedIdentity, false);
                AppendJson(builder, "snapshot_completed", completed, false);
                AppendJson(builder, "snapshot_completion_status", status, false);
                AppendJson(builder, "selected_diagnostic_slice", this.configuration.Slice, false);
                AppendJson(builder, "expected_quarantined_row_count", this.configuration.ExpectedQuarantinedRowCount, false);
                builder.Append('}');
                File.AppendAllText(this.summaryPath, builder + "\r\n", Encoding.UTF8);
            }
        }

        private static string PacketPrefix(SubwayVisibilityDiagnosticPacketKind kind)
        {
            if (kind == SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate)
            {
                return "SCFU";
            }

            return kind == SubwayVisibilityDiagnosticPacketKind.WeaponDefinition
                       ? "WEAPON_DEFINITION"
                       : "CHAR_IN_PLAY";
        }

        private static void AppendLine(string path, string line)
        {
            lock (typeof(SubwayVisibilityDiagnosticSnapshot))
            {
                File.AppendAllText(path, line + "\r\n", Encoding.UTF8);
            }
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(JsonEscape(name)).Append("\":\"").Append(JsonEscape(value)).Append('"');
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool first)
        {
            AppendJsonNumber(builder, name, value.ToString(CultureInfo.InvariantCulture), first);
        }

        private static void AppendJson(StringBuilder builder, string name, long value, bool first)
        {
            AppendJsonNumber(builder, name, value.ToString(CultureInfo.InvariantCulture), first);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool first)
        {
            AppendJsonNumber(builder, name, value ? "true" : "false", first);
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, string value, bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(JsonEscape(name)).Append("\":").Append(value);
        }

        private static string JsonEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }

    internal sealed class SubwayVisibilityDiagnosticEnemy
    {
        private readonly object sync = new object();

        internal SubwayVisibilityDiagnosticEnemy(
            int sendOrdinal,
            Identity runtimeIdentity,
            string name,
            string family,
            string sourceGroup,
            int manifestOrdinal,
            int sourceInstance,
            string sourceCapture,
            float x,
            float y,
            float z,
            int level,
            DateTime startedUtc)
        {
            this.SendOrdinal = sendOrdinal;
            this.RuntimeIdentity = runtimeIdentity;
            this.Name = name ?? string.Empty;
            this.Family = family ?? string.Empty;
            this.SourceGroup = sourceGroup ?? string.Empty;
            this.ManifestOrdinal = manifestOrdinal;
            this.SourceInstance = sourceInstance;
            this.SourceCapture = sourceCapture ?? string.Empty;
            this.PositionText = string.Format(CultureInfo.InvariantCulture, "{0:0.######}|{1:0.######}|{2:0.######}", x, y, z);
            this.Level = level;
            this.StartedUtc = startedUtc;
        }

        internal int SendOrdinal { get; private set; }
        internal Identity RuntimeIdentity { get; private set; }
        internal string Name { get; private set; }
        internal string Family { get; private set; }
        internal string SourceGroup { get; private set; }
        internal int ManifestOrdinal { get; private set; }
        internal int SourceInstance { get; private set; }
        internal string SourceCapture { get; private set; }
        internal string PositionText { get; private set; }
        internal int Level { get; private set; }
        internal DateTime StartedUtc { get; private set; }
        internal DateTime? CompletedUtc { get; set; }
        internal int ScfuBytes { get; private set; }
        internal int WeaponCount { get; private set; }
        internal int WeaponBytes { get; private set; }
        internal int CharInPlayBytes { get; private set; }
        internal int PacketCount { get; private set; }
        internal int TotalBytes { get; private set; }
        internal int CumulativeNpcCount { get; set; }
        internal int CumulativePacketCount { get; set; }
        internal long CumulativeBytes { get; set; }
        internal bool LedgerWritten { get; set; }

        internal void AddPacket(SubwayVisibilityDiagnosticPacketKind kind, int size)
        {
            lock (this.sync)
            {
                this.PacketCount++;
                this.TotalBytes += size;
                if (kind == SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate)
                {
                    this.ScfuBytes = size;
                }
                else if (kind == SubwayVisibilityDiagnosticPacketKind.WeaponDefinition)
                {
                    this.WeaponCount++;
                    this.WeaponBytes += size;
                }
                else
                {
                    this.CharInPlayBytes = size;
                }
            }
        }
    }

    internal sealed class SubwayVisibilityDiagnosticPacketContext
    {
        internal SubwayVisibilityDiagnosticPacketContext(
            SubwayVisibilityDiagnosticSnapshot snapshot,
            SubwayVisibilityDiagnosticEnemy enemy,
            SubwayVisibilityDiagnosticPacketKind kind,
            int weaponIndex)
        {
            this.Snapshot = snapshot;
            this.Enemy = enemy;
            this.Kind = kind;
            this.WeaponIndex = weaponIndex;
        }

        internal SubwayVisibilityDiagnosticSnapshot Snapshot { get; private set; }
        internal SubwayVisibilityDiagnosticEnemy Enemy { get; private set; }
        internal SubwayVisibilityDiagnosticPacketKind Kind { get; private set; }
        internal int WeaponIndex { get; private set; }
    }

    internal sealed class SubwayVisibilityDiagnosticPacketRecord
    {
        internal SubwayVisibilityDiagnosticPacketRecord(
            SubwayVisibilityDiagnosticSnapshot snapshot,
            SubwayVisibilityDiagnosticEnemy enemy,
            SubwayVisibilityDiagnosticPacketKind kind,
            int weaponIndex,
            int serializedSize)
        {
            this.Snapshot = snapshot;
            this.Enemy = enemy;
            this.Kind = kind;
            this.WeaponIndex = weaponIndex;
            this.SerializedSize = serializedSize;
        }

        internal SubwayVisibilityDiagnosticSnapshot Snapshot { get; private set; }
        internal SubwayVisibilityDiagnosticEnemy Enemy { get; private set; }
        internal SubwayVisibilityDiagnosticPacketKind Kind { get; private set; }
        internal int WeaponIndex { get; private set; }
        internal int SerializedSize { get; private set; }
    }
}
