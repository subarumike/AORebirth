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

    internal static class MarcusPadAmbientCombat
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string MarcusName = "Marcus Stone";

        private const string BurningRobotName = "Burning Cleaning Robot";

        private const float RobotX = 3636.5132f;

        private const float RobotY = 40.984997f;

        private const float RobotZ = 832.7695f;

        private const int RobotHealth = 58;

        private const int RobotLevel = 5;

        private const int RobotCharacterFlags = 269226497;

        private const int RobotScale = 200;

        private const double RobotRespawnSeconds = 20.0;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> NextRobotRespawnUtc = new Dictionary<int, DateTime>();

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRobotRespawnUtc.Remove(playfieldInstance);
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

            Character marcus = FindNamedNpc(playfield, MarcusName);
            if (marcus == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MarcusPadAmbientCombat: Marcus Stone not found pf=" + playfieldIdentity.Instance);
                return;
            }

            QuarantineMarcus(marcus);
            Character robot = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (robot == null)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat combat quarantined Marcus="
                + marcus.Identity.ToString(true)
                + " robot="
                + robot.Identity.ToString(true)
                + " source=20260720-064523");
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Contains(playfieldIdentity.Instance))
            {
                return;
            }

            Character robot = FindNamedNpc(playfield, BurningRobotName);
            if (robot != null && robot.Stats[StatIds.health].Value > 0)
            {
                NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
                return;
            }

            DateTime nextRespawn;
            if (!NextRobotRespawnUtc.TryGetValue(playfieldIdentity.Instance, out nextRespawn))
            {
                NextRobotRespawnUtc[playfieldIdentity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(RobotRespawnSeconds);
                return;
            }

            if (nextRespawn > DateTime.UtcNow)
            {
                return;
            }

            Character marcusForRespawn = FindNamedNpc(playfield, MarcusName);
            if (marcusForRespawn == null)
            {
                return;
            }

            QuarantineMarcus(marcusForRespawn);
            Character spawned = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (spawned == null)
            {
                return;
            }

            NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat respawned robot="
                + spawned.Identity.ToString(true)
                + " source=20260720-064523");
        }

        private static void QuarantineMarcus(Character marcus)
        {
            string failure;
            CapturedEnemyCombatRuntime.Prepare(
                marcus,
                marcus.Controller as NPCController,
                CapturedEnemyCombatContract.Unresolved(
                    "20260720-064523 Marcus ambient fight lacks an exact owner weapon/full attack packet chain",
                    true),
                out failure);
        }

        private static Character SpawnBurningRobot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character robot = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = RobotX, y = RobotY, z = RobotZ },
                new Quaternion(0.0, 0.9414477, 0.0, 0.3371589),
                controller,
                RobotLevel);
            if (robot == null)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "MarcusPadAmbientCombat: Burning Cleaning Robot spawn failed");
                return null;
            }

            robot.Name = BurningRobotName;
            robot.Playfield = playfield;
            CombatTestMobArchetype.Prepare(robot, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            robot.Name = BurningRobotName;
            robot.Stats[StatIds.life].Value = RobotHealth;
            robot.Stats[StatIds.life].BaseValue = (uint)RobotHealth;
            robot.Stats[StatIds.health].Value = RobotHealth;
            robot.Stats[StatIds.health].BaseValue = (uint)RobotHealth;
            robot.Stats[StatIds.level].Value = RobotLevel;
            robot.Stats[StatIds.level].BaseValue = (uint)RobotLevel;
            robot.Stats[StatIds.monsterscale].Value = RobotScale;
            robot.Stats[StatIds.monsterscale].BaseValue = (uint)RobotScale;
            robot.Stats[StatIds.flags].Value = RobotCharacterFlags;
            robot.Stats[StatIds.flags].BaseValue = (uint)RobotCharacterFlags;
            robot.Stats[StatIds.visualflags].Value = 31;
            robot.Stats[StatIds.visualflags].BaseValue = 31u;
            robot.Coordinates(new Coordinate { x = RobotX, y = RobotY, z = RobotZ });
            string combatFailure;
            CapturedEnemyCombatRuntime.Prepare(
                robot,
                controller,
                CapturedEnemyCombatContract.Unresolved(
                    "20260720-064523 Burning Cleaning Robot ambient fight lacks an exact owner weapon/full attack packet chain",
                    true),
                out combatFailure);
            robot.DoNotDoTimers = false;
            activateNpc(robot);
            playfield.AnnounceSpawnedCharacterVisibility(robot, Identity.None);
            return robot;
        }

        private static Character FindNamedNpc(Playfield playfield, string name)
        {
            if (playfield == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate as Character;
            }

            return null;
        }
    }
}
