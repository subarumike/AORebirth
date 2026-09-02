namespace ZoneEngine.Core.Playfields.Locality
{
    using System.Collections.Generic;

    using AORebirth.Core.Vector;

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
}
