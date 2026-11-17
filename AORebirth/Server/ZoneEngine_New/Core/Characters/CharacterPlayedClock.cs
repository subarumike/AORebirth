namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Data;

    /// <summary>
    /// Played-time counter. A character accumulates time from world entry until it leaves the playfield; the total is
    /// persisted on exit, and the first entry records the creation time fallback for characters created before tracking.
    /// </summary>
    public sealed class CharacterPlayedClock
    {
        sealed class OnlinePlay
        {
            public string Name = string.Empty;
            public long BaseSeconds;
            public DateTime AnchorUtc;

            public long PlayedAt(DateTime nowUtc)
                => BaseSeconds + Math.Max(0, (long)(nowUtc - AnchorUtc).TotalSeconds);
        }

        readonly ICharacterPlayedBornRepository _repository;
        readonly Dictionary<int, OnlinePlay> _online = new();
        readonly object _gate = new();

        public CharacterPlayedClock(ICharacterPlayedBornRepository repository)
            => _repository = repository;

        /// <summary>Total played seconds of a character, live while it is in the world.</summary>
        public long Played(int characterId)
        {
            lock (_gate)
            {
                if (_online.TryGetValue(characterId, out OnlinePlay? entry))
                    return entry.PlayedAt(DateTime.UtcNow);
            }

            return StoredPlayed(characterId);
        }

        /// <summary>Creation time (Unix seconds UTC) of a character, or null when none is recorded.</summary>
        public long? CreatedAtUnix(int characterId)
            => _repository.Load(characterId)?.CreatedAtUnix;

        /// <summary>Starts the counter when a character enters the world.</summary>
        public void Start(int characterId, string characterName)
        {
            if (characterId <= 0)
                return;

            lock (_gate)
            {
                if (_online.ContainsKey(characterId))
                    return;

                PersistedCharacterPlayedBornData? stored = _repository.Load(characterId);
                long baseSeconds = Math.Max(0, stored?.PlayedSeconds ?? 0);
                // Pre-existing characters have no creation time; their first world entry after deployment is recorded.
                _repository.Save(characterId, characterName, baseSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                _online[characterId] = new OnlinePlay
                {
                    Name = characterName,
                    BaseSeconds = baseSeconds,
                    AnchorUtc = DateTime.UtcNow,
                };
            }
        }

        /// <summary>Freezes the counter of a character that left the world and persists it.</summary>
        public void Stop(int characterId)
        {
            lock (_gate)
            {
                if (!_online.Remove(characterId, out OnlinePlay? entry))
                    return;

                _repository.Save(characterId, entry.Name, entry.PlayedAt(DateTime.UtcNow), null);
            }
        }

        long StoredPlayed(int characterId)
            => Math.Max(0, _repository.Load(characterId)?.PlayedSeconds ?? 0);
    }
}
