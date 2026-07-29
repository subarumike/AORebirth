using System;
using System.Collections.Concurrent;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using AORebirth.Stats.SpecialStats;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldCharacterHeartbeatRuntimeService
{
	private readonly ConcurrentDictionary<int, DateTime> nextNpcHealthRegenUtc = new ConcurrentDictionary<int, DateTime>();

	private readonly ConcurrentDictionary<int, byte> npcRegenSuspendedForCombat = new ConcurrentDictionary<int, byte>();

	internal void ProcessRegeneration(ICharacter dynel, Action<ICharacter> sendChangedStats)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Expected O, but got Unknown
		Require(sendChangedStats, "sendChangedStats");
		if (PetCombatRules.IsPlayerOwnedPet(dynel))
		{
			PetRuntimeService.Default.ProcessPetPassiveRegen(dynel);
			return;
		}
		if (((IDynel)dynel).Controller is NPCController)
		{
			ProcessNpcPassiveRegen(dynel, sendChangedStats);
			return;
		}
		bool flag = false;
		if (MongoSlamRuntimeService.ProcessHotTick(dynel))
		{
			flag = true;
		}
		StatHealInterval val = (StatHealInterval)((IStats)dynel).Stats[(StatIds)342];
		int value = ((Stat)val).Value;
		int value2 = ((IStats)dynel).Stats[(StatIds)343].Value;
		if (value > 0 && value2 != 0 && val.LastTick < DateTime.UtcNow)
		{
			((IStats)dynel).Stats[(StatIds)27].Value = Math.Min(((IStats)dynel).Stats[(StatIds)1].Value, ((IStats)dynel).Stats[(StatIds)27].Value + value2);
			val.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(value);
			flag = true;
		}
		StatNanoInterval val2 = (StatNanoInterval)((IStats)dynel).Stats[(StatIds)363];
		int value3 = ((Stat)val2).Value;
		int value4 = ((IStats)dynel).Stats[(StatIds)364].Value;
		if (value3 > 0 && value4 != 0 && val2.LastTick < DateTime.UtcNow)
		{
			IStat obj = ((IStats)dynel).Stats[(StatIds)214];
			obj.Value += value4;
			val2.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(value3);
			flag = true;
		}
		if (flag)
		{
			sendChangedStats(dynel);
		}
	}

	internal void SuspendNpcRegen(ICharacter npc)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Identity identity = ((IEntity)npc).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				ConcurrentDictionary<int, byte> concurrentDictionary = npcRegenSuspendedForCombat;
				identity = ((IEntity)npc).Identity;
				concurrentDictionary[((Identity)(ref identity)).Instance] = 1;
			}
		}
	}

	internal void NotifyNpcDamaged(ICharacter npc)
	{
		SuspendNpcRegen(npc);
	}

	private void ProcessNpcPassiveRegen(ICharacter npc, Action<ICharacter> sendChangedStats)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)npc).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (instance == 0)
		{
			return;
		}
		int value = ((IStats)npc).Stats[(StatIds)27].Value;
		if (!PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(value))
		{
			nextNpcHealthRegenUtc.TryRemove(instance, out var _);
			npcRegenSuspendedForCombat.TryRemove(instance, out var _);
			return;
		}
		double value4 = 5.0;
		int value5 = ((IStats)npc).Stats[(StatIds)1].Value;
		int num = PetCombatRules.ResolveNpcHealthRegenDelta(value5);
		bool flag = false;
		if (OrdinaryEnemyRuntimeRegistry.TryGet(instance, out var definition) && definition.Profile.Combat.HealthRegenIntervalSeconds.HasValue && definition.Profile.Combat.HealthRegenDelta.HasValue)
		{
			value4 = definition.Profile.Combat.HealthRegenIntervalSeconds.Value;
			num = definition.Profile.Combat.HealthRegenDelta.Value;
			flag = definition.Profile.Combat.RegenerateHealthWhileInCombat;
		}
		if (!flag && IsNpcRegenBlocked(npc))
		{
			return;
		}
		if (flag)
		{
			npcRegenSuspendedForCombat.TryRemove(instance, out var _);
		}
		if (PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(value, value5))
		{
			DateTime utcNow = DateTime.UtcNow;
			DateTime orAdd = nextNpcHealthRegenUtc.GetOrAdd(instance, utcNow);
			if (!(utcNow < orAdd))
			{
				((IStats)npc).Stats[(StatIds)27].Value = Math.Min(value5, value + num);
				nextNpcHealthRegenUtc[instance] = utcNow.AddSeconds(value4);
				sendChangedStats(npc);
			}
		}
	}

	private bool IsNpcRegenBlocked(ICharacter npc)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (IsNpcUnderAttack(npc))
		{
			SuspendNpcRegen(npc);
			return true;
		}
		ConcurrentDictionary<int, byte> concurrentDictionary = npcRegenSuspendedForCombat;
		Identity identity = ((IEntity)npc).Identity;
		concurrentDictionary.TryRemove(((Identity)(ref identity)).Instance, out var _);
		return false;
	}

	private bool IsNpcUnderAttack(ICharacter npc)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null)
		{
			return false;
		}
		Identity val = ((ITargetingEntity)npc).FightingTarget;
		if (((Identity)(ref val)).Instance != 0 && ((IStats)npc).Stats[(StatIds)27].Value > 0)
		{
			return true;
		}
		val = ((IEntity)npc).Identity;
		int instance = ((Identity)(ref val)).Instance;
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((IEntity)((IInstancedEntity)npc).Playfield).Identity))
		{
			if (item == null)
			{
				continue;
			}
			val = ((IEntity)item).Identity;
			if (((Identity)(ref val)).Instance != instance)
			{
				val = ((ITargetingEntity)item).FightingTarget;
				int num;
				if (((Identity)(ref val)).Instance != instance)
				{
					val = ((ITargetingEntity)item).SelectedTarget;
					num = ((((Identity)(ref val)).Instance == instance) ? 1 : 0);
				}
				else
				{
					num = 1;
				}
				bool targetsNpc = (byte)num != 0;
				if (PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(item, targetsNpc, (ICharacter candidate) => ((IStats)candidate).Stats[(StatIds)27].Value))
				{
					return true;
				}
			}
		}
		return false;
	}

	internal void ProcessFollow(ICharacter dynel)
	{
		if (((IDynel)dynel).Controller.IsFollowing())
		{
			((IDynel)dynel).Controller.DoFollow();
		}
	}

	internal void ProcessPlayerCollisionChecks(ICharacter dynel, Action<ICharacter> checkWallCollision, Action<ICharacter> checkStatelCollision)
	{
		Require(checkWallCollision, "checkWallCollision");
		Require(checkStatelCollision, "checkStatelCollision");
		checkWallCollision(dynel);
		checkStatelCollision(dynel);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
