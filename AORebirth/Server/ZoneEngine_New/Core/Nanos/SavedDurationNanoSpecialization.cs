namespace ZoneEngine_New.Core.Nanos;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;

/// <summary>Reads existing duration rows whose former effect execution has been retired.</summary>
public sealed class SavedDurationNanoSpecialization(NanoMechanicCatalog? mechanics = null) : INanoSpecialization
{
    private readonly NanoMechanicCatalog _mechanics = mechanics ?? NanoMechanicCatalog.LoadDefault();
    public bool Handles(int id) => _mechanics.TryGet(id, NanoMechanicKind.AreaTaunt, out _);
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(true, nano.DurationCentiseconds);
        return ReferenceEquals(caster, target) && Handles(nano.Id)
            && nano.HasValidPersistentFields && nano.DurationCentiseconds > 0;
    }
    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
