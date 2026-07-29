using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class WorldPopulationController
{
	private const int MaximumRespawnsPerTick = 32;

	private const double OrdinaryEnemyDefaultRespawnSeconds = 240.0;

	private const string OrdinaryEnemyDefaultRespawnPolicyKey = "ordinary.default.240";

	private readonly Playfield playfield;

	private readonly OrdinaryEnemyCatalog catalog;

	private readonly OrdinaryEnemyRuntimeService runtime;

	private readonly WorldRespawnScheduler scheduler;

	private readonly IDictionary<string, RespawnPolicyDefinition> ordinaryGroupRespawnPolicies;

	private readonly IPopulationRandomSource respawnRandom;

	private readonly Dictionary<string, WorldSpawnDefinition> definitions = new Dictionary<string, WorldSpawnDefinition>(StringComparer.Ordinal);

	private readonly Dictionary<string, SpawnGroupDefinition> groups = new Dictionary<string, SpawnGroupDefinition>(StringComparer.Ordinal);

	private readonly Dictionary<string, RespawnPolicyDefinition> policies = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal);

	private readonly Dictionary<string, OrdinaryEnemySpawnDefinition> ordinaryRows = new Dictionary<string, OrdinaryEnemySpawnDefinition>(StringComparer.Ordinal);

	private readonly Dictionary<string, PopulationRuntimeState> states = new Dictionary<string, PopulationRuntimeState>(StringComparer.Ordinal);

	private readonly Dictionary<int, string> spawnKeyByRuntimeIdentity = new Dictionary<int, string>();

	private readonly bool diagnosticsEnabled = string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_POPULATION_DIAGNOSTICS"), "1", StringComparison.Ordinal);

	internal WorldPopulationController(Playfield playfield, OrdinaryEnemyCatalog catalog, OrdinaryEnemyRuntimeService runtime, IDictionary<string, RespawnPolicyDefinition> ordinaryGroupRespawnPolicies = null, IPopulationRandomSource respawnRandom = null)
	{
		this.playfield = playfield;
		this.catalog = catalog;
		this.runtime = runtime;
		scheduler = new WorldRespawnScheduler();
		this.ordinaryGroupRespawnPolicies = ((ordinaryGroupRespawnPolicies == null) ? new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal) : new Dictionary<string, RespawnPolicyDefinition>(ordinaryGroupRespawnPolicies, StringComparer.Ordinal));
		this.respawnRandom = respawnRandom ?? new SystemPopulationRandomSource();
		RegisterOrdinaryDefinitions();
	}

	internal void ActivatePlayfield(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		foreach (WorldSpawnDefinition item in definitions.Values.Where((WorldSpawnDefinition x) => x.PlayfieldId == ((Identity)(ref playfieldIdentity)).Instance).OrderBy((WorldSpawnDefinition x) => x.SpawnKey, StringComparer.Ordinal))
		{
			PopulationRuntimeState populationRuntimeState = states[item.SpawnKey];
			if (!item.Enabled || item.Quarantined || item.ActivationPolicy != WorldSpawnActivationPolicy.PlayfieldStart)
			{
				populationRuntimeState.LifecycleState = (item.Quarantined ? PopulationLifecycleState.Quarantined : PopulationLifecycleState.Disabled);
			}
			else if (populationRuntimeState.LifecycleState != PopulationLifecycleState.Alive && populationRuntimeState.LifecycleState != PopulationLifecycleState.Spawning)
			{
				Spawn(item, populationRuntimeState, respawn: false);
			}
		}
	}

	internal void RegisterDefinition(WorldSpawnDefinition definition)
	{
		if (definition == null || definitions.ContainsKey(definition.SpawnKey))
		{
			throw new InvalidOperationException("Duplicate or missing world spawn definition: " + ((definition == null) ? "null" : definition.SpawnKey));
		}
		definitions.Add(definition.SpawnKey, definition);
		states.Add(definition.SpawnKey, NewState(definition));
	}

	internal void RegisterGroup(SpawnGroupDefinition group)
	{
		if (group == null || groups.ContainsKey(group.SpawnGroupKey))
		{
			throw new InvalidOperationException("Duplicate or missing spawn group: " + ((group == null) ? "null" : group.SpawnGroupKey));
		}
		if (!string.IsNullOrWhiteSpace(group.SharedRespawnPolicyKey) && !policies.ContainsKey(group.SharedRespawnPolicyKey))
		{
			throw new InvalidOperationException("Missing shared respawn policy: " + group.SharedRespawnPolicyKey);
		}
		groups.Add(group.SpawnGroupKey, group);
	}

	internal void RegisterRespawnPolicy(RespawnPolicyDefinition policy)
	{
		WorldRespawnPolicyValidator.RegisterOrRejectConflict(policies, policy);
	}

	internal bool Spawn(string spawnKey)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (definitions.TryGetValue(spawnKey, out var value) && states.TryGetValue(spawnKey, out var value2))
		{
			Identity currentRuntimeIdentity = value2.CurrentRuntimeIdentity;
			if (((Identity)(ref currentRuntimeIdentity)).Instance == 0)
			{
				Spawn(value, value2, respawn: false);
				return value2.LifecycleState == PopulationLifecycleState.Alive;
			}
		}
		return false;
	}

	internal void DeactivatePlayfield(int playfieldId)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		scheduler.CancelPlayfield(playfieldId);
		foreach (PopulationRuntimeState item in states.Values.Where((PopulationRuntimeState x) => x.PlayfieldId == playfieldId))
		{
			Identity currentRuntimeIdentity = item.CurrentRuntimeIdentity;
			if (((Identity)(ref currentRuntimeIdentity)).Instance != 0)
			{
				Dictionary<int, string> dictionary = spawnKeyByRuntimeIdentity;
				currentRuntimeIdentity = item.CurrentRuntimeIdentity;
				dictionary.Remove(((Identity)(ref currentRuntimeIdentity)).Instance);
			}
			WorldPopulationGenerationLifecycle.ClearRuntime(item, DateTime.UtcNow);
		}
	}

	internal void NotifyDeath(ICharacter target, Identity corpseIdentity, DateTime diedAtUtc)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			return;
		}
		Dictionary<int, string> dictionary = spawnKeyByRuntimeIdentity;
		Identity identity = ((IEntity)target).Identity;
		if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			PopulationRuntimeState populationRuntimeState = states[value];
			if (!(populationRuntimeState.CurrentRuntimeIdentity != ((IEntity)target).Identity) && populationRuntimeState.LifecycleState == PopulationLifecycleState.Alive)
			{
				populationRuntimeState.DiedAt = diedAtUtc;
				populationRuntimeState.CorpseIdentity = corpseIdentity;
				populationRuntimeState.LifecycleState = PopulationLifecycleState.DeadCorpseActive;
				populationRuntimeState.LastTransition = diedAtUtc;
				ScheduleIfStartMatches(value, RespawnDelayStartsAt.Death, diedAtUtc);
				Trace("death", populationRuntimeState, null);
			}
		}
	}

	internal void NotifyNpcDespawn(ICharacter target, DateTime despawnedAtUtc)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (!runtime.ReleasePopulationRuntime(target, out var _))
		{
			return;
		}
		Dictionary<int, string> dictionary = spawnKeyByRuntimeIdentity;
		Identity identity = ((IEntity)target).Identity;
		if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			Dictionary<int, string> dictionary2 = spawnKeyByRuntimeIdentity;
			identity = ((IEntity)target).Identity;
			dictionary2.Remove(((Identity)(ref identity)).Instance);
			PopulationRuntimeState populationRuntimeState = states[value];
			populationRuntimeState.CurrentRuntimeIdentity = Identity.None;
			populationRuntimeState.LifecycleState = PopulationLifecycleState.Despawned;
			populationRuntimeState.LastTransition = despawnedAtUtc;
			ScheduleIfStartMatches(value, RespawnDelayStartsAt.NpcDespawn, despawnedAtUtc);
			RespawnPolicyDefinition policy = policies[definitions[value].RespawnPolicyKey];
			if (WorldRespawnScheduler.TryResumePendingAfterRuntimeRelease(scheduler, populationRuntimeState, policy, despawnedAtUtc))
			{
				Trace("respawn-resumed-after-despawn", populationRuntimeState, null);
			}
		}
	}

	internal void NotifyCorpseRemoved(Identity corpseIdentity, DateTime removedAtUtc)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		PopulationRuntimeState populationRuntimeState = states.Values.FirstOrDefault((PopulationRuntimeState x) => x.CorpseIdentity == corpseIdentity);
		if (populationRuntimeState != null)
		{
			populationRuntimeState.CorpseIdentity = Identity.None;
			ScheduleIfStartMatches(populationRuntimeState.SpawnKey, RespawnDelayStartsAt.CorpseRemoval, removedAtUtc);
			Trace("corpse-removed", populationRuntimeState, null);
		}
	}

	internal void ProcessDue(DateTime utcNow)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		WorldRespawnSchedule[] array = scheduler.TakeDue(utcNow, 32);
		foreach (WorldRespawnSchedule worldRespawnSchedule in array)
		{
			PopulationRuntimeState populationRuntimeState = states[worldRespawnSchedule.SpawnKey];
			if (populationRuntimeState.Generation != worldRespawnSchedule.Generation)
			{
				if (populationRuntimeState.RespawnDueAt == worldRespawnSchedule.DueAtUtc)
				{
					populationRuntimeState.RespawnDueAt = null;
				}
				continue;
			}
			Identity currentRuntimeIdentity = populationRuntimeState.CurrentRuntimeIdentity;
			if (((Identity)(ref currentRuntimeIdentity)).Instance == 0)
			{
				populationRuntimeState.LifecycleState = PopulationLifecycleState.Respawning;
				populationRuntimeState.RespawnDueAt = null;
				Spawn(definitions[worldRespawnSchedule.SpawnKey], populationRuntimeState, respawn: true);
			}
		}
	}

	internal bool CancelRespawn(string spawnKey)
	{
		return scheduler.Cancel(spawnKey);
	}

	internal bool ScheduleRespawn(string spawnKey, DateTime startedAtUtc)
	{
		if (!states.TryGetValue(spawnKey, out var _))
		{
			return false;
		}
		RespawnPolicyDefinition respawnPolicyDefinition = policies[definitions[spawnKey].RespawnPolicyKey];
		ScheduleIfStartMatches(spawnKey, respawnPolicyDefinition.DelayStartsAt, startedAtUtc);
		return scheduler.Contains(spawnKey);
	}

	internal PopulationRuntimeState GetState(string spawnKey)
	{
		PopulationRuntimeState value;
		return states.TryGetValue(spawnKey, out value) ? value : null;
	}

	internal PopulationRuntimeState[] EnumeratePlayfield(int playfieldId)
	{
		return states.Values.Where((PopulationRuntimeState x) => x.PlayfieldId == playfieldId).OrderBy((PopulationRuntimeState x) => x.SpawnKey, StringComparer.Ordinal).ToArray();
	}

	internal void ResetSpawn(string spawnKey)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		PopulationRuntimeState state = GetState(spawnKey);
		if (state != null)
		{
			scheduler.Cancel(spawnKey);
			state.RespawnDueAt = null;
			Identity currentRuntimeIdentity = state.CurrentRuntimeIdentity;
			if (((Identity)(ref currentRuntimeIdentity)).Instance == 0)
			{
				Spawn(definitions[spawnKey], state, respawn: true);
			}
		}
	}

	internal void ResetGroup(string groupKey)
	{
		string[] spawnKeys = groups[groupKey].SpawnKeys;
		foreach (string spawnKey in spawnKeys)
		{
			ResetSpawn(spawnKey);
		}
	}

	internal void ClearPlayfield(int playfieldId)
	{
		DeactivatePlayfield(playfieldId);
	}

	internal void ClearAll()
	{
		int[] array = states.Values.Select((PopulationRuntimeState x) => x.PlayfieldId).Distinct().ToArray();
		foreach (int playfieldId in array)
		{
			DeactivatePlayfield(playfieldId);
		}
	}

	private void RegisterOrdinaryDefinitions()
	{
		//IL_033a: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		RespawnPolicyDefinition respawnPolicyDefinition = new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "ordinary.default.240",
			Mode = WorldRespawnMode.FixedDelay,
			FixedDelaySeconds = 240.0,
			RespawnAtOriginalPosition = true,
			ResetHealth = true,
			ResetMovementState = true,
			ResetAggressionState = true,
			DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
			Evidence = "PF127 ordinary-enemy project policy; not universal official AO timing",
			Confidence = "POLICY",
			Enabled = true
		};
		WorldRespawnPolicyValidator.RegisterOrRejectConflict(policies, respawnPolicyDefinition);
		OrdinaryEnemySpawnDefinition[] array = (from x in catalog.GetSpawns()
			orderby x.SourceIdentity
			select x).ToArray();
		foreach (IGrouping<int, OrdinaryEnemySpawnDefinition> item in from x in array
			group x by x.PlayfieldInstance)
		{
			string text = "ordinary.playfield." + item.Key;
			SpawnGroupDefinition spawnGroupDefinition = new SpawnGroupDefinition
			{
				SpawnGroupKey = text,
				DisplayName = text,
				PlayfieldId = item.Key,
				SpawnKeys = item.Select((OrdinaryEnemySpawnDefinition x) => x.SpawnKey).OrderBy((string x) => x, StringComparer.Ordinal).ToArray(),
				ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart,
				MinimumAlive = 0,
				MaximumAlive = item.Count((OrdinaryEnemySpawnDefinition x) => x.Disposition == OrdinaryEnemyRuntimeDisposition.Active || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(x.SourceIdentity)),
				Enabled = true,
				Evidence = "ordinary-catalog",
				Confidence = "CAPTURE_BACKED"
			};
			WorldRespawnPolicyResolver.ApplyGroupConfiguration(spawnGroupDefinition, ordinaryGroupRespawnPolicies, policies);
			groups.Add(text, spawnGroupDefinition);
		}
		string text2 = ordinaryGroupRespawnPolicies.Keys.FirstOrDefault((string key) => !groups.ContainsKey(key));
		if (text2 != null)
		{
			throw new InvalidOperationException("Unknown ordinary respawn group configuration: " + text2);
		}
		OrdinaryEnemySpawnDefinition[] array2 = array;
		foreach (OrdinaryEnemySpawnDefinition ordinaryEnemySpawnDefinition in array2)
		{
			string text3 = "ordinary.playfield." + ordinaryEnemySpawnDefinition.PlayfieldInstance;
			bool flag = ordinaryEnemySpawnDefinition.Disposition == OrdinaryEnemyRuntimeDisposition.Active || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(ordinaryEnemySpawnDefinition.SourceIdentity);
			WorldRespawnPolicyResolution worldRespawnPolicyResolution = WorldRespawnPolicyResolver.Resolve(WorldPopulationClassification.OrdinaryEnemy, ordinaryEnemySpawnDefinition.RespawnPolicy, WorldRespawnPolicyResolver.ResolveGroupAssignment(groups[text3], policies), respawnPolicyDefinition);
			if (!worldRespawnPolicyResolution.IsValid)
			{
				throw new InvalidOperationException("Ordinary enemy respawn policy failed closed: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			RespawnPolicyDefinition policy = worldRespawnPolicyResolution.Policy;
			WorldRespawnPolicyValidator.RegisterOrRejectConflict(policies, policy);
			WorldSpawnDefinition obj = new WorldSpawnDefinition
			{
				SpawnKey = ordinaryEnemySpawnDefinition.SpawnKey,
				EnemyProfileKey = ordinaryEnemySpawnDefinition.ProfileKey
			};
			Identity configuredIdentity = default(Identity);
			((Identity)(ref configuredIdentity)).Type = (IdentityType)50000;
			((Identity)(ref configuredIdentity)).Instance = ordinaryEnemySpawnDefinition.SourceIdentity;
			obj.ConfiguredIdentity = configuredIdentity;
			obj.PlayfieldId = ordinaryEnemySpawnDefinition.PlayfieldInstance;
			obj.X = ordinaryEnemySpawnDefinition.X;
			obj.Y = ordinaryEnemySpawnDefinition.Y;
			obj.Z = ordinaryEnemySpawnDefinition.Z;
			obj.OrientationX = ordinaryEnemySpawnDefinition.HeadingX;
			obj.OrientationY = ordinaryEnemySpawnDefinition.HeadingY;
			obj.OrientationZ = ordinaryEnemySpawnDefinition.HeadingZ;
			obj.OrientationW = ordinaryEnemySpawnDefinition.HeadingW;
			obj.SpawnGroupKey = text3;
			obj.RespawnPolicyKey = policy.RespawnPolicyKey;
			obj.ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart;
			obj.Classification = WorldPopulationClassification.OrdinaryEnemy;
			obj.Enabled = flag;
			obj.Quarantined = ordinaryEnemySpawnDefinition.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined && !flag;
			obj.Evidence = ordinaryEnemySpawnDefinition.SourceCapture;
			obj.Confidence = "CAPTURE_BACKED";
			obj.Source = ordinaryEnemySpawnDefinition.SourceOwnerIdentity;
			WorldSpawnDefinition worldSpawnDefinition = obj;
			definitions.Add(worldSpawnDefinition.SpawnKey, worldSpawnDefinition);
			ordinaryRows.Add(worldSpawnDefinition.SpawnKey, ordinaryEnemySpawnDefinition);
			states.Add(worldSpawnDefinition.SpawnKey, NewState(worldSpawnDefinition));
		}
		WorldPopulationDefinitionValidator.Validate(definitions.Values, groups.Values, policies.Values, from x in catalog.GetProfiles()
			select x.ProfileKey);
	}

	private void Spawn(WorldSpawnDefinition definition, PopulationRuntimeState state, bool respawn)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		if (!definition.Enabled || definition.Quarantined)
		{
			return;
		}
		SpawnGroupDefinition spawnGroupDefinition = groups[definition.SpawnGroupKey];
		int num = spawnGroupDefinition.SpawnKeys.Count((string x) => states[x].LifecycleState == PopulationLifecycleState.Alive);
		if (spawnGroupDefinition.Enabled && num < spawnGroupDefinition.MaximumAlive)
		{
			state.LifecycleState = (respawn ? PopulationLifecycleState.Respawning : PopulationLifecycleState.Spawning);
			state.SelectedLevel = null;
			int generation = checked(state.Generation + 1);
			if (!runtime.SpawnFromPopulation(playfield, ((PooledObject)playfield).Identity, ordinaryRows[definition.SpawnKey], generation, out var runtimeIdentity, out var selectedGeneration))
			{
				state.LifecycleState = PopulationLifecycleState.Failed;
				state.FailureState = "ordinary-runtime-spawn-failed";
				state.LastTransition = DateTime.UtcNow;
			}
			else
			{
				WorldPopulationGenerationLifecycle.ApplySpawnSuccess(state, runtimeIdentity, selectedGeneration, DateTime.UtcNow);
				spawnKeyByRuntimeIdentity[((Identity)(ref runtimeIdentity)).Instance] = definition.SpawnKey;
				Trace(respawn ? "respawn-completed" : "spawn-success", state, null);
			}
		}
	}

	private void ScheduleIfStartMatches(string spawnKey, RespawnDelayStartsAt start, DateTime startedAtUtc)
	{
		RespawnPolicyDefinition policy = policies[definitions[spawnKey].RespawnPolicyKey];
		PopulationRuntimeState state = states[spawnKey];
		if (WorldRespawnScheduler.TryScheduleForLifecycle(scheduler, state, policy, start, startedAtUtc, respawnRandom))
		{
			Trace("respawn-scheduled", state, null);
		}
	}

	private static PopulationRuntimeState NewState(WorldSpawnDefinition value)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		return new PopulationRuntimeState
		{
			SpawnKey = value.SpawnKey,
			SpawnGroupKey = value.SpawnGroupKey,
			EnemyProfileKey = value.EnemyProfileKey,
			ConfiguredIdentity = value.ConfiguredIdentity,
			CurrentRuntimeIdentity = Identity.None,
			PlayfieldId = value.PlayfieldId,
			LifecycleState = (value.Quarantined ? PopulationLifecycleState.Quarantined : (value.Enabled ? PopulationLifecycleState.Ready : PopulationLifecycleState.Disabled)),
			CorpseIdentity = Identity.None,
			LastTransition = DateTime.UtcNow
		};
	}

	private void Trace(string eventName, PopulationRuntimeState state, string failure)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if (diagnosticsEnabled)
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "population event={0} spawn={1} group={2} profile={3} configured={4} runtime={5} playfield={6} generation={7} state={8} due={9} failure={10}", eventName, state.SpawnKey, state.SpawnGroupKey, state.EnemyProfileKey, state.ConfiguredIdentity, state.CurrentRuntimeIdentity, state.PlayfieldId, state.Generation, state.LifecycleState, state.RespawnDueAt.HasValue ? state.RespawnDueAt.Value.ToString("o") : "none", failure ?? "none"));
		}
	}
}
