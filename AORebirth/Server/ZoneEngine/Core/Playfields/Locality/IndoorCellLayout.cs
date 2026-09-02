namespace ZoneEngine.Core.Playfields.Locality
{
    using System.Collections.Generic;

    using AORebirth.Core.Vector;

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
}
