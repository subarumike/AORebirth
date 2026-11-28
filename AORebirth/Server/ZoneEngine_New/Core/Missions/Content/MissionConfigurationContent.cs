namespace ZoneEngine.Core.Missions;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using ZoneEngine_New.Core.Missions;

internal sealed class MissionGeographyContent
{
    internal static MissionGeographyContent Current => MissionContent.Shared.Value.Geography;
    public required ImmutableDictionary<int, MissionLocationSide> Cities { get; init; }
    public required ImmutableDictionary<int, MissionLocationSide> Markers { get; init; }
    public required ImmutableDictionary<int, int> TravelTiers { get; init; }
    public required ImmutableDictionary<int, string> Names { get; init; }
    public required ImmutableArray<MissionTravelCluster> Clusters { get; init; }
    internal static MissionGeographyContent Parse(ReadOnlyMemory<byte> bytes)
    {
        var result = MissionContentJson.ParseImmutable<MissionGeographyContent>(bytes);
        if (result.Cities.Any(pair => pair.Key <= 0 || !Enum.IsDefined(pair.Value))
            || result.Markers.Any(pair => pair.Key <= 0 || !Enum.IsDefined(pair.Value))
            || result.TravelTiers.Any(pair => pair.Key <= 0 || pair.Value < 0)
            || result.Names.Any(pair => pair.Key <= 0 || string.IsNullOrWhiteSpace(pair.Value))
            || result.Clusters.SelectMany(c => c.From).Distinct().Count() != result.Clusters.Sum(c => c.From.Length))
            throw new InvalidDataException("Invalid or overlapping mission geography.");
        return result;
    }
}

internal sealed class MissionTravelCluster
{
    public required ImmutableArray<int> From { get; init; }
    public required ImmutableArray<int> Near { get; init; }
    public required ImmutableArray<int> Next { get; init; }
}

internal sealed class MissionTextContent
{
    internal static MissionTextContent Current => MissionContent.Shared.Value.Text;
    public required ImmutableDictionary<int, string> Objectives { get; init; }
    public string RewardFormat { get; init; } = string.Empty;
    public required ImmutableDictionary<int, string> RequiredObjectiveText { get; init; }

    internal static MissionTextContent Parse(ReadOnlyMemory<byte> bytes)
    {
        var content = MissionContentJson.ParseImmutable<MissionTextContent>(bytes);
        var types = Enum.GetValues<MissionRollType>().Where(type => type != MissionRollType.Unknown).Select(type => (int)type).ToArray();
        if (string.IsNullOrWhiteSpace(content.RewardFormat) || CompositeFormat.Parse(content.RewardFormat).MinimumArgumentCount != 3
            || content.Objectives.Count != types.Length || content.RequiredObjectiveText.Count != types.Length
            || types.Any(type => !content.Objectives.TryGetValue(type, out var objective) || string.IsNullOrWhiteSpace(objective)
                || CompositeFormat.Parse(objective).MinimumArgumentCount is < 1 or > 2
                || !content.RequiredObjectiveText.TryGetValue(type, out var required) || string.IsNullOrWhiteSpace(required)
                || !objective.Contains(required, StringComparison.Ordinal)))
            throw new InvalidDataException("Invalid mission offer text content.");
        return content;
    }
}
