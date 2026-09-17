namespace ZoneEngine_New.Core.Missions;

using System;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Missions;

internal sealed class MissionGenerationSettings
{
    internal static MissionGenerationSettings Current { get; } = Load();
    public string LayoutSelection { get; init; } = "";
    public MissionStatScale Level { get; init; }
    public MissionStatScale Health { get; init; }
    public float PositionTolerance { get; init; }
    public int MinimumGeometryPoints { get; init; }
    public string[] Provenance { get; init; } = [];
    static MissionGenerationSettings Load()
    {
        var data = MissionContentJson.Read<MissionGenerationSettings>("Generation.json");
        if (data.LayoutSelection != "seeded-index" || !float.IsFinite(data.PositionTolerance) || data.PositionTolerance < 0
            || data.MinimumGeometryPoints < 1)
            throw new InvalidDataException("Invalid mission generation policy.");
        data.Level.Validate(); data.Health.Validate();
        return data;
    }
    internal MissionAcgLayoutBundle Select(MissionAcgLayoutCatalog catalog, int type, int quality, int seed)
    {
        var options = catalog.SelectableLayouts.Where(layout => layout.Completeness.IsSelectionComplete
            && layout.SupportsMission((MissionRollType)type, quality)).OrderBy(layout => layout.LayoutId, StringComparer.Ordinal).ToArray();
        if (options.Length == 0) throw new InvalidOperationException("No valid mission layout matches the requested type and quality.");
        return options[new Random(seed).Next(options.Length)];
    }
}

/// <summary>A bounded affine stat projection; random samples are content-selected additive units or percentages.</summary>
internal sealed class MissionStatScale
{
    public int Multiplier { get; init; }
    public int Minimum { get; init; }
    public int Maximum { get; init; }
    public int RandomMinimum { get; init; }
    public int RandomMaximum { get; init; }
    public bool RandomIsPercent { get; init; }
    internal void Validate()
    {
        if (Multiplier <= 0 || Minimum <= 0 || Maximum < Minimum || RandomMaximum < RandomMinimum || RandomMaximum == int.MaxValue)
            throw new InvalidDataException("Invalid mission stat scaling range.");
    }
    internal int Sample(int input, Random random)
    {
        long value = checked((long)Math.Max(1, input) * Multiplier);
        int sample = random.Next(RandomMinimum, checked(RandomMaximum + 1));
        long adjustment = RandomIsPercent ? checked(value * sample) / 100 : sample;
        return checked((int)Math.Clamp(checked(value + adjustment), Minimum, Maximum));
    }
}
