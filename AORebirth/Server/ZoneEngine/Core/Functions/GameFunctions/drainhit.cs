namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.DrainHit (53185) — perk action drain hits reuse Hit for now.
    /// </summary>
    internal class drainhit : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.DrainHit;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            return new hit().Execute(self, caller, target, arguments);
        }
    }
}
