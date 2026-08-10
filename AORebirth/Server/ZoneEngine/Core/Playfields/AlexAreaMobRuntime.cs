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
    using ZoneEngine.Core.Missions;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    internal static class AlexAreaMobRuntime
    {
        private enum MobKind
        {
            Docker,

            WasteCollector,

            GarbageFlea,

            CleaningRobot
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

            public string CombatProfileSelector { get; private set; }

            public int CombatEvidenceSourceIdentity { get; private set; }

            public int CombatAttackRangeMicrometers { get; private set; }

            public int CombatSpecialAttackWeaponUnknown5 { get; private set; }

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
                float z,
                string combatProfileSelector = "",
                int combatEvidenceSourceIdentity = 0,
                int combatAttackRangeMicrometers = 0,
                int combatSpecialAttackWeaponUnknown5 = 0)
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
                this.CombatProfileSelector = combatProfileSelector;
                this.CombatEvidenceSourceIdentity = combatEvidenceSourceIdentity;
                this.CombatAttackRangeMicrometers = combatAttackRangeMicrometers;
                this.CombatSpecialAttackWeaponUnknown5 = combatSpecialAttackWeaponUnknown5;
            }
        }

        private const int AreteLandingPlayfieldId = 6553;

        private const int MissingVisualId = 1234567890;

        private const string WasteCollectorCombatProfile =
            "fa6fc8256e910451-c850f3966b62b38e";

        private const int WasteCollectorCombatSource = unchecked((int)0x7988CAFF);

        // Exact reach comes from the generated capture-linked ItemDb projection.
        private const int WasteCollectorCombatRangeMicrometers = 0;

        private const string GarbageFleaLevel2CombatProfile =
            "46a87c17eefbd77b-c850f3966b62b38e";

        private const int GarbageFleaLevel2CombatSource = unchecked((int)0x7988C914);

        private const int GarbageFleaLevel2CombatRangeMicrometers = 0;

        private const string CleanmeisterCombatProfile =
            "028210688643a4d8-6ff86979de5c6526";

        private const int CleanmeisterCombatSource = unchecked((int)0x798915E0);

        private const int CleanmeisterCombatRangeMicrometers = 0;

        private const double RespawnSeconds = 30.0;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        private static readonly object AggroGate = new object();

        private static readonly Dictionary<int, float> AggroRadiusByNpcInstance = new Dictionary<int, float>();

        // Capture 20260720-204431 SCFU TextureOverrides / HasExtendedTextures.
        private static readonly byte[] GarbageFleaExtendedTextureOverrideData =
            {
                0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
                97, 108, 32, 35, 57, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 1, 118, 139,
                0, 0, 0, 0, 0, 0, 0, 1
            };

        // Capture 20260721-finish Mutated Garbage Flea Material #9 / 275711 (0x0434BF).
        private static readonly byte[] MutatedGarbageFleaExtendedTextureOverrideData =
            {
                0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
                97, 108, 32, 35, 57, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 4, 52, 191,
                0, 0, 0, 0, 0, 0, 0, 1
            };

        private static readonly byte[] WasteCollectorExtendedTextureOverrideData =
            {
                0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
                97, 108, 32, 35, 50, 50, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 67, 166,
                0, 0, 0, 0, 0, 0, 0, 1
            };

        private static readonly byte[] DockerExtendedTextureOverrideData =
            {
                0, 0, 11, 211, 77, 97, 116, 101, 114, 105,
                97, 108, 32, 35, 49, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 124, 81,
                0, 0, 0, 0, 0, 0, 0, 0, 77, 97,
                116, 101, 114, 105, 97, 108, 32, 35, 51, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 124, 80, 0, 0, 0, 0, 0, 0,
                0, 0
            };

        // Capture 20260720-204431 ScfuUnknown1Hex for Docker / Waste (robot body).
        private static readonly byte[] RobotCapturedScfuUnknown1 =
            {
                0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00
            };

        // Capture 20260720-204431 ScfuUnknown1Hex for Garbage Flea.
        private static readonly byte[] GarbageFleaCapturedScfuUnknown1 =
            {
                0xBF, 0xB4, 0xFF, 0x00, 0x3D, 0x59, 0x32, 0xC1, 0x3E, 0xFE, 0x20, 0xF8,
                0x02, 0x02, 0x01, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00
            };

        // Capture 20260720-212302 SCFU TextureOverrides Material #1 / 295519.
        private static readonly byte[] CleaningRobotExtendedTextureOverrideData =
            {
                0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
                97, 108, 32, 35, 49, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 4, 130, 95,
                0, 0, 0, 0, 0, 0, 0, 0
            };

        // Capture 20260722-cap-mob-drop-cred exact Alex-pad spots (3m cluster) + prior oasis fleas.
        private static readonly MobSlot[] Slots =
            {
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3489.426f, 5.110f, 875.835f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3493.492f, 5.110f, 892.886f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3494.314f, 5.223f, 906.400f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3495.097f, 5.110f, 880.632f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3495.464f, 5.110f, 890.018f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3496.017f, 5.110f, 903.404f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3498.525f, 7.730f, 913.251f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3499.633f, 5.110f, 855.734f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3503.399f, 5.110f, 888.679f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3512.169f, 5.110f, 892.637f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3516.197f, 5.110f, 914.061f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3523.440f, 5.110f, 898.358f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3524.225f, 5.110f, 879.666f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3492.532f, 5.110f, 866.601f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3497.703f, 9.380f, 918.384f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3506.614f, 5.110f, 891.661f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3507.345f, 8.185f, 930.825f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3510.761f, 5.110f, 864.661f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3510.884f, 7.982f, 923.173f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3519.717f, 9.080f, 940.067f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3526.509f, 9.501f, 951.883f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3539.143f, 9.087f, 942.056f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3542.399f, 8.045f, 890.165f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3549.620f, 7.396f, 906.258f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3555.855f, 8.336f, 915.219f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3558.766f, 8.575f, 927.072f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3567.060f, 8.218f, 918.754f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 1.332759f, 3585.885f, 6.998f, 921.242f, WasteCollectorCombatProfile, WasteCollectorCombatSource, WasteCollectorCombatRangeMicrometers, 0),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3502.315f, 5.110f, 902.829f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 15.576482f, 3502.761f, 5.110f, 891.211f, GarbageFleaLevel2CombatProfile, GarbageFleaLevel2CombatSource, GarbageFleaLevel2CombatRangeMicrometers, 117),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3509.731f, 7.394f, 926.952f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 15.576482f, 3529.015f, 5.110f, 891.201f, GarbageFleaLevel2CombatProfile, GarbageFleaLevel2CombatSource, GarbageFleaLevel2CombatRangeMicrometers, 117),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3541.526f, 8.190f, 892.471f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3549.231f, 9.371f, 938.996f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3554.468f, 8.074f, 919.910f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 15.576482f, 3562.391f, 5.110f, 864.537f, GarbageFleaLevel2CombatProfile, GarbageFleaLevel2CombatSource, GarbageFleaLevel2CombatRangeMicrometers, 117),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 12, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3565.514f, 8.356f, 913.615f),
                new MobSlot("Cleanmeister Intelligence Robot", MobKind.CleaningRobot, 297023, 2, 180, 1019, 100, 13, NpcAiProfile.Aggressive, 2.771750f, 3549.280f, 5.110f, 864.321f, CleanmeisterCombatProfile, CleanmeisterCombatSource, CleanmeisterCombatRangeMicrometers, 82),
                new MobSlot("IIV-X Advanced Docker", MobKind.Docker, 17649, 4, 323, 1019, 110, 15, NpcAiProfile.Passive, 0.0f, 3515.109f, 5.305f, 904.289f),
                new MobSlot("Supreme Collector of Waste", MobKind.WasteCollector, 17714, 4, 370, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3506.573f, 11.074f, 943.099f),
                // Prior oasis fleas (outside this capture cluster).
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.36f, 0.01f, 809.73f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.59f, 0.01f, 858.98f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 5, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.62f, 0.01f, 884.29f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3426.74f, 0.01f, 835.28f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3422.32f, 0.01f, 866.07f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 5, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3425.29f, 0.01f, 818.4f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3437.13f, 0.01f, 803.59f),
                new MobSlot("Mutated Garbage Flea", MobKind.GarbageFlea, 17657, 7, 559, 25, 200, 23, NpcAiProfile.Aggressive, 1.0f, 3422.907f, 0.01f, 878.7842f),
            };

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Mutated Garbage Flea", StringComparison.Ordinal))
            {
                data = (byte[])MutatedGarbageFleaExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Garbage Flea", StringComparison.Ordinal))
            {
                data = (byte[])GarbageFleaExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Waste Collector", StringComparison.Ordinal))
            {
                data = (byte[])WasteCollectorExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Cleaning Robot", StringComparison.Ordinal)
                || string.Equals(name, "Cleanmeister Intelligence Robot", StringComparison.Ordinal))
            {
                data = (byte[])CleaningRobotExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "32-V Docker", StringComparison.Ordinal)
                || string.Equals(name, "IIV-X Advanced Docker", StringComparison.Ordinal))
            {
                data = (byte[])DockerExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        internal static bool TryGetCapturedScfuUnknown1(string name, out byte[] data)
        {
            if (string.Equals(name, "Garbage Flea", StringComparison.Ordinal))
            {
                data = (byte[])GarbageFleaCapturedScfuUnknown1.Clone();
                return true;
            }

            if (string.Equals(name, "Waste Collector", StringComparison.Ordinal)
                || string.Equals(name, "32-V Docker", StringComparison.Ordinal)
                || string.Equals(name, "IIV-X Advanced Docker", StringComparison.Ordinal)
                || string.Equals(name, "Cleaning Robot", StringComparison.Ordinal)
                || string.Equals(name, "Cleanmeister Intelligence Robot", StringComparison.Ordinal))
            {
                data = (byte[])RobotCapturedScfuUnknown1.Clone();
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
            if (playfield == null)
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
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                double distance = candidate.Coordinates().Distance3D(npcCoord);
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
                if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    timers[i] = DateTime.MaxValue;
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AlexAreaMobRuntime spawned="
                + spawned
                + "/"
                + Slots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260720-204431");
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
                if (HasLivingMobNear(playfield, Slots[i]))
                {
                    timers[i] = DateTime.MaxValue;
                }
                else if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(timers[i] > DateTime.UtcNow)
                         && SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    timers[i] = DateTime.MaxValue;
                }
            }
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = Slots[slotIndex];
            NPCController controller = new NPCController { AiProfile = slot.AiProfile };
            string templateHash = slot.Kind == MobKind.GarbageFlea
                                      ? CombatTestMobArchetype.DuneFlea.TemplateHash
                                      : "A004";
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
            if (slot.Kind == MobKind.GarbageFlea)
            {
                CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.DuneFlea);
            }
            else
            {
                // Docker / Waste Collector / Cleanmeister share robot SCFU body (MonsterData + ExtTex),
                // not the flea archetype used previously for Waste.
                CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            }

            mob.Name = slot.Name;
            ApplyCaptureStats(mob, slot);
            controller.AiProfile = slot.AiProfile;

            // Capture 20260722-cap-mob-drop-cred AttackInfo vs player:
            // Amount=6.. AmmoCount=-1 WeaponSlot=1 HitType=Normal WeaponInstance=1279612721/22
            // → client red "You got hit … damage" text.
            CapturedEnemyCombatContract contract =
                AreteRegularMobCombatProfileSelector.Create(
                    "arete-alex-regular-mob-selector",
                    slot.CombatProfileSelector,
                    slot.CombatEvidenceSourceIdentity,
                    slot.CombatAttackRangeMicrometers,
                    slot.CombatSpecialAttackWeaponUnknown5,
                    slot.AiProfile);
            string combatFailure;
            bool combatReady = CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                mob,
                controller,
                contract,
                out combatFailure);
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            if (combatReady && slot.AggroRadiusMeters > 0f)
            {
                RegisterAggro(mob.Identity.Instance, slot.AggroRadiusMeters);
                MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
            }

            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
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
            SetStat(mob, StatIds.flags, 268964353);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            // Capture 20260720-204431: Docker +400 XP, Waste ~347/316, Flea +316.
            SetStat(mob, StatIds.xp, ResolveCaptureXp(slot));
            SetStat(mob, StatIds.mindamage, 0);
            SetStat(mob, StatIds.maxdamage, 0);
            // Capture AttackInfo HitType=Normal → client red hit line uses damagetype.
            SetStat(mob, StatIds.damagetype, 1);
            SetStat(mob, StatIds.defaultattacktype, 1);
            if (slot.Kind == MobKind.GarbageFlea)
            {
                SetStat(mob, StatIds.catmesh, 15231);
                SetStat(mob, StatIds.displaycatmesh, 15231);
            }
            else
            {
                // Capture dossier uses placeholder catMesh for robots (no live mesh id).
                SetStat(mob, StatIds.catmesh, MissingVisualId);
                SetStat(mob, StatIds.displaycatmesh, MissingVisualId);
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

        private static int ResolveCaptureXp(MobSlot slot)
        {
            switch (slot.Kind)
            {
                case MobKind.Docker:
                    return 400;
                case MobKind.WasteCollector:
                    return 330;
                case MobKind.GarbageFlea:
                    return 316;
                case MobKind.CleaningRobot:
                    return 316;
                default:
                    return 200;
            }
        }

        private static void SetStat(ICharacter mob, StatIds stat, int value)
        {
            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }

        private static void RegisterAggro(int npcInstance, float radiusMeters)
        {
            if (npcInstance == 0)
            {
                return;
            }

            lock (AggroGate)
            {
                AggroRadiusByNpcInstance[npcInstance] = radiusMeters;
            }
        }

        private static bool HasLivingMobNear(Playfield playfield, MobSlot slot)
        {
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
                if (dx * dx + dz * dz <= 6.25f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
