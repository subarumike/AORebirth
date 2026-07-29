using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Nanos;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyRuntimeService
{
	private readonly OrdinaryEnemyCatalog catalog;

	private readonly NpcPatrolReplayCoordinator patrolReplay;

	private readonly PlayfieldDynelRegistry dynelRegistry;

	private readonly Action<ICharacter> activateNpc;

	private readonly Random spawnRandom;

	private readonly Func<int, int> levelSelector;

	private readonly Dictionary<int, OrdinaryEnemyLevelSelectionState> levelSelectionBySource = new Dictionary<int, OrdinaryEnemyLevelSelectionState>();

	private readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> activeByRuntimeIdentity = new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

	private readonly Dictionary<int, int> activeRuntimeIdentityBySource = new Dictionary<int, int>();

	private readonly Dictionary<int, OrdinaryEnemySupportNanoRuntimeState> supportNanoStateByRuntimeIdentity = new Dictionary<int, OrdinaryEnemySupportNanoRuntimeState>();

	private readonly Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> transientNanoEffectsByRecipient = new Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>>();

	internal OrdinaryEnemyRuntimeService(OrdinaryEnemyCatalog catalog, NpcPatrolReplayCoordinator patrolReplay, PlayfieldDynelRegistry dynelRegistry, Action<ICharacter> activateNpc, Func<int, int> levelSelector = null)
	{
		this.catalog = catalog;
		this.patrolReplay = patrolReplay;
		this.dynelRegistry = dynelRegistry;
		this.activateNpc = activateNpc;
		if (levelSelector == null)
		{
			spawnRandom = new Random();
			this.levelSelector = spawnRandom.Next;
		}
		else
		{
			this.levelSelector = levelSelector;
		}
	}

	internal bool SpawnFromPopulation(Playfield playfield, Identity playfieldIdentity, OrdinaryEnemySpawnDefinition spawn, int generation, out Identity runtimeIdentity, out OrdinaryEnemySpawnGeneration selectedGeneration)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		runtimeIdentity = Identity.None;
		selectedGeneration = null;
		if (spawn == null)
		{
			return false;
		}
		if (activeRuntimeIdentityBySource.ContainsKey(spawn.SourceIdentity))
		{
			return false;
		}
		if (!catalog.TryGetProfile(spawn.ProfileKey, out var profile))
		{
			SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(spawn.SourceIdentity, "profile lookup failed");
			return false;
		}
		OrdinaryEnemySpawnGeneration ordinaryEnemySpawnGeneration;
		bool flag;
		try
		{
			if (!levelSelectionBySource.TryGetValue(spawn.SourceIdentity, out var value))
			{
				value = new OrdinaryEnemyLevelSelectionState();
				levelSelectionBySource.Add(spawn.SourceIdentity, value);
			}
			ordinaryEnemySpawnGeneration = value.ResolveForGeneration(spawn.LevelDefinition, generation, levelSelector);
			flag = Spawn(playfield, playfieldIdentity, spawn, profile, ordinaryEnemySpawnGeneration, out runtimeIdentity);
		}
		catch (Exception ex)
		{
			SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(spawn.SourceIdentity, "materialization exception: " + ex.GetType().Name);
			throw;
		}
		if (flag)
		{
			selectedGeneration = ordinaryEnemySpawnGeneration;
		}
		else
		{
			SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(spawn.SourceIdentity, "runtime materialization returned false");
		}
		return flag;
	}

	internal void ClearRuntimeState(int playfieldInstance)
	{
		RemoveAllTransientNanoEffects();
		int[] array = activeByRuntimeIdentity.Keys.ToArray();
		foreach (int num in array)
		{
			OrdinaryEnemyRuntimeRegistry.Remove(num);
			SubwayVisibilityDiagnosticSelection.RemoveRuntimeIdentity(num);
			CapturedEnemyCombatRuntimeRegistry.Remove(num);
		}
		activeByRuntimeIdentity.Clear();
		activeRuntimeIdentityBySource.Clear();
		supportNanoStateByRuntimeIdentity.Clear();
		transientNanoEffectsByRecipient.Clear();
		levelSelectionBySource.Clear();
		OrdinaryEnemyRuntimeRegistry.RemoveForPlayfield(playfieldInstance);
	}

	internal bool ReleasePopulationRuntime(ICharacter target, out OrdinaryEnemyRuntimeDefinition definition)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		definition = null;
		if (target != null)
		{
			Dictionary<int, OrdinaryEnemyRuntimeDefinition> dictionary = activeByRuntimeIdentity;
			Identity identity = ((IEntity)target).Identity;
			if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out definition))
			{
				Dictionary<int, OrdinaryEnemyRuntimeDefinition> dictionary2 = activeByRuntimeIdentity;
				identity = ((IEntity)target).Identity;
				dictionary2.Remove(((Identity)(ref identity)).Instance);
				activeRuntimeIdentityBySource.Remove(definition.Spawn.SourceIdentity);
				Dictionary<int, OrdinaryEnemySupportNanoRuntimeState> dictionary3 = supportNanoStateByRuntimeIdentity;
				identity = ((IEntity)target).Identity;
				dictionary3.Remove(((Identity)(ref identity)).Instance);
				identity = ((IEntity)target).Identity;
				RemoveTransientNanoEffectsForCaster(((Identity)(ref identity)).Instance);
				RemoveTransientNanoEffectsForRecipient(target);
				return true;
			}
		}
		return false;
	}

	internal void NotifyCharacterDied(ICharacter character)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			Dictionary<int, OrdinaryEnemySupportNanoRuntimeState> dictionary = supportNanoStateByRuntimeIdentity;
			Identity identity = ((IEntity)character).Identity;
			dictionary.Remove(((Identity)(ref identity)).Instance);
			identity = ((IEntity)character).Identity;
			RemoveTransientNanoEffectsForCaster(((Identity)(ref identity)).Instance);
			RemoveTransientNanoEffectsForRecipient(character);
		}
	}

	internal void ProcessExpiredSupportNanoEffects(DateTime utcNow)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		OrdinaryEnemyTransientNanoEffectState[] array = (from value in transientNanoEffectsByRecipient.SelectMany((KeyValuePair<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> value) => value.Value.Values)
			where value.PeriodicSchedule != null && value.PeriodicSchedule.RemainingTicks > 0 && value.PeriodicSchedule.NextTickAtUtc <= utcNow
			select value).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState in array)
		{
			ICharacter recipient = dynelRegistry.FindByIdentity<ICharacter>(ordinaryEnemyTransientNanoEffectState.RecipientIdentity);
			ProcessPeriodicNanoTicks(ordinaryEnemyTransientNanoEffectState, recipient, utcNow);
		}
		OrdinaryEnemyTransientNanoEffectState[] array2 = (from value in transientNanoEffectsByRecipient.SelectMany((KeyValuePair<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> value) => value.Value.Values)
			where value.ExpiresAtUtc <= utcNow
			select value).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState2 in array2)
		{
			ICharacter recipient2 = dynelRegistry.FindByIdentity<ICharacter>(ordinaryEnemyTransientNanoEffectState2.RecipientIdentity);
			RemoveTransientNanoEffect(ordinaryEnemyTransientNanoEffectState2, recipient2);
		}
	}

	internal bool TryProcessSupportNano(ICharacter caster, DateTime utcNow)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		if (caster != null && ((IStats)caster).Stats[(StatIds)27].Value > 0)
		{
			Dictionary<int, OrdinaryEnemyRuntimeDefinition> dictionary = activeByRuntimeIdentity;
			Identity val = ((IEntity)caster).Identity;
			if (dictionary.TryGetValue(((Identity)(ref val)).Instance, out var value) && value.Profile.SupportNano != null)
			{
				Dictionary<int, OrdinaryEnemySupportNanoRuntimeState> dictionary2 = supportNanoStateByRuntimeIdentity;
				val = ((IEntity)caster).Identity;
				if (dictionary2.TryGetValue(((Identity)(ref val)).Instance, out var value2))
				{
					OrdinaryEnemySupportNanoProfile supportNano = value.Profile.SupportNano;
					bool flag = !supportNano.AllowCombatActionsDuringCast;
					if (value2.CastInProgress)
					{
						if (utcNow < value2.FinishAtUtc)
						{
							return flag;
						}
						FinishSupportNanoCast(caster, supportNano, value2, utcNow);
						value2.CastInProgress = false;
						value2.TargetIdentity = Identity.None;
						return flag;
					}
					if (!supportNano.CastWhileFighting)
					{
						val = ((ITargetingEntity)caster).FightingTarget;
						if (((Identity)(ref val)).Instance != 0)
						{
							goto IL_0108;
						}
					}
					if (!(utcNow < value2.NextCastAtUtc))
					{
						value2.NextCastAtUtc = utcNow.AddSeconds(supportNano.RepeatSeconds);
						if (!RollSupportNanoChance(supportNano.CastChanceBasisPoints))
						{
							return false;
						}
						ICharacter val2 = FindSupportNanoTarget(caster, supportNano);
						if (val2 == null)
						{
							return false;
						}
						if (supportNano.ResolvePrimaryModifierFromNanoData && !TryResolveNanoDataStaticModifier(supportNano.PrimaryNanoId, out var _, out var _))
						{
							return false;
						}
						if (!OrdinaryEnemySupportNanoRuntimeRules.TrySpendNano(((IStats)caster).Stats[(StatIds)214].Value, supportNano.NanoCost, out var remainingNano))
						{
							return false;
						}
						if (supportNano.NanoCost > 0)
						{
							((IStats)caster).Stats[(StatIds)214].Value = remainingNano;
							BaseMessageHandler<StatMessage, StatMessageHandler>.Default.AnnounceSingle(caster, 214, (uint)remainingNano);
						}
						if (flag)
						{
							((IDynel)caster).Controller.StopMovement();
						}
						BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.SendNpcCast(caster, supportNano.PrimaryNanoId, ((IEntity)val2).Identity);
						value2.CastInProgress = true;
						value2.TargetIdentity = ((IEntity)val2).Identity;
						value2.FinishAtUtc = utcNow.AddSeconds(supportNano.CastSeconds);
						return flag;
					}
					goto IL_0108;
				}
			}
		}
		return false;
		IL_0108:
		return false;
	}

	internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Identity identity = ((IEntity)npc).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition) && definition.Profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto && definition.Profile.Aggression.AutomaticAggroRadius.HasValue)
			{
				return (from candidate in dynelRegistry.FindCharactersInRange((IDynel)(object)npc, (float)definition.Profile.Aggression.AutomaticAggroRadius.Value)
					where candidate != null && ((IEntity)candidate).Identity != ((IEntity)npc).Identity && ((IDynel)candidate).Controller is PlayerController && ((IStats)candidate).Stats[(StatIds)27].Value > 0
					orderby ((IDynel)candidate).Coordinates().coordinate.Distance2D(((IDynel)npc).Coordinates().coordinate)
					select candidate).ThenBy(delegate(ICharacter candidate)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					Identity identity2 = ((IEntity)candidate).Identity;
					return ((Identity)(ref identity2)).Instance;
				}).FirstOrDefault();
			}
		}
		return null;
	}

	internal void TryReturnToSpawn(ICharacter npc)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		NPCController nPCController = ((npc == null) ? null : (((IDynel)npc).Controller as NPCController));
		if (nPCController == null)
		{
			return;
		}
		Identity val = ((ITargetingEntity)npc).FightingTarget;
		if (((Identity)(ref val)).Instance != 0 || nPCController.IsFollowing())
		{
			return;
		}
		val = ((IEntity)npc).Identity;
		if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var definition) && definition.Profile.Aggression.ReturnToSpawn && definition.Spawn.MovementMode == OrdinaryEnemyMovementMode.Static)
		{
			Vector3 val2 = new Vector3((double)definition.Spawn.X, (double)definition.Spawn.Y, (double)definition.Spawn.Z);
			if (!(((IDynel)npc).Coordinates().coordinate.Distance2D(val2) <= 0.5))
			{
				nPCController.MoveTo(new Vector3
				{
					X = definition.Spawn.X,
					Y = definition.Spawn.Y,
					Z = definition.Spawn.Z
				});
			}
		}
	}

	private bool Spawn(Playfield playfield, Identity playfieldIdentity, OrdinaryEnemySpawnDefinition spawn, OrdinaryEnemyProfile profile, OrdinaryEnemySpawnGeneration spawnGeneration, out Identity runtimeIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		runtimeIdentity = Identity.None;
		NPCController controller = new NPCController();
		OrdinaryEnemySpawnVariant selectedVariant = spawnGeneration.SelectedVariant;
		Character val = ConstructCharacter(playfield, playfieldIdentity, spawn, selectedVariant, profile, controller);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Ordinary enemy spawn construction failed profile=" + profile.ProfileKey);
			return false;
		}
		ApplyStats(val, selectedVariant, profile);
		ApplyAppearance(val, profile);
		ApplyMovement(val, controller, spawn);
		CapturedEnemyCombatContract capturedEnemyCombatContract = profile.Combat.ResolveContract(spawn.SourceIdentity, selectedVariant);
		string failure;
		bool flag = CapturedEnemyCombatRuntime.Prepare(val, controller, capturedEnemyCombatContract, out failure);
		Identity identity;
		if (!flag)
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "Ordinary enemy combat contract incomplete sourceIdentity=SimpleChar:{0:X8} profile={1} reason={2}", spawn.SourceIdentity, profile.ProfileKey, failure));
			identity = ((PooledObject)val).Identity;
			CapturedEnemyCombatRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
			return false;
		}
		((Dynel)val).DoNotDoTimers = false;
		OrdinaryEnemyRuntimeDefinition ordinaryEnemyRuntimeDefinition = new OrdinaryEnemyRuntimeDefinition(spawn, profile, spawnGeneration);
		identity = ((PooledObject)val).Identity;
		OrdinaryEnemyRuntimeRegistry.Register(((Identity)(ref identity)).Instance, ordinaryEnemyRuntimeDefinition);
		activateNpc((ICharacter)(object)val);
		Dictionary<int, OrdinaryEnemyRuntimeDefinition> dictionary = activeByRuntimeIdentity;
		identity = ((PooledObject)val).Identity;
		dictionary[((Identity)(ref identity)).Instance] = ordinaryEnemyRuntimeDefinition;
		Dictionary<int, int> dictionary2 = activeRuntimeIdentityBySource;
		int sourceIdentity = spawn.SourceIdentity;
		identity = ((PooledObject)val).Identity;
		dictionary2[sourceIdentity] = ((Identity)(ref identity)).Instance;
		if (profile.SupportNano != null)
		{
			Dictionary<int, OrdinaryEnemySupportNanoRuntimeState> dictionary3 = supportNanoStateByRuntimeIdentity;
			identity = ((PooledObject)val).Identity;
			dictionary3[((Identity)(ref identity)).Instance] = new OrdinaryEnemySupportNanoRuntimeState
			{
				NextCastAtUtc = DateTime.UtcNow.AddSeconds(SelectSupportNanoInitialDelay(profile.SupportNano))
			};
		}
		identity = ((PooledObject)val).Identity;
		SubwayVisibilityDiagnosticSelection.RegisterRuntimeIdentity(((Identity)(ref identity)).Instance, spawn.SourceIdentity);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		runtimeIdentity = ((PooledObject)val).Identity;
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Ordinary enemy spawned sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} profile={2} name={3} monsterData={4} level={5} position=({6},{7},{8}) combatModel={9} combatReady={10}", spawn.SourceIdentity, ((PooledObject)val).Identity, profile.ProfileKey, profile.DisplayName, profile.MonsterData, selectedVariant.Level, spawn.X, spawn.Y, spawn.Z, capturedEnemyCombatContract.AttackModel, flag));
		return true;
	}

	private ICharacter FindSupportNanoTarget(ICharacter caster, OrdinaryEnemySupportNanoProfile profile)
	{
		if (RollSupportNanoChance(profile.SelfTargetChanceBasisPoints))
		{
			return caster;
		}
		ICharacter val = (from candidate in dynelRegistry.FindCharactersInRange((IDynel)(object)caster, (float)profile.TargetRange)
			where candidate != null && ((IEntity)candidate).Identity != ((IEntity)caster).Identity && ((IStats)candidate).Stats[(StatIds)27].Value > 0 && IsOrdinaryEnemy(candidate)
			orderby ((IDynel)candidate).Coordinates().coordinate.Distance2D(((IDynel)caster).Coordinates().coordinate)
			select candidate).ThenBy(delegate(ICharacter candidate)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity identity = ((IEntity)candidate).Identity;
			return ((Identity)(ref identity)).Instance;
		}).FirstOrDefault();
		return val ?? (profile.FallbackToSelf ? caster : null);
	}

	private double SelectSupportNanoInitialDelay(OrdinaryEnemySupportNanoProfile profile)
	{
		return OrdinaryEnemySupportNanoRuntimeRules.SelectInitialDelaySeconds(profile, levelSelector);
	}

	private bool RollSupportNanoChance(int chanceBasisPoints)
	{
		return OrdinaryEnemySupportNanoRuntimeRules.RollChance(chanceBasisPoints, levelSelector);
	}

	internal static bool TryResolveNanoDataStaticModifier(int nanoId, out int statId, out int modifierDelta)
	{
		statId = 0;
		modifierDelta = 0;
		NanoFormula value;
		return NanoLoader.NanoList.TryGetValue(nanoId, out value) && TryResolveNanoDataStaticModifier(value, out statId, out modifierDelta);
	}

	internal static bool TryResolveNanoDataStaticModifier(NanoFormula nano, out int statId, out int modifierDelta)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		statId = 0;
		modifierDelta = 0;
		if (nano == null || nano.Events == null || nano.Events.Count != 1)
		{
			return false;
		}
		Event val = nano.Events[0];
		if (val == null || (int)val.EventType != 0 || val.Functions == null || val.Functions.Count != 1)
		{
			return false;
		}
		Function val2 = val.Functions[0];
		if (val2 == null || val2.FunctionType != 53012 || val2.Target != 3 || val2.TickCount != 1 || val2.TickInterval != 0 || !val2.dolocalstats || val2.Requirements == null || val2.Requirements.Count != 0 || val2.Arguments == null || val2.Arguments.Values == null || val2.Arguments.Values.Count != 2)
		{
			return false;
		}
		MessagePackObject val3 = val2.Arguments.Values[0];
		statId = ((MessagePackObject)(ref val3)).AsInt32();
		val3 = val2.Arguments.Values[1];
		modifierDelta = ((MessagePackObject)(ref val3)).AsInt32();
		return statId > 0 && modifierDelta != 0;
	}

	private static bool IsOrdinaryEnemy(ICharacter candidate)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (candidate != null)
		{
			Identity identity = ((IEntity)candidate).Identity;
			result = (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var _) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private void FinishSupportNanoCast(ICharacter caster, OrdinaryEnemySupportNanoProfile profile, OrdinaryEnemySupportNanoRuntimeState state, DateTime utcNow)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.FinishNanoCasting(caster, (CharacterActionType)107, Identity.None, 1, profile.PrimaryNanoId);
		ICharacter val = dynelRegistry.FindByIdentity<ICharacter>(state.TargetIdentity);
		if (val == null || ((IStats)val).Stats[(StatIds)27].Value <= 0)
		{
			return;
		}
		int modifierDelta = profile.PrimaryModifierDelta;
		int[] affectedStatIds = profile.AffectedStatIds;
		if (profile.ResolvePrimaryModifierFromNanoData)
		{
			if (!TryResolveNanoDataStaticModifier(profile.PrimaryNanoId, out var statId, out modifierDelta))
			{
				return;
			}
			affectedStatIds = new int[1] { statId };
		}
		if (profile.HasPeriodicStatHit ? ApplyOrRefreshPeriodicNanoHit(caster, val, profile, utcNow) : ApplyOrRefreshTransientNanoEffect(caster, val, profile.PrimaryNanoId, profile.PrimaryStrain, modifierDelta, profile, utcNow, affectedStatIds))
		{
			BaseMessageHandler<BuffMessage, BuffMessageHandler>.Default.SendAddNanoBuff(val, profile.PrimaryNanoId);
		}
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.NotifyActiveNanoDurationToPlayfield(caster, ((IEntity)val).Identity, profile.PrimaryNanoId, profile.DurationParameter);
		if (profile.HasTriggeredSelfEffect)
		{
			bool flag = ApplyOrRefreshTransientNanoEffect(caster, caster, profile.TriggeredSelfNanoId, profile.TriggeredSelfStrain, profile.TriggeredSelfModifierDelta, profile, utcNow);
			BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.SendTriggeredSelfCast(caster, profile.TriggeredSelfNanoId);
			if (flag)
			{
				BaseMessageHandler<BuffMessage, BuffMessageHandler>.Default.SendAddNanoBuff(caster, profile.TriggeredSelfNanoId);
			}
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.NotifyActiveNanoDurationToPlayfield(caster, ((IEntity)caster).Identity, profile.TriggeredSelfNanoId, profile.DurationParameter);
		}
	}

	private bool ApplyOrRefreshPeriodicNanoHit(ICharacter caster, ICharacter recipient, OrdinaryEnemySupportNanoProfile profile, DateTime utcNow)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Expected O, but got Unknown
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		ApplyPeriodicNanoStatHit(recipient, profile.PeriodicStatId, profile.PeriodicStatDelta);
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary = transientNanoEffectsByRecipient;
		Identity identity = ((IEntity)recipient).Identity;
		if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value2))
		{
			value2 = new Dictionary<int, OrdinaryEnemyTransientNanoEffectState>();
			Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary2 = transientNanoEffectsByRecipient;
			identity = ((IEntity)recipient).Identity;
			dictionary2.Add(((Identity)(ref identity)).Instance, value2);
		}
		if (value2.TryGetValue(profile.PrimaryNanoId, out var value3))
		{
			OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState = value3;
			identity = ((IEntity)caster).Identity;
			ordinaryEnemyTransientNanoEffectState.CasterInstance = ((Identity)(ref identity)).Instance;
			value3.PeriodicSchedule.Refresh(profile, utcNow);
			value3.ExpiresAtUtc = value3.PeriodicSchedule.ExpiresAtUtc;
			RefreshProjectedActiveNano(recipient, value3, profile.DurationParameter);
			return false;
		}
		OrdinaryEnemyTransientNanoEffectState[] array = value2.Values.Where((OrdinaryEnemyTransientNanoEffectState value) => value.Strain == profile.PrimaryStrain && value.NanoId != profile.PrimaryNanoId).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState state in array)
		{
			RemoveTransientNanoEffect(state, recipient);
		}
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary3 = transientNanoEffectsByRecipient;
		identity = ((IEntity)recipient).Identity;
		if (!dictionary3.ContainsKey(((Identity)(ref identity)).Instance))
		{
			Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary4 = transientNanoEffectsByRecipient;
			identity = ((IEntity)recipient).Identity;
			dictionary4.Add(((Identity)(ref identity)).Instance, value2);
		}
		int num = ResolveAvailableActiveNanoKey(recipient, profile.PrimaryStrain, profile.PrimaryNanoId);
		OrdinaryEnemyPeriodicNanoSchedule ordinaryEnemyPeriodicNanoSchedule = new OrdinaryEnemyPeriodicNanoSchedule(profile, utcNow);
		OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState2 = new OrdinaryEnemyTransientNanoEffectState();
		ordinaryEnemyTransientNanoEffectState2.RecipientIdentity = ((IEntity)recipient).Identity;
		ordinaryEnemyTransientNanoEffectState2.NanoId = profile.PrimaryNanoId;
		ordinaryEnemyTransientNanoEffectState2.Strain = profile.PrimaryStrain;
		ordinaryEnemyTransientNanoEffectState2.ModifierDelta = 0;
		ordinaryEnemyTransientNanoEffectState2.StatIds = new int[0];
		identity = ((IEntity)caster).Identity;
		ordinaryEnemyTransientNanoEffectState2.CasterInstance = ((Identity)(ref identity)).Instance;
		ordinaryEnemyTransientNanoEffectState2.ActiveNanoKey = num;
		ordinaryEnemyTransientNanoEffectState2.ExpiresAtUtc = ordinaryEnemyPeriodicNanoSchedule.ExpiresAtUtc;
		ordinaryEnemyTransientNanoEffectState2.PeriodicStatId = profile.PeriodicStatId;
		ordinaryEnemyTransientNanoEffectState2.PeriodicStatDelta = profile.PeriodicStatDelta;
		ordinaryEnemyTransientNanoEffectState2.PeriodicSchedule = ordinaryEnemyPeriodicNanoSchedule;
		OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState3 = ordinaryEnemyTransientNanoEffectState2;
		value2.Add(profile.PrimaryNanoId, ordinaryEnemyTransientNanoEffectState3);
		Dictionary<int, IActiveNano> activeNanos = recipient.ActiveNanos;
		ActiveNanoState val = new ActiveNanoState
		{
			ID = profile.PrimaryNanoId,
			Instance = profile.PrimaryNanoId,
			Nanotype = 0,
			TickCounter = profile.DurationParameter,
			TickInterval = profile.DurationParameter,
			NcuCost = profile.NcuCost,
			ExpiresAtUtc = ordinaryEnemyTransientNanoEffectState3.ExpiresAtUtc,
			PlayfieldBound = true,
			DurationPacketIdentity = ((IEntity)recipient).Identity
		};
		identity = ((IEntity)caster).Identity;
		val.DurationParameter1 = ((Identity)(ref identity)).Instance;
		activeNanos[num] = (IActiveNano)val;
		return true;
	}

	private bool ApplyOrRefreshTransientNanoEffect(ICharacter caster, ICharacter recipient, int nanoId, int strain, int modifierDelta, OrdinaryEnemySupportNanoProfile profile, DateTime utcNow, int[] affectedStatIds = null)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Expected O, but got Unknown
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary = transientNanoEffectsByRecipient;
		Identity identity = ((IEntity)recipient).Identity;
		if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value2))
		{
			value2 = new Dictionary<int, OrdinaryEnemyTransientNanoEffectState>();
			Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary2 = transientNanoEffectsByRecipient;
			identity = ((IEntity)recipient).Identity;
			dictionary2.Add(((Identity)(ref identity)).Instance, value2);
		}
		if (value2.TryGetValue(nanoId, out var value3))
		{
			OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState = value3;
			identity = ((IEntity)caster).Identity;
			ordinaryEnemyTransientNanoEffectState.CasterInstance = ((Identity)(ref identity)).Instance;
			value3.ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds);
			RefreshProjectedActiveNano(recipient, value3, profile.DurationParameter);
			return false;
		}
		OrdinaryEnemyTransientNanoEffectState[] array = value2.Values.Where((OrdinaryEnemyTransientNanoEffectState value) => value.Strain == strain && value.NanoId != nanoId).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState state in array)
		{
			RemoveTransientNanoEffect(state, recipient);
		}
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary3 = transientNanoEffectsByRecipient;
		identity = ((IEntity)recipient).Identity;
		if (!dictionary3.ContainsKey(((Identity)(ref identity)).Instance))
		{
			Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary4 = transientNanoEffectsByRecipient;
			identity = ((IEntity)recipient).Identity;
			dictionary4.Add(((Identity)(ref identity)).Instance, value2);
		}
		int num = ResolveAvailableActiveNanoKey(recipient, strain, nanoId);
		OrdinaryEnemyTransientNanoEffectState obj = new OrdinaryEnemyTransientNanoEffectState
		{
			RecipientIdentity = ((IEntity)recipient).Identity,
			NanoId = nanoId,
			Strain = strain,
			ModifierDelta = modifierDelta,
			StatIds = (int[])(affectedStatIds ?? profile.AffectedStatIds).Clone()
		};
		identity = ((IEntity)caster).Identity;
		obj.CasterInstance = ((Identity)(ref identity)).Instance;
		obj.ActiveNanoKey = num;
		obj.ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds);
		OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState2 = obj;
		int[] statIds = ordinaryEnemyTransientNanoEffectState2.StatIds;
		foreach (int num2 in statIds)
		{
			IStat obj2 = ((IStats)recipient).Stats[num2];
			obj2.Modifier += modifierDelta;
		}
		value2.Add(nanoId, ordinaryEnemyTransientNanoEffectState2);
		Dictionary<int, IActiveNano> activeNanos = recipient.ActiveNanos;
		ActiveNanoState val = new ActiveNanoState
		{
			ID = nanoId,
			Instance = nanoId,
			Nanotype = 0,
			TickCounter = profile.DurationParameter,
			TickInterval = profile.DurationParameter,
			NcuCost = profile.NcuCost,
			ExpiresAtUtc = ordinaryEnemyTransientNanoEffectState2.ExpiresAtUtc,
			PlayfieldBound = true,
			DurationPacketIdentity = ((IEntity)recipient).Identity
		};
		identity = ((IEntity)caster).Identity;
		val.DurationParameter1 = ((Identity)(ref identity)).Instance;
		activeNanos[num] = (IActiveNano)val;
		return true;
	}

	private void ProcessPeriodicNanoTicks(OrdinaryEnemyTransientNanoEffectState state, ICharacter recipient, DateTime utcNow)
	{
		if (state != null && state.PeriodicSchedule != null && recipient != null && ((IStats)recipient).Stats[(StatIds)27].Value > 0)
		{
			int num = state.PeriodicSchedule.ConsumeDueTicks(utcNow);
			for (int i = 0; i < num; i++)
			{
				ApplyPeriodicNanoStatHit(recipient, state.PeriodicStatId, state.PeriodicStatDelta);
			}
		}
	}

	private void ApplyPeriodicNanoStatHit(ICharacter recipient, int statId, int delta)
	{
		if (recipient != null && statId == 214 && delta > 0)
		{
			int maximum = Math.Max(0, ((IStats)recipient).Stats[(StatIds)221].Value);
			int num = Math.Max(0, ((IStats)recipient).Stats[(StatIds)214].Value);
			int num2 = OrdinaryEnemySupportNanoRuntimeRules.ApplyPositiveCappedDelta(num, maximum, delta);
			if (num2 > num)
			{
				((IStats)recipient).Stats[(StatIds)214].Value = num2;
				BaseMessageHandler<StatMessage, StatMessageHandler>.Default.AnnounceSingle(recipient, 214, (uint)num2);
			}
		}
	}

	private static int ResolveAvailableActiveNanoKey(ICharacter recipient, int strain, int nanoId)
	{
		if (!recipient.ActiveNanos.TryGetValue(strain, out var value) || value == null || value.ID == nanoId)
		{
			return strain;
		}
		int num = -nanoId;
		while (recipient.ActiveNanos.ContainsKey(num))
		{
			num--;
		}
		return num;
	}

	private static void RefreshProjectedActiveNano(ICharacter recipient, OrdinaryEnemyTransientNanoEffectState state, int durationParameter)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (recipient.ActiveNanos.TryGetValue(state.ActiveNanoKey, out var value) && value != null && value.ID == state.NanoId)
		{
			value.TickCounter = durationParameter;
			value.TickInterval = durationParameter;
			ActiveNanoState val = (ActiveNanoState)(object)((value is ActiveNanoState) ? value : null);
			if (val != null)
			{
				val.ExpiresAtUtc = state.ExpiresAtUtc;
				val.DurationPacketIdentity = ((IEntity)recipient).Identity;
				val.DurationParameter1 = state.CasterInstance;
			}
		}
	}

	private void RemoveAllTransientNanoEffects()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		OrdinaryEnemyTransientNanoEffectState[] array = transientNanoEffectsByRecipient.SelectMany((KeyValuePair<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> value) => value.Value.Values).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState in array)
		{
			ICharacter recipient = dynelRegistry.FindByIdentity<ICharacter>(ordinaryEnemyTransientNanoEffectState.RecipientIdentity);
			RemoveTransientNanoEffect(ordinaryEnemyTransientNanoEffectState, recipient);
		}
	}

	private void RemoveTransientNanoEffectsForRecipient(ICharacter recipient)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (recipient == null)
		{
			return;
		}
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary = transientNanoEffectsByRecipient;
		Identity identity = ((IEntity)recipient).Identity;
		if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			OrdinaryEnemyTransientNanoEffectState[] array = value.Values.ToArray();
			foreach (OrdinaryEnemyTransientNanoEffectState state in array)
			{
				RemoveTransientNanoEffect(state, recipient);
			}
		}
	}

	private void RemoveTransientNanoEffectsForCaster(int casterInstance)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		OrdinaryEnemyTransientNanoEffectState[] array = (from value in transientNanoEffectsByRecipient.SelectMany((KeyValuePair<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> value) => value.Value.Values)
			where value.CasterInstance == casterInstance
			select value).ToArray();
		foreach (OrdinaryEnemyTransientNanoEffectState ordinaryEnemyTransientNanoEffectState in array)
		{
			ICharacter recipient = dynelRegistry.FindByIdentity<ICharacter>(ordinaryEnemyTransientNanoEffectState.RecipientIdentity);
			RemoveTransientNanoEffect(ordinaryEnemyTransientNanoEffectState, recipient);
		}
	}

	private void RemoveTransientNanoEffect(OrdinaryEnemyTransientNanoEffectState state, ICharacter recipient)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		if (state == null)
		{
			return;
		}
		if (recipient != null)
		{
			int[] statIds = state.StatIds;
			foreach (int num in statIds)
			{
				IStat obj = ((IStats)recipient).Stats[num];
				obj.Modifier -= state.ModifierDelta;
			}
			if (recipient.ActiveNanos.TryGetValue(state.ActiveNanoKey, out var value) && value != null && value.ID == state.NanoId)
			{
				recipient.ActiveNanos.Remove(state.ActiveNanoKey);
			}
		}
		Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary = transientNanoEffectsByRecipient;
		Identity recipientIdentity = state.RecipientIdentity;
		if (dictionary.TryGetValue(((Identity)(ref recipientIdentity)).Instance, out var value2))
		{
			value2.Remove(state.NanoId);
			if (value2.Count == 0)
			{
				Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>> dictionary2 = transientNanoEffectsByRecipient;
				recipientIdentity = state.RecipientIdentity;
				dictionary2.Remove(((Identity)(ref recipientIdentity)).Instance);
			}
		}
	}

	private Character ConstructCharacter(Playfield playfield, Identity playfieldIdentity, OrdinaryEnemySpawnDefinition spawn, OrdinaryEnemySpawnVariant variant, OrdinaryEnemyProfile profile, NPCController controller)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Expected O, but got Unknown
		//IL_007b: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Expected O, but got Unknown
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Expected O, but got Unknown
		Character val;
		if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
		{
			val = NonPlayerCharacterHandler.SpawnMobFromTemplate(profile.TemplateHash, playfieldIdentity, new Coordinate
			{
				x = spawn.X,
				y = spawn.Y,
				z = spawn.Z
			}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)controller, variant.Level);
		}
		else
		{
			int freeInstance = Pool.Instance.GetFreeInstance<Character>(1000000, (IdentityType)50000);
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)50000;
			((Identity)(ref val2)).Instance = freeInstance;
			Identity val3 = val2;
			val = new Character(playfieldIdentity, val3, (IController)(object)controller);
			((Dynel)val).Read();
			controller.Character = (ICharacter)(object)val;
		}
		if (val == null)
		{
			return null;
		}
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Name = profile.DisplayName;
		val.FirstName = string.Empty;
		val.LastName = string.Empty;
		((Dynel)val).Coordinates(new Coordinate
		{
			x = spawn.X,
			y = spawn.Y,
			z = spawn.Z
		});
		((Dynel)val).RawHeading = new Quaternion((double)spawn.HeadingX, (double)spawn.HeadingY, (double)spawn.HeadingZ, (double)spawn.HeadingW);
		return val;
	}

	private void ApplyMovement(Character character, NPCController controller, OrdinaryEnemySpawnDefinition spawn)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		character.Waypoints.Clear();
		OrdinaryEnemyWaypoint[] waypoints = spawn.Waypoints;
		foreach (OrdinaryEnemyWaypoint ordinaryEnemyWaypoint in waypoints)
		{
			character.AddWaypoint(new Vector3((double)ordinaryEnemyWaypoint.X, (double)ordinaryEnemyWaypoint.Y, (double)ordinaryEnemyWaypoint.Z), false);
		}
		if (character.Waypoints.Count > 1)
		{
			controller.State = (CharacterState)4;
		}
		if (!spawn.UseCapturedPatrolReplay)
		{
			return;
		}
		patrolReplay.AssignCapturedSubwayReplay(spawn.SourceIdentity, delegate(NpcPatrolReplaySegment[] segments)
		{
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Expected O, but got Unknown
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Expected O, but got Unknown
			if (segments != null && segments.Length != 0)
			{
				Vector3 val = (spawn.UseSpawnAsPatrolStart ? new Vector3((double)spawn.X, (double)spawn.Y, (double)spawn.Z) : new Vector3((double)segments[0].StartX, (double)segments[0].StartY, (double)segments[0].StartZ));
				Vector3 val2 = new Vector3((double)segments[0].EndX, (double)segments[0].EndY, (double)segments[0].EndZ);
				((Dynel)character).Coordinates(val);
				character.Waypoints.Clear();
				character.AddWaypoint(val, false);
				character.AddWaypoint(val2, false);
				controller.SetCapturedPatrolReplaySegments(segments, useRuntimeStart: false, batchZeroDelaySegments: true, spawn.UseSpawnAsPatrolStart);
				controller.State = (CharacterState)4;
			}
		});
	}

	private static void ApplyStats(Character character, OrdinaryEnemySpawnVariant variant, OrdinaryEnemyProfile profile)
	{
		OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
		SetMobStat((ICharacter)(object)character, (StatIds)33, appearance.Side, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)47, appearance.Fatness, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)4, appearance.Breed, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)59, appearance.Sex, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)89, appearance.Race, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)0, appearance.CharacterFlags, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)660, appearance.AccountFlags, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)389, appearance.Expansions, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)455, appearance.NpcFamily, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)466, appearance.NpcLosHeight, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)359, profile.MonsterData, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)360, variant.MonsterScale, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)673, appearance.VisualFlags, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)173, 3, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)174, 3, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)156, variant.RunSpeed, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)60, 1, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)37, 1, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)54, variant.Level, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)1, variant.Health, profile.ConstructionMode);
		SetMobStat((ICharacter)(object)character, (StatIds)27, Math.Max(0, variant.Health - variant.HealthDamage), profile.ConstructionMode);
		int num = ((profile.SupportNano != null) ? profile.SupportNano.ResolveSpawnNanoPool(variant.Level) : 0);
		if (num > 0)
		{
			SetMobStat((ICharacter)(object)character, (StatIds)221, num, profile.ConstructionMode);
			SetMobStat((ICharacter)(object)character, (StatIds)214, num, profile.ConstructionMode);
		}
		if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.CapturedDirect)
		{
			SetMobStat((ICharacter)(object)character, (StatIds)64, appearance.HeadMesh, profile.ConstructionMode);
		}
	}

	private static void ApplyAppearance(Character character, OrdinaryEnemyProfile profile)
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
		OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
		if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
		{
			if (appearance.HeadMesh > 0)
			{
				SetHeadMesh(character, appearance.HeadMesh);
			}
			else if (appearance.ClearTemplateHeadWhenZero)
			{
				((Dynel)character).MeshLayer.RemoveMesh(0, 0, 0, 4);
				character.SocialMeshLayer.RemoveMesh(0, 0, 0, 4);
			}
		}
		if (appearance.ReplaceTextures)
		{
			((Dynel)character).Textures.Clear();
		}
		OrdinaryEnemyTextureProfile[] textures = appearance.Textures;
		foreach (OrdinaryEnemyTextureProfile ordinaryEnemyTextureProfile in textures)
		{
			((Dynel)character).Textures.Add(new AOTextures(ordinaryEnemyTextureProfile.Place, ordinaryEnemyTextureProfile.Id));
		}
		OrdinaryEnemyMeshProfile[] meshes = appearance.Meshes;
		foreach (OrdinaryEnemyMeshProfile ordinaryEnemyMeshProfile in meshes)
		{
			((Dynel)character).MeshLayer.AddMesh(ordinaryEnemyMeshProfile.Position, (int)ordinaryEnemyMeshProfile.Id, ordinaryEnemyMeshProfile.OverrideTextureId, ordinaryEnemyMeshProfile.Layer);
			character.SocialMeshLayer.AddMesh(ordinaryEnemyMeshProfile.Position, (int)ordinaryEnemyMeshProfile.Id, ordinaryEnemyMeshProfile.OverrideTextureId, ordinaryEnemyMeshProfile.Layer);
		}
	}

	private static void SetHeadMesh(Character character, int headMesh)
	{
		int value = ((Dynel)character).Stats[(StatIds)64].Value;
		if (value != 0 && value != headMesh)
		{
			((Dynel)character).MeshLayer.RemoveMesh(0, value, 0, 4);
			character.SocialMeshLayer.RemoveMesh(0, value, 0, 4);
		}
		((Dynel)character).Stats[(StatIds)64].Value = headMesh;
		((Dynel)character).Stats[(StatIds)64].BaseValue = (uint)headMesh;
		((Dynel)character).MeshLayer.AddMesh(0, headMesh, 0, 4);
		character.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
	}

	private static void SetMobStat(ICharacter character, StatIds stat, int value, OrdinaryEnemyConstructionMode constructionMode)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected I4, but got Unknown
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (constructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
		{
			((IStats)character).Stats[stat].Value = value;
			((IStats)character).Stats[stat].BaseValue = (uint)value;
		}
		else
		{
			((IStats)character).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
		}
	}
}
