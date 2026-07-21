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

        // Capture 20260720-204431 (Alex pad) + 20260720-goldman combat extras + 30s respawn.
        private static readonly MobSlot[] Slots =
            {
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3520.2432f, 5.315f, 872.97473f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3502.3074f, 5.1100006f, 857.66364f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3521.3774f, 5.1100006f, 876.4073f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3522.3467f, 5.1100006f, 874.9996f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3523.994f, 5.1100006f, 880.1992f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3495.174f, 5.1100006f, 879.16656f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0f, 3492.6052f, 5.1100006f, 878.40924f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0f, 3513.9517f, 5.1100006f, 865.63983f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0f, 3514.402f, 5.1100006f, 866.71875f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0f, 3510.8594f, 5.1100006f, 864.0514f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0f, 3492.674f, 5.1100006f, 866.95435f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1f, 3529.97f, 5.1100006f, 894.44257f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1f, 3499.842f, 5.1100006f, 898.7892f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1f, 3559.97f, 5.1100006f, 865.22f),
                new MobSlot("IIV-X Advanced Docker", MobKind.Docker, 17649, 4, 323, 1019, 110, 15, NpcAiProfile.Passive, 0f, 3515.6375f, 5.3050003f, 905.0099f),
                new MobSlot("Cleanmeister Intelligence Robot", MobKind.CleaningRobot, 297023, 2, 180, 1019, 100, 13, NpcAiProfile.Passive, 0f, 3544.5f, 5.31f, 872.4f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3514.95f, 5.11f, 914.61f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3523.71f, 5.11f, 897.75f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3514.13f, 5.11f, 884.92f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3511.82f, 5.11f, 891.78f),
                new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Passive, 0.0f, 3494.28f, 5.24f, 906.53f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3531.89f, 6.81f, 906.31f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 1, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3555.44f, 8.29f, 919.14f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3545.62f, 6.73f, 897.12f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3516.64f, 7.78f, 930.04f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3502.75f, 5.11f, 891.65f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.36f, 0.01f, 809.73f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.59f, 0.01f, 858.98f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 5, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3452.62f, 0.01f, 884.29f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3426.74f, 0.01f, 835.28f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3422.32f, 0.01f, 866.07f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 5, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3425.29f, 0.01f, 818.4f),
                new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 6, 24, 25, 125, 8, NpcAiProfile.Aggressive, 1.0f, 3437.13f, 0.01f, 803.59f),
                new MobSlot("Mutated Garbage Flea", MobKind.GarbageFlea, 17657, 7, 559, 25, 200, 23, NpcAiProfile.Aggressive, 1.0f, 3422.907f, 0.01f, 878.7842f),
                new MobSlot("Supreme Collector of Waste", MobKind.WasteCollector, 17714, 4, 60, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3505.91f, 11.02f, 943.13f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3548.63f, 6.93f, 906.13f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3567.44f, 8.14f, 919.03f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3543.79f, 7.4f, 889.6f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3556.35f, 8.37f, 915.44f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3507.55f, 5.11f, 891.72f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3511.22f, 7.78f, 923.88f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3507.34f, 8.4f, 931.53f),
                new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Passive, 0.0f, 3498.86f, 9.26f, 918.92f),
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

            // Capture 20260720-204431: AttackInfo WeaponInstance 1279612721 / 1279612722;
            // Docker hits 4–14, Waste ~8, Cleanmeister ~6, Flea 6–8.
            int minDamage;
            int maxDamage;
            ResolveCaptureDamage(slot, out minDamage, out maxDamage);
            // Capture AttackInfo start context for all; only fleas use Aggressive AiProfile.
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "alex-area-20260720-204431",
                minDamage,
                maxDamage,
                2.0,
                0,
                0,
                1279612721);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            // FixedAttackOnSight sets Aggressive; restore capture AI (Passive except fleas).
            controller.AiProfile = slot.AiProfile;
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            if (slot.Kind == MobKind.GarbageFlea && slot.AggroRadiusMeters > 0f)
            {
                RegisterAggro(mob.Identity.Instance, slot.AggroRadiusMeters);
                MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
            }

            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static void ResolveCaptureDamage(MobSlot slot, out int minDamage, out int maxDamage)
        {
            switch (slot.Kind)
            {
                case MobKind.Docker:
                    minDamage = 4;
                    maxDamage = 14;
                    return;
                case MobKind.WasteCollector:
                    minDamage = 8;
                    maxDamage = 8;
                    return;
                case MobKind.CleaningRobot:
                    minDamage = 6;
                    maxDamage = 6;
                    return;
                case MobKind.GarbageFlea:
                default:
                    minDamage = 6;
                    maxDamage = 8;
                    return;
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
            SetStat(mob, StatIds.flags, 268964353);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            // Capture 20260720-204431: Docker +400 XP, Waste ~347/316, Flea +316.
            SetStat(mob, StatIds.xp, ResolveCaptureXp(slot));
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
