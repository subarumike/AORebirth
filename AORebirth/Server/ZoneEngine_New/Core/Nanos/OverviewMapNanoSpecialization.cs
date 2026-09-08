namespace ZoneEngine_New.Core.Nanos
{
    using System.Collections.Generic;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Captures 20260830-110744 and 20260830-124309: MapsC is governed by actual Overview NCU
    /// membership. The capture-backed map mask is not a permanent character unlock.
    /// </summary>
    public sealed class OverviewMapNanoSpecialization : INanoSpecialization, IActiveNanoProjection
    {
        public const int NanoId = 223767;
        public const int MapsMask = 403669119;

        public bool Handles(int nanoId) => nanoId == NanoId;
        public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
        {
            plan = new NanoSpecializationPlan(true, nano.DurationCentiseconds);
            return Handles(nano.Id) && ReferenceEquals(caster, target) && nano.DurationCentiseconds > 0;
        }
        public IReadOnlyDictionary<CharacterStat, int> Project(IReadOnlyList<ActiveNanoRecord> active)
            => new Dictionary<CharacterStat, int> { [CharacterStat.MapsC] = active.Any(a => a.NanoId == NanoId) ? MapsMask : 0 };
        public void SendProjection(Player player)
        {
            if (player.Session is not { State: SessionState.InPlay } session || !ReferenceEquals(session.Player, player)) return;
            session.Send(new StatMessage { Identity = player.Identity, Unknown = 0,
                Stats = [new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.MapsC,
                    Value2 = (uint)player.Stats.GetOrZero(CharacterStat.MapsC) }] });
        }
        public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
        public void Removed(Player target, int nanoId) { }
        public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
        public void Tick(Player player) { }
        public void Detached(Player player) { }
    }
}
