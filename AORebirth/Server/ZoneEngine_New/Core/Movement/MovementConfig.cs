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
        public const float GroundCheckLift = 4f;
        public const float GroundCheckDepth = 64f;
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
