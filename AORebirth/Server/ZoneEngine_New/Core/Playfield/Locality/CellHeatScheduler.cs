namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine_New.Core.Entities;

    internal enum CellHeat
    {
        Asleep = 0,
        Cold = 1,
        Warm = 2,
        Hot = 3
    }

    /// <summary>
    /// Outdoor cell tick cadence by heat tier. Indoor and disabled-heat paths tick every dynel every heartbeat.
    /// Warm/Cold pass accumulated wall-clock delta since the cell's last successful tick.
    /// Cells default Asleep until Hot/Warm (player proximity or forced hot); Cold is only the
    /// post-Hot/Warm cooldown before sleep. Heat candidates = occupied ∪ spawn-bearing ∪ cells
    /// within Warm range of connected players (so empty neighbor heat transitions are tracked).
    /// </summary>
    internal sealed class CellHeatScheduler
    {
        private readonly int _playfieldId;
        private readonly CellGrid _grid;
        private readonly LocalityPolicy _policy;
        private readonly Dictionary<int, DateTime> _coldSinceUtcByCell = new();
        private readonly Dictionary<int, DateTime> _lastTickUtcByCell = new();
        private readonly Dictionary<int, CellHeat> _heatByCell = new();
        private readonly HashSet<int> _spawnCellIds = new();
        private readonly List<int> _playerCells = new();
        private readonly HashSet<int> _forcedHotCells = new();
        private readonly List<int> _heatCellBuffer = new();
        private readonly List<int> _neighborBuffer = new();
        private readonly List<Dynel> _tickDynelBuffer = new();
        private Action<int>? _onCellSleep;
        private Action<int>? _onCellTick;
        private Action? _onIndoorSpawnTick;
        private int _heartbeatCounter;

        internal CellHeatScheduler(int playfieldId, CellGrid grid, LocalityPolicy policy)
        {
            _playfieldId = playfieldId;
            _grid = grid;
            _policy = policy;
        }

        internal void ConfigureSpawnHooks(
            IEnumerable<int> spawnCellIds,
            Action<int> onCellSleep,
            Action<int> onCellTick,
            Action onIndoorSpawnTick)
        {
            ArgumentNullException.ThrowIfNull(spawnCellIds);
            ArgumentNullException.ThrowIfNull(onCellSleep);
            ArgumentNullException.ThrowIfNull(onCellTick);
            ArgumentNullException.ThrowIfNull(onIndoorSpawnTick);

            _spawnCellIds.Clear();
            foreach (int cellId in spawnCellIds)
                _spawnCellIds.Add(cellId);

            _onCellSleep = onCellSleep;
            _onCellTick = onCellTick;
            _onIndoorSpawnTick = onIndoorSpawnTick;
        }

        internal void Tick(IEnumerable<Dynel> tracked, double heartbeatDeltaTime)
        {
            _heartbeatCounter++;

            if (!_grid.IsOutdoor || !_policy.EnableCellHeatScheduling)
            {
                // Snapshot: Tick may despawn/spawn (death → corpse) and mutate _tracked.
                _tickDynelBuffer.Clear();
                foreach (Dynel dynel in tracked)
                    _tickDynelBuffer.Add(dynel);

                for (int i = 0; i < _tickDynelBuffer.Count; i++)
                    _tickDynelBuffer[i].Tick(heartbeatDeltaTime);

                _onIndoorSpawnTick?.Invoke();
                return;
            }

            DateTime now = DateTime.UtcNow;
            CollectHeatContext(tracked);
            TrackHeatTransitions(now, heartbeatDeltaTime);
        }

        private void CollectHeatContext(IEnumerable<Dynel> tracked)
        {
            _playerCells.Clear();
            _forcedHotCells.Clear();

            HashSet<int> connectedPlayerInstances = new();

            foreach (Dynel dynel in tracked)
            {
                if (dynel is Player player && player.Session != null && player.Cell != null)
                {
                    _playerCells.Add(player.Cell.Id);
                    connectedPlayerInstances.Add(player.Identity.Instance);
                }
            }

            foreach (Dynel dynel in tracked)
            {
                if (dynel.Cell == null)
                    continue;

                if (IsCombatHot(dynel) || IsPetPinnedToConnectedPlayer(dynel, connectedPlayerInstances))
                    _forcedHotCells.Add(dynel.Cell.Id);
            }
        }

        private void TrackHeatTransitions(DateTime now, double heartbeatDeltaTime)
        {
            CollectHeatCells(_heatCellBuffer);
            HashSet<int> seenCells = new();

            foreach (int cellId in _heatCellBuffer)
            {
                seenCells.Add(cellId);
                bool isNewCell = !_heatByCell.TryGetValue(cellId, out CellHeat previousHeat);
                CellHeat heat = ResolveHeat(cellId);

                // Leaving Hot/Warm with no cold timer would resolve Asleep; enter Cold cooldown first.
                if (!isNewCell
                    && (previousHeat == CellHeat.Hot || previousHeat == CellHeat.Warm)
                    && heat == CellHeat.Asleep)
                {
                    heat = CellHeat.Cold;
                }

                if (!isNewCell && previousHeat != heat)
                    LogHeatChange(cellId, previousHeat, heat);

                _heatByCell[cellId] = heat;
                UpdateColdTimer(cellId, heat, now);

                if (heat == CellHeat.Asleep)
                {
                    if (!isNewCell && previousHeat != CellHeat.Asleep)
                        _onCellSleep?.Invoke(cellId);

                    // Keep last-tick current so wake does not dump the full sleep duration as delta.
                    _lastTickUtcByCell[cellId] = now;
                    continue;
                }

                if (!ShouldTickCell(cellId, heat, now))
                    continue;

                double elapsed = heartbeatDeltaTime;
                if (_lastTickUtcByCell.TryGetValue(cellId, out DateTime lastTick))
                {
                    elapsed = (now - lastTick).TotalSeconds;
                    if (elapsed <= 0.0)
                        elapsed = heartbeatDeltaTime;
                }

                _lastTickUtcByCell[cellId] = now;
                _onCellTick?.Invoke(cellId);

                // Snapshot: Tick may despawn NPCs and spawn corpses into this cell.
                _tickDynelBuffer.Clear();
                foreach (Dynel dynel in _grid.OccupantsInCell(cellId))
                    _tickDynelBuffer.Add(dynel);

                for (int i = 0; i < _tickDynelBuffer.Count; i++)
                    _tickDynelBuffer[i].Tick(elapsed);
            }

            List<int> staleCells = new();
            foreach (int cellId in _heatByCell.Keys)
            {
                if (!seenCells.Contains(cellId) && !_spawnCellIds.Contains(cellId))
                    staleCells.Add(cellId);
            }

            foreach (int cellId in staleCells)
            {
                if (_heatByCell.TryGetValue(cellId, out CellHeat previousHeat)
                    && previousHeat != CellHeat.Asleep)
                {
                    LogHeatChange(cellId, previousHeat, CellHeat.Asleep);
                }

                _heatByCell.Remove(cellId);
                _coldSinceUtcByCell.Remove(cellId);
                _lastTickUtcByCell.Remove(cellId);
            }
        }

        private void CollectHeatCells(List<int> results)
        {
            results.Clear();
            HashSet<int> added = new();

            foreach (int cellId in _grid.EnumeratePopulatedCells())
            {
                if (added.Add(cellId))
                    results.Add(cellId);
            }

            foreach (int cellId in _spawnCellIds)
            {
                if (added.Add(cellId))
                    results.Add(cellId);
            }

            // Track empty cells near players so Hot/Warm/Cold transitions are visible without occupants.
            if (_grid.IsOutdoor && _playerCells.Count > 0)
            {
                foreach (int playerCell in _playerCells)
                {
                    _grid.CollectNeighbors(playerCell, _policy.WarmNeighborLevel, _neighborBuffer);
                    foreach (int neighborId in _neighborBuffer)
                    {
                        if (added.Add(neighborId))
                            results.Add(neighborId);
                    }
                }
            }
        }

        private CellHeat ResolveHeat(int cellId)
        {
            if (_forcedHotCells.Contains(cellId))
                return CellHeat.Hot;

            int minDistance = int.MaxValue;
            if (_playerCells.Count == 0)
            {
                minDistance = int.MaxValue;
            }
            else
            {
                foreach (int playerCell in _playerCells)
                {
                    int distance = _grid.ChebyshevDistance(cellId, playerCell);
                    if (distance < minDistance)
                        minDistance = distance;
                }
            }

            if (minDistance <= _policy.HotNeighborLevel)
                return CellHeat.Hot;

            if (minDistance <= _policy.WarmNeighborLevel)
                return CellHeat.Warm;

            // No cooling timer → never woken (or fully slept) → Asleep. Cold only while cooling.
            if (!_coldSinceUtcByCell.TryGetValue(cellId, out DateTime coldSince))
                return CellHeat.Asleep;

            if ((DateTime.UtcNow - coldSince).TotalSeconds >= _policy.CellSleepTimeSeconds)
                return CellHeat.Asleep;

            return CellHeat.Cold;
        }

        private void UpdateColdTimer(int cellId, CellHeat heat, DateTime now)
        {
            if (heat == CellHeat.Hot || heat == CellHeat.Warm)
            {
                _coldSinceUtcByCell.Remove(cellId);
                return;
            }

            if (heat == CellHeat.Asleep)
            {
                _coldSinceUtcByCell.Remove(cellId);
                return;
            }

            if (!_coldSinceUtcByCell.ContainsKey(cellId))
                _coldSinceUtcByCell[cellId] = now;
        }

        private bool ShouldTickCell(int cellId, CellHeat heat, DateTime now)
        {
            switch (heat)
            {
                case CellHeat.Asleep:
                    return false;
                case CellHeat.Hot:
                    return true;
                case CellHeat.Warm:
                    return _heartbeatCounter % 2 == 0;
                case CellHeat.Cold:
                    if (!_lastTickUtcByCell.TryGetValue(cellId, out DateTime lastTick))
                        return true;

                    return (now - lastTick).TotalSeconds >= 1.0;
                default:
                    return false;
            }
        }

        private void LogHeatChange(int cellId, CellHeat previousHeat, CellHeat newHeat)
        {
            if (!LogUtil.HasDetail(DebugInfoDetail.Locality))
                return;

            _grid.GetCellCoords(cellId, out int ix, out int iz);
            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} cell {1} ({2},{3}) heat {4} -> {5}",
                    _playfieldId,
                    cellId,
                    ix,
                    iz,
                    previousHeat,
                    newHeat));
        }

        private static bool IsCombatHot(Dynel dynel)
        {
            int health = dynel.Stats.Get(CharacterStat.Health);
            if (StatCollection.IsUnset(health) || health <= 0)
                return false;

            int selectedTarget = dynel.Stats.Get(CharacterStat.SelectedTarget);
            return !StatCollection.IsUnset(selectedTarget) && selectedTarget != 0;
        }

        private static bool IsPetPinnedToConnectedPlayer(Dynel dynel, HashSet<int> connectedPlayerInstances)
        {
            int petMaster = dynel.Stats.Get(CharacterStat.PetMaster);
            return !StatCollection.IsUnset(petMaster)
                   && petMaster != 0
                   && connectedPlayerInstances.Contains(petMaster);
        }
    }
}
