namespace ZoneEngine_New.Core.Nanos;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Network;

/// <summary>Projects editable bit-mask stat overlays only while their nano is active.</summary>
public sealed class ActiveStatOverlayNanoSpecialization(NanoMechanicCatalog? mechanics = null) : INanoSpecialization, IActiveNanoProjection
{
    private readonly NanoMechanicCatalog _mechanics = mechanics ?? NanoMechanicCatalog.LoadDefault();
    public bool Handles(int nanoId) => _mechanics.TryGet(nanoId, NanoMechanicKind.ActiveStatOverlay, out _);
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(true, nano.DurationCentiseconds);
        return Handles(nano.Id) && ReferenceEquals(caster, target) && nano.DurationCentiseconds > 0;
    }
    public IReadOnlyDictionary<CharacterStat, int> Project(IReadOnlyList<ActiveNanoRecord> active)
    {
        var result = _mechanics.Definitions.Where(d => d.Kind == NanoMechanicKind.ActiveStatOverlay)
            .SelectMany(d => d.Modifiers.Keys).Distinct().ToDictionary(stat => stat, _ => 0);
        foreach (var nano in active)
            if (_mechanics.TryGet(nano.NanoId, NanoMechanicKind.ActiveStatOverlay, out var definition))
                foreach (var stat in definition.Modifiers) result[stat.Key] |= stat.Value;
        return result;
    }
    public void SendProjection(Player player)
    {
        if (player.Session is not { State: SessionState.InPlay } session || !ReferenceEquals(session.Player, player)) return;
        session.Send(new StatMessage { Identity = player.Identity, Unknown = 0,
            Stats = _mechanics.Definitions.Where(d => d.Kind == NanoMechanicKind.ActiveStatOverlay)
                .SelectMany(d => d.Modifiers.Keys).Distinct().Select(stat => new GameTuple<CharacterStat, uint>
                { Value1 = stat, Value2 = (uint)player.Stats.GetOrZero(stat) }).ToArray() });
    }
    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
