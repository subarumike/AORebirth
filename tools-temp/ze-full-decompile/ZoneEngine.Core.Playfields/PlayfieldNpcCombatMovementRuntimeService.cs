using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Navigation;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldNpcCombatMovementRuntimeService
{
	private readonly NpcChaseNavigationRuntimeService chaseNavigation;

	private const double MaxMeleeCombatDistance = 8.0;

	private const double MaxMeleeFollowHoldDistance = 3.0;

	private const string CapturedCleaningRobotName = "Malfunctioning Cleaning Robot";

	private const int CapturedCleaningRobotMonsterData = 297023;

	private const double CapturedCleaningRobotFollowStopDistance = 0.0;

	internal ChaseNavigationCapability NavigationCapability => chaseNavigation.Capability;

	internal PlayfieldNpcCombatMovementRuntimeService(NpcChaseNavigationRuntimeService chaseNavigation)
	{
		this.chaseNavigation = chaseNavigation ?? throw new ArgumentNullException("chaseNavigation");
	}

	internal bool HasActiveNavigation(ICharacter attacker)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (attacker != null)
		{
			NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
			Identity identity = ((IEntity)attacker).Identity;
			result = (npcChaseNavigationRuntimeService.HasActivePursuit(((Identity)(ref identity)).Instance) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	internal bool IsAttackPathTraversable(ICharacter attacker, ICharacter target)
	{
		if (attacker == null || target == null)
		{
			return false;
		}
		return chaseNavigation.IsAttackPathTraversable(ToNavigationPoint(GetCombatPosition(attacker)), ToNavigationPoint(GetCombatPosition(target)));
	}

	internal void ClearNavigation(Identity identity, NpcChaseInvalidationReason reason)
	{
		chaseNavigation.Clear(((Identity)(ref identity)).Instance, reason);
	}

	internal void ClearAllNavigation(NpcChaseInvalidationReason reason)
	{
		chaseNavigation.ClearAll(reason);
	}

	internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
	{
		return GetCombatDistance(attacker, target) <= range;
	}

	internal void UpdateNpcMeleeFollowHold(ICharacter attacker, ICharacter target, double range, Action<ICharacter, Vector3> moveNpcToPosition, Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
	{
		Require(moveNpcToPosition, "moveNpcToPosition");
		Require(logNpcBrain, "logNpcBrain");
		if (!(((IDynel)attacker).Controller is NPCController nPCController))
		{
			return;
		}
		if (!CanChase(attacker))
		{
			nPCController.StopFollow();
			return;
		}
		double combatDistance = GetCombatDistance(attacker, target);
		if (combatDistance <= 3.0)
		{
			nPCController.StopFollow();
		}
		else
		{
			MoveNpcTowardCombatTarget(attacker, target, range, "melee-separated", moveNpcToPosition, logNpcBrain);
		}
	}

	internal void TryMoveNpcIntoCombatRange(ICharacter attacker, ICharacter target, double range, Action<ICharacter, Vector3> moveNpcToPosition, Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
	{
		Require(moveNpcToPosition, "moveNpcToPosition");
		Require(logNpcBrain, "logNpcBrain");
		if (((IDynel)attacker).Controller is NPCController nPCController)
		{
			if (!CanChase(attacker))
			{
				nPCController.StopFollow();
			}
			else
			{
				MoveNpcTowardCombatTarget(attacker, target, range, "out-of-range", moveNpcToPosition, logNpcBrain);
			}
		}
	}

	internal void HoldNpcAtCombatPosition(ICharacter attacker, ICharacter target)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || target == null || chaseNavigation.Capability != ChaseNavigationCapability.Supported)
		{
			return;
		}
		NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
		Identity identity = ((IEntity)attacker).Identity;
		if (npcChaseNavigationRuntimeService.HasActivePursuit(((Identity)(ref identity)).Instance))
		{
			NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService2 = chaseNavigation;
			identity = ((IEntity)attacker).Identity;
			npcChaseNavigationRuntimeService2.Clear(((Identity)(ref identity)).Instance, NpcChaseInvalidationReason.DirectPathRestored);
			if (((IDynel)attacker).Controller is NPCController nPCController && nPCController.IsFollowing())
			{
				nPCController.StopFollowForCombatRange(GetCombatPosition(target));
			}
		}
	}

	internal bool TryResolveCapturedMovementDestination(ICharacter attacker, ICharacter target, double range, DateTime utcNow, out Vector3 destination)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		destination = (Vector3)((attacker != null) ? ((object)GetCombatPosition(attacker)) : ((object)new Vector3()));
		if (attacker == null || target == null)
		{
			return false;
		}
		if (chaseNavigation.Capability == ChaseNavigationCapability.Unsupported)
		{
			destination = GetCombatPosition(target);
			return true;
		}
		NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
		Identity identity = ((IEntity)attacker).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		identity = ((IEntity)target).Identity;
		NpcChaseUpdateResult npcChaseUpdateResult = npcChaseNavigationRuntimeService.UpdatePursuit(instance, ((Identity)(ref identity)).Instance, ToNavigationPoint(GetCombatPosition(attacker)), ToNavigationPoint(GetCombatPosition(target)), BuildNpcCombatStopDistance(range), utcNow);
		if (!npcChaseUpdateResult.HasDestination)
		{
			return false;
		}
		destination = ToRuntimeVector(npcChaseUpdateResult.Destination);
		return true;
	}

	internal static double GetCombatDistance(ICharacter attacker, ICharacter target)
	{
		return GetCombatPosition(attacker).Distance2D(GetCombatPosition(target));
	}

	private static bool CanChase(ICharacter attacker)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)attacker).Identity;
		OrdinaryEnemyRuntimeDefinition definition;
		return !OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out definition) || definition.Profile.Aggression.Chase;
	}

	internal static bool IsCapturedCleaningRobot(ICharacter character)
	{
		return character != null && string.Equals(((INamedEntity)character).Name, "Malfunctioning Cleaning Robot", StringComparison.OrdinalIgnoreCase) && ((IStats)character).Stats[(StatIds)359].Value == 297023;
	}

	private static Vector3 GetCombatPosition(ICharacter character)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		if (((IDynel)character).Controller is PlayerController)
		{
			Vector3 rawCoordinates = ((IDynel)character).RawCoordinates;
			Vector3 start = new Vector3((double)rawCoordinates.X, (double)rawCoordinates.Y, (double)rawCoordinates.Z);
			Vector3 coordinate = ((IDynel)character).Coordinates().coordinate;
			return MoveCombatPositionToward(start, coordinate, 3.0);
		}
		return ((IDynel)character).Coordinates().coordinate;
	}

	private static Vector3 MoveCombatPositionToward(Vector3 start, Vector3 destination, double maxDistance)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		double num = start.Distance2D(destination);
		if (num < 0.001 || maxDistance <= 0.0)
		{
			return new Vector3(start.x, start.y, start.z);
		}
		double num2 = Math.Min(num, maxDistance);
		double num3 = num2 / num;
		return new Vector3(start.x + (destination.x - start.x) * num3, start.y + (destination.y - start.y) * num3, start.z + (destination.z - start.z) * num3);
	}

	private static double BuildNpcCombatStopDistance(double range)
	{
		return (range > 8.0) ? range : 3.0;
	}

	private void MoveNpcTowardCombatTarget(ICharacter attacker, ICharacter target, double range, string reason, Action<ICharacter, Vector3> moveNpcToPosition, Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Expected O, but got Unknown
		if (!(((IDynel)attacker).Controller is NPCController nPCController))
		{
			return;
		}
		Vector3 combatPosition = GetCombatPosition(attacker);
		Vector3 combatPosition2 = GetCombatPosition(target);
		double stopDistance = (IsCapturedCleaningRobot(attacker) ? 0.0 : BuildNpcCombatStopDistance(range));
		double arg = combatPosition.Distance2D(combatPosition2);
		NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
		Identity identity = ((IEntity)attacker).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		identity = ((IEntity)target).Identity;
		NpcChaseUpdateResult npcChaseUpdateResult = npcChaseNavigationRuntimeService.UpdatePursuit(instance, ((Identity)(ref identity)).Instance, ToNavigationPoint(combatPosition), ToNavigationPoint(combatPosition2), stopDistance, DateTime.UtcNow);
		if (npcChaseUpdateResult.Kind == NpcChaseMovementKind.Unavailable)
		{
			nPCController.SnapshotCurrentMotionPosition();
			nPCController.StopFollow();
			logNpcBrain("ChaseNavigationHold", "navigation-unavailable", attacker, target, range, arg);
		}
		else if (npcChaseUpdateResult.Kind != 0)
		{
			if (npcChaseUpdateResult.Kind == NpcChaseMovementKind.Hold)
			{
				NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService2 = chaseNavigation;
				identity = ((IEntity)attacker).Identity;
				if (!npcChaseNavigationRuntimeService2.HasActivePursuit(((Identity)(ref identity)).Instance))
				{
					nPCController.SnapshotCurrentMotionPosition();
					nPCController.StopFollow();
				}
			}
			else if (npcChaseUpdateResult.HasDestination && (npcChaseUpdateResult.ShouldIssueMovement || !nPCController.IsFollowing()))
			{
				bool flag = nPCController.IsFollowing();
				Vector3 combatPosition3 = GetCombatPosition(attacker);
				if (!flag)
				{
					moveNpcToPosition(attacker, combatPosition3);
				}
				Vector3 val = ToRuntimeVector(npcChaseUpdateResult.Destination);
				nPCController.MoveTo(new Vector3
				{
					X = val.xf,
					Y = val.yf,
					Z = val.zf
				});
				logNpcBrain((npcChaseUpdateResult.Kind == NpcChaseMovementKind.Route) ? "ChaseRouteSegment" : "ChaseDirectSegment", reason, attacker, target, range, arg);
			}
		}
		else if (!nPCController.IsFollowing(((IEntity)target).Identity))
		{
			moveNpcToPosition(attacker, combatPosition);
			nPCController.Follow(((IEntity)target).Identity, stopDistance);
			logNpcBrain("FollowTargetStart", reason, attacker, target, range, arg);
		}
		else
		{
			logNpcBrain("FollowTargetContinue", reason, attacker, target, range, arg);
		}
	}

	private static ChaseNavigationPoint ToNavigationPoint(Vector3 point)
	{
		return new ChaseNavigationPoint(point.x, point.y, point.z);
	}

	private static Vector3 ToRuntimeVector(ChaseNavigationPoint point)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		return new Vector3(point.X, point.Y, point.Z);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
