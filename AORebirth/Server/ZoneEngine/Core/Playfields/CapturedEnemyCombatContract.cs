namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

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
                        return true;
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
            int weaponInstance)
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
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance
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
            int unknown5)
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
            return contract;
        }

        internal static CapturedEnemyCombatContract Specialized(string evidence)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Specialized
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

            return true;
        }

        private static void SetMobStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            switch (monsterData)
            {
                case 17657:
                    return CapturedEnemyCombatContract.Specialized(
                        "20260709-193914: Filth Flea poison opener and melee cycle");
                case 17720:
                    return CapturedEnemyCombatContract.FixedAttack(
                        "20260709-210452/220439: Discarded Pet AttackInfo",
                        9,
                        9,
                        0.0,
                        0,
                        0,
                        1397315377);
                case 17649:
                    return CapturedEnemyCombatContract.Unresolved(
                        "20260709-220439: Disobedient Bot retaliation observed; no hit landed",
                        true);
                case 203734:
                    return CapturedEnemyCombatContract.FixedAttack(
                        "20260709-210452/212115/212336/220439: Mugger AttackInfo",
                        10,
                        21,
                        5.900159,
                        (int)WeaponSlots.Righthand,
                        0,
                        0);
                case 26092:
                    return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(
                        "20260711-170337 packets 301-654: Thief attack start, movement transition, three 9-point landed hits, and six-second repeat cadence",
                        121567,
                        121567,
                        1,
                        (int)WeaponSlots.Righthand,
                        NpcCombatAttackRules.CapturedSubwayThiefDamage,
                        NpcCombatAttackRules.CapturedSubwayThiefDamage,
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
                    return CapturedEnemyCombatContract.EquippedWeapon(
                        "20260709-205921/210452/212115/212336: Violent Vagabond QL1 weapon 130590",
                        130590,
                        130590,
                        1,
                        (int)WeaponSlots.Righthand);
                default:
                    return CapturedEnemyCombatContract.Unresolved(
                        "No captured combat contract for " + name + " monsterData=" + monsterData,
                        false);
            }
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
            if (combat == null || !combat.Observed)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Generated ordinary archetype has no observed AttackInfo: " + archetype.Name,
                    false);
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
    }
}
