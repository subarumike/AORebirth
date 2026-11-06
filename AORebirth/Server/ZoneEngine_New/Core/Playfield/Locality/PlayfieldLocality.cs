namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;
    using AORebirth.World.Collision;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;

    public sealed class PlayfieldLocality
    {
        private readonly int _playfieldId;
        private readonly CellGrid _grid;
        private readonly LocalityPolicy _policy;
        private readonly LocalityVisibility _visibility;
        private readonly CellHeatScheduler _heatScheduler;
        private readonly HashSet<Dynel> _tracked = [];
        private readonly List<Dynel> _tickBuffer = [];
        private readonly List<int> _nearbyCellBuffer = [];

        // RespawnForObservers: dynels despawned for their observers, with the tick serial they were despawned on.
        private readonly Dictionary<Dynel, long> _pendingRespawns = [];
        private readonly List<Dynel> _respawnBuffer = [];
        private long _tickSerial;

        public PlayfieldLocality(int playfieldId, PlayfieldMetaData? metaData)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            _playfieldId = playfieldId;
            _policy = LocalityPolicy.FromConfig();
            _grid = new CellGrid(metaData, _policy.VisibilityNeighborLevel);
            _visibility = new LocalityVisibility(_grid, _policy, _tracked);
            _heatScheduler = new CellHeatScheduler(playfieldId, _grid, _policy);
        }

        internal CellGrid Grid => _grid;

        /// <summary>
        /// Fills <paramref name="results"/> with the dynels an NPC concerns itself with: those in
        /// <paramref name="center"/>'s cell and the cells touching it. Indoors the playfield is one cell, and a
        /// dungeon's rooms have no grid adjacency, so both return every occupant. Includes <paramref name="center"/>.
        /// Tick thread only (shares a scratch buffer).
        /// </summary>
        internal void CollectNearby(Dynel center, List<Dynel> results) => CollectNearby(center, 0f, results);

        /// <summary>
        /// Same, typed: only occupants that are <typeparamref name="T"/>. <paramref name="reachMeters"/> is the
        /// farthest the caller will act (an aggro range); the ring is the adjacent cells, and grows past them only
        /// when that reach is longer than a cell, so data-driven ranges are never cut short.
        /// </summary>
        internal void CollectNearby<T>(Dynel center, float reachMeters, List<T> results)
            where T : Dynel
        {
            ArgumentNullException.ThrowIfNull(center);
            ArgumentNullException.ThrowIfNull(results);
            results.Clear();

            if (NearbyIsWholePlayfield)
            {
                _grid.CollectAllOccupants(results);
                return;
            }

            int cellId = center.Cell?.Id ?? (_grid.TryGetCellId(center.Position, out int resolved) ? resolved : CellGrid.NonLocalCellId);
            CollectAround(cellId, reachMeters, results);
        }

        /// <summary>
        /// Same as <see cref="CollectNearby{T}(Dynel, float, List{T})"/> around a point rather than a dynel (a door
        /// the client creates from the playfield data, which the server holds no dynel for). Tick thread only.
        /// </summary>
        internal void CollectNear<T>(AORebirth.Core.Vector.Vector3 position, float reachMeters, List<T> results)
            where T : Dynel
        {
            ArgumentNullException.ThrowIfNull(results);
            results.Clear();

            if (NearbyIsWholePlayfield)
            {
                _grid.CollectAllOccupants(results);
                return;
            }

            CollectAround(_grid.TryGetCellId(position, out int cellId) ? cellId : CellGrid.NonLocalCellId, reachMeters, results);
        }

        void CollectAround<T>(int cellId, float reachMeters, List<T> results)
            where T : Dynel
        {
            if (cellId == CellGrid.NonLocalCellId)
                return;

            _grid.CollectNeighbors(cellId, RingFor(reachMeters), _nearbyCellBuffer);
            for (int i = 0; i < _nearbyCellBuffer.Count; i++)
            {
                if (!_grid.TryGetCell(_nearbyCellBuffer[i], out Cell cell))
                    continue;
                foreach (Dynel dynel in cell.OccupantSet)
                {
                    if (dynel is T typed)
                        results.Add(typed);
                }
            }
        }

        /// <summary>
        /// Indoors and in dungeons every occupant is nearby: <see cref="CollectNear{T}"/> and
        /// <see cref="CollectNearby{T}(Dynel, float, List{T})"/> return the whole playfield wherever they are asked.
        /// </summary>
        internal bool NearbyIsWholePlayfield => !_grid.IsOutdoor || _grid.IsDungeon;

        /// <summary>Every occupant of type <typeparamref name="T"/> in the playfield, without allocating. Tick thread only.</summary>
        internal void CollectAll<T>(List<T> results)
            where T : Dynel
        {
            ArgumentNullException.ThrowIfNull(results);
            results.Clear();
            _grid.CollectAllOccupants(results);
        }

        /// <summary>
        /// Body radii a range check measured edge-to-edge can add to centre distance. Generous on purpose: it only
        /// decides whether a long reach needs a second ring of cells.
        /// </summary>
        const float ReachBodyMarginMeters = 5f;

        /// <summary>Adjacent cells (1), or more when <paramref name="reachMeters"/> plus body margin passes one cell.</summary>
        int RingFor(float reachMeters)
        {
            float cell = _grid.CellWorldSize;
            if (reachMeters <= 0f || cell <= 0f)
                return 1;
            return Math.Max(1, (int)Math.Ceiling((reachMeters + ReachBodyMarginMeters) / cell));
        }

        internal LocalityPolicy Policy => _policy;

        public void ApplyDungeonRooms(AORebirth.World.Collision.DungeonWorldLayout layout)
            => _grid.ApplyDungeonRooms(layout);

        /// <summary>
        /// Outdoor XZ must lie in the legacy playfield extent (<c>Width|Height * 4</c>).
        /// Indoor layouts always accept.
        /// </summary>
        public bool ContainsWorldPosition(float x, float z) => _grid.ContainsWorldPosition(x, z);

        /// <summary>Outdoor world width (X), or 0 for indoor.</summary>
        public float WorldSizeX => _grid.WorldSizeX;

        /// <summary>Outdoor world depth (Z), or 0 for indoor.</summary>
        public float WorldSizeZ => _grid.WorldSizeZ;

        internal void AttachHashSpawns(
            IEnumerable<int> spawnCellIds,
            IEnumerable<int> scriptedSpawnCellIds,
            Action<int> onCellSleep,
            Action<int> onCellTick,
            Action onIndoorSpawnTick)
        {
            _heatScheduler.ConfigureSpawnHooks(
                spawnCellIds,
                onCellSleep,
                onCellTick,
                onIndoorSpawnTick,
                scriptedSpawnCellIds);
        }

        // Players sneaking as of the last tick, and when sneakers were last re-checked against everyone's Perception.
        readonly HashSet<Player> _sneakers = new();
        readonly List<Player> _sneakChanges = new();
        long _lastSneakRecheckMs;

        /// <summary>Re-checks a sneaker this often, for Perception or Concealment that changed while it stood still.</summary>
        const long SneakRecheckMs = 1000;

        /// <summary>
        /// A player who starts or stops sneaking is re-shown to everyone near (hidden from those who cannot perceive it,
        /// spawned again for the rest), and sneakers are re-checked every <see cref="SneakRecheckMs"/>.
        /// </summary>
        void TickSneakVisibility()
        {
            _sneakChanges.Clear();
            foreach (Dynel dynel in _tracked)
            {
                if (dynel is not Player player)
                    continue;
                bool sneaking = player.Motor.State == Movement.MovementState.Sneak;
                if (sneaking ? _sneakers.Add(player) : _sneakers.Remove(player))
                    _sneakChanges.Add(player);
            }

            _sneakers.RemoveWhere(player => !_tracked.Contains(player));

            long now = Environment.TickCount64;
            if (_sneakers.Count > 0 && now - _lastSneakRecheckMs >= SneakRecheckMs)
            {
                _lastSneakRecheckMs = now;
                foreach (Player sneaker in _sneakers)
                    if (!_sneakChanges.Contains(sneaker))
                        _sneakChanges.Add(sneaker);
            }

            for (int i = 0; i < _sneakChanges.Count; i++)
                _visibility.Reconcile(_sneakChanges[i]);
        }

        public void RegisterDynel(Dynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);

            _tracked.Add(dynel);
            _visibility.Track(dynel);
            PlaceInCell(dynel, logPlayerCellChange: dynel is Player);

            // Players activate visibility after self spawn packets (see ActivatePlayerVisibility).
            if (dynel is not Player)
                _visibility.Reconcile(dynel, AORebirth.Modules.Events.VisibilityReason.Spawned);
        }

        public void ActivatePlayerVisibility(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            _visibility.ActivatePlayerVisibility(player);
        }

        public void PrimeVendingMachineVisibility(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            _visibility.PrimeVendingMachineVisibility(player);
        }

        public void DeactivatePlayerVisibility(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            _visibility.DeactivatePlayerVisibility(player);
        }

        /// <inheritdoc cref="LocalityVisibility.DespawnForObservers"/>
        public void DespawnForObservers(Dynel source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _visibility.DespawnForObservers(source);
        }

        /// <summary>
        /// Sends <paramref name="source"/> afresh to everyone who sees it, for a change only its spawn packet carries
        /// (an NPC becoming or ceasing to be a pet). It is despawned for them now and shown again on a later locality
        /// tick: the client does not show a spawn in the same burst as the despawn. <paramref name="except"/> keeps the
        /// copy it has and is not sent it again.
        /// </summary>
        public void RespawnForObservers(Dynel source, Player? except = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!_tracked.Contains(source))
                return;

            _visibility.DespawnForObservers(source, except);
            _pendingRespawns[source] = _tickSerial;
        }

        /// <summary>Re-checks now who sees <paramref name="dynel"/>, after a change to who it is hidden from.</summary>
        public void ReconcileVisibility(Dynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);
            if (_tracked.Contains(dynel))
                _visibility.Reconcile(dynel);
        }

        public void UnregisterDynel(Dynel dynel, Player? transferredTo = null)
        {
            if (dynel == null)
                return;

            _visibility.Untrack(dynel, transferredTo);
            _tracked.Remove(dynel);
            _pendingRespawns.Remove(dynel);
            dynel.Cell?.Remove(dynel);
            dynel.Cell = null;
        }

        /// <summary>
        /// Sends <paramref name="message"/> to players who currently see <paramref name="source"/>.
        /// When <paramref name="includeSelf"/> is true and source is a connected player, also sends to self.
        /// </summary>
        public void Announce(Dynel source, MessageBody message, bool includeSelf = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(message);
            _visibility.Announce(source, message, includeSelf);
        }

        internal Player[] SnapshotObservers(Dynel source, bool includeSelf = false)
            => _visibility.SnapshotObservers(source, includeSelf);

        /// <summary>True when <paramref name="source"/> is currently spawned on <paramref name="player"/>'s client.</summary>
        public bool IsVisibleTo(Dynel source, Player player) => _visibility.IsVisibleTo(source, player);

        public void Tick(double deltaTime)
        {
            if (_pendingRespawns.Count > 0)
            {
                TickStallWatch.Stage("locality.respawn");
                _respawnBuffer.Clear();
                foreach (KeyValuePair<Dynel, long> pending in _pendingRespawns)
                {
                    if (pending.Value < _tickSerial)
                        _respawnBuffer.Add(pending.Key);
                }

                for (int i = 0; i < _respawnBuffer.Count; i++)
                {
                    Dynel dynel = _respawnBuffer[i];
                    _pendingRespawns.Remove(dynel);
                    if (_tracked.Contains(dynel))
                        _visibility.Reconcile(dynel, AORebirth.Modules.Events.VisibilityReason.Spawned);
                }
            }

            TickStallWatch.Stage("locality.cells");
            _tickBuffer.Clear();
            foreach (Dynel dynel in _tracked)
                _tickBuffer.Add(dynel);

            for (int i = 0; i < _tickBuffer.Count; i++)
            {
                Dynel dynel = _tickBuffer[i];
                if (!_tracked.Contains(dynel) || !dynel.Transform.PositionChangedSinceLastTick)
                    continue;

                Cell? previous = dynel.Cell;
                PlaceInCell(dynel, logPlayerCellChange: dynel is Player);
                dynel.Transform.AcknowledgePositionChange();

                if (!ReferenceEquals(previous, dynel.Cell))
                    _visibility.Reconcile(dynel);
            }

            TickStallWatch.Stage("locality.sneak");
            TickSneakVisibility();

            TickStallWatch.Stage("locality.heat");
            _heatScheduler.Tick(_tickBuffer, deltaTime);
            _tickSerial++;
        }

        private void PlaceInCell(Dynel dynel, bool logPlayerCellChange)
        {
            Cell? previous = dynel.Cell;
            dynel.Cell?.Remove(dynel);

            // Floor then clamp: a registered dynel always has a cell, including at the exclusive
            // far edge where floor(worldSize/cellSize) would otherwise fall past the last index.
            Cell cell = _grid.ResolveCell(dynel.Position, previous?.Id ?? CellGrid.NonLocalCellId);
            cell.Add(dynel);
            dynel.Cell = cell;

            if (logPlayerCellChange
                && dynel is Player player
                && !ReferenceEquals(previous, dynel.Cell))
            {
                LogPlayerCellChange(
                    player,
                    previous?.Id ?? CellGrid.NonLocalCellId,
                    cell.Id);
            }
        }

        private void LogPlayerCellChange(Player player, int oldCellId, int newCellId)
        {
            if (!LogUtil.HasDetail(DebugInfoDetail.Locality))
                return;

            Vector3 position = player.Position;
            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} player {1}/{2} cell {3} -> {4} pos=({5:F1},{6:F1},{7:F1})",
                    _playfieldId,
                    player.Identity,
                    player.Name ?? string.Empty,
                    FormatCellLabel(oldCellId),
                    FormatCellLabel(newCellId),
                    position.xf,
                    position.yf,
                    position.zf));
        }

        private string FormatCellLabel(int cellId)
        {
            if (cellId < 0)
                return "non-local";

            if (_grid.IsDungeon)
                return "indoor:" + cellId.ToString(CultureInfo.InvariantCulture);

            if (!_grid.IsOutdoor)
                return "indoor:0";

            _grid.GetCellCoords(cellId, out int ix, out int iz);
            return string.Format(CultureInfo.InvariantCulture, "{0}:({1},{2})", cellId, ix, iz);
        }
    }
}
