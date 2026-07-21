namespace ZoneEngine.Core.Thrak.Quests
{
    #region Usings ...

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260718-185306: Insignia of Thrak (214789) + Ancient Device/Analyzer (214998)
    /// → Ancient Pattern Analyzer favored by the Chosen One (214785). Delete both inputs.
    /// </summary>
    internal static class ThrakGardenKeyCombineRules
    {
        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            if (IsInsignia(sourceHighId) && IsAncientDevice(targetHighId))
            {
                return CreateEntry(sourceHighId, targetHighId);
            }

            // Allow reverse drag order too (error text mentions order).
            if (IsAncientDevice(sourceHighId) && IsInsignia(targetHighId))
            {
                return CreateEntry(sourceHighId, targetHighId);
            }

            return null;
        }

        private static bool IsInsignia(int itemId)
        {
            return itemId == ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId;
        }

        private static bool IsAncientDevice(int itemId)
        {
            return itemId == ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                   || itemId == ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId;
        }

        private static TradeSkillEntry CreateEntry(int id1, int id2)
        {
            return new TradeSkillEntry
                   {
                       ID1 = id1,
                       ID2 = id2,
                       DeleteFlag = 3, // delete source and target (capture DeleteItem both slots)
                       IsImplant = false,
                       MaxBump = 0,
                       MaxXP = 0,
                       MinTargetQL = 0,
                       MinXP = 0,
                       QLRangePercent = 0,
                       ResultLowId = ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId,
                       ResultHighId = ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId,
                       Skills = new System.Collections.Generic.List<TradeSkillSkill>()
                   };
        }
    }
}
