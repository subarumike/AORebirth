namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture-backed SpellList visual effects (20260722-keeper-exect-nano + 20260731-172247).
    /// Ambient Restoration: named SpellList after CastNanoSpell pair (Effect 0xCF4A / nano 302365).
    /// Robot fire: paired GfxEffect SpellLists (0xCF26) with GfxValue 0xA8E3 then 0xA871
    /// (capture 20260731-172247 Malfunctioning Cleaning Robot), unique Effect.Instance each.
    /// </summary>
    internal static class CapturedSpellListVisualEffects
    {
        internal const int AmbientRestorationNanoId = 302365;

        internal const int AmbientRestorationChildNanoId = 300495;

        private const int AmbientSpellEffectIdentityType = 0x0000CF4A;

        private const int BurningRobotFireGfxEffectBase = unchecked((int)0x43DD5590);

        // Capture 20260731-172247: live sends A8E3 then A871 as a pair (same timestamp).
        private const int BurningRobotFireGfxValuePrimary = 0xA8E3;

        private const int BurningRobotFireGfxValueSecondary = 0xA871;

        private const double BurningRobotFireSpellListSeconds = 5.0;

        private const string BurningRobotName = "Burning Cleaning Robot";

        private const string AmbientRestorationNanoName = "Ambient Restoration";

        private static int nextBurningFireGfxInstance = BurningRobotFireGfxEffectBase;

        internal static void AnnounceAmbientRestoration(ICharacter character)
        {
            if (character == null || character.Playfield == null)
            {
                return;
            }

            try
            {
                // Capture: CriterionCount=1, GfxGreen/Blue = character Identity,
                // NanoName "Ambient Restoration" after Character (Int16 length).
                character.Playfield.Announce(
                    new SpellListMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Character = character.Identity,
                        NanoName = AmbientRestorationNanoName,
                        NanoEffects =
                            new[]
                            {
                                new NanoEffect
                                {
                                    Effect =
                                        new Identity
                                        {
                                            Type = (IdentityType)AmbientSpellEffectIdentityType,
                                            Instance = AmbientRestorationNanoId
                                        },
                                    Unknown1 = 4,
                                    CriterionCount = 1,
                                    Hits = 0x80,
                                    Delay = 0x90,
                                    Unknown2 = 1,
                                    Unknown3 = 1,
                                    GfxValue = 0,
                                    GfxLife = 2,
                                    GfxSize = 9,
                                    GfxRed = AmbientRestorationChildNanoId,
                                    GfxGreen = (int)IdentityType.CanbeAffected,
                                    GfxBlue = character.Identity.Instance,
                                    GfxFade = 0
                                }
                            }
                    });
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "CapturedSpellListVisualEffects AmbientRestoration failed: "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
            }
        }

        internal static void AnnounceBurningRobotFire(ICharacter robot)
        {
            if (robot == null || robot.Playfield == null)
            {
                return;
            }

            try
            {
                // Live 20260731-172247: two SpellLists back-to-back (A8E3 then A871).
                AnnounceBurningRobotFireGfx(robot, BurningRobotFireGfxValuePrimary);
                AnnounceBurningRobotFireGfx(robot, BurningRobotFireGfxValueSecondary);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "CapturedSpellListVisualEffects BurningFire failed: "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
            }
        }

        private static void AnnounceBurningRobotFireGfx(ICharacter robot, int gfxValue)
        {
            int gfxInstance = Interlocked.Increment(ref nextBurningFireGfxInstance);
            robot.Playfield.Announce(
                new SpellListMessage
                {
                    Identity = robot.Identity,
                    Unknown = 0,
                    Character = robot.Identity,
                    NanoEffects =
                        new[]
                        {
                            new NanoEffect
                            {
                                Effect =
                                    new Identity
                                    {
                                        Type = IdentityType.GfxEffect,
                                        Instance = gfxInstance
                                    },
                                Unknown1 = 4,
                                CriterionCount = 0,
                                Hits = 1,
                                Delay = 0,
                                Unknown2 = 0,
                                Unknown3 = 0,
                                GfxValue = gfxValue,
                                GfxLife = 0,
                                GfxSize = 0,
                                GfxRed = 0,
                                GfxGreen = 0,
                                GfxBlue = 0,
                                GfxFade = 0
                            }
                        }
                });
        }

        internal static bool IsAmbientRestorationNano(int nanoId)
        {
            return nanoId == AmbientRestorationNanoId;
        }

        internal static bool IsBurningCleaningRobot(ICharacter character)
        {
            return character != null
                   && string.Equals(character.Name, BurningRobotName, StringComparison.OrdinalIgnoreCase);
        }

        internal static double BurningFireIntervalSeconds
        {
            get { return BurningRobotFireSpellListSeconds; }
        }
    }
}
