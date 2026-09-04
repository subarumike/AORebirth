namespace ZoneEngine_New.Core.Data
{
    public sealed class ItemInstanceRecord
    {
        public int InstanceId { get; init; }

        public int ContainerType { get; init; }

        public int ContainerInstance { get; init; }

        public int ContainerPlacement { get; init; }

        public int ItemType { get; init; }

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int StackCount { get; init; }
    }
}
