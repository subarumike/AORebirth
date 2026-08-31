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

        internal void CancelAttack(ICharacter character, Action<Identity> resetCombatTick)
        {
            Require(resetCombatTick, "resetCombatTick");
            character.SetFightingTarget(Identity.None);
            resetCombatTick(character.Identity);
        }

        internal void ResetCombatTick(Identity attacker, Action<Identity> resetCombatTick)
        {
            Require(resetCombatTick, "resetCombatTick");
            resetCombatTick(attacker);
        }

        internal void ProcessCombatTick(
            ICharacter attacker,
            WeaponSlot preferredSlot,
            Action<Identity> clearCombatTracking,
            Func<Identity, ICharacter> findTarget,
            Func<ICharacter, bool> isValidTarget,
            Action<ICharacter, ICharacter> logInvalidTarget,
            Action<ICharacter, ICharacter, WeaponSlot> processValidatedCombatTick)
        {
            Require(clearCombatTracking, "clearCombatTracking");
            Require(findTarget, "findTarget");
            Require(isValidTarget, "isValidTarget");
            Require(logInvalidTarget, "logInvalidTarget");
            Require(processValidatedCombatTick, "processValidatedCombatTick");

            if (attacker.FightingTarget.Instance == 0)
            {
                clearCombatTracking(attacker.Identity);
                return;
            }

            ICharacter target = findTarget(attacker.FightingTarget);
            if (!isValidTarget(target))
            {
                this.ClearInvalidCombatTarget(attacker, target, logInvalidTarget, clearCombatTracking);
                return;
            }

            processValidatedCombatTick(attacker, target, preferredSlot);
        }

        internal void ClearInvalidCombatTarget(
            ICharacter attacker,
            ICharacter target,
            Action<ICharacter, ICharacter> logInvalidTarget,
            Action<Identity> clearCombatTracking)
        {
            Require(logInvalidTarget, "logInvalidTarget");
            Require(clearCombatTracking, "clearCombatTracking");

            logInvalidTarget(attacker, target);
            this.ClearFightingTarget(attacker, clearCombatTracking);
        }

        internal void ClearFightingTarget(ICharacter character, Action<Identity> clearCombatTracking)
        {
            Require(clearCombatTracking, "clearCombatTracking");
            character.SetFightingTarget(Identity.None);
            clearCombatTracking(character.Identity);
        }

        internal void BeginDeath(ICharacter target, Action<ICharacter> beginDeath)
        {
            Require(beginDeath, "beginDeath");
            beginDeath(target);
        }

        internal void CleanupDeathCombat(
            ICharacter target,
            Action<Identity> clearCombatTracking,
            Action<Identity> stopFightingDeadTarget,
            Action<ICharacter> sendCombatStop)
        {
            Require(clearCombatTracking, "clearCombatTracking");
            Require(stopFightingDeadTarget, "stopFightingDeadTarget");
            Require(sendCombatStop, "sendCombatStop");

            target.SetTarget(Identity.None);
            this.ClearFightingTarget(target, clearCombatTracking);
            stopFightingDeadTarget(target.Identity);
            sendCombatStop(target);
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
