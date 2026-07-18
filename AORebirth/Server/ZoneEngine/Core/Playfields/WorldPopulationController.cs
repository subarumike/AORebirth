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
    using ZoneEngine.Core.Playfields;

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

        internal WorldPopulationController(
            Playfield playfield,
            OrdinaryEnemyCatalog catalog,
            OrdinaryEnemyRuntimeService runtime,
            IDictionary<string, RespawnPolicyDefinition> ordinaryGroupRespawnPolicies = null,
            IPopulationRandomSource respawnRandom = null)
        {
            this.playfield = playfield;
            this.catalog = catalog;
            this.runtime = runtime;
            this.scheduler = new WorldRespawnScheduler();
            this.ordinaryGroupRespawnPolicies = ordinaryGroupRespawnPolicies == null
                ? new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal)
                : new Dictionary<string, RespawnPolicyDefinition>(ordinaryGroupRespawnPolicies, StringComparer.Ordinal);
            this.respawnRandom = respawnRandom ?? new SystemPopulationRandomSource();
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
            if (!string.IsNullOrWhiteSpace(group.SharedRespawnPolicyKey) && !this.policies.ContainsKey(group.SharedRespawnPolicyKey))
                throw new InvalidOperationException("Missing shared respawn policy: " + group.SharedRespawnPolicyKey);
            this.groups.Add(group.SpawnGroupKey, group);
        }

        internal void RegisterRespawnPolicy(RespawnPolicyDefinition policy)
        {
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(this.policies, policy);
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
                WorldPopulationGenerationLifecycle.ClearRuntime(state, DateTime.UtcNow);
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
            RespawnPolicyDefinition policy = this.policies[this.definitions[spawnKey].RespawnPolicyKey];
            if (WorldRespawnScheduler.TryResumePendingAfterRuntimeRelease(this.scheduler, state, policy, despawnedAtUtc))
                this.Trace("respawn-resumed-after-despawn", state, null);
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
                if (state.Generation != due.Generation)
                {
                    if (state.RespawnDueAt == due.DueAtUtc) state.RespawnDueAt = null;
                    continue;
                }
                if (state.CurrentRuntimeIdentity.Instance != 0) continue;
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
            RespawnPolicyDefinition ordinaryDefault = new RespawnPolicyDefinition
            {
                RespawnPolicyKey = OrdinaryEnemyDefaultRespawnPolicyKey,
                Mode = WorldRespawnMode.FixedDelay,
                FixedDelaySeconds = OrdinaryEnemyDefaultRespawnSeconds,
                RespawnAtOriginalPosition = true,
                ResetHealth = true,
                ResetMovementState = true,
                ResetAggressionState = true,
                DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
                Evidence = "PF127 ordinary-enemy project policy; not universal official AO timing",
                Confidence = "POLICY",
                Enabled = true
            };
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(this.policies, ordinaryDefault);
            OrdinaryEnemySpawnDefinition[] ordinaryRows = this.catalog.GetSpawns().OrderBy(x => x.SourceIdentity).ToArray();
            foreach (IGrouping<int, OrdinaryEnemySpawnDefinition> playfieldRows in ordinaryRows.GroupBy(x => x.PlayfieldInstance))
            {
                string key = "ordinary.playfield." + playfieldRows.Key;
                var group = new SpawnGroupDefinition { SpawnGroupKey = key, DisplayName = key, PlayfieldId = playfieldRows.Key,
                    SpawnKeys = playfieldRows.Select(x => x.SpawnKey).OrderBy(x => x, StringComparer.Ordinal).ToArray(), ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart,
                    MinimumAlive = 0, MaximumAlive = playfieldRows.Count(
                        x => x.Disposition == OrdinaryEnemyRuntimeDisposition.Active
                             || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(x.SourceIdentity)), Enabled = true,
                    Evidence = "ordinary-catalog", Confidence = "CAPTURE_BACKED" };
                WorldRespawnPolicyResolver.ApplyGroupConfiguration(group, this.ordinaryGroupRespawnPolicies, this.policies);
                this.groups.Add(key, group);
            }
            string unusedGroupPolicy = this.ordinaryGroupRespawnPolicies.Keys.FirstOrDefault(key => !this.groups.ContainsKey(key));
            if (unusedGroupPolicy != null)
                throw new InvalidOperationException("Unknown ordinary respawn group configuration: " + unusedGroupPolicy);
            foreach (OrdinaryEnemySpawnDefinition row in ordinaryRows)
            {
                string groupKey = "ordinary.playfield." + row.PlayfieldInstance;
                bool runtimeEnabled = row.Disposition == OrdinaryEnemyRuntimeDisposition.Active
                                      || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(
                                          row.SourceIdentity);
                WorldRespawnPolicyResolution respawn = WorldRespawnPolicyResolver.Resolve(
                    WorldPopulationClassification.OrdinaryEnemy,
                    row.RespawnPolicy,
                    WorldRespawnPolicyResolver.ResolveGroupAssignment(this.groups[groupKey], this.policies),
                    ordinaryDefault);
                if (!respawn.IsValid)
                {
                    throw new InvalidOperationException(
                        "Ordinary enemy respawn policy failed closed: " + row.SpawnKey);
                }

                RespawnPolicyDefinition resolvedPolicy = respawn.Policy;
                WorldRespawnPolicyValidator.RegisterOrRejectConflict(this.policies, resolvedPolicy);
                var definition = new WorldSpawnDefinition
                {
                    SpawnKey = row.SpawnKey, EnemyProfileKey = row.ProfileKey,
                    ConfiguredIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = row.SourceIdentity },
                    PlayfieldId = row.PlayfieldInstance, X = row.X, Y = row.Y, Z = row.Z,
                    OrientationX = row.HeadingX, OrientationY = row.HeadingY, OrientationZ = row.HeadingZ, OrientationW = row.HeadingW,
                    SpawnGroupKey = groupKey,
                    RespawnPolicyKey = resolvedPolicy.RespawnPolicyKey,
                    ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart,
                    Classification = WorldPopulationClassification.OrdinaryEnemy,
                    Enabled = runtimeEnabled,
                    Quarantined = row.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined
                                  && !runtimeEnabled,
                    Evidence = row.SourceCapture, Confidence = "CAPTURE_BACKED", Source = row.SourceOwnerIdentity
                };
                this.definitions.Add(definition.SpawnKey, definition); this.ordinaryRows.Add(definition.SpawnKey, row);
                this.states.Add(definition.SpawnKey, NewState(definition));
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
            state.SelectedLevel = null;
            int nextGeneration = checked(state.Generation + 1);
            Identity runtimeIdentity;
            OrdinaryEnemySpawnGeneration selectedGeneration;
            if (!this.runtime.SpawnFromPopulation(
                this.playfield,
                this.playfield.Identity,
                this.ordinaryRows[definition.SpawnKey],
                nextGeneration,
                out runtimeIdentity,
                out selectedGeneration))
            {
                state.LifecycleState = PopulationLifecycleState.Failed; state.FailureState = "ordinary-runtime-spawn-failed"; state.LastTransition = DateTime.UtcNow; return;
            }
            WorldPopulationGenerationLifecycle.ApplySpawnSuccess(state, runtimeIdentity, selectedGeneration, DateTime.UtcNow);
            this.spawnKeyByRuntimeIdentity[runtimeIdentity.Instance] = definition.SpawnKey;
            this.Trace(respawn ? "respawn-completed" : "spawn-success", state, null);
        }

        private void ScheduleIfStartMatches(string spawnKey, RespawnDelayStartsAt start, DateTime startedAtUtc)
        {
            RespawnPolicyDefinition policy = this.policies[this.definitions[spawnKey].RespawnPolicyKey];
            PopulationRuntimeState state = this.states[spawnKey];
            if (WorldRespawnScheduler.TryScheduleForLifecycle(this.scheduler, state, policy, start, startedAtUtc, this.respawnRandom)) this.Trace("respawn-scheduled", state, null);
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
