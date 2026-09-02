namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;

    internal sealed class PlayfieldCellResourceHub
    {
        private readonly List<ICellSurfaceLoader> loaders = new List<ICellSurfaceLoader>();

        internal void AddLoader(ICellSurfaceLoader loader)
        {
            if (loader != null)
            {
                this.loaders.Add(loader);
            }
        }

        internal void NotifyCellsFound(IReadOnlyList<int> cellIds)
        {
            if (cellIds == null || cellIds.Count == 0)
            {
                return;
            }

            foreach (ICellSurfaceLoader loader in this.loaders)
            {
                loader.OnCellsFound(cellIds);
            }
        }

        internal void NotifyCellsLost(IReadOnlyList<int> cellIds)
        {
            if (cellIds == null || cellIds.Count == 0)
            {
                return;
            }

            foreach (ICellSurfaceLoader loader in this.loaders)
            {
                loader.OnCellsLost(cellIds);
            }
        }

        internal void Clear()
        {
            foreach (ICellSurfaceLoader loader in this.loaders)
            {
                loader.Clear();
            }
        }
    }
}
