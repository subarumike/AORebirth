namespace ZoneEngine.Core.Playfields.Locality
{
    using System.Collections.Generic;

    internal interface ICellSurfaceLoader
    {
        void OnCellsFound(IReadOnlyList<int> cellIds);

        void OnCellsLost(IReadOnlyList<int> cellIds);

        void Clear();
    }
}
