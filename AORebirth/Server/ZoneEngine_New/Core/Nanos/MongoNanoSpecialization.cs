namespace ZoneEngine_New.Core.Nanos;

using System;
using System.Linq;
using System.Threading;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;

/// <summary>Exact accepted 100198 -> 100194 graph, not a general area/NPC nano interpreter.</summary>
public sealed class MongoNanoSpecialization(INanoCatalog catalog) : INanoSpecialization, INanoCastContextSpecialization
{
    public bool Handles(int nanoId) => nanoId == 100198;

    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(true, 2000) { InitialResourceDeltas = new System.Collections.Generic.Dictionary<CharacterStat, int>
            { [CharacterStat.Health] = 12 } };
        if (!Handles(nano.Id) || !ReferenceEquals(caster, target) || nano.DurationCentiseconds != 2000
            || nano.NcuCost != 0 || nano.Template.Defend.Count != 0
            || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count != 3
            || !catalog.TryGet(100194, out var child) || child.DurationCentiseconds != 0
            || child.Template.Defend.Count != 0 || !ChildMatches(child)) return false;
        var heal = spells[0]; var text = spells[1]; var area = spells[2];
        // Events.Perform executes each function once. Its tick metadata does not
        // create a periodic scheduler. HeadText has no registered Legacy handler.
        return Shape(heal, FunctionType.Hit, 1, 10, 200, 4) && Int(heal, 0, 27)
            && Int(heal, 1, 12) && Int(heal, 2, 12) && Int(heal, 3, 0)
            && Shape(text, FunctionType.HeadText, 1, 1, 0, 2)
            && text.Arguments[0] is string name && name == "Mongo Slam!" && Int(text, 1, 2)
            && Shape(area, FunctionType.AreaCastNano, 1, 1, 0, 2) && Int(area, 0, 100194) && Int(area, 1, 20);
    }

    private static bool Shape(ItemSpell spell, FunctionType function, int target, int ticks, int interval, int arguments)
        => spell.FunctionType == (int)function && spell.Target == target && spell.TickCount == ticks
            && spell.TickInterval == interval && spell.Arguments.Count == arguments && spell.Requirements.Count == 0;
    private static bool Int(ItemSpell spell, int index, int value)
        => ItemUseFunctions.TryReadInt(spell.Arguments, index, out int actual) && actual == value;
    private static bool Requirement(ItemRequirement value, int link, int op, int target, int amount)
        => value.ChildOperator == link && value.Operator == op && value.StatNumber == 129
            && value.Target == target && value.Value == amount;
    private static bool ChildMatches(NanoDefinition child)
    {
        if (!child.Template.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count != 3) return false;
        for (int i = 0; i < spells.Count; i++)
            if (spells[i].FunctionType != (int)FunctionType.TauntNpc || spells[i].Target != 3
                || spells[i].TickCount != 1 || spells[i].TickInterval != 0
                || spells[i].Arguments.Count != 1 || !Int(spells[i], 0, 2000 + i * 1000)) return false;
        return spells[0].Requirements.Count == 1 && Requirement(spells[0].Requirements[0], 3, 1, 100, 50)
            && spells[1].Requirements.Count == 2 && Requirement(spells[1].Requirements[0], 4, 1, 100, 150)
            && Requirement(spells[1].Requirements[1], 4, 2, 19, 49)
            && spells[2].Requirements.Count == 1 && Requirement(spells[2].Requirements[0], 3, 2, 100, 149);
    }

    bool INanoCastContextSpecialization.TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
        Func<bool> casterStillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        if (!TryPrepare(caster, caster, nano, out _) || caster.Playfield is not { } world) return false;
        var registry = world.GetRequiredService<DynelRegistry>();
        // Recipient selection is at completion, like Legacy OnUse, and 2D like
        // PlayfieldDynelRegistry.FindCharactersInRange. No implicit LOS/PvP policy.
        bool Eligible(NpcCharacter npc) => npc.AcceptsPlayerCombatNanos && npc.Shop == null && !npc.IsDead
            && npc.Stats.GetOrZero(CharacterStat.Health) > 0 && ReferenceEquals(npc.Playfield, world)
            && registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc)
            && double.IsFinite(npc.Position.x) && double.IsFinite(npc.Position.z)
            && double.IsFinite(caster.Position.x) && double.IsFinite(caster.Position.z)
            && Math.Pow(npc.Position.x - caster.Position.x, 2) + Math.Pow(npc.Position.z - caster.Position.z, 2) <= 400;
        var recipients = registry.Dynels().OfType<NpcCharacter>().Where(Eligible).ToArray();
        int published = 0;
        afterCommit = () =>
        {
            if (Interlocked.Exchange(ref published, 1) != 0 || !casterStillCurrent() || caster.IsDead
                || !ReferenceEquals(caster.Playfield, world)) return;
            foreach (var npc in recipients)
            {
                if (!casterStillCurrent() || caster.IsDead || !Eligible(npc)) continue;
                // The three accepted skill branches choose taunt magnitude only.
                // Each invokes the same one-point engage hit and forced target.
                // Do not reinterpret the magnitude as HP damage or add a HoT.
                caster.Cell?.Announce(new CastNanoSpellMessage { Identity = caster.Identity, Caster = caster.Identity,
                    Target = npc.Identity, NanoId = 100194, Unknown = 0, Unknown1 = 0 });
                caster.Session?.Send(new CharacterActionMessage { Identity = caster.Identity,
                    Action = CharacterActionType.FinishNanoCasting, Target = Identity.None, Parameter1 = 1, Parameter2 = 100194 });
                npc.ApplyDamage(caster, 1, HitType.Normal);
                if (!npc.IsDead) npc.StartFighting(caster.Identity, 0);
                npc.FlushDirtyStats();
            }
        };
        return true;
    }

    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
