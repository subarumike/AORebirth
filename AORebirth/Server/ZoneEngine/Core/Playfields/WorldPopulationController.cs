namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using Utility;

    internal sealed class WorldPopulationController
    {
        private const int MaximumRespawnsPerTick = 32;
        private readonly Playfield playfield;
        private readonly OrdinaryEnemyCatalog catalog;
        private readonly OrdinaryEnemyRuntimeService runtime;
        private readonly WorldRespawnScheduler scheduler;
        private readonly Dictionary<string, WorldSpawnDefinition> definitions = new Dictionary<string, WorldSpawnDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpawnGroupDefinition> groups = new Dictionary<string, SpawnGroupDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RespawnPolicyDefinition> policies = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrdinaryEnemySpawnDefinition> ordinaryRows = new Dictionary<string, OrdinaryEnemySpawnDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, PopulationRuntimeState> states = new Dictionary<string, PopulationRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> spawnKeyByRuntimeIdentity = new Dictionary<int, string>();
        private readonly bool diagnosticsEnabled = string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_POPULATION_DIAGNOSTICS"), "1", StringComparison.Ordinal);

        internal WorldPopulationController(Playfield playfield, OrdinaryEnemyCatalog catalog, OrdinaryEnemyRuntimeService runtime)
        {
            this.playfield = playfield;
            this.catalog = catalog;
            this.runtime = runtime;
            this.scheduler = new WorldRespawnScheduler();
            this.RegisterOrdinaryDefinitions();
        }

        internal void ActivatePlayfield(Identity playfieldIdentity)
        {
            foreach (WorldSpawnDefinition definition in this.definitions.Values
                .Where(x => x.PlayfieldId == playfieldIdentity.Instance)
                .OrderBy(x => x.SpawnKey, StringComparer.Ordinal))
            {
                PopulationRuntimeState state = this.states[definition.SpawnKey];
                if (!definition.Enabled || definition.Quarantined || definition.ActivationPolicy != WorldSpawnActivationPolicy.PlayfieldStart)
                {
                    state.LifecycleState = definition.Quarantined ? PopulationLifecycleState.Quarantined : PopulationLifecycleState.Disabled;
                    continue;
                }
                if (state.LifecycleState == PopulationLifecycleState.Alive || state.LifecycleState == PopulationLifecycleState.Spawning) continue;
                this.Spawn(definition, state, false);
            }
        }

        internal void RegisterDefinition(WorldSpawnDefinition definition)
        {
            if (definition == null || this.definitions.ContainsKey(definition.SpawnKey)) throw new InvalidOperationException("Duplicate or missing world spawn definition: " + (definition == null ? "null" : definition.SpawnKey));
            this.definitions.Add(definition.SpawnKey, definition);
            this.states.Add(definition.SpawnKey, NewState(definition));
        }

        internal void RegisterGroup(SpawnGroupDefinition group)
        {
            if (group == null || this.groups.ContainsKey(group.SpawnGroupKey)) throw new InvalidOperationException("Duplicate or missing spawn group: " + (group == null ? "null" : group.SpawnGroupKey));
            this.groups.Add(group.SpawnGroupKey, group);
        }

        internal void RegisterRespawnPolicy(RespawnPolicyDefinition policy)
        {
            if (policy == null || this.policies.ContainsKey(policy.RespawnPolicyKey)) throw new InvalidOperationException("Duplicate or missing respawn policy: " + (policy == null ? "null" : policy.RespawnPolicyKey));
            this.policies.Add(policy.RespawnPolicyKey, policy);
        }

        internal bool Spawn(string spawnKey)
        {
            WorldSpawnDefinition definition; PopulationRuntimeState state;
            if (!this.definitions.TryGetValue(spawnKey, out definition) || !this.states.TryGetValue(spawnKey, out state) || state.CurrentRuntimeIdentity.Instance != 0) return false;
            this.Spawn(definition, state, false);
            return state.LifecycleState == PopulationLifecycleState.Alive;
        }

        internal void DeactivatePlayfield(int playfieldId)
        {
            this.scheduler.CancelPlayfield(playfieldId);
            foreach (PopulationRuntimeState state in this.states.Values.Where(x => x.PlayfieldId == playfieldId))
            {
                if (state.CurrentRuntimeIdentity.Instance != 0) this.spawnKeyByRuntimeIdentity.Remove(state.CurrentRuntimeIdentity.Instance);
                state.CurrentRuntimeIdentity = Identity.None;
                state.RespawnDueAt = null;
                state.LifecycleState = PopulationLifecycleState.Despawned;
                state.LastTransition = DateTime.UtcNow;
            }
        }

        internal void NotifyDeath(ICharacter target, Identity corpseIdentity, DateTime diedAtUtc)
        {
            string spawnKey;
            if (target == null || !this.spawnKeyByRuntimeIdentity.TryGetValue(target.Identity.Instance, out spawnKey)) return;
            PopulationRuntimeState state = this.states[spawnKey];
            if (state.CurrentRuntimeIdentity != target.Identity || state.LifecycleState != PopulationLifecycleState.Alive) return;
            state.DiedAt = diedAtUtc;
            state.CorpseIdentity = corpseIdentity;
            state.LifecycleState = PopulationLifecycleState.DeadCorpseActive;
            state.LastTransition = diedAtUtc;
            this.ScheduleIfStartMatches(spawnKey, RespawnDelayStartsAt.Death, diedAtUtc);
            this.Trace("death", state, null);
        }

        internal void NotifyNpcDespawn(ICharacter target, DateTime despawnedAtUtc)
        {
            OrdinaryEnemyRuntimeDefinition ignored;
            if (!this.runtime.ReleasePopulationRuntime(target, out ignored)) return;
            string spawnKey;
            if (!this.spawnKeyByRuntimeIdentity.TryGetValue(target.Identity.Instance, out spawnKey)) return;
            this.spawnKeyByRuntimeIdentity.Remove(target.Identity.Instance);
            PopulationRuntimeState state = this.states[spawnKey];
            state.CurrentRuntimeIdentity = Identity.None;
            state.LifecycleState = PopulationLifecycleState.Despawned;
            state.LastTransition = despawnedAtUtc;
            this.ScheduleIfStartMatches(spawnKey, RespawnDelayStartsAt.NpcDespawn, despawnedAtUtc);
        }

        internal void NotifyCorpseRemoved(Identity corpseIdentity, DateTime removedAtUtc)
        {
            PopulationRuntimeState state = this.states.Values.FirstOrDefault(x => x.CorpseIdentity == corpseIdentity);
            if (state == null) return;
            state.CorpseIdentity = Identity.None;
            this.ScheduleIfStartMatches(state.SpawnKey, RespawnDelayStartsAt.CorpseRemoval, removedAtUtc);
            this.Trace("corpse-removed", state, null);
        }

        internal void ProcessDue(DateTime utcNow)
        {
            foreach (WorldRespawnSchedule due in this.scheduler.TakeDue(utcNow, MaximumRespawnsPerTick))
            {
                PopulationRuntimeState state = this.states[due.SpawnKey];
                if (state.Generation != due.Generation || state.CurrentRuntimeIdentity.Instance != 0) continue;
                state.LifecycleState = PopulationLifecycleState.Respawning;
                state.RespawnDueAt = null;
                this.Spawn(this.definitions[due.SpawnKey], state, true);
            }
        }

        internal bool CancelRespawn(string spawnKey) { return this.scheduler.Cancel(spawnKey); }
        internal bool ScheduleRespawn(string spawnKey, DateTime startedAtUtc)
        {
            PopulationRuntimeState state;
            if (!this.states.TryGetValue(spawnKey, out state)) return false;
            RespawnPolicyDefinition policy = this.policies[this.definitions[spawnKey].RespawnPolicyKey];
            this.ScheduleIfStartMatches(spawnKey, policy.DelayStartsAt, startedAtUtc);
            return this.scheduler.Contains(spawnKey);
        }
        internal PopulationRuntimeState GetState(string spawnKey) { PopulationRuntimeState value; return this.states.TryGetValue(spawnKey, out value) ? value : null; }
        internal PopulationRuntimeState[] EnumeratePlayfield(int playfieldId) { return this.states.Values.Where(x => x.PlayfieldId == playfieldId).OrderBy(x => x.SpawnKey, StringComparer.Ordinal).ToArray(); }
        internal void ResetSpawn(string spawnKey)
        {
            PopulationRuntimeState state = this.GetState(spawnKey); if (state == null) return;
            this.scheduler.Cancel(spawnKey); state.RespawnDueAt = null;
            if (state.CurrentRuntimeIdentity.Instance == 0) this.Spawn(this.definitions[spawnKey], state, true);
        }
        internal void ResetGroup(string groupKey) { foreach (string key in this.groups[groupKey].SpawnKeys) this.ResetSpawn(key); }
        internal void ClearPlayfield(int playfieldId) { this.DeactivatePlayfield(playfieldId); }
        internal void ClearAll() { foreach (int id in this.states.Values.Select(x => x.PlayfieldId).Distinct().ToArray()) this.DeactivatePlayfield(id); }

        private void RegisterOrdinaryDefinitions()
        {
            foreach (OrdinaryEnemySpawnDefinition row in this.catalog.GetSpawns().OrderBy(x => x.SourceIdentity))
            {
                string policyKey = row.HasRespawnDelay ? "ordinary.fixed." + row.RespawnDelaySeconds.Value.ToString("0.###", CultureInfo.InvariantCulture) : "ordinary.none";
                if (!this.policies.ContainsKey(policyKey)) this.policies.Add(policyKey, new RespawnPolicyDefinition
                {
                    RespawnPolicyKey = policyKey, Mode = row.HasRespawnDelay ? WorldRespawnMode.FixedDelay : WorldRespawnMode.None,
                    FixedDelaySeconds = row.RespawnDelaySeconds, RespawnAtOriginalPosition = true, ResetHealth = true,
                    ResetMovementState = true, ResetAggressionState = true, DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
                    Evidence = row.SourceCapture, Confidence = row.RespawnEvidence.ToString(), Enabled = true
                });
                var definition = new WorldSpawnDefinition
                {
                    SpawnKey = row.SpawnKey, EnemyProfileKey = row.ProfileKey,
                    ConfiguredIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = row.SourceIdentity },
                    PlayfieldId = row.PlayfieldInstance, X = row.X, Y = row.Y, Z = row.Z,
                    OrientationX = row.HeadingX, OrientationY = row.HeadingY, OrientationZ = row.HeadingZ, OrientationW = row.HeadingW,
                    SpawnGroupKey = "ordinary.playfield." + row.PlayfieldInstance, RespawnPolicyKey = policyKey,
                    ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart,
                    Enabled = row.Disposition == OrdinaryEnemyRuntimeDisposition.Active,
                    Quarantined = row.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined,
                    Evidence = row.SourceCapture, Confidence = "CAPTURE_BACKED", Source = row.SourceOwnerIdentity
                };
                this.definitions.Add(definition.SpawnKey, definition); this.ordinaryRows.Add(definition.SpawnKey, row);
                this.states.Add(definition.SpawnKey, NewState(definition));
            }
            foreach (IGrouping<int, WorldSpawnDefinition> playfieldRows in this.definitions.Values.GroupBy(x => x.PlayfieldId))
            {
                string key = "ordinary.playfield." + playfieldRows.Key;
                this.groups.Add(key, new SpawnGroupDefinition { SpawnGroupKey = key, DisplayName = key, PlayfieldId = playfieldRows.Key,
                    SpawnKeys = playfieldRows.Select(x => x.SpawnKey).OrderBy(x => x, StringComparer.Ordinal).ToArray(), ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart,
                    MinimumAlive = 0, MaximumAlive = playfieldRows.Count(x => x.Enabled), Enabled = true, Evidence = "ordinary-catalog", Confidence = "CAPTURE_BACKED" });
            }
            WorldPopulationDefinitionValidator.Validate(this.definitions.Values, this.groups.Values, this.policies.Values, this.catalog.GetProfiles().Select(x => x.ProfileKey));
        }

        private void Spawn(WorldSpawnDefinition definition, PopulationRuntimeState state, bool respawn)
        {
            if (!definition.Enabled || definition.Quarantined) return;
            SpawnGroupDefinition group = this.groups[definition.SpawnGroupKey];
            int alive = group.SpawnKeys.Count(x => this.states[x].LifecycleState == PopulationLifecycleState.Alive);
            if (!group.Enabled || alive >= group.MaximumAlive) return;
            state.LifecycleState = respawn ? PopulationLifecycleState.Respawning : PopulationLifecycleState.Spawning;
            Identity runtimeIdentity;
            if (!this.runtime.SpawnFromPopulation(this.playfield, this.playfield.Identity, this.ordinaryRows[definition.SpawnKey], out runtimeIdentity))
            {
                state.LifecycleState = PopulationLifecycleState.Failed; state.FailureState = "ordinary-runtime-spawn-failed"; state.LastTransition = DateTime.UtcNow; return;
            }
            state.CurrentRuntimeIdentity = runtimeIdentity; state.SpawnedAt = DateTime.UtcNow; state.Generation++; state.LifecycleState = PopulationLifecycleState.Alive;
            state.LastTransition = state.SpawnedAt.Value; state.FailureState = null; state.CorpseIdentity = Identity.None; this.spawnKeyByRuntimeIdentity[runtimeIdentity.Instance] = definition.SpawnKey;
            this.Trace(respawn ? "respawn-completed" : "spawn-success", state, null);
        }

        private void ScheduleIfStartMatches(string spawnKey, RespawnDelayStartsAt start, DateTime startedAtUtc)
        {
            RespawnPolicyDefinition policy = this.policies[this.definitions[spawnKey].RespawnPolicyKey];
            if (!policy.Enabled || policy.Mode == WorldRespawnMode.None || policy.DelayStartsAt != start) return;
            if (policy.Mode == WorldRespawnMode.Scripted || policy.Mode == WorldRespawnMode.Unresolved) return;
            PopulationRuntimeState state = this.states[spawnKey];
            DateTime due = startedAtUtc.Add(WorldRespawnScheduler.SelectDelay(policy, null));
            if (!this.scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = spawnKey, GroupKey = state.SpawnGroupKey, PlayfieldId = state.PlayfieldId, DueAtUtc = due, Generation = state.Generation })) return;
            state.RespawnDueAt = due; state.LifecycleState = PopulationLifecycleState.WaitingForRespawn; state.LastTransition = startedAtUtc; this.Trace("respawn-scheduled", state, null);
        }

        private static PopulationRuntimeState NewState(WorldSpawnDefinition value) { return new PopulationRuntimeState { SpawnKey = value.SpawnKey, SpawnGroupKey = value.SpawnGroupKey,
            EnemyProfileKey = value.EnemyProfileKey, ConfiguredIdentity = value.ConfiguredIdentity, CurrentRuntimeIdentity = Identity.None, PlayfieldId = value.PlayfieldId,
            LifecycleState = value.Quarantined ? PopulationLifecycleState.Quarantined : value.Enabled ? PopulationLifecycleState.Ready : PopulationLifecycleState.Disabled,
            CorpseIdentity = Identity.None, LastTransition = DateTime.UtcNow }; }

        private void Trace(string eventName, PopulationRuntimeState state, string failure)
        {
            if (!this.diagnosticsEnabled) return;
            LogUtil.Debug(DebugInfoDetail.Engine, string.Format(CultureInfo.InvariantCulture,
                "population event={0} spawn={1} group={2} profile={3} configured={4} runtime={5} playfield={6} generation={7} state={8} due={9} failure={10}",
                eventName, state.SpawnKey, state.SpawnGroupKey, state.EnemyProfileKey, state.ConfiguredIdentity, state.CurrentRuntimeIdentity,
                state.PlayfieldId, state.Generation, state.LifecycleState, state.RespawnDueAt.HasValue ? state.RespawnDueAt.Value.ToString("o") : "none", failure ?? "none"));
        }
    }
}
