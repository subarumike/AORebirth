namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal interface IPopulationRandomSource { double NextUnit(); }

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
            if (value == null || string.IsNullOrWhiteSpace(value.SpawnKey) || this.scheduled.ContainsKey(value.SpawnKey)) return false;
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
            if (policy.Mode == WorldRespawnMode.FixedDelay || policy.Mode == WorldRespawnMode.GroupSharedDelay) return TimeSpan.FromSeconds(policy.FixedDelaySeconds.Value);
            if (policy.Mode != WorldRespawnMode.RandomDelayRange) throw new InvalidOperationException("Respawn policy cannot be scheduled: " + policy.Mode);
            double unit = random == null ? 0 : random.NextUnit();
            return TimeSpan.FromSeconds(policy.MinimumDelaySeconds.Value + ((policy.MaximumDelaySeconds.Value - policy.MinimumDelaySeconds.Value) * unit));
        }
    }
}
