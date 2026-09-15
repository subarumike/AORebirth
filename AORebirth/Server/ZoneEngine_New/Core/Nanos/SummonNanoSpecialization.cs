namespace ZoneEngine_New.Core.Nanos;
using System;
using System.IO;
using System.Linq;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

public interface INanoActionRequirements
{
    bool ActionRequirements(Player caster, Player target, NanoDefinition nano);
}

/// <summary>Validates a content-defined world summon against its ordinary SpawnMonster2 spell graph.</summary>
public sealed class SummonNanoSpecialization : INanoSpecialization, INanoCastContextSpecialization, INanoActionRequirements
{
    readonly WorldContentCatalog _content;
    public SummonNanoSpecialization(IGameData? data = null)
        => _content = data?.WorldContent ?? WorldContentCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData"));
    public SummonNanoSpecialization(WorldContentCatalog content) => _content = content;
    internal int NormalizeNanoId(int id) => _content.Summons.FirstOrDefault(x => x.Aliases.Contains(id))?.NanoId ?? id;
    internal bool IsUploaded(Player player, int id) => player.UploadedNanoIds.Contains(id)
        || (_content.Summons.FirstOrDefault(x => x.NanoId == id)?.Aliases.Any(player.UploadedNanoIds.Contains) ?? false);
    public bool Handles(int nanoId) => _content.Summons.Any(x => x.NanoId == nanoId);
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(false, 0);
        var rule = _content.Summons.FirstOrDefault(x => x.NanoId == nano.Id);
        if (rule == null || !ReferenceEquals(caster, target) || nano.DurationCentiseconds != 0
            || nano.Template.Defend.Count != 0 || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count != 1) return false;
        var spell = spells[0];
        return spell.FunctionType == (int)FunctionType.SpawnMonster2 && spell.Target == (int)ItemTarget.Wearer
            && spell.TickCount == 1 && spell.TickInterval == 0 && spell.Requirements.Count == 0
            && spell.Arguments.Count == 3 && spell.Arguments[0] is string hash && hash == rule.Hash
            && ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int level) && level == rule.Level
            && ItemUseFunctions.TryReadInt(spell.Arguments, 2, out int lifetime) && lifetime == rule.LifetimeSeconds;
    }
    public bool ActionRequirements(Player caster, Player target, NanoDefinition nano)
    {
        if (!TryPrepare(caster, target, nano, out _)) return false;
        var rule = _content.Summons.Single(x => x.NanoId == nano.Id);
        if (!rule.IgnoreActionRequirements) return NanoEffectPlan.ActionRequirements(caster, target, nano);
        if (nano.Template.Actions.Count != rule.RequiredActions.Length) return false;
        return nano.Template.Actions.Zip(rule.RequiredActions).All(pair => pair.First.ActionType == pair.Second.ActionType
            && pair.First.Requirements.Count == pair.Second.Requirements.Count
            && pair.First.Requirements.Zip(pair.Second.Requirements).All(r => r.First.ChildOperator == r.Second.ChildOperator
                && r.First.Operator == r.Second.Operator && r.First.StatNumber == r.Second.StatNumber
                && r.First.Target == r.Second.Target && r.First.Value == r.Second.Value));
    }
    bool INanoCastContextSpecialization.TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
        Func<bool> casterStillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        return TryPrepare(caster, caster, nano, out _) && caster.Playfield is { } world
            && world.GetRequiredService<SummonService>().TryPrepare(caster, _content.Summons.Single(x => x.NanoId == nano.Id), casterStillCurrent, out afterCommit);
    }
    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active)
        => throw new InvalidOperationException("A world summon cannot be restored as a persisted buff.");
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
