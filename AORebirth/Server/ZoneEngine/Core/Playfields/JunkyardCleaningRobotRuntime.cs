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
    /// Capture 20260720-212302 Cleaning Robots near Alex pad (mesh / attack / loot).
    /// Slots kept to the pad cluster so spawn stays reliable.
    /// </summary>
    internal static class JunkyardCleaningRobotRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string RobotName = "Cleaning Robot";

        private const int RobotMonsterData = 297023;

        private const int RobotLevel = 1;

        private const int RobotHealth = 15;

        private const int RobotScale = 200;

        private const int RobotNpcFamily = 1019;

        private const int RobotRunSpeed = 8;

        private const int RobotCharacterFlags = 268964353;

        private const int MissingVisualId = 1234567890;

        private const double RespawnSeconds = 30.0;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        // Alex-pad Cleaning Robot cluster from capture 20260720-212302 (y≈5).
        private static readonly float[][] SpawnSlots =
            {
                new[] { 3589.222f, 5.1100006f, 864.95667f },
                new[] { 3583.181f, 5.1100006f, 870.7136f },
                new[] { 3587.6567f, 5.1100006f, 881.64233f },
                new[] { 3580.9883f, 5.1100006f, 866.9392f },
                new[] { 3582.4028f, 5.1100006f, 884.2308f },
                new[] { 3585.8594f, 5.1100006f, 869.0397f },
                new[] { 3578.9949f, 5.1100006f, 871.8415f },
                new[] { 3578.7f, 5.1100006f, 863.3f },
                new[] { 3586.5f, 5.1100006f, 862.3f },
                new[] { 3586.1200f, 5.110001f, 861.9959f },
                new[] { 3575.9670f, 5.110001f, 873.3813f },
                new[] { 3555.2160f, 5.110001f, 862.7046f },
                new[] { 3561.2063f, 5.110001f, 871.3840f },
                new[] { 3549.5845f, 5.110001f, 855.8112f }
            };

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            DateTime[] timers = new DateTime[SpawnSlots.Length];
            NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            int spawned = 0;
            for (int i = 0; i < SpawnSlots.Length; i++)
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
                        "JunkyardCleaningRobotRuntime spawn slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "JunkyardCleaningRobotRuntime spawned="
                + spawned
                + "/"
                + SpawnSlots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260720-212302");
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
                || timers.Length != SpawnSlots.Length)
            {
                timers = new DateTime[SpawnSlots.Length];
                NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            }

            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                if (HasLivingRobotNear(playfield, SpawnSlots[i]))
                {
                    timers[i] = DateTime.MaxValue;
                }
                else if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(timers[i] > DateTime.UtcNow))
                {
                    try
                    {
                        if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                        {
                            timers[i] = DateTime.MaxValue;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            float[] pos = SpawnSlots[slotIndex];
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character robot = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = pos[0], y = pos[1], z = pos[2] },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                RobotLevel);
            if (robot == null)
            {
                return null;
            }

            robot.Name = RobotName;
            robot.Playfield = playfield;
            CombatTestMobArchetype.Prepare(robot, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            robot.Name = RobotName;
            ApplyCaptureStats(robot);
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "cleaning-robot-20260720-212302",
                4,
                6,
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
            string unused;
            CapturedEnemyCombatRuntime.Prepare(robot, controller, contract, out unused);
            controller.AiProfile = NpcAiProfile.Passive;
            robot.Coordinates(new Coordinate { x = pos[0], y = pos[1], z = pos[2] });
            robot.DoNotDoTimers = false;
            activateNpc(robot);
            playfield.AnnounceSpawnedCharacterVisibility(robot, Identity.None);
            return robot;
        }

        private static void ApplyCaptureStats(Character robot)
        {
            SetStat(robot, StatIds.monsterdata, RobotMonsterData);
            SetStat(robot, StatIds.life, RobotHealth);
            SetStat(robot, StatIds.health, RobotHealth);
            SetStat(robot, StatIds.level, RobotLevel);
            SetStat(robot, StatIds.npcfamily, RobotNpcFamily);
            SetStat(robot, StatIds.monsterscale, RobotScale);
            SetStat(robot, StatIds.runspeed, RobotRunSpeed);
            SetStat(robot, StatIds.flags, RobotCharacterFlags);
            SetStat(robot, StatIds.visualflags, 31);
            SetStat(robot, StatIds.side, 3);
            SetStat(robot, StatIds.breed, 6);
            SetStat(robot, StatIds.sex, 1);
            SetStat(robot, StatIds.race, 1);
            SetStat(robot, StatIds.fatness, 1);
            SetStat(robot, StatIds.xp, 316);
            SetStat(robot, StatIds.catmesh, MissingVisualId);
            SetStat(robot, StatIds.displaycatmesh, MissingVisualId);
            if (robot.Textures != null)
            {
                robot.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    robot.Textures.Add(new AORebirth.Core.Textures.AOTextures(i, 0));
                }
            }

            if (robot.MeshLayer != null)
            {
                robot.MeshLayer.Clear();
            }

            if (robot.SocialMeshLayer != null)
            {
                robot.SocialMeshLayer.Clear();
            }
        }

        private static void SetStat(ICharacter mob, StatIds stat, int value)
        {
            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }

        private static bool HasLivingRobotNear(Playfield playfield, float[] pos)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, RobotName, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - pos[0];
                float dz = candidate.Coordinates().z - pos[2];
                if (dx * dx + dz * dz <= 6.25f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
