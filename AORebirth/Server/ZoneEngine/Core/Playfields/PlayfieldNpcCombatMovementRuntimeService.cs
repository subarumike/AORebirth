namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Navigation;

    #endregion

    internal sealed class PlayfieldNpcCombatMovementRuntimeService
    {
        private readonly NpcChaseNavigationRuntimeService chaseNavigation;

        private const int CapturedCleaningRobotMonsterData = 297023;

        private const double CapturedCleaningRobotFollowStopDistance = 0.0;

        internal PlayfieldNpcCombatMovementRuntimeService(
            NpcChaseNavigationRuntimeService chaseNavigation)
        {
            this.chaseNavigation = chaseNavigation
                                   ?? throw new ArgumentNullException("chaseNavigation");
        }

        internal ChaseNavigationCapability NavigationCapability
        {
            get { return this.chaseNavigation.Capability; }
        }

        internal bool HasActiveNavigation(ICharacter attacker)
        {
            return attacker != null
                   && this.chaseNavigation.HasActivePursuit(attacker.Identity.Instance);
        }

        internal bool IsAttackPathTraversable(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            return this.chaseNavigation.IsAttackPathTraversable(
                ToNavigationPoint(GetCombatPosition(attacker)),
                ToNavigationPoint(GetCombatPosition(target)));
        }

        internal void ClearNavigation(Identity identity, NpcChaseInvalidationReason reason)
        {
            this.chaseNavigation.Clear(identity.Instance, reason);
        }

        internal void ClearAllNavigation(NpcChaseInvalidationReason reason)
        {
            this.chaseNavigation.ClearAll(reason);
        }

        internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            return NpcCombatSpatialPolicy.IsWithinAttackEnvelope(
                GetCombatDistance(attacker, target),
                range);
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

            string missionStationaryReason;
            if (ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.RequiresStationaryNpc(
                attacker,
                target,
                out missionStationaryReason))
            {
                this.chaseNavigation.Clear(
                    attacker.Identity.Instance,
                    NpcChaseInvalidationReason.RouteSegmentInvalid);
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
                return;
            }

            if (!CanChase(attacker))
            {
                npcController.StopFollow();
                return;
            }

            double distance = GetCombatDistance(attacker, target);
            if (NpcCombatSpatialPolicy.ShouldHoldMeleeFollow(distance, range))
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

            string missionStationaryReason;
            if (ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.RequiresStationaryNpc(
                attacker,
                target,
                out missionStationaryReason))
            {
                this.chaseNavigation.Clear(
                    attacker.Identity.Instance,
                    NpcChaseInvalidationReason.RouteSegmentInvalid);
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
                return;
            }

            if (!CanChase(attacker))
            {
                npcController.StopFollow();
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

        internal void HoldNpcAtCombatPosition(ICharacter attacker, ICharacter target)
        {
            if (attacker == null
                || target == null
                || this.chaseNavigation.Capability != ChaseNavigationCapability.Supported
                || !this.chaseNavigation.HasActivePursuit(attacker.Identity.Instance))
            {
                return;
            }

            this.chaseNavigation.Clear(
                attacker.Identity.Instance,
                NpcChaseInvalidationReason.DirectPathRestored);
            NPCController controller = attacker.Controller as NPCController;
            if (controller != null && controller.IsFollowing())
            {
                controller.StopFollowForCombatRange(GetCombatPosition(target));
            }
        }

        internal bool TryResolveCapturedMovementDestination(
            ICharacter attacker,
            ICharacter target,
            double range,
            DateTime utcNow,
            out AORebirth.Core.Vector.Vector3 destination)
        {
            destination = attacker == null
                              ? new AORebirth.Core.Vector.Vector3()
                              : GetCombatPosition(attacker);
            if (attacker == null || target == null)
            {
                return false;
            }

            string missionStationaryReason;
            if (ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.RequiresStationaryNpc(
                attacker,
                target,
                out missionStationaryReason))
            {
                return false;
            }

            if (this.chaseNavigation.Capability == ChaseNavigationCapability.Unsupported)
            {
                destination = GetCombatPosition(target);
                return true;
            }

            NpcChaseUpdateResult result = this.chaseNavigation.UpdatePursuit(
                attacker.Identity.Instance,
                target.Identity.Instance,
                ToNavigationPoint(GetCombatPosition(attacker)),
                ToNavigationPoint(GetCombatPosition(target)),
                BuildNpcCombatStopDistance(range),
                utcNow);
            if (!result.HasDestination)
            {
                return false;
            }

            destination = ToRuntimeVector(result.Destination);
            return true;
        }

        internal static double GetCombatDistance(ICharacter attacker, ICharacter target)
        {
            return GetCombatPosition(attacker).Distance2D(GetCombatPosition(target));
        }

        private static bool CanChase(ICharacter attacker)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            return !OrdinaryEnemyRuntimeRegistry.TryGet(attacker.Identity.Instance, out definition)
                   || definition.Profile.Aggression.Chase;
        }

        internal static bool IsCapturedCleaningRobot(ICharacter character)
        {
            if (character == null
                || character.Stats[StatIds.monsterdata].Value != CapturedCleaningRobotMonsterData)
            {
                return false;
            }

            string name = character.Name ?? string.Empty;
            return name.IndexOf("Cleaning Robot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AORebirth.Core.Vector.Vector3 GetCombatPosition(ICharacter character)
        {
            if (character.Controller is PlayerController)
            {
                Vector3 raw = character.Position;
                AORebirth.Core.Vector.Vector3 rawPosition =
                    new AORebirth.Core.Vector.Vector3(raw.X, raw.Y, raw.Z);
                AORebirth.Core.Vector.Vector3 predictedPosition = character.CalculatePredictedPosition().coordinate;
                return MoveCombatPositionToward(
                    rawPosition,
                    predictedPosition,
                    EnemyBehaviorContract.MaxPlayerChaseProjectionDistance);
            }

            return character.CalculatePredictedPosition().coordinate;
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
            return NpcCombatSpatialPolicy.BuildPursuitStopDistance(range);
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

            AORebirth.Core.Vector.Vector3 attackerPosition = GetCombatPosition(attacker);
            AORebirth.Core.Vector.Vector3 targetPosition = GetCombatPosition(target);
            double stopDistance = IsCapturedCleaningRobot(attacker)
                                      ? CapturedCleaningRobotFollowStopDistance
                                      : BuildNpcCombatStopDistance(range);
            double distance = attackerPosition.Distance2D(targetPosition);

            NpcChaseUpdateResult navigationResult = this.chaseNavigation.UpdatePursuit(
                attacker.Identity.Instance,
                target.Identity.Instance,
                ToNavigationPoint(attackerPosition),
                ToNavigationPoint(targetPosition),
                stopDistance,
                DateTime.UtcNow);
            if (navigationResult.Kind == NpcChaseMovementKind.Unavailable)
            {
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
                logNpcBrain("ChaseNavigationHold", "navigation-unavailable", attacker, target, range, distance);
                return;
            }

            if (navigationResult.Kind != NpcChaseMovementKind.Unsupported)
            {
                if (navigationResult.Kind == NpcChaseMovementKind.Hold)
                {
                    if (!this.chaseNavigation.HasActivePursuit(attacker.Identity.Instance))
                    {
                        npcController.SnapshotCurrentMotionPosition();
                        npcController.StopFollow();
                    }

                    return;
                }

                if (navigationResult.HasDestination
                    && (navigationResult.ShouldIssueMovement || !npcController.IsFollowing()))
                {
                    bool wasFollowing = npcController.IsFollowing();
                    AORebirth.Core.Vector.Vector3 current = GetCombatPosition(attacker);
                    if (!wasFollowing)
                    {
                        moveNpcToPosition(attacker, current);
                    }

                    AORebirth.Core.Vector.Vector3 destination =
                        ToRuntimeVector(navigationResult.Destination);
                    npcController.MoveTo(
                        new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                        {
                            X = destination.xf,
                            Y = destination.yf,
                            Z = destination.zf
                        });
                    logNpcBrain(
                        navigationResult.Kind == NpcChaseMovementKind.Route
                            ? "ChaseRouteSegment"
                            : "ChaseDirectSegment",
                        reason,
                        attacker,
                        target,
                        range,
                        distance);
                }

                return;
            }

            if (!npcController.IsFollowing(target.Identity))
            {
                // Live NPC combat starts with an authoritative current-position SetPos,
                // immediately followed by continuous run-mode NpcPath updates.
                moveNpcToPosition(attacker, attackerPosition);
                npcController.Follow(target.Identity, stopDistance);
                logNpcBrain("FollowTargetStart", reason, attacker, target, range, distance);
                return;
            }

            logNpcBrain("FollowTargetContinue", reason, attacker, target, range, distance);
        }

        private static ChaseNavigationPoint ToNavigationPoint(
            AORebirth.Core.Vector.Vector3 point)
        {
            return new ChaseNavigationPoint(point.x, point.y, point.z);
        }

        private static AORebirth.Core.Vector.Vector3 ToRuntimeVector(ChaseNavigationPoint point)
        {
            return new AORebirth.Core.Vector.Vector3(point.X, point.Y, point.Z);
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
