namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Missions;

// Stable values belong to the accepted generated-mission persistence/protocol contract.
internal enum MissionRollType { Unknown = -1, KillPerson = 0, FindPerson = 1, FindItem = 2, RepairMachine = 3, FindItemReturn = 4 }

internal sealed class MissionRollPolicy
{
    internal static MissionRollPolicy Current { get; } = Load();
    public PolicyProvenance Provenance { get; set; } = new();
    public int ClientClockBaseSeconds { get; set; }
    public int OfferLifetimeSeconds { get; set; }
    public int AcceptedLifetimeSeconds { get; set; }
    public int MinimumRollFee { get; set; }
    public int FeePerCharacterLevel { get; set; }
    public RollTypeRule[] Types { get; set; } = [];
    public string DefaultSliderEvidence { get; set; } = string.Empty;
    public SliderEvidenceRule[] SliderProfiles { get; set; } = [];
    public RollTravelPolicy Travel { get; set; } = new();
    public RollRewardPolicy Rewards { get; set; } = new();
    internal MissionRollType TypeFromIcon(int icon) => Types.SingleOrDefault(t => t.Icon == icon)?.Type ?? MissionRollType.Unknown;
    internal int Icon(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.Icon ?? 0;
    internal int Action(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.Action ?? 0;
    internal int ObjectiveInteraction(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.ObjectiveInteraction ?? 0;
    internal string Title(MissionRollType type) => Types.Single(t => t.Type == type).Title;
    internal int Fee(int level) => Math.Max(MinimumRollFee, checked(level * FeePerCharacterLevel));
    static MissionRollPolicy Load()
    {
        var policy = MissionContentJson.Read<MissionRollPolicy>("RollPolicy.json");
        policy.Validate();
        return policy;
    }
    internal void Validate()
    {
        var types = Enum.GetValues<MissionRollType>().Where(t => t != MissionRollType.Unknown).Order().ToArray();
        if (ClientClockBaseSeconds <= 0 || OfferLifetimeSeconds <= 0 || AcceptedLifetimeSeconds <= 0 || MinimumRollFee <= 0 || FeePerCharacterLevel <= 0
            || !Types.Select(t => t.Type).Order().SequenceEqual(types) || Types.Any(t => t.Icon <= 0 || t.Action <= 0 || t.ObjectiveInteraction <= 0 || string.IsNullOrWhiteSpace(t.Title))
            || Types.Select(t => t.Icon).Distinct().Count() != Types.Length)
            throw new InvalidDataException("Invalid mission roll protocol/policy content.");
        var names = SliderProfiles.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (names.Count != SliderProfiles.Length || !names.Contains(DefaultSliderEvidence)
            || SliderProfiles.Any(p => string.IsNullOrWhiteSpace(p.Name) || p.Values.Length != 6 || p.Values.Any(v => v < -100 || v > 100)
                || p.Preference.Length != names.Count || p.Preference.Distinct().Count() != names.Count || p.Preference.Any(v => !names.Contains(v)))
            || SliderProfiles.Select(p => string.Join(",", p.Values)).Distinct().Count() != SliderProfiles.Length)
            throw new InvalidDataException("Invalid mission slider evidence preferences.");
        string[] categories = ["same", "near", "ring", "local", "distance", "side", "all"];
        if (Travel.Bands.Length == 0 || Travel.Bands[^1].MaximumLevel != int.MaxValue
            || !Travel.Bands.Select(b => b.MaximumLevel).SequenceEqual(Travel.Bands.Select(b => b.MaximumLevel).Distinct().Order())
            || Travel.Bands.Any(b => b.Priority.Length == 0 || b.Priority.Any(p => !categories.Contains(p)))
            || Travel.DistinctAttempts <= 0 || Travel.DistinctPlayfieldsAboveLevel < 0 || Travel.MinimumDistanceStartsAboveLevel < 0
            || !double.IsFinite(Travel.MaximumDistanceOffset) || !double.IsFinite(Travel.MaximumDistancePerLevel)
            || !double.IsFinite(Travel.MinimumDistanceOffset) || !double.IsFinite(Travel.MinimumDistancePerLevel) || !double.IsFinite(Travel.MinimumDistanceFloor)
            || Rewards.NanoQualityTolerance < 0 || Rewards.Catalogs.Length == 0
            || Rewards.Catalogs.Any(c => string.IsNullOrWhiteSpace(c.File) || Path.GetFileName(c.File) != c.File)
            || Rewards.Catalogs.Select(c => c.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Rewards.Catalogs.Length
            || Rewards.ExcludedNameFragments.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Invalid mission travel/reward policy content.");
    }
}

internal sealed class PolicyProvenance { public string SourceRevision { get; set; } = string.Empty; public string[] SourcePaths { get; set; } = []; public string Basis { get; set; } = string.Empty; }
internal sealed class RollTypeRule { public MissionRollType Type { get; set; } public int Icon { get; set; } public int Action { get; set; } public int ObjectiveInteraction { get; set; } public string Title { get; set; } = string.Empty; }
internal sealed class SliderEvidenceRule { public string Name { get; set; } = string.Empty; public int[] Values { get; set; } = []; public string[] Preference { get; set; } = []; }
internal sealed class RollTravelPolicy
{
    public int DistinctPlayfieldsAboveLevel { get; set; }
    public int DistinctAttempts { get; set; }
    public int MinimumDistanceStartsAboveLevel { get; set; }
    public double MinimumDistancePerLevel { get; set; }
    public double MinimumDistanceOffset { get; set; }
    public double MinimumDistanceFloor { get; set; }
    public double MaximumDistancePerLevel { get; set; }
    public double MaximumDistanceOffset { get; set; }
    public RollTravelBand[] Bands { get; set; } = [];
}
internal sealed class RollTravelBand { public int MaximumLevel { get; set; } public string[] Priority { get; set; } = []; }
internal sealed class RollRewardPolicy { public int NanoQualityTolerance { get; set; } public string[] ExcludedNameFragments { get; set; } = []; public RollRewardFile[] Catalogs { get; set; } = []; }
internal sealed class RollRewardFile { public string File { get; set; } = string.Empty; public bool FixedQuality { get; set; } }
