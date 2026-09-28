namespace ZoneEngine.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    internal static MissionTextContent Current { get; } = MissionContentJson.Read<MissionTextContent>("Text.json");
    public Dictionary<int, string> Objectives { get; set; } = new();
    public string RewardFormat { get; set; } = string.Empty;
    public Dictionary<int, string> RequiredObjectiveText { get; set; } = new();
}
