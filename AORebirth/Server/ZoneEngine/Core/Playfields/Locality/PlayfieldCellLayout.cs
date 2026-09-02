namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;

    using Utility;

    internal interface IPlayfieldCellLayout
    {
        int PlayfieldId { get; }

        bool IsIndoor { get; }

        int NumZonesX { get; }

        int NumZonesZ { get; }

        float CellWorldSize { get; }

        bool TryGetCellId(Coordinate worldPosition, out int cellId);

        void GetCellCoords(int cellId, out int ix, out int iz);

        int GetCellId(int ix, int iz);

        void CollectNeighbors(int cellId, int radius, List<int> results);
    }

    /// <summary>
    /// Selects the cell layout from the playfield GameData. Playfields without an extracted
    /// ground tilemap, and tilemaps without a usable chunk grid, run as a single indoor cell.
    /// </summary>
    internal static class PlayfieldCellLayoutFactory
    {
        internal static IPlayfieldCellLayout Create(int playfieldId, PlayfieldMetaData metaData)
        {
            int numZonesX;
            int numZonesZ;
            float cellWorldSize;
            if (metaData == null
                || !metaData.TryGetOutdoorGrid(out numZonesX, out numZonesZ, out cellWorldSize))
            {
                return new IndoorCellLayout(playfieldId);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} locality outdoor grid {1}x{2} cellWorldSize={3}",
                    playfieldId,
                    numZonesX,
                    numZonesZ,
                    cellWorldSize));

            return new OutdoorCellLayout(playfieldId, numZonesX, numZonesZ, cellWorldSize);
        }
    }

    internal sealed class IndoorCellLayout : IPlayfieldCellLayout
    {
        internal IndoorCellLayout(int playfieldId)
        {
            PlayfieldId = playfieldId;
        }

        public int PlayfieldId { get; }

        public bool IsIndoor
        {
            get { return true; }
        }

        public int NumZonesX
        {
            get { return 0; }
        }

        public int NumZonesZ
        {
            get { return 0; }
        }

        public float CellWorldSize
        {
            get { return 0f; }
        }

        public bool TryGetCellId(Coordinate worldPosition, out int cellId)
        {
            cellId = -1;
            return false;
        }

        public void GetCellCoords(int cellId, out int ix, out int iz)
        {
            ix = 0;
            iz = 0;
        }

        public int GetCellId(int ix, int iz)
        {
            return -1;
        }

        public void CollectNeighbors(int cellId, int radius, List<int> results)
        {
            results.Clear();
        }
    }

    internal sealed class OutdoorCellLayout : IPlayfieldCellLayout
    {
        internal OutdoorCellLayout(int playfieldId, int numZonesX, int numZonesZ, float cellWorldSize)
        {
            PlayfieldId = playfieldId;
            NumZonesX = numZonesX;
            NumZonesZ = numZonesZ;
            CellWorldSize = cellWorldSize;
        }

        public int PlayfieldId { get; }

        public bool IsIndoor
        {
            get { return false; }
        }

        public int NumZonesX { get; }

        public int NumZonesZ { get; }

        public float CellWorldSize { get; }

        public bool TryGetCellId(Coordinate worldPosition, out int cellId)
        {
            if (CellWorldSize <= 0f || NumZonesX <= 0 || NumZonesZ <= 0)
            {
                cellId = -1;
                return false;
            }

            int ix = (int)Math.Floor(worldPosition.x / CellWorldSize);
            int iz = (int)Math.Floor(worldPosition.z / CellWorldSize);
            if (ix < 0 || iz < 0 || ix >= NumZonesX || iz >= NumZonesZ)
            {
                cellId = -1;
                return false;
            }

            cellId = GetCellId(ix, iz);
            return true;
        }

        public void GetCellCoords(int cellId, out int ix, out int iz)
        {
            ix = cellId % NumZonesX;
            iz = cellId / NumZonesX;
        }

        public int GetCellId(int ix, int iz)
        {
            return (iz * NumZonesX) + ix;
        }

        public void CollectNeighbors(int cellId, int radius, List<int> results)
        {
            results.Clear();
            if (radius < 0 || NumZonesX <= 0 || NumZonesZ <= 0)
            {
                return;
            }

            GetCellCoords(cellId, out int cx, out int cz);
            int minX = Math.Max(0, cx - radius);
            int maxX = Math.Min(NumZonesX - 1, cx + radius);
            int minZ = Math.Max(0, cz - radius);
            int maxZ = Math.Min(NumZonesZ - 1, cz + radius);

            for (int iz = minZ; iz <= maxZ; iz++)
            {
                for (int ix = minX; ix <= maxX; ix++)
                {
                    results.Add(GetCellId(ix, iz));
                }
            }
        }
    }
}
