namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;

    public sealed class MySqlCharacterCoalesceCommit : ICharacterCoalesceCommit
    {
        readonly ICharacterPersistenceDao _persistence;
        readonly IZoneLogger _logger;

        public MySqlCharacterCoalesceCommit(
            MySqlInventoryRepository inventory,
            MySqlUploadedNanoRepository nanos,
            MySqlActiveNanoRepository activeNanos,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(nanos);
            ArgumentNullException.ThrowIfNull(activeNanos);
            _persistence = (inventory ?? throw new ArgumentNullException(nameof(inventory))).Persistence;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Persist(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates,
            int characterId,
            IReadOnlyList<int> uploadedNanoIds,
            IReadOnlyList<ActiveNanoRecord>? activeNanos,
            IReadOnlyList<SkillLockRecord>? skillLocks)
            => Run(() => _persistence.SaveInventoryAndUploadedNanos(
                characterId,
                inserts.Select(Map).ToArray(),
                updates.Select(Map).ToArray(),
                uploadedNanoIds.ToArray(),
                activeNanos?.Select(v => new PersistedActiveNanoData
                {
                    NanoId = v.NanoId,
                    Strain = v.Strain,
                    NanoInstance = v.NanoInstance,
                    DurationCentiseconds = v.DurationCentiseconds,
                    ExpiresAtUtcTicks = v.ExpiresAtUtcTicks
                }).ToArray(),
                skillLocks?.Select(v => new PersistedSkillLockData
                {
                    StatId = v.StatId,
                    ExpiresAtUtcTicks = v.ExpiresAtUtcTicks
                }).ToArray()), _logger);
    }
}
