namespace ZoneEngine_New.Core.Nanos;
using System.Linq;
using AORebirth.Enums;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

/// <summary>Registers a duration without executing termination functions as OnUse effects.</summary>
public sealed class DurationOnlyNanoSpecialization(NanoMechanicCatalog? mechanics = null) : INanoSpecialization
{
    private readonly NanoMechanicCatalog _mechanics = mechanics ?? NanoMechanicCatalog.LoadDefault();
    public bool Handles(int nanoId) => _mechanics.TryGet(nanoId, NanoMechanicKind.DurationOnly, out _);
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(true, nano.DurationCentiseconds);
        if (!ReferenceEquals(caster, target) || !Handles(nano.Id) || nano.DurationCentiseconds <= 0
            || nano.NcuCost < 0 || nano.Template.SpellList.Count != 1
            || !nano.Template.SpellList.TryGetValue(EventType.OnTerminate, out var termination)) return false;
        return termination.All(spell => spell.FunctionType == (int)FunctionType.RemoveNanoStrain
            && spell.Target == (int)ItemTarget.Wearer && spell.Requirements.Count == 0
            && spell.TickCount is >= 0 and <= 1 && spell.TickInterval == 0 && spell.Arguments.Count == 1
            && ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int strain) && strain >= 0);
    }
    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
