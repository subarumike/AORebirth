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

            if (!_grid.IsOutdoor || _grid.IsDungeon)
            {
                foreach (Dynel dynel in _grid.OccupantsInAllCells())
                {
                    if (dynel is T typed)
                        results.Add(typed);
                }

                return;
            }

            int cellId = center.Cell?.Id ?? (_grid.TryGetCellId(center.Position, out int resolved) ? resolved : CellGrid.NonLocalCellId);
            if (cellId == CellGrid.NonLocalCellId)
                return;

            _grid.CollectNeighbors(cellId, RingFor(reachMeters), _nearbyCellBuffer);
            for (int i = 0; i < _nearbyCellBuffer.Count; i++)
            {
                if (!_grid.TryGetCell(_nearbyCellBuffer[i], out Cell cell))
                    continue;
                foreach (Dynel dynel in cell.Occupants)
                {
                    if (dynel is T typed)
                        results.Add(typed);
                }
            }
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

        public void ApplyDungeonRooms(IReadOnlyList<DungeonRoomBounds> rooms)
            => _grid.ApplyDungeonRooms(rooms);

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
            Action<int> onCellSleep,
            Action<int> onCellTick,
            Action onIndoorSpawnTick)
        {
            _heatScheduler.ConfigureSpawnHooks(
                spawnCellIds,
                onCellSleep,
                onCellTick,
                onIndoorSpawnTick);
        }

        public void RegisterDynel(Dynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);

            _tracked.Add(dynel);
            _visibility.Track(dynel);
            PlaceInCell(dynel, logPlayerCellChange: dynel is Player);

            // Players activate visibility after self spawn packets (see ActivatePlayerVisibility).
            if (dynel is not Player)
                _visibility.Reconcile(dynel);
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

        public void UnregisterDynel(Dynel dynel)
        {
            if (dynel == null)
                return;

            _visibility.Untrack(dynel);
            _tracked.Remove(dynel);
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

            TickStallWatch.Stage("locality.heat");
            _heatScheduler.Tick(_tickBuffer, deltaTime);
        }

        private void PlaceInCell(Dynel dynel, bool logPlayerCellChange)
        {
            Cell? previous = dynel.Cell;
            dynel.Cell?.Remove(dynel);

            // Floor then clamp: a registered dynel always has a cell, including at the exclusive
            // far edge where floor(worldSize/cellSize) would otherwise fall past the last index.
            Cell cell = _grid.ResolveCell(dynel.Position);
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
