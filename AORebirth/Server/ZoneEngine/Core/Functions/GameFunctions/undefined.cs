namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    /// <summary>
    /// FunctionType.Undefined (53240) — Channel Rage style perk-tier wrapper.
    /// Arg0 = tier nano id (e.g. 227683). Nano OnUse uses ItemTarget.Target (pet) for
    /// CastNano/Modify; duration Attr8=720000 (2h). Do not apply to the caster.
    /// </summary>
    internal class undefined : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.Undefined;
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
            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return castnano.ApplyInstantNano(character, target, nanoId);
            }

            IInstancedEntity petTarget = ResolvePerkPetTarget(character, target);
            if (petTarget == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "Undefined/ChannelRage-style nano=" + nanoId + " needs a pet target");
                return false;
            }

            // So ItemTarget.Target (3) Modify/CastNano resolve to the pet.
            character.SetTarget(petTarget.Identity);
            return castnano.ApplyInstantNano(character, petTarget, nanoId);
        }

        /// <summary>
        /// Channel Rage description: pet damage buff. Prefer selected/fighting owned pet, else active attack pet.
        /// </summary>
        internal static IInstancedEntity ResolvePerkPetTarget(Character owner, IInstancedEntity functionTarget)
        {
            if (owner == null || owner.Playfield == null)
            {
                return null;
            }

            ICharacter candidate = functionTarget as ICharacter;
            if (IsOwnedPet(owner, candidate))
            {
                return candidate;
            }

            candidate = owner.Playfield.FindByIdentity<ICharacter>(owner.SelectedTarget);
            if (IsOwnedPet(owner, candidate))
            {
                return candidate;
            }

            candidate = owner.Playfield.FindByIdentity<ICharacter>(owner.FightingTarget);
            if (IsOwnedPet(owner, candidate))
            {
                return candidate;
            }

            candidate = PetRuntimeService.Default.GetActivePetInStrain(owner, PetSlotClassifier.RegularPetStrain);
            if (candidate != null)
            {
                return candidate;
            }

            candidate = PetRuntimeService.Default.GetActivePetInStrain(owner, PetSlotClassifier.HealingPetStrain);
            if (candidate != null)
            {
                return candidate;
            }

            return PetRuntimeService.Default.GetActivePetInStrain(
                owner,
                PetSlotClassifier.BureaucratCompanionStrain);
        }

        private static bool IsOwnedPet(Character owner, ICharacter candidate)
        {
            if (owner == null || candidate == null || candidate.Identity.Instance == owner.Identity.Instance)
            {
                return false;
            }

            return PetCombatRules.IsPlayerOwnedPet(candidate)
                   && PetCombatRules.ResolvePetOwner(candidate) != null
                   && PetCombatRules.ResolvePetOwner(candidate).Identity.Instance == owner.Identity.Instance;
        }
    }
}
