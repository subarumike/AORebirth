namespace ZoneEngine_New.Core.Nanos
{
    using System.Linq;
    using AORebirth.Enums;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Exact Sparrow child: accepted CastNano executes OnUse only, then attempts this duration.
    /// Legacy active expiry does NOT dispatch OnTerminate. Retaining the real duration entry
    /// must not invent the catalog's otherwise unexecuted RemoveNanoStrain behavior.
    /// </summary>
    public sealed class SparrowChildNanoSpecialization : INanoSpecialization
    {
        public const int NanoId = 273292;
        private static readonly int[] DeclaredTerminationStrains = [251, 841, 252, 818, 247, 766, 224];
        public bool Handles(int nanoId) => nanoId == NanoId;
        public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
        {
            plan = new(true, nano.DurationCentiseconds);
            if (!ReferenceEquals(caster, target) || nano.Id != NanoId || nano.DurationCentiseconds != 1
                || nano.NcuCost != 999
                || nano.Template.SpellList.Count != 1
                || !nano.Template.SpellList.TryGetValue(EventType.OnTerminate, out var termination)
                || termination.Count != DeclaredTerminationStrains.Length) return false;
            // The packaged child has a Defend descriptor, but this exact self-scripted path
            // executes no OnUse effect. Legacy ApplyInstantNano does not resolve resistance;
            // rejecting it through the unrelated generic hostile-effect guard loses Sparrow.
            return termination.Select((spell, index) => spell.FunctionType == (int)FunctionType.RemoveNanoStrain
                && spell.Target == (int)ItemTarget.Wearer && spell.Requirements.Count == 0
                && spell.TickCount is >= 0 and <= 1 && spell.TickInterval == 0 && spell.Arguments.Count == 1
                && ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int strain)
                && strain == DeclaredTerminationStrains[index]).All(valid => valid);
        }
        public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
        public void Removed(Player target, int nanoId) { }
        public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
        public void Tick(Player player) { }
        public void Detached(Player player) { }
    }
}
