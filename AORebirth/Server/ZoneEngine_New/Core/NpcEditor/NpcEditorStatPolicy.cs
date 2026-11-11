namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;

internal static class NpcEditorStatPolicy
{
    const int MaxCombatStat = 100_000;
    const int MaxVital = 100_000_000;

    internal static readonly CharacterStat[] EditableStats =
    [
        CharacterStat.Health, CharacterStat.MaxHealth, CharacterStat.CurrentNano, CharacterStat.MaxNanoEnergy,
        CharacterStat.AMSModifier, CharacterStat.DMSModifier,
        CharacterStat.ProjectileAC, CharacterStat.MeleeAC, CharacterStat.EnergyAC, CharacterStat.ChemicalAC,
        CharacterStat.RadiationAC, CharacterStat.ColdAC, CharacterStat.PoisonAC, CharacterStat.FireAC,
        CharacterStat.DuckExp, CharacterStat.DodgeRanged, CharacterStat.EvadeClsC, CharacterStat.NanoAC,
        CharacterStat.ProjectileDamageModifier, CharacterStat.MeleeDamageModifier, CharacterStat.EnergyDamageModifier,
        CharacterStat.ChemicalDamageModifier, CharacterStat.RadiationDamageModifier, CharacterStat.ColdDamageModifier,
        CharacterStat.FireDamageModifier, CharacterStat.PoisonDamageModifier, CharacterStat.NanoDamageModifier,
        CharacterStat.BreedHostility, CharacterStat.ProximityRangeIndoors, CharacterStat.ProximityRangeOutdoors
    ];

    internal static bool Supports(CharacterStat stat) => Array.IndexOf(EditableStats, stat) >= 0;

    internal static bool IsBehaviorStat(CharacterStat stat)
        => stat is CharacterStat.BreedHostility or CharacterStat.ProximityRangeIndoors or CharacterStat.ProximityRangeOutdoors;

    internal static bool IsVital(CharacterStat stat)
        => stat is CharacterStat.Health or CharacterStat.MaxHealth or CharacterStat.CurrentNano or CharacterStat.MaxNanoEnergy;

    internal static CharacterStat? DependentVital(CharacterStat stat) => stat switch
    {
        CharacterStat.MaxHealth => CharacterStat.Health,
        CharacterStat.MaxNanoEnergy => CharacterStat.CurrentNano,
        _ => null
    };

    internal static bool AreRelated(CharacterStat first, CharacterStat second)
        => first == second || DependentVital(first) == second || DependentVital(second) == first;

    internal static bool IsAllowedValue(CharacterStat stat, int value)
    {
        if (!Supports(stat) || StatCollection.IsUnset(value))
            return false;
        return stat switch
        {
            CharacterStat.MaxHealth or CharacterStat.Health => value is >= 1 and <= MaxVital,
            CharacterStat.MaxNanoEnergy or CharacterStat.CurrentNano => value is >= 0 and <= MaxVital,
            CharacterStat.BreedHostility => value is >= 0 and <= 100,
            CharacterStat.ProximityRangeIndoors or CharacterStat.ProximityRangeOutdoors
                => value >= 0 && value <= NpcAiRules.MaxLeashRange,
            CharacterStat.AMSModifier or CharacterStat.DMSModifier or CharacterStat.ProjectileDamageModifier
                or CharacterStat.MeleeDamageModifier or CharacterStat.EnergyDamageModifier or CharacterStat.ChemicalDamageModifier
                or CharacterStat.RadiationDamageModifier or CharacterStat.ColdDamageModifier or CharacterStat.FireDamageModifier
                or CharacterStat.PoisonDamageModifier or CharacterStat.NanoDamageModifier => value is >= -MaxCombatStat and <= MaxCombatStat,
            _ => value is >= 0 and <= MaxCombatStat
        };
    }

    internal static int ResizeVital(int current, int oldMaximum, int maximum, int minimum)
    {
        long scaled = oldMaximum == 0 ? current : Character.ScaleVital(current, oldMaximum, maximum);
        return (int)Math.Clamp(scaled, minimum, maximum);
    }

    internal static bool ValidVitalPair(int current, int maximum, int minimum)
        => maximum >= minimum && current >= minimum && current <= maximum
            && !StatCollection.IsUnset(current) && !StatCollection.IsUnset(maximum);

    internal static bool TryApplyConfigured(
        Dictionary<int, int> stats, IReadOnlyDictionary<int, int> values, Dictionary<int, string>? sources, out string? error)
    {
        error = VitalPlan(CharacterStat.Health, CharacterStat.MaxHealth, CharacterStat.PercentRemainingHealth, 1, out var health);
        if (error != null)
            return false;
        error = VitalPlan(CharacterStat.CurrentNano, CharacterStat.MaxNanoEnergy, CharacterStat.PercentRemainingNano, 0, out var nano);
        if (error != null)
            return false;

        foreach ((int statId, int value) in values)
        {
            stats[statId] = value;
            if (sources != null)
                sources[statId] = "NPC editor template band override";
        }
        Apply(health);
        Apply(nano);
        return true;

        string? VitalPlan(CharacterStat currentStat, CharacterStat maxStat, CharacterStat percentStat, int minimum,
            out VitalOverride? plan)
        {
            plan = null;
            bool hasCurrent = values.TryGetValue((int)currentStat, out int nextCurrent);
            bool hasMaximum = values.TryGetValue((int)maxStat, out int nextMaximum);
            if (!hasCurrent && !hasMaximum)
                return null;

            bool authoredCurrent = stats.TryGetValue((int)currentStat, out int oldCurrent);
            bool authoredMaximum = stats.TryGetValue((int)maxStat, out int oldMaximum);
            if (!hasMaximum)
            {
                if (!authoredMaximum)
                    return $"The authored template has no {maxStat} value.";
                nextMaximum = oldMaximum;
            }
            if (!hasCurrent)
            {
                if (!authoredCurrent || !authoredMaximum || !ValidVitalPair(oldCurrent, oldMaximum, minimum)
                    || nextMaximum < minimum)
                    return $"The authored {currentStat}/{maxStat} pair cannot be safely resized.";
                nextCurrent = ResizeVital(oldCurrent, oldMaximum, nextMaximum, minimum);
            }
            if (!ValidVitalPair(nextCurrent, nextMaximum, minimum))
                return $"The configured {currentStat} must remain within {maxStat}.";

            plan = new(currentStat, percentStat, nextCurrent, nextMaximum, DerivedCurrent: !hasCurrent);
            return null;
        }

        void Apply(VitalOverride? plan)
        {
            if (plan == null)
                return;
            stats[(int)plan.Current] = plan.Value;
            stats[(int)plan.Percent] = Character.VitalPercent(plan.Value, plan.Maximum);
            if (sources != null)
            {
                if (plan.DerivedCurrent)
                    sources[(int)plan.Current] = "Derived from the template band maximum override";
                sources[(int)plan.Percent] = "Derived from the template band vital override";
            }
        }
    }

    sealed record VitalOverride(CharacterStat Current, CharacterStat Percent, int Value, int Maximum, bool DerivedCurrent);
}
