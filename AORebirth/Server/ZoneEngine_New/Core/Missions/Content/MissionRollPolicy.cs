namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Missions;

// Stable values belong to the accepted generated-mission persistence/protocol contract.
internal enum MissionRollType { Unknown = -1, KillPerson = 0, FindPerson = 1, FindItem = 2, RepairMachine = 3, FindItemReturn = 4 }

internal sealed class MissionRollPolicy
{
    internal static MissionRollPolicy Current => MissionContent.Shared.Value.Policy;
    public PolicyProvenance Provenance { get; init; } = new();
    /// <summary>False turns mission terminal rolling off: a roll is refused before any credits are taken. Missing reads as on.</summary>
    public bool Enabled { get; init; } = true;
    public int OfferLifetimeSeconds { get; init; }
    public int AcceptedLifetimeSeconds { get; init; }
    public int MinimumRollFee { get; init; }
    public int FeePerCharacterLevel { get; init; }
    public ImmutableArray<RollTypeRule> Types { get; init; } = [];
    public string DefaultSliderEvidence { get; init; } = string.Empty;
    public ImmutableArray<SliderEvidenceRule> SliderProfiles { get; init; } = [];
    public RollTravelPolicy Travel { get; init; } = new();
    public RollRewardPolicy Rewards { get; init; } = new();
    internal MissionRollType TypeFromIcon(int icon) => Types.SingleOrDefault(t => t.Icon == icon)?.Type ?? MissionRollType.Unknown;
    internal int Icon(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.Icon ?? 0;
    internal int Action(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.Action ?? 0;
    internal int ObjectiveInteraction(MissionRollType type) => Types.SingleOrDefault(t => t.Type == type)?.ObjectiveInteraction ?? 0;
    internal string Title(MissionRollType type) => Types.Single(t => t.Type == type).Title;
    internal int Fee(int level) => Math.Max(MinimumRollFee, checked(level * FeePerCharacterLevel));
    internal static MissionRollPolicy Parse(ReadOnlyMemory<byte> bytes)
    {
        var policy = MissionContentJson.ParseImmutable<MissionRollPolicy>(bytes);
        policy.Validate();
        return policy;
    }
    internal void Validate()
    {
        var types = Enum.GetValues<MissionRollType>().Where(t => t != MissionRollType.Unknown).Order().ToArray();
        if (OfferLifetimeSeconds <= 0 || AcceptedLifetimeSeconds <= 0 || MinimumRollFee <= 0 || FeePerCharacterLevel <= 0
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

internal sealed class PolicyProvenance { public string SourceRevision { get; init; } = string.Empty; public ImmutableArray<string> SourcePaths { get; init; } = []; public string Basis { get; init; } = string.Empty; }
internal sealed class RollTypeRule { public MissionRollType Type { get; init; } public int Icon { get; init; } public int Action { get; init; } public int ObjectiveInteraction { get; init; } public string Title { get; init; } = string.Empty; }
internal sealed class SliderEvidenceRule { public string Name { get; init; } = string.Empty; public ImmutableArray<int> Values { get; init; } = []; public ImmutableArray<string> Preference { get; init; } = []; }
internal sealed class RollTravelPolicy
{
    public int DistinctPlayfieldsAboveLevel { get; init; }
    public int DistinctAttempts { get; init; }
    public int MinimumDistanceStartsAboveLevel { get; init; }
    public double MinimumDistancePerLevel { get; init; }
    public double MinimumDistanceOffset { get; init; }
    public double MinimumDistanceFloor { get; init; }
    public double MaximumDistancePerLevel { get; init; }
    public double MaximumDistanceOffset { get; init; }
    public ImmutableArray<RollTravelBand> Bands { get; init; } = [];
}
internal sealed class RollTravelBand { public int MaximumLevel { get; init; } public ImmutableArray<string> Priority { get; init; } = []; }
internal sealed class RollRewardPolicy { public int NanoQualityTolerance { get; init; } public ImmutableArray<string> ExcludedNameFragments { get; init; } = []; public ImmutableArray<RollRewardFile> Catalogs { get; init; } = []; }
internal sealed class RollRewardFile { public string File { get; init; } = string.Empty; public bool FixedQuality { get; init; } }
