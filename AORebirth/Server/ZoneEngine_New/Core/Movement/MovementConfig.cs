namespace ZoneEngine_New.Core.Movement
{
    /// <summary>Lost Eden MovementConfig numeric defaults (no ScriptableObject).</summary>
    public static class MovementConfig
    {
        public const float Mass = 50f;
        public const float ForceReachTime = 0.5f;
        public const float Gravity = -20f;
        public const float GroundStickVelocity = -2f;
        public const float TerminalVelocity = 50f;
        public const float SpeedStopEpsilon = 0.05f;
        public const float WaypointArrivalRadius = 0.5f;
        public const float WalkBaseVelocity = 1.5f;
        public const float TurnRateRadiansStopped = 3.5f;
        public const float TurnRateRadiansMoving = 1.5f;
        public const float PathTurnRateDegrees = 500f;
        public const float JumpStatCap = 800f;
        public const float JumpHeightPerStatPool = 200f;
        public const float JumpHeightBase = 1f;
        public const float JumpHeightFloor = 0.5f;
        public const float HealthPenalty = 0.15f;
        public const float StatOffset = 1000f;
        public const float CapsuleRadius = 0.4f;
        public const float CapsuleHalfHeight = 0.9f;

        /// <summary>
        /// Obstacles shorter than this are stepped over instead of blocking. Movement sweeps ignore
        /// everything below it, which is also what keeps a character resting on a slope from
        /// intersecting the terrain it is standing on and freezing in place.
        /// </summary>
        public const float StepHeight = 0.5f;

        /// <summary>
        /// Distance from the feet to the sweep capsule's center. Character positions are foot-level,
        /// so the capsule has to be lifted clear of the floor or every sweep reports zero travel.
        /// </summary>
        public const float CapsuleCenterLift = CapsuleRadius + CapsuleHalfHeight + StepHeight;

        /// <summary>Sweeps stop this far short of the contact so the capsule never rests inside geometry.</summary>
        public const float SweepSkin = 0.02f;

        /// <summary>Ground probes start a step above the feet so a small rise still reads as walkable floor.</summary>
        public const float GroundProbeLift = StepHeight;

        /// <summary>Ground found within this distance below the feet counts as support; past it the character is airborne.</summary>
        public const float GroundSnapTolerance = 2.5f;

        /// <summary>Probe used only to tell "standing above a drop" apart from "no geometry here at all".</summary>
        public const float VoidProbeDepth = 4096f;

        public const float LineOfSightEyeHeight = 1.6f;

        public const float RunForwardSlope = 1f / 275f;
        public const float RunForwardBase = 5f;
        public const float RunForwardMin = 1.5f;
        public const float RunForwardMax = 13f;

        public const float RunBackSlope = 0.0025454545f;
        public const float RunBackBase = 3f;
        public const float RunBackMin = 1.05f;
        public const float RunBackMax = 9.1f;

        public const float RunStrafeBase = 2.5f;
        public const float RunStrafeSlope = 0.5f / 275f;
        public const float RunStrafeMin = 0.75f;
        public const float RunStrafeMax = 6.5f;
    }
}
