using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class NpcCombatTickCoordinator
{
	private sealed class CombatAttackSource
	{
		public int MinDamage { get; set; }

		public int MaxDamage { get; set; }

		public int DamageBonus { get; set; }

		public double Range { get; set; }

		public double RechargeSeconds { get; set; }

		public bool UsesEquippedWeapon { get; set; }

		public int AttackInfoAmmoCount { get; set; }

		public int AttackInfoWeaponSlot { get; set; }

		public int AttackInfoUnk1 { get; set; }

		public int AttackInfoHitType { get; set; }

		public int AttackInfoWeaponInstance { get; set; }

		public bool SendAttackInfo { get; set; }

		public bool CompletesCapturedOpeningAttack { get; set; }
	}

	private enum CombatDamageSource
	{
		WeaponAutoAttack,
		UnarmedAutoAttack,
		DamageOverTime,
		HealOverTime,
		Nano,
		Environment
	}

	private sealed class EquippedCombatWeapon
	{
		public IItem Item { get; set; }

		public int Slot { get; set; }
	}

	private const int MissingItemStatValue = 1234567890;

	private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, int> lastNpcCombatWeaponSlots = new Dictionary<int, int>();

	private readonly Dictionary<int, int> lastNpcUnarmedAttackInfoSlots = new Dictionary<int, int>();

	private readonly Dictionary<int, int> lastNpcSpecialAttackWeaponTargets = new Dictionary<int, int>();

	private readonly HashSet<int> completedCapturedOpeningAttacks = new HashSet<int>();

	private readonly Dictionary<int, DateTime> pendingCapturedAttackStarts = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> pendingCapturedMovementTransitions = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime[]> nextCapturedParallelAttackTicks = new Dictionary<int, DateTime[]>();

	private readonly HashSet<int> startedCapturedParallelAttackClocks = new HashSet<int>();

	private readonly Dictionary<int, DateTime> nextLineOfSightRetryTicks = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> nextLineOfSightDiagnosticTicks = new Dictionary<int, DateTime>();

	private readonly Playfield playfield;

	private readonly NpcDamageLineOfSightRuntimeService damageLineOfSight;

	internal NpcCombatTickCoordinator(Playfield playfield)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		this.playfield = playfield;
		Identity identity = ((PooledObject)playfield).Identity;
		damageLineOfSight = new NpcDamageLineOfSightRuntimeService(((Identity)(ref identity)).Instance);
	}

	internal void ResetCombatTick(ICharacter attacker)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, DateTime> dictionary = nextLineOfSightRetryTicks;
		Identity identity = ((IEntity)attacker).Identity;
		dictionary.Remove(((Identity)(ref identity)).Instance);
		Dictionary<int, DateTime> dictionary2 = nextLineOfSightDiagnosticTicks;
		identity = ((IEntity)attacker).Identity;
		dictionary2.Remove(((Identity)(ref identity)).Instance);
		identity = ((IEntity)attacker).Identity;
		CapturedEnemyCombatContract contract;
		bool flag = CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out contract) && contract.IsCombatReady;
		CapturedEnemySpecialAttackSequenceDefinition capturedEnemySpecialAttackSequenceDefinition = (flag ? contract.SpecialAttackSequence : null);
		CapturedEnemyParallelAttackSequenceDefinition capturedEnemyParallelAttackSequenceDefinition = (flag ? contract.ParallelAttackSequence : null);
		bool flag2 = flag && contract.HasCapturedAttackStartContext;
		double value = capturedEnemySpecialAttackSequenceDefinition?.InitialAttackDelaySeconds ?? (Playfield.IsCapturedCleaningRobot(attacker) ? 2.7 : 2.0);
		DateTime utcNow = DateTime.UtcNow;
		if (capturedEnemyParallelAttackSequenceDefinition != null)
		{
			Dictionary<int, DateTime> dictionary3 = pendingCapturedAttackStarts;
			identity = ((IEntity)attacker).Identity;
			dictionary3.Remove(((Identity)(ref identity)).Instance);
			Dictionary<int, DateTime> dictionary4 = pendingCapturedMovementTransitions;
			identity = ((IEntity)attacker).Identity;
			dictionary4.Remove(((Identity)(ref identity)).Instance);
			Dictionary<int, int> dictionary5 = lastNpcSpecialAttackWeaponTargets;
			identity = ((IEntity)attacker).Identity;
			dictionary5.Remove(((Identity)(ref identity)).Instance);
			HashSet<int> hashSet = completedCapturedOpeningAttacks;
			identity = ((IEntity)attacker).Identity;
			hashSet.Remove(((Identity)(ref identity)).Instance);
			AnnounceCapturedParallelAttackSequenceContext(attacker, capturedEnemyParallelAttackSequenceDefinition);
			return;
		}
		if (flag2 && contract.AttackStartDelaySeconds > 0.0)
		{
			Dictionary<int, DateTime> dictionary6 = pendingCapturedAttackStarts;
			identity = ((IEntity)attacker).Identity;
			dictionary6[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(contract.AttackStartDelaySeconds);
			if (contract.HasCapturedCombatStopSequence)
			{
				Dictionary<int, DateTime> dictionary7 = pendingCapturedMovementTransitions;
				identity = ((IEntity)attacker).Identity;
				dictionary7[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(contract.AttackStartDelaySeconds + contract.MovementTransitionDelaySeconds);
			}
			Dictionary<int, DateTime> dictionary8 = nextCombatTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary8[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(contract.AttackStartDelaySeconds + contract.FirstHitDelaySeconds);
		}
		else
		{
			Dictionary<int, DateTime> dictionary9 = pendingCapturedAttackStarts;
			identity = ((IEntity)attacker).Identity;
			dictionary9.Remove(((Identity)(ref identity)).Instance);
			Dictionary<int, DateTime> dictionary10 = pendingCapturedMovementTransitions;
			identity = ((IEntity)attacker).Identity;
			dictionary10.Remove(((Identity)(ref identity)).Instance);
			Dictionary<int, DateTime> dictionary11 = nextCombatTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary11[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(value);
		}
		Dictionary<int, int> dictionary12 = lastNpcSpecialAttackWeaponTargets;
		identity = ((IEntity)attacker).Identity;
		dictionary12.Remove(((Identity)(ref identity)).Instance);
		HashSet<int> hashSet2 = completedCapturedOpeningAttacks;
		identity = ((IEntity)attacker).Identity;
		hashSet2.Remove(((Identity)(ref identity)).Instance);
		if (capturedEnemySpecialAttackSequenceDefinition != null)
		{
			AnnounceCapturedSpecialAttackSequenceContext(attacker, capturedEnemySpecialAttackSequenceDefinition);
		}
		else if (!Playfield.IsCapturedCleaningRobot(attacker) && flag2 && contract.AttackStartDelaySeconds <= 0.0)
		{
			AnnounceCapturedEnemyAttackStartContext(attacker, contract);
		}
	}

	internal void ClearTracking(Identity identity)
	{
		nextCombatTicks.Remove(((Identity)(ref identity)).Instance);
		lastNpcCombatWeaponSlots.Remove(((Identity)(ref identity)).Instance);
		lastNpcUnarmedAttackInfoSlots.Remove(((Identity)(ref identity)).Instance);
		lastNpcSpecialAttackWeaponTargets.Remove(((Identity)(ref identity)).Instance);
		completedCapturedOpeningAttacks.Remove(((Identity)(ref identity)).Instance);
		pendingCapturedAttackStarts.Remove(((Identity)(ref identity)).Instance);
		pendingCapturedMovementTransitions.Remove(((Identity)(ref identity)).Instance);
		nextCapturedParallelAttackTicks.Remove(((Identity)(ref identity)).Instance);
		startedCapturedParallelAttackClocks.Remove(((Identity)(ref identity)).Instance);
		nextLineOfSightRetryTicks.Remove(((Identity)(ref identity)).Instance);
		nextLineOfSightDiagnosticTicks.Remove(((Identity)(ref identity)).Instance);
	}

	internal void ProcessCombatTick(ICharacter attacker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0473: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0591: Unknown result type (might be due to invalid IL or missing references)
		//IL_0596: Unknown result type (might be due to invalid IL or missing references)
		//IL_0692: Unknown result type (might be due to invalid IL or missing references)
		//IL_0697: Unknown result type (might be due to invalid IL or missing references)
		//IL_065a: Unknown result type (might be due to invalid IL or missing references)
		//IL_065f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0661: Unknown result type (might be due to invalid IL or missing references)
		//IL_066c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0679: Expected O, but got Unknown
		if (attacker == null || playfield == null)
		{
			return;
		}
		Identity val = ((ITargetingEntity)attacker).FightingTarget;
		if (((Identity)(ref val)).Instance == 0)
		{
			playfield.ClearNpcCombatTracking(((IEntity)attacker).Identity);
			return;
		}
		ICharacter val2 = playfield.FindByIdentity<ICharacter>(((ITargetingEntity)attacker).FightingTarget);
		if (val2 == null || !((IDynel)val2).InPlayfield(((PooledObject)playfield).Identity) || ((IStats)val2).Stats[(StatIds)27].Value <= 0 || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(attacker, val2))
		{
			LogUtil.Debug((DebugInfoDetail)512, $"CombatTickTargetInvalid attacker={((IEntity)attacker).Identity} target={((ITargetingEntity)attacker).FightingTarget} found={val2 != null} inPlayfield={val2 != null && ((IDynel)val2).InPlayfield(((PooledObject)playfield).Identity)} health={((val2 != null) ? ((IStats)val2).Stats[(StatIds)27].Value : 0)}");
			double distance = ((val2 == null) ? (-1.0) : Playfield.GetCombatDistance(attacker, val2));
			Playfield.LogNpcBrain("Idle", "target-invalid", attacker, val2, 0.0, distance);
			playfield.ClearInvalidNpcCombatTarget(attacker);
			return;
		}
		Dictionary<int, DateTime> dictionary = pendingCapturedAttackStarts;
		val = ((IEntity)attacker).Identity;
		if (dictionary.TryGetValue(((Identity)(ref val)).Instance, out var value))
		{
			if (!(value > DateTime.UtcNow))
			{
				val = ((IEntity)attacker).Identity;
				if (CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var contract) && contract.IsCombatReady)
				{
					AnnounceCapturedEnemyAttackStartContext(attacker, contract);
				}
				Dictionary<int, DateTime> dictionary2 = pendingCapturedAttackStarts;
				val = ((IEntity)attacker).Identity;
				dictionary2.Remove(((Identity)(ref val)).Instance);
			}
			return;
		}
		Dictionary<int, DateTime> dictionary3 = pendingCapturedMovementTransitions;
		val = ((IEntity)attacker).Identity;
		if (dictionary3.TryGetValue(((Identity)(ref val)).Instance, out var value2))
		{
			if (value2 > DateTime.UtcNow)
			{
				return;
			}
			if (((IDynel)attacker).Controller is NPCController nPCController)
			{
				val = ((IEntity)attacker).Identity;
				if (CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var contract2) && contract2.IsCombatReady && contract2.HasCapturedCombatStopSequence)
				{
					CombatAttackSource combatAttackSource = GetCombatAttackSource(attacker);
					if (!playfield.TryResolveCapturedNpcMovementDestination(attacker, val2, combatAttackSource.Range, DateTime.UtcNow, out var destination))
					{
						destination = ((IDynel)attacker).Coordinates().coordinate;
					}
					nPCController.StopFollowForCapturedCombatRange(((IDynel)val2).Coordinates().coordinate, destination);
				}
			}
			Dictionary<int, DateTime> dictionary4 = pendingCapturedMovementTransitions;
			val = ((IEntity)attacker).Identity;
			dictionary4.Remove(((Identity)(ref val)).Instance);
			return;
		}
		val = ((IEntity)attacker).Identity;
		if (CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var contract3) && contract3.IsCombatReady && contract3.ParallelAttackSequence != null)
		{
			ProcessCapturedParallelAttackTicks(attacker, val2, contract3);
			return;
		}
		CombatAttackSource combatAttackSource2 = GetCombatAttackSource(attacker);
		val = ((IEntity)attacker).Identity;
		CapturedEnemyCombatContract contract4;
		bool flag = CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out contract4) && contract4.IsCombatReady;
		bool flag2 = Playfield.IsCapturedCleaningRobot(attacker) || PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker) || flag;
		DateTime utcNow = DateTime.UtcNow;
		Dictionary<int, DateTime> dictionary5 = nextCombatTicks;
		val = ((IEntity)attacker).Identity;
		if (dictionary5.TryGetValue(((Identity)(ref val)).Instance, out var value3) && value3 > utcNow)
		{
			if (flag2 && (playfield.HasActiveNpcChaseNavigation(attacker) || !playfield.IsInCombatRange(attacker, val2, combatAttackSource2.Range)))
			{
				playfield.TryMoveNpcIntoCombatRange(attacker, val2, combatAttackSource2.Range);
			}
			else if (flag2 && combatAttackSource2.Range <= 8.0)
			{
				playfield.UpdateNpcMeleeFollowHold(attacker, val2, combatAttackSource2.Range);
			}
			return;
		}
		if (!playfield.IsInCombatRange(attacker, val2, combatAttackSource2.Range))
		{
			playfield.TryMoveNpcIntoCombatRange(attacker, val2, combatAttackSource2.Range);
			Dictionary<int, DateTime> dictionary6 = nextCombatTicks;
			val = ((IEntity)attacker).Identity;
			dictionary6[((Identity)(ref val)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(1.0);
			return;
		}
		if (!CanApplyNpcDamage(attacker, val2, contract4, utcNow))
		{
			playfield.TryMoveNpcIntoCombatRange(attacker, val2, combatAttackSource2.Range);
			return;
		}
		playfield.HoldNpcAtCombatPosition(attacker, val2);
		if (combatAttackSource2.Range <= 8.0)
		{
			playfield.UpdateNpcMeleeFollowHold(attacker, val2, combatAttackSource2.Range);
		}
		int value4 = ((IStats)val2).Stats[(StatIds)27].Value;
		int num = CalculateCombatDamage(attacker, combatAttackSource2);
		int num2 = Math.Max(0, value4 - num);
		bool flag3 = num2 == 0;
		AnnounceNpcSpecialAttackWeaponContextIfNeeded(attacker, val2, combatAttackSource2);
		AnnounceCombatDamage(attacker, val2, num, combatAttackSource2, (!combatAttackSource2.UsesEquippedWeapon) ? CombatDamageSource.UnarmedAutoAttack : CombatDamageSource.WeaponAutoAttack);
		((IStats)val2).Stats[(StatIds)27].Value = num2;
		if (combatAttackSource2.CompletesCapturedOpeningAttack)
		{
			HashSet<int> hashSet = completedCapturedOpeningAttacks;
			val = ((IEntity)attacker).Identity;
			hashSet.Add(((Identity)(ref val)).Instance);
		}
		((IDynel)val2).SendChangedStats();
		playfield.NotifyNpcCombatDamage(val2);
		LogUtil.Debug((DebugInfoDetail)4, $"Combat hit attacker={((IEntity)attacker).Identity} target={((IEntity)val2).Identity} damage={num} health={num2}/{((IStats)val2).Stats[(StatIds)1].Value} weaponBased={(combatAttackSource2.UsesEquippedWeapon ? 1 : 0)} slot={combatAttackSource2.AttackInfoWeaponSlot}");
		if (flag3)
		{
			if (PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker))
			{
				playfield.Announce((MessageBody)new StopFightMessage
				{
					Identity = ((IEntity)attacker).Identity,
					Unknown1 = 1
				});
			}
			playfield.HandleCombatKillingHit(attacker, val2);
		}
		else
		{
			Dictionary<int, DateTime> dictionary7 = nextCombatTicks;
			val = ((IEntity)attacker).Identity;
			dictionary7[((Identity)(ref val)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(combatAttackSource2.RechargeSeconds);
		}
	}

	private bool IsLineOfSightRetryPending(ICharacter attacker, DateTime utcNow)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, DateTime> dictionary = nextLineOfSightRetryTicks;
		Identity identity = ((IEntity)attacker).Identity;
		DateTime value;
		return dictionary.TryGetValue(((Identity)(ref identity)).Instance, out value) && value > utcNow;
	}

	private bool CanApplyNpcDamage(ICharacter attacker, ICharacter target, CapturedEnemyCombatContract capturedContract, DateTime utcNow)
	{
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		bool flag = NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(activationEnabled: true, ((IStats)attacker).Stats[(StatIds)359].Value, capturedContract?.RequiresDamageLineOfSight);
		if (IsLineOfSightRetryPending(attacker, utcNow))
		{
			return false;
		}
		Identity identity;
		if (flag)
		{
			CollisionPoint3 start = new CollisionPoint3(((IDynel)attacker).RawCoordinates.X, ((IDynel)attacker).RawCoordinates.Y, ((IDynel)attacker).RawCoordinates.Z);
			CollisionPoint3 end = new CollisionPoint3(((IDynel)target).RawCoordinates.X, ((IDynel)target).RawCoordinates.Y, ((IDynel)target).RawCoordinates.Z);
			SegmentTriangleHit hit;
			NpcDamageLineOfSightDecision npcDamageLineOfSightDecision = damageLineOfSight.EvaluateAttackLine(requiresDamageLineOfSight: true, start, end, out hit);
			if (npcDamageLineOfSightDecision != NpcDamageLineOfSightDecision.AllowedClear && npcDamageLineOfSightDecision != 0)
			{
				Dictionary<int, DateTime> dictionary = nextLineOfSightRetryTicks;
				identity = ((IEntity)attacker).Identity;
				dictionary[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(1.0);
				LogLineOfSightDenied(attacker, target, npcDamageLineOfSightDecision, hit, utcNow);
				return false;
			}
		}
		if (!playfield.IsNpcAttackPathTraversable(attacker, target))
		{
			Dictionary<int, DateTime> dictionary2 = nextLineOfSightRetryTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(1.0);
			LogNavigationDenied(attacker, target, utcNow);
			return false;
		}
		Dictionary<int, DateTime> dictionary3 = nextLineOfSightRetryTicks;
		identity = ((IEntity)attacker).Identity;
		dictionary3.Remove(((Identity)(ref identity)).Instance);
		return true;
	}

	private void LogNavigationDenied(ICharacter attacker, ICharacter target, DateTime utcNow)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, DateTime> dictionary = nextLineOfSightDiagnosticTicks;
		Identity identity = ((IEntity)attacker).Identity;
		if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) || !(value > utcNow))
		{
			Dictionary<int, DateTime> dictionary2 = nextLineOfSightDiagnosticTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(10.0);
			LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "NpcChaseNavigationAttackDenied attacker={0} target={1} reason=movement-segment-blocked", ((IEntity)attacker).Identity, ((IEntity)target).Identity));
		}
	}

	private void LogLineOfSightDenied(ICharacter attacker, ICharacter target, NpcDamageLineOfSightDecision decision, SegmentTriangleHit hit, DateTime utcNow)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, DateTime> dictionary = nextLineOfSightDiagnosticTicks;
		Identity identity = ((IEntity)attacker).Identity;
		if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) || !(value > utcNow))
		{
			Dictionary<int, DateTime> dictionary2 = nextLineOfSightDiagnosticTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = utcNow + TimeSpan.FromSeconds(10.0);
			string text = ((decision == NpcDamageLineOfSightDecision.DeniedBlocked) ? string.Format(CultureInfo.InvariantCulture, "triangle={0} fraction={1:0.000000}", hit.TriangleId, hit.SegmentFraction) : ("geometryError=" + damageLineOfSight.GeometryError));
			LogUtil.Debug((DebugInfoDetail)((decision == NpcDamageLineOfSightDecision.DeniedBlocked) ? 4 : 512), string.Format(CultureInfo.InvariantCulture, "NpcDamageLineOfSightDenied attacker={0} target={1} decision={2} {3}", ((IEntity)attacker).Identity, ((IEntity)target).Identity, decision, text));
		}
	}

	private int CalculateCombatDamage(ICharacter attacker, CombatAttackSource attackSource)
	{
		return CombatDamageRules.Calculate(attackSource.MinDamage, attackSource.MaxDamage, attackSource.DamageBonus, ((IStats)attacker).Stats[(StatIds)54].Value, isPlayer: false);
	}

	private void AnnounceNpcSpecialAttackWeaponContextIfNeeded(ICharacter attacker, ICharacter target, CombatAttackSource attackSource)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected O, but got Unknown
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Expected O, but got Unknown
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Expected O, but got Unknown
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)attacker).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		identity = ((IEntity)target).Identity;
		int instance2 = ((Identity)(ref identity)).Instance;
		int value;
		int? previousTargetInstance = (lastNpcSpecialAttackWeaponTargets.TryGetValue(instance, out value) ? new int?(value) : null);
		if (NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(Playfield.IsCapturedCleaningRobot(attacker), attackSource.UsesEquippedWeapon, previousTargetInstance, instance2) || NpcCombatAttackRules.ShouldSendPlayerOwnedAttackPetAttackStartContext(PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker), previousTargetInstance, instance2))
		{
			lastNpcSpecialAttackWeaponTargets[instance] = instance2;
			if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
			{
				Playfield obj = playfield;
				SpecialAttackWeaponMessage val = new SpecialAttackWeaponMessage();
				((N3Message)val).Identity = ((IEntity)attacker).Identity;
				((N3Message)val).Unknown = 0;
				val.Specials = (SpecialAttack[])(object)new SpecialAttack[0];
				val.Unknown1 = 0;
				val.Unknown2 = 0;
				val.Unknown3 = 0;
				val.Unknown4 = 0;
				val.Unknown5 = 0;
				obj.Announce((MessageBody)(object)val);
				playfield.Announce((MessageBody)new AttackMessage
				{
					Identity = ((IEntity)attacker).Identity,
					Target = ((IEntity)target).Identity,
					Action = 0
				});
			}
			else if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker) || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker))
			{
				AnnouncePlayerOwnedAttackPetAttackStartContext(attacker, target);
			}
			else
			{
				playfield.Announce((MessageBody)new SpecialAttackWeaponMessage
				{
					Identity = ((IEntity)attacker).Identity,
					Specials = CreateCapturedCleaningRobotSpecialAttacks(),
					Unknown1 = 8,
					Unknown2 = 8,
					Unknown3 = 8,
					Unknown4 = 8,
					Unknown5 = 0
				});
				Identity identity2 = ((IEntity)attacker).Identity;
				identity = ((IEntity)target).Identity;
				PlayfieldLifecycleTrace.Record("cleaning-robot-npc-attack", "robot-special-attack-weapon-context", "SpecialAttackWeapon", identity2, "target=" + ((object)(Identity)(ref identity)).ToString());
				playfield.Announce((MessageBody)new AttackMessage
				{
					Identity = ((IEntity)attacker).Identity,
					Target = ((IEntity)target).Identity,
					Action = 0
				});
				Identity identity3 = ((IEntity)attacker).Identity;
				identity = ((IEntity)target).Identity;
				PlayfieldLifecycleTrace.Record("cleaning-robot-npc-attack", "robot-attack-start-context", "Attack", identity3, "target=" + ((object)(Identity)(ref identity)).ToString());
				LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "CombatNpcAttackStartContextSend attacker={0} target={1} monsterData={2}", ((IEntity)attacker).Identity, ((IEntity)target).Identity, ((IStats)attacker).Stats[(StatIds)359].Value));
			}
		}
	}

	private void AnnouncePlayerOwnedAttackPetAttackStartContext(ICharacter attacker, ICharacter target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		playfield.Announce((MessageBody)new SpecialAttackWeaponMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Specials = CreatePlayerOwnedAttackPetSpecialAttacks(),
			Unknown1 = 841,
			Unknown2 = 841,
			Unknown3 = 841,
			Unknown4 = 841,
			Unknown5 = 0
		});
		playfield.Announce((MessageBody)new AttackMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Target = ((IEntity)target).Identity,
			Action = 0
		});
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "CombatPetAttackStartContextSend attacker={0} target={1}", ((IEntity)attacker).Identity, ((IEntity)target).Identity));
	}

	private void AnnounceCapturedSpecialAttackSequenceContext(ICharacter attacker, CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected O, but got Unknown
		Identity val = ((ITargetingEntity)attacker).FightingTarget;
		if (((Identity)(ref val)).Instance != 0 && specialAttackSequence != null)
		{
			Dictionary<int, int> dictionary = lastNpcSpecialAttackWeaponTargets;
			val = ((IEntity)attacker).Identity;
			int instance = ((Identity)(ref val)).Instance;
			val = ((ITargetingEntity)attacker).FightingTarget;
			dictionary[instance] = ((Identity)(ref val)).Instance;
			playfield.Announce((MessageBody)new SpecialAttackWeaponMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Specials = CreateCapturedSpecialAttacks(specialAttackSequence.SpecialAttacks),
				Unknown1 = specialAttackSequence.SpecialAttackWeaponUnknown1,
				Unknown2 = specialAttackSequence.SpecialAttackWeaponUnknown2,
				Unknown3 = specialAttackSequence.SpecialAttackWeaponUnknown3,
				Unknown4 = specialAttackSequence.SpecialAttackWeaponUnknown4,
				Unknown5 = specialAttackSequence.SpecialAttackWeaponUnknown5
			});
			playfield.Announce((MessageBody)new AttackMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Target = ((ITargetingEntity)attacker).FightingTarget,
				Action = 0
			});
		}
	}

	private void AnnounceCapturedParallelAttackSequenceContext(ICharacter attacker, CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected O, but got Unknown
		Identity val = ((ITargetingEntity)attacker).FightingTarget;
		if (((Identity)(ref val)).Instance != 0 && parallelAttackSequence != null)
		{
			Dictionary<int, int> dictionary = lastNpcSpecialAttackWeaponTargets;
			val = ((IEntity)attacker).Identity;
			int instance = ((Identity)(ref val)).Instance;
			val = ((ITargetingEntity)attacker).FightingTarget;
			dictionary[instance] = ((Identity)(ref val)).Instance;
			playfield.Announce((MessageBody)new SpecialAttackWeaponMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Specials = CreateCapturedSpecialAttacks(parallelAttackSequence.SpecialAttacks),
				Unknown1 = parallelAttackSequence.SpecialAttackWeaponUnknown1,
				Unknown2 = parallelAttackSequence.SpecialAttackWeaponUnknown2,
				Unknown3 = parallelAttackSequence.SpecialAttackWeaponUnknown3,
				Unknown4 = parallelAttackSequence.SpecialAttackWeaponUnknown4,
				Unknown5 = parallelAttackSequence.SpecialAttackWeaponUnknown5
			});
			playfield.Announce((MessageBody)new AttackMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Target = ((ITargetingEntity)attacker).FightingTarget,
				Action = 0
			});
		}
	}

	private void ProcessCapturedParallelAttackTicks(ICharacter attacker, ICharacter target, CapturedEnemyCombatContract contract)
	{
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence = contract.ParallelAttackSequence;
		CapturedEnemyParallelAttackStreamDefinition[] streams = parallelAttackSequence.Streams;
		DateTime now = DateTime.UtcNow;
		double range = streams.Max((CapturedEnemyParallelAttackStreamDefinition value) => value.Attack.Range);
		if (!playfield.IsInCombatRange(attacker, target, range))
		{
			playfield.TryMoveNpcIntoCombatRange(attacker, target, range);
			return;
		}
		if (!CanApplyNpcDamage(attacker, target, contract, now))
		{
			playfield.TryMoveNpcIntoCombatRange(attacker, target, range);
			return;
		}
		playfield.HoldNpcAtCombatPosition(attacker, target);
		playfield.UpdateNpcMeleeFollowHold(attacker, target, range);
		HashSet<int> hashSet = startedCapturedParallelAttackClocks;
		Identity identity = ((IEntity)attacker).Identity;
		DateTime[] value2;
		if (hashSet.Contains(((Identity)(ref identity)).Instance))
		{
			Dictionary<int, DateTime[]> dictionary = nextCapturedParallelAttackTicks;
			identity = ((IEntity)attacker).Identity;
			if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out value2) && value2.Length == streams.Length)
			{
				goto IL_015e;
			}
		}
		value2 = streams.Select((CapturedEnemyParallelAttackStreamDefinition value) => now + TimeSpan.FromSeconds(value.InitialDelaySeconds)).ToArray();
		Dictionary<int, DateTime[]> dictionary2 = nextCapturedParallelAttackTicks;
		identity = ((IEntity)attacker).Identity;
		dictionary2[((Identity)(ref identity)).Instance] = value2;
		HashSet<int> hashSet2 = startedCapturedParallelAttackClocks;
		identity = ((IEntity)attacker).Identity;
		hashSet2.Add(((Identity)(ref identity)).Instance);
		goto IL_015e;
		IL_015e:
		int num = -1;
		DateTime dateTime = DateTime.MaxValue;
		for (int i = 0; i < value2.Length; i++)
		{
			if (value2[i] <= now && value2[i] < dateTime)
			{
				num = i;
				dateTime = value2[i];
			}
		}
		if (num >= 0)
		{
			CapturedEnemyCombatAttackDefinition attack = streams[num].Attack;
			CombatAttackSource attackSource = new CombatAttackSource
			{
				MinDamage = attack.MinDamage,
				MaxDamage = attack.MaxDamage,
				DamageBonus = attack.DamageBonus,
				Range = attack.Range,
				RechargeSeconds = attack.RechargeSeconds,
				UsesEquippedWeapon = attack.UsesEquippedWeapon,
				AttackInfoAmmoCount = attack.AttackInfoAmmoCount,
				AttackInfoWeaponSlot = attack.AttackInfoWeaponSlot,
				AttackInfoUnk1 = attack.AttackInfoUnknown,
				AttackInfoHitType = attack.AttackInfoHitType,
				AttackInfoWeaponInstance = attack.AttackInfoWeaponInstance,
				SendAttackInfo = attack.SendAttackInfo
			};
			int value3 = ((IStats)target).Stats[(StatIds)27].Value;
			int num2 = CalculateCombatDamage(attacker, attackSource);
			int num3 = Math.Max(0, value3 - num2);
			AnnounceCombatDamage(attacker, target, num2, attackSource, CombatDamageSource.UnarmedAutoAttack);
			((IStats)target).Stats[(StatIds)27].Value = num3;
			((IDynel)target).SendChangedStats();
			playfield.NotifyNpcCombatDamage(target);
			value2[num] = now + TimeSpan.FromSeconds(attack.RechargeSeconds);
			LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Combat parallel hit attacker={0} target={1} stream={2} damage={3} health={4}/{5}", ((IEntity)attacker).Identity, ((IEntity)target).Identity, num, num2, num3, ((IStats)target).Stats[(StatIds)1].Value));
			if (num3 == 0)
			{
				playfield.HandleCombatKillingHit(attacker, target);
			}
		}
	}

	private void AnnounceCapturedEnemyAttackStartContext(ICharacter attacker, CapturedEnemyCombatContract capturedContract)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || capturedContract == null)
		{
			return;
		}
		Identity val = ((ITargetingEntity)attacker).FightingTarget;
		if (((Identity)(ref val)).Instance != 0)
		{
			if (capturedContract.HasEmptySpecialAttackWeaponContext)
			{
				Dictionary<int, int> dictionary = lastNpcSpecialAttackWeaponTargets;
				val = ((IEntity)attacker).Identity;
				int instance = ((Identity)(ref val)).Instance;
				val = ((ITargetingEntity)attacker).FightingTarget;
				dictionary[instance] = ((Identity)(ref val)).Instance;
				Playfield obj = playfield;
				SpecialAttackWeaponMessage val2 = new SpecialAttackWeaponMessage();
				((N3Message)val2).Identity = ((IEntity)attacker).Identity;
				((N3Message)val2).Unknown = 0;
				val2.Specials = (SpecialAttack[])(object)new SpecialAttack[0];
				val2.Unknown1 = capturedContract.SpecialAttackWeaponUnknown1;
				val2.Unknown2 = capturedContract.SpecialAttackWeaponUnknown2;
				val2.Unknown3 = capturedContract.SpecialAttackWeaponUnknown3;
				val2.Unknown4 = capturedContract.SpecialAttackWeaponUnknown4;
				val2.Unknown5 = capturedContract.SpecialAttackWeaponUnknown5;
				obj.Announce((MessageBody)(object)val2);
			}
			playfield.Announce((MessageBody)new AttackMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Unknown = 0,
				Target = ((ITargetingEntity)attacker).FightingTarget,
				Action = 0
			});
			LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "CombatCapturedEnemyAttackStartContextSend attacker={0} target={1}", ((IEntity)attacker).Identity, ((ITargetingEntity)attacker).FightingTarget));
		}
	}

	private static SpecialAttack[] CreatePlayerOwnedAttackPetSpecialAttacks()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		return (SpecialAttack[])(object)new SpecialAttack[2]
		{
			new SpecialAttack
			{
				Unknown1 = 120634,
				Unknown2 = 120635,
				Unknown3 = 1296389938,
				Unknown4 = "MEW2"
			},
			new SpecialAttack
			{
				Unknown1 = 120637,
				Unknown2 = 120638,
				Unknown3 = 1296389937,
				Unknown4 = "MEW1"
			}
		};
	}

	private static SpecialAttack[] CreateCapturedCleaningRobotSpecialAttacks()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		return (SpecialAttack[])(object)new SpecialAttack[2]
		{
			new SpecialAttack
			{
				Unknown1 = 125280,
				Unknown2 = 125280,
				Unknown3 = 1279874866,
				Unknown4 = "LIW2"
			},
			new SpecialAttack
			{
				Unknown1 = 125277,
				Unknown2 = 125277,
				Unknown3 = 1279874865,
				Unknown4 = "LIW1"
			}
		};
	}

	private static SpecialAttack[] CreateCapturedSpecialAttacks(CapturedEnemySpecialAttackDefinition[] definitions)
	{
		if (definitions == null || definitions.Length == 0)
		{
			return (SpecialAttack[])(object)new SpecialAttack[0];
		}
		return ((IEnumerable<CapturedEnemySpecialAttackDefinition>)definitions).Select((Func<CapturedEnemySpecialAttackDefinition, SpecialAttack>)((CapturedEnemySpecialAttackDefinition definition) => new SpecialAttack
		{
			Unknown1 = definition.LowTemplate,
			Unknown2 = definition.HighTemplate,
			Unknown3 = definition.Tag,
			Unknown4 = definition.Name
		})).ToArray();
	}

	private void AnnounceCombatDamage(ICharacter attacker, ICharacter target, int damage, CombatAttackSource attackSource, CombatDamageSource source)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Expected O, but got Unknown
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackInfoSend source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage} u2={attackSource.AttackInfoAmmoCount} u3={attackSource.AttackInfoWeaponSlot} u4={attackSource.AttackInfoUnk1} u5={attackSource.AttackInfoHitType} u6={attackSource.AttackInfoWeaponInstance} weaponBased={(attackSource.UsesEquippedWeapon ? 1 : 0)} atkDefault={((IStats)attacker).Stats[(StatIds)292].Value} atkDamageType={((IStats)attacker).Stats[(StatIds)436].Value} atkWeaponType={((IStats)attacker).Stats[(StatIds)1003].Value} atkEquippedWeapons={((IStats)attacker).Stats[(StatIds)274].Value}");
		if (attackSource.SendAttackInfo)
		{
			playfield.Announce((MessageBody)new AttackInfoMessage
			{
				Identity = ((IEntity)attacker).Identity,
				Unknown = 0,
				Target = ((IEntity)target).Identity,
				Unknown1 = damage,
				Unknown2 = attackSource.AttackInfoAmmoCount,
				Unknown3 = attackSource.AttackInfoWeaponSlot,
				Unknown4 = attackSource.AttackInfoUnk1,
				Unknown5 = attackSource.AttackInfoHitType,
				Unknown6 = attackSource.AttackInfoWeaponInstance
			});
			Identity identity = ((IEntity)attacker).Identity;
			Identity identity2 = ((IEntity)target).Identity;
			PlayfieldLifecycleTrace.Record("cleaning-robot-npc-attack", "robot-attack-info", "AttackInfo", identity, "target=" + ((object)(Identity)(ref identity2)).ToString());
		}
		else
		{
			LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackInfoSkip source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage} reason=no_captured_or_equipped_context");
		}
		AnnounceHealthDamageIfNeeded(attacker, target, damage, source);
	}

	private void AnnounceHealthDamageIfNeeded(ICharacter attacker, ICharacter target, int damage, CombatDamageSource source)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (!ShouldSendHealthDamage(source))
		{
			LogUtil.Debug((DebugInfoDetail)4, $"CombatHealthDamageSkip source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage}");
			return;
		}
		LogUtil.Debug((DebugInfoDetail)4, $"CombatHealthDamageSend source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage}");
		playfield.Announce((MessageBody)new HealthDamageMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Unknown1 = damage,
			Unknown2 = 0,
			Unknown3 = 0,
			Unknown4 = 0,
			Target = ((IEntity)target).Identity,
			Unknown5 = 0
		});
	}

	private static bool ShouldSendHealthDamage(CombatDamageSource source)
	{
		return source != 0 && source != CombatDamageSource.UnarmedAutoAttack;
	}

	private CombatAttackSource GetCombatAttackSource(ICharacter attacker)
	{
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0726: Unknown result type (might be due to invalid IL or missing references)
		if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
		{
			int num = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)286].Value, 0);
			int num2 = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)285].Value, 0);
			int num3 = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(((IStats)attacker).Stats[(StatIds)54].Value);
			int num4 = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(((IStats)attacker).Stats[(StatIds)54].Value);
			int minDamage = ((num > 0) ? num : ((num2 > 0) ? num2 : num3));
			int maxDamage = ((num2 > 0) ? num2 : ((num > 0) ? num : num4));
			return new CombatAttackSource
			{
				MinDamage = minDamage,
				MaxDamage = maxDamage,
				DamageBonus = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)284].Value, 0),
				Range = 8.0,
				RechargeSeconds = 2.0,
				UsesEquippedWeapon = true,
				AttackInfoAmmoCount = -1,
				AttackInfoWeaponSlot = 6,
				AttackInfoUnk1 = 4,
				AttackInfoHitType = 1,
				AttackInfoWeaponInstance = 0,
				SendAttackInfo = true
			};
		}
		if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker) || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker))
		{
			int num5 = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)286].Value, 0);
			int num6 = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)285].Value, 0);
			int num7 = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(((IStats)attacker).Stats[(StatIds)54].Value);
			int num8 = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(((IStats)attacker).Stats[(StatIds)54].Value);
			int minDamage2 = ((num5 > 0) ? num5 : ((num6 > 0) ? num6 : num7));
			int maxDamage2 = ((num6 > 0) ? num6 : ((num5 > 0) ? num5 : num8));
			int attackPetAttackInfoWeaponInstance = GetAttackPetAttackInfoWeaponInstance(attacker);
			return new CombatAttackSource
			{
				MinDamage = minDamage2,
				MaxDamage = maxDamage2,
				DamageBonus = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)284].Value, 0),
				Range = 8.0,
				RechargeSeconds = 2.0,
				UsesEquippedWeapon = true,
				AttackInfoAmmoCount = -1,
				AttackInfoWeaponSlot = 1,
				AttackInfoUnk1 = 4,
				AttackInfoHitType = 1,
				AttackInfoWeaponInstance = attackPetAttackInfoWeaponInstance,
				SendAttackInfo = true
			};
		}
		Identity identity = ((IEntity)attacker).Identity;
		CapturedEnemyCombatContract contract;
		bool flag = CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out contract) && contract.IsCombatReady;
		if (flag && contract.AttackModel == CapturedEnemyAttackModel.Specialized && contract.SpecialAttackSequence != null)
		{
			CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence = contract.SpecialAttackSequence;
			int num9;
			if (specialAttackSequence.OpeningAttack != null)
			{
				HashSet<int> hashSet = completedCapturedOpeningAttacks;
				identity = ((IEntity)attacker).Identity;
				num9 = (hashSet.Contains(((Identity)(ref identity)).Instance) ? 1 : 0);
			}
			else
			{
				num9 = 1;
			}
			bool flag2 = (byte)num9 != 0;
			CapturedEnemyCombatAttackDefinition capturedEnemyCombatAttackDefinition = (flag2 ? specialAttackSequence.RepeatingAttack : specialAttackSequence.OpeningAttack);
			return new CombatAttackSource
			{
				MinDamage = capturedEnemyCombatAttackDefinition.MinDamage,
				MaxDamage = capturedEnemyCombatAttackDefinition.MaxDamage,
				DamageBonus = capturedEnemyCombatAttackDefinition.DamageBonus,
				Range = capturedEnemyCombatAttackDefinition.Range,
				RechargeSeconds = capturedEnemyCombatAttackDefinition.RechargeSeconds,
				UsesEquippedWeapon = capturedEnemyCombatAttackDefinition.UsesEquippedWeapon,
				AttackInfoAmmoCount = capturedEnemyCombatAttackDefinition.AttackInfoAmmoCount,
				AttackInfoWeaponSlot = capturedEnemyCombatAttackDefinition.AttackInfoWeaponSlot,
				AttackInfoUnk1 = capturedEnemyCombatAttackDefinition.AttackInfoUnknown,
				AttackInfoHitType = capturedEnemyCombatAttackDefinition.AttackInfoHitType,
				AttackInfoWeaponInstance = capturedEnemyCombatAttackDefinition.AttackInfoWeaponInstance,
				SendAttackInfo = capturedEnemyCombatAttackDefinition.SendAttackInfo,
				CompletesCapturedOpeningAttack = !flag2
			};
		}
		if (flag && contract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
		{
			int attackInfoWeaponSlot = contract.AttackInfoWeaponSlot;
			int num10 = contract.AttackInfoWeaponInstance;
			if (num10 == 0)
			{
				attackInfoWeaponSlot = GetUnarmedAttackInfoWeaponSlot(attacker);
				num10 = GetUnarmedAttackInfoWeaponInstance(attacker);
			}
			return new CombatAttackSource
			{
				MinDamage = contract.MinDamage,
				MaxDamage = contract.MaxDamage,
				DamageBonus = 0,
				Range = 8.0,
				RechargeSeconds = ((contract.RechargeSeconds > 0.0) ? contract.RechargeSeconds : 2.0),
				UsesEquippedWeapon = false,
				AttackInfoAmmoCount = -1,
				AttackInfoWeaponSlot = attackInfoWeaponSlot,
				AttackInfoUnk1 = contract.AttackInfoUnknown,
				AttackInfoHitType = 3,
				AttackInfoWeaponInstance = num10,
				SendAttackInfo = true
			};
		}
		EquippedCombatWeapon equippedCombatWeapon = GetEquippedCombatWeapon(attacker);
		if (equippedCombatWeapon == null)
		{
			LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackSource unarmed attacker={((IEntity)attacker).Identity} mindmg={((IStats)attacker).Stats[(StatIds)286].Value} maxdmg={((IStats)attacker).Stats[(StatIds)285].Value} bonus={((IStats)attacker).Stats[(StatIds)284].Value} defaultattack={((IStats)attacker).Stats[(StatIds)292].Value} damagetype={((IStats)attacker).Stats[(StatIds)436].Value} weapontype={((IStats)attacker).Stats[(StatIds)1003].Value} equippedweapons={((IStats)attacker).Stats[(StatIds)274].Value}");
			int unarmedAttackInfoWeaponSlot = GetUnarmedAttackInfoWeaponSlot(attacker);
			int unarmedAttackDamage = GetUnarmedAttackDamage(attacker, unarmedAttackInfoWeaponSlot);
			return new CombatAttackSource
			{
				MinDamage = unarmedAttackDamage,
				MaxDamage = unarmedAttackDamage,
				DamageBonus = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)284].Value, 0),
				Range = 8.0,
				RechargeSeconds = (Playfield.IsCapturedCleaningRobot(attacker) ? 2.7 : 2.0),
				UsesEquippedWeapon = false,
				AttackInfoAmmoCount = -1,
				AttackInfoWeaponSlot = unarmedAttackInfoWeaponSlot,
				AttackInfoUnk1 = 0,
				AttackInfoHitType = 3,
				AttackInfoWeaponInstance = GetUnarmedAttackInfoWeaponInstance(attacker),
				SendAttackInfo = Playfield.IsCapturedCleaningRobot(attacker)
			};
		}
		IItem item = equippedCombatWeapon.Item;
		bool flag3 = contract?.HasCapturedEquippedAttackInfo ?? false;
		int num11 = ((flag3 && contract.MinDamage > 0) ? contract.MinDamage : NormalizeCombatItemStat(item.GetAttribute(286), 0));
		int num12 = ((flag3 && contract.MaxDamage > 0) ? contract.MaxDamage : NormalizeCombatItemStat(item.GetAttribute(285), 0));
		int num13 = NormalizeCombatItemStat(item.GetAttribute(284), 0);
		LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackSource weapon attacker={((IEntity)attacker).Identity} item={item.LowID}/{item.HighID} slot={equippedCombatWeapon.Slot} min={num11} max={num12} damageBonus={num13} rangeRaw={item.GetAttribute(287)}");
		return new CombatAttackSource
		{
			MinDamage = num11,
			MaxDamage = num12,
			DamageBonus = num13,
			Range = NormalizeCombatRange(item.GetAttribute(287)),
			RechargeSeconds = ((flag3 && contract.RechargeSeconds > 0.0) ? contract.RechargeSeconds : NormalizeCombatDelaySeconds(item.GetAttribute(294), item.GetAttribute(210))),
			UsesEquippedWeapon = true,
			AttackInfoAmmoCount = (flag3 ? contract.AttackInfoAmmoCount : 40),
			AttackInfoWeaponSlot = (flag3 ? contract.AttackInfoWeaponSlot : equippedCombatWeapon.Slot),
			AttackInfoUnk1 = (flag3 ? contract.AttackInfoUnknown : 4),
			AttackInfoHitType = 3,
			AttackInfoWeaponInstance = (flag3 ? contract.AttackInfoWeaponInstance : 0),
			SendAttackInfo = true
		};
	}

	private int GetAttackPetAttackInfoWeaponInstance(ICharacter attacker)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)attacker).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (lastNpcUnarmedAttackInfoSlots.TryGetValue(instance, out var value) && value == 1)
		{
			lastNpcUnarmedAttackInfoSlots[instance] = 0;
			return 1296389937;
		}
		lastNpcUnarmedAttackInfoSlots[instance] = 1;
		return 1296389938;
	}

	private int GetUnarmedAttackInfoWeaponSlot(ICharacter attacker)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, int> dictionary = lastNpcUnarmedAttackInfoSlots;
		Identity identity = ((IEntity)attacker).Identity;
		if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) && value == 0)
		{
			Dictionary<int, int> dictionary2 = lastNpcUnarmedAttackInfoSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = 1;
			return 1;
		}
		Dictionary<int, int> dictionary3 = lastNpcUnarmedAttackInfoSlots;
		identity = ((IEntity)attacker).Identity;
		dictionary3[((Identity)(ref identity)).Instance] = 0;
		return 0;
	}

	private int GetUnarmedAttackDamage(ICharacter attacker, int attackInfoWeaponSlot)
	{
		if (Playfield.IsCapturedCleaningRobot(attacker))
		{
			return (attackInfoWeaponSlot == 1) ? 8 : 10;
		}
		return Math.Max(NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)286].Value, 0), NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)285].Value, 0));
	}

	private int GetUnarmedAttackInfoWeaponInstance(ICharacter attacker)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, int> dictionary = lastNpcUnarmedAttackInfoSlots;
		Identity identity = ((IEntity)attacker).Identity;
		if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) || value == 0)
		{
			return 1279874865;
		}
		return 1279874866;
	}

	private EquippedCombatWeapon GetEquippedCombatWeapon(ICharacter attacker)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		if (((IItemContainer)attacker).BaseInventory == null || !((IItemContainer)attacker).BaseInventory.Pages.ContainsKey(101))
		{
			Dictionary<int, int> dictionary = lastNpcCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary.Remove(((Identity)(ref identity)).Instance);
			return null;
		}
		IInventoryPage val = ((IItemContainer)attacker).BaseInventory.Pages[101];
		IItem item = val[6];
		IItem item2 = val[8];
		bool flag = IsWieldableCombatWeapon(item);
		bool flag2 = IsWieldableCombatWeapon(item2);
		if (flag && flag2)
		{
			identity = ((IEntity)attacker).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			if (lastNpcCombatWeaponSlots.TryGetValue(instance, out var value) && value == 6)
			{
				lastNpcCombatWeaponSlots[instance] = 8;
				return new EquippedCombatWeapon
				{
					Item = item2,
					Slot = 8
				};
			}
			lastNpcCombatWeaponSlots[instance] = 6;
			return new EquippedCombatWeapon
			{
				Item = item,
				Slot = 6
			};
		}
		if (flag)
		{
			Dictionary<int, int> dictionary2 = lastNpcCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = 6;
			return new EquippedCombatWeapon
			{
				Item = item,
				Slot = 6
			};
		}
		if (flag2)
		{
			Dictionary<int, int> dictionary3 = lastNpcCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary3[((Identity)(ref identity)).Instance] = 8;
			return new EquippedCombatWeapon
			{
				Item = item2,
				Slot = 8
			};
		}
		Dictionary<int, int> dictionary4 = lastNpcCombatWeaponSlots;
		identity = ((IEntity)attacker).Identity;
		dictionary4.Remove(((Identity)(ref identity)).Instance);
		return null;
	}

	private static int NormalizeCombatItemStat(int value, int fallback)
	{
		return (value == 1234567890) ? fallback : value;
	}

	private static bool IsWieldableCombatWeapon(IItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (item.ItemActions != null && item.ItemActions.Any((AOAction x) => (int)x.ActionType == 8))
		{
			return true;
		}
		return NormalizeCombatItemStat(item.GetAttribute(286), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(285), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(287), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(294), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(210), 0) > 0;
	}

	private static double NormalizeCombatRange(int range)
	{
		int num = NormalizeCombatItemStat(range, 0);
		if (num <= 0)
		{
			return 8.0;
		}
		return (num > 1000) ? ((double)num / 100.0) : ((double)num);
	}

	private static double NormalizeCombatDelaySeconds(int attackDelay, int rechargeDelay)
	{
		int num = NormalizeCombatItemStat(attackDelay, 0);
		int num2 = NormalizeCombatItemStat(rechargeDelay, 0);
		int num3 = num + num2;
		if (num3 <= 0)
		{
			return 2.0;
		}
		return Math.Max(0.25, (double)num3 / 100.0);
	}
}
