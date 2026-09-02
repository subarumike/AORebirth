namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Text;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Playfields.Content;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Capture-backed Nascence Life outdoor mob/NPC population (PF 4310–4313, 4001, 4531).
    /// Captures: 20260718-170408 (4310 Frontier), 20260718-173204 (4311 Crippler cave),
    /// 20260718-174130 (4311 Two Mountains), 20260718-180726 (4312 East / Core; Hecklers excluded),
    /// 20260718-230406 (4310 Drake + missing frontier roamers; NPCInfo only),
    /// 20260723-221330 (PF 4001 Drake, PF 4531 Goldman Harbor, Chimera patrol),
    /// 20260723-225021 (Barking Chimera fight packets + 15 corpse loot snapshots).
    /// Total 830 NPCs (4310=246, 4311=387, 4312=197) plus Harbor/Jobe Research.
    /// PF 4312 Hecklers remain in NascenceCoreHecklerSpawnOrchestrator.
    /// </summary>
    internal static class NascenceLifeSpawn
    {
        private const string TemplateHash = "BART";

        private sealed class LifeNpc
        {
            public int PlayfieldId;
            public string Name;
            public int Level;
            public int Health;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
            public int CharacterFlags;
            public int HeadMesh;
            public float X;
            public float Y;
            public float Z;
            public float Hx;
            public float Hy;
            public float Hz;
            public float Hw;
            public int[][] Textures;
            public int[][] Meshes;
            public string CaptureFolder;
            // Capture 20260823-000659 focusedEnemyIdentities full-circle patrol (aocap tag).
            public string PatrolCaptureInstance = null;
            // Optional patrol path; leave null unless a spawn sets identity-local points.
            public float[][] Waypoints = null;
            // Soft-respawn delay after death when no living mob remains near this spawn point.
            public double RespawnSeconds = 0;
        }

        // Capture 20260723-221330 SCFU ExtTex for Barking Chimera 798E09BC / Yuttos 798C1F0D (identical wire).
        private static readonly byte[] BarkingChimeraExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x6C, 0x6F, 0x77, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x49, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture 20260822-082554 SCFU 7A18D461 HasExtendedTextures: "grey" material + texture 236639 only.
        private static readonly byte[] PapagenaExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x67, 0x72, 0x65, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x9C, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture 20260822-224319 SCFU 7A1B033F HasExtendedTextures: "druid" + "druid 2 side(cloak)" texture 235151.
        private static readonly byte[] AbanFalaExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x64, 0x72, 0x75, 0x69, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0x8F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x64, 0x72, 0x75, 0x69, 0x64, 0x20, 0x32, 0x20, 0x73, 0x69, 0x64, 0x65, 0x28, 0x63, 0x6C, 0x6F,
                0x61, 0x6B, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0x8F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture 20260822-224319 SCFU 7A1B033F Unknown1 (same wire shape as Papagena Unknown1).
        private static readonly byte[] AbanFalaScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x01, 0x00,
                0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        // Capture CharacterFlags / NpcFamily for Redeemed Village Clan NPCs (20260822-224319).
        internal const short AbanFalaNpcFamily = 201;
        private const int AbanFalaCharacterFlags = 277352961;
        internal const int AbanFalaAppearanceValue = 1225;
        private const int RedeemedVillageClanCharacterFlags = 268964353;
        private const int RedeemedVillageClanTextureDruid = 235151;
        private const int RedeemedVillageClanTextureWarrior = 213984;
        private const int RedeemedVillageClanTextureNanoman = 213996;

        // Capture 20260822-082554 SCFU 7A18D461 Unknown1 + NanoProgram ActiveNanos (fire visuals on wire).
        private static readonly byte[] PapagenaScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x01, 0x00,
                0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        private static readonly PapagenaScfuActiveNano[] PapagenaScfuActiveNanos =
            {
                new PapagenaScfuActiveNano(0x3A900, 0, 7050327, 3733851),
                new PapagenaScfuActiveNano(0x3B26E, 0, 7050327, 3733851),
                new PapagenaScfuActiveNano(0x3B26C, 0, 10875952, 7559476),
                new PapagenaScfuActiveNano(0x3B26A, 0, 10010029, 6693553),
                new PapagenaScfuActiveNano(0x3B268, 0, 4414580, 1098104),
                new PapagenaScfuActiveNano(0x3B266, 0, 9644023, 6327547),
            };

        internal const short PapagenaNpcFamily = 207;

        private const int BarkingChimeraNpcFamily = 187;
        private const int GeosurveyDogNpcFamily = 200;
        private const int SwiftSilvertailNpcFamily = 172;
        // Capture 20260823-103458 SCFU npcFamily.
        private const int NascenceSpiritHunterNpcFamily = 211;
        private const int SoulDredgeNpcFamily = 207;
        // Capture 20260823-112044 SCFU npcFamily.
        private const int DiseaseRiddenRafterNpcFamily = 175;
        private const int TempterusNpcFamily = 202;
        private const int PredatorStrikerNpcFamily = 207;
        private const int CripplerOfGrowthNpcFamily = 207;

        // Capture 20260822-221109 SCFU Swift Silvertail textures.
        private const int SwiftSilvertailTextureSlot0 = 0x384DD;
        private const int SwiftSilvertailTextureSlot1 = 0x3931A;

        // Capture 20260822-221109 kill XP (50% buff removed from wire deltas).
        private const int BarkingChimeraKillXp = 250;
        private const int GeosurveyDogKillXp = 275;
        private const int SwiftSilvertailKillXp = 300;
        // Capture 20260823-103458 player XP Stat deltas (pre-level-up kills).
        private const int NascenceSpiritHunterKillXp = 830;
        private const int CascadingSpiritKillXp = 830;
        private const int SoulDredgeKillXp = 890;
        // Capture 20260823-112044 player XP Stat deltas.
        private const int DiseaseRiddenRafterKillXp = 890;
        private const int TempterusKillXp = 763;
        private const int PredatorStrikerKillXp = 500;
        private const int DeadlyPredatorKillXp = 500;
        private const int SpinetoothHatchlingKillXp = 500;
        private const int WeaverOfMaliceKillXp = 500;
        // Capture 20260826-052537 Death Parameter2=500.
        private const int HiathlinKillXp = 500;
        private const int OmathonKillXp = 500;

        // Capture 20260822-221109 starter-bridge local patrol box on PF 4310.
        private const float StarterBridgeMinX = 790f;
        private const float StarterBridgeMaxX = 900f;
        private const float StarterBridgeMinZ = 1090f;
        private const float StarterBridgeMaxZ = 1260f;
        private const double StarterBridgeCapturedAttackRange = 4.0d;
        // Mike: Cascading Spirit social aggro radius 10m (capture 20260823-103458 cave pack).
        private const float CascadingSpiritSocialAggroRadiusMeters = 10f;
        // Mike: Predator Striker social aggro radius 10m (capture 20260826-054154 pocket).
        private const float PredatorStrikerSocialAggroRadiusMeters = 10f;
        // Capture 20260827-221909: second cave Crippler Attack ~3s after first.
        private const float CripplerOfGrowthSocialAggroRadiusMeters = 10f;

        // Capture 20260826-192602: login crash when char loads at ~900/1640 fork visibility.
        // Tight bubble only — Demonic Subjugator @ 733/1565 stays outside (~183m).
        private const float FrontierForkCrashCenterX = 900.2f;
        private const float FrontierForkCrashCenterZ = 1640.7f;
        private const float FrontierForkCrashMobRadiusMeters = 85f;
        private const float FrontierForkWeaverSkipRadiusMeters = 150f;
        // Northwest branch from fork (Demonic entrance): keep crash-mob spawns outside skip bubble.
        private const float FrontierForkDemonicCorridorMinZ = 1635f;
        // Malah-Ana pocket @ ~953/1650 is inside the 150m fork bubble but outside the Spinetooth pocket.
        private const float FrontierSpinetoothDeferredMinX = 972f;
        private const float FrontierSpinetoothDeferredMinZ = 1585f;
        private const float FrontierSpinetoothDeferredMaxZ = 1720f;
        private const double FrontierForkDeferredLoginGraceSeconds = 8d;
        private const double FrontierForkDeferredBatchIntervalSeconds = 1d;
        private const int FrontierForkDeferredSpawnBatchSize = 3;

        private static readonly object FrontierForkDeferredSync = new object();

        private static readonly List<int> FrontierForkDeferredNpcIndices = new List<int>();

        private static readonly HashSet<int> FrontierForkDeferredSpawnedKeys = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> FrontierForkLoginReadyAtUtc = new Dictionary<int, DateTime>();

        private static DateTime FrontierForkDeferredLastBatchAtUtc = DateTime.MinValue;

        internal sealed class PapagenaScfuActiveNano
        {
            public PapagenaScfuActiveNano(int nanoIdentityInstance, int time1, int time2, int nanoInstance)
            {
                NanoIdentityInstance = nanoIdentityInstance;
                Time1 = time1;
                Time2 = time2;
                NanoInstance = nanoInstance;
            }

            public int NanoIdentityInstance { get; private set; }

            public int Time1 { get; private set; }

            public int Time2 { get; private set; }

            public int NanoInstance { get; private set; }
        }

        // Capture CharacterFlags: Dreaming Silvertail 277352961; animal mobs 268964353.
        private const int DreamingSilvertailCharacterFlags = 277352961;
        private const int DefaultAnimalCharacterFlags = 268964353;

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            return TryGetExtendedTextureOverride(name, 0, out data);
        }

        internal static bool TryGetExtendedTextureOverride(string name, int playfieldId, out byte[] data)
        {
            if (string.Equals(name, "Barking Chimera", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260723-221330 Barking + 20260825-202932 Slivering: low2:208969 ExtTex.
                data = (byte[])BarkingChimeraExtendedTextureOverrideData.Clone();
                return true;
            }

            // Geosurvey Dog ExtTex disabled — same blob as Chimera but crashes client near Demonic exit.

            if (string.Equals(name, "Papagena", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])PapagenaExtendedTextureOverrideData.Clone();
                return true;
            }

            if (IsAbanFalaName(name) || IsCurBeatName(name))
            {
                data = BuildDualMaterialExtTex("druid", "druid 2 side(cloak)", RedeemedVillageClanTextureDruid);
                return true;
            }

            if (IsHumeOcraName(name) || IsLuxWeiName(name))
            {
                data = BuildDualMaterialExtTex("varrior", "varrior 2 side(cloak)", RedeemedVillageClanTextureWarrior);
                return true;
            }

            if (IsPathDunaName(name))
            {
                data = BuildDualMaterialExtTex(
                    "nanoman 2 side cloak",
                    "nanoman",
                    RedeemedVillageClanTextureNanoman);
                return true;
            }

            if (NascenceSwampClanMobRuntime.TryGetExtendedTextureOverride(name, out data))
            {
                return true;
            }

            // NascenceFrontierOutdoorMobRuntime ExtTex: Deadly sabre (SCFU v58); Striker/Stalking off (crash).
            // Crippler PF4311 cave ExtTex gated by playfieldId (Demonic-exit crash on outdoor).
            if (NascenceFrontierOutdoorMobRuntime.TryGetExtendedTextureOverride(name, playfieldId, out data))
            {
                return true;
            }

            data = null;
            return false;
        }

        // Capture 20260826-054154 Deadly Predator SCFU Version=58 (0x07E2 sabre ExtTex).
        internal static bool RequiresScfuVersion58(string name)
        {
            return string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase);
        }

        // Barking Chimera starter-bridge patrol: per-spawn capture routes via
        // NascenceLifeStarterBridgePatrolRuntime (20260823-000659 movement-packets.csv).

        internal static bool IsRedeemedVillageClanNpcName(string name)
        {
            return IsAbanFalaName(name)
                   || IsCurBeatName(name)
                   || IsHumeOcraName(name)
                   || IsPathDunaName(name)
                   || IsLuxWeiName(name);
        }

        private static bool ShouldSkipFrontierForkCrashSpawn(LifeNpc def)
        {
            if (def == null || def.PlayfieldId != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return false;
            }

            // Geosurvey Dog crashes client on visibility (capture 20260826 @ 900/1640 fork).
            if (string.Equals(def.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Hwall: PF4310 outdoor SCFU not wired yet (client crash in Malah-Ana pocket).
            if (def.Name != null && def.Name.StartsWith("Hwall", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            float dx = def.X - FrontierForkCrashCenterX;
            float dz = def.Z - FrontierForkCrashCenterZ;
            float distSq = (dx * dx) + (dz * dz);

            if (string.Equals(def.Name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                float weaverRadiusSq = FrontierForkWeaverSkipRadiusMeters * FrontierForkWeaverSkipRadiusMeters;
                return distSq <= weaverRadiusSq;
            }

            if (!IsFrontierForkScfuCrashMobName(def.Name))
            {
                return false;
            }

            if (IsFrontierForkDemonicCorridorExempt(def))
            {
                return false;
            }

            float mobRadiusSq = FrontierForkCrashMobRadiusMeters * FrontierForkCrashMobRadiusMeters;
            return distSq <= mobRadiusSq;
        }

        private static bool IsWithinFrontierForkWeaverSkipBubble(LifeNpc def)
        {
            if (def == null || def.PlayfieldId != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return false;
            }

            float dx = def.X - FrontierForkCrashCenterX;
            float dz = def.Z - FrontierForkCrashCenterZ;
            float distSq = (dx * dx) + (dz * dz);
            float weaverRadiusSq = FrontierForkWeaverSkipRadiusMeters * FrontierForkWeaverSkipRadiusMeters;
            return distSq <= weaverRadiusSq;
        }

        private static bool ShouldDeferFrontierForkSpawn(LifeNpc def)
        {
            if (def == null || def.PlayfieldId != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return false;
            }

            if (!IsWithinFrontierForkWeaverSkipBubble(def))
            {
                return false;
            }

            // PF4310 fork bubble: stagger Weaver + Spinetooth visibility (capture 20260826-212737).
            return string.Equals(def.Name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(def.Name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlayerInFrontierSpinetoothZone(ICharacter character)
        {
            if (character == null || !(character.Controller is PlayerController))
            {
                return false;
            }

            Coordinate pos = character.CalculatePredictedPosition();
            return pos.coordinate.x >= FrontierSpinetoothDeferredMinX
                   && pos.coordinate.z >= FrontierSpinetoothDeferredMinZ
                   && pos.coordinate.z <= FrontierSpinetoothDeferredMaxZ;
        }

        private static bool AnyPlayerReadyForFrontierForkDeferredSpawn(Playfield playfield, out DateTime utcNow)
        {
            utcNow = DateTime.UtcNow;
            if (playfield == null)
            {
                return false;
            }

            foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (character == null || !(character.Controller is PlayerController))
                {
                    continue;
                }

                DateTime loginReadyAtUtc;
                lock (FrontierForkDeferredSync)
                {
                    if (!FrontierForkLoginReadyAtUtc.TryGetValue(character.Identity.Instance, out loginReadyAtUtc))
                    {
                        continue;
                    }
                }

                if ((utcNow - loginReadyAtUtc).TotalSeconds < FrontierForkDeferredLoginGraceSeconds)
                {
                    continue;
                }

                if (IsPlayerInFrontierSpinetoothZone(character))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void NotifyFrontierForkPlayerLoginReady(ICharacter character)
        {
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return;
            }

            lock (FrontierForkDeferredSync)
            {
                FrontierForkLoginReadyAtUtc[character.Identity.Instance] = DateTime.UtcNow;
            }
        }

        internal static void ClearFrontierForkPlayerLoginReady(int characterInstance)
        {
            if (characterInstance == 0)
            {
                return;
            }

            lock (FrontierForkDeferredSync)
            {
                FrontierForkLoginReadyAtUtc.Remove(characterInstance);
            }
        }

        private static int FrontierForkDeferredSpawnKey(LifeNpc def)
        {
            unchecked
            {
                return (def.Name.GetHashCode() * 397) ^ def.X.GetHashCode() ^ def.Z.GetHashCode();
            }
        }

        internal static void TickFrontierForkDeferredSpawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return;
            }

            int[] pendingIndices;
            lock (FrontierForkDeferredSync)
            {
                if (FrontierForkDeferredNpcIndices.Count == 0)
                {
                    return;
                }

                pendingIndices = FrontierForkDeferredNpcIndices.ToArray();
            }

            DateTime utcNow;
            if (!AnyPlayerReadyForFrontierForkDeferredSpawn(playfield, out utcNow))
            {
                return;
            }

            lock (FrontierForkDeferredSync)
            {
                if ((utcNow - FrontierForkDeferredLastBatchAtUtc).TotalSeconds
                    < FrontierForkDeferredBatchIntervalSeconds)
                {
                    return;
                }
            }

            int spawned = 0;
            for (int i = 0; i < pendingIndices.Length && spawned < FrontierForkDeferredSpawnBatchSize; i++)
            {
                LifeNpc def = Npcs[pendingIndices[i]];
                int spawnKey = FrontierForkDeferredSpawnKey(def);
                lock (FrontierForkDeferredSync)
                {
                    if (!FrontierForkDeferredSpawnedKeys.Add(spawnKey))
                    {
                        continue;
                    }
                }

                try
                {
                    if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                    {
                        spawned++;
                        int totalSpawned;
                        lock (FrontierForkDeferredSync)
                        {
                            totalSpawned = FrontierForkDeferredSpawnedKeys.Count;
                        }

                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "NascenceLifeSpawn pf=4310 deferred visibility npc=" + def.Name
                            + " x=" + def.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                            + " z=" + def.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                            + " capture=" + (def.CaptureFolder ?? "none")
                            + " n=" + totalSpawned + "/" + pendingIndices.Length);
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "NascenceLifeSpawn deferred SpawnOne threw npc=" + def.Name
                        + " ex=" + ex.GetType().Name + ": " + ex.Message
                        + " stack=" + ex.StackTrace);
                }
            }

            if (spawned > 0)
            {
                lock (FrontierForkDeferredSync)
                {
                    FrontierForkDeferredLastBatchAtUtc = utcNow;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "NascenceLifeSpawn pf=4310 deferred batch done spawned=" + spawned);
            }
        }

        private static bool IsFrontierForkDemonicCorridorExempt(LifeNpc def)
        {
            return def.X <= FrontierForkCrashCenterX
                && def.Z >= FrontierForkDemonicCorridorMinZ;
        }

        private static bool IsFrontierForkScfuCrashMobName(string name)
        {
            return string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetRedeemedVillageClanScfuUnknown1(out byte[] data)
        {
            data = (byte[])AbanFalaScfuUnknown1.Clone();
            return true;
        }

        internal static int ResolveRedeemedVillageClanCharacterFlags(string name)
        {
            return IsAbanFalaName(name) ? AbanFalaCharacterFlags : RedeemedVillageClanCharacterFlags;
        }

        private static bool IsCurBeatName(string name)
        {
            return string.Equals(name, "Cur-Beat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHumeOcraName(string name)
        {
            return string.Equals(name, "Diviner Aban Hume-Ocra", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathDunaName(string name)
        {
            return string.Equals(name, "Devoted Aban Path-Duna", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLuxWeiName(string name)
        {
            return string.Equals(name, "Sipius Aban Lux-Wei", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] BuildDualMaterialExtTex(string primaryMaterial, string secondaryMaterial, int textureId)
        {
            byte[] buffer = new byte[92];
            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x0B;
            buffer[3] = 0xD3;
            WriteAsciiField(buffer, 4, primaryMaterial, 32);
            WriteTextureId(buffer, 36, textureId);
            WriteAsciiField(buffer, 48, secondaryMaterial, 32);
            WriteTextureId(buffer, 80, textureId);
            return buffer;
        }

        private static void WriteAsciiField(byte[] buffer, int offset, string text, int fieldLength)
        {
            if (buffer == null || string.IsNullOrEmpty(text) || fieldLength <= 0)
            {
                return;
            }

            byte[] ascii = Encoding.ASCII.GetBytes(text);
            int copy = Math.Min(ascii.Length, fieldLength - 1);
            Array.Copy(ascii, 0, buffer, offset, copy);
        }

        private static void WriteTextureId(byte[] buffer, int offset, int textureId)
        {
            buffer[offset] = 0;
            buffer[offset + 1] = (byte)((textureId >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((textureId >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(textureId & 0xFF);
        }

        internal static bool IsPapagenaName(string name)
        {
            return string.Equals(name, "Papagena", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsAbanFalaName(string name)
        {
            return string.Equals(name, "Ecclesiast Aban Fala", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetAbanFalaScfuUnknown1(out byte[] data)
        {
            data = (byte[])AbanFalaScfuUnknown1.Clone();
            return true;
        }

        internal static bool TryGetPapagenaScfuUnknown1(out byte[] data)
        {
            data = (byte[])PapagenaScfuUnknown1.Clone();
            return true;
        }

        internal static PapagenaScfuActiveNano[] GetPapagenaScfuActiveNanos()
        {
            return PapagenaScfuActiveNanos;
        }

        internal static bool UsesCaptureOpenableEmptyCorpse(string name)
        {
            return string.Equals(name, "Barking Chimera", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Dreaming Silvertail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Soul Dredge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Tempterus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hesosas", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Papagena", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Papageno", StringComparison.OrdinalIgnoreCase)
                || NascenceDungeon1Rules.IsDungeonCorpseName(name)
                || NascenceDungeon2Rules.IsDungeonCorpseName(name)
                || NascenceDungeon3Rules.IsDungeonCorpseName(name)
                || NascenceDungeon4Rules.IsDungeonCorpseName(name);
        }

        // Mike: empty loot closes too fast — keep opened empty corpse ~2s before cleanup.
        internal static readonly TimeSpan OpenableEmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(2);

        // Mike: starter-bridge corpse despawn 3 minutes (capture-backed Nascence Life farm mobs).
        internal static readonly TimeSpan CaptureCorpseLifetime = TimeSpan.FromMinutes(3);

        // Mike: Spirit Hunter corpse 30m; Cascading Spirit corpse 2m (20260823-103458).
        internal static readonly TimeSpan SpiritHunterCorpseLifetime = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan CascadingSpiritCorpseLifetime = TimeSpan.FromMinutes(2);
        // Mike: Tempterus corpse 10m; Predator Striker 3m; Papageno (Omni) 30m (20260823-112044).
        internal static readonly TimeSpan TempterusCorpseLifetime = TimeSpan.FromMinutes(10);
        internal static readonly TimeSpan PredatorStrikerCorpseLifetime = TimeSpan.FromMinutes(3);
        internal static readonly TimeSpan PapagenaCorpseLifetime = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan PapagenoCorpseLifetime = TimeSpan.FromMinutes(30);
        // Capture 20260826-052537 + Mike: Hiathlin corpse 2m; Omathon/Hesosas corpse 20m / respawn 30m.
        internal static readonly TimeSpan HiathlinCorpseLifetime = TimeSpan.FromMinutes(2);
        internal static readonly TimeSpan OmathonCorpseLifetime = TimeSpan.FromMinutes(20);
        internal static readonly TimeSpan HesosasCorpseLifetime = TimeSpan.FromMinutes(20);
        // Capture 20260825-202932 Demonic Subjugator boss corpse.
        internal static readonly TimeSpan DemonicSubjugatorCorpseLifetime = TimeSpan.FromMinutes(30);

        internal static bool TryGetCaptureCorpseLifetime(string name, out TimeSpan lifetime)
        {
            if (string.Equals(name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = SpiritHunterCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = CascadingSpiritCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = TempterusCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = PredatorStrikerCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = DemonicSubjugatorCorpseLifetime;
                return true;
            }

            if (IsPapagenaName(name))
            {
                lifetime = PapagenaCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Papageno", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = PapagenoCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = HiathlinCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = OmathonCorpseLifetime;
                return true;
            }

            if (string.Equals(name, "Hesosas", StringComparison.OrdinalIgnoreCase))
            {
                lifetime = HesosasCorpseLifetime;
                return true;
            }

            if (UsesCaptureOpenableEmptyCorpse(name))
            {
                lifetime = CaptureCorpseLifetime;
                return true;
            }

            lifetime = TimeSpan.Zero;
            return false;
        }

        // Mike: starter-bridge Chimera / Silvertail soft-respawn 2 minutes after death.
        private const double BarkingChimeraRespawnSeconds = 120.0;
        private const double SwiftSilvertailRespawnSeconds = 120.0;
        // Mike: Spirit Hunter / Cascading Spirit soft-respawn 10 minutes.
        private const double SpiritHunterRespawnSeconds = 600.0;
        private const double CascadingSpiritRespawnSeconds = 600.0;
        // Mike: Tempterus 5m (capture 112044 ~310s); Predator Striker 2m.
        private const double TempterusRespawnSeconds = 300.0;
        private const double PredatorStrikerRespawnSeconds = 120.0;
        private const double DeadlyPredatorRespawnSeconds = 600.0;
        private const double PapagenaRespawnSeconds = 1200.0;
        private const double PapagenaPadRespawnSeconds = 120.0;
        // Capture 20260823-112044 Papageno (Omni): Mike stated 20m respawn / 30m corpse.
        private const double PapagenoRespawnSeconds = 1200.0;
        // Capture 20260826-052537 + Mike: Hiathlin respawn 5m; Omathon/Hesosas respawn 30m.
        private const double HiathlinRespawnSeconds = 300.0;
        private const double OmathonRespawnSeconds = 1800.0;
        private const double HesosasRespawnSeconds = 1800.0;
        private const float SoftRespawnAliveProximityMetersSq = 6.25f; // 2.5m, same as Alex-pad
        private static readonly object ChimeraRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> ChimeraNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object SilvertailRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> SilvertailNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object SpiritHunterRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> SpiritHunterNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object CascadingSpiritRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> CascadingSpiritNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object TempterusRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> TempterusNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object PredatorStrikerRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> PredatorStrikerNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object DeadlyPredatorRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> DeadlyPredatorNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object PapagenaAreaRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> PapagenaAreaNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object PapagenoRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> PapagenoNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object HiathlinRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> HiathlinNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object OmathonRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> OmathonNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static readonly object HesosasRespawnSync = new object();
        private static readonly Dictionary<int, DateTime[]> HesosasNextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();
        private static int[] chimeraSpawnIndices;
        private static int[] silvertailSpawnIndices;
        private static int[] spiritHunterSpawnIndices;
        private static int[] cascadingSpiritSpawnIndices;
        private static int[] tempterusSpawnIndices;
        private static int[] predatorStrikerSpawnIndices;
        private static int[] deadlyPredatorSpawnIndices;
        private static int[] papagenaAreaSpawnIndices;
        private static int[] papagenoSpawnIndices;
        private static int[] hiathlinSpawnIndices;
        private static int[] omathonSpawnIndices;
        private static int[] hesosasSpawnIndices;

        private static int[] ChimeraSpawnIndices
        {
            get
            {
                if (chimeraSpawnIndices != null)
                {
                    return chimeraSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                chimeraSpawnIndices = list.ToArray();
                return chimeraSpawnIndices;
            }
        }

        private static int[] SilvertailSpawnIndices
        {
            get
            {
                if (silvertailSpawnIndices != null)
                {
                    return silvertailSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                silvertailSpawnIndices = list.ToArray();
                return silvertailSpawnIndices;
            }
        }

        private static int[] SpiritHunterSpawnIndices
        {
            get
            {
                if (spiritHunterSpawnIndices != null)
                {
                    return spiritHunterSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                spiritHunterSpawnIndices = list.ToArray();
                return spiritHunterSpawnIndices;
            }
        }

        private static int[] CascadingSpiritSpawnIndices
        {
            get
            {
                if (cascadingSpiritSpawnIndices != null)
                {
                    return cascadingSpiritSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                cascadingSpiritSpawnIndices = list.ToArray();
                return cascadingSpiritSpawnIndices;
            }
        }

        private static int[] TempterusSpawnIndices
        {
            get
            {
                if (tempterusSpawnIndices != null)
                {
                    return tempterusSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Tempterus", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                tempterusSpawnIndices = list.ToArray();
                return tempterusSpawnIndices;
            }
        }

        private static int[] PredatorStrikerSpawnIndices
        {
            get
            {
                if (predatorStrikerSpawnIndices != null)
                {
                    return predatorStrikerSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                predatorStrikerSpawnIndices = list.ToArray();
                return predatorStrikerSpawnIndices;
            }
        }

        private static int[] DeadlyPredatorSpawnIndices
        {
            get
            {
                if (deadlyPredatorSpawnIndices != null)
                {
                    return deadlyPredatorSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                deadlyPredatorSpawnIndices = list.ToArray();
                return deadlyPredatorSpawnIndices;
            }
        }

        private static int[] PapagenaAreaSpawnIndices
        {
            get
            {
                if (papagenaAreaSpawnIndices != null)
                {
                    return papagenaAreaSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    LifeNpc def = Npcs[i];
                    if (IsPapagenaName(def.Name)
                        || string.Equals(def.CaptureFolder, "20260822-104635", StringComparison.Ordinal))
                    {
                        list.Add(i);
                    }
                }

                papagenaAreaSpawnIndices = list.ToArray();
                return papagenaAreaSpawnIndices;
            }
        }

        private static int[] PapagenoSpawnIndices
        {
            get
            {
                if (papagenoSpawnIndices != null)
                {
                    return papagenoSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Papageno", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                papagenoSpawnIndices = list.ToArray();
                return papagenoSpawnIndices;
            }
        }

        private static int[] HiathlinSpawnIndices
        {
            get
            {
                if (hiathlinSpawnIndices != null)
                {
                    return hiathlinSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Npcs[i].Name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                hiathlinSpawnIndices = list.ToArray();
                return hiathlinSpawnIndices;
            }
        }

        private static int[] OmathonSpawnIndices
        {
            get
            {
                if (omathonSpawnIndices != null)
                {
                    return omathonSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Omathon", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                omathonSpawnIndices = list.ToArray();
                return omathonSpawnIndices;
            }
        }

        private static int[] HesosasSpawnIndices
        {
            get
            {
                if (hesosasSpawnIndices != null)
                {
                    return hesosasSpawnIndices;
                }

                var list = new List<int>();
                for (int i = 0; i < Npcs.Length; i++)
                {
                    if (string.Equals(Npcs[i].Name, "Hesosas", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(i);
                    }
                }

                hesosasSpawnIndices = list.ToArray();
                return hesosasSpawnIndices;
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            lock (ChimeraRespawnSync)
            {
                ChimeraNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (SilvertailRespawnSync)
            {
                SilvertailNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (SpiritHunterRespawnSync)
            {
                SpiritHunterNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (CascadingSpiritRespawnSync)
            {
                CascadingSpiritNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (TempterusRespawnSync)
            {
                TempterusNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (PredatorStrikerRespawnSync)
            {
                PredatorStrikerNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (DeadlyPredatorRespawnSync)
            {
                DeadlyPredatorNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (PapagenaAreaRespawnSync)
            {
                PapagenaAreaNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (PapagenoRespawnSync)
            {
                PapagenoNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (HiathlinRespawnSync)
            {
                HiathlinNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (OmathonRespawnSync)
            {
                OmathonNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            lock (HesosasRespawnSync)
            {
                HesosasNextRespawnUtcByPlayfield.Remove(playfieldInstance);
            }

            if (playfieldInstance == NascenceLifeContentModule.FrontierPlayfieldId)
            {
                lock (FrontierForkDeferredSync)
                {
                    FrontierForkDeferredNpcIndices.Clear();
                    FrontierForkDeferredSpawnedKeys.Clear();
                    FrontierForkLoginReadyAtUtc.Clear();
                    FrontierForkDeferredLastBatchAtUtc = DateTime.MinValue;
                }
            }
        }

        public static void TickBarkingChimeraRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId)
            {
                return;
            }

            int[] indices = ChimeraSpawnIndices;
            DateTime[] timers;
            lock (ChimeraRespawnSync)
            {
                if (!ChimeraNextRespawnUtcByPlayfield.TryGetValue(pf, out timers)
                    || timers == null
                    || timers.Length != indices.Length)
                {
                    timers = new DateTime[indices.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    ChimeraNextRespawnUtcByPlayfield[pf] = timers;
                }
            }

            for (int slot = 0; slot < indices.Length; slot++)
            {
                LifeNpc def = Npcs[indices[slot]];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                if (HasLivingBarkingChimeraNear(playfield, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
                else if (timers[slot] == DateTime.MaxValue)
                {
                    timers[slot] = DateTime.UtcNow.AddSeconds(BarkingChimeraRespawnSeconds);
                }
                else if (!(timers[slot] > DateTime.UtcNow)
                         && SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
            }
        }

        private static bool HasLivingBarkingChimeraNear(Playfield playfield, LifeNpc def)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.CalculatePredictedPosition().x - def.X;
                float dz = candidate.CalculatePredictedPosition().z - def.Z;
                if ((dx * dx) + (dz * dz) <= SoftRespawnAliveProximityMetersSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static void TickSwiftSilvertailRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId)
            {
                return;
            }

            int[] indices = SilvertailSpawnIndices;
            DateTime[] timers;
            lock (SilvertailRespawnSync)
            {
                if (!SilvertailNextRespawnUtcByPlayfield.TryGetValue(pf, out timers)
                    || timers == null
                    || timers.Length != indices.Length)
                {
                    timers = new DateTime[indices.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    SilvertailNextRespawnUtcByPlayfield[pf] = timers;
                }
            }

            for (int slot = 0; slot < indices.Length; slot++)
            {
                LifeNpc def = Npcs[indices[slot]];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                if (HasLivingSwiftSilvertailNear(playfield, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
                else if (timers[slot] == DateTime.MaxValue)
                {
                    timers[slot] = DateTime.UtcNow.AddSeconds(SwiftSilvertailRespawnSeconds);
                }
                else if (!(timers[slot] > DateTime.UtcNow)
                         && SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
            }
        }

        private static bool HasLivingSwiftSilvertailNear(Playfield playfield, LifeNpc def)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.CalculatePredictedPosition().x - def.X;
                float dz = candidate.CalculatePredictedPosition().z - def.Z;
                if ((dx * dx) + (dz * dz) <= SoftRespawnAliveProximityMetersSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static void TickSpiritHunterRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                SpiritHunterSpawnIndices,
                SpiritHunterRespawnSync,
                SpiritHunterNextRespawnUtcByPlayfield,
                SpiritHunterRespawnSeconds,
                "Nascence Spirit Hunter");
        }

        public static void TickCascadingSpiritRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                CascadingSpiritSpawnIndices,
                CascadingSpiritRespawnSync,
                CascadingSpiritNextRespawnUtcByPlayfield,
                CascadingSpiritRespawnSeconds,
                "Cascading Spirit");
        }

        public static void TickTempterusRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                TempterusSpawnIndices,
                TempterusRespawnSync,
                TempterusNextRespawnUtcByPlayfield,
                TempterusRespawnSeconds,
                "Tempterus");
        }

        public static void TickPredatorStrikerRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                PredatorStrikerSpawnIndices,
                PredatorStrikerRespawnSync,
                PredatorStrikerNextRespawnUtcByPlayfield,
                PredatorStrikerRespawnSeconds,
                "Predator Striker");
        }

        public static void TickDeadlyPredatorRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                DeadlyPredatorSpawnIndices,
                DeadlyPredatorRespawnSync,
                DeadlyPredatorNextRespawnUtcByPlayfield,
                DeadlyPredatorRespawnSeconds,
                "Deadly Predator");
        }

        public static void TickPapagenoRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                PapagenoSpawnIndices,
                PapagenoRespawnSync,
                PapagenoNextRespawnUtcByPlayfield,
                PapagenoRespawnSeconds,
                "Papageno");
        }

        public static void TickHiathlinRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawnByDefName(
                playfield,
                playfieldIdentity,
                activateNpc,
                HiathlinSpawnIndices,
                HiathlinRespawnSync,
                HiathlinNextRespawnUtcByPlayfield,
                HiathlinRespawnSeconds);
        }

        public static void TickOmathonRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                OmathonSpawnIndices,
                OmathonRespawnSync,
                OmathonNextRespawnUtcByPlayfield,
                OmathonRespawnSeconds,
                "Omathon");
        }

        public static void TickHesosasRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            TickNamedSoftRespawn(
                playfield,
                playfieldIdentity,
                activateNpc,
                HesosasSpawnIndices,
                HesosasRespawnSync,
                HesosasNextRespawnUtcByPlayfield,
                HesosasRespawnSeconds,
                "Hesosas");
        }

        private static void TickNamedSoftRespawnByDefName(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int[] indices,
            object sync,
            Dictionary<int, DateTime[]> timersByPlayfield,
            double respawnSeconds)
        {
            if (playfield == null || activateNpc == null || indices == null || indices.Length == 0)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId)
            {
                return;
            }

            DateTime[] timers;
            lock (sync)
            {
                if (!timersByPlayfield.TryGetValue(pf, out timers)
                    || timers == null
                    || timers.Length != indices.Length)
                {
                    timers = new DateTime[indices.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    timersByPlayfield[pf] = timers;
                }
            }

            for (int slot = 0; slot < indices.Length; slot++)
            {
                LifeNpc def = Npcs[indices[slot]];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                if (HasLivingNamedNpcNear(playfield, def, def.Name))
                {
                    timers[slot] = DateTime.MaxValue;
                }
                else if (timers[slot] == DateTime.MaxValue)
                {
                    timers[slot] = DateTime.UtcNow.AddSeconds(respawnSeconds);
                }
                else if (!(timers[slot] > DateTime.UtcNow)
                         && SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
            }
        }

        private static void TickNamedSoftRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int[] indices,
            object sync,
            Dictionary<int, DateTime[]> timersByPlayfield,
            double respawnSeconds,
            string npcName)
        {
            if (playfield == null || activateNpc == null || indices == null || indices.Length == 0)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId)
            {
                return;
            }

            DateTime[] timers;
            lock (sync)
            {
                if (!timersByPlayfield.TryGetValue(pf, out timers)
                    || timers == null
                    || timers.Length != indices.Length)
                {
                    timers = new DateTime[indices.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    timersByPlayfield[pf] = timers;
                }
            }

            for (int slot = 0; slot < indices.Length; slot++)
            {
                LifeNpc def = Npcs[indices[slot]];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                if (HasLivingNamedNpcNear(playfield, def, npcName))
                {
                    timers[slot] = DateTime.MaxValue;
                }
                else if (timers[slot] == DateTime.MaxValue)
                {
                    timers[slot] = DateTime.UtcNow.AddSeconds(respawnSeconds);
                }
                else if (!(timers[slot] > DateTime.UtcNow)
                         && SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
            }
        }

        private static bool HasLivingNamedNpcNear(Playfield playfield, LifeNpc def, string npcName)
        {
            float proximitySq = SoftRespawnAliveProximityMetersSq;
            if (string.Equals(npcName, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(npcName, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(npcName, "Omathon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(npcName, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                // PF4310 patrol / pocket bosses can drift from spawn anchor.
                proximitySq = 8100f; // 90m
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, npcName, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.CalculatePredictedPosition().x - def.X;
                float dz = candidate.CalculatePredictedPosition().z - def.Z;
                if ((dx * dx) + (dz * dz) <= proximitySq)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mike: Cascading Spirit social aggro within 10m (capture 20260823-103458 cave pack).
        /// </summary>
        public static ICharacter[] FindCascadingSpiritSocialAggroAllies(ICharacter npc, ICharacter target)
        {
            if (npc == null
                || target == null
                || npc.Playfield == null
                || !string.Equals(npc.Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return new ICharacter[0];
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return new ICharacter[0];
            }

            var allies = new List<ICharacter>();
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, CascadingSpiritSocialAggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is NPCController)
                    || !string.Equals(candidate.Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance != 0)
                {
                    continue;
                }

                allies.Add(candidate);
            }

            return allies.ToArray();
        }

        /// <summary>
        /// Mike: Predator Striker social aggro within 10m (capture 20260826-054154 pocket).
        /// </summary>
        public static ICharacter[] FindPredatorStrikerSocialAggroAllies(ICharacter npc, ICharacter target)
        {
            if (npc == null
                || target == null
                || npc.Playfield == null
                || !string.Equals(npc.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                return new ICharacter[0];
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return new ICharacter[0];
            }

            var allies = new List<ICharacter>();
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, PredatorStrikerSocialAggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is NPCController)
                    || !string.Equals(candidate.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance != 0)
                {
                    continue;
                }

                allies.Add(candidate);
            }

            return allies.ToArray();
        }

        /// <summary>
        /// Capture 20260827-221909: Crippler of Growth social aggro within 10m (7A372E07 joined 7A372E06).
        /// </summary>
        public static ICharacter[] FindCripplerOfGrowthSocialAggroAllies(ICharacter npc, ICharacter target)
        {
            if (npc == null
                || target == null
                || npc.Playfield == null
                || !string.Equals(npc.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                return new ICharacter[0];
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return new ICharacter[0];
            }

            var allies = new List<ICharacter>();
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, CripplerOfGrowthSocialAggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is NPCController)
                    || !string.Equals(candidate.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance != 0)
                {
                    continue;
                }

                allies.Add(candidate);
            }

            return allies.ToArray();
        }

        public static void TickPapagenaAreaRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return;
            }

            int[] indices = PapagenaAreaSpawnIndices;
            DateTime[] timers;
            lock (PapagenaAreaRespawnSync)
            {
                if (!PapagenaAreaNextRespawnUtcByPlayfield.TryGetValue(pf, out timers)
                    || timers == null
                    || timers.Length != indices.Length)
                {
                    timers = new DateTime[indices.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    PapagenaAreaNextRespawnUtcByPlayfield[pf] = timers;
                }
            }

            for (int slot = 0; slot < indices.Length; slot++)
            {
                LifeNpc def = Npcs[indices[slot]];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                double respawnSeconds = IsPapagenaName(def.Name)
                    ? PapagenaRespawnSeconds
                    : PapagenaPadRespawnSeconds;

                if (HasLivingMobNear(playfield, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
                else if (timers[slot] == DateTime.MaxValue)
                {
                    timers[slot] = DateTime.UtcNow.AddSeconds(respawnSeconds);
                }
                else if (!(timers[slot] > DateTime.UtcNow)
                         && SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    timers[slot] = DateTime.MaxValue;
                }
            }
        }

        private static bool HasLivingMobNear(Playfield playfield, LifeNpc def)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, def.Name, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.CalculatePredictedPosition().x - def.X;
                float dz = candidate.CalculatePredictedPosition().z - def.Z;
                if ((dx * dx) + (dz * dz) <= SoftRespawnAliveProximityMetersSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly LifeNpc[] Npcs =
        {
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 6, Health = 180, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 824.0807f, Y = 32.41f, Z = 1238.53235f,
                Hx = 0f, Hy = -0.331301033f, Hz = 0f, Hw = 0.943525136f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 8, Health = 240, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 829.6133f, Y = 32.0579872f, Z = 1202.55847f,
                Hx = 0.06849744f, Hy = -0.9184757f, Hz = 0.0289674476f, Hw = 0.3884217f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 8, Health = 240, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 884.0123f, Y = 31.5381317f, Z = 1104.36157f,
                Hx = 0.0163042024f, Hy = -0.679217458f, Hz = 0.005894537f, Hw = 0.7337323f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 6, Health = 180, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 888.4846f, Y = 31.58028f, Z = 1107.71887f,
                Hx = 0.0194900241f, Hy = -0.220980912f, Hz = 0.0117061147f, Hw = 0.9750131f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 7, Health = 210, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 879.0811f, Y = 30.6347942f, Z = 1126.05432f,
                Hx = -0.10982085f, Hy = -0.6473039f, Hz = 0.09501119f, Hw = 0.748271346f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 7, Health = 210, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 823.5885f, Y = 31.7542725f, Z = 1220.27515f,
                Hx = 0.02396098f, Hy = -0.3212905f, Hz = 0.07040514f, Hw = 0.944055855f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 878.012634f, Y = 28.39159f, Z = 1097.16956f,
                Hx = 0.022185389f, Hy = -0.51345557f, Hz = 0.0354322642f, Hw = 0.857097268f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Garden 160734 — patrol cluster @ 806/1189 (route 1/3).
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 6, Health = 180, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 806.3f, Y = 29.7f, Z = 1189.1f,
                Hx = 0.102776475f, Hy = -0.57205826f, Hz = 0.0179232024f, Hw = 0.813550949f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 805.997f, 29.410f, 1193.337f },
                    new[] { 817.752f, 29.079f, 1187.273f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 8, Health = 240, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 806.5f, Y = 29.7f, Z = 1189.5f,
                Hx = 0.012532426f, Hy = -0.335815579f, Hz = 0.03512459f, Hw = 0.94118917f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 789.936f, 31.561f, 1195.725f },
                    new[] { 796.934f, 31.674f, 1181.981f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 6, Health = 180, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 806.1f, Y = 29.7f, Z = 1188.7f,
                Hx = 0f, Hy = 0.9368594f, Hz = 0f, Hw = 0.349706143f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 810.740f, 29.316f, 1177.945f },
                    new[] { 830.257f, 31.155f, 1181.520f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                // Garden 160734 — patrol cluster @ 803/1213 (route 1/3).
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 8, Health = 240, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 802.8f, Y = 29.2f, Z = 1213.2f,
                Hx = 0.00485091656f, Hy = 0.06504547f, Hz = -0.07421241f, Hw = 0.995107055f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 815.7023f, 29.9654f, 1207.0614f },
                    new[] { 825.2365f, 31.3955f, 1200.2180f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 802.9f, Y = 29.2f, Z = 1213.0f,
                Hx = 0f, Hy = -0.168575823f, Hz = 0f, Hw = 0.9856887f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 802.2364f, 30.5391f, 1184.5519f },
                    new[] { 807.8224f, 29.7839f, 1177.8626f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 802.7f, Y = 29.2f, Z = 1213.4f,
                Hx = 0f, Hy = -0.52254647f, Hz = 0f, Hw = 0.8526108f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 825.517f, 33.010f, 1146.048f },
                    new[] { 841.762f, 32.410f, 1142.206f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 837.6182f, Y = 32.05873f, Z = 1176.55981f,
                Hx = 0.0246792249f, Hy = -0.330921382f, Hz = 0.0701566041f, Hw = 0.9407231f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 7, Health = 210, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 845.581848f, Y = 41.7555733f, Z = 1119.59875f,
                Hx = -0.113041125f, Hy = -0.7208961f, Hz = -0.09206367f, Hw = 0.677535832f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 802.2795f, Y = 32.157917f, Z = 1153.69226f,
                Hx = 0.0482197553f, Hy = -0.6465741f, Hz = 0.0566203855f, Hw = 0.7592173f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 5, Health = 150, MonsterData = 209173, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 854.0158f, Y = 28.51187f, Z = 1110.60986f,
                Hx = 0.07041768f, Hy = 0.9440499f, Hz = -0.02396646f, Hw = 0.321304739f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Barking Chimera",
                Level = 8, Health = 240, MonsterData = 209173, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 845.5315f, Y = 32.41f, Z = 1137.07373f,
                Hx = 0f, Hy = 0.9330358f, Hz = 0f, Hw = 0.35978356f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-230406",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Nascence Spirit Hunter",
                Level = 12, Health = 975, MonsterData = 209215, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 857.9946f, Y = 17.345f, Z = 1435.7676f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A19FD9E",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 853.9039f, Y = 16.865f, Z = 1340.2701f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1B444F",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 839.4312f, Y = 7.21f, Z = 1370.654f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1C3B73",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 869.8517f, Y = 9.365f, Z = 1364.7692f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1C3B88",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 840.1165f, Y = 7.21f, Z = 1370.1171f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A2260E0",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Nascence Spirit Hunter",
                Level = 12, Health = 975, MonsterData = 209215, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 862.5264f, Y = 7.21f, Z = 1376.4276f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A226153",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 854.4845f, Y = 16.865f, Z = 1342.1422f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A233B75",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 919.2295f, Y = 26.865f, Z = 1369.02686f,
                Hx = 0f, Hy = -0.924079537f, Hz = 0f, Hw = 0.382200271f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 906.8674f, Y = 16.865f, Z = 1369.0242f,
                Hx = 0f, Hy = -0.756919265f, Hz = 0f, Hw = 0.6535084f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1C3CA1",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 879.7872f, Y = 26.865f, Z = 1334.53955f,
                Hx = 0f, Hy = -0.391245067f, Hz = 0f, Hw = 0.920286536f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 877.6614f, Y = 9.365f, Z = 1369.1194f,
                Hx = 0f, Hy = -0.397909284f, Hz = 0f, Hw = 0.917424738f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1C3B42",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 876.0099f, Y = 9.365f, Z = 1357.9469f,
                Hx = 0f, Hy = 0.8991294f, Hz = 0f, Hw = 0.437682927f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A233B6C",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 891.5055f, Y = 16.865f, Z = 1383.57971f,
                Hx = 0f, Hy = -0.4076205f, Hz = 0f, Hw = 0.913151443f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 901.020447f, Y = 16.865f, Z = 1376.288f,
                Hx = 0f, Hy = -0.4414332f, Hz = 0f, Hw = 0.8972941f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 849.55896f, Y = 9.845f, Z = 1399.01624f,
                Hx = 0f, Hy = 0.908706963f, Hz = 0f, Hw = 0.4174346f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 840.802856f, Y = 9.845f, Z = 1399.76208f,
                Hx = 0f, Hy = 0.917137861f, Hz = 0f, Hw = 0.398570061f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 849.0526f, Y = 9.845f, Z = 1408.323f,
                Hx = 0f, Hy = 0.937879264f, Hz = 0f, Hw = 0.346961826f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 865.7973f, Y = 9.845f, Z = 1420.8308f,
                Hx = 0f, Hy = -0.93436414f, Hz = 0f, Hw = 0.356319547f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A233A2E",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 814.0223f, Y = 17.345f, Z = 1388.61926f,
                Hx = 0f, Hy = 0.945535243f, Hz = 0f, Hw = 0.325519741f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 822.421143f, Y = 17.345f, Z = 1397.90381f,
                Hx = 0f, Hy = 0.943712234f, Hz = 0f, Hw = 0.330767572f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 878.658936f, Y = 16.865f, Z = 1350.7041f,
                Hx = 0f, Hy = 0.0119212223f, Hz = 0f, Hw = 0.999928951f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 876.3686f, Y = 21.865f, Z = 1337.82739f,
                Hx = 0f, Hy = 0.3690046f, Hz = 0f, Hw = 0.929427564f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 848.916f, Y = 16.865f, Z = 1341.5485f,
                Hx = 0f, Hy = -0.76239866f, Hz = 0f, Hw = 0.6471076f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A1C3B70",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Cascading Spirit",
                Level = 10, Health = 250, MonsterData = 217008, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 878.2246f, Y = 9.60790348f, Z = 1408.66309f,
                Hx = 0f, Hy = -0.813920438f, Hz = 0f, Hw = 0.5809763f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Corrupting Imp",
                Level = 20, Health = 950, MonsterData = 40515, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 760.013062f, Y = 26.2293949f, Z = 1606.74292f,
                Hx = 0f, Hy = 0.9168395f, Hz = 0f, Hw = 0.399255961f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Corrupting Imp",
                Level = 20, Health = 950, MonsterData = 40515, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 720.0089f, Y = 31.2246685f, Z = 1560.83362f,
                Hx = 0f, Hy = 0.582929969f, Hz = 0f, Hw = 0.812522352f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Corrupting Imp 7A2ED7B9 SAW 134 VQIR; patrol near boss.
                PlayfieldId = 4310,
                Name = "Corrupting Imp",
                Level = 20, Health = 950, MonsterData = 40515, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 779.0792f, Y = 31.3622437f, Z = 1565.05212f,
                Hx = 0f, Hy = -0.960809469f, Hz = 0f, Hw = 0.27720958f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 775.64f, 31.80f, 1560.05f },
                    new[] { 779.44f, 31.34f, 1565.29f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 869.711548f, Y = 49.62684f, Z = 1135.08862f,
                Hx = 0f, Hy = 0.9964082f, Hz = 0f, Hw = 0.0846794f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 940.0171f, Y = 49.3002357f, Z = 1664.98669f,
                Hx = 0f, Hy = 0.110352233f, Hz = 0f, Hw = 0.99389255f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 949.6507f, Y = 49.61795f, Z = 1651.32568f,
                Hx = 0f, Hy = 0.429172933f, Hz = 0f, Hw = 0.9032223f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 930.2991f, Y = 49.4979477f, Z = 1663.69409f,
                Hx = 0f, Hy = -0.160201013f, Hz = 0f, Hw = 0.9870844f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 849.906738f, Y = 48.50282f, Z = 1667.76929f,
                Hx = 0f, Hy = 0.2295472f, Hz = 0f, Hw = 0.973297536f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 845.1725f, Y = 48.9545441f, Z = 1669.63232f,
                Hx = 0f, Hy = -0.0631535649f, Hz = 0f, Hw = 0.99800384f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 838.6937f, Y = 48.976635f, Z = 1666.52063f,
                Hx = 0f, Hy = -0.485683471f, Hz = 0f, Hw = 0.8741348f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 854.19574f, Y = 49.1192474f, Z = 1667.67017f,
                Hx = 0f, Hy = -0.07224095f, Hz = 0f, Hw = 0.99738723f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 830.412048f, Y = 49.5189934f, Z = 1210.2074f,
                Hx = 0f, Hy = -0.5051915f, Hz = 0f, Hw = 0.8630073f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 880.158936f, Y = 49.8768539f, Z = 1309.017f,
                Hx = 0f, Hy = -0.474604934f, Hz = 0f, Hw = 0.880198956f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 891.7111f, Y = 49.6234245f, Z = 1323.57788f,
                Hx = 0f, Hy = -0.5819399f, Hz = 0f, Hw = 0.8132318f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 897.369751f, Y = 49.72488f, Z = 1326.35181f,
                Hx = 0f, Hy = -0.0887803361f, Hz = 0f, Hw = 0.996051252f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 765.0136f, Y = 48.4368553f, Z = 1363.5896f,
                Hx = 0f, Hy = 0.999937832f, Hz = 0f, Hw = 0.0111503238f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 767.753052f, Y = 49.12797f, Z = 1366.96716f,
                Hx = 0f, Hy = 0.577258945f, Hz = 0f, Hw = 0.816561162f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 726.4361f, Y = 49.7149963f, Z = 1300.69873f,
                Hx = 0f, Hy = -0.252235085f, Hz = 0f, Hw = 0.967666f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 729.289368f, Y = 49.7149963f, Z = 1283.03857f,
                Hx = 0f, Hy = 0.8690948f, Hz = 0f, Hw = 0.494645566f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 730.942749f, Y = 49.7149963f, Z = 1284.50269f,
                Hx = 0f, Hy = 0.9822359f, Hz = 0f, Hw = 0.1876503f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 743.461f, Y = 48.9561157f, Z = 1242.4585f,
                Hx = 0f, Hy = 0.213585973f, Hz = 0f, Hw = 0.976924241f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 762.3445f, Y = 48.99246f, Z = 1227.79578f,
                Hx = 0f, Hy = 0.12819685f, Hz = 0f, Hw = 0.99174875f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 826.6433f, Y = 49.6784554f, Z = 1248.63928f,
                Hx = 0f, Hy = -0.851068139f, Hz = 0f, Hw = 0.5250553f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 830.280457f, Y = 49.59512f, Z = 1222.44165f,
                Hx = 0f, Hy = -0.89080137f, Hz = 0f, Hw = 0.45439294f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 826.9979f, Y = 50.1032524f, Z = 1268.36389f,
                Hx = 0f, Hy = -0.6104301f, Hz = 0f, Hw = 0.7920701f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 830.1933f, Y = 49.79077f, Z = 1285.495f,
                Hx = 0f, Hy = -0.6778765f, Hz = 0f, Hw = 0.7351758f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 892.1318f, Y = 49.5123749f, Z = 1109.83191f,
                Hx = 0f, Hy = -0.8475218f, Hz = 0f, Hw = 0.5307606f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 890.7891f, Y = 49.6057854f, Z = 1111.66052f,
                Hx = 0f, Hy = 0.999961555f, Hz = 0f, Hw = 0.008769952f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 839.245544f, Y = 33.5793839f, Z = 1303.38757f,
                Hx = 0f, Hy = -0.264197767f, Hz = 0f, Hw = 0.964468539f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 792.4533f, Y = 48.33625f, Z = 1175.214f,
                Hx = 0f, Hy = 0.9016611f, Hz = 0f, Hw = 0.432443351f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 785.7208f, Y = 49.0370636f, Z = 1189.92871f,
                Hx = 0f, Hy = 0.4343516f, Hz = 0f, Hw = 0.9007434f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 840.397766f, Y = 49.6193733f, Z = 1182.51685f,
                Hx = 0f, Hy = -0.8941797f, Hz = 0f, Hw = 0.4477082f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Crippler of Growth",
                Level = 10, Health = 250, MonsterData = 209333, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 776.2069f, Y = 48.9434662f, Z = 1208.32019f,
                Hx = 0f, Hy = 0.48484233f, Hz = 0f, Hw = 0.8746016f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260826-054154 Deadly Predator 7A2FFA29 @ striker pocket edge (~747/1968).
                PlayfieldId = 4310,
                Name = "Deadly Predator",
                Level = 20, Health = 2375, MonsterData = 209022, Scale = 128, VisualFlags = 31, HeadMesh = 0,
                X = 747.658936f, Y = 30.809124f, Z = 1968.21631f,
                Hx = 0f, Hy = 0.900533855f, Hz = 0f, Hw = 0.4322828f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Deadly Predator 7A2ED7B6 ExtTex sabre self 235170 SAW 171.
                PlayfieldId = 4310,
                Name = "Deadly Predator",
                Level = 20, Health = 2375, MonsterData = 209022, Scale = 128, VisualFlags = 31, HeadMesh = 0,
                X = 737.1644f, Y = 26.258f, Z = 1584.936f,
                Hx = 0.000331116054f, Hy = 0.6274317f, Hz = -0.0306506716f, Hw = 0.778068066f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 Disease-Ridden Rafter 7A226106 Side=Monster.
                PlayfieldId = 4310,
                Name = "Disease-Ridden Rafter",
                Level = 9, Health = 180, MonsterData = 212186, Scale = 125, VisualFlags = 31, HeadMesh = 0,
                X = 720.0085f, Y = 31.8100014f, Z = 1356.88525f,
                Hx = -0.01602128f, Hy = 0.73755366f, Hz = 0.01466116f, Hw = 0.6749392f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 720.0085f, 31.8100014f, 1356.88525f },
                    new[] { 732.1141f, 32.3813438f, 1355.809f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 Disease-Ridden Rafter 7A233C9B near Papageno.
                PlayfieldId = 4310,
                Name = "Disease-Ridden Rafter",
                Level = 8, Health = 160, MonsterData = 212186, Scale = 125, VisualFlags = 31, HeadMesh = 0,
                X = 680.027832f, Y = 30.1591129f, Z = 1360.96619f,
                Hx = 0.03018842f, Hy = 0.88050664f, Hz = -0.09919795f, Hw = 0.4625543f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 680.027832f, 30.1591129f, 1360.96619f },
                    new[] { 685.252747f, 30.0100021f, 1357.39661f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                // Capture 20260822-082554 SCFU 7A18D419: Omni questgiver (blue name), flags=277352961.
                PlayfieldId = 4310,
                Name = "Dr. Rosenblatt",
                Level = 100, Health = 6829, MonsterData = 26131, Scale = 112, VisualFlags = 31,
                CharacterFlags = 277352961, HeadMesh = 40253,
                X = 882.7177f, Y = 28.8100014f, Z = 1572.25793f,
                Hx = 0f, Hy = -0.06911527f, Hz = 0f, Hw = 0.997608364f,
                Textures = new[] { new[] { 0, 248006 }, new[] { 1, 247963 }, new[] { 2, 247978 }, new[] { 3, 247917 }, new[] { 4, 248056 } },
                Meshes = new[] { new[] { 0, 234533, 0, 0 }, new[] { 0, 40253, 0, 4 } },
                CaptureFolder = "20260822-082554",
            },
            new LifeNpc
            {
                // Capture 20260822-103209 fight + 082554 SCFU 7A18D461: monsterscale=194, ExtTex "grey" fire, no mesh layer.
                // Clan Papagena — not the Omni Papageno target.
                PlayfieldId = 4310,
                Name = "Papagena",
                Level = 15, Health = 840, MonsterData = 236640, Scale = 194, VisualFlags = 31, HeadMesh = 0,
                X = 955.5843f, Y = 33.3049965f, Z = 1266.085f,
                Hx = 0f, Hy = 0.183506534f, Hz = 0f, Hw = 0.9830185f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 955.5843f, 33.3049965f, 1266.085f },
                    new[] { 958.32312f, 33.9243622f, 1264.25464f },
                },
                CaptureFolder = "20260822-103209",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 SCFU 7A226136 Papageno OmniTek MD=208640 mesh 209532;
                // Capture 20260825-204815 SCFU 7A2ED761 mesh 209541 SAW 139/139/139/101 AttackInfo=32.
                // waypoints (681.29,30.81,1343.87)->(688.60,32.41,1320.36); Clan Silvertail disc kill target.
                PlayfieldId = 4310,
                Name = "Papageno",
                Level = 15, Health = 840, MonsterData = 208640, Scale = 194, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = DefaultAnimalCharacterFlags,
                X = 681.2867f, Y = 30.809f, Z = 1343.86975f,
                Hx = 0f, Hy = 0.988645f, Hz = 0f, Hw = 0.150269851f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                Waypoints = new[]
                {
                    new[] { 681.2867f, 30.809f, 1343.86975f },
                    new[] { 688.60f, 32.41f, 1320.36f },
                },
                CaptureFolder = "20260825-204815",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Frail Rafter",
                Level = 10, Health = 300, MonsterData = 212186, Scale = 125, VisualFlags = 31, HeadMesh = 0,
                X = 916.5201f, Y = 28.838072f, Z = 1268.65063f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Frail Rafter",
                Level = 10, Health = 300, MonsterData = 212186, Scale = 125, VisualFlags = 31, HeadMesh = 0,
                X = 992.7034f, Y = 29.521513f, Z = 1291.54272f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 6, Health = 270, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 966.397034f, Y = 27.61f, Z = 1297.86719f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                // Garden 160734 Hai-Tempterus patrol cluster — 4 distinct capture routes @ ~994/1271.
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 989.706f, Y = 30.272f, Z = 1248.109f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 989.706f, 30.272f, 1248.109f },
                    new[] { 993.992f, 29.709f, 1273.231f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 990.984f, Y = 31.774f, Z = 1231.224f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 990.984f, 31.774f, 1231.224f },
                    new[] { 999.211f, 31.098f, 1263.571f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 8, Health = 200, MonsterData = 209182, Scale = 141, VisualFlags = 31, HeadMesh = 0,
                X = 973.784f, Y = 31.210f, Z = 1215.306f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 973.784f, 31.210f, 1215.306f },
                    new[] { 991.287f, 31.709f, 1232.448f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 1012.224f, Y = 33.740f, Z = 1259.360f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 1012.224f, 33.740f, 1259.360f },
                    new[] { 1013.781f, 36.362f, 1238.263f },
                },
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 8, Health = 200, MonsterData = 209182, Scale = 141, VisualFlags = 31, HeadMesh = 0,
                X = 943.0371f, Y = 27.835f, Z = 1235.41211f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 952.894531f, Y = 28.183382f, Z = 1232.35547f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 956.829f, Y = 29.807936f, Z = 1221.108f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260822-104635",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Dreaming Silvertail",
                Level = 15, Health = 600, MonsterData = 208922, Scale = 73, VisualFlags = 31, HeadMesh = 0,
                X = 826.222656f, Y = 33.08879f, Z = 1277.10657f,
                Hx = 0.07993122f, Hy = -0.5445479f, Hz = 0.121252954f, Hw = 0.8260607f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Dreaming Silvertail",
                Level = 15, Health = 600, MonsterData = 208922, Scale = 73, VisualFlags = 31, HeadMesh = 0,
                X = 823.976868f, Y = 32.41f, Z = 1246.44531f,
                Hx = 0f, Hy = -0.5455931f, Hz = 0f, Hw = 0.838050246f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Dreaming Silvertail",
                Level = 15, Health = 600, MonsterData = 208922, Scale = 73, VisualFlags = 31, HeadMesh = 0,
                X = 847.958435f, Y = 32.41f, Z = 1156.49756f,
                Hx = 0f, Hy = 0.8369775f, Hz = 0f, Hw = 0.547237337f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Dreaming Silvertail",
                Level = 15, Health = 600, MonsterData = 208922, Scale = 73, VisualFlags = 31, HeadMesh = 0,
                X = 778.1665f, Y = 31.4892635f, Z = 1218.39453f,
                Hx = 0.0514909f, Hy = 0.690436542f, Hz = -0.05366284f, Hw = 0.719559848f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Dreaming Silvertail",
                Level = 15, Health = 600, MonsterData = 208922, Scale = 73, VisualFlags = 31, HeadMesh = 0,
                X = 789.352966f, Y = 29.2112961f, Z = 1223.38867f,
                Hx = -0.0405315533f, Hy = -0.5433836f, Hz = -0.06237174f, Hw = 0.836182535f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Emissary of Jobe",
                Level = 200, Health = 164773, MonsterData = 215047, Scale = 100, VisualFlags = 31,
                CharacterFlags = 277352961, HeadMesh = 40656,
                X = 854.6917f, Y = 30.015f, Z = 1062.84363f,
                Hx = 0f, Hy = -0.749773562f, Hz = 0f, Hw = 0.661691844f,
                Textures = new[] { new[] { 0, 215295 }, new[] { 1, 213751 }, new[] { 2, 213807 }, new[] { 3, 215296 }, new[] { 4, 215294 } },
                Meshes = new[] { new[] { 0, 40656, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Guardian of the Weak",
                Level = 200, Health = 329546, MonsterData = 26097, Scale = 100, VisualFlags = 31, HeadMesh = 40111,
                X = 849.671265f, Y = 30.015f, Z = 1063.204f,
                Hx = 0f, Hy = -0.365595758f, Hz = 0f, Hw = -0.930773735f,
                Textures = new[] { new[] { 0, 215295 }, new[] { 1, 213751 }, new[] { 2, 213807 }, new[] { 3, 215296 }, new[] { 4, 213925 } },
                Meshes = new[] { new[] { 0, 20007, 215300, 2 }, new[] { 0, 40111, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 876.595642f, Y = 32.23766f, Z = 1258.82275f,
                Hx = 0f, Hy = -0.990521431f, Hz = 0f, Hw = 0.137358367f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 8, Health = 200, MonsterData = 209182, Scale = 141, VisualFlags = 31, HeadMesh = 0,
                X = 894.7094f, Y = 29.0078278f, Z = 1238.94263f,
                Hx = 0f, Hy = 0.8573512f, Hz = 0f, Hw = 0.514731944f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 10, Health = 250, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 875.9156f, Y = 31.8438034f, Z = 1242.90918f,
                Hx = 0f, Hy = 0.9899217f, Hz = 0f, Hw = 0.141616017f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 8, Health = 200, MonsterData = 209182, Scale = 141, VisualFlags = 31, HeadMesh = 0,
                X = 878.0095f, Y = 32.112812f, Z = 1262.94275f,
                Hx = 0f, Hy = 0.165412545f, Hz = 0f, Hw = 0.9862245f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 8, Health = 200, MonsterData = 209182, Scale = 141, VisualFlags = 31, HeadMesh = 0,
                X = 944.891f, Y = 33.01f, Z = 1187.39929f,
                Hx = 0f, Hy = -0.335440844f, Hz = 0f, Hw = 0.942061245f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 9, Health = 225, MonsterData = 209182, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 879.5006f, Y = 31.2891483f, Z = 1239.99243f,
                Hx = 0f, Hy = -0.9730712f, Hz = 0f, Hw = 0.230504557f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Hai-Tempterus",
                Level = 5, Health = 125, MonsterData = 209182, Scale = 139, VisualFlags = 31, HeadMesh = 0,
                X = 958.7882f, Y = 32.41f, Z = 1343.03589f,
                Hx = 0f, Hy = -0.4779478f, Hz = 0f, Hw = 0.8783882f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFD73
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 780.718262f, Y = 31.210001f, Z = 1752.508910f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFD74
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 781.519100f, Y = 31.210001f, Z = 1767.033810f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4E
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 784.334656f, Y = 31.210001f, Z = 1737.349370f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE46
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 784.384033f, Y = 30.323670f, Z = 1777.581420f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 812.571167f, 26.915647f, 1787.342650f },
                    new[] { 775.703125f, 31.210001f, 1774.565920f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4C
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 788.887146f, Y = 31.210001f, Z = 1756.482790f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 811.693237f, 31.210001f, 1732.054570f },
                    new[] { 804.681946f, 31.210001f, 1728.911130f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4D
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 789.502400f, Y = 31.210001f, Z = 1730.788570f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE48
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 792.868300f, Y = 30.049507f, Z = 1770.924930f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFD70
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 794.684300f, Y = 31.210001f, Z = 1721.945310f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE49
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 794.703200f, Y = 31.210001f, Z = 1729.588620f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 798.418152f, 31.210001f, 1725.845460f },
                    new[] { 791.719666f, 31.210001f, 1732.654910f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4B
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 795.614258f, Y = 30.579650f, Z = 1764.644650f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 816.271729f, 28.835663f, 1751.613770f },
                    new[] { 811.101868f, 28.724684f, 1761.466920f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE47
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 803.885132f, Y = 27.919825f, Z = 1774.106000f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE50
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 806.303900f, Y = 31.210001f, Z = 1719.103150f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4F
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 811.136000f, Y = 31.210001f, Z = 1734.692000f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 792.114380f, 31.210001f, 1756.957030f },
                    new[] { 790.121948f, 31.210001f, 1750.718140f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE45
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 815.574036f, Y = 31.210001f, Z = 1724.869510f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 785.971313f, 31.210001f, 1739.974610f },
                    new[] { 790.208252f, 31.210001f, 1731.844730f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE51
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 816.596069f, Y = 31.210001f, Z = 1721.473750f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE4A
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 816.881200f, Y = 27.639343f, Z = 1766.979740f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE52
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 823.816900f, Y = 31.210001f, Z = 1729.878420f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE43
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 825.670900f, Y = 27.198381f, Z = 1754.772460f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A2FFE42
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 828.399353f, Y = 29.865330f, Z = 1740.701900f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A30AA96
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 829.845300f, Y = 26.371036f, Z = 1773.528930f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 837.538635f, 26.454039f, 1759.734740f },
                    new[] { 824.579163f, 26.410002f, 1782.945560f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-225804 garden Hiathlin 7A30AA95
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 14, Health = 530, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 844.433533f, Y = 31.210001f, Z = 1739.721680f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 844.497314f, 31.210001f, 1734.318240f },
                    new[] { 844.345276f, 30.748772f, 1746.559200f },
                },
                CaptureFolder = "20260826-225804",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED793
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 711.748962f, Y = 33.05171f, Z = 1956.02954f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED794
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 709.331238f, Y = 32.17127f, Z = 1964.31689f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED795
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 671.866f, Y = 36.5488777f, Z = 1967.51208f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED796
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 695.489563f, Y = 32.95223f, Z = 1970.88745f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED797
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 700.7891f, Y = 34.09587f, Z = 1945.67529f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED799
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 17, Health = 740, MonsterData = 209196, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 667.7074f, Y = 35.41f, Z = 1941.89893f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED79A
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 681.897339f, Y = 31.8776226f, Z = 1924.42249f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED79C
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 700.0698f, Y = 33.3209839f, Z = 1958.9917f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 705.874756f, 33.9330254f, 1949.19739f },
                    new[] { 698.997375f, 33.4654083f, 1960.73645f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AA
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 17, Health = 740, MonsterData = 209196, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 695.1429f, Y = 34.3428078f, Z = 1944.95081f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 709.141907f, 30.6925449f, 1899.47803f },
                    new[] { 710.468445f, 30.9951954f, 1910.63464f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AB
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 665.294067f, Y = 43.3049965f, Z = 1879.081f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AC
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 673.4858f, Y = 41.1285057f, Z = 1872.72876f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 675.890625f, 41.0083046f, 1881.69299f },
                    new[] { 672.58374f, 41.078476f, 1885.9873f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AD
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 17, Health = 740, MonsterData = 209196, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 670.1218f, Y = 52.6449966f, Z = 1856.219f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AE
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 655.6686f, Y = 52.6449966f, Z = 1857.03162f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7AF
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 673.4858f, Y = 41.1285057f, Z = 1872.72876f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 675.890625f, 41.0083046f, 1881.69299f },
                    new[] { 672.58374f, 41.078476f, 1885.9873f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7B0
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 673.4858f, Y = 41.1285057f, Z = 1872.72876f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 675.890625f, 41.0083046f, 1881.69299f },
                    new[] { 672.58374f, 41.078476f, 1885.9873f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2ED7B1
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 17, Health = 740, MonsterData = 209196, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 676.8149f, Y = 37.8474274f, Z = 1886.10266f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2F8BE0
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 15, Health = 600, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 665.047058f, Y = 35.2877235f, Z = 1944.84351f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 710.078857f, 31.210001f, 1925.35474f },
                    new[] { 703.997925f, 32.3718987f, 1935.81262f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 pocket Hiathlin 7A2F8BE5
                PlayfieldId = 4310,
                Name = "Hiathlin",
                Level = 16, Health = 670, MonsterData = 209196, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 678.308167f, Y = 34.5136452f, Z = 1938.12378f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 695.958801f, 30.1410866f, 1908.84558f },
                    new[] { 690.914185f, 31.4733429f, 1921.75562f },
                },
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 Hesosas 7A2ED792
                PlayfieldId = 4310,
                Name = "Hesosas",
                Level = 18, Health = 2025, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 710.5007f, Y = 32.98488f, Z = 1957.72339f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 Hiathlin Prime 7A2ED7A7
                PlayfieldId = 4310,
                Name = "Hiathlin Prime",
                Level = 29, Health = 1572, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 591.0072f, Y = 84.44859f, Z = 1820.082f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 Hiathlin Prime 7A2ED7A8
                PlayfieldId = 4310,
                Name = "Hiathlin Prime",
                Level = 28, Health = 1504, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 604.3509f, Y = 86.815f, Z = 1832.94983f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                // Capture 20260826-055143 Hiathlin Prime 7A2ED7A9
                PlayfieldId = 4310,
                Name = "Hiathlin Prime",
                Level = 26, Health = 1368, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 620.648743f, Y = 75.65764f, Z = 1837.41638f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-055143",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Joshua Falker",
                Level = 100, Health = 6829, MonsterData = 259896, Scale = 112, VisualFlags = 31,
                CharacterFlags = 277352961, HeadMesh = 223820,
                X = 847.667847f, Y = 29.7936077f, Z = 1100.85828f,
                Hx = 0f, Hy = -0.7140167f, Hz = 0f, Hw = -0.7001274f,
                Textures = new[] { new[] { 0, 248006 }, new[] { 1, 247963 }, new[] { 2, 247978 }, new[] { 3, 247917 }, new[] { 4, 248056 } },
                Meshes = new[] { new[] { 0, 234530, 0, 0 }, new[] { 0, 223820, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Libbie Hyman",
                Level = 100, Health = 6829, MonsterData = 259895, Scale = 112, VisualFlags = 31, HeadMesh = 223858,
                X = 881.081665f, Y = 28.8100014f, Z = 1574.20972f,
                Hx = 0f, Hy = -0.829195142f, Hz = 0f, Hw = -0.5589591f,
                Textures = null,
                Meshes = new[] { new[] { 0, 223858, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 16, Health = 670, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 975.1205f, Y = 30.3706551f, Z = 1628.7561f,
                Hx = 0f, Hy = -0.9050121f, Hz = 0f, Hw = 0.4253859f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 16, Health = 670, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 997.4657f, Y = 31.04711f, Z = 1609.11426f,
                Hx = 0f, Hy = 0.6961716f, Hz = 0f, Hw = 0.7178754f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 15, Health = 600, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 986.759155f, Y = 31.210001f, Z = 1590.04407f,
                Hx = 0f, Hy = -0.5334631f, Hz = 0f, Hw = 0.8458233f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 15, Health = 600, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 954.5445f, Y = 31.210001f, Z = 1600.68311f,
                Hx = 0f, Hy = 0.955224037f, Hz = 0f, Hw = 0.295883536f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 15, Health = 600, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 956.3739f, Y = 31.210001f, Z = 1614.60217f,
                Hx = 0f, Hy = 0.8592781f, Hz = 0f, Hw = 0.511508763f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 17, Health = 740, MonsterData = 209229, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 958.2126f, Y = 30.0100021f, Z = 1632.52332f,
                Hx = 0f, Hy = -0.39300245f, Hz = 0f, Hw = 0.9195374f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 17, Health = 740, MonsterData = 209229, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 953.368835f, Y = 29.8944073f, Z = 1644.77722f,
                Hx = 0f, Hy = -0.9995214f, Hz = 0f, Hw = 0.030936515f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Ana",
                Level = 16, Health = 670, MonsterData = 209229, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 952.390747f, Y = 29.8611832f, Z = 1676.658f,
                Hx = 0f, Hy = 0.9999512f, Hz = 0f, Hw = 0.009881649f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Malah-Aya",
                Level = 20, Health = 2375, MonsterData = 209229, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 976.7179f, Y = 31.210001f, Z = 1599.73f,
                Hx = 0f, Hy = 0.0131572206f, Hz = 0f, Hw = 0.999913454f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Menacing Spirit",
                Level = 15, Health = 600, MonsterData = 217008, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 887.0232f, Y = 13.81f, Z = 1404.07751f,
                Hx = 0f, Hy = -0.9052915f, Hz = 0f, Hw = 0.424790859f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Nascence Spirit Hunter",
                Level = 12, Health = 975, MonsterData = 209215, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 832.732f, Y = 8.206662f, Z = 1359.97644f,
                Hx = 0f, Hy = -0.928118348f, Hz = 0f, Hw = 0.372285277f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Omathon",
                Level = 15, Health = 1500, MonsterData = 209196, Scale = 145, VisualFlags = 31, HeadMesh = 0,
                X = 802.67334f, Y = 31.210001f, Z = 1747.513f,
                Hx = 0f, Hy = 0.8768507f, Hz = 0f, Hw = 0.48076278f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 Predator Striker 7A226107 fought near Papageno (loot+respawn).
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 10, Health = 250, MonsterData = 209022, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 706.4015f, Y = 30.6967926f, Z = 1351.44971f,
                Hx = 0.00336162f, Hy = 0.99621147f, Hz = 0.07429365f, Hw = 0.04507653f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 Predator Striker 7A202DB0 fought (AttackInfo Amount=8).
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 10, Health = 250, MonsterData = 209022, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 782.2719f, Y = 26.2491436f, Z = 1309.10071f,
                Hx = 0.06680033f, Hy = -0.4383286f, Hz = -0.03268887f, Hw = 0.89573276f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 782.2719f, 26.2491436f, 1309.10071f },
                    new[] { 770.6011f, 28.0296783f, 1318.04944f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 752.3129f, Y = 25.6373177f, Z = 1582.01062f,
                Hx = 0.0128116244f, Hy = 0.481599241f, Hz = 0.0283778664f, Hw = 0.8758383f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 12, Health = 390, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 756.779541f, Y = 32.8182335f, Z = 1574.36707f,
                Hx = 0.0837885f, Hy = 0.04034139f, Hz = -0.0161937177f, Hw = 0.995534956f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 10, Health = 250, MonsterData = 209022, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 769.524353f, Y = 28.7510223f, Z = 1294.92542f,
                Hx = 0.096955426f, Hy = 0.9131316f, Hz = 0.03849579f, Hw = 0.3940919f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 785.2869f, Y = 31.8100014f, Z = 1652.03137f,
                Hx = 0f, Hy = -0.00346746529f, Hz = 0f, Hw = 0.999994f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 12, Health = 390, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 768.4901f, Y = 30.6384544f, Z = 1628.16138f,
                Hx = -0.0550727546f, Hy = -0.6701654f, Hz = 0.04997838f, Hw = 0.738476455f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 772.3483f, Y = 27.61f, Z = 1614.5835f,
                Hx = 0f, Hy = 0.34553647f, Hz = 0f, Hw = 0.938405335f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 781.637f, Y = 31.8100014f, Z = 1561.12451f,
                Hx = 0f, Hy = 0.0300183911f, Hz = 0f, Hw = 0.9995493f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 14, Health = 530, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 755.316833f, Y = 32.1921768f, Z = 1546.51953f,
                Hx = -0.0184315853f, Hy = 0.9661648f, Hz = -0.0720494539f, Hw = 0.246970966f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            // Capture 20260826-054154 Predator Striker pocket ~755-810/1900-1965 (12 spawns; patrol in OutdoorMobRuntime).
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 794.750061f, Y = 31.210001f, Z = 1902.32019f,
                Hx = 0f, Hy = 0.0198501f, Hz = 0f, Hw = 0.9998029f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 756.767f, Y = 31.210001f, Z = 1909.91064f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 772.6582f, Y = 31.210001f, Z = 1919.97949f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 776.882935f, Y = 31.210001f, Z = 1929.22363f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 762.076965f, Y = 31.210001f, Z = 1942.66f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 802.872253f, Y = 31.210001f, Z = 1945.83679f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 790.3375f, Y = 31.210001f, Z = 1936.46448f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 766.3778f, Y = 31.210001f, Z = 1953.64856f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 800.4486f, Y = 31.70385f, Z = 1964.01709f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 755.2589f, Y = 31.210001f, Z = 1906.75f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 808.7332f, Y = 31.210001f, Z = 1924.254f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 15, Health = 600, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 759.6166f, Y = 31.210001f, Z = 1920.06677f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null, Meshes = null, CaptureFolder = "20260826-054154",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 10, Health = 250, MonsterData = 209022, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 767.686768f, Y = 28.9124489f, Z = 1294.529f,
                Hx = 0.142293423f, Hy = 0.969489753f, Hz = -0.0289858077f, Hw = 0.1974893f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 727.0008f, Y = 32.7817841f, Z = 1592.11169f,
                Hx = -0.00492549874f, Hy = 0.509932637f, Hz = -0.0512838624f, Hw = 0.8586701f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 817.2187f, Y = 32.0400734f, Z = 1651.72485f,
                Hx = 0.05278429f, Hy = 0.250692964f, Hz = 0.08998924f, Hw = 0.96242857f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Sabina Florenta",
                Level = 100, Health = 8195, MonsterData = 259890, Scale = 112, VisualFlags = 31, HeadMesh = 223849,
                X = 884.188843f, Y = 28.8443241f, Z = 1575.22693f,
                Hx = 0f, Hy = -0.953375161f, Hz = 0f, Hw = 0.301789343f,
                Textures = new[] { new[] { 0, 248006 }, new[] { 1, 247963 }, new[] { 2, 247978 }, new[] { 3, 247917 }, new[] { 4, 248056 } },
                Meshes = new[] { new[] { 0, 234532, 0, 0 }, new[] { 0, 223849, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            // Capture 20260826-160734 — not previously in LifeSpawn (no Hwall / no Spinetooth).
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Rainbow Feathers",
                Level = 15, Health = 1500, MonsterData = 209182, Scale = 242, VisualFlags = 31, HeadMesh = 0,
                X = 888.914063f, Y = 30.0100021f, Z = 1250.3916f,
                Hx = 0f, Hy = 0.650363743f, Hz = 0f, Hw = 0.759622931f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Scientist Donna Red",
                Level = 100, Health = 6829, MonsterData = 216692, Scale = 112, VisualFlags = 31, HeadMesh = 40645,
                X = 987.4562f, Y = 33.11915f, Z = 1758.26465f,
                Hx = 0f, Hy = -1.00000012f, Hz = 0f, Hw = 1.17683868E-07f,
                Textures = new[] { new[] { 0, 213851 }, new[] { 1, 213751 }, new[] { 2, 213807 }, new[] { 3, 215296 }, new[] { 4, 215294 } },
                Meshes = new[] { new[] { 0, 20091, 215300, 2 }, new[] { 0, 40645, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260723-221330 scfu-appearance SimpleChar:7963A853 on PF 4001 (Jobe Research).
                PlayfieldId = NascenceLifeContentModule.JobeResearchPlayfieldId,
                Name = "Scientist Drake Rodriguez",
                Level = 200, Health = 164773, MonsterData = 26092, Scale = 100, VisualFlags = 31,
                CharacterFlags = 277352961, HeadMesh = 40694,
                X = 854.5125f, Y = 34.405f, Z = 958.5875f,
                Hx = 0f, Hy = -0.9730012f, Hz = 0f, Hw = 0.23080005f,
                Textures = new[]
                {
                    new[] { 0, 213851 }, new[] { 1, 213751 }, new[] { 2, 213807 },
                    new[] { 3, 213708 }, new[] { 4, 213925 }
                },
                Meshes = new[]
                {
                    new[] { 0, 20108, 215300, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 5, 214715, 0, 0 }
                },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Scientist Veronica Escobar",
                Level = 100, Health = 27316, MonsterData = 216693, Scale = 112, VisualFlags = 31, HeadMesh = 40638,
                X = 745.490662f, Y = 31.210001f, Z = 1876.54688f,
                Hx = 0f, Hy = 1.00000048f, Hz = 0f, Hw = 2.8894064E-07f,
                Textures = new[] { new[] { 0, 213851 }, new[] { 1, 213751 }, new[] { 2, 213807 }, new[] { 3, 215296 }, new[] { 4, 215294 } },
                Meshes = new[] { new[] { 0, 20091, 215300, 2 }, new[] { 0, 40638, 0, 4 } },
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Slivering Chimera 7A2ED7C1 ExtTex low2:208969 SAW 88 HP 368.
                PlayfieldId = 4310,
                Name = "Slivering Chimera",
                Level = 12, Health = 368, MonsterData = 209173, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 778.767f, Y = 27.61f, Z = 1609.97363f,
                Hx = 0f, Hy = 0.198784322f, Hz = 0f, Hw = 0.9800433f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 786.0128f, 29.5924f, 1602.8093f },
                    new[] { 795.7056f, 31.7217f, 1606.3458f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Slivering Chimera 7A2ED7C4.
                PlayfieldId = 4310,
                Name = "Slivering Chimera",
                Level = 12, Health = 368, MonsterData = 209173, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 785.8796f, Y = 29.4451218f, Z = 1603.605f,
                Hx = -0.0383969247f, Hy = 0.6390103f, Hz = 0.156461075f, Hw = 0.7521379f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 775.5828f, 27.894f, 1602.1064f },
                    new[] { 781.5366f, 28.4405f, 1616.5199f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Slivering Chimera",
                Level = 15, Health = 480, MonsterData = 209173, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 802.5594f, Y = 30.3999062f, Z = 1644.006f,
                Hx = 0.07435763f, Hy = -0.9968707f, Hz = 0.00199558842f, Hw = 0.0267546959f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Slivering Chimera 7A2ED6B6 @ demonic fork mouth.
                PlayfieldId = 4310,
                Name = "Slivering Chimera",
                Level = 15, Health = 480, MonsterData = 209173, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 811.2371f, Y = 31.21f, Z = 1656.7668f,
                Hx = 0f, Hy = 0.34553647f, Hz = 0f, Hw = 0.938405335f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 802.203f, 30.340f, 1649.725f },
                    new[] { 813.361f, 31.210f, 1657.542f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Predator Striker 7A2ED7BF @ demonic fork mouth.
                PlayfieldId = 4310,
                Name = "Predator Striker",
                Level = 14, Health = 530, MonsterData = 209022, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 809.1596f, Y = 31.9899f, Z = 1640.8262f,
                Hx = 0f, Hy = 0.6274317f, Hz = 0f, Hw = 0.7786681f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 802.856f, 31.210f, 1626.312f },
                    new[] { 809.984f, 31.914f, 1643.277f },
                },
                CaptureFolder = "20260825-202932",
            },
            // Capture 20260826-160734 — not previously in LifeSpawn.
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Starry Feathers",
                Level = 15, Health = 1500, MonsterData = 209189, Scale = 387, VisualFlags = 31, HeadMesh = 0,
                X = 682.484741f, Y = 32.7887077f, Z = 1270.67664f,
                Hx = 0f, Hy = -0.166885644f, Hz = 0f, Hw = 0.9859763f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Soul Dredge",
                Level = 15, Health = 1500, MonsterData = 209215, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 872.4091f, Y = 16.865f, Z = 1338.81653f,
                Hx = 0f, Hy = -0.504925668f, Hz = 0f, Hw = 0.8631628f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260826-135727 Swift Silvertail 7A2ED6BF @ Spinetooth zone entry.
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 10, Health = 450, MonsterData = 208922, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 896.613f, Y = 30.708f, Z = 1614.367f,
                Hx = -0.0510836765f, Hy = 0.6849761f, Hz = 0.0540506355f, Hw = 0.724759758f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 896.613f, 30.708f, 1614.367f },
                    new[] { 899.060f, 31.210f, 1584.261f },
                },
                CaptureFolder = "20260826-135727",
            },
            new LifeNpc
            {
                // Capture 20260826-051307 Spinetooth 7A2ED6F5 @ north loop.
                PlayfieldId = 4310,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 980.916138f, Y = 30.1901321f, Z = 1656.24475f,
                Hx = 0f, Hy = 0.9808188f, Hz = 0f, Hw = 0.194921732f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 980.916138f, 30.1901321f, 1656.24475f },
                    new[] { 984.4677f, 30.0100021f, 1644.71875f },
                },
                CaptureFolder = "20260826-051307",
            },
            new LifeNpc
            {
                // Capture 20260826-051307 Spinetooth 7A2ED6F4 @ west pocket.
                PlayfieldId = 4310,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 978.669067f, Y = 31.210001f, Z = 1604.36829f,
                Hx = 0f, Hy = 0.085654214f, Hz = 0f, Hw = 0.9963249f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 978.669067f, 31.210001f, 1604.36829f },
                    new[] { 979.681763f, 31.210001f, 1610.21423f },
                },
                CaptureFolder = "20260826-051307",
            },
            new LifeNpc
            {
                // Capture 20260826-051307 Spinetooth 7A2ED6F3 @ east pocket.
                PlayfieldId = 4310,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1019.60406f, Y = 29.09971f, Z = 1636.85632f,
                Hx = 0f, Hy = 0.3125347f, Hz = 0f, Hw = 0.949906349f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 1019.60406f, 29.09971f, 1636.85632f },
                    new[] { 1023.84265f, 28.9718666f, 1642.6062f },
                },
                CaptureFolder = "20260826-051307",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2ED6BC.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 808.8242f, Y = 32.0691f, Z = 1666.4951f,
                Hx = -0.0196583718f, Hy = 0.96176064f, Hz = -0.07172443f, Hw = 0.2636013f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 810.019f, 32.111f, 1668.380f },
                    new[] { 810.717f, 31.705f, 1647.417f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2F8970.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 11, Health = 320, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 840.4116f, Y = 31.9013f, Z = 1686.4675f,
                Hx = 0.0282715876f, Hy = 0.867516041f, Hz = -0.100414135f, Hw = 0.486347228f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 832.234f, 31.838f, 1683.955f },
                    new[] { 850.801f, 31.809f, 1672.036f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 12, Health = 390, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 834.9964f, Y = 31.1906643f, Z = 1679.84277f,
                Hx = -0.0196583718f, Hy = 0.96176064f, Hz = -0.07172443f, Hw = 0.2636013f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 11, Health = 320, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 817.729736f, Y = 31.0670223f, Z = 1668.72f,
                Hx = 0.0282715876f, Hy = 0.867516041f, Hz = -0.100414135f, Hw = 0.486347228f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2ED6B7.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 13, Health = 460, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 852.0815f, Y = 31.202f, Z = 1676.2471f,
                Hx = 0f, Hy = 0.509932637f, Hz = 0f, Hw = 0.8601921f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 856.148f, 32.154f, 1669.708f },
                    new[] { 873.640f, 29.768f, 1676.816f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2ED6B8.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 12, Health = 390, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 858.8698f, Y = 31.3838f, Z = 1692.8748f,
                Hx = 0f, Hy = 0.250692964f, Hz = 0f, Hw = 0.968076f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 871.169f, 30.139f, 1676.044f },
                    new[] { 896.555f, 29.753f, 1682.269f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2ED6B9.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 12, Health = 390, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 869.3756f, Y = 30.01f, Z = 1688.6782f,
                Hx = 0f, Hy = 0.04034139f, Hz = 0f, Hw = 0.999186f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 879.169f, 29.410f, 1665.192f },
                    new[] { 896.555f, 29.753f, 1682.269f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2ED6BA.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 11, Health = 320, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 881.4546f, Y = 29.7961f, Z = 1658.1475f,
                Hx = 0f, Hy = -0.00346746529f, Hz = 0f, Hw = 0.999994f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 824.339f, 31.103f, 1664.744f },
                    new[] { 827.097f, 31.543f, 1679.124f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 Stalking Predator 7A2F8962.
                PlayfieldId = 4310,
                Name = "Stalking Predator",
                Level = 11, Health = 320, MonsterData = 209022, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 877.7025f, Y = 29.41f, Z = 1673.3356f,
                Hx = 0f, Hy = 0.6849761f, Hz = 0f, Hw = 0.7286f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 813.569f, 32.008f, 1670.831f },
                    new[] { 825.321f, 31.191f, 1664.130f },
                },
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 17, Health = 1332, MonsterData = 208922, Scale = 98, VisualFlags = 31, HeadMesh = 0,
                X = 659.999146f, Y = 52.6449966f, Z = 1852.33191f,
                Hx = 0f, Hy = 0.0754485f, Hz = 0f, Hw = 0.9971497f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 12, Health = 702, MonsterData = 208922, Scale = 96, VisualFlags = 31, HeadMesh = 0,
                X = 629.169556f, Y = 53.885f, Z = 1866.9425f,
                Hx = 0f, Hy = 0.809404969f, Hz = 0f, Hw = 0.5872509f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 10, Health = 450, MonsterData = 208922, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 870.186f, Y = 30.044426f, Z = 1498.35889f,
                Hx = -0.0967476442f, Hy = -0.9153524f, Hz = 0.0390578955f, Hw = 0.3889014f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 8, Health = 360, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 774.6057f, Y = 30.4421482f, Z = 1239.99243f,
                Hx = 0.158704013f, Hy = 0.9455033f, Hz = 0.0277024843f, Hw = 0.282964915f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 7, Health = 315, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 807.4224f, Y = 32.0287f, Z = 1265.9959f,
                Hx = 0f, Hy = 0.792831659f, Hz = 0f, Hw = 0.6094407f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103458",
                PatrolCaptureInstance = "7A226731",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 9, Health = 405, MonsterData = 208922, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 880.278137f, Y = 28.8100014f, Z = 1505.67676f,
                Hx = 0f, Hy = 0.347341567f, Hz = 0f, Hw = 0.937738657f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 7, Health = 315, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 956.348755f, Y = 32.9619255f, Z = 1353.13062f,
                Hx = 0.07282108f, Hy = 0.9760911f, Hz = -0.0152367549f, Hw = 0.204232961f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 6, Health = 270, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 847.7317f, Y = 32.41f, Z = 1147.73816f,
                Hx = 0f, Hy = -0.408930153f, Hz = 0f, Hw = 0.9125657f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 6, Health = 270, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 825.64624f, Y = 31.0839329f, Z = 1119.0907f,
                Hx = -0.00442240154f, Hy = -0.9320445f, Hz = -0.0407234952f, Hw = 0.360020936f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 5, Health = 225, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 824.227234f, Y = 31.8100014f, Z = 1132.86768f,
                Hx = 0f, Hy = 0.251376748f, Hz = 0f, Hw = 0.9678893f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 6, Health = 270, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 786.519f, Y = 29.036f, Z = 1229.889f,
                Hx = 0.10369987f, Hy = 0.6226327f, Hz = -0.0113409711f, Hw = 0.7755297f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 8, Health = 360, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 765.531f, Y = 31.351f, Z = 1252.481f,
                Hx = 0.158704013f, Hy = 0.9455033f, Hz = 0.0277024843f, Hw = 0.282964915f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 8, Health = 360, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 806.400f, Y = 29.410f, Z = 1224.275f,
                Hx = -0.09947748f, Hy = 0.882358134f, Hz = -0.03140881f, Hw = 0.4588702f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 8, Health = 360, MonsterData = 208922, Scale = 94, VisualFlags = 31, HeadMesh = 0,
                X = 812.620f, Y = 32.410f, Z = 1288.259f,
                Hx = 0f, Hy = -0.9545999f, Hz = 0f, Hw = 0.297891f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-160734",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 5, Health = 225, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 825.4033f, Y = 31.8100014f, Z = 1135.0824f,
                Hx = 0f, Hy = 0.9498087f, Hz = 0f, Hw = 0.312831342f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 14, Health = 954, MonsterData = 208922, Scale = 97, VisualFlags = 31, HeadMesh = 0,
                X = 679.138855f, Y = 29.2868271f, Z = 1889.99072f,
                Hx = -0.009008231f, Hy = 0.120768249f, Hz = 0.07383693f, Hw = 0.98988986f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-230406",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swift Silvertail",
                Level = 5, Health = 225, MonsterData = 208922, Scale = 93, VisualFlags = 31, HeadMesh = 0,
                X = 855.9302f, Y = 32.41f, Z = 1140.48779f,
                Hx = 0f, Hy = -0.409773767f, Hz = 0f, Hw = 0.912187159f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-230406",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Swiftwind",
                Level = 10, Health = 625, MonsterData = 208922, Scale = 142, VisualFlags = 31, HeadMesh = 0,
                X = 792.961365f, Y = 30.7602024f, Z = 1270.34741f,
                Hx = 0.05375963f, Hy = -0.72072494f, Hz = 0.0514095537f, Hw = 0.68921876f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260823-112044 Tempterus pack (OmniTek flyers) — replace single old slot.
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 684.326965f, Y = 33.09908f, Z = 1263.43445f,
                Hx = 0f, Hy = -0.40794244f, Hz = 0f, Hw = 0.9130076f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 659.886536f, Y = 32.41f, Z = 1267.16284f,
                Hx = 0f, Hy = 0.72313225f, Hz = 0f, Hw = 0.6907096f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 659.886536f, 32.41f, 1267.16284f },
                    new[] { 664.2028f, 32.41f, 1266.96472f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 689.012146f, Y = 33.01f, Z = 1267.427f,
                Hx = 0f, Hy = -0.9991893f, Hz = 0f, Hw = 0.04025825f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 689.012146f, 33.01f, 1267.427f },
                    new[] { 688.723f, 33.118454f, 1263.84412f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 9, Health = 225, MonsterData = 209189, Scale = 237, VisualFlags = 31, HeadMesh = 0,
                X = 634.4851f, Y = 32.64147f, Z = 1274.63367f,
                Hx = 0f, Hy = 0.6736884f, Hz = 0f, Hw = 0.7390155f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 677.780151f, Y = 32.3819f, Z = 1278.00745f,
                Hx = 0f, Hy = 0.29466066f, Hz = 0f, Hw = 0.95560193f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 685.434631f, Y = 32.63119f, Z = 1283.11536f,
                Hx = 0f, Hy = 0.9971637f, Hz = 0f, Hw = 0.07526346f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 685.434631f, 32.63119f, 1283.11536f },
                    new[] { 685.948547f, 33.3314934f, 1279.73071f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 650.785156f, Y = 30.5857315f, Z = 1285.83f,
                Hx = 0f, Hy = -0.99926496f, Hz = 0f, Hw = 0.03833538f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 650.785156f, 30.5857315f, 1285.83f },
                    new[] { 650.330933f, 32.2f, 1279.99487f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 9, Health = 225, MonsterData = 209189, Scale = 237, VisualFlags = 31, HeadMesh = 0,
                X = 627.9273f, Y = 32.45312f, Z = 1287.74084f,
                Hx = 0f, Hy = 0.4257429f, Hz = 0f, Hw = 0.90484417f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 645.1165f, Y = 29.2592144f, Z = 1290.00757f,
                Hx = 0f, Hy = 0.5848823f, Hz = 0f, Hw = 0.8111182f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 641.7504f, Y = 27.1053982f, Z = 1297.12708f,
                Hx = 0f, Hy = -0.8046756f, Hz = 0f, Hw = 0.5937148f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 641.7504f, 27.1053982f, 1297.12708f },
                    new[] { 638.5365f, 28.8364258f, 1296.10718f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 644.667969f, Y = 24.5189152f, Z = 1303.99585f,
                Hx = 0f, Hy = 0.78351575f, Hz = 0f, Hw = 0.6213719f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 644.667969f, 24.5189152f, 1303.99585f },
                    new[] { 646.6487f, 24.28283f, 1303.53247f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 680.0311f, Y = 30.7910652f, Z = 1316.99377f,
                Hx = 0f, Hy = 0.9749727f, Hz = 0f, Hw = 0.22232439f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 680.0311f, 30.7910652f, 1316.99377f },
                    new[] { 683.634338f, 32.2454529f, 1309.504f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 713.163757f, Y = 32.41f, Z = 1335.629f,
                Hx = 0f, Hy = 0.8064957f, Hz = 0f, Hw = 0.5912399f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                // Fought 7A226210 / loot path; near Disease-Ridden + Papageno.
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 9, Health = 225, MonsterData = 209189, Scale = 237, VisualFlags = 31, HeadMesh = 0,
                X = 721.0239f, Y = 31.9694767f, Z = 1359.99927f,
                Hx = 0f, Hy = 0.93011946f, Hz = 0f, Hw = 0.36725715f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 721.0239f, 31.9694767f, 1359.99927f },
                    new[] { 731.900452f, 31.2660313f, 1348.37354f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 9, Health = 225, MonsterData = 209189, Scale = 237, VisualFlags = 31, HeadMesh = 0,
                X = 659.3258f, Y = 31.210001f, Z = 1371.60693f,
                Hx = 0f, Hy = -0.9227075f, Hz = 0f, Hw = 0.38550082f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 8, Health = 200, MonsterData = 209189, Scale = 236, VisualFlags = 31, HeadMesh = 0,
                X = 680.0515f, Y = 30.8609562f, Z = 1373.58154f,
                Hx = 0f, Hy = 0.7737947f, Hz = 0f, Hw = 0.63343644f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 680.0515f, 30.8609562f, 1373.58154f },
                    new[] { 688.2116f, 31.23235f, 1371.93738f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 680.0539f, Y = 31.7655258f, Z = 1383.60962f,
                Hx = 0f, Hy = 0.76431894f, Hz = 0f, Hw = 0.6448384f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 680.0539f, 31.7655258f, 1383.60962f },
                    new[] { 692.989441f, 32.41f, 1381.4f },
                },
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Tempterus",
                Level = 10, Health = 250, MonsterData = 209189, Scale = 238, VisualFlags = 31, HeadMesh = 0,
                X = 681.1344f, Y = 32.41f, Z = 1391.806f,
                Hx = 0f, Hy = 0.8940439f, Hz = 0f, Hw = 0.4479793f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-112044",
            },
            new LifeNpc
            {
                // Capture 20260825-202932 SCFU 7A2ED7C3 boss: MD 223690 HP 3800 Scale 99 RunSpeed 69 npcFamily 174.
                PlayfieldId = 4310,
                Name = "The Demonic Subjugator",
                Level = 20, Health = 3800, MonsterData = 223690, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 733.1152f, Y = 32.41f, Z = 1565.043f,
                Hx = 0f, Hy = -0.9351043f, Hz = 0f, Hw = 0.3543726f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260825-202932",
            },
            new LifeNpc
            {
                // Capture 20260823-103221 SCFU 7A226263: bridge spawn was wrong; Lady stands at cave ledge.
                PlayfieldId = 4310,
                Name = "The Lady",
                Level = 15, Health = 1500, MonsterData = 217022, Scale = 145, VisualFlags = 31, HeadMesh = 0,
                X = 818.644958f, Y = 17.345f, Z = 1392.973f,
                Hx = 0f, Hy = 0.875993431f, Hz = 0f, Hw = 0.482323021f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-103221",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "The Lord",
                Level = 20, Health = 2375, MonsterData = 217007, Scale = 148, VisualFlags = 31, HeadMesh = 0,
                X = 846.1871f, Y = 9.845f, Z = 1403.21875f,
                Hx = 0f, Hy = 0.951536536f, Hz = 0f, Hw = 0.307535738f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1038.67468f, Y = 31.210001f, Z = 1658.98621f,
                Hx = 0f, Hy = -0.265198827f, Hz = 0f, Hw = 0.964193761f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                // Capture 20260826-212737 outdoor Weaver probe (7A2ED6A8 @ 1043/1666).
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1043.56824f, Y = 31.3046932f, Z = 1666.442f,
                Hx = 0f, Hy = -0.265198827f, Hz = 0f, Hw = 0.964193761f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260826-212737",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1042.8761f, Y = 31.275404f, Z = 1666.92065f,
                Hx = 0.0117302258f, Hy = -0.692380667f, Hz = 0.0295159835f, Hw = 0.720833f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1033.30774f, Y = 30.8121681f, Z = 1650.70837f,
                Hx = 0.022572726f, Hy = -0.302653223f, Hz = 0.07086816f, Hw = 0.9501943f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1032.883f, Y = 30.7484646f, Z = 1641.977f,
                Hx = -0.06514834f, Hy = 0.873503447f, Hz = 0.0358819962f, Hw = 0.481102765f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1034.27051f, Y = 31.210001f, Z = 1629.253f,
                Hx = 0f, Hy = -0.148934111f, Hz = 0f, Hw = 0.988847136f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1016.19128f, Y = 30.2669067f, Z = 1626.51855f,
                Hx = 0.104112633f, Hy = -0.657744169f, Hz = 0.006545341f, Hw = 0.745982766f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1016.13654f, Y = 30.6364784f, Z = 1616.63367f,
                Hx = 0.0585003942f, Hy = -0.7842816f, Hz = 0.04594284f, Hw = 0.615929663f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1032.18384f, Y = 31.210001f, Z = 1676.68164f,
                Hx = 0f, Hy = 0.7773639f, Hz = 0f, Hw = 0.6290512f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1016.12952f, Y = 30.2156563f, Z = 1689.20154f,
                Hx = -0.08275143f, Hy = 0.129670352f, Hz = 0.06351757f, Hw = 0.9860544f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 1014.09613f, Y = 29.4508343f, Z = 1676.98059f,
                Hx = -0.0118399374f, Hy = 0.08066921f, Hz = 0.144732192f, Hw = 0.9861059f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 964.7748f, Y = 28.9322147f, Z = 1691.03137f,
                Hx = 0.06893667f, Hy = -0.924365163f, Hz = 0.0279060658f, Hw = 0.374189764f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 979.2685f, Y = 29.6928272f, Z = 1702.577f,
                Hx = -0.0116906306f, Hy = -0.619936645f, Hz = 0.103670508f, Hw = 0.7776852f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 984.0649f, Y = 30.6084251f, Z = 1707.88464f,
                Hx = -0.103780091f, Hy = 0.770962f, Hz = -0.0105817793f, Hw = 0.6282797f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 984.7245f, Y = 31.04463f, Z = 1710.133f,
                Hx = 0.0456819125f, Hy = -0.9401455f, Hz = 0.0937944949f, Hw = 0.324410528f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 989.171143f, Y = 30.4385681f, Z = 1666.59351f,
                Hx = 0.0670548156f, Hy = 0.8991316f, Hz = -0.0321662f, Hw = 0.431313485f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 958.514038f, Y = 29.4100018f, Z = 1702.57739f,
                Hx = 0f, Hy = 0.5096641f, Hz = 0f, Hw = 0.860373437f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Weaver of Malice",
                Level = 15, Health = 600, MonsterData = 209354, Scale = 39, VisualFlags = 31, HeadMesh = 0,
                X = 999.989868f, Y = 30.6713676f, Z = 1704.37927f,
                Hx = 0.0146786887f, Hy = -0.9704194f, Hz = 0.07970398f, Hw = 0.22741577f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Yuttos Nascence Geosurvey Dog",
                Level = 21, Health = 612, MonsterData = 209173, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 803.015747f, Y = 28.1124973f, Z = 1823.32166f,
                Hx = -0.0741990656f, Hy = -0.06750711f, Hz = 0.0050344225f, Hw = 0.9949432f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-170408",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Yuttos Nascence Geosurvey Dog",
                Level = 21, Health = 612, MonsterData = 209173, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 770.051636f, Y = 31.210001f, Z = 1898.1217f,
                Hx = 0f, Hy = 0.08724495f, Hz = 0f, Hw = 0.9961869f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-230406",
            },
            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Yuttos Nascence Geosurvey Dog",
                Level = 10, Health = 150, MonsterData = 209173, Scale = 95, VisualFlags = 31, HeadMesh = 0,
                X = 799.5469f, Y = 29.2789f, Z = 1208.9022f,
                Hx = 0f, Hy = 0.994135857f, Hz = 0f, Hw = 0.108138159f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260823-000659",
                PatrolCaptureInstance = "7A202B50",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Ahwere",
                Level = 40, Health = 5800, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 672.524353f, Y = 10.8569851f, Z = 1195.219f,
                Hx = 0f, Hy = -0.423609257f, Hz = 0f, Hw = 0.905845046f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Avatar Urga-Pi Thrak",
                Level = 40, Health = 9280, MonsterData = 208635, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 162.521286f, Y = 96.61001f, Z = 1061.92761f,
                Hx = 0f, Hy = 0.5897229f, Hz = 0f, Hw = 0.8076057f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 130.61673f, Y = 107.522606f, Z = 826.098267f,
                Hx = 0f, Hy = -0.9003742f, Hz = 0f, Hw = 0.43511644f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 146.603622f, Y = 107.504791f, Z = 820.972839f,
                Hx = 0f, Hy = -0.416992128f, Hz = 0f, Hw = 0.9089101f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 145.707352f, Y = 107.123878f, Z = 835.9502f,
                Hx = 0f, Hy = -0.9132749f, Hz = 0f, Hw = 0.407343745f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 92.89652f, Y = 105.01001f, Z = 870.579163f,
                Hx = 0f, Hy = -0.898085833f, Hz = 0f, Hw = 0.43982017f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 90.36528f, Y = 105.01001f, Z = 861.511536f,
                Hx = 0f, Hy = -0.242629915f, Hz = 0f, Hw = 0.97011894f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 84.3412f, Y = 105.01001f, Z = 873.714539f,
                Hx = 0f, Hy = 0.9269806f, Hz = 0f, Hw = 0.375109255f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 224.8687f, Y = 106.55249f, Z = 826.385559f,
                Hx = 0f, Hy = -0.212747365f, Hz = 0f, Hw = 0.9771072f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 194.120667f, Y = 106.525307f, Z = 838.7157f,
                Hx = 0f, Hy = 0.89598304f, Hz = 0f, Hw = 0.444088221f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 183.531158f, Y = 109.4451f, Z = 825.4774f,
                Hx = 0f, Hy = 0.5575081f, Hz = 0f, Hw = 0.830171466f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 159.627777f, Y = 105.01001f, Z = 868.137f,
                Hx = 0f, Hy = 0.973971367f, Hz = 0f, Hw = 0.22667107f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 168.780762f, Y = 105.01001f, Z = 864.3072f,
                Hx = 0f, Hy = -0.363367051f, Hz = 0f, Hw = 0.931646049f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 167.0054f, Y = 105.01001f, Z = 872.328735f,
                Hx = 0f, Hy = 0.9806891f, Hz = 0f, Hw = 0.195573151f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 277.6204f, Y = 105.201889f, Z = 758.3549f,
                Hx = 0f, Hy = 0.92312026f, Hz = 0f, Hw = 0.384511322f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 295.925385f, Y = 105.01001f, Z = 873.1195f,
                Hx = 0f, Hy = -0.681195557f, Hz = 0f, Hw = 0.7321015f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 299.1753f, Y = 105.01001f, Z = 868.675842f,
                Hx = 0f, Hy = 0.975251555f, Hz = 0f, Hw = 0.2210983f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 285.036621f, Y = 105.01001f, Z = 868.3439f,
                Hx = 0f, Hy = 0.715566039f, Hz = 0f, Hw = 0.6985451f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 282.539368f, Y = 106.602928f, Z = 823.422058f,
                Hx = 0f, Hy = -0.389652222f, Hz = 0f, Hw = 0.9209621f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Barad-Or",
                Level = 45, Health = 6650, MonsterData = 208644, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 269.972534f, Y = 107.228325f, Z = 830.3362f,
                Hx = 0f, Hy = 0.9235636f, Hz = 0f, Hw = 0.383445263f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Blighter of Growth",
                Level = 28, Health = 3760, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 154.732468f, Y = 106.025879f, Z = 1589.14319f,
                Hx = 0f, Hy = 0.8324324f, Hz = 0f, Hw = 0.5541266f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 39, Health = 2252, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 171.972656f, Y = 100.811905f, Z = 1138.20142f,
                Hx = 0f, Hy = -0.9549469f, Hz = 0f, Hw = 0.296776742f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 39, Health = 2252, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 290.544617f, Y = 96.61001f, Z = 901.9826f,
                Hx = 0f, Hy = 0.7067696f, Hz = 0f, Hw = 0.7074438f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 63.708828f, Y = 96.61001f, Z = 901.949646f,
                Hx = 0f, Hy = -0.707983851f, Hz = 0f, Hw = 0.7062286f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 40, Health = 2320, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 154.446335f, Y = 96.61001f, Z = 906.037842f,
                Hx = 0f, Hy = 0.7068175f, Hz = 0f, Hw = 0.7073959f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 39, Health = 2252, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 177.969284f, Y = 96.61001f, Z = 1010.37152f,
                Hx = 0f, Hy = 0.000738546369f, Hz = 0f, Hw = 0.9999997f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 37, Health = 2116, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 170.0742f, Y = 96.61001f, Z = 1040.06567f,
                Hx = 0f, Hy = -0.0042965184f, Hz = 0f, Hw = 0.999990761f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 185.96225f, Y = 96.61001f, Z = 1056.99915f,
                Hx = 0f, Hy = 0.999999046f, Hz = 0f, Hw = -0.001353851f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 170.059784f, Y = 96.61001f, Z = 960.401f,
                Hx = 0f, Hy = 0.999996066f, Hz = 0f, Hw = -0.00281023537f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 185.9586f, Y = 96.61001f, Z = 932.5282f,
                Hx = 0f, Hy = 0.99999994f, Hz = 0f, Hw = 0.0003132606f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 205.433289f, Y = 96.61001f, Z = 898.0739f,
                Hx = 0f, Hy = 0.7086895f, Hz = 0f, Hw = 0.7055205f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 191.190231f, Y = 96.61001f, Z = 906.044434f,
                Hx = 0f, Hy = -0.703991532f, Hz = 0f, Hw = 0.710208356f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 39, Health = 2252, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 119.865143f, Y = 105.01001f, Z = 733.1537f,
                Hx = 0f, Hy = -0.940559268f, Hz = 0f, Hw = 0.33962965f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 37, Health = 2116, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 105.037079f, Y = 105.01001f, Z = 732.1247f,
                Hx = 0f, Hy = -0.280390978f, Hz = 0f, Hw = 0.9598859f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 37, Health = 2116, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 125.231537f, Y = 106.595253f, Z = 851.6594f,
                Hx = 0f, Hy = 0.05836424f, Hz = 0f, Hw = 0.998295367f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 222.203064f, Y = 110.58374f, Z = 1309.1156f,
                Hx = 0f, Hy = -0.6564222f, Hz = 0f, Hw = 0.754393756f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 113.29097f, Y = 104.8206f, Z = 860.1669f,
                Hx = 0f, Hy = 0.52567786f, Hz = 0f, Hw = 0.850683749f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 163.241318f, Y = 96.61001f, Z = 898.0259f,
                Hx = 0f, Hy = 0.704492867f, Hz = 0f, Hw = 0.7097111f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 233.705887f, Y = 105.01001f, Z = 733.7038f,
                Hx = 0f, Hy = -0.9390942f, Hz = 0f, Hw = 0.343659818f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 256.605743f, Y = 105.01001f, Z = 743.6206f,
                Hx = 0f, Hy = -0.048118f, Hz = 0f, Hw = 0.998841643f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 36, Health = 2048, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 268.4438f, Y = 106.081345f, Z = 734.658447f,
                Hx = 0f, Hy = 0.162129983f, Hz = 0f, Hw = 0.986769438f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 37, Health = 2116, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 248.280212f, Y = 104.792694f, Z = 860.9087f,
                Hx = 0f, Hy = -0.3906805f, Hz = 0f, Hw = 0.9205263f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 37, Health = 2116, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 228.618423f, Y = 105.551674f, Z = 852.8327f,
                Hx = 0f, Hy = 0.00414098846f, Hz = 0f, Hw = 0.9999914f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Calan-Cur",
                Level = 38, Health = 2184, MonsterData = 246185, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 272.314575f, Y = 106.377472f, Z = 726.9118f,
                Hx = 0f, Hy = -0.9982398f, Hz = 0f, Hw = 0.05930661f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 338.381561f, Y = 105.435f, Z = 919.3593f,
                Hx = 0f, Hy = 0.9991042f, Hz = 0f, Hw = 0.042318143f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 52.6848221f, Y = 105.435f, Z = 919.741638f,
                Hx = 0f, Hy = -0.9994545f, Hz = 0f, Hw = 0.03302611f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 309.009338f, Y = 105.435f, Z = 919.6031f,
                Hx = 0f, Hy = 0.999474466f, Hz = 0f, Hw = 0.0324165523f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 175.681412f, Y = 101.368217f, Z = 1136.08789f,
                Hx = 0f, Hy = 0.7495446f, Hz = 0f, Hw = 0.6619538f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 167.092545f, Y = 101.125015f, Z = 1137.136f,
                Hx = 0f, Hy = 0.5261199f, Hz = 0f, Hw = 0.8504104f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 197.611725f, Y = 105.435f, Z = 1061.72058f,
                Hx = 0f, Hy = -0.725214243f, Hz = 0f, Hw = 0.6885233f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 139.442337f, Y = 105.435f, Z = 919.7498f,
                Hx = 0f, Hy = 0.9994999f, Hz = 0f, Hw = 0.03162117f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 115.537239f, Y = 105.435f, Z = 919.925f,
                Hx = 0f, Hy = 0.9994416f, Hz = 0f, Hw = 0.03341278f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 84.37098f, Y = 105.435f, Z = 919.7193f,
                Hx = 0f, Hy = 0.999850631f, Hz = 0f, Hw = 0.01728218f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 157.017212f, Y = 105.435f, Z = 1032.6709f,
                Hx = 0f, Hy = 0.713687062f, Hz = 0f, Hw = 0.700464666f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 197.66272f, Y = 105.435f, Z = 1029.51868f,
                Hx = 0f, Hy = -0.699067f, Hz = 0f, Hw = 0.7150562f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 157.6471f, Y = 105.435f, Z = 963.200134f,
                Hx = 0f, Hy = 0.6994417f, Hz = 0f, Hw = 0.7146897f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 156.772675f, Y = 105.435f, Z = 1056.4939f,
                Hx = 0f, Hy = 0.7273428f, Hz = 0f, Hw = 0.68627435f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 158.154465f, Y = 105.435f, Z = 934.156738f,
                Hx = 0f, Hy = 0.6954589f, Hz = 0f, Hw = 0.7185659f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 197.912659f, Y = 105.435f, Z = 966.009766f,
                Hx = 0f, Hy = -0.734349847f, Hz = 0f, Hw = 0.6787712f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 198.377121f, Y = 105.435f, Z = 936.33f,
                Hx = 0f, Hy = -0.703964949f, Hz = 0f, Hw = 0.710234761f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 217.640915f, Y = 105.435f, Z = 918.4452f,
                Hx = 0f, Hy = 0.9999887f, Hz = 0f, Hw = 0.0047557936f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 245.134689f, Y = 105.435f, Z = 918.799561f,
                Hx = 0f, Hy = -0.9996344f, Hz = 0f, Hw = 0.02703796f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 212.766663f, Y = 105.435f, Z = 884.8233f,
                Hx = 0f, Hy = 0.00221127225f, Hz = 0f, Hw = 0.999997556f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 338.368256f, Y = 105.435f, Z = 884.4024f,
                Hx = 0f, Hy = -0.0169253945f, Hz = 0f, Hw = 0.99985677f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 274.592346f, Y = 105.435f, Z = 919.1138f,
                Hx = 0f, Hy = -0.999462545f, Hz = 0f, Hw = 0.03278118f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 221.291626f, Y = 108.748512f, Z = 1311.56665f,
                Hx = 0f, Hy = -0.453704566f, Hz = 0f, Hw = 0.891152143f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 139.836777f, Y = 105.435f, Z = 1076.09717f,
                Hx = 0f, Hy = 0.03387017f, Hz = 0f, Hw = 0.999426246f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 49.8476334f, Y = 105.435f, Z = 1076.30481f,
                Hx = 0f, Hy = 0.0121124424f, Hz = 0f, Hw = 0.9999266f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 301.652618f, Y = 105.435f, Z = 1073.84119f,
                Hx = 0f, Hy = 0.0402898155f, Hz = 0f, Hw = 0.999188066f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 274.1186f, Y = 105.435f, Z = 1074.19946f,
                Hx = 0f, Hy = 0.0132576255f, Hz = 0f, Hw = 0.999912143f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 243.133133f, Y = 105.435f, Z = 1074.41992f,
                Hx = 0f, Hy = 0.00900332f, Hz = 0f, Hw = 0.999959469f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 215.101837f, Y = 105.435f, Z = 1074.56506f,
                Hx = 0f, Hy = -0.0331955068f, Hz = 0f, Hw = 0.9994489f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 111.549538f, Y = 105.435f, Z = 1076.1958f,
                Hx = 0f, Hy = 0.00349448086f, Hz = 0f, Hw = 0.9999939f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 80.31168f, Y = 105.435f, Z = 1076.6051f,
                Hx = 0f, Hy = -0.0245445482f, Hz = 0f, Hw = 0.999698758f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 85.43356f, Y = 105.435f, Z = 884.317261f,
                Hx = 0f, Hy = -0.0206705239f, Hz = 0f, Hw = 0.9997863f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 54.21466f, Y = 105.435f, Z = 884.197449f,
                Hx = 0f, Hy = -0.0344047956f, Hz = 0f, Hw = 0.999408f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 182.38179f, Y = 105.435f, Z = 884.356f,
                Hx = 0f, Hy = -0.0199544616f, Hz = 0f, Hw = 0.999800861f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 306.42f, Y = 105.435f, Z = 884.1868f,
                Hx = 0f, Hy = 0.0271294583f, Hz = 0f, Hw = 0.999631941f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 150.722839f, Y = 105.435f, Z = 884.3175f,
                Hx = 0f, Hy = 0.0275746416f, Hz = 0f, Hw = 0.9996197f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 275.502533f, Y = 105.435f, Z = 884.5067f,
                Hx = 0f, Hy = -0.0133669293f, Hz = 0f, Hw = 0.999910653f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or",
                Level = 30, Health = 1640, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 219.069077f, Y = 108.68486f, Z = 1312.48853f,
                Hx = 0f, Hy = -0.7122598f, Hz = 0f, Hw = 0.7019159f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or of Flaming Barrels",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 145.609634f, Y = 105.01001f, Z = 971.056f,
                Hx = 0f, Hy = -0.8390646f, Hz = 0f, Hw = 0.5440318f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or of Gear & Ammo",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 138.601974f, Y = 105.01001f, Z = 1049.893f,
                Hx = 0f, Hy = -0.9944f, Hz = 0f, Hw = 0.105681337f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or of Preservation",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 116.035431f, Y = 105.01001f, Z = 1048.88562f,
                Hx = 0f, Hy = 0.9731598f, Hz = 0f, Hw = 0.230130449f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or of Protection",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 115.613449f, Y = 105.01001f, Z = 945.9393f,
                Hx = 0f, Hy = 0.00111398834f, Hz = 0f, Hw = 0.9999994f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Craig-Or of the Furious Fists",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 143.383545f, Y = 105.01001f, Z = 946.3773f,
                Hx = 0f, Hy = -0.00119129f, Hz = 0f, Hw = 0.9999993f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 131.876633f, Y = 125.155f, Z = 1792.88269f,
                Hx = 0f, Hy = 0.642962158f, Hz = 0f, Hw = 0.765898f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 148.782257f, Y = 125.155f, Z = 1636.80249f,
                Hx = 0f, Hy = 0.171160936f, Hz = 0f, Hw = 0.9852431f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 159.979218f, Y = 156.454987f, Z = 1528.02991f,
                Hx = 0f, Hy = -0.9780204f, Hz = 0f, Hw = 0.208509058f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 180.483917f, Y = 105.409248f, Z = 1386.1377f,
                Hx = 0f, Hy = -0.259638965f, Hz = 0f, Hw = 0.965705752f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 144.005936f, Y = 105.023788f, Z = 1692.97034f,
                Hx = 0f, Hy = -0.910732567f, Hz = 0f, Hw = 0.41299665f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 154.437836f, Y = 105.356f, Z = 1674.27832f,
                Hx = 0f, Hy = 0.05771036f, Hz = 0f, Hw = 0.9983334f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 150.646912f, Y = 106.613045f, Z = 1501.67578f,
                Hx = 0f, Hy = 0.9027066f, Hz = 0f, Hw = 0.4302566f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 155.725266f, Y = 107.374794f, Z = 1491.61584f,
                Hx = 0f, Hy = -0.9678167f, Hz = 0f, Hw = 0.251656145f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 115.7444f, Y = 104.453232f, Z = 1483.74011f,
                Hx = 0f, Hy = 0.989008665f, Hz = 0f, Hw = 0.147857681f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 125.0386f, Y = 105.01001f, Z = 1451.75293f,
                Hx = 0f, Hy = -0.983299434f, Hz = 0f, Hw = 0.181995153f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 150.849625f, Y = 102.8593f, Z = 1376.11731f,
                Hx = 0f, Hy = 0.871049643f, Hz = 0f, Hw = 0.491194963f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 136.777054f, Y = 107.233986f, Z = 1357.20178f,
                Hx = 0f, Hy = 0.1492453f, Hz = 0f, Hw = 0.9888002f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 175.180115f, Y = 105.259056f, Z = 1399.72961f,
                Hx = 0f, Hy = -0.6563849f, Hz = 0f, Hw = 0.7544262f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 158.643158f, Y = 105.610008f, Z = 1710.35486f,
                Hx = 0f, Hy = 0.0932013f, Hz = 0f, Hw = 0.9956473f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 221.921875f, Y = 105.795059f, Z = 1671.72754f,
                Hx = 0f, Hy = -0.3225536f, Hz = 0f, Hw = 0.9465512f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 206.906982f, Y = 105.610008f, Z = 1869.67871f,
                Hx = 0f, Hy = 0.755102634f, Hz = 0f, Hw = 0.655606568f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 180.448608f, Y = 106.04464f, Z = 1548.036f,
                Hx = 0f, Hy = 0.08790191f, Hz = 0f, Hw = 0.996129155f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 160.644623f, Y = 108.610008f, Z = 1474.91614f,
                Hx = 0f, Hy = -0.643570244f, Hz = 0f, Hw = 0.765387058f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 152.043243f, Y = 107.754593f, Z = 1458.214f,
                Hx = 0f, Hy = 0.734976232f, Hz = 0f, Hw = 0.6780929f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 206.959885f, Y = 106.363464f, Z = 1741.97681f,
                Hx = 0f, Hy = 0.145135462f, Hz = 0f, Hw = 0.9894118f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 180.188538f, Y = 103.810005f, Z = 1681.0752f,
                Hx = 0f, Hy = -0.486326218f, Hz = 0f, Hw = 0.87377733f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 192.450256f, Y = 105.600594f, Z = 1619.97339f,
                Hx = 0f, Hy = 0.944394052f, Hz = 0f, Hw = 0.328815877f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 161.604828f, Y = 105.872032f, Z = 1605.71851f,
                Hx = 0f, Hy = 0.273545176f, Hz = 0f, Hw = 0.961859167f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 172.236969f, Y = 105.308578f, Z = 1590.37952f,
                Hx = 0f, Hy = 0.7946522f, Hz = 0f, Hw = 0.6070649f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 162.491135f, Y = 105.831154f, Z = 1572.09119f,
                Hx = 0f, Hy = 0.9974757f, Hz = 0f, Hw = 0.07100869f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 143.149429f, Y = 105.01001f, Z = 1577.13f,
                Hx = 0f, Hy = -0.955960751f, Hz = 0f, Hw = 0.293494582f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 159.946808f, Y = 105.5997f, Z = 1552.06873f,
                Hx = 0f, Hy = -0.774408042f, Hz = 0f, Hw = 0.6326865f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 233.0789f, Y = 105.860985f, Z = 1378.02258f,
                Hx = 0f, Hy = -0.9506046f, Hz = 0f, Hw = 0.310404271f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 233.674057f, Y = 105.305847f, Z = 1368.34229f,
                Hx = 0f, Hy = -0.387527466f, Hz = 0f, Hw = 0.9218582f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 235.329956f, Y = 107.952286f, Z = 1351.39209f,
                Hx = 0f, Hy = 0.9987886f, Hz = 0f, Hw = 0.0492066443f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 207.649323f, Y = 106.085495f, Z = 1751.1416f,
                Hx = 0f, Hy = 0.9963515f, Hz = 0f, Hw = 0.08534492f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 519.1503f, Y = 14.7105713f, Z = 1493.1781f,
                Hx = 0f, Hy = -0.123445973f, Hz = 0f, Hw = 0.9923513f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 116.282059f, Y = 109.271957f, Z = 1351.909f,
                Hx = 0f, Hy = 0.3351629f, Hz = 0f, Hw = 0.9421602f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 182.507141f, Y = 107.787178f, Z = 1350.29224f,
                Hx = 0f, Hy = 0.9826721f, Hz = 0f, Hw = 0.185352579f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 240.951584f, Y = 107.519493f, Z = 1234.37512f,
                Hx = 0f, Hy = -0.931253552f, Hz = 0f, Hw = 0.364371777f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 207.095047f, Y = 110.933083f, Z = 1268.823f,
                Hx = 0f, Hy = 0.423574537f, Hz = 0f, Hw = 0.905861259f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 572.9564f, Y = 12.2370682f, Z = 1156.96729f,
                Hx = 0f, Hy = 0.7685116f, Hz = 0f, Hw = 0.639835835f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 596.6806f, Y = 11.7789879f, Z = 1541.7395f,
                Hx = 0f, Hy = 0.5563091f, Hz = 0f, Hw = 0.8309754f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 126.440567f, Y = 105.01001f, Z = 1441.704f,
                Hx = 0f, Hy = -0.104016379f, Hz = 0f, Hw = 0.99457556f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 123.543449f, Y = 103.370918f, Z = 1424.508f,
                Hx = 0f, Hy = 0.3698662f, Hz = 0f, Hw = 0.929085f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 145.9134f, Y = 104.727242f, Z = 1735.43079f,
                Hx = 0f, Hy = 0.998546839f, Hz = 0f, Hw = 0.05389103f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 94.68677f, Y = 103.151329f, Z = 1404.89746f,
                Hx = 0f, Hy = 0.28734082f, Hz = 0f, Hw = 0.9578284f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 136.033325f, Y = 105.01001f, Z = 1760.55859f,
                Hx = 0f, Hy = 0.418908626f, Hz = 0f, Hw = 0.9080284f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 584.735046f, Y = 12.0803738f, Z = 1570.50146f,
                Hx = 0f, Hy = 0.58099854f, Hz = 0f, Hw = 0.8139046f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 706.553467f, Y = 17.035f, Z = 1424.36072f,
                Hx = 0f, Hy = -0.649553f, Hz = 0f, Hw = 0.7603163f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 197.007492f, Y = 105.01001f, Z = 1822.64148f,
                Hx = 0f, Hy = -0.983781338f, Hz = 0f, Hw = 0.179371923f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 154.90007f, Y = 105.046333f, Z = 1834.362f,
                Hx = 0f, Hy = 0.06961165f, Hz = 0f, Hw = 0.997574151f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 166.498825f, Y = 103.810005f, Z = 1810.90723f,
                Hx = 0f, Hy = -0.086232394f, Hz = 0f, Hw = 0.996275067f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 23, Health = 1160, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 145.157791f, Y = 105.01001f, Z = 1798.32043f,
                Hx = 0f, Hy = -0.0207813643f, Hz = 0f, Hw = 0.999784052f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 151.970337f, Y = 105.610008f, Z = 1774.16711f,
                Hx = 0f, Hy = -0.0599331632f, Hz = 0f, Hw = 0.9982024f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 210.983139f, Y = 105.166779f, Z = 1846.84326f,
                Hx = 0f, Hy = -0.158179134f, Hz = 0f, Hw = 0.9874104f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 25, Health = 1300, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 233.881119f, Y = 105.01001f, Z = 1843.23157f,
                Hx = 0f, Hy = 0.836785436f, Hz = 0f, Hw = 0.547530949f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 233.615326f, Y = 106.046722f, Z = 1810.7439f,
                Hx = 0f, Hy = 0.223233745f, Hz = 0f, Hw = 0.974764943f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 687.0208f, Y = 17.035f, Z = 1449.7522f,
                Hx = 0f, Hy = -0.9391632f, Hz = 0f, Hw = 0.3434712f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 651.721f, Y = 11.9817581f, Z = 1360.07166f,
                Hx = 0f, Hy = 0.101423308f, Hz = 0f, Hw = 0.994843364f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 640.4051f, Y = 12.3788052f, Z = 1283.01746f,
                Hx = 0f, Hy = 0.03128189f, Hz = 0f, Hw = 0.9995106f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 606.82135f, Y = 12.0810289f, Z = 1310.70313f,
                Hx = 0f, Hy = 0.6200282f, Hz = 0f, Hw = 0.7845795f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 631.40564f, Y = 11.7490883f, Z = 1278.28137f,
                Hx = 0f, Hy = -0.673062f, Hz = 0f, Hw = 0.739586055f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 629.8565f, Y = 12.1854658f, Z = 1244.8988f,
                Hx = 0f, Hy = -0.6658536f, Hz = 0f, Hw = 0.7460824f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 636.772339f, Y = 11.7953281f, Z = 1208.17456f,
                Hx = 0f, Hy = -0.6260628f, Hz = 0f, Hw = 0.779772639f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 616.8971f, Y = 11.7170906f, Z = 1169.15186f,
                Hx = 0f, Hy = -0.5467772f, Hz = 0f, Hw = 0.8372781f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 621.451965f, Y = 12.0382776f, Z = 1168.07507f,
                Hx = 0f, Hy = 0.253768653f, Hz = 0f, Hw = 0.96726495f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 604.4719f, Y = 12.2674322f, Z = 1284.80493f,
                Hx = 0f, Hy = 0.770739853f, Hz = 0f, Hw = 0.63715f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 577.221558f, Y = 12.0680618f, Z = 1200.006f,
                Hx = 0f, Hy = 0.0717533156f, Hz = 0f, Hw = 0.9974224f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 599.8763f, Y = 11.762145f, Z = 1261.98279f,
                Hx = 0f, Hy = 0.775402248f, Hz = 0f, Hw = 0.63146764f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 595.576355f, Y = 11.9901829f, Z = 1230.24426f,
                Hx = 0f, Hy = 0.7830995f, Hz = 0f, Hw = 0.621896446f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 580.594666f, Y = 11.8300419f, Z = 1203.92883f,
                Hx = 0f, Hy = 0.7572043f, Hz = 0f, Hw = 0.6531781f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 614.664368f, Y = 15.0865889f, Z = 1480.058f,
                Hx = 0f, Hy = -0.7003387f, Hz = 0f, Hw = 0.7138107f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 560.043f, Y = 12.9214325f, Z = 1449.92358f,
                Hx = 0f, Hy = 0.5186226f, Hz = 0f, Hw = 0.8550033f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 573.102966f, Y = 11.9724922f, Z = 1469.77881f,
                Hx = 0f, Hy = 0.9149359f, Hz = 0f, Hw = 0.403599143f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 576.438843f, Y = 11.8901176f, Z = 1453.84949f,
                Hx = 0f, Hy = 0.3533726f, Hz = 0f, Hw = 0.9354827f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 524.132f, Y = 73.02972f, Z = 1799.30432f,
                Hx = 0f, Hy = 0.2675155f, Hz = 0f, Hw = 0.963553548f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 24, Health = 1230, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 519.874756f, Y = 69.12541f, Z = 1798.36487f,
                Hx = 0f, Hy = -0.7903419f, Hz = 0f, Hw = 0.612666f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            // Capture 20260718-173204 cave mouth cluster (patrol filled by 20260827-221909 routes).
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 559.9511f, Y = 48.880043f, Z = 1726.40247f,
                Hx = 0f, Hy = -0.6976899f, Hz = 0f, Hw = 0.7163999f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 570.7248f, Y = 49.0771751f, Z = 1731.08618f,
                Hx = 0f, Hy = -0.9205477f, Hz = 0f, Hw = 0.390630126f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 559.9958f, Y = 46.32206f, Z = 1720.329f,
                Hx = 0f, Hy = -0.700605631f, Hz = 0f, Hw = 0.7135487f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            // Capture 20260827-221909 SCFU 7A372E06 / 7A372E07 / 7A372E0C cave-mouth pair + ledge.
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 535.7803f, Y = 55.8844f, Z = 1739.284f,
                Hx = 0f, Hy = 0.2810554f, Hz = 0f, Hw = 0.9596915f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 535.7803f, 55.8844f, 1739.284f },
                    new[] { 539.498535f, 55.8814468f, 1743.97485f },
                },
                CaptureFolder = "20260827-221909",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 536.4755f, Y = 53.59499f, Z = 1730.014f,
                Hx = 0f, Hy = -0.4962857f, Hz = 0f, Hw = 0.8681592f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 536.4755f, 53.59499f, 1730.014f },
                    new[] { 534.471069f, 56.1467514f, 1736.71606f },
                },
                CaptureFolder = "20260827-221909",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 556.2581f, Y = 47.41654f, Z = 1720.475f,
                Hx = 0f, Hy = 0.7058362f, Hz = 0f, Hw = 0.708375f,
                Textures = null,
                Meshes = null,
                Waypoints = new[]
                {
                    new[] { 556.2581f, 47.41654f, 1720.475f },
                    new[] { 573.944153f, 45.4753075f, 1717.77551f },
                },
                CaptureFolder = "20260827-221909",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 573.0863f, Y = 12.9989309f, Z = 1640.1554f,
                Hx = 0f, Hy = 0.0158257652f, Hz = 0f, Hw = 0.9998748f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 579.9517f, Y = 11.9481459f, Z = 1608.30884f,
                Hx = 0f, Hy = 0.80689317f, Hz = 0f, Hw = 0.5906974f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 594.6964f, Y = 12.5442314f, Z = 1630.90784f,
                Hx = 0f, Hy = 0.8717811f, Hz = 0f, Hw = 0.4898956f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 600.4356f, Y = 11.881422f, Z = 1601.20081f,
                Hx = 0f, Hy = -0.8445101f, Hz = 0f, Hw = 0.5355396f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 617.372131f, Y = 12.3494053f, Z = 1584.23865f,
                Hx = 0f, Hy = 0.32782203f, Hz = 0f, Hw = 0.9447395f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 619.648438f, Y = 12.0526237f, Z = 1564.05322f,
                Hx = 0f, Hy = -0.9753425f, Hz = 0f, Hw = 0.220696718f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 638.5719f, Y = 11.7077169f, Z = 1530.81f,
                Hx = 0f, Hy = -0.91326046f, Hz = 0f, Hw = 0.4073761f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 670.396851f, Y = 12.5696831f, Z = 1509.40369f,
                Hx = 0f, Hy = -0.860502064f, Hz = 0f, Hw = 0.5094469f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 673.854736f, Y = 14.1010761f, Z = 1530.32178f,
                Hx = 0f, Hy = -0.550420046f, Hz = 0f, Hw = 0.834887862f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 681.646f, Y = 16.4944572f, Z = 1518.20728f,
                Hx = 0f, Hy = -0.446350724f, Hz = 0f, Hw = 0.8948581f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 645.419739f, Y = 12.5910788f, Z = 1502.321f,
                Hx = 0f, Hy = -0.6201587f, Hz = 0f, Hw = 0.7844764f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 640.848633f, Y = 12.1456947f, Z = 1465.6427f,
                Hx = 0f, Hy = -0.6443867f, Hz = 0f, Hw = 0.764699757f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 646.4863f, Y = 11.9128284f, Z = 1422.36108f,
                Hx = 0f, Hy = -0.763921261f, Hz = 0f, Hw = 0.645309448f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 665.8695f, Y = 12.9414043f, Z = 1410.75537f,
                Hx = 0f, Hy = -0.119066566f, Hz = 0f, Hw = 0.992886245f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 651.511047f, Y = 11.8425026f, Z = 1399.77856f,
                Hx = 0f, Hy = -0.7704116f, Hz = 0f, Hw = 0.637546837f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 28, Health = 1504, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 682.009766f, Y = 12.6604118f, Z = 1363.678f,
                Hx = 0f, Hy = 0.960133731f, Hz = 0f, Hw = 0.279541165f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Crippler of Growth",
                Level = 22, Health = 1090, MonsterData = 209333, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 199.2232f, Y = 105.411469f, Z = 1788.58679f,
                Hx = 0f, Hy = 0.756544232f, Hz = 0f, Hw = 0.6539425f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 648.0897f, Y = 49.81f, Z = 1124.92041f,
                Hx = 0f, Hy = 0.007152981f, Hz = 0f, Hw = 0.9999744f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 160.1001f, Y = 105.01001f, Z = 733.367737f,
                Hx = 0f, Hy = -0.932124f, Hz = 0f, Hw = 0.362139255f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 86.1426f, Y = 105.01001f, Z = 680.0226f,
                Hx = 0f, Hy = 0.4986097f, Hz = 0f, Hw = 0.8668266f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 89.1608047f, Y = 105.01001f, Z = 670.0564f,
                Hx = 0f, Hy = -0.270086348f, Hz = 0f, Hw = 0.9628361f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 115.1987f, Y = 125.519409f, Z = 657.1096f,
                Hx = 0f, Hy = 0.9988326f, Hz = 0f, Hw = 0.04830599f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 136.601227f, Y = 105.01001f, Z = 670.957458f,
                Hx = 0f, Hy = 0.9998752f, Hz = 0f, Hw = -0.0157999974f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 129.352615f, Y = 106.045387f, Z = 659.8014f,
                Hx = 0f, Hy = -0.7924684f, Hz = 0f, Hw = 0.609912932f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 140.198914f, Y = 105.01001f, Z = 657.615051f,
                Hx = 0f, Hy = 0.136953264f, Hz = 0f, Hw = 0.9905775f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 157.36525f, Y = 105.01001f, Z = 720.0458f,
                Hx = 0f, Hy = 0.216354519f, Hz = 0f, Hw = 0.976314843f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 656.2139f, Y = 12.4990826f, Z = 1204.62854f,
                Hx = 0f, Hy = -0.07405107f, Hz = 0f, Hw = 0.997254431f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 669.8893f, Y = 14.0798721f, Z = 1217.17456f,
                Hx = 0f, Hy = -0.501054645f, Hz = 0f, Hw = 0.865415633f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 685.8491f, Y = 10.3222446f, Z = 1208.72f,
                Hx = 0f, Hy = -0.343208015f, Hz = 0f, Hw = 0.9392594f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 672.3651f, Y = 10.210001f, Z = 1179.26453f,
                Hx = 0f, Hy = -0.257689148f, Hz = 0f, Hw = 0.9662279f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 526.175659f, Y = 12.0182581f, Z = 1496.02673f,
                Hx = 0f, Hy = -0.896976352f, Hz = 0f, Hw = 0.442078531f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 125.344933f, Y = 105.01001f, Z = 694.7503f,
                Hx = 0f, Hy = 0.423596f, Hz = 0f, Hw = 0.905851245f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 133.043076f, Y = 105.01001f, Z = 664.3768f,
                Hx = 0f, Hy = -0.6607646f, Hz = 0f, Hw = 0.7505932f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 307.1102f, Y = 104.366882f, Z = 656.2624f,
                Hx = 0f, Hy = -0.9787327f, Hz = 0f, Hw = 0.205139756f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 255.962891f, Y = 104.468079f, Z = 664.0572f,
                Hx = 0f, Hy = -0.8501996f, Hz = 0f, Hw = 0.5264605f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 289.797668f, Y = 105.167137f, Z = 652.9752f,
                Hx = 0f, Hy = 0.690353036f, Hz = 0f, Hw = 0.723472655f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 246.5224f, Y = 104.601585f, Z = 646.751038f,
                Hx = 0f, Hy = 0.784488f, Hz = 0f, Hw = 0.62014395f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 278.2072f, Y = 104.411613f, Z = 568.5679f,
                Hx = 0f, Hy = -0.248973474f, Hz = 0f, Hw = 0.9685103f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 240.9043f, Y = 105.461731f, Z = 551.4775f,
                Hx = 0f, Hy = 0.7635157f, Hz = 0f, Hw = 0.645789266f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 643.920044f, Y = 12.2998247f, Z = 1196.77722f,
                Hx = 0f, Hy = 0.267458916f, Hz = 0f, Hw = 0.9635693f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 206.095154f, Y = 105.330284f, Z = 475.9915f,
                Hx = 0f, Hy = -0.282996029f, Hz = 0f, Hw = 0.9591211f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 193.656891f, Y = 105.01001f, Z = 490.689f,
                Hx = 0f, Hy = -0.2987229f, Hz = 0f, Hw = 0.9543399f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 204.922256f, Y = 105.01001f, Z = 510.1279f,
                Hx = 0f, Hy = -0.293383777f, Hz = 0f, Hw = 0.9559947f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 230.760086f, Y = 106.210007f, Z = 496.9605f,
                Hx = 0f, Hy = -0.242518425f, Hz = 0f, Hw = 0.9701468f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 245.40274f, Y = 104.57766f, Z = 502.057922f,
                Hx = 0f, Hy = -0.286640584f, Hz = 0f, Hw = 0.9580382f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 228.13855f, Y = 105.964195f, Z = 533.556763f,
                Hx = 0f, Hy = 0.2708703f, Hz = 0f, Hw = 0.962615848f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 212.292114f, Y = 104.970436f, Z = 520.008362f,
                Hx = 0f, Hy = -0.8867848f, Hz = 0f, Hw = 0.462182552f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 265.37146f, Y = 104.529243f, Z = 541.8195f,
                Hx = 0f, Hy = -0.9988532f, Hz = 0f, Hw = 0.0478781536f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 213.919525f, Y = 105.01001f, Z = 552.0294f,
                Hx = 0f, Hy = 0.675882459f, Hz = 0f, Hw = 0.7370094f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 202.618927f, Y = 105.432816f, Z = 522.902f,
                Hx = 0f, Hy = -0.9363992f, Hz = 0f, Hw = 0.350936562f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 217.665878f, Y = 105.21138f, Z = 585.3142f,
                Hx = 0f, Hy = 0.9685136f, Hz = 0f, Hw = 0.248960644f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 34, Health = 1912, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 231.703842f, Y = 105.571587f, Z = 577.6015f,
                Hx = 0f, Hy = -0.882427037f, Hz = 0f, Hw = 0.4704493f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 251.764557f, Y = 104.239563f, Z = 590.8968f,
                Hx = 0f, Hy = -0.219924167f, Hz = 0f, Hw = 0.975517f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 251.56691f, Y = 104.364861f, Z = 595.2094f,
                Hx = 0f, Hy = 0.0305627f, Hz = 0f, Hw = 0.9995329f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 261.783447f, Y = 104.718765f, Z = 619.7853f,
                Hx = 0f, Hy = -0.9138507f, Hz = 0f, Hw = 0.406050265f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 264.328918f, Y = 105.065346f, Z = 641.476746f,
                Hx = 0f, Hy = -0.877088f, Hz = 0f, Hw = 0.480329722f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 32, Health = 1776, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 278.8567f, Y = 105.307961f, Z = 613.5128f,
                Hx = 0f, Hy = -0.8628206f, Hz = 0f, Hw = 0.5055102f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 272.7596f, Y = 104.981636f, Z = 634.6203f,
                Hx = 0f, Hy = -0.986336946f, Hz = 0f, Hw = 0.1647406f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 529.435242f, Y = 12.3262043f, Z = 1464.33826f,
                Hx = 0f, Hy = -0.973399341f, Hz = 0f, Hw = 0.22911498f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 531.019958f, Y = 12.6780338f, Z = 1460.11829f,
                Hx = 0f, Hy = 0.6224526f, Hz = 0f, Hw = 0.7826575f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 520.0723f, Y = 11.2167692f, Z = 1450.68347f,
                Hx = 0f, Hy = -0.924844444f, Hz = 0f, Hw = 0.3803455f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 33, Health = 1844, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 524.2564f, Y = 11.4100008f, Z = 1481.75293f,
                Hx = 0f, Hy = 0.6432286f, Hz = 0f, Hw = 0.765674233f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Croaker of Night",
                Level = 35, Health = 1980, MonsterData = 209319, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 536.939941f, Y = 11.8732481f, Z = 1496.44788f,
                Hx = 0f, Hy = -0.988243043f, Hz = 0f, Hw = 0.152891219f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Ehmat",
                Level = 40, Health = 5800, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 530.755737f, Y = 11.905117f, Z = 1491.27246f,
                Hx = 0f, Hy = 0.9983738f, Hz = 0f, Hw = 0.05700609f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Follower Gulu-Man Thrak",
                Level = 40, Health = 2320, MonsterData = 208647, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 210.5639f, Y = 105.01001f, Z = 1022.53455f,
                Hx = 0f, Hy = 0.563808441f, Hz = 0f, Hw = 0.8259056f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Follower Orma-Urga Thrak",
                Level = 40, Health = 2320, MonsterData = 208647, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 345.435944f, Y = 106.109642f, Z = 939.1179f,
                Hx = 0f, Hy = 0.9751882f, Hz = 0f, Hw = 0.221377492f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Fortuitous Chi-Chi Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 203.213638f, Y = 106.91272f, Z = 832.6803f,
                Hx = 0f, Hy = 0.360881478f, Hz = 0f, Hw = 0.9326117f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Fortuitous Orma-Yutt Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 276.65f, Y = 107.044235f, Z = 828.3868f,
                Hx = 0f, Hy = 0.9998496f, Hz = 0f, Hw = 0.01734237f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hateful Weaver",
                Level = 35, Health = 4950, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 209.383133f, Y = 110.306351f, Z = 1263.83826f,
                Hx = 0.0294982232f, Hy = 0.547002256f, Hz = 0.00355546456f, Hw = 0.8366037f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 80.02282f, Y = 105.01001f, Z = 604.8822f,
                Hx = 0f, Hy = 0.4358534f, Hz = 0f, Hw = 0.9000177f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 101.190849f, Y = 105.01001f, Z = 600.053833f,
                Hx = 0f, Hy = 0.3827511f, Hz = 0f, Hw = 0.9238515f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 178.855316f, Y = 105.610008f, Z = 1911.89551f,
                Hx = 0f, Hy = 0.507909536f, Hz = 0f, Hw = 0.861410439f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 143.998459f, Y = 105.015778f, Z = 1897.60132f,
                Hx = 0f, Hy = -0.105265416f, Hz = 0f, Hw = 0.9944442f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 100.20327f, Y = 105.01001f, Z = 1879.46765f,
                Hx = 0f, Hy = 0.8416784f, Hz = 0f, Hw = 0.5399792f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 105.96521f, Y = 105.01001f, Z = 1771.37415f,
                Hx = 0f, Hy = 0.8575994f, Hz = 0f, Hw = 0.514318347f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 119.791359f, Y = 105.01001f, Z = 1726.51013f,
                Hx = 0f, Hy = -0.06377233f, Hz = 0f, Hw = 0.9979645f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 135.377655f, Y = 105.01001f, Z = 1760.02771f,
                Hx = 0f, Hy = 0.371293753f, Hz = 0f, Hw = 0.928515434f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 116.720528f, Y = 105.01001f, Z = 1626.59619f,
                Hx = 0f, Hy = -0.269269258f, Hz = 0f, Hw = 0.9630649f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 85.61709f, Y = 104.486923f, Z = 1575.47961f,
                Hx = 0f, Hy = 0.9621721f, Hz = 0f, Hw = 0.2724424f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 87.57123f, Y = 105.374619f, Z = 1609.8905f,
                Hx = 0f, Hy = -0.3589823f, Hz = 0f, Hw = 0.933344364f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 147.010956f, Y = 105.544327f, Z = 152.2269f,
                Hx = 0f, Hy = 0.201071039f, Hz = 0f, Hw = 0.979576647f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 181.115723f, Y = 105.01001f, Z = 93.41251f,
                Hx = 0f, Hy = 0.6139437f, Hz = 0f, Hw = 0.7893498f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 208.585754f, Y = 109.066711f, Z = 33.41357f,
                Hx = 0f, Hy = 0.0172311012f, Hz = 0f, Hw = 0.9998515f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 260.521118f, Y = 105.01001f, Z = 53.69665f,
                Hx = 0f, Hy = 0.798347f, Hz = 0f, Hw = 0.602197766f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Earth",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 338.3746f, Y = 105.915016f, Z = 54.78953f,
                Hx = 0f, Hy = 0.9059213f, Hz = 0f, Hw = 0.423446178f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 181.532974f, Y = 105.01001f, Z = 169.439316f,
                Hx = 0f, Hy = 0.7573363f, Hz = 0f, Hw = 0.653025031f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 73.56276f, Y = 102.336609f, Z = 1472.368f,
                Hx = 0f, Hy = -0.9790159f, Hz = 0f, Hw = 0.203783914f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 111.602074f, Y = 105.01001f, Z = 569.459534f,
                Hx = 0f, Hy = 0.9686617f, Hz = 0f, Hw = 0.248383582f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 104.118881f, Y = 105.01001f, Z = 523.3233f,
                Hx = 0f, Hy = 0.4087737f, Hz = 0f, Hw = 0.9126358f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 152.141617f, Y = 105.01001f, Z = 530.69696f,
                Hx = 0f, Hy = -0.594466746f, Hz = 0f, Hw = 0.8041202f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 140.172287f, Y = 104.930939f, Z = 1939.73254f,
                Hx = 0f, Hy = -0.9256262f, Hz = 0f, Hw = 0.378439069f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 92.8295f, Y = 108.839256f, Z = 1956.77026f,
                Hx = 0f, Hy = -0.410498053f, Hz = 0f, Hw = 0.9118615f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 86.22924f, Y = 105.238686f, Z = 1678.91418f,
                Hx = 0f, Hy = 0.09696617f, Hz = 0f, Hw = 0.995287657f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 111.90583f, Y = 105.01001f, Z = 1581.44043f,
                Hx = 0f, Hy = 0.3846718f, Hz = 0f, Hw = 0.9230534f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 112.859627f, Y = 103.604538f, Z = 1523.43323f,
                Hx = 0f, Hy = 0.225830659f, Hz = 0f, Hw = 0.9741666f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 88.99033f, Y = 105.01001f, Z = 1448.92126f,
                Hx = 0f, Hy = -0.936714351f, Hz = 0f, Hw = 0.350094646f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 71.3125f, Y = 105.01001f, Z = 1332.92078f,
                Hx = 0f, Hy = -0.689963758f, Hz = 0f, Hw = 0.723843932f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 88.32345f, Y = 105.610008f, Z = 1313.04419f,
                Hx = 0f, Hy = -0.5078961f, Hz = 0f, Hw = 0.8614183f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 59.4598541f, Y = 105.846809f, Z = 1284.68909f,
                Hx = 0f, Hy = 0.349353373f, Hz = 0f, Hw = 0.936991036f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 109.719986f, Y = 107.674f, Z = 1219.47253f,
                Hx = 0f, Hy = 0.9084117f, Hz = 0f, Hw = 0.418076873f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 88.36378f, Y = 105.01001f, Z = 1208.556f,
                Hx = 0f, Hy = 0.696297f, Hz = 0f, Hw = 0.717753768f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 66.606f, Y = 104.490158f, Z = 1223.0481f,
                Hx = 0f, Hy = -0.128344953f, Hz = 0f, Hw = 0.991729558f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 159.2489f, Y = 104.903343f, Z = 197.9685f,
                Hx = 0f, Hy = -0.9979046f, Hz = 0f, Hw = 0.06470283f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 178.626144f, Y = 106.497f, Z = 235.247147f,
                Hx = 0f, Hy = -0.3494831f, Hz = 0f, Hw = 0.9369427f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 135.420532f, Y = 105.6048f, Z = 127.717407f,
                Hx = 0f, Hy = -0.6526403f, Hz = 0f, Hw = 0.7576679f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 372.8281f, Y = 105.208672f, Z = 35.29255f,
                Hx = 0f, Hy = -0.0380304232f, Hz = 0f, Hw = 0.9992766f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 432.562561f, Y = 108.91394f, Z = 55.4753647f,
                Hx = 0f, Hy = -0.7991169f, Hz = 0f, Hw = 0.601175666f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 442.850861f, Y = 106.927986f, Z = 67.0015945f,
                Hx = 0f, Hy = 0.4732433f, Hz = 0f, Hw = 0.8809318f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 475.002625f, Y = 104.335793f, Z = 91.15116f,
                Hx = 0f, Hy = -0.460876256f, Hz = 0f, Hw = 0.8874644f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 436.252167f, Y = 105.01001f, Z = 152.73288f,
                Hx = 0f, Hy = 0.0963283f, Hz = 0f, Hw = 0.995349646f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 417.963959f, Y = 106.106033f, Z = 154.7858f,
                Hx = 0f, Hy = -0.0493341945f, Hz = 0f, Hw = 0.998782337f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 439.26f, Y = 105.638367f, Z = 185.887527f,
                Hx = 0f, Hy = -0.899661362f, Hz = 0f, Hw = 0.436588436f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 401.2536f, Y = 104.600555f, Z = 186.75798f,
                Hx = 0f, Hy = 0.467568129f, Hz = 0f, Hw = 0.883957f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 388.3601f, Y = 109.69828f, Z = 168.773132f,
                Hx = 0f, Hy = -0.4513072f, Hz = 0f, Hw = 0.8923687f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 433.095276f, Y = 105.160057f, Z = 202.144791f,
                Hx = 0f, Hy = 0.104818694f, Hz = 0f, Hw = 0.994491339f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 385.7283f, Y = 109.080444f, Z = 228.984741f,
                Hx = 0f, Hy = 0.994847238f, Hz = 0f, Hw = 0.101385348f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 367.7724f, Y = 105.12516f, Z = 221.458771f,
                Hx = 0f, Hy = 0.9275939f, Hz = 0f, Hw = 0.3735901f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 338.720245f, Y = 107.912292f, Z = 231.620178f,
                Hx = 0f, Hy = 0.850720644f, Hz = 0f, Hw = 0.5256181f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 340.104156f, Y = 104.899025f, Z = 254.91629f,
                Hx = 0f, Hy = -0.704361439f, Hz = 0f, Hw = 0.7098415f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 368.442871f, Y = 106.365364f, Z = 262.1451f,
                Hx = 0f, Hy = 0.9998106f, Hz = 0f, Hw = -0.0194615163f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 337.344543f, Y = 105.860382f, Z = 280.280823f,
                Hx = 0f, Hy = 0.007008534f, Hz = 0f, Hw = 0.999975443f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 300.59024f, Y = 105.01001f, Z = 264.8704f,
                Hx = 0f, Hy = -0.6625762f, Hz = 0f, Hw = 0.7489945f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 311.307f, Y = 106.3182f, Z = 287.131256f,
                Hx = 0f, Hy = -0.751406133f, Hz = 0f, Hw = 0.65984f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 283.509979f, Y = 105.360657f, Z = 286.727264f,
                Hx = 0f, Hy = 0.657741666f, Hz = 0f, Hw = 0.753243566f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 340.094635f, Y = 105.3814f, Z = 302.656342f,
                Hx = 0f, Hy = 0.445023119f, Hz = 0f, Hw = 0.8955191f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 297.363251f, Y = 106.009766f, Z = 296.060577f,
                Hx = 0f, Hy = -0.7444823f, Hz = 0f, Hw = 0.6676422f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 308.139557f, Y = 107.224991f, Z = 318.499237f,
                Hx = 0f, Hy = -0.328531116f, Hz = 0f, Hw = 0.9444931f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 284.9054f, Y = 108.01001f, Z = 319.940979f,
                Hx = 0f, Hy = -0.9670956f, Hz = 0f, Hw = 0.2544132f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Metals",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 147.3083f, Y = 104.789558f, Z = 280.16626f,
                Hx = 0f, Hy = -0.6247125f, Hz = 0f, Hw = 0.7808548f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 73.56276f, Y = 102.336609f, Z = 1472.368f,
                Hx = 0f, Hy = -0.9790159f, Hz = 0f, Hw = 0.203783914f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 90.09162f, Y = 105.01001f, Z = 599.374146f,
                Hx = 0f, Hy = 0.09826661f, Hz = 0f, Hw = 0.9951601f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 90.802f, Y = 103.636307f, Z = 566.870056f,
                Hx = 0f, Hy = -0.9847704f, Hz = 0f, Hw = 0.173859775f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 129.482635f, Y = 105.01001f, Z = 577.8252f,
                Hx = 0f, Hy = -0.9943362f, Hz = 0f, Hw = 0.106280625f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 64.77211f, Y = 103.877724f, Z = 557.0075f,
                Hx = 0f, Hy = -0.980527639f, Hz = 0f, Hw = 0.196381271f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 155.279984f, Y = 101.5122f, Z = 1932.653f,
                Hx = 0f, Hy = 0.7144493f, Hz = 0f, Hw = 0.699687243f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 77.37412f, Y = 105.90229f, Z = 1778.26709f,
                Hx = 0f, Hy = 0.157411367f, Hz = 0f, Hw = 0.9875331f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 56.58757f, Y = 105.337746f, Z = 1837.0061f,
                Hx = 0f, Hy = -0.7730684f, Hz = 0f, Hw = 0.634322643f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 112.12384f, Y = 104.60862f, Z = 1647.71533f,
                Hx = 0f, Hy = 0.07191162f, Hz = 0f, Hw = 0.997411f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 75.3685455f, Y = 104.98378f, Z = 1493.22839f,
                Hx = 0f, Hy = 0.8376151f, Hz = 0f, Hw = 0.546260953f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 60.8758469f, Y = 105.271744f, Z = 1425.636f,
                Hx = 0f, Hy = -0.4143866f, Hz = 0f, Hw = 0.910100937f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 72.75448f, Y = 104.134079f, Z = 1373.1416f,
                Hx = 0f, Hy = 0.8244444f, Hz = 0f, Hw = 0.565942943f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 201.942123f, Y = 105.01001f, Z = 73.03599f,
                Hx = 0f, Hy = 0.998695f, Hz = 0f, Hw = 0.0510714464f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 155.11879f, Y = 104.283821f, Z = 320.853973f,
                Hx = 0f, Hy = -0.9454802f, Hz = 0f, Hw = 0.325679451f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heckler of Stones",
                Level = 80, Health = 5733, MonsterData = 214982, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 152.301788f, Y = 105.01001f, Z = 250.921616f,
                Hx = 0f, Hy = -0.9526391f, Hz = 0f, Hw = 0.304103255f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Heru-Maat",
                Level = 40, Health = 5800, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 512.861755f, Y = 10.3452606f, Z = 1448.49451f,
                Hx = 0f, Hy = 0.607707858f, Hz = 0f, Hw = 0.794160664f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hiathlin Prime",
                Level = 22, Health = 1090, MonsterData = 209196, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 497.553131f, Y = 53.5412331f, Z = 1719.28174f,
                Hx = 0f, Hy = 0.9630927f, Hz = 0f, Hw = 0.26917f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hiathlin Prime",
                Level = 23, Health = 1160, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 497.8496f, Y = 53.41f, Z = 1716.5603f,
                Hx = 0f, Hy = -0.288781047f, Hz = 0f, Hw = 0.9573952f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hiathlin Prime",
                Level = 24, Health = 1230, MonsterData = 209196, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 484.574036f, Y = 56.71176f, Z = 1795.30115f,
                Hx = 0f, Hy = -0.8925195f, Hz = 0f, Hw = 0.451008916f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hiathlin Prime",
                Level = 22, Health = 1090, MonsterData = 209196, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 481.849518f, Y = 56.7328339f, Z = 1793.8761f,
                Hx = 0f, Hy = 0.422969162f, Hz = 0f, Hw = 0.9061441f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hypnagogic Man-Dom Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 87.28653f, Y = 105.01001f, Z = 868.4844f,
                Hx = 0f, Hy = -0.09307142f, Hz = 0f, Hw = 0.9956594f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Hypnagogic Urga-Pi Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 296.494354f, Y = 105.01001f, Z = 867.03894f,
                Hx = 0f, Hy = -0.0192171745f, Hz = 0f, Hw = 0.999815345f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Lord of the Void",
                Level = 250, Health = 2700000, MonsterData = 213208, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 356.883179f, Y = 47.711937f, Z = 1904.32031f,
                Hx = 0f, Hy = 0.9421375f, Hz = 0f, Hw = 0.335226685f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Mesut-Ra",
                Level = 38, Health = 5460, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 218.132156f, Y = 105.01001f, Z = 577.654f,
                Hx = 0f, Hy = 0.9718243f, Hz = 0f, Hw = 0.235706434f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Nesbaneb",
                Level = 38, Health = 5460, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 142.251633f, Y = 105.01001f, Z = 663.184448f,
                Hx = 0f, Hy = -0.6528128f, Hz = 0f, Hw = 0.7575193f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 115.874634f, Y = 105.01001f, Z = 828.8457f,
                Hx = 0f, Hy = 0.4932353f, Hz = 0f, Hw = 0.869895935f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 130.124008f, Y = 106.979706f, Z = 833.383057f,
                Hx = 0f, Hy = -0.6804982f, Hz = 0f, Hw = 0.73274976f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 140.750488f, Y = 106.6162f, Z = 827.9954f,
                Hx = 0f, Hy = 0.8773253f, Hz = 0f, Hw = 0.4798962f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 81.89282f, Y = 105.01001f, Z = 866.602356f,
                Hx = 0f, Hy = 0.5189976f, Hz = 0f, Hw = 0.8547757f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 207.008926f, Y = 106.968613f, Z = 840.042847f,
                Hx = 0f, Hy = 0.431576043f, Hz = 0f, Hw = 0.902076542f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 207.132355f, Y = 107.1509f, Z = 836.8629f,
                Hx = 0f, Hy = -0.9329362f, Hz = 0f, Hw = 0.360041738f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 177.180023f, Y = 105.01001f, Z = 868.3746f,
                Hx = 0f, Hy = -0.7270952f, Hz = 0f, Hw = 0.68653667f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 278.004456f, Y = 104.672134f, Z = 769.8915f,
                Hx = 0f, Hy = 0.987820566f, Hz = 0f, Hw = 0.155597433f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 293.351f, Y = 104.55719f, Z = 768.011f,
                Hx = 0f, Hy = -0.9829071f, Hz = 0f, Hw = 0.184102148f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 288.9415f, Y = 104.782822f, Z = 755.9574f,
                Hx = 0f, Hy = -0.943681f, Hz = 0f, Hw = 0.33085677f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 304.9261f, Y = 105.01001f, Z = 865.8597f,
                Hx = 0f, Hy = -0.7046927f, Hz = 0f, Hw = 0.709512651f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 280.408844f, Y = 106.776772f, Z = 833.1009f,
                Hx = 0f, Hy = -0.9143594f, Hz = 0f, Hw = 0.40490362f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Orad-Or",
                Level = 50, Health = 7500, MonsterData = 208645, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 290.8192f, Y = 106.284515f, Z = 819.83f,
                Hx = 0f, Hy = -0.771345139f, Hz = 0f, Hw = 0.6364171f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Prophet Yutt Thrak",
                Level = 40, Health = 9280, MonsterData = 208635, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 287.974182f, Y = 106.696884f, Z = 1005.84973f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1.00000024f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Ran-Roth Ur",
                Level = 110, Health = 9822, MonsterData = 209368, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 471.809937f, Y = 59.62377f, Z = 1759.09924f,
                Hx = 0f, Hy = 0.6148261f, Hz = 0f, Hw = 0.788662732f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Remur-Nefer",
                Level = 38, Health = 5460, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 127.032669f, Y = 105.01001f, Z = 676.2907f,
                Hx = 0f, Hy = -0.04635785f, Hz = 0f, Hw = 0.9989249f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Stunter of Growth",
                Level = 35, Health = 1980, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 704.354f, Y = 17.035f, Z = 1403.33032f,
                Hx = 0f, Hy = -0.6184897f, Hz = 0f, Hw = 0.7857929f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 34, Health = 1912, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 273.700256f, Y = 105.435f, Z = 921.38385f,
                Hx = 0f, Hy = -0.7455098f, Hz = 0f, Hw = 0.666494668f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 234.136627f, Y = 96.61001f, Z = 892.2941f,
                Hx = 0f, Hy = -0.002049119f, Hz = 0f, Hw = 0.9999979f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 33, Health = 1844, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 107.936676f, Y = 96.61001f, Z = 891.934265f,
                Hx = 0f, Hy = 0.0114544826f, Hz = 0f, Hw = 0.9999344f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 31, Health = 1708, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 221.194366f, Y = 105.01001f, Z = 1001.54919f,
                Hx = 0f, Hy = 0.718492448f, Hz = 0f, Hw = 0.695534766f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 220.904724f, Y = 105.01001f, Z = 993.2076f,
                Hx = 0f, Hy = 0.709604561f, Hz = 0f, Hw = 0.704600155f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 165.188828f, Y = 96.61001f, Z = 1006.18408f,
                Hx = 0f, Hy = 0.695242941f, Hz = 0f, Hw = 0.718774855f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 35, Health = 1980, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 165.8727f, Y = 96.61001f, Z = 987.358643f,
                Hx = 0f, Hy = 0.698830843f, Hz = 0f, Hw = 0.71528697f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 189.989365f, Y = 96.61001f, Z = 988.4388f,
                Hx = 0f, Hy = -0.7129957f, Hz = 0f, Hw = 0.7011684f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 31, Health = 1708, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 127.217888f, Y = 96.61001f, Z = 891.668945f,
                Hx = 0f, Hy = 0.01361452f, Hz = 0f, Hw = 0.9999073f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 33, Health = 1844, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 117.886055f, Y = 104.860664f, Z = 745.023865f,
                Hx = 0f, Hy = 0.999914646f, Hz = 0f, Hw = -0.0130645344f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 33, Health = 1844, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 110.904266f, Y = 105.01001f, Z = 745.024231f,
                Hx = 0f, Hy = 0.9995881f, Hz = 0f, Hw = 0.0286998264f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 31, Health = 1708, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 253.903488f, Y = 96.61001f, Z = 892.111145f,
                Hx = 0f, Hy = -0.018135896f, Hz = 0f, Hw = 0.999835551f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 35, Health = 1980, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 111.752533f, Y = 105.574547f, Z = 851.971069f,
                Hx = 0f, Hy = -0.0198905785f, Hz = 0f, Hw = 0.9998022f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 119.2684f, Y = 105.892349f, Z = 851.402649f,
                Hx = 0f, Hy = 0.0277713686f, Hz = 0f, Hw = 0.9996143f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 32, Health = 1776, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 243.2003f, Y = 105.01001f, Z = 745.2186f,
                Hx = 0f, Hy = 0.999822855f, Hz = 0f, Hw = 0.01882199f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 33, Health = 1844, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 250.512589f, Y = 105.01001f, Z = 744.5907f,
                Hx = 0f, Hy = 0.9995513f, Hz = 0f, Hw = 0.0299531911f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 31, Health = 1708, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 280.763428f, Y = 107.805885f, Z = 739.346069f,
                Hx = 0f, Hy = -0.8520262f, Hz = 0f, Hw = 0.523499131f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 31, Health = 1708, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 247.408356f, Y = 105.675148f, Z = 850.503967f,
                Hx = 0f, Hy = -0.0238442775f, Hz = 0f, Hw = 0.9997157f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Sun-Len",
                Level = 33, Health = 1844, MonsterData = 208647, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 239.3888f, Y = 105.853889f, Z = 851.3419f,
                Hx = 0f, Hy = 0.02422794f, Hz = 0f, Hw = 0.999706447f,
                Textures = null,
                Meshes = new[] { new[] { 1, 247035, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Takheperu",
                Level = 35, Health = 4950, MonsterData = 209319, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 166.718079f, Y = 105.01001f, Z = 739.600769f,
                Hx = 0f, Hy = -0.940762341f, Hz = 0f, Hw = 0.3390667f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Tormenter of Growth",
                Level = 28, Health = 3760, MonsterData = 209333, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 161.83139f, Y = 105.3054f, Z = 1784.256f,
                Hx = 0f, Hy = 0.10311421f, Hz = 0f, Hw = 0.9946695f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Vile Weaver",
                Level = 35, Health = 4950, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 219.171021f, Y = 107.083252f, Z = 1166.98071f,
                Hx = -0.06985699f, Hy = 0.9146193f, Hz = 0.3101f, Hw = 0.249858856f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Visionist Eckel-Man Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 287.169281f, Y = 104.899673f, Z = 765.3255f,
                Hx = 0f, Hy = 0.8781176f, Hz = 0f, Hw = 0.4784448f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 29, Health = 1572, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 118.940094f, Y = 101.10524f, Z = 1120.004f,
                Hx = -0.09383598f, Hy = -0.13340956f, Hz = 0.1445253f, Hw = 0.975965738f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 30, Health = 1640, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 66.4730148f, Y = 102.749054f, Z = 1104.1991f,
                Hx = -0.0259195957f, Hy = -0.9964849f, Hz = 0.00312658586f, Hw = 0.07960083f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 193.5662f, Y = 105.850937f, Z = 1718.75525f,
                Hx = 0.0221945532f, Hy = -0.297473133f, Hz = 0.07101604f, Hw = 0.9518266f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 167.497833f, Y = 104.940681f, Z = 1734.152f,
                Hx = 0.00344777945f, Hy = -0.0462445728f, Hz = 0.074269f, Hw = 0.996159434f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 171.451172f, Y = 105.903419f, Z = 1617.92773f,
                Hx = -0.0224247761f, Hy = 0.950855553f, Hz = -0.07094266f, Hw = 0.300562739f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 233.505478f, Y = 108.01001f, Z = 1231.96094f,
                Hx = 0f, Hy = 0.9056205f, Hz = 0f, Hw = 0.424088985f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 272.5777f, Y = 107.112724f, Z = 1204.78271f,
                Hx = -0.05588918f, Hy = -0.107483171f, Hz = 0.151077271f, Hw = 0.9810706f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 198.02562f, Y = 108.378746f, Z = 1163.66333f,
                Hx = -0.178833917f, Hy = -0.7824103f, Hz = 0.260803461f, Hw = 0.5365017f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 29, Health = 1572, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 212.5097f, Y = 103.810005f, Z = 1136.58691f,
                Hx = 0f, Hy = 0.9868365f, Hz = 0f, Hw = 0.161721081f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 228.989761f, Y = 100.810005f, Z = 1119.90637f,
                Hx = 0f, Hy = -0.568470657f, Hz = 0f, Hw = 0.82270354f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 28, Health = 1504, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 252.245544f, Y = 107.977425f, Z = 1172.337f,
                Hx = 0.06468052f, Hy = 0.8669117f, Hz = -0.0367737226f, Hw = 0.492877483f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 29, Health = 1572, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 229.5615f, Y = 105.01001f, Z = 1148.849f,
                Hx = 0f, Hy = 0.9671591f, Hz = 0f, Hw = 0.254171878f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 28, Health = 1504, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 231.170624f, Y = 105.149117f, Z = 1164.899f,
                Hx = -0.0597071834f, Hy = 0.5950132f, Hz = -0.0443935134f, Hw = 0.800264657f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 29, Health = 1572, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 275.635742f, Y = 105.668892f, Z = 1237.73291f,
                Hx = 9.369333E-05f, Hy = 0.00125604728f, Hz = -0.0743870661f, Hw = 0.9972286f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 28, Health = 1504, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 249.087891f, Y = 107.092125f, Z = 1254.61121f,
                Hx = -0.139161348f, Hy = -0.9481668f, Hz = -0.0414846763f, Hw = 0.2826532f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 234.089218f, Y = 108.84771f, Z = 1260.38269f,
                Hx = 0.101679251f, Hy = 0.529175162f, Hz = -0.0232307632f, Hw = 0.842078f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 30, Health = 1640, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 229.89505f, Y = 108.663307f, Z = 1279.98389f,
                Hx = -0.08771903f, Hy = -0.9053291f, Hz = -0.1732638f, Hw = 0.377709121f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 27, Health = 1436, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 222.078064f, Y = 111.595078f, Z = 1276.65491f,
                Hx = 0.07813252f, Hy = 0.532350838f, Hz = -0.12240167f, Hw = 0.833975852f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 28, Health = 1504, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 101.874413f, Y = 104.869125f, Z = 1164.96753f,
                Hx = 0.0517811924f, Hy = -0.715239763f, Hz = -0.05333547f, Hw = 0.6949145f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 120.8664f, Y = 105.01001f, Z = 1599.07788f,
                Hx = 0f, Hy = -0.7849773f, Hz = 0f, Hw = 0.619524539f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 142.251678f, Y = 102.790207f, Z = 1541.67371f,
                Hx = -0.201763928f, Hy = 0.9720652f, Hz = 0.08688847f, Hw = 0.0826496f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 135.5374f, Y = 101.604904f, Z = 1833.72192f,
                Hx = -0.0834213644f, Hy = 0.9846286f, Hz = -0.06260365f, Hw = 0.1401005f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 164.171463f, Y = 103.810005f, Z = 1798.31592f,
                Hx = 0f, Hy = 0.679831445f, Hz = 0f, Hw = 0.7333684f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 207.976944f, Y = 105.01001f, Z = 1819.967f,
                Hx = 0f, Hy = 0.08065062f, Hz = 0f, Hw = 0.9967424f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Weaver of Decay",
                Level = 25, Health = 1300, MonsterData = 209354, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 151.140549f, Y = 103.285446f, Z = 1913.77686f,
                Hx = 0.102345809f, Hy = -0.701702535f, Hz = -0.102998272f, Hw = 0.6975172f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-174130",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Chemicals",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 307.893433f, Y = 60.61f, Z = 1623.98572f,
                Hx = 0f, Hy = 0.855677068f, Hz = 0f, Hw = 0.5175099f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Electronics",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 356.822266f, Y = 60.61f, Z = 1592.275f,
                Hx = 0f, Hy = -0.3009819f, Hz = 0f, Hw = 0.9536301f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Machines",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 315.3345f, Y = 60.61f, Z = 1542.70142f,
                Hx = 0f, Hy = 0.838815033f, Hz = 0f, Hw = 0.5444166f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Manuals",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 318.791229f, Y = 61.21f, Z = 1610.19214f,
                Hx = 0f, Hy = -0.4908789f, Hz = 0f, Hw = 0.871227741f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Pharmaceuticals",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 311.698822f, Y = 60.61f, Z = 1611.97009f,
                Hx = 0f, Hy = 0.6851162f, Hz = 0f, Hw = 0.728433847f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4311,
                Name = "Yuttos Tools",
                Level = 50, Health = 60000, MonsterData = 22802, Scale = 250, VisualFlags = 31, HeadMesh = 0,
                X = 363.168854f, Y = 60.61f, Z = 1603.4696f,
                Hx = 0f, Hy = -0.9971203f, Hz = 0f, Hw = -0.07583129f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-173204",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Aquqa",
                Level = 34, Health = 4780, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1615.27747f, Y = 29.9076271f, Z = 1604.3324f,
                Hx = 0f, Hy = -0.5617423f, Hz = 0f, Hw = 0.8273123f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1808.92676f, Y = 63.0100021f, Z = 694.809f,
                Hx = 0f, Hy = 0.7775507f, Hz = 0f, Hw = 0.62882024f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1880.78589f, Y = 63.0100021f, Z = 708.248352f,
                Hx = 0f, Hy = 0.1904981f, Hz = 0f, Hw = 0.981687546f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1815.8147f, Y = 64.2306f, Z = 723.104858f,
                Hx = 0f, Hy = 0.4570564f, Hz = 0f, Hw = 0.889437735f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1948.58118f, Y = 63.6516533f, Z = 726.8704f,
                Hx = 0f, Hy = -0.460148066f, Hz = 0f, Hw = 0.8878422f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1950.52466f, Y = 63.57301f, Z = 720.496948f,
                Hx = 0f, Hy = -0.797283f, Hz = 0f, Hw = 0.6036057f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1943.0498f, Y = 63.88199f, Z = 720.0157f,
                Hx = 0f, Hy = 0.879756331f, Hz = 0f, Hw = 0.475424856f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1921.83472f, Y = 63.59517f, Z = 663.872864f,
                Hx = 0f, Hy = 0.9836291f, Hz = 0f, Hw = 0.1802049f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1932.66248f, Y = 63.0100021f, Z = 650.475769f,
                Hx = 0f, Hy = -0.944060862f, Hz = 0f, Hw = 0.32977128f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1972.8147f, Y = 64.0052643f, Z = 687.3992f,
                Hx = 0f, Hy = 0.260123432f, Hz = 0f, Hw = 0.9655754f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1972.75879f, Y = 63.8133125f, Z = 690.605957f,
                Hx = 0f, Hy = 0.982592762f, Hz = 0f, Hw = 0.185772672f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1977.3219f, Y = 63.5579453f, Z = 691.2426f,
                Hx = 0f, Hy = -0.962411046f, Hz = 0f, Hw = 0.271597177f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Cur-Beat",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1977.5188f, Y = 63.81511f, Z = 686.498f,
                Hx = 0f, Hy = -0.492944181f, Hz = 0f, Hw = 0.8700609f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Devoted Aban Path-Duna",
                Level = 40, Health = 2320, MonsterData = 214072, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1887.81567f, Y = 68.465004f, Z = 691.554f,
                Hx = 0f, Hy = 0.9559844f, Hz = 0f, Hw = 0.2934174f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Devoted Aban Path-Duna",
                Level = 40, Health = 2320, MonsterData = 214072, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1876.10217f, Y = 63.0100021f, Z = 714.944641f,
                Hx = 0f, Hy = 0.190553486f, Hz = 0f, Hw = 0.9816768f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Diviner Aban Hume-Ocra",
                Level = 40, Health = 2320, MonsterData = 214067, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1892.96216f, Y = 68.465004f, Z = 687.30835f,
                Hx = 0f, Hy = -0.333005f, Hz = 0f, Hw = 0.942925036f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Ecclesiast Aban Fala",
                Level = 40, Health = 9280, MonsterData = 214078, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = AbanFalaCharacterFlags,
                X = 1893.47546f, Y = 68.465004f, Z = 690.309143f,
                Hx = 0f, Hy = 0.5387633f, Hz = 0f, Hw = 0.8424551f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
                CaptureFolder = "20260822-224319",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Hatskiri",
                Level = 40, Health = 5800, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1735.87683f, Y = 28.1034145f, Z = 1660.71057f,
                Hx = 0f, Hy = -0.5728802f, Hz = 0f, Hw = 0.8196391f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Hawilli",
                Level = 30, Health = 4100, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1662.30334f, Y = 28.8100014f, Z = 1586.42542f,
                Hx = 0f, Hy = -0.9795139f, Hz = 0f, Hw = 0.201376736f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1881.03442f, Y = 53.41f, Z = 544.8864f,
                Hx = 0f, Hy = -0.3325839f, Hz = 0f, Hw = 0.9430737f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1823.11365f, Y = 63.0100021f, Z = 514.3576f,
                Hx = 0f, Hy = -0.357361823f, Hz = 0f, Hw = 0.933966041f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1842.95581f, Y = 63.0100021f, Z = 481.443573f,
                Hx = 0f, Hy = 0.999994457f, Hz = 0f, Hw = -0.00333432131f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1844.09521f, Y = 53.57271f, Z = 534.943542f,
                Hx = 0f, Hy = -0.000632437f, Hz = 0f, Hw = 0.9999998f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1915.39636f, Y = 63.0100021f, Z = 501.9282f,
                Hx = 0f, Hy = -0.561651766f, Hz = 0f, Hw = 0.827373743f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1801.859f, Y = 63.0100021f, Z = 583.2184f,
                Hx = 0f, Hy = -0.901808739f, Hz = 0f, Hw = 0.432135344f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1817.87292f, Y = 64.73766f, Z = 594.395264f,
                Hx = 0f, Hy = -0.7437649f, Hz = 0f, Hw = 0.668441355f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1849.45386f, Y = 66.245f, Z = 714.0737f,
                Hx = 0f, Hy = 0.7538294f, Hz = 0f, Hw = 0.65707016f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1944.51013f, Y = 64.00603f, Z = 669.5327f,
                Hx = 0f, Hy = 0.315152526f, Hz = 0f, Hw = 0.949041f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1929.85107f, Y = 63.0100021f, Z = 616.3519f,
                Hx = 0f, Hy = 0.9999966f, Hz = 0f, Hw = -0.00260810158f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1920.11719f, Y = 64.73901f, Z = 623.4985f,
                Hx = 0f, Hy = -0.558736145f, Hz = 0f, Hw = 0.829345465f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Len-Dosa",
                Level = 35, Health = 1980, MonsterData = 214072, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1943.689f, Y = 63.0100021f, Z = 610.5231f,
                Hx = 0f, Hy = -0.9884773f, Hz = 0f, Hw = 0.151369318f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Aerarium",
                Level = 27, Health = 3590, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1622.59192f, Y = 30.2693329f, Z = 1674.29944f,
                Hx = 0f, Hy = -0.549320638f, Hz = 0f, Hw = 0.835611641f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Alter",
                Level = 29, Health = 3930, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1508.52808f, Y = 30.61f, Z = 1609.14014f,
                Hx = 0f, Hy = -0.910132945f, Hz = 0f, Hw = 0.414316326f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Ana",
                Level = 32, Health = 1776, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1851.47522f, Y = 32.7259026f, Z = 799.9927f,
                Hx = 0f, Hy = -0.8572312f, Hz = 0f, Hw = 0.514931738f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Animi",
                Level = 32, Health = 4440, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1582.04163f, Y = 30.3073273f, Z = 1526.04614f,
                Hx = 0f, Hy = -0.5586952f, Hz = 0f, Hw = 0.829373062f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Animos",
                Level = 26, Health = 3420, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1557.8905f, Y = 29.4054966f, Z = 1755.94165f,
                Hx = 0f, Hy = -0.5848346f, Hz = 0f, Hw = 0.8111526f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Annos",
                Level = 40, Health = 5800, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1517.86169f, Y = 28.8100014f, Z = 1556.35132f,
                Hx = 0f, Hy = -0.627672434f, Hz = 0f, Hw = 0.77847755f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Malah-Audisse",
                Level = 33, Health = 4610, MonsterData = 209229, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1721.84375f, Y = 28.210001f, Z = 1621.2749f,
                Hx = 0f, Hy = 0.08220843f, Hz = 0f, Hw = 0.9966152f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Narunkt",
                Level = 32, Health = 4440, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1527.93457f, Y = 28.8100014f, Z = 1579.652f,
                Hx = 0f, Hy = -0.5530242f, Hz = 0f, Hw = 0.8331652f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1893.25647f, Y = 53.13753f, Z = 526.1553f,
                Hx = 0f, Hy = -0.8388555f, Hz = 0f, Hw = 0.544354141f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1856.4502f, Y = 63.6696854f, Z = 576.3696f,
                Hx = 0f, Hy = 0.447991967f, Hz = 0f, Hw = 0.8940376f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1856.62109f, Y = 63.0100021f, Z = 502.294861f,
                Hx = 0f, Hy = -0.205266729f, Hz = 0f, Hw = 0.978706062f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1870.89917f, Y = 62.9135742f, Z = 497.8003f,
                Hx = 0f, Hy = -0.196342438f, Hz = 0f, Hw = 0.9805354f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1811.40881f, Y = 63.52733f, Z = 606.970642f,
                Hx = 0f, Hy = 0.999457955f, Hz = 0f, Hw = 0.03292135f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1814.12988f, Y = 64.12047f, Z = 623.3748f,
                Hx = 0f, Hy = 0.955121934f, Hz = 0f, Hw = 0.296212941f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1838.49316f, Y = 72.84028f, Z = 622.764465f,
                Hx = 0f, Hy = 0.992435932f, Hz = 0f, Hw = 0.122763574f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1871.22852f, Y = 76.46456f, Z = 577.5111f,
                Hx = 0f, Hy = -0.9811319f, Hz = 0f, Hw = 0.193339676f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1829.87878f, Y = 66.245f, Z = 670.503f,
                Hx = 0f, Hy = -0.00160518417f, Hz = 0f, Hw = 0.9999987f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1824.47766f, Y = 66.245f, Z = 731.942932f,
                Hx = 0f, Hy = -0.0005263408f, Hz = 0f, Hw = 0.9999999f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1845.12231f, Y = 66.245f, Z = 739.9734f,
                Hx = 0f, Hy = 0.00280030561f, Hz = 0f, Hw = 0.999996066f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Farat",
                Level = 45, Health = 6650, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1865.17627f, Y = 63.50117f, Z = 746.0319f,
                Hx = 0f, Hy = 0.00180465973f, Hz = 0f, Hw = 0.9999984f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1871.22058f, Y = 63.36484f, Z = 572.7507f,
                Hx = 0f, Hy = -0.6090196f, Hz = 0f, Hw = 0.7931552f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1816.34546f, Y = 64.21001f, Z = 620.963135f,
                Hx = 0f, Hy = -0.6134425f, Hz = 0f, Hw = 0.7897394f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1868.06445f, Y = 73.01191f, Z = 671.8095f,
                Hx = 0f, Hy = 0.00327140884f, Hz = 0f, Hw = 0.999994636f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1825.8208f, Y = 72.93815f, Z = 650.057556f,
                Hx = 0f, Hy = 0.00313300081f, Hz = 0f, Hw = 0.9999951f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1829.19934f, Y = 72.80961f, Z = 610.4475f,
                Hx = 0f, Hy = -0.004119297f, Hz = 0f, Hw = 0.999991536f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1843.72327f, Y = 73.0100555f, Z = 592.693054f,
                Hx = 0f, Hy = 0.00214326824f, Hz = 0f, Hw = 0.9999977f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Jerad",
                Level = 50, Health = 7500, MonsterData = 214067, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1871.80554f, Y = 76.5148239f, Z = 583.5028f,
                Hx = 0f, Hy = -0.001701982f, Hz = 0f, Hw = 0.999998569f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Mada of Flaming Barrels",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1848.92188f, Y = 72.78218f, Z = 623.833f,
                Hx = 0f, Hy = 0.9271203f, Hz = 0f, Hw = 0.374763966f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Mada of Gear & Ammo",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1854.03589f, Y = 72.765f, Z = 612.997253f,
                Hx = 0f, Hy = 0.423952341f, Hz = 0f, Hw = 0.905684531f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Mada of Preservation",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1868.82654f, Y = 72.765f, Z = 611.8269f,
                Hx = 0f, Hy = -0.489193231f, Hz = 0f, Hw = 0.8721754f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Mada of Protection",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1868.34375f, Y = 72.765f, Z = 642.5172f,
                Hx = 0f, Hy = -0.8332082f, Hz = 0f, Hw = 0.5529596f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Or-Mada of the Furious Fists",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1859.41785f, Y = 72.765f, Z = 643.626f,
                Hx = 0f, Hy = 0.993497849f, Hz = 0f, Hw = 0.113851167f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Qallyawi",
                Level = 32, Health = 4440, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1688.69263f, Y = 29.274065f, Z = 1512.48145f,
                Hx = 0f, Hy = 0.1034705f, Hz = 0f, Hw = 0.994632542f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Saliwata",
                Level = 25, Health = 3250, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1492.58118f, Y = 28.8100014f, Z = 1710.52771f,
                Hx = 0f, Hy = -0.8762671f, Hz = 0f, Hw = 0.4818257f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Sashuqa",
                Level = 30, Health = 4100, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1445.36438f, Y = 29.6161613f, Z = 1467.77136f,
                Hx = 0f, Hy = -0.550920963f, Hz = 0f, Hw = 0.8345574f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Seeker Aban Kald-Nuir",
                Level = 40, Health = 2320, MonsterData = 214067, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1861.74011f, Y = 66.245f, Z = 695.4687f,
                Hx = 0f, Hy = 0.451292872f, Hz = 0f, Hw = 0.892375767f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Sipius Aban Lux-Nuir",
                Level = 40, Health = 2320, MonsterData = 214067, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1827.9386f, Y = 66.245f, Z = 700.105164f,
                Hx = 0f, Hy = 0.9774589f, Hz = 0f, Hw = 0.21112591f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Sipius Aban Ulma-Kald",
                Level = 40, Health = 2320, MonsterData = 214067, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1828.76587f, Y = 66.245f, Z = 693.3787f,
                Hx = 0f, Hy = 0.126132771f, Hz = 0f, Hw = 0.9920134f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1708.52161f, Y = 28.89456f, Z = 1574.97131f,
                Hx = 0f, Hy = 0.9785185f, Hz = 0f, Hw = 0.20615904f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1683.69373f, Y = 28.8100014f, Z = 1591.65015f,
                Hx = 0f, Hy = -0.234745115f, Hz = 0f, Hw = 0.972057f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1693.61047f, Y = 28.8345566f, Z = 1574.51318f,
                Hx = 0f, Hy = -0.456615f, Hz = 0f, Hw = 0.8896644f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1719.951f, Y = 29.0829029f, Z = 1566.209f,
                Hx = 0f, Hy = -0.7113991f, Hz = 0f, Hw = 0.7027883f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1713.748f, Y = 27.947794f, Z = 1676.106f,
                Hx = 0f, Hy = 0.939187f, Hz = 0f, Hw = 0.34340623f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1722.6731f, Y = 28.210001f, Z = 1659.69592f,
                Hx = 0f, Hy = -0.07602132f, Hz = 0f, Hw = 0.9971062f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1704.112f, Y = 28.210001f, Z = 1646.992f,
                Hx = 0f, Hy = -0.941197753f, Hz = 0f, Hw = 0.337856084f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1723.13367f, Y = 28.210001f, Z = 1616.81921f,
                Hx = 0f, Hy = -0.842083335f, Hz = 0f, Hw = 0.53934747f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1711.46619f, Y = 28.210001f, Z = 1612.542f,
                Hx = 0f, Hy = 0.962509632f, Hz = 0f, Hw = 0.271247476f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1708.93384f, Y = 28.210001f, Z = 1593.68677f,
                Hx = 0f, Hy = 0.2617935f, Hz = 0f, Hw = 0.9651239f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1687.61523f, Y = 29.52479f, Z = 1636.08923f,
                Hx = 0f, Hy = 0.0374237f, Hz = 0f, Hw = 0.999299467f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1706.19055f, Y = 28.210001f, Z = 1641.78186f,
                Hx = 0f, Hy = -0.395133972f, Hz = 0f, Hw = 0.9186235f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1727.18481f, Y = 28.210001f, Z = 1642.55823f,
                Hx = 0f, Hy = 0.433136463f, Hz = 0f, Hw = 0.9013284f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1719.958f, Y = 28.210001f, Z = 1639.91833f,
                Hx = 0f, Hy = -0.84381485f, Hz = 0f, Hw = 0.536634445f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1730.33521f, Y = 28.210001f, Z = 1639.26855f,
                Hx = 0f, Hy = -0.9908096f, Hz = 0f, Hw = 0.1352637f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1742.005f, Y = 28.210001f, Z = 1614.66028f,
                Hx = 0f, Hy = 0.229983777f, Hz = 0f, Hw = 0.9731945f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1750.751f, Y = 28.210001f, Z = 1610.50085f,
                Hx = 0f, Hy = 0.5672951f, Hz = 0f, Hw = 0.8235146f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1750.05188f, Y = 28.210001f, Z = 1594.65894f,
                Hx = 0f, Hy = -0.7256929f, Hz = 0f, Hw = 0.688018739f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1755.83167f, Y = 30.1313286f, Z = 1579.55933f,
                Hx = 0f, Hy = -0.9747715f, Hz = 0f, Hw = 0.223205164f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1742.997f, Y = 28.659544f, Z = 1567.2948f,
                Hx = 0f, Hy = 0.6211085f, Hz = 0f, Hw = 0.7837246f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1697.0293f, Y = 28.210001f, Z = 1663.956f,
                Hx = 0f, Hy = -0.934436738f, Hz = 0f, Hw = 0.356129169f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1713.41882f, Y = 28.210001f, Z = 1632.65759f,
                Hx = 0f, Hy = 0.246745467f, Hz = 0f, Hw = 0.9690803f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1737.37671f, Y = 28.210001f, Z = 1616.00134f,
                Hx = 0f, Hy = 0.365916938f, Hz = 0f, Hw = 0.9306475f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1594.01721f, Y = 30.2832489f, Z = 1650.20667f,
                Hx = 0f, Hy = -0.9643794f, Hz = 0f, Hw = 0.264523f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1613.333f, Y = 28.00159f, Z = 1673.35181f,
                Hx = 0f, Hy = 0.397458583f, Hz = 0f, Hw = 0.9176201f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1603.13354f, Y = 30.7894077f, Z = 1621.91882f,
                Hx = 0f, Hy = -0.383872539f, Hz = 0f, Hw = 0.9233861f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1585.12793f, Y = 33.7479324f, Z = 1629.28967f,
                Hx = 0f, Hy = 0.260144383f, Hz = 0f, Hw = 0.965569735f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1517.91956f, Y = 28.8100014f, Z = 1581.59192f,
                Hx = 0f, Hy = 0.8905338f, Hz = 0f, Hw = 0.454917073f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1531.49084f, Y = 28.8100014f, Z = 1593.02917f,
                Hx = 0f, Hy = -0.8676068f, Hz = 0f, Hw = 0.497250855f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1540.95007f, Y = 28.8100014f, Z = 1565.4679f,
                Hx = 0f, Hy = -0.4953303f, Hz = 0f, Hw = 0.868704736f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1564.21021f, Y = 28.778471f, Z = 1590.942f,
                Hx = 0f, Hy = -0.9294684f, Hz = 0f, Hw = 0.368901759f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1573.71655f, Y = 28.4907932f, Z = 1588.1554f,
                Hx = 0f, Hy = -0.8631721f, Hz = 0f, Hw = 0.504909754f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1566.34912f, Y = 28.56237f, Z = 1575.21277f,
                Hx = 0f, Hy = 0.457541555f, Hz = 0f, Hw = 0.88918823f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1557.304f, Y = 28.8100014f, Z = 1581.58679f,
                Hx = 0f, Hy = 0.04965618f, Hz = 0f, Hw = 0.998766363f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1534.8175f, Y = 28.8100014f, Z = 1562.42114f,
                Hx = 0f, Hy = 0.991306245f, Hz = 0f, Hw = 0.131575018f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1570.06421f, Y = 29.4619312f, Z = 1543.121f,
                Hx = 0f, Hy = -0.691574633f, Hz = 0f, Hw = 0.722305f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1616.7467f, Y = 30.9656277f, Z = 1600.8429f,
                Hx = 0f, Hy = -0.8630332f, Hz = 0f, Hw = 0.5051473f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1613.34949f, Y = 30.5346f, Z = 1610.0304f,
                Hx = 0f, Hy = -0.594774f, Hz = 0f, Hw = 0.80389297f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1627.10925f, Y = 30.61f, Z = 1603.72778f,
                Hx = 0f, Hy = -0.6372239f, Hz = 0f, Hw = 0.7706787f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1631.36829f, Y = 31.1081944f, Z = 1596.707f,
                Hx = 0f, Hy = -0.412112117f, Hz = 0f, Hw = 0.91113317f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1642.7207f, Y = 31.30205f, Z = 1601.94543f,
                Hx = 0f, Hy = -0.385974735f, Hz = 0f, Hw = 0.9225094f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1645.67139f, Y = 30.0100021f, Z = 1593.20471f,
                Hx = 0f, Hy = -0.805364132f, Hz = 0f, Hw = 0.5927804f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1656.5022f, Y = 29.5529518f, Z = 1597.87244f,
                Hx = 0f, Hy = 0.9927002f, Hz = 0f, Hw = 0.12060795f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1659.79346f, Y = 29.25211f, Z = 1590.91907f,
                Hx = 0f, Hy = -0.227796167f, Hz = 0f, Hw = 0.973708868f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1676.41516f, Y = 28.8100014f, Z = 1594.33875f,
                Hx = 0f, Hy = -0.0445072539f, Hz = 0f, Hw = 0.9990091f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1716.71167f, Y = 29.3179359f, Z = 1558.63452f,
                Hx = 0f, Hy = -0.426037163f, Hz = 0f, Hw = 0.904705644f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1734.38293f, Y = 28.909996f, Z = 1553.75635f,
                Hx = 0f, Hy = 0.5666625f, Hz = 0f, Hw = 0.82395f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1727.71448f, Y = 29.3363571f, Z = 1531.86877f,
                Hx = 0f, Hy = 0.1896125f, Hz = 0f, Hw = 0.981859f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1693.51575f, Y = 29.0264683f, Z = 1708.69324f,
                Hx = 0f, Hy = -0.2953506f, Hz = 0f, Hw = 0.955388963f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1643.17529f, Y = 29.3365383f, Z = 1678.9375f,
                Hx = 0f, Hy = 0.5821851f, Hz = 0f, Hw = 0.8130563f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1445.6311f, Y = 28.210001f, Z = 1486.86182f,
                Hx = 0f, Hy = 0.998087764f, Hz = 0f, Hw = 0.0618129671f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1468.25488f, Y = 31.9979382f, Z = 1461.94482f,
                Hx = 0f, Hy = 0.997998f, Hz = 0f, Hw = 0.06324531f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1440.61194f, Y = 28.7243881f, Z = 1471.20081f,
                Hx = 0f, Hy = -0.251881868f, Hz = 0f, Hw = 0.967758f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1457.89868f, Y = 31.1538429f, Z = 1442.30176f,
                Hx = 0f, Hy = 0.68857944f, Hz = 0f, Hw = 0.725160956f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1433.9231f, Y = 28.4255066f, Z = 1496.2085f,
                Hx = 0f, Hy = -0.6334252f, Hz = 0f, Hw = 0.773803949f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1474.11682f, Y = 32.6823235f, Z = 1446.08447f,
                Hx = 0f, Hy = 0.843202055f, Hz = 0f, Hw = 0.537596762f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1504.75854f, Y = 28.8100014f, Z = 1582.63721f,
                Hx = 0f, Hy = -0.9995158f, Hz = 0f, Hw = 0.03111535f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1539.91711f, Y = 28.8100014f, Z = 1577.69958f,
                Hx = 0f, Hy = 0.661077738f, Hz = 0f, Hw = 0.750317454f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1521.70056f, Y = 28.8100014f, Z = 1560.9635f,
                Hx = 0f, Hy = -0.999388635f, Hz = 0f, Hw = 0.03496204f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1502.97571f, Y = 28.9603863f, Z = 1618.01318f,
                Hx = 0f, Hy = 0.999927f, Hz = 0f, Hw = 0.0120818587f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1468.61121f, Y = 28.8100014f, Z = 1739.47815f,
                Hx = 0f, Hy = -0.9408104f, Hz = 0f, Hw = 0.338933349f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1459.0354f, Y = 28.2395f, Z = 1732.19666f,
                Hx = 0f, Hy = -0.0321900137f, Hz = 0f, Hw = 0.999481738f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1442.11133f, Y = 28.210001f, Z = 1716.31482f,
                Hx = 0f, Hy = 0.5675448f, Hz = 0f, Hw = 0.8233425f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1420.234f, Y = 28.7192421f, Z = 1716.4436f,
                Hx = 0f, Hy = -0.934793532f, Hz = 0f, Hw = 0.355191648f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1442.4823f, Y = 28.2f, Z = 1686.443f,
                Hx = 0f, Hy = 0.94622606f, Hz = 0f, Hw = 0.323506117f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1442.27393f, Y = 28.210001f, Z = 1663.6156f,
                Hx = 0f, Hy = -0.9472583f, Hz = 0f, Hw = 0.3204711f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1423.10193f, Y = 28.348959f, Z = 1664.04846f,
                Hx = 0f, Hy = 0.871134639f, Hz = 0f, Hw = 0.491044283f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1431.12756f, Y = 28.8100014f, Z = 1630.50684f,
                Hx = 0f, Hy = -0.377988219f, Hz = 0f, Hw = 0.9258104f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1427.61511f, Y = 28.2834454f, Z = 1643.51038f,
                Hx = 0f, Hy = 0.6664416f, Hz = 0f, Hw = 0.745557249f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1447.72937f, Y = 28.210001f, Z = 1673.34985f,
                Hx = 0f, Hy = -0.127207085f, Hz = 0f, Hw = 0.9918762f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1461.48792f, Y = 28.210001f, Z = 1679.28809f,
                Hx = 0f, Hy = 0.8827439f, Hz = 0f, Hw = 0.4698545f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1460.354f, Y = 28.210001f, Z = 1683.34155f,
                Hx = 0f, Hy = 0.9676986f, Hz = 0f, Hw = 0.252110153f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1483.88892f, Y = 28.8100014f, Z = 1719.96191f,
                Hx = 0f, Hy = 0.9689951f, Hz = 0f, Hw = 0.247080073f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1471.41663f, Y = 28.21846f, Z = 1719.9436f,
                Hx = 0f, Hy = 0.9991498f, Hz = 0f, Hw = 0.041227337f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1477.509f, Y = 28.210001f, Z = 1702.55322f,
                Hx = 0f, Hy = 0.6024785f, Hz = 0f, Hw = 0.7981351f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1495.10742f, Y = 28.210001f, Z = 1738.88428f,
                Hx = 0f, Hy = 0.9383079f, Hz = 0f, Hw = 0.345800936f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1507.43408f, Y = 28.712534f, Z = 1709.27234f,
                Hx = 0f, Hy = 0.04380957f, Hz = 0f, Hw = 0.9990399f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1507.25684f, Y = 28.210001f, Z = 1724.92944f,
                Hx = 0f, Hy = -0.321858883f, Hz = 0f, Hw = 0.946787655f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1503.04944f, Y = 28.673624f, Z = 1747.0625f,
                Hx = 0f, Hy = -0.191870749f, Hz = 0f, Hw = 0.9814202f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1478.51807f, Y = 28.210001f, Z = 1682.5271f,
                Hx = 0f, Hy = -0.02778367f, Hz = 0f, Hw = 0.999613941f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1515.35217f, Y = 28.9114227f, Z = 1749.64136f,
                Hx = 0f, Hy = -0.9127346f, Hz = 0f, Hw = 0.408552885f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1519.17249f, Y = 28.210001f, Z = 1717.50562f,
                Hx = 0f, Hy = -0.109566092f, Hz = 0f, Hw = 0.9939795f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1518.94482f, Y = 28.210001f, Z = 1682.39258f,
                Hx = 0f, Hy = -0.551291049f, Hz = 0f, Hw = 0.834313f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1519.76868f, Y = 28.210001f, Z = 1692.41089f,
                Hx = 0f, Hy = -0.4433844f, Hz = 0f, Hw = 0.896331549f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1500.82275f, Y = 28.210001f, Z = 1676.61377f,
                Hx = 0f, Hy = 0.945498049f, Hz = 0f, Hw = 0.325627685f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1489.66736f, Y = 28.210001f, Z = 1678.40527f,
                Hx = 0f, Hy = -0.6706171f, Hz = 0f, Hw = 0.7418037f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1479.98755f, Y = 28.210001f, Z = 1677.993f,
                Hx = 0f, Hy = -0.9798901f, Hz = 0f, Hw = 0.199537858f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1486.108f, Y = 27.6993f, Z = 1654.75635f,
                Hx = 0f, Hy = -0.442347229f, Hz = 0f, Hw = 0.896843851f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1471.50659f, Y = 28.210001f, Z = 1639.9718f,
                Hx = 0f, Hy = -0.9811989f, Hz = 0f, Hw = 0.192999288f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1455.39246f, Y = 28.210001f, Z = 1643.13489f,
                Hx = 0f, Hy = -0.8747f, Hz = 0f, Hw = 0.484664768f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1557.62109f, Y = 28.210001f, Z = 1694.34668f,
                Hx = 0f, Hy = 0.9580467f, Hz = 0f, Hw = 0.286612153f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1546.50146f, Y = 28.4785252f, Z = 1683.24866f,
                Hx = 0f, Hy = -0.4891941f, Hz = 0f, Hw = 0.8721749f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1581.4231f, Y = 30.8560219f, Z = 1663.42017f,
                Hx = 0f, Hy = 0.9902457f, Hz = 0f, Hw = 0.139332309f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1572.65442f, Y = 27.6702423f, Z = 1680.98132f,
                Hx = 0f, Hy = -0.24818413f, Hz = 0f, Hw = 0.968712866f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1592.35291f, Y = 33.03783f, Z = 1695.65344f,
                Hx = 0f, Hy = 0.9838772f, Hz = 0f, Hw = 0.17884554f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1571.96521f, Y = 28.70662f, Z = 1707.28247f,
                Hx = 0f, Hy = -0.6297358f, Hz = 0f, Hw = 0.776809335f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1555.67712f, Y = 28.210001f, Z = 1713.03687f,
                Hx = 0f, Hy = -0.9477127f, Hz = 0f, Hw = 0.319124728f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1554.76013f, Y = 28.210001f, Z = 1739.89966f,
                Hx = 0f, Hy = -0.743860364f, Hz = 0f, Hw = 0.6683351f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 22, Health = 1090, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1549.18811f, Y = 28.210001f, Z = 1729.31653f,
                Hx = 0f, Hy = -0.996976137f, Hz = 0f, Hw = 0.0777085945f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1553.69482f, Y = 28.210001f, Z = 1719.95593f,
                Hx = 0f, Hy = 0.989174664f, Hz = 0f, Hw = 0.1467429f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1559.79187f, Y = 29.4100018f, Z = 1757.51257f,
                Hx = 0f, Hy = -0.247277528f, Hz = 0f, Hw = 0.968944669f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1553.51831f, Y = 28.8100014f, Z = 1761.59229f,
                Hx = 0f, Hy = -0.820094466f, Hz = 0f, Hw = 0.5722282f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 21, Health = 1020, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1536.89441f, Y = 28.9501686f, Z = 1750.66235f,
                Hx = 0f, Hy = -0.7833266f, Hz = 0f, Hw = 0.621610343f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1616.21472f, Y = 30.20043f, Z = 1519.28906f,
                Hx = 0f, Hy = -0.156941891f, Hz = 0f, Hw = 0.987607837f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1591.67334f, Y = 42.9641151f, Z = 1527.346f,
                Hx = 0f, Hy = -0.9025478f, Hz = 0f, Hw = 0.430589676f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1584.84375f, Y = 33.55199f, Z = 1517.90332f,
                Hx = 0f, Hy = 0.7724183f, Hz = 0f, Hw = 0.635114133f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1485.75488f, Y = 34.0417061f, Z = 1449.63318f,
                Hx = 0f, Hy = -0.362463921f, Hz = 0f, Hw = 0.9319978f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1497.30225f, Y = 34.8768044f, Z = 1435.01465f,
                Hx = 0f, Hy = 0.6178222f, Hz = 0f, Hw = 0.7863178f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1483.901f, Y = 33.81291f, Z = 1445.42322f,
                Hx = 0f, Hy = 0.9275911f, Hz = 0f, Hw = 0.373597056f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1452.75684f, Y = 30.5978165f, Z = 1449.45728f,
                Hx = 0f, Hy = 0.9959817f, Hz = 0f, Hw = 0.08955713f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1442.52283f, Y = 28.210001f, Z = 1510.37341f,
                Hx = 0f, Hy = 0.995754242f, Hz = 0f, Hw = 0.09205166f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1423.42114f, Y = 28.43156f, Z = 1512.33948f,
                Hx = 0f, Hy = -0.12250866f, Hz = 0f, Hw = 0.992467463f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1417.47241f, Y = 28.8714123f, Z = 1505.74561f,
                Hx = 0f, Hy = -0.148814335f, Hz = 0f, Hw = 0.988865137f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1403.87341f, Y = 28.1309338f, Z = 1499.62061f,
                Hx = 0f, Hy = -0.5599692f, Hz = 0f, Hw = 0.828513443f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1419.16687f, Y = 28.4353123f, Z = 1486.10364f,
                Hx = 0f, Hy = 0.02363006f, Hz = 0f, Hw = 0.999720752f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 20, Health = 950, MonsterData = 226557, Scale = 99, VisualFlags = 31, HeadMesh = 0,
                X = 1458.60413f, Y = 28.210001f, Z = 1753.593f,
                Hx = 0f, Hy = -0.6732151f, Hz = 0f, Hw = 0.7394467f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1526.77161f, Y = 28.8100014f, Z = 1565.98022f,
                Hx = 0f, Hy = -0.27892223f, Hz = 0f, Hw = 0.9603137f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1546.91284f, Y = 28.8100014f, Z = 1585.32263f,
                Hx = 0f, Hy = -0.309607655f, Hz = 0f, Hw = 0.9508644f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1544.94885f, Y = 28.8100014f, Z = 1567.711f,
                Hx = 0f, Hy = -0.04488024f, Hz = 0f, Hw = 0.9989924f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1515.10315f, Y = 28.6754742f, Z = 1561.84607f,
                Hx = 0f, Hy = 0.8867373f, Hz = 0f, Hw = 0.462273657f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 24, Health = 1230, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1594.752f, Y = 30.3004246f, Z = 1524.10364f,
                Hx = 0f, Hy = -0.9502306f, Hz = 0f, Hw = 0.3115474f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1599.16821f, Y = 29.0849533f, Z = 1515.54187f,
                Hx = 0f, Hy = 0.277368337f, Hz = 0f, Hw = 0.960763633f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 23, Health = 1160, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1565.68481f, Y = 32.23195f, Z = 1523.64282f,
                Hx = 0f, Hy = 0.892043054f, Hz = 0f, Hw = 0.451950461f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1568.02563f, Y = 31.2589417f, Z = 1528.46985f,
                Hx = 0f, Hy = -0.993232548f, Hz = 0f, Hw = 0.1161424f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1577.35645f, Y = 49.1169968f, Z = 1546.042f,
                Hx = 0f, Hy = -0.9119672f, Hz = 0f, Hw = 0.4102631f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Spinetooth Hatchling",
                Level = 25, Health = 1300, MonsterData = 226557, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 1625.571f, Y = 33.76421f, Z = 1503.00024f,
                Hx = 0f, Hy = -0.483403236f, Hz = 0f, Hw = 0.8753978f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Watcher Aban Wei-Nuir",
                Level = 40, Health = 2320, MonsterData = 214067, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                X = 1836.23865f, Y = 66.245f, Z = 697.9356f,
                Hx = 0f, Hy = -0.7712152f, Hz = 0f, Hw = 0.6365745f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260718-180726",
            },
            new LifeNpc
            {
                PlayfieldId = 4312,
                Name = "Waychaw",
                Level = 29, Health = 3930, MonsterData = 226557, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 1593.687f, Y = 31.86118f, Z = 1636.31287f,
                Hx = 0f, Hy = 0.046652168f, Hz = 0f, Hw = 0.9989112f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-180726",
            },
            // Capture 20260723-221330 Goldman Harbor PF 4531 (0x11B3) SCFU + enemy-dossier.
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Sharon Goldman",
                Level = 50, Health = 45320, MonsterData = 215029, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40664,
                X = 177.375259f, Y = 282.006378f, Z = 359.798065f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 0 }, new[] { 1, 46219 }, new[] { 2, 45792 }, new[] { 3, 46220 }, new[] { 4, 46221 }
                },
                Meshes = new[] { new[] { 0, 40664, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Doctor James Monaghan",
                Level = 40, Health = 32984, MonsterData = 215027, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40132,
                X = 199.06012f, Y = 280.005f, Z = 343.575562f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 117653 }, new[] { 1, 55995 }, new[] { 2, 40903 }, new[] { 3, 55994 }, new[] { 4, 56001 }
                },
                Meshes = new[] { new[] { 0, 20007, 55996, 2 }, new[] { 0, 40132, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Winnie Glowtail",
                Level = 5, Health = 2296, MonsterData = 215023, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40665,
                X = 198.689789f, Y = 280.005f, Z = 355.650116f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 40972 }, new[] { 1, 40942 }, new[] { 2, 40962 }, new[] { 3, 40921 }, new[] { 4, 40982 }
                },
                Meshes = new[] { new[] { 0, 40665, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Pedro Gavrillo",
                Level = 20, Health = 11167, MonsterData = 215028, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40715,
                X = 174.95314f, Y = 282.0062f, Z = 354.525635f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 81914 }, new[] { 3, 81909 }, new[] { 4, 81917 }
                },
                Meshes = new[] { new[] { 0, 40715, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Trond McDougal",
                Level = 45, Health = 39152, MonsterData = 215025, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40283,
                X = 182.3278f, Y = 280.006226f, Z = 353.1129f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 0 }, new[] { 1, 42249 }, new[] { 2, 42260 }, new[] { 3, 42247 }, new[] { 4, 42248 }
                },
                Meshes = new[] { new[] { 0, 40283, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Luna Erke",
                Level = 31, Health = 1095, MonsterData = 26143, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 40137,
                X = 196.280258f, Y = 280.005f, Z = 355.0273f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[]
                {
                    new[] { 0, 0 }, new[] { 1, 40900 }, new[] { 2, 42235 }, new[] { 3, 40894 }, new[] { 4, 40909 }
                },
                Meshes = new[] { new[] { 0, 40137, 0, 4 } },
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                // Capture 20260723-221330 dossier only (no SCFU headMesh); keep HeadMesh=0.
                PlayfieldId = NascenceLifeContentModule.GoldmanAretePlayfieldId,
                Name = "Prince Creehan",
                Level = 19, Health = 526, MonsterData = 26103, Scale = 100, VisualFlags = 31,
                CharacterFlags = 268964353, HeadMesh = 0,
                X = 179.270325f, Y = 285.005f, Z = 311.859344f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260723-221330",
            },
            new LifeNpc
            {
                // Capture 20260822-224319 SCFU SimpleChar:7A2013BC Garden of Aban.
                PlayfieldId = NascenceLifeContentModule.GardenOfAbanPlayfieldId,
                Name = "Sipius Aban Lux-Wei",
                Level = 40,
                Health = 2320,
                MonsterData = 214067,
                Scale = 140,
                VisualFlags = 31,
                HeadMesh = 0,
                X = 468.5054f,
                Y = 116.985f,
                Z = 495.266968f,
                Hx = 0f,
                Hy = -0.944331765f,
                Hz = 0f,
                Hw = 0.328994632f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234635, 0, 2 } },
                CaptureFolder = "20260822-224319",
            },
        };

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId
                && pf != NascenceLifeContentModule.JobeResearchPlayfieldId
                && pf != NascenceLifeContentModule.GoldmanAretePlayfieldId
                && pf != NascenceLifeContentModule.GardenOfAbanPlayfieldId)
            {
                return;
            }

            int spawned = 0;
            if (pf == NascenceLifeContentModule.FrontierPlayfieldId)
            {
                lock (FrontierForkDeferredSync)
                {
                    FrontierForkDeferredNpcIndices.Clear();
                    FrontierForkDeferredSpawnedKeys.Clear();
                    FrontierForkLoginReadyAtUtc.Clear();
                    FrontierForkDeferredLastBatchAtUtc = DateTime.MinValue;
                }
            }

            for (int i = 0; i < Npcs.Length; i++)
            {
                LifeNpc def = Npcs[i];
                if (def.PlayfieldId != pf)
                {
                    continue;
                }

                if (ShouldDeferFrontierForkSpawn(def))
                {
                    lock (FrontierForkDeferredSync)
                    {
                        FrontierForkDeferredNpcIndices.Add(i);
                    }

                    continue;
                }

                if (ShouldSkipFrontierForkCrashSpawn(def))
                {
                    continue;
                }

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
                        "NascenceLifeSpawn SpawnOne threw npc=" + def.Name
                        + " ex=" + ex.GetType().Name + ": " + ex.Message
                        + " stack=" + ex.StackTrace);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceLifeSpawn pf=" + pf + " spawned=" + spawned + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            LifeNpc def)
        {
            var npcController = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceLifeSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            // Capture 20260723-221330 CharacterFlags: Dreaming=277352961; animals=268964353;
            // Drake/Falker SCFU CharacterFlags=277352961 when set on LifeNpc.
            int characterFlags;
            if (string.Equals(def.Name, "Dreaming Silvertail", StringComparison.Ordinal))
            {
                characterFlags = DreamingSilvertailCharacterFlags;
            }
            else if (def.CharacterFlags != 0)
            {
                characterFlags = def.CharacterFlags;
            }
            else
            {
                characterFlags = DefaultAnimalCharacterFlags;
            }

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)characterFlags);
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    if (t == null || t.Length < 2 || t[1] <= 0)
                    {
                        continue;
                    }

                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            if (def.Meshes != null && def.Meshes.Length > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    if (m == null || m.Length < 4 || m[1] <= 0)
                    {
                        continue;
                    }

                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }
            }

            // HeadMesh is OverridingModifierStat: BaseValue-only leaves template modifier and SCFU emits wrong/0 head.
            // Match OrdinaryEnemyRuntimeService.SetHeadMesh (Value+BaseValue + layer-4 mesh).
            ApplyCaptureHeadMesh(mob, def.HeadMesh);

            if (IsPapagenaName(def.Name))
            {
                // Capture 20260823-112044 SCFU Side=Clan (Data Disk quest clan target).
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)PapagenaNpcFamily);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Clan);
                mob.Stats[StatIds.side].Value = (int)Side.Clan;
            }
            else if (IsRedeemedVillageClanNpcName(def.Name))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)AbanFalaNpcFamily);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Clan);
                mob.Stats[StatIds.side].Value = (int)Side.Clan;
            }
            else if (string.Equals(def.Name, "Papageno", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-112044 SCFU npcFamily=207 Side=Omni.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)PapagenaNpcFamily);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Omni);
                mob.Stats[StatIds.side].Value = (int)Side.Omni;
            }
            else if (string.Equals(def.Name, "Dr. Rosenblatt", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260822-082554 Omni-side questgiver — blue name (Side=Omni).
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Omni);
                mob.Stats[StatIds.side].Value = (int)Side.Omni;
            }
            else if (string.Equals(def.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)BarkingChimeraNpcFamily);
            }
            else if (string.Equals(def.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)GeosurveyDogNpcFamily);
            }
            else if (string.Equals(def.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)SwiftSilvertailNpcFamily);
            }
            else if (string.Equals(def.Name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)NascenceSpiritHunterNpcFamily);
            }
            else if (string.Equals(def.Name, "Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)SoulDredgeNpcFamily);
            }
            else if (string.Equals(def.Name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)DiseaseRiddenRafterNpcFamily);
            }
            else if (string.Equals(def.Name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-112044 SCFU Side=OmniTek npcFamily=202.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)TempterusNpcFamily);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Omni);
                mob.Stats[StatIds.side].Value = (int)Side.Omni;
            }
            else if (string.Equals(def.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)PredatorStrikerNpcFamily);
            }
            else if (string.Equals(def.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260830-110744 SCFU Side=Monster (red PF map dots).
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)CripplerOfGrowthNpcFamily);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Monster);
                mob.Stats[StatIds.side].Value = (int)Side.Monster;
            }

            NascenceSwampClanMobRuntime.ApplySpawnStats(mob, def.Name);
            NascenceFrontierOutdoorMobRuntime.ApplySpawnStats(mob, def.Name);

            uint killXp = ResolveCaptureKillXp(def.Name);
            if (killXp > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.xp, killXp);
            }

            if (string.Equals(def.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase)
                && (def.Textures == null || def.Textures.Length == 0))
            {
                mob.Textures.Clear();
                mob.Textures.Add(new AOTextures(0, SwiftSilvertailTextureSlot0));
                mob.Textures.Add(new AOTextures(1, SwiftSilvertailTextureSlot1));
            }

            string combatFailure;
            CapturedEnemyCombatContract combatContract;
            if (string.Equals(def.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildBarkingChimeraCombatContract();
            }
            else if (string.Equals(def.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildGeosurveyDogCombatContract();
            }
            else if (string.Equals(def.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildSwiftSilvertailCombatContract();
            }
            else if (string.Equals(def.Name, "Papagena", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildPapagenaCombatContract();
            }
            else if (string.Equals(def.Name, "Papageno", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildPapagenoCombatContract();
            }
            else if (string.Equals(def.Name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildNascenceSpiritHunterCombatContract();
            }
            else if (string.Equals(def.Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildCascadingSpiritCombatContract();
            }
            else if (string.Equals(def.Name, "Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildSoulDredgeCombatContract();
            }
            else if (string.Equals(def.Name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildDiseaseRiddenRafterCombatContract();
            }
            else if (string.Equals(def.Name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                combatContract = BuildTempterusCombatContract();
            }
            else if (string.Equals(def.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                if (!NascenceFrontierOutdoorMobRuntime.TryGetCombatContract(def.Name, out combatContract))
                {
                    combatContract = BuildPredatorStrikerCombatContract();
                }
            }
            else if (string.Equals(def.Name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(def.Name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 7A2ED7C3 SAW 171/171/171/134 VQIR + AttackInfo 36|69.
                if (!NascenceFrontierOutdoorMobRuntime.TryGetCombatContract(def.Name, out combatContract))
                {
                    combatContract = CapturedEnemyCombatContract.Unresolved(
                        "Demonic Subjugator combat contract missing",
                        true);
                }
            }
            else if (string.Equals(def.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260827-221909 SAW 181 + AttackInfo Amount=24 (supersedes SAW-only 112044).
                if (!NascenceFrontierOutdoorMobRuntime.TryGetCombatContract(def.Name, out combatContract))
                {
                    combatContract = BuildCripplerOfGrowthCombatContractSawOnly();
                }
            }
            else if (NascenceFrontierOutdoorMobRuntime.TryGetCombatContract(def.Name, out combatContract))
            {
                // Corrupting Imp, Stalking/Deadly Predator, Malah-Ana, Weaver, Slivering, etc.
            }
            else
            {
                combatContract = CapturedEnemyCombatContract.Unresolved(
                    "NascenceLifeSpawn capture-backed actor has no source-local WIFU/attack-start/AttackInfo contract mapped; source NPC="
                    + def.Name + " monsterData=" + def.MonsterData + " level=" + def.Level,
                    true);
            }

            if (string.Equals(def.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 16u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 38u);
                ApplyRunSpeed(mob, 24);
            }
            else if (string.Equals(def.Name, "Papagena", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 4u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 8u);
            }
            else if (string.Equals(def.Name, "Papageno", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-112044 AttackInfo Amount=32; SCFU RunSpeedBase=52.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 32u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 32u);
            }
            else if (string.Equals(def.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 6u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 7u);
                ApplyRunSpeed(mob, 21);
            }
            else if (string.Equals(def.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 16u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 38u);
                ApplyRunSpeed(mob, 44);
            }
            else if (string.Equals(def.Name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-103458 AttackInfo Amount=10; SCFU RunSpeedBase=41.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 10u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 10u);
                ApplyRunSpeed(mob, 41);
            }
            else if (string.Equals(def.Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-103458 AttackInfo Amount=8; SCFU RunSpeedBase=34.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 8u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 8u);
                ApplyRunSpeed(mob, 34);
            }
            else if (string.Equals(def.Name, "Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-103458 AttackInfo Amount=13; SCFU RunSpeedBase=53.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 13u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 13u);
                ApplyRunSpeed(mob, 53);
            }
            else if (string.Equals(def.Name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-112044 AttackInfo Amount=21; SCFU RunSpeedBase=31.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 21u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 21u);
                ApplyRunSpeed(mob, 31);
            }
            else if (string.Equals(def.Name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260823-112044 AttackInfo Amount=10..12; SCFU RunSpeedBase=28. Flying Y observed in fight.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 10u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 12u);
                ApplyRunSpeed(mob, 28);
            }
            else if (string.Equals(def.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(def.CaptureFolder, "20260823-112044", StringComparison.Ordinal))
            {
                // Capture 20260823-112044 AttackInfo Amount=8; SCFU RunSpeedBase=34.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 8u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 8u);
                ApplyRunSpeed(mob, 34);
            }
            else if (string.Equals(def.Name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(def.Name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 AttackInfo Amount 36|69; SCFU RunSpeedBase=69 npcFamily=174.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 174u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 36u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 69u);
                ApplyRunSpeed(mob, 69);
            }
            else if (string.Equals(def.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260827-221909 AttackInfo Amount=24; SCFU RunSpeedBase=97.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 24u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 24u);
                ApplyRunSpeed(mob, 97);
            }

            if (!CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                    mob,
                    npcController,
                    combatContract,
                    out combatFailure))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceLifeSpawn combat not ready npc=" + def.Name + " reason=" + combatFailure);
            }

            mob.DoNotDoTimers = false;

            if (!NascenceLifeStarterBridgePatrolRuntime.TryApply(
                    def.PatrolCaptureInstance,
                    def.PlayfieldId,
                    def.X,
                    def.Y,
                    def.Z,
                    mob,
                    npcController))
            {
                float[][] waypoints = def.Waypoints;
                // Keep per-spawn capture waypoints; outdoor resolver fills gaps only.
                if (waypoints == null)
                {
                    bool staticCaptureHiathlin =
                        string.Equals(def.Name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(def.CaptureFolder, "20260826-225804", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(def.CaptureFolder, "20260826-055143", StringComparison.OrdinalIgnoreCase));
                    if (!staticCaptureHiathlin)
                    {
                        float[][] outdoorWaypoints;
                        if (NascenceFrontierOutdoorMobRuntime.TryResolvePatrolWaypoints(
                                def.Name,
                                def.X,
                                def.Y,
                                def.Z,
                                out outdoorWaypoints))
                        {
                            waypoints = outdoorWaypoints;
                        }
                    }
                }

                ApplyWaypoints(mob, npcController, waypoints);
                if (waypoints != null
                    && waypoints.Length > 0
                    && npcController.State == CharacterState.Patrolling)
                {
                    if (string.Equals(def.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
                    {
                        npcController.Run();
                    }
                    else
                    {
                        npcController.Walk();
                    }

                    npcController.StartPatrolling();
                }
            }

            activateNpc(mob);

            if (string.Equals(def.Name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase)
                && def.PlayfieldId == NascenceLifeContentModule.FrontierPlayfieldId)
            {
                NascenceFrontierSpinetoothMobCombat.RegisterAggressive(mob.Identity);
            }

            // Match AreteLandingSpawn / OrdinaryEnemy: register spatial visibility interest so
            // clients receive SCFU for Nascence life NPCs (ActivateNpc alone is dynel-only).
            try
            {
                playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceLifeSpawn visibility announce failed npc=" + def.Name
                    + " ex=" + ex.GetType().Name + ": " + ex.Message);
            }

            if (string.Equals(def.Name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "NascenceLifeSpawn Deadly Predator spawned id="
                    + mob.Identity.ToString(true)
                    + " x=" + def.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " y=" + def.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " z=" + def.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " capture=" + (def.CaptureFolder ?? "none"));
            }

            return true;
        }

        /// <summary>
        /// Capture 20260823-000659 fight 7A202B33: SAW 56/56/56/56/0 + Attack + AttackInfo slots 0/4/3 ammo=-1 hitType=3.
        /// FixedAttackInfo (not parallel replay): parallel stream initial delays blocked retaliation in playtests.
        /// Patrol: disabled here — shared 2-point replay synced all mobs; needs per-spawn routes from capture.
        /// </summary>
        private static CapturedEnemyCombatContract BuildBarkingChimeraCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(232045, 232046, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(232042, 232043, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(232039, 232040, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(232036, 232037, 0x4D584C50, "MXLP"),
                new CapturedEnemySpecialAttackDefinition(232033, 232034, 0x544B5251, "TKRQ"),
            };

            int[] damageObservations =
                {
                    19, 19, 19, 19, 19, 19, 25, 26, 29, 34, 27, 30, 22, 27, 28, 20, 30, 21, 35, 26, 31, 35,
                    36, 16, 37, 22, 19, 36, 27, 32, 30, 32, 37, 25, 31, 22, 29, 36, 27, 32, 24, 21, 32, 26,
                    27, 33, 24, 37, 19, 26, 31, 21, 34, 25, 29, 30, 28, 38, 30
                };
            double[] attackStartDelays = { 0.0, 0.0, 0.0, 0.0, 0.0 };
            double[] firstHitDelays =
                {
                    3.351970, 4.096567, 5.084861, 6.451340, 7.903126
                };
            double[] landedIntervals =
                {
                    4.321511, 4.579176, 4.653643, 4.703398, 4.735585, 5.017904, 5.430544, 6.145209
                };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-000659: Barking Chimera fight 7A202B33 SAW 56/Attack/AttackInfo",
                unchecked((int)0x7A202B33),
                NpcAiProfile.Passive,
                16,
                38,
                landedIntervals[0],
                specials,
                0,
                56,
                56,
                56,
                56,
                0,
                0,
                0,
                -1,
                0,
                0,
                3,
                unchecked((int)0x544B5251),
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        private static void ApplyRunSpeed(Character mob, int runSpeed)
        {
            mob.Stats[StatIds.runspeed].BaseValue = (uint)runSpeed;
            mob.Stats[StatIds.runspeed].Value = runSpeed;
        }

        /// <summary>
        /// Capture 20260723-225021 Papagena fight 7A1B402D + 082554 SAW BGVX/YAPK/LWEK/MXLP/TKRQ, unknowns 139/139/139/101;
        /// AttackInfo hitType=3 ammo=-1 damage span from capture packets.
        /// </summary>
        private static CapturedEnemyCombatContract BuildPapagenaCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(232045, 232046, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(232042, 232043, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(232039, 232040, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(232036, 232037, 0x4D584C50, "MXLP"),
                new CapturedEnemySpecialAttackDefinition(232033, 232034, 0x544B5251, "TKRQ"),
            };

            int[] damageObservations = { 8, 8, 4 };
            double[] attackStartDelays = { 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 1.354298, 2.068602, 4.276108 };
            double[] landedIntervals = { 3.115287, 2.20831, 2.456722 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260822-103209: Papagena fight 7A1B402D SAW/Attack/AttackInfo",
                unchecked((int)0x7A1B402D),
                NpcAiProfile.Passive,
                4,
                8,
                landedIntervals[0],
                specials,
                0,
                139,
                139,
                139,
                101,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x42475658,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-112044 Papageno Omni (7A226136) + 20260825-204815 (7A2ED761):
        /// SAW 139/139/139/101 specials BGVX/YAPK/LWEK/MXLP; AttackInfo Amount=32 ammo=-1.
        /// </summary>
        private static CapturedEnemyCombatContract BuildPapagenoCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(233069, 233070, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(233066, 233067, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(233063, 233064, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(233060, 233061, 0x4D584C50, "MXLP"),
            };

            int[] damageObservations = { 32, 32, 32, 32 };
            double[] attackStartDelays = { 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 2.5, 2.8, 3.0 };
            double[] landedIntervals = { 3.0, 3.2, 2.9 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260825-204815: Papageno Omni 7A2ED761 SAW/Attack/AttackInfo (also 20260823-112044)",
                unchecked((int)0x7A2ED761),
                NpcAiProfile.Passive,
                32,
                32,
                landedIntervals[0],
                specials,
                0,
                139,
                139,
                139,
                101,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x42475658,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        private static void ApplyCaptureHeadMesh(Character mob, int headMesh)
        {
            if (mob == null || headMesh <= 0)
            {
                return;
            }

            int existingHeadMesh = mob.Stats[StatIds.headmesh].Value;
            if (existingHeadMesh != 0 && existingHeadMesh != headMesh)
            {
                mob.MeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
                mob.SocialMeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
            }

            // BaseValue first so OverridingModifierStat.Set leaves modifier=0 when Value is assigned.
            mob.Stats[StatIds.headmesh].BaseValue = (uint)headMesh;
            mob.Stats[StatIds.headmesh].Value = headMesh;
            mob.MeshLayer.AddMesh(0, headMesh, 0, 4);
            mob.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
        }

        private static void ApplyWaypoints(Character mob, NPCController controller, float[][] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return;
            }

            mob.Waypoints.Clear();
            foreach (float[] wp in waypoints)
            {
                mob.AddWaypoint(new Vector3(wp[0], wp[1], wp[2]), false);
            }

            controller.State = CharacterState.Patrolling;
        }

        private static uint ResolveCaptureKillXp(string name)
        {
            if (string.Equals(name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)BarkingChimeraKillXp;
            }

            if (string.Equals(name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)GeosurveyDogKillXp;
            }

            if (string.Equals(name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)SwiftSilvertailKillXp;
            }

            if (string.Equals(name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)NascenceSpiritHunterKillXp;
            }

            if (string.Equals(name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)CascadingSpiritKillXp;
            }

            if (string.Equals(name, "Soul Dredge", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)SoulDredgeKillXp;
            }

            if (string.Equals(name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)DiseaseRiddenRafterKillXp;
            }

            if (string.Equals(name, "Tempterus", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)TempterusKillXp;
            }

            if (string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)PredatorStrikerKillXp;
            }

            if (string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)DeadlyPredatorKillXp;
            }

            if (string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)SpinetoothHatchlingKillXp;
            }

            if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)WeaverOfMaliceKillXp;
            }

            if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)HiathlinKillXp;
            }

            if (string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase))
            {
                return (uint)OmathonKillXp;
            }

            return 0;
        }

        /// <summary>
        /// Capture 20260822-221109 Swift Silvertail fight: SAW unknowns 67/67/67/67 + AttackInfo damage 6-7.
        /// </summary>
        private static CapturedEnemyCombatContract BuildSwiftSilvertailCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(232045, 232046, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(232042, 232043, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(232039, 232040, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(232036, 232037, 0x4D584C50, "MXLP"),
                new CapturedEnemySpecialAttackDefinition(232033, 232034, 0x544B5251, "TKRQ"),
            };

            int[] damageObservations = { 6, 7, 6, 7, 7, 6 };
            double[] attackStartDelays = { 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 2.5, 3.0, 2.8 };
            double[] landedIntervals = { 4.0, 4.5, 4.2 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260822-221109: Swift Silvertail SAW/Attack/AttackInfo",
                unchecked((int)0x7A1B4453),
                NpcAiProfile.Passive,
                6,
                7,
                landedIntervals[0],
                specials,
                0,
                67,
                67,
                67,
                67,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x42475658,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260822-221109 Geosurvey Dog shares Chimera wire family; uses Chimera SAW pattern.
        /// </summary>
        private static CapturedEnemyCombatContract BuildGeosurveyDogCombatContract()
        {
            return BuildBarkingChimeraCombatContract();
        }

        /// <summary>
        /// Capture 20260823-103458 Nascence Spirit Hunter fight (e.g. 7A19FD9E):
        /// SAW 120/120/120/81 specials RIJL/DATJ/UZBM/CHCF/IFOH; AttackInfo Amount=10 HitType=Normal ammo=-1.
        /// </summary>
        private static CapturedEnemyCombatContract BuildNascenceSpiritHunterCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(236699, 236700, 0x52494A4C, "RIJL"),
                new CapturedEnemySpecialAttackDefinition(236696, 236697, 0x4441544A, "DATJ"),
                new CapturedEnemySpecialAttackDefinition(236693, 236694, 0x555A424D, "UZBM"),
                new CapturedEnemySpecialAttackDefinition(211013, 211014, 0x43484346, "CHCF"),
                new CapturedEnemySpecialAttackDefinition(211010, 211011, 0x49464F48, "IFOH"),
            };

            int[] damageObservations =
                {
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10
                };
            double[] attackStartDelays = { 0.0, 0.0, 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 3.0, 3.2, 2.9, 3.1, 3.4 };
            double[] landedIntervals = { 4.049, 3.805, 4.653, 4.82, 4.004, 4.892, 4.485, 3.962, 2.924 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-103458: Nascence Spirit Hunter SAW 120/Attack/AttackInfo",
                unchecked((int)0x7A19FD9E),
                NpcAiProfile.Passive,
                10,
                10,
                landedIntervals[0],
                specials,
                0,
                120,
                120,
                120,
                81,
                0,
                0,
                0,
                -1,
                3,
                0,
                3,
                0x4441544A,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-103458 Cascading Spirit fight (e.g. 7A1C3B42):
        /// SAW 68/68/68/68 specials RLNV/TMDT/QRHO/QHBG/ZAFF; AttackInfo Amount=8 slot mostly 4.
        /// </summary>
        private static CapturedEnemyCombatContract BuildCascadingSpiritCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(213154, 213157, 0x524C4E56, "RLNV"),
                new CapturedEnemySpecialAttackDefinition(213145, 213148, 0x544D4454, "TMDT"),
                new CapturedEnemySpecialAttackDefinition(213135, 213138, 0x5152484F, "QRHO"),
                new CapturedEnemySpecialAttackDefinition(213129, 213131, 0x51484247, "QHBG"),
                new CapturedEnemySpecialAttackDefinition(210259, 210260, 0x5A414646, "ZAFF"),
            };

            int[] damageObservations = { 8, 8, 8, 8, 8, 8, 8 };
            double[] attackStartDelays = { 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 2.5, 2.8, 2.7 };
            double[] landedIntervals = { 2.915, 2.772, 5.865 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-103458: Cascading Spirit SAW 68/Attack/AttackInfo",
                unchecked((int)0x7A1C3B42),
                NpcAiProfile.Passive,
                8,
                8,
                landedIntervals[0],
                specials,
                0,
                68,
                68,
                68,
                68,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x524C4E56,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-103458 Soul Dredge fight 7A20292F:
        /// SAW 139/139/139/101 same special family as Hunter; AttackInfo Amount=13.
        /// </summary>
        private static CapturedEnemyCombatContract BuildSoulDredgeCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(236699, 236700, 0x52494A4C, "RIJL"),
                new CapturedEnemySpecialAttackDefinition(236696, 236697, 0x4441544A, "DATJ"),
                new CapturedEnemySpecialAttackDefinition(236693, 236694, 0x555A424D, "UZBM"),
                new CapturedEnemySpecialAttackDefinition(211013, 211014, 0x43484346, "CHCF"),
                new CapturedEnemySpecialAttackDefinition(211010, 211011, 0x49464F48, "IFOH"),
            };

            int[] damageObservations = { 13, 13, 13, 13, 13, 13, 13, 13, 13 };
            double[] attackStartDelays = { 0.0, 0.0, 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 2.8, 3.0, 2.9, 3.1, 3.2 };
            double[] landedIntervals = { 3.548, 2.849, 4.03, 5.509, 4.556, 4.398, 4.465, 4.494 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-103458: Soul Dredge SAW 139/Attack/AttackInfo",
                unchecked((int)0x7A20292F),
                NpcAiProfile.Passive,
                13,
                13,
                landedIntervals[0],
                specials,
                0,
                139,
                139,
                139,
                101,
                0,
                0,
                0,
                -1,
                3,
                0,
                3,
                0x4441544A,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-112044 Disease-Ridden Rafter (Shadowlands Rafter fight family):
        /// SAW 64/64/64/64 specials BGVX/YAPK/LWEK/MXLP/TKRQ (templates 233069..233058);
        /// AttackInfo Amount=21 ammo=-1 slot=4 HitType wire=3 WeaponInstance=BGVX.
        /// Keep this SAW pattern — Rafters recur across Shadowlands.
        /// </summary>
        private static CapturedEnemyCombatContract BuildDiseaseRiddenRafterCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(233069, 233070, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(233066, 233067, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(233063, 233064, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(233060, 233061, 0x4D584C50, "MXLP"),
                new CapturedEnemySpecialAttackDefinition(233057, 233058, 0x544B5251, "TKRQ"),
            };

            int[] damageObservations = { 21 };
            double[] attackStartDelays = { 0.0 };
            double[] firstHitDelays = { 3.0 };
            double[] landedIntervals = { 4.0 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-112044: Disease-Ridden Rafter SAW 64/Attack/AttackInfo",
                unchecked((int)0x7A19FDA0),
                NpcAiProfile.Passive,
                21,
                21,
                landedIntervals[0],
                specials,
                0,
                64,
                64,
                64,
                64,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x42475658,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-112044 Tempterus (Unredeemed flyer name in task):
        /// SAW 60/60/60/60 specials JIBR/VIMD/EGXN/RYJT/GYOV; AttackInfo Amount=10..12.
        /// </summary>
        private static CapturedEnemyCombatContract BuildTempterusCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(213168, 213169, 0x4A494252, "JIBR"),
                new CapturedEnemySpecialAttackDefinition(213165, 213166, 0x56494D44, "VIMD"),
                new CapturedEnemySpecialAttackDefinition(213162, 213163, 0x4547584E, "EGXN"),
                new CapturedEnemySpecialAttackDefinition(213159, 213160, 0x52594A54, "RYJT"),
                new CapturedEnemySpecialAttackDefinition(210262, 210263, 0x47594F56, "GYOV"),
            };

            int[] damageObservations = { 10, 12 };
            double[] attackStartDelays = { 0.0, 0.0 };
            double[] firstHitDelays = { 2.8, 3.0 };
            double[] landedIntervals = { 1.318, 4.0 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-112044: Tempterus SAW 60/Attack/AttackInfo",
                unchecked((int)0x7A22621E),
                NpcAiProfile.Passive,
                10,
                12,
                landedIntervals[0],
                specials,
                0,
                60,
                60,
                60,
                60,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x4A494252,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-112044 Predator Striker:
        /// SAW 68/68/68/68 specials RIJL/DATJ/UZBM/CHCF/IFOH (same family as Spirit Hunter);
        /// AttackInfo Amount=8 ammo=-1 slot=4 HitType wire=3.
        /// </summary>
        private static CapturedEnemyCombatContract BuildPredatorStrikerCombatContract()
        {
            var specials = new[]
            {
                new CapturedEnemySpecialAttackDefinition(236699, 236700, 0x52494A4C, "RIJL"),
                new CapturedEnemySpecialAttackDefinition(236696, 236697, 0x4441544A, "DATJ"),
                new CapturedEnemySpecialAttackDefinition(236693, 236694, 0x555A424D, "UZBM"),
                new CapturedEnemySpecialAttackDefinition(211013, 211014, 0x43484346, "CHCF"),
                new CapturedEnemySpecialAttackDefinition(211010, 211011, 0x49464F48, "IFOH"),
            };

            int[] damageObservations = { 8, 8 };
            double[] attackStartDelays = { 0.0, 0.0 };
            double[] firstHitDelays = { 2.8, 3.0 };
            double[] landedIntervals = { 4.0, 36.941 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                "20260823-112044: Predator Striker SAW 68/Attack/AttackInfo",
                unchecked((int)0x7A202DB0),
                NpcAiProfile.Passive,
                8,
                8,
                4.0,
                specials,
                0,
                68,
                68,
                68,
                68,
                0,
                0,
                0,
                -1,
                4,
                0,
                3,
                0x52494A4C,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                StarterBridgeCapturedAttackRange,
                true);
        }

        /// <summary>
        /// Capture 20260823-112044 Crippler of Growth 7A226132: SAW RIJL family unknowns 68
        /// and Attack start observed, but no AttackInfo Amount before death (one-shot fight).
        /// Loot/corpse mesh are capture-backed; re-fight needed for damage Amount.
        /// </summary>
        private static CapturedEnemyCombatContract BuildCripplerOfGrowthCombatContractSawOnly()
        {
            return CapturedEnemyCombatContract.Unresolved(
                "20260823-112044: Crippler of Growth SAW RIJL/DATJ/UZBM/CHCF/IFOH unknowns 68 captured; "
                + "AttackInfo Amount not observed before death — re-fight for damage",
                true);
        }
    }
}
