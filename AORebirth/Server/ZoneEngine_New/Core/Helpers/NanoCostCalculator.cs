namespace ZoneEngine_New.Core.Helpers
{
    using System;

    /// <summary>
    /// Nano point cost after <c>NPCostModifier</c> (stat 318), which is a percentage reduction.
    /// The reduction is capped server-side so stacked cost buffs cannot make casting free.
    /// </summary>
    public static class NanoCostCalculator
    {
        /// <summary>Largest share of a nano's cost that modifiers may remove.</summary>
        public const int MaxReductionPercent = 50;

        public static int Compute(int baseCost, int npCostModifierPercent)
        {
            if (baseCost <= 0)
                return 0;

            int reduction = Math.Clamp(npCostModifierPercent, 0, MaxReductionPercent);
            if (reduction == 0)
                return baseCost;

            int cost = (int)Math.Round(baseCost * (100 - reduction) / 100.0, MidpointRounding.AwayFromZero);
            return Math.Max(1, cost);
        }
    }
}
