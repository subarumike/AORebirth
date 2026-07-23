namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.ScalingModify (53175) — nano Advantage modifiers (Sparrow Flight runspeed/evade).
    /// Same runtime as Modify: add to Stat.Modifier.
    /// Capture 20260723-053632 Sparrow Flight OnUse.
    /// </summary>
    internal class scalingmodify : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.ScalingModify;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character affected = target as Character;
            if (affected == null)
            {
                affected = self as Character;
            }

            if (affected == null || arguments == null || arguments.Length < 2)
            {
                return false;
            }

            int statId = arguments[0].AsInt32();
            int amount = arguments[1].AsInt32();
            if (statId == (int)StatIds.cash)
            {
                return true;
            }

            affected.Stats[statId].Modifier += amount;
            AdventurerMorphFlightRuntime.NoteScalingModify(affected, statId, amount);
            return true;
        }
    }
}
