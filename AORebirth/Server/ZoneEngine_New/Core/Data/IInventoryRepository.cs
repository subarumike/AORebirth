namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IInventoryRepository
    {
        IReadOnlyList<ItemRecord> GetItemsForCharacter(int characterId);

        IReadOnlyList<InstancedItemRecord> GetInstancedItemsForCharacter(int characterId);
    }
}
