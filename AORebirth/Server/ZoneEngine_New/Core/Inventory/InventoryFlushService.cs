namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Per-character write-behind for dirty item locations and uploaded nanos.
    /// Coalesces bursts, writes on a dedicated thread (off playfield tick), hard-flushes on authority boundaries.
    /// Inventory and nano inserts commit in one transaction.
    /// </summary>
    public sealed class InventoryFlushService : IDisposable
    {
        /// <summary>Quiet period after last MarkDirty before an async write runs.</summary>
        public const int CoalesceMilliseconds = 300;

        private readonly Lazy<PlayfieldManager> _playfieldManager;
        private readonly ICharacterCoalesceCommit _persist;
        private readonly IZoneLogger _logger;
        private readonly object _scheduleGate = new();
        private readonly Dictionary<int, long> _dueAtMs = new();
        private readonly ManualResetEventSlim _wake = new(false);
        private readonly Thread _writer;
        private volatile bool _disposed;

        public InventoryFlushService(
            Lazy<PlayfieldManager> playfieldManager,
            ICharacterCoalesceCommit persist,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(persist);
            ArgumentNullException.ThrowIfNull(logger);

            _playfieldManager = playfieldManager;
            _persist = persist;
            _logger = logger;

            _writer = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "InventoryFlushWriter"
            };
            _writer.Start();
        }

        /// <summary>
        /// Schedules a coalesced async flush for this character. Safe to call from the playfield tick.
        /// </summary>
        public void NotifyDirty(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (_disposed || !player.Inventory.IsHydrated || player.IsPersistenceQuarantined)
                return;

            int characterId = player.Identity.Instance;
            if (characterId <= 0)
                return;

            long due = Environment.TickCount64 + CoalesceMilliseconds;
            lock (_scheduleGate)
            {
                _dueAtMs[characterId] = due;
                _wake.Set();
            }
        }

        /// <summary>
        /// Synchronous durable commit (logout, despawn, transfer, shutdown). Cancels a pending coalesce.
        /// </summary>
        public void HardFlush(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            int characterId = player.Identity.Instance;
            lock (_scheduleGate)
                _dueAtMs.Remove(characterId);

            FlushCharacter(player);
        }

        /// <summary>
        /// Trade and snapshot writes share the aggregate's gate with the background writer.
        /// Ordered acquisition prevents opposing player trades from deadlocking. The caller
        /// combines pending dirty entries with the final trade in a single transaction.
        /// </summary>
        public void WithExclusivePlayers(Player first, Player? second, Action action)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(action);
            Player low = second != null && second.Identity.Instance < first.Identity.Instance ? second : first;
            Player? high = second == null ? null : ReferenceEquals(low, first) ? second : first;
            lock (low.PersistenceGate)
            {
                if (high == null)
                    Run();
                else
                    lock (high.PersistenceGate) Run();
            }

            void Run()
            {
                if (first.IsPersistenceQuarantined || second?.IsPersistenceQuarantined == true)
                    throw new InvalidOperationException("Trade participant persistence is quarantined.");
                lock (_scheduleGate)
                {
                    _dueAtMs.Remove(first.Identity.Instance);
                    if (second != null) _dueAtMs.Remove(second.Identity.Instance);
                }
                action();
            }
        }

        void WriterLoop()
        {
            while (!_disposed)
            {
                int waitMs = Timeout.Infinite;
                List<int> dueIds = [];

                lock (_scheduleGate)
                {
                    if (_dueAtMs.Count > 0)
                    {
                        long now = Environment.TickCount64;
                        long soonest = long.MaxValue;
                        foreach (KeyValuePair<int, long> pair in _dueAtMs)
                        {
                            if (pair.Value <= now)
                                dueIds.Add(pair.Key);
                            else if (pair.Value < soonest)
                                soonest = pair.Value;
                        }

                        foreach (int id in dueIds)
                            _dueAtMs.Remove(id);

                        if (dueIds.Count == 0 && soonest != long.MaxValue)
                        {
                            long delta = soonest - now;
                            waitMs = delta <= 0 ? 0 : (int)Math.Min(delta, int.MaxValue);
                        }
                    }

                    if (dueIds.Count == 0)
                        _wake.Reset();
                }

                if (dueIds.Count == 0)
                {
                    _wake.Wait(waitMs);
                    continue;
                }

                PlayfieldManager playfields = _playfieldManager.Value;
                foreach (int characterId in dueIds)
                {
                    if (_disposed)
                        break;

                    if (!playfields.FindPlayer(characterId, out Player player))
                        continue;

                    try
                    {
                        FlushCharacter(player);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            exception,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "InventoryFlushService async flush failed for character {0}",
                                characterId));
                    }
                }
            }
        }

        void FlushCharacter(Player player)
        {
            if (!player.Inventory.IsHydrated)
                return;

            if (!player.Inventory.HasDirtyEntries && !player.HasDirtyUploadedNanos)
                return;

            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined)
                    throw new InvalidOperationException("Inventory persistence is quarantined pending database reconciliation.");
                if (!player.Inventory.HasDirtyEntries && !player.HasDirtyUploadedNanos)
                    return;

                PlayerInventory.InventoryDirtyFlush? inventory = player.Inventory.TakeDirty();
                int[] nanos = player.DrainDirtyUploadedNanos();
                if (inventory == null && nanos.Length == 0)
                    return;

                try
                {
                    _persist.Persist(
                        inventory?.Inserts ?? [],
                        inventory?.Updates ?? [],
                        player.Identity.Instance,
                        nanos);
                    inventory?.MarkNewlyPersisted();
                }
                catch (DatabaseCommitOutcomeUnknownException exception)
                {
                    player.QuarantinePersistence();
                    player.Session?.Close();
                    _logger.Error(exception, "Inventory commit outcome is unknown; player persistence quarantined.");
                    throw;
                }
                catch (Exception exception)
                {
                    if (inventory != null)
                        player.Inventory.RestoreDirty(inventory);
                    player.RestoreDirtyUploadedNanos(nanos);
                    _logger.Error(
                        exception,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "InventoryFlushService flush failed for character {0}",
                            player.Identity.Instance));
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            List<int> remaining;
            lock (_scheduleGate)
            {
                remaining = new List<int>(_dueAtMs.Keys);
                _dueAtMs.Clear();
                _wake.Set();
            }

            try
            {
                PlayfieldManager playfields = _playfieldManager.Value;
                foreach (Player player in playfields.SnapshotPlayers())
                {
                    try
                    {
                        HardFlush(player);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            exception,
                            "InventoryFlushService shutdown flush failed for character "
                            + player.Identity.Instance);
                    }
                }

                // Characters scheduled but already unregistered still need nothing if HardFlush ran on despawn.
                _ = remaining;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "InventoryFlushService Dispose flush failed");
            }

            _wake.Set();
            if (!_writer.Join(2000))
                _logger.Warn("InventoryFlushService writer did not stop within 2s");

            _wake.Dispose();
        }
    }
}
