namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    internal static class CapturedEnemyCombatPacketFactory
    {
        internal static WeaponItemFullUpdateMessage CreateWeaponDefinition(
            Identity owner,
            int playfieldId,
            Identity weaponIdentity,
            CapturedEnemyWeaponDefinition definition,
            int? currentEnergy = null)
        {
            if (definition == null || !definition.IsValid)
            {
                throw new InvalidOperationException(
                    "A complete capture-backed WeaponItemFullUpdate definition is required.");
            }

            return new WeaponItemFullUpdateMessage
            {
                Identity = weaponIdentity,
                Unknown = definition.N3Unknown,
                Unknown1 = definition.Unknown1,
                Owner = owner,
                PlayfieldId = playfieldId,
                StateMachine = new Identity
                {
                    Type = (IdentityType)definition.StateMachineType,
                    Instance = definition.StateMachineInstance
                },
                Unknown2 = definition.Unknown2,
                Stats = definition.Stats.Select(
                    value => new GameTuple<CharacterStat, uint>
                    {
                        Value1 = value.Stat,
                        Value2 = value.Stat == CharacterStat.Energy && currentEnergy.HasValue
                                     ? unchecked((uint)currentEnergy.Value)
                                     : value.Value
                    }).ToArray(),
                Unknown3 = definition.Unknown3
            };
        }

        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemySpecialAttackSequenceDefinition sequence)
        {
            if (sequence == null || !sequence.IsValid)
            {
                throw new InvalidOperationException("A complete captured special-attack sequence is required.");
            }

            return CreateSpecialAttackWeapon(
                attacker,
                sequence.SpecialAttacks,
                sequence.SpecialAttackWeaponN3Unknown,
                sequence.SpecialAttackWeaponUnknown1,
                sequence.SpecialAttackWeaponUnknown2,
                sequence.SpecialAttackWeaponUnknown3,
                sequence.SpecialAttackWeaponUnknown4,
                sequence.SpecialAttackWeaponUnknown5);
        }

        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemyParallelAttackSequenceDefinition sequence)
        {
            if (sequence == null || !sequence.IsValid)
            {
                throw new InvalidOperationException("A complete captured parallel-attack sequence is required.");
            }

            return CreateSpecialAttackWeapon(
                attacker,
                sequence.SpecialAttacks,
                sequence.SpecialAttackWeaponN3Unknown,
                sequence.SpecialAttackWeaponUnknown1,
                sequence.SpecialAttackWeaponUnknown2,
                sequence.SpecialAttackWeaponUnknown3,
                sequence.SpecialAttackWeaponUnknown4,
                sequence.SpecialAttackWeaponUnknown5);
        }

        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemyCombatContract contract)
        {
            if (contract == null
                || !contract.IsCombatReady
                || !contract.HasEmptySpecialAttackWeaponContext)
            {
                throw new InvalidOperationException("A complete captured empty attack-start context is required.");
            }

            return CreateSpecialAttackWeapon(
                attacker,
                new CapturedEnemySpecialAttackDefinition[0],
                contract.SpecialAttackWeaponN3Unknown,
                contract.SpecialAttackWeaponUnknown1,
                contract.SpecialAttackWeaponUnknown2,
                contract.SpecialAttackWeaponUnknown3,
                contract.SpecialAttackWeaponUnknown4,
                contract.SpecialAttackWeaponUnknown5);
        }

        internal static AttackMessage CreateAttack(
            Identity attacker,
            Identity target,
            CapturedEnemySpecialAttackSequenceDefinition sequence)
        {
            if (sequence == null || !sequence.IsValid)
            {
                throw new InvalidOperationException("A complete captured special-attack sequence is required.");
            }

            return CreateAttack(attacker, target, sequence.AttackN3Unknown, sequence.AttackAction);
        }

        internal static AttackMessage CreateAttack(
            Identity attacker,
            Identity target,
            CapturedEnemyParallelAttackSequenceDefinition sequence)
        {
            if (sequence == null || !sequence.IsValid)
            {
                throw new InvalidOperationException("A complete captured parallel-attack sequence is required.");
            }

            return CreateAttack(attacker, target, sequence.AttackN3Unknown, sequence.AttackAction);
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

        internal static AttackInfoMessage CreateAttackInfo(
            Identity attacker,
            Identity target,
            int damage,
            int ammoCount,
            CapturedEnemyCombatAttackDefinition attack)
        {
            if (attack == null || !attack.IsValid || !attack.SendAttackInfo)
            {
                throw new InvalidOperationException("A complete captured AttackInfo definition is required.");
            }

            return CreateAttackInfo(
                attacker,
                target,
                damage,
                ammoCount,
                attack.AttackInfoWeaponSlot,
                attack.AttackInfoUnknown,
                attack.AttackInfoHitType,
                attack.AttackInfoWeaponInstance,
                attack.AttackInfoN3Unknown);
        }

        internal static AttackInfoMessage CreateAttackInfo(
            Identity attacker,
            Identity target,
            int damage,
            int ammoCount,
            int weaponSlot,
            int attackInfoUnknown,
            int hitTypeWireValue,
            int weaponInstance,
            byte n3Unknown)
        {
            return new AttackInfoMessage
            {
                Identity = attacker,
                Unknown = n3Unknown,
                Unknown1 = damage,
                Unknown2 = ammoCount,
                Unknown3 = weaponSlot,
                Target = target,
                Unknown4 = attackInfoUnknown,
                Unknown5 = hitTypeWireValue,
                Unknown6 = weaponInstance
            };
        }

        internal static SpecialAttackWeaponMessage CreateSpecialAttackWeapon(
            Identity attacker,
            CapturedEnemySpecialAttackDefinition[] definitions,
            byte n3Unknown,
            int unknown1,
            int unknown2,
            int unknown3,
            int unknown4,
            int unknown5)
        {
            return new SpecialAttackWeaponMessage
            {
                Identity = attacker,
                Unknown = n3Unknown,
                Specials = (definitions ?? new CapturedEnemySpecialAttackDefinition[0]).Select(
                    definition => new SpecialAttack
                    {
                        Unknown1 = definition.LowTemplate,
                        Unknown2 = definition.HighTemplate,
                        Unknown3 = definition.Tag,
                        Unknown4 = definition.Name
                    }).ToArray(),
                Unknown1 = unknown1,
                Unknown2 = unknown2,
                Unknown3 = unknown3,
                Unknown4 = unknown4,
                Unknown5 = unknown5
            };
        }

        internal static AttackMessage CreateAttack(
            Identity attacker,
            Identity target,
            byte n3Unknown,
            byte action)
        {
            return new AttackMessage
            {
                Identity = attacker,
                Unknown = n3Unknown,
                Target = target,
                Action = action
            };
        }
    }
}
