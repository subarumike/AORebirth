namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    public readonly struct HateEntry
    {
        public HateEntry(Identity identity, float threat)
        {
            Identity = identity;
            Threat = threat;
        }

        public Identity Identity { get; }

        public float Threat { get; }
    }

    /// <summary>Per-NPC threat table. Keyed by identity so Type+Instance stay unique.</summary>
    public sealed class HateList
    {
        readonly Dictionary<ulong, HateEntry> _entries = new();

        public int Count => _entries.Count;

        public bool IsEmpty => _entries.Count == 0;

        public void Add(Identity identity, float amount)
        {
            if (identity.Instance == 0 || amount <= 0f)
                return;

            ulong key = identity.Long();
            if (_entries.TryGetValue(key, out HateEntry existing))
            {
                _entries[key] = new HateEntry(existing.Identity, existing.Threat + amount);
                return;
            }

            _entries[key] = new HateEntry(identity, amount);
        }

        public bool Contains(Identity identity)
            => identity.Instance != 0 && _entries.ContainsKey(identity.Long());

        public float ThreatOf(Identity identity)
            => identity.Instance != 0 && _entries.TryGetValue(identity.Long(), out HateEntry entry)
                ? entry.Threat
                : 0f;

        public void Remove(Identity identity)
        {
            if (identity.Instance == 0)
                return;
            _entries.Remove(identity.Long());
        }

        public void Clear() => _entries.Clear();

        public IEnumerable<HateEntry> Entries => _entries.Values;
    }

    public static class NpcAiRules
    {
        /// <summary>AO BreedHostility. Messaging enum name for stat 204 is <see cref="CharacterStat.Taunt"/>.</summary>
        public const CharacterStat BreedHostilityStat = CharacterStat.Taunt;

        public const float NearbyRange = 30f;

        public const float MaxLeashRange = 70f;

        public const float ArriveHomeMeters = 1.5f;

        /// <summary>
        /// A complete nav path whose last point is this close in XZ does not count as a chase
        /// chance when the target is still out of LOS or attack range.
        /// </summary>
        public const float PathEndGiveUpMeters = 1.5f;

        /// <summary>
        /// ...and only when the last point is within this height of the NPC. A path ending right above
        /// or below, on another floor, still has a ramp or stairs to walk.
        /// </summary>
        public const float PathEndGiveUpHeightMeters = 2f;

        /// <summary>Stuck-warps in a row without real progress before the NPC evades home and resets.</summary>
        public const int MaxStuckWarps = 2;

        /// <summary>Hold chase this long after the last real chance so a jump over cover does not leash.</summary>
        public const double NoChanceGraceSeconds = 2;

        public const float ProximityHate = 1f;

        public static bool IsWithinNoChanceGrace(DateTime lastChanceUtc, DateTime now)
        {
            if (lastChanceUtc == default)
                return false;
            return (now - lastChanceUtc).TotalSeconds < NoChanceGraceSeconds;
        }

        public static bool IsProximityHostile(int breedHostility) => breedHostility > 0;

        /// <summary>
        /// A new target must be within <see cref="NearbyRange"/>. The target already being chased keeps
        /// out to <see cref="MaxLeashRange"/>: a route around a wall can carry the NPC past
        /// <see cref="NearbyRange"/>, and dropping it there leashes, walks back into range and chases again.
        /// </summary>
        public static float EngageRange(bool isCurrentTarget) => isCurrentTarget ? MaxLeashRange : NearbyRange;

        public static bool IsNearby(Vector3 from, Vector3 to, float range)
            => Vector3.Abs(to - from) <= range;

        public static bool ShouldLeash(
            HateList hate,
            Vector3? home,
            Vector3 npcPosition,
            Func<Identity, bool> isEngageable)
        {
            ArgumentNullException.ThrowIfNull(hate);
            ArgumentNullException.ThrowIfNull(isEngageable);

            if (home is not null && Vector3.Abs(npcPosition - home) > MaxLeashRange)
                return true;

            // Empty list is idle/patrol, not a leash. Leash only after hate exists but nobody is nearby.
            if (hate.IsEmpty)
                return false;

            foreach (HateEntry entry in hate.Entries)
            {
                if (isEngageable(entry.Identity))
                    return false;
            }

            return true;
        }

        public static bool TryHighestNearby(
            HateList hate,
            Func<Identity, bool> isValidNearby,
            out Identity identity,
            out float threat)
        {
            ArgumentNullException.ThrowIfNull(hate);
            ArgumentNullException.ThrowIfNull(isValidNearby);

            identity = Identity.None;
            threat = 0f;
            bool found = false;

            foreach (HateEntry entry in hate.Entries)
            {
                if (!isValidNearby(entry.Identity))
                    continue;
                if (found && entry.Threat <= threat)
                    continue;

                identity = entry.Identity;
                threat = entry.Threat;
                found = true;
            }

            return found;
        }
    }
}
