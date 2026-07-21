namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    using ZoneEngine.Core;

    /// <summary>
    /// FunctionType.ReduceNanoStrainDuration (53177) — used by some perk actions.
    /// Removes active nano in the given strain when present.
    /// </summary>
    internal class reducenanostrainduration : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.ReduceNanoStrainDuration;
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

            int strain = arguments[0].AsInt32();
            ActiveNanoRuntimeService.Default.RemoveActiveNanoInStrain(character, strain, true);
            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                "ReduceNanoStrainDuration char=" + character.Identity + " strain=" + strain);
            return true;
        }
    }
}
