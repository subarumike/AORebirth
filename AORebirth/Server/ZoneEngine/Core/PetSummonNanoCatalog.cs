#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Requirements;
    using AORebirth.Enums;

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

        private static readonly Dictionary<int, string> PreferredPetHashByNano =
            new Dictionary<int, string>
            {
                { 125738, "MT01" },
                { 125743, "MT04" },
                { 125744, "MT03" },
                { 125745, "MT02" },
                { 125746, "BSLX" },
                { 43324, "PT50" },
                { 43733, "PT51" },
                { 43723, "PT52" },
                { 43734, "PT52" },
                { 43735, "PT53" },
                { 43732, "PT54" },
                { 43737, "PT56" },
            };

        private static readonly Dictionary<int, int> PreferredPetTypeByNano =
            new Dictionary<int, int>
            {
                { 125738, 14 },
                { 125743, 77 },
                { 125744, 55 },
                { 125745, 33 },
                { 125746, 192 },
                { 43324, 10 },
                { 43733, 32 },
                { 43723, 52 },
                { 43734, 62 },
                { 43735, 95 },
                { 43732, 137 },
                { 43737, 200 },
            };

        public static bool IsCatalogSummonNano(int nanoId)
        {
            return PreferredPetHashByNano.ContainsKey(nanoId);
        }

        public static bool TryResolve(ICharacter character, int nanoId, out PetSummonParams summonParams)
        {
            summonParams = null;
            NanoFormula nano;
            if (character == null || !NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            string preferredHash;
            if (PreferredPetHashByNano.TryGetValue(nanoId, out preferredHash))
            {
                List<PetSummonParams> candidates = CollectCandidates(nanoId, nano);
                PetSummonParams preferred = candidates.FirstOrDefault(
                    x => string.Equals(x.PetHash, preferredHash, StringComparison.OrdinalIgnoreCase));
                if (preferred == null)
                {
                    preferred = new PetSummonParams
                    {
                        NanoId = nanoId,
                        PetHash = preferredHash,
                        PetTypeId = ResolvePreferredPetType(nanoId),
                    };
                }

                if (string.IsNullOrWhiteSpace(PetMobTemplateResolver.Resolve(preferred.PetHash)))
                {
                    summonParams = preferred;
                    return false;
                }

                summonParams = preferred;
                return true;
            }

            List<PetSummonParams> catalogCandidates = CollectCandidates(nanoId, nano);
            if (catalogCandidates.Count == 0)
            {
                return false;
            }

            List<PetSummonParams> qualified = CollectQualifiedCandidates(character, nanoId, nano);
            List<PetSummonParams> selectionPool = qualified.Count > 0 ? qualified : catalogCandidates;
            PetSummonParams bestMatch = SelectBestCandidate(character, selectionPool);
            if (bestMatch == null)
            {
                return false;
            }

            summonParams = bestMatch;
            return true;
        }

        public static string GetPreferredPetHash(int nanoId)
        {
            string preferredHash;
            return PreferredPetHashByNano.TryGetValue(nanoId, out preferredHash)
                ? preferredHash
                : null;
        }

        public static string GetSummonNanoDisplayName(int nanoId)
        {
            string displayName;
            return SummonNanoDisplayName.TryGetValue(nanoId, out displayName)
                ? displayName
                : "Calling";
        }

        private static readonly Dictionary<int, string> SummonNanoDisplayName =
            new Dictionary<int, string>
            {
                { 125738, "Calling of Medinos" },
                { 125743, "Calling of Sanoo" },
                { 125744, "Calling of Valentyia" },
                { 125745, "Calling of Salvinous" },
                { 125746, "Calling of Belamorte" },
            };

        private static int ResolvePreferredPetType(int nanoId)
        {
            int petTypeId;
            return PreferredPetTypeByNano.TryGetValue(nanoId, out petTypeId)
                ? petTypeId
                : 1;
        }

        private static List<PetSummonParams> CollectCandidates(int nanoId, NanoFormula nano)
        {
            var candidates = new List<PetSummonParams>();
            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions.Where(IsSummonFunction))
                {
                    PetSummonParams candidate = BuildSummonParams(nanoId, function);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        private static List<PetSummonParams> CollectQualifiedCandidates(
            ICharacter character,
            int nanoId,
            NanoFormula nano)
        {
            var qualified = new List<PetSummonParams>();
            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions.Where(IsSummonFunction))
                {
                    PetSummonParams candidate = BuildSummonParams(nanoId, function);
                    if (candidate == null || !FunctionRequirementsPass(character, function))
                    {
                        continue;
                    }

                    qualified.Add(candidate);
                }
            }

            return qualified;
        }

        private static PetSummonParams SelectBestCandidate(
            ICharacter character,
            List<PetSummonParams> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            int ownerLevel = character.Stats[StatIds.level].Value;
            List<PetSummonParams> resolvable = candidates
                .Where(x => !string.IsNullOrWhiteSpace(PetMobTemplateResolver.Resolve(x.PetHash)))
                .ToList();

            if (resolvable.Count == 0)
            {
                return null;
            }

            List<PetSummonParams> pool = resolvable;
            List<PetSummonParams> withinLevel = pool
                .Where(x => x.PetTypeId <= ownerLevel)
                .ToList();

            if (withinLevel.Count > 0)
            {
                int bestType = withinLevel.Max(x => x.PetTypeId);
                return withinLevel.Last(x => x.PetTypeId == bestType);
            }

            return pool.OrderByDescending(x => x.PetTypeId).First();
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
