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
    using AORebirth.Core.Requirements;
    using AORebirth.Enums;

    using MsgPack;

    #endregion

    internal sealed class PetSummonParams
    {
        public int NanoId { get; set; }

        public string PetHash { get; set; }

        public int PetTypeId { get; set; }
    }

    internal static class PetSummonNanoCatalog
    {
        private static readonly int SummonPetFunctionId = (int)FunctionType.SummonPet;
        private static readonly int SummonPetsFunctionId = (int)FunctionType.SummonPets;

        public static bool TryResolve(ICharacter character, int nanoId, out PetSummonParams summonParams)
        {
            summonParams = null;
            NanoFormula nano;
            if (character == null || !NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            PetSummonParams bestMatch = null;
            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions.Where(IsSummonFunction))
                {
                    PetSummonParams candidate = BuildSummonParams(nanoId, function);
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (!FunctionRequirementsPass(character, function))
                    {
                        continue;
                    }

                    if (bestMatch == null || candidate.PetTypeId > bestMatch.PetTypeId)
                    {
                        bestMatch = candidate;
                    }
                }
            }

            if (bestMatch == null)
            {
                foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
                {
                    if (nanoEvent.Functions == null)
                    {
                        continue;
                    }

                    foreach (Function function in nanoEvent.Functions.Where(IsSummonFunction))
                    {
                        PetSummonParams candidate = BuildSummonParams(nanoId, function);
                        if (candidate == null)
                        {
                            continue;
                        }

                        if (bestMatch == null || candidate.PetTypeId < bestMatch.PetTypeId)
                        {
                            bestMatch = candidate;
                        }
                    }
                }
            }

            if (bestMatch == null)
            {
                return false;
            }

            summonParams = bestMatch;
            return true;
        }

        private static bool IsSummonFunction(Function function)
        {
            return function.FunctionType == SummonPetFunctionId
                || function.FunctionType == SummonPetsFunctionId;
        }

        private static PetSummonParams BuildSummonParams(int nanoId, Function function)
        {
            if (function == null || function.Arguments == null || function.Arguments.Values.Count < 2)
            {
                return null;
            }

            string petHash = function.Arguments.Values[0].AsString();
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return null;
            }

            return new PetSummonParams
            {
                NanoId = nanoId,
                PetHash = petHash,
                PetTypeId = function.Arguments.Values[1].AsInt32()
            };
        }

        private static bool FunctionRequirementsPass(ICharacter character, Function function)
        {
            if (function.Requirements == null || function.Requirements.Count == 0)
            {
                return true;
            }

            bool result = true;
            foreach (Requirement requirement in function.Requirements)
            {
                result &= requirement.CheckRequirement(character);
                if (!result)
                {
                    break;
                }
            }

            return result;
        }
    }
}
