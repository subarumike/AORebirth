namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.CanFly (53138) — Enable Flight when not in Shadowlands.
    /// Requirement on nano: expansionplayfield (531) == 0.
    /// Capture 20260723-053632 Sparrow Flight; aogalaxy "Enable Flight if Inside Shadowlands == 0".
    /// </summary>
    internal class canfly : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.CanFly;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null)
            {
                return false;
            }

            // Event.Perform already gated requirements; still refuse SL playfields.
            if (character.Stats[StatIds.expansionplayfield].Value != 0)
            {
                return true;
            }

            AdventurerMorphFlightRuntime.EnableFlight(character);
            return true;
        }
    }
}
