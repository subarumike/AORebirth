namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;
    using AORebirth.World.Collision;

    using ZoneEngine_New.Core.Entities;

    internal sealed class CellGrid
    {
        internal const int NonLocalCellId = -1;

        private readonly Dictionary<int, Cell> _cells = [];
        private readonly float _cellWorldSize;
        private readonly float _worldSizeX;
        private readonly float _worldSizeZ;
        private readonly int _numZonesX;
        private readonly int _numZonesZ;
        private readonly bool _outdoor;
        private readonly int _visibilityNeighborLevel;
        private DungeonRoomBounds[] _rooms = [];
        private Dictionary<int, DungeonRoomBounds> _roomById = [];
        private IReadOnlyDictionary<int, IReadOnlyList<int>> _roomLinks = new Dictionary<int, IReadOnlyList<int>>();
        private Dictionary<int, Dictionary<int, int>> _roomHops = [];

        internal CellGrid(PlayfieldMetaData? metaData, int visibilityNeighborLevel)
        {
            if (metaData != null
                && metaData.TryGetOutdoorGrid(out int zonesX, out int zonesZ, out float cellSize)
                && metaData.TryGetOutdoorWorldSize(out float worldSizeX, out float worldSizeZ))
            {
                _outdoor = true;
                _numZonesX = Math.Max(1, zonesX);
                _numZonesZ = Math.Max(1, zonesZ);
                _cellWorldSize = cellSize;
                _worldSizeX = worldSizeX;
                _worldSizeZ = worldSizeZ;
                _visibilityNeighborLevel = visibilityNeighborLevel;
                int cellCount = _numZonesX * _numZonesZ;
                for (int i = 0; i < cellCount; i++)
                    _cells[i] = new Cell(i, this, visibilityNeighborLevel);
            }
            else
            {
                _outdoor = false;
                _numZonesX = 1;
                _numZonesZ = 1;
                _cellWorldSize = PlayfieldMetaData.CellSize;
                _worldSizeX = 0f;
                _worldSizeZ = 0f;
                _visibilityNeighborLevel = visibilityNeighborLevel;
                _cells[0] = new Cell(0, this, visibilityNeighborLevel);
            }
        }

        internal bool IsOutdoor => _outdoor;

        internal bool IsDungeon => _rooms.Length > 0;

        internal int NumZonesX => _numZonesX;

        /// <summary>World size of one cell (outdoor grid).</summary>
        internal float CellWorldSize => _cellWorldSize;

        internal int NumZonesZ => _numZonesZ;

        internal float WorldSizeX => _worldSizeX;

        internal float WorldSizeZ => _worldSizeZ;

        /// <summary>
        /// Outdoor XZ must lie in <c>[0, worldSize)</c>. Indoor layouts always accept.
        /// </summary>
        internal bool ContainsWorldPosition(float x, float z)
        {
            if (!_outdoor)
                return true;

            return x >= 0f
                && z >= 0f
                && x < _worldSizeX
                && z < _worldSizeZ;
        }

        internal void ApplyDungeonRooms(DungeonWorldLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);
            IReadOnlyList<DungeonRoomBounds> rooms = layout.Rooms;
            if (_outdoor || rooms.Count == 0)
                return;

            _rooms = new DungeonRoomBounds[rooms.Count];
            _roomById = new Dictionary<int, DungeonRoomBounds>(rooms.Count);
            _roomLinks = layout.RoomLinks;
            _cells.Clear();
            for (int i = 0; i < rooms.Count; i++)
            {
                _rooms[i] = rooms[i];
                int id = rooms[i].Index;
                _roomById.TryAdd(id, rooms[i]);
                if (!_cells.ContainsKey(id))
                    _cells[id] = new Cell(id, this, _visibilityNeighborLevel);
            }

            // Room-to-room door hops, for cell heat (a dungeon's distance is in rooms, not grid squares).
            _roomHops = new Dictionary<int, Dictionary<int, int>>(_cells.Count);
            var reached = new List<int>();
            foreach (int room in _cells.Keys)
            {
                var hops = new Dictionary<int, int> { [room] = 0 };
                reached.Clear();
                reached.Add(room);
                for (int i = 0; i < reached.Count; i++)
                {
                    if (!_roomLinks.TryGetValue(reached[i], out IReadOnlyList<int>? linked))
                        continue;
                    for (int j = 0; j < linked.Count; j++)
                    {
                        if (_cells.ContainsKey(linked[j]) && hops.TryAdd(linked[j], hops[reached[i]] + 1))
                            reached.Add(linked[j]);
                    }
                }

                _roomHops[room] = hops;
            }
        }

        /// <summary>
        /// The room a dynel in room <paramref name="currentRoom"/> is in after moving to <paramref name="position"/>, as
        /// the client picks it (Vehicle.dll RoomSpace_t, 0x100073e2): it stays in its room while still inside it, else
        /// takes the first linked room that holds it, and only then looks at every room. Room boxes overlap, so the
        /// room a dynel is already in wins.
        /// </summary>
        private int ResolveRoom(System.Numerics.Vector3 position, int currentRoom)
        {
            if (currentRoom >= 0 && _roomById.TryGetValue(currentRoom, out DungeonRoomBounds current))
            {
                if (current.Contains(position))
                    return currentRoom;

                if (_roomLinks.TryGetValue(currentRoom, out IReadOnlyList<int>? linked))
                {
                    for (int i = 0; i < linked.Count; i++)
                    {
                        if (_roomById.TryGetValue(linked[i], out DungeonRoomBounds next) && next.Contains(position))
                            return linked[i];
                    }
                }
            }

            return DungeonRoomCellResolver.Resolve(_rooms, position);
        }

        /// <summary>
        /// Resolves the cell for <paramref name="position"/>. Outdoor indices are floored then
        /// clamped into the grid so a registered dynel always has a cell.
        /// </summary>
        internal Cell ResolveCell(Vector3 position, int currentCellId = NonLocalCellId)
        {
            if (_rooms.Length > 0)
            {
                int roomId = ResolveRoom(new System.Numerics.Vector3(position.xf, position.yf, position.zf), currentCellId);
                if (_cells.TryGetValue(roomId, out Cell? dungeonCell))
                    return dungeonCell;

                foreach (KeyValuePair<int, Cell> pair in _cells)
                    return pair.Value;
            }

            if (!_outdoor)
                return _cells[0];

            TryGetCellId(position, out int cellId, clampToGrid: true);
            return _cells[cellId];
        }

        internal bool TryResolveCell(Vector3 position, out Cell cell)
        {
            cell = ResolveCell(position);
            return true;
        }

        internal bool TryGetCellId(Vector3 position, out int cellId) =>
            TryGetCellId(position, out cellId, clampToGrid: true);

        internal bool TryGetCellId(Vector3 position, out int cellId, bool clampToGrid)
        {
            if (_rooms.Length > 0)
            {
                cellId = DungeonRoomCellResolver.Resolve(
                    _rooms,
                    new System.Numerics.Vector3(position.xf, position.yf, position.zf));
                return true;
            }

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
            if (clampToGrid)
            {
                ix = Math.Clamp(ix, 0, _numZonesX - 1);
                iz = Math.Clamp(iz, 0, _numZonesZ - 1);
            }
            else if (ix < 0 || iz < 0 || ix >= _numZonesX || iz >= _numZonesZ)
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

        /// <summary>
        /// Legacy outdoor index: <c>_cellCountWidth * heightIndex + widthIndex</c>.
        /// </summary>
        internal int GetCellId(int ix, int iz) => (iz * _numZonesX) + ix;

        internal bool TryGetCell(int cellId, out Cell cell) => _cells.TryGetValue(cellId, out cell!);

        internal void CollectNeighbors(int cellId, int radius, List<int> results)
        {
            results.Clear();
            if (_rooms.Length > 0)
            {
                CollectLinkedRooms(cellId, radius, results);
                return;
            }

            if (!_outdoor || radius < 0 || _numZonesX <= 0 || _numZonesZ <= 0 || cellId < 0)
                return;

            GetCellCoords(cellId, out int cx, out int cz);
            int minX = Math.Max(0, cx - radius);
            int maxX = Math.Min(_numZonesX - 1, cx + radius);
            int minZ = Math.Max(0, cz - radius);
            int maxZ = Math.Min(_numZonesZ - 1, cz + radius);

            for (int iz = minZ; iz <= maxZ; iz++)
            {
                for (int ix = minX; ix <= maxX; ix++)
                    results.Add(GetCellId(ix, iz));
            }
        }

        /// <summary>A dungeon room and every room within <paramref name="radius"/> door links of it.</summary>
        private void CollectLinkedRooms(int roomId, int radius, List<int> results)
        {
            if (roomId < 0 || radius < 0 || !_cells.ContainsKey(roomId))
                return;

            results.Add(roomId);
            int frontierStart = 0;
            for (int depth = 0; depth < radius; depth++)
            {
                int frontierEnd = results.Count;
                for (int i = frontierStart; i < frontierEnd; i++)
                {
                    if (!_roomLinks.TryGetValue(results[i], out IReadOnlyList<int>? linked))
                        continue;
                    for (int j = 0; j < linked.Count; j++)
                    {
                        if (_cells.ContainsKey(linked[j]) && !results.Contains(linked[j]))
                            results.Add(linked[j]);
                    }
                }

                if (frontierEnd == results.Count)
                    return;
                frontierStart = frontierEnd;
            }
        }

        /// <summary>
        /// Cell distance for heat: grid squares outdoors (Chebyshev), door hops between dungeon rooms (rooms no door
        /// path joins are infinitely far). Other indoor playfields have no distance.
        /// </summary>
        internal int ChebyshevDistance(int cellA, int cellB)
        {
            if (_rooms.Length > 0)
            {
                return _roomHops.TryGetValue(cellA, out Dictionary<int, int>? hops) && hops.TryGetValue(cellB, out int distance)
                    ? distance
                    : int.MaxValue;
            }

            if (!_outdoor)
                return int.MaxValue;

            GetCellCoords(cellA, out int ax, out int az);
            GetCellCoords(cellB, out int bx, out int bz);
            return Math.Max(Math.Abs(ax - bx), Math.Abs(az - bz));
        }

        internal IEnumerable<int> EnumeratePopulatedCells()
        {
            foreach (KeyValuePair<int, Cell> pair in _cells)
            {
                if (pair.Value.OccupantCount > 0)
                    yield return pair.Key;
            }
        }

        internal IEnumerable<Dynel> OccupantsInCell(int cellId)
        {
            if (!_cells.TryGetValue(cellId, out Cell? cell))
                yield break;

            foreach (Dynel dynel in cell.Occupants)
                yield return dynel;
        }

        internal IEnumerable<Dynel> OccupantsInAllCells()
        {
            foreach (KeyValuePair<int, Cell> pair in _cells)
            {
                foreach (Dynel dynel in pair.Value.Occupants)
                    yield return dynel;
            }
        }
    }
}
