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

    internal class summonpets : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get { return FunctionType.SummonPets; }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter owner = self as ICharacter;
            if (owner == null)
            {
                return false;
            }

            // Capture 20260806-pet-warp: SummonPets [0] warps existing pets to the caster
            // (all pet professions share this nano — not a new summon).
            if (PetCommandService.IsPetWarpSummonPetsArguments(arguments))
            {
                return PetCommandService.WarpAllOwnedPetsToOwner(owner);
            }

            PetSummonParams summonParams;
            if (arguments != null && arguments.Length >= 2)
            {
                return PetRuntimeService.Default.SummonPet(
                    owner,
                    arguments[0].AsString(),
                    arguments[1].AsInt32());
            }

            foreach (var activeNano in owner.ActiveNanos.Values)
            {
                if (activeNano == null)
                {
                    continue;
                }

                if (PetSummonNanoCatalog.TryResolve(owner, activeNano.ID, out summonParams))
                {
                    return PetRuntimeService.Default.SummonPet(
                        owner,
                        summonParams.PetHash,
                        summonParams.PetTypeId);
                }
            }

            return false;
        }
    }
}
