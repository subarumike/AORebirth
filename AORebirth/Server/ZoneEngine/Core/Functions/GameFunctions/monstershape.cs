namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.MonsterShape (53060) — Change Shape (Sparrow Flight parrot/Reet 30365).
    /// Capture 20260723-053632 / aogalaxy AOID 82835.
    /// </summary>
    internal class monstershape : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.MonsterShape;
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

            int shapeId = arguments[0].AsInt32();
            AdventurerMorphFlightRuntime.ApplyMonsterShape(character, shapeId);
            return true;
        }
    }
}
