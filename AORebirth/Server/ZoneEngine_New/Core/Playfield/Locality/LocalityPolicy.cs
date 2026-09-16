namespace ZoneEngine_New.Core.Playfield.Locality;
using System;
using System.IO;
using Utility.Config;
using ZoneEngine_New.Core.GameData;

/// <summary>Immutable scheduling settings. Zero XML fields mean unspecified.</summary>
internal sealed record LocalityPolicy(bool EnableCellHeatScheduling, int VisibilityNeighborLevel,
    int HotNeighborLevel, int WarmNeighborLevel, int CellSleepTimeSeconds, int SpawnRate)
{
    internal static LocalityPolicy FromConfig() => FromConfig(ConfigReadWrite.Instance.CurrentConfig?.Locality);
    internal static LocalityPolicy FromConfig(LocalitySettings? settings) =>
        Load(Path.Combine(AppContext.BaseDirectory, "GameData", "Locality.json"), settings);

    internal static LocalityPolicy Load(string path, LocalitySettings? overrides = null)
    {
        var data = RuleDocument.Read<LocalitySettings>(path);
        var defaults = Validate(new(data.EnableCellHeatScheduling, data.VisibilityNeighborLevel,
            data.HotNeighborLevel, data.WarmNeighborLevel, data.CellSleepTime, data.SpawnRate));
        if (overrides == null) return defaults;
        return Validate(new(overrides.EnableCellHeatScheduling,
            Select(overrides.VisibilityNeighborLevel, defaults.VisibilityNeighborLevel),
            Select(overrides.HotNeighborLevel, defaults.HotNeighborLevel),
            Select(overrides.WarmNeighborLevel, defaults.WarmNeighborLevel),
            Select(overrides.CellSleepTime, defaults.CellSleepTimeSeconds),
            Select(overrides.SpawnRate, defaults.SpawnRate)));
    }

    private static int Select(int configured, int packaged) => configured == 0 ? packaged : configured;
    private static LocalityPolicy Validate(LocalityPolicy policy)
    {
        if (policy.HotNeighborLevel <= 0 || policy.HotNeighborLevel > policy.WarmNeighborLevel
            || policy.WarmNeighborLevel > policy.VisibilityNeighborLevel
            || policy.CellSleepTimeSeconds <= 0 || policy.SpawnRate <= 0)
            throw new InvalidDataException("Locality settings require 0 < hot <= warm <= visibility and positive sleep/spawn values.");
        return policy;
    }
}
