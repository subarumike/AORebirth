namespace ZoneEngine_New.Core.Helpers
{
    using System;

    /// <summary>
    /// Cast time and post-cast recharge for a nano, in centiseconds.
    /// Both use the same reduction: aggressiveness above the defensive midpoint and half of
    /// nano initiative come off the template delay, floored at the template's cap.
    /// </summary>
    public static class NanoDelayCalculator
    {
        /// <summary>AggDef midpoint; full defensive (0) adds delay, full aggressive (100) removes it.</summary>
        public const int AggDefNeutral = 25;

        /// <summary>Initiative above this contributes at a third of its value.</summary>
        public const int InitiativeSoftCap = 1200;

        public static int AttackTimeCentiseconds(
            int attackDelayCentiseconds,
            int attackDelayCapCentiseconds,
            int aggDef,
            int nanoInitiative)
            => Reduce(attackDelayCentiseconds, attackDelayCapCentiseconds, aggDef, nanoInitiative);

        public static int RechargeTimeCentiseconds(
            int rechargeDelayCentiseconds,
            int rechargeDelayCapCentiseconds,
            int aggDef,
            int nanoInitiative)
            => Reduce(rechargeDelayCentiseconds, rechargeDelayCapCentiseconds, aggDef, nanoInitiative);

        static int Reduce(int delay, int cap, int aggDef, int nanoInitiative)
        {
            if (delay <= 0)
                return 0;

            int initiative = Math.Max(0, nanoInitiative);
            if (initiative > InitiativeSoftCap)
                initiative = ((initiative - InitiativeSoftCap) / 3) + InitiativeSoftCap;

            int reduction = (aggDef - AggDefNeutral) + (initiative / 2);
            int floor = cap > 0 ? Math.Min(cap, delay) : 0;

            // Reduction can be negative (defensive stance), so clamp to the template delay too.
            return Math.Clamp(delay - reduction, floor, delay);
        }
    }
}
