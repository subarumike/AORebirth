namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ZoneEngine_New.Core.Quests.Dungeons;

/// <summary>Validated dungeon policy and retained private input identities, published by the shared GameData provider.</summary>
internal sealed record DungeonGenerationContent(DungeonGenerationPolicy? Policy,
    ImmutableDictionary<int, DungeonDoorStyle> Doors)
{
    internal const string ModuleId = "MissionDungeons";

    static readonly ConditionalWeakTable<GameDataContentProvider, Lazy<ContentSlot<DungeonGenerationContent>>> Slots = new();

    internal static ContentSlot<DungeonGenerationContent> Register(GameDataContentProvider provider)
        => Slots.GetValue(provider, static owner => new(() =>
            owner.Register(new ContentModule<DungeonGenerationContent>(ModuleId, 1,
                [new(DungeonLayoutGenerator.GenerationFileName, Required: false),
                 new(DungeonLayoutGenerator.DoorStylesFileName, Required: false)], [], Load)))).Value;

    static DungeonGenerationContent Load(ContentInputs inputs)
    {
        var generation = inputs.Optional(DungeonLayoutGenerator.GenerationFileName);
        var doors = inputs.Optional(DungeonLayoutGenerator.DoorStylesFileName);
        var policy = generation is { } generationBytes ? DungeonGenerationPolicy.Parse(generationBytes.Span) : null;
        var doorStyles = doors is { } doorBytes ? ParseDoors(doorBytes.Span) : ImmutableDictionary<int, DungeonDoorStyle>.Empty;
        return new(policy, doorStyles);
    }

    internal static ImmutableDictionary<int, DungeonDoorStyle> ParseDoors(ReadOnlySpan<byte> bytes)
    {
        var file = JsonSerializer.Deserialize<DoorStylesFile>(bytes, RetainedDungeonContent.JsonOptions)
            ?? throw new InvalidDataException("Invalid dungeon door styles.");
        int open = Math.Clamp(file.OpenChancePercent, 0, 100);
        int locked = Math.Clamp(file.LockedChancePercent, 0, 100);
        var result = ImmutableDictionary.CreateBuilder<int, DungeonDoorStyle>();
        foreach (DoorStyleRow row in file.Styles ?? [])
        {
            if (row.Style > 0 && row.DoorTemplate > 0 && row.ExitTemplate > 0)
                result[row.Style] = new DungeonDoorStyle(row.DoorTemplate, row.ExitTemplate, open, locked);
        }

        return result.ToImmutable();
    }

    sealed class DoorStylesFile
    {
        public int OpenChancePercent { get; init; }
        public int LockedChancePercent { get; init; }
        public List<DoorStyleRow> Styles { get; init; } = [];
    }

    sealed record DoorStyleRow(int Style, int DoorTemplate, int ExitTemplate);
}

/// <summary>The active dependency selector used only for acceptance; historical restoration does not depend on it.</summary>
internal sealed record DungeonContentIndex(ImmutableDictionary<int, string> Bundles)
{
    const string ModuleId = "MissionDungeonActiveContent";
    static readonly ConditionalWeakTable<GameDataContentProvider, Lazy<ContentSlot<DungeonContentIndex>>> Slots = new();

    internal static ContentSlot<DungeonContentIndex> Register(GameDataContentProvider provider)
    {
        DungeonGenerationContent.Register(provider);
        return Slots.GetValue(provider, static owner => new(() =>
            owner.Register(new ContentModule<DungeonContentIndex>(ModuleId, 1,
                [new(RetainedDungeonContent.CurrentFile, Required: false)], [DungeonGenerationContent.ModuleId], Load)))).Value;
    }

    static DungeonContentIndex Load(ContentInputs inputs)
    {
        if (inputs.Optional(RetainedDungeonContent.CurrentFile) is not { } bytes)
            return new(ImmutableDictionary<int, string>.Empty);

        var policy = inputs.RequireDependency<DungeonGenerationContent>(DungeonGenerationContent.ModuleId).Value.Policy;
        var value = JsonSerializer.Deserialize<BundleIndex>(bytes.Span, RetainedDungeonContent.JsonOptions);
        if (value?.SchemaVersion != DungeonContentReference.SupportedSchemaVersion || value.Styles is not { Count: > 0 }
            || value.Styles.Any(style => !new DungeonContentReference(value.SchemaVersion, style.Key, style.Value).IsValid))
            throw new InvalidDataException("Invalid active retained dungeon content index.");
        if (policy is null || !policy.Profiles.SelectMany(profile => profile.Styles).ToHashSet().SetEquals(value.Styles.Keys))
            throw new InvalidDataException("Active retained dungeon styles do not match the validated generation profiles.");

        return new(value.Styles.ToImmutableDictionary());
    }

    sealed class BundleIndex
    {
        public int SchemaVersion { get; init; }
        public Dictionary<int, string> Styles { get; init; } = [];
    }
}

internal sealed class DungeonGenerationPolicy
{
    public int FloorHeight { get; init; }
    public int LockedDoorChancePercent { get; init; }
    public DungeonPopulationPolicy? Population { get; init; }
    public DungeonFindItemPolicy? FindItem { get; init; }
    public DungeonFindItemPolicy? ReturnItem { get; init; }
    public DungeonRepairMachinePolicy? RepairMachine { get; init; }
    public DungeonNpcObjectivePolicy? NpcObjectives { get; init; }
    public MissionChestPolicy? Chests { get; init; }
    public MissionTokenRewardPolicy? TokenReward { get; init; }
    public ImmutableArray<DungeonGenerationProfile> Profiles { get; init; } = [];

    internal static DungeonGenerationPolicy Parse(ReadOnlySpan<byte> bytes)
    {
        var value = JsonSerializer.Deserialize<DungeonGenerationPolicy>(bytes, RetainedDungeonContent.JsonOptions);
        if (value == null || value.Population is not { SpawnPointChancePercent: >= 0 and <= 100, RegularNpcHashes.Length: > 0 }
            || value.Population.RegularNpcHashes.Any(string.IsNullOrWhiteSpace)
            || value.Population.RegularNpcHashes.Distinct(StringComparer.Ordinal).Count() != value.Population.RegularNpcHashes.Length
            || value.FloorHeight is <= 0 or > 255
            || (value.TokenReward != null && !value.TokenReward.IsValid)
            || (value.Chests != null && !value.Chests.IsValid)
            || value.LockedDoorChancePercent is < 0 or > 100 || value.Profiles.IsDefaultOrEmpty
            || value.Profiles.Any(p => p.MinQuality < 1 || p.MinQuality > p.MaxQuality
                || p.Styles.IsDefaultOrEmpty || p.Styles.Any(s => s <= 0) || p.Floors < (p.Team ? 2 : 1) || p.Floors > 8
                || (!p.Team && p.Floors != 1) || p.MinFloorSize < 3 || p.MaxFloorSize > 40 || p.MinFloorSize > p.MaxFloorSize))
            throw new InvalidDataException("Invalid dungeon generation policy.");

        for (int i = 0; i < value.Profiles.Length; i++)
        for (int j = i + 1; j < value.Profiles.Length; j++)
        {
            if (value.Profiles[i].Team == value.Profiles[j].Team && value.Profiles[i].MinQuality <= value.Profiles[j].MaxQuality
                && value.Profiles[j].MinQuality <= value.Profiles[i].MaxQuality)
                throw new InvalidDataException("Overlapping dungeon generation profiles.");
        }

        return value;
    }
}

internal sealed record DungeonGenerationProfile(bool Team, int MinQuality, int MaxQuality, ImmutableArray<int> Styles,
    int Floors, int MinFloorSize, int MaxFloorSize);
