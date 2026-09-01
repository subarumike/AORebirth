namespace AORebirth.Core.Combat
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    public static class CharacterEngageRules
    {
        private const int PvpFlaggedVisualFlagBit = 0x40;

        private const int DefaultSuppressionGasPercent = 75;

        public static bool IsPlayerCombatant(ICharacter character)
        {
            return character != null && character.Controller != null && character.Controller.Client != null;
        }

        /// <summary>
        /// Mirrors ZoneEngine PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat for player clients.
        /// </summary>
        public static bool CanEngagePlayerVersusPlayer(ICharacter attacker, ICharacter target)
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

        private static bool IsProtectedPlayerVersusPlayerTarget(ICharacter target)
        {
            return IsPlayerCombatant(target);
        }

        private static bool IsPlayerControlledCombatant(ICharacter character)
        {
            return IsPlayerCombatant(character);
        }

        private static bool IsPvpFlagged(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            return (character.Stats[StatIds.visualflags].Value & PvpFlaggedVisualFlagBit) != 0;
        }

        private static bool IsLowSuppressionGasZone(int suppressionGas)
        {
            return suppressionGas == 5 || suppressionGas == 25;
        }

        private static int ResolveSuppressionGas(ICharacter character)
        {
            Dynel dynel = character as Dynel;
            if (dynel?.Playfield == null || dynel.Playfield.Districts == null || dynel.Playfield.Districts.Count == 0)
            {
                return DefaultSuppressionGasPercent;
            }

            if (dynel.Playfield.Districts.Count == 1)
            {
                return dynel.Playfield.Districts[0].SuppressionGas;
            }

            int firstGas = dynel.Playfield.Districts[0].SuppressionGas;
            for (int index = 1; index < dynel.Playfield.Districts.Count; index++)
            {
                if (dynel.Playfield.Districts[index].SuppressionGas != firstGas)
                {
                    return DefaultSuppressionGasPercent;
                }
            }

            return firstGas;
        }
    }
}
