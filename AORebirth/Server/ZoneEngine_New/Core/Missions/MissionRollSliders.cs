namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

/// <summary>Protocol slider values plus editable, capture-derived categorical evidence preferences.</summary>
internal sealed class MissionRollSliders
{
    readonly int[] _values;
    readonly SliderEvidenceRule _evidence;
    MissionRollSliders(int[] values)
    {
        _values = values;
        var policy = MissionRollPolicy.Current;
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
    internal static bool TryCreate(QuestAlternativeMessage? request, out MissionRollSliders profile, out string error)
    {
        profile = null!;
        error = "Mission sliders must be signed-byte percentages from -100 through 100.";
        if (request == null) return false;
        byte[] wire = [request.GoodBadSlider, request.OrderChaosSlider, request.OpenHiddenSlider,
            request.PhysicalMysticalSlider, request.HeadOnStealthSlider, request.MoneyExperienceSlider];
        var values = wire.Select(value => (int)unchecked((sbyte)value)).ToArray();
        if (values.Any(value => value is < -100 or > 100)) return false;
        profile = new(values);
        error = string.Empty;
        return true;
    }
    internal bool Matches(params int[] values) => _values.SequenceEqual(values);
    internal int SemanticDistance(params int[] values)
    {
        string? category = MissionRollPolicy.Current.SliderProfiles.SingleOrDefault(p => p.Values.SequenceEqual(values))?.Name;
        int rank = Array.IndexOf(_evidence.Preference, category);
        return rank < 0 ? _evidence.Preference.Length : rank;
    }
}
