namespace ZoneEngine.Core
{
    public enum EnemyBehaviorState
    {
        Idle,
        Aggroed,
        Chasing,
        InRangeAttacking,
        Returning,
        Dead
    }

    public enum EnemyBehaviorSignal
    {
        AddThreat,
        TargetFollow,
        TargetOutOfRange,
        CoordinateFollowTarget,
        TargetInRange,
        AttackInfo,
        MissedAttackInfo,
        HealthDamage,
        StopFightFromPlayer,
        StopFightFromNpc,
        DeathAction,
        WipeHatelist,
        TargetInvalidOrZoned,
        ScriptedReset,
        ResetArrived,
        HardCorrection
    }

    public struct EnemyBehaviorTransition
    {
        public EnemyBehaviorTransition(EnemyBehaviorState state, string reason)
        {
            this.State = state;
            this.Reason = reason;
        }

        public EnemyBehaviorState State { get; private set; }

        public string Reason { get; private set; }
    }

    public static class EnemyBehaviorContract
    {
        public const int CharDcMoveIirKey = 0x54111123;
        public const int RelocateDynelsIirKey = 0x264B514B;
        public const int DropDynelIirKey = 0x47483633;
        public const int NpcMovementActionSpellId = 53191;
        public const int NpcWipeHatelistSpellId = 53126;

        public const byte OfficialFollowTargetUnknown = 0;
        public const byte CoordinateFollowInfoType = 1;
        public const byte CorrectionFollowInfoType = 2;
        public const byte FollowStopMoveType = 21;
        public const byte WalkMoveMode = 24;
        public const byte RunMoveMode = 25;
        public const byte CoordinateFollowPointCount = 2;

        public const double MaxNpcFollowSpeedPerSecond = 6.0;
        public const int NpcRunSpeedForMaxFollowSpeed = 400;
        public const double MaxPlayerChaseProjectionDistance = 3.0;
        public static EnemyBehaviorTransition Apply(EnemyBehaviorState current, EnemyBehaviorSignal signal)
        {
            switch (signal)
            {
                case EnemyBehaviorSignal.AddThreat:
                    return current == EnemyBehaviorState.Idle
                               ? new EnemyBehaviorTransition(EnemyBehaviorState.Aggroed, "threat-added")
                               : new EnemyBehaviorTransition(current, "threat-kept");

                case EnemyBehaviorSignal.TargetFollow:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Chasing, "target-follow");

                case EnemyBehaviorSignal.TargetOutOfRange:
                case EnemyBehaviorSignal.CoordinateFollowTarget:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Chasing, "coordinate-follow");

                case EnemyBehaviorSignal.TargetInRange:
                case EnemyBehaviorSignal.AttackInfo:
                case EnemyBehaviorSignal.MissedAttackInfo:
                case EnemyBehaviorSignal.HealthDamage:
                    return current == EnemyBehaviorState.Dead
                               ? new EnemyBehaviorTransition(current, "dead")
                               : new EnemyBehaviorTransition(EnemyBehaviorState.InRangeAttacking, "combat-result");

                case EnemyBehaviorSignal.StopFightFromPlayer:
                case EnemyBehaviorSignal.HardCorrection:
                    return new EnemyBehaviorTransition(current, "state-preserved");

                case EnemyBehaviorSignal.StopFightFromNpc:
                case EnemyBehaviorSignal.WipeHatelist:
                case EnemyBehaviorSignal.TargetInvalidOrZoned:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Idle, "target-cleared");

                case EnemyBehaviorSignal.ScriptedReset:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Returning, "scripted-reset");

                case EnemyBehaviorSignal.ResetArrived:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Idle, "reset-arrived");

                case EnemyBehaviorSignal.DeathAction:
                    return new EnemyBehaviorTransition(EnemyBehaviorState.Dead, "death-action");

                default:
                    return new EnemyBehaviorTransition(current, "unknown");
            }
        }
    }
}
