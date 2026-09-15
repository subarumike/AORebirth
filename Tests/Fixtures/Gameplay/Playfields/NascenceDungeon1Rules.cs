namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Capture 20260824-125154: Nascence Frontier (4310) Lord/Lady cave ACG dungeon 1.
    /// Live ChangePlayfield / PAF Playfield2 = dyn Playfield2:1F900B with ACGEntrance C7A1:C00010D6.
    /// </summary>
    internal static class NascenceDungeon1Rules
    {
        internal const int SourcePlayfieldId = 4310;

        /// <summary>
        /// Live ChangePlayfield / PAF Playfield2 from capture 20260824-203715.
        /// </summary>
        internal const int DungeonPlayfieldId = unchecked((int)0x00208038);

        /// <summary>Earlier reserved id — still recognized and remapped on login.</summary>
        internal const int ReservedDungeonPlayfieldId = unchecked((int)0x001F900B);

        /// <summary>Door/chest capture stamp before pf remap.</summary>
        internal const int LegacyCapturedPlayfieldId = unchecked((int)0x001F804E);

        /// <summary>Live dyn PF stamp from capture 20260824-220326 (Playfield2:2080EE).</summary>
        internal const int LiveCapturedPlayfieldId220326 = unchecked((int)0x002080EE);

        // Captures show a fresh Playfield2 lease for each ACG entry (for example 208038,
        // 2080EE, and 208851).  Reusing one id preserves the client's explored PF-map cells.
        private const int DynamicPlayfieldFloor = unchecked((int)0x00208000);

        private const int DynamicPlayfieldMask = 0x00007FFF;

        private static int nextDynamicPlayfieldSlot = Environment.TickCount;

        private static readonly ConcurrentDictionary<int, byte> DynamicPlayfieldIds =
            new ConcurrentDictionary<int, byte>();

        /// <summary>Live N3Teleport / PAF PlayfieldId1 — ACGEntrance.</summary>
        internal const IdentityType BuildingGeneratorType = (IdentityType)0x0000C7A1;

        /// <summary>Stable ACGEntrance stamp paired with generator payload header.</summary>
        internal const int BuildingInstance = unchecked((int)0xC00010D6);

        /// <summary>Capture outdoor entrance identity (DoorStatusUpdate / externaldoor stats).</summary>
        internal const IdentityType AcgEntranceIdentityType = (IdentityType)0x0000C7A1;

        internal const int AcgEntranceInstance = unchecked((int)0xC00010D6);

        internal const uint AcgEntranceInstanceStat = 0xC00010D6u;

        internal const float EntryTriggerX = 889.3599f;

        internal const float EntryTriggerY = 13.985f;

        internal const float EntryTriggerZ = 1405.318f;

        internal const float EntryTriggerRadius = 4.0f;

        internal const float EntryTriggerVerticalTolerance = 6.0f;

        /// <summary>Max chase distance from spawn before leash reset (trash must not follow into boss wing).</summary>
        internal const double MaximumNpcLeashDistanceFromHomeMeters = 40.0;

        /// <summary>Capture 20260824-125154 N3Teleport heading on outdoor pad.</summary>
        internal const float EntryHeadingX = 0.0f;

        internal const float EntryHeadingY = 0.3331171f;

        internal const float EntryHeadingZ = 0.0f;

        internal const float EntryHeadingW = 0.9428855f;

        internal const float InteriorLandingX = 801.801f;

        internal const float InteriorLandingY = 52.01f;

        internal const float InteriorLandingZ = 125.01f;

        internal const float InteriorLandingHeadingX = 0.0f;

        internal const float InteriorLandingHeadingY = 0.7071066f;

        internal const float InteriorLandingHeadingZ = 0.0f;

        internal const float InteriorLandingHeadingW = 0.7071069f;

        /// <summary>Live boss-wing visibility + floor-button wing (PlayfieldVisibilityInterest).</summary>
        internal const float BossWingMaxWorldX = 300.0f;

        /// <summary>Capture 20260824-132534 walk-into exit FullStop on dyn PF.</summary>
        internal const float ExitTriggerX = 800.7633f;

        internal const float ExitTriggerY = 52.01f;

        internal const float ExitTriggerZ = 125.01f;

        internal const float ExitTriggerRadius = 2.5f;

        internal const float ExitTriggerVerticalTolerance = 4.0f;

        internal const float ExitHeadingX = 0.0f;

        internal const float ExitHeadingY = -0.7111939f;

        internal const float ExitHeadingZ = 0.0f;

        internal const float ExitHeadingW = 0.7029959f;

        /// <summary>Outdoor ACGEntrance world pos stamped in N3Teleport payload.</summary>
        internal const float ExitOutdoorDoorX = 889.0596f;

        internal const float ExitOutdoorDoorY = 15.69766f;

        internal const float ExitOutdoorDoorZ = 1405.961f;

        /// <summary>Capture 20260824-132534 post-exit SCFU landing on PF 4310.</summary>
        internal const float ExitOutdoorLandingX = 887.6047f;

        internal const float ExitOutdoorLandingY = 13.81f;

        internal const float ExitOutdoorLandingZ = 1404.455f;

        internal const float ExitOutdoorLandingHeadingX = 0.0f;

        internal const float ExitOutdoorLandingHeadingY = -0.9271821f;

        internal const float ExitOutdoorLandingHeadingZ = 0.0f;

        internal const float ExitOutdoorLandingHeadingW = 0.3746108f;

        /// <summary>Live exit N3Teleport Playfield2 type (100007:1).</summary>
        internal const int ExitTeleportPlayfield2Type = 0x000186A7;

        internal const int ExitTeleportPlayfield2Instance = 1;

        internal static bool IsDungeonPlayfield(int playfieldInstance)
        {
            // D2/D3/D4 leases share the live ACG band; never claim them as D1 (wrong PAF/layout).
            if (NascenceDungeon2Rules.IsDungeonPlayfield(playfieldInstance)
                || NascenceDungeon3Rules.IsDungeonPlayfield(playfieldInstance)
                || NascenceDungeon4Rules.IsDungeonPlayfield(playfieldInstance))
            {
                return false;
            }

            // Lease dictionary only — do NOT claim the whole 0x00208xxx band (that stole D3
            // logins after ZoneEngine restart and stamped the wrong cave).
            return DynamicPlayfieldIds.ContainsKey(playfieldInstance)
                   || playfieldInstance == DungeonPlayfieldId
                   || playfieldInstance == ReservedDungeonPlayfieldId
                   || playfieldInstance == LegacyCapturedPlayfieldId
                   || playfieldInstance == LiveCapturedPlayfieldId220326;
        }

        /// <summary>
        /// Allocates the client-visible lease for one fresh D1 entry.  The physical playfield is
        /// created through ZoneServer's normal GetOrCreate path and receives the same D1 content.
        /// Fresh id each entry so client PF Map fog starts black (reuse preserves explored cells).
        /// </summary>
        internal static int AllocateDynamicPlayfieldId()
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int slot = Interlocked.Increment(ref nextDynamicPlayfieldSlot) & DynamicPlayfieldMask;
                int playfieldInstance = DynamicPlayfieldFloor | slot;
                if (playfieldInstance == DungeonPlayfieldId
                    || playfieldInstance == ReservedDungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon2Rules.DungeonPlayfieldId
                    || playfieldInstance == NascenceDungeon2Rules.LegacyDungeonPlayfieldId
                    || NascenceDungeon2Rules.IsDungeonPlayfield(playfieldInstance)
                    || playfieldInstance == NascenceDungeon3Rules.DungeonPlayfieldId
                    || NascenceDungeon3Rules.IsDungeonPlayfield(playfieldInstance)
                    || playfieldInstance == NascenceDungeon4Rules.DungeonPlayfieldId
                    || NascenceDungeon4Rules.IsDungeonPlayfield(playfieldInstance))
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

        /// <summary>Legacy ids from earlier rebirth attempts that must remap on login.</summary>
        internal static bool IsLegacyDungeonPlayfield(int playfieldInstance)
        {
            return playfieldInstance == LegacyCapturedPlayfieldId
                   || playfieldInstance == ReservedDungeonPlayfieldId
                   || playfieldInstance == LiveCapturedPlayfieldId220326
                   || playfieldInstance == unchecked((int)0x0016FFF0)
                   || playfieldInstance == 362;
        }

        internal static bool IsDungeonCorpseName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.EndsWith("Coral Rafter", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Crippler of Destiny", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Croaker of Desolation", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase);
        }

        // Same dungeon timers as D2 (Mike).
        internal static readonly TimeSpan MobRespawnDelay = TimeSpan.FromMinutes(10);

        internal static readonly TimeSpan TreasureRespawnDelay = TimeSpan.FromMinutes(10);

        internal static readonly TimeSpan CorpseLifetime = TimeSpan.FromMinutes(2);

        internal static readonly TimeSpan HavarisRespawnWhenEmpty = TimeSpan.FromMinutes(20);

        internal static readonly TimeSpan HavarisRespawnWhenOccupied = TimeSpan.FromHours(1);
    }
}
