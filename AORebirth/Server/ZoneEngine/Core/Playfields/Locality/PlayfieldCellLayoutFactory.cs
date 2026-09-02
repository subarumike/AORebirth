namespace ZoneEngine.Core.Playfields.Locality
{
    using System.Globalization;

    using AORebirth.Core.GameData;

    using Utility;

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
}
