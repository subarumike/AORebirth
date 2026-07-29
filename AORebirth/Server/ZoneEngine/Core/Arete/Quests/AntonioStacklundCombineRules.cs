namespace ZoneEngine.Core.Arete.Quests
{
    using System.Collections.Generic;

    using AORebirth.Core.Items;

    using ZoneEngine.Core;

    /// <summary>
    /// Capture 20260726-Antonio-Stacklund Adaptation Factory weapon/gadget recipes.
    /// </summary>
    internal static class AntonioStacklundCombineRules
    {
        private static readonly int[][] Recipes =
            {
                new[] { 248306, 248315, 248316 },
                new[] { 248316, 121569, 248347 },
                new[] { 248306, 248322, 248321 },
                new[] { 248306, 248334, 248335 },
                new[] { 248321, 248335, 248375 },
                new[] { 248306, 248333, 248332 },
                new[] { 248332, 248325, 248373 },
                new[] { 248306, 248318, 248317 },
                new[] { 248306, 248307, 248308 },
                new[] { 248308, 248317, 248374 },
                new[] { 248306, 248328, 248327 },
                new[] { 248327, 121564, 248341 },
                new[] { 248306, 248325, 248326 },
                new[] { 248326, 218403, 248352 },
                new[] { 248332, 248339, 248354 },
                new[] { 248308, 121567, 248343 },
                new[] { 248321, 218395, 248350 },
                new[] { 248306, 248319, 248320 },
                new[] { 248320, 248340, 248349 },
                new[] { 248335, 248338, 248346 },
                new[] { 248306, 248330, 248331 },
                new[] { 248331, 218406, 248353 },
                new[] { 248317, 121568, 248348 },
                new[] { 150922, 121570, 248345 },
                new[] { 248306, 248310, 248312 },
                new[] { 248312, 121571, 248344 },
                new[] { 248306, 248323, 248324 },
                new[] { 248324, 218404, 248351 },
                new[] { 248326, 121565, 301071 },
                new[] { 248308, 121564, 248355 },
                new[] { 248321, 302163, 302602 },
            };

        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            foreach (int[] recipe in Recipes)
            {
                int left = recipe[0];
                int right = recipe[1];
                int result = recipe[2];
                if ((sourceHighId == left && targetHighId == right)
                    || (sourceHighId == right && targetHighId == left))
                {
                    return CreateEntry(sourceHighId, targetHighId, result);
                }
            }

            return null;
        }

        internal static int SourceProcessBonus(int itemHighId)
        {
            foreach (int[] recipe in Recipes)
            {
                if (itemHighId == recipe[0] || itemHighId == recipe[1])
                {
                    return 1;
                }
            }

            return 0;
        }

        internal static int TargetProcessBonus(int itemHighId)
        {
            return SourceProcessBonus(itemHighId);
        }

        /// <summary>
        /// Capture 20260726-Antonio-1 / antonio-2: all Adaptation Factory results are
        /// Overflow QL1 TemplateAction grants (never AddTemplate).
        /// </summary>
        internal static bool IsCombineResult(int lowId, int highId)
        {
            foreach (int[] recipe in Recipes)
            {
                int result = recipe[2];
                if (lowId == result || highId == result)
                {
                    return true;
                }
            }

            return false;
        }

        private static TradeSkillEntry CreateEntry(int id1, int id2, int resultId)
        {
            int resolved = resultId;
            if (ItemLoader.ItemList != null && !ItemLoader.ItemList.ContainsKey(resolved))
            {
                // keep captured id even if missing; TradeSkillReceiver will fail soft
            }

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
                       ResultLowId = resolved,
                       ResultHighId = resolved,
                       Skills = new List<TradeSkillSkill>()
                   };
        }
    }
}
