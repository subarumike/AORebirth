namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    /// <summary>
    /// FunctionType.ChangeVariable (53110) — vehicle OnWear sets MonsterScale (360).
    /// Capture 20260723-133842 yalm 117322 OnWear args=[360,20].
    /// </summary>
    internal class changevariable : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.ChangeVariable;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length < 2)
            {
                return false;
            }

            int statId = arguments[0].AsInt32();
            int value = arguments[1].AsInt32();
            if (statId == (int)StatIds.monsterscale)
            {
                VehicleHudWearRuntime.SetMonsterScale(character, value);
                return true;
            }

            try
            {
                character.Stats[statId].Value = value;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
