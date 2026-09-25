namespace ZoneEngine_New.Core.Movement
{
    /// <summary>
    /// Server-side movement values. The vehicle's own constants (gravity, speed curves, step height,
    /// probe sizes) live in <c>N3Lite</c>; these cover what only the server does.
    /// </summary>
    public static class MovementConfig
    {
        /// <summary><c>N3CharVehicle.BuildSim</c>: the player / NPC factory body mass.</summary>
        public const float Mass = 50f;

        /// <summary><c>N3CharVehicle.BuildSim</c>: the body radius, used as the near-probe offset.</summary>
        public const float BodyRadius = 0.5f;

        /// <summary><c>N3CharVehicle.BuildSim</c>: seed values replaced by the first <c>UpdateMotionConstraints</c>.</summary>
        public const float InitialMaxForce = 10f;
        public const float InitialMaxVel = 1f;
        public const float InitialSlowingDistance = 1.5f;

        /// <summary>Unported Lost-Eden glue: keyboard turn rates, not stock.</summary>
        public const float TurnRateRadiansStopped = 3.5f;
        public const float TurnRateRadiansMoving = 1.5f;

        /// <summary>Unported Lost-Eden glue: a player-body path waypoint counts as reached inside this XZ radius.</summary>
        public const float WaypointArrivalRadius = 0.5f;

        /// <summary>An NPC path ends once its guide is done and the body is this close (XZ) to the last waypoint.</summary>
        public const float PathArrivalRadius = 0.25f;

        public const float SpeedStopEpsilon = 0.05f;

        /// <summary>
        /// When false, NPC bodies take their steering velocity at once instead of accelerating toward it.
        /// Observers move NPCs along FollowTarget paths at full speed, so server-side acceleration only
        /// puts the server position behind the client's.
        /// </summary>
        public const bool NpcAccelerationEnabled = false;

        /// <summary>Server floor snap: probes start this far above the feet so a small rise still reads as floor.</summary>
        public const float GroundProbeLift = 0.5f;

        /// <summary>Server floor snap: floor this far below the feet still counts as support.</summary>
        public const float GroundSnapTolerance = 2.5f;

        /// <summary>Torso height the void probe starts from, above any floor the feet could be under.</summary>
        public const float VoidProbeLift = 1.8f;

        /// <summary>Probe used only to tell "standing above a drop" apart from "no geometry here at all".</summary>
        public const float VoidProbeDepth = 4096f;

        public const float LineOfSightEyeHeight = 1.6f;
    }
}
