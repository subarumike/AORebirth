namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine_New.Core.Entities;

    public sealed class PlayfieldLocality
    {
        private readonly int _playfieldId;
        private readonly CellGrid _grid;
        private readonly LocalityPolicy _policy;
        private readonly LocalityVisibility _visibility;
        private readonly CellHeatScheduler _heatScheduler;
        private readonly HashSet<Dynel> _tracked = [];

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

        internal LocalityPolicy Policy => _policy;

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

        public void Tick(double deltaTime)
        {
            foreach (Dynel dynel in _tracked)
            {
                if (!dynel.Transform.PositionChangedSinceLastTick)
                    continue;

                Cell? previous = dynel.Cell;
                PlaceInCell(dynel, logPlayerCellChange: dynel is Player);
                dynel.Transform.AcknowledgePositionChange();

                if (!ReferenceEquals(previous, dynel.Cell))
                    _visibility.Reconcile(dynel);
            }

            _heatScheduler.Tick(_tracked, deltaTime);
        }

        private void PlaceInCell(Dynel dynel, bool logPlayerCellChange)
        {
            Cell? previous = dynel.Cell;
            dynel.Cell?.Remove(dynel);

            if (_grid.TryResolveCell(dynel.Position, out Cell cell))
            {
                cell.Add(dynel);
                dynel.Cell = cell;
            }
            else
            {
                dynel.Cell = null;
            }

            if (logPlayerCellChange
                && dynel is Player player
                && !ReferenceEquals(previous, dynel.Cell))
            {
                LogPlayerCellChange(
                    player,
                    previous?.Id ?? CellGrid.NonLocalCellId,
                    dynel.Cell?.Id ?? CellGrid.NonLocalCellId);
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

            if (!_grid.IsOutdoor)
                return "indoor:0";

            _grid.GetCellCoords(cellId, out int ix, out int iz);
            return string.Format(CultureInfo.InvariantCulture, "{0}:({1},{2})", cellId, ix, iz);
        }
    }
}
