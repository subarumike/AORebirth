namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Items;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260721-Mason tip recipes (cluster = Source, implant = Target):
    /// Agility Cluster Shiny Leg (101781/101782) + Basic Leg Implant (101261/101262) → 113127
    /// Stamina Cluster Bright Leg (101785/101786) + 113127/113128 → 113186
    /// Max Health Cluster Faded Leg (101807/101808) + 113186/113187 → 113440
    /// Live UseItemOnItem produced TemplateAction Overflow results at those ids.
    /// </summary>
    internal static class DoctorMasonCombineRules
    {
        internal const int AgilityClusterShinyLegLowId = 101781;

        internal const int AgilityClusterShinyLegHighId = 101782;

        internal const int BasicLegImplantLowId = 101261;

        internal const int BasicLegImplantHighId = 101262;

        internal const int Assemble1ResultLowId = 113127;

        internal const int Assemble1ResultHighId = 113128;

        internal const int StaminaClusterBrightLegLowId = 101785;

        internal const int StaminaClusterBrightLegHighId = 101786;

        internal const int Assemble2ResultLowId = 113186;

        internal const int Assemble2ResultHighId = 113187;

        internal const int MaxHealthClusterFadedLegLowId = 101807;

        internal const int MaxHealthClusterFadedLegHighId = 101808;

        internal const int Assemble3ResultLowId = 113440;

        internal const int Assemble3ResultHighId = 113441;

        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            // Tip order: cluster Source, implant Target. Accept reverse drag order.
            if ((IsAgilityClusterShinyLeg(sourceHighId) && IsBasicLegImplant(targetHighId))
                || (IsBasicLegImplant(sourceHighId) && IsAgilityClusterShinyLeg(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    Assemble1ResultLowId,
                    Assemble1ResultHighId,
                    deleteFlag: 3);
            }

            if ((IsStaminaClusterBrightLeg(sourceHighId) && IsAssemble1Result(targetHighId))
                || (IsAssemble1Result(sourceHighId) && IsStaminaClusterBrightLeg(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    Assemble2ResultLowId,
                    Assemble2ResultHighId,
                    deleteFlag: 3);
            }

            if ((IsMaxHealthClusterFadedLeg(sourceHighId) && IsAssemble2Result(targetHighId))
                || (IsAssemble2Result(sourceHighId) && IsMaxHealthClusterFadedLeg(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    Assemble3ResultLowId,
                    Assemble3ResultHighId,
                    deleteFlag: 3);
            }

            return null;
        }

        internal static int SourceProcessBonus(int itemHighId)
        {
            if (IsAgilityClusterShinyLeg(itemHighId)
                || IsBasicLegImplant(itemHighId)
                || IsStaminaClusterBrightLeg(itemHighId)
                || IsAssemble1Result(itemHighId)
                || IsMaxHealthClusterFadedLeg(itemHighId)
                || IsAssemble2Result(itemHighId))
            {
                return 1;
            }

            return 0;
        }

        internal static int TargetProcessBonus(int itemHighId)
        {
            return SourceProcessBonus(itemHighId);
        }

        /// <summary>
        /// Capture Overflow results: 113127, 113186, 113440 (low==high).
        /// </summary>
        internal static bool IsAssembleResult(int lowId, int highId)
        {
            return IsAssemble1Result(lowId)
                   || IsAssemble1Result(highId)
                   || IsAssemble2Result(lowId)
                   || IsAssemble2Result(highId)
                   || IsAssemble3Result(lowId)
                   || IsAssemble3Result(highId);
        }

        internal static bool IsAssemble1Result(int id)
        {
            return id == Assemble1ResultLowId || id == Assemble1ResultHighId;
        }

        internal static bool IsAssemble2Result(int id)
        {
            return id == Assemble2ResultLowId || id == Assemble2ResultHighId;
        }

        internal static bool IsAssemble3Result(int id)
        {
            return id == Assemble3ResultLowId || id == Assemble3ResultHighId;
        }

        private static bool IsAgilityClusterShinyLeg(int id)
        {
            return id == AgilityClusterShinyLegLowId || id == AgilityClusterShinyLegHighId;
        }

        private static bool IsBasicLegImplant(int id)
        {
            return id == BasicLegImplantLowId || id == BasicLegImplantHighId;
        }

        private static bool IsStaminaClusterBrightLeg(int id)
        {
            return id == StaminaClusterBrightLegLowId || id == StaminaClusterBrightLegHighId;
        }

        private static bool IsMaxHealthClusterFadedLeg(int id)
        {
            return id == MaxHealthClusterFadedLegLowId || id == MaxHealthClusterFadedLegHighId;
        }

        private static TradeSkillEntry CreateEntry(
            int id1,
            int id2,
            int resultLowId,
            int resultHighId,
            int deleteFlag)
        {
            // Capture Overflow results used low==high (113127/113127 etc.).
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
