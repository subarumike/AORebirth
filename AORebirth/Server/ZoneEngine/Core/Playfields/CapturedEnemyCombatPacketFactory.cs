namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    internal static partial class CapturedEnemyCombatPacketFactory
    {
        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemyCombatContract contract)
        {
            return CreateSpecialAttackWeapon(
                attacker,
                contract,
                contract == null ? 0 : contract.SpecialAttackWeaponUnknown5);
        }

        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemyCombatContract contract,
            int aggDef)
        {
            if (contract == null
                || !contract.IsCombatReady
                || !contract.HasCapturedSpecialAttackWeaponContext)
            {
                throw new InvalidOperationException("A complete captured attack-start context is required.");
            }

            return CreateSpecialAttackWeapon(
                attacker,
                contract.CapturedSpecialAttacks,
                contract.SpecialAttackWeaponN3Unknown,
                contract.SpecialAttackWeaponUnknown1,
                contract.SpecialAttackWeaponUnknown2,
                contract.SpecialAttackWeaponUnknown3,
                contract.SpecialAttackWeaponUnknown4,
                aggDef);
        }

        internal static AttackMessage CreateAttack(
            Identity attacker,
            Identity target,
            CapturedEnemyCombatContract contract)
        {
            if (contract == null || !contract.IsCombatReady || !contract.HasCapturedAttackStartContext)
            {
                throw new InvalidOperationException("A complete captured attack-start context is required.");
            }

            return CreateAttack(attacker, target, contract.AttackN3Unknown, contract.AttackAction);
        }

    }
}
