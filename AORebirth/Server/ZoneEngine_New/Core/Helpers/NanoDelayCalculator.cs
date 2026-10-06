namespace ZoneEngine_New.Core.Helpers
{
    using System;

    /// <summary>
    /// Nano cast time and post-cast recharge, in centiseconds.
    /// Cast time takes half of nano initiative, and AggDef, off the template delay (the client's formula, see Reduce). Initiative above <see cref="InitiativeSoftCap"/> adds only one
    /// sixth of the extra. A defensive slider can run longer than the template. The result floors
    /// at 0, then at the template cap when that cap is set.
    /// Recharge is the template delay alone.
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

        /// <summary>Template recharge delay, in centiseconds. The client waits this value as-is.</summary>
        public static int RechargeTimeCentiseconds(int rechargeDelayCentiseconds)
            => Math.Max(0, rechargeDelayCentiseconds);

        /// <summary>
        /// The client's own cast time (Gamecode.dll 0x100511fd): initiative factor = init * 0.5 up to 1200, else
        /// (init - 1200) / 6 + 600 (negative init included); seconds = (delay - factor - AggDef) / 100, floored at 0, then
        /// raised to the template
        /// cap when it has one. Float math as the client does, rounded to whole centiseconds; AggDef is not clamped.
        /// </summary>
        static int Reduce(int delay, int cap, int aggDef, int nanoInitiative)
        {
            if (delay <= 0)
                return 0;

            // Not floored at 0: negative initiative lengthens the cast on the client (NanoCInit -639 adds 3.2 s).
            double initiative = nanoInitiative;
            double initFactor = initiative <= InitiativeSoftCap
                ? initiative * 0.5
                : (initiative - InitiativeSoftCap) / 6.0 + InitiativeSoftCap / 2;

            double result = delay - initFactor - aggDef;
            if (result < 0)
                result = 0;
            if (cap > 0 && result < cap)
                result = cap;

            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }
    }
}
