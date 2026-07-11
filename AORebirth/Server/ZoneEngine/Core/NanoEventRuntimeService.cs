#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;

    #endregion

    public sealed class NanoEventRuntimeService
    {
        private static readonly int SummonPetFunctionId = (int)FunctionType.SummonPet;
        private static readonly int SummonPetsFunctionId = (int)FunctionType.SummonPets;

        private static readonly NanoEventRuntimeService DefaultInstance = new NanoEventRuntimeService();

        private NanoEventRuntimeService()
        {
        }

        public static NanoEventRuntimeService Default
        {
            get { return DefaultInstance; }
        }

        public void ExecuteOnUseEvents(ICharacter character, NanoFormula nano)
        {
            if (character == null || nano == null || nano.Events == null)
            {
                return;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                nanoEvent.Perform(character, character);
            }
        }

        public bool HasSummonPetOnUse(int nanoId)
        {
            if (PetSummonNanoCatalog.IsCatalogSummonNano(nanoId))
            {
                return true;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            return this.HasSummonPetOnUse(nano);
        }

        public bool HasSummonPetOnUse(NanoFormula nano)
        {
            if (nano == null || nano.Events == null)
            {
                return false;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function.FunctionType == SummonPetFunctionId
                        || function.FunctionType == SummonPetsFunctionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
