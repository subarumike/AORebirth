namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    internal enum CellHeat
    {
        Asleep = 0,
        Cold = 1,
        Warm = 2,
        Hot = 3
    }

    internal sealed class PlayfieldCellHeatScheduler
    {
        private readonly IPlayfieldCellLayout layout;
        private readonly PlayfieldLocalityPolicy policy;
        private readonly PlayfieldDynelCellRegistry cells;
        private readonly Dictionary<int, DateTime> coldSinceUtcByCell = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> lastTickUtcByCell = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, CellHeat> heatByCell = new Dictionary<int, CellHeat>();
        private readonly List<int> neighborBuffer = new List<int>();
        private readonly List<int> playerCells = new List<int>();
        private int heartbeatCounter;

        internal PlayfieldCellHeatScheduler(
            IPlayfieldCellLayout layout,
            PlayfieldLocalityPolicy policy,
            PlayfieldDynelCellRegistry cells)
        {
            this.layout = layout;
            this.policy = policy;
            this.cells = cells;
        }

        internal void Tick(
            IEnumerable<ICharacter> connectedPlayers,
            IEnumerable<ICharacter> combatHotCharacters,
            Action<ICharacter, double> processDynel,
            double deltaTime)
        {
            this.heartbeatCounter++;
            if (this.layout.IsIndoor)
            {
                foreach (ICharacter dynel in this.cells.AllRegisteredCharacters())
                {
                    if (dynel != null)
                    {
                        processDynel(dynel, deltaTime);
                    }
                }

                return;
            }

            this.playerCells.Clear();
            if (connectedPlayers != null)
            {
                foreach (ICharacter player in connectedPlayers)
                {
                    if (player == null || player.Controller == null || player.Controller.Client == null)
                    {
                        continue;
                    }

                    int cellId;
                    if (this.cells.TryGetCellId(player, out cellId) && cellId >= 0)
                    {
                        this.playerCells.Add(cellId);
                    }
                }
            }

            HashSet<int> combatHotCells = new HashSet<int>();
            if (combatHotCharacters != null)
            {
                foreach (ICharacter character in combatHotCharacters)
                {
                    int cellId;
                    if (character != null && this.cells.TryGetCellId(character, out cellId) && cellId >= 0)
                    {
                        combatHotCells.Add(cellId);
                    }
                }
            }

            DateTime now = DateTime.UtcNow;
            HashSet<int> seenCells = new HashSet<int>();
            foreach (int cellId in this.cells.EnumeratePopulatedCells())
            {
                seenCells.Add(cellId);
                CellHeat heat = this.ResolveHeat(cellId, combatHotCells);
                CellHeat previousHeat;
                bool isNewCell = !this.heatByCell.TryGetValue(cellId, out previousHeat);
                if (isNewCell || previousHeat != heat)
                {
                    this.LogHeatChange(cellId, previousHeat, heat, isNewCell);
                }

                this.heatByCell[cellId] = heat;
                this.UpdateColdTimer(cellId, heat, now);

                if (!this.ShouldTickCell(cellId, heat, now))
                {
                    continue;
                }

                this.lastTickUtcByCell[cellId] = now;
                foreach (ICharacter dynel in this.cells.GetCharactersInCell(cellId))
                {
                    if (dynel != null)
                    {
                        processDynel(dynel, deltaTime);
                    }
                }
            }

            List<int> staleCells = new List<int>();
            foreach (int cellId in this.heatByCell.Keys)
            {
                if (!seenCells.Contains(cellId))
                {
                    staleCells.Add(cellId);
                }
            }

            foreach (int cellId in staleCells)
            {
                this.heatByCell.Remove(cellId);
                this.coldSinceUtcByCell.Remove(cellId);
                this.lastTickUtcByCell.Remove(cellId);
            }
        }

        private CellHeat ResolveHeat(int cellId, HashSet<int> combatHotCells)
        {
            if (combatHotCells.Contains(cellId))
            {
                return CellHeat.Hot;
            }

            int minDistance = int.MaxValue;
            foreach (int playerCell in this.playerCells)
            {
                int distance = this.ChebyshevDistance(cellId, playerCell);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            if (this.playerCells.Count == 0)
            {
                minDistance = int.MaxValue;
            }

            if (minDistance <= this.policy.HotNeighborLevel)
            {
                return CellHeat.Hot;
            }

            if (minDistance <= this.policy.WarmNeighborLevel)
            {
                return CellHeat.Warm;
            }

            DateTime coldSince;
            if (this.coldSinceUtcByCell.TryGetValue(cellId, out coldSince)
                && (DateTime.UtcNow - coldSince).TotalSeconds >= this.policy.CellSleepTimeSeconds)
            {
                return CellHeat.Asleep;
            }

            return CellHeat.Cold;
        }

        private void UpdateColdTimer(int cellId, CellHeat heat, DateTime now)
        {
            if (heat == CellHeat.Hot || heat == CellHeat.Warm)
            {
                this.coldSinceUtcByCell.Remove(cellId);
                return;
            }

            if (!this.coldSinceUtcByCell.ContainsKey(cellId))
            {
                this.coldSinceUtcByCell[cellId] = now;
            }
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
                    return this.heartbeatCounter % 2 == 0;
                case CellHeat.Cold:
                    DateTime lastTick;
                    if (!this.lastTickUtcByCell.TryGetValue(cellId, out lastTick))
                    {
                        return true;
                    }

                    return (now - lastTick).TotalSeconds >= 1.0;
                default:
                    return false;
            }
        }

        private int ChebyshevDistance(int cellA, int cellB)
        {
            if (this.layout.IsIndoor)
            {
                return int.MaxValue;
            }

            this.layout.GetCellCoords(cellA, out int ax, out int az);
            this.layout.GetCellCoords(cellB, out int bx, out int bz);
            return Math.Max(Math.Abs(ax - bx), Math.Abs(az - bz));
        }

        private void LogHeatChange(int cellId, CellHeat previousHeat, CellHeat newHeat, bool isNewCell)
        {
            this.layout.GetCellCoords(cellId, out int ix, out int iz);
            string previousLabel = isNewCell ? "new" : previousHeat.ToString();
            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} cell {1} ({2},{3}) heat {4} -> {5}",
                    this.layout.PlayfieldId,
                    cellId,
                    ix,
                    iz,
                    previousLabel,
                    newHeat));
        }
    }
}
