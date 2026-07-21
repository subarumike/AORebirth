namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.SpecialHit (53196) — same argument shape as Hit for perk action damage.
    /// </summary>
    internal class specialhit : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SpecialHit;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            // Reuse Hit implementation via a temporary hit instance.
            return new hit().Execute(self, caller, target, arguments);
        }
    }
}
