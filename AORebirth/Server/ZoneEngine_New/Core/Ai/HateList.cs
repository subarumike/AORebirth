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

        public const float ProximityHate = 1f;

        public static bool IsProximityHostile(int breedHostility) => breedHostility > 0;

        public static bool IsNearby(Vector3 from, Vector3 to, float range)
            => Vector3.Abs(to - from) <= range;

        public static bool ShouldLeash(
            HateList hate,
            Vector3 home,
            Vector3 npcPosition,
            Func<Identity, bool> isValidNearby)
        {
            ArgumentNullException.ThrowIfNull(hate);
            ArgumentNullException.ThrowIfNull(isValidNearby);

            if (Vector3.Abs(npcPosition - home) > MaxLeashRange)
                return true;

            // Empty list is idle/patrol, not a leash. Leash only after hate exists but nobody is nearby.
            if (hate.IsEmpty)
                return false;

            foreach (HateEntry entry in hate.Entries)
            {
                if (isValidNearby(entry.Identity))
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
