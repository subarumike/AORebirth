namespace AORebirth.Core.Playfields
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Capture 20260825-094236: Nascence Dungeon 2 ACG interior (dyn Playfield2:2080D9).
    /// UI "PF-1073671978 (3221295318)" is ACGEntrance stamp C00110D6 on outdoor 4310 — not interior PF.
    /// Live interior Playfield2 from SCFU/Chest/Door = 0x002080D9. ACGEntrance remains C7A1:C00110D6.
    /// </summary>
    internal static class NascenceDungeon2Rules
    {
        internal const int SourcePlayfieldId = 4310;

        /// <summary>
        /// Capture 20260825-094236 ChangePlayfield / PAF Playfield2.
        /// </summary>
        internal const int DungeonPlayfieldId = unchecked((int)0x002080D9);

        /// <summary>Prior dyn instance from 20260823-182854 — still accepted for in-progress sessions.</summary>
        internal const int LegacyDungeonPlayfieldId = unchecked((int)0x00208047);

        /// <summary>Live N3Teleport / PAF PlayfieldId1 — ACGEntrance (NOT D1 C00010D6).</summary>
        internal const IdentityType BuildingGeneratorType = (IdentityType)0x0000C7A1;

        /// <summary>Stable ACGEntrance stamp paired with generator payload header.</summary>
        internal const int BuildingInstance = unchecked((int)0xC00110D6);

        /// <summary>Capture outdoor entrance identity (DoorStatusUpdate / externaldoor stats).</summary>
        internal const IdentityType AcgEntranceIdentityType = (IdentityType)0x0000C7A1;

        internal const int AcgEntranceInstance = unchecked((int)0xC00110D6);

        internal const uint AcgEntranceInstanceStat = 0xC00110D6u;

        /// <summary>Outdoor entry trigger — ACGEntrance world pos from N3Teleport.</summary>
        internal const float EntryTriggerX = 833.2006f;

        internal const float EntryTriggerY = 19.505f;

        internal const float EntryTriggerZ = 1414.998f;

        internal const float EntryTriggerRadius = 4.0f;

        internal const float EntryTriggerVerticalTolerance = 6.0f;

        /// <summary>Max chase distance from spawn before leash reset.</summary>
        internal const double MaximumNpcLeashDistanceFromHomeMeters = 40.0;

        /// <summary>Capture 20260823-182854 N3Teleport heading on outdoor pad.</summary>
        internal const float EntryHeadingX = 0.0f;

        internal const float EntryHeadingY = -0.4105664f;

        internal const float EntryHeadingZ = 0.0f;

        internal const float EntryHeadingW = 0.9118307f;

        internal const float InteriorLandingX = 801.801f;

        internal const float InteriorLandingY = 52.01f;

        internal const float InteriorLandingZ = 195.01f;

        internal const float InteriorLandingHeadingX = 0.0f;

        internal const float InteriorLandingHeadingY = 0.70710665f;

        internal const float InteriorLandingHeadingZ = 0.0f;

        internal const float InteriorLandingHeadingW = 0.7071069f;

        /// <summary>Boss-wing visibility + floor-button wing (PlayfieldVisibilityInterest).</summary>
        internal const float BossWingMaxWorldX = 300.0f;

        /// <summary>Exit trigger near interior landing.</summary>
        internal const float ExitTriggerX = 801.8f;

        internal const float ExitTriggerY = 52.01f;

        internal const float ExitTriggerZ = 195.0f;

        internal const float ExitTriggerRadius = 2.5f;

        internal const float ExitTriggerVerticalTolerance = 4.0f;

        internal const float ExitHeadingX = 0.0f;

        internal const float ExitHeadingY = -0.7111939f;

        internal const float ExitHeadingZ = 0.0f;

        internal const float ExitHeadingW = 0.7029959f;

        /// <summary>Outdoor ACGEntrance world pos stamped in N3Teleport payload (entry coords).</summary>
        internal const float ExitOutdoorDoorX = 833.2006f;

        internal const float ExitOutdoorDoorY = 19.505f;

        internal const float ExitOutdoorDoorZ = 1414.998f;

        /// <summary>
        /// Outdoor landing in front of ACGEntrance (entry door Y=19.505). Prior Y=13.8 buried the
        /// character in terrain. No post-exit SCFU in 20260823-182854; land just south of door.
        /// </summary>
        internal const float ExitOutdoorLandingX = 833.2f;

        internal const float ExitOutdoorLandingY = 19.505f;

        internal const float ExitOutdoorLandingZ = 1412.0f;

        internal const float ExitOutdoorLandingHeadingX = 0.0f;

        /// <summary>Opposite of entry heading (0, -0.4105664, 0, 0.9118307) — face away from door.</summary>
        internal const float ExitOutdoorLandingHeadingY = 0.4105664f;

        internal const float ExitOutdoorLandingHeadingZ = 0.0f;

        internal const float ExitOutdoorLandingHeadingW = 0.9118307f;

        /// <summary>Live exit N3Teleport Playfield2 type (100007:1).</summary>
        internal const int ExitTeleportPlayfield2Type = 0x000186A7;

        internal const int ExitTeleportPlayfield2Instance = 1;

        /// <summary>
        /// Stable GOS gain per qualifying kill (Infernal Vortexoid / Croaker of Solitude).
        /// Capture observed deltas 11–26; use mid-range 13 until dynamic formula is known.
        /// </summary>
        internal const int GuardianOfShadowFactionGainPerKill = 13;

        internal static bool IsDungeonPlayfield(int playfieldInstance)
        {
            return playfieldInstance == DungeonPlayfieldId
                   || playfieldInstance == LegacyDungeonPlayfieldId;
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

            return string.Equals(name, "Infernal Vortexoid", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Malah-Fama", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Bound Dryad", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Burning Shadow", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Icy Shadow", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Guard Turret", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool GrantsGuardianOfShadowFaction(string name)
        {
            return string.Equals(name, "Infernal Vortexoid", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase);
        }

        // Mike D2 finish timers (apply to both Nascence ACG dungeons).
        internal static readonly TimeSpan MobRespawnDelay = TimeSpan.FromMinutes(10);

        internal static readonly TimeSpan TreasureRespawnDelay = TimeSpan.FromMinutes(10);

        internal static readonly TimeSpan CorpseLifetime = TimeSpan.FromMinutes(2);

        internal static readonly TimeSpan HavarisRespawnWhenEmpty = TimeSpan.FromMinutes(20);

        internal static readonly TimeSpan HavarisRespawnWhenOccupied = TimeSpan.FromHours(1);
    }
}
