using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayerCombatRuntimeService
{
	internal void StartAttack(ICharacter character, Identity target, Action<Identity> resetCombatTick)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Require(resetCombatTick, "resetCombatTick");
		((ITargetingEntity)character).SetTarget(target);
		((ITargetingEntity)character).SetFightingTarget(target);
		resetCombatTick(((IEntity)character).Identity);
	}

	internal void CancelAttack(ICharacter character, Action<Identity> resetCombatTick)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		Require(resetCombatTick, "resetCombatTick");
		((ITargetingEntity)character).SetFightingTarget(Identity.None);
		resetCombatTick(((IEntity)character).Identity);
	}

	internal void ResetCombatTick(Identity attacker, Action<Identity> resetCombatTick)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		Require(resetCombatTick, "resetCombatTick");
		resetCombatTick(attacker);
	}

	internal void ProcessCombatTick(ICharacter attacker, Action<Identity> clearCombatTracking, Func<Identity, ICharacter> findTarget, Func<ICharacter, bool> isValidTarget, Action<ICharacter, ICharacter> logInvalidTarget, Action<ICharacter, ICharacter> processValidatedCombatTick)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		Require(clearCombatTracking, "clearCombatTracking");
		Require(findTarget, "findTarget");
		Require(isValidTarget, "isValidTarget");
		Require(logInvalidTarget, "logInvalidTarget");
		Require(processValidatedCombatTick, "processValidatedCombatTick");
		Identity fightingTarget = ((ITargetingEntity)attacker).FightingTarget;
		if (((Identity)(ref fightingTarget)).Instance == 0)
		{
			clearCombatTracking(((IEntity)attacker).Identity);
			return;
		}
		ICharacter val = findTarget(((ITargetingEntity)attacker).FightingTarget);
		if (!isValidTarget(val))
		{
			ClearInvalidCombatTarget(attacker, val, logInvalidTarget, clearCombatTracking);
		}
		else
		{
			processValidatedCombatTick(attacker, val);
		}
	}

	internal void ClearInvalidCombatTarget(ICharacter attacker, ICharacter target, Action<ICharacter, ICharacter> logInvalidTarget, Action<Identity> clearCombatTracking)
	{
		Require(logInvalidTarget, "logInvalidTarget");
		Require(clearCombatTracking, "clearCombatTracking");
		logInvalidTarget(attacker, target);
		ClearFightingTarget(attacker, clearCombatTracking);
	}

	internal void ClearFightingTarget(ICharacter character, Action<Identity> clearCombatTracking)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		Require(clearCombatTracking, "clearCombatTracking");
		((ITargetingEntity)character).SetFightingTarget(Identity.None);
		clearCombatTracking(((IEntity)character).Identity);
	}

	internal void BeginDeath(ICharacter target, Action<ICharacter> beginDeath)
	{
		Require(beginDeath, "beginDeath");
		beginDeath(target);
	}

	internal void CleanupDeathCombat(ICharacter target, Action<Identity> clearCombatTracking, Action<Identity> stopFightingDeadTarget, Action<ICharacter> sendCombatStop)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		Require(clearCombatTracking, "clearCombatTracking");
		Require(stopFightingDeadTarget, "stopFightingDeadTarget");
		Require(sendCombatStop, "sendCombatStop");
		((ITargetingEntity)target).SetTarget(Identity.None);
		ClearFightingTarget(target, clearCombatTracking);
		stopFightingDeadTarget(((IEntity)target).Identity);
		sendCombatStop(target);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
