namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>Identity of the retained original resources needed to replay the accepted layout.</summary>
public sealed record DungeonContentReference(int SchemaVersion, int StyleId, string Sha256)
{
    public const int SupportedSchemaVersion = 1;

    [JsonIgnore]
    public bool IsValid => SchemaVersion == SupportedSchemaVersion && StyleId > 0
        && Sha256 is { Length: 64 } && Sha256.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Configures new mission dungeons and reconstructs accepted layouts through the existing procedural generator.</summary>
public interface IMissionDungeonGenerator
{
    bool IsAvailable { get; }

    bool ConfigureNewDungeon(QuestDungeonParameters parameters, bool team);

    bool TryGenerate(QuestDungeonParameters parameters, out DungeonLayout layout);

    DungeonFindItemPolicy? FindItemPolicy { get; }
    DungeonFindItemPolicy? ReturnItemPolicy { get; }
    DungeonRepairMachinePolicy? RepairMachinePolicy { get; }
    DungeonNpcObjectivePolicy? NpcObjectivePolicy { get; }
    MissionChestPolicy? ChestPolicy { get; }
}
