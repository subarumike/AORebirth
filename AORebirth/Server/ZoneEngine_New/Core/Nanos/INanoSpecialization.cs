namespace ZoneEngine_New.Core.Nanos
{
    using ZoneEngine_New.Core.Entities;
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed record NanoSpecializationPlan(bool UsesActiveNano, int DurationCentiseconds)
    {
        public IReadOnlyDictionary<CharacterStat, int> Modifiers { get; init; } = new Dictionary<CharacterStat, int>();
    }

    public interface INanoOwnerProjection
    {
        void Refresh(Player player);
        void ReapplyAfterRebase(Player player);
        bool IsFightingRestricted(Player player);
    }

    public interface INanoActiveSetValidator
    {
        bool IsValidActiveSet(Player player, IReadOnlyList<ActiveNanoRecord> active);
    }

    /// <summary>
    /// Exact, catalog-specific behavior not interchangeable with ordinary buffs. Preparation is
    /// side-effect free. Applied is called once, on the owner tick, after the durable cast transition.
    /// For example the accepted Keeper aura has no default duration/NCU row, unlike a normal buff.
    /// </summary>
    public interface INanoSpecialization
    {
        bool Handles(int nanoId);
        bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan);
        void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active);
        void Removed(Player target, int nanoId);
        void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active);
        void Tick(Player player);
        void Detached(Player player);
    }
}
