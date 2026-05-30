namespace CellAO.Tools.EnemyMovementReplay
{
    using System;
    using System.Collections.Generic;

    public enum ReplayEnemyState
    {
        Idle,
        Aggroed,
        Chasing,
        MeleeAttackingWhileMoving,
        RangedStopAndAttack
    }

    public enum ReplayEnemyAction
    {
        None,
        AddThreat,
        FollowCoordinatePath,
        AttackWithoutStop,
        StopAndAttack,
        PreserveAggro,
        CapturedCorrectionSequence,
        ClearTarget
    }

    public sealed class ReplayDecision
    {
        public ReplayDecision(ReplayEnemyState state, ReplayEnemyAction action, string reason)
        {
            this.State = state;
            this.Action = action;
            this.Reason = reason;
        }

        public ReplayEnemyState State { get; private set; }

        public ReplayEnemyAction Action { get; private set; }

        public string Reason { get; private set; }
    }

    public sealed class EnemyMovementReplayContract
    {
        private readonly Dictionary<string, ReplayEnemyState> states =
            new Dictionary<string, ReplayEnemyState>(StringComparer.OrdinalIgnoreCase);

        public ReplayDecision Apply(
            string key,
            double distance,
            double attackRange,
            string attackKind,
            string eventName)
        {
            ReplayEnemyState current = this.GetState(key);
            string normalizedEvent = Normalize(eventName);
            string normalizedAttackKind = Normalize(attackKind);

            ReplayDecision decision;
            if (normalizedEvent == "add_threat")
            {
                decision = new ReplayDecision(ReplayEnemyState.Aggroed, ReplayEnemyAction.AddThreat, "threat-added");
            }
            else if ((normalizedEvent == "target_invalid") || (normalizedEvent == "death") ||
                     (normalizedEvent == "zoned") || (normalizedEvent == "wipe_hatelist"))
            {
                decision = new ReplayDecision(ReplayEnemyState.Idle, ReplayEnemyAction.ClearTarget, "target-cleared");
            }
            else if (normalizedEvent == "player_stopfight")
            {
                decision = new ReplayDecision(current, ReplayEnemyAction.PreserveAggro, "player-stopfight-does-not-clear-npc");
            }
            else if (normalizedEvent == "hard_correction")
            {
                decision = new ReplayDecision(current, ReplayEnemyAction.CapturedCorrectionSequence, "captured-correction-preserves-chase");
            }
            else if (normalizedEvent == "coordinate_follow")
            {
                decision = new ReplayDecision(ReplayEnemyState.Chasing, ReplayEnemyAction.FollowCoordinatePath, "captured-coordinate-follow");
            }
            else if ((normalizedEvent == "attack_info") || (normalizedEvent == "missed_attack_info") ||
                     (normalizedEvent == "health_damage"))
            {
                if (normalizedAttackKind == "melee")
                {
                    decision = new ReplayDecision(
                        ReplayEnemyState.MeleeAttackingWhileMoving,
                        ReplayEnemyAction.AttackWithoutStop,
                        "captured-melee-result");
                }
                else
                {
                    decision = new ReplayDecision(
                        ReplayEnemyState.RangedStopAndAttack,
                        ReplayEnemyAction.StopAndAttack,
                        "captured-ranged-result");
                }
            }
            else if (distance > attackRange)
            {
                decision = new ReplayDecision(ReplayEnemyState.Chasing, ReplayEnemyAction.FollowCoordinatePath, "target-out-of-range");
            }
            else if (normalizedAttackKind == "melee")
            {
                decision = new ReplayDecision(ReplayEnemyState.MeleeAttackingWhileMoving, ReplayEnemyAction.AttackWithoutStop, "melee-can-attack-while-moving");
            }
            else
            {
                decision = new ReplayDecision(ReplayEnemyState.RangedStopAndAttack, ReplayEnemyAction.StopAndAttack, "ranged-requires-stop");
            }

            this.states[key] = decision.State;
            return decision;
        }

        private ReplayEnemyState GetState(string key)
        {
            ReplayEnemyState current;
            if (this.states.TryGetValue(key ?? string.Empty, out current))
            {
                return current;
            }

            return ReplayEnemyState.Idle;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
