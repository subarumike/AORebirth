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
        /// Inserts a new row and returns it with the minted <see cref="ItemInstanceRecord.InstanceId"/>.
        /// Input <see cref="ItemInstanceRecord.InstanceId"/> is ignored.
        /// </summary>
        ItemInstanceRecord Insert(ItemInstanceRecord item);

        /// <summary>
        /// Moves an existing instance; does not remint InstanceId.
        /// </summary>
        void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement);
    }
}
