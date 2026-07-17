namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal interface IPopulationRandomSource { double NextUnit(); }

    internal sealed class SystemPopulationRandomSource : IPopulationRandomSource
    {
        private readonly Random random = new Random();
        public double NextUnit() { return this.random.NextDouble(); }
    }

    internal sealed class WorldRespawnSchedule
    {
        internal string SpawnKey { get; set; }
        internal string GroupKey { get; set; }
        internal int PlayfieldId { get; set; }
        internal DateTime DueAtUtc { get; set; }
        internal int Generation { get; set; }
    }

    internal sealed class WorldRespawnScheduler
    {
        private readonly Dictionary<string, WorldRespawnSchedule> scheduled = new Dictionary<string, WorldRespawnSchedule>(StringComparer.Ordinal);
        internal bool Schedule(WorldRespawnSchedule value)
        {
            if (value == null
                || string.IsNullOrWhiteSpace(value.SpawnKey)
                || value.Generation <= 0
                || value.DueAtUtc == default(DateTime)
                || this.scheduled.ContainsKey(value.SpawnKey)) return false;
            this.scheduled.Add(value.SpawnKey, value); return true;
        }
        internal bool Cancel(string spawnKey) { return this.scheduled.Remove(spawnKey); }
        internal void CancelPlayfield(int playfieldId)
        {
            foreach (string key in this.scheduled.Where(x => x.Value.PlayfieldId == playfieldId).Select(x => x.Key).ToArray()) this.scheduled.Remove(key);
        }
        internal WorldRespawnSchedule[] TakeDue(DateTime utcNow, int maximumWork)
        {
            WorldRespawnSchedule[] due = this.scheduled.Values.Where(x => x.DueAtUtc <= utcNow)
                .OrderBy(x => x.DueAtUtc).ThenBy(x => x.PlayfieldId).ThenBy(x => x.SpawnKey, StringComparer.Ordinal)
                .Take(Math.Max(0, maximumWork)).ToArray();
            foreach (WorldRespawnSchedule value in due) this.scheduled.Remove(value.SpawnKey);
            return due;
        }
        internal bool Contains(string spawnKey) { return this.scheduled.ContainsKey(spawnKey); }
        internal int Count { get { return this.scheduled.Count; } }
        internal static TimeSpan SelectDelay(RespawnPolicyDefinition policy, IPopulationRandomSource random)
        {
            if (!WorldRespawnPolicyValidator.IsSchedulable(policy))
                throw new InvalidOperationException("Respawn policy cannot be scheduled: " + (policy == null ? "null" : policy.Mode.ToString()));
            if (policy.Mode == WorldRespawnMode.FixedDelay) return TimeSpan.FromSeconds(policy.FixedDelaySeconds.Value);
            if (random == null) throw new InvalidOperationException("Random respawn policy requires an explicit random source.");
            double unit = random.NextUnit();
            if (double.IsNaN(unit) || double.IsInfinity(unit) || unit < 0.0 || unit > 1.0)
                throw new InvalidOperationException("Population random source must return a finite value in the inclusive range 0..1.");
            return TimeSpan.FromSeconds(policy.MinimumDelaySeconds.Value + ((policy.MaximumDelaySeconds.Value - policy.MinimumDelaySeconds.Value) * unit));
        }

        internal static bool TryScheduleForLifecycle(
            WorldRespawnScheduler scheduler,
            PopulationRuntimeState state,
            RespawnPolicyDefinition policy,
            RespawnDelayStartsAt start,
            DateTime startedAtUtc,
            IPopulationRandomSource random = null)
        {
            if (scheduler == null || state == null || state.Generation <= 0
                || !WorldRespawnPolicyValidator.IsSchedulable(policy)
                || (policy.Mode == WorldRespawnMode.RandomDelayRange && random == null)
                || policy.DelayStartsAt != start) return false;
            DateTime due;
            try
            {
                due = startedAtUtc.Add(SelectDelay(policy, random));
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            if (!scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = state.SpawnKey, GroupKey = state.SpawnGroupKey, PlayfieldId = state.PlayfieldId, DueAtUtc = due, Generation = state.Generation })) return false;
            state.RespawnDueAt = due; state.LifecycleState = PopulationLifecycleState.WaitingForRespawn; state.LastTransition = startedAtUtc;
            return true;
        }

        internal static bool IsCurrentGeneration(PopulationRuntimeState state, WorldRespawnSchedule due)
        {
            return state != null && due != null && state.Generation == due.Generation && state.CurrentRuntimeIdentity.Instance == 0;
        }

        internal static bool TryResumePendingAfterRuntimeRelease(
            WorldRespawnScheduler scheduler,
            PopulationRuntimeState state,
            RespawnPolicyDefinition policy,
            DateTime releasedAtUtc)
        {
            if (scheduler == null || state == null || state.CurrentRuntimeIdentity.Instance != 0
                || !state.RespawnDueAt.HasValue || scheduler.Contains(state.SpawnKey)
                || !WorldRespawnPolicyValidator.IsSchedulable(policy)) return false;
            DateTime due = state.RespawnDueAt.Value > releasedAtUtc ? state.RespawnDueAt.Value : releasedAtUtc;
            if (!scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = state.SpawnKey, GroupKey = state.SpawnGroupKey, PlayfieldId = state.PlayfieldId, DueAtUtc = due, Generation = state.Generation })) return false;
            state.RespawnDueAt = due;
            state.LifecycleState = PopulationLifecycleState.WaitingForRespawn;
            state.LastTransition = releasedAtUtc;
            return true;
        }
    }
}
