using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayEncounterRuntimeService
{
	private sealed class InfectorSlotState
	{
		internal int Slot { get; private set; }

		internal Identity ActiveIdentity { get; set; }

		internal DateTime? SpawnDueAtUtc { get; set; }

		internal int Generation { get; set; }

		internal InfectorSlotState(int slot)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Slot = slot;
			ActiveIdentity = Identity.None;
		}
	}

	private sealed class CapturedEncounterLevelHealthVariant
	{
		internal int Level { get; private set; }

		internal int Health { get; private set; }

		internal int MonsterScale { get; private set; }

		internal int RunSpeed { get; private set; }

		internal string Evidence { get; private set; }

		internal CapturedEncounterLevelHealthVariant(int level, int health, int monsterScale, int runSpeed, string evidence)
		{
			Level = level;
			Health = health;
			MonsterScale = monsterScale;
			RunSpeed = runSpeed;
			Evidence = evidence;
		}
	}

	private sealed class PendingVergilHeal
	{
		internal Identity TargetIdentity { get; private set; }

		internal int NanoId { get; private set; }

		internal int HealAmount { get; private set; }

		internal int DurationMilliseconds { get; private set; }

		internal DateTime FinishAtUtc { get; private set; }

		internal PendingVergilHeal(Identity targetIdentity, int nanoId, int healAmount, int durationMilliseconds, DateTime finishAtUtc)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			TargetIdentity = targetIdentity;
			NanoId = nanoId;
			HealAmount = healAmount;
			DurationMilliseconds = durationMilliseconds;
			FinishAtUtc = finishAtUtc;
		}
	}

	internal const int SubwayPlayfieldId = 127;

	internal const int AbmouthMonsterData = 155962;

	internal const int InfectorMonsterData = 31909;

	internal const int VergilAeneidMonsterData = 203748;

	internal const int EumenidesMonsterData = 203726;

	internal const string AbmouthProfileKey = "subway.127.boss.abmouth-supremus";

	internal const string InfectorProfileKey = "subway.127.encounter.abmouth-infector";

	internal const string VergilAeneidProfileKey = "subway.127.boss.vergil-aeneid";

	internal const string EumenidesProfileKey = "subway.127.named.eumenides";

	internal const string EncounterKey = "subway.127.encounter.abmouth";

	internal const string VergilAeneidEncounterKey = "subway.127.encounter.vergil-aeneid";

	internal const string EumenidesEncounterKey = "subway.127.encounter.eumenides";

	private const float CapturedAggroRadius = 13.4151f;

	private const float CapturedEumenidesAggroRadius = 23.359f;

	private const float CapturedReplacementInfectorOffsetX = 3f;

	private const string FirstInfectorUnknown1 = "80000000000000000000000003010001000100010001000000020000";

	private const string SecondInfectorUnknown1 = "80000000000000008000000003010001000100010001000000020000";

	private const string ReplacementInfectorUnknown1 = "00000000000000008000000003010001000100010001000000020000";

	private const double FirstInfectorDelaySeconds = 1.212281;

	private const double SecondInfectorDelaySeconds = 2.326367;

	private const int VergilDirectHealNanoId = 43827;

	private const int VergilDirectHealAmount = 187;

	private const double VergilDirectHealCastSeconds = 1.480007;

	private const int VergilSelfHealNanoId = 43880;

	private const int VergilSelfHealAmount = 34;

	private const int VergilSelfHealDurationMilliseconds = 14000;

	private const double VergilSelfHealCastSeconds = 1.763334;

	private const double VergilDirectHealCooldownSeconds = 30.654;

	private const int VergilSelfHealTriggerPermille = 180;

	private const float VergilDirectHealRange = 13f;

	private static readonly TimeSpan CapturedNamedBossRespawnDelay = TimeSpan.FromMinutes(10.0);

	private static readonly TimeSpan EumenidesObservedRespawnDelay = TimeSpan.FromMinutes(10.0);

	private static readonly double[] CapturedRefillDelays = new double[4] { 0.83, 0.38, 3.322, 3.49 };

	private static readonly CapturedEncounterLevelHealthVariant[] VergilAeneidVariants = new CapturedEncounterLevelHealthVariant[3]
	{
		new CapturedEncounterLevelHealthVariant(29, 6796, 131, 131, "20260716-034433 fight/corpse"),
		new CapturedEncounterLevelHealthVariant(30, 7227, 132, 135, "20260709-222339 SCFU #5445; 20260712-234401 fight"),
		new CapturedEncounterLevelHealthVariant(31, 7659, 132, 140, "20260712-232711 fight")
	};

	private readonly Playfield playfield;

	private readonly PlayfieldDynelRegistry dynelRegistry;

	private readonly Action<ICharacter> activateNpc;

	private readonly object spawnRandomSync = new object();

	private readonly Random spawnRandom = new Random();

	private readonly InfectorSlotState[] infectorSlots = new InfectorSlotState[2]
	{
		new InfectorSlotState(0),
		new InfectorSlotState(1)
	};

	private Identity abmouthIdentity = Identity.None;

	private Identity vergilAeneidIdentity = Identity.None;

	private Identity eumenidesIdentity = Identity.None;

	private bool combatActive;

	private bool abmouthDead;

	private DateTime? abmouthRespawnDueAtUtc;

	private bool vergilCombatActive;

	private bool vergilDead;

	private DateTime? vergilRespawnDueAtUtc;

	private DateTime? eumenidesRespawnDueAtUtc;

	private DateTime vergilNextHealAtUtc;

	private PendingVergilHeal vergilPendingHeal;

	private int refillDelayIndex;

	internal CapturedSubwayEncounterRuntimeService(Playfield playfield, PlayfieldDynelRegistry dynelRegistry, Action<ICharacter> activateNpc)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		this.playfield = playfield;
		this.dynelRegistry = dynelRegistry;
		this.activateNpc = activateNpc;
	}

	internal void ActivatePlayfield(Identity playfieldIdentity)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance != 127)
		{
			return;
		}
		if (((Identity)(ref abmouthIdentity)).Instance == 0 && !abmouthRespawnDueAtUtc.HasValue)
		{
			CapturedEncounterRuntimeDefinition definition = CreateBossDefinition();
			Character val = SpawnCharacter(definition, Identity.None);
			if (val != null)
			{
				abmouthIdentity = ((PooledObject)val).Identity;
				abmouthDead = false;
			}
		}
		if (((Identity)(ref vergilAeneidIdentity)).Instance == 0 && !vergilRespawnDueAtUtc.HasValue)
		{
			Character val2 = SpawnCharacter(CreateVergilAeneidDefinition(), Identity.None);
			if (val2 != null)
			{
				vergilAeneidIdentity = ((PooledObject)val2).Identity;
			}
		}
		if (((Identity)(ref eumenidesIdentity)).Instance == 0 && !eumenidesRespawnDueAtUtc.HasValue)
		{
			Character val3 = SpawnCharacter(CreateEumenidesDefinition(), Identity.None);
			if (val3 != null)
			{
				eumenidesIdentity = ((PooledObject)val3).Identity;
			}
		}
	}

	internal void ClearRuntimeState()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((PooledObject)playfield).Identity;
		CapturedEncounterRuntimeRegistry.RemoveForPlayfield(((Identity)(ref identity)).Instance);
		abmouthIdentity = Identity.None;
		vergilAeneidIdentity = Identity.None;
		eumenidesIdentity = Identity.None;
		combatActive = false;
		abmouthDead = false;
		abmouthRespawnDueAtUtc = null;
		ClearVergilCombatState();
		vergilRespawnDueAtUtc = null;
		eumenidesRespawnDueAtUtc = null;
		refillDelayIndex = 0;
		InfectorSlotState[] array = infectorSlots;
		foreach (InfectorSlotState infectorSlotState in array)
		{
			infectorSlotState.ActiveIdentity = Identity.None;
			infectorSlotState.SpawnDueAtUtc = null;
			infectorSlotState.Generation = 0;
		}
	}

	internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Identity val = ((ITargetingEntity)npc).FightingTarget;
			if (((Identity)(ref val)).Instance == 0 && ((IStats)npc).Stats[(StatIds)27].Value > 0)
			{
				val = ((IEntity)npc).Identity;
				if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var definition))
				{
					bool flag = string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal);
					bool flag2 = string.Equals(definition.ProfileKey, "subway.127.named.eumenides", StringComparison.Ordinal);
					if (!flag && !flag2)
					{
						return null;
					}
					float range = (flag2 ? 23.359f : 13.4151f);
					return (from candidate in dynelRegistry.FindCharactersInRange((IDynel)(object)npc, range)
						where candidate != null && ((IDynel)candidate).Controller is PlayerController && ((IStats)candidate).Stats[(StatIds)27].Value > 0
						orderby ((IDynel)candidate).Coordinates().coordinate.Distance2D(((IDynel)npc).Coordinates().coordinate)
						select candidate).ThenBy(delegate(ICharacter candidate)
					{
						//IL_0001: Unknown result type (might be due to invalid IL or missing references)
						//IL_0006: Unknown result type (might be due to invalid IL or missing references)
						Identity identity = ((IEntity)candidate).Identity;
						return ((Identity)(ref identity)).Instance;
					}).FirstOrDefault();
				}
			}
		}
		return null;
	}

	internal void NotifyCombatStarted(ICharacter npc, ICharacter target, DateTime utcNow)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || target == null)
		{
			return;
		}
		Identity identity = ((IEntity)npc).Identity;
		if (!CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
		{
			return;
		}
		if (string.Equals(definition.ProfileKey, "subway.127.boss.vergil-aeneid", StringComparison.Ordinal))
		{
			if (!vergilCombatActive)
			{
				vergilCombatActive = true;
				vergilDead = false;
				vergilNextHealAtUtc = utcNow;
				vergilPendingHeal = null;
			}
		}
		else if (string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal) && !combatActive)
		{
			combatActive = true;
			abmouthDead = false;
			infectorSlots[0].SpawnDueAtUtc = utcNow.AddSeconds(1.212281);
			infectorSlots[1].SpawnDueAtUtc = utcNow.AddSeconds(2.326367);
		}
	}

	internal ICharacter[] NotifyCombatReset(ICharacter npc)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Identity val = ((IEntity)npc).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var definition))
			{
				if (string.Equals(definition.ProfileKey, "subway.127.boss.vergil-aeneid", StringComparison.Ordinal))
				{
					ClearVergilCombatState();
					return (ICharacter[])(object)new ICharacter[0];
				}
				if (!string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal))
				{
					return (ICharacter[])(object)new ICharacter[0];
				}
				combatActive = false;
				abmouthDead = false;
				refillDelayIndex = 0;
				List<ICharacter> list = new List<ICharacter>();
				InfectorSlotState[] array = infectorSlots;
				foreach (InfectorSlotState infectorSlotState in array)
				{
					infectorSlotState.SpawnDueAtUtc = null;
					val = infectorSlotState.ActiveIdentity;
					ICharacter val2 = ((((Identity)(ref val)).Instance == 0) ? null : playfield.FindByIdentity<ICharacter>(infectorSlotState.ActiveIdentity));
					infectorSlotState.ActiveIdentity = Identity.None;
					infectorSlotState.Generation = 0;
					if (val2 != null && ((IStats)val2).Stats[(StatIds)27].Value > 0)
					{
						list.Add(val2);
					}
				}
				return list.ToArray();
			}
		}
		return (ICharacter[])(object)new ICharacter[0];
	}

	internal void ProcessDue(DateTime utcNow, Action<ICharacter, ICharacter> acquireAggro)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		ProcessNamedBossRespawns(utcNow);
		ProcessEumenidesRespawn(utcNow);
		ProcessVergilHealing(utcNow);
		if (!combatActive || abmouthDead || ((Identity)(ref abmouthIdentity)).Instance == 0)
		{
			return;
		}
		ICharacter val = playfield.FindByIdentity<ICharacter>(abmouthIdentity);
		if (val == null || ((IStats)val).Stats[(StatIds)27].Value <= 0)
		{
			return;
		}
		ICharacter val2 = playfield.FindByIdentity<ICharacter>(((ITargetingEntity)val).FightingTarget);
		if (val2 == null || ((IStats)val2).Stats[(StatIds)27].Value <= 0)
		{
			return;
		}
		InfectorSlotState[] array = infectorSlots;
		foreach (InfectorSlotState infectorSlotState in array)
		{
			Identity activeIdentity = infectorSlotState.ActiveIdentity;
			if (((Identity)(ref activeIdentity)).Instance == 0 && infectorSlotState.SpawnDueAtUtc.HasValue && !(infectorSlotState.SpawnDueAtUtc.Value > utcNow))
			{
				Character val3 = SpawnInfector(infectorSlotState, val);
				infectorSlotState.SpawnDueAtUtc = null;
				if (val3 != null)
				{
					infectorSlotState.ActiveIdentity = ((PooledObject)val3).Identity;
					infectorSlotState.Generation++;
					acquireAggro(val2, (ICharacter)(object)val3);
				}
			}
		}
	}

	internal ICharacter[] NotifyDeath(ICharacter target, DateTime diedAtUtc)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		if (target != null)
		{
			Identity val = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var definition))
			{
				if (string.Equals(definition.ProfileKey, "subway.127.boss.vergil-aeneid", StringComparison.Ordinal))
				{
					ClearVergilCombatState();
					vergilDead = true;
					vergilRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);
					return (ICharacter[])(object)new ICharacter[0];
				}
				if (string.Equals(definition.ProfileKey, "subway.127.named.eumenides", StringComparison.Ordinal))
				{
					eumenidesRespawnDueAtUtc = diedAtUtc.Add(EumenidesObservedRespawnDelay);
					return (ICharacter[])(object)new ICharacter[0];
				}
				if (!string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal))
				{
					return (ICharacter[])(object)new ICharacter[0];
				}
				abmouthDead = true;
				combatActive = false;
				abmouthRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);
				List<ICharacter> list = new List<ICharacter>();
				InfectorSlotState[] array = infectorSlots;
				foreach (InfectorSlotState infectorSlotState in array)
				{
					infectorSlotState.SpawnDueAtUtc = null;
					val = infectorSlotState.ActiveIdentity;
					ICharacter val2 = ((((Identity)(ref val)).Instance == 0) ? null : playfield.FindByIdentity<ICharacter>(infectorSlotState.ActiveIdentity));
					if (val2 != null && ((IStats)val2).Stats[(StatIds)27].Value > 0)
					{
						((IStats)val2).Stats[(StatIds)196].Value = 0;
						((IDynel)val2).SendChangedStats();
						list.Add(val2);
					}
				}
				return list.ToArray();
			}
		}
		return (ICharacter[])(object)new ICharacter[0];
	}

	private void ProcessNamedBossRespawns(DateTime utcNow)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		if (abmouthRespawnDueAtUtc.HasValue && abmouthRespawnDueAtUtc.Value <= utcNow && ((Identity)(ref abmouthIdentity)).Instance == 0)
		{
			Character val = SpawnCharacter(CreateBossDefinition(), Identity.None);
			if (val != null)
			{
				abmouthIdentity = ((PooledObject)val).Identity;
				abmouthDead = false;
				combatActive = false;
				refillDelayIndex = 0;
				InfectorSlotState[] array = infectorSlots;
				foreach (InfectorSlotState infectorSlotState in array)
				{
					infectorSlotState.ActiveIdentity = Identity.None;
					infectorSlotState.SpawnDueAtUtc = null;
					infectorSlotState.Generation = 0;
				}
				abmouthRespawnDueAtUtc = null;
			}
		}
		if (vergilRespawnDueAtUtc.HasValue && vergilRespawnDueAtUtc.Value <= utcNow && ((Identity)(ref vergilAeneidIdentity)).Instance == 0)
		{
			Character val2 = SpawnCharacter(CreateVergilAeneidDefinition(), Identity.None);
			if (val2 != null)
			{
				vergilAeneidIdentity = ((PooledObject)val2).Identity;
				ClearVergilCombatState();
				vergilRespawnDueAtUtc = null;
			}
		}
	}

	private void ProcessEumenidesRespawn(DateTime utcNow)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (eumenidesRespawnDueAtUtc.HasValue && !(eumenidesRespawnDueAtUtc.Value > utcNow) && ((Identity)(ref eumenidesIdentity)).Instance == 0)
		{
			Character val = SpawnCharacter(CreateEumenidesDefinition(), Identity.None);
			if (val != null)
			{
				eumenidesIdentity = ((PooledObject)val).Identity;
				eumenidesRespawnDueAtUtc = null;
			}
		}
	}

	internal void NotifyNpcDespawn(ICharacter target, DateTime utcNow)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			return;
		}
		Identity identity = ((IEntity)target).Identity;
		if (!CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
		{
			return;
		}
		if (string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal))
		{
			abmouthIdentity = Identity.None;
			combatActive = false;
			return;
		}
		if (string.Equals(definition.ProfileKey, "subway.127.boss.vergil-aeneid", StringComparison.Ordinal))
		{
			vergilAeneidIdentity = Identity.None;
			ClearVergilCombatState();
			return;
		}
		if (string.Equals(definition.ProfileKey, "subway.127.named.eumenides", StringComparison.Ordinal))
		{
			eumenidesIdentity = Identity.None;
			return;
		}
		InfectorSlotState infectorSlotState = infectorSlots.FirstOrDefault((InfectorSlotState value) => value.ActiveIdentity == ((IEntity)target).Identity);
		if (infectorSlotState != null)
		{
			infectorSlotState.ActiveIdentity = Identity.None;
			if (!abmouthDead && combatActive && ((Identity)(ref abmouthIdentity)).Instance != 0)
			{
				double value2 = CapturedRefillDelays[refillDelayIndex % CapturedRefillDelays.Length];
				refillDelayIndex++;
				infectorSlotState.SpawnDueAtUtc = utcNow.AddSeconds(value2);
			}
		}
	}

	internal bool IsCapturedNanoCastInProgress(ICharacter character)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		return character != null && vergilPendingHeal != null && ((IEntity)character).Identity == vergilAeneidIdentity;
	}

	private void ProcessVergilHealing(DateTime utcNow)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		if (!vergilCombatActive || vergilDead || ((Identity)(ref vergilAeneidIdentity)).Instance == 0)
		{
			return;
		}
		ICharacter val = playfield.FindByIdentity<ICharacter>(vergilAeneidIdentity);
		if (val == null || ((IStats)val).Stats[(StatIds)27].Value <= 0)
		{
			return;
		}
		Identity fightingTarget = ((ITargetingEntity)val).FightingTarget;
		if (((Identity)(ref fightingTarget)).Instance == 0)
		{
			return;
		}
		if (vergilPendingHeal != null)
		{
			if (vergilPendingHeal.FinishAtUtc <= utcNow)
			{
				FinishVergilHeal(val, vergilPendingHeal);
			}
		}
		else
		{
			if (vergilNextHealAtUtc > utcNow)
			{
				return;
			}
			switch (((IStats)val).Stats[(StatIds)54].Value)
			{
			case 31:
			{
				ICharacter val2 = FindVergilDirectHealTarget(val);
				if (val2 != null)
				{
					StartVergilHeal(val, val2, 43827, 187, 1.480007, 0, utcNow);
					vergilNextHealAtUtc = utcNow.AddSeconds(30.654);
				}
				break;
			}
			case 30:
			{
				int value = ((IStats)val).Stats[(StatIds)1].Value;
				int value2 = ((IStats)val).Stats[(StatIds)27].Value;
				if (value > 0 && value2 * 1000 <= value * 180)
				{
					StartVergilHeal(val, val, 43880, 34, 1.763334, 14000, utcNow);
					vergilNextHealAtUtc = DateTime.MaxValue;
				}
				break;
			}
			}
		}
	}

	private ICharacter FindVergilDirectHealTarget(ICharacter vergil)
	{
		IEnumerable<ICharacter> enumerable = from candidate in dynelRegistry.FindCharactersInRange((IDynel)(object)vergil, 13f)
			where candidate != null && ((IEntity)candidate).Identity != ((IEntity)vergil).Identity && ((IDynel)candidate).Controller is NPCController && ((IStats)candidate).Stats[(StatIds)196].Value == 0 && ((IStats)candidate).Stats[(StatIds)27].Value > 0 && ((IStats)candidate).Stats[(StatIds)1].Value > 0 && ((IStats)candidate).Stats[(StatIds)27].Value < ((IStats)candidate).Stats[(StatIds)1].Value
			select candidate;
		IEnumerable<ICharacter> enumerable2 = enumerable;
		if (((IStats)vergil).Stats[(StatIds)27].Value > 0 && ((IStats)vergil).Stats[(StatIds)27].Value < ((IStats)vergil).Stats[(StatIds)1].Value)
		{
			enumerable2 = enumerable2.Concat((IEnumerable<ICharacter>)(object)new ICharacter[1] { vergil });
		}
		return enumerable2.OrderBy((ICharacter candidate) => (double)((IStats)candidate).Stats[(StatIds)27].Value / (double)((IStats)candidate).Stats[(StatIds)1].Value).ThenBy(delegate(ICharacter candidate)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity identity = ((IEntity)candidate).Identity;
			return ((Identity)(ref identity)).Instance;
		}).FirstOrDefault();
	}

	private void StartVergilHeal(ICharacter vergil, ICharacter target, int nanoId, int healAmount, double castSeconds, int durationMilliseconds, DateTime utcNow)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		vergilPendingHeal = new PendingVergilHeal(((IEntity)target).Identity, nanoId, healAmount, durationMilliseconds, utcNow.AddSeconds(castSeconds));
		BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.Send(vergil, nanoId, ((IEntity)target).Identity);
	}

	private void FinishVergilHeal(ICharacter vergil, PendingVergilHeal pending)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Expected O, but got Unknown
		vergilPendingHeal = null;
		ICharacter val = playfield.FindByIdentity<ICharacter>(pending.TargetIdentity);
		if (val != null && ((IStats)val).Stats[(StatIds)27].Value > 0)
		{
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.FinishNanoCasting(vergil, (CharacterActionType)107, Identity.None, 1, pending.NanoId);
			if (pending.DurationMilliseconds > 0)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.NotifyActiveNanoDuration(vergil, ((IEntity)val).Identity, pending.NanoId, pending.DurationMilliseconds);
			}
			int value = ((IStats)val).Stats[(StatIds)27].Value;
			int value2 = ((IStats)val).Stats[(StatIds)1].Value;
			int num = Math.Min(value2, value + pending.HealAmount);
			int num2 = num - value;
			if (num2 > 0)
			{
				((IStats)val).Stats[(StatIds)27].Value = num;
				playfield.Announce((MessageBody)new HealthDamageMessage
				{
					Identity = ((IEntity)val).Identity,
					Unknown1 = num,
					Unknown2 = num2,
					Unknown3 = 0,
					Unknown4 = 0,
					Target = ((IEntity)vergil).Identity,
					Unknown5 = 0
				});
			}
		}
	}

	private void ClearVergilCombatState()
	{
		vergilCombatActive = false;
		vergilDead = false;
		vergilNextHealAtUtc = DateTime.MinValue;
		vergilPendingHeal = null;
	}

	private Character SpawnInfector(InfectorSlotState slot, ICharacter boss)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		CapturedEncounterRuntimeDefinition definition = ((slot.Generation != 0) ? CreateInfectorDefinition(slot, ((IDynel)boss).RawCoordinates.X + 3f, ((IDynel)boss).RawCoordinates.Y, ((IDynel)boss).RawCoordinates.Z, ((IDynel)boss).RawHeading.xf, ((IDynel)boss).RawHeading.yf, ((IDynel)boss).RawHeading.zf, ((IDynel)boss).RawHeading.wf, "00000000000000008000000003010001000100010001000000020000") : ((slot.Slot == 0) ? CreateFirstInfectorDefinition() : CreateSecondInfectorDefinition()));
		return SpawnCharacter(definition, ((IEntity)boss).Identity);
	}

	private Character SpawnCharacter(CapturedEncounterRuntimeDefinition definition, Identity ownerIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Expected O, but got Unknown
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Expected O, but got Unknown
		//IL_03ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		//IL_048d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_045a: Unknown result type (might be due to invalid IL or missing references)
		int freeInstance = Pool.Instance.GetFreeInstance<Character>(1000000, (IdentityType)50000);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = freeInstance;
		Identity val2 = val;
		NPCController nPCController = new NPCController();
		Character val3 = new Character(((PooledObject)playfield).Identity, val2, (IController)(object)nPCController);
		((Dynel)val3).Read();
		nPCController.Character = (ICharacter)(object)val3;
		((Dynel)val3).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val3).Name = definition.DisplayName;
		val3.FirstName = string.Empty;
		val3.LastName = string.Empty;
		((Dynel)val3).Coordinates(new Coordinate
		{
			x = definition.X,
			y = definition.Y,
			z = definition.Z
		});
		((Dynel)val3).RawHeading = new Quaternion((double)definition.HeadingX, (double)definition.HeadingY, (double)definition.HeadingZ, (double)definition.HeadingW);
		SetStat((ICharacter)(object)val3, (StatIds)33, definition.Side);
		SetStat((ICharacter)(object)val3, (StatIds)47, definition.Fatness);
		SetStat((ICharacter)(object)val3, (StatIds)4, definition.Breed);
		SetStat((ICharacter)(object)val3, (StatIds)59, definition.Sex);
		SetStat((ICharacter)(object)val3, (StatIds)89, definition.Race);
		SetStat((ICharacter)(object)val3, (StatIds)0, 268964353);
		SetStat((ICharacter)(object)val3, (StatIds)660, 0);
		SetStat((ICharacter)(object)val3, (StatIds)389, 0);
		SetStat((ICharacter)(object)val3, (StatIds)455, definition.NpcFamily);
		SetStat((ICharacter)(object)val3, (StatIds)466, definition.NpcLosHeight);
		SetStat((ICharacter)(object)val3, (StatIds)359, definition.MonsterData);
		SetStat((ICharacter)(object)val3, (StatIds)360, definition.MonsterScale);
		SetStat((ICharacter)(object)val3, (StatIds)64, definition.HeadMesh);
		SetStat((ICharacter)(object)val3, (StatIds)673, 31);
		SetStat((ICharacter)(object)val3, (StatIds)173, 3);
		SetStat((ICharacter)(object)val3, (StatIds)174, 3);
		SetStat((ICharacter)(object)val3, (StatIds)156, definition.RunSpeed);
		SetStat((ICharacter)(object)val3, (StatIds)60, 1);
		SetStat((ICharacter)(object)val3, (StatIds)37, 1);
		SetStat((ICharacter)(object)val3, (StatIds)54, definition.Level);
		SetStat((ICharacter)(object)val3, (StatIds)1, definition.Health);
		SetStat((ICharacter)(object)val3, (StatIds)27, definition.Health);
		SetStat((ICharacter)(object)val3, (StatIds)161, 6);
		if (((Identity)(ref ownerIdentity)).Instance != 0)
		{
			SetStat((ICharacter)(object)val3, (StatIds)196, ((Identity)(ref ownerIdentity)).Instance);
		}
		((Dynel)val3).Textures.Clear();
		CapturedSubwayTextureDefinition[] textures = definition.Textures;
		foreach (CapturedSubwayTextureDefinition capturedSubwayTextureDefinition in textures)
		{
			((Dynel)val3).Textures.Add(new AOTextures(capturedSubwayTextureDefinition.Place, capturedSubwayTextureDefinition.Id));
		}
		CapturedSubwayMeshDefinition[] meshes = definition.Meshes;
		foreach (CapturedSubwayMeshDefinition capturedSubwayMeshDefinition in meshes)
		{
			((Dynel)val3).MeshLayer.AddMesh(capturedSubwayMeshDefinition.Position, (int)capturedSubwayMeshDefinition.Id, capturedSubwayMeshDefinition.OverrideTextureId, capturedSubwayMeshDefinition.Layer);
			val3.SocialMeshLayer.AddMesh(capturedSubwayMeshDefinition.Position, (int)capturedSubwayMeshDefinition.Id, capturedSubwayMeshDefinition.OverrideTextureId, capturedSubwayMeshDefinition.Layer);
		}
		val3.Waypoints.Clear();
		CapturedSubwayWaypointDefinition[] waypoints = definition.Waypoints;
		foreach (CapturedSubwayWaypointDefinition capturedSubwayWaypointDefinition in waypoints)
		{
			val3.AddWaypoint(new Vector3((double)capturedSubwayWaypointDefinition.X, (double)capturedSubwayWaypointDefinition.Y, (double)capturedSubwayWaypointDefinition.Z), false);
		}
		CapturedEnemyCombatContract contract = CapturedSubwayCombatCatalog.For(definition.DisplayName, definition.MonsterData, definition.Level);
		if (!CapturedEnemyCombatRuntime.Prepare(val3, nPCController, contract, out var failure))
		{
			LogUtil.Debug((DebugInfoDetail)512, "Captured Subway encounter combat refused actor=" + definition.ProfileKey + " reason=" + failure);
			Pool.Instance.RemoveObject<Character>(val3);
			return null;
		}
		((Dynel)val3).DoNotDoTimers = false;
		val = ((PooledObject)val3).Identity;
		CapturedEncounterRuntimeRegistry.Register(((Identity)(ref val)).Instance, definition);
		activateNpc((ICharacter)(object)val3);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val3, Identity.None);
		if (((Identity)(ref ownerIdentity)).Instance != 0)
		{
			AnnounceCapturedInfectorStat(((PooledObject)val3).Identity, (StatIds)196, ((Identity)(ref ownerIdentity)).Instance);
			SetStat((ICharacter)(object)val3, (StatIds)0, 403182081);
			AnnounceCapturedInfectorStat(((PooledObject)val3).Identity, (StatIds)0, 403182081);
		}
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Encounter actor spawned profile={0} identity={1} position=({2},{3},{4}) evidence={5}", definition.ProfileKey, ((PooledObject)val3).Identity, definition.X, definition.Y, definition.Z, definition.Evidence));
		return val3;
	}

	private void AnnounceCapturedInfectorStat(Identity identity, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Playfield obj = playfield;
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = identity;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)stat,
				Value2 = (uint)value
			}
		};
		obj.Announce((MessageBody)(object)val);
	}

	private static void SetStat(ICharacter character, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected I4, but got Unknown
		((IStats)character).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
	}

	private static CapturedEncounterRuntimeDefinition CreateBossDefinition()
	{
		return new CapturedEncounterRuntimeDefinition("subway.127.boss.abmouth-supremus", "subway.127.boss.abmouth-supremus.spawn", "subway.127.encounter.abmouth", "Abmouth Supremus", 155962, isBoss: true, isEncounterSummon: false, 30, 10324, 162, 115, 114, 0, 3, 357.0884f, 76.10795f, 99.12354f, 0f, -0.7132262f, 0f, 0.70093393f, 1227u, 36325955, 0, HexToBytes("80000000000000008000000003010001000100010001000000020000"), 0, 155548, 1800.0, 3.0, "20260712-224840 SCFU #1808; 20260712-232137 fight/corpse/loot; 20260716-220400 spawn/fight/death/corpse");
	}

	private CapturedEncounterRuntimeDefinition CreateVergilAeneidDefinition()
	{
		CapturedEncounterLevelHealthVariant capturedEncounterLevelHealthVariant;
		lock (spawnRandomSync)
		{
			capturedEncounterLevelHealthVariant = VergilAeneidVariants[spawnRandom.Next(VergilAeneidVariants.Length)];
		}
		return new CapturedEncounterRuntimeDefinition("subway.127.boss.vergil-aeneid", "subway.127.boss.vergil-aeneid.spawn", "subway.127.encounter.vergil-aeneid", "Vergil Aeneid", 203748, isBoss: true, isEncounterSummon: false, capturedEncounterLevelHealthVariant.Level, capturedEncounterLevelHealthVariant.Health, capturedEncounterLevelHealthVariant.MonsterScale, capturedEncounterLevelHealthVariant.RunSpeed, 134, 0, 3, 278.04507f, 73.01795f, 98.80104f, 0f, -0.7096085f, 0f, 0.70459616f, 1643u, 34294475, 0, HexToBytes("00000000000000000000000002010001000100010001000000020000"), 0, 5921, 1800.0, 3.0, capturedEncounterLevelHealthVariant.Evidence + "; exact spawn/appearance 20260709-222339 SCFU #5445; Mike 20260716 30-minute loot corpse and 10-minute respawn; 20260716-222007 two approximately 40-unit leash resets", 138, 0, 1, 3, 2, 1, 40171, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 117653, 0),
			new CapturedSubwayTextureDefinition(1, 9609, 0),
			new CapturedSubwayTextureDefinition(2, 9615, 0),
			new CapturedSubwayTextureDefinition(3, 9607, 0),
			new CapturedSubwayTextureDefinition(4, 9622, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 40171u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 21126u, 0, 2)
		}, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(278.04507f, 73.01795f, 98.80104f)
		}, 40.0);
	}

	private static CapturedEncounterRuntimeDefinition CreateEumenidesDefinition()
	{
		return new CapturedEncounterRuntimeDefinition("subway.127.named.eumenides", "subway.127.named.eumenides.spawn", "subway.127.encounter.eumenides", "Eumenides", 203726, isBoss: false, isEncounterSummon: false, 20, 2792, 130, 76, 76, 0, 3, 241.10513f, 73.045395f, 44.046906f, 0f, 0.25087696f, 0f, -0.96801883f, 1643u, 34228939, 0, HexToBytes("80000000000000000000000002010001000100010001000000020000"), 0, 17905, 1800.0, 3.0, "20260716-034559 atomic SCFU; 20260709-222339 plus 20260717-214612/214751/215250 weapon/combat/chase; 20260716-222007 exact 416-byte corpse CATMesh 17905/MonsterData 203726/scale 130; 20260717-214751/215250 two exact 186-credit item-plus-credit corpse snapshots; 20260717-220340-associated Mike observation (not packet-timestamp encoded): official-live exact 10-minute respawn and Temporary 30m loot-bearing corpse; confirmed 3-second empty cleanup and shared 100-unit leash; active nano refresh unresolved and omitted", 148, 0, 1, 3, 2, 1, 29708, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9620, 0),
			new CapturedSubwayTextureDefinition(1, 9612, 0),
			new CapturedSubwayTextureDefinition(2, 9618, 0),
			new CapturedSubwayTextureDefinition(3, 99779, 0),
			new CapturedSubwayTextureDefinition(4, 9625, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 29708u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 35564u, 0, 2)
		}, null, 100.0);
	}

	private static CapturedEncounterRuntimeDefinition CreateFirstInfectorDefinition()
	{
		return CreateInfectorDefinition(new InfectorSlotState(0), 355.54214f, 68.9559f, 99.45995f, 0f, -0.6734858f, 0f, 0.7392001f, "80000000000000000000000003010001000100010001000000020000");
	}

	private static CapturedEncounterRuntimeDefinition CreateSecondInfectorDefinition()
	{
		return CreateInfectorDefinition(new InfectorSlotState(1), 350.4255f, 71.64708f, 99.78681f, 0f, -0.7155183f, 0f, 0.69859403f, "80000000000000008000000003010001000100010001000000020000");
	}

	private static CapturedEncounterRuntimeDefinition CreateInfectorDefinition(InfectorSlotState slot, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, string capturedScfuUnknown1)
	{
		return new CapturedEncounterRuntimeDefinition("subway.127.encounter.abmouth-infector", "subway.127.encounter.abmouth-infector.slot." + slot.Slot, "subway.127.encounter.abmouth", "Infector", 31909, isBoss: false, isEncounterSummon: true, 24, 968, 70, 162, 105, 10, 0, x, y, z, headingX, headingY, headingZ, headingW, 1224u, 36325955, 2, HexToBytes(capturedScfuUnknown1), 0, 31868, 300.0, 3.0, "20260712-224840 SCFU #1835/#1870; 20260712-232137 two-slot refill");
	}

	private static byte[] HexToBytes(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
