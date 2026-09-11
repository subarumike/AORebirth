namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    using ZoneEngine_New.Core.Nanos;
    public sealed class MySqlActiveNanoRepository : IActiveNanoRepository
    {
        readonly ICharacterPersistenceDao _persistence;
        public MySqlActiveNanoRepository(ICharacterPersistenceDao? persistence = null) => _persistence = persistence ?? Create();
        public IReadOnlyList<ActiveNanoRecord> Load(int characterId) => Run(() => _persistence.LoadActiveNanos(characterId)
            .Select(v => new ActiveNanoRecord(v.NanoId, v.Strain, v.NanoInstance, v.DurationCentiseconds, v.ExpiresAtUtcTicks)).ToArray());
        public void Commit(IReadOnlyList<NanoCharacterWrite> characters) => Run(() => _persistence.CommitActiveNanos(characters.Select(c => new CharacterActiveNanoData
        {
            CharacterId = c.CharacterId, BaseStats = c.BaseStats.Select(Map).ToArray(),
            ActiveNanos = c.ActiveNanos.Select(v => new PersistedActiveNanoData { NanoId = v.NanoId, Strain = v.Strain, NanoInstance = v.NanoInstance,
                DurationCentiseconds = v.DurationCentiseconds, ExpiresAtUtcTicks = v.ExpiresAtUtcTicks }).ToArray()
        }).ToArray()));
    }
}
