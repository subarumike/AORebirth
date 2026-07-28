namespace AORebirth.Core.Items
{
    using System.Collections.Generic;

    /// <summary>
    /// Test-only dependency seam for the unrelated loot helper that shares the legacy shape
    /// catalog source file. ACG catalog tests never execute the loot path.
    /// </summary>
    internal static class ItemLoader
    {
        internal static readonly Dictionary<int, object> ItemList =
            new Dictionary<int, object>();
    }
}

namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Test-only dependency seam for runtime instance-to-source mapping. Factory fixtures pass
    /// captured source PF2 values directly, so no runtime allocation mapping is needed.
    /// </summary>
    internal static class MissionInstanceService
    {
        internal static bool TryGetShapeSource(int playfieldInstance, out int sourcePlayfield2)
        {
            sourcePlayfield2 = 0;
            return false;
        }
    }
}
