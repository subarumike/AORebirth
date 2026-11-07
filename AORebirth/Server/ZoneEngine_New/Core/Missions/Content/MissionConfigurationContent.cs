namespace ZoneEngine.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ZoneEngine_New.Core.Missions;

internal sealed class MissionGeographyContent
{
    internal static MissionGeographyContent Current { get; } = Load();
    public Dictionary<int, MissionLocationSide> Cities { get; set; } = new();
    public Dictionary<int, MissionLocationSide> Markers { get; set; } = new();
    public Dictionary<int, int> TravelTiers { get; set; } = new();
    public Dictionary<int, string> Names { get; set; } = new();
    public MissionTravelCluster[] Clusters { get; set; } = [];
    static MissionGeographyContent Load()
    {
        var result = MissionContentJson.Read<MissionGeographyContent>("Locations.json");
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
    public int[] From { get; set; } = [];
    public int[] Near { get; set; } = [];
    public int[] Next { get; set; } = [];
}

internal sealed class MissionTextContent
{
    internal static MissionTextContent Current { get; } = Load();
    public Dictionary<int, string> Objectives { get; set; } = new();
    public string RewardFormat { get; set; } = string.Empty;
    public Dictionary<int, string> RequiredObjectiveText { get; set; } = new();

    static MissionTextContent Load()
    {
        var content = MissionContentJson.Read<MissionTextContent>("Text.json");
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
