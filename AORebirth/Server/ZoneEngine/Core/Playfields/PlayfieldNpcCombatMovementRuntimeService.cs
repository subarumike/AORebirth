namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    #endregion

    internal sealed class PlayfieldNpcCombatMovementRuntimeService
    {
        private const double MaxMeleeCombatDistance = NpcCombatAttackRules.MaxMeleeCombatDistance;

        private const double MaxMeleeFollowHoldDistance = 3.0;

        private const double MinNpcCombatMoveDistance = 0.3;

        private const string CapturedCleaningRobotName = "Malfunctioning Cleaning Robot";

        private const int CapturedCleaningRobotMonsterData = 297023;

        private const double CapturedCleaningRobotFollowStopDistance = 0.0;

        private const double OutOfRangeRetrySeconds = NpcCombatAttackRules.OutOfRangeRetrySeconds;

        internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            return GetCombatDistance(attacker, target) <= range;
        }

        internal void UpdateNpcMeleeFollowHold(
            ICharacter attacker,
            ICharacter target,
            double range,
            Action<ICharacter, AORebirth.Core.Vector.Vector3> moveNpcToPosition,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            Require(moveNpcToPosition, "moveNpcToPosition");
            Require(logNpcBrain, "logNpcBrain");

            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            double distance = GetCombatDistance(attacker, target);
            if (distance <= MaxMeleeFollowHoldDistance)
            {
                npcController.StopFollow();
                return;
            }

            this.MoveNpcTowardCombatTarget(
                attacker,
                target,
                range,
                "melee-separated",
                moveNpcToPosition,
                logNpcBrain);
        }

        internal void TryMoveNpcIntoCombatRange(
            ICharacter attacker,
            ICharacter target,
            double range,
            Action<ICharacter, AORebirth.Core.Vector.Vector3> moveNpcToPosition,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            Require(moveNpcToPosition, "moveNpcToPosition");
            Require(logNpcBrain, "logNpcBrain");

            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            this.MoveNpcTowardCombatTarget(
                attacker,
                target,
                range,
                "out-of-range",
                moveNpcToPosition,
                logNpcBrain);
        }

        internal static double GetCombatDistance(ICharacter attacker, ICharacter target)
        {
            return GetCombatPosition(attacker).Distance2D(GetCombatPosition(target));
        }

        internal static bool IsCapturedCleaningRobot(ICharacter character)
        {
            return character != null
                   && string.Equals(character.Name, CapturedCleaningRobotName, StringComparison.OrdinalIgnoreCase)
                   && character.Stats[StatIds.monsterdata].Value == CapturedCleaningRobotMonsterData;
        }

        private static AORebirth.Core.Vector.Vector3 GetCombatPosition(ICharacter character)
        {
            if (character.Controller is PlayerController)
            {
                Vector3 raw = character.RawCoordinates;
                AORebirth.Core.Vector.Vector3 rawPosition =
                    new AORebirth.Core.Vector.Vector3(raw.X, raw.Y, raw.Z);
                AORebirth.Core.Vector.Vector3 predictedPosition = character.Coordinates().coordinate;
                return MoveCombatPositionToward(
                    rawPosition,
                    predictedPosition,
                    EnemyBehaviorContract.MaxPlayerChaseProjectionDistance);
            }

            return character.Coordinates().coordinate;
        }

        private static AORebirth.Core.Vector.Vector3 MoveCombatPositionToward(
            AORebirth.Core.Vector.Vector3 start,
            AORebirth.Core.Vector.Vector3 destination,
            double maxDistance)
        {
            double distance = start.Distance2D(destination);
            if (distance < 0.001 || maxDistance <= 0)
            {
                return new AORebirth.Core.Vector.Vector3(start.x, start.y, start.z);
            }

            double step = Math.Min(distance, maxDistance);
            double factor = step / distance;
            return new AORebirth.Core.Vector.Vector3(
                start.x + ((destination.x - start.x) * factor),
                start.y + ((destination.y - start.y) * factor),
                start.z + ((destination.z - start.z) * factor));
        }

        private static double BuildNpcCombatStopDistance(double range)
        {
            return range > MaxMeleeCombatDistance ? range : MaxMeleeFollowHoldDistance;
        }

        private void MoveNpcTowardCombatTarget(
            ICharacter attacker,
            ICharacter target,
            double range,
            string reason,
            Action<ICharacter, AORebirth.Core.Vector.Vector3> moveNpcToPosition,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            if (IsCapturedCleaningRobot(attacker))
            {
                this.MoveCapturedCleaningRobotTowardCombatTarget(attacker, target, range, reason, npcController, logNpcBrain);
                return;
            }

            npcController.StopFollow();

            AORebirth.Core.Vector.Vector3 attackerPosition = GetCombatPosition(attacker);
            AORebirth.Core.Vector.Vector3 targetPosition = GetCombatPosition(target);
            double stopDistance = BuildNpcCombatStopDistance(range);
            double distance = attackerPosition.Distance2D(targetPosition);
            double travelDistance = Math.Min(
                EnemyBehaviorContract.MaxNpcFollowSpeedPerSecond * OutOfRangeRetrySeconds,
                Math.Max(0.0, distance - stopDistance));

            if (travelDistance < MinNpcCombatMoveDistance)
            {
                return;
            }

            AORebirth.Core.Vector.Vector3 nextPosition =
                MoveCombatPositionToward(attackerPosition, targetPosition, travelDistance);

            moveNpcToPosition(attacker, nextPosition);
            logNpcBrain("Chasing", reason, attacker, target, range, distance);
        }

        private void MoveCapturedCleaningRobotTowardCombatTarget(
            ICharacter attacker,
            ICharacter target,
            double range,
            string reason,
            NPCController npcController,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            AORebirth.Core.Vector.Vector3 attackerPosition = GetCombatPosition(attacker);
            AORebirth.Core.Vector.Vector3 targetPosition = GetCombatPosition(target);
            double stopDistance = CapturedCleaningRobotFollowStopDistance;
            double distance = attackerPosition.Distance2D(targetPosition);

            if (!npcController.IsFollowing(target.Identity))
            {
                npcController.Follow(target.Identity, stopDistance);
                logNpcBrain("FollowTargetStart", reason, attacker, target, range, distance);
                return;
            }

            logNpcBrain("FollowTargetContinue", reason, attacker, target, range, distance);
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
