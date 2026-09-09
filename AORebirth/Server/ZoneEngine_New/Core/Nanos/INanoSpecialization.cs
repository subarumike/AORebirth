namespace ZoneEngine_New.Core.Nanos
{
    using ZoneEngine_New.Core.Entities;
    using System;
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed record NanoSpecializationPlan(bool UsesActiveNano, int DurationCentiseconds)
    {
        public IReadOnlyDictionary<CharacterStat, int> Modifiers { get; init; } = new Dictionary<CharacterStat, int>();
        public IReadOnlyList<int> ScriptedChildren { get; init; } = System.Array.Empty<int>();
        // Cast-time only. Restoring an active row must never replay these hits.
        public IReadOnlyDictionary<CharacterStat, int> InitialResourceDeltas { get; init; } = new Dictionary<CharacterStat, int>();
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
    /// Exact remote actions whose durable recipient is the caster, not a foreign playfield's
    /// character aggregate. Preparation has no effects; its one-shot publication follows commit.
    /// The supplied cancellation fence is transport-safe and must not read foreign owner stats.
    /// </summary>
    internal interface INanoCastContextSpecialization
    {
        bool TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
            Func<bool> casterStillCurrent, out Action afterCommit);
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
