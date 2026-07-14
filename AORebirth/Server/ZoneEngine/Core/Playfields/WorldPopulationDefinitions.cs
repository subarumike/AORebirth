namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal enum WorldSpawnActivationPolicy { Disabled, PlayfieldStart, InstanceStart, OnDemand, EventControlled, QuestControlled, Scripted }
    internal enum WorldRespawnMode { None, FixedDelay, RandomDelayRange, GroupSharedDelay, Scripted, Unresolved }
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
        internal DateTime LastTransition { get; set; }
        internal string FailureState { get; set; }
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
                if (string.IsNullOrWhiteSpace(policy.RespawnPolicyKey)
                    || (policy.Mode == WorldRespawnMode.FixedDelay && (!policy.FixedDelaySeconds.HasValue || policy.FixedDelaySeconds.Value <= 0))
                    || (policy.Mode == WorldRespawnMode.RandomDelayRange && (!policy.MinimumDelaySeconds.HasValue || !policy.MaximumDelaySeconds.HasValue || policy.MinimumDelaySeconds.Value <= 0 || policy.MaximumDelaySeconds.Value < policy.MinimumDelaySeconds.Value)))
                    throw new InvalidOperationException("Invalid respawn policy: " + policy.RespawnPolicyKey);
            }
            foreach (WorldSpawnDefinition spawn in spawnRows)
            {
                if (string.IsNullOrWhiteSpace(spawn.SpawnKey) || !profiles.Contains(spawn.EnemyProfileKey) || !policyKeys.Contains(spawn.RespawnPolicyKey)
                    || spawn.PlayfieldId <= 0 || spawn.ConfiguredIdentity.Instance <= 0 || !Finite(spawn.X) || !Finite(spawn.Y) || !Finite(spawn.Z)
                    || !Finite(spawn.OrientationX) || !Finite(spawn.OrientationY) || !Finite(spawn.OrientationZ) || !Finite(spawn.OrientationW)
                    || spawn.OwnedSummon || spawn.BossOrScripted || (!spawn.Enabled && !spawn.Quarantined && spawn.ActivationPolicy == WorldSpawnActivationPolicy.PlayfieldStart))
                    throw new InvalidOperationException("Invalid world spawn definition: " + spawn.SpawnKey);
            }
            foreach (SpawnGroupDefinition group in groupRows)
            {
                if (string.IsNullOrWhiteSpace(group.SpawnGroupKey) || group.PlayfieldId <= 0 || group.MinimumAlive < 0 || group.MaximumAlive < group.MinimumAlive
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
