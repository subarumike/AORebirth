namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture 20260726-leonora-pathing: Leonora Marty FollowTarget NpcPath loop
    /// (SimpleChar:78E0FC74) around Arete Landing.
    /// South plaza legs (z &lt; 850 / y spike) removed — she was walking into floor mesh there.
    /// </summary>
    internal static class AreteLeonoraMartyPatrolRuntime
    {
        internal const string LeonoraName = "Leonora Marty";

        // See AreteMobDiagnosticSwitches.LeonoraMarty — keep in sync.
        internal const bool SpawnEnabled = true;

        private const int LeonoraCaptureInstance = unchecked((int)0x78E0FC74);

        // Matches NPCController.WalkFollowSpeedPerSecond.
        private const double WalkSpeedPerSecond = 1.5;

        // Fire next FollowTarget before the walk finishes so direction changes
        // without a full stop at the corner.
        private const double EarlyTurnFactor = 0.85;

        // Capture loop with south floor-stuck segment removed (z&lt;850 + y=9.733 spur).
        private static readonly float[][] PatrolLoopWaypoints =
            {
                new[] { 3423.965f, 9.105f, 851.925f },
                new[] { 3428.058f, 9.105f, 857.513f },
                new[] { 3431.677f, 9.030f, 857.515f },
                new[] { 3436.988f, 9.010f, 857.010f },
                new[] { 3442.626f, 9.010f, 858.759f },
                new[] { 3448.847f, 9.051f, 862.537f },
                new[] { 3450.215f, 9.108f, 867.728f },
                new[] { 3444.302f, 9.033f, 868.083f },
                new[] { 3441.890f, 9.010f, 868.380f },
                new[] { 3437.906f, 9.010f, 869.041f },
                new[] { 3434.727f, 9.010f, 871.858f },
                new[] { 3438.517f, 9.010f, 877.417f },
                new[] { 3439.743f, 9.010f, 880.811f },
                new[] { 3438.977f, 9.010f, 882.045f },
                new[] { 3435.548f, 9.010f, 885.122f },
                new[] { 3433.159f, 9.010f, 887.523f },
                new[] { 3432.581f, 9.107f, 889.405f },
                new[] { 3437.235f, 9.010f, 889.476f },
                new[] { 3440.216f, 9.010f, 889.429f },
                new[] { 3443.825f, 9.035f, 889.508f },
                new[] { 3447.959f, 9.053f, 889.980f },
                new[] { 3451.550f, 9.110f, 889.622f },
                new[] { 3461.761f, 9.010f, 888.968f },
                new[] { 3466.598f, 9.010f, 889.095f },
                new[] { 3469.487f, 9.010f, 884.308f },
                new[] { 3461.424f, 9.010f, 885.247f },
                new[] { 3457.029f, 9.110f, 880.685f },
                new[] { 3456.763f, 9.110f, 877.424f },
                new[] { 3457.140f, 9.108f, 869.205f },
                // Bridge back to start — skip south z&lt;850 floor-stuck legs.
                new[] { 3445.000f, 9.080f, 860.500f },
                new[] { 3434.000f, 9.080f, 855.000f },
            };

        private static readonly NpcPatrolReplaySegment[] PatrolSegments = BuildContinuousLoop(PatrolLoopWaypoints);

        public static bool IsLeonoraDefinition(string name, int captureInstance)
        {
            return captureInstance == LeonoraCaptureInstance
                   || string.Equals(name, LeonoraName, StringComparison.OrdinalIgnoreCase);
        }

        public static void PrepareSpawnedLeonora(Character mob, NPCController controller)
        {
            if (!AreteMobDiagnosticSwitches.LeonoraMarty || mob == null || controller == null)
            {
                return;
            }

            controller.AiProfile = NpcAiProfile.Passive;
        }

        public static bool TryApplyPatrol(int captureInstance, NPCController controller)
        {
            if (!AreteMobDiagnosticSwitches.LeonoraMarty
                || controller == null
                || captureInstance != LeonoraCaptureInstance)
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
            if (controller == null || !IsLeonoraNpc(npc))
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
            if (controller == null || !IsLeonoraNpc(npc))
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

        private static bool IsLeonoraNpc(ICharacter npc)
        {
            return npc != null
                   && (npc.Identity.Instance == LeonoraCaptureInstance
                       || string.Equals(npc.Name, LeonoraName, StringComparison.OrdinalIgnoreCase));
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
