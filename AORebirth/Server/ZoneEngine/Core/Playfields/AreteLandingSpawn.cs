namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Arete Landing (PF 6553) humanoid NPCs.
    /// Quest NPCs (Rex/Marcus/Flint/Alex/Bill/wounded) from prior captures.
    /// Wounded Dockworkers (Sit + 12/32 HP): capture 20260722-134750 (six identities).
    /// Ground protest cluster: ONLY tagged identities from capture 20260720-151642.
    /// Surveillance Droid owned solely by SurveillanceDroidRuntime (capture 78E0FC8A @ 3567).
    /// Bruiser @ 3556 is a separate tagged NPC.
    /// Kneebreaker Alfonzo Rizzolo (7981F40C) from capture 20260720-171317.
    /// Stan-area ambient NPCs/vendors from capture 20260720-goldman (Stanley Goodman cluster).
    /// </summary>
    internal static class AreteLandingSpawn
    {
        private const int AreteLandingPlayfieldId = 6553;

        /// <summary>Presence radius for multi-slot same-name NPCs (capture pad slots).</summary>
        private const float MultiSpawnPresenceRadius = 2.5f;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        // CaptureInstance → live pool Instance. Pool ids ≠ capture ids, so FindByIdentity(capture)
        // never hits; without this, patrolling multi-slot NPCs (ICC Peacekeeper) leave the 2.5m
        // pad check and TickEnsure respawns forever (~100 stacked).
        private static readonly Dictionary<int, int> LivingCaptureSlots = new Dictionary<int, int>();

        private const string TemplateHash = "BART";

        private sealed class AreteNpc
        {
            public string Name;
            public int Level;
            public int Health;
            /// <summary>Current HP; 0 means full (same as Health). Capture wounded dockworkers are 12/32.</summary>
            public int CurrentHealth;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
            public int HeadMesh;
            public int RunSpeed;
            public int NpcFamily;
            public int LosHeight;
            public int CharacterFlags;
            public int AppearanceValue;
            public int Side;
            public int Breed;
            public int Gender;
            public int Race;
            public int Fatness;
            public int MovementMode;
            public float X;
            public float Y;
            public float Z;
            public float Hx;
            public float Hy;
            public float Hz;
            public float Hw;
            public int[][] Textures;
            public int[][] Meshes;
            /// <summary>Live capture SimpleChar instance; 0 = runtime pool id.</summary>
            public int CaptureInstance;
        }

        private static readonly AreteNpc[] Npcs =
        {
            new AreteNpc
            {
                // Capture 20260720-061810 SCFU + dossier; dialogue id 782DE568
                Name = "Rex Larsson",
                Level = 15, Health = 511, MonsterData = 26074, Scale = 97, VisualFlags = 31, HeadMesh = 40691, RunSpeed = 52,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3624.06128f, Y = 51.745f, Z = 787.764648f,
                Hx = 0f, Hy = -0.708068252f, Hz = 0f, Hw = 0.706144f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205120, 0, 2 }, new[] { 0, 40691, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260719-do-flint-bio-com SCFU 78E0FC62; flamethrower mesh 292936; dialogue id 782DE567
                Name = "Marcus Stone",
                Level = 15, Health = 117800, MonsterData = 258744, Scale = 105, VisualFlags = 31, HeadMesh = 40667, RunSpeed = 52,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3630.13062f, Y = 40.9849968f, Z = 824.1919f,
                Hx = 0f, Hy = -0.2588223f, Hz = 0f, Hw = -0.965926f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205120, 0, 2 }, new[] { 0, 40667, 0, 4 }, new[] { 1, 292936, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260719-Rex-Markus-stone 78E0FC64
                Name = "Flint Novak",
                Level = 20, Health = 559, MonsterData = 26133, Scale = 90, VisualFlags = 31, HeadMesh = 40251, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3598.331f, Y = 5.11000061f, Z = 862.9781f,
                Hx = 0.0f, Hy = 0.493891031f, Hz = 0.0f, Hw = 0.8695238f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205116, 0, 2 }, new[] { 0, 40251, 0, 4 }, new[] { 1, 258983, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260719-do-flint-bio-com scfu-appearance.csv Alex Gibbs 78E0FC61
                Name = "Alex Gibbs",
                CaptureInstance = unchecked((int)0x78E0FC61),
                Level = 20, Health = 559, MonsterData = 263050, Scale = 115, VisualFlags = 31, HeadMesh = 40137, RunSpeed = 73,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1896,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3520.78442f, Y = 5.11000061f, Z = 856.6935f,
                Hx = 0.0f, Hy = -0.6912034f, Hz = 0.0f, Hw = 0.722660244f,
                Textures = new[] { new[] { 0, 265571 }, new[] { 1, 265567 }, new[] { 2, 265569 }, new[] { 3, 265575 }, new[] { 4, 265573 } },
                Meshes = new[] { new[] { 0, 265714, 0, 2 }, new[] { 0, 40137, 0, 4 }, new[] { 1, 268617, 0, 2 }, new[] { 5, 267981, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-105157 scfu 78E0FC66 ICC Immigration Officer Bill
                Name = "ICC Immigration Officer Bill",
                CaptureInstance = unchecked((int)0x78E0FC66),
                Level = 25, Health = 724, MonsterData = 26088, Scale = 100, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 86,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 6054,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3510.2f, Y = 5.11000061f, Z = 826.2723f,
                Hx = 0.0f, Hy = 0.6667155f, Hz = 0.0f, Hw = 0.745312333f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            // --- Tagged ground cluster ONLY (capture 20260720-151642 focusedEnemyIdentities) ---
            new AreteNpc
            {
                // 78E0FC76 Bodyguard Logan Fixx
                Name = "Bodyguard Logan Fixx",
                CaptureInstance = unchecked((int)0x78E0FC76),
                Level = 100, Health = 13658, MonsterData = 247041, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 346,
                NpcFamily = 105, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1578,
                Side = 2, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3602.789f, Y = 8.145f, Z = 819.8485f,
                Hx = 0f, Hy = -0.6498167f, Hz = 0f, Hw = 0.760090947f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = new[] { new[] { 1, 233232, 0, 2 } },
            },
            new AreteNpc
            {
                // 78E0FC77 Desmond Calitri
                Name = "Desmond Calitri",
                CaptureInstance = unchecked((int)0x78E0FC77),
                Level = 20, Health = 559, MonsterData = 295565, Scale = 120, VisualFlags = 31, HeadMesh = 236703, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3605.20337f, Y = 8.075f, Z = 826.8822f,
                Hx = 0f, Hy = -0.7337265f, Hz = 0f, Hw = 0.6794449f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 284557 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                Meshes = new[] { new[] { 0, 236703, 0, 4 } },
            },
            new AreteNpc
            {
                // 78E0FC7D Barry the Food Vendor (SCFU from 105157; tagged in 151642, no SCFU that session)
                Name = "Barry the Food Vendor",
                CaptureInstance = unchecked((int)0x78E0FC7D),
                Level = 10, Health = 227, MonsterData = 26139, Scale = 95, VisualFlags = 31, HeadMesh = 40249, RunSpeed = 34,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3516.73145f, Y = 6.80500031f, Z = 826.9838f,
                Hx = 0f, Hy = -0.6416705f, Hz = 0f, Hw = 0.766981542f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40249, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // 7981A543 Bruiser
                Name = "Bruiser",
                CaptureInstance = unchecked((int)0x7981A543),
                Level = 5, Health = 138, MonsterData = 26088, Scale = 93, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3555.97559f, Y = 5.11000061f, Z = 820.7616f,
                Hx = 0f, Hy = -0.9218881f, Hz = 0f, Hw = 0.3874562f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 7826, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 7988CC87 Bruiser
                Name = "Bruiser",
                CaptureInstance = unchecked((int)0x7988CC87),
                Level = 5, Health = 138, MonsterData = 26088, Scale = 93, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3525.66f, Y = 5.11000061f, Z = 829.66f,
                Hx = 0f, Hy = -0.9218881f, Hz = 0f, Hw = 0.3874562f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 7826, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-171317 Kneebreaker Alfonzo Rizzolo (SimpleChar:7981F40C)
                Name = "Kneebreaker Alfonzo Rizzolo",
                CaptureInstance = unchecked((int)0x7981F40C),
                Level = 4, Health = 28, MonsterData = 165196, Scale = 110, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 17,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3580.73462f, Y = 8.055f, Z = 833.1199f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 7826, 0, 2 } },
            },
            new AreteNpc
            {
                // 7981A53D Obedience Enforcement
                Name = "Obedience Enforcement",
                CaptureInstance = unchecked((int)0x7981A53D),
                Level = 5, Health = 138, MonsterData = 165196, Scale = 110, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3573.05371f, Y = 5.11000061f, Z = 817.9967f,
                Hx = 0f, Hy = -0.577261448f, Hz = 0f, Hw = 0.8165594f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 7826, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 7987C7AE Obedience Enforcement
                Name = "Obedience Enforcement",
                CaptureInstance = unchecked((int)0x7987C7AE),
                Level = 5, Health = 138, MonsterData = 165196, Scale = 110, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3602.18f, Y = 5.11000061f, Z = 805.87f,
                Hx = 0f, Hy = -0.577261448f, Hz = 0f, Hw = 0.8165594f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 7826, 0, 2 } },
            },
            new AreteNpc
            {
                // 797E764B Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x797E764B),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3575.25049f, Y = 5.11000061f, Z = 825.5049f,
                Hx = 0f, Hy = 0.9942326f, Hz = 0f, Hw = 0.107245035f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            new AreteNpc
            {
                // 797FD55B Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x797FD55B),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3566.39819f, Y = 5.11000061f, Z = 822.3082f,
                Hx = 0f, Hy = 0.9999995f, Hz = 0f, Hw = -0.0009882333f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            new AreteNpc
            {
                // 7981A552 Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x7981A552),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3592.00659f, Y = 5.11000061f, Z = 820.006958f,
                Hx = 0f, Hy = 0.638314664f, Hz = 0f, Hw = 0.769775569f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            new AreteNpc
            {
                // 7981A554 Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x7981A554),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3591.99341f, Y = 5.11000061f, Z = 824.003662f,
                Hx = 0f, Hy = 0.753128767f, Hz = 0f, Hw = 0.657873154f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 7985C910 Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x7985C910),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3523.32f, Y = 6.52f, Z = 780.90f,
                Hx = 0f, Hy = 0.9942326f, Hz = 0f, Hw = 0.107245035f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 797FD582 Protester
                Name = "Protester",
                CaptureInstance = unchecked((int)0x797FD582),
                Level = 2, Health = 48, MonsterData = 203740, Scale = 91, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 10,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3562.63f, Y = 6.91f, Z = 777.90f,
                Hx = 0f, Hy = 0.9942326f, Hz = 0f, Hw = 0.107245035f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 284183, 0, 2 } },
            },
            // Capture 20260722-134750: six Wounded Dockworkers, Sit (MovementMode=8), HP 12/32.
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC6E),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3583.531f, Y = 40.965f, Z = 831.2881f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC6F),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3605.379f, Y = 40.965f, Z = 838.2296f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC72),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3599.11182f, Y = 25.585f, Z = 878.1775f,
                Hx = 0.0f, Hy = 0.00216258457f, Hz = 0.0f, Hw = 0.9999977f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC5F),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3547.33618f, Y = 5.505f, Z = 809.1123f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 79666CF1 (Marcus pad standing dockworker)
                CaptureInstance = unchecked((int)0x79666CF1),
                Name = "Dockworker",
                Level = 3, Health = 3495, MonsterData = 26137, Scale = 92, VisualFlags = 31, HeadMesh = 40209, RunSpeed = 13,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 42824,
                Side = 0, Breed = 2, Gender = 3, Race = 41, Fatness = 1, MovementMode = 3,
                X = 3589.06f, Y = 40.9649963f, Z = 842.77f,
                Hx = 0.0f, Hy = -0.7891955f, Hz = 0.0f, Hw = 0.6141474f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205112, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 292936, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-134750 797DD44C (ramp standing dockworker)
                CaptureInstance = unchecked((int)0x797DD44C),
                Name = "Dockworker",
                Level = 3, Health = 3495, MonsterData = 26137, Scale = 92, VisualFlags = 31, HeadMesh = 40209, RunSpeed = 13,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 42824,
                Side = 0, Breed = 2, Gender = 3, Race = 41, Fatness = 1, MovementMode = 3,
                X = 3620.593f, Y = 31.205f, Z = 875.2614f,
                Hx = 0.0f, Hy = -0.7891955f, Hz = 0.0f, Hw = 0.6141474f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205112, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 292936, 0, 2 } },
            },
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC70),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3621.29224f, Y = 37.565f, Z = 855.1413f,
                Hx = 0.0f, Hy = 0.0012998787f, Hz = 0.0f, Hw = 0.999999166f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                CaptureInstance = unchecked((int)0x78E0FC71),
                Name = "Wounded Dockworker",
                Level = 1, Health = 32, CurrentHealth = 12, MonsterData = 296008, Scale = 90, VisualFlags = 31, HeadMesh = 40130, RunSpeed = 30,
                NpcFamily = 137, LosHeight = 3000, CharacterFlags = 277615105, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3620.278f, Y = 31.205f, Z = 873.665833f,
                Hx = 0.0f, Hy = -0.00200429629f, Hz = 0.0f, Hw = 0.999998f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40130, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC7C (scfu)
                CaptureInstance = unchecked((int)0x78E0FC7C),
                Name = "Antonio Stacklund",
                Level = 20, Health = 559, MonsterData = 26088, Scale = 99, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3443.073f, Y = 9.145f, Z = 832.652649f,
                Hx = 0.0f, Hy = -0.388969f, Hz = 0.0f, Hw = 0.9212495f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD15 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD15),
                Name = "Carol Schieffer",
                Level = 21, Health = 592, MonsterData = 26090, Scale = 99, VisualFlags = 31, HeadMesh = 40645, RunSpeed = 73,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3470.92017f, Y = 9.01f, Z = 858.4814f,
                Hx = 0.0f, Hy = -0.00196519564f, Hz = 0.0f, Hw = 0.9999981f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40645, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 7982B96D (dossier)
                CaptureInstance = unchecked((int)0x7982B96D),
                Name = "Cedric Harding",
                Level = 6, Health = 42, MonsterData = 165188, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 23,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 6, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3501.87085f, Y = 5.11000061f, Z = 825.5927f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD10 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD10),
                Name = "Chauncey Varela",
                Level = 1, Health = 25, MonsterData = 26139, Scale = 90, VisualFlags = 31, HeadMesh = 40279, RunSpeed = 6,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3438.50854f, Y = 9.01f, Z = 846.927063f,
                Hx = 0.0f, Hy = 0.213938951f, Hz = 0.0f, Hw = 0.976847053f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40279, 0, 4 }, new[] { 1, 29084, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD18 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD18),
                Name = "Dion Giscombe",
                Level = 7, Health = 160, MonsterData = 26097, Scale = 94, VisualFlags = 31, HeadMesh = 40124, RunSpeed = 25,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3429.213f, Y = 9.215f, Z = 800.1055f,
                Hx = 0.0f, Hy = 0.985210657f, Hz = 0.0f, Hw = 0.17134732f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40124, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-loralei 78E0FC6B (dossier + SCFU CharacterFlags=279450113)
                CaptureInstance = unchecked((int)0x78E0FC6B),
                Name = "Lorelei the Bartender",
                Level = 10, Health = 227, MonsterData = 26137, Scale = 100, VisualFlags = 31, HeadMesh = 40209, RunSpeed = 35,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1864,
                Side = 0, Breed = 2, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3369.1416f, Y = 17.315f, Z = 794.4232f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40209, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish Omni-AF pad 7982B839
                CaptureInstance = unchecked((int)0x7982B839),
                Name = "Omni-AF Private",
                Level = 10, Health = 227, MonsterData = 26151, Scale = 110, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 119,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1642,
                Side = 1, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3393.15747f, Y = 18.545f, Z = 854.9975f,
                Hx = 0.0f, Hy = -0.709484041f, Hz = 0.0f, Hw = 0.7047215f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish Omni-AF pad 7982BA04
                CaptureInstance = unchecked((int)0x7982BA04),
                Name = "Omni-AF Private",
                Level = 10, Health = 227, MonsterData = 26151, Scale = 110, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 119,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1642,
                Side = 1, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3393.19434f, Y = 18.545f, Z = 858.1331f,
                Hx = 0.0f, Hy = -0.737966955f, Hz = 0.0f, Hw = 0.6748368f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish Omni-AF pad 798467B8
                CaptureInstance = unchecked((int)0x798467B8),
                Name = "Omni-AF Private",
                Level = 10, Health = 227, MonsterData = 26151, Scale = 110, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 119,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1642,
                Side = 1, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3387.38f, Y = 18.545f, Z = 854.7541f,
                Hx = 0.0f, Hy = -0.739335656f, Hz = 0.0f, Hw = 0.673337042f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish Omni-AF pad 7985C927
                CaptureInstance = unchecked((int)0x7985C927),
                Name = "Omni-AF Private",
                Level = 10, Health = 227, MonsterData = 26151, Scale = 110, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 119,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1642,
                Side = 1, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3387.17114f, Y = 18.545f, Z = 858.179443f,
                Hx = 0.0f, Hy = -0.7357304f, Hz = 0.0f, Hw = 0.6772745f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC6C (scfu)
                CaptureInstance = unchecked((int)0x78E0FC6C),
                Name = "Dr. Mason",
                Level = 20, Health = 559, MonsterData = 26147, Scale = 99, VisualFlags = 31, HeadMesh = 40172, RunSpeed = 77,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1640,
                Side = 0, Breed = 3, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3430.51563f, Y = 9.215f, Z = 795.449158f,
                Hx = 0.0f, Hy = -0.5077101f, Hz = 0.0f, Hw = 0.861528f,
                Textures = new[] { new[] { 0, 213839 }, new[] { 1, 213739 }, new[] { 2, 213796 }, new[] { 3, 213694 }, new[] { 4, 213914 } },
                Meshes = new[] { new[] { 0, 40172, 0, 4 }, new[] { 1, 262517, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0D (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0D),
                Name = "Eliseo Ye",
                Level = 19, Health = 526, MonsterData = 26139, Scale = 98, VisualFlags = 31, HeadMesh = 40279, RunSpeed = 66,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3436.187f, Y = 9.01f, Z = 887.3641f,
                Hx = 0.0f, Hy = 0.00240426f, Hz = 0.0f, Hw = 0.999997139f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40279, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 79227BED (scfu)
                CaptureInstance = unchecked((int)0x79227BED),
                Name = "Food Provider",
                Level = 10, Health = 227, MonsterData = 26090, Scale = 95, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 34,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3439.04688f, Y = 9.01f, Z = 849.458069f,
                Hx = 0.0f, Hy = 0.0335111581f, Hz = 0.0f, Hw = 0.999438345f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40629, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC80 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC80),
                Name = "Furniture Merchant",
                Level = 104, Health = 7304, MonsterData = 26137, Scale = 112, VisualFlags = 31, HeadMesh = 40209, RunSpeed = 356,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1864,
                Side = 0, Breed = 2, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3438.173f, Y = 9.165f, Z = 892.195251f,
                Hx = 0.0f, Hy = -0.9435723f, Hz = 0.0f, Hw = 0.331167072f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40209, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260722-235242 797D337E FollowTarget loop (elevator approach)
                CaptureInstance = unchecked((int)0x797D337E),
                Name = "ICC Peacekeeper",
                Level = 40, Health = 1650, MonsterData = 26092, Scale = 103, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 137,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3391.324f, Y = 12.910f, Z = 801.860f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 7962A325 (scfu)
                CaptureInstance = unchecked((int)0x7962A325),
                Name = "ICC Peacekeeper",
                Level = 40, Health = 1650, MonsterData = 26092, Scale = 103, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 137,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3481.32544f, Y = 8.018829f, Z = 786.583069f,
                Hx = 0.0f, Hy = 0.6419681f, Hz = 0.0f, Hw = 0.7667314f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 7962A3F9 (scfu); pathing 20260722-235242
                CaptureInstance = unchecked((int)0x7962A3F9),
                Name = "ICC Peacekeeper",
                Level = 40, Health = 1650, MonsterData = 26092, Scale = 103, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 137,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3410.723f, Y = 3.393f, Z = 773.751f,
                Hx = 0.0f, Hy = 0.7455374f, Hz = 0.0f, Hw = 0.6664638f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 797FD043 (scfu)
                CaptureInstance = unchecked((int)0x797FD043),
                Name = "ICC Peacekeeper",
                Level = 40, Health = 1650, MonsterData = 26092, Scale = 103, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 137,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3456.158f, Y = 9.145f, Z = 834.197754f,
                Hx = 0.0f, Hy = 0.04987623f, Hz = 0.0f, Hw = 0.9987554f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0C (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0C),
                Name = "Jamison Clasen",
                Level = 6, Health = 138, MonsterData = 26097, Scale = 93, VisualFlags = 31, HeadMesh = 40120, RunSpeed = 22,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3404.147f, Y = 9.01f, Z = 811.6471f,
                Hx = 0.0f, Hy = 0.9682141f, Hz = 0.0f, Hw = 0.250122935f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40120, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD08 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD08),
                Name = "Janae Seaman",
                Level = 23, Health = 658, MonsterData = 26149, Scale = 100, VisualFlags = 31, HeadMesh = 40169, RunSpeed = 80,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3463.00171f, Y = 9.01f, Z = 889.49884f,
                Hx = 0.0f, Hy = -0.69432646f, Hz = 0.0f, Hw = 0.7196602f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40169, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD07 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD07),
                Name = "Janee Forejt",
                Level = 6, Health = 138, MonsterData = 26090, Scale = 93, VisualFlags = 31, HeadMesh = 40637, RunSpeed = 22,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3416.31763f, Y = 9.145f, Z = 843.8199f,
                Hx = 0.0f, Hy = -0.241314024f, Hz = 0.0f, Hw = 0.970447063f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40637, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD12 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD12),
                Name = "Joseph Schuemann",
                Level = 8, Health = 183, MonsterData = 26139, Scale = 94, VisualFlags = 31, HeadMesh = 223900, RunSpeed = 28,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3405.26953f, Y = 9.01f, Z = 834.66f,
                Hx = 0.0f, Hy = 0.9359442f, Hz = 0.0f, Hw = 0.352148384f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 223900, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD17 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD17),
                Name = "Keesha McKesson",
                Level = 19, Health = 526, MonsterData = 26149, Scale = 98, VisualFlags = 31, HeadMesh = 223911, RunSpeed = 66,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3441.17188f, Y = 9.01f, Z = 860.7495f,
                Hx = 0.0f, Hy = -0.004078528f, Hz = 0.0f, Hw = 0.999991655f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 223911, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC67 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC67),
                Name = "Lady Sheila Black",
                Level = 15, Health = 393, MonsterData = 26137, Scale = 100, VisualFlags = 31, HeadMesh = 40242, RunSpeed = 62,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1864,
                Side = 0, Breed = 2, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3411.6333f, Y = 9.205f, Z = 903.6198f,
                Hx = 0.0f, Hy = -0.708538651f, Hz = 0.0f, Hw = 0.705672f,
                Textures = new[] { new[] { 0, 215431 }, new[] { 1, 216740 }, new[] { 2, 215436 }, new[] { 3, 216769 }, new[] { 4, 216761 } },
                Meshes = new[] { new[] { 0, 40242, 0, 4 }, new[] { 1, 204732, 0, 2 }, new[] { 2, 226469, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC74 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC74),
                Name = "Leonora Marty",
                Level = 10, Health = 227, MonsterData = 26125, Scale = 100, VisualFlags = 31, HeadMesh = 40228, RunSpeed = 34,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1864,
                Side = 0, Breed = 2, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3440.039f, Y = 9.01f, Z = 858.082642f,
                Hx = 0.0f, Hy = 0.611119151f, Hz = 0.0f, Hw = 0.7915386f,
                Textures = new[] { new[] { 0, 85939 }, new[] { 1, 296228 }, new[] { 2, 296231 }, new[] { 3, 296229 }, new[] { 4, 296230 } },
                Meshes = new[] { new[] { 0, 40228, 0, 4 }, new[] { 1, 268645, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC78 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC78),
                Name = "Logistics Manager Fausto",
                Level = 20, Health = 559, MonsterData = 26101, Scale = 99, VisualFlags = 31, HeadMesh = 40105, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3408.101f, Y = 9.01f, Z = 866.9548f,
                Hx = 0.0f, Hy = 0.706344068f, Hz = 0.0f, Hw = 0.7078687f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247966 }, new[] { 2, 9619 }, new[] { 3, 247920 }, new[] { 4, 9626 } },
                Meshes = new[] { new[] { 0, 40105, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0F (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0F),
                Name = "Luna Erke",
                Level = 21, Health = 592, MonsterData = 26090, Scale = 99, VisualFlags = 31, HeadMesh = 40645, RunSpeed = 73,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3437.89746f, Y = 9.01f, Z = 834.5817f,
                Hx = 0.0f, Hy = 0.9999974f, Hz = 0.0f, Hw = 0.00228698459f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40645, 0, 4 }, new[] { 1, 29084, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC81 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC81),
                Name = "Marco Spida",
                Level = 10, Health = 227, MonsterData = 26092, Scale = 95, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 34,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3407.67676f, Y = 9.01f, Z = 831.262451f,
                Hx = 0.0f, Hy = -0.02306845f, Hz = 0.0f, Hw = 0.9997331f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247966 }, new[] { 2, 9619 }, new[] { 3, 247920 }, new[] { 4, 9626 } },
                Meshes = new[] { new[] { 0, 40694, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD16 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD16),
                Name = "Max Barchus",
                Level = 20, Health = 559, MonsterData = 26097, Scale = 99, VisualFlags = 31, HeadMesh = 223940, RunSpeed = 69,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3463.823f, Y = 9.01f, Z = 858.8283f,
                Hx = 0.0f, Hy = -0.00359358825f, Hz = 0.0f, Hw = 0.999993563f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 223940, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD09 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD09),
                Name = "Mitchell Dorph",
                Level = 29, Health = 971, MonsterData = 26139, Scale = 101, VisualFlags = 31, HeadMesh = 223891, RunSpeed = 100,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3423.436f, Y = 9.135f, Z = 806.42865f,
                Hx = 0.0f, Hy = 0.9825001f, Hz = 0.0f, Hw = 0.186262324f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 223891, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC83 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC83),
                Name = "Neutral Clothing Salesman",
                Level = 10, Health = 272, MonsterData = 26092, Scale = 105, VisualFlags = 31, HeadMesh = 223811, RunSpeed = 34,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3464.07764f, Y = 9.01f, Z = 861.159546f,
                Hx = 0.0f, Hy = 0.914993f, Hz = 0.0f, Hw = 0.403470844f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 265346 }, new[] { 2, 248878 }, new[] { 3, 37036 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 223811, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC7B (scfu)
                CaptureInstance = unchecked((int)0x78E0FC7B),
                Name = "Patrick Sun",
                Level = 20, Health = 559, MonsterData = 26092, Scale = 99, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3462.502f, Y = 9.01f, Z = 812.4979f,
                Hx = 0.0f, Hy = -0.8417724f, Hz = 0.0f, Hw = 0.539832652f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 258544 }, new[] { 2, 287244 }, new[] { 3, 37036 }, new[] { 4, 154204 } },
                Meshes = new[] { new[] { 0, 292933, 0, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 268625, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD13 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD13),
                Name = "Rashida Ardman",
                Level = 20, Health = 559, MonsterData = 26149, Scale = 99, VisualFlags = 31, HeadMesh = 40140, RunSpeed = 69,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3439.935f, Y = 9.01f, Z = 860.554932f,
                Hx = 0.0f, Hy = 0.00147117546f, Hz = 0.0f, Hw = 0.9999989f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40140, 0, 4 }, new[] { 1, 29084, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC75 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC75),
                Name = "Remi Gallois",
                Level = 10, Health = 227, MonsterData = 26084, Scale = 95, VisualFlags = 31, HeadMesh = 40689, RunSpeed = 34,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3433.65186f, Y = 9.01f, Z = 832.7955f,
                Hx = 0.0f, Hy = -0.110045508f, Hz = 0.0f, Hw = 0.9939262f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 21824 }, new[] { 2, 42219 }, new[] { 3, 21819 }, new[] { 4, 21831 } },
                Meshes = new[] { new[] { 0, 20108, 17998, 2 }, new[] { 0, 40689, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FCE9 (scfu)
                CaptureInstance = unchecked((int)0x78E0FCE9),
                Name = "Robotic Guard Dog",
                Level = 13, Health = 1306, MonsterData = 17720, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 36,
                NpcFamily = 1019, LosHeight = 0, CharacterFlags = 268980737, AppearanceValue = 1483,
                Side = 3, Breed = 6, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3405.59521f, Y = 9.01f, Z = 885.640259f,
                Hx = 0.0f, Hy = -0.00116706f, Hz = 0.0f, Hw = 0.999999344f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
            },
            // Rollerrats near oasis: LoreleiOasisMobRuntime (A004 + ExtTex), not BART.
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0E (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0E),
                Name = "Russel Aronstein",
                Level = 1, Health = 25, MonsterData = 26139, Scale = 90, VisualFlags = 31, HeadMesh = 40279, RunSpeed = 6,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3472.17114f, Y = 9.01f, Z = 851.716248f,
                Hx = 0.0f, Hy = -0.706724644f, Hz = 0.0f, Hw = 0.7074887f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40279, 0, 4 }, new[] { 1, 29084, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC69 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC69),
                Name = "Sarah Greene",
                Level = 20, Health = 559, MonsterData = 295889, Scale = 99, VisualFlags = 31, HeadMesh = 40618, RunSpeed = 72,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3471.26025f, Y = 9.01f, Z = 840.8831f,
                Hx = 0.0f, Hy = -0.7743728f, Hz = 0.0f, Hw = 0.6327316f,
                Textures = new[] { new[] { 0, 164946 }, new[] { 1, 164943 }, new[] { 2, 164945 }, new[] { 3, 164944 }, new[] { 4, 164948 } },
                Meshes = new[] { new[] { 0, 204942, 0, 0 }, new[] { 0, 40618, 0, 4 }, new[] { 1, 99152, 0, 2 }, new[] { 5, 291500, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC85 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC85),
                Name = "Secondhand Peddler",
                Level = 200, Health = 36434, MonsterData = 26090, Scale = 121, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 515,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3440.286f, Y = 9.01f, Z = 863.595459f,
                Hx = 0.0f, Hy = 0.8604388f, Hz = 0.0f, Hw = 0.5095545f,
                Textures = new[] { new[] { 0, 40975 }, new[] { 1, 82112 }, new[] { 2, 40968 }, new[] { 3, 40927 }, new[] { 4, 40988 } },
                Meshes = new[] { new[] { 0, 20093, 0, 0 }, new[] { 0, 40629, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC79 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC79),
                Name = "Shady Guy",
                Level = 20, Health = 559, MonsterData = 26074, Scale = 99, VisualFlags = 31, HeadMesh = 40691, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3540.858f, Y = 6.085f, Z = 748.305237f,
                Hx = 0.0f, Hy = -0.709456146f, Hz = 0.0f, Hw = 0.7047496f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247958 }, new[] { 2, 247992 }, new[] { 3, 247912 }, new[] { 4, 248031 } },
                Meshes = new[] { new[] { 0, 40691, 0, 4 }, new[] { 1, 30240, 0, 2 }, new[] { 5, 268581, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0A (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0A),
                Name = "Shane Streller",
                Level = 8, Health = 183, MonsterData = 26097, Scale = 94, VisualFlags = 31, HeadMesh = 40111, RunSpeed = 28,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3420.938f, Y = 9.165f, Z = 811.621948f,
                Hx = 0.0f, Hy = 0.003729624f, Hz = 0.0f, Hw = 0.999993f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40111, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD14 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD14),
                Name = "Sherwood Bannister",
                Level = 21, Health = 592, MonsterData = 26097, Scale = 99, VisualFlags = 31, HeadMesh = 40116, RunSpeed = 73,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3470.66479f, Y = 9.01f, Z = 831.8767f,
                Hx = 0.0f, Hy = 0.999995649f, Hz = 0.0f, Hw = 0.0029441833f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40116, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman / 20260719-do-flint-bio-com 78E0FC6A (SCFU)
                // Identity heading left the cargo-terminal mesh on its side (tilted plate).
                CaptureInstance = unchecked((int)0x78E0FC6A),
                Name = "Shipping Manifest Terminal",
                Level = 25, Health = 724, MonsterData = 279184, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 87,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 6, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3551.00586f, Y = 8.315f, Z = 832.852f,
                Hx = 0.0f, Hy = -0.7046964f, Hz = 0.0f, Hw = 0.709509f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC65 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC65),
                Name = "Stan Goodman",
                Level = 20, Health = 559, MonsterData = 26084, Scale = 110, VisualFlags = 31, HeadMesh = 40689, RunSpeed = 69,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3463.06055f, Y = 9.01f, Z = 880.1275f,
                Hx = 0.0f, Hy = -0.00337376748f, Hz = 0.0f, Hw = 0.999994338f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22586 }, new[] { 2, 9615 }, new[] { 3, 22557 }, new[] { 4, 22645 } },
                Meshes = new[] { new[] { 0, 45777, 0, 0 }, new[] { 0, 40689, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC84 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC84),
                Name = "Tailor",
                Level = 122, Health = 9439, MonsterData = 26076, Scale = 114, VisualFlags = 31, HeadMesh = 40635, RunSpeed = 401,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3471.41919f, Y = 9.01f, Z = 861.150452f,
                Hx = 0.0f, Hy = 0.971465051f, Hz = 0.0f, Hw = -0.2371816f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40635, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD11 (scfu)
                CaptureInstance = unchecked((int)0x78E0FD11),
                Name = "Trinh Alsaqri",
                Level = 28, Health = 910, MonsterData = 26149, Scale = 101, VisualFlags = 31, HeadMesh = 40143, RunSpeed = 97,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3465.02344f, Y = 9.01f, Z = 849.869141f,
                Hx = 0.0f, Hy = 0.705775857f, Hz = 0.0f, Hw = 0.708435237f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40143, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FD0B (scfu)
                CaptureInstance = unchecked((int)0x78E0FD0B),
                Name = "Velva Age",
                Level = 30, Health = 1033, MonsterData = 26149, Scale = 101, VisualFlags = 31, HeadMesh = 40169, RunSpeed = 103,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3459.96021f, Y = 9.135f, Z = 801.8846f,
                Hx = 0.0f, Hy = 0.705375254f, Hz = 0.0f, Hw = 0.7088341f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40169, 0, 4 }, new[] { 1, 258990, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260720-goldman 78E0FC68 (scfu)
                CaptureInstance = unchecked((int)0x78E0FC68),
                Name = "Vernon Godfray",
                Level = 15, Health = 393, MonsterData = 295564, Scale = 97, VisualFlags = 31, HeadMesh = 40271, RunSpeed = 55,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1352,
                Side = 0, Breed = 2, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3433.95337f, Y = 12.285f, Z = 825.898254f,
                Hx = 0.0f, Hy = 0.92450124f, Hz = 0.0f, Hw = 0.381179065f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 164803 }, new[] { 2, 164804 }, new[] { 3, 164802 }, new[] { 4, 164806 } },
                Meshes = new[] { new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-loralei Vaughn Hammond 78E0FC73 (finish dossier + loralei SCFU)
                CaptureInstance = unchecked((int)0x78E0FC73),
                Name = "Vaughn Hammond",
                Level = 25, Health = 724, MonsterData = 281855, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 86,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3369.26465f, Y = 18.1111526f, Z = 828.5384f,
                Hx = 0.0f, Hy = 0.7086759f, Hz = 0.0f, Hw = 0.70553416f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 1, 264698, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FC7F
                CaptureInstance = unchecked((int)0x78E0FC7F),
                Name = "Omni-Trans Equipment Vendor",
                Level = 40, Health = 1650, MonsterData = 250380, Scale = 103, VisualFlags = 31, HeadMesh = 40173, RunSpeed = 137,
                NpcFamily = 88, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1642,
                Side = 1, Breed = 1, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3392.456f, Y = 18.545f, Z = 876.7765f,
                Hx = 0.0f, Hy = -0.979695261f, Hz = 0.0f, Hw = 0.200497165f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22579 }, new[] { 2, 9619 }, new[] { 3, 22550 }, new[] { 4, 22638 } },
                Meshes = new[] { new[] { 0, 40173, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCF6
                CaptureInstance = unchecked((int)0x78E0FCF6),
                Name = "Omni-AF Officer Milne",
                Level = 35, Health = 1341, MonsterData = 165186, Scale = 110, VisualFlags = 31, HeadMesh = 40681, RunSpeed = 120,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1578,
                Side = 1, Breed = 1, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3390.00366f, Y = 18.545f, Z = 856.0561f,
                Hx = 0.0f, Hy = -0.7103106f, Hz = 0.0f, Hw = 0.7038884f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 40681, 0, 4 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFA
                CaptureInstance = unchecked((int)0x78E0FCFA),
                Name = "Omni-Pol Guard",
                Level = 20, Health = 447, MonsterData = 26097, Scale = 99, VisualFlags = 31, HeadMesh = 40111, RunSpeed = 62,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1418,
                Side = 1, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3381.43726f, Y = 17.11f, Z = 850.484131f,
                Hx = 0.0f, Hy = 0.999769f, Hz = 0.0f, Hw = 0.02149579f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20003, 0, 2 }, new[] { 0, 40111, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFB
                CaptureInstance = unchecked((int)0x78E0FCFB),
                Name = "Omni-Pol Guard",
                Level = 20, Health = 447, MonsterData = 26097, Scale = 99, VisualFlags = 31, HeadMesh = 40111, RunSpeed = 62,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1418,
                Side = 1, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3383.63623f, Y = 17.11f, Z = 850.8805f,
                Hx = 0.0f, Hy = 0.9997583f, Hz = 0.0f, Hw = -0.0219841618f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20003, 0, 2 }, new[] { 0, 40111, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFC
                CaptureInstance = unchecked((int)0x78E0FCFC),
                Name = "Omni-Pol Guard",
                Level = 25, Health = 724, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 86,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1834,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3371.524f, Y = 18.525f, Z = 877.873657f,
                Hx = 0.0f, Hy = 0.7098279f, Hz = 0.0f, Hw = 0.704375148f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20087, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFD
                CaptureInstance = unchecked((int)0x78E0FCFD),
                Name = "Omni-Pol Guard",
                Level = 25, Health = 724, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 86,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1834,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3371.587f, Y = 18.525f, Z = 875.7479f,
                Hx = 0.0f, Hy = 0.7055516f, Hz = 0.0f, Hw = 0.7086585f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20087, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFE
                CaptureInstance = unchecked((int)0x78E0FCFE),
                Name = "Omni-Pol Guard",
                Level = 25, Health = 724, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 86,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1834,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3376.234f, Y = 18.525f, Z = 875.3516f,
                Hx = 0.0f, Hy = 0.7064728f, Hz = 0.0f, Hw = 0.7077402f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20087, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FCFF
                CaptureInstance = unchecked((int)0x78E0FCFF),
                Name = "Omni-Pol Guard",
                Level = 25, Health = 724, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 86,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1834,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3376.49438f, Y = 18.525f, Z = 878.162f,
                Hx = 0.0f, Hy = 0.707606f, Hz = 0.0f, Hw = 0.7066072f,
                Textures = new[] { new[] { 0, 8744 }, new[] { 1, 8738 }, new[] { 2, 8742 }, new[] { 3, 8735 }, new[] { 4, 8746 } },
                Meshes = new[] { new[] { 0, 20087, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7783, 0, 2 }, new[] { 2, 155083, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD00
                CaptureInstance = unchecked((int)0x78E0FD00),
                Name = "Omni-Med Surgeon",
                Level = 20, Health = 559, MonsterData = 26092, Scale = 99, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 69,
                NpcFamily = 105, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1578,
                Side = 1, Breed = 1, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3372.307f, Y = 18.525f, Z = 855.9362f,
                Hx = 0.0f, Hy = 0.7095248f, Hz = 0.0f, Hw = 0.704680443f,
                Textures = new[] { new[] { 0, 14048 }, new[] { 1, 120608 }, new[] { 2, 284442 }, new[] { 3, 120607 }, new[] { 4, 120606 } },
                Meshes = new[] { new[] { 0, 40694, 0, 4 }, new[] { 1, 81804, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD01
                CaptureInstance = unchecked((int)0x78E0FD01),
                Name = "Omni-Med Guard",
                Level = 20, Health = 559, MonsterData = 26139, Scale = 99, VisualFlags = 31, HeadMesh = 40249, RunSpeed = 69,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1610,
                Side = 1, Breed = 2, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3375.78125f, Y = 18.525f, Z = 854.6186f,
                Hx = 0.0f, Hy = 0.709713757f, Hz = 0.0f, Hw = 0.7044901f,
                Textures = new[] { new[] { 0, 206966 }, new[] { 1, 206963 }, new[] { 2, 206965 }, new[] { 3, 206964 }, new[] { 4, 206968 } },
                Meshes = new[] { new[] { 0, 20064, 206967, 2 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 284456, 0, 2 }, new[] { 2, 284456, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD02
                CaptureInstance = unchecked((int)0x78E0FD02),
                Name = "Omni-Med Guard",
                Level = 20, Health = 559, MonsterData = 26139, Scale = 99, VisualFlags = 31, HeadMesh = 40249, RunSpeed = 69,
                NpcFamily = 2, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1610,
                Side = 1, Breed = 2, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3375.82227f, Y = 18.525f, Z = 857.3344f,
                Hx = 0.0f, Hy = 0.708575845f, Hz = 0.0f, Hw = 0.705634654f,
                Textures = new[] { new[] { 0, 206966 }, new[] { 1, 206963 }, new[] { 2, 206965 }, new[] { 3, 206964 }, new[] { 4, 206968 } },
                Meshes = new[] { new[] { 0, 20064, 206967, 2 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 284456, 0, 2 }, new[] { 2, 284456, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD1C
                CaptureInstance = unchecked((int)0x78E0FD1C),
                Name = "Clan Protester",
                Level = 20, Health = 447, MonsterData = 26139, Scale = 99, VisualFlags = 31, HeadMesh = 40249, RunSpeed = 62,
                NpcFamily = 104, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1609,
                Side = 1, Breed = 2, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3381.953f, Y = 17.11f, Z = 842.000061f,
                Hx = 0.0f, Hy = 0.00200117147f, Hz = 0.0f, Hw = 0.999998f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 248873 }, new[] { 3, 37031 }, new[] { 4, 30883 } },
                Meshes = new[] { new[] { 0, 204921, 0, 0 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 262812, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD1E
                CaptureInstance = unchecked((int)0x78E0FD1E),
                Name = "Clan Protester",
                Level = 20, Health = 447, MonsterData = 26090, Scale = 99, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 62,
                NpcFamily = 104, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1833,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3385.4978f, Y = 17.11f, Z = 842.0052f,
                Hx = 0.0f, Hy = -0.199943557f, Hz = 0.0f, Hw = 0.979807436f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 248873 }, new[] { 3, 37031 }, new[] { 4, 30883 } },
                Meshes = new[] { new[] { 0, 204935, 0, 0 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262812, 0, 2 } },
            },
            new AreteNpc
            {
                // Capture 20260721-finish 78E0FD1F
                CaptureInstance = unchecked((int)0x78E0FD1F),
                Name = "Clan Protester",
                Level = 20, Health = 447, MonsterData = 26103, Scale = 99, VisualFlags = 31, HeadMesh = 40103, RunSpeed = 62,
                NpcFamily = 104, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1673,
                Side = 1, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3377.99951f, Y = 17.11f, Z = 842.001f,
                Hx = 0.0f, Hy = 0.212661952f, Hz = 0.0f, Hw = 0.9771258f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 248873 }, new[] { 3, 37031 }, new[] { 4, 30883 } },
                Meshes = new[] { new[] { 0, 40103, 0, 4 }, new[] { 1, 262812, 0, 2 } },
            },
        };

        internal static void ClearPlayfield(int playfieldInstance)
        {
            SpawnedPlayfields.Remove(playfieldInstance);
            LivingCaptureSlots.Clear();
        }

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            if (!SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                // Already attempted this session — still fill any missing capture NPCs.
                TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
                return;
            }

            int spawned = 0;
            try
            {
                foreach (AreteNpc def in Npcs)
                {
                    try
                    {
                        if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                        {
                            spawned++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Error,
                            "AreteLandingSpawn exception npc=" + def.Name + " " + ex.GetType().Name + ": "
                            + ex.Message);
                    }
                }
            }
            finally
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteLandingSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                    + "/" + Npcs.Length + " source=20260722-134750+prior");

                if (spawned == 0)
                {
                    SpawnedPlayfields.Remove(playfieldIdentity.Instance);
                }
            }
        }

        internal static void TickEnsureMissingNpcs(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            foreach (AreteNpc def in Npcs)
            {
                if (IsNpcPresent(playfield, def))
                {
                    continue;
                }

                try
                {
                    SpawnOne(playfield, playfieldIdentity, activateNpc, def);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteLandingSpawn ensure exception npc=" + def.Name + " " + ex.GetType().Name + ": "
                        + ex.Message);
                }
            }
        }

        /// <summary>Legacy name — ensures all capture NPCs, not only quest ones.</summary>
        internal static void TickEnsureQuestNpcs(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
        }

        private static bool IsNpcPresent(Playfield playfield, AreteNpc def)
        {
            if (def.CaptureInstance != 0)
            {
                int poolInstance;
                if (LivingCaptureSlots.TryGetValue(def.CaptureInstance, out poolInstance))
                {
                    ICharacter bySlot = playfield.FindByIdentity<ICharacter>(
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = poolInstance
                        });
                    if (bySlot != null && bySlot.Stats[StatIds.health].Value > 0)
                    {
                        return true;
                    }

                    LivingCaptureSlots.Remove(def.CaptureInstance);
                }

                ICharacter byCaptureId = playfield.FindByIdentity<ICharacter>(
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = def.CaptureInstance
                    });
                if (byCaptureId != null && byCaptureId.Stats[StatIds.health].Value > 0)
                {
                    LivingCaptureSlots[def.CaptureInstance] = byCaptureId.Identity.Instance;
                    return true;
                }
            }

            // Multi-slot same-name NPCs (Wounded Dockworkers, Protestors, etc.) must be
            // matched by pad position — name-only presence collapsed them to one spawn.
            if (AllowsMultipleSpawns(def.Name))
            {
                return IsLivingNamedNpcNear(playfield, def, MultiSpawnPresenceRadius);
            }

            // Unique Arete names (Rex/Marcus/Flint/…): name-only so a walk-off pad
            // does not create a second copy (prior 2.5m bug).
            foreach (ICharacter npc in playfield.EnumerateActiveCharacters())
            {
                if (npc == null || npc.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                if (string.Equals(npc.Name, def.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AllowsMultipleSpawns(string name)
        {
            return string.Equals(name, "Wounded Dockworker", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Dockworker", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Protester", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Clan Protester", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Bruiser", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Obedience Enforcement", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "ICC Peacekeeper", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLivingNamedNpcNear(Playfield playfield, AreteNpc def, float radius)
        {
            double radiusSq = (double)radius * radius;
            foreach (ICharacter npc in playfield.EnumerateActiveCharacters())
            {
                if (npc == null || npc.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                if (!string.Equals(npc.Name, def.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double dx = npc.Coordinates().x - def.X;
                double dy = npc.Coordinates().y - def.Y;
                double dz = npc.Coordinates().z - def.Z;
                if ((dx * dx) + (dy * dy) + (dz * dz) <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            AreteNpc def)
        {
            if (IsNpcPresent(playfield, def))
            {
                return true;
            }

            var npcController = new NPCController { AiProfile = NpcAiProfile.Social };
            Character mob;
            try
            {
                mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    TemplateHash,
                    playfieldIdentity,
                    new Coordinate { x = def.X, y = def.Y, z = def.Z },
                    new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                    npcController,
                    def.Level);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteLandingSpawn SpawnMobFromTemplate threw npc=" + def.Name + " "
                    + ex.GetType().Name + ": " + ex.Message);
                return false;
            }

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteLandingSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats[StatIds.monsterdata].Value = def.MonsterData;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            int currentHealth = def.CurrentHealth > 0 ? def.CurrentHealth : def.Health;
            if (currentHealth > def.Health)
            {
                currentHealth = def.Health;
            }

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)currentHealth);
            mob.Stats[StatIds.health].Value = currentHealth;
            mob.Stats[StatIds.life].Value = def.Health;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)def.NpcFamily);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.losheight, (uint)def.LosHeight);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)def.Side);
            mob.Stats[StatIds.side].Value = def.Side;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)def.Breed);
            mob.Stats[StatIds.breed].Value = def.Breed;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)def.Gender);
            mob.Stats[StatIds.sex].Value = def.Gender;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, (uint)def.Race);
            mob.Stats[StatIds.race].Value = def.Race;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, (uint)def.Fatness);
            mob.Stats[StatIds.fatness].Value = def.Fatness;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualprofession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, (uint)def.MovementMode);
            mob.Stats[StatIds.currentmovementmode].Value = def.MovementMode;
            // Sit NPCs must restore to Run on StandUp (capture 20260720-064523 heal).
            uint previousMovementMode = def.MovementMode == (int)MoveModes.Sit
                                            ? (uint)MoveModes.Run
                                            : (uint)def.MovementMode;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, previousMovementMode);
            mob.Stats[StatIds.prevmovementmode].Value = (int)previousMovementMode;
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }
            else
            {
                // Monster bodies (e.g. Surveillance Droid) must not keep BART bartender headmesh.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, 0);
            }

            if (def.RunSpeed > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);
            }

            ApplyAppearance(mob, def);
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });
            if (string.Equals(def.Name, ZoneEngine.Core.Playfields.AreteRoboticGuardDogRuntime.DogName, StringComparison.OrdinalIgnoreCase))
            {
                ZoneEngine.Core.Playfields.AreteRoboticGuardDogRuntime.PrepareSpawnedDog(mob, npcController);
            }

            if (string.Equals(def.Name, ZoneEngine.Core.Playfields.AreteIccPeacekeeperPatrolRuntime.PeacekeeperName, StringComparison.OrdinalIgnoreCase))
            {
                ZoneEngine.Core.Playfields.AreteIccPeacekeeperPatrolRuntime.PrepareSpawnedPeacekeeper(mob, npcController);
            }

            ZoneEngine.Core.Playfields.AreteIccPeacekeeperPatrolRuntime.TryApplyPatrol(def.CaptureInstance, npcController);

            if (def.CaptureInstance != 0)
            {
                LivingCaptureSlots[def.CaptureInstance] = mob.Identity.Instance;
            }

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }

        private static void ApplyAppearance(Character mob, AreteNpc def)
        {
            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            if (def.Meshes != null)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);

                    // Capture flamethrower mesh position 1 → WeaponMeshRight so AttackInfo slot 6
                    // can drive the weapon texture animation VFX (Marcus / Dockworker 292936).
                    if (m.Length >= 2 && m[0] == 1 && m[1] > 0)
                    {
                        mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.weaponmeshright, (uint)m[1]);
                        mob.Stats[StatIds.weaponmeshright].Value = m[1];
                    }
                }
            }
            else if (def.HeadMesh > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
                mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
            }
        }
    }
}
