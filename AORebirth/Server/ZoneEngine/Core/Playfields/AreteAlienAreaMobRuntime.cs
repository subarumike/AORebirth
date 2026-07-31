namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260726-spawn-mob-tll-alien: Alien Spider - Zix, Scout - Jaax'Sinuh,
    /// Specialist - Cha'Heru, Angry Minibull / Harvey, Saltworm, and extra Rollerrats
    /// east of the Lorelei oasis cluster.
    /// Oasis Desert Reet / Lolly / local Rollerrats / Gnarl remain in LoreleiOasisMobRuntime.
    /// </summary>
    internal static class AreteAlienAreaMobRuntime
    {
        private enum MobKind
        {
            Spider,

            Scout,

            Specialist,

            Saltworm,

            Rollerrat,

            Minibull
        }

        private sealed class MobSlot
        {
            public string Name { get; private set; }

            public MobKind Kind { get; private set; }

            public int MonsterData { get; private set; }

            public int Level { get; private set; }

            public int Health { get; private set; }

            public int NpcFamily { get; private set; }

            public int Scale { get; private set; }

            public int RunSpeed { get; private set; }

            public NpcAiProfile AiProfile { get; private set; }

            public float AggroRadiusMeters { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }

            public MobSlot(
                string name,
                MobKind kind,
                int monsterData,
                int level,
                int health,
                int npcFamily,
                int scale,
                int runSpeed,
                NpcAiProfile aiProfile,
                float aggroRadiusMeters,
                float x,
                float y,
                float z)
            {
                this.Name = name;
                this.Kind = kind;
                this.MonsterData = monsterData;
                this.Level = level;
                this.Health = health;
                this.NpcFamily = npcFamily;
                this.Scale = scale;
                this.RunSpeed = runSpeed;
                this.AiProfile = aiProfile;
                this.AggroRadiusMeters = aggroRadiusMeters;
                this.X = x;
                this.Y = y;
                this.Z = z;
            }
        }

        private const int AreteLandingPlayfieldId = 6553;

        private const int MissingVisualId = 1234567890;

        // Alien-family timings remain unresolved and retain the prior 60-second soft timer.
        private const double DefaultRespawnSeconds = 60.0;

        // Captures 20260722-104809/152454: ordinary Rollerrats and Angry
        // Minibulls respawn at approximately 40 seconds. Harvey and alien
        // families retain the prior default because their timing is unresolved.
        private const double CapturedWildlifeRespawnSeconds = 40.0;

        // Minibull / Saltworm / Harvey / Rollerrat AOS at 5m.
        // Capture 20260726-230559: Spider / Scout / Specialist are passive until player attacks.
        private const float WildlifeAggroRadiusMeters = 5.0f;

        // Capture 20260726-124832: Rollerrat AOS ~13.07m.
        private const float RollerratAggroRadiusMeters =
            NpcCombatAttackRules.CapturedAreteRollerratAggroRadiusMeters;

        // Capture CharacterFlags for Angry Minibull / Saltworm / Spider (includes alien AXP bit 0x4000).
        private const int WildlifeCharacterFlags = 268980737;

        // Capture CharacterFlags for Scout / Specialist / Rollerrat (no AXP bit).
        private const int AlienHumanoidCharacterFlags = 268964353;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        private static readonly object AggroGate = new object();

        private static readonly Dictionary<int, float> AggroRadiusByNpcInstance = new Dictionary<int, float>();

        // Capture 20260726-spawn-mob-tll-alien packets.hex: Angry Minibull / Harvey Material #7 / 26905.
        private static readonly byte[] AngryMinibullExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x37, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x69, 0x19, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture 20260726-spawn-mob-tll-alien: Saltworm Material #3 / 95955.
        private static readonly byte[] SaltwormExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x33, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0xD3, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ScfuUnk1 for Minibull / Rollerrat / Spider (oasis-style).
        private static readonly byte[] WildlifeCapturedScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00
            };

        // Capture Saltworm ScfuUnk1 ends 03 00 00 (not 02).
        private static readonly byte[] SaltwormCapturedScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x03, 0x00, 0x00
            };

        // Capture 20260726-spawn-mob-tll-alien enemy-dossier clustered slots (oasis rats excluded).
        private static readonly MobSlot[] Slots =
            {
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 9, 74, 220, 123, 75, NpcAiProfile.Passive, 0f, 3754.769f, 0.795f, 327.577f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 7, 60, 220, 121, 61, NpcAiProfile.Passive, 0f, 3778.519f, 0.053f, 238.011f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 8, 67, 220, 122, 68, NpcAiProfile.Passive, 0f, 3784.375f, 0.010f, 283.851f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 8, 67, 220, 122, 68, NpcAiProfile.Passive, 0f, 3784.398f, 1.127f, 225.772f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 8, 67, 220, 122, 68, NpcAiProfile.Passive, 0f, 3790.951f, 0.086f, 256.086f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 8, 67, 220, 122, 68, NpcAiProfile.Passive, 0f, 3805.080f, 1.498f, 300.400f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 9, 74, 220, 123, 75, NpcAiProfile.Passive, 0f, 3806.655f, 3.799f, 339.188f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 10, 80, 220, 123, 81, NpcAiProfile.Passive, 0f, 3817.736f, 8.297f, 313.033f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 8, 67, 220, 122, 68, NpcAiProfile.Passive, 0f, 3824.555f, 10.434f, 306.007f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 10, 80, 220, 123, 81, NpcAiProfile.Passive, 0f, 3832.139f, 1.850f, 364.183f),
                new MobSlot("Alien Spider - Zix", MobKind.Spider, 247728, 7, 60, 220, 121, 61, NpcAiProfile.Passive, 0f, 3835.857f, 12.640f, 319.647f),
                // Capture SCFU: MonsterData body, TextureOverrides=null, CharacterFlags=268964353.
                // Scout HasExtendedRunSpeed (RunSpeedBase=262). Specialist RunSpeedBase 40–51.
                new MobSlot("Scout - Jaax'Sinuh", MobKind.Scout, 251782, 11, 284, 220, 96, 262, NpcAiProfile.Passive, 0f, 3835.278f, 9.549f, 288.615f),
                new MobSlot("Scout - Jaax'Sinuh", MobKind.Scout, 251782, 11, 284, 220, 96, 262, NpcAiProfile.Passive, 0f, 3835.819f, 1.931f, 245.897f),
                new MobSlot("Specialist - Cha'Heru", MobKind.Specialist, 251772, 12, 1008, 220, 154, 48, NpcAiProfile.Passive, 0f, 3814.187f, 1.786f, 216.272f),
                new MobSlot("Specialist - Cha'Heru", MobKind.Specialist, 251772, 13, 1237, 220, 154, 51, NpcAiProfile.Passive, 0f, 3852.061f, 2.733f, 223.334f),
                new MobSlot("Specialist - Cha'Heru", MobKind.Specialist, 251772, 10, 550, 220, 152, 40, NpcAiProfile.Passive, 0f, 3860.882f, 7.469f, 249.608f),
                new MobSlot("Specialist - Cha'Heru", MobKind.Specialist, 251772, 10, 550, 220, 152, 40, NpcAiProfile.Passive, 0f, 3886.423f, 8.759f, 274.576f),
                new MobSlot("Specialist - Cha'Heru", MobKind.Specialist, 251772, 10, 550, 220, 152, 40, NpcAiProfile.Passive, 0f, 3893.533f, 4.607f, 231.076f),
                // Capture 20260727-054719: HP 1143, living scale 75, ExtTex Material #3 / 95955.
                new MobSlot("Saltworm", MobKind.Saltworm, 17712, 13, 1143, 58, 75, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3434.092f, 0.010f, 629.830f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3458.478f, 2.346f, 568.022f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3464.815f, 2.110f, 698.560f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3492.766f, 2.662f, 544.020f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3495.094f, 2.110f, 693.141f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3507.762f, 2.359f, 576.707f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3514.796f, 2.110f, 692.689f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3524.854f, 2.110f, 527.842f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3525.883f, 0.010f, 468.636f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3527.167f, 0.286f, 504.498f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3533.234f, 3.084f, 673.433f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3556.184f, 0.010f, 485.107f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3572.904f, 0.310f, 500.685f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3577.877f, 0.010f, 371.673f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 6, 69, 55, 125, 21, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3608.443f, 10.463f, 533.241f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3736.135f, 0.010f, 383.117f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3815.560f, 0.010f, 367.723f),
                new MobSlot("Rollerrat", MobKind.Rollerrat, 17687, 5, 58, 55, 125, 18, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3869.587f, 0.010f, 474.544f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 11, 131, 42, 105, 36, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3536.126f, 0.010f, 339.404f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 11, 131, 42, 105, 36, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3552.088f, 0.010f, 408.943f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 11, 131, 42, 105, 36, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3553.019f, 0.010f, 403.762f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3558.688f, 3.924f, 584.517f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3559.891f, 1.664f, 528.942f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 13, 164, 42, 105, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3575.381f, 0.010f, 399.720f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 13, 164, 42, 105, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3579.301f, 0.010f, 328.001f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3582.177f, 0.010f, 396.855f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 13, 164, 42, 105, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3589.763f, 0.010f, 454.320f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 9, 103, 42, 105, 30, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3614.056f, 0.010f, 395.344f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 12, 148, 42, 105, 39, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3615.125f, 2.178f, 281.662f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3617.207f, 0.010f, 430.436f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3619.367f, 0.010f, 395.553f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 9, 103, 42, 105, 30, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3621.853f, 0.010f, 350.504f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3644.612f, 0.010f, 397.724f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 13, 164, 42, 105, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3651.043f, 0.010f, 398.197f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 10, 114, 42, 105, 32, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3662.097f, 3.204f, 331.812f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 12, 148, 42, 105, 39, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3675.634f, 8.384f, 456.476f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 10, 114, 42, 105, 32, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3722.297f, 0.010f, 331.565f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 13, 164, 42, 105, 42, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3728.818f, 0.010f, 394.514f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 12, 148, 42, 105, 39, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3737.575f, 0.010f, 340.804f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3767.514f, 0.277f, 413.558f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 11, 131, 42, 105, 36, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3783.050f, 0.394f, 304.045f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3833.219f, 0.104f, 412.938f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 9, 103, 42, 105, 30, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3835.416f, 0.569f, 398.076f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 8, 92, 42, 105, 27, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3848.715f, 1.310f, 439.811f),
                new MobSlot("Harvey the Bully", MobKind.Minibull, 30360, 10, 682, 3, 100, 32, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3849.048f, 1.402f, 439.513f),
                new MobSlot("Angry Minibull", MobKind.Minibull, 30360, 10, 114, 42, 105, 32, NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters, 3857.483f, 2.638f, 426.471f),
            };

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Angry Minibull", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Harvey the Bully", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])AngryMinibullExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Saltworm", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])SaltwormExtendedTextureOverrideData.Clone();
                return true;
            }

            // Rollerrat ExtTex is owned by LoreleiOasisMobRuntime (same Material #1 wire).
            data = null;
            return false;
        }

        internal static bool TryGetCapturedScfuUnknown1(string name, out byte[] data)
        {
            if (string.Equals(name, "Saltworm", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])SaltwormCapturedScfuUnknown1.Clone();
                return true;
            }

            if (string.Equals(name, "Angry Minibull", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Harvey the Bully", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Alien Spider - Zix", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Scout - Jaax'Sinuh", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Specialist - Cha'Heru", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Rollerrat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])WildlifeCapturedScfuUnknown1.Clone();
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>
        /// Scout / Specialist / Spider: capture TextureOverrides=null; SCFU still needs ScfuUnk1.
        /// </summary>
        internal static bool TryGetMonsterDataBodyScfuUnknown1(string name, out byte[] data)
        {
            if (string.Equals(name, "Alien Spider - Zix", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Scout - Jaax'Sinuh", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Specialist - Cha'Heru", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])WildlifeCapturedScfuUnknown1.Clone();
                return true;
            }

            data = null;
            return false;
        }

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            if (npc.FightingTarget.Instance != 0)
            {
                return null;
            }

            float radius;
            lock (AggroGate)
            {
                if (!AggroRadiusByNpcInstance.TryGetValue(npc.Identity.Instance, out radius) || radius <= 0f)
                {
                    return null;
                }
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null || npc.RawCoordinates == null)
            {
                return null;
            }

            Coordinate npcCoord = npc.Coordinates();
            ICharacter best = null;
            double bestDistance = radius;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, radius);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.RawCoordinates == null)
                {
                    continue;
                }

                double distance = candidate.Coordinates().coordinate.Distance2D(npcCoord.coordinate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            NextRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[Slots.Length];
            DateTime[] timers = NextRespawnUtcBySlot[playfieldIdentity.Instance];
            int spawned = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                try
                {
                    if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                    {
                        timers[i] = DateTime.MaxValue;
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteAlienAreaMobRuntime slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteAlienAreaMobRuntime spawned="
                + spawned
                + "/"
                + Slots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260726-spawn-mob-tll-alien");
            if (spawned == 0)
            {
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
                NextRespawnUtcBySlot.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRespawnUtcBySlot.Remove(playfieldInstance);
            // Keep aggro map entries; instances are unique and overwritten on next spawn.
            // Wiping here left living NPCs with no AOS after ensure/clear races.
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            DateTime[] timers;
            if (!NextRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out timers)
                || timers == null
                || timers.Length != Slots.Length)
            {
                timers = new DateTime[Slots.Length];
                NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                ICharacter living;
                if (TryFindLivingMobNear(playfield, Slots[i], out living))
                {
                    timers[i] = DateTime.MaxValue;
                    // Re-bind AOS if spawn registration was lost after a Clear race.
                    if (Slots[i].AggroRadiusMeters > 0f && living != null)
                    {
                        RegisterAggro(living.Identity.Instance, Slots[i].AggroRadiusMeters);
                    }
                }
                else if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(
                        ResolveRespawnSeconds(Slots[i]));
                }
                else if (!(timers[i] > DateTime.UtcNow)
                         && SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    timers[i] = DateTime.MaxValue;
                }
            }
        }

        private static double ResolveRespawnSeconds(MobSlot slot)
        {
            if (slot == null)
            {
                return DefaultRespawnSeconds;
            }

            if (slot.Kind == MobKind.Rollerrat
                || (slot.Kind == MobKind.Minibull
                    && string.Equals(slot.Name, "Angry Minibull", StringComparison.Ordinal)))
            {
                return CapturedWildlifeRespawnSeconds;
            }

            return DefaultRespawnSeconds;
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = Slots[slotIndex];
            // Rollerrat AOS when AggroRadius > 0; other alien-area mobs are Passive retaliators.
            NpcAiProfile aiProfile = slot.AggroRadiusMeters > 0f
                                         ? NpcAiProfile.Aggressive
                                         : slot.AiProfile;
            NPCController controller = new NPCController { AiProfile = aiProfile };
            string templateHash = ResolveTemplateHash(slot);
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                templateHash,
                playfieldIdentity,
                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                slot.Level);
            if (mob == null)
            {
                return null;
            }

            mob.Name = slot.Name;
            mob.Playfield = playfield;
            PrepareArchetype(mob, slot);
            mob.Name = slot.Name;
            ApplyCaptureStats(mob, slot);
            controller.AiProfile = aiProfile;

            int minDamage;
            int maxDamage;
            ResolveCaptureDamage(slot, out minDamage, out maxDamage);
            CapturedEnemyCombatContract contract;
            if (slot.Kind == MobKind.Rollerrat)
            {
                // Capture 20260726-124832: dual LEW1/LEW2 fight anim + ~3s cadence.
                contract = CapturedEnemyCombatContract.AreteRollerratAttackOnSight(
                    "arete-alien-rollerrat-20260726-124832",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else if (slot.Kind == MobKind.Minibull)
            {
                // Capture 20260726-220219 / 20260726-230559: dual LEW1/LEW2 SAW.
                contract = CapturedEnemyCombatContract.AreteAngryMinibullAttackOnSight(
                    "arete-angry-minibull-20260726-220219",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else if (slot.Kind == MobKind.Saltworm)
            {
                // Capture 20260727-054719: dual LEW1/LEW2 SAW Unknown=109, Amount 13..21.
                contract = CapturedEnemyCombatContract.AreteSaltwormAttackOnSight(
                    "arete-saltworm-20260727-054719",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else if (slot.Kind == MobKind.Spider)
            {
                // Capture 20260726-230559: dual VZCX/CKHC SAW + AttackInfo.
                contract = CapturedEnemyCombatContract.AreteAlienSpiderAttackOnSight(
                    "arete-alien-spider-zix-20260726-230559",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else if (slot.Kind == MobKind.Scout)
            {
                // Capture 20260726-230559: DXZJ/HFRS/UGPQ SAW + cycling AttackInfo.
                contract = CapturedEnemyCombatContract.AreteScoutJaaxSinuhAttackOnSight(
                    "arete-scout-jaaxsinuh-20260726-230559",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else if (slot.Kind == MobKind.Specialist)
            {
                // Capture 20260726-230559: five-special SAW + cycling AttackInfo.
                contract = CapturedEnemyCombatContract.AreteSpecialistChaHeruAttackOnSight(
                    "arete-specialist-chaheru-20260726-230559",
                    mob.Identity.Instance,
                    minDamage,
                    maxDamage);
            }
            else
            {
                contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                    "arete-alien-area-20260726-spawn-mob-tll-alien",
                    minDamage,
                    maxDamage,
                    2.0,
                    0,
                    0,
                    1279612721,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            controller.AiProfile = aiProfile;
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            // Register AOS before Activate so the first NPC tick can aggro.
            if (slot.AggroRadiusMeters > 0f)
            {
                RegisterAggro(mob.Identity.Instance, slot.AggroRadiusMeters);
            }

            activateNpc(mob);
            if (slot.AggroRadiusMeters > 0f)
            {
                RegisterAggro(mob.Identity.Instance, slot.AggroRadiusMeters);
            }

            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static string ResolveTemplateHash(MobSlot slot)
        {
            switch (slot.Kind)
            {
                case MobKind.Spider:
                    return CombatTestMobArchetype.AlienSpiderZix.TemplateHash;
                case MobKind.Rollerrat:
                    return CombatTestMobArchetype.StowawayRollerrat.TemplateHash;
                case MobKind.Scout:
                case MobKind.Specialist:
                case MobKind.Minibull:
                case MobKind.Saltworm:
                default:
                    // A004 + MonsterData body (Scout/Specialist: no ExtTex in capture).
                    return CombatTestMobArchetype.TemplateHash;
            }
        }

        private static void PrepareArchetype(Character mob, MobSlot slot)
        {
            switch (slot.Kind)
            {
                case MobKind.Spider:
                    CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.AlienSpiderZix);
                    return;
                case MobKind.Rollerrat:
                    CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.StowawayRollerrat);
                    return;
                case MobKind.Scout:
                case MobKind.Specialist:
                case MobKind.Minibull:
                case MobKind.Saltworm:
                default:
                    // Do NOT Prepare IslandReet — that stamped reet mesh onto minibulls.
                    // MonsterData (+ ExtTex for minibull/saltworm) drives the live body.
                    return;
            }
        }

        private static void ResolveCaptureDamage(MobSlot slot, out int minDamage, out int maxDamage)
        {
            // Capture AttackInfo Amounts vs local player.
            switch (slot.Kind)
            {
                case MobKind.Saltworm:
                    // Capture 20260727-054719 AttackInfo Amounts: 13 normal, 21 critical.
                    minDamage = 13;
                    maxDamage = 21;
                    return;
                case MobKind.Minibull:
                    // Capture 20260726-220219 / 20260726-230559 AttackInfo Amounts: 5..20.
                    minDamage = 5;
                    maxDamage = 20;
                    return;
                case MobKind.Rollerrat:
                    // Capture 20260726-124832 AttackInfo Amounts: 5..11.
                    minDamage = 5;
                    maxDamage = 11;
                    return;
                case MobKind.Specialist:
                    // Capture 20260726-230559 AttackInfo Amounts: 16..52.
                    minDamage = 16;
                    maxDamage = 52;
                    return;
                case MobKind.Scout:
                    // Capture 20260726-230559 AttackInfo Amounts: 20..23.
                    minDamage = 20;
                    maxDamage = 23;
                    return;
                case MobKind.Spider:
                    // Capture 20260726-230559 AttackInfo Amount=11 (one landed hit).
                    minDamage = 9;
                    maxDamage = 14;
                    return;
                default:
                    minDamage = 6;
                    maxDamage = 10;
                    return;
            }
        }

        private static int ResolveCaptureXp(MobSlot slot)
        {
            // Capture Stat XP deltas after kills (side-bonus tips excluded).
            // Scout/Specialist: no kill XP observed yet — provisional spider-tier.
            if (string.Equals(slot.Name, "Harvey the Bully", StringComparison.OrdinalIgnoreCase))
            {
                return 890;
            }

            switch (slot.Kind)
            {
                case MobKind.Saltworm:
                    return 830;
                case MobKind.Minibull:
                    return slot.Level >= 10 ? 890 : 830;
                case MobKind.Rollerrat:
                    return 1;
                case MobKind.Specialist:
                    return slot.Level >= 12 ? 600 : 450;
                case MobKind.Scout:
                case MobKind.Spider:
                default:
                    return 400;
            }
        }

        private static void ApplyCaptureStats(Character mob, MobSlot slot)
        {
            SetStat(mob, StatIds.monsterdata, slot.MonsterData);
            SetStat(mob, StatIds.life, slot.Health);
            SetStat(mob, StatIds.health, slot.Health);
            SetStat(mob, StatIds.level, slot.Level);
            SetStat(mob, StatIds.npcfamily, slot.NpcFamily);
            SetStat(mob, StatIds.monsterscale, slot.Scale);
            SetStat(mob, StatIds.runspeed, slot.RunSpeed);
            // Capture: Minibull/Saltworm/Spider use 268980737; Scout/Specialist/Rollerrat use 268964353.
            int characterFlags = WildlifeCharacterFlags;
            if (slot.Kind == MobKind.Rollerrat
                || slot.Kind == MobKind.Scout
                || slot.Kind == MobKind.Specialist)
            {
                characterFlags = AlienHumanoidCharacterFlags;
            }

            SetStat(mob, StatIds.flags, characterFlags);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            SetStat(mob, StatIds.xp, ResolveCaptureXp(slot));
            int minDamage;
            int maxDamage;
            ResolveCaptureDamage(slot, out minDamage, out maxDamage);
            SetStat(mob, StatIds.mindamage, minDamage);
            SetStat(mob, StatIds.maxdamage, maxDamage);
            SetStat(mob, StatIds.damagetype, 1);
            SetStat(mob, StatIds.defaultattacktype, 1);
            if (slot.Kind == MobKind.Spider)
            {
                SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.AlienSpiderZix.CorpseCatMesh);
                SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.AlienSpiderZix.CorpseCatMesh);
            }
            else if (slot.Kind == MobKind.Rollerrat)
            {
                SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
                SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
            }
            else if (slot.Kind == MobKind.Minibull)
            {
                // Capture 20260726-220219 corpse-full-updates: CorpseCatMesh 26904.
                // Living body stays MonsterData/ExtTex; usable catmesh is for corpse CFU only.
                SetStat(mob, StatIds.catmesh, 26904);
                SetStat(mob, StatIds.displaycatmesh, 26904);
            }
            else if (slot.Kind == MobKind.Saltworm)
            {
                // Capture 20260727-054719 corpse-full-updates: CorpseCatMesh 17097.
                SetStat(mob, StatIds.catmesh, 17097);
                SetStat(mob, StatIds.displaycatmesh, 17097);
            }
            else if (slot.Kind == MobKind.Scout)
            {
                // Capture 20260726-230559: CorpseCatMesh 247278.
                SetStat(mob, StatIds.catmesh, 247278);
                SetStat(mob, StatIds.displaycatmesh, 247278);
            }
            else if (slot.Kind == MobKind.Specialist)
            {
                // Capture 20260726-230559: CorpseCatMesh 246962.
                SetStat(mob, StatIds.catmesh, 246962);
                SetStat(mob, StatIds.displaycatmesh, 246962);
            }
            else
            {
                // Living body is MonsterData / ExtTex — never stamp MissingVisualId on catmesh.
                SetStat(mob, StatIds.catmesh, 0);
                SetStat(mob, StatIds.displaycatmesh, 0);
            }

            if (mob.Textures != null)
            {
                mob.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    mob.Textures.Add(new AORebirth.Core.Textures.AOTextures(i, 0));
                }
            }

            if (mob.MeshLayer != null)
            {
                mob.MeshLayer.Clear();
            }

            if (mob.SocialMeshLayer != null)
            {
                mob.SocialMeshLayer.Clear();
            }
        }

        private static bool TryFindLivingMobNear(Playfield playfield, MobSlot slot, out ICharacter living)
        {
            living = null;
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, slot.Name, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - slot.X;
                float dz = candidate.Coordinates().z - slot.Z;
                if ((dx * dx) + (dz * dz) <= 9.0f)
                {
                    living = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void RegisterAggro(int npcInstance, float radiusMeters)
        {
            lock (AggroGate)
            {
                AggroRadiusByNpcInstance[npcInstance] = radiusMeters;
            }
        }

        private static void SetStat(Character mob, StatIds stat, int value)
        {
            if (mob == null || mob.Stats == null || mob.Stats[stat] == null)
            {
                return;
            }

            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }
    }
}
