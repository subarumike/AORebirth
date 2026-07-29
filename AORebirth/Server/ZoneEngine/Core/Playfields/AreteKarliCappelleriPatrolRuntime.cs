namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260727-055715: Karli Cappelleri inside Crashed Alien Ship (PF 8009).
    /// SCFU (SimpleChar:799AD394) + FollowTarget NpcPath loop on the ship floor.
    /// Dialogue / quest / rewards were not present in this capture.
    /// </summary>
    internal static class AreteKarliCappelleriPatrolRuntime
    {
        internal const string KarliName = "Karli Cappelleri";

        internal const bool SpawnEnabled = true;

        private const int CrashedAlienShipPlayfieldId = 8009;

        private const int KarliCaptureInstance = unchecked((int)0x799AD394);

        private const string TemplateHash = "BART";

        // Matches NPCController.WalkFollowSpeedPerSecond.
        private const double WalkSpeedPerSecond = 1.5;

        private const double EarlyTurnFactor = 0.85;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        // Capture FollowTarget NpcPath destinations (y=0.435 throughout).
        private static readonly float[][] PatrolLoopWaypoints =
            {
                new[] { 49.341f, 0.435f, 49.576f },
                new[] { 50.422f, 0.435f, 41.723f },
                new[] { 50.031f, 0.435f, 32.054f },
                new[] { 45.423f, 0.435f, 31.171f },
                new[] { 38.128f, 0.435f, 32.729f },
                new[] { 31.111f, 0.435f, 41.707f },
                new[] { 34.599f, 0.435f, 46.464f },
                new[] { 41.239f, 0.435f, 46.778f },
            };

        private static readonly NpcPatrolReplaySegment[] PatrolSegments = BuildContinuousLoop(PatrolLoopWaypoints);

        public static bool IsKarliDefinition(string name, int captureInstance)
        {
            return captureInstance == KarliCaptureInstance
                   || string.Equals(name, KarliName, StringComparison.OrdinalIgnoreCase);
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (!AreteMobDiagnosticSwitches.KarliCappelleri
                || !SpawnEnabled
                || playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != CrashedAlienShipPlayfieldId
                || !SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            try
            {
                if (!SpawnKarli(playfield, playfieldIdentity, activateNpc))
                {
                    SpawnedPlayfields.Remove(playfieldIdentity.Instance);
                }
            }
            catch (Exception ex)
            {
                SpawnedPlayfields.Remove(playfieldIdentity.Instance);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteKarliCappelleriPatrolRuntime spawn failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            SpawnedPlayfields.Remove(playfieldInstance);
        }

        public static void PrepareSpawnedKarli(Character mob, NPCController controller)
        {
            if (!AreteMobDiagnosticSwitches.KarliCappelleri || mob == null || controller == null)
            {
                return;
            }

            controller.AiProfile = NpcAiProfile.Passive;
        }

        public static bool TryApplyPatrol(int captureInstance, NPCController controller)
        {
            if (!AreteMobDiagnosticSwitches.KarliCappelleri
                || controller == null
                || captureInstance != KarliCaptureInstance)
            {
                return false;
            }

            controller.SetCapturedPatrolReplaySegments(
                PatrolSegments,
                true,
                false,
                false);
            controller.State = CharacterState.Patrolling;
            return true;
        }

        public static void PauseForDialogue(ICharacter npc)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller == null || !IsKarliNpc(npc))
            {
                return;
            }

            controller.SnapshotCurrentMotionPosition();
            controller.StopFollow();
            controller.State = CharacterState.Idle;
        }

        public static void ResumeAfterDialogue(ICharacter npc)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller == null || !IsKarliNpc(npc))
            {
                return;
            }

            controller.SetCapturedPatrolReplaySegments(
                PatrolSegments,
                true,
                false,
                false);
            controller.State = CharacterState.Patrolling;
            controller.StartPatrolling();
        }

        private static bool IsKarliNpc(ICharacter npc)
        {
            return npc != null
                   && (npc.Identity.Instance == KarliCaptureInstance
                       || string.Equals(npc.Name, KarliName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool SpawnKarli(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = 36.147f, y = 0.435f, z = 45.919f },
                new Quaternion(0.0, 0.6452817, 0.0, 0.7639447),
                controller,
                15);
            if (mob == null)
            {
                return false;
            }

            // Pool instance ≠ capture id (AreteLandingSpawn pattern). Match by name for dialogue.
            mob.Name = KarliName;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, 26125u);
            mob.Stats[StatIds.monsterdata].Value = 26125;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, 393u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, 393u);
            mob.Stats[StatIds.health].Value = 393;
            mob.Stats[StatIds.life].Value = 393;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, 15u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 137u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.losheight, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, 277352961u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, 2u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, 3u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, 1u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, 1u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, 3u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, 3u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, 100u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, 40215u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);

            // Capture SCFU texture IDs (places 1..4) + mesh layers from 20260727-055715.
            mob.Textures.Clear();
            mob.Textures.Add(new AOTextures(0, 0));
            mob.Textures.Add(new AOTextures(1, 268366));
            mob.Textures.Add(new AOTextures(2, 247989));
            mob.Textures.Add(new AOTextures(3, 268369));
            mob.Textures.Add(new AOTextures(4, 268368));

            mob.MeshLayer.Clear();
            mob.SocialMeshLayer.Clear();
            int[][] meshes =
                {
                    new[] { 0, 268360, 246944, 2 },
                    new[] { 0, 40215, 0, 4 },
                    new[] { 1, 268359, 0, 2 },
                    new[] { 2, 268385, 0, 3 },
                    new[] { 5, 268358, 246944, 0 },
                };
            foreach (int[] m in meshes)
            {
                mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
            }

            PrepareSpawnedKarli(mob, controller);
            TryApplyPatrol(KarliCaptureInstance, controller);
            mob.Coordinates(new Coordinate { x = 36.147f, y = 0.435f, z = 45.919f });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            controller.StartPatrolling();

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteKarliCappelleriPatrolRuntime spawned pf=8009 id="
                + mob.Identity.ToString(true) + " source=20260727-055715");
            return true;
        }

        private static NpcPatrolReplaySegment[] BuildContinuousLoop(float[][] waypoints)
        {
            var segments = new NpcPatrolReplaySegment[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                float[] start = waypoints[i];
                float[] end = waypoints[(i + 1) % waypoints.Length];
                double dx = end[0] - start[0];
                double dz = end[2] - start[2];
                double distance = Math.Sqrt((dx * dx) + (dz * dz));
                double delay = Math.Max(0.25, (distance / WalkSpeedPerSecond) * EarlyTurnFactor);
                segments[i] = new NpcPatrolReplaySegment(
                    delay,
                    start[0],
                    start[1],
                    start[2],
                    end[0],
                    end[1],
                    end[2]);
            }

            return segments;
        }
    }
}
