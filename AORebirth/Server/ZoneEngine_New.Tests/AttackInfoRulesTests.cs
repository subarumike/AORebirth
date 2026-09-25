namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class AttackInfoRulesTests
    {
        [TestMethod]
        public void NullWeaponUsesPlayerUnarmedShape()
        {
            Assert.IsTrue(AttackInfoRules.IsUnarmedPresentation(null));
            Assert.AreEqual(AttackInfoRules.PlayerMeleeAmmoCount, AttackInfoRules.ResolveAmmoCount(null));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponSlot(WeaponSlot.MainHand, null));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(null, attackerIsPlayer: true));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(null, attackerIsPlayer: false));
        }

        [TestMethod]
        public void SyntheticMaFistUsesPlayerUnarmedShapeNotRightHand()
        {
            Item fist = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 0);
            var armed = new CharacterWeapon { Item = fist, IsSyntheticFist = true };

            Assert.IsTrue(AttackInfoRules.IsUnarmedPresentation(armed, fist));
            Assert.AreEqual(
                AttackInfoRules.PlayerMeleeAmmoCount,
                AttackInfoRules.ResolveAmmoCount(armed, fist, attackerIsPlayer: true));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.MainHand, fist));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(armed, fist, attackerIsPlayer: true));
        }

        [TestMethod]
        public void PhysicalInitWeaponUsesPlayerUnarmedShape()
        {
            Item fist = Weapon(
                initiativeType: (int)CharacterStat.PhysicalInit,
                instanceId: 42);

            Assert.IsTrue(AttackInfoRules.IsUnarmedPresentation(fist));
            Assert.AreEqual(AttackInfoRules.PlayerMeleeAmmoCount, AttackInfoRules.ResolveAmmoCount(fist));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponSlot(WeaponSlot.MainHand, fist));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(fist, attackerIsPlayer: true));
        }

        [TestMethod]
        public void MeleeWeaponUsesMeleeAmmoAndHandSlotWithZeroInstance()
        {
            Item club = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 77);

            Assert.IsFalse(AttackInfoRules.IsUnarmedPresentation(club));
            Assert.AreEqual(AttackInfoRules.PlayerMeleeAmmoCount, AttackInfoRules.ResolveAmmoCount(club));
            Assert.AreEqual((int)WeaponSlots.Righthand, AttackInfoRules.ResolveWeaponSlot(WeaponSlot.MainHand, club));
            Assert.AreEqual((int)WeaponSlots.LeftHand, AttackInfoRules.ResolveWeaponSlot(WeaponSlot.OffHand, club));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(club, attackerIsPlayer: true));
        }

        [TestMethod]
        public void NpcMonsterWeaponWithoutVisualUsesSlotZeroAndSawTag()
        {
            Item claw = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 77);
            var armed = new CharacterWeapon { Item = claw, WireSlot = 3, SawTag = 0x53495731, SawTagName = "SIW1" };

            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.Npc3, claw, attackerIsPlayer: false));
            Assert.AreEqual(
                AttackInfoRules.PlayerMeleeAmmoCount,
                AttackInfoRules.ResolveAmmoCount(armed, claw, attackerIsPlayer: false));
            Assert.AreEqual(
                0x53495731,
                AttackInfoRules.ResolveWeaponInstance(armed, claw, attackerIsPlayer: false));
        }

        [TestMethod]
        public void NpcVisualOverrideUsesRightHandOverrideStyleAndNoTag()
        {
            Item claw = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 77);
            Item pistol = Weapon(
                initiativeType: (int)CharacterStat.RangedInit,
                instanceId: 88);
            var armed = new CharacterWeapon
            {
                Item = claw,
                DamageOverride = pistol,
                WireSlot = 0,
                SawTag = 0x53495731,
                SawTagName = "SIW1"
            };

            Assert.AreEqual(
                (int)WeaponSlots.Righthand,
                AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.Npc0, claw, attackerIsPlayer: false));
            Assert.AreEqual(
                AttackInfoRules.NpcRangedAmmoCount,
                AttackInfoRules.ResolveAmmoCount(armed, claw, attackerIsPlayer: false));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(armed, claw, attackerIsPlayer: false));
            Assert.IsTrue(armed.IsRanged());
        }

        [TestMethod]
        public void RangedWeaponUsesRangedAmmoPlaceholder()
        {
            Item pistol = Weapon(
                initiativeType: (int)CharacterStat.RangedInit,
                instanceId: 88);

            Assert.IsFalse(AttackInfoRules.IsUnarmedPresentation(pistol));
            Assert.AreEqual(AttackInfoRules.RangedAmmoCount, AttackInfoRules.ResolveAmmoCount(pistol));
            Assert.AreEqual((int)WeaponSlots.Righthand, AttackInfoRules.ResolveWeaponSlot(WeaponSlot.MainHand, pistol));
            Assert.AreEqual(0, AttackInfoRules.ResolveWeaponInstance(pistol, attackerIsPlayer: false));
        }

        static Item Weapon(int initiativeType, int instanceId)
            => new()
            {
                InstanceId = instanceId,
                LowId = 1000,
                HighId = 1000,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = 1000,
                    Name = "Test Weapon",
                    Quality = 1,
                    Stats = new Dictionary<CharacterStat, int>
                    {
                        [CharacterStat.InitiativeType] = initiativeType,
                        [CharacterStat.ItemClass] = (int)ItemClass.Weapon
                    }
                }
            };
    }
}
