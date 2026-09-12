namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlTradePersistence : ITradePersistence
    {
        readonly ICharacterPersistenceDao _persistence;
        public MySqlTradePersistence(MySqlInventoryRepository inventory, MySqlUploadedNanoRepository nanos)
        {
            ArgumentNullException.ThrowIfNull(nanos);
            _persistence = (inventory ?? throw new ArgumentNullException(nameof(inventory))).Persistence;
        }
        public void Persist(TradePersistenceBatch batch) => Run(() => _persistence.CommitItemCredits(new ItemCreditMutationData
        {
            Inserts = batch.Inserts.Select(Map).ToArray(), Locations = batch.Updates.Select(Map).ToArray(),
            Characters = batch.Characters.Select(c => new CharacterCreditData { CharacterId = c.CharacterId, Cash = c.Cash, UploadedNanoIds = c.UploadedNanoIds.ToArray() }).ToArray()
        }));
    }
}
