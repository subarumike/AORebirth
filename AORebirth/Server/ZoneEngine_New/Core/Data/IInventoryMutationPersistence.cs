namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public readonly record struct ItemStackUpdate(int InstanceId, int ExpectedCount, int FinalCount);

    public sealed record InventoryMutationBatch(int CharacterId,
        IReadOnlyList<ItemInstanceRecord> Inserts, IReadOnlyList<ItemLocationUpdate> Locations,
        IReadOnlyList<ItemStackUpdate> Stacks, IReadOnlyList<int> UploadedNanoIds)
    {
        public IReadOnlyList<StatRecord> FinalStats { get; init; } = [];
        public IReadOnlyList<int> EmptyContainersBeforeRetire { get; init; } = [];
    }

    public interface IInventoryMutationPersistence
    {
        void Persist(InventoryMutationBatch batch);
    }
}
