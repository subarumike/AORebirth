namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    internal sealed class CapturedTempleOfThreeWindsContentProvider
    {
        internal const int PlayfieldInstance = 647;
        internal const int ExpectedCultistProfileCount = 7;
        internal const int ExpectedCultistSpawnCount = 122;
        internal const int ExpectedProfileCount = 8;
        internal const int ExpectedSpawnCount = 125;
        internal const double CapturedDeathToRespawnSeconds = 310.0;
        internal const double RuntimeRespawnAfterNpcDespawnSeconds = 300.0;
        internal const double PolicyAutomaticAggroRadius = 7.0;
        internal const double CapturedMaximumObservedChaseDistance = 60.421;
        internal const double CapturedMedianAttackIntervalSeconds = 4.635295;

        private const string EvidenceReference =
            "20260721-030515,20260721-031913,20260721-032247,20260721-032547,20260721-033006";

        private static readonly OrdinaryEnemyAggressionProfile CultistAggression =
            new OrdinaryEnemyAggressionProfile(
                OrdinaryEnemyAggressionMode.Auto,
                PolicyAutomaticAggroRadius,
                true,
                true,
                OrdinaryEnemyEvidenceState.Policy);

        private static readonly CapturedEnemyCombatContract CultistCombat =
            CapturedEnemyCombatContract.FixedAttack(
                EvidenceReference + ":60 normal local-player hits 15..32;2 criticals 42..58 report-only",
                15,
                32,
                CapturedMedianAttackIntervalSeconds,
                6,
                0,
                0,
                -1);

        private static readonly CapturedEnemyCombatContract EternalSentinelCombat =
            CapturedTempleOfThreeWindsCombatCatalog.EternalSentinel();

        private static readonly RespawnPolicyDefinition CultistRespawn =
            new RespawnPolicyDefinition
            {
                RespawnPolicyKey = "totw.ordinary.300-after-npc-despawn",
                Mode = WorldRespawnMode.FixedDelay,
                FixedDelaySeconds = RuntimeRespawnAfterNpcDespawnSeconds,
                RespawnAtOriginalPosition = true,
                ResetHealth = true,
                ResetMovementState = true,
                ResetAggressionState = true,
                DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
                Evidence =
                    "20260721-033006: seven death-to-new-identity intervals 309.935..310.408 seconds; "
                    + "normalized against the server's fixed 10-second dead-NPC despawn boundary",
                Confidence = "CAPTURE_DERIVED_POLICY",
                Enabled = true
            };

        private static readonly ProfileSeed[] ProfileSeeds =
        {
            new ProfileSeed("totw.cultist.26074", 26074, 1579u, 40691, 204735u, 17532,
                "80000000000000008000000003010001000100010001000000020000"),
            new ProfileSeed("totw.cultist.26082", 26082, 1835u, 40634, 96330u, 17528,
                "00000000000000000000000003010001000100010001000000020000"),
            new ProfileSeed("totw.cultist.26103", 26103, 1419u, 40103, 30224u, 23365,
                "3E1BFAAD37B61EA53EA8B78C02020101000100010001000000020000"),
            new ProfileSeed("totw.cultist.26135", 26135, 1611u, 40271, 81802u, 23378,
                "00000000000000008000000003010001000100010001000000020000"),
            new ProfileSeed("totw.cultist.26137", 26137, 1867u, 40209, 204735u, 5934,
                "00000000000000000000000003010001000100010001000000020000"),
            new ProfileSeed("totw.cultist.26147", 26147, 1643u, 40172, 99144u, 17905,
                "00000000000000008000000003010001000100010001000000020000"),
            new ProfileSeed("totw.cultist.26149", 26149, 1899u, 40151, 99154u, 5941,
                "3D1F6090B62FB31C3FBFEF7502020101000100010001000000020000")
        };

        private static readonly CreditObservation[] CreditObservations =
        {
            new CreditObservation(26074, 20, 1), new CreditObservation(26074, 24, 1),
            new CreditObservation(26074, 27, 1), new CreditObservation(26074, 28, 2),
            new CreditObservation(26074, 31, 1), new CreditObservation(26074, 34, 2),
            new CreditObservation(26074, 35, 1),
            new CreditObservation(26082, 22, 2), new CreditObservation(26082, 28, 1),
            new CreditObservation(26082, 29, 1), new CreditObservation(26082, 30, 1),
            new CreditObservation(26082, 31, 3), new CreditObservation(26082, 32, 1),
            new CreditObservation(26082, 34, 1),
            new CreditObservation(26103, 21, 1), new CreditObservation(26103, 23, 1),
            new CreditObservation(26103, 28, 1), new CreditObservation(26103, 30, 1),
            new CreditObservation(26103, 32, 2), new CreditObservation(26103, 33, 2),
            new CreditObservation(26103, 34, 1), new CreditObservation(26103, 35, 1),
            new CreditObservation(26135, 20, 1), new CreditObservation(26135, 26, 1),
            new CreditObservation(26135, 28, 2), new CreditObservation(26135, 30, 1),
            new CreditObservation(26135, 32, 2), new CreditObservation(26135, 35, 1),
            new CreditObservation(26137, 21, 2), new CreditObservation(26137, 23, 1),
            new CreditObservation(26137, 25, 1), new CreditObservation(26137, 28, 2),
            new CreditObservation(26137, 33, 1), new CreditObservation(26137, 34, 1),
            new CreditObservation(26137, 35, 1),
            new CreditObservation(26147, 20, 1), new CreditObservation(26147, 21, 3),
            new CreditObservation(26147, 22, 1), new CreditObservation(26147, 24, 1),
            new CreditObservation(26147, 27, 1), new CreditObservation(26147, 28, 1),
            new CreditObservation(26147, 29, 1), new CreditObservation(26147, 33, 2),
            new CreditObservation(26147, 34, 1), new CreditObservation(26147, 35, 1),
            new CreditObservation(26149, 21, 2), new CreditObservation(26149, 22, 3),
            new CreditObservation(26149, 26, 3), new CreditObservation(26149, 29, 2),
            new CreditObservation(26149, 30, 1), new CreditObservation(26149, 31, 1),
            new CreditObservation(26149, 32, 1), new CreditObservation(26149, 33, 1),
            new CreditObservation(26149, 34, 1)
        };

        private static readonly SpawnSeed[] SpawnSeeds =
        {
            new SpawnSeed(0x79822FDF, "totw.cultist.26149", 20, 670, 0, 99, 138, 190.380386f, 24.011248f, 187.835831f, 0f, -0.997093f, 0f, 0.076188f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834A37, "totw.cultist.26137", 29, 1165, 0, 101, 200, 230.163132f, 30.011251f, 222.139343f, 0f, 1f, 0f, 0.0009f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834AFB, "totw.cultist.26082", 21, 710, 0, 99, 145, 117.01f, 27.011251f, 189.172226f, 0f, 0.318026f, 0f, 0.948082f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x79834B66, "totw.cultist.26149", 24, 830, 0, 100, 166, 220.499313f, 27.01125f, 187.81398f, 0f, 0.999994f, 0f, -0.003386f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834B77, "totw.cultist.26149", 25, 869, 0, 100, 172, 230.16362f, 31.01125f, 232.200638f, 0f, 1f, 0f, -0.000607f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834CB9, "totw.cultist.26147", 35, 1609, 0, 102, 240, 199.799072f, 25.011248f, 183.20224f, 0f, 0.418738f, 0f, 0.908106f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834CBB, "totw.cultist.26147", 20, 670, 0, 99, 138, 199.8179f, 25.011248f, 186.886444f, 0f, -0.830368f, 0f, -0.557216f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834CC7, "totw.cultist.26137", 35, 1609, 0, 102, 240, 210.872055f, 26.011248f, 183.307175f, 0f, -0.485796f, 0f, -0.87407f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834CC9, "totw.cultist.26137", 22, 750, 0, 99, 152, 210.222672f, 26.011251f, 187.3523f, 0f, -0.88986f, 0f, -0.456232f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834CE1, "totw.cultist.26149", 32, 1387, 0, 102, 220, 221.669174f, 27.011248f, 183.99f, 0f, -0.621175f, 0f, 0.783672f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834D05, "totw.cultist.26082", 31, 1313, 0, 102, 213, 230.292419f, 27.011248f, 182.203339f, 0f, -0.133849f, 0f, 0.991002f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x79834D07, "totw.cultist.26082", 21, 710, 0, 99, 145, 235.518921f, 27.011248f, 187.9471f, 0f, 0.312428f, 0f, 0.949942f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x79834D0B, "totw.cultist.26082", 27, 1017, 0, 101, 186, 236.569458f, 27.011248f, 181.086258f, 0f, 0.449486f, 0f, 0.893287f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x79834D0D, "totw.cultist.26082", 22, 750, 0, 99, 152, 229.3836f, 27.011251f, 188.790146f, 0f, -0.776424f, 0f, -0.63021f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x79834DA6, "totw.cultist.26149", 33, 1461, 0, 102, 227, 132.831329f, 31.011251f, 279.093353f, 0f, -0.759086f, 0f, -0.65099f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DA8, "totw.cultist.26137", 31, 1313, 0, 102, 213, 135.636139f, 26.011251f, 182.185959f, 0f, 0.999903f, 0f, 0.013899f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834DCB, "totw.cultist.26149", 22, 750, 0, 99, 152, 236.01f, 31.011248f, 239.93512f, 0f, 0.996917f, 0f, 0.078459f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DCE, "totw.cultist.26074", 20, 670, 0, 99, 138, 239.227859f, 31.011248f, 272.61322f, 0f, 0.855116f, 0f, -0.518438f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DCF, "totw.cultist.26103", 28, 1091, 0, 101, 193, 233.5167f, 31.011248f, 278.798279f, 0f, 0.998342f, 0f, 0.057559f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DD0, "totw.cultist.26149", 31, 1313, 0, 102, 213, 223.2721f, 31.011248f, 279.515656f, 0f, 0.928229f, 0f, 0.372007f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DD1, "totw.cultist.26137", 30, 1239, 0, 101, 206, 212.962524f, 31.011248f, 278.920471f, 0f, 0.81753f, 0f, 0.575887f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DDF, "totw.cultist.26074", 28, 1091, 0, 101, 193, 106.821396f, 31.011251f, 272.36795f, 0f, 0.817157f, 0f, 0.576415f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DE9, "totw.cultist.26135", 28, 1091, 0, 101, 193, 143.03598f, 31.011248f, 239.391754f, 0f, -0.517479f, 0f, 0.855695f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DEA, "totw.cultist.26074", 24, 830, 0, 100, 166, 153.697388f, 31.011248f, 238.883636f, 0f, -0.493529f, 0f, 0.86973f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DF3, "totw.cultist.26137", 33, 1461, 0, 102, 227, 182.485565f, 31.011248f, 238.898026f, 0f, -0.1678f, 0f, -0.985821f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834DF5, "totw.cultist.26082", 30, 1239, 0, 101, 206, 192.620346f, 31.011248f, 239.131622f, 0f, -0.291802f, 0f, -0.956478f, 0x022A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834E50, "totw.cultist.26103", 23, 790, 0, 100, 159, 113.378487f, 31.011248f, 279.4885f, 0f, 0.997124f, 0f, -0.075784f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834E8E, "totw.cultist.26149", 34, 1535, 0, 102, 234, 189.504379f, 24.011248f, 183.302277f, 0f, -0.497945f, 0f, 0.867207f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x79834EC1, "totw.cultist.26137", 28, 1091, 0, 101, 193, 184.525482f, 33.611248f, 346.13913f, 0f, -0.57116f, 0f, 0.820839f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834EC3, "totw.cultist.26082", 33, 1461, 0, 102, 227, 184.942245f, 33.611244f, 360.01f, 0f, -0.697822f, 0f, 0.716271f, 0x022A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834EC5, "totw.cultist.26147", 33, 1461, 0, 102, 227, 178.40451f, 33.611244f, 366.952759f, 0f, -0.732982f, 0f, 0.680248f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834EC9, "totw.cultist.26074", 22, 750, 0, 99, 152, 154.539825f, 33.611244f, 366.535858f, 0f, 0.743539f, 0f, -0.668692f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834ECC, "totw.cultist.26103", 30, 1239, 0, 101, 206, 142.8622f, 33.611244f, 366.080566f, 0f, -0.894483f, 0f, 0.447103f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834ECD, "totw.cultist.26137", 26, 943, 0, 101, 179, 118.742416f, 33.611244f, 366.901f, 0f, -0.902793f, 0f, -0.430074f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x79834ECF, "totw.cultist.26147", 23, 790, 0, 100, 159, 93.92581f, 33.611244f, 366.7496f, 0f, 0.877368f, 0f, 0.479819f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983F839, "totw.cultist.26074", 34, 1535, 0, 102, 234, 190.64537f, 24.011248f, 185.092133f, 0f, 0.72076f, 0f, 0.693184f, 0x020B4ACBu, 193.804138f, 24.011387f, 184.968872f, "20260721-031913"),
            new SpawnSeed(0x7983F8FC, "totw.cultist.26137", 21, 710, 0, 99, 145, 115.589943f, 31.011248f, 239.2707f, 0f, 0.666171f, 0f, 0.745799f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983F8FD, "totw.cultist.26135", 20, 670, 0, 99, 138, 159.948517f, 31.011248f, 242.194f, 0f, 0.703039f, 0f, 0.711152f, 0x020B4ACBu, 166.449554f, 31.011248f, 242.268585f, "20260721-032547"),
            new SpawnSeed(0x7983F8FE, "totw.cultist.26147", 24, 830, 0, 100, 166, 133.204834f, 31.011248f, 238.99f, 0f, -0.485827f, 0f, 0.874055f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983F8FF, "totw.cultist.26082", 31, 1313, 0, 102, 213, 122.97686f, 31.011251f, 238.299988f, 0f, 0.128111f, 0f, 0.99176f, 0x022A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983F9F4, "totw.cultist.26149", 26, 943, 0, 101, 179, 172.567871f, 31.011248f, 239.075089f, 0f, -0.440226f, 0f, 0.897887f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FA5D, "totw.cultist.26147", 35, 1609, 0, 102, 240, 196.147751f, 31.011248f, 241.525772f, 0f, 0.704761f, 0f, 0.709445f, 0x020B4ACBu, 204.114792f, 31.011248f, 241.578552f, "20260721-032547"),
            new SpawnSeed(0x7983FAC2, "totw.cultist.26147", 22, 750, 0, 99, 152, 145.5497f, 25.011251f, 182.140411f, 0f, 0.999773f, 0f, 0.021312f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FAE0, "totw.cultist.26149", 23, 790, 0, 100, 159, 109.965851f, 31.011248f, 239.707611f, 0f, 0.384085f, 0f, 0.923298f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FAE3, "totw.cultist.26103", 29, 1165, 0, 101, 200, 106.854515f, 31.011248f, 244.721329f, 0f, 0.700909f, 0f, 0.71325f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FAEF, "totw.cultist.26147", 22, 750, 0, 99, 152, 178.176666f, 20.01125f, 166.317612f, 0f, -0.532194f, 0f, 0.846622f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FAFD, "totw.cultist.26135", 33, 1461, 0, 102, 227, 167.646072f, 20.01125f, 166.519638f, 0f, 0.003358f, 0f, 0.999994f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB02, "totw.cultist.26074", 23, 790, 0, 100, 159, 236.5462f, 13.011248f, 261.8455f, 0f, 0.306892f, 0f, 0.951744f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB03, "totw.cultist.26103", 26, 943, 0, 101, 179, 236.433884f, 13.011249f, 271.713135f, 0f, 0.771679f, 0f, 0.636012f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB27, "totw.cultist.26135", 30, 1239, 0, 101, 206, 210.595856f, 31.011248f, 246.367783f, 0f, -0.738347f, 0f, 0.674421f, 0x020B4ACBu, 199.90918f, 31.011248f, 245.3987f, "20260721-032547"),
            new SpawnSeed(0x7983FB29, "totw.cultist.26135", 32, 1387, 0, 102, 220, 212.88266f, 31.011248f, 239.140732f, 0f, -0.129114f, 0f, -0.99163f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB2A, "totw.cultist.26074", 28, 1091, 0, 101, 193, 223.166748f, 31.011248f, 238.806f, 0f, -0.479386f, 0f, 0.877605f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB2C, "totw.cultist.26103", 35, 1609, 0, 102, 240, 230.202515f, 31.011248f, 239.92131f, 0f, 0.996917f, 0f, 0.078459f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB2D, "totw.cultist.26137", 21, 710, 0, 99, 145, 239.055023f, 31.011248f, 245.01f, 0f, -0.560662f, 0f, 0.828045f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB2E, "totw.cultist.26147", 28, 1091, 0, 101, 193, 225.50853f, 31.011248f, 244.793411f, 0f, 0.379942f, 0f, 0.92501f, 0x020B4ACBu, 230.6722f, 31.011248f, 250.02066f, "20260721-032547"),
            new SpawnSeed(0x7983FB30, "totw.cultist.26147", 33, 1461, 0, 102, 227, 202.486511f, 31.011248f, 238.8394f, 0f, -0.264698f, 0f, -0.964332f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB3C, "totw.cultist.26074", 20, 670, 0, 99, 138, 233.1691f, 31.01125f, 228.109924f, 0f, -0.000797f, 0f, 1f, 0x020B4ACBu, 233.1472f, 31.011248f, 234.020447f, "20260721-032547"),
            new SpawnSeed(0x7983FB3D, "totw.cultist.26137", 24, 830, 0, 100, 166, 234.776688f, 30.011248f, 221.680542f, 0f, -0.347459f, 0f, 0.937696f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB3E, "totw.cultist.26147", 26, 943, 0, 101, 179, 234.7837f, 29.011248f, 211.885132f, 0f, -0.259275f, 0f, 0.965804f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB3F, "totw.cultist.26149", 32, 1387, 0, 102, 220, 235.530441f, 28.01125f, 202.01413f, 0f, 0.932341f, 0f, -0.361581f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB40, "totw.cultist.26149", 28, 1091, 0, 101, 193, 234.7532f, 31.011248f, 234.044861f, 0f, 0.982121f, 0f, -0.188253f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB41, "totw.cultist.26147", 33, 1461, 0, 102, 227, 231.223984f, 29.011248f, 211.406326f, 0f, 0.150035f, 0f, 0.988681f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB43, "totw.cultist.26149", 23, 790, 0, 100, 159, 230.627243f, 28.011248f, 202.7523f, 0f, -0.574134f, 0f, -0.818762f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB84, "totw.cultist.26103", 30, 1239, 0, 101, 206, 172.945145f, 20.011248f, 164.810043f, 0f, -0.068324f, 0f, 0.997663f, 0x020B4ACBu, 172.5354f, 19.986961f, 167.715958f, "20260721-031913"),
            new SpawnSeed(0x7983FB85, "totw.cultist.26149", 26, 943, 0, 101, 179, 176.364685f, 23.011248f, 183.9572f, 0f, -0.245744f, 0f, -0.969335f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB88, "totw.cultist.26149", 29, 1165, 0, 101, 200, 155.195114f, 24.011248f, 186.487366f, 0f, 0.830668f, 0f, 0.55677f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB8B, "totw.cultist.26147", 33, 1461, 0, 102, 227, 144.402344f, 25.011248f, 187.403473f, 0f, 0.88438f, 0f, 0.466771f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB8D, "totw.cultist.26074", 34, 1535, 0, 102, 234, 135.47821f, 26.011248f, 184.99f, 0f, -0.689761f, 0f, 0.724037f, 0x020B4ACBu, 132.183167f, 26.011248f, 185.166733f, "20260721-031913"),
            new SpawnSeed(0x7983FB8E, "totw.cultist.26137", 29, 1165, 0, 101, 200, 135.626053f, 26.011248f, 187.188171f, 0f, 0.900838f, 0f, 0.434158f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB8F, "totw.cultist.26149", 21, 710, 0, 99, 145, 125.740845f, 27.01125f, 182.755615f, 0f, 0.523873f, 0f, 0.851796f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB90, "totw.cultist.26149", 34, 1535, 0, 102, 234, 123.173584f, 27.011248f, 186.1207f, 0f, 0.805727f, 0f, 0.592288f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB93, "totw.cultist.26149", 29, 1165, 0, 101, 200, 114.847145f, 28.011248f, 201.670715f, 0f, -0.276033f, 0f, 0.961148f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB94, "totw.cultist.26149", 32, 1387, 0, 102, 220, 111.28138f, 28.011248f, 202.087631f, 0f, -0.987884f, 0f, -0.155199f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB96, "totw.cultist.26074", 31, 1313, 0, 102, 213, 112.99f, 31.011248f, 233.124557f, 0f, 0.063763f, 0f, 0.997965f, 0x020B4ACBu, 112.578133f, 31.011248f, 228.173111f, "20260721-032547"),
            new SpawnSeed(0x7983FB97, "totw.cultist.26137", 34, 1535, 0, 102, 234, 169.151962f, 23.011248f, 183.808289f, 0f, -0.665247f, 0f, -0.746623f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FB98, "totw.cultist.26147", 27, 1017, 0, 101, 186, 110.695648f, 29.011248f, 213.613617f, 0f, -0.2068f, 0f, -0.978383f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB9A, "totw.cultist.26147", 21, 710, 0, 99, 145, 115.235062f, 29.011248f, 213.78894f, 0f, 0.28999f, 0f, -0.957031f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB9B, "totw.cultist.26137", 23, 790, 0, 100, 159, 111.270325f, 30.011248f, 222.88176f, 0f, -0.167984f, 0f, -0.98579f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FB9F, "totw.cultist.26149", 22, 750, 0, 99, 152, 114.079033f, 31.011248f, 234.566864f, 0f, 0.991107f, 0f, -0.133066f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBA1, "totw.cultist.26149", 22, 750, 0, 99, 152, 110.955833f, 31.011248f, 232.6234f, 0f, -0.95582f, 0f, -0.293954f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBA2, "totw.cultist.26137", 25, 869, 0, 100, 172, 114.549713f, 30.011248f, 222.970016f, 0f, 0.23618f, 0f, -0.971709f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBA4, "totw.cultist.26082", 34, 1535, 0, 102, 234, 140.11232f, 31.011248f, 241.542542f, 0f, 0.719177f, 0f, 0.694827f, 0x022B4ACBu, 156.901f, 31.011248f, 240.964142f, "20260721-032547"),
            new SpawnSeed(0x7983FBA5, "totw.cultist.26082", 22, 750, 0, 99, 152, 109.840225f, 27.011248f, 187.818253f, 0f, -0.568404f, 0f, 0.82275f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x7983FBA6, "totw.cultist.26082", 22, 750, 0, 99, 152, 115.99f, 27.011248f, 181.947815f, 0f, 0.256423f, 0f, 0.966565f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x7983FBA7, "totw.cultist.26082", 30, 1239, 0, 101, 206, 109.413795f, 27.011248f, 181.409485f, 0f, 0.910155f, 0f, 0.414268f, 0x022A4ACBu, null, null, null, "20260721-032247"),
            new SpawnSeed(0x7983FBAD, "totw.cultist.26147", 35, 1609, 0, 102, 240, 96.31124f, 13.011248f, 271.948639f, 0f, 0.080306f, 0f, 0.99677f, 0x020B4ACBu, 97.14606f, 13.011248f, 280.0547f, "20260721-032547"),
            new SpawnSeed(0x7983FBAF, "totw.cultist.26149", 35, 1609, 0, 102, 240, 91.91414f, 13.011248f, 270.8641f, 0f, -0.766699f, 0f, 0.642007f, 0x020B4ACBu, 89.99637f, 13.011248f, 270.5225f, "20260721-032547"),
            new SpawnSeed(0x7983FBB4, "totw.cultist.26103", 35, 1609, 0, 102, 240, 106.113419f, 13.011248f, 265.483337f, 0f, 0.359378f, 0f, 0.933192f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBB6, "totw.cultist.26135", 35, 1609, 0, 102, 240, 105.587105f, 13.011248f, 270.377838f, 0f, 0.918894f, 0f, -0.394506f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBB8, "totw.cultist.26135", 35, 1609, 0, 102, 240, 108.882683f, 13.01125f, 268.545563f, 0f, -0.707854f, 0f, 0.706359f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBB9, "totw.cultist.26103", 35, 1609, 0, 102, 240, 103.562271f, 13.011248f, 268.454346f, 0f, 0.394506f, 0f, 0.918894f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBD0, "totw.cultist.26103", 24, 830, 0, 100, 166, 184.6049f, 33.611244f, 320.9364f, 0f, -0.391838f, 0f, 0.920034f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBD8, "totw.cultist.26147", 23, 790, 0, 100, 159, 118.4612f, 33.611244f, 313.4101f, 0f, 0.22372f, 0f, 0.974653f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBD9, "totw.cultist.26137", 23, 790, 0, 100, 159, 95.200714f, 33.611248f, 313.4347f, 0f, -0.206747f, 0f, 0.978395f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBDA, "totw.cultist.26149", 20, 670, 0, 99, 138, 87.71508f, 33.611244f, 319.843f, 0f, 0.267769f, 0f, 0.963483f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBDF, "totw.cultist.26135", 29, 1165, 0, 101, 200, 86.89195f, 33.611244f, 359.407471f, 0f, 0.669633f, 0f, 0.742692f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FBE1, "totw.cultist.26149", 30, 1239, 0, 101, 206, 155.70285f, 24.011248f, 182.255554f, 0f, 0.487845f, 0f, 0.872929f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7983FBE5, "totw.cultist.26082", 31, 1313, 0, 102, 213, 157.618912f, 31.011248f, 243.16568f, 0f, -0.71307f, 0f, 0.701093f, 0x022B4ACBu, 143.188843f, 31.011248f, 242.921219f, "20260721-032547"),
            new SpawnSeed(0x7983FBE7, "totw.cultist.26103", 21, 710, 0, 99, 145, 162.610428f, 31.011248f, 238.852432f, 0f, -0.231675f, 0f, 0.972793f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC33, "totw.cultist.26074", 27, 1017, 0, 101, 186, 87.89184f, 33.611244f, 346.741455f, 0f, -0.322089f, 0f, -0.946709f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC37, "totw.cultist.26137", 28, 1091, 0, 101, 193, 179.208221f, 33.611244f, 313.516876f, 0f, -0.453263f, 0f, 0.891377f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC38, "totw.cultist.26103", 35, 1609, 0, 102, 240, 153.527618f, 33.611244f, 313.435425f, 0f, 0.116785f, 0f, -0.993157f, 0x030A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC39, "totw.cultist.26149", 22, 750, 0, 99, 152, 165.118011f, 33.611244f, 313.118469f, 0f, -0.219696f, 0f, 0.975568f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC3A, "totw.cultist.26135", 35, 1609, 0, 102, 240, 130.337448f, 33.611244f, 313.522034f, 0f, 0.071503f, 0f, 0.99744f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC43, "totw.cultist.26149", 30, 1239, 0, 101, 206, 248.714935f, 13.011248f, 300.3354f, 0f, 0.999952f, 0f, 0.009831f, 0x020B4ACBu, 248.9989f, 13.011248f, 285.8937f, "20260721-032547"),
            new SpawnSeed(0x7983FC46, "totw.cultist.26082", 20, 670, 0, 99, 138, 248.752579f, 13.01125f, 316.1802f, 0f, -0.918162f, 0f, 0.396205f, 0x022A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7983FC47, "totw.cultist.26137", 29, 1165, 0, 101, 200, 241.515884f, 13.011248f, 316.302582f, 0f, -0.864698f, 0f, -0.502292f, 0x020A4ACBu, null, null, null, "20260721-032547"),
            new SpawnSeed(0x7984B36E, "totw.cultist.26074", 20, 670, 0, 99, 138, 175.9834f, 20.011248f, 151.608719f, 0f, -0.971984f, 0f, 0.235045f, 0x020A4ACBu, null, null, null, "20260721-030515"),
            new SpawnSeed(0x7984B373, "totw.cultist.26135", 32, 1387, 0, 102, 220, 165.67662f, 20.011248f, 129.640747f, 0f, 0.715881f, 0f, 0.698222f, 0x020A4ACBu, null, null, null, "20260721-030515"),
            new SpawnSeed(0x7984B374, "totw.cultist.26137", 28, 1091, 0, 101, 193, 166.540588f, 20.011251f, 62.02947f, 0f, 0.577038f, 0f, 0.816717f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7984B375, "totw.cultist.26103", 34, 1535, 0, 102, 234, 172.633575f, 20.011248f, 60.898228f, 0f, 0.21481f, 0f, 0.976656f, 0x020B4ACBu, 173.693069f, 20.011236f, 70.4245f, "20260721-031913"),
            new SpawnSeed(0x7984B376, "totw.cultist.26082", 31, 1313, 0, 102, 213, 179.106232f, 20.01125f, 61.728966f, 0f, 0.69096f, 0f, 0.722893f, 0x022A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7984B378, "totw.cultist.26147", 34, 1535, 0, 102, 234, 168.2736f, 20.011248f, 100.881378f, 0f, 0.912063f, 0f, 0.410051f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7984B379, "totw.cultist.26147", 20, 670, 0, 99, 138, 179.249588f, 20.011248f, 100.92115f, 0f, 0.859256f, 0f, -0.511546f, 0x020A4ACBu, null, null, null, "20260721-031913"),
            new SpawnSeed(0x7984B37C, "totw.cultist.26149", 30, 1239, 0, 101, 206, 172.400726f, 20.011248f, 137.289841f, 0f, 0.012971f, 0f, 0.999916f, 0x020B4ACBu, 172.646286f, 20.01123f, 146.753067f, "20260721-031913"),
            new SpawnSeed(0x7984B3A8, "totw.cultist.26082", 28, 1091, 0, 101, 193, 109.841995f, 31.01125f, 247.247269f, 0f, 0.996048f, 0f, 0.08882f, 0x022A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3AB, "totw.cultist.26135", 28, 1091, 0, 101, 193, 134.900909f, 31.011248f, 250.129929f, 0f, 0.996521f, 0f, 0.08334f, 0x020A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3C9, "totw.cultist.26074", 24, 830, 0, 100, 166, 112.822586f, 27.011248f, 195.166153f, 0f, 0.660544f, 0f, 0.750787f, 0x020A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3D9, "totw.cultist.26147", 34, 1535, 0, 102, 234, 224.826416f, 31.011248f, 259.7224f, 0f, 0.983969f, 0f, 0.178342f, 0x020A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3DF, "totw.cultist.26135", 26, 943, 0, 101, 179, 215.4928f, 31.011248f, 267.682159f, 0f, 0.992772f, 0f, 0.120012f, 0x020A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3E0, "totw.cultist.26082", 32, 1387, 0, 102, 220, 235.172958f, 31.011251f, 250.139069f, 0f, 0.996917f, 0f, 0.078459f, 0x022A4ACBu, null, null, null, "20260721-033006"),
            new SpawnSeed(0x7984B3E2, "totw.cultist.26147", 31, 1313, 0, 102, 213, 123.0731f, 31.011248f, 258.01f, 0f, -0.999334f, 0f, 0.036483f, 0x020A4ACBu, null, null, null, "20260721-033006")
        };

        internal OrdinaryEnemyProfile[] GetProfiles()
        {
            OrdinaryEnemyProfile[] profiles = ProfileSeeds
                .Select(BuildProfile)
                .Concat(new[] { BuildEternalSentinelProfile() })
                .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                .ToArray();
            if (profiles.Length != ExpectedProfileCount)
            {
                throw new InvalidOperationException("Temple ordinary profile count changed unexpectedly.");
            }

            return profiles;
        }

        internal OrdinaryEnemySpawnDefinition[] GetSpawns()
        {
            Dictionary<string, ProfileSeed> profiles = ProfileSeeds.ToDictionary(
                value => value.ProfileKey,
                StringComparer.Ordinal);
            OrdinaryEnemySpawnDefinition[] spawns = SpawnSeeds
                .Select(seed => BuildSpawn(seed, profiles[seed.ProfileKey]))
                .Concat(BuildEternalSentinelSpawns())
                .OrderBy(value => value.SourceIdentity)
                .ToArray();
            if (spawns.Length != ExpectedSpawnCount
                || spawns.Select(value => value.SourceIdentity).Distinct().Count() != spawns.Length)
            {
                throw new InvalidOperationException("Temple ordinary spawn rows are incomplete or duplicated.");
            }

            return spawns;
        }

        private static OrdinaryEnemyProfile BuildEternalSentinelProfile()
        {
            const string evidence =
                "20260721-041439/043204: exact Eternal Sentinel SCFU, 17..18 normal damage, "
                + "CATMesh 41664, empty observed loot and level-credit outcomes";
            return new OrdinaryEnemyProfile(
                "totw.ordinary.eternal-sentinel.41690",
                "totw.ordinary.eternal-sentinel",
                "Eternal Sentinel",
                41690,
                OrdinaryEnemyConstructionMode.CapturedDirect,
                string.Empty,
                new OrdinaryEnemyAppearanceProfile(
                    3,
                    1,
                    6,
                    0,
                    1,
                    268964353,
                    0,
                    0,
                    136,
                    0,
                    31,
                    1,
                    1227u,
                    0,
                    true,
                    false,
                    new[]
                    {
                        new OrdinaryEnemyTextureProfile(0, 0, 0),
                        new OrdinaryEnemyTextureProfile(1, 0, 0),
                        new OrdinaryEnemyTextureProfile(2, 0, 0),
                        new OrdinaryEnemyTextureProfile(3, 0, 0),
                        new OrdinaryEnemyTextureProfile(4, 0, 0)
                    },
                    new[]
                    {
                        new OrdinaryEnemyMeshProfile(1, 81804u, 0, 2)
                    },
                    OrdinaryEnemyScfuProfile.CapturedExact),
                CultistAggression,
                new OrdinaryEnemyCombatProfile(
                    OrdinaryEnemyCombatMode.UnarmedMelee,
                    OrdinaryEnemyDamageSource.CapturedFixed,
                    false,
                    EternalSentinelCombat,
                    OrdinaryEnemyEvidenceState.Observed),
                BuildEternalSentinelLoot(evidence),
                new OrdinaryEnemyCorpseProfile(
                    OrdinaryEnemyCorpsePacketProfile.Generic,
                    30.0,
                    120.0,
                    30.0,
                    41664,
                    evidence),
                new[] { evidence },
                false,
                false);
        }

        private static OrdinaryEnemyLootProfile BuildEternalSentinelLoot(string evidence)
        {
            return new OrdinaryEnemyLootProfile(
                OrdinaryEnemyLootEvidence.NoneProven,
                new OrdinaryEnemyLootEntry[0],
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                0,
                true,
                5,
                5,
                evidence,
                OrdinaryEnemyEvidenceState.Observed,
                null,
                null,
                new[]
                {
                    new OrdinaryEnemyLevelCreditRule(18, 111, 111, 1, evidence, OrdinaryEnemyEvidenceState.Observed),
                    new OrdinaryEnemyLevelCreditRule(19, 118, 118, 1, evidence, OrdinaryEnemyEvidenceState.Observed),
                    new OrdinaryEnemyLevelCreditRule(20, 124, 124, 1, evidence, OrdinaryEnemyEvidenceState.Observed)
                });
        }

        private static OrdinaryEnemySpawnDefinition[] BuildEternalSentinelSpawns()
        {
            return new[]
            {
                BuildEternalSentinelSpawn(
                    unchecked((int)0x7983FA22u),
                    18,
                    247,
                    98,
                    62,
                    92.95905f,
                    12.187273f,
                    290.411774f),
                BuildEternalSentinelSpawn(
                    unchecked((int)0x7983FA26u),
                    20,
                    280,
                    99,
                    69,
                    89.83454f,
                    11.4112511f,
                    306.880341f),
                BuildEternalSentinelSpawn(
                    unchecked((int)0x7983FBC2u),
                    18,
                    247,
                    98,
                    62,
                    59.7886162f,
                    13.16832f,
                    283.302765f)
            };
        }

        private static OrdinaryEnemySpawnDefinition BuildEternalSentinelSpawn(
            int sourceIdentity,
            int level,
            int health,
            int scale,
            int runSpeed,
            float x,
            float y,
            float z)
        {
            return new OrdinaryEnemySpawnDefinition(
                "totw.ordinary." + sourceIdentity.ToString("X8", CultureInfo.InvariantCulture),
                sourceIdentity,
                "totw.ordinary.eternal-sentinel.41690",
                PlayfieldInstance,
                level,
                health,
                0,
                scale,
                runSpeed,
                x,
                y,
                z,
                0f,
                0f,
                0f,
                1f,
                OrdinaryEnemyMovementMode.Static,
                new OrdinaryEnemyWaypoint[0],
                false,
                true,
                true,
                0x020A4A43u,
                0,
                HexToBytes("80000000000000000000000003010001000100010001000000020000"),
                0,
                OrdinaryEnemyEvidenceState.Policy,
                RuntimeRespawnAfterNpcDespawnSeconds,
                OrdinaryEnemyRuntimeDisposition.Active,
                string.Empty,
                "20260721-041439",
                string.Empty,
                null,
                WorldRespawnPolicyAssignment.Explicit(CultistRespawn));
        }

        private static OrdinaryEnemyProfile BuildProfile(ProfileSeed seed)
        {
            uint appearance = seed.AppearanceValue;
            return new OrdinaryEnemyProfile(
                seed.ProfileKey,
                "totw.ordinary.cultist",
                "Cultist",
                seed.MonsterData,
                OrdinaryEnemyConstructionMode.CapturedDirect,
                string.Empty,
                new OrdinaryEnemyAppearanceProfile(
                    (int)(appearance & 7),
                    (int)((appearance & 31) >> 3),
                    Math.Max(1, Math.Min(7, (int)((appearance & 255) >> 5))),
                    (int)((appearance & 1023) >> 8),
                    (int)(appearance >> 10),
                    268964353,
                    0,
                    0,
                    136,
                    0,
                    31,
                    1,
                    appearance,
                    seed.HeadMesh,
                    true,
                    false,
                    new[]
                    {
                        new OrdinaryEnemyTextureProfile(0, 85939, 0),
                        new OrdinaryEnemyTextureProfile(1, 30843, 0),
                        new OrdinaryEnemyTextureProfile(2, 30867, 0),
                        new OrdinaryEnemyTextureProfile(3, 30827, 0),
                        new OrdinaryEnemyTextureProfile(4, 30874, 0)
                    },
                    new[]
                    {
                        new OrdinaryEnemyMeshProfile(0, (uint)seed.HeadMesh, 0, 4),
                        new OrdinaryEnemyMeshProfile(1, seed.BodyMesh, 0, 2)
                    },
                    OrdinaryEnemyScfuProfile.CapturedExact),
                CultistAggression,
                new OrdinaryEnemyCombatProfile(
                    OrdinaryEnemyCombatMode.UnarmedMelee,
                    OrdinaryEnemyDamageSource.CapturedFixed,
                    false,
                    CultistCombat,
                    OrdinaryEnemyEvidenceState.Observed),
                BuildLoot(seed.MonsterData),
                new OrdinaryEnemyCorpseProfile(
                    OrdinaryEnemyCorpsePacketProfile.Generic,
                    30.0,
                    120.0,
                    30.0,
                    seed.CorpseCatMesh,
                    EvidenceReference + ":exact identity-linked 414-byte corpse profile"),
                new[] { EvidenceReference },
                false,
                false);
        }

        private static OrdinaryEnemySpawnDefinition BuildSpawn(SpawnSeed seed, ProfileSeed profile)
        {
            OrdinaryEnemyWaypoint[] waypoints = seed.PatrolX.HasValue
                ? new[]
                    {
                        new OrdinaryEnemyWaypoint(seed.X, seed.Y, seed.Z),
                        new OrdinaryEnemyWaypoint(
                            seed.PatrolX.Value,
                            seed.PatrolY.Value,
                            seed.PatrolZ.Value)
                    }
                : new OrdinaryEnemyWaypoint[0];
            return new OrdinaryEnemySpawnDefinition(
                "totw.ordinary." + seed.SourceIdentity.ToString("X8", CultureInfo.InvariantCulture),
                seed.SourceIdentity,
                seed.ProfileKey,
                PlayfieldInstance,
                seed.Level,
                seed.Health,
                seed.HealthDamage,
                seed.MonsterScale,
                seed.RunSpeed,
                seed.X,
                seed.Y,
                seed.Z,
                seed.HeadingX,
                seed.HeadingY,
                seed.HeadingZ,
                seed.HeadingW,
                waypoints.Length > 0 ? OrdinaryEnemyMovementMode.Patrol : OrdinaryEnemyMovementMode.Static,
                waypoints,
                false,
                true,
                true,
                seed.CapturedScfuFlags,
                0,
                HexToBytes(profile.CapturedScfuUnknown1Hex),
                0,
                OrdinaryEnemyEvidenceState.Policy,
                RuntimeRespawnAfterNpcDespawnSeconds,
                OrdinaryEnemyRuntimeDisposition.Active,
                string.Empty,
                seed.SourceCapture,
                string.Empty,
                null,
                WorldRespawnPolicyAssignment.Explicit(CultistRespawn));
        }

        private static OrdinaryEnemyLootProfile BuildLoot(int monsterData)
        {
            LootSeed seed = LootFor(monsterData);
            OrdinaryEnemyLootEntry[] entries = seed.Items.Select(
                item => new OrdinaryEnemyLootEntry(
                    item.ItemId,
                    item.ItemId,
                    1,
                    0,
                    1,
                    item.ObservedCount,
                    0,
                    OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                    OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem,
                    OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy,
                    item.ObservedCount,
                    item.ObservedCount,
                    EvidenceReference)).ToArray();
            OrdinaryEnemyLevelCreditRule[] credits = Enumerable.Range(20, 16)
                .Select(
                    level =>
                    {
                        int observed = CreditObservations
                            .Where(value => value.MonsterData == monsterData && value.Level == level)
                            .Select(value => value.Count)
                            .SingleOrDefault();
                        int creditsForLevel = CreditsForLevel(level);
                        return new OrdinaryEnemyLevelCreditRule(
                            level,
                            creditsForLevel,
                            creditsForLevel,
                            observed,
                            observed > 0
                                ? EvidenceReference
                                : "policy:Temple Cultist level-credit mapping from 74 strict outcomes",
                            observed > 0
                                ? OrdinaryEnemyEvidenceState.Observed
                                : OrdinaryEnemyEvidenceState.Policy);
                    })
                .ToArray();
            if (entries.Length == 0)
            {
                return new OrdinaryEnemyLootProfile(
                    OrdinaryEnemyLootEvidence.NoneProven,
                    entries,
                    OrdinaryEnemyLootPoolMode.IndependentEntries,
                    0,
                    true,
                    seed.ObservedCorpses,
                    seed.ObservedEmptyCorpses,
                    EvidenceReference,
                    OrdinaryEnemyEvidenceState.Policy,
                    null,
                    null,
                    credits);
            }

            return new OrdinaryEnemyLootProfile(
                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                entries,
                OrdinaryEnemyLootPoolMode.WeightedOne,
                seed.ObservedEmptyCorpses,
                true,
                seed.ObservedCorpses,
                seed.ObservedEmptyCorpses,
                EvidenceReference,
                OrdinaryEnemyEvidenceState.Policy,
                null,
                null,
                credits);
        }

        private static LootSeed LootFor(int monsterData)
        {
            switch (monsterData)
            {
                case 26074:
                    return new LootSeed(9, 5,
                        new LootItemSeed(204571, 1), new LootItemSeed(204711, 1),
                        new LootItemSeed(204720, 1), new LootItemSeed(204721, 1));
                case 26082:
                    return new LootSeed(10, 10);
                case 26103:
                    return new LootSeed(10, 10);
                case 26135:
                    return new LootSeed(8, 4,
                        new LootItemSeed(204711, 1), new LootItemSeed(204712, 3));
                case 26137:
                    return new LootSeed(9, 6,
                        new LootItemSeed(204571, 2), new LootItemSeed(204712, 1));
                case 26147:
                    return new LootSeed(13, 10,
                        new LootItemSeed(204571, 1), new LootItemSeed(204720, 1),
                        new LootItemSeed(204721, 1));
                case 26149:
                    return new LootSeed(15, 12,
                        new LootItemSeed(204712, 1), new LootItemSeed(204721, 2));
                default:
                    throw new InvalidOperationException(
                        "Unknown Temple cultist MonsterData: "
                        + monsterData.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static int CreditsForLevel(int level)
        {
            switch (level)
            {
                case 20: return 371;
                case 21: return 391;
                case 22: return 410;
                case 23: return 430;
                case 24: return 449;
                case 25: return 468;
                case 26: return 492;
                case 27: return 516;
                case 28: return 539;
                case 29: return 563;
                case 30: return 587;
                case 31: return 610;
                case 32: return 634;
                case 33: return 658;
                case 34: return 681;
                case 35: return 705;
                default: throw new ArgumentOutOfRangeException("level");
            }
        }

        private static byte[] HexToBytes(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0)
            {
                throw new InvalidOperationException("Captured Temple SCFU unknown data is invalid.");
            }

            var result = new byte[value.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = byte.Parse(
                    value.Substring(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return result;
        }

        private sealed class ProfileSeed
        {
            internal ProfileSeed(
                string profileKey,
                int monsterData,
                uint appearanceValue,
                int headMesh,
                uint bodyMesh,
                int corpseCatMesh,
                string capturedScfuUnknown1Hex)
            {
                this.ProfileKey = profileKey;
                this.MonsterData = monsterData;
                this.AppearanceValue = appearanceValue;
                this.HeadMesh = headMesh;
                this.BodyMesh = bodyMesh;
                this.CorpseCatMesh = corpseCatMesh;
                this.CapturedScfuUnknown1Hex = capturedScfuUnknown1Hex;
            }

            internal string ProfileKey { get; private set; }
            internal int MonsterData { get; private set; }
            internal uint AppearanceValue { get; private set; }
            internal int HeadMesh { get; private set; }
            internal uint BodyMesh { get; private set; }
            internal int CorpseCatMesh { get; private set; }
            internal string CapturedScfuUnknown1Hex { get; private set; }
        }

        private sealed class SpawnSeed
        {
            internal SpawnSeed(
                int sourceIdentity,
                string profileKey,
                int level,
                int health,
                int healthDamage,
                int monsterScale,
                int runSpeed,
                float x,
                float y,
                float z,
                float headingX,
                float headingY,
                float headingZ,
                float headingW,
                uint capturedScfuFlags,
                float? patrolX,
                float? patrolY,
                float? patrolZ,
                string sourceCapture)
            {
                this.SourceIdentity = sourceIdentity;
                this.ProfileKey = profileKey;
                this.Level = level;
                this.Health = health;
                this.HealthDamage = healthDamage;
                this.MonsterScale = monsterScale;
                this.RunSpeed = runSpeed;
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.HeadingX = headingX;
                this.HeadingY = headingY;
                this.HeadingZ = headingZ;
                this.HeadingW = headingW;
                this.CapturedScfuFlags = capturedScfuFlags;
                this.PatrolX = patrolX;
                this.PatrolY = patrolY;
                this.PatrolZ = patrolZ;
                this.SourceCapture = sourceCapture;
            }

            internal int SourceIdentity { get; private set; }
            internal string ProfileKey { get; private set; }
            internal int Level { get; private set; }
            internal int Health { get; private set; }
            internal int HealthDamage { get; private set; }
            internal int MonsterScale { get; private set; }
            internal int RunSpeed { get; private set; }
            internal float X { get; private set; }
            internal float Y { get; private set; }
            internal float Z { get; private set; }
            internal float HeadingX { get; private set; }
            internal float HeadingY { get; private set; }
            internal float HeadingZ { get; private set; }
            internal float HeadingW { get; private set; }
            internal uint CapturedScfuFlags { get; private set; }
            internal float? PatrolX { get; private set; }
            internal float? PatrolY { get; private set; }
            internal float? PatrolZ { get; private set; }
            internal string SourceCapture { get; private set; }
        }

        private sealed class CreditObservation
        {
            internal CreditObservation(int monsterData, int level, int count)
            {
                this.MonsterData = monsterData;
                this.Level = level;
                this.Count = count;
            }

            internal int MonsterData { get; private set; }
            internal int Level { get; private set; }
            internal int Count { get; private set; }
        }

        private sealed class LootSeed
        {
            internal LootSeed(int observedCorpses, int observedEmptyCorpses, params LootItemSeed[] items)
            {
                this.ObservedCorpses = observedCorpses;
                this.ObservedEmptyCorpses = observedEmptyCorpses;
                this.Items = items ?? new LootItemSeed[0];
            }

            internal int ObservedCorpses { get; private set; }
            internal int ObservedEmptyCorpses { get; private set; }
            internal LootItemSeed[] Items { get; private set; }
        }

        private sealed class LootItemSeed
        {
            internal LootItemSeed(int itemId, int observedCount)
            {
                this.ItemId = itemId;
                this.ObservedCount = observedCount;
            }

            internal int ItemId { get; private set; }
            internal int ObservedCount { get; private set; }
        }
    }
}
