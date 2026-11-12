namespace AORebirth.World.Contracts;

/// <summary>NPC instance identity block advertised in a World registration reply.</summary>
public readonly record struct NpcInstanceRange(int FirstInstance, int Count)
{
    /// <summary>Slot zero retains the standalone zone identity range.</summary>
    public const int FirstStandaloneInstance = 1_000_000;

    public const int InstancesPerSlot = 0x00800000;

    public static NpcInstanceRange ForSlot(int slot)
        => new(FirstStandaloneInstance + slot * InstancesPerSlot, InstancesPerSlot);
}
