namespace ZoneEngine_New.Core.Inventory
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;

    public interface IItemBuilder
    {
        Item Create(
            int lowId,
            int highId,
            int quality,
            int stackCount = 1,
            Identity? identity = null,
            byte[]? statsBlob = null);

        bool TryFromRecord(ItemRecord row, out Item item);

        bool TryFromInstancedRecord(InstancedItemRecord row, out Item item);
    }
}
