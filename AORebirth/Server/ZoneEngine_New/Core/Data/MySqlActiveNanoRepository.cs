namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;

    /// <summary>
    /// NCU rows in <c>charactersactivenanos</c>. A flush replaces the character's whole set:
    /// NCU is small and always known in full, so there is no per-row diff to get wrong.
    /// </summary>
    public sealed class MySqlActiveNanoRepository : IActiveNanoRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlActiveNanoRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public IReadOnlyList<ActiveNanoRecord> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Run(() => _persistence.LoadActiveNanos(characterId)
                .Select(v => new ActiveNanoRecord
                {
                    NanoId = v.NanoId,
                    Strain = v.Strain,
                    NanoInstance = v.NanoInstance,
                    DurationCentiseconds = v.DurationCentiseconds,
                    ExpiresAtUtcTicks = v.ExpiresAtUtcTicks
                }).ToArray());
        }

        public void WriteReplaceAll(int characterId, IReadOnlyList<ActiveNanoRecord> nanos)
        {
            ArgumentNullException.ThrowIfNull(nanos);
            if (characterId <= 0)
                return;

            Run(() => _persistence.CommitActiveNanos(
            [
                new CharacterActiveNanoData
                {
                    CharacterId = characterId,
                    BaseStats = [],
                    ActiveNanos = nanos.Select(v => new PersistedActiveNanoData
                    {
                        NanoId = v.NanoId,
                        Strain = v.Strain,
                        NanoInstance = v.NanoInstance,
                        DurationCentiseconds = v.DurationCentiseconds,
                        ExpiresAtUtcTicks = v.ExpiresAtUtcTicks
                    }).ToArray()
                }
            ]));
        }
    }
}
