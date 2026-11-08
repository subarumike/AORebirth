namespace ZoneEngine_New.Core.Organizations
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Data;

    /// <summary>
    /// Organization lockout countdown. Online characters decay their lock in real time;
    /// offline locks stay frozen in <c>org_lockout</c> until the next login.
    /// </summary>
    public sealed class OrgLockoutClock
    {
        sealed class OnlineLockout
        {
            public string Name = string.Empty;
            public long PersistedSeconds;
            public long BaseSeconds;
            public DateTime AnchorUtc;

            public long RemainingAt(DateTime nowUtc)
                => Math.Max(0, BaseSeconds - (long)(nowUtc - AnchorUtc).TotalSeconds);
        }

        readonly IOrganizationRepository _repository;
        readonly Dictionary<int, OnlineLockout> _online = new();
        readonly object _gate = new();

        public OrgLockoutClock(IOrganizationRepository repository)
            => _repository = repository;

        public long Remaining(int characterId)
        {
            lock (_gate)
            {
                if (_online.TryGetValue(characterId, out OnlineLockout? entry))
                    return entry.RemainingAt(DateTime.UtcNow);
            }

            return StoredSeconds(characterId);
        }

        /// <summary>Adds seconds to the remaining lock (cumulative) and returns the new total.</summary>
        public long Add(int characterId, string characterName, long seconds)
        {
            lock (_gate)
            {
                if (_online.TryGetValue(characterId, out OnlineLockout? entry))
                {
                    DateTime now = DateTime.UtcNow;
                    long total = entry.RemainingAt(now) + seconds;
                    entry.BaseSeconds = total;
                    entry.AnchorUtc = now;
                    _repository.SaveLockout(characterId, characterName, total);
                    entry.PersistedSeconds = total;
                    return total;
                }

                long offlineTotal = StoredSeconds(characterId) + seconds;
                _repository.SaveLockout(characterId, characterName, offlineTotal);
                return offlineTotal;
            }
        }

        /// <summary>Starts the countdown of a character that entered the world.</summary>
        public void Start(int characterId, string characterName)
        {
            lock (_gate)
            {
                if (_online.ContainsKey(characterId))
                    return;

                long stored = StoredSeconds(characterId);
                _online[characterId] = new OnlineLockout
                {
                    Name = characterName,
                    PersistedSeconds = stored,
                    BaseSeconds = stored,
                    AnchorUtc = DateTime.UtcNow,
                };
            }
        }

        /// <summary>Freezes the countdown of a character that left the world and persists it.</summary>
        public void Stop(int characterId)
        {
            lock (_gate)
            {
                if (!_online.Remove(characterId, out OnlineLockout? entry))
                    return;

                long remaining = entry.RemainingAt(DateTime.UtcNow);
                if (remaining > 0 || entry.PersistedSeconds > 0)
                    _repository.SaveLockout(characterId, entry.Name, remaining);
            }
        }

        long StoredSeconds(int characterId)
        {
            PersistedOrgLockoutData? stored = _repository.GetLockout(characterId);
            return Math.Max(0, stored?.SecondsRemaining ?? 0);
        }
    }
}
