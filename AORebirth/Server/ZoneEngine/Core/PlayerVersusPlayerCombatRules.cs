#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using ZoneEngine.Core.Controllers;

    using PlayfieldsCatalog = ZoneEngine.Core.Playfields.Playfields;

    #endregion

    /// <summary>
    /// Blocks player-vs-player and player-vs-pet combat outside 5%/25% suppression gas unless flagged.
    /// </summary>
    internal static class PlayerVersusPlayerCombatRules
    {
        private const int PvpFlaggedVisualFlagBit = 0x40;

        private const int DefaultSuppressionGasPercent = 75;

        internal static bool IsPlayerCharacter(ICharacter character)
        {
            return character != null && character.Controller is PlayerController;
        }

        internal static bool IsPlayerOwnedPetTarget(ICharacter character)
        {
            return PetCombatRules.IsPlayerOwnedPet(character);
        }

        internal static bool IsProtectedPlayerVersusPlayerTarget(ICharacter target)
        {
            return IsPlayerCharacter(target) || IsPlayerOwnedPetTarget(target);
        }

        internal static bool IsPlayerControlledCombatant(ICharacter character)
        {
            return IsPlayerCharacter(character) || PetCombatRules.IsPlayerOwnedPet(character);
        }

        internal static bool CanEngagePlayerVersusPlayerCombat(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            if (!IsPlayerControlledCombatant(attacker)
                || !IsProtectedPlayerVersusPlayerTarget(target))
            {
                return true;
            }

            int attackerGas = ResolveSuppressionGas(attacker);
            int targetGas = ResolveSuppressionGas(target);
            if (IsLowSuppressionGasZone(attackerGas) || IsLowSuppressionGasZone(targetGas))
            {
                return true;
            }

            return IsPvpFlagged(attacker) || IsPvpFlagged(target);
        }

        internal static bool IsPvpFlagged(ICharacter character)
        {
            ICharacter subject = ResolveAuthorizationSubject(character);
            if (subject == null)
            {
                return false;
            }

            return (subject.Stats[StatIds.visualflags].Value & PvpFlaggedVisualFlagBit) != 0;
        }

        internal static int ResolveSuppressionGas(ICharacter character)
        {
            ICharacter subject = ResolveAuthorizationSubject(character);
            if (subject == null || subject.Playfield == null)
            {
                return DefaultSuppressionGasPercent;
            }

            return PlayfieldsCatalog.ResolveSuppressionGasPercent(subject.Playfield.Identity.Instance);
        }

        private static bool IsLowSuppressionGasZone(int suppressionGas)
        {
            return suppressionGas == 5 || suppressionGas == 25;
        }

        private static ICharacter ResolveAuthorizationSubject(ICharacter character)
        {
            if (character == null)
            {
                return null;
            }

            if (PetCombatRules.IsPlayerOwnedPet(character))
            {
                ICharacter owner = PetCombatRules.ResolvePetOwner(character);
                if (owner != null)
                {
                    return owner;
                }
            }

            return character;
        }
    }
}
