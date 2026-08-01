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
    /// Capture 20260731-180854 Cleaning Robots around Flint Novak / Alex pad.
    /// Unique living positions only — removes prior stacked extras at Flint.
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

        // Capture 20260731-180854: unique living Cleaning Robot positions (5m cluster).
        private static readonly float[][] SpawnSlots =
            {
                // Flint Novak ground cluster (y≈5) — two near Flint, not a dense pile.
                new[] { 3587.3360f, 5.110001f, 862.4274f },
                new[] { 3578.4930f, 5.110001f, 862.4495f },
                new[] { 3575.4875f, 5.110001f, 873.9719f },
                new[] { 3575.0576f, 5.110001f, 886.1254f },
                new[] { 3589.1553f, 5.110001f, 884.7966f },
                new[] { 3560.9440f, 5.110001f, 871.1031f },
                new[] { 3554.4130f, 5.177658f, 877.9979f },
                new[] { 3550.2827f, 5.110001f, 855.3019f },
                new[] { 3538.1343f, 5.340942f, 878.4525f },
                // Elevated / alley Cleaning Robots from same capture.
                new[] { 3558.6274f, 8.110001f, 908.8449f },
                new[] { 3551.0073f, 9.057640f, 929.5439f },
                new[] { 3562.2760f, 8.710001f, 890.5980f },
                new[] { 3603.3179f, 9.460402f, 895.8156f },
                new[] { 3577.5325f, 10.597379f, 909.5761f },
                new[] { 3599.0752f, 14.925001f, 914.4692f },
                new[] { 3604.7920f, 26.513996f, 878.2173f },
                new[] { 3610.5615f, 28.619068f, 877.5854f },
                new[] { 3621.8042f, 35.376926f, 863.7999f },
                new[] { 3622.1458f, 37.565000f, 847.7902f },
                new[] { 3630.9453f, 40.984997f, 855.5093f },
                new[] { 3638.7380f, 40.984997f, 823.8665f }
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
                + " source=20260731-180854");
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
                "cleaning-robot-20260731-180854",
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
