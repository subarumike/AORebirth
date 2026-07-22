namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    internal enum CapturedEnemyAttackModel
    {
        Unresolved,
        FixedAttackInfo,
        EquippedWeapon,
        Specialized
    }

    internal sealed class CapturedEnemyCombatAttackDefinition
    {
        internal CapturedEnemyCombatAttackDefinition(
            int minDamage,
            int maxDamage,
            int damageBonus,
            double range,
            double rechargeSeconds,
            bool usesEquippedWeapon,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoHitType,
            int attackInfoWeaponInstance,
            bool sendAttackInfo)
        {
            this.MinDamage = minDamage;
            this.MaxDamage = maxDamage;
            this.DamageBonus = damageBonus;
            this.Range = range;
            this.RechargeSeconds = rechargeSeconds;
            this.UsesEquippedWeapon = usesEquippedWeapon;
            this.AttackInfoAmmoCount = attackInfoAmmoCount;
            this.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            this.AttackInfoUnknown = attackInfoUnknown;
            this.AttackInfoHitType = attackInfoHitType;
            this.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            this.SendAttackInfo = sendAttackInfo;
        }

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal int DamageBonus { get; private set; }

        internal double Range { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal bool UsesEquippedWeapon { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoHitType { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal bool SendAttackInfo { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.MinDamage > 0
                       && this.MaxDamage >= this.MinDamage
                       && this.Range > 0
                       && this.RechargeSeconds > 0;
            }
        }
    }

    internal sealed class CapturedEnemySpecialAttackDefinition
    {
        internal CapturedEnemySpecialAttackDefinition(
            int lowTemplate,
            int highTemplate,
            int tag,
            string name)
        {
            this.LowTemplate = lowTemplate;
            this.HighTemplate = highTemplate;
            this.Tag = tag;
            this.Name = name;
        }

        internal int LowTemplate { get; private set; }

        internal int HighTemplate { get; private set; }

        internal int Tag { get; private set; }

        internal string Name { get; private set; }
    }

    internal sealed class CapturedEnemySpecialAttackSequenceDefinition
    {
        internal CapturedEnemySpecialAttackSequenceDefinition(
            double initialAttackDelaySeconds,
            CapturedEnemyCombatAttackDefinition openingAttack,
            CapturedEnemyCombatAttackDefinition repeatingAttack,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5)
        {
            this.InitialAttackDelaySeconds = initialAttackDelaySeconds;
            this.OpeningAttack = openingAttack;
            this.RepeatingAttack = repeatingAttack;
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
        }

        internal double InitialAttackDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition OpeningAttack { get; private set; }

        internal CapturedEnemyCombatAttackDefinition RepeatingAttack { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.InitialAttackDelaySeconds >= 0
                       && (this.OpeningAttack == null || this.OpeningAttack.IsValid)
                       && this.RepeatingAttack != null
                       && this.RepeatingAttack.IsValid;
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackStreamDefinition
    {
        internal CapturedEnemyParallelAttackStreamDefinition(
            double initialDelaySeconds,
            CapturedEnemyCombatAttackDefinition attack)
        {
            this.InitialDelaySeconds = initialDelaySeconds;
            this.Attack = attack;
        }

        internal double InitialDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition Attack { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.InitialDelaySeconds >= 0
                       && this.Attack != null
                       && this.Attack.IsValid;
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackSequenceDefinition
    {
        internal CapturedEnemyParallelAttackSequenceDefinition(
            CapturedEnemyParallelAttackStreamDefinition[] streams,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5)
        {
            this.Streams = streams ?? new CapturedEnemyParallelAttackStreamDefinition[0];
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
        }

        internal CapturedEnemyParallelAttackStreamDefinition[] Streams { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal bool IsValid
        {
            get
            {
                if (this.Streams.Length == 0)
                {
                    return false;
                }

                foreach (CapturedEnemyParallelAttackStreamDefinition stream in this.Streams)
                {
                    if (stream == null || !stream.IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    internal sealed class CapturedEnemyCombatContract
    {
        private CapturedEnemyCombatContract()
        {
        }

        internal string Evidence { get; private set; }

        internal bool Retaliates { get; private set; }

        internal NpcAiProfile AiProfile { get; private set; }

        internal CapturedEnemyAttackModel AttackModel { get; private set; }

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal int WeaponLowId { get; private set; }

        internal int WeaponHighId { get; private set; }

        internal int WeaponQuality { get; private set; }

        internal int WeaponInventorySlot { get; private set; }

        internal bool HasEmptySpecialAttackWeaponContext { get; private set; }

        internal bool HasCapturedAttackStartContext { get; private set; }

        internal bool HasCapturedEquippedAttackInfo { get; private set; }

        internal bool HasCapturedCombatStopSequence { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal double AttackStartDelaySeconds { get; private set; }

        internal double MovementTransitionDelaySeconds { get; private set; }

        internal double FirstHitDelaySeconds { get; private set; }

        internal bool SendStopFightOnDeath { get; private set; }

        internal bool RequiresDamageLineOfSight { get; private set; }

        internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; private set; }

        internal CapturedEnemyParallelAttackSequenceDefinition ParallelAttackSequence { get; private set; }

        internal bool IsCombatReady
        {
            get
            {
                if (!this.Retaliates)
                {
                    return false;
                }

                switch (this.AttackModel)
                {
                    case CapturedEnemyAttackModel.FixedAttackInfo:
                        return this.MinDamage > 0 && this.MaxDamage >= this.MinDamage;
                    case CapturedEnemyAttackModel.EquippedWeapon:
                        return this.WeaponLowId > 0
                               && this.WeaponHighId > 0
                               && this.WeaponQuality > 0
                               && this.WeaponInventorySlot > 0;
                    case CapturedEnemyAttackModel.Specialized:
                        return (this.SpecialAttackSequence != null
                                && this.SpecialAttackSequence.IsValid)
                               || (this.ParallelAttackSequence != null
                                   && this.ParallelAttackSequence.IsValid);
                    default:
                        return false;
                }
            }
        }

        internal static CapturedEnemyCombatContract FixedAttack(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance,
            int attackInfoAmmoCount = 0)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance
            };
        }

        /// <summary>
        /// Fixed damage + attack-on-sight (mission interiors).
        /// Enables AttackMessage start context so the client plays a real melee swing,
        /// and uses unarmed AttackInfo tags (not zeros) so hits are not "UNKNOWN damage".
        /// </summary>
        internal static CapturedEnemyCombatContract FixedAttackOnSight(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Aggressive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                // Capture 20260722-cap-mob-drop-cred: AmmoCount=-1 WeaponSlot=1 unarmed robot melee.
                AttackInfoAmmoCount = -1,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                HasCapturedAttackStartContext = true,
                HasEmptySpecialAttackWeaponContext = true
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeapon(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                WeaponLowId = lowId,
                WeaponHighId = highId,
                WeaponQuality = quality,
                WeaponInventorySlot = inventorySlot
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedAttackInfo(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoWeaponInstance,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasCapturedEquippedAttackInfo = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithEmptySpecialAttackContext(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int minDamage,
            int maxDamage,
            double attackStartDelaySeconds,
            double movementTransitionDelaySeconds,
            double firstHitDelaySeconds,
            double rechargeSeconds,
            bool sendStopFightOnDeath,
            int attackInfoAmmoCount,
            int attackInfoUnknown,
            int unknown1,
            int unknown2,
            int unknown3,
            int unknown4,
            int unknown5,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasEmptySpecialAttackWeaponContext = true;
            contract.HasCapturedAttackStartContext = true;
            contract.HasCapturedEquippedAttackInfo = true;
            contract.HasCapturedCombatStopSequence = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = inventorySlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = 0;
            contract.MinDamage = minDamage;
            contract.MaxDamage = maxDamage;
            contract.AttackStartDelaySeconds = attackStartDelaySeconds;
            contract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
            contract.FirstHitDelaySeconds = firstHitDelaySeconds;
            contract.RechargeSeconds = rechargeSeconds;
            contract.SendStopFightOnDeath = sendStopFightOnDeath;
            contract.SpecialAttackWeaponUnknown1 = unknown1;
            contract.SpecialAttackWeaponUnknown2 = unknown2;
            contract.SpecialAttackWeaponUnknown3 = unknown3;
            contract.SpecialAttackWeaponUnknown4 = unknown4;
            contract.SpecialAttackWeaponUnknown5 = unknown5;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract CapturedSpecialSequence(
            string evidence,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                SpecialAttackSequence = specialAttackSequence
            };
        }

        internal static CapturedEnemyCombatContract CapturedParallelAttackSequence(
            string evidence,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                ParallelAttackSequence = parallelAttackSequence,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract Unresolved(string evidence, bool retaliationObserved)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = retaliationObserved,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Unresolved
            };
        }
    }

    internal static class CapturedEnemyCombatRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedEnemyCombatContract> Contracts =
            new Dictionary<int, CapturedEnemyCombatContract>();

        internal static void Register(int serverInstance, CapturedEnemyCombatContract contract)
        {
            lock (Sync)
            {
                Contracts[serverInstance] = contract;
            }
        }

        internal static bool TryGet(int serverInstance, out CapturedEnemyCombatContract contract)
        {
            lock (Sync)
            {
                return Contracts.TryGetValue(serverInstance, out contract);
            }
        }

        internal static void Remove(int serverInstance)
        {
            lock (Sync)
            {
                Contracts.Remove(serverInstance);
            }
        }
    }

    internal static class CapturedEnemyCombatRuntime
    {
        private const int MissingItemStatValue = 1234567890;

        internal static bool Prepare(
            Character character,
            NPCController controller,
            CapturedEnemyCombatContract contract,
            out string failure)
        {
            failure = string.Empty;
            if (character == null || controller == null || contract == null)
            {
                failure = "character, controller, or combat contract is null";
                return false;
            }

            if (!contract.IsCombatReady)
            {
                CapturedEnemyCombatRuntimeRegistry.Register(character.Identity.Instance, contract);
                failure = "captured attack source is unresolved; evidence=" + contract.Evidence;
                return false;
            }

            controller.AiProfile = contract.AiProfile;
            if (contract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            {
                SetMobStat(character, StatIds.mindamage, contract.MinDamage);
                SetMobStat(character, StatIds.maxdamage, contract.MaxDamage);
                // Client combat text uses damagetype (91 = Melee). Leave 0 → "UNKNOWN damage".
                SetMobStat(character, StatIds.damagetype, (int)StatIds.meleeac);
                SetMobStat(character, StatIds.damageoverridetype, (int)StatIds.meleeac);
                SetMobStat(character, StatIds.defaultattacktype, 0);
                SetMobStat(character, StatIds.weapontype, 0);
            }
            else if (contract.AttackModel == CapturedEnemyAttackModel.EquippedWeapon
                     && !TryEquipCapturedWeapon(character, contract, out failure))
            {
                CapturedEnemyCombatRuntimeRegistry.Register(
                    character.Identity.Instance,
                    CapturedEnemyCombatContract.Unresolved(
                        contract.Evidence + "; runtime failure=" + failure,
                        contract.Retaliates));
                return false;
            }

            CapturedEnemyCombatRuntimeRegistry.Register(character.Identity.Instance, contract);
            return true;
        }

        private static bool TryEquipCapturedWeapon(
            Character character,
            CapturedEnemyCombatContract contract,
            out string failure)
        {
            failure = string.Empty;
            if (!ItemLoader.ItemList.ContainsKey(contract.WeaponLowId)
                || !ItemLoader.ItemList.ContainsKey(contract.WeaponHighId))
            {
                failure = string.Format(
                    "captured weapon template missing low={0} high={1}",
                    contract.WeaponLowId,
                    contract.WeaponHighId);
                return false;
            }

            IInventoryPage weaponPage;
            if (character.BaseInventory == null
                || !character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                failure = "weapon inventory page is unavailable";
                return false;
            }

            if (!weaponPage.ValidSlot(contract.WeaponInventorySlot)
                || weaponPage[contract.WeaponInventorySlot] != null)
            {
                failure = "captured weapon slot is invalid or occupied: " + contract.WeaponInventorySlot;
                return false;
            }

            var weapon = new Item(
                contract.WeaponQuality,
                contract.WeaponLowId,
                contract.WeaponHighId)
            {
                MultipleCount = 1
            };
            InventoryError result = weaponPage.Add(contract.WeaponInventorySlot, weapon);
            if (result != InventoryError.OK)
            {
                failure = "captured weapon add failed: " + result;
                return false;
            }

            if (contract.HasCapturedEquippedAttackInfo)
            {
                ApplyCapturedEquippedAttackDisplayStats(character, weapon);
            }

            return true;
        }

        private static void ApplyCapturedEquippedAttackDisplayStats(ICharacter character, IItem weapon)
        {
            ApplyWeaponStatIfPresent(character, weapon, StatIds.defaultattacktype);
            ApplyWeaponStatIfPresent(character, weapon, StatIds.damagetype);
            ApplyWeaponStatIfPresent(character, weapon, StatIds.weapontype);
        }

        private static void ApplyWeaponStatIfPresent(ICharacter character, IItem weapon, StatIds stat)
        {
            int value = weapon.GetAttribute((int)stat);
            if (value == MissingItemStatValue)
            {
                return;
            }

            SetMobStat(character, stat, value);
        }

        private static void SetMobStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        private const int DerangedShopperMonsterData = 203736;

        private const int DerangedShopperSourceInstance = unchecked((int)0x79574527);

        private const int IncompleteRebuildMonsterData = 203728;

        private const int FragmentedSoulMonsterData = 203729;

        private const int LooterMonsterData = 203745;

        private const int MuggerMonsterData = 203734;

        private const int RedundantScanMonsterData = 204178;

        private const int WorkmanStrikerMonsterData = 203854;

        private static readonly int[] MuggerSourceInstances =
        {
            unchecked((int)0x7953AA11),
            unchecked((int)0x7953AD6B),
            unchecked((int)0x795450D4),
            unchecked((int)0x795451FE),
            unchecked((int)0x79557F14),
            unchecked((int)0x7957E5C6),
            unchecked((int)0x7957E5C7),
            unchecked((int)0x7957E5C8),
            unchecked((int)0x7957E5CA)
        };

        private static readonly int[] IncompleteRebuildSourceInstances =
        {
            unchecked((int)0x79545170),
            unchecked((int)0x79545172),
            unchecked((int)0x79545177),
            unchecked((int)0x79545181),
            unchecked((int)0x79545188),
            unchecked((int)0x795451BC),
            unchecked((int)0x795451C1),
            unchecked((int)0x795451CB),
            unchecked((int)0x795451FD),
            unchecked((int)0x79545241)
        };

        private static readonly int[] RedundantScanSourceInstances =
        {
            unchecked((int)0x7953AF85),
            unchecked((int)0x795451BF),
            unchecked((int)0x795451C4),
            unchecked((int)0x795451D3)
        };

        private static readonly int[] FragmentedSoulSourceInstances =
        {
            unchecked((int)0x7954516A),
            unchecked((int)0x7954516F),
            unchecked((int)0x7954517A),
            unchecked((int)0x7954518A),
            unchecked((int)0x7954518B),
            unchecked((int)0x7954518E),
            unchecked((int)0x795451AA),
            unchecked((int)0x795451AE),
            unchecked((int)0x79545248),
            unchecked((int)0x79545367)
        };

        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            return For(name, monsterData, null);
        }

        internal static CapturedEnemyCombatContract For(string name, int monsterData, int? level)
        {
            switch (monsterData)
            {
                case 203726:
                    return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(
                        "20260709-222339 plus 20260717-214612/214751/215250: Eumenides owner-linked 123267/123268 weapons are observed at QL20 and QL17; runtime retains QL20 because the respawn selection rule is unresolved; initial empty-special context is 143/143/143/143/0, with two captured misses; immediate attack start, 0.233124-second movement transition, 5.199992-second first hit, 21 observed normal local-player hits 25..45, and 4.311321-second median interval across 17 intervals; weapon owns runtime damage and recharge",
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponQuality,
                        (int)WeaponSlots.Righthand,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponDamageMinimumOverride,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponDamageMaximumOverride,
                        NpcCombatAttackRules.CapturedSubwayEumenidesAttackStartDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayEumenidesMovementTransitionDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayEumenidesFirstHitDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayEumenidesRechargeOverrideSeconds,
                        false,
                        NpcCombatAttackRules.CapturedSubwayEumenidesInitialAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayEumenidesAttackInfoUnknown,
                        NpcCombatAttackRules.CapturedSubwayEumenidesSpecialAttackWeaponUnknown1,
                        NpcCombatAttackRules.CapturedSubwayEumenidesSpecialAttackWeaponUnknown2,
                        NpcCombatAttackRules.CapturedSubwayEumenidesSpecialAttackWeaponUnknown3,
                        NpcCombatAttackRules.CapturedSubwayEumenidesSpecialAttackWeaponUnknown4,
                        NpcCombatAttackRules.CapturedSubwayEumenidesSpecialAttackWeaponUnknown5,
                        requiresDamageLineOfSight: true);
                case 203748:
                    return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(
                        "20260712-232711/234401 and 20260720-053542: Vergil Aeneid QL23 Cast-Off E-Beamer 122123; 22-25 normal player damage with one captured 54 critical, captured attack-start/first-hit timing, and weapon-owned roll/cadence",
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponTemplate,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponTemplate,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponQuality,
                        (int)WeaponSlots.Righthand,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMinimumOverride,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMaximumOverride,
                        NpcCombatAttackRules.CapturedSubwayVergilAttackStartDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilMovementTransitionDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilFirstHitDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilRechargeOverrideSeconds,
                        true,
                        NpcCombatAttackRules.CapturedSubwayVergilInitialAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayVergilAttackInfoUnknown,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponLastValue,
                        requiresDamageLineOfSight: true);
                case 155962:
                    CapturedEnemyCombatAttackDefinition abmouthXopzAttack =
                        new CapturedEnemyCombatAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzMinimumDamage,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzMaximumDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            NpcCombatAttackRules.CapturedSubwayAbmouthAttackCycleSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzTag,
                            true);
                    CapturedEnemyCombatAttackDefinition abmouthDenwAttack =
                        new CapturedEnemyCombatAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwMinimumDamage,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwMaximumDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            NpcCombatAttackRules.CapturedSubwayAbmouthAttackCycleSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwTag,
                            true);
                    return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                        "20260712-224840/232137 and 20260720-053802: Abmouth XOPZ paired stream, DENW stream, captured SIW context, and one 21.8-second combat warp cast (nano 286237) that teleports the engaged player and owned pets to Abmouth",
                        new CapturedEnemyParallelAttackSequenceDefinition(
                            new[]
                            {
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzFirstInitialSeconds,
                                    abmouthXopzAttack),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwInitialSeconds,
                                    abmouthDenwAttack),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzSecondInitialSeconds,
                                    abmouthXopzAttack)
                            },
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwName)
                            },
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponLastValue));
                case 31909:
                    return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "20260712-224840/232137: Abmouth-owned Infector DMXF attacks, 21-26 player damage, and 3.7-second cadence",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorInitialAttackSeconds,
                            null,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorTag,
                                true),
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorName)
                            },
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponLastValue));
                case 17657:
                    return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "20260708-004038 and 20260709-193914: Filth Flea normal slot rolls with criticals excluded",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules.CapturedSubwayFilthFleaInitialAttackSeconds,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag,
                                true),
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag,
                                true),
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsName)
                            },
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponLastValue));
                case 17720:
                    return CapturedEnemyCombatContract.FixedAttack(
                        "20260708-143600 and 20260709-210452: 37 normal local-player Discarded Pet SIW1 hits span 9..18; four 30..33 criticals remain report-only; 30 same-source landed-hit intervals span 4.609299..5.950416 seconds with conventional median 5.089763; AttackInfo uses ammo -1, slot 0, unknown 0, and instance SIW1; raw SpecialAttackWeapon first four fields are exact by level while the varying fifth field remains unresolved and is not synthesized",
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetMinimumDamage,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetMaximumDamage,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetRechargeSeconds,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetWeaponSlot,
                        0,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetWeaponTag,
                        -1);
                case 17649:
                    return ForDisobedientBot(level);
                case 30379:
                    return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                        "20260709-222339 and 20260716-033326/034104: Bloodcreeper proactive dual Skinspider Bite/Spit natural attacks, 21-41 rolled damage, and independent captured hand cadence",
                        new CapturedEnemyParallelAttackSequenceDefinition(
                            new[]
                            {
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitInitialSeconds,
                                    new CapturedEnemyCombatAttackDefinition(
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitMinimumDamage,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitMaximumDamage,
                                        0,
                                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitRechargeSeconds,
                                        false,
                                        NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitWeaponSlot,
                                        0,
                                        NpcCombatAttackRules.NormalAttackInfoHitType,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitTag,
                                        true)),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteInitialSeconds,
                                    new CapturedEnemyCombatAttackDefinition(
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteMinimumDamage,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteMaximumDamage,
                                        0,
                                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteRechargeSeconds,
                                        false,
                                        NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteWeaponSlot,
                                        0,
                                        NpcCombatAttackRules.NormalAttackInfoHitType,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteTag,
                                        true))
                            },
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitTag,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteTag,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteName)
                            },
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponLastValue));
                case 203734:
                    return CapturedEnemyCombatContract.Unresolved(
                        "Mugger combat requires an exact captured source identity; aggregate weapon fallback is forbidden",
                        true);
                case 26092:
                    return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(
                        "20260711-170337 packets 301-654: Thief attack start, movement transition, three landed projectile hits, and six-second repeat cadence; 2026-07-12 private validation proved the weapon context renders projectile damage",
                        121567,
                        121567,
                        1,
                        (int)WeaponSlots.Righthand,
                        NpcCombatAttackRules.CapturedSubwayThiefWeaponDamageMinimumOverride,
                        NpcCombatAttackRules.CapturedSubwayThiefWeaponDamageMaximumOverride,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackStartDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefMovementTransitionDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefFirstHitDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefRechargeSeconds,
                        true,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoUnknown,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown1,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown2,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown3,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown4,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown5);
                case 203733:
                    return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "Official-live captures 20260719-010047 and 20260719-020104 prove repeated Violent Vagabond attack attempts, all misses, a 4.5802404-second corpus cadence, AttackInfo 0/6/0/0, and SpecialAttackWeapon 32/35/29/31/0. Landed damage is unavailable because the Vagabonds could not hit the test character, so the private-project playability policy uses the adjacent same-level Subway Mugger normal range of 9..12. QL1 template 130590 is Red Wine and remains excluded from combat.",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackSeconds,
                            null,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.PolicySubwayViolentVagabondMinimumDamage,
                                NpcCombatAttackRules.PolicySubwayViolentVagabondMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackSeconds,
                                false,
                                NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackInfoWeaponSlot,
                                NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackInfoUnknown,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayViolentVagabondAttackInfoWeaponInstance,
                                true),
                            new CapturedEnemySpecialAttackDefinition[0],
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondSpecialAttackWeaponUnknown1,
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondSpecialAttackWeaponUnknown2,
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondSpecialAttackWeaponUnknown3,
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondSpecialAttackWeaponUnknown4,
                            NpcCombatAttackRules.CapturedSubwayViolentVagabondSpecialAttackWeaponUnknown5));
                default:
                    return CapturedEnemyCombatContract.Unresolved(
                        "No captured combat contract for " + name + " monsterData=" + monsterData,
                        false);
            }
        }

        private static CapturedEnemyCombatContract ForDisobedientBot(int? level)
        {
            int specialAttackWeaponValue;
            int specialAttackWeaponLastValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotSpecialAttackWeaponLastValue;
            switch (level)
            {
                case 5:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponValue;
                    specialAttackWeaponLastValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponLastValue;
                    break;
                case 6:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel6SpecialAttackWeaponValue;
                    break;
                case 7:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel7SpecialAttackWeaponPolicyValue;
                    break;
                case 8:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel8SpecialAttackWeaponValue;
                    break;
                case 9:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel9SpecialAttackWeaponValue;
                    break;
                case 10:
                    specialAttackWeaponValue = NpcCombatAttackRules.CapturedSubwayDisobedientBotLevel10SpecialAttackWeaponValue;
                    break;
                default:
                    return CapturedEnemyCombatContract.Unresolved(
                        "Disobedient Bot SIW1 attack context is unresolved for level "
                        + (level.HasValue ? level.Value.ToString() : "unknown"),
                        true);
            }

            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260708-143600, 20260709-205921/210452/220439, 20260712-153918, 20260713-014714/033511, and 20260719-020104: 15 Disobedient Bot SIW1 normal local-player hits span 6-15 damage; three other-player hits and two player-owned Killer-pet hits remain separate; focused raw packets prove a 3.270444-second first hit and 5.973723-second repeat attempt cadence; SpecialAttackWeapon contexts are capture-backed for levels 5, 6, 8, 9, and 10, including the level-5 terminal value 22, with level 7 explicitly using the bounded 35/45 midpoint policy",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotInitialAttackSeconds,
                    null,
                    new CapturedEnemyCombatAttackDefinition(
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotMinimumDamage,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotMaximumDamage,
                        0,
                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotRechargeSeconds,
                        false,
                        NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponSlot,
                        0,
                        NpcCombatAttackRules.NormalAttackInfoHitType,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                        true),
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName)
                    },
                    specialAttackWeaponValue,
                    specialAttackWeaponValue,
                    specialAttackWeaponValue,
                    specialAttackWeaponValue,
                    specialAttackWeaponLastValue));
        }

        private static CapturedEnemyCombatContract ForWorkmanStriker(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            return CapturedEnemyCombatContract.Unresolved(
                string.Format(
                    "Workman Striker source 0x{0:X8} requires a selected capture-reviewed atomic generation variant",
                    sourceInstance),
                archetype != null && archetype.Combat != null && archetype.Combat.Observed);
        }

        private static CapturedEnemyCombatContract ForWorkmanStriker(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            bool hasExactCombatEvidence = combat != null && combat.Observed;
            bool hasExactGeneration = variant != null
                                      && weapon != null
                                      && generationEvidence != null
                                      && Array.Exists(
                                          generationEvidence,
                                          value => value != null
                                                   && value.MonsterData == WorkmanStrikerMonsterData
                                                   && value.SourceInstance == sourceInstance
                                                   && value.Level == variant.Level
                                                   && value.Health == variant.Health
                                                   && value.HealthDamage == variant.HealthDamage
                                                   && value.MonsterScale == variant.MonsterScale
                                                   && value.RunSpeed == variant.RunSpeed
                                                   && value.WeaponLowId == weapon.LowId
                                                   && value.WeaponHighId == weapon.HighId
                                                   && value.WeaponQuality == weapon.Quality);
            if (!hasExactCombatEvidence
                || !hasExactGeneration)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Workman Striker combat requires one exact reviewed atomic level/stat/weapon generation for the selected source.",
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Workman Striker source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; 59 distinct normal local-player hits span 9..23, seven criticals remain report-only, and captured AttackInfo uses ammo -1, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; captured SIW shapes remain report-only",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForLooter(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            return ForSourceSpecificWeaponArchetype(archetype, sourceInstance, "Looter");
        }

        private static CapturedEnemyCombatContract ForIncompleteRebuild(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 2
                                          && combat.MinDamage == 17
                                          && combat.MaxDamage == 35
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            if (!hasExactCombatEvidence
                || !HasCompleteIncompleteRebuildSourceWeaponEvidence(evidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Incomplete Rebuild combat requires the exact two normal 17..35 local-player hits and one owner-linked weapon tuple for each of the ten current sources",
                    combat != null && combat.Observed);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
            {
                if (candidate.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = candidate;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Incomplete Rebuild source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    true);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Incomplete Rebuild source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; no empty SIW or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                9,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForIncompleteRebuild(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 2
                                          && combat.MinDamage == 17
                                          && combat.MaxDamage == 35
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
                || !HasCompleteIncompleteRebuildSourceWeaponEvidence(
                    archetype.SourceWeaponEvidence)
                || Array.IndexOf(IncompleteRebuildSourceInstances, sourceInstance) < 0
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    IncompleteRebuildMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Incomplete Rebuild combat requires one exact reviewed atomic level/stat/weapon generation for the selected source",
                    hasExactCombatEvidence);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Incomplete Rebuild source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; uniform selection over distinct captured generations is private policy",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                9,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForDerangedShopper(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            if (sourceInstance != DerangedShopperSourceInstance
                || evidence == null
                || evidence.Length != 1
                || evidence[0].SourceInstance != DerangedShopperSourceInstance
                || evidence[0].LowId != 125454
                || evidence[0].HighId != 125455
                || evidence[0].Quality != 8)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Deranged Shopper source 0x{0:X8} requires the one exact owner-linked QL8 125454/125455 tuple",
                        sourceInstance),
                    archetype != null && archetype.Combat != null && archetype.Combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                evidence[0].EvidenceCaptures + ",20260720-031025"
                + ": Deranged Shopper source 0x79574527 owner-linked QL8 weapon 125454/125455; ten normal local-player hits span 7..15, one 27-point critical is report-only, and six captured misses preserve ammo -1, slot 6, unknown 0, and weapon instance 0; capture 20260720-031025 also proves empty SpecialAttackWeapon 56/45/45/45/0 plus attack-start, StopFight, and death context; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; the newly observed SIW/start/stop/death context remains evidence-only so runtime behavior is unchanged",
                evidence[0].LowId,
                evidence[0].HighId,
                evidence[0].Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForRedundantScan(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            bool retaliationObserved = archetype != null
                                       && archetype.Combat != null
                                       && archetype.Combat.Observed;
            if (!HasCompleteRedundantScanSourceWeaponEvidence(evidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Redundant Scan combat requires one exact owner-linked weapon tuple for each of the four current sources",
                    retaliationObserved);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
            {
                if (candidate.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = candidate;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Redundant Scan source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    retaliationObserved);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Redundant Scan source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; no fixed damage, empty SIW, or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                17,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForFragmentedSoul(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 2
                                          && combat.MinDamage == 18
                                          && combat.MaxDamage == 23
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
                || Array.IndexOf(FragmentedSoulSourceInstances, sourceInstance) < 0
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    FragmentedSoulMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Fragmented Soul combat requires one exact reviewed atomic level/stat/weapon generation for the selected source",
                    hasExactCombatEvidence);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Fragmented Soul source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; two normal local-player hits span 18..23 with ammo 24, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; uniform selection over distinct captured generations is private policy",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                24,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForRedundantScan(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 1
                                          && combat.MinDamage == 19
                                          && combat.MaxDamage == 19
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
                || !HasCompleteRedundantScanSourceWeaponEvidence(
                    archetype.SourceWeaponEvidence)
                || Array.IndexOf(RedundantScanSourceInstances, sourceInstance) < 0
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    RedundantScanMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Redundant Scan combat requires one exact reviewed atomic level/stat/weapon generation for the selected source",
                    hasExactCombatEvidence);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Redundant Scan source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; uniform selection over distinct captured generations is private policy",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                17,
                (int)WeaponSlots.Righthand,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForSourceSpecificWeaponArchetype(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            string displayName)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in
                archetype.SourceWeaponEvidence)
            {
                if (evidence.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = evidence;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "{0} source 0x{1:X8} requires exactly one owner-linked captured weapon tuple; found {2}",
                        displayName,
                        sourceInstance,
                        matches),
                    archetype.Combat != null && archetype.Combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeapon(
                string.Format(
                    "{0}: {1} source 0x{2:X8} owner-linked QL{3} weapon {4}/{5}; item owns normal damage and recharge; no fixed damage, special-attack, or captured AttackInfo context",
                    matched.EvidenceCaptures,
                    displayName,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand);
        }

        internal static CapturedEnemyCombatContract ForSupportedSourceWeapon(
            string name,
            int monsterData,
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence,
            int sourceInstance)
        {
            if (!string.Equals(name, "Mugger", StringComparison.Ordinal)
                || monsterData != MuggerMonsterData)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Unsupported source-specific weapon profile {0} monsterData={1}",
                        name,
                        monsterData),
                    false);
            }

            if (!HasCompleteMuggerSourceWeaponEvidence(sourceWeaponEvidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Mugger combat requires one exact QL1 121567/121567 owner-linked weapon tuple for each of the nine current sources",
                    true);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
            {
                if (evidence.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = evidence;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Mugger source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    true);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Mugger source 0x{1:X8} owner-linked QL1 weapon 121567/121567; 38 normal local-player hits span 9..12, three 21-point criticals are report-only, and the median interval is 5.816469 seconds; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; no empty SIW or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0,
                true);
        }

        private static bool HasCompleteMuggerSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != MuggerSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in MuggerSourceInstances)
            {
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (evidence.SourceInstance == expectedSource
                        && evidence.LowId == 121567
                        && evidence.HighId == 121567
                        && evidence.Quality == 1)
                    {
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCompleteRedundantScanSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != RedundantScanSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in RedundantScanSourceInstances)
            {
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (IsExactRedundantScanSourceWeapon(evidence, expectedSource))
                    {
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCompleteIncompleteRebuildSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != IncompleteRebuildSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in IncompleteRebuildSourceInstances)
            {
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (IsExactIncompleteRebuildSourceWeapon(evidence, expectedSource))
                    {
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExactIncompleteRebuildSourceWeapon(
            CapturedSubwaySourceWeaponEvidenceDefinition evidence,
            int expectedSource)
        {
            if (evidence == null || evidence.SourceInstance != expectedSource)
            {
                return false;
            }

            switch (expectedSource)
            {
                case 0x79545170:
                case 0x79545177:
                case 0x795451BC:
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 18;
                case 0x79545172:
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 14;
                case 0x79545188:
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 17;
                case 0x79545181:
                case 0x795451FD:
                case 0x79545241:
                    return evidence.LowId == 122654
                           && evidence.HighId == 122654
                           && evidence.Quality == 20;
                case 0x795451C1:
                    return evidence.LowId == 122655
                           && evidence.HighId == 122655
                           && evidence.Quality == 21;
                case 0x795451CB:
                    return evidence.LowId == 122655
                           && evidence.HighId == 122656
                           && evidence.Quality == 24;
                default:
                    return false;
            }
        }

        private static bool IsExactRedundantScanSourceWeapon(
            CapturedSubwaySourceWeaponEvidenceDefinition evidence,
            int expectedSource)
        {
            if (evidence == null || evidence.SourceInstance != expectedSource)
            {
                return false;
            }

            switch (expectedSource)
            {
                case 0x7953AF85:
                    return evidence.LowId == 122027
                           && evidence.HighId == 122027
                           && evidence.Quality == 20;
                case 0x795451BF:
                    return evidence.LowId == 122026
                           && evidence.HighId == 122027
                           && evidence.Quality == 14;
                case 0x795451C4:
                    return evidence.LowId == 122028
                           && evidence.HighId == 122029
                           && evidence.Quality == 25;
                case 0x795451D3:
                    return evidence.LowId == 122026
                           && evidence.HighId == 122027
                           && evidence.Quality == 16;
                default:
                    return false;
            }
        }

        private static CapturedEnemyCombatContract ForMeldedPatterns(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
            bool hasFocusedWeaponCapture = archetype.EvidenceCaptures != null
                                           && Array.IndexOf(
                                               archetype.EvidenceCaptures,
                                               "20260716-034559") >= 0;
            bool hasExactNormalHitBoundary = combat != null
                                             && combat.Observed
                                             && combat.ObservedRows == 7
                                             && combat.MinDamage == 21
                                             && combat.MaxDamage == 34
                                             && combat.WeaponSlot
                                                == (int)WeaponSlots.Righthand;
            if (!hasFocusedWeaponCapture || !hasExactNormalHitBoundary)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Melded Patterns equipped-weapon context requires focused capture 20260716-034559 and its seven normal 21..34 local-player hits",
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeapon(
                "20260716-034559: Melded Patterns QL20 Irreparable Sleekblaster Minor 121817/121818; seven normal local-player hits span 21..34 and no critical was observed; weapon owns runtime damage and recharge",
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponLowTemplate,
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponHighTemplate,
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponQuality,
                (int)WeaponSlots.Righthand);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            if (archetype != null
                && (archetype.MonsterData == DerangedShopperMonsterData
                    || archetype.MonsterData == IncompleteRebuildMonsterData
                    || archetype.MonsterData == FragmentedSoulMonsterData
                    || archetype.MonsterData == WorkmanStrikerMonsterData
                    || archetype.MonsterData == LooterMonsterData
                    || archetype.MonsterData == RedundantScanMonsterData))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    archetype.Name
                    + " combat requires an exact captured source identity; aggregate weapon fallback is forbidden",
                    archetype.Combat != null && archetype.Combat.Observed);
            }

            if (archetype != null
                && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData)
            {
                return ForMeldedPatterns(archetype);
            }

            if (archetype != null
                && archetype.MonsterData == NpcCombatAttackRules.CapturedSubwayBloodcreeperMonsterData)
            {
                return For(archetype.Name, archetype.MonsterData);
            }

            CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
            if (combat == null || !combat.Observed)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Generated ordinary archetype has no observed AttackInfo: " + archetype.Name,
                    false);
            }

            if (!combat.RuntimeReady)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Generated ordinary archetype has report-only AttackInfo evidence without a runtime-ready damage range and cadence: "
                    + archetype.Name,
                    true);
            }

            return CapturedEnemyCombatContract.FixedAttack(
                string.Join(",", archetype.EvidenceCaptures),
                combat.MinDamage,
                combat.MaxDamage,
                combat.RechargeSeconds,
                combat.WeaponSlot,
                combat.AttackInfoUnknown,
                combat.WeaponInstance);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            if (archetype != null && archetype.MonsterData == DerangedShopperMonsterData)
            {
                return ForDerangedShopper(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return ForWorkmanStriker(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                return ForIncompleteRebuild(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == LooterMonsterData)
            {
                return ForLooter(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == RedundantScanMonsterData)
            {
                return ForRedundantScan(archetype, sourceInstance);
            }

            return ForOrdinary(archetype);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            if (archetype != null
                && archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return ForWorkmanStriker(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype != null
                && archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                return ForIncompleteRebuild(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype != null
                && archetype.MonsterData == FragmentedSoulMonsterData)
            {
                return ForFragmentedSoul(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            return archetype != null
                   && archetype.MonsterData == RedundantScanMonsterData
                ? ForRedundantScan(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence)
                : ForOrdinary(archetype, sourceInstance);
        }
    }
}
