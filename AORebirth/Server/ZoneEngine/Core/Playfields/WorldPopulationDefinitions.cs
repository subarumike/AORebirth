namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal enum WorldSpawnActivationPolicy { Disabled, PlayfieldStart, InstanceStart, OnDemand, EventControlled, QuestControlled, Scripted }
    internal enum WorldPopulationClassification { Unsupported, OrdinaryEnemy, NamedEnemy, Boss, ScriptedEncounter, Summon, Pet, TemporaryEncounterAdd, Vendor, StaticObject, Container, QuestOwned }
    internal enum WorldRespawnMode { None, FixedDelay, RandomDelayRange, GroupSharedDelay, Scripted, Unresolved }
    internal enum WorldRespawnPolicyAssignmentMode { Invalid, Inherit, Explicit, NoRespawn, Unresolved }
    internal enum WorldRespawnPolicyResolutionSource { Unresolved, ExplicitSpawnOrArchetype, ExplicitGroup, OrdinaryDefault, ExplicitNoRespawn, ExcludedClassification }
    internal enum RespawnDelayStartsAt { Death, CorpseCreation, CorpseRemoval, NpcDespawn, Scripted, Unresolved }
    internal enum PopulationLifecycleState { Disabled, Ready, Spawning, Alive, DeadCorpseActive, WaitingForRespawn, Respawning, Despawned, Quarantined, Failed }

    internal sealed class WorldSpawnDefinition
    {
        internal string SpawnKey { get; set; }
        internal string EnemyProfileKey { get; set; }
        internal Identity ConfiguredIdentity { get; set; }
        internal int PlayfieldId { get; set; }
        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
        internal float OrientationX { get; set; }
        internal float OrientationY { get; set; }
        internal float OrientationZ { get; set; }
        internal float OrientationW { get; set; }
        internal int? ScaleOverride { get; set; }
        internal int? LevelOverride { get; set; }
        internal int? HealthOverride { get; set; }
        internal string MovementProfileOverride { get; set; }
        internal string AggressionProfileOverride { get; set; }
        internal string CombatProfileOverride { get; set; }
        internal string LootAssignmentOverride { get; set; }
        internal string SpawnGroupKey { get; set; }
        internal string RespawnPolicyKey { get; set; }
        internal string ZoneKey { get; set; }
        internal string SubzoneKey { get; set; }
        internal string CampKey { get; set; }
        internal WorldSpawnActivationPolicy ActivationPolicy { get; set; }
        internal bool Enabled { get; set; }
        internal bool Quarantined { get; set; }
        internal bool BossOrScripted { get; set; }
        internal bool OwnedSummon { get; set; }
        internal string Evidence { get; set; }
        internal string Confidence { get; set; }
        internal string Source { get; set; }
        internal WorldPopulationClassification Classification { get; set; }
    }

    internal sealed class SpawnGroupDefinition
    {
        internal string SpawnGroupKey { get; set; }
        internal string DisplayName { get; set; }
        internal int PlayfieldId { get; set; }
        internal string ZoneKey { get; set; }
        internal string CampKey { get; set; }
        internal string[] SpawnKeys { get; set; }
        internal WorldSpawnActivationPolicy ActivationPolicy { get; set; }
        internal int MaximumAlive { get; set; }
        internal int MinimumAlive { get; set; }
        internal string SharedRespawnPolicyKey { get; set; }
        internal string ResetPolicy { get; set; }
        internal bool Enabled { get; set; }
        internal string Evidence { get; set; }
        internal string Confidence { get; set; }
    }

    internal sealed class RespawnPolicyDefinition
    {
        internal string RespawnPolicyKey { get; set; }
        internal WorldRespawnMode Mode { get; set; }
        internal double? FixedDelaySeconds { get; set; }
        internal double? MinimumDelaySeconds { get; set; }
        internal double? MaximumDelaySeconds { get; set; }
        internal bool SharedGroupTimer { get; set; }
        internal bool RespawnAtOriginalPosition { get; set; }
        internal bool ResetHealth { get; set; }
        internal bool ResetMovementState { get; set; }
        internal bool ResetAggressionState { get; set; }
        internal RespawnDelayStartsAt DelayStartsAt { get; set; }
        internal string Evidence { get; set; }
        internal string Confidence { get; set; }
        internal bool Enabled { get; set; }
    }

    internal sealed class WorldRespawnPolicyAssignment
    {
        internal WorldRespawnPolicyAssignment(
            WorldRespawnPolicyAssignmentMode mode,
            RespawnPolicyDefinition explicitPolicy,
            string policyKey,
            string evidence,
            string confidence)
        {
            this.Mode = mode;
            this.ExplicitPolicy = explicitPolicy;
            this.PolicyKey = policyKey ?? string.Empty;
            this.Evidence = evidence ?? string.Empty;
            this.Confidence = confidence ?? string.Empty;
        }

        internal WorldRespawnPolicyAssignmentMode Mode { get; private set; }
        internal RespawnPolicyDefinition ExplicitPolicy { get; private set; }
        internal string PolicyKey { get; private set; }
        internal string Evidence { get; private set; }
        internal string Confidence { get; private set; }

        internal static WorldRespawnPolicyAssignment Inherit(string evidence)
        {
            return new WorldRespawnPolicyAssignment(
                WorldRespawnPolicyAssignmentMode.Inherit,
                null,
                null,
                evidence,
                "POLICY");
        }

        internal static WorldRespawnPolicyAssignment Explicit(RespawnPolicyDefinition policy)
        {
            return new WorldRespawnPolicyAssignment(
                WorldRespawnPolicyAssignmentMode.Explicit,
                policy,
                policy == null ? null : policy.RespawnPolicyKey,
                policy == null ? string.Empty : policy.Evidence,
                policy == null ? string.Empty : policy.Confidence);
        }

        internal static WorldRespawnPolicyAssignment NoRespawn(
            string policyKey,
            string evidence,
            string confidence)
        {
            return new WorldRespawnPolicyAssignment(
                WorldRespawnPolicyAssignmentMode.NoRespawn,
                null,
                policyKey,
                evidence,
                confidence);
        }

        internal static WorldRespawnPolicyAssignment Unresolved(string evidence)
        {
            return new WorldRespawnPolicyAssignment(
                WorldRespawnPolicyAssignmentMode.Unresolved,
                null,
                null,
                evidence,
                "UNRESOLVED");
        }
    }

    internal sealed class WorldRespawnPolicyResolution
    {
        internal WorldRespawnPolicyResolution(
            RespawnPolicyDefinition policy,
            WorldRespawnPolicyResolutionSource source)
        {
            this.Policy = policy;
            this.Source = source;
        }

        internal RespawnPolicyDefinition Policy { get; private set; }
        internal WorldRespawnPolicyResolutionSource Source { get; private set; }

        internal bool IsValid
        {
            get { return WorldRespawnPolicyValidator.IsValid(this.Policy); }
        }
    }

    internal static class WorldRespawnPolicyResolver
    {
        internal static void ApplyGroupConfiguration(
            SpawnGroupDefinition group,
            IDictionary<string, RespawnPolicyDefinition> configuredByGroupKey,
            IDictionary<string, RespawnPolicyDefinition> availablePolicies)
        {
            if (group == null) throw new ArgumentNullException("group");
            RespawnPolicyDefinition configured;
            if (configuredByGroupKey == null
                || !configuredByGroupKey.TryGetValue(group.SpawnGroupKey, out configured)) return;
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(availablePolicies, configured);
            group.SharedRespawnPolicyKey = configured.RespawnPolicyKey;
        }

        internal static WorldRespawnPolicyAssignment ResolveGroupAssignment(
            SpawnGroupDefinition group,
            IDictionary<string, RespawnPolicyDefinition> availablePolicies)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.SharedRespawnPolicyKey)) return null;
            RespawnPolicyDefinition policy;
            if (availablePolicies == null || !availablePolicies.TryGetValue(group.SharedRespawnPolicyKey, out policy))
                return WorldRespawnPolicyAssignment.Unresolved("missing-group-policy:" + group.SharedRespawnPolicyKey);
            return WorldRespawnPolicyAssignment.Explicit(policy);
        }

        internal static WorldRespawnPolicyResolution Resolve(
            WorldPopulationClassification classification,
            WorldRespawnPolicyAssignment spawnOrArchetype,
            WorldRespawnPolicyAssignment group,
            RespawnPolicyDefinition ordinaryDefault)
        {
            if (!Enum.IsDefined(typeof(WorldPopulationClassification), classification)
                || classification == WorldPopulationClassification.Unsupported)
            {
                return Unresolved("unsupported-classification");
            }

            WorldRespawnPolicyResolution direct = ResolveAssignment(
                spawnOrArchetype,
                WorldRespawnPolicyResolutionSource.ExplicitSpawnOrArchetype);
            if (direct != null)
            {
                return direct;
            }

            WorldRespawnPolicyResolution grouped = ResolveAssignment(
                group,
                WorldRespawnPolicyResolutionSource.ExplicitGroup);
            if (grouped != null)
            {
                return grouped;
            }

            if (classification == WorldPopulationClassification.OrdinaryEnemy)
            {
                return WorldRespawnPolicyValidator.IsSchedulable(ordinaryDefault)
                    ? new WorldRespawnPolicyResolution(
                        ordinaryDefault,
                        WorldRespawnPolicyResolutionSource.OrdinaryDefault)
                    : Unresolved("invalid-ordinary-default");
            }

            if (IsExcludedFromOrdinaryDefault(classification))
            {
                return NoRespawn(
                    "excluded." + classification,
                    "classification-excluded-from-ordinary-default",
                    "POLICY",
                    WorldRespawnPolicyResolutionSource.ExcludedClassification);
            }

            return Unresolved("invalid-classification");
        }

        internal static bool IsExcludedFromOrdinaryDefault(WorldPopulationClassification classification)
        {
            return classification == WorldPopulationClassification.NamedEnemy
                   || classification == WorldPopulationClassification.Boss
                   || classification == WorldPopulationClassification.ScriptedEncounter
                   || classification == WorldPopulationClassification.Summon
                   || classification == WorldPopulationClassification.Pet
                   || classification == WorldPopulationClassification.TemporaryEncounterAdd
                   || classification == WorldPopulationClassification.Vendor
                   || classification == WorldPopulationClassification.StaticObject
                   || classification == WorldPopulationClassification.Container
                   || classification == WorldPopulationClassification.QuestOwned;
        }

        private static WorldRespawnPolicyResolution ResolveAssignment(
            WorldRespawnPolicyAssignment assignment,
            WorldRespawnPolicyResolutionSource explicitSource)
        {
            if (assignment == null || assignment.Mode == WorldRespawnPolicyAssignmentMode.Inherit)
            {
                return null;
            }

            if (assignment.Mode == WorldRespawnPolicyAssignmentMode.NoRespawn)
            {
                if (string.IsNullOrWhiteSpace(assignment.PolicyKey))
                    return Unresolved("missing-no-respawn-policy-key");
                return NoRespawn(
                    assignment.PolicyKey,
                    assignment.Evidence,
                    assignment.Confidence,
                    WorldRespawnPolicyResolutionSource.ExplicitNoRespawn);
            }

            if (assignment.Mode == WorldRespawnPolicyAssignmentMode.Explicit
                && (WorldRespawnPolicyValidator.IsSchedulable(assignment.ExplicitPolicy)
                    || (WorldRespawnPolicyValidator.IsValid(assignment.ExplicitPolicy)
                        && assignment.ExplicitPolicy.Enabled
                        && assignment.ExplicitPolicy.Mode == WorldRespawnMode.None)))
            {
                return new WorldRespawnPolicyResolution(assignment.ExplicitPolicy, explicitSource);
            }

            return Unresolved(
                assignment.Mode == WorldRespawnPolicyAssignmentMode.Unresolved
                    ? assignment.Evidence
                    : "invalid-policy-assignment");
        }

        private static WorldRespawnPolicyResolution NoRespawn(
            string key,
            string evidence,
            string confidence,
            WorldRespawnPolicyResolutionSource source)
        {
            return new WorldRespawnPolicyResolution(
                new RespawnPolicyDefinition
                {
                    RespawnPolicyKey = key,
                    Mode = WorldRespawnMode.None,
                    DelayStartsAt = RespawnDelayStartsAt.Unresolved,
                    Evidence = evidence,
                    Confidence = confidence,
                    Enabled = true
                },
                source);
        }

        private static WorldRespawnPolicyResolution Unresolved(string evidence)
        {
            return new WorldRespawnPolicyResolution(
                new RespawnPolicyDefinition
                {
                    RespawnPolicyKey = "respawn.unresolved",
                    Mode = WorldRespawnMode.Unresolved,
                    DelayStartsAt = RespawnDelayStartsAt.Unresolved,
                    Evidence = evidence,
                    Confidence = "UNRESOLVED",
                    Enabled = false
                },
                WorldRespawnPolicyResolutionSource.Unresolved);
        }
    }

    internal static class WorldRespawnPolicyValidator
    {
        internal static bool IsValid(RespawnPolicyDefinition policy)
        {
            if (policy == null || string.IsNullOrWhiteSpace(policy.RespawnPolicyKey))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(WorldRespawnMode), policy.Mode)
                || !Enum.IsDefined(typeof(RespawnDelayStartsAt), policy.DelayStartsAt))
            {
                return false;
            }

            if (policy.SharedGroupTimer || policy.Mode == WorldRespawnMode.GroupSharedDelay)
            {
                return false;
            }

            if (policy.Mode == WorldRespawnMode.None)
            {
                return !policy.FixedDelaySeconds.HasValue
                       && !policy.MinimumDelaySeconds.HasValue
                       && !policy.MaximumDelaySeconds.HasValue;
            }

            if (policy.Mode == WorldRespawnMode.Scripted)
            {
                return policy.DelayStartsAt == RespawnDelayStartsAt.Scripted
                       && !policy.FixedDelaySeconds.HasValue
                       && !policy.MinimumDelaySeconds.HasValue
                       && !policy.MaximumDelaySeconds.HasValue;
            }

            if (policy.DelayStartsAt == RespawnDelayStartsAt.Scripted
                || policy.DelayStartsAt == RespawnDelayStartsAt.Unresolved
                || policy.DelayStartsAt == RespawnDelayStartsAt.CorpseCreation)
            {
                return false;
            }

            if (policy.Mode == WorldRespawnMode.FixedDelay)
            {
                return IsUsableDelay(policy.FixedDelaySeconds)
                       && !policy.MinimumDelaySeconds.HasValue
                       && !policy.MaximumDelaySeconds.HasValue;
            }

            if (policy.Mode == WorldRespawnMode.RandomDelayRange)
            {
                return !policy.FixedDelaySeconds.HasValue
                       && IsUsableDelay(policy.MinimumDelaySeconds)
                       && IsUsableDelay(policy.MaximumDelaySeconds)
                       && policy.MaximumDelaySeconds.Value >= policy.MinimumDelaySeconds.Value;
            }

            return false;
        }

        internal static bool IsSchedulable(RespawnPolicyDefinition policy)
        {
            return IsValid(policy)
                   && policy.Enabled
                   && (policy.Mode == WorldRespawnMode.FixedDelay
                       || policy.Mode == WorldRespawnMode.RandomDelayRange);
        }

        internal static bool AreEquivalent(RespawnPolicyDefinition left, RespawnPolicyDefinition right)
        {
            return left != null && right != null
                   && string.Equals(left.RespawnPolicyKey, right.RespawnPolicyKey, StringComparison.Ordinal)
                   && left.Mode == right.Mode
                   && left.FixedDelaySeconds == right.FixedDelaySeconds
                   && left.MinimumDelaySeconds == right.MinimumDelaySeconds
                   && left.MaximumDelaySeconds == right.MaximumDelaySeconds
                   && left.SharedGroupTimer == right.SharedGroupTimer
                   && left.RespawnAtOriginalPosition == right.RespawnAtOriginalPosition
                   && left.ResetHealth == right.ResetHealth
                   && left.ResetMovementState == right.ResetMovementState
                   && left.ResetAggressionState == right.ResetAggressionState
                   && left.DelayStartsAt == right.DelayStartsAt
                   && string.Equals(left.Evidence, right.Evidence, StringComparison.Ordinal)
                   && string.Equals(left.Confidence, right.Confidence, StringComparison.Ordinal)
                   && left.Enabled == right.Enabled;
        }

        internal static void RegisterOrRejectConflict(
            IDictionary<string, RespawnPolicyDefinition> availablePolicies,
            RespawnPolicyDefinition policy)
        {
            if (availablePolicies == null || !IsValid(policy))
                throw new InvalidOperationException("Invalid respawn policy registration.");
            RespawnPolicyDefinition existing;
            if (!availablePolicies.TryGetValue(policy.RespawnPolicyKey, out existing))
            {
                availablePolicies.Add(policy.RespawnPolicyKey, policy);
                return;
            }
            if (!AreEquivalent(existing, policy))
                throw new InvalidOperationException("Conflicting respawn policy key: " + policy.RespawnPolicyKey);
        }

        private static bool IsUsableDelay(double? seconds)
        {
            if (!seconds.HasValue || seconds.Value <= 0.0 || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value)) return false;
            try
            {
                TimeSpan.FromSeconds(seconds.Value);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    internal sealed class PopulationRuntimeState
    {
        internal string SpawnKey { get; set; }
        internal string SpawnGroupKey { get; set; }
        internal string EnemyProfileKey { get; set; }
        internal Identity ConfiguredIdentity { get; set; }
        internal Identity CurrentRuntimeIdentity { get; set; }
        internal int PlayfieldId { get; set; }
        internal PopulationLifecycleState LifecycleState { get; set; }
        internal DateTime? SpawnedAt { get; set; }
        internal DateTime? DiedAt { get; set; }
        internal Identity CorpseIdentity { get; set; }
        internal DateTime? RespawnDueAt { get; set; }
        internal int Generation { get; set; }
        internal int? SelectedLevel { get; set; }
        internal DateTime LastTransition { get; set; }
        internal string FailureState { get; set; }
    }

    internal static class WorldPopulationGenerationLifecycle
    {
        internal static void ApplySpawnSuccess(
            PopulationRuntimeState state,
            Identity runtimeIdentity,
            OrdinaryEnemySpawnGeneration generation,
            DateTime spawnedAtUtc)
        {
            if (state == null || runtimeIdentity.Instance <= 0 || generation == null
                || state.Generation == int.MaxValue
                || generation.Number != state.Generation + 1)
                throw new InvalidOperationException("Spawn success must advance exactly one population generation.");
            state.CurrentRuntimeIdentity = runtimeIdentity;
            state.Generation = generation.Number;
            state.SelectedLevel = generation.SelectedVariant.Level;
            state.SpawnedAt = spawnedAtUtc;
            state.DiedAt = null;
            state.CorpseIdentity = Identity.None;
            state.RespawnDueAt = null;
            state.LifecycleState = PopulationLifecycleState.Alive;
            state.LastTransition = spawnedAtUtc;
            state.FailureState = null;
        }

        internal static void ClearRuntime(PopulationRuntimeState state, DateTime clearedAtUtc)
        {
            if (state == null) throw new ArgumentNullException("state");
            state.CurrentRuntimeIdentity = Identity.None;
            state.SelectedLevel = null;
            state.SpawnedAt = null;
            state.DiedAt = null;
            state.CorpseIdentity = Identity.None;
            state.RespawnDueAt = null;
            state.LifecycleState = PopulationLifecycleState.Despawned;
            state.LastTransition = clearedAtUtc;
            state.FailureState = null;
        }
    }

    internal static class WorldPopulationDefinitionValidator
    {
        internal static void Validate(
            IEnumerable<WorldSpawnDefinition> spawns,
            IEnumerable<SpawnGroupDefinition> groups,
            IEnumerable<RespawnPolicyDefinition> policies,
            IEnumerable<string> profileKeys)
        {
            WorldSpawnDefinition[] spawnRows = (spawns ?? Enumerable.Empty<WorldSpawnDefinition>()).OrderBy(x => x.SpawnKey, StringComparer.Ordinal).ToArray();
            SpawnGroupDefinition[] groupRows = (groups ?? Enumerable.Empty<SpawnGroupDefinition>()).OrderBy(x => x.SpawnGroupKey, StringComparer.Ordinal).ToArray();
            RespawnPolicyDefinition[] policyRows = (policies ?? Enumerable.Empty<RespawnPolicyDefinition>()).OrderBy(x => x.RespawnPolicyKey, StringComparer.Ordinal).ToArray();
            var profiles = new HashSet<string>(profileKeys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            RejectDuplicates(spawnRows.Select(x => x.SpawnKey), "spawn key");
            RejectDuplicates(groupRows.Select(x => x.SpawnGroupKey), "group key");
            RejectDuplicates(policyRows.Select(x => x.RespawnPolicyKey), "respawn policy key");
            RejectDuplicates(spawnRows.Select(x => x.ConfiguredIdentity.Instance.ToString()), "configured identity");
            var policyKeys = new HashSet<string>(policyRows.Select(x => x.RespawnPolicyKey), StringComparer.Ordinal);
            var spawnKeys = new HashSet<string>(spawnRows.Select(x => x.SpawnKey), StringComparer.Ordinal);
            foreach (RespawnPolicyDefinition policy in policyRows)
            {
                if (!WorldRespawnPolicyValidator.IsValid(policy))
                    throw new InvalidOperationException("Invalid respawn policy: " + policy.RespawnPolicyKey);
            }
            foreach (WorldSpawnDefinition spawn in spawnRows)
            {
                if (string.IsNullOrWhiteSpace(spawn.SpawnKey) || !profiles.Contains(spawn.EnemyProfileKey) || !policyKeys.Contains(spawn.RespawnPolicyKey)
                    || spawn.PlayfieldId <= 0 || spawn.ConfiguredIdentity.Instance <= 0 || !Finite(spawn.X) || !Finite(spawn.Y) || !Finite(spawn.Z)
                    || !Finite(spawn.OrientationX) || !Finite(spawn.OrientationY) || !Finite(spawn.OrientationZ) || !Finite(spawn.OrientationW)
                    || !Enum.IsDefined(typeof(WorldPopulationClassification), spawn.Classification)
                    || spawn.Classification == WorldPopulationClassification.Unsupported
                    || spawn.OwnedSummon || spawn.BossOrScripted || (!spawn.Enabled && !spawn.Quarantined && spawn.ActivationPolicy == WorldSpawnActivationPolicy.PlayfieldStart))
                    throw new InvalidOperationException("Invalid world spawn definition: " + spawn.SpawnKey);
            }
            foreach (SpawnGroupDefinition group in groupRows)
            {
                if (string.IsNullOrWhiteSpace(group.SpawnGroupKey) || group.PlayfieldId <= 0 || group.MinimumAlive < 0 || group.MaximumAlive < group.MinimumAlive
                    || (!string.IsNullOrWhiteSpace(group.SharedRespawnPolicyKey) && !policyKeys.Contains(group.SharedRespawnPolicyKey))
                    || (group.SpawnKeys ?? new string[0]).Any(x => !spawnKeys.Contains(x)))
                    throw new InvalidOperationException("Invalid spawn group: " + group.SpawnGroupKey);
            }
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void RejectDuplicates(IEnumerable<string> values, string label)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) throw new InvalidOperationException("Duplicate or missing " + label + ": " + value);
        }
    }

    internal interface IPopulationStateStore
    {
        void Save(PopulationRuntimeState state);
        PopulationRuntimeState Load(string spawnKey);
    }
}
