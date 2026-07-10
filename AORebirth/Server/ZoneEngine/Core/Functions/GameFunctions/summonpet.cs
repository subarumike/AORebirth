#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using ZoneEngine.Core;

    #endregion

    internal class summonpet : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get { return FunctionType.SummonPet; }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter owner = self as ICharacter;
            if (owner == null || arguments == null || arguments.Length < 2)
            {
                return false;
            }

            string petHash = arguments[0].AsString();
            int petTypeId = arguments[1].AsInt32();
            return PetRuntimeService.Default.SummonPet(owner, petHash, petTypeId);
        }
    }
}
