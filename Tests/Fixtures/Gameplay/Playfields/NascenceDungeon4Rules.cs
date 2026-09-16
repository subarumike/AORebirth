namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Capture 20260830-143801: Nascence Dungeon 4 ACG interior (dyn Playfield2:2090C1).
    /// Outdoor ACGEntrance stamp C00110D7 "A Door" on PF 4311.
    /// Live interior Playfield2 from SCFU/Chest/Door = 0x002090C1. ACGEntrance C7A1:C00110D7.
    /// </summary>
    internal static class NascenceDungeon4Rules
    {
        internal const int SourcePlayfieldId = 4311;

        /// <summary>
        /// Capture 20260830-143801 ChangePlayfield / PAF Playfield2.
        /// </summary>
        internal const int DungeonPlayfieldId = unchecked((int)0x002090C1);

        // Same ACG dyn band as live / D1 / D2 / D3. Fresh lease each entry so client PF Map fog resets
        // (reusing one id preserves explored map cells).
        private const int DynamicPlayfieldFloor = unchecked((int)0x00208000);

        private const int DynamicPlayfieldMask = 0x00007FFF;

        private static int nextDynamicPlayfieldSlot = Environment.TickCount ^ unchecked((int)0x4D4D4D4D);

        private static readonly ConcurrentDictionary<int, byte> DynamicPlayfieldIds =
            new ConcurrentDictionary<int, byte>();

        /// <summary>Live N3Teleport / PAF PlayfieldId1 — ACGEntrance (NOT D1/D2/D3 stamps).</summary>
        internal const IdentityType BuildingGeneratorType = (IdentityType)0x0000C7A1;

        /// <summary>Stable ACGEntrance stamp paired with generator payload header (NOT D3 C00010D7).</summary>
        internal const int BuildingInstance = unchecked((int)0xC00110D7);

        /// <summary>Capture outdoor entrance identity (DoorStatusUpdate / externaldoor stats).</summary>
        internal const IdentityType AcgEntranceIdentityType = (IdentityType)0x0000C7A1;

        internal const int AcgEntranceInstance = unchecked((int)0xC00110D7);

        internal const uint AcgEntranceInstanceStat = 0xC00110D7u;

        /// <summary>Outdoor entry trigger — ACGEntrance world pos from capture (A Door).</summary>
        internal const float EntryTriggerX = 574.1825f;

        internal const float EntryTriggerY = 14.51956f;

        internal const float EntryTriggerZ = 1308.852f;

        internal const float EntryTriggerRadius = 4.0f;

        internal const float EntryTriggerVerticalTolerance = 6.0f;

        /// <summary>Max chase distance from spawn before leash reset.</summary>
        internal const double MaximumNpcLeashDistanceFromHomeMeters = 40.0;

        /// <summary>Capture approach — face door from outdoor side (approx toward +Z).</summary>
        internal const float EntryHeadingX = 0.0f;

        internal const float EntryHeadingY = 0.0f;

        internal const float EntryHeadingZ = 0.0f;

        internal const float EntryHeadingW = 1.0f;

        internal const float InteriorLandingX = 1498.199f;

        internal const float InteriorLandingY = 52.01f;

        internal const float InteriorLandingZ = 45.01001f;

        internal const float InteriorLandingHeadingX = 0.0f;

        internal const float InteriorLandingHeadingY = -0.7071068f;

        internal const float InteriorLandingHeadingZ = 0.0f;

        internal const float InteriorLandingHeadingW = 0.7071068f;

        /// <summary>Boss-wing visibility + floor-button wing (Havaris at x≈170). evidence=20260830-143801.</summary>
        internal const float BossWingMaxWorldX = 300.0f;

        /// <summary>Exit trigger near interior landing (MARK "exit").</summary>
        internal const float ExitTriggerX = 1494.0f;

        internal const float ExitTriggerY = 52.01f;

        internal const float ExitTriggerZ = 45.0f;

        internal const float ExitTriggerRadius = 2.5f;

        internal const float ExitTriggerVerticalTolerance = 4.0f;

        internal const float ExitHeadingX = 0.0f;

        internal const float ExitHeadingY = 0.7071068f;

        internal const float ExitHeadingZ = 0.0f;

        internal const float ExitHeadingW = 0.7071068f;

        /// <summary>Outdoor ACGEntrance world pos stamped in N3Teleport payload (entry coords).</summary>
        internal const float ExitOutdoorDoorX = 574.1825f;

        internal const float ExitOutdoorDoorY = 14.51956f;

        internal const float ExitOutdoorDoorZ = 1308.852f;

        /// <summary>
        /// Outdoor landing in front of A Door (~south of door). evidence=20260830-143801.
        /// </summary>
        internal const float ExitOutdoorLandingX = 574.2f;

        internal const float ExitOutdoorLandingY = 14.5f;

        internal const float ExitOutdoorLandingZ = 1306.0f;

        internal const float ExitOutdoorLandingHeadingX = 0.0f;

        /// <summary>Face away from door (south / -Z).</summary>
        internal const float ExitOutdoorLandingHeadingY = 0.0f;

        internal const float ExitOutdoorLandingHeadingZ = 0.0f;

        internal const float ExitOutdoorLandingHeadingW = 1.0f;

        /// <summary>Live exit N3Teleport Playfield2 type (100007:1).</summary>
        internal const int ExitTeleportPlayfield2Type = 0x000186A7;

        internal const int ExitTeleportPlayfield2Instance = 1;

        /// <summary>
        /// Stable GOS gain per qualifying kill (Mortiig Predator). Mid-range 13 until dynamic formula known.
        /// Capture 20260830-143801 had no faction chat msgs — still wired like D3.
        /// </summary>
        internal const int GuardianOfShadowFactionGainPerKill = 13;

        internal static bool IsDungeonPlayfield(int playfieldInstance)
        {
            return playfieldInstance == DungeonPlayfieldId
                   || DynamicPlayfieldIds.ContainsKey(playfieldInstance);
        }

        /// <summary>
        /// Allocates a fresh client-visible lease for one D4 entry so PF Map fog starts black.
        /// </summary>
        internal static int AllocateDynamicPlayfieldId()
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int slot = Interlocked.Increment(ref nextDynamicPlayfieldSlot) & DynamicPlayfieldMask;
                int playfieldInstance = DynamicPlayfieldFloor | slot;
                if (playfieldInstance == DungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon1Rules.DungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon1Rules.ReservedDungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon2Rules.DungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon2Rules.LegacyDungeonPlayfieldId
                    || NascenceDungeon2Rules.IsDungeonPlayfield(playfieldInstance)
                    || playfieldInstance == NascenceDungeon3Rules.DungeonPlayfieldId
                    || NascenceDungeon3Rules.IsDungeonPlayfield(playfieldInstance))
                {
                    continue;
                }

                if (DynamicPlayfieldIds.TryAdd(playfieldInstance, 0))
                {
                    return playfieldInstance;
                }
            }

            int fallback = DynamicPlayfieldFloor
                           | (Interlocked.Increment(ref nextDynamicPlayfieldSlot) & DynamicPlayfieldMask);
            DynamicPlayfieldIds[fallback] = 0;
            return fallback;
        }

        /// <summary>Re-claim a saved dyn PF after ZoneEngine restart (login rehydrate).</summary>
        internal static void AdoptLease(int playfieldInstance)
        {
            if (playfieldInstance == 0)
            {
                return;
            }

            DynamicPlayfieldIds[playfieldInstance] = 0;
        }

        internal static bool IsSourcePlayfield(int playfieldInstance)
        {
            return playfieldInstance == SourcePlayfieldId;
        }

        internal static bool IsDungeonCorpseName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Bound Dryad", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Mortiig Predator", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Kolaana", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Kolaana-Ana", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Wandering Coral Rafter", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Panicked Coral Rafter", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Hoathlan", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Brisk Hoathlan", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Guard Turret", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Alarm Sentry", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Security Camera", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Hued Sewer Scuttler", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool GrantsGuardianOfShadowFaction(string name)
        {
            return string.Equals(name, "Mortiig Predator", StringComparison.OrdinalIgnoreCase);
        }

        // Capture 20260830-143801 timers (NOT D3 10min/1h).
        internal static readonly TimeSpan MobRespawnDelay = TimeSpan.FromMinutes(5);

        internal static readonly TimeSpan TreasureRespawnDelay = TimeSpan.FromMinutes(5);

        internal static readonly TimeSpan CorpseLifetime = TimeSpan.FromMinutes(2);

        internal static readonly TimeSpan HavarisRespawnWhenEmpty = TimeSpan.FromMinutes(20);

        internal static readonly TimeSpan HavarisRespawnWhenOccupied = TimeSpan.FromHours(2);
    }
}
