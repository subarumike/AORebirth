namespace ZoneEngine_New.Core.Inventory
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;

    public interface IItemBuilder
    {
        /// <param name="instanceId">Durable DB key; 0 for ephemeral non-persisted items.</param>
        Item Create(
            int lowId,
            int highId,
            int quality,
            int stackCount = 1,
            int instanceId = 0,
            Identity? identity = null,
            byte[]? statsBlob = null);

        /// <summary>
        /// Interpolated catalog definition only. No instance id or occupancy identity.
        /// </summary>
        ItemTemplate CreateTemplate(int lowId, int highId, int quality);

        bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item);
    }
}
