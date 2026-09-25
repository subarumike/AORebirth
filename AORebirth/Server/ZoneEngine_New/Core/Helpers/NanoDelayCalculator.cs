namespace ZoneEngine_New.Core.Helpers
{
    using System;

    /// <summary>
    /// Cast time and post-cast recharge for a nano, in centiseconds.
    /// Half of nano initiative, and AggDef clamped to <see cref="AggDefMin"/>..<see cref="AggDefMax"/>,
    /// come off the template delay. Initiative above <see cref="InitiativeSoftCap"/> adds only one
    /// sixth of the extra. A defensive slider can run longer than the template. The result floors
    /// at 0, then at the template cap when that cap is set.
    /// </summary>
    public static class NanoDelayCalculator
    {
        public const int AggDefMin = -100;

        public const int AggDefMax = 100;

        /// <summary>Initiative above this contributes at one sixth of the extra amount.</summary>
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
            int initFactor = initiative <= InitiativeSoftCap
                ? initiative / 2
                : (initiative - InitiativeSoftCap) / 6 + (InitiativeSoftCap / 2);

            int result = delay - initFactor - Math.Clamp(aggDef, AggDefMin, AggDefMax);
            if (result < 0)
                result = 0;
            if (cap > 0 && result < cap)
                result = cap;

            return result;
        }
    }
}
