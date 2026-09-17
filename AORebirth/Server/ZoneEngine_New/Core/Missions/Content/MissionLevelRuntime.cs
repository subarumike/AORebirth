namespace ZoneEngine.Core.Missions;

using System;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>Reads the accepted MissionLevels.csv directly; no generated C# data or fallback table.</summary>
internal static class MissionLevelRuntime
{
    internal const int SliderPositions = 11; // Terminal protocol detents, sent one-based.
    static readonly Lazy<MissionLevelData> Current = new(() => MissionLevelData.Load(
        Path.Combine(MissionContentJson.RootPath, "Source", "MissionLevels.csv")));

    internal static bool TryDecodeDifficultySlider(int wire, out int index)
    { index = wire - 1; return index >= 0 && index < SliderPositions; }

    internal static bool TryGetMissionQuality(int level, int wire, out int quality)
    {
        quality = 0;
        if (!TryDecodeDifficultySlider(wire, out _)) return false;
        try { quality = Current.Value.Quality(level, wire); return true; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or FormatException or OverflowException)
        { return false; }
    }

    internal static int GetMissionQuality(int level, int wire)
        => TryGetMissionQuality(level, wire, out int quality) ? quality
            : throw new InvalidOperationException("Mission level content is unavailable or the difficulty is invalid.");

    internal static int ClampCharacterLevel(int level) => Current.Value.Clamp(level);

    internal static int GetTokenReward(int level)
        => TryGetTokenReward(level, out int tokens, out _) ? tokens : 0;

    internal static bool TryGetTokenReward(int level, out int tokens, out string reason)
    {
        tokens = 0;
        reason = string.Empty;
        try { tokens = Current.Value.Tokens(level); return true; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or FormatException or OverflowException)
        { reason = error.Message; return false; }
    }
}

internal sealed class MissionLevelData
{
    readonly int[][] rows;
    MissionLevelData(int[][] values) => rows = values;

    internal static MissionLevelData Load(string path)
    {
        var rules = MissionContentJson.Read<MissionLevelRules>("Source/MissionLevelRules.json");
        rules.Validate();
        var lines = File.ReadAllLines(path);
        var header = new[] { "Level" }.Concat(Enumerable.Range(0, MissionLevelRuntime.SliderPositions)
            .Select(index => "Q" + index.ToString(CultureInfo.InvariantCulture))).Append("Tokens");
        if (lines.Length < 2 || !lines[0].Split(',').SequenceEqual(header))
            throw new InvalidDataException("Invalid mission level columns or empty table.");
        int Cell(string text)
        {
            int value = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
            if (text != value.ToString(CultureInfo.InvariantCulture)) throw new InvalidDataException("Noncanonical mission level number.");
            return value;
        }
        var values = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(',').Select(Cell).ToArray()).ToArray();
        if (values.Length != rules.LevelCount || values.Where((row, index) => row.Length != MissionLevelRuntime.SliderPositions + 2
            || row[0] != index + 1 || row.Any(cell => cell <= 0)).Any())
            throw new InvalidDataException("Mission levels must be contiguous and contain positive quality and token values.");
        foreach (var row in values)
        {
            var qualities = row.Skip(1).Take(MissionLevelRuntime.SliderPositions).ToArray();
            if (qualities.Any(value => value < rules.MinimumQuality || value > rules.MaximumQuality)
                || qualities.Zip(qualities.Skip(1), (left, right) => left <= right).Any(valid => !valid)
                || qualities[rules.NeutralDifficultyIndex] != row[0]
                || row[^1] < rules.MinimumTokens || row[^1] > rules.MaximumTokens)
                throw new InvalidDataException("Mission quality or token data violates the configured validation policy.");
        }
        if (values.Zip(values.Skip(1), (previous, next) => previous.Skip(1).Zip(next.Skip(1), (left, right) => left <= right).All(valid => valid)).Any(valid => !valid))
            throw new InvalidDataException("Mission quality and token values must not decrease between levels.");
        return new(values);
    }

    internal int Clamp(int level) => Math.Clamp(level, 1, rows.Length);
    int[] Row(int level) => rows[Clamp(level) - 1];
    internal int Quality(int level, int wire)
        => MissionLevelRuntime.TryDecodeDifficultySlider(wire, out _) ? Row(level)[wire]
            : throw new ArgumentOutOfRangeException(nameof(wire));
    internal int Tokens(int level) => Row(level)[^1];
}

internal sealed class MissionLevelRules
{
    public int LevelCount { get; set; }
    public int MinimumQuality { get; set; }
    public int MaximumQuality { get; set; }
    public int MinimumTokens { get; set; }
    public int MaximumTokens { get; set; }
    public int NeutralDifficultyIndex { get; set; }
    public string[] Provenance { get; set; } = [];
    internal void Validate()
    {
        if (LevelCount <= 0 || MinimumQuality <= 0 || MaximumQuality < MinimumQuality
            || MinimumTokens <= 0 || MaximumTokens < MinimumTokens
            || NeutralDifficultyIndex < 0 || NeutralDifficultyIndex >= MissionLevelRuntime.SliderPositions)
            throw new InvalidDataException("Invalid mission level validation policy.");
    }
}
