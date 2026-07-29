using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Navigation;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class NPCRuntimeService
{
	private class NpcHomeState
	{
		public Coordinate Coordinates { get; set; }

		public double MaximumNpcDistanceFromHome { get; set; }

		public bool ReturningHome { get; set; }

		public CharacterState ControllerStateBeforeReturn { get; set; }
	}

	private readonly Playfield playfield;

	private readonly PlayfieldDynelRegistry dynelRegistry;

	private readonly PlayfieldRewardRuntimeService rewards;

	private readonly NpcCorpseLifecycleCoordinator corpseLifecycle;

	private readonly NpcCombatTickCoordinator combatTick;

	private readonly NpcChaseNavigationRuntimeService chaseNavigation;

	private readonly CapturedAreteRobotContentProvider capturedAreteRobotContent;

	private readonly CapturedSubwayContentProvider capturedSubwayContent;

	private readonly CapturedSubwayOrdinaryContentProvider capturedSubwayOrdinaryContent;

	private readonly OrdinaryEnemyCatalog ordinaryEnemyCatalog;

	private readonly NpcPatrolReplayCoordinator patrolReplay;

	private readonly CapturedAreteRobotSpawnOrchestrator capturedAreteRobotSpawns;

	private readonly OrdinaryEnemyRuntimeService ordinaryEnemies;

	private readonly WorldPopulationController worldPopulation;

	private readonly CapturedSubwayEncounterRuntimeService capturedSubwayEncounters;

	private readonly NascenceCoreHecklerSpawnOrchestrator nascenceCoreHecklers;

	private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();

	private readonly Dictionary<int, DateTime> corpseDespawnTicks = new Dictionary<int, DateTime>();

	internal NPCRuntimeService(Playfield playfield, PlayfieldDynelRegistry dynelRegistry, PlayfieldRewardRuntimeService rewards, NpcChaseNavigationRuntimeService chaseNavigation)
	{
		this.playfield = playfield;
		this.dynelRegistry = dynelRegistry;
		this.rewards = rewards ?? new PlayfieldRewardRuntimeService();
		this.chaseNavigation = chaseNavigation ?? throw new ArgumentNullException("chaseNavigation");
		corpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield, RemoveNpcHome);
		combatTick = new NpcCombatTickCoordinator(playfield);
		capturedAreteRobotContent = new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent);
		capturedSubwayContent = new CapturedSubwayContentProvider();
		capturedSubwayOrdinaryContent = new CapturedSubwayOrdinaryContentProvider();
		ordinaryEnemyCatalog = new OrdinaryEnemyCatalog(capturedSubwayContent, capturedSubwayOrdinaryContent);
		patrolReplay = new NpcPatrolReplayCoordinator(capturedAreteRobotContent, capturedSubwayContent);
		capturedAreteRobotSpawns = new CapturedAreteRobotSpawnOrchestrator(capturedAreteRobotContent, patrolReplay, ActivateNpc);
		ordinaryEnemies = new OrdinaryEnemyRuntimeService(ordinaryEnemyCatalog, patrolReplay, this.dynelRegistry, ActivateNpc);
		worldPopulation = new WorldPopulationController(this.playfield, ordinaryEnemyCatalog, ordinaryEnemies);
		capturedSubwayEncounters = new CapturedSubwayEncounterRuntimeService(this.playfield, this.dynelRegistry, ActivateNpc);
		nascenceCoreHecklers = new NascenceCoreHecklerSpawnOrchestrator(ActivateNpc);
	}

	internal void ActivateNpc(ICharacter character)
	{
		dynelRegistry.Register((IEntity)(object)character);
		RegisterNpcHome(character);
	}

	internal void EnsureAreteCapturePopulation()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		AreteLandingPopulationEnsure.Tick(playfield, ((PooledObject)playfield).Identity, ActivateNpc);
	}

	internal void ClearRuntimeState()
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		chaseNavigation.ClearAll(NpcChaseInvalidationReason.PlayfieldReset);
		foreach (ICharacter item in dynelRegistry.Characters())
		{
			if (((IDynel)item).Controller is NPCController)
			{
				combatTick.ClearTracking(((IEntity)item).Identity);
			}
		}
		WorldPopulationController worldPopulationController = worldPopulation;
		Identity identity = ((PooledObject)playfield).Identity;
		worldPopulationController.ClearPlayfield(((Identity)(ref identity)).Instance);
		OrdinaryEnemyRuntimeService ordinaryEnemyRuntimeService = ordinaryEnemies;
		identity = ((PooledObject)playfield).Identity;
		ordinaryEnemyRuntimeService.ClearRuntimeState(((Identity)(ref identity)).Instance);
		chaseNavigation.ClearAll(NpcChaseInvalidationReason.EncounterReset);
		capturedSubwayEncounters.ClearRuntimeState();
		npcHomeStates.Clear();
		corpseDespawnTicks.Clear();
		AndromedaIccHqIdleGestureRuntime.Clear();
		identity = ((PooledObject)playfield).Identity;
		AndromedaIccHqSpawn.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		AreteLandingSpawn.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		MarcusPadAmbientCombat.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		JunkyardCleaningRobotRuntime.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		AlexAreaMobRuntime.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		SurveillanceDroidRuntime.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		AreteLandingPopulationEnsure.ClearPlayfield(((Identity)(ref identity)).Instance);
		identity = ((PooledObject)playfield).Identity;
		HoloDeckSpawn.ClearPlayfield(((Identity)(ref identity)).Instance);
	}

	internal void RegisterNpcHome(ICharacter character)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		if (character != null && ((IDynel)character).Controller is NPCController)
		{
			double maximumNpcDistanceFromHome = 100.0;
			Identity identity = ((IEntity)character).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition) && definition.MaximumNpcLeashDistanceFromHome.HasValue)
			{
				maximumNpcDistanceFromHome = definition.MaximumNpcLeashDistanceFromHome.Value;
			}
			Dictionary<int, NpcHomeState> dictionary = npcHomeStates;
			identity = ((IEntity)character).Identity;
			dictionary[((Identity)(ref identity)).Instance] = new NpcHomeState
			{
				Coordinates = new Coordinate(((IDynel)character).Coordinates()),
				MaximumNpcDistanceFromHome = maximumNpcDistanceFromHome
			};
		}
	}

	internal void RemoveNpcHome(Identity identity)
	{
		npcHomeStates.Remove(((Identity)(ref identity)).Instance);
	}

	internal void DespawnNpcImmediately(ICharacter target, Action<Identity> stopFightingDeadTarget, Action<Identity> cancelPendingCorpseSpawn)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if (target != null)
		{
			Identity identity = ((IEntity)target).Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				stopFightingDeadTarget(((IEntity)target).Identity);
				cancelPendingCorpseSpawn(((IEntity)target).Identity);
				FinalizeNpcDespawn(target);
			}
		}
	}

	internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		capturedAreteRobotSpawns.SpawnForPlayfield(playfield, playfieldIdentity);
		PerkResetServiceProviderSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		nascenceCoreHecklers.SpawnForPlayfield(playfield, playfieldIdentity);
		NascenceLifeSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		ThrakOmniGardenSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		RomeBlueCitySpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		AndromedaIccHqSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		try
		{
			AreteLandingSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "AreteLandingSpawn batch failed: " + ex.GetType().Name + ": " + ex.Message);
		}
		try
		{
			MarcusPadAmbientCombat.StartForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		}
		catch (Exception ex2)
		{
			LogUtil.Debug((DebugInfoDetail)512, "MarcusPadAmbientCombat start failed: " + ex2.GetType().Name + ": " + ex2.Message);
		}
		try
		{
			SurveillanceDroidRuntime.StartForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		}
		catch (Exception ex3)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SurveillanceDroidRuntime start failed: " + ex3.GetType().Name + ": " + ex3.Message);
		}
		HoloDeckSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		MissionInstanceSpawn.SpawnForPlayfield(playfield, playfieldIdentity, ActivateNpc);
		worldPopulation.ActivatePlayfield(playfieldIdentity);
		capturedSubwayEncounters.ActivatePlayfield(playfieldIdentity);
	}

	internal bool HasPendingDeadNpcDespawn(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return corpseLifecycle.HasPendingDeadNpcDespawn(identity);
	}

	internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)
	{
		corpseDespawnTicks[((Identity)(ref corpseIdentity)).Instance] = expiresAtUtc;
	}

	internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)
	{
		int[] array = (from x in corpseDespawnTicks
			where x.Value <= utcNow
			select x.Key).ToArray();
		foreach (int obj in array)
		{
			despawnCorpse(obj);
		}
	}

	internal void ProcessDueCapturedSubwayRespawns(Identity playfieldIdentity, DateTime utcNow)
	{
		ordinaryEnemies.ProcessExpiredSupportNanoEffects(utcNow);
		worldPopulation.ProcessDue(utcNow);
		capturedSubwayEncounters.ProcessDue(utcNow, AcquireAggro);
		nascenceCoreHecklers.ProcessDue(utcNow);
		AndromedaIccHqIdleGestureRuntime.ProcessDue(utcNow);
	}

	internal void ClearNpcCorpseDespawn(int corpseInstance)
	{
		corpseDespawnTicks.Remove(corpseInstance);
	}

	internal void NotifyCorpseRemoved(Identity corpseIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		worldPopulation.NotifyCorpseRemoved(corpseIdentity, DateTime.UtcNow);
	}

	internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		if (((IDynel)target).Controller is NPCController && !corpseLifecycle.HasPendingDeadNpcDespawn(((IEntity)target).Identity))
		{
			DateTime utcNow = DateTime.UtcNow;
			ordinaryEnemies.NotifyCharacterDied(target);
			Identity corpseIdentity = Identity.None;
			if (playfield.CanBuildKnownCorpseVisual(target))
			{
				corpseIdentity = playfield.AllocateCorpseIdentity();
			}
			NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
			Identity identity = ((IEntity)target).Identity;
			npcChaseNavigationRuntimeService.Clear(((Identity)(ref identity)).Instance, NpcChaseInvalidationReason.CorpseTransition);
			playfield.MarkNpcDead(target);
			playfield.StopFightingDeadTarget(((IEntity)target).Identity);
			playfield.StopDyingNpcCombatState(target);
			playfield.SendNpcDeathAnimation(target);
			rewards.RunNpcDeathRewardHooks(attacker, target, playfield.AwardCombatXp);
			ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);
			worldPopulation.NotifyDeath(target, corpseIdentity, utcNow);
			nascenceCoreHecklers.NotifyDeath(target, utcNow);
			ICharacter[] array = capturedSubwayEncounters.NotifyDeath(target, utcNow);
			foreach (ICharacter target2 in array)
			{
				playfield.DespawnNpcImmediately(target2);
			}
			ScheduleDeadNpcDespawn(target);
			LogUtil.Debug((DebugInfoDetail)4, $"NPC died target={((IEntity)target).Identity}");
		}
	}

	internal bool ProcessDeadNpcDespawn(ICharacter character)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (!(((IDynel)character).Controller is NPCController) || ((IStats)character).Stats[(StatIds)27].Value > 0)
		{
			return false;
		}
		if (!corpseLifecycle.TryGetDeadNpcDespawn(((IEntity)character).Identity, out var despawnTick))
		{
			BeginNpcDeath(null, character);
			return true;
		}
		if (despawnTick > DateTime.UtcNow)
		{
			return true;
		}
		FinalizeNpcDespawn(character);
		return true;
	}

	internal void FinalizeNpcDespawn(ICharacter target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
		Identity identity = ((IEntity)target).Identity;
		npcChaseNavigationRuntimeService.Clear(((Identity)(ref identity)).Instance, NpcChaseInvalidationReason.Despawn);
		DateTime utcNow = DateTime.UtcNow;
		worldPopulation.NotifyNpcDespawn(target, utcNow);
		capturedSubwayEncounters.NotifyNpcDespawn(target, utcNow);
		nascenceCoreHecklers.NotifyNpcDespawn(target);
		corpseLifecycle.FinalizeNpcDespawn(target);
		dynelRegistry.Unregister(((IEntity)target).Identity);
		identity = ((IEntity)target).Identity;
		OrdinaryEnemyRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
		identity = ((IEntity)target).Identity;
		SubwayVisibilityDiagnosticSelection.RemoveRuntimeIdentity(((Identity)(ref identity)).Instance);
		identity = ((IEntity)target).Identity;
		CapturedEnemyCombatRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
		identity = ((IEntity)target).Identity;
		CapturedEncounterRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
	}

	internal void ResetCombatTick(ICharacter attacker)
	{
		combatTick.ResetCombatTick(attacker);
	}

	internal void ProcessCombatTick(ICharacter attacker)
	{
		if (!TryBeginLeashReturn(attacker) && !ordinaryEnemies.TryProcessSupportNano(attacker, DateTime.UtcNow) && !capturedSubwayEncounters.IsCapturedNanoCastInProgress(attacker))
		{
			combatTick.ProcessCombatTick(attacker);
		}
	}

	internal void ClearInvalidCombatTarget(ICharacter attacker)
	{
		ClearFightingTarget(attacker);
	}

	internal void ClearFightingTarget(ICharacter character)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		((ITargetingEntity)character).SetFightingTarget(Identity.None);
		ClearCombatTracking(((IEntity)character).Identity, NpcChaseInvalidationReason.TargetLost);
	}

	internal void StopDyingNpcCombatState(ICharacter target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		((ITargetingEntity)target).SetTarget(Identity.None);
		((ITargetingEntity)target).SetFightingTarget(Identity.None);
		ClearCombatTracking(((IEntity)target).Identity, NpcChaseInvalidationReason.Death);
		if (((IDynel)target).Controller is NPCController nPCController)
		{
			nPCController.SnapshotCurrentMotionPosition();
			nPCController.StopFollow();
		}
	}

	internal void AcquireAggro(ICharacter attacker, ICharacter target)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		if (!(((IDynel)target).Controller is NPCController nPCController))
		{
			return;
		}
		Dictionary<int, NpcHomeState> dictionary = npcHomeStates;
		Identity val = ((IEntity)target).Identity;
		if (dictionary.TryGetValue(((Identity)(ref val)).Instance, out var value))
		{
			if (value.ReturningHome)
			{
				return;
			}
			bool isPlayerOwnedPet = IsPlayerControlledPet(target);
			ChaseNavigationPoint home = ToNavigationPoint(value.Coordinates.coordinate);
			ChaseNavigationPoint chaseNavigationPoint = ToNavigationPoint(((IDynel)target).Coordinates().coordinate);
			ChaseNavigationPoint target2 = ToNavigationPoint(((IDynel)attacker).Coordinates().coordinate);
			val = ((PooledObject)playfield).Identity;
			if (NpcCombatLeashPolicy.ShouldResetCombat(((Identity)(ref val)).Instance, isPlayerOwnedPet, home, chaseNavigationPoint, target2, value.MaximumNpcDistanceFromHome))
			{
				if (home.Distance2D(chaseNavigationPoint) > value.MaximumNpcDistanceFromHome)
				{
					BeginLeashReturn(target, value);
				}
				return;
			}
		}
		val = ((IEntity)target).Identity;
		if (CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var contract) && !contract.IsCombatReady)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Captured enemy combat refused npc={((IEntity)target).Identity} attacker={((IEntity)attacker).Identity} reason=contract-incomplete evidence={contract.Evidence}");
			return;
		}
		val = ((IEntity)target).Identity;
		if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref val)).Instance, out var definition) && definition.Profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Passive)
		{
			return;
		}
		if (nPCController.KnuBot == null && NpcAiProfiles.CanRetaliate(nPCController.AiProfile) && ((IStats)target).Stats[(StatIds)27].Value > 0)
		{
			val = ((ITargetingEntity)target).FightingTarget;
			if (((Identity)(ref val)).Instance == 0)
			{
				StartCombatWithAcquiredTarget(attacker, target, contract);
				return;
			}
		}
		LogUtil.Debug((DebugInfoDetail)4, $"NPC combat refused npc={((IEntity)target).Identity} attacker={((IEntity)attacker).Identity} knubot={nPCController.KnuBot != null} profile={nPCController.AiProfile} health={((IStats)target).Stats[(StatIds)27].Value} fightingTarget={((ITargetingEntity)target).FightingTarget}");
	}

	internal void ForceTauntAggro(ICharacter taunter, ICharacter target)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (taunter != null && target != null && ((IDynel)target).Controller is NPCController { KnuBot: null } && ((IStats)target).Stats[(StatIds)27].Value > 0)
		{
			Identity identity = ((IEntity)target).Identity;
			CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var contract);
			StartCombatWithAcquiredTarget(taunter, target, contract);
			LogUtil.Debug((DebugInfoDetail)256, $"ForceTauntAggro taunter={((IEntity)taunter).Identity} npc={((IEntity)target).Identity} previousTargetStolen=true");
		}
	}

	internal void ProcessPatrolTick(ICharacter character)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Invalid comparison between Unknown and I4
		if (character == null || !(((IDynel)character).Controller is NPCController))
		{
			return;
		}
		Identity val;
		if (playfield != null)
		{
			val = ((PooledObject)playfield).Identity;
			if (((Identity)(ref val)).Instance == 6553 && string.Equals(((INamedEntity)character).Name, "Marcus Stone", StringComparison.OrdinalIgnoreCase))
			{
				MarcusPadAmbientCombat.TickRespawn(playfield, ((PooledObject)playfield).Identity, ActivateNpc);
				MarcusWoundedWorkersQuestRuntime.TickHealRecoveries(playfield);
			}
		}
		if (playfield != null)
		{
			val = ((PooledObject)playfield).Identity;
			if (((Identity)(ref val)).Instance == 6553)
			{
				SurveillanceDroidRuntime.TickEnsurePresent(playfield, ((PooledObject)playfield).Identity, ActivateNpc);
			}
		}
		PetCommandService.ProcessPetHealTick(character);
		DateTime utcNow = DateTime.UtcNow;
		if (TryBeginLeashReturn(character))
		{
			TryProcessLeashReturn(character, utcNow);
		}
		else
		{
			if (TryProcessLeashReturn(character, utcNow) || ordinaryEnemies.TryProcessSupportNano(character, utcNow))
			{
				return;
			}
			ICharacter val2 = capturedSubwayEncounters.FindAutomaticAggroTarget(character) ?? ordinaryEnemies.FindAutomaticAggroTarget(character) ?? AlexAreaMobRuntime.FindAutomaticAggroTarget(character) ?? MissionInstanceMobCombat.FindAutomaticAggroTarget(character);
			if (val2 != null)
			{
				AcquireAggro(val2, character);
			}
			val = ((ITargetingEntity)character).FightingTarget;
			if (((Identity)(ref val)).Instance != 0)
			{
				if (((IDynel)character).Controller.IsFollowing())
				{
					((IDynel)character).Controller.DoFollow();
				}
				return;
			}
			NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
			val = ((IEntity)character).Identity;
			npcChaseNavigationRuntimeService.Clear(((Identity)(ref val)).Instance, NpcChaseInvalidationReason.LeashReset);
			ordinaryEnemies.TryReturnToSpawn(character);
			if (((IDynel)character).Controller.IsFollowing())
			{
				((IDynel)character).Controller.DoFollow();
			}
			else if ((int)((IDynel)character).Controller.State == 4)
			{
				((IDynel)character).Controller.StartPatrolling();
			}
		}
	}

	internal void ClearCombatTracking(Identity identity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		ClearCombatTracking(identity, NpcChaseInvalidationReason.CombatCancelled);
	}

	private void ClearCombatTracking(Identity identity, NpcChaseInvalidationReason navigationReason)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		combatTick.ClearTracking(identity);
		chaseNavigation.Clear(((Identity)(ref identity)).Instance, navigationReason);
	}

	private void StartCombatWithAcquiredTarget(ICharacter attacker, ICharacter target, CapturedEnemyCombatContract capturedContract)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
		Identity identity = ((IEntity)target).Identity;
		npcChaseNavigationRuntimeService.Clear(((Identity)(ref identity)).Instance, NpcChaseInvalidationReason.TargetReplaced);
		((ITargetingEntity)target).SetTarget(((IEntity)attacker).Identity);
		((ITargetingEntity)target).SetFightingTarget(((IEntity)attacker).Identity);
		NPCController nPCController = ((IDynel)target).Controller as NPCController;
		if (nPCController != null && capturedContract != null && capturedContract.HasCapturedCombatStopSequence)
		{
			ResetCombatTick(target);
		}
		else
		{
			nPCController?.StopFollowForCombatRange(((IDynel)attacker).Coordinates().coordinate);
			ResetCombatTick(target);
		}
		capturedSubwayEncounters.NotifyCombatStarted(target, attacker, DateTime.UtcNow);
		identity = ((IEntity)attacker).Identity;
		string arg = ((Identity)(ref identity)).ToString(true);
		identity = ((IEntity)target).Identity;
		LogUtil.Debug((DebugInfoDetail)4, $"NPC combat engaged attacker={arg} npc={((Identity)(ref identity)).ToString(true)}");
	}

	private bool TryBeginLeashReturn(ICharacter npc)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Identity val = ((ITargetingEntity)npc).FightingTarget;
			if (((Identity)(ref val)).Instance != 0 && !IsPlayerControlledPet(npc))
			{
				Dictionary<int, NpcHomeState> dictionary = npcHomeStates;
				val = ((IEntity)npc).Identity;
				if (!dictionary.TryGetValue(((Identity)(ref val)).Instance, out var value))
				{
					return false;
				}
				ICharacter val2 = dynelRegistry.FindByIdentity<ICharacter>(((ITargetingEntity)npc).FightingTarget);
				if (val2 != null)
				{
					val = ((PooledObject)playfield).Identity;
					if (NpcCombatLeashPolicy.ShouldResetCombat(((Identity)(ref val)).Instance, isPlayerOwnedPet: false, ToNavigationPoint(value.Coordinates.coordinate), ToNavigationPoint(((IDynel)npc).Coordinates().coordinate), ToNavigationPoint(((IDynel)val2).Coordinates().coordinate), value.MaximumNpcDistanceFromHome))
					{
						BeginLeashReturn(npc, value);
						return true;
					}
				}
				return false;
			}
		}
		return false;
	}

	private void BeginLeashReturn(ICharacter npc, NpcHomeState home)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		Identity fightingTarget = ((ITargetingEntity)npc).FightingTarget;
		bool flag = ((Identity)(ref fightingTarget)).Instance != 0;
		home.ReturningHome = true;
		((ITargetingEntity)npc).SetTarget(Identity.None);
		((ITargetingEntity)npc).SetFightingTarget(Identity.None);
		ClearCombatTracking(((IEntity)npc).Identity, NpcChaseInvalidationReason.LeashReset);
		if (((IDynel)npc).Controller is NPCController nPCController)
		{
			home.ControllerStateBeforeReturn = nPCController.State;
			nPCController.State = (CharacterState)0;
			nPCController.SnapshotCurrentMotionPosition();
			nPCController.StopFollow();
		}
		if (flag)
		{
			playfield.Announce((MessageBody)new StopFightMessage
			{
				Identity = ((IEntity)npc).Identity,
				Unknown1 = 1
			});
		}
		ICharacter[] array = capturedSubwayEncounters.NotifyCombatReset(npc);
		foreach (ICharacter target in array)
		{
			playfield.DespawnNpcImmediately(target);
		}
		LogUtil.Debug((DebugInfoDetail)4, $"NPC leash reset npc={((IEntity)npc).Identity} home={home.Coordinates.coordinate} position={((IDynel)npc).Coordinates().coordinate} maxDistance={home.MaximumNpcDistanceFromHome}");
	}

	private bool TryProcessLeashReturn(ICharacter npc, DateTime utcNow)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Expected O, but got Unknown
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		if (npc != null)
		{
			Dictionary<int, NpcHomeState> dictionary = npcHomeStates;
			Identity identity = ((IEntity)npc).Identity;
			if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) && value.ReturningHome)
			{
				if (!(((IDynel)npc).Controller is NPCController nPCController))
				{
					return true;
				}
				if (nPCController.IsFollowing())
				{
					nPCController.DoFollow();
				}
				ChaseNavigationPoint home = ToNavigationPoint(value.Coordinates.coordinate);
				ChaseNavigationPoint chaseNavigationPoint = ToNavigationPoint(((IDynel)npc).Coordinates().coordinate);
				if (NpcCombatLeashPolicy.HasReturnedHome(home, chaseNavigationPoint))
				{
					value.ReturningHome = false;
					NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService = chaseNavigation;
					identity = ((IEntity)npc).Identity;
					npcChaseNavigationRuntimeService.Clear(((Identity)(ref identity)).Instance, NpcChaseInvalidationReason.LeashReset);
					nPCController.SnapshotCurrentMotionPosition();
					nPCController.StopFollow();
					nPCController.State = value.ControllerStateBeforeReturn;
					return false;
				}
				NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService2 = chaseNavigation;
				identity = ((IEntity)npc).Identity;
				NpcChaseUpdateResult npcChaseUpdateResult = npcChaseNavigationRuntimeService2.UpdateReturnToHome(((Identity)(ref identity)).Instance, chaseNavigationPoint, home, 0.25, utcNow);
				if (npcChaseUpdateResult.HasDestination && (npcChaseUpdateResult.ShouldIssueMovement || !nPCController.IsFollowing()))
				{
					nPCController.MoveTo(new Vector3
					{
						X = (float)npcChaseUpdateResult.Destination.X,
						Y = (float)npcChaseUpdateResult.Destination.Y,
						Z = (float)npcChaseUpdateResult.Destination.Z
					});
				}
				else
				{
					if (npcChaseUpdateResult.Kind == NpcChaseMovementKind.Unavailable)
					{
						goto IL_01cd;
					}
					if (npcChaseUpdateResult.Kind == NpcChaseMovementKind.Hold)
					{
						NpcChaseNavigationRuntimeService npcChaseNavigationRuntimeService3 = chaseNavigation;
						identity = ((IEntity)npc).Identity;
						if (!npcChaseNavigationRuntimeService3.HasActivePursuit(((Identity)(ref identity)).Instance))
						{
							goto IL_01cd;
						}
					}
				}
				goto IL_01dd;
			}
		}
		return false;
		IL_01dd:
		return true;
		IL_01cd:
		nPCController.SnapshotCurrentMotionPosition();
		nPCController.StopFollow();
		goto IL_01dd;
	}

	private bool IsPlayerControlledPet(ICharacter npc)
	{
		if (!PetCombatRules.IsPlayerOwnedPet(npc))
		{
			return false;
		}
		ICharacter val = PetCombatRules.ResolvePetOwner(npc);
		return val == null || ((IDynel)val).Controller is PlayerController;
	}

	private static ChaseNavigationPoint ToNavigationPoint(Vector3 point)
	{
		return new ChaseNavigationPoint(point.x, point.y, point.z);
	}

	private void ScheduleNpcDeathCorpseSpawn(ICharacter target, Identity corpseIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (corpseIdentity != Identity.None)
		{
			playfield.ScheduleCorpseSpawn(target, corpseIdentity);
		}
		else
		{
			LogUtil.Debug((DebugInfoDetail)128, $"Skipping corpse visual spawn for {((IEntity)target).Identity}; no known MonsterData-to-CATMesh mapping.");
		}
	}

	private void ScheduleDeadNpcDespawn(ICharacter target)
	{
		corpseLifecycle.ScheduleDeadNpcDespawn(target);
	}

	private static void LogCapturedAreteRobotContent(bool isError, string message)
	{
		LogUtil.Debug((DebugInfoDetail)(isError ? 512 : 128), message);
	}
}
