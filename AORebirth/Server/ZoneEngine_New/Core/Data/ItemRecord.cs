namespace ZoneEngine_New.Core.Data
{
    public sealed class ItemRecord
    {
        public int ContainerInstance { get; init; }

        public int ContainerPlacement { get; init; }

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int MultipleCount { get; init; }
    }
}
