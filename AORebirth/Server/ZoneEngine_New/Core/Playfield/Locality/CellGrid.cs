namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;

    using ZoneEngine_New.Core.Entities;

    internal sealed class CellGrid
    {
        internal const int NonLocalCellId = -1;

        private readonly Dictionary<int, Cell> _cells = [];
        private readonly float _cellWorldSize;
        private readonly int _numZonesX;
        private readonly int _numZonesZ;
        private readonly bool _outdoor;

        internal CellGrid(PlayfieldMetaData? metaData, int visibilityNeighborLevel)
        {
            if (metaData != null && metaData.TryGetOutdoorGrid(out int zonesX, out int zonesZ, out float cellSize))
            {
                _outdoor = true;
                _numZonesX = Math.Max(1, zonesX);
                _numZonesZ = Math.Max(1, zonesZ);
                _cellWorldSize = cellSize;
                int cellCount = _numZonesX * _numZonesZ;
                for (int i = 0; i < cellCount; i++)
                {
                    _cells[i] = new Cell(i, this, visibilityNeighborLevel);
                }
            }
            else
            {
                _outdoor = false;
                _numZonesX = 1;
                _numZonesZ = 1;
                _cellWorldSize = PlayfieldMetaData.CellSize;
                _cells[0] = new Cell(0, this, visibilityNeighborLevel);
            }
        }

        internal bool IsOutdoor => _outdoor;

        internal int NumZonesX => _numZonesX;

        internal int NumZonesZ => _numZonesZ;

        internal bool TryResolveCell(Vector3 position, out Cell cell)
        {
            if (!_outdoor)
            {
                cell = _cells[0];
                return true;
            }

            if (!TryGetCellId(position, out int cellId))
            {
                cell = null!;
                return false;
            }

            cell = _cells[cellId];
            return true;
        }

        internal bool TryGetCellId(Vector3 position, out int cellId)
        {
            if (!_outdoor)
            {
                cellId = 0;
                return true;
            }

            if (_cellWorldSize <= 0f || _numZonesX <= 0 || _numZonesZ <= 0)
            {
                cellId = NonLocalCellId;
                return false;
            }

            int ix = (int)Math.Floor(position.xf / _cellWorldSize);
            int iz = (int)Math.Floor(position.zf / _cellWorldSize);
            if (ix < 0 || iz < 0 || ix >= _numZonesX || iz >= _numZonesZ)
            {
                cellId = NonLocalCellId;
                return false;
            }

            cellId = GetCellId(ix, iz);
            return true;
        }

        internal void GetCellCoords(int cellId, out int ix, out int iz)
        {
            if (!_outdoor || _numZonesX <= 0)
            {
                ix = 0;
                iz = 0;
                return;
            }

            ix = cellId % _numZonesX;
            iz = cellId / _numZonesX;
        }

        internal int GetCellId(int ix, int iz) => (iz * _numZonesX) + ix;

        internal bool TryGetCell(int cellId, out Cell cell) => _cells.TryGetValue(cellId, out cell!);

        internal void CollectNeighbors(int cellId, int radius, List<int> results)
        {
            results.Clear();
            if (!_outdoor || radius < 0 || _numZonesX <= 0 || _numZonesZ <= 0 || cellId < 0)
            {
                return;
            }

            GetCellCoords(cellId, out int cx, out int cz);
            int minX = Math.Max(0, cx - radius);
            int maxX = Math.Min(_numZonesX - 1, cx + radius);
            int minZ = Math.Max(0, cz - radius);
            int maxZ = Math.Min(_numZonesZ - 1, cz + radius);

            for (int iz = minZ; iz <= maxZ; iz++)
            {
                for (int ix = minX; ix <= maxX; ix++)
                {
                    results.Add(GetCellId(ix, iz));
                }
            }
        }

        internal int ChebyshevDistance(int cellA, int cellB)
        {
            if (!_outdoor)
            {
                return int.MaxValue;
            }

            GetCellCoords(cellA, out int ax, out int az);
            GetCellCoords(cellB, out int bx, out int bz);
            return Math.Max(Math.Abs(ax - bx), Math.Abs(az - bz));
        }

        internal IEnumerable<int> EnumeratePopulatedCells()
        {
            foreach (KeyValuePair<int, Cell> pair in _cells)
            {
                if (pair.Value.OccupantCount > 0)
                {
                    yield return pair.Key;
                }
            }
        }

        internal IEnumerable<Dynel> OccupantsInCell(int cellId)
        {
            if (!_cells.TryGetValue(cellId, out Cell? cell))
            {
                yield break;
            }

            foreach (Dynel dynel in cell.Occupants)
            {
                yield return dynel;
            }
        }
    }
}
