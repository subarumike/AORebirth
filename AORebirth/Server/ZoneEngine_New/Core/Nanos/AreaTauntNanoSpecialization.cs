namespace ZoneEngine_New.Core.Nanos;

using System;
using System.Collections.Generic;
using ZoneEngine_New.Core.GameData;
using System.Linq;
using System.Threading;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;

/// <summary>Reusable area taunt mechanics; parent effects and child identity come from loaded nano data.</summary>
public sealed class AreaTauntNanoSpecialization(INanoCatalog catalog, NanoMechanicCatalog? mechanics = null) : INanoSpecialization, INanoCastContextSpecialization
{
    private readonly NanoMechanicCatalog _mechanics = mechanics ?? NanoMechanicCatalog.LoadDefault();
    public bool Handles(int nanoId) => _mechanics.TryGet(nanoId, NanoMechanicKind.AreaTaunt, out _);

    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(true, nano.DurationCentiseconds);
        if (!ReferenceEquals(caster, target) || !TryDescribe(caster, nano, out _, out int heal, out _, out _)) return false;
        plan = plan with { InitialResourceDeltas = new Dictionary<CharacterStat, int> { [CharacterStat.Health] = heal } };
        return true;
    }

    private bool TryDescribe(Player caster, NanoDefinition nano, out int childId, out int heal, out int radius,
        out IReadOnlyList<ItemSpell> childSpells)
    {
        childId = heal = radius = 0;
        childSpells = [];
        if (!Handles(nano.Id) || nano.DurationCentiseconds <= 0 || nano.Template.Defend.Count != 0
            || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells)) return false;
        bool hasArea = false, hasHeal = false;
        foreach (var spell in spells)
        {
            if (spell.Requirements.Count != 0) return false;
            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.Hit:
                    if (hasHeal || spell.Target != (int)ItemTarget.User || spell.Arguments.Count != 4
                        || spell.TickCount < 1 || spell.TickInterval < 0 || spell.TickCount > 1 && spell.TickInterval == 0
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int stat) || stat != (int)CharacterStat.Health
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out heal) || heal < 0
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 2, out int maximum) || maximum != heal
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 3, out int mode) || mode != 0) return false;
                    hasHeal = true; break;
                case FunctionType.HeadText:
                    if (spell.Target != (int)ItemTarget.User || spell.TickCount != 1 || spell.TickInterval != 0
                        || spell.Arguments.Count != 2 || spell.Arguments[0] is not string
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out _)) return false;
                    break;
                case FunctionType.AreaCastNano:
                    if (hasArea || spell.Target != (int)ItemTarget.User || spell.Arguments.Count != 2 || spell.TickCount != 1 || spell.TickInterval != 0
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out childId) || childId <= 0
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out radius) || radius <= 0) return false;
                    hasArea = true; break;
                default: return false;
            }
        }
        if (!hasArea || !hasHeal || !catalog.TryGet(childId, out var child) || child.DurationCentiseconds != 0
            || child.Template.Defend.Count != 0 || !child.Template.SpellList.TryGetValue(EventType.OnUse, out var taunts)
            || taunts.Count == 0) return false;
        childSpells = taunts;
        return taunts.All(spell => spell.FunctionType == (int)FunctionType.TauntNpc && spell.Target == (int)ItemTarget.Target
            && spell.Arguments.Count == 1
            && spell.TickCount == 1 && spell.TickInterval == 0
            && ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int magnitude) && magnitude >= 0
            && NanoRequirements.TryEvent(caster, caster, spell.Requirements, out _));
    }

    bool INanoCastContextSpecialization.TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
        Func<bool> casterStillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        if (!TryPrepare(caster, caster, nano, out _) || !TryDescribe(caster, nano, out int childId, out _, out int radius, out var taunts)
            || caster.Playfield is not { } world) return false;
        var registry = world.GetRequiredService<DynelRegistry>();
        // Recipient selection is at completion, like Legacy OnUse, and 2D like
        // PlayfieldDynelRegistry.FindCharactersInRange. No implicit LOS/PvP policy.
        bool Eligible(NpcCharacter npc) => npc.AcceptsPlayerCombatNanos && npc.Shop == null && !npc.IsDead
            && npc.Stats.GetOrZero(CharacterStat.Health) > 0 && ReferenceEquals(npc.Playfield, world)
            && registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc)
            && double.IsFinite(npc.Position.x) && double.IsFinite(npc.Position.z)
            && double.IsFinite(caster.Position.x) && double.IsFinite(caster.Position.z)
            && Math.Pow(npc.Position.x - caster.Position.x, 2) + Math.Pow(npc.Position.z - caster.Position.z, 2) <= (double)radius * radius
            && taunts.Any(spell => NanoRequirements.TryEvent(caster, npc, spell.Requirements, out bool met) && met);
        var recipients = registry.Dynels().OfType<NpcCharacter>().Where(Eligible).ToArray();
        int published = 0;
        afterCommit = () =>
        {
            if (Interlocked.Exchange(ref published, 1) != 0 || !casterStillCurrent() || caster.IsDead
                || !ReferenceEquals(caster.Playfield, world)) return;
            foreach (var npc in recipients)
            {
                if (!casterStillCurrent() || caster.IsDead || !Eligible(npc)) continue;
                // An eligible taunt branch invokes the existing one-point engage hit
                // and forced target. Branch requirements are evaluated on current actors.
                // Do not reinterpret the magnitude as HP damage or add a HoT.
                caster.Cell?.Announce(new CastNanoSpellMessage { Identity = caster.Identity, Caster = caster.Identity,
                    Target = npc.Identity, NanoId = childId, Unknown = 0, Unknown1 = 0 });
                caster.Session?.Send(new CharacterActionMessage { Identity = caster.Identity,
                    Action = CharacterActionType.FinishNanoCasting, Target = Identity.None, Parameter1 = 1, Parameter2 = childId });
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
