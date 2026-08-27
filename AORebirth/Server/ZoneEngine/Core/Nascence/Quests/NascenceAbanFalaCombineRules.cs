namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260822-224319: Insignia of Aban (214788) + Ancient Device (214998/214783)
    /// → Ancient Pattern Analyzer favored by the Faithful (214784). Player crafts blue device.
    /// </summary>
    internal static class NascenceAbanFalaCombineRules
    {
        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            if (IsInsignia(sourceHighId) && IsAncientDevice(targetHighId))
            {
                return CreateEntry(sourceHighId, targetHighId);
            }

            if (IsAncientDevice(sourceHighId) && IsInsignia(targetHighId))
            {
                return CreateEntry(sourceHighId, targetHighId);
            }

            return null;
        }

        private static bool IsInsignia(int itemId)
        {
            return itemId == NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId;
        }

        private static bool IsAncientDevice(int itemId)
        {
            return itemId == NascenceAbanFalaInteractionRules.AncientDeviceItemId
                   || itemId == NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId;
        }

        private static TradeSkillEntry CreateEntry(int id1, int id2)
        {
            return new TradeSkillEntry
                   {
                       ID1 = id1,
                       ID2 = id2,
                       DeleteFlag = 3,
                       IsImplant = false,
                       MaxBump = 0,
                       MaxXP = 0,
                       MinTargetQL = 0,
                       MinXP = 0,
                       QLRangePercent = 0,
                       ResultLowId = NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId,
                       ResultHighId = NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId,
                       Skills = new List<TradeSkillSkill>()
                   };
        }
    }
}
