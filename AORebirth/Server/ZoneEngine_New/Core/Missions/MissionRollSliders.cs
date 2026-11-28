namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

/// <summary>Protocol slider values plus editable, capture-derived categorical evidence preferences.</summary>
internal sealed class MissionRollSliders
{
    readonly int[] _values;
    readonly SliderEvidenceRule _evidence;
    readonly MissionRollPolicy _policy;
    MissionRollSliders(int[] values, MissionRollPolicy policy)
    {
        _values = values;
        _policy = policy;
        var exact = policy.SliderProfiles.SingleOrDefault(p => p.Values.SequenceEqual(values));
        EvidenceProfile = exact?.Name ?? "Unresolved";
        _evidence = exact ?? policy.SliderProfiles.Single(p => p.Name == policy.DefaultSliderEvidence);
    }
    internal int GoodBad => _values[0];
    internal int OrderChaos => _values[1];
    internal int OpenHidden => _values[2];
    internal int PhysicalMystical => _values[3];
    internal int HeadOnStealth => _values[4];
    internal int MoneyExperience => _values[5];
    internal string EvidenceProfile { get; }
    internal static bool TryCreate(QuestAlternativeMessage? request, out MissionRollSliders profile, out string error, MissionRollPolicy? policy = null)
    {
        profile = null!;
        error = "Mission sliders must be signed-byte percentages from -100 through 100.";
        if (request == null) return false;
        byte[] wire = [request.GoodBadSlider, request.OrderChaosSlider, request.OpenHiddenSlider,
            request.PhysicalMysticalSlider, request.HeadOnStealthSlider, request.MoneyExperienceSlider];
        var values = wire.Select(value => (int)unchecked((sbyte)value)).ToArray();
        if (values.Any(value => !IsSupportedPercentage(value))) return false;
        profile = new(values, policy ?? MissionRollPolicy.Current);
        error = string.Empty;
        return true;
    }
    internal static bool IsSupportedPercentage(int value) => value is >= -100 and <= 100;
    internal bool Matches(params int[] values) => _values.SequenceEqual(values);
    internal int SemanticDistance(params int[] values)
    {
        string? category = _policy.SliderProfiles.SingleOrDefault(p => p.Values.SequenceEqual(values))?.Name;
        int rank = _evidence.Preference.IndexOf(category!);
        return rank < 0 ? _evidence.Preference.Length : rank;
    }
}
