namespace ZoneEngine_New.Core.Inventory
{
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;

    public interface IItemBuilder
    {
        /// <param name="instanceId">Durable DB key; 0 for ephemeral non-persisted items.</param>
        Item Create(
            int lowId,
            int highId,
            int quality,
            ItemSource source,
            int stackCount = 1,
            int instanceId = 0,
            Identity? identity = null,
            byte[]? statsBlob = null);

        /// <summary>
        /// Creates an item holding a freshly allocated <see cref="Item.InstanceId"/> and the matching
        /// occupancy identity. Not persisted until a flush inserts its row, so an abandoned item
        /// (unlooted corpse, rejected purchase) only burns an id.
        /// </summary>
        Item CreateWithNewInstance(
            int lowId,
            int highId,
            int quality,
            ItemSource source,
            int stackCount = 1);

        /// <summary>
        /// Interpolated catalog definition only. No instance id or occupancy identity.
        /// </summary>
        ItemTemplate CreateTemplate(int lowId, int highId, int quality);

        bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item);
    }
}
