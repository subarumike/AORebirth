namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using AORebirth.Database.Dao;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Durable writes of the in-memory character aggregate. Logout/despawn commits location + base stats
    /// synchronously; while online, dirty and periodic base-stat checkpoints are captured on the playfield
    /// tick and written behind on a dedicated thread, one transaction per character.
    /// Inventory is flushed separately by <see cref="ZoneEngine_New.Core.Inventory.InventoryFlushService"/>.
    /// </summary>
    public sealed class CharacterSnapshotService : IDisposable
    {
        /// <summary>Quiet period after the last <see cref="CharacterSaveState.MarkDirty"/> before a checkpoint.</summary>
        public const int CoalesceMilliseconds = 2000;

        /// <summary>Continuous changes still checkpoint this long after the first unsaved one.</summary>
        public const int MaxDirtyDelayMilliseconds = 10000;

        /// <summary>Upper bound on progress lost to a crash for changes nobody marked dirty.</summary>
        public const int CheckpointIntervalMilliseconds = 60000;

        private readonly ICharacterRepository _characters;
        private readonly IStatRepository _stats;
        private readonly IZoneLogger _logger;
        private readonly object _queueGate = new();
        private readonly Dictionary<Player, PendingCheckpoint> _pending = new(ReferenceEqualityComparer.Instance);
        private readonly ManualResetEventSlim _wake = new(false);
        private Thread? _writer;
        private volatile bool _disposed;

        public CharacterSnapshotService(
            ICharacterRepository characters,
            IStatRepository stats,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(logger);

            _characters = characters;
            _stats = stats;
            _logger = logger;
        }

        public void Commit(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined)
                    throw new InvalidOperationException("A quarantined player must be reloaded before persistence.");
                long sequence = player.SaveState.NextSequence();
                try
                {
                    if (CommitCore(player) is { } committed)
                        player.SaveState.MarkCommitted(sequence, committed.Stats, committed.Location);
                }
                catch (DatabaseCommitOutcomeUnknownException)
                {
                    player.QuarantinePersistence();
                    player.Session?.Close();
                    throw;
                }
            }
        }

        private (CharacterRecord Location, List<StatRecord> Stats)? CommitCore(Player player)
        {
            int characterId = player.Identity.Instance;
            if (characterId <= 0)
                return null;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character snapshot skipped character={0}: no playfield",
                        characterId));
                return null;
            }

            int playfieldId = playfield.Identity.Instance;
            if (playfieldId <= 0)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character snapshot skipped character={0}: playfield id {1}",
                        characterId,
                        playfieldId));
                return null;
            }

            Vector3 position = player.Position;
            CharacterRecord record = CaptureLocation(player, characterId, playfieldId);
            List<StatRecord> stats = CaptureBaseStats(player);
            _characters.SaveSnapshot(record, online: 0, stats);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Character snapshot character={0} playfield={1} pos=({2},{3},{4}) stats={5}",
                    characterId,
                    playfieldId,
                    position.xf,
                    position.yf,
                    position.zf,
                    stats.Count));
            return (record, stats);
        }

        /// <summary>
        /// Queues a checkpoint (location + base stats) when the player's save is due. Must run on the owning
        /// playfield tick: state is copied here because the writer thread may not read the live aggregate.
        /// </summary>
        public void CheckpointIfDue(Player player) => CheckpointIfDue(player, Environment.TickCount64);

        internal void CheckpointIfDue(Player player, long nowMs)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (_disposed || player.IsPersistenceQuarantined || player.Identity.Instance <= 0)
                return;
            if (!player.SaveState.TakeDue(nowMs, player.Identity.Instance))
                return;

            var checkpoint = new PendingCheckpoint(
                player.SaveState.NextSequence(),
                CaptureResumableLocation(player),
                CaptureBaseStats(player));
            lock (_queueGate)
            {
                if (_disposed)
                    return;
                _pending[player] = checkpoint;
                _writer ??= StartWriter();
                _wake.Set();
            }
        }

        /// <summary>
        /// Null keeps the stored location: a dead player must resume at the respawn point, not the corpse.
        /// Mission worlds are stored as-is; login resolves them to the world or its exterior.
        /// </summary>
        private static CharacterRecord? CaptureResumableLocation(Player player)
        {
            if (player.IsDead || player.IsRespawnPending)
                return null;
            int playfieldId = player.Playfield?.Identity.Instance ?? 0;
            return playfieldId <= 0 ? null : CaptureLocation(player, player.Identity.Instance, playfieldId);
        }

        private static CharacterRecord CaptureLocation(Player player, int characterId, int playfieldId)
        {
            Vector3 position = player.Position;
            Quaternion heading = player.Rotation;
            return new CharacterRecord
            {
                Id = characterId,
                Playfield = playfieldId,
                X = position.xf,
                Y = position.yf,
                Z = position.zf,
                HeadingW = heading.wf,
                HeadingX = heading.xf,
                HeadingY = heading.yf,
                HeadingZ = heading.zf
            };
        }

        private static List<StatRecord> CaptureBaseStats(Player player)
        {
            List<StatRecord> stats = [];
            foreach (var entry in player.Stats.GetEntries())
            {
                if (entry.Stat == CharacterStat.NumberOfFightingOpponents)
                    continue;

                if (StatCollection.IsUnset(entry.Base))
                    continue;

                stats.Add(
                    new StatRecord
                    {
                        StatId = (int)entry.Stat,
                        StatValue = entry.Base
                    });
            }

            return stats;
        }

        private Thread StartWriter()
        {
            var writer = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "CharacterCheckpointWriter"
            };
            writer.Start();
            return writer;
        }

        private void WriterLoop()
        {
            while (!_disposed)
            {
                _wake.Wait();
                List<KeyValuePair<Player, PendingCheckpoint>> batch;
                lock (_queueGate)
                {
                    batch = [.. _pending];
                    _pending.Clear();
                    _wake.Reset();
                }

                foreach (KeyValuePair<Player, PendingCheckpoint> pair in batch)
                {
                    if (_disposed)
                        break;

                    try
                    {
                        WriteCheckpoint(pair.Key, pair.Value);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            exception,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Character checkpoint failed character={0}",
                                pair.Key.Identity.Instance));
                    }
                }
            }
        }

        private void WriteCheckpoint(Player player, PendingCheckpoint checkpoint)
        {
            CharacterSaveState state = player.SaveState;
            lock (player.PersistenceGate)
            {
                // A logout snapshot or newer checkpoint already made this capture obsolete.
                if (player.IsPersistenceQuarantined || !state.IsNewerThanCommitted(checkpoint.Sequence))
                    return;

                List<StatRecord> changed = state.ChangedSincePersisted(checkpoint.Stats);
                CharacterRecord? location = checkpoint.Location != null && state.LocationChanged(checkpoint.Location)
                    ? checkpoint.Location
                    : null;
                if (changed.Count > 0 || location != null)
                {
                    bool written;
                    try
                    {
                        written = _characters.SaveOnlineCheckpoint(player.Identity.Instance, location, changed);
                    }
                    catch (DatabaseCommitOutcomeUnknownException)
                    {
                        player.QuarantinePersistence();
                        player.Session?.Close();
                        throw;
                    }

                    if (!written)
                    {
                        // Storage says offline while this aggregate is live; leave the baseline so nothing is lost.
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Character checkpoint skipped character={0}: row is not marked online",
                                player.Identity.Instance));
                        return;
                    }
                }

                state.MarkCommitted(checkpoint.Sequence, changed, location);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Thread? writer;
            lock (_queueGate)
            {
                _disposed = true;
                _pending.Clear();
                writer = _writer;
                _wake.Set();
            }

            if (writer != null && !writer.Join(2000))
            {
                // The writer may still wait on the event; disposing it would fault that thread.
                _logger.Warn("Character checkpoint writer did not stop within 2s");
                return;
            }

            _wake.Dispose();
        }

        private sealed record PendingCheckpoint(long Sequence, CharacterRecord? Location, IReadOnlyList<StatRecord> Stats);

        public IDisposable AcquireOnlineOwnership(int characterId)
            => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(characterId, _characters.SetOnline);

        public void AbandonOnlineOwnership(int characterId, IDisposable ownership)
        {
            ArgumentNullException.ThrowIfNull(ownership);
            ownership.Dispose();
            ClearOnlineIfUnowned(characterId);
        }

        public void ClearOnlineIfUnowned(int characterId)
            => CharacterOnlineOwnershipGuard.TryClearLoginOwnership(characterId, _characters.SetOffline);
    }

    /// <summary>
    /// Write-behind bookkeeping for one player aggregate. The schedule belongs to the owning playfield
    /// tick; persisted values and the committed sequence are guarded by <see cref="Player.PersistenceGate"/>.
    /// </summary>
    public sealed class CharacterSaveState
    {
        /// <summary>Smaller moves ride along with the next write instead of causing one.</summary>
        public const float MinimumPersistedMove = 1f;

        private readonly Dictionary<int, int> _persisted = new();
        private CharacterRecord? _persistedLocation;
        private long _sequence;
        private long _committedSequence;
        private long _dueAtMs;
        private long _deadlineMs;
        private long _nextCheckpointMs;

        /// <summary>Requests a coalesced checkpoint. Call from the owning playfield tick.</summary>
        public void MarkDirty()
        {
            long now = Environment.TickCount64;
            if (_dueAtMs == 0)
                _deadlineMs = now + CharacterSnapshotService.MaxDirtyDelayMilliseconds;
            _dueAtMs = Math.Min(now + CharacterSnapshotService.CoalesceMilliseconds, _deadlineMs);
        }

        /// <summary>
        /// High-frequency values that would otherwise checkpoint every few seconds for anyone in combat.
        /// They are still written by the periodic checkpoint and the logout snapshot.
        /// </summary>
        public static bool IsCheckpointOnly(CharacterStat stat) => stat switch
        {
            CharacterStat.Health or CharacterStat.CurrentNano or CharacterStat.MaxHealth
                or CharacterStat.MaxNanoEnergy or CharacterStat.PercentRemainingHealth
                or CharacterStat.PercentRemainingNano or CharacterStat.CurrentNCU
                or CharacterStat.NumberOfFightingOpponents or CharacterStat.SelectedTargetType
                or CharacterStat.CurrentMovementMode or CharacterStat.State or CharacterStat.CurrentState
                or CharacterStat.ActionCategory or CharacterStat.DeadTimer => true,
            _ => false,
        };

        /// <summary>Location and base stats as they exist in storage right after hydration.</summary>
        public void SeedPersisted(CharacterRecord location, IReadOnlyList<StatRecord> stats)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(stats);
            _persistedLocation = location;
            foreach (StatRecord stat in stats)
                _persisted[stat.StatId] = stat.StatValue;
            // Hydration itself raises base changes; those are not player progress.
            _dueAtMs = 0;
        }

        internal bool TakeDue(long nowMs, int characterId)
        {
            // Spread first checkpoints over the second half of the interval so a mass login does not
            // write in lockstep, without ever exceeding the interval.
            const int half = CharacterSnapshotService.CheckpointIntervalMilliseconds / 2;
            if (_nextCheckpointMs == 0)
                _nextCheckpointMs = nowMs + half + characterId % half;

            bool dirtyDue = _dueAtMs != 0 && nowMs >= _dueAtMs;
            if (!dirtyDue && nowMs < _nextCheckpointMs)
                return false;

            _dueAtMs = 0;
            _nextCheckpointMs = nowMs + CharacterSnapshotService.CheckpointIntervalMilliseconds;
            return true;
        }

        internal long NextSequence() => Interlocked.Increment(ref _sequence);

        internal bool IsNewerThanCommitted(long sequence) => sequence > _committedSequence;

        internal List<StatRecord> ChangedSincePersisted(IReadOnlyList<StatRecord> stats)
        {
            List<StatRecord> changed = [];
            foreach (StatRecord stat in stats)
                if (!_persisted.TryGetValue(stat.StatId, out int value) || value != stat.StatValue)
                    changed.Add(stat);
            return changed;
        }

        internal bool LocationChanged(CharacterRecord location)
        {
            CharacterRecord? stored = _persistedLocation;
            if (stored == null || stored.Playfield != location.Playfield)
                return true;
            float dx = stored.X - location.X, dy = stored.Y - location.Y, dz = stored.Z - location.Z;
            return dx * dx + dy * dy + dz * dz >= MinimumPersistedMove * MinimumPersistedMove;
        }

        internal void MarkCommitted(long sequence, IReadOnlyList<StatRecord> written, CharacterRecord? location)
        {
            if (location != null)
                _persistedLocation = location;
            foreach (StatRecord stat in written)
                _persisted[stat.StatId] = stat.StatValue;
            if (sequence > _committedSequence)
                _committedSequence = sequence;
        }
    }
}
