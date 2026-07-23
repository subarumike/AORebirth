namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.RestrictAction (53068) — Sparrow Flight args=[2] (no fighting).
    /// Capture 20260723-053632.
    /// </summary>
    internal class restrictaction : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.RestrictAction;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            AdventurerMorphFlightRuntime.RestrictActions(character, arguments[0].AsInt32());
            return true;
        }
    }
}
