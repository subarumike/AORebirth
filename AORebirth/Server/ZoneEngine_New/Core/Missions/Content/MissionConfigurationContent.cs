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

internal sealed class MissionArtifactContent
{
    internal static MissionArtifactContent Current { get; } = Load();
    public MissionItemContent Key { get; set; } = new();
    public MissionItemContent[] RepairPool { get; set; } = [];
    public MissionItemContent[] RepairFallbacks { get; set; } = [];
    public Dictionary<int, MissionItemContent> Tokens { get; set; } = new();
    public int[] ReservedPlayfields { get; set; } = [];
    public int MinimumCorpseCredits { get; set; }
    public int MaximumCorpseCredits { get; set; }
    public int RepairComponentTemplateId { get; set; }
    public int RepairMachineTemplateId { get; set; }
    public int BrokenMachineTemplateId { get; set; }
    internal MissionItemContent SelectRepairComponent(Func<int, bool> templateExists, Random random)
    {
        bool Available(MissionItemContent item) => templateExists(item.LowId) && templateExists(item.HighId);
        var candidates = RepairPool.Where(Available).ToArray();
        return candidates.Length != 0 ? candidates[random.Next(candidates.Length)]
            : RepairFallbacks.FirstOrDefault(Available)
                ?? throw new InvalidOperationException("No configured repair component template exists in the item catalog.");
    }
    static MissionArtifactContent Load()
    {
        var result = MissionContentJson.Read<MissionArtifactContent>("Artifacts.json");
        foreach (var item in result.RepairPool.Concat(result.RepairFallbacks).Concat(result.Tokens.Values).Append(result.Key))
            if (item.LowId <= 0 || item.HighId <= 0 || item.Quality < 0 || string.IsNullOrWhiteSpace(item.Name))
                throw new InvalidDataException("Invalid generated mission artifact.");
        if (result.ReservedPlayfields.Any(id => id <= 0)) throw new InvalidDataException("Invalid reserved mission world identifier.");
        if (result.MinimumCorpseCredits < 0 || result.MaximumCorpseCredits < result.MinimumCorpseCredits)
            throw new InvalidDataException("Invalid mission corpse currency range.");
        if (result.RepairComponentTemplateId <= 0 || result.RepairMachineTemplateId <= 0 || result.BrokenMachineTemplateId <= 0)
            throw new InvalidDataException("Missing generated mission objective role binding.");
        return result;
    }
}

internal sealed class MissionItemContent
{
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int Quality { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint Flags { get; set; }
}

internal sealed class MissionCorpseContent
{
    internal static MissionCorpseContent Current { get; } = Load();
    public string TemplateHex { get; set; } = string.Empty;
    public Dictionary<int, int> CatMeshes { get; set; } = new();
    public int DefaultCatMesh { get; set; }
    public int DefaultMonsterData { get; set; }
    public int Sex { get; set; }
    public int Breed { get; set; }
    public int Race { get; set; }
    public string[] Provenance { get; set; } = [];
    static MissionCorpseContent Load()
    {
        var content = MissionContentJson.Read<MissionCorpseContent>("Corpse.json");
        byte[] template = Convert.FromHexString(content.TemplateHex);
        if (template.Length < 352 || content.CatMeshes.Any(pair => pair.Key <= 0 || pair.Value <= 0)
            || content.DefaultCatMesh <= 0 || content.DefaultMonsterData <= 0 || content.Sex < 0 || content.Breed < 0 || content.Race < 0)
            throw new InvalidDataException("Invalid mission corpse appearance content.");
        int nameLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(template.AsSpan(235, 4));
        if (nameLength <= 0 || nameLength > template.Length - 239 || template[239 + nameLength - 1] != 0)
            throw new InvalidDataException("Invalid mission corpse packet name layout.");
        return content;
    }
}
