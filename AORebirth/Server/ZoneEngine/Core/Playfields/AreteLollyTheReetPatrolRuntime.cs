namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture 20260726-150800: Lolly the Reet FollowTarget NpcPath loop
    /// (live SimpleChar:799A348D; server spawn uses fixed 7985CAEC).
    /// Full oasis roam — destinations form a closed loop (~93s capture lap).
    /// </summary>
    internal static class AreteLollyTheReetPatrolRuntime
    {
        internal const string LollyName = "Lolly the Reet";

        private const int LollyFixedInstance = unchecked((int)0x7985CAEC);

        // Matches NPCController.WalkFollowSpeedPerSecond.
        private const double WalkSpeedPerSecond = 1.5;

        // Fire next FollowTarget before the walk finishes so direction changes
        // without a full stop at the corner.
        private const double EarlyTurnFactor = 0.85;

        // Capture 20260726-150800 NpcPath destinations (closed loop; final
        // duplicate of first destination omitted — BuildContinuousLoop closes it).
        private static readonly float[][] PatrolLoopWaypoints =
            {
                new[] { 3394.019f, 2.110f, 563.814f },
                new[] { 3389.094f, 2.110f, 574.690f },
                new[] { 3380.191f, 2.110f, 586.305f },
                new[] { 3365.323f, 2.110f, 606.774f },
                new[] { 3359.935f, 3.575f, 620.666f },
                new[] { 3358.177f, 3.610f, 640.515f },
                new[] { 3357.214f, 3.238f, 667.747f },
                new[] { 3358.525f, 3.121f, 689.946f },
                new[] { 3352.972f, 6.736f, 707.575f },
                new[] { 3347.177f, 8.685f, 707.688f },
                new[] { 3325.106f, 2.514f, 718.390f },
                new[] { 3308.655f, 0.834f, 717.823f },
                new[] { 3289.000f, 4.508f, 716.995f },
                new[] { 3287.007f, 4.568f, 700.197f },
                new[] { 3293.097f, 2.261f, 691.497f },
                new[] { 3308.740f, 0.010f, 677.322f },
                new[] { 3295.716f, 0.010f, 665.360f },
                new[] { 3306.716f, 0.010f, 658.161f },
                new[] { 3327.757f, 0.010f, 638.225f },
                new[] { 3326.984f, 2.074f, 629.648f },
                new[] { 3332.558f, 1.435f, 625.050f },
                new[] { 3335.060f, 0.240f, 611.099f },
                new[] { 3335.830f, 0.297f, 610.446f },
                new[] { 3346.712f, 3.391f, 606.238f },
                new[] { 3354.315f, 2.110f, 594.019f },
                new[] { 3362.068f, 2.110f, 583.905f },
                new[] { 3370.319f, 2.280f, 565.864f },
                new[] { 3379.428f, 2.862f, 554.990f },
                new[] { 3390.616f, 2.110f, 555.187f },
            };

        private static readonly NpcPatrolReplaySegment[] PatrolSegments = BuildContinuousLoop(PatrolLoopWaypoints);

        public static bool IsLollyNpc(ICharacter npc)
        {
            return npc != null
                   && (npc.Identity.Instance == LollyFixedInstance
                       || string.Equals(npc.Name, LollyName, StringComparison.OrdinalIgnoreCase)
                       || (npc.Name != null
                           && npc.Name.IndexOf("Lolly", StringComparison.OrdinalIgnoreCase) >= 0
                           && npc.Name.IndexOf("Reet", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public static void ApplyPatrol(NPCController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.SetCapturedPatrolReplaySegments(
                PatrolSegments,
                true,
                false,
                false);
            controller.State = CharacterState.Patrolling;
        }

        public static void PauseForDialogue(ICharacter npc)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller == null || !IsLollyNpc(npc))
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
            if (controller == null || !IsLollyNpc(npc))
            {
                return;
            }

            ApplyPatrol(controller);
            controller.StartPatrolling();
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
