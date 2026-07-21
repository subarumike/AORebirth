namespace ZoneEngine.Core.Functions.GameFunctions
{
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// FunctionType.TeamCastNano (53066). Perk actions use this for pet/team buffs
    /// (e.g. Soothing Spirits / Spirit of Blessing). Apply to owner's active pets, not only self.
    /// </summary>
    internal class teamcastnano : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.TeamCastNano;
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

            int nanoId = arguments[0].AsInt32();
            bool any = false;

            foreach (IInstancedEntity petTarget in ResolveTeamCastTargets(character, target))
            {
                if (castnano.ApplyInstantNano(character, petTarget, nanoId))
                {
                    any = true;
                }
            }

            return any;
        }

        private static IEnumerable<IInstancedEntity> ResolveTeamCastTargets(
            Character character,
            IInstancedEntity functionTarget)
        {
            var yielded = new HashSet<int>();

            // Prefer explicit non-self target from ItemTarget resolution.
            if (functionTarget != null
                && functionTarget.Identity.Instance != 0
                && functionTarget.Identity.Instance != character.Identity.Instance)
            {
                yielded.Add(functionTarget.Identity.Instance);
                yield return functionTarget;
            }

            // Owner pets (attack / heal / companion).
            int[] strains =
            {
                PetSlotClassifier.RegularPetStrain,
                PetSlotClassifier.HealingPetStrain,
                PetSlotClassifier.BureaucratCompanionStrain
            };
            foreach (int strain in strains)
            {
                ICharacter pet = PetRuntimeService.Default.GetActivePetInStrain(character, strain);
                if (pet == null || yielded.Contains(pet.Identity.Instance))
                {
                    continue;
                }

                yielded.Add(pet.Identity.Instance);
                yield return pet;
            }

            // If no pets and no other target, fall back to self (true self/team cast with empty pet list).
            if (yielded.Count == 0)
            {
                yield return character;
            }
        }
    }
}
