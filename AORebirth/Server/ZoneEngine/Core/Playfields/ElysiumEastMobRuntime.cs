namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
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
    /// Elysium wildlife PF 4543 East + PF 4540 South.
    /// Captures 20260727-182451 / 190145 / 193914 / 201436: appearance, ExtTex, Side.
    /// Aggressive AOS 8m; Omni/Clan skip same-side and Neutral players. Heckler fight from 190145.
    /// </summary>
    internal static class ElysiumEastMobRuntime
    {
        private sealed class MobSlot
        {
            public string Name;
            public int PlayfieldId;
            public int Side;
            public int MonsterData;
            public int Level;
            public int Health;
            public int NpcFamily;
            public int Scale;
            public int RunSpeed;
            public int CharacterFlags;
            public int VisualFlags;
            public int HeadMesh;
            public float X;
            public float Y;
            public float Z;
            public float HeadingY;
            public float HeadingW;
            public int[][] Textures;
            public int[][] Meshes;
        }

        private const int ElysiumEastPlayfieldId = 4543;
        private const int ElysiumSouthPlayfieldId = 4540;
        private const double RespawnSeconds = 60.0;
        private const float AggroRadiusMeters = 8.0f;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot =
            new Dictionary<int, DateTime[]>();

        private static readonly Dictionary<int, float> AggroRadiusByNpcInstance =
            new Dictionary<int, float>();

        private static readonly object AggroGate = new object();

        // Capture ExtTex: Arachno Frigida
        private static readonly byte[] ExtTex_Arachno_Frigida =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C,
                0x20, 0x23, 0x31, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xDA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: Arachno Gelida
        private static readonly byte[] ExtTex_Arachno_Gelida =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C,
                0x20, 0x23, 0x31, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xDA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: Arcorash
        private static readonly byte[] ExtTex_Arcorash =
            {
                0x00, 0x00, 0x07, 0xE2, 0x64, 0x6F, 0x70, 0x70, 0x65, 0x6C, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: CEO Guardian
        private static readonly byte[] ExtTex_CEO_Guardian =
            {
                0x00, 0x00, 0x0F, 0xC4, 0x68, 0x65, 0x6C, 0x6C, 0x66, 0x61, 0x63, 0x65,
                0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x6C, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x68, 0x65, 0x6C, 0x6C, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x6C, 0x56,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x68, 0x65, 0x6C, 0x6C,
                0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x6C, 0x56, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Callous Mortiig
        private static readonly byte[] ExtTex_Callous_Mortiig =
            {
                0x00, 0x00, 0x0F, 0xC4, 0x6D, 0x61, 0x69, 0x6E, 0x20, 0x74, 0x70, 0x61,
                0x67, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x31, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x61, 0x72, 0x6D, 0x6F, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x95,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x73, 0x68, 0x61, 0x67,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x9A, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Cascading Spirit
        private static readonly byte[] ExtTex_Cascading_Spirit =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xB9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB7,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Chill Spider
        private static readonly byte[] ExtTex_Chill_Spider =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C,
                0x20, 0x23, 0x31, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x31, 0x8A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: Cur-Dosa
        private static readonly byte[] ExtTex_Cur_Dosa =
            {
                0x00, 0x00, 0x07, 0xE2, 0x67, 0x72, 0x65, 0x79, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x9C, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

        // Capture ExtTex: Cur-Lendar
        private static readonly byte[] ExtTex_Cur_Lendar =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x64, 0x72, 0x75, 0x69, 0x64, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x64, 0x72, 0x75, 0x69, 0x64, 0x20, 0x32, 0x20, 0x73, 0x69, 0x64, 0x65,
                0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x90,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Deceitful Weaver
        private static readonly byte[] ExtTex_Deceitful_Weaver =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C,
                0x20, 0x23, 0x31, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x31, 0x8A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: Devoted Enel Ilad-Ulma
        private static readonly byte[] ExtTex_Devoted_Enel_Ilad_Ulma =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x20, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x97, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x97,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Devourer of Life
        private static readonly byte[] ExtTex_Devourer_of_Life =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x62, 0x69, 0x6C, 0x65, 0x5F, 0x73, 0x77, 0x61,
                0x72, 0x6D, 0x5F, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x7C, 0x81, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x62, 0x69, 0x6C, 0x65, 0x5F, 0x73, 0x77, 0x61, 0x72, 0x6D, 0x5F, 0x31,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x95, 0xE3,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture ExtTex: El-Karat
        private static readonly byte[] ExtTex_El_Karat =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74,
                0x20, 0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61,
                0x6B, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74, 0x20, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: El-Mada
        private static readonly byte[] ExtTex_El_Mada =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74,
                0x20, 0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61,
                0x6B, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74, 0x20, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: El-Nodor
        private static readonly byte[] ExtTex_El_Nodor =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74,
                0x20, 0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61,
                0x6B, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x73, 0x65, 0x72, 0x67, 0x65, 0x61, 0x6E, 0x74, 0x20, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Elysian Spirit Hunter
        private static readonly byte[] ExtTex_Elysian_Spirit_Hunter =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x53, 0x6F, 0x75, 0x6C, 0x73, 0x74, 0x65, 0x61,
                0x6C, 0x65, 0x72, 0x20, 0x74, 0x70, 0x61, 0x67, 0x65, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x46, 0xFC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x32, 0x20, 0x73, 0x68, 0x61, 0x67, 0x73, 0x20, 0x61, 0x6E, 0x64, 0x20,
                0x72, 0x61, 0x67, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x61,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Flagging Arcorash
        private static readonly byte[] ExtTex_Flagging_Arcorash =
            {
                0x00, 0x00, 0x07, 0xE2, 0x64, 0x6F, 0x70, 0x70, 0x65, 0x6C, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        // Capture ExtTex: Insidious Spirit
        private static readonly byte[] ExtTex_Insidious_Spirit =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xB9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB6,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Kolaana
        private static readonly byte[] ExtTex_Kolaana =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x53, 0x6F, 0x75, 0x6C, 0x73, 0x74, 0x65, 0x61,
                0x6C, 0x65, 0x72, 0x20, 0x74, 0x70, 0x61, 0x67, 0x65, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x46, 0xFC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x32, 0x20, 0x73, 0x68, 0x61, 0x67, 0x73, 0x20, 0x61, 0x6E, 0x64, 0x20,
                0x72, 0x61, 0x67, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x61,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Kolaana-Behn
        private static readonly byte[] ExtTex_Kolaana_Behn =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x53, 0x6F, 0x75, 0x6C, 0x73, 0x74, 0x65, 0x61,
                0x6C, 0x65, 0x72, 0x20, 0x74, 0x70, 0x61, 0x67, 0x65, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x46, 0xFC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x32, 0x20, 0x73, 0x68, 0x61, 0x67, 0x73, 0x20, 0x61, 0x6E, 0x64, 0x20,
                0x72, 0x61, 0x67, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x61,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Len-Dasa
        private static readonly byte[] ExtTex_Len_Dasa =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x20, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x97, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x97,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Len-Dosa
        private static readonly byte[] ExtTex_Len_Dosa =
            {
                0x00, 0x00, 0x07, 0xE2, 0x67, 0x72, 0x65, 0x79, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x9C, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

        // Capture ExtTex: Len-Lendar
        private static readonly byte[] ExtTex_Len_Lendar =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x20, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x97, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x97,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Len-Lochquid
        private static readonly byte[] ExtTex_Len_Lochquid =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x20, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x97, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x6E, 0x61, 0x6E, 0x6F, 0x6D, 0x61, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x97,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Lost Soul
        private static readonly byte[] ExtTex_Lost_Soul =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x47, 0x90,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: One With A Graceful Neck
        private static readonly byte[] ExtTex_One_With_A_Graceful_Neck =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x79, 0x75, 0x74, 0x74, 0x6F, 0x73, 0x6C, 0x69,
                0x67, 0x68, 0x74, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xDE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x32, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xDF,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Or-Karat
        private static readonly byte[] ExtTex_Or_Karat =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9C,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Or-Mada
        private static readonly byte[] ExtTex_Or_Mada =
            {
                0x00, 0x00, 0x07, 0xE2, 0x67, 0x72, 0x65, 0x79, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x9C, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

        // Capture ExtTex: Or-Mada of Flaming Barrels
        private static readonly byte[] ExtTex_Or_Mada_of_Flaming_Barrels =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x20, 0x32, 0x20, 0x73, 0x69,
                0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B, 0x29, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9C,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture ExtTex: Or-Mada of Preservation
        private static readonly byte[] ExtTex_Or_Mada_of_Preservation =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x20, 0x32, 0x20, 0x73, 0x69,
                0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B, 0x29, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9C,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture ExtTex: Or-Mada of the Furious Fists
        private static readonly byte[] ExtTex_Or_Mada_of_the_Furious_Fists =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x20, 0x32, 0x20, 0x73, 0x69,
                0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B, 0x29, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9C,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture ExtTex: Or-Nodor
        private static readonly byte[] ExtTex_Or_Nodor =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x20,
                0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F, 0x61, 0x6B,
                0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x76, 0x61, 0x72, 0x72, 0x69, 0x6F, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x9C,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Sadistic Soul Dredge
        private static readonly byte[] ExtTex_Sadistic_Soul_Dredge =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x53, 0x6F, 0x75, 0x6C, 0x73, 0x74, 0x65, 0x61,
                0x6C, 0x65, 0x72, 0x20, 0x74, 0x70, 0x61, 0x67, 0x65, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x46, 0xFC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x32, 0x20, 0x73, 0x68, 0x61, 0x67, 0x73, 0x20, 0x61, 0x6E, 0x64, 0x20,
                0x72, 0x61, 0x67, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x61,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Shades Of Grey
        private static readonly byte[] ExtTex_Shades_Of_Grey =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x79, 0x75, 0x74, 0x74, 0x6F, 0x73, 0x6C, 0x69,
                0x67, 0x68, 0x74, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xDE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x32, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xDF,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Shifty Spirit
        private static readonly byte[] ExtTex_Shifty_Spirit =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xBB, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB8,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Slinking Spirit
        private static readonly byte[] ExtTex_Slinking_Spirit =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xBB, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB6,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Wandering Soul
        private static readonly byte[] ExtTex_Wandering_Soul =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xBB, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB7,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Waning Soul
        private static readonly byte[] ExtTex_Waning_Soul =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64, 0x5F, 0x73, 0x70, 0x69,
                0x72, 0x69, 0x74, 0x20, 0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xBC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64, 0x20, 0x62, 0x6F, 0x64,
                0x79, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB8,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture ExtTex: Yuttos Elysium Geosurvey Dog
        private static readonly byte[] ExtTex_Yuttos_Elysium_Geosurvey_Dog =
            {
                0x00, 0x00, 0x07, 0xE2, 0x6C, 0x6F, 0x77, 0x32, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x30, 0x49, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        private static readonly byte[] DefaultScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00
            };

        private static readonly byte[] ExtTexScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x03, 0x00, 0x00
            };

        private static readonly MobSlot[] Slots =
            {
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 62, Health = 4093, NpcFamily = 202, Scale = 150, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 603.337f, Y = 31.050f, Z = 537.682f, HeadingY = -0.314638f, HeadingW = 0.949212f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 59, Health = 3820, NpcFamily = 202, Scale = 150, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 603.222f, Y = 30.940f, Z = 535.535f, HeadingY = -0.839495f, HeadingW = 0.543367f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 60, Health = 3911, NpcFamily = 202, Scale = 150, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 602.861f, Y = 32.251f, Z = 524.486f, HeadingY = -0.751220f, HeadingW = 0.660052f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 60, Health = 3911, NpcFamily = 202, Scale = 150, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 603.486f, Y = 32.605f, Z = 522.076f, HeadingY = -0.949381f, HeadingW = 0.314128f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 601.624f, Y = 31.127f, Z = 536.739f, HeadingY = -0.566482f, HeadingW = 0.824074f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 70, Health = 4822, NpcFamily = 202, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 601.019f, Y = 32.331f, Z = 522.926f, HeadingY = -0.841813f, HeadingW = 0.539769f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 609.038f, Y = 32.006f, Z = 541.806f, HeadingY = 0.370398f, HeadingW = 0.928873f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 594.983f, Y = 32.324f, Z = 529.451f, HeadingY = 0.342446f, HeadingW = 0.939538f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 666.698f, Y = 49.115f, Z = 486.501f, HeadingY = -0.885073f, HeadingW = 0.465453f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 677.271f, Y = 64.205f, Z = 488.441f, HeadingY = -0.909936f, HeadingW = 0.414749f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 567.963f, Y = 28.522f, Z = 541.703f, HeadingY = 0.364540f, HeadingW = 0.931188f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 59, Health = 3820, NpcFamily = 202, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 587.021f, Y = 34.831f, Z = 614.845f, HeadingY = -0.062191f, HeadingW = 0.998064f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or of Preservation", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 80037, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 592.715f, Y = 35.980f, Z = 574.270f, HeadingY = 0.971881f, HeadingW = 0.235472f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or of Gear & Ammo", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 80037, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 592.748f, Y = 35.983f, Z = 574.135f, HeadingY = 0.971644f, HeadingW = 0.236449f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 65, Health = 4367, NpcFamily = 202, Scale = 200, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.471f, Y = 38.415f, Z = 589.815f, HeadingY = -0.370774f, HeadingW = 0.928723f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.860f, Y = 38.415f, Z = 563.644f, HeadingY = 0.064473f, HeadingW = 0.997919f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Son-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208648, Level = 65, Health = 4367, NpcFamily = 202, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 683.265f, Y = 49.115f, Z = 556.801f, HeadingY = -0.488939f, HeadingW = 0.872318f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 4002, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 619.182f, Y = 29.295f, Z = 589.155f, HeadingY = 0.706301f, HeadingW = 0.707911f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.279f, Y = 38.415f, Z = 587.278f, HeadingY = -0.714254f, HeadingW = 0.699886f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.235f, Y = 38.415f, Z = 563.958f, HeadingY = -0.712435f, HeadingW = 0.701738f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or of Protection", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 83680, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 629.147f, Y = 37.610f, Z = 585.775f, HeadingY = -0.745328f, HeadingW = 0.666697f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 655.388f, Y = 41.348f, Z = 582.467f, HeadingY = -0.548565f, HeadingW = 0.836108f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 70, Health = 4822, NpcFamily = 202, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 658.519f, Y = 42.125f, Z = 581.991f, HeadingY = -0.364286f, HeadingW = 0.931287f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 659.642f, Y = 40.819f, Z = 585.048f, HeadingY = -0.169312f, HeadingW = 0.985563f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 660.983f, Y = 38.415f, Z = 589.393f, HeadingY = 0.697825f, HeadingW = 0.716268f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Hypnagogic Ixi-Bhotaar Shere", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 70, Health = 4822, NpcFamily = 202, Scale = 200, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 668.288f, Y = 49.155f, Z = 564.289f, HeadingY = 0.623059f, HeadingW = 0.782175f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 61, Health = 4002, NpcFamily = 202, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 560.009f, Y = 29.843f, Z = 530.874f, HeadingY = -0.732660f, HeadingW = 0.680595f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 60, Health = 3911, NpcFamily = 202, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 587.802f, Y = 32.810f, Z = 524.568f, HeadingY = -0.140646f, HeadingW = 0.990060f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 63, Health = 4184, NpcFamily = 202, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 673.617f, Y = 41.615f, Z = 479.518f, HeadingY = -0.624507f, HeadingW = 0.781019f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 60, Health = 3911, NpcFamily = 202, Scale = 200, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.387f, Y = 38.415f, Z = 609.024f, HeadingY = -0.925189f, HeadingW = 0.379507f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 623.177f, Y = 38.415f, Z = 635.951f, HeadingY = -0.020276f, HeadingW = 0.999794f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 65, Health = 4367, NpcFamily = 190, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 590.090f, Y = 34.398f, Z = 613.159f, HeadingY = -0.298393f, HeadingW = 0.954443f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.304f, Y = 38.415f, Z = 634.749f, HeadingY = 0.470812f, HeadingW = 0.882234f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.814f, Y = 38.415f, Z = 610.808f, HeadingY = -0.711227f, HeadingW = 0.702962f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-El", PlayfieldId = 4543, Side = 2, MonsterData = 246043, Level = 63, Health = 4184, NpcFamily = 202, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 608.915f, Y = 32.446f, Z = 627.040f, HeadingY = 0.726900f, HeadingW = 0.686743f, Textures = null, Meshes = new[] { new[] { 1, 209521, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.718f, Y = 38.415f, Z = 632.167f, HeadingY = 0.934729f, HeadingW = 0.355362f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 65, Health = 4367, NpcFamily = 202, Scale = 200, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.457f, Y = 38.415f, Z = 609.144f, HeadingY = 0.960623f, HeadingW = 0.277854f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 60, Health = 3911, NpcFamily = 202, Scale = 150, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 658.728f, Y = 37.610f, Z = 623.882f, HeadingY = 0.773654f, HeadingW = 0.633608f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 60, Health = 3911, NpcFamily = 202, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 706.520f, Y = 31.354f, Z = 601.439f, HeadingY = -0.211505f, HeadingW = 0.977377f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 60, Health = 3911, NpcFamily = 202, Scale = 200, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.453f, Y = 38.415f, Z = 634.606f, HeadingY = 0.702875f, HeadingW = 0.711314f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.531f, Y = 38.415f, Z = 610.761f, HeadingY = 0.698214f, HeadingW = 0.715889f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 61, Health = 4002, NpcFamily = 202, Scale = 150, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 645.401f, Y = 37.610f, Z = 601.233f, HeadingY = 0.027093f, HeadingW = 0.999633f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 59, Health = 3820, NpcFamily = 202, Scale = 150, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 643.451f, Y = 37.610f, Z = 624.840f, HeadingY = 0.030068f, HeadingW = 0.999548f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 61, Health = 4002, NpcFamily = 202, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 640.004f, Y = 37.610f, Z = 594.220f, HeadingY = 0.845713f, HeadingW = 0.533639f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 65, Health = 4367, NpcFamily = 202, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 564.316f, Y = 44.474f, Z = 634.341f, HeadingY = -0.760437f, HeadingW = 0.649412f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 59, Health = 3820, NpcFamily = 202, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 567.035f, Y = 48.519f, Z = 667.540f, HeadingY = 0.880548f, HeadingW = 0.473957f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 4002, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.501f, Y = 38.415f, Z = 656.506f, HeadingY = -0.716224f, HeadingW = 0.697871f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 568.950f, Y = 42.205f, Z = 634.466f, HeadingY = -0.959340f, HeadingW = 0.282252f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 58, Health = 3729, NpcFamily = 202, Scale = 150, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 643.239f, Y = 37.610f, Z = 652.040f, HeadingY = -0.402803f, HeadingW = 0.915287f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 61, Health = 4002, NpcFamily = 202, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 644.445f, Y = 38.958f, Z = 674.498f, HeadingY = -0.390164f, HeadingW = 0.920745f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 60, Health = 3911, NpcFamily = 202, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 735.093f, Y = 39.854f, Z = 692.692f, HeadingY = 0.030783f, HeadingW = 0.999526f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 58, Health = 3729, NpcFamily = 202, Scale = 150, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 643.294f, Y = 37.610f, Z = 648.128f, HeadingY = 0.025070f, HeadingW = 0.999686f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 62, Health = 4093, NpcFamily = 202, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 679.994f, Y = 37.434f, Z = 584.993f, HeadingY = -0.710926f, HeadingW = 0.703267f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Visionist Dom-Xum Shere", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 70, Health = 4822, NpcFamily = 202, Scale = 200, RunSpeed = 246, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 0, X = 699.717f, Y = 59.115f, Z = 556.506f, HeadingY = 0.999777f, HeadingW = 0.021135f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Follower Yutt-Ixi Shere", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 70, Health = 4822, NpcFamily = 202, Scale = 140, RunSpeed = 246, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 0, X = 694.296f, Y = 60.365f, Z = 557.757f, HeadingY = 0.999954f, HeadingW = -0.009606f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 60, Health = 3911, NpcFamily = 202, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 712.143f, Y = 41.615f, Z = 542.509f, HeadingY = 0.389533f, HeadingW = 0.921013f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 60, Health = 3911, NpcFamily = 202, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 713.534f, Y = 41.615f, Z = 571.652f, HeadingY = 0.999939f, HeadingW = -0.011058f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 704.403f, Y = 72.045f, Z = 521.207f, HeadingY = 0.358732f, HeadingW = 0.933441f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 65, Health = 4367, NpcFamily = 202, Scale = 150, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 705.840f, Y = 52.769f, Z = 535.105f, HeadingY = 0.700722f, HeadingW = 0.713434f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 60, Health = 3911, NpcFamily = 202, Scale = 200, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 706.012f, Y = 52.765f, Z = 533.688f, HeadingY = 0.678862f, HeadingW = 0.734266f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 62, Health = 4093, NpcFamily = 202, Scale = 150, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 705.832f, Y = 52.753f, Z = 523.570f, HeadingY = 0.711269f, HeadingW = 0.702920f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 4002, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 705.691f, Y = 52.755f, Z = 524.900f, HeadingY = 0.667709f, HeadingW = 0.744422f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 62, Health = 4093, NpcFamily = 202, Scale = 150, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 710.371f, Y = 41.615f, Z = 531.299f, HeadingY = 0.711904f, HeadingW = 0.702276f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 62, Health = 4093, NpcFamily = 202, Scale = 150, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 712.303f, Y = 41.615f, Z = 529.932f, HeadingY = 0.713634f, HeadingW = 0.700519f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 61, Health = 4002, NpcFamily = 202, Scale = 150, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 710.292f, Y = 41.615f, Z = 527.705f, HeadingY = 0.711498f, HeadingW = 0.702688f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 70, Health = 4822, NpcFamily = 202, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 689.584f, Y = 54.115f, Z = 556.665f, HeadingY = -0.719781f, HeadingW = 0.694202f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 65, Health = 4367, NpcFamily = 202, Scale = 150, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 709.002f, Y = 49.115f, Z = 542.419f, HeadingY = 0.940443f, HeadingW = 0.339953f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 710.397f, Y = 41.615f, Z = 581.502f, HeadingY = 0.017135f, HeadingW = 0.999853f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 713.952f, Y = 41.615f, Z = 577.706f, HeadingY = 0.667359f, HeadingW = 0.744736f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 705.521f, Y = 49.115f, Z = 575.321f, HeadingY = -0.931859f, HeadingW = 0.362819f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 69, Health = 4731, NpcFamily = 202, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 708.558f, Y = 49.115f, Z = 572.394f, HeadingY = -0.941412f, HeadingW = 0.337259f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 70, Health = 4822, NpcFamily = 202, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 689.460f, Y = 54.115f, Z = 561.805f, HeadingY = -0.730772f, HeadingW = 0.682621f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 61, Health = 4002, NpcFamily = 202, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 714.042f, Y = 41.615f, Z = 582.354f, HeadingY = 0.276556f, HeadingW = 0.960998f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Fortuitous Hes-Man Shere", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 70, Health = 4822, NpcFamily = 202, Scale = 200, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 708.294f, Y = 49.115f, Z = 575.233f, HeadingY = -0.936124f, HeadingW = 0.351671f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 59, Health = 3820, NpcFamily = 202, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 732.683f, Y = 26.549f, Z = 559.946f, HeadingY = 0.998611f, HeadingW = 0.052690f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 65, Health = 4367, NpcFamily = 202, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 712.418f, Y = 41.615f, Z = 507.496f, HeadingY = 0.310063f, HeadingW = 0.950716f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 697.317f, Y = 64.205f, Z = 488.008f, HeadingY = 0.049689f, HeadingW = 0.998765f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 4002, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 684.933f, Y = 72.245f, Z = 494.219f, HeadingY = 0.936894f, HeadingW = 0.349614f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 60, Health = 3911, NpcFamily = 202, Scale = 200, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 704.216f, Y = 72.045f, Z = 513.395f, HeadingY = 0.919491f, HeadingW = 0.393110f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 64, Health = 4276, NpcFamily = 202, Scale = 200, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 697.325f, Y = 64.205f, Z = 508.243f, HeadingY = -0.210890f, HeadingW = 0.977510f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 64, Health = 4276, NpcFamily = 202, Scale = 150, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 687.287f, Y = 49.115f, Z = 482.415f, HeadingY = -0.996143f, HeadingW = 0.087744f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 61, Health = 4002, NpcFamily = 202, Scale = 200, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 685.734f, Y = 49.115f, Z = 482.720f, HeadingY = -0.887139f, HeadingW = 0.461502f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 702.780f, Y = 52.772f, Z = 506.411f, HeadingY = 1.000000f, HeadingW = 0.000650f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 705.181f, Y = 52.735f, Z = 507.605f, HeadingY = 0.935518f, HeadingW = 0.353278f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 60, Health = 3911, NpcFamily = 202, Scale = 200, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 706.163f, Y = 52.733f, Z = 511.383f, HeadingY = 0.730584f, HeadingW = 0.682822f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 65, Health = 4367, NpcFamily = 202, Scale = 150, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 708.849f, Y = 49.115f, Z = 515.392f, HeadingY = 0.403802f, HeadingW = 0.914846f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 736.041f, Y = 30.882f, Z = 491.300f, HeadingY = 0.642663f, HeadingW = 0.766149f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 64, Health = 4276, NpcFamily = 202, Scale = 150, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 668.339f, Y = 36.665f, Z = 472.156f, HeadingY = -0.699729f, HeadingW = 0.714408f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 660.761f, Y = 41.615f, Z = 477.204f, HeadingY = -0.996475f, HeadingW = 0.083885f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 64, Health = 4276, NpcFamily = 202, Scale = 150, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 677.569f, Y = 36.665f, Z = 474.332f, HeadingY = 0.888725f, HeadingW = 0.458440f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 62, Health = 4093, NpcFamily = 202, Scale = 200, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 647.258f, Y = 36.176f, Z = 478.357f, HeadingY = -0.994138f, HeadingW = 0.108118f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208640, Level = 63, Health = 4184, NpcFamily = 202, Scale = 200, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 651.937f, Y = 35.815f, Z = 477.931f, HeadingY = -0.994206f, HeadingW = 0.107494f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 61, Health = 4002, NpcFamily = 202, Scale = 150, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 690.348f, Y = 41.615f, Z = 479.429f, HeadingY = -0.996362f, HeadingW = 0.085217f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 64, Health = 4276, NpcFamily = 202, Scale = 150, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 684.045f, Y = 41.615f, Z = 479.769f, HeadingY = -0.995905f, HeadingW = 0.090409f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 63, Health = 4184, NpcFamily = 202, Scale = 150, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 686.438f, Y = 36.665f, Z = 474.488f, HeadingY = -0.996492f, HeadingW = 0.083691f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 62, Health = 4093, NpcFamily = 202, Scale = 150, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.887f, Y = 36.665f, Z = 474.440f, HeadingY = -0.912821f, HeadingW = 0.408360f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Cama-El", PlayfieldId = 4543, Side = 2, MonsterData = 246042, Level = 65, Health = 4367, NpcFamily = 202, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 714.087f, Y = 41.615f, Z = 476.657f, HeadingY = 0.938037f, HeadingW = 0.346535f, Textures = null, Meshes = new[] { new[] { 1, 209492, 0, 2 } } },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 780.815f, Y = 39.387f, Z = 439.961f, HeadingY = 0.999829f, HeadingW = -0.018489f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 796.315f, Y = 28.147f, Z = 450.086f, HeadingY = -0.469932f, HeadingW = 0.882703f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 799.308f, Y = 27.754f, Z = 443.354f, HeadingY = 0.999099f, HeadingW = 0.042444f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 57, Health = 3638, NpcFamily = 202, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 750.193f, Y = 35.833f, Z = 623.276f, HeadingY = -0.926051f, HeadingW = 0.377398f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Calan-El", PlayfieldId = 4543, Side = 2, MonsterData = 246043, Level = 63, Health = 4184, NpcFamily = 202, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 739.693f, Y = 35.583f, Z = 626.476f, HeadingY = 0.996448f, HeadingW = 0.084209f, Textures = null, Meshes = new[] { new[] { 1, 209521, 0, 2 } } },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 58, Health = 3729, NpcFamily = 202, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 746.476f, Y = 34.521f, Z = 612.527f, HeadingY = 0.512500f, HeadingW = 0.858687f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4543, Side = 3, MonsterData = 209173, Level = 58, Health = 2238, NpcFamily = 200, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 803.548f, Y = 35.658f, Z = 663.494f, HeadingY = -0.233205f, HeadingW = 0.971152f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 67, Health = 4549, NpcFamily = 191, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 699.034f, Y = 47.415f, Z = 734.801f, HeadingY = -0.915830f, HeadingW = 0.401566f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 69, Health = 4731, NpcFamily = 189, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 778.579f, Y = 47.415f, Z = 708.506f, HeadingY = 0.999697f, HeadingW = 0.024607f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 692.170f, Y = 45.565f, Z = 701.497f, HeadingY = 0.142900f, HeadingW = 0.989737f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.051f, Y = 45.565f, Z = 700.511f, HeadingY = -0.038958f, HeadingW = 0.999241f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.426f, Y = 47.415f, Z = 704.205f, HeadingY = 0.473180f, HeadingW = 0.880966f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hiathlin Lookout", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 95, Health = 8926, NpcFamily = 207, Scale = 100, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 701.671f, Y = 57.415f, Z = 715.827f, HeadingY = -0.934128f, HeadingW = 0.356938f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 717.351f, Y = 47.415f, Z = 704.665f, HeadingY = 0.998969f, HeadingW = 0.045391f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 696.239f, Y = 45.565f, Z = 701.702f, HeadingY = -0.302915f, HeadingW = 0.953018f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Dren", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 80, Health = 5733, NpcFamily = 191, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 706.505f, Y = 47.415f, Z = 706.919f, HeadingY = 1.000000f, HeadingW = 0.000809f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 65, Health = 4367, NpcFamily = 189, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 754.593f, Y = 47.415f, Z = 704.816f, HeadingY = 0.998950f, HeadingW = 0.045815f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 65, Health = 4367, NpcFamily = 189, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 777.191f, Y = 47.415f, Z = 704.737f, HeadingY = 0.999160f, HeadingW = 0.040990f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 753.288f, Y = 47.415f, Z = 706.544f, HeadingY = -0.000115f, HeadingW = 1.000000f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 70, Health = 4822, NpcFamily = 191, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 687.807f, Y = 40.498f, Z = 750.062f, HeadingY = -0.953655f, HeadingW = 0.300903f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 720.002f, Y = 39.915f, Z = 751.961f, HeadingY = 0.709020f, HeadingW = 0.705188f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 718.876f, Y = 47.415f, Z = 704.525f, HeadingY = 0.999660f, HeadingW = 0.026094f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 70, Health = 4822, NpcFamily = 191, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 735.514f, Y = 39.915f, Z = 715.415f, HeadingY = 0.004785f, HeadingW = 0.999989f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 68, Health = 4640, NpcFamily = 189, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 736.275f, Y = 39.915f, Z = 714.939f, HeadingY = 0.003147f, HeadingW = 0.999995f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 68, Health = 4640, NpcFamily = 189, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 753.009f, Y = 39.915f, Z = 752.430f, HeadingY = 0.695554f, HeadingW = 0.718474f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 70, Health = 4822, NpcFamily = 189, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 786.726f, Y = 39.915f, Z = 756.907f, HeadingY = 0.514787f, HeadingW = 0.857318f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 67, Health = 4549, NpcFamily = 189, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 765.508f, Y = 39.915f, Z = 753.827f, HeadingY = 1.000000f, HeadingW = 0.000601f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 51, Health = 3092, NpcFamily = 191, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 718.734f, Y = 47.415f, Z = 746.208f, HeadingY = 0.432753f, HeadingW = 0.901512f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 50, Health = 3000, NpcFamily = 191, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 717.067f, Y = 47.415f, Z = 748.220f, HeadingY = -0.042499f, HeadingW = 0.999097f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 53, Health = 3274, NpcFamily = 191, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.499f, Y = 47.415f, Z = 748.141f, HeadingY = -0.147859f, HeadingW = 0.989008f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Dren", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 80, Health = 5733, NpcFamily = 191, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 689.488f, Y = 47.415f, Z = 732.472f, HeadingY = -0.602974f, HeadingW = 0.797761f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 58, Health = 3729, NpcFamily = 191, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 692.616f, Y = 47.415f, Z = 746.618f, HeadingY = -0.151371f, HeadingW = 0.988477f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 752.569f, Y = 47.415f, Z = 746.047f, HeadingY = -0.667692f, HeadingW = 0.744437f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 755.138f, Y = 47.415f, Z = 748.431f, HeadingY = 0.058195f, HeadingW = 0.998305f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 65, Health = 4367, NpcFamily = 189, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 788.739f, Y = 39.915f, Z = 756.738f, HeadingY = 0.360773f, HeadingW = 0.932654f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 776.407f, Y = 47.415f, Z = 748.711f, HeadingY = 0.059329f, HeadingW = 0.998239f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 779.048f, Y = 47.415f, Z = 746.621f, HeadingY = 0.727386f, HeadingW = 0.686229f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 69, Health = 4731, NpcFamily = 191, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 712.206f, Y = 48.648f, Z = 726.974f, HeadingY = -0.983947f, HeadingW = 0.178463f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 69, Health = 4731, NpcFamily = 191, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 734.772f, Y = 39.915f, Z = 705.105f, HeadingY = 0.999999f, HeadingW = 0.001485f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 67, Health = 4549, NpcFamily = 191, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 659.253f, Y = 30.710f, Z = 791.249f, HeadingY = -0.676751f, HeadingW = 0.736212f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 66, Health = 4458, NpcFamily = 189, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 738.015f, Y = 30.136f, Z = 774.176f, HeadingY = 0.999996f, HeadingW = 0.002876f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 64, Health = 4276, NpcFamily = 190, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 788.637f, Y = 36.349f, Z = 790.710f, HeadingY = -0.866255f, HeadingW = 0.499603f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 765.826f, Y = 34.410f, Z = 769.144f, HeadingY = 0.999339f, HeadingW = 0.036355f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 66, Health = 4458, NpcFamily = 191, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 704.771f, Y = 14.909f, Z = 815.267f, HeadingY = 0.050665f, HeadingW = 0.998716f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 65, Health = 4367, NpcFamily = 191, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 711.142f, Y = 18.715f, Z = 810.284f, HeadingY = 0.272775f, HeadingW = 0.962078f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 67, Health = 4549, NpcFamily = 191, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 687.703f, Y = 6.215f, Z = 838.679f, HeadingY = 0.721024f, HeadingW = 0.692910f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 65, Health = 4367, NpcFamily = 189, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 768.772f, Y = 18.715f, Z = 810.213f, HeadingY = 0.835565f, HeadingW = 0.549392f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 64, Health = 4276, NpcFamily = 189, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 750.825f, Y = 28.269f, Z = 807.139f, HeadingY = 0.953422f, HeadingW = 0.301639f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 65, Health = 4367, NpcFamily = 191, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 692.397f, Y = 13.715f, Z = 830.364f, HeadingY = -0.714042f, HeadingW = 0.700103f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 719.087f, Y = 13.715f, Z = 830.688f, HeadingY = 0.580482f, HeadingW = 0.814273f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.119f, Y = 13.715f, Z = 832.542f, HeadingY = -0.245069f, HeadingW = 0.969506f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 65, Health = 4367, NpcFamily = 191, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 717.806f, Y = 13.715f, Z = 832.258f, HeadingY = 0.161434f, HeadingW = 0.986883f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 66, Health = 4458, NpcFamily = 191, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 720.218f, Y = 25.610f, Z = 806.912f, HeadingY = -0.501825f, HeadingW = 0.864969f, Textures = null, Meshes = null },
                new MobSlot { Name = "Malah-Ana", PlayfieldId = 4543, Side = 3, MonsterData = 209229, Level = 64, Health = 4276, NpcFamily = 191, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 726.930f, Y = 25.610f, Z = 809.545f, HeadingY = 0.702014f, HeadingW = 0.712163f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 65, Health = 4367, NpcFamily = 189, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 745.167f, Y = 26.912f, Z = 806.699f, HeadingY = -0.799380f, HeadingW = 0.600825f, Textures = null, Meshes = null },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 69, Health = 4731, NpcFamily = 189, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 776.008f, Y = 6.215f, Z = 838.823f, HeadingY = -0.717866f, HeadingW = 0.696181f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 903.429f, Y = 36.892f, Z = 783.992f, HeadingY = 0.840706f, HeadingW = 0.444041f, Textures = null, Meshes = null },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 60, Health = 3911, NpcFamily = 202, Scale = 150, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 837.048f, Y = 33.367f, Z = 792.478f, HeadingY = 0.394986f, HeadingW = 0.918687f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Brisk Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 69, Health = 4731, NpcFamily = 189, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 804.851f, Y = 34.824f, Z = 835.400f, HeadingY = -0.588802f, HeadingW = 0.808277f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 64, Health = 4276, NpcFamily = 190, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 813.058f, Y = 34.815f, Z = 799.979f, HeadingY = -0.964671f, HeadingW = 0.263457f, Textures = null, Meshes = null },
                new MobSlot { Name = "Calan-El", PlayfieldId = 4543, Side = 2, MonsterData = 246043, Level = 65, Health = 4367, NpcFamily = 202, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 642.120f, Y = 45.250f, Z = 736.782f, HeadingY = -0.977627f, HeadingW = 0.210345f, Textures = null, Meshes = new[] { new[] { 1, 209521, 0, 2 } } },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 55, Health = 3456, NpcFamily = 175, Scale = 125, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 878.030f, Y = 34.338f, Z = 599.875f, HeadingY = -0.792862f, HeadingW = 0.609402f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 54, Health = 3365, NpcFamily = 175, Scale = 125, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 880.017f, Y = 29.029f, Z = 583.011f, HeadingY = 0.854905f, HeadingW = 0.518784f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 52, Health = 3183, NpcFamily = 175, Scale = 125, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 876.448f, Y = 7.519f, Z = 572.198f, HeadingY = 0.999934f, HeadingW = -0.011496f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 66, Health = 4458, NpcFamily = 170, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 880.394f, Y = 6.462f, Z = 563.512f, HeadingY = 0.030028f, HeadingW = 0.999549f, Textures = null, Meshes = null },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4543, Side = 3, MonsterData = 209173, Level = 54, Health = 2019, NpcFamily = 200, Scale = 100, RunSpeed = 242, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 893.115f, Y = 32.410f, Z = 679.869f, HeadingY = -0.999396f, HeadingW = 0.034764f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 65, Health = 4367, NpcFamily = 170, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 943.580f, Y = 4.771f, Z = 580.725f, HeadingY = -0.871505f, HeadingW = 0.490387f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 67, Health = 4549, NpcFamily = 170, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 947.731f, Y = 3.618f, Z = 579.713f, HeadingY = -0.894851f, HeadingW = 0.446366f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1032.194f, Y = 34.010f, Z = 591.480f, HeadingY = 0.805919f, HeadingW = 0.592026f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 109, Health = 9663, NpcFamily = 183, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1016.400f, Y = 51.645f, Z = 561.901f, HeadingY = -0.495438f, HeadingW = 0.868644f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 55, Health = 3456, NpcFamily = 175, Scale = 125, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1003.494f, Y = 33.470f, Z = 597.564f, HeadingY = -0.761420f, HeadingW = 0.648259f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1078.995f, Y = 34.010f, Z = 591.633f, HeadingY = -0.715360f, HeadingW = 0.698757f, Textures = null, Meshes = null },
                new MobSlot { Name = "Guard - Elmo Fitz", PlayfieldId = 4543, Side = 0, MonsterData = 26135, Level = 200, Health = 65910, NpcFamily = 158, Scale = 100, RunSpeed = 515, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 40271, X = 1005.531f, Y = 31.533f, Z = 633.792f, HeadingY = 0.058493f, HeadingW = 0.998288f, Textures = new[] { new[] { 0, 215123, 0 }, new[] { 1, 215121, 0 }, new[] { 2, 215122, 0 }, new[] { 3, 215120, 0 }, new[] { 4, 215124, 0 } }, Meshes = new[] { new[] { 0, 205116, 0, 2 }, new[] { 0, 40271, 0, 4 } } },
                new MobSlot { Name = "Guard", PlayfieldId = 4543, Side = 0, MonsterData = 26097, Level = 200, Health = 65910, NpcFamily = 158, Scale = 100, RunSpeed = 515, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 40111, X = 1023.255f, Y = 30.262f, Z = 671.737f, HeadingY = -0.788095f, HeadingW = 0.615554f, Textures = new[] { new[] { 0, 215123, 0 }, new[] { 1, 215121, 0 }, new[] { 2, 215122, 0 }, new[] { 3, 215120, 0 }, new[] { 4, 215124, 0 } }, Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40111, 0, 4 } } },
                new MobSlot { Name = "Guard", PlayfieldId = 4543, Side = 0, MonsterData = 26097, Level = 200, Health = 65910, NpcFamily = 158, Scale = 100, RunSpeed = 515, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 40111, X = 1036.903f, Y = 28.948f, Z = 660.773f, HeadingY = -0.998893f, HeadingW = 0.047040f, Textures = new[] { new[] { 0, 215123, 0 }, new[] { 1, 215121, 0 }, new[] { 2, 215122, 0 }, new[] { 3, 215120, 0 }, new[] { 4, 215124, 0 } }, Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40111, 0, 4 } } },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 939.637f, Y = 31.921f, Z = 523.438f, HeadingY = -0.111391f, HeadingW = 0.993777f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1030.151f, Y = 34.010f, Z = 530.812f, HeadingY = 0.774463f, HeadingW = 0.632619f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 58, Health = 3729, NpcFamily = 175, Scale = 125, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 926.818f, Y = 5.156f, Z = 551.490f, HeadingY = -0.260132f, HeadingW = 0.965573f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 923.427f, Y = 3.619f, Z = 547.461f, HeadingY = -0.925931f, HeadingW = 0.377692f, Textures = null, Meshes = null },
                new MobSlot { Name = "Prime Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 223561, Level = 110, Health = 9822, NpcFamily = 195, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1034.424f, Y = 34.010f, Z = 529.151f, HeadingY = -0.999198f, HeadingW = 0.040046f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 938.386f, Y = 34.461f, Z = 484.485f, HeadingY = -0.760493f, HeadingW = 0.649346f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 56, Health = 3547, NpcFamily = 175, Scale = 125, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 968.568f, Y = 24.296f, Z = 492.327f, HeadingY = 0.925384f, HeadingW = 0.379032f, Textures = null, Meshes = null },
                new MobSlot { Name = "Prime Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 223561, Level = 110, Health = 9822, NpcFamily = 195, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1026.669f, Y = 34.010f, Z = 519.566f, HeadingY = 0.385165f, HeadingW = 0.922848f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 100, Health = 8233, NpcFamily = 195, Scale = 130, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1039.328f, Y = 42.010f, Z = 498.878f, HeadingY = 0.995610f, HeadingW = 0.093594f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1003.000f, Y = 25.024f, Z = 516.900f, HeadingY = -0.086645f, HeadingW = 0.996239f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 62, Health = 4093, NpcFamily = 170, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 894.680f, Y = 4.410f, Z = 509.612f, HeadingY = 0.152102f, HeadingW = 0.988365f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 161, Health = 25162, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 908.028f, Y = 4.086f, Z = 529.107f, HeadingY = 0.153622f, HeadingW = 0.988130f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 889.866f, Y = 29.758f, Z = 497.913f, HeadingY = 0.670681f, HeadingW = 0.741746f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 890.764f, Y = 29.835f, Z = 499.475f, HeadingY = 0.775774f, HeadingW = 0.631011f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 895.302f, Y = 27.783f, Z = 511.084f, HeadingY = -0.935785f, HeadingW = 0.352570f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 896.906f, Y = 27.709f, Z = 513.572f, HeadingY = 0.230669f, HeadingW = 0.973032f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 64, Health = 4276, NpcFamily = 170, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 968.673f, Y = 28.888f, Z = 475.328f, HeadingY = 0.999936f, HeadingW = -0.011352f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 999.987f, Y = 33.446f, Z = 562.329f, HeadingY = -0.869242f, HeadingW = 0.494386f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 979.214f, Y = 8.361f, Z = 463.090f, HeadingY = -0.998909f, HeadingW = 0.046693f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 983.878f, Y = 21.153f, Z = 473.696f, HeadingY = -0.998895f, HeadingW = 0.047003f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1047.358f, Y = 37.586f, Z = 319.885f, HeadingY = -0.962129f, HeadingW = 0.272594f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 55, Health = 3456, NpcFamily = 175, Scale = 125, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1060.895f, Y = 30.456f, Z = 466.589f, HeadingY = -0.890506f, HeadingW = 0.454972f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1122.814f, Y = 1.610f, Z = 442.693f, HeadingY = 0.384431f, HeadingW = 0.923154f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 57, Health = 3638, NpcFamily = 190, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1041.483f, Y = 34.010f, Z = 500.734f, HeadingY = -0.958928f, HeadingW = 0.283651f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1066.327f, Y = 34.010f, Z = 508.393f, HeadingY = 0.821932f, HeadingW = 0.569585f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 108, Health = 9504, NpcFamily = 183, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1053.500f, Y = 51.645f, Z = 500.300f, HeadingY = 0.999185f, HeadingW = 0.040369f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1071.142f, Y = 34.010f, Z = 517.607f, HeadingY = -0.325543f, HeadingW = 0.945527f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 103, Health = 8710, NpcFamily = 195, Scale = 130, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1072.601f, Y = 34.010f, Z = 513.714f, HeadingY = 0.643744f, HeadingW = 0.765241f, Textures = null, Meshes = null },
                new MobSlot { Name = "Arcorash", PlayfieldId = 4543, Side = 3, MonsterData = 208904, Level = 57, Health = 3638, NpcFamily = 173, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1039.801f, Y = 34.010f, Z = 517.034f, HeadingY = -0.380245f, HeadingW = 0.924886f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 103, Health = 8710, NpcFamily = 195, Scale = 130, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.878f, Y = 42.008f, Z = 498.596f, HeadingY = 0.716377f, HeadingW = 0.697713f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1067.596f, Y = 42.011f, Z = 499.008f, HeadingY = -0.982330f, HeadingW = 0.187156f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 100, Health = 8233, NpcFamily = 183, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1041.999f, Y = 46.235f, Z = 558.100f, HeadingY = -0.992013f, HeadingW = 0.126133f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 105, Health = 9027, NpcFamily = 183, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1045.600f, Y = 49.765f, Z = 553.400f, HeadingY = -0.025940f, HeadingW = 0.999664f, Textures = null, Meshes = null },
                new MobSlot { Name = "Prime Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 223561, Level = 110, Health = 9822, NpcFamily = 195, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1047.144f, Y = 34.010f, Z = 528.820f, HeadingY = -0.927238f, HeadingW = 0.374472f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1068.646f, Y = 34.010f, Z = 521.856f, HeadingY = -0.630728f, HeadingW = 0.776004f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 69, Health = 4731, NpcFamily = 170, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 886.520f, Y = 4.410f, Z = 319.893f, HeadingY = 0.975621f, HeadingW = 0.219462f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 978.779f, Y = 69.832f, Z = 422.071f, HeadingY = -0.990678f, HeadingW = 0.136228f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 66, Health = 4458, NpcFamily = 170, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 973.298f, Y = 7.586f, Z = 423.751f, HeadingY = 0.973747f, HeadingW = 0.227633f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 68, Health = 4640, NpcFamily = 170, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 973.473f, Y = 6.194f, Z = 405.442f, HeadingY = -0.644484f, HeadingW = 0.764618f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 68, Health = 4640, NpcFamily = 170, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 968.430f, Y = 6.933f, Z = 426.072f, HeadingY = 0.990458f, HeadingW = 0.137817f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 988.960f, Y = 24.249f, Z = 417.154f, HeadingY = 0.848032f, HeadingW = 0.529945f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 973.454f, Y = 8.327f, Z = 426.231f, HeadingY = 0.862896f, HeadingW = 0.505381f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 54, Health = 3365, NpcFamily = 186, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 974.036f, Y = 5.188f, Z = 400.853f, HeadingY = -0.530589f, HeadingW = 0.847629f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 978.472f, Y = 10.781f, Z = 438.798f, HeadingY = -0.948472f, HeadingW = 0.316862f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1051.561f, Y = 31.840f, Z = 406.368f, HeadingY = -0.920902f, HeadingW = 0.389795f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1098.891f, Y = 28.602f, Z = 479.101f, HeadingY = 0.998101f, HeadingW = 0.061603f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1103.377f, Y = 37.202f, Z = 423.191f, HeadingY = 0.610539f, HeadingW = 0.791986f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1103.714f, Y = 35.534f, Z = 416.828f, HeadingY = 0.857956f, HeadingW = 0.513723f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1116.975f, Y = 1.610f, Z = 427.997f, HeadingY = 0.958854f, HeadingW = 0.283900f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1106.763f, Y = 34.290f, Z = 497.586f, HeadingY = -0.998886f, HeadingW = 0.047197f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1104.577f, Y = 34.072f, Z = 500.788f, HeadingY = -0.975520f, HeadingW = 0.219911f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 100, Health = 8233, NpcFamily = 195, Scale = 130, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1109.146f, Y = 34.393f, Z = 500.918f, HeadingY = 0.978949f, HeadingW = 0.204107f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1135.896f, Y = 1.610f, Z = 429.012f, HeadingY = 0.994986f, HeadingW = 0.100014f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1132.082f, Y = 1.610f, Z = 459.948f, HeadingY = 0.999185f, HeadingW = 0.040372f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1125.965f, Y = 1.610f, Z = 453.879f, HeadingY = 0.403115f, HeadingW = 0.915149f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1131.965f, Y = 1.610f, Z = 518.271f, HeadingY = 0.794795f, HeadingW = 0.606878f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1143.571f, Y = 1.610f, Z = 507.984f, HeadingY = 0.968871f, HeadingW = 0.247567f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1150.432f, Y = 1.610f, Z = 499.031f, HeadingY = -0.903679f, HeadingW = 0.428210f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1132.741f, Y = 1.610f, Z = 487.854f, HeadingY = -0.526335f, HeadingW = 0.850277f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1129.475f, Y = 2.507f, Z = 493.087f, HeadingY = -0.082368f, HeadingW = 0.996602f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1097.447f, Y = 34.010f, Z = 538.602f, HeadingY = -0.711304f, HeadingW = 0.702884f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1080.029f, Y = 34.010f, Z = 516.295f, HeadingY = 0.677813f, HeadingW = 0.735234f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1093.279f, Y = 34.020f, Z = 576.839f, HeadingY = 0.915168f, HeadingW = 0.403072f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1089.190f, Y = 34.010f, Z = 542.634f, HeadingY = 0.921504f, HeadingW = 0.388369f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1091.509f, Y = 34.010f, Z = 550.441f, HeadingY = -0.877294f, HeadingW = 0.479954f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1090.372f, Y = 34.010f, Z = 549.043f, HeadingY = -0.430350f, HeadingW = 0.902662f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1088.410f, Y = 34.010f, Z = 556.343f, HeadingY = 0.337537f, HeadingW = 0.941312f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 102, Health = 8551, NpcFamily = 183, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1136.901f, Y = 46.215f, Z = 541.801f, HeadingY = -0.677348f, HeadingW = 0.731097f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 103, Health = 8710, NpcFamily = 183, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1129.500f, Y = 46.190f, Z = 526.000f, HeadingY = 0.906956f, HeadingW = 0.414128f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1149.109f, Y = 1.610f, Z = 555.352f, HeadingY = -0.920425f, HeadingW = 0.390919f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 103, Health = 8710, NpcFamily = 183, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1054.100f, Y = 45.525f, Z = 571.199f, HeadingY = -0.370113f, HeadingW = 0.928987f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 103, Health = 8710, NpcFamily = 183, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1072.000f, Y = 45.525f, Z = 570.699f, HeadingY = 0.331814f, HeadingW = 0.943345f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1079.289f, Y = 40.125f, Z = 576.519f, HeadingY = 0.533040f, HeadingW = 0.846090f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 105, Health = 9027, NpcFamily = 195, Scale = 130, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1080.319f, Y = 33.981f, Z = 582.329f, HeadingY = 0.897966f, HeadingW = 0.440064f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1094.526f, Y = 38.298f, Z = 585.912f, HeadingY = -0.011447f, HeadingW = 0.999934f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1095.949f, Y = 30.995f, Z = 597.108f, HeadingY = 0.578779f, HeadingW = 0.815484f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 103, Health = 8710, NpcFamily = 195, Scale = 130, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1081.632f, Y = 38.463f, Z = 572.789f, HeadingY = 0.351693f, HeadingW = 0.936116f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 101, Health = 8392, NpcFamily = 183, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1133.000f, Y = 37.108f, Z = 582.200f, HeadingY = 0.830626f, HeadingW = 0.555682f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 102, Health = 8551, NpcFamily = 183, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1132.300f, Y = 35.931f, Z = 565.200f, HeadingY = -0.713220f, HeadingW = 0.700040f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1153.556f, Y = 1.610f, Z = 577.611f, HeadingY = 0.965268f, HeadingW = 0.261261f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1145.330f, Y = 1.610f, Z = 581.520f, HeadingY = 0.643337f, HeadingW = 0.765583f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1140.476f, Y = 2.320f, Z = 595.292f, HeadingY = 0.466260f, HeadingW = 0.884648f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1152.344f, Y = 1.610f, Z = 573.858f, HeadingY = 0.467136f, HeadingW = 0.884186f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1147.411f, Y = 1.610f, Z = 573.620f, HeadingY = -0.999106f, HeadingW = 0.042272f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1146.254f, Y = 1.610f, Z = 578.118f, HeadingY = -0.424161f, HeadingW = 0.905587f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 100, Health = 8233, NpcFamily = 183, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.309f, Y = 42.013f, Z = 609.097f, HeadingY = 0.700765f, HeadingW = 0.713392f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 100, Health = 8233, NpcFamily = 183, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1051.500f, Y = 51.645f, Z = 607.500f, HeadingY = -0.993860f, HeadingW = 0.110648f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 100, Health = 8233, NpcFamily = 183, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1077.300f, Y = 42.014f, Z = 609.100f, HeadingY = -0.652716f, HeadingW = 0.757602f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4543, Side = 3, MonsterData = 209354, Level = 102, Health = 8551, NpcFamily = 183, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1077.600f, Y = 32.716f, Z = 613.800f, HeadingY = -0.237482f, HeadingW = 0.850099f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 180, Health = 32616, NpcFamily = 174, Scale = 100, RunSpeed = 448, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1083.850f, Y = 28.409f, Z = 627.995f, HeadingY = -0.869929f, HeadingW = 0.493178f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 102, Health = 8551, NpcFamily = 195, Scale = 130, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1088.901f, Y = 28.725f, Z = 621.900f, HeadingY = -0.087349f, HeadingW = 0.996178f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 101, Health = 8392, NpcFamily = 195, Scale = 130, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1117.774f, Y = 28.136f, Z = 612.803f, HeadingY = -0.676343f, HeadingW = 0.736587f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1141.102f, Y = 2.195f, Z = 601.118f, HeadingY = -0.836853f, HeadingW = 0.547428f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4543, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1127.442f, Y = 28.757f, Z = 617.759f, HeadingY = 0.369625f, HeadingW = 0.929181f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1108.599f, Y = 1.610f, Z = 395.756f, HeadingY = 0.740118f, HeadingW = 0.672477f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1108.277f, Y = 1.610f, Z = 364.152f, HeadingY = 0.375209f, HeadingW = 0.926940f, Textures = null, Meshes = null },
                new MobSlot { Name = "Carlo Pinnetti", PlayfieldId = 4543, Side = 0, MonsterData = 258209, Level = 220, Health = 55687, NpcFamily = 97, Scale = 130, RunSpeed = 1138, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 40121, X = 953.025f, Y = 28.511f, Z = 600.868f, HeadingY = 0.000000f, HeadingW = 1.000000f, Textures = new[] { new[] { 0, 0, 0 }, new[] { 1, 284557, 0 }, new[] { 2, 247977, 0 }, new[] { 3, 247887, 0 }, new[] { 4, 248016, 0 } }, Meshes = new[] { new[] { 0, 204896, 0, 0 }, new[] { 0, 40121, 0, 4 }, new[] { 1, 29084, 0, 2 } } },
                new MobSlot { Name = "CEO Guardian", PlayfieldId = 4543, Side = 0, MonsterData = 227701, Level = 215, Health = 34513, NpcFamily = 95, Scale = 125, RunSpeed = 1062, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 953.025f, Y = 28.511f, Z = 600.868f, HeadingY = 0.000000f, HeadingW = 1.000000f, Textures = null, Meshes = new[] { new[] { 1, 273304, 0, 2 } } },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 953.507f, Y = 36.712f, Z = 361.473f, HeadingY = 0.978942f, HeadingW = 0.204139f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 896.210f, Y = 32.374f, Z = 679.969f, HeadingY = 0.990768f, HeadingW = 0.135571f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 987.986f, Y = 31.042f, Z = 744.869f, HeadingY = -0.381507f, HeadingW = 0.924366f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 66, Health = 4458, NpcFamily = 170, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 884.899f, Y = 4.104f, Z = 488.692f, HeadingY = 0.485930f, HeadingW = 0.873997f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 62, Health = 4093, NpcFamily = 170, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 851.829f, Y = 7.172f, Z = 489.184f, HeadingY = 0.843977f, HeadingW = 0.536379f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 52, Health = 3183, NpcFamily = 172, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 860.868f, Y = 12.611f, Z = 555.599f, HeadingY = 0.696169f, HeadingW = 0.717878f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 64, Health = 4276, NpcFamily = 170, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 876.264f, Y = 6.071f, Z = 555.863f, HeadingY = -0.870249f, HeadingW = 0.492612f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 69, Health = 4731, NpcFamily = 170, Scale = 100, RunSpeed = 243, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 876.703f, Y = 6.809f, Z = 559.968f, HeadingY = -0.422273f, HeadingW = 0.906469f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 864.678f, Y = 27.041f, Z = 524.420f, HeadingY = 0.465994f, HeadingW = 0.884788f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 58, Health = 3729, NpcFamily = 175, Scale = 125, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 841.837f, Y = 30.659f, Z = 547.100f, HeadingY = 0.469854f, HeadingW = 0.882744f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 845.866f, Y = 36.378f, Z = 525.491f, HeadingY = 0.982631f, HeadingW = 0.185570f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 880.448f, Y = 5.154f, Z = 450.098f, HeadingY = -0.284819f, HeadingW = 0.958581f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 871.755f, Y = 34.539f, Z = 439.963f, HeadingY = 0.998619f, HeadingW = 0.052541f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 845.115f, Y = 32.773f, Z = 439.980f, HeadingY = 0.976812f, HeadingW = 0.214100f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 63, Health = 4184, NpcFamily = 186, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 889.645f, Y = 4.415f, Z = 427.979f, HeadingY = 0.979418f, HeadingW = 0.201844f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 936.677f, Y = 34.669f, Z = 403.877f, HeadingY = -0.817418f, HeadingW = 0.576046f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 938.060f, Y = 34.764f, Z = 404.176f, HeadingY = 0.640353f, HeadingW = 0.768081f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 954.254f, Y = 4.508f, Z = 422.460f, HeadingY = -0.530210f, HeadingW = 0.847867f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 844.150f, Y = 31.927f, Z = 364.594f, HeadingY = 0.279761f, HeadingW = 0.960070f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 56, Health = 3547, NpcFamily = 202, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 768.841f, Y = 36.410f, Z = 439.953f, HeadingY = 0.998274f, HeadingW = 0.058726f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 61, Health = 4002, NpcFamily = 186, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 902.718f, Y = 4.232f, Z = 385.573f, HeadingY = -0.920396f, HeadingW = 0.390988f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 887.085f, Y = 32.330f, Z = 364.830f, HeadingY = 0.215087f, HeadingW = 0.976595f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 885.667f, Y = 32.305f, Z = 361.845f, HeadingY = -0.981321f, HeadingW = 0.192377f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 860.835f, Y = 5.839f, Z = 348.436f, HeadingY = 0.918831f, HeadingW = 0.394651f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 66, Health = 4458, NpcFamily = 170, Scale = 100, RunSpeed = 231, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 911.483f, Y = 4.072f, Z = 323.957f, HeadingY = -0.984277f, HeadingW = 0.176631f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 65, Health = 4367, NpcFamily = 170, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 909.872f, Y = 4.582f, Z = 325.753f, HeadingY = -0.640945f, HeadingW = 0.767586f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 900.856f, Y = 41.044f, Z = 334.830f, HeadingY = -0.796833f, HeadingW = 0.604199f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 903.293f, Y = 41.264f, Z = 332.937f, HeadingY = -0.994760f, HeadingW = 0.102240f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 936.411f, Y = 30.205f, Z = 279.976f, HeadingY = 0.995510f, HeadingW = 0.094656f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 999.439f, Y = 50.736f, Z = 250.393f, HeadingY = 0.985071f, HeadingW = 0.172147f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 928.999f, Y = 33.517f, Z = 327.458f, HeadingY = 0.932756f, HeadingW = 0.360508f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 65, Health = 4367, NpcFamily = 186, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 885.084f, Y = 4.069f, Z = 335.443f, HeadingY = 0.201417f, HeadingW = 0.979506f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 930.139f, Y = 32.964f, Z = 306.932f, HeadingY = -0.582510f, HeadingW = 0.812824f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 64, Health = 4276, NpcFamily = 190, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 930.422f, Y = 33.049f, Z = 303.653f, HeadingY = -0.865588f, HeadingW = 0.500757f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 921.390f, Y = 38.466f, Z = 279.343f, HeadingY = 0.255133f, HeadingW = 0.966906f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 825.801f, Y = 40.384f, Z = 360.574f, HeadingY = -0.742677f, HeadingW = 0.669649f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 804.012f, Y = 1.610f, Z = 387.327f, HeadingY = -0.736489f, HeadingW = 0.676450f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 68, Health = 4640, NpcFamily = 170, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 898.516f, Y = 2.921f, Z = 192.913f, HeadingY = -0.998928f, HeadingW = 0.046290f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 797.552f, Y = 2.655f, Z = 353.906f, HeadingY = 0.150680f, HeadingW = 0.988583f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 798.665f, Y = 1.610f, Z = 384.230f, HeadingY = 0.191926f, HeadingW = 0.981409f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 797.372f, Y = 1.610f, Z = 395.513f, HeadingY = 0.999999f, HeadingW = -0.001638f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 798.269f, Y = 28.384f, Z = 434.045f, HeadingY = -0.090744f, HeadingW = 0.995874f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 781.896f, Y = 40.574f, Z = 429.345f, HeadingY = 0.996902f, HeadingW = 0.078650f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 775.257f, Y = 39.922f, Z = 415.518f, HeadingY = 0.893793f, HeadingW = 0.448479f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 790.058f, Y = 32.214f, Z = 439.966f, HeadingY = 0.978926f, HeadingW = 0.204217f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 782.737f, Y = 37.853f, Z = 439.932f, HeadingY = 0.999713f, HeadingW = -0.023947f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 162, Health = 25554, NpcFamily = 171, Scale = 100, RunSpeed = 439, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 875.720f, Y = 4.386f, Z = 319.950f, HeadingY = -0.997535f, HeadingW = 0.070171f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4543, Side = 3, MonsterData = 217022, Level = 64, Health = 4276, NpcFamily = 170, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 879.985f, Y = 4.410f, Z = 324.447f, HeadingY = -0.154302f, HeadingW = 0.988024f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 824.909f, Y = 2.188f, Z = 304.189f, HeadingY = -0.248656f, HeadingW = 0.968592f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 831.940f, Y = 2.184f, Z = 287.829f, HeadingY = 0.552706f, HeadingW = 0.833376f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 842.984f, Y = 4.410f, Z = 305.511f, HeadingY = 0.690112f, HeadingW = 0.723703f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 825.116f, Y = 2.934f, Z = 325.113f, HeadingY = 0.633691f, HeadingW = 0.773587f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 848.758f, Y = 2.512f, Z = 256.961f, HeadingY = -0.956038f, HeadingW = 0.293243f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 876.065f, Y = 2.645f, Z = 224.473f, HeadingY = -0.772479f, HeadingW = 0.635040f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 894.235f, Y = 2.676f, Z = 193.763f, HeadingY = -0.994400f, HeadingW = 0.105682f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 905.836f, Y = 2.549f, Z = 164.437f, HeadingY = 0.918169f, HeadingW = 0.396190f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 64, Health = 4276, NpcFamily = 186, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 942.581f, Y = 1.610f, Z = 199.631f, HeadingY = 0.970741f, HeadingW = 0.240128f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 64, Health = 4276, NpcFamily = 190, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 950.694f, Y = 38.690f, Z = 256.673f, HeadingY = 0.977734f, HeadingW = 0.209847f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 60, Health = 3911, NpcFamily = 190, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 954.460f, Y = 39.206f, Z = 256.413f, HeadingY = -0.994420f, HeadingW = 0.105491f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 939.133f, Y = 37.216f, Z = 248.859f, HeadingY = 0.994581f, HeadingW = 0.103962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 960.342f, Y = 1.610f, Z = 235.858f, HeadingY = -0.343766f, HeadingW = 0.939055f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 921.182f, Y = 2.958f, Z = 157.877f, HeadingY = -0.087208f, HeadingW = 0.996190f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 974.192f, Y = 3.124f, Z = 101.584f, HeadingY = 0.748092f, HeadingW = 0.663595f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 986.776f, Y = 2.642f, Z = 79.779f, HeadingY = -0.922564f, HeadingW = 0.385845f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1026.786f, Y = 3.446f, Z = 68.724f, HeadingY = -0.868960f, HeadingW = 0.494883f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1032.817f, Y = 1.610f, Z = 172.683f, HeadingY = -0.314380f, HeadingW = 0.949297f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1067.191f, Y = 1.610f, Z = 143.022f, HeadingY = -0.510327f, HeadingW = 0.859980f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1053.869f, Y = 1.610f, Z = 179.006f, HeadingY = 0.006611f, HeadingW = 0.999978f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1050.301f, Y = 1.610f, Z = 132.899f, HeadingY = 0.655588f, HeadingW = 0.755119f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1082.162f, Y = 1.610f, Z = 141.038f, HeadingY = -0.295926f, HeadingW = 0.955211f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1151.227f, Y = 1.610f, Z = 233.821f, HeadingY = -0.376492f, HeadingW = 0.926420f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1102.915f, Y = 1.610f, Z = 159.369f, HeadingY = 0.078241f, HeadingW = 0.996934f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1002.333f, Y = 50.857f, Z = 250.796f, HeadingY = -0.643544f, HeadingW = 0.765409f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1008.808f, Y = 50.215f, Z = 249.386f, HeadingY = -0.832052f, HeadingW = 0.554698f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1111.053f, Y = 1.610f, Z = 258.677f, HeadingY = 0.869833f, HeadingW = 0.493346f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1071.301f, Y = 36.140f, Z = 315.532f, HeadingY = 0.996923f, HeadingW = 0.078389f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1071.629f, Y = 38.979f, Z = 292.835f, HeadingY = 0.783951f, HeadingW = 0.620823f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1069.441f, Y = 37.066f, Z = 286.385f, HeadingY = 0.996585f, HeadingW = 0.082573f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1041.542f, Y = 40.241f, Z = 283.797f, HeadingY = 0.470337f, HeadingW = 0.882487f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 999.207f, Y = 1.610f, Z = 207.356f, HeadingY = 0.527801f, HeadingW = 0.849368f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 61, Health = 4002, NpcFamily = 190, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1079.505f, Y = 36.010f, Z = 377.645f, HeadingY = -0.997607f, HeadingW = 0.069134f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.519f, Y = 36.424f, Z = 320.533f, HeadingY = 0.606603f, HeadingW = 0.795005f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 175, Health = 30654, NpcFamily = 174, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1180.808f, Y = 1.610f, Z = 324.604f, HeadingY = 0.059525f, HeadingW = 0.998227f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1179.805f, Y = 1.610f, Z = 402.939f, HeadingY = 0.496183f, HeadingW = 0.868218f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1167.522f, Y = 1.610f, Z = 546.507f, HeadingY = -0.231209f, HeadingW = 0.972904f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1168.764f, Y = 1.610f, Z = 550.725f, HeadingY = -0.776972f, HeadingW = 0.629535f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1207.326f, Y = 1.610f, Z = 486.297f, HeadingY = 0.718046f, HeadingW = 0.695996f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1234.770f, Y = 1.610f, Z = 489.195f, HeadingY = 0.407859f, HeadingW = 0.913045f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1197.045f, Y = 1.610f, Z = 570.338f, HeadingY = 0.948150f, HeadingW = 0.317824f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1216.900f, Y = 1.610f, Z = 650.404f, HeadingY = -0.693590f, HeadingW = 0.720370f, Textures = null, Meshes = null },
                new MobSlot { Name = "Guard", PlayfieldId = 4543, Side = 0, MonsterData = 26097, Level = 200, Health = 65910, NpcFamily = 158, Scale = 100, RunSpeed = 515, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 40111, X = 1122.116f, Y = 1.610f, Z = 713.219f, HeadingY = 0.899794f, HeadingW = 0.436315f, Textures = new[] { new[] { 0, 215123, 0 }, new[] { 1, 215121, 0 }, new[] { 2, 215122, 0 }, new[] { 3, 215120, 0 }, new[] { 4, 215124, 0 } }, Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40111, 0, 4 } } },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1277.462f, Y = 1.610f, Z = 726.459f, HeadingY = 0.209121f, HeadingW = 0.977890f, Textures = null, Meshes = null },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4543, Side = 3, MonsterData = 209173, Level = 57, Health = 2183, NpcFamily = 200, Scale = 100, RunSpeed = 257, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1122.632f, Y = 31.089f, Z = 827.917f, HeadingY = 0.878446f, HeadingW = 0.472495f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1180.805f, Y = 30.341f, Z = 805.625f, HeadingY = 0.107169f, HeadingW = 0.994241f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1155.976f, Y = 30.010f, Z = 804.829f, HeadingY = -0.948668f, HeadingW = 0.316273f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1223.975f, Y = 48.675f, Z = 831.727f, HeadingY = -0.776699f, HeadingW = 0.629872f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1222.285f, Y = 48.675f, Z = 829.585f, HeadingY = -0.708282f, HeadingW = 0.705930f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 101, Health = 8392, NpcFamily = 193, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1218.930f, Y = 33.231f, Z = 835.821f, HeadingY = 0.971042f, HeadingW = 0.238911f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1222.445f, Y = 32.909f, Z = 836.323f, HeadingY = 0.988843f, HeadingW = 0.148963f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 109, Health = 9663, NpcFamily = 193, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1217.311f, Y = 33.499f, Z = 831.943f, HeadingY = 0.583286f, HeadingW = 0.812267f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1224.099f, Y = 48.675f, Z = 828.560f, HeadingY = 0.999997f, HeadingW = -0.002503f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1225.379f, Y = 48.675f, Z = 830.026f, HeadingY = 0.728569f, HeadingW = 0.684972f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1195.238f, Y = 31.009f, Z = 808.569f, HeadingY = -0.795900f, HeadingW = 0.605428f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1171.789f, Y = 9.515f, Z = 777.415f, HeadingY = 0.499675f, HeadingW = 0.866213f, Textures = null, Meshes = null },
                new MobSlot { Name = "Guard", PlayfieldId = 4543, Side = 0, MonsterData = 26097, Level = 200, Health = 65910, NpcFamily = 158, Scale = 100, RunSpeed = 515, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 40111, X = 1317.392f, Y = 0.932f, Z = 793.720f, HeadingY = -0.096190f, HeadingW = 0.995363f, Textures = new[] { new[] { 0, 215123, 0 }, new[] { 1, 215121, 0 }, new[] { 2, 215122, 0 }, new[] { 3, 215120, 0 }, new[] { 4, 215124, 0 } }, Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40111, 0, 4 } } },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1212.003f, Y = 42.618f, Z = 856.476f, HeadingY = 0.948119f, HeadingW = 0.317916f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1203.943f, Y = 3.210f, Z = 891.864f, HeadingY = -0.999295f, HeadingW = 0.037555f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 103, Health = 8710, NpcFamily = 193, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1208.498f, Y = 3.065f, Z = 897.482f, HeadingY = -0.833899f, HeadingW = 0.551917f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1201.442f, Y = 3.535f, Z = 882.083f, HeadingY = 0.296729f, HeadingW = 0.954962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1200.469f, Y = 3.114f, Z = 883.162f, HeadingY = -0.770210f, HeadingW = 0.637791f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 101, Health = 8392, NpcFamily = 193, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1203.217f, Y = 3.248f, Z = 923.755f, HeadingY = 0.706762f, HeadingW = 0.707452f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1166.887f, Y = 31.682f, Z = 971.305f, HeadingY = 0.351730f, HeadingW = 0.936101f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1184.486f, Y = 30.469f, Z = 986.497f, HeadingY = -0.058515f, HeadingW = 0.998287f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 107, Health = 9345, NpcFamily = 193, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1173.802f, Y = 12.403f, Z = 895.858f, HeadingY = 0.808128f, HeadingW = 0.589007f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 107, Health = 9345, NpcFamily = 193, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1174.036f, Y = 15.419f, Z = 907.934f, HeadingY = 0.432527f, HeadingW = 0.901621f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 108, Health = 9504, NpcFamily = 193, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1180.319f, Y = 14.395f, Z = 913.927f, HeadingY = 0.696892f, HeadingW = 0.717176f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1184.775f, Y = 10.915f, Z = 905.924f, HeadingY = 0.773365f, HeadingW = 0.633961f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 100, Health = 8233, NpcFamily = 193, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1187.860f, Y = 13.205f, Z = 916.626f, HeadingY = 0.736309f, HeadingW = 0.676646f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 109, Health = 9663, NpcFamily = 193, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1198.352f, Y = 2.752f, Z = 908.605f, HeadingY = 0.080380f, HeadingW = 0.996764f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1184.975f, Y = 2.281f, Z = 906.686f, HeadingY = -0.459391f, HeadingW = 0.888234f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 100, Health = 8233, NpcFamily = 193, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1166.001f, Y = 12.895f, Z = 897.897f, HeadingY = 0.673311f, HeadingW = 0.739359f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 103, Health = 8710, NpcFamily = 193, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1162.028f, Y = 14.535f, Z = 910.715f, HeadingY = -0.921529f, HeadingW = 0.388310f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 108, Health = 9504, NpcFamily = 193, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1177.184f, Y = 56.485f, Z = 901.676f, HeadingY = 0.946732f, HeadingW = 0.322023f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 108, Health = 9504, NpcFamily = 193, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1177.792f, Y = 56.877f, Z = 906.528f, HeadingY = 0.531949f, HeadingW = 0.846776f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 104, Health = 8868, NpcFamily = 193, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1175.833f, Y = 56.713f, Z = 904.581f, HeadingY = -0.586419f, HeadingW = 0.810008f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 109, Health = 9663, NpcFamily = 193, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1176.695f, Y = 57.163f, Z = 910.538f, HeadingY = -0.595554f, HeadingW = 0.803315f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1181.064f, Y = 7.305f, Z = 897.401f, HeadingY = -0.874400f, HeadingW = 0.485206f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 107, Health = 9345, NpcFamily = 193, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1162.756f, Y = 17.635f, Z = 921.463f, HeadingY = 0.578123f, HeadingW = 0.815950f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 102, Health = 8551, NpcFamily = 193, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1197.144f, Y = 3.357f, Z = 922.556f, HeadingY = 0.970906f, HeadingW = 0.239460f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 103, Health = 8710, NpcFamily = 193, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1210.076f, Y = 40.021f, Z = 989.142f, HeadingY = 0.934817f, HeadingW = 0.355129f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 106, Health = 9186, NpcFamily = 193, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1206.336f, Y = 39.083f, Z = 999.960f, HeadingY = -0.881540f, HeadingW = 0.472109f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1196.990f, Y = 37.946f, Z = 841.319f, HeadingY = -0.876580f, HeadingW = 0.481257f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1121.275f, Y = 1.610f, Z = 866.951f, HeadingY = 0.993858f, HeadingW = 0.110665f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1146.055f, Y = 1.610f, Z = 856.840f, HeadingY = 0.896405f, HeadingW = 0.443235f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 53, Health = 3274, NpcFamily = 190, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1117.610f, Y = 4.425f, Z = 870.770f, HeadingY = 0.243533f, HeadingW = 0.969893f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1212.698f, Y = 39.244f, Z = 1002.862f, HeadingY = 0.800535f, HeadingW = 0.599285f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1189.983f, Y = 46.775f, Z = 1050.088f, HeadingY = -0.111774f, HeadingW = 0.993734f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1190.068f, Y = 46.775f, Z = 1048.335f, HeadingY = 0.696006f, HeadingW = 0.718036f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1192.607f, Y = 46.775f, Z = 1050.304f, HeadingY = 0.356104f, HeadingW = 0.934446f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1192.561f, Y = 46.775f, Z = 1048.198f, HeadingY = 0.971233f, HeadingW = 0.238132f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1185.949f, Y = 30.126f, Z = 1054.960f, HeadingY = 0.969254f, HeadingW = 0.246063f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1188.926f, Y = 30.507f, Z = 1043.195f, HeadingY = 0.356907f, HeadingW = 0.934140f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1185.925f, Y = 30.003f, Z = 1045.209f, HeadingY = 0.354434f, HeadingW = 0.935081f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1184.840f, Y = 30.185f, Z = 1048.676f, HeadingY = 0.361581f, HeadingW = 0.932341f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1196.843f, Y = 31.120f, Z = 1044.114f, HeadingY = -0.437906f, HeadingW = 0.899021f, Textures = null, Meshes = null },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 57, Health = 3638, NpcFamily = 202, Scale = 150, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1083.232f, Y = 32.449f, Z = 1023.604f, HeadingY = 0.744359f, HeadingW = 0.667780f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 54, Health = 3365, NpcFamily = 202, Scale = 150, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1083.201f, Y = 32.414f, Z = 1023.607f, HeadingY = 0.758825f, HeadingW = 0.651294f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 55, Health = 3456, NpcFamily = 202, Scale = 150, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1076.159f, Y = 27.504f, Z = 1029.940f, HeadingY = 0.308964f, HeadingW = 0.951074f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1142.994f, Y = 1.610f, Z = 991.674f, HeadingY = 0.148810f, HeadingW = 0.988866f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1134.927f, Y = 1.610f, Z = 972.187f, HeadingY = 0.497419f, HeadingW = 0.867511f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1144.856f, Y = 1.610f, Z = 982.456f, HeadingY = -0.718673f, HeadingW = 0.695348f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1127.244f, Y = 1.610f, Z = 979.406f, HeadingY = -0.819044f, HeadingW = 0.573731f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1130.351f, Y = 1.610f, Z = 966.694f, HeadingY = -0.311968f, HeadingW = 0.950093f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1129.467f, Y = 1.610f, Z = 974.354f, HeadingY = -0.265261f, HeadingW = 0.964177f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1140.963f, Y = 1.610f, Z = 981.405f, HeadingY = -0.000221f, HeadingW = 1.000000f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1141.091f, Y = 1.610f, Z = 987.101f, HeadingY = -0.943372f, HeadingW = 0.331736f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1130.024f, Y = 1.610f, Z = 977.900f, HeadingY = -0.734432f, HeadingW = 0.678682f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1146.195f, Y = 1.829f, Z = 988.974f, HeadingY = -0.891911f, HeadingW = 0.452211f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1130.454f, Y = 1.610f, Z = 961.447f, HeadingY = 0.890782f, HeadingW = 0.454431f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 170, Health = 28693, NpcFamily = 174, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1137.720f, Y = 1.610f, Z = 978.577f, HeadingY = 0.017074f, HeadingW = 0.999854f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1089.543f, Y = 29.561f, Z = 905.927f, HeadingY = -0.837728f, HeadingW = 0.546088f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1114.944f, Y = 1.610f, Z = 907.688f, HeadingY = -0.613742f, HeadingW = 0.789506f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1105.427f, Y = 2.202f, Z = 842.851f, HeadingY = 0.094057f, HeadingW = 0.995567f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1105.416f, Y = 1.610f, Z = 833.960f, HeadingY = 0.900466f, HeadingW = 0.434925f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1093.925f, Y = 1.610f, Z = 817.787f, HeadingY = -0.260484f, HeadingW = 0.965478f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1113.951f, Y = 1.610f, Z = 833.156f, HeadingY = -0.885226f, HeadingW = 0.465162f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1139.358f, Y = 1.610f, Z = 826.242f, HeadingY = 0.481687f, HeadingW = 0.876344f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1126.460f, Y = 1.610f, Z = 828.380f, HeadingY = 0.967362f, HeadingW = 0.253397f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 175, Health = 30654, NpcFamily = 174, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1120.229f, Y = 1.610f, Z = 832.557f, HeadingY = -0.943429f, HeadingW = 0.331574f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1173.255f, Y = 30.010f, Z = 830.301f, HeadingY = -0.498129f, HeadingW = 0.867103f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 56, Health = 3547, NpcFamily = 175, Scale = 125, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 971.471f, Y = 30.680f, Z = 793.882f, HeadingY = -0.938391f, HeadingW = 0.345574f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1055.160f, Y = 1.610f, Z = 765.855f, HeadingY = 0.579864f, HeadingW = 0.814713f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1065.404f, Y = 1.610f, Z = 797.687f, HeadingY = 0.566969f, HeadingW = 0.823739f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1086.494f, Y = 1.610f, Z = 790.653f, HeadingY = -0.991856f, HeadingW = 0.127368f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1105.027f, Y = 1.610f, Z = 773.515f, HeadingY = 0.734187f, HeadingW = 0.678947f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1071.633f, Y = 1.610f, Z = 811.758f, HeadingY = 0.872344f, HeadingW = 0.488892f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 58, Health = 3729, NpcFamily = 175, Scale = 125, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1062.837f, Y = 29.210f, Z = 843.540f, HeadingY = -0.278676f, HeadingW = 0.960385f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1011.313f, Y = 29.682f, Z = 749.194f, HeadingY = 0.963771f, HeadingW = 0.266732f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1053.022f, Y = 1.610f, Z = 733.661f, HeadingY = 0.999974f, HeadingW = -0.007269f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1040.223f, Y = 1.610f, Z = 727.920f, HeadingY = 0.603339f, HeadingW = 0.797485f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1052.329f, Y = 1.610f, Z = 748.163f, HeadingY = -0.981416f, HeadingW = 0.191890f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1067.059f, Y = 1.610f, Z = 747.830f, HeadingY = -0.312365f, HeadingW = 0.949962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1107.887f, Y = 1.610f, Z = 755.741f, HeadingY = 0.839056f, HeadingW = 0.544044f, Textures = null, Meshes = null },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4543, Side = 3, MonsterData = 209173, Level = 54, Health = 2019, NpcFamily = 200, Scale = 100, RunSpeed = 242, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1129.009f, Y = 71.906f, Z = 800.066f, HeadingY = -0.329104f, HeadingW = 0.943728f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1032.032f, Y = 1.610f, Z = 719.996f, HeadingY = -0.289271f, HeadingW = 0.957247f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 55, Health = 3456, NpcFamily = 175, Scale = 125, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1051.723f, Y = 29.610f, Z = 926.524f, HeadingY = -0.914130f, HeadingW = 0.405422f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 56, Health = 3547, NpcFamily = 202, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1000.928f, Y = 28.810f, Z = 803.053f, HeadingY = -0.921543f, HeadingW = 0.388277f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1110.255f, Y = 1.610f, Z = 986.569f, HeadingY = -0.082937f, HeadingW = 0.996555f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1101.965f, Y = 2.238f, Z = 992.691f, HeadingY = 0.999648f, HeadingW = 0.026514f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1090.943f, Y = 1.610f, Z = 974.723f, HeadingY = 0.681890f, HeadingW = 0.731454f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1091.604f, Y = 1.610f, Z = 966.545f, HeadingY = 0.301910f, HeadingW = 0.953336f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1102.047f, Y = 1.610f, Z = 975.749f, HeadingY = 0.998416f, HeadingW = 0.056261f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1081.592f, Y = 1.610f, Z = 964.203f, HeadingY = -0.870305f, HeadingW = 0.492514f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1043.729f, Y = 1.610f, Z = 960.846f, HeadingY = 0.747205f, HeadingW = 0.664594f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1079.635f, Y = 1.610f, Z = 960.393f, HeadingY = -0.994714f, HeadingW = 0.102685f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1053.712f, Y = 1.610f, Z = 967.706f, HeadingY = 0.064011f, HeadingW = 0.997949f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1056.611f, Y = 2.521f, Z = 974.386f, HeadingY = -0.750019f, HeadingW = 0.661416f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1048.539f, Y = 1.610f, Z = 972.128f, HeadingY = 0.997398f, HeadingW = 0.072085f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1060.085f, Y = 2.760f, Z = 963.624f, HeadingY = -0.696350f, HeadingW = 0.717702f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 60, Health = 3911, NpcFamily = 175, Scale = 125, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.730f, Y = 28.323f, Z = 931.100f, HeadingY = -0.994579f, HeadingW = 0.103987f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 60, Health = 3911, NpcFamily = 175, Scale = 125, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1051.381f, Y = 29.610f, Z = 929.416f, HeadingY = 0.992335f, HeadingW = 0.123575f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1063.650f, Y = 3.075f, Z = 959.273f, HeadingY = 0.353178f, HeadingW = 0.935556f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 170, Health = 28693, NpcFamily = 174, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1066.387f, Y = 2.168f, Z = 958.270f, HeadingY = -0.658653f, HeadingW = 0.752447f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1060.626f, Y = 2.374f, Z = 959.757f, HeadingY = 0.863914f, HeadingW = 0.503639f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1031.021f, Y = 1.610f, Z = 958.804f, HeadingY = -0.422291f, HeadingW = 0.906460f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1031.305f, Y = 1.610f, Z = 947.998f, HeadingY = 0.233423f, HeadingW = 0.972375f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1028.781f, Y = 1.610f, Z = 946.275f, HeadingY = -0.999674f, HeadingW = 0.025526f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1017.425f, Y = 1.610f, Z = 959.047f, HeadingY = -0.297722f, HeadingW = 0.954653f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1026.392f, Y = 1.610f, Z = 955.737f, HeadingY = -0.051013f, HeadingW = 0.998698f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1021.514f, Y = 1.610f, Z = 948.700f, HeadingY = -0.224156f, HeadingW = 0.974553f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1010.230f, Y = 1.610f, Z = 971.888f, HeadingY = -0.954387f, HeadingW = 0.298573f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1019.199f, Y = 1.610f, Z = 968.408f, HeadingY = -0.918506f, HeadingW = 0.395406f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1011.773f, Y = 1.610f, Z = 975.761f, HeadingY = 0.054006f, HeadingW = 0.998541f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1010.948f, Y = 1.610f, Z = 982.795f, HeadingY = -0.162769f, HeadingW = 0.986664f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1003.810f, Y = 1.610f, Z = 974.734f, HeadingY = 0.035886f, HeadingW = 0.999356f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1006.213f, Y = 1.610f, Z = 970.660f, HeadingY = 0.744217f, HeadingW = 0.667937f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 175, Health = 30654, NpcFamily = 174, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1016.782f, Y = 1.610f, Z = 963.505f, HeadingY = -0.904781f, HeadingW = 0.425877f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sun-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208647, Level = 58, Health = 3729, NpcFamily = 202, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 960.697f, Y = 29.116f, Z = 994.959f, HeadingY = -0.916833f, HeadingW = 0.399271f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 998.740f, Y = 2.498f, Z = 976.415f, HeadingY = 0.836809f, HeadingW = 0.547494f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 993.111f, Y = 2.067f, Z = 976.200f, HeadingY = 0.991837f, HeadingW = 0.127510f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 57, Health = 3638, NpcFamily = 190, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 950.375f, Y = 30.576f, Z = 952.130f, HeadingY = -0.004501f, HeadingW = 0.999990f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 56, Health = 3547, NpcFamily = 190, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 984.992f, Y = 1.610f, Z = 849.052f, HeadingY = 0.933217f, HeadingW = 0.359315f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 970.858f, Y = 1.610f, Z = 848.124f, HeadingY = -0.989201f, HeadingW = 0.146565f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 969.194f, Y = 1.610f, Z = 850.411f, HeadingY = -0.987744f, HeadingW = 0.156082f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 56, Health = 3547, NpcFamily = 175, Scale = 125, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1027.876f, Y = 28.825f, Z = 860.276f, HeadingY = 0.844885f, HeadingW = 0.534948f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1017.748f, Y = 29.437f, Z = 866.244f, HeadingY = -0.656223f, HeadingW = 0.754567f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 172, Health = 29477, NpcFamily = 174, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 942.419f, Y = 1.610f, Z = 815.818f, HeadingY = 0.136287f, HeadingW = 0.990669f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 174, Health = 30262, NpcFamily = 174, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 929.413f, Y = 1.610f, Z = 795.717f, HeadingY = 0.994695f, HeadingW = 0.102871f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 913.885f, Y = 32.059f, Z = 796.643f, HeadingY = -0.925149f, HeadingW = 0.379605f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 906.766f, Y = 33.000f, Z = 851.658f, HeadingY = -0.939242f, HeadingW = 0.343256f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 880.576f, Y = 26.852f, Z = 765.757f, HeadingY = 0.998216f, HeadingW = 0.059705f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 883.128f, Y = 24.540f, Z = 770.986f, HeadingY = -0.654102f, HeadingW = 0.588343f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 887.058f, Y = 27.462f, Z = 772.455f, HeadingY = -0.433902f, HeadingW = 0.821260f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 891.247f, Y = 31.422f, Z = 774.013f, HeadingY = 0.221049f, HeadingW = 0.883645f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 884.692f, Y = 27.143f, Z = 775.400f, HeadingY = -0.444330f, HeadingW = 0.843103f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 889.626f, Y = 30.014f, Z = 777.776f, HeadingY = 0.095590f, HeadingW = 0.894956f, Textures = null, Meshes = null },
                new MobSlot { Name = "Deceitful Weaver", PlayfieldId = 4543, Side = 3, MonsterData = 209361, Level = 1, Health = 25, NpcFamily = 183, Scale = 36, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 893.312f, Y = 32.726f, Z = 776.070f, HeadingY = 0.703571f, HeadingW = 0.607189f, Textures = null, Meshes = null },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4543, Side = 3, MonsterData = 209173, Level = 57, Health = 2183, NpcFamily = 200, Scale = 100, RunSpeed = 257, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1006.960f, Y = 27.668f, Z = 972.178f, HeadingY = 0.942851f, HeadingW = 0.324315f, Textures = null, Meshes = null },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 55, Health = 3456, NpcFamily = 202, Scale = 150, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1075.170f, Y = 31.170f, Z = 1052.388f, HeadingY = -0.999561f, HeadingW = 0.029640f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 57, Health = 3638, NpcFamily = 202, Scale = 150, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.298f, Y = 31.210f, Z = 1054.459f, HeadingY = -0.997758f, HeadingW = 0.066930f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "One With A Graceful Neck", PlayfieldId = 4543, Side = 0, MonsterData = 22802, Level = 80, Health = 5733, NpcFamily = 200, Scale = 250, RunSpeed = 284, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 0, X = 1004.015f, Y = 26.016f, Z = 1052.880f, HeadingY = -0.971449f, HeadingW = 0.237248f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1021.785f, Y = 1.610f, Z = 1079.554f, HeadingY = -0.875546f, HeadingW = 0.483134f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 190, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1006.522f, Y = 19.745f, Z = 1120.088f, HeadingY = -0.055706f, HeadingW = 0.998447f, Textures = null, Meshes = null },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 54, Health = 3365, NpcFamily = 202, Scale = 150, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1078.506f, Y = 28.723f, Z = 1097.333f, HeadingY = -0.976304f, HeadingW = 0.216404f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Calan-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 246185, Level = 58, Health = 3729, NpcFamily = 202, Scale = 150, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1074.713f, Y = 28.993f, Z = 1096.808f, HeadingY = 0.968187f, HeadingW = 0.250229f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1077.604f, Y = 2.031f, Z = 1114.976f, HeadingY = 0.964774f, HeadingW = 0.263082f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1061.626f, Y = 1.610f, Z = 1113.000f, HeadingY = -0.921256f, HeadingW = 0.388957f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 988.973f, Y = 1.610f, Z = 1102.276f, HeadingY = -0.740310f, HeadingW = 0.672266f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1014.753f, Y = 2.120f, Z = 1084.292f, HeadingY = 0.999945f, HeadingW = 0.010501f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1021.783f, Y = 1.610f, Z = 1090.341f, HeadingY = 0.864612f, HeadingW = 0.502440f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 72, Health = 5004, NpcFamily = 211, Scale = 100, RunSpeed = 254, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 971.113f, Y = 1.919f, Z = 1121.001f, HeadingY = 0.938384f, HeadingW = 0.345594f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1011.730f, Y = 1.610f, Z = 1130.906f, HeadingY = 0.860445f, HeadingW = 0.509543f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1015.640f, Y = 1.558f, Z = 1140.523f, HeadingY = -0.992384f, HeadingW = 0.123181f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 175, Health = 30654, NpcFamily = 174, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1005.553f, Y = 1.610f, Z = 1133.190f, HeadingY = -0.871609f, HeadingW = 0.490201f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1007.657f, Y = 1.610f, Z = 1139.887f, HeadingY = -0.816566f, HeadingW = 0.577252f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1076.913f, Y = 1.610f, Z = 1120.575f, HeadingY = -0.987740f, HeadingW = 0.156107f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1082.887f, Y = 1.610f, Z = 1120.997f, HeadingY = 0.933278f, HeadingW = 0.359154f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 70, Health = 4822, NpcFamily = 189, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1041.436f, Y = 20.410f, Z = 1240.015f, HeadingY = 0.241439f, HeadingW = 0.970416f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 190, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1054.412f, Y = 19.789f, Z = 1224.749f, HeadingY = -0.898995f, HeadingW = 0.437960f, Textures = null, Meshes = null },
                new MobSlot { Name = "Flagging Arcorash", PlayfieldId = 4543, Side = 3, MonsterData = 208904, Level = 73, Health = 5095, NpcFamily = 173, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1002.870f, Y = 18.241f, Z = 1238.545f, HeadingY = -0.993978f, HeadingW = 0.109581f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1073.357f, Y = 1.532f, Z = 1215.427f, HeadingY = -0.582951f, HeadingW = 0.812507f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1104.083f, Y = 1.610f, Z = 1236.086f, HeadingY = 0.762282f, HeadingW = 0.647245f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1098.803f, Y = 1.610f, Z = 1236.293f, HeadingY = 0.758619f, HeadingW = 0.651534f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1110.096f, Y = 1.610f, Z = 1239.107f, HeadingY = 0.730936f, HeadingW = 0.682446f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1082.722f, Y = 1.482f, Z = 1223.643f, HeadingY = 0.998535f, HeadingW = 0.054109f, Textures = null, Meshes = null },
                new MobSlot { Name = "Flagging Arcorash", PlayfieldId = 4543, Side = 3, MonsterData = 208904, Level = 71, Health = 4913, NpcFamily = 173, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1001.507f, Y = 36.255f, Z = 1262.312f, HeadingY = 0.505276f, HeadingW = 0.862958f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 190, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1066.339f, Y = 19.448f, Z = 1249.362f, HeadingY = -0.986056f, HeadingW = 0.166413f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 72, Health = 5004, NpcFamily = 190, Scale = 100, RunSpeed = 254, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1087.423f, Y = 19.349f, Z = 1269.927f, HeadingY = 0.066070f, HeadingW = 0.997815f, Textures = null, Meshes = null },
                new MobSlot { Name = "Flagging Arcorash", PlayfieldId = 4543, Side = 3, MonsterData = 208904, Level = 71, Health = 4913, NpcFamily = 173, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1008.219f, Y = 18.612f, Z = 1242.184f, HeadingY = 0.936677f, HeadingW = 0.350193f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1110.258f, Y = 1.610f, Z = 1279.619f, HeadingY = 0.786032f, HeadingW = 0.618186f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1109.269f, Y = 1.610f, Z = 1245.704f, HeadingY = 0.782657f, HeadingW = 0.622453f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 190, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1055.752f, Y = 16.327f, Z = 1281.130f, HeadingY = -0.951750f, HeadingW = 0.306874f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1109.516f, Y = 1.610f, Z = 1347.546f, HeadingY = 0.759522f, HeadingW = 0.650482f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1118.376f, Y = 1.610f, Z = 1346.128f, HeadingY = 0.715245f, HeadingW = 0.698873f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1111.222f, Y = 1.610f, Z = 1339.963f, HeadingY = -0.046460f, HeadingW = 0.998920f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 71, Health = 4913, NpcFamily = 190, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1045.444f, Y = 22.093f, Z = 1372.199f, HeadingY = -0.126710f, HeadingW = 0.991940f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1051.300f, Y = 2.069f, Z = 1390.497f, HeadingY = 0.720361f, HeadingW = 0.693599f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1048.918f, Y = 1.610f, Z = 1395.802f, HeadingY = -0.872913f, HeadingW = 0.487875f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1094.113f, Y = 1.379f, Z = 1397.694f, HeadingY = 0.732188f, HeadingW = 0.681102f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1105.063f, Y = 1.610f, Z = 1387.209f, HeadingY = 0.258672f, HeadingW = 0.965965f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1103.505f, Y = 1.610f, Z = 1399.111f, HeadingY = -0.996938f, HeadingW = 0.078189f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1071.844f, Y = 1.610f, Z = 1428.302f, HeadingY = 0.790132f, HeadingW = 0.612936f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1069.467f, Y = 1.357f, Z = 1420.918f, HeadingY = 0.723448f, HeadingW = 0.690379f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1050.020f, Y = 1.610f, Z = 1435.443f, HeadingY = 0.788357f, HeadingW = 0.615218f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1052.774f, Y = 1.610f, Z = 1428.796f, HeadingY = 0.983358f, HeadingW = 0.181677f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1058.548f, Y = 1.610f, Z = 1432.143f, HeadingY = -0.136143f, HeadingW = 0.990689f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1077.151f, Y = 1.610f, Z = 1437.879f, HeadingY = -0.580624f, HeadingW = 0.814172f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1089.558f, Y = 1.610f, Z = 1409.249f, HeadingY = 0.788923f, HeadingW = 0.614491f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1092.418f, Y = 1.610f, Z = 1423.101f, HeadingY = 0.713911f, HeadingW = 0.700236f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1083.885f, Y = 1.610f, Z = 1423.281f, HeadingY = 0.735857f, HeadingW = 0.677137f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1080.527f, Y = 1.431f, Z = 1413.686f, HeadingY = -0.724378f, HeadingW = 0.689403f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1029.188f, Y = 3.442f, Z = 1396.547f, HeadingY = -0.725191f, HeadingW = 0.688548f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1034.321f, Y = 3.528f, Z = 1393.795f, HeadingY = -0.183154f, HeadingW = 0.983084f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1028.903f, Y = 3.123f, Z = 1401.925f, HeadingY = 0.771052f, HeadingW = 0.636772f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1016.818f, Y = 2.254f, Z = 1433.210f, HeadingY = 0.721887f, HeadingW = 0.692011f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1039.202f, Y = 1.303f, Z = 1436.132f, HeadingY = 0.238068f, HeadingW = 0.971248f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1021.614f, Y = 3.051f, Z = 1412.237f, HeadingY = 0.994580f, HeadingW = 0.103976f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1009.087f, Y = 2.123f, Z = 1408.645f, HeadingY = 1.000000f, HeadingW = -0.000395f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1038.295f, Y = 1.610f, Z = 1446.593f, HeadingY = 0.764325f, HeadingW = 0.644831f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1012.606f, Y = 1.610f, Z = 1441.025f, HeadingY = 0.953584f, HeadingW = 0.301128f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1023.597f, Y = 1.610f, Z = 1442.605f, HeadingY = -0.989040f, HeadingW = 0.147648f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1024.571f, Y = 1.610f, Z = 1449.365f, HeadingY = 0.999975f, HeadingW = 0.007045f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 73, Health = 5095, NpcFamily = 189, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1010.614f, Y = 18.998f, Z = 1326.079f, HeadingY = 0.997856f, HeadingW = 0.065456f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 79, Health = 5642, NpcFamily = 190, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 927.201f, Y = 2.478f, Z = 1455.000f, HeadingY = 0.872714f, HeadingW = 0.488232f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 75, Health = 5277, NpcFamily = 189, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 897.420f, Y = 23.845f, Z = 1593.023f, HeadingY = 0.702232f, HeadingW = 0.711949f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 76, Health = 5368, NpcFamily = 189, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 896.934f, Y = 17.905f, Z = 1598.684f, HeadingY = -0.418644f, HeadingW = 0.908151f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 160, Health = 24770, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 947.992f, Y = 1.610f, Z = 1593.309f, HeadingY = 0.968491f, HeadingW = 0.249048f, Textures = null, Meshes = null },
                new MobSlot { Name = "Flagging Arcorash", PlayfieldId = 4543, Side = 3, MonsterData = 208904, Level = 80, Health = 5733, NpcFamily = 173, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 906.455f, Y = 15.251f, Z = 1570.762f, HeadingY = -0.993768f, HeadingW = 0.111469f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 75, Health = 5277, NpcFamily = 189, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 895.200f, Y = 17.905f, Z = 1598.700f, HeadingY = -0.364993f, HeadingW = 0.931010f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 76, Health = 5368, NpcFamily = 189, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 898.600f, Y = 17.905f, Z = 1598.600f, HeadingY = 0.331771f, HeadingW = 0.943360f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 80, Health = 5733, NpcFamily = 189, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 900.288f, Y = 11.152f, Z = 1613.536f, HeadingY = 0.990645f, HeadingW = 0.136468f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 169, Health = 28300, NpcFamily = 174, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 945.277f, Y = 1.610f, Z = 1638.334f, HeadingY = -0.216751f, HeadingW = 0.976227f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 76, Health = 5368, NpcFamily = 189, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 848.298f, Y = 26.895f, Z = 1548.498f, HeadingY = -0.923886f, HeadingW = 0.382669f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cagey Hoathlan", PlayfieldId = 4543, Side = 3, MonsterData = 209203, Level = 75, Health = 5277, NpcFamily = 189, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 846.780f, Y = 21.205f, Z = 1544.654f, HeadingY = 0.945420f, HeadingW = 0.325855f, Textures = null, Meshes = null },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 81, Health = 5824, NpcFamily = 202, Scale = 150, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 849.875f, Y = 15.052f, Z = 1663.953f, HeadingY = -0.901487f, HeadingW = 0.432806f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 80, Health = 5733, NpcFamily = 202, Scale = 150, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 858.201f, Y = 14.861f, Z = 1673.310f, HeadingY = -0.918950f, HeadingW = 0.394373f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 84, Health = 6097, NpcFamily = 202, Scale = 150, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 858.446f, Y = 14.676f, Z = 1676.967f, HeadingY = 0.997927f, HeadingW = 0.064349f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 82, Health = 5915, NpcFamily = 202, Scale = 150, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 860.924f, Y = 14.501f, Z = 1672.875f, HeadingY = -0.818981f, HeadingW = 0.573821f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 84, Health = 6097, NpcFamily = 202, Scale = 250, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 863.400f, Y = 30.575f, Z = 1679.400f, HeadingY = -0.730587f, HeadingW = 0.682820f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 80, Health = 5733, NpcFamily = 202, Scale = 250, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 865.000f, Y = 30.575f, Z = 1677.799f, HeadingY = -0.991745f, HeadingW = 0.128225f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 80, Health = 5733, NpcFamily = 202, Scale = 250, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 868.900f, Y = 14.560f, Z = 1683.100f, HeadingY = 0.398449f, HeadingW = 0.917191f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 84, Health = 6097, NpcFamily = 202, Scale = 250, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 861.001f, Y = 20.079f, Z = 1688.400f, HeadingY = -0.936870f, HeadingW = 0.349677f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 83, Health = 6006, NpcFamily = 202, Scale = 150, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 805.320f, Y = 14.574f, Z = 1677.246f, HeadingY = -0.756184f, HeadingW = 0.654359f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 954.968f, Y = 1.610f, Z = 1754.736f, HeadingY = 0.923616f, HeadingW = 0.383318f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 817.056f, Y = 1.610f, Z = 1789.152f, HeadingY = 0.880762f, HeadingW = 0.473558f, Textures = null, Meshes = null },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 81, Health = 5824, NpcFamily = 202, Scale = 150, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 843.167f, Y = 19.296f, Z = 1768.956f, HeadingY = 0.998032f, HeadingW = 0.062710f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Dachu-Cur", PlayfieldId = 4543, Side = 2, MonsterData = 208636, Level = 82, Health = 5915, NpcFamily = 202, Scale = 150, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 846.678f, Y = 17.745f, Z = 1763.346f, HeadingY = -0.556395f, HeadingW = 0.830918f, Textures = null, Meshes = new[] { new[] { 1, 233207, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 80, Health = 5733, NpcFamily = 202, Scale = 250, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 846.400f, Y = 33.475f, Z = 1771.200f, HeadingY = -0.731707f, HeadingW = 0.681619f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 82, Health = 5915, NpcFamily = 202, Scale = 250, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 848.201f, Y = 33.475f, Z = 1769.199f, HeadingY = -0.994505f, HeadingW = 0.104692f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Coloss-Or", PlayfieldId = 4543, Side = 2, MonsterData = 208641, Level = 83, Health = 6006, NpcFamily = 202, Scale = 250, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 847.400f, Y = 33.475f, Z = 1769.900f, HeadingY = -0.928644f, HeadingW = 0.370971f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 829.282f, Y = 16.528f, Z = 1742.403f, HeadingY = 0.362370f, HeadingW = 0.932034f, Textures = null, Meshes = null },
                new MobSlot { Name = "Rippled Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 84, Health = 6097, NpcFamily = 186, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 835.738f, Y = 1.610f, Z = 1833.010f, HeadingY = -0.908361f, HeadingW = 0.418186f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 831.261f, Y = 1.610f, Z = 1829.752f, HeadingY = -0.355046f, HeadingW = 0.934849f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 807.981f, Y = 1.610f, Z = 1801.001f, HeadingY = 0.834185f, HeadingW = 0.551485f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 808.219f, Y = 2.774f, Z = 1839.990f, HeadingY = -0.758189f, HeadingW = 0.652035f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 854.662f, Y = 1.610f, Z = 1827.792f, HeadingY = 0.918632f, HeadingW = 0.395115f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 77, Health = 5460, NpcFamily = 190, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 818.437f, Y = 18.128f, Z = 1877.438f, HeadingY = 0.053779f, HeadingW = 0.998553f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana-Behn", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 75, Health = 5277, NpcFamily = 190, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 821.500f, Y = 18.544f, Z = 1875.899f, HeadingY = -0.446215f, HeadingW = 0.894926f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 898.724f, Y = 1.610f, Z = 1868.402f, HeadingY = 0.679808f, HeadingW = 0.733390f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 785.978f, Y = 2.020f, Z = 1820.821f, HeadingY = 0.863713f, HeadingW = 0.503984f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Elements", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 781.970f, Y = 2.164f, Z = 1757.258f, HeadingY = -0.962731f, HeadingW = 0.270460f, Textures = null, Meshes = null },
                new MobSlot { Name = "Rippled Eremite", PlayfieldId = 4543, Side = 3, MonsterData = 209158, Level = 81, Health = 5824, NpcFamily = 186, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 756.323f, Y = 2.380f, Z = 1756.207f, HeadingY = 0.991737f, HeadingW = 0.128285f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 80, Health = 5733, NpcFamily = 172, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 688.700f, Y = 21.210f, Z = 1811.479f, HeadingY = 0.189028f, HeadingW = 0.981972f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 79, Health = 5642, NpcFamily = 201, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 676.290f, Y = 22.159f, Z = 1867.155f, HeadingY = -0.334906f, HeadingW = 0.942251f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 83, Health = 6006, NpcFamily = 172, Scale = 100, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 750.178f, Y = 16.595f, Z = 1855.708f, HeadingY = 0.438729f, HeadingW = 0.897238f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 657.699f, Y = 26.944f, Z = 1797.148f, HeadingY = 0.336066f, HeadingW = 0.941839f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 617.468f, Y = 21.438f, Z = 1917.454f, HeadingY = -0.883248f, HeadingW = 0.468905f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 618.778f, Y = 34.010f, Z = 1732.430f, HeadingY = 0.925040f, HeadingW = 0.379870f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 616.215f, Y = 34.010f, Z = 1739.148f, HeadingY = 0.931693f, HeadingW = 0.363247f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 620.177f, Y = 34.010f, Z = 1744.389f, HeadingY = 0.926345f, HeadingW = 0.376676f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 585.237f, Y = 51.210f, Z = 1770.198f, HeadingY = -0.898902f, HeadingW = 0.438150f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 582.930f, Y = 51.848f, Z = 1806.532f, HeadingY = 0.999757f, HeadingW = 0.022025f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 562.360f, Y = 51.210f, Z = 1818.958f, HeadingY = -0.440874f, HeadingW = 0.897569f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Sipius Enel Lux-Mara", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 70, Health = 4822, NpcFamily = 201, Scale = 140, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 537.882f, Y = 51.360f, Z = 1757.075f, HeadingY = -0.387245f, HeadingW = 0.921977f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Devoted Enel Ilad-Ulma", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 70, Health = 4822, NpcFamily = 201, Scale = 140, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 529.508f, Y = 51.345f, Z = 1758.981f, HeadingY = 0.343726f, HeadingW = 0.939070f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 525.540f, Y = 51.210f, Z = 1743.029f, HeadingY = -0.063938f, HeadingW = 0.997954f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Watcher Enel Ulma-Thar", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 70, Health = 4822, NpcFamily = 201, Scale = 140, RunSpeed = 246, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 0, X = 544.943f, Y = 51.345f, Z = 1775.518f, HeadingY = -0.906339f, HeadingW = 0.422552f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 550.249f, Y = 51.345f, Z = 1780.627f, HeadingY = -0.998761f, HeadingW = 0.049761f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 548.170f, Y = 51.345f, Z = 1776.099f, HeadingY = -0.997424f, HeadingW = 0.071738f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 546.438f, Y = 51.345f, Z = 1779.438f, HeadingY = -0.999258f, HeadingW = 0.038507f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 532.579f, Y = 51.885f, Z = 1810.723f, HeadingY = -0.781752f, HeadingW = 0.623589f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 83, Health = 6006, NpcFamily = 201, Scale = 100, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 551.247f, Y = 51.210f, Z = 1828.084f, HeadingY = 0.476846f, HeadingW = 0.878987f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Or-Mada of Flaming Barrels", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 63, Health = 83680, NpcFamily = 201, Scale = 100, RunSpeed = 220, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 521.274f, Y = 52.285f, Z = 1808.349f, HeadingY = 0.823144f, HeadingW = 0.567833f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Mada of Preservation", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 63, Health = 83680, NpcFamily = 201, Scale = 100, RunSpeed = 220, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 532.778f, Y = 51.886f, Z = 1810.734f, HeadingY = -0.214960f, HeadingW = 0.976623f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 530.212f, Y = 51.778f, Z = 1817.189f, HeadingY = -0.945223f, HeadingW = 0.326425f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Mada of the Furious Fists", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 62, Health = 81858, NpcFamily = 201, Scale = 100, RunSpeed = 216, CharacterFlags = 271061505, VisualFlags = 31, HeadMesh = 0, X = 528.085f, Y = 53.580f, Z = 1812.150f, HeadingY = -0.964133f, HeadingW = -0.265420f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 550.984f, Y = 51.210f, Z = 1830.798f, HeadingY = 0.679758f, HeadingW = 0.733437f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 513.599f, Y = 51.210f, Z = 1780.146f, HeadingY = 0.079563f, HeadingW = 0.996830f, Textures = null, Meshes = null },
                new MobSlot { Name = "Mire Rafter", PlayfieldId = 4543, Side = 3, MonsterData = 212186, Level = 88, Health = 6461, NpcFamily = 175, Scale = 125, RunSpeed = 314, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 488.587f, Y = 66.645f, Z = 1769.846f, HeadingY = -0.992020f, HeadingW = 0.126078f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 520.006f, Y = 51.612f, Z = 1803.712f, HeadingY = 0.489150f, HeadingW = 0.872200f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 492.448f, Y = 51.210f, Z = 1782.791f, HeadingY = 0.141321f, HeadingW = 0.989964f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 501.543f, Y = 51.210f, Z = 1774.429f, HeadingY = 0.571515f, HeadingW = 0.820591f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 480.813f, Y = 55.705f, Z = 1787.283f, HeadingY = 0.149875f, HeadingW = 0.988705f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 502.649f, Y = 51.210f, Z = 1762.715f, HeadingY = -0.702794f, HeadingW = 0.711393f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 480.203f, Y = 84.299f, Z = 1756.963f, HeadingY = 0.991490f, HeadingW = 0.130186f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 490.590f, Y = 32.576f, Z = 1859.220f, HeadingY = -0.400762f, HeadingW = 0.916182f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 501.186f, Y = 32.568f, Z = 1868.834f, HeadingY = -0.843828f, HeadingW = 0.536614f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 536.005f, Y = 32.813f, Z = 1876.061f, HeadingY = 0.259570f, HeadingW = 0.965724f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Or-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 496.847f, Y = 51.446f, Z = 1841.010f, HeadingY = -0.676928f, HeadingW = 0.736050f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 443.180f, Y = 51.813f, Z = 1818.520f, HeadingY = 0.997410f, HeadingW = 0.071920f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 473.599f, Y = 69.115f, Z = 1782.662f, HeadingY = 0.018982f, HeadingW = 0.999820f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214067, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 454.899f, Y = 40.603f, Z = 1824.476f, HeadingY = -0.273554f, HeadingW = 0.961857f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Cur-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214078, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 469.786f, Y = 39.319f, Z = 1834.942f, HeadingY = 0.450551f, HeadingW = 0.892751f, Textures = null, Meshes = new[] { new[] { 1, 234636, 0, 2 } } },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 80, Health = 5733, NpcFamily = 172, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 455.105f, Y = 29.944f, Z = 1875.425f, HeadingY = 0.200614f, HeadingW = 0.960211f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4543, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 473.992f, Y = 85.794f, Z = 1731.780f, HeadingY = 0.977530f, HeadingW = 0.210796f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 82, Health = 5915, NpcFamily = 172, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 560.160f, Y = 19.718f, Z = 1966.944f, HeadingY = 0.853877f, HeadingW = 0.518084f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 80, Health = 5733, NpcFamily = 172, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 599.944f, Y = 20.081f, Z = 1935.402f, HeadingY = -0.958893f, HeadingW = 0.274949f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 625.842f, Y = 20.586f, Z = 1914.628f, HeadingY = 0.713268f, HeadingW = 0.700892f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4543, Side = 1, MonsterData = 214083, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 631.415f, Y = 9.983f, Z = 1936.050f, HeadingY = 0.963069f, HeadingW = 0.269254f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 624.256f, Y = 4.829f, Z = 1988.720f, HeadingY = 0.725054f, HeadingW = 0.688692f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lendar", PlayfieldId = 4543, Side = 1, MonsterData = 214072, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 622.124f, Y = 3.892f, Z = 1981.121f, HeadingY = -0.913451f, HeadingW = 0.406949f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 83, Health = 6006, NpcFamily = 172, Scale = 100, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 599.266f, Y = 17.091f, Z = 2051.500f, HeadingY = -0.340211f, HeadingW = 0.937726f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 82, Health = 5915, NpcFamily = 172, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 667.309f, Y = 12.298f, Z = 2000.234f, HeadingY = 0.206725f, HeadingW = 0.959949f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 81, Health = 5824, NpcFamily = 172, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.433f, Y = 12.577f, Z = 2031.471f, HeadingY = -0.556497f, HeadingW = 0.818061f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lost Hiathlin", PlayfieldId = 4543, Side = 3, MonsterData = 209196, Level = 80, Health = 5733, NpcFamily = 189, Scale = 60, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 561.612f, Y = 19.210f, Z = 2110.491f, HeadingY = 0.507625f, HeadingW = 0.861578f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 177, Health = 31439, NpcFamily = 174, Scale = 100, RunSpeed = 447, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 573.030f, Y = 1.610f, Z = 2147.977f, HeadingY = 0.922793f, HeadingW = 0.385296f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 171, Health = 29085, NpcFamily = 174, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 659.604f, Y = 10.581f, Z = 2200.831f, HeadingY = 0.438958f, HeadingW = 0.898508f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 723.165f, Y = 13.616f, Z = 2200.002f, HeadingY = 0.327990f, HeadingW = 0.944681f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 714.144f, Y = 15.579f, Z = 2207.664f, HeadingY = -0.962308f, HeadingW = 0.271962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 694.013f, Y = 13.826f, Z = 2209.333f, HeadingY = -0.815210f, HeadingW = 0.579166f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 102, Health = 8551, NpcFamily = 207, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 719.671f, Y = 15.536f, Z = 2206.272f, HeadingY = 0.999980f, HeadingW = -0.006322f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 696.651f, Y = 13.494f, Z = 2248.416f, HeadingY = 0.988505f, HeadingW = 0.151189f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 108, Health = 9504, NpcFamily = 207, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 745.466f, Y = 11.265f, Z = 2186.245f, HeadingY = -0.143744f, HeadingW = 0.989615f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 753.341f, Y = 10.732f, Z = 2189.815f, HeadingY = -0.355164f, HeadingW = 0.934804f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 721.723f, Y = 14.315f, Z = 2202.887f, HeadingY = -0.919581f, HeadingW = 0.392900f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 744.219f, Y = 16.810f, Z = 2233.421f, HeadingY = -0.877001f, HeadingW = 0.480489f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 730.571f, Y = 12.132f, Z = 2258.812f, HeadingY = -0.075416f, HeadingW = 0.997152f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 799.034f, Y = 12.604f, Z = 2157.910f, HeadingY = 0.989361f, HeadingW = 0.145479f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 796.548f, Y = 8.881f, Z = 2262.226f, HeadingY = -0.601884f, HeadingW = 0.798584f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 767.874f, Y = 14.303f, Z = 2251.204f, HeadingY = -0.935268f, HeadingW = 0.353942f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 799.700f, Y = 10.344f, Z = 2143.600f, HeadingY = -0.144208f, HeadingW = 0.989547f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 108, Health = 9504, NpcFamily = 207, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 727.921f, Y = 14.006f, Z = 2277.428f, HeadingY = -0.989659f, HeadingW = 0.143443f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 742.435f, Y = 14.501f, Z = 2284.800f, HeadingY = 0.801767f, HeadingW = 0.597637f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 746.391f, Y = 14.401f, Z = 2313.760f, HeadingY = -0.976426f, HeadingW = 0.215853f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 790.220f, Y = 19.267f, Z = 2292.431f, HeadingY = 0.763727f, HeadingW = 0.645539f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 750.349f, Y = 16.922f, Z = 2333.266f, HeadingY = -0.687108f, HeadingW = 0.726555f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4543, Side = 3, MonsterData = 214973, Level = 165, Health = 26731, NpcFamily = 174, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 661.103f, Y = 1.610f, Z = 2367.333f, HeadingY = 0.914688f, HeadingW = 0.404161f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 697.816f, Y = 1.610f, Z = 2383.055f, HeadingY = -0.165419f, HeadingW = 0.986223f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 757.225f, Y = 21.404f, Z = 2381.504f, HeadingY = -0.559087f, HeadingW = 0.829109f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 108, Health = 9504, NpcFamily = 207, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 759.511f, Y = 23.425f, Z = 2392.996f, HeadingY = -0.716611f, HeadingW = 0.697473f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 751.038f, Y = 17.118f, Z = 2360.272f, HeadingY = -0.690103f, HeadingW = 0.723711f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4543, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 741.910f, Y = 16.810f, Z = 2364.989f, HeadingY = 0.584088f, HeadingW = 0.811690f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 567.034f, Y = 1.610f, Z = 2296.733f, HeadingY = -0.900692f, HeadingW = 0.434458f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 170, Health = 28693, NpcFamily = 171, Scale = 100, RunSpeed = 443, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 599.720f, Y = 1.610f, Z = 2351.876f, HeadingY = -0.268575f, HeadingW = 0.963259f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 505.432f, Y = 1.610f, Z = 2277.750f, HeadingY = -0.915758f, HeadingW = 0.401730f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 498.534f, Y = 2.950f, Z = 2322.536f, HeadingY = -0.978284f, HeadingW = 0.207270f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 160, Health = 24770, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 471.502f, Y = 3.618f, Z = 2260.423f, HeadingY = -0.698878f, HeadingW = 0.715240f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 539.850f, Y = 2.867f, Z = 2363.327f, HeadingY = 0.418433f, HeadingW = 0.908248f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 164, Health = 26339, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 434.374f, Y = 3.649f, Z = 2265.305f, HeadingY = -0.698650f, HeadingW = 0.715463f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lodoth-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208648, Level = 84, Health = 6097, NpcFamily = 202, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 428.650f, Y = 50.748f, Z = 2193.294f, HeadingY = -0.991981f, HeadingW = 0.126386f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 161, Health = 25162, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 474.580f, Y = 7.740f, Z = 2233.366f, HeadingY = -0.352149f, HeadingW = 0.935944f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lodoth-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208648, Level = 80, Health = 5733, NpcFamily = 202, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 429.806f, Y = 50.810f, Z = 2199.979f, HeadingY = -0.834107f, HeadingW = 0.551603f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 161, Health = 25162, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 409.020f, Y = 2.428f, Z = 2220.468f, HeadingY = -0.703849f, HeadingW = 0.710349f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 163, Health = 25947, NpcFamily = 171, Scale = 100, RunSpeed = 439, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 411.711f, Y = 2.451f, Z = 2223.559f, HeadingY = -0.702406f, HeadingW = 0.711777f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 163, Health = 25947, NpcFamily = 171, Scale = 100, RunSpeed = 439, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 425.394f, Y = 3.169f, Z = 2235.668f, HeadingY = -0.700350f, HeadingW = 0.713800f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lodoth-Len", PlayfieldId = 4543, Side = 2, MonsterData = 208648, Level = 85, Health = 6188, NpcFamily = 202, Scale = 100, RunSpeed = 303, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 416.472f, Y = 50.810f, Z = 2184.499f, HeadingY = -0.862855f, HeadingW = 0.505452f, Textures = null, Meshes = new[] { new[] { 1, 247035, 0, 2 } } },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 401.057f, Y = 35.732f, Z = 2166.757f, HeadingY = -0.720011f, HeadingW = 0.693963f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 164, Health = 26339, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 391.938f, Y = 3.950f, Z = 2174.729f, HeadingY = 0.781348f, HeadingW = 0.624095f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 160, Health = 24770, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 381.467f, Y = 2.301f, Z = 2129.442f, HeadingY = -0.993763f, HeadingW = 0.111512f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 162, Health = 25554, NpcFamily = 171, Scale = 100, RunSpeed = 439, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 406.685f, Y = 3.448f, Z = 2136.589f, HeadingY = 0.649538f, HeadingW = 0.760329f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 160, Health = 24770, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 391.722f, Y = 3.569f, Z = 2148.157f, HeadingY = 0.653436f, HeadingW = 0.756981f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 164, Health = 26339, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 375.697f, Y = 1.962f, Z = 2090.253f, HeadingY = -0.482820f, HeadingW = 0.875720f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tranquil Silvertail", PlayfieldId = 4543, Side = 3, MonsterData = 208929, Level = 82, Health = 5915, NpcFamily = 172, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 421.505f, Y = 45.397f, Z = 2105.686f, HeadingY = -0.154536f, HeadingW = 0.940412f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 160, Health = 24770, NpcFamily = 171, Scale = 100, RunSpeed = 438, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 377.497f, Y = 2.064f, Z = 2096.419f, HeadingY = 0.049197f, HeadingW = 0.998789f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 162, Health = 25554, NpcFamily = 171, Scale = 100, RunSpeed = 439, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 381.315f, Y = 3.194f, Z = 2100.302f, HeadingY = 0.834811f, HeadingW = 0.550537f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 110, Health = 9822, NpcFamily = 193, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 440.647f, Y = 26.819f, Z = 2091.316f, HeadingY = -0.686895f, HeadingW = 0.726756f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 106, Health = 9186, NpcFamily = 193, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 450.345f, Y = 32.810f, Z = 2083.269f, HeadingY = 0.201620f, HeadingW = 0.979464f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 473.891f, Y = 29.852f, Z = 2111.097f, HeadingY = -0.999113f, HeadingW = 0.042121f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 104, Health = 8868, NpcFamily = 193, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 466.194f, Y = 40.489f, Z = 2101.429f, HeadingY = -0.690010f, HeadingW = 0.723800f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 108, Health = 9504, NpcFamily = 193, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 472.243f, Y = 29.445f, Z = 2113.347f, HeadingY = -0.671911f, HeadingW = 0.740632f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 101, Health = 8392, NpcFamily = 193, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 470.200f, Y = 29.206f, Z = 2112.188f, HeadingY = -0.998122f, HeadingW = 0.061257f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 103, Health = 8710, NpcFamily = 193, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 458.554f, Y = 41.068f, Z = 2102.677f, HeadingY = -0.843474f, HeadingW = 0.537170f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 473.431f, Y = 40.274f, Z = 2101.384f, HeadingY = -0.683527f, HeadingW = 0.729925f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 101, Health = 8392, NpcFamily = 193, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 469.201f, Y = 22.415f, Z = 2086.000f, HeadingY = 0.717033f, HeadingW = 0.697039f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 107, Health = 9345, NpcFamily = 193, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 468.101f, Y = 20.301f, Z = 2080.401f, HeadingY = 0.357850f, HeadingW = 0.933779f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 108, Health = 9504, NpcFamily = 193, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 470.498f, Y = 18.889f, Z = 2084.103f, HeadingY = 0.704533f, HeadingW = 0.709671f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 100, Health = 8233, NpcFamily = 193, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 478.897f, Y = 21.409f, Z = 2085.495f, HeadingY = 0.919326f, HeadingW = 0.393496f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 101, Health = 8392, NpcFamily = 193, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 465.798f, Y = 19.850f, Z = 2082.301f, HeadingY = 0.983483f, HeadingW = 0.180999f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 105, Health = 9027, NpcFamily = 193, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 466.303f, Y = 36.645f, Z = 2124.304f, HeadingY = 0.255287f, HeadingW = 0.966865f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4543, Side = 3, MonsterData = 209333, Level = 107, Health = 9345, NpcFamily = 193, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 479.885f, Y = 36.779f, Z = 2123.854f, HeadingY = -0.845779f, HeadingW = 0.533534f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1183.558f, Y = 2.010f, Z = 773.941f, HeadingY = -0.975029f, HeadingW = 0.222076f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1184.595f, Y = 1.953f, Z = 775.253f, HeadingY = -0.798634f, HeadingW = 0.601816f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1187.200f, Y = 1.690f, Z = 777.013f, HeadingY = 0.923032f, HeadingW = 0.384722f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1215.314f, Y = 1.610f, Z = 755.479f, HeadingY = 0.741977f, HeadingW = 0.670426f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1150.478f, Y = 1.858f, Z = 777.167f, HeadingY = 0.937432f, HeadingW = 0.348168f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1209.369f, Y = 1.610f, Z = 788.627f, HeadingY = 0.580778f, HeadingW = 0.814062f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1217.507f, Y = 1.610f, Z = 760.366f, HeadingY = -0.094231f, HeadingW = 0.995550f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1213.307f, Y = 1.610f, Z = 761.021f, HeadingY = -0.711822f, HeadingW = 0.702360f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1233.037f, Y = 1.610f, Z = 795.067f, HeadingY = -0.963742f, HeadingW = 0.266834f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 165, Health = 26731, NpcFamily = 171, Scale = 100, RunSpeed = 440, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1223.864f, Y = 1.610f, Z = 812.107f, HeadingY = -0.566213f, HeadingW = 0.824259f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1240.036f, Y = 1.610f, Z = 827.894f, HeadingY = -0.517949f, HeadingW = 0.855411f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1244.878f, Y = 1.610f, Z = 806.948f, HeadingY = 0.782828f, HeadingW = 0.622238f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1276.786f, Y = 1.610f, Z = 808.564f, HeadingY = 0.973953f, HeadingW = 0.226750f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1270.366f, Y = 1.610f, Z = 827.863f, HeadingY = 0.802715f, HeadingW = 0.596363f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1255.083f, Y = 1.610f, Z = 853.656f, HeadingY = 0.789841f, HeadingW = 0.613312f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1164.831f, Y = 1.610f, Z = 680.621f, HeadingY = -0.311865f, HeadingW = 0.950126f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1137.118f, Y = 1.610f, Z = 659.859f, HeadingY = 0.531784f, HeadingW = 0.846880f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1160.901f, Y = 1.610f, Z = 677.033f, HeadingY = 0.894521f, HeadingW = 0.447027f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1172.010f, Y = 1.610f, Z = 640.230f, HeadingY = -0.913846f, HeadingW = 0.406061f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1164.253f, Y = 1.610f, Z = 675.217f, HeadingY = -0.300559f, HeadingW = 0.953763f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1171.441f, Y = 1.610f, Z = 642.887f, HeadingY = -0.074547f, HeadingW = 0.997217f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1168.955f, Y = 1.610f, Z = 640.676f, HeadingY = -0.043171f, HeadingW = 0.999068f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1139.017f, Y = 1.610f, Z = 629.504f, HeadingY = -0.880357f, HeadingW = 0.474311f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1151.936f, Y = 1.610f, Z = 615.921f, HeadingY = -0.125553f, HeadingW = 0.992087f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1099.286f, Y = 1.610f, Z = 637.200f, HeadingY = 0.161685f, HeadingW = 0.986842f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1115.468f, Y = 1.672f, Z = 627.956f, HeadingY = 0.323619f, HeadingW = 0.946188f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1092.526f, Y = 1.610f, Z = 640.350f, HeadingY = -0.994948f, HeadingW = 0.100390f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1108.245f, Y = 1.610f, Z = 364.174f, HeadingY = -0.938595f, HeadingW = 0.345022f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1108.558f, Y = 1.610f, Z = 395.854f, HeadingY = -0.565303f, HeadingW = 0.824883f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 166, Health = 27124, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1100.454f, Y = 1.685f, Z = 325.208f, HeadingY = -0.508450f, HeadingW = 0.861091f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 171, Health = 29085, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1118.257f, Y = 1.610f, Z = 330.850f, HeadingY = 0.323641f, HeadingW = 0.946180f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4543, Side = 3, MonsterData = 209215, Level = 58, Health = 3729, NpcFamily = 190, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 996.614f, Y = 51.065f, Z = 251.488f, HeadingY = -0.804538f, HeadingW = 0.593901f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Earth", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 174, Health = 30262, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 976.292f, Y = 32.208f, Z = 244.393f, HeadingY = 0.685932f, HeadingW = 0.727666f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Metals", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 175, Health = 30654, NpcFamily = 171, Scale = 100, RunSpeed = 446, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1107.304f, Y = 1.610f, Z = 182.354f, HeadingY = -0.247611f, HeadingW = 0.968860f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 999.131f, Y = 1.610f, Z = 207.381f, HeadingY = 0.993571f, HeadingW = 0.113210f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 168, Health = 27908, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 893.798f, Y = 2.659f, Z = 194.048f, HeadingY = 0.982835f, HeadingW = 0.184488f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1017.466f, Y = 1.610f, Z = 208.328f, HeadingY = -0.440569f, HeadingW = 0.897719f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 173, Health = 29870, NpcFamily = 171, Scale = 100, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1031.533f, Y = 1.610f, Z = 230.601f, HeadingY = 0.454338f, HeadingW = 0.890829f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 172, Health = 29477, NpcFamily = 171, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1055.844f, Y = 1.610f, Z = 247.428f, HeadingY = 0.835621f, HeadingW = 0.549306f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 167, Health = 27516, NpcFamily = 171, Scale = 100, RunSpeed = 441, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1060.152f, Y = 1.610f, Z = 259.320f, HeadingY = 0.003198f, HeadingW = 0.999995f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1080.261f, Y = 1.610f, Z = 279.778f, HeadingY = 0.967197f, HeadingW = 0.254026f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4543, Side = 3, MonsterData = 214982, Level = 169, Health = 28300, NpcFamily = 171, Scale = 100, RunSpeed = 442, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1084.414f, Y = 1.610f, Z = 299.736f, HeadingY = 0.673427f, HeadingW = 0.739254f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 875.394f, Y = 15.205f, Z = 821.106f, HeadingY = -0.934844f, HeadingW = 0.355060f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 876.505f, Y = 15.205f, Z = 820.996f, HeadingY = 0.224300f, HeadingW = 0.974520f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 879.539f, Y = 14.905f, Z = 823.705f, HeadingY = -0.997819f, HeadingW = 0.066016f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 878.331f, Y = 14.905f, Z = 824.077f, HeadingY = 0.990623f, HeadingW = 0.136622f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 873.280f, Y = 16.005f, Z = 823.526f, HeadingY = 0.008556f, HeadingW = 0.999963f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 867.603f, Y = 2.168f, Z = 821.939f, HeadingY = 0.992914f, HeadingW = 0.118832f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 873.004f, Y = 2.275f, Z = 831.729f, HeadingY = -0.774935f, HeadingW = 0.632042f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 95, Health = 7438, NpcFamily = 175, Scale = 125, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 812.710f, Y = 9.413f, Z = 788.531f, HeadingY = 0.931554f, HeadingW = 0.363604f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 54, Health = 3365, NpcFamily = 186, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 904.926f, Y = 15.030f, Z = 806.263f, HeadingY = 0.969422f, HeadingW = 0.245398f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 56, Health = 3547, NpcFamily = 186, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 887.231f, Y = 2.410f, Z = 819.071f, HeadingY = -0.016758f, HeadingW = 0.999860f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 95, Health = 7438, NpcFamily = 175, Scale = 125, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 858.272f, Y = 3.493f, Z = 844.372f, HeadingY = -0.425365f, HeadingW = 0.905022f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 858.963f, Y = 2.010f, Z = 852.490f, HeadingY = -0.457598f, HeadingW = 0.889159f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 57, Health = 3638, NpcFamily = 186, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 743.307f, Y = 2.882f, Z = 858.669f, HeadingY = 0.813786f, HeadingW = 0.581164f, Textures = null, Meshes = null },
                new MobSlot { Name = "Devourer of Life", PlayfieldId = 4540, Side = 3, MonsterData = 209409, Level = 104, Health = 8868, NpcFamily = 195, Scale = 130, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 720.221f, Y = 2.836f, Z = 688.245f, HeadingY = 0.963800f, HeadingW = 0.266627f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 639.991f, Y = 4.413f, Z = 726.269f, HeadingY = -0.302480f, HeadingW = 0.953156f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 109, Health = 9663, NpcFamily = 207, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 642.240f, Y = 3.568f, Z = 680.884f, HeadingY = 0.696297f, HeadingW = 0.717754f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 634.286f, Y = 7.815f, Z = 637.420f, HeadingY = 0.999993f, HeadingW = 0.003726f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 110, Health = 9822, NpcFamily = 207, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 635.347f, Y = 7.815f, Z = 697.555f, HeadingY = 0.986132f, HeadingW = 0.165963f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 110, Health = 9822, NpcFamily = 207, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 610.961f, Y = 15.635f, Z = 684.287f, HeadingY = 0.855652f, HeadingW = 0.517551f, Textures = null, Meshes = null },
                new MobSlot { Name = "Inicha", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 115, Health = 10617, NpcFamily = 207, Scale = 130, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 617.171f, Y = 15.635f, Z = 700.112f, HeadingY = 0.727637f, HeadingW = 0.685962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 627.721f, Y = 7.813f, Z = 637.503f, HeadingY = 0.999997f, HeadingW = 0.002328f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 655.505f, Y = 2.410f, Z = 689.335f, HeadingY = 0.999773f, HeadingW = -0.021316f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 601.592f, Y = 7.808f, Z = 652.863f, HeadingY = 0.001897f, HeadingW = 0.999998f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 601.053f, Y = 7.808f, Z = 658.988f, HeadingY = 1.000000f, HeadingW = -0.000807f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 645.224f, Y = 3.372f, Z = 660.015f, HeadingY = 0.697134f, HeadingW = 0.716941f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 644.013f, Y = 3.993f, Z = 651.767f, HeadingY = 0.356240f, HeadingW = 0.934394f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 686.224f, Y = 23.205f, Z = 677.845f, HeadingY = 0.942642f, HeadingW = 0.333805f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 685.154f, Y = 23.205f, Z = 677.734f, HeadingY = -0.316893f, HeadingW = 0.948461f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 653.957f, Y = 2.810f, Z = 640.430f, HeadingY = 0.003375f, HeadingW = 0.999994f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 647.138f, Y = 2.588f, Z = 602.089f, HeadingY = 0.334779f, HeadingW = 0.942296f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 634.903f, Y = 7.815f, Z = 615.625f, HeadingY = -0.003947f, HeadingW = 0.999992f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 627.934f, Y = 7.815f, Z = 609.884f, HeadingY = -0.942396f, HeadingW = 0.334498f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 676.746f, Y = 26.905f, Z = 617.733f, HeadingY = -0.025327f, HeadingW = 0.999679f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 676.359f, Y = 26.905f, Z = 616.856f, HeadingY = 0.643520f, HeadingW = 0.765429f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 109, Health = 9663, NpcFamily = 207, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 641.850f, Y = 3.863f, Z = 628.151f, HeadingY = 0.481927f, HeadingW = 0.876211f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 103, Health = 8710, NpcFamily = 207, Scale = 100, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 574.718f, Y = 15.886f, Z = 660.743f, HeadingY = 0.458807f, HeadingW = 0.888536f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 565.237f, Y = 24.735f, Z = 688.193f, HeadingY = 0.003965f, HeadingW = 0.999992f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 526.238f, Y = 18.244f, Z = 711.406f, HeadingY = 0.969751f, HeadingW = 0.244097f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 561.960f, Y = 15.305f, Z = 646.131f, HeadingY = 0.079928f, HeadingW = 0.996801f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 110, Health = 9822, NpcFamily = 207, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 569.362f, Y = 15.305f, Z = 654.356f, HeadingY = -0.720016f, HeadingW = 0.693957f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 102, Health = 8551, NpcFamily = 207, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 563.085f, Y = 15.305f, Z = 664.288f, HeadingY = 0.986906f, HeadingW = 0.161298f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 108, Health = 9504, NpcFamily = 207, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 593.978f, Y = 7.807f, Z = 658.654f, HeadingY = 0.999994f, HeadingW = -0.003386f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 593.822f, Y = 7.807f, Z = 652.957f, HeadingY = 0.001932f, HeadingW = 0.999998f, Textures = null, Meshes = null },
                new MobSlot { Name = "Aniuchach", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 115, Health = 26541, NpcFamily = 207, Scale = 130, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 587.750f, Y = 7.806f, Z = 656.183f, HeadingY = -0.731786f, HeadingW = 0.681534f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 578.170f, Y = 16.706f, Z = 730.478f, HeadingY = -0.825441f, HeadingW = 0.564489f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 109, Health = 9663, NpcFamily = 207, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 567.040f, Y = 16.705f, Z = 739.288f, HeadingY = -0.722251f, HeadingW = 0.691631f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 564.376f, Y = 7.227f, Z = 731.995f, HeadingY = 0.903063f, HeadingW = 0.429509f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 529.667f, Y = 6.505f, Z = 640.031f, HeadingY = 0.047656f, HeadingW = 0.998864f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 548.000f, Y = 12.642f, Z = 632.901f, HeadingY = 0.635610f, HeadingW = 0.772010f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 102, Health = 8551, NpcFamily = 207, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 522.915f, Y = 6.505f, Z = 630.331f, HeadingY = 0.040018f, HeadingW = 0.999199f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 100, Health = 8233, NpcFamily = 207, Scale = 100, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 521.518f, Y = 6.505f, Z = 611.897f, HeadingY = -0.001614f, HeadingW = 0.999999f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tuaninnik", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 115, Health = 21233, NpcFamily = 207, Scale = 150, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 526.551f, Y = 6.505f, Z = 609.984f, HeadingY = 0.241244f, HeadingW = 0.970465f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 527.368f, Y = 6.505f, Z = 605.315f, HeadingY = 0.049948f, HeadingW = 0.998752f, Textures = null, Meshes = null },
                new MobSlot { Name = "Ichiachich", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 115, Health = 21233, NpcFamily = 207, Scale = 150, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 537.488f, Y = 6.505f, Z = 654.368f, HeadingY = -0.720791f, HeadingW = 0.693152f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 101, Health = 8392, NpcFamily = 207, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 537.501f, Y = 6.505f, Z = 649.744f, HeadingY = -0.392625f, HeadingW = 0.919699f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 537.104f, Y = 6.505f, Z = 658.715f, HeadingY = -0.941261f, HeadingW = 0.337681f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 554.966f, Y = 15.305f, Z = 654.553f, HeadingY = 0.777446f, HeadingW = 0.628949f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 562.211f, Y = 14.943f, Z = 690.702f, HeadingY = -0.265536f, HeadingW = 0.964101f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 104, Health = 8868, NpcFamily = 207, Scale = 100, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 523.163f, Y = 6.505f, Z = 690.616f, HeadingY = -0.968459f, HeadingW = 0.249175f, Textures = null, Meshes = null },
                new MobSlot { Name = "Suininnik", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 115, Health = 21233, NpcFamily = 207, Scale = 130, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 523.775f, Y = 6.505f, Z = 695.625f, HeadingY = -0.936400f, HeadingW = 0.350935f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 564.884f, Y = 8.267f, Z = 730.565f, HeadingY = 0.215577f, HeadingW = 0.976487f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 599.916f, Y = 14.357f, Z = 583.329f, HeadingY = 0.666969f, HeadingW = 0.745086f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 109, Health = 9663, NpcFamily = 207, Scale = 100, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 581.280f, Y = 14.410f, Z = 610.177f, HeadingY = 0.618909f, HeadingW = 0.785463f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 108, Health = 9504, NpcFamily = 207, Scale = 100, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 554.642f, Y = 15.770f, Z = 581.572f, HeadingY = 0.928771f, HeadingW = 0.370654f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 514.840f, Y = 1.894f, Z = 595.493f, HeadingY = -0.376913f, HeadingW = 0.926249f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 498.410f, Y = 1.610f, Z = 621.168f, HeadingY = 0.026665f, HeadingW = 0.999644f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 110, Health = 9822, NpcFamily = 207, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 514.051f, Y = 2.428f, Z = 654.823f, HeadingY = -0.716850f, HeadingW = 0.697228f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 455.592f, Y = 9.385f, Z = 673.097f, HeadingY = -0.904789f, HeadingW = 0.425859f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 499.802f, Y = 1.610f, Z = 660.442f, HeadingY = -0.017439f, HeadingW = 0.999848f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 102, Health = 8551, NpcFamily = 207, Scale = 100, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 519.011f, Y = 6.505f, Z = 698.180f, HeadingY = -0.394707f, HeadingW = 0.918807f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 531.527f, Y = 24.255f, Z = 733.986f, HeadingY = -0.700293f, HeadingW = 0.713856f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 106, Health = 9186, NpcFamily = 207, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 566.101f, Y = 5.779f, Z = 743.185f, HeadingY = 0.985073f, HeadingW = 0.172139f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 110, Health = 9822, NpcFamily = 207, Scale = 100, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 523.201f, Y = 17.834f, Z = 725.786f, HeadingY = -0.717580f, HeadingW = 0.696476f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 524.851f, Y = 18.597f, Z = 738.659f, HeadingY = -0.658831f, HeadingW = 0.752291f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 563.901f, Y = 8.519f, Z = 766.199f, HeadingY = 0.467906f, HeadingW = 0.883778f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 443.308f, Y = 11.210f, Z = 708.886f, HeadingY = 0.987667f, HeadingW = 0.156570f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 566.099f, Y = 9.610f, Z = 788.799f, HeadingY = 0.060624f, HeadingW = 0.998161f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 107, Health = 9345, NpcFamily = 207, Scale = 100, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 567.594f, Y = 9.610f, Z = 791.123f, HeadingY = 0.999931f, HeadingW = 0.011769f, Textures = null, Meshes = null },
                new MobSlot { Name = "Callous Mortiig", PlayfieldId = 4540, Side = 3, MonsterData = 209368, Level = 105, Health = 9027, NpcFamily = 207, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 489.191f, Y = 1.610f, Z = 778.442f, HeadingY = -0.992327f, HeadingW = 0.123638f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1687.600f, Y = 63.775f, Z = 1453.900f, HeadingY = -0.238381f, HeadingW = 0.971172f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1699.710f, Y = 38.859f, Z = 1457.258f, HeadingY = 0.817468f, HeadingW = 0.575975f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1689.100f, Y = 49.264f, Z = 1440.300f, HeadingY = 0.896782f, HeadingW = 0.442472f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 190, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1719.908f, Y = 17.210f, Z = 1471.423f, HeadingY = -0.967594f, HeadingW = 0.252510f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 63, Health = 4184, NpcFamily = 190, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1719.279f, Y = 17.210f, Z = 1473.789f, HeadingY = 0.650241f, HeadingW = 0.759728f, Textures = null, Meshes = null },
                new MobSlot { Name = "Stalking Slayer", PlayfieldId = 4540, Side = 3, MonsterData = 209029, Level = 61, Health = 4002, NpcFamily = 181, Scale = 100, RunSpeed = 213, CharacterFlags = 268980737, VisualFlags = 31, HeadMesh = 0, X = 1720.547f, Y = 17.210f, Z = 1473.780f, HeadingY = 0.690786f, HeadingW = 0.723059f, Textures = null, Meshes = null },
                new MobSlot { Name = "Stalking Slayer", PlayfieldId = 4540, Side = 3, MonsterData = 209029, Level = 61, Health = 4002, NpcFamily = 181, Scale = 100, RunSpeed = 213, CharacterFlags = 268980737, VisualFlags = 31, HeadMesh = 0, X = 1721.633f, Y = 17.210f, Z = 1473.600f, HeadingY = 0.750724f, HeadingW = 0.660616f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 59, Health = 3820, NpcFamily = 190, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1724.953f, Y = 17.210f, Z = 1472.604f, HeadingY = 0.532402f, HeadingW = 0.846492f, Textures = null, Meshes = null },
                new MobSlot { Name = "Stalking Slayer", PlayfieldId = 4540, Side = 3, MonsterData = 209029, Level = 64, Health = 4276, NpcFamily = 181, Scale = 100, RunSpeed = 224, CharacterFlags = 268980737, VisualFlags = 31, HeadMesh = 0, X = 1723.729f, Y = 17.210f, Z = 1476.089f, HeadingY = 0.484873f, HeadingW = 0.874585f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 60, Health = 3911, NpcFamily = 175, Scale = 125, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1671.454f, Y = 17.210f, Z = 1492.489f, HeadingY = 0.953127f, HeadingW = 0.302569f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 58, Health = 3729, NpcFamily = 175, Scale = 125, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1709.881f, Y = 53.618f, Z = 1392.049f, HeadingY = 0.929787f, HeadingW = 0.368099f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 60, Health = 3911, NpcFamily = 175, Scale = 125, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1714.163f, Y = 57.834f, Z = 1357.706f, HeadingY = 0.083862f, HeadingW = 0.996477f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1735.917f, Y = 33.997f, Z = 1308.942f, HeadingY = 0.992019f, HeadingW = 0.126086f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 55, Health = 3456, NpcFamily = 172, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1770.488f, Y = 80.100f, Z = 1014.605f, HeadingY = 0.823853f, HeadingW = 0.526970f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 59, Health = 3820, NpcFamily = 172, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1770.488f, Y = 80.100f, Z = 1014.605f, HeadingY = 0.823853f, HeadingW = 0.526970f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shell Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 78, Health = 5551, NpcFamily = 207, Scale = 70, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1799.969f, Y = 55.233f, Z = 1038.002f, HeadingY = -0.648767f, HeadingW = 0.760987f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 58, Health = 3729, NpcFamily = 175, Scale = 125, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1670.889f, Y = 84.040f, Z = 1108.867f, HeadingY = 0.021116f, HeadingW = 0.999777f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 59, Health = 3820, NpcFamily = 172, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1743.288f, Y = 73.392f, Z = 964.234f, HeadingY = 0.729906f, HeadingW = 0.612782f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 56, Health = 3547, NpcFamily = 172, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1660.323f, Y = 71.370f, Z = 944.110f, HeadingY = 0.119276f, HeadingW = 0.908840f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 58, Health = 3729, NpcFamily = 172, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1730.545f, Y = 67.867f, Z = 941.840f, HeadingY = 0.175791f, HeadingW = 0.973653f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 64, Health = 4276, NpcFamily = 170, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1564.196f, Y = 43.665f, Z = 940.155f, HeadingY = -0.398517f, HeadingW = 0.917161f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 56, Health = 3547, NpcFamily = 172, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1692.739f, Y = 54.529f, Z = 917.111f, HeadingY = -0.691720f, HeadingW = 0.714579f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1785.880f, Y = 44.987f, Z = 886.192f, HeadingY = -0.858607f, HeadingW = 0.512634f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1877.147f, Y = 77.861f, Z = 778.299f, HeadingY = 0.261105f, HeadingW = 0.900489f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 106, Health = 11942, NpcFamily = 207, Scale = 60, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1846.422f, Y = 81.414f, Z = 772.671f, HeadingY = -0.197370f, HeadingW = 0.917168f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1879.193f, Y = 79.533f, Z = 771.783f, HeadingY = 0.842528f, HeadingW = 0.445415f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1882.538f, Y = 54.746f, Z = 796.241f, HeadingY = -0.195613f, HeadingW = 0.969870f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 107, Health = 12149, NpcFamily = 207, Scale = 60, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1881.513f, Y = 73.819f, Z = 762.052f, HeadingY = 0.488397f, HeadingW = 0.756008f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1902.647f, Y = 83.448f, Z = 777.443f, HeadingY = -0.698971f, HeadingW = 0.715150f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 107, Health = 12149, NpcFamily = 207, Scale = 60, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1911.504f, Y = 88.186f, Z = 793.845f, HeadingY = -0.404720f, HeadingW = 0.809315f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 104, Health = 11529, NpcFamily = 207, Scale = 60, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1896.482f, Y = 69.714f, Z = 802.761f, HeadingY = -0.960202f, HeadingW = 0.135048f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 104, Health = 11529, NpcFamily = 207, Scale = 60, RunSpeed = 356, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1890.468f, Y = 66.977f, Z = 814.557f, HeadingY = -0.640809f, HeadingW = 0.616031f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1934.506f, Y = 99.739f, Z = 766.352f, HeadingY = -0.879901f, HeadingW = 0.450012f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 107, Health = 12149, NpcFamily = 207, Scale = 60, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1959.009f, Y = 102.010f, Z = 761.783f, HeadingY = -0.032947f, HeadingW = 0.999457f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 60, Health = 3911, NpcFamily = 172, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1955.446f, Y = 58.883f, Z = 870.371f, HeadingY = -0.973067f, HeadingW = 0.124054f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 109, Health = 12562, NpcFamily = 207, Scale = 60, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1862.113f, Y = 94.929f, Z = 757.683f, HeadingY = 0.515407f, HeadingW = 0.777604f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 106, Health = 11942, NpcFamily = 207, Scale = 60, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1873.789f, Y = 92.586f, Z = 745.680f, HeadingY = -0.025950f, HeadingW = 0.979602f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 109, Health = 12562, NpcFamily = 207, Scale = 60, RunSpeed = 369, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1873.900f, Y = 94.076f, Z = 731.899f, HeadingY = -0.688324f, HeadingW = 0.661731f, Textures = null, Meshes = null },
                new MobSlot { Name = "Arachno Gelida", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 115, Health = 26541, NpcFamily = 207, Scale = 130, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1877.400f, Y = 92.414f, Z = 742.635f, HeadingY = -0.501814f, HeadingW = 0.854003f, Textures = null, Meshes = null },
                new MobSlot { Name = "Arachno Frigida", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 115, Health = 26541, NpcFamily = 207, Scale = 130, RunSpeed = 384, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1889.581f, Y = 103.216f, Z = 732.706f, HeadingY = -0.699971f, HeadingW = 0.704774f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1874.716f, Y = 93.346f, Z = 737.392f, HeadingY = -0.711093f, HeadingW = 0.686346f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 106, Health = 11942, NpcFamily = 207, Scale = 60, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1895.132f, Y = 79.556f, Z = 738.992f, HeadingY = -0.095025f, HeadingW = 0.861522f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 100, Health = 10702, NpcFamily = 207, Scale = 60, RunSpeed = 346, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1908.439f, Y = 84.998f, Z = 743.183f, HeadingY = -0.711072f, HeadingW = 0.703119f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 102, Health = 11116, NpcFamily = 207, Scale = 60, RunSpeed = 351, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1916.616f, Y = 94.470f, Z = 727.440f, HeadingY = -0.629476f, HeadingW = 0.716503f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 105, Health = 11735, NpcFamily = 207, Scale = 60, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1899.460f, Y = 77.334f, Z = 750.961f, HeadingY = -0.817326f, HeadingW = 0.576175f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1880.286f, Y = 93.185f, Z = 731.422f, HeadingY = -0.712165f, HeadingW = 0.698492f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 107, Health = 12149, NpcFamily = 207, Scale = 60, RunSpeed = 364, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1910.326f, Y = 82.415f, Z = 721.171f, HeadingY = -0.713690f, HeadingW = 0.700461f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 106, Health = 11942, NpcFamily = 207, Scale = 60, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1937.846f, Y = 95.428f, Z = 751.065f, HeadingY = 0.702698f, HeadingW = 0.709743f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1955.692f, Y = 92.225f, Z = 720.522f, HeadingY = -0.023931f, HeadingW = 0.984776f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 101, Health = 10909, NpcFamily = 207, Scale = 60, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1985.914f, Y = 100.687f, Z = 780.627f, HeadingY = 0.329054f, HeadingW = 0.941700f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 110, Health = 12768, NpcFamily = 207, Scale = 60, RunSpeed = 371, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1965.792f, Y = 95.670f, Z = 727.430f, HeadingY = 0.005890f, HeadingW = 0.998744f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 106, Health = 11942, NpcFamily = 207, Scale = 60, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1906.108f, Y = 64.794f, Z = 688.932f, HeadingY = 0.981954f, HeadingW = 0.003131f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 103, Health = 11322, NpcFamily = 207, Scale = 60, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1944.575f, Y = 90.106f, Z = 700.268f, HeadingY = -0.944426f, HeadingW = 0.309844f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 108, Health = 12355, NpcFamily = 207, Scale = 60, RunSpeed = 366, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1917.259f, Y = 93.077f, Z = 718.717f, HeadingY = -0.787793f, HeadingW = 0.508362f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 57, Health = 3638, NpcFamily = 172, Scale = 100, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1941.107f, Y = 87.618f, Z = 710.933f, HeadingY = -0.996456f, HeadingW = 0.046323f, Textures = null, Meshes = null },
                new MobSlot { Name = "Chill Spider", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 103, Health = 11322, NpcFamily = 207, Scale = 60, RunSpeed = 354, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2020.766f, Y = 86.167f, Z = 760.794f, HeadingY = 0.063032f, HeadingW = 0.991953f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 54, Health = 3365, NpcFamily = 201, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2081.146f, Y = 56.187f, Z = 733.138f, HeadingY = -0.994801f, HeadingW = 0.101840f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 105, Health = 9027, NpcFamily = 183, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2111.856f, Y = 8.386f, Z = 738.181f, HeadingY = -0.768453f, HeadingW = 0.529250f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4540, Side = 3, MonsterData = 209333, Level = 106, Health = 9186, NpcFamily = 193, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2133.943f, Y = 2.208f, Z = 698.708f, HeadingY = 0.492662f, HeadingW = 0.870221f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2099.323f, Y = 72.825f, Z = 853.289f, HeadingY = 0.910056f, HeadingW = 0.414486f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2114.482f, Y = 66.254f, Z = 860.457f, HeadingY = -0.698914f, HeadingW = 0.715206f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2106.147f, Y = 71.351f, Z = 848.295f, HeadingY = -0.524673f, HeadingW = 0.851304f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2089.326f, Y = 74.199f, Z = 848.946f, HeadingY = 0.279595f, HeadingW = 0.960118f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 56, Health = 3547, NpcFamily = 201, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2153.892f, Y = 52.629f, Z = 879.971f, HeadingY = 0.950759f, HeadingW = 0.309930f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Crippler of Growth", PlayfieldId = 4540, Side = 3, MonsterData = 209333, Level = 106, Health = 9186, NpcFamily = 193, Scale = 100, RunSpeed = 361, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2172.994f, Y = 2.380f, Z = 768.328f, HeadingY = 0.895250f, HeadingW = 0.445565f, Textures = null, Meshes = null },
                new MobSlot { Name = "Minion Grunt", PlayfieldId = 4540, Side = 3, MonsterData = 207420, Level = 174, Health = 16947, NpcFamily = 3, Scale = 119, RunSpeed = 445, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2115.977f, Y = 6.431f, Z = 668.428f, HeadingY = 0.518659f, HeadingW = 0.854981f, Textures = null, Meshes = null },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 105, Health = 9027, NpcFamily = 183, Scale = 100, RunSpeed = 359, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2146.679f, Y = 1.610f, Z = 651.777f, HeadingY = -0.505420f, HeadingW = 0.862873f, Textures = null, Meshes = null },
                new MobSlot { Name = "Heckler of Stones", PlayfieldId = 4540, Side = 3, MonsterData = 214982, Level = 178, Health = 31831, NpcFamily = 171, Scale = 100, RunSpeed = 447, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2148.090f, Y = 1.610f, Z = 463.582f, HeadingY = -0.279139f, HeadingW = 0.960251f, Textures = null, Meshes = null },
                new MobSlot { Name = "Voracious Horror", PlayfieldId = 4540, Side = 3, MonsterData = 214973, Level = 172, Health = 29477, NpcFamily = 174, Scale = 100, RunSpeed = 444, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 2145.273f, Y = 1.610f, Z = 460.032f, HeadingY = 0.636225f, HeadingW = 0.771504f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1965.471f, Y = 35.610f, Z = 519.960f, HeadingY = -0.986437f, HeadingW = 0.164139f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 55, Health = 3456, NpcFamily = 172, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1932.136f, Y = 12.934f, Z = 453.643f, HeadingY = -0.395426f, HeadingW = 0.897712f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1923.590f, Y = 12.734f, Z = 453.592f, HeadingY = 0.161429f, HeadingW = 0.981952f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1931.978f, Y = 12.488f, Z = 448.756f, HeadingY = 0.999992f, HeadingW = -0.004008f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1935.611f, Y = 12.161f, Z = 449.059f, HeadingY = 0.808636f, HeadingW = 0.588310f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1922.021f, Y = 14.541f, Z = 468.724f, HeadingY = 0.597798f, HeadingW = 0.801646f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1920.101f, Y = 14.588f, Z = 470.171f, HeadingY = -0.020245f, HeadingW = 0.999795f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1933.626f, Y = 12.544f, Z = 456.299f, HeadingY = 0.469526f, HeadingW = 0.882919f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1880.008f, Y = 10.815f, Z = 446.904f, HeadingY = -0.096876f, HeadingW = 0.995297f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1910.857f, Y = 10.917f, Z = 445.043f, HeadingY = 0.969511f, HeadingW = 0.245047f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1908.938f, Y = 10.894f, Z = 444.810f, HeadingY = -0.990617f, HeadingW = 0.136665f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 52, Health = 3183, NpcFamily = 170, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1901.392f, Y = 14.708f, Z = 461.678f, HeadingY = -0.756956f, HeadingW = 0.653465f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1900.036f, Y = 14.437f, Z = 458.730f, HeadingY = -0.262326f, HeadingW = 0.964979f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1899.642f, Y = 15.122f, Z = 463.438f, HeadingY = -0.990566f, HeadingW = 0.137039f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1896.784f, Y = 14.070f, Z = 458.956f, HeadingY = 0.547596f, HeadingW = 0.836743f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 52, Health = 3183, NpcFamily = 170, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1896.332f, Y = 14.541f, Z = 462.283f, HeadingY = 0.794799f, HeadingW = 0.606873f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1847.745f, Y = 16.757f, Z = 464.839f, HeadingY = 0.741931f, HeadingW = 0.666791f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1844.088f, Y = 8.473f, Z = 427.494f, HeadingY = -0.362943f, HeadingW = 0.931811f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 52, Health = 3183, NpcFamily = 170, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1874.829f, Y = 11.610f, Z = 431.630f, HeadingY = -0.107686f, HeadingW = 0.994185f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 52, Health = 3183, NpcFamily = 170, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1840.048f, Y = 8.891f, Z = 416.780f, HeadingY = 0.966972f, HeadingW = 0.254882f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1856.113f, Y = 10.041f, Z = 425.546f, HeadingY = 0.963736f, HeadingW = 0.266857f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1854.991f, Y = 9.989f, Z = 424.758f, HeadingY = 0.992169f, HeadingW = 0.124906f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 55, Health = 3456, NpcFamily = 170, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1852.342f, Y = 10.086f, Z = 431.461f, HeadingY = -0.358485f, HeadingW = 0.933535f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1876.068f, Y = 11.602f, Z = 429.440f, HeadingY = 0.939776f, HeadingW = 0.341792f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1874.202f, Y = 11.610f, Z = 428.998f, HeadingY = 0.960592f, HeadingW = 0.277961f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 55, Health = 3456, NpcFamily = 170, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1842.479f, Y = 16.262f, Z = 478.355f, HeadingY = -0.845845f, HeadingW = 0.533429f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1878.215f, Y = 13.218f, Z = 470.001f, HeadingY = -0.926814f, HeadingW = 0.375521f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 55, Health = 3456, NpcFamily = 170, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1844.410f, Y = 16.734f, Z = 469.388f, HeadingY = -0.845501f, HeadingW = 0.533973f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1856.220f, Y = 15.152f, Z = 469.523f, HeadingY = 0.947418f, HeadingW = 0.319998f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1879.132f, Y = 2.834f, Z = 458.489f, HeadingY = 0.552189f, HeadingW = 0.833719f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1821.599f, Y = 1.610f, Z = 474.596f, HeadingY = -0.996658f, HeadingW = 0.081688f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 53, Health = 3274, NpcFamily = 172, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1817.414f, Y = 1.610f, Z = 399.854f, HeadingY = -0.646912f, HeadingW = 0.762564f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1819.131f, Y = 8.642f, Z = 404.853f, HeadingY = 0.996641f, HeadingW = 0.081897f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 52, Health = 3183, NpcFamily = 170, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1820.110f, Y = 8.699f, Z = 405.134f, HeadingY = 0.962986f, HeadingW = 0.269551f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 54, Health = 3365, NpcFamily = 170, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1808.839f, Y = 7.298f, Z = 407.541f, HeadingY = -0.693233f, HeadingW = 0.720713f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 51, Health = 3092, NpcFamily = 170, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1807.205f, Y = 6.866f, Z = 406.067f, HeadingY = -0.757625f, HeadingW = 0.652690f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 54, Health = 3365, NpcFamily = 201, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1800.076f, Y = 6.185f, Z = 401.765f, HeadingY = 0.708562f, HeadingW = 0.705649f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 50, Health = 3000, NpcFamily = 170, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1795.402f, Y = 6.777f, Z = 396.196f, HeadingY = -0.939936f, HeadingW = 0.341350f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 53, Health = 3274, NpcFamily = 170, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1793.620f, Y = 6.086f, Z = 400.736f, HeadingY = -0.492911f, HeadingW = 0.870080f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1693.998f, Y = 27.872f, Z = 505.159f, HeadingY = 0.234448f, HeadingW = 0.971470f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 56, Health = 3547, NpcFamily = 172, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1693.585f, Y = 14.335f, Z = 508.739f, HeadingY = -0.639089f, HeadingW = 0.762795f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1745.048f, Y = 23.405f, Z = 498.832f, HeadingY = -0.859858f, HeadingW = 0.510533f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1744.581f, Y = 23.405f, Z = 500.317f, HeadingY = -0.064944f, HeadingW = 0.997889f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1635.944f, Y = 1.610f, Z = 520.066f, HeadingY = 0.081848f, HeadingW = 0.996645f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 54, Health = 3365, NpcFamily = 175, Scale = 125, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1685.815f, Y = 10.909f, Z = 524.780f, HeadingY = 0.999950f, HeadingW = -0.009962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1690.527f, Y = 3.379f, Z = 543.840f, HeadingY = 0.971251f, HeadingW = 0.238058f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 53, Health = 3274, NpcFamily = 190, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1643.930f, Y = 9.635f, Z = 589.458f, HeadingY = -0.998090f, HeadingW = 0.061777f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 55, Health = 3456, NpcFamily = 190, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1673.869f, Y = 9.635f, Z = 586.443f, HeadingY = 0.999963f, HeadingW = 0.008553f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1709.212f, Y = 34.505f, Z = 592.530f, HeadingY = 0.831744f, HeadingW = 0.555159f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1707.546f, Y = 34.505f, Z = 592.661f, HeadingY = 0.230851f, HeadingW = 0.972989f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 54, Health = 3365, NpcFamily = 175, Scale = 125, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1694.703f, Y = 3.610f, Z = 573.694f, HeadingY = -0.982624f, HeadingW = 0.185606f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 53, Health = 3274, NpcFamily = 172, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1740.149f, Y = 51.186f, Z = 554.432f, HeadingY = -0.922537f, HeadingW = 0.373116f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1738.191f, Y = 51.543f, Z = 574.551f, HeadingY = 0.840487f, HeadingW = 0.530598f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 95, Health = 7438, NpcFamily = 175, Scale = 125, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1729.479f, Y = 8.694f, Z = 562.732f, HeadingY = -0.796730f, HeadingW = 0.604336f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 54, Health = 3365, NpcFamily = 190, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1645.515f, Y = 17.425f, Z = 606.242f, HeadingY = 0.718511f, HeadingW = 0.695516f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 54, Health = 3365, NpcFamily = 190, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1709.354f, Y = 44.637f, Z = 605.564f, HeadingY = -0.809919f, HeadingW = 0.586542f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 53, Health = 3274, NpcFamily = 190, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1685.406f, Y = 60.010f, Z = 625.713f, HeadingY = -0.681117f, HeadingW = 0.732174f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 54, Health = 3365, NpcFamily = 190, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1714.064f, Y = 52.435f, Z = 609.885f, HeadingY = 0.996595f, HeadingW = 0.082457f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 55, Health = 3456, NpcFamily = 190, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1709.735f, Y = 44.410f, Z = 614.233f, HeadingY = -0.737350f, HeadingW = 0.675511f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 54, Health = 3365, NpcFamily = 172, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1736.555f, Y = 67.672f, Z = 619.972f, HeadingY = 0.984112f, HeadingW = 0.162847f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 52, Health = 3183, NpcFamily = 190, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1720.244f, Y = 52.435f, Z = 606.050f, HeadingY = -0.493222f, HeadingW = 0.869904f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shades Of Grey", PlayfieldId = 4540, Side = 0, MonsterData = 22802, Level = 80, Health = 5733, NpcFamily = 200, Scale = 250, RunSpeed = 284, CharacterFlags = 277352961, VisualFlags = 31, HeadMesh = 0, X = 1670.076f, Y = 61.705f, Z = 621.408f, HeadingY = 0.525015f, HeadingW = 0.851092f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 54, Health = 3365, NpcFamily = 190, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1649.481f, Y = 25.335f, Z = 602.754f, HeadingY = -0.942663f, HeadingW = 0.333747f, Textures = null, Meshes = null },
                new MobSlot { Name = "Tempterus", PlayfieldId = 4540, Side = 2, MonsterData = 209189, Level = 1, Health = 25, NpcFamily = 202, Scale = 225, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1710.949f, Y = 44.410f, Z = 614.822f, HeadingY = -0.818334f, HeadingW = 0.574742f, Textures = null, Meshes = null },
                new MobSlot { Name = "Kolaana", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 56, Health = 3547, NpcFamily = 190, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1717.069f, Y = 60.035f, Z = 607.891f, HeadingY = -0.935083f, HeadingW = 0.354429f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 55, Health = 3456, NpcFamily = 172, Scale = 100, RunSpeed = 190, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1615.783f, Y = 50.032f, Z = 629.644f, HeadingY = -0.972820f, HeadingW = 0.028788f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1578.743f, Y = 52.821f, Z = 559.153f, HeadingY = -0.582251f, HeadingW = 0.813009f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1566.412f, Y = 52.191f, Z = 558.587f, HeadingY = -0.677537f, HeadingW = 0.735488f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1582.901f, Y = 51.880f, Z = 576.699f, HeadingY = -0.954649f, HeadingW = 0.297734f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1528.429f, Y = 3.610f, Z = 567.707f, HeadingY = -0.398586f, HeadingW = 0.917131f, Textures = null, Meshes = null },
                new MobSlot { Name = "Stalking Slayer", PlayfieldId = 4540, Side = 3, MonsterData = 209029, Level = 60, Health = 3911, NpcFamily = 181, Scale = 100, RunSpeed = 209, CharacterFlags = 268980737, VisualFlags = 31, HeadMesh = 0, X = 1667.314f, Y = 51.759f, Z = 670.133f, HeadingY = -0.797578f, HeadingW = 0.599117f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1554.558f, Y = 39.087f, Z = 593.273f, HeadingY = 0.930186f, HeadingW = 0.367088f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 95, Health = 7438, NpcFamily = 175, Scale = 125, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1531.293f, Y = 3.213f, Z = 560.005f, HeadingY = -0.424242f, HeadingW = 0.905549f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shadowleet", PlayfieldId = 4540, Side = 3, MonsterData = 226880, Level = 1, Health = 20, NpcFamily = 168, Scale = 90, RunSpeed = 5, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1560.062f, Y = 2.903f, Z = 535.061f, HeadingY = -0.216835f, HeadingW = 0.976208f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1496.705f, Y = 25.705f, Z = 623.318f, HeadingY = 0.877613f, HeadingW = 0.479369f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1495.577f, Y = 25.705f, Z = 622.478f, HeadingY = -0.621681f, HeadingW = 0.783270f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 78, Health = 5551, NpcFamily = 172, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1451.230f, Y = 52.853f, Z = 609.786f, HeadingY = -0.126396f, HeadingW = 0.985887f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 81, Health = 5824, NpcFamily = 172, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1438.893f, Y = 59.041f, Z = 625.743f, HeadingY = 0.941561f, HeadingW = 0.275159f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 79, Health = 5642, NpcFamily = 201, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1441.875f, Y = 61.182f, Z = 669.713f, HeadingY = 0.667147f, HeadingW = 0.744926f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1474.004f, Y = 58.453f, Z = 701.447f, HeadingY = -0.087646f, HeadingW = 0.996152f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Insidious Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 80, Health = 5733, NpcFamily = 170, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1468.168f, Y = 17.342f, Z = 906.155f, HeadingY = -0.217513f, HeadingW = 0.976058f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 83, Health = 6006, NpcFamily = 201, Scale = 100, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1400.896f, Y = 78.410f, Z = 603.650f, HeadingY = -0.863511f, HeadingW = 0.504330f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 79, Health = 5642, NpcFamily = 201, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1453.502f, Y = 61.849f, Z = 669.223f, HeadingY = 0.604662f, HeadingW = 0.796482f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1454.249f, Y = 61.853f, Z = 669.435f, HeadingY = 0.605328f, HeadingW = 0.795976f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1406.344f, Y = 64.935f, Z = 666.864f, HeadingY = -0.684810f, HeadingW = 0.728721f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Nodor", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1423.707f, Y = 67.026f, Z = 610.568f, HeadingY = -0.757420f, HeadingW = 0.652929f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Weaver of Decay", PlayfieldId = 4540, Side = 3, MonsterData = 209354, Level = 101, Health = 8392, NpcFamily = 183, Scale = 100, RunSpeed = 349, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1414.277f, Y = 45.245f, Z = 787.676f, HeadingY = -0.134058f, HeadingW = 0.989720f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 76, Health = 5368, NpcFamily = 172, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1362.600f, Y = 68.525f, Z = 692.134f, HeadingY = -0.714029f, HeadingW = 0.691460f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1359.979f, Y = 70.412f, Z = 750.891f, HeadingY = -0.388367f, HeadingW = 0.921505f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1390.570f, Y = 79.453f, Z = 769.912f, HeadingY = -0.983807f, HeadingW = 0.179229f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1348.408f, Y = 70.181f, Z = 874.383f, HeadingY = -0.337943f, HeadingW = 0.941166f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 75, Health = 5277, NpcFamily = 201, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1432.177f, Y = 68.495f, Z = 825.380f, HeadingY = -0.673289f, HeadingW = 0.739380f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1433.589f, Y = 78.515f, Z = 819.755f, HeadingY = 0.631986f, HeadingW = 0.774980f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1447.190f, Y = 65.231f, Z = 832.982f, HeadingY = 0.156898f, HeadingW = 0.987615f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1379.451f, Y = 69.386f, Z = 838.100f, HeadingY = 0.698503f, HeadingW = 0.715607f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 79, Health = 5642, NpcFamily = 201, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1384.198f, Y = 80.162f, Z = 798.506f, HeadingY = -0.542993f, HeadingW = 0.839737f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1424.603f, Y = 67.953f, Z = 580.337f, HeadingY = -0.945050f, HeadingW = 0.326927f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 85, Health = 6188, NpcFamily = 201, Scale = 100, RunSpeed = 303, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1400.650f, Y = 77.604f, Z = 615.467f, HeadingY = 0.792199f, HeadingW = 0.610263f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1400.356f, Y = 78.410f, Z = 599.186f, HeadingY = 0.976540f, HeadingW = 0.215335f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 79, Health = 5642, NpcFamily = 201, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1428.249f, Y = 67.374f, Z = 590.163f, HeadingY = 0.108948f, HeadingW = 0.994047f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1425.145f, Y = 68.002f, Z = 584.107f, HeadingY = 0.346891f, HeadingW = 0.937905f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 51, Health = 3092, NpcFamily = 186, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1449.862f, Y = 3.000f, Z = 526.712f, HeadingY = -0.344544f, HeadingW = 0.938770f, Textures = null, Meshes = null },
                new MobSlot { Name = "Insidious Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 88, Health = 6461, NpcFamily = 170, Scale = 100, RunSpeed = 314, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1559.968f, Y = 38.760f, Z = 596.590f, HeadingY = -0.835553f, HeadingW = 0.549410f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 78, Health = 5551, NpcFamily = 172, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1430.372f, Y = 63.254f, Z = 541.969f, HeadingY = -0.917365f, HeadingW = 0.391808f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 80, Health = 5733, NpcFamily = 172, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1435.599f, Y = 62.893f, Z = 540.054f, HeadingY = 0.755309f, HeadingW = 0.646114f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 81, Health = 5824, NpcFamily = 172, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1435.464f, Y = 63.476f, Z = 536.698f, HeadingY = 0.779727f, HeadingW = 0.618318f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1416.144f, Y = 68.013f, Z = 569.955f, HeadingY = 0.392737f, HeadingW = 0.919651f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1431.335f, Y = 24.905f, Z = 494.818f, HeadingY = -0.246601f, HeadingW = 0.969117f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1429.753f, Y = 24.905f, Z = 493.993f, HeadingY = 0.823213f, HeadingW = 0.567733f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1373.177f, Y = 95.943f, Z = 561.841f, HeadingY = -0.632225f, HeadingW = 0.774785f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1362.696f, Y = 33.405f, Z = 456.993f, HeadingY = -0.816545f, HeadingW = 0.577282f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1363.836f, Y = 33.405f, Z = 455.980f, HeadingY = -0.346507f, HeadingW = 0.938047f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 51, Health = 3092, NpcFamily = 186, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1394.191f, Y = 15.459f, Z = 472.412f, HeadingY = 0.714774f, HeadingW = 0.699356f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1395.643f, Y = 23.205f, Z = 486.162f, HeadingY = -0.948969f, HeadingW = 0.315369f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1394.426f, Y = 23.205f, Z = 485.578f, HeadingY = 0.989358f, HeadingW = 0.145505f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 53, Health = 3274, NpcFamily = 202, Scale = 200, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1338.648f, Y = 21.305f, Z = 453.845f, HeadingY = -0.161214f, HeadingW = 0.986919f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 52, Health = 3183, NpcFamily = 202, Scale = 200, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1341.825f, Y = 21.255f, Z = 446.435f, HeadingY = -0.945769f, HeadingW = 0.324840f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 52, Health = 3183, NpcFamily = 202, Scale = 200, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1325.500f, Y = 18.985f, Z = 419.800f, HeadingY = -0.969186f, HeadingW = 0.246330f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 52, Health = 3183, NpcFamily = 202, Scale = 200, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1347.254f, Y = 44.525f, Z = 473.138f, HeadingY = -0.984525f, HeadingW = 0.175244f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1340.899f, Y = 33.439f, Z = 504.500f, HeadingY = 0.978365f, HeadingW = 0.206887f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 50, Health = 3000, NpcFamily = 186, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1345.300f, Y = 34.246f, Z = 512.700f, HeadingY = 0.063560f, HeadingW = 0.997978f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 52, Health = 3183, NpcFamily = 186, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1338.599f, Y = 34.388f, Z = 481.000f, HeadingY = 0.550316f, HeadingW = 0.834957f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 53, Health = 3274, NpcFamily = 202, Scale = 200, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1319.099f, Y = 19.015f, Z = 423.900f, HeadingY = -0.960244f, HeadingW = 0.279162f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4540, Side = 3, MonsterData = 209173, Level = 51, Health = 1855, NpcFamily = 200, Scale = 100, RunSpeed = 227, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1289.160f, Y = 8.986f, Z = 400.278f, HeadingY = 0.939431f, HeadingW = 0.324685f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 95, Health = 7438, NpcFamily = 175, Scale = 125, RunSpeed = 334, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1334.928f, Y = 3.707f, Z = 375.851f, HeadingY = -0.347197f, HeadingW = 0.937792f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1340.101f, Y = 25.805f, Z = 370.772f, HeadingY = -0.241477f, HeadingW = 0.970406f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1338.858f, Y = 25.805f, Z = 371.735f, HeadingY = 0.997347f, HeadingW = 0.072796f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 53, Health = 3274, NpcFamily = 202, Scale = 200, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1249.714f, Y = 20.685f, Z = 440.604f, HeadingY = -0.480977f, HeadingW = 0.876733f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 53, Health = 3274, NpcFamily = 202, Scale = 200, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1257.752f, Y = 20.705f, Z = 446.549f, HeadingY = -0.094451f, HeadingW = 0.995530f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 51, Health = 3092, NpcFamily = 202, Scale = 200, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1254.033f, Y = 16.001f, Z = 446.844f, HeadingY = 1.000000f, HeadingW = 0.000216f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1233.984f, Y = 3.463f, Z = 370.667f, HeadingY = -0.819257f, HeadingW = 0.573426f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1259.882f, Y = 30.105f, Z = 347.663f, HeadingY = -0.932905f, HeadingW = 0.360123f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1261.312f, Y = 30.105f, Z = 347.278f, HeadingY = -0.888100f, HeadingW = 0.459649f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1260.564f, Y = 30.105f, Z = 347.922f, HeadingY = -0.994455f, HeadingW = 0.105161f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1296.116f, Y = 27.605f, Z = 333.850f, HeadingY = -0.185154f, HeadingW = 0.982709f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1297.716f, Y = 27.605f, Z = 333.594f, HeadingY = 0.364285f, HeadingW = 0.931287f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1296.961f, Y = 27.605f, Z = 334.127f, HeadingY = 0.979762f, HeadingW = 0.200168f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1314.862f, Y = 19.905f, Z = 349.489f, HeadingY = 0.938325f, HeadingW = 0.345755f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1315.696f, Y = 19.905f, Z = 348.979f, HeadingY = 0.318830f, HeadingW = 0.947812f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1313.985f, Y = 19.905f, Z = 349.205f, HeadingY = -0.934425f, HeadingW = 0.356160f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1231.014f, Y = 4.715f, Z = 429.772f, HeadingY = -0.356355f, HeadingW = 0.934351f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1231.437f, Y = 29.905f, Z = 376.391f, HeadingY = 0.047889f, HeadingW = 0.998853f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1232.185f, Y = 29.905f, Z = 377.017f, HeadingY = 0.974544f, HeadingW = 0.224197f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1232.731f, Y = 29.905f, Z = 376.342f, HeadingY = 0.531363f, HeadingW = 0.847144f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1226.817f, Y = 27.205f, Z = 424.042f, HeadingY = -0.989731f, HeadingW = 0.142940f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1227.927f, Y = 27.205f, Z = 423.138f, HeadingY = 0.691713f, HeadingW = 0.722173f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1233.235f, Y = 25.405f, Z = 428.736f, HeadingY = 0.522385f, HeadingW = 0.852710f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1234.375f, Y = 25.405f, Z = 427.763f, HeadingY = 0.999932f, HeadingW = 0.011675f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1219.554f, Y = 2.329f, Z = 389.580f, HeadingY = -0.994539f, HeadingW = 0.104367f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shore Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 80, Health = 5733, NpcFamily = 175, Scale = 125, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1240.002f, Y = 6.298f, Z = 469.499f, HeadingY = 0.764535f, HeadingW = 0.644582f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1267.465f, Y = 20.481f, Z = 481.425f, HeadingY = 0.173817f, HeadingW = 0.984778f, Textures = null, Meshes = null },
                new MobSlot { Name = "Craig-Or", PlayfieldId = 4540, Side = 2, MonsterData = 208640, Level = 51, Health = 3092, NpcFamily = 202, Scale = 200, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1251.399f, Y = 35.625f, Z = 480.300f, HeadingY = 0.811092f, HeadingW = 0.584919f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 52, Health = 3183, NpcFamily = 186, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1257.900f, Y = 28.407f, Z = 487.300f, HeadingY = 0.776540f, HeadingW = 0.630067f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1256.200f, Y = 30.877f, Z = 517.400f, HeadingY = -0.560224f, HeadingW = 0.828341f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 53, Health = 3274, NpcFamily = 175, Scale = 125, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1251.297f, Y = 19.238f, Z = 507.528f, HeadingY = 0.999998f, HeadingW = 0.001895f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1295.084f, Y = 20.410f, Z = 547.339f, HeadingY = 0.849040f, HeadingW = 0.528329f, Textures = null, Meshes = null },
                new MobSlot { Name = "Yuttos Elysium Geosurvey Dog", PlayfieldId = 4540, Side = 3, MonsterData = 209173, Level = 52, Health = 1910, NpcFamily = 200, Scale = 100, RunSpeed = 232, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1336.968f, Y = 28.410f, Z = 531.790f, HeadingY = 0.999638f, HeadingW = 0.026900f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 51, Health = 3092, NpcFamily = 186, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1249.101f, Y = 34.085f, Z = 532.200f, HeadingY = 0.636011f, HeadingW = 0.771680f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 84, Health = 6097, NpcFamily = 201, Scale = 100, RunSpeed = 299, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1388.524f, Y = 84.155f, Z = 553.695f, HeadingY = 0.815166f, HeadingW = 0.579228f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1352.211f, Y = 68.075f, Z = 569.298f, HeadingY = -0.969141f, HeadingW = 0.246507f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1291.149f, Y = 25.477f, Z = 596.881f, HeadingY = -0.079279f, HeadingW = 0.990683f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1322.332f, Y = 26.970f, Z = 594.430f, HeadingY = -0.984281f, HeadingW = 0.176612f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1399.622f, Y = 74.935f, Z = 560.919f, HeadingY = 0.771273f, HeadingW = 0.636504f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 83, Health = 6006, NpcFamily = 201, Scale = 100, RunSpeed = 295, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1248.325f, Y = 107.620f, Z = 567.194f, HeadingY = -0.685689f, HeadingW = 0.727895f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1318.211f, Y = 25.085f, Z = 621.509f, HeadingY = -0.044131f, HeadingW = 0.999026f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1287.493f, Y = 32.570f, Z = 623.448f, HeadingY = 0.776779f, HeadingW = 0.629773f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 52, Health = 3183, NpcFamily = 186, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1289.638f, Y = 26.165f, Z = 600.737f, HeadingY = 0.807533f, HeadingW = 0.589822f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 56, Health = 3547, NpcFamily = 186, Scale = 100, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1318.511f, Y = 25.817f, Z = 600.780f, HeadingY = 0.809280f, HeadingW = 0.587423f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 52, Health = 3183, NpcFamily = 172, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1327.938f, Y = 39.831f, Z = 601.452f, HeadingY = -0.150521f, HeadingW = 0.976065f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 77, Health = 5460, NpcFamily = 172, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1275.778f, Y = 51.229f, Z = 640.560f, HeadingY = -0.991100f, HeadingW = 0.075303f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1271.929f, Y = 70.267f, Z = 675.553f, HeadingY = 0.876090f, HeadingW = 0.482147f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 80, Health = 5733, NpcFamily = 172, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1352.679f, Y = 71.963f, Z = 680.313f, HeadingY = 0.148943f, HeadingW = 0.948898f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 76, Health = 5368, NpcFamily = 172, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1345.969f, Y = 66.626f, Z = 671.741f, HeadingY = 0.595612f, HeadingW = 0.631472f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 53, Health = 3274, NpcFamily = 172, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1295.491f, Y = 30.225f, Z = 649.932f, HeadingY = -0.839358f, HeadingW = 0.509624f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1305.304f, Y = 28.637f, Z = 656.792f, HeadingY = -0.320762f, HeadingW = 0.947160f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1304.945f, Y = 46.973f, Z = 658.203f, HeadingY = -0.471800f, HeadingW = 0.881706f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1315.119f, Y = 43.070f, Z = 664.118f, HeadingY = -0.436898f, HeadingW = 0.899511f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1323.490f, Y = 32.203f, Z = 646.294f, HeadingY = -0.654219f, HeadingW = 0.756305f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1321.212f, Y = 36.117f, Z = 655.888f, HeadingY = 0.994858f, HeadingW = 0.101280f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1327.988f, Y = 43.464f, Z = 660.276f, HeadingY = -0.587805f, HeadingW = 0.809003f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 78, Health = 5551, NpcFamily = 172, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1271.545f, Y = 69.454f, Z = 683.743f, HeadingY = 0.958993f, HeadingW = 0.143366f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1312.995f, Y = 25.972f, Z = 710.584f, HeadingY = 0.999283f, HeadingW = 0.037850f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 76, Health = 5368, NpcFamily = 172, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1345.645f, Y = 79.083f, Z = 720.990f, HeadingY = -0.330990f, HeadingW = 0.931219f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 51, Health = 3092, NpcFamily = 172, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1307.505f, Y = 26.335f, Z = 698.695f, HeadingY = 0.252335f, HeadingW = 0.965092f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 52, Health = 3183, NpcFamily = 172, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1315.646f, Y = 26.746f, Z = 716.323f, HeadingY = -0.818945f, HeadingW = 0.566396f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1292.984f, Y = 28.692f, Z = 708.338f, HeadingY = -0.298400f, HeadingW = 0.954441f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1294.325f, Y = 27.044f, Z = 718.337f, HeadingY = -0.148931f, HeadingW = 0.988848f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1291.736f, Y = 36.157f, Z = 714.807f, HeadingY = 0.155170f, HeadingW = 0.987888f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1335.786f, Y = 35.572f, Z = 717.330f, HeadingY = -0.524244f, HeadingW = 0.851568f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1335.848f, Y = 32.614f, Z = 707.563f, HeadingY = 0.156640f, HeadingW = 0.986406f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1325.625f, Y = 24.851f, Z = 690.413f, HeadingY = 0.921705f, HeadingW = 0.387892f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 50, Health = 3000, NpcFamily = 201, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1335.032f, Y = 31.693f, Z = 697.497f, HeadingY = 0.999367f, HeadingW = 0.035588f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 81, Health = 5824, NpcFamily = 201, Scale = 100, RunSpeed = 288, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1277.922f, Y = 60.591f, Z = 744.395f, HeadingY = -0.995435f, HeadingW = 0.095439f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 50, Health = 3000, NpcFamily = 201, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1275.720f, Y = 40.822f, Z = 756.383f, HeadingY = -0.517159f, HeadingW = 0.855890f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1251.706f, Y = 37.618f, Z = 756.654f, HeadingY = -0.900294f, HeadingW = 0.435283f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1279.269f, Y = 58.997f, Z = 726.012f, HeadingY = 0.181617f, HeadingW = 0.983369f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1272.443f, Y = 41.003f, Z = 757.796f, HeadingY = 0.552615f, HeadingW = 0.833436f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1272.337f, Y = 39.875f, Z = 755.721f, HeadingY = 0.666611f, HeadingW = 0.745406f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1300.431f, Y = 37.742f, Z = 724.810f, HeadingY = -0.118767f, HeadingW = 0.992922f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1316.552f, Y = 42.395f, Z = 727.196f, HeadingY = -0.715174f, HeadingW = 0.698947f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1284.985f, Y = 38.278f, Z = 741.019f, HeadingY = 0.422278f, HeadingW = 0.906466f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1287.563f, Y = 36.379f, Z = 734.577f, HeadingY = -0.091201f, HeadingW = 0.995833f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1292.980f, Y = 36.972f, Z = 721.844f, HeadingY = -0.067928f, HeadingW = 0.997690f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 50, Health = 3000, NpcFamily = 201, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1296.157f, Y = 27.171f, Z = 722.885f, HeadingY = -0.092169f, HeadingW = 0.995743f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 50, Health = 3000, NpcFamily = 201, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1311.629f, Y = 31.319f, Z = 753.326f, HeadingY = 0.999912f, HeadingW = 0.013246f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1311.250f, Y = 36.758f, Z = 741.668f, HeadingY = -0.902501f, HeadingW = 0.430688f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1302.507f, Y = 37.432f, Z = 756.683f, HeadingY = 0.936316f, HeadingW = 0.351160f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1299.655f, Y = 37.184f, Z = 754.126f, HeadingY = 0.967880f, HeadingW = 0.251414f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1302.900f, Y = 36.354f, Z = 753.227f, HeadingY = 0.958673f, HeadingW = 0.284509f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1287.335f, Y = 36.449f, Z = 738.585f, HeadingY = 0.214377f, HeadingW = 0.976751f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1314.297f, Y = 42.343f, Z = 740.658f, HeadingY = -0.919139f, HeadingW = 0.393932f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1284.709f, Y = 35.942f, Z = 754.277f, HeadingY = 0.893933f, HeadingW = 0.448200f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 52, Health = 3183, NpcFamily = 186, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1315.640f, Y = 35.650f, Z = 744.879f, HeadingY = 0.994928f, HeadingW = 0.100588f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 51, Health = 3092, NpcFamily = 172, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1326.472f, Y = 37.338f, Z = 754.050f, HeadingY = 0.969243f, HeadingW = 0.123456f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 50, Health = 3000, NpcFamily = 201, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1327.252f, Y = 40.086f, Z = 725.841f, HeadingY = -0.711820f, HeadingW = 0.702362f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 76, Health = 5368, NpcFamily = 172, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1240.007f, Y = 63.610f, Z = 741.168f, HeadingY = 0.063549f, HeadingW = 0.996042f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1276.724f, Y = 55.916f, Z = 794.197f, HeadingY = 0.804686f, HeadingW = 0.593701f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 51, Health = 3092, NpcFamily = 186, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1240.451f, Y = 32.409f, Z = 792.035f, HeadingY = -0.136026f, HeadingW = 0.990705f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1286.085f, Y = 55.601f, Z = 773.766f, HeadingY = 0.123090f, HeadingW = 0.992395f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Cur-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 53, Health = 3274, NpcFamily = 201, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1271.927f, Y = 51.940f, Z = 771.512f, HeadingY = 0.047016f, HeadingW = 0.998894f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 82, Health = 5915, NpcFamily = 201, Scale = 100, RunSpeed = 291, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1290.173f, Y = 55.654f, Z = 783.499f, HeadingY = -0.059344f, HeadingW = 0.998238f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1298.511f, Y = 43.650f, Z = 777.278f, HeadingY = 0.448851f, HeadingW = 0.878970f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 53, Health = 3274, NpcFamily = 186, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1283.633f, Y = 27.202f, Z = 760.424f, HeadingY = -0.738548f, HeadingW = 0.674200f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1321.282f, Y = 46.465f, Z = 783.652f, HeadingY = -0.915437f, HeadingW = 0.260193f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1256.586f, Y = 56.112f, Z = 826.451f, HeadingY = -0.688913f, HeadingW = 0.724844f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 79, Health = 5642, NpcFamily = 172, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1252.028f, Y = 58.410f, Z = 812.687f, HeadingY = 0.644168f, HeadingW = 0.758642f, Textures = null, Meshes = null },
                new MobSlot { Name = "Gelid Eremite", PlayfieldId = 4540, Side = 3, MonsterData = 209158, Level = 54, Health = 3365, NpcFamily = 186, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1321.759f, Y = 27.841f, Z = 806.254f, HeadingY = 0.169942f, HeadingW = 0.985454f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1302.233f, Y = 67.832f, Z = 832.848f, HeadingY = -0.869387f, HeadingW = 0.494132f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 76, Health = 5368, NpcFamily = 172, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1315.750f, Y = 70.894f, Z = 870.247f, HeadingY = 0.956907f, HeadingW = 0.233870f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1309.544f, Y = 68.924f, Z = 843.702f, HeadingY = 0.797236f, HeadingW = 0.603667f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1344.790f, Y = 70.264f, Z = 871.654f, HeadingY = -0.430773f, HeadingW = 0.902460f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1366.704f, Y = 23.635f, Z = 855.774f, HeadingY = -0.947875f, HeadingW = 0.318643f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1363.596f, Y = 23.941f, Z = 860.115f, HeadingY = -0.997328f, HeadingW = 0.073054f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dasa", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 60, Health = 3911, NpcFamily = 201, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1361.862f, Y = 23.869f, Z = 855.609f, HeadingY = -0.997765f, HeadingW = 0.066816f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 51, Health = 3092, NpcFamily = 172, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1367.478f, Y = 23.665f, Z = 854.922f, HeadingY = -0.880616f, HeadingW = 0.471205f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 52, Health = 3183, NpcFamily = 172, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1362.914f, Y = 23.573f, Z = 861.141f, HeadingY = -0.873242f, HeadingW = 0.467363f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1381.865f, Y = 69.642f, Z = 845.309f, HeadingY = -0.427541f, HeadingW = 0.903996f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1354.386f, Y = 78.810f, Z = 894.388f, HeadingY = 0.840176f, HeadingW = 0.542314f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1367.259f, Y = 76.234f, Z = 911.136f, HeadingY = 0.360621f, HeadingW = 0.932712f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1415.948f, Y = 17.210f, Z = 916.971f, HeadingY = -0.041678f, HeadingW = 0.999131f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1419.926f, Y = 17.210f, Z = 902.901f, HeadingY = 0.365311f, HeadingW = 0.930886f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1432.344f, Y = 17.210f, Z = 919.481f, HeadingY = 0.182654f, HeadingW = 0.983177f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 77, Health = 5460, NpcFamily = 211, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1427.300f, Y = 20.722f, Z = 906.069f, HeadingY = 0.062747f, HeadingW = 0.998030f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1399.232f, Y = 26.158f, Z = 952.346f, HeadingY = 0.689277f, HeadingW = 0.724498f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1398.679f, Y = 26.152f, Z = 947.087f, HeadingY = -0.768485f, HeadingW = 0.639868f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1427.577f, Y = 22.333f, Z = 932.713f, HeadingY = -0.975268f, HeadingW = 0.221025f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1424.696f, Y = 22.351f, Z = 939.678f, HeadingY = 0.997675f, HeadingW = 0.068158f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1411.452f, Y = 17.210f, Z = 935.195f, HeadingY = -0.040184f, HeadingW = 0.999192f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 54, Health = 3365, NpcFamily = 201, Scale = 100, RunSpeed = 186, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1332.849f, Y = 23.699f, Z = 944.702f, HeadingY = -0.976361f, HeadingW = 0.216147f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1331.584f, Y = 23.610f, Z = 948.622f, HeadingY = -0.985081f, HeadingW = 0.172089f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Dasa", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 60, Health = 3911, NpcFamily = 201, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1328.182f, Y = 23.232f, Z = 945.697f, HeadingY = -0.953474f, HeadingW = 0.301474f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1331.472f, Y = 23.610f, Z = 949.605f, HeadingY = -0.882620f, HeadingW = 0.470087f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 53, Health = 3274, NpcFamily = 172, Scale = 100, RunSpeed = 183, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1333.222f, Y = 23.755f, Z = 943.815f, HeadingY = -0.879208f, HeadingW = 0.471241f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1399.110f, Y = 26.161f, Z = 952.400f, HeadingY = -0.957081f, HeadingW = 0.289820f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1398.666f, Y = 26.151f, Z = 952.206f, HeadingY = -0.402095f, HeadingW = 0.915598f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1399.360f, Y = 26.148f, Z = 947.159f, HeadingY = 0.998836f, HeadingW = 0.048244f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 65, Health = 4367, NpcFamily = 201, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1386.031f, Y = 17.210f, Z = 951.964f, HeadingY = -0.738540f, HeadingW = 0.674210f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 64, Health = 4276, NpcFamily = 201, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1386.512f, Y = 17.210f, Z = 945.926f, HeadingY = -0.736292f, HeadingW = 0.676664f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1398.918f, Y = 26.152f, Z = 946.964f, HeadingY = -0.693063f, HeadingW = 0.720877f, Textures = null, Meshes = null },
                new MobSlot { Name = "Waning Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 80, Health = 17197, NpcFamily = 207, Scale = 40, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1436.580f, Y = 22.410f, Z = 938.150f, HeadingY = 0.387676f, HeadingW = 0.921796f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1425.485f, Y = 22.410f, Z = 948.621f, HeadingY = 0.699705f, HeadingW = 0.714432f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 60, Health = 3911, NpcFamily = 170, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1417.347f, Y = 21.458f, Z = 952.575f, HeadingY = -0.024110f, HeadingW = 0.999709f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1425.178f, Y = 22.410f, Z = 953.555f, HeadingY = 0.699776f, HeadingW = 0.714362f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1423.378f, Y = 22.410f, Z = 947.640f, HeadingY = -0.026159f, HeadingW = 0.999658f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1421.072f, Y = 17.646f, Z = 924.801f, HeadingY = 0.359408f, HeadingW = 0.933181f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1439.802f, Y = 22.410f, Z = 948.760f, HeadingY = 0.545728f, HeadingW = 0.837962f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1432.697f, Y = 31.485f, Z = 935.214f, HeadingY = 0.305296f, HeadingW = 0.952257f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1506.688f, Y = 80.948f, Z = 887.858f, HeadingY = 0.355642f, HeadingW = 0.934622f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1467.471f, Y = 25.353f, Z = 903.747f, HeadingY = 0.971296f, HeadingW = 0.237875f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1471.771f, Y = 25.358f, Z = 906.851f, HeadingY = -0.233969f, HeadingW = 0.972244f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1458.761f, Y = 17.210f, Z = 912.238f, HeadingY = -0.041678f, HeadingW = 0.999131f, Textures = null, Meshes = null },
                new MobSlot { Name = "Shifty Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 80, Health = 5733, NpcFamily = 170, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1462.307f, Y = 20.703f, Z = 922.660f, HeadingY = 0.979601f, HeadingW = 0.200950f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1463.666f, Y = 22.410f, Z = 946.234f, HeadingY = 0.795460f, HeadingW = 0.606006f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1445.505f, Y = 22.410f, Z = 936.465f, HeadingY = -0.056933f, HeadingW = 0.998378f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1458.559f, Y = 22.410f, Z = 944.253f, HeadingY = 0.715030f, HeadingW = 0.699094f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1464.265f, Y = 22.410f, Z = 958.614f, HeadingY = -0.666973f, HeadingW = 0.745082f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 75, Health = 5277, NpcFamily = 201, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1478.121f, Y = 80.792f, Z = 865.020f, HeadingY = 0.999633f, HeadingW = 0.027108f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1475.970f, Y = 81.208f, Z = 867.234f, HeadingY = -0.330345f, HeadingW = 0.943860f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1454.368f, Y = 22.815f, Z = 879.662f, HeadingY = 0.100979f, HeadingW = 0.994889f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 64, Health = 4276, NpcFamily = 201, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1471.369f, Y = 16.986f, Z = 894.240f, HeadingY = 0.935403f, HeadingW = 0.353582f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 65, Health = 4367, NpcFamily = 201, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1476.629f, Y = 17.273f, Z = 897.320f, HeadingY = 0.962738f, HeadingW = 0.270435f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1472.043f, Y = 25.351f, Z = 906.197f, HeadingY = 0.559418f, HeadingW = 0.828885f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1467.319f, Y = 25.350f, Z = 904.225f, HeadingY = -0.415379f, HeadingW = 0.909649f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1448.516f, Y = 23.086f, Z = 880.339f, HeadingY = -0.840222f, HeadingW = 0.542242f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1448.462f, Y = 18.721f, Z = 912.412f, HeadingY = 0.777219f, HeadingW = 0.629230f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1449.921f, Y = 22.410f, Z = 945.629f, HeadingY = -0.733644f, HeadingW = 0.679534f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1447.089f, Y = 22.410f, Z = 943.969f, HeadingY = -0.152012f, HeadingW = 0.988379f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1440.268f, Y = 22.410f, Z = 952.339f, HeadingY = 0.754260f, HeadingW = 0.656576f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1476.886f, Y = 17.417f, Z = 934.953f, HeadingY = -0.685734f, HeadingW = 0.727853f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1445.053f, Y = 22.410f, Z = 955.976f, HeadingY = 0.996969f, HeadingW = 0.077797f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1451.455f, Y = 22.410f, Z = 951.682f, HeadingY = -0.768441f, HeadingW = 0.639921f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lost Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 80, Health = 22929, NpcFamily = 207, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1458.969f, Y = 22.410f, Z = 949.303f, HeadingY = -0.708151f, HeadingW = 0.706061f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1463.315f, Y = 22.410f, Z = 947.333f, HeadingY = 0.538454f, HeadingW = 0.842655f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1460.515f, Y = 32.025f, Z = 947.315f, HeadingY = -0.257690f, HeadingW = 0.966228f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1469.076f, Y = 22.200f, Z = 957.833f, HeadingY = 0.261070f, HeadingW = 0.965320f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1443.808f, Y = 22.410f, Z = 944.255f, HeadingY = 0.255210f, HeadingW = 0.966886f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1450.031f, Y = 22.410f, Z = 954.120f, HeadingY = -0.115781f, HeadingW = 0.993275f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1460.455f, Y = 22.325f, Z = 930.414f, HeadingY = -0.008368f, HeadingW = 0.999965f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1447.300f, Y = 22.410f, Z = 955.645f, HeadingY = 0.723701f, HeadingW = 0.690114f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1441.336f, Y = 22.410f, Z = 946.220f, HeadingY = 0.437843f, HeadingW = 0.899051f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1442.214f, Y = 22.410f, Z = 954.753f, HeadingY = 0.597876f, HeadingW = 0.801589f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1451.380f, Y = 22.410f, Z = 948.414f, HeadingY = 0.913698f, HeadingW = 0.406395f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1467.600f, Y = 17.210f, Z = 921.183f, HeadingY = 0.501791f, HeadingW = 0.864989f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1456.043f, Y = 22.410f, Z = 928.505f, HeadingY = 0.926112f, HeadingW = 0.377248f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1392.318f, Y = 17.210f, Z = 964.649f, HeadingY = -0.897499f, HeadingW = 0.441017f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1426.244f, Y = 22.499f, Z = 963.677f, HeadingY = 0.698870f, HeadingW = 0.715249f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1415.781f, Y = 17.210f, Z = 972.450f, HeadingY = 0.861724f, HeadingW = 0.507377f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1429.452f, Y = 17.210f, Z = 999.985f, HeadingY = -0.896948f, HeadingW = 0.442137f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1400.061f, Y = 17.210f, Z = 977.597f, HeadingY = 0.860244f, HeadingW = 0.509882f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1476.528f, Y = 26.156f, Z = 987.334f, HeadingY = -0.959128f, HeadingW = 0.282973f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1445.740f, Y = 22.588f, Z = 975.191f, HeadingY = -0.638139f, HeadingW = 0.769921f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1439.975f, Y = 22.410f, Z = 967.856f, HeadingY = -0.809073f, HeadingW = 0.587708f, Textures = null, Meshes = null },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 58, Health = 3729, NpcFamily = 172, Scale = 100, RunSpeed = 201, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1389.713f, Y = 17.210f, Z = 963.472f, HeadingY = -0.465796f, HeadingW = 0.884892f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1373.426f, Y = 24.058f, Z = 969.092f, HeadingY = 0.806892f, HeadingW = 0.590699f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 59, Health = 3820, NpcFamily = 170, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1431.686f, Y = 22.444f, Z = 964.030f, HeadingY = 0.995686f, HeadingW = 0.092791f, Textures = null, Meshes = null },
                new MobSlot { Name = "Cascading Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 59, Health = 3820, NpcFamily = 170, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1435.267f, Y = 22.410f, Z = 965.942f, HeadingY = -0.524898f, HeadingW = 0.851165f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1407.453f, Y = 17.210f, Z = 994.523f, HeadingY = 0.810220f, HeadingW = 0.586126f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1443.021f, Y = 22.410f, Z = 948.085f, HeadingY = -0.619779f, HeadingW = 0.784776f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1477.026f, Y = 26.152f, Z = 987.939f, HeadingY = 0.739485f, HeadingW = 0.673174f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1476.522f, Y = 26.149f, Z = 987.535f, HeadingY = 0.844083f, HeadingW = 0.536212f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1443.455f, Y = 17.210f, Z = 984.092f, HeadingY = 0.998147f, HeadingW = 0.060851f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1461.407f, Y = 22.410f, Z = 966.069f, HeadingY = -0.955849f, HeadingW = 0.293857f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1457.482f, Y = 22.330f, Z = 968.824f, HeadingY = -0.955289f, HeadingW = 0.295672f, Textures = null, Meshes = null },
                new MobSlot { Name = "Comatosed Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217007, Level = 80, Health = 22929, NpcFamily = 207, Scale = 130, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1440.793f, Y = 22.410f, Z = 961.599f, HeadingY = 0.970307f, HeadingW = 0.241876f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1443.503f, Y = 31.495f, Z = 967.587f, HeadingY = -0.955949f, HeadingW = 0.293532f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1442.272f, Y = 31.495f, Z = 969.303f, HeadingY = -0.076944f, HeadingW = 0.997035f, Textures = null, Meshes = null },
                new MobSlot { Name = "Hai-Tempterus", PlayfieldId = 4540, Side = 1, MonsterData = 209182, Level = 1, Health = 25, NpcFamily = 201, Scale = 135, RunSpeed = 6, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1451.958f, Y = 22.410f, Z = 966.794f, HeadingY = -0.921596f, HeadingW = 0.388151f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crystal Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 76, Health = 5368, NpcFamily = 175, Scale = 125, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1512.532f, Y = 81.546f, Z = 882.427f, HeadingY = 0.789552f, HeadingW = 0.613684f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1501.781f, Y = 17.756f, Z = 983.812f, HeadingY = 0.840168f, HeadingW = 0.542327f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1488.678f, Y = 16.902f, Z = 972.244f, HeadingY = -0.039721f, HeadingW = 0.999211f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1441.449f, Y = 17.210f, Z = 999.996f, HeadingY = 0.860428f, HeadingW = 0.509572f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1508.661f, Y = 81.011f, Z = 886.337f, HeadingY = 0.222930f, HeadingW = 0.974835f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 67, Health = 4549, NpcFamily = 211, Scale = 100, RunSpeed = 235, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1495.461f, Y = 35.695f, Z = 929.580f, HeadingY = 0.297921f, HeadingW = 0.954591f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1519.144f, Y = 26.495f, Z = 937.126f, HeadingY = -0.203050f, HeadingW = 0.979168f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 71, Health = 4913, NpcFamily = 211, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1509.615f, Y = 27.115f, Z = 933.341f, HeadingY = -0.651329f, HeadingW = 0.758795f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1494.161f, Y = 29.465f, Z = 924.726f, HeadingY = -0.546675f, HeadingW = 0.837345f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 78, Health = 5551, NpcFamily = 211, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1518.699f, Y = 29.585f, Z = 958.398f, HeadingY = -0.983754f, HeadingW = 0.179521f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 68, Health = 4640, NpcFamily = 211, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1499.924f, Y = 27.385f, Z = 933.037f, HeadingY = 0.997820f, HeadingW = 0.065995f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 211, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1499.907f, Y = 35.695f, Z = 931.938f, HeadingY = 0.995853f, HeadingW = 0.090976f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 68, Health = 4640, NpcFamily = 211, Scale = 100, RunSpeed = 239, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1509.910f, Y = 25.345f, Z = 959.410f, HeadingY = -0.995905f, HeadingW = 0.090410f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1494.531f, Y = 27.385f, Z = 933.055f, HeadingY = 0.180351f, HeadingW = 0.983602f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 72, Health = 5004, NpcFamily = 211, Scale = 100, RunSpeed = 254, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1493.970f, Y = 27.385f, Z = 929.914f, HeadingY = -0.547307f, HeadingW = 0.836932f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 65, Health = 4367, NpcFamily = 201, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1484.522f, Y = 17.210f, Z = 998.107f, HeadingY = -0.080275f, HeadingW = 0.996773f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1507.909f, Y = 27.655f, Z = 963.854f, HeadingY = -0.000003f, HeadingW = 1.000000f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 76, Health = 5368, NpcFamily = 211, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1514.601f, Y = 29.495f, Z = 963.434f, HeadingY = 0.996452f, HeadingW = 0.084166f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1480.682f, Y = 17.542f, Z = 960.235f, HeadingY = -0.855035f, HeadingW = 0.518571f, Textures = null, Meshes = null },
                new MobSlot { Name = "Wandering Soul", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 75, Health = 10554, NpcFamily = 207, Scale = 70, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1466.437f, Y = 17.454f, Z = 1010.803f, HeadingY = -0.897780f, HeadingW = 0.440444f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 63, Health = 4184, NpcFamily = 201, Scale = 100, RunSpeed = 220, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1410.533f, Y = 30.402f, Z = 1023.446f, HeadingY = -0.938834f, HeadingW = 0.344371f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1437.010f, Y = 72.596f, Z = 1036.812f, HeadingY = 0.250789f, HeadingW = 0.968042f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 64, Health = 4276, NpcFamily = 201, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1408.746f, Y = 30.511f, Z = 1023.197f, HeadingY = 0.968794f, HeadingW = 0.247868f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 61, Health = 4002, NpcFamily = 201, Scale = 100, RunSpeed = 213, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1426.863f, Y = 26.314f, Z = 1032.732f, HeadingY = -0.468620f, HeadingW = 0.883400f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 60, Health = 3911, NpcFamily = 201, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1425.179f, Y = 27.329f, Z = 1035.989f, HeadingY = -0.467894f, HeadingW = 0.883785f, Textures = null, Meshes = new[] { new[] { 1, 209532, 0, 2 } } },
                new MobSlot { Name = "Len-Dosa", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 64, Health = 4276, NpcFamily = 201, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1479.537f, Y = 17.210f, Z = 1001.503f, HeadingY = 0.757235f, HeadingW = 0.653142f, Textures = null, Meshes = null },
                new MobSlot { Name = "Or-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214067, Level = 75, Health = 5277, NpcFamily = 201, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1389.213f, Y = 93.827f, Z = 1014.665f, HeadingY = -0.453387f, HeadingW = 0.891314f, Textures = null, Meshes = new[] { new[] { 1, 234635, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1398.791f, Y = 71.575f, Z = 1026.921f, HeadingY = 0.221987f, HeadingW = 0.975050f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1367.574f, Y = 64.981f, Z = 1013.514f, HeadingY = 0.497025f, HeadingW = 0.867736f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1359.859f, Y = 23.804f, Z = 1002.598f, HeadingY = -0.164979f, HeadingW = 0.986297f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 56, Health = 3547, NpcFamily = 175, Scale = 125, RunSpeed = 194, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1530.245f, Y = 24.060f, Z = 954.065f, HeadingY = -0.785034f, HeadingW = 0.619453f, Textures = null, Meshes = null },
                new MobSlot { Name = "Sadistic Soul Dredge", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 85, Health = 24751, NpcFamily = 207, Scale = 130, RunSpeed = 303, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1543.857f, Y = 43.627f, Z = 1010.609f, HeadingY = 0.902779f, HeadingW = 0.430105f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 77, Health = 5460, NpcFamily = 211, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1552.416f, Y = 36.925f, Z = 946.710f, HeadingY = 0.999874f, HeadingW = 0.015896f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 79, Health = 5642, NpcFamily = 211, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1550.186f, Y = 33.055f, Z = 953.922f, HeadingY = -0.095912f, HeadingW = 0.995390f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 75, Health = 5277, NpcFamily = 211, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1552.946f, Y = 22.107f, Z = 954.009f, HeadingY = -0.651461f, HeadingW = 0.758682f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 77, Health = 5460, NpcFamily = 211, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1548.280f, Y = 22.946f, Z = 950.415f, HeadingY = -0.033382f, HeadingW = 0.999443f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 77, Health = 5460, NpcFamily = 211, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1554.759f, Y = 35.715f, Z = 951.918f, HeadingY = 0.999485f, HeadingW = 0.032087f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1551.690f, Y = 28.985f, Z = 958.920f, HeadingY = 0.083313f, HeadingW = 0.996523f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1534.109f, Y = 32.115f, Z = 954.765f, HeadingY = -0.922671f, HeadingW = 0.385588f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 78, Health = 5551, NpcFamily = 211, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1542.881f, Y = 33.105f, Z = 958.482f, HeadingY = -0.997696f, HeadingW = 0.067836f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 71, Health = 4913, NpcFamily = 211, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1528.748f, Y = 33.215f, Z = 950.245f, HeadingY = -0.420716f, HeadingW = 0.907192f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1522.537f, Y = 26.535f, Z = 945.385f, HeadingY = -0.547505f, HeadingW = 0.836803f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1523.427f, Y = 28.295f, Z = 950.887f, HeadingY = 0.208875f, HeadingW = 0.977942f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 79, Health = 5642, NpcFamily = 211, Scale = 100, RunSpeed = 280, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1544.721f, Y = 32.745f, Z = 951.510f, HeadingY = 0.139969f, HeadingW = 0.990156f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1524.401f, Y = 58.074f, Z = 993.072f, HeadingY = 0.582856f, HeadingW = 0.812575f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 211, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1528.447f, Y = 63.002f, Z = 992.532f, HeadingY = 0.996484f, HeadingW = 0.083782f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1544.991f, Y = 41.119f, Z = 984.571f, HeadingY = 0.929596f, HeadingW = 0.368579f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 71, Health = 4913, NpcFamily = 211, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1536.189f, Y = 57.276f, Z = 997.282f, HeadingY = 0.670558f, HeadingW = 0.741857f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 75, Health = 5277, NpcFamily = 211, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1533.591f, Y = 57.591f, Z = 991.820f, HeadingY = 0.879043f, HeadingW = 0.476743f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1552.247f, Y = 34.415f, Z = 972.681f, HeadingY = 0.458148f, HeadingW = 0.888876f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 211, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1528.606f, Y = 57.066f, Z = 989.436f, HeadingY = -0.859149f, HeadingW = 0.511725f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 72, Health = 5004, NpcFamily = 211, Scale = 100, RunSpeed = 254, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1556.183f, Y = 33.915f, Z = 988.544f, HeadingY = -0.998713f, HeadingW = 0.050724f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 76, Health = 5368, NpcFamily = 211, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1546.427f, Y = 33.415f, Z = 968.560f, HeadingY = -0.978194f, HeadingW = 0.207693f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 71, Health = 4913, NpcFamily = 211, Scale = 100, RunSpeed = 250, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1555.410f, Y = 35.415f, Z = 980.886f, HeadingY = 0.411594f, HeadingW = 0.911367f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 70, Health = 4822, NpcFamily = 211, Scale = 100, RunSpeed = 246, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1556.136f, Y = 40.863f, Z = 998.629f, HeadingY = -0.997789f, HeadingW = 0.066458f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1535.304f, Y = 45.126f, Z = 979.757f, HeadingY = 0.324961f, HeadingW = 0.945727f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 65, Health = 4367, NpcFamily = 211, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1552.732f, Y = 41.247f, Z = 1011.916f, HeadingY = 0.383127f, HeadingW = 0.923696f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 74, Health = 5186, NpcFamily = 211, Scale = 100, RunSpeed = 261, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1532.627f, Y = 56.590f, Z = 1004.774f, HeadingY = 0.259968f, HeadingW = 0.965617f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 73, Health = 5095, NpcFamily = 211, Scale = 100, RunSpeed = 258, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1527.652f, Y = 57.717f, Z = 1006.057f, HeadingY = 0.162900f, HeadingW = 0.986643f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 62, Health = 4093, NpcFamily = 211, Scale = 100, RunSpeed = 216, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1545.482f, Y = 43.473f, Z = 1018.494f, HeadingY = 0.700043f, HeadingW = 0.714101f, Textures = null, Meshes = null },
                new MobSlot { Name = "Elysian Spirit Hunter", PlayfieldId = 4540, Side = 3, MonsterData = 209215, Level = 65, Health = 4367, NpcFamily = 211, Scale = 100, RunSpeed = 228, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1535.806f, Y = 41.077f, Z = 1024.529f, HeadingY = -0.440651f, HeadingW = 0.897679f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 59, Health = 3820, NpcFamily = 175, Scale = 125, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1559.280f, Y = 32.963f, Z = 1015.128f, HeadingY = -0.872571f, HeadingW = 0.488487f, Textures = null, Meshes = null },
                new MobSlot { Name = "Slinking Spirit", PlayfieldId = 4540, Side = 3, MonsterData = 217022, Level = 64, Health = 4276, NpcFamily = 170, Scale = 100, RunSpeed = 224, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1564.553f, Y = 43.788f, Z = 941.663f, HeadingY = -0.616470f, HeadingW = 0.787378f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1582.592f, Y = 102.612f, Z = 934.533f, HeadingY = -0.043759f, HeadingW = 0.999042f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1580.729f, Y = 102.464f, Z = 931.157f, HeadingY = 0.956574f, HeadingW = 0.291489f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 76, Health = 5368, NpcFamily = 201, Scale = 100, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1599.156f, Y = 90.330f, Z = 1017.092f, HeadingY = -0.283029f, HeadingW = 0.959111f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1578.379f, Y = 82.779f, Z = 1038.993f, HeadingY = 0.969875f, HeadingW = 0.243605f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1567.270f, Y = 25.786f, Z = 1006.264f, HeadingY = -0.295419f, HeadingW = 0.955368f, Textures = null, Meshes = null },
                new MobSlot { Name = "Crystal Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 76, Health = 5368, NpcFamily = 175, Scale = 125, RunSpeed = 269, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1635.509f, Y = 86.514f, Z = 957.160f, HeadingY = 0.975548f, HeadingW = 0.219787f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 75, Health = 5277, NpcFamily = 201, Scale = 100, RunSpeed = 265, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1527.989f, Y = 82.008f, Z = 1072.926f, HeadingY = -0.541486f, HeadingW = 0.840710f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Or-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 236640, Level = 59, Health = 3820, NpcFamily = 201, Scale = 100, RunSpeed = 205, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1527.154f, Y = 22.447f, Z = 1050.744f, HeadingY = -0.117378f, HeadingW = 0.993087f, Textures = null, Meshes = new[] { new[] { 1, 209541, 0, 2 } } },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 80, Health = 5733, NpcFamily = 201, Scale = 100, RunSpeed = 284, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1507.999f, Y = 28.984f, Z = 865.034f, HeadingY = 0.771727f, HeadingW = 0.635954f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Lochquid", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 78, Health = 5551, NpcFamily = 201, Scale = 100, RunSpeed = 276, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1500.649f, Y = 25.092f, Z = 867.461f, HeadingY = -0.322306f, HeadingW = 0.946635f, Textures = null, Meshes = null },
                new MobSlot { Name = "Coral Rafter", PlayfieldId = 4540, Side = 3, MonsterData = 212846, Level = 57, Health = 3638, NpcFamily = 175, Scale = 125, RunSpeed = 198, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1374.256f, Y = 23.872f, Z = 992.952f, HeadingY = 0.847188f, HeadingW = 0.531293f, Textures = null, Meshes = null },
                new MobSlot { Name = "Majestic Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 77, Health = 5460, NpcFamily = 172, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1341.284f, Y = 75.554f, Z = 893.691f, HeadingY = 0.001364f, HeadingW = 0.995130f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Karat", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 77, Health = 5460, NpcFamily = 201, Scale = 100, RunSpeed = 273, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1342.317f, Y = 65.160f, Z = 976.767f, HeadingY = -0.951951f, HeadingW = 0.306250f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 51, Health = 3092, NpcFamily = 201, Scale = 100, RunSpeed = 175, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1350.191f, Y = 23.670f, Z = 994.372f, HeadingY = -0.666221f, HeadingW = 0.745755f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 52, Health = 3183, NpcFamily = 172, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1349.901f, Y = 23.610f, Z = 993.128f, HeadingY = -0.442599f, HeadingW = 0.896720f, Textures = null, Meshes = null },
                new MobSlot { Name = "El-Mada", PlayfieldId = 4540, Side = 1, MonsterData = 214083, Level = 52, Health = 3183, NpcFamily = 201, Scale = 100, RunSpeed = 179, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1353.347f, Y = 24.834f, Z = 1001.536f, HeadingY = -0.839457f, HeadingW = 0.543426f, Textures = null, Meshes = new[] { new[] { 1, 234632, 0, 2 } } },
                new MobSlot { Name = "Lucent Silvertail", PlayfieldId = 4540, Side = 3, MonsterData = 208929, Level = 50, Health = 3000, NpcFamily = 172, Scale = 100, RunSpeed = 171, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1354.463f, Y = 24.832f, Z = 1002.623f, HeadingY = -0.543951f, HeadingW = 0.836176f, Textures = null, Meshes = null },
                new MobSlot { Name = "Len-Dasa", PlayfieldId = 4540, Side = 1, MonsterData = 214072, Level = 60, Health = 3911, NpcFamily = 201, Scale = 100, RunSpeed = 209, CharacterFlags = 268964353, VisualFlags = 31, HeadMesh = 0, X = 1348.300f, Y = 24.099f, Z = 1000.213f, HeadingY = -0.810384f, HeadingW = 0.585900f, Textures = null, Meshes = null },
            };

        private static bool SupportsPlayfield(int playfieldInstance)
        {
            return playfieldInstance == ElysiumEastPlayfieldId
                   || playfieldInstance == ElysiumSouthPlayfieldId;
        }

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Arachno Frigida", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Arachno_Frigida.Clone();
                return true;
            }
            if (string.Equals(name, "Arachno Gelida", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Arachno_Gelida.Clone();
                return true;
            }
            if (string.Equals(name, "Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Arcorash.Clone();
                return true;
            }
            if (string.Equals(name, "CEO Guardian", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_CEO_Guardian.Clone();
                return true;
            }
            if (string.Equals(name, "Callous Mortiig", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Callous_Mortiig.Clone();
                return true;
            }
            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Cascading_Spirit.Clone();
                return true;
            }
            if (string.Equals(name, "Chill Spider", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Chill_Spider.Clone();
                return true;
            }
            if (string.Equals(name, "Cur-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Cur_Dosa.Clone();
                return true;
            }
            if (string.Equals(name, "Cur-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Cur_Lendar.Clone();
                return true;
            }
            if (string.Equals(name, "Deceitful Weaver", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Deceitful_Weaver.Clone();
                return true;
            }
            if (string.Equals(name, "Devoted Enel Ilad-Ulma", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Devoted_Enel_Ilad_Ulma.Clone();
                return true;
            }
            if (string.Equals(name, "Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Devourer_of_Life.Clone();
                return true;
            }
            if (string.Equals(name, "El-Karat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_El_Karat.Clone();
                return true;
            }
            if (string.Equals(name, "El-Mada", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_El_Mada.Clone();
                return true;
            }
            if (string.Equals(name, "El-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_El_Nodor.Clone();
                return true;
            }
            if (string.Equals(name, "Elysian Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Elysian_Spirit_Hunter.Clone();
                return true;
            }
            if (string.Equals(name, "Flagging Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Flagging_Arcorash.Clone();
                return true;
            }
            if (string.Equals(name, "Insidious Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Insidious_Spirit.Clone();
                return true;
            }
            if (string.Equals(name, "Kolaana", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Kolaana.Clone();
                return true;
            }
            if (string.Equals(name, "Kolaana-Behn", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Kolaana_Behn.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Dasa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Len_Dasa.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Len_Dosa.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Len_Lendar.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Lochquid", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Len_Lochquid.Clone();
                return true;
            }
            if (string.Equals(name, "Lost Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Lost_Soul.Clone();
                return true;
            }
            if (string.Equals(name, "One With A Graceful Neck", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_One_With_A_Graceful_Neck.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Karat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Karat.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Mada.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of Flaming Barrels", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Mada_of_Flaming_Barrels.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Mada_of_Preservation.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of the Furious Fists", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Mada_of_the_Furious_Fists.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Or_Nodor.Clone();
                return true;
            }
            if (string.Equals(name, "Sadistic Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Sadistic_Soul_Dredge.Clone();
                return true;
            }
            if (string.Equals(name, "Shades Of Grey", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Shades_Of_Grey.Clone();
                return true;
            }
            if (string.Equals(name, "Shifty Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Shifty_Spirit.Clone();
                return true;
            }
            if (string.Equals(name, "Slinking Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Slinking_Spirit.Clone();
                return true;
            }
            if (string.Equals(name, "Wandering Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Wandering_Soul.Clone();
                return true;
            }
            if (string.Equals(name, "Waning Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Waning_Soul.Clone();
                return true;
            }
            if (string.Equals(name, "Yuttos Elysium Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])ExtTex_Yuttos_Elysium_Geosurvey_Dog.Clone();
                return true;
            }
            data = null;
            return false;
        }

        internal static bool UsesPetScfuFlags(string name)
        {
            if (string.Equals(name, "Aniuchach", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arachno Frigida", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arachno Gelida", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Brisk Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "CEO Guardian", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cagey Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Callous Mortiig", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Carlo Pinnetti", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Chill Spider", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Coral Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Craig-Or of Gear & Ammo", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Craig-Or of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Craig-Or of Protection", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Crystal Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cur-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cur-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Deceitful Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Devoted Enel Ilad-Ulma", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Karat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Mada", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Elysian Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Flagging Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Follower Yutt-Ixi Shere", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Guard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Guard - Elmo Fitz", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Hiathlin Lookout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Ichiachich", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Inicha", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Insidious Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Kolaana", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Kolaana-Behn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Dasa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Lochquid", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Lost Hiathlin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Lost Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah-Dren", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Mire Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "One With A Graceful Neck", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Karat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of Flaming Barrels", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of the Furious Fists", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Sadistic Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shades Of Grey", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shadowleet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shifty Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shore Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Sipius Enel Lux-Mara", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Slinking Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Suininnik", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Tuaninnik", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Visionist Dom-Xum Shere", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Wandering Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Waning Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Watcher Enel Ulma-Thar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Yuttos Elysium Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        internal static bool UsesUnknownFlag7(string name)
        {
            if (string.Equals(name, "Aniuchach", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arachno Frigida", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arachno Gelida", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Brisk Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "CEO Guardian", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cagey Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Callous Mortiig", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Chill Spider", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Comatosed Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Coral Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Crystal Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cur-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Cur-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Deceitful Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Devoted Enel Ilad-Ulma", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Karat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Mada", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "El-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Elysian Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Flagging Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Gelid Eremite", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Guard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Guard - Elmo Fitz", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Hai-Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Heckler of Earth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Heckler of Elements", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Heckler of Metals", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Heckler of Stones", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Hiathlin Lookout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Ichiachich", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Inicha", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Insidious Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Kolaana", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Kolaana-Behn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Dasa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Len-Lochquid", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Lost Hiathlin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Lost Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Lucent Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Majestic Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Malah-Dren", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Minion Grunt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Mire Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "One With A Graceful Neck", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Karat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of Flaming Barrels", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Mada of the Furious Fists", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Or-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Prime Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Rippled Eremite", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Sadistic Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shades Of Grey", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shadowleet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shell Eremite", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shifty Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Shore Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Slinking Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Stalking Slayer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Suininnik", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Tranquil Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Tuaninnik", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Voracious Horror", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Wandering Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Waning Soul", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Weaver of Decay", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "Yuttos Elysium Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        internal static bool TryGetCapturedScfuUnknown1(string name, out byte[] data)
        {
            if (string.IsNullOrEmpty(name))
            {
                data = null;
                return false;
            }

            byte[] unused;
            if (TryGetExtendedTextureOverride(name, out unused))
            {
                data = (byte[])ExtTexScfuUnknown1.Clone();
                return true;
            }

            if (string.Equals(name, "Aniuchach", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Arachno Frigida", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Arachno Gelida", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Brisk Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "CEO Guardian", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Cagey Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Calan-Cur", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Calan-El", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Callous Mortiig", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Cama-El", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Carlo Pinnetti", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Chill Spider", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Coloss-Or", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Comatosed Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Coral Rafter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Craig-Or", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Craig-Or of Gear & Ammo", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Craig-Or of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Craig-Or of Protection", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Crystal Rafter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Cur-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Cur-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Dachu-Cur", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Deceitful Weaver", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Devoted Enel Ilad-Ulma", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "El-Karat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "El-Mada", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "El-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Elysian Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Flagging Arcorash", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Follower Yutt-Ixi Shere", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Fortuitous Hes-Man Shere", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Gelid Eremite", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Guard", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Guard - Elmo Fitz", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Hai-Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Heckler of Earth", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Heckler of Elements", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Heckler of Metals", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Heckler of Stones", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Hiathlin Lookout", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Hoathlan", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Hypnagogic Ixi-Bhotaar Shere", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Ichiachich", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Inicha", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Insidious Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Kolaana", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Kolaana-Behn", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Dasa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Dosa", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Lendar", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Len-Lochquid", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Lodoth-Len", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Lost Hiathlin", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Lost Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Lucent Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Majestic Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Malah", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Malah-Dren", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Minion Grunt", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Mire Rafter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "One With A Graceful Neck", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Karat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of Flaming Barrels", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of Preservation", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Mada of the Furious Fists", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Or-Nodor", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Prime Devourer of Life", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Rippled Eremite", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Sadistic Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Shades Of Grey", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Shadowleet", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Shell Eremite", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Shifty Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Shore Rafter", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Sipius Enel Lux-Mara", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Slinking Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Son-Len", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Stalking Slayer", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Suininnik", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Sun-Len", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Tranquil Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Tuaninnik", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Visionist Dom-Xum Shere", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Voracious Horror", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Wandering Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Waning Soul", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Watcher Enel Ulma-Thar", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Weaver of Decay", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
                return true;
            }
            if (string.Equals(name, "Yuttos Elysium Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DefaultScfuUnknown1.Clone();
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
                if (!AggroRadiusByNpcInstance.TryGetValue(npc.Identity.Instance, out radius)
                    || radius <= 0f)
                {
                    return null;
                }
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null || npc.RawCoordinates == null)
            {
                return null;
            }

            int npcSide = npc.Stats[StatIds.side].Value;
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

                int playerSide = candidate.Stats[StatIds.side].Value;
                // Omni/Clan: skip same-side and Neutral players (only aggro opposing side).
                if (npcSide == (int)Side.Omni || npcSide == (int)Side.Clan)
                {
                    if (playerSide == (int)Side.Neutral || playerSide == npcSide)
                    {
                        continue;
                    }
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

        public static void StartForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !SupportsPlayfield(playfieldIdentity.Instance)
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            NextRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[Slots.Length];
            int spawned = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].PlayfieldId != playfieldIdentity.Instance)
                {
                    continue;
                }

                if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ElysiumEastMobRuntime started pf="
                + playfieldIdentity.Instance
                + " spawned="
                + spawned
                + "/"
                + Slots.Length
                + " source=182451+190145+193914+201436");
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRespawnUtcBySlot.Remove(playfieldInstance);
        }

        public static void TickRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !SupportsPlayfield(playfieldIdentity.Instance)
                || !LinkedPlayfields.Contains(playfieldIdentity.Instance))
            {
                return;
            }

            DateTime[] next;
            if (!NextRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out next)
                || next == null
                || next.Length != Slots.Length)
            {
                next = new DateTime[Slots.Length];
                NextRespawnUtcBySlot[playfieldIdentity.Instance] = next;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].PlayfieldId != playfieldIdentity.Instance)
                {
                    continue;
                }

                Character living = FindLivingSlotMob(playfield, i);
                if (living != null)
                {
                    next[i] = DateTime.MinValue;
                    RegisterAggro(living.Identity.Instance);
                    continue;
                }

                if (next[i] == DateTime.MinValue)
                {
                    next[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                    continue;
                }

                if (next[i] > DateTime.UtcNow)
                {
                    continue;
                }

                if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    next[i] = DateTime.MinValue;
                }
            }
        }

        private static Character FindLivingSlotMob(Playfield playfield, int slotIndex)
        {
            MobSlot slot = Slots[slotIndex];
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, slot.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Character mob = candidate as Character;
                if (mob == null || mob.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = mob.Coordinates().x - slot.X;
                float dz = mob.Coordinates().z - slot.Z;
                if ((dx * dx) + (dz * dz) <= 25.0f)
                {
                    return mob;
                }
            }

            return null;
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = Slots[slotIndex];
            if (slot.PlayfieldId != playfieldIdentity.Instance)
            {
                return null;
            }

            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Aggressive };
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },
                new Quaternion(0.0, slot.HeadingY, 0.0, slot.HeadingW),
                controller,
                slot.Level);
            if (mob == null)
            {
                return null;
            }

            mob.Name = slot.Name;
            mob.Playfield = playfield;
            ApplyCaptureStats(mob, slot);
            PrepareCombat(mob, controller, slot);
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            RegisterAggro(mob.Identity.Instance);
            activateNpc(mob);
            RegisterAggro(mob.Identity.Instance);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static void PrepareCombat(Character mob, NPCController controller, MobSlot slot)
        {
            CapturedEnemyCombatContract contract;
            if (IsElysiumHeckler(slot.Name))
            {
                SetStat(mob, StatIds.mindamage, NpcCombatAttackRules.CapturedElysiumHecklerMinDamage);
                SetStat(mob, StatIds.maxdamage, NpcCombatAttackRules.CapturedElysiumHecklerMaxDamage);
                contract = CapturedEnemyCombatContract.ElysiumHecklerAttack(
                    "elysium-heckler-20260727-190145",
                    mob.Identity.Instance);
            }
            else
            {
                int minDamage = Math.Max(1, slot.Level);
                int maxDamage = Math.Max(minDamage + 1, slot.Level + (slot.Level / 2));
                SetStat(mob, StatIds.mindamage, minDamage);
                SetStat(mob, StatIds.maxdamage, maxDamage);
                contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                    "elysium-aos-20260727-193914",
                    minDamage,
                    maxDamage,
                    2.0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            controller.AiProfile = NpcAiProfile.Aggressive;
        }

        private static bool IsElysiumHeckler(string name)
        {
            return !string.IsNullOrEmpty(name)
                   && name.StartsWith("Heckler of ", StringComparison.OrdinalIgnoreCase);
        }

        private static void RegisterAggro(int npcInstance)
        {
            lock (AggroGate)
            {
                AggroRadiusByNpcInstance[npcInstance] = AggroRadiusMeters;
            }
        }

        private static void ApplyCaptureStats(Character mob, MobSlot slot)
        {
            SetStat(mob, StatIds.monsterdata, slot.MonsterData);
            SetStat(mob, StatIds.level, slot.Level);
            SetStat(mob, StatIds.life, slot.Health);
            SetStat(mob, StatIds.health, slot.Health);
            SetStat(mob, StatIds.npcfamily, slot.NpcFamily);
            SetStat(mob, StatIds.monsterscale, slot.Scale);
            SetStat(mob, StatIds.runspeed, slot.RunSpeed);
            SetStat(mob, StatIds.flags, slot.CharacterFlags);
            SetStat(mob, StatIds.visualflags, slot.VisualFlags);
            SetStat(mob, StatIds.side, slot.Side);
            if (slot.HeadMesh > 0)
            {
                SetStat(mob, StatIds.headmesh, slot.HeadMesh);
            }

            mob.Textures.Clear();
            if (slot.Textures != null)
            {
                for (int i = 0; i < slot.Textures.Length; i++)
                {
                    int[] t = slot.Textures[i];
                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            mob.MeshLayer.Clear();
            mob.SocialMeshLayer.Clear();
            if (slot.Meshes != null)
            {
                for (int i = 0; i < slot.Meshes.Length; i++)
                {
                    int[] m = slot.Meshes[i];
                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }
            }
        }

        private static void SetStat(Character mob, StatIds stat, int value)
        {
            mob.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)value);
            mob.Stats[stat].Value = value;
        }
    }
}
