namespace ZoneEngine.Core
{
    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using Utility;

    /// <summary>
    /// Capture-backed Mongo Slam identity (20260719-Rex-Markus-stone / nanos.dat).
    /// Real slam effect: nano 100198 — Hit +12 HP + AreaCastNano(100194, 20) TauntNpc.
    /// Nested 100194: TauntNpc 2000/3000/4000.
    /// Nano 287046 is Composite Utility Expertise (Modify skill buffs, strain 51) — not Mongo Slam.
    /// Casting 100198 already runs those OnUse events in PlayerController; do not inject slam
    /// effects onto unrelated buffs.
    /// </summary>
    internal static class MongoSlamRuntimeService
    {
        internal const int MongoSlamEffectNanoId = 100198;

        internal const int MongoSlamNestedTauntNanoId = 100194;

        /// <summary>
        /// Misidentified historically as uploaded Mongo Slam; this is Composite Utility Expertise.
        /// Kept only so callers can reject the wrong mapping explicitly.
        /// </summary>
        internal const int CompositeUtilityExpertiseNanoId = 287046;

        internal static bool IsMongoSlamNano(int nanoId)
        {
            return nanoId == MongoSlamEffectNanoId;
        }

        /// <summary>
        /// Intentionally does not apply slam/taunt for Composite Utility Expertise (287046).
        /// Mongo Slam (100198) is applied solely via its nanos.dat OnUse chain.
        /// </summary>
        internal static void ApplyCaptureBackedSlamEffects(Character caster, int castNanoId)
        {
            if (caster == null)
            {
                return;
            }

            if (castNanoId == CompositeUtilityExpertiseNanoId)
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "MongoSlam skip: nano "
                    + castNanoId
                    + " is Composite Utility Expertise, not Mongo Slam");
            }
        }

        /// <summary>
        /// No HoT injection. Previous HoT was keyed to 287046/strain 51 (wrong nano).
        /// </summary>
        internal static bool ProcessHotTick(ICharacter character)
        {
            return false;
        }
    }
}
