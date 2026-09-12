namespace AORebirth.Interfaces.Persistence.Characters
{
    using System.Collections.Generic;

    /// <summary>Existing characters-row gameplay projection. Other character columns are never overwritten.</summary>
    public sealed class CharacterStateData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Playfield { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float HeadingW { get; set; }
        public float HeadingX { get; set; }
        public float HeadingY { get; set; }
        public float HeadingZ { get; set; }
    }

    /// <summary>Persisted base value, not an effective value with equipment/nano modifiers applied.</summary>
    public sealed class CharacterStatData
    {
        public int StatId { get; set; }
        public int StatValue { get; set; }
    }

    public sealed class PersistedItemData
    {
        public int InstanceId { get; set; }
        public int ContainerType { get; set; }
        public int ContainerInstance { get; set; }
        public int ContainerPlacement { get; set; }
        public int ItemType { get; set; }
        public int LowId { get; set; }
        public int HighId { get; set; }
        public int Quality { get; set; }
        public int StackCount { get; set; }
        public int Source { get; set; }
    }

    public sealed class ItemLocationData
    {
        public int InstanceId { get; set; }
        public int ContainerType { get; set; }
        public int ContainerInstance { get; set; }
        public int ContainerPlacement { get; set; }
    }

    public sealed class ItemStackData
    {
        public int InstanceId { get; set; }
        public int ExpectedCount { get; set; }
        public int FinalCount { get; set; }
    }

    public sealed class CharacterInventoryMutationData
    {
        public int CharacterId { get; set; }
        public IList<PersistedItemData> Inserts { get; set; } = new List<PersistedItemData>();
        public IList<ItemLocationData> Locations { get; set; } = new List<ItemLocationData>();
        public IList<ItemStackData> Stacks { get; set; } = new List<ItemStackData>();
        public IList<int> UploadedNanoIds { get; set; } = new List<int>();
        public IList<CharacterStatData> FinalStats { get; set; } = new List<CharacterStatData>();
        public IList<int> EmptyContainersBeforeRetire { get; set; } = new List<int>();
    }

    public sealed class CharacterCreditData
    {
        public int CharacterId { get; set; }
        public int Cash { get; set; }
        public IList<int> UploadedNanoIds { get; set; } = new List<int>();
    }

    public sealed class ItemCreditMutationData
    {
        public IList<PersistedItemData> Inserts { get; set; } = new List<PersistedItemData>();
        public IList<ItemLocationData> Locations { get; set; } = new List<ItemLocationData>();
        public IList<CharacterCreditData> Characters { get; set; } = new List<CharacterCreditData>();
    }

    public sealed class PersistedActiveNanoData
    {
        public int NanoId { get; set; }
        public int Strain { get; set; }
        public int NanoInstance { get; set; }
        public int DurationCentiseconds { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
    }

    public sealed class CharacterActiveNanoData
    {
        public int CharacterId { get; set; }
        public IList<PersistedActiveNanoData> ActiveNanos { get; set; } = new List<PersistedActiveNanoData>();
        public IList<CharacterStatData> BaseStats { get; set; } = new List<CharacterStatData>();
    }
}
