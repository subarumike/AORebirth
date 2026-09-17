namespace ZoneEngine_New.Core.Teams;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>Validated editable eligibility windows; invitation policy remains in TeamService.</summary>
internal sealed class TeamLevelEligibility
{
    internal readonly record struct LevelRange(int Level, int Minimum, int Maximum);
    sealed record Content(int MinimumLevel, int MaximumLevel, LevelRange[] Ranges);

    internal static TeamLevelEligibility Current { get; } = Load(
        Path.Combine(AppContext.BaseDirectory, "GameData", "Teams", "LevelEligibility.json"));

    readonly LevelRange[] ranges;
    TeamLevelEligibility(LevelRange[] values) => ranges = values;

    internal static TeamLevelEligibility Load(string path)
    {
        var content = JsonSerializer.Deserialize<Content>(File.ReadAllText(path));
        if (content?.Ranges == null || content.MinimumLevel <= 0 || content.MaximumLevel < content.MinimumLevel
            || content.Ranges.LongLength != (long)content.MaximumLevel - content.MinimumLevel + 1
            || content.Ranges.Where((range, index) => range.Level != (long)content.MinimumLevel + index
                || range.Minimum < content.MinimumLevel || range.Maximum > content.MaximumLevel
                || range.Minimum > range.Level || range.Maximum < range.Level).Any())
            throw new InvalidDataException("Team eligibility requires one ordered, valid range for every configured level.");
        return new(content.Ranges);
    }

    internal LevelRange ForLevel(int level)
        => ranges[Math.Clamp(level, ranges[0].Level, ranges[^1].Level) - ranges[0].Level];
}
