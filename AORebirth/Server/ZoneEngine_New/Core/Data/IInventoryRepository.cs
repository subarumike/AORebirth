namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IInventoryRepository
    {
        /// <summary>
        /// Carried pages only (inventory / equipment / armor / implant / social). Not bank or bag interiors.
        /// </summary>
        IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId);

        IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId);

        /// <summary>
        /// Items whose parent is a backpack/container instance. Ownership follows the bag item location.
        /// </summary>
        IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId);

        /// <summary>
        /// Atomically leases <paramref name="count"/> InstanceIds from the shared sequence.
        /// Returns the inclusive start of the half-open range <c>[start, start+count)</c>.
        /// </summary>
        int LeaseInstanceIdBlock(int count);

        /// <summary>
        /// Inserts a new row using the caller-supplied unique <see cref="ItemInstanceRecord.InstanceId"/>.
        /// </summary>
        ItemInstanceRecord Insert(ItemInstanceRecord item);

        /// <summary>
        /// Moves an existing instance; does not remint InstanceId.
        /// </summary>
        void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement);

        /// <summary>
        /// Applies many location moves atomically. Uses a park-then-commit pass so swaps
        /// do not violate <c>UX_item_instances_location</c>.
        /// </summary>
        void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations);

        /// <summary>
        /// One transaction: insert newly durable rows (explicit InstanceId), then apply location updates
        /// (park-then-commit). Empty lists are no-ops.
        /// </summary>
        void PersistNewAndUpdateLocations(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates);
    }

    public readonly struct ItemLocationUpdate
    {
        public ItemLocationUpdate(int instanceId, int containerType, int containerInstance, int containerPlacement)
        {
            InstanceId = instanceId;
            ContainerType = containerType;
            ContainerInstance = containerInstance;
            ContainerPlacement = containerPlacement;
        }

        public int InstanceId { get; }

        public int ContainerType { get; }

        public int ContainerInstance { get; }

        public int ContainerPlacement { get; }
    }
}
