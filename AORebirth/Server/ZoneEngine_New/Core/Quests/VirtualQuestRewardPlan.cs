namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

/// <summary>Plans the virtual OnUse functions whose entire durable effect is a self stat write.</summary>
internal static class VirtualQuestRewardPlan
{
    internal static bool TryAdd(Item item, Player player, Dictionary<CharacterStat, int> values, out Action? publish)
    {
        publish = null;
        if (!item.Definition.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count == 0)
            return true;

        var stats = new Dictionary<CharacterStat, int>();
        var effects = new List<Publication>();
        foreach (ItemSpell spell in spells)
        {
            if (!IsSupported(spell, out CharacterStat stat))
                return false;

            if (!ItemTemplate.MeetsRequirements(spell.Requirements, FullStat))
                continue;

            if (spell.Is(FunctionType.SystemText) || spell.Is(FunctionType.Text))
            {
                effects.Add(new Publication(default, 0, spell));
                continue;
            }

            spell.TryReadInt(1, out int operand);
            int current = BaseStat(stat);
            int next = (FunctionType)spell.FunctionType switch
            {
                FunctionType.Set => operand,
                FunctionType.Hit => unchecked(current + operand),
                FunctionType.SetFlag => current | (1 << operand),
                FunctionType.ClearFlag => current & ~(1 << operand),
                FunctionType.ToggleFlag => current ^ (1 << operand),
                _ => throw new InvalidOperationException("The virtual reward function was not validated.")
            };
            stats[stat] = next;
            effects.Add(new Publication(stat, next, null));
        }

        foreach (var pair in stats)
            values[pair.Key] = pair.Value;
        if (effects.Count > 0)
        {
            publish = () =>
            {
                foreach (var effect in effects)
                {
                    if (effect.Text is { } text)
                        ItemUseFunctions.SystemText(player, text);
                    else
                        player.Stats.Set(effect.Stat, effect.Value, StatDetail.Base, dirty: true);
                }
            };
        }
        return true;

        bool IsProjected(CharacterStat stat, out int value)
            => stats.TryGetValue(stat, out value) || values.TryGetValue(stat, out value);

        int BaseStat(CharacterStat stat) => IsProjected(stat, out int projected)
            ? StatCollection.Normalize(projected)
            : player.Stats.GetOrZero(stat, StatDetail.Base);

        int FullStat(CharacterStat stat) => IsProjected(stat, out int projected)
            ? unchecked(projected + player.Stats.GetOrZero(stat, StatDetail.Bonus))
            : player.Stats.Get(stat);
    }

    static bool IsSupported(ItemSpell spell, out CharacterStat stat)
    {
        stat = default;
        if ((ItemTarget)spell.Target is not (ItemTarget.User or ItemTarget.Wearer or ItemTarget.Self or ItemTarget.Target)
            || spell.TickCount > 1 || spell.TickInterval != 0)
            return false;
        foreach (ItemRequirement criterion in spell.Requirements)
        {
            if (!IsSupportedCriterion(criterion))
                return false;
        }

        if (spell.Is(FunctionType.Text) || spell.Is(FunctionType.SystemText))
            return true;
        if (!spell.TryReadInt(0, out int id) || !spell.TryReadInt(1, out int operand))
            return false;

        stat = (CharacterStat)id;
        if (MissionBits.IsBitStat(stat))
        {
            return spell.Is(FunctionType.Set)
                || (spell.Is(FunctionType.SetFlag) || spell.Is(FunctionType.ClearFlag) || spell.Is(FunctionType.ToggleFlag))
                && operand is >= 0 and < 32;
        }

        if (stat == CharacterStat.BattlestationRep && spell.Is(FunctionType.Set))
            return true;
        // MetaType is the existing Omni token counter on a character. XP and AlienXP fixed hits are flat
        // Base-stat writes. The level-percentage form (a positive minimum with zero maximum) is a different mechanic.
        return stat is CharacterStat.AlignmentClanTokens or CharacterStat.MetaType or CharacterStat.XP or CharacterStat.AlienXP
            && spell.Is(FunctionType.Hit) && spell.TryReadInt(2, out int maximum) && operand == maximum;
    }

    static bool IsSupportedCriterion(ItemRequirement criterion)
    {
        if (ItemTemplate.IsRequirementLinkOperator(criterion) || ItemTemplate.IsSubjectSelector(criterion))
            return true;

        var stat = (CharacterStat)criterion.StatNumber;
        return (stat is CharacterStat.Side or CharacterStat.Level or CharacterStat.AlienLevel || MissionBits.IsBitStat(stat))
            && (Operator)criterion.Operator is Operator.EqualTo or Operator.Unequal or Operator.GreaterThan
                or Operator.LessThan or Operator.BitAnd or Operator.NotBitAnd;
    }

    readonly record struct Publication(CharacterStat Stat, int Value, ItemSpell? Text);
}
