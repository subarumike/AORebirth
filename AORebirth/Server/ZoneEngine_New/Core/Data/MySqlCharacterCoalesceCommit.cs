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
        public MySqlCharacterCoalesceCommit(MySqlInventoryRepository inventory, MySqlUploadedNanoRepository nanos, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(nanos);
            _persistence = (inventory ?? throw new ArgumentNullException(nameof(inventory))).Persistence;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> uploadedNanoIds)
            => Run(() => _persistence.SaveInventoryAndUploadedNanos(characterId, inserts.Select(Map).ToArray(), updates.Select(Map).ToArray(), uploadedNanoIds.ToArray()), _logger);
    }
}
