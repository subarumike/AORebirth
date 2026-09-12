namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlInventoryRepository : IInventoryRepository
    {
        readonly IZoneLogger _logger;
        internal ICharacterPersistenceDao Persistence { get; }
        public MySqlInventoryRepository(IZoneLogger logger, ICharacterPersistenceDao? persistence = null)
        { _logger = logger ?? throw new ArgumentNullException(nameof(logger)); Persistence = persistence ?? Create(); }
        public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId) => Run(() => Persistence.LoadCarriedItems(characterId).Select(Map).ToArray(), _logger);
        public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId) => Run(() => Persistence.LoadBankItems(characterId).Select(Map).ToArray(), _logger);
        public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId) => Run(() => Persistence.LoadContainerItems(containerInstanceId).Select(Map).ToArray(), _logger);
        public int LeaseInstanceIdBlock(int count) => Run(() => Persistence.LeaseItemInstanceIds(count), _logger);
        public ItemInstanceRecord Insert(ItemInstanceRecord item) { Run(() => Persistence.InsertItem(Map(item)), _logger); return item; }
        public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement)
            => Run(() => Persistence.UpdateItemLocation(new ItemLocationData { InstanceId = instanceId, ContainerType = containerType, ContainerInstance = containerInstance, ContainerPlacement = containerPlacement }), _logger);
        public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations) => PersistNewAndUpdateLocations([], locations);
        public void PersistNewAndUpdateLocations(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates)
            => Run(() => Persistence.SaveItemLocations(inserts.Select(Map).ToArray(), updates.Select(Map).ToArray()), _logger);
    }
}
