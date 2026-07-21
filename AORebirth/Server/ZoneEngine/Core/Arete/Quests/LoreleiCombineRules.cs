namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Items;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260721-loralei: Pet Cage (297366) + Lorelei's Reet (297369) → Pet Cage With a Reet (297367).
    /// </summary>
    internal static class LoreleiCombineRules
    {
        internal const int PetCageItemId = 297366;

        internal const int LoreleisReetItemId = 297369;

        internal const int PetCageWithReetItemId = 297367;

        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            if ((IsPetCage(sourceHighId) && IsLoreleisReet(targetHighId))
                || (IsLoreleisReet(sourceHighId) && IsPetCage(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    PetCageWithReetItemId,
                    PetCageWithReetItemId,
                    deleteFlag: 3);
            }

            return null;
        }

        internal static int SourceProcessBonus(int itemHighId)
        {
            if (IsPetCage(itemHighId) || IsLoreleisReet(itemHighId))
            {
                return 1;
            }

            return 0;
        }

        internal static int TargetProcessBonus(int itemHighId)
        {
            return SourceProcessBonus(itemHighId);
        }

        private static bool IsPetCage(int id)
        {
            return id == PetCageItemId;
        }

        private static bool IsLoreleisReet(int id)
        {
            return id == LoreleisReetItemId;
        }

        private static TradeSkillEntry CreateEntry(
            int id1,
            int id2,
            int resultLowId,
            int resultHighId,
            int deleteFlag)
        {
            int low = ResolveTemplateId(resultLowId, resultHighId);
            int high = ResolveTemplateId(resultLowId, resultHighId);
            return new TradeSkillEntry
                   {
                       ID1 = id1,
                       ID2 = id2,
                       DeleteFlag = deleteFlag,
                       IsImplant = false,
                       MaxBump = 0,
                       MaxXP = 0,
                       MinTargetQL = 0,
                       MinXP = 0,
                       QLRangePercent = 0,
                       ResultLowId = low,
                       ResultHighId = high,
                       Skills = new List<TradeSkillSkill>()
                   };
        }

        private static int ResolveTemplateId(int preferred, int fallback)
        {
            if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(preferred))
            {
                return preferred;
            }

            if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(fallback))
            {
                return fallback;
            }

            return preferred;
        }
    }
}
