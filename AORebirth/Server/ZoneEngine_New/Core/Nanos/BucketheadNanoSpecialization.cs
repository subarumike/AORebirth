namespace ZoneEngine_New.Core.Nanos;

using System;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

/// <summary>Only the accepted 300439 SpawnMonster2 graph; never a generic NPC caster.</summary>
public sealed class BucketheadNanoSpecialization : INanoSpecialization, INanoCastContextSpecialization
{
    // Existing Legacy compatibility for already persisted crystal IDs. This does
    // not upload a new program, modify stored rows, or relax any other nano's gate.
    internal static int NormalizeNanoId(int id) => id == 300440 ? 300439 : id;
    internal static bool IsUploaded(Player player, int id) => player.UploadedNanoIds.Contains(id)
        || (id == 300439 && player.UploadedNanoIds.Contains(300440));
    public bool Handles(int nanoId) => nanoId == 300439;
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(false, 0);
        if (!Handles(nano.Id) || !ReferenceEquals(caster, target) || nano.DurationCentiseconds != 0
            || nano.Template.Defend.Count != 0 || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells)
            || spells.Count != 1) return false;
        var spell = spells[0];
        return spell.FunctionType == (int)FunctionType.SpawnMonster2 && spell.Target == (int)ItemTarget.Wearer
            && spell.TickCount == 1 && spell.TickInterval == 0 && spell.Requirements.Count == 0
            && spell.Arguments.Count == 3 && spell.Arguments[0] is string hash && hash == "BKTH"
            && ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int level) && level == 220
            && ItemUseFunctions.TryReadInt(spell.Arguments, 2, out int lifetime) && lifetime == 600;
    }
    internal static bool ActionRequirements(Player caster, Player target, NanoDefinition nano)
    {
        // PlayerController.CastNano's dedicated Buckethead branch does not evaluate
        // its action criteria. Preserve that exact accepted compatibility
        // path, not a general interpretation/bypass for unknown requirement operators.
        // In particular, do not invent PlayfieldType=0 when its nonpersistent stat
        // is absent. Only the unchanged catalog declaration is admitted here.
        if (!new BucketheadNanoSpecialization().TryPrepare(caster, target, nano, out _)
            || nano.Template.Actions.Count != 1) return false;
        var action = nano.Template.Actions[0];
        if (action.ActionType != (int)ActionType.ToUse || action.Requirements.Count != 2) return false;
        var marker = action.Requirements[0]; var restriction = action.Requirements[1];
        return marker.ChildOperator == (int)Operator.And && marker.Operator == 136
            && marker.StatNumber == 0 && marker.Target == (int)ItemTarget.Self && marker.Value == 2
            && restriction.ChildOperator == (int)Operator.And && restriction.Operator == (int)Operator.NotBitAnd
            && restriction.StatNumber == (int)CharacterStat.PlayfieldType
            && restriction.Target == (int)ItemTarget.Self && restriction.Value == 2;
    }
    bool INanoCastContextSpecialization.TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
        Func<bool> casterStillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        return TryPrepare(caster, caster, nano, out _) && caster.Playfield is { } world
            && world.GetRequiredService<BucketheadSummonService>().TryPrepare(caster, casterStillCurrent, out afterCommit);
    }
    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active)
        => throw new InvalidOperationException("Buckethead is a world summon, not a persisted buff.");
    public void Tick(Player player) { } // Source-playfield service owns lifetime even when the owner leaves.
    public void Detached(Player player) { }
}
