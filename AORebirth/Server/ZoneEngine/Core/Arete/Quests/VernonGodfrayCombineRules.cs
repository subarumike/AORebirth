namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Items;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260721-Vernon-Godfray: UseItemOnItem Hacker Tool (87810) +
    /// Omni-Tek Technical Library (248377) → Hacked Technical Library (295756).
    /// Deletes library only (flag 2) so Hacker Tool remains for Shipping Manifest Terminal.
    /// </summary>
    internal static class VernonGodfrayCombineRules
    {
        internal const int HackerToolItemId = 87810;

        internal const int OmniTekTechnicalLibraryItemId = 248377;

        internal const int HackedTechnicalLibraryItemId = 295756;

        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            // DeleteFlag applies to TradeSkillSource/Target slots, not recipe ID1/ID2.
            // Capture: keep Hacker Tool, consume Omni-Tek Technical Library only.
            if (IsHackerTool(sourceHighId) && IsOmniTekLibrary(targetHighId))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    HackedTechnicalLibraryItemId,
                    HackedTechnicalLibraryItemId,
                    deleteFlag: 2);
            }

            if (IsOmniTekLibrary(sourceHighId) && IsHackerTool(targetHighId))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    HackedTechnicalLibraryItemId,
                    HackedTechnicalLibraryItemId,
                    deleteFlag: 1);
            }

            return null;
        }

        internal static int SourceProcessBonus(int itemHighId)
        {
            if (IsHackerTool(itemHighId) || IsOmniTekLibrary(itemHighId))
            {
                return 1;
            }

            return 0;
        }

        internal static int TargetProcessBonus(int itemHighId)
        {
            return SourceProcessBonus(itemHighId);
        }

        internal static bool IsHackedTechnicalLibrary(int lowId, int highId)
        {
            return lowId == HackedTechnicalLibraryItemId || highId == HackedTechnicalLibraryItemId;
        }

        private static bool IsHackerTool(int id)
        {
            return id == HackerToolItemId;
        }

        private static bool IsOmniTekLibrary(int id)
        {
            return id == OmniTekTechnicalLibraryItemId;
        }

        private static TradeSkillEntry CreateEntry(
            int id1,
            int id2,
            int resultLowId,
            int resultHighId,
            int deleteFlag)
        {
            int low = ResolveTemplateId(resultLowId, resultHighId);
            int high = ResolveTemplateId(resultHighId, low);
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
