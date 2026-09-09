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
        public void NpcWireSlotUsesNaturalAmmoAndOrdinalSlot()
        {
            Item club = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 77);
            var armed = new CharacterWeapon { Item = club, WireSlot = 3, SawTag = 0x53495731, SawTagName = "SIW1" };

            Assert.AreEqual(3, AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.MainHand, club, attackerIsPlayer: false));
            Assert.AreEqual(
                AttackInfoRules.NaturalMeleeAmmoCount,
                AttackInfoRules.ResolveAmmoCount(armed, club, attackerIsPlayer: false));
            Assert.AreEqual(
                0x53495731,
                AttackInfoRules.ResolveWeaponInstance(armed, club, attackerIsPlayer: false));
        }

        [TestMethod]
        public void NpcMaFistFallbackUsesSlotZero()
        {
            Item fist = Weapon(
                initiativeType: (int)CharacterStat.MeleeInit,
                instanceId: 0);
            var armed = new CharacterWeapon { Item = fist, WireSlot = -1 };

            Assert.AreEqual(
                0,
                AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.MainHand, fist, attackerIsPlayer: false));
            Assert.AreEqual(
                AttackInfoRules.NaturalMeleeAmmoCount,
                AttackInfoRules.ResolveAmmoCount(armed, fist, attackerIsPlayer: false));
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
