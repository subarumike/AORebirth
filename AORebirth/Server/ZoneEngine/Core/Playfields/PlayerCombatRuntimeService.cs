namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal sealed class PlayerCombatRuntimeService
    {
        internal void StartAttack(
            ICharacter character,
            Identity target,
            Action<Identity> resetCombatTick)
        {
            Require(resetCombatTick, "resetCombatTick");
            character.SetTarget(target);
            character.SetFightingTarget(target);
            resetCombatTick(character.Identity);
        }

        internal void CancelAttack(ICharacter character, Action<ICharacter> cancelAttack)
        {
            Require(cancelAttack, "cancelAttack");
            cancelAttack(character);
        }

        internal void ResetCombatTick(Identity attacker, Action<Identity> resetCombatTick)
        {
            Require(resetCombatTick, "resetCombatTick");
            resetCombatTick(attacker);
        }

        internal void ProcessCombatTick(ICharacter attacker, Action<ICharacter> processCombatTick)
        {
            Require(processCombatTick, "processCombatTick");
            processCombatTick(attacker);
        }

        internal void ClearFightingTarget(ICharacter character, Action<ICharacter> clearFightingTarget)
        {
            Require(clearFightingTarget, "clearFightingTarget");
            clearFightingTarget(character);
        }

        internal void BeginDeath(ICharacter target, Action<ICharacter> beginDeath)
        {
            Require(beginDeath, "beginDeath");
            beginDeath(target);
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
