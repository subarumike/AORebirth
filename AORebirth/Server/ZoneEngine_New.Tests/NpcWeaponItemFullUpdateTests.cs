namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class NpcWeaponItemFullUpdateTests
    {
        [TestMethod]
        public void EquippedMeleeWeaponIsAnnouncedAndFistIsNot()
        {
            Assert.IsTrue(AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(MeleeWeapon(instanceId: 55)));
            Assert.IsFalse(AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(Fist(instanceId: 0)));
            Assert.IsFalse(
                AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(
                    Fist(instanceId: 99, physicalInit: true)));
            Assert.IsFalse(
                AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(
                    MaFist(instanceId: 88)));
        }

        [TestMethod]
        public void NpcTagBackedWeaponsSkipWifuAndUseWireOrdinalsInAttackInfo()
        {
            NpcCharacter npc = CreateNpc();
            npc.ArmFromItemForTests(WeaponSlot.Npc0, MeleeWeapon(instanceId: 101, lowId: 121567), wireSlot: 0, sawHash: "SIW1");
            npc.ArmFromItemForTests(WeaponSlot.Npc1, MeleeWeapon(instanceId: 102, lowId: 121568), wireSlot: 1, sawHash: "SIW2");
            npc.ArmFromItemForTests(WeaponSlot.Npc2, MeleeWeapon(instanceId: 103, lowId: 121569), wireSlot: 2, sawHash: "SIW3");

            Assert.AreEqual(0, npc.BuildWeaponInstanceMessages().Count);

            SpecialAttack[] specials = npc.BuildSpecialAttackWeaponMessage().Specials;
            Assert.AreEqual(3, specials.Length);
            Assert.AreEqual(121567, specials[0].Unknown1);
            Assert.AreEqual(121569, specials[2].Unknown1);
            Assert.AreEqual("SIW1", specials[0].Unknown4);
            Assert.AreEqual("SIW3", specials[2].Unknown4);

            CharacterWeapon armed = npc.Weapons[WeaponSlot.Npc2];
            Assert.AreEqual(2, AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.Npc2, armed.Item, false));
            Assert.AreEqual(armed.SawTag, AttackInfoRules.ResolveWeaponInstance(armed, armed.Item, false));
        }

        [TestMethod]
        public void NpcAttackInfoSlotUsesWireOrdinal()
        {
            var armed = new CharacterWeapon { WireSlot = 5, Item = MeleeWeapon(77) };
            Assert.AreEqual(
                5,
                AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.Npc5, armed.Item, attackerIsPlayer: false));
        }

        [TestMethod]
        public void NpcBuildWeaponInstanceMessagesSkipsUnarmedFistArm()
        {
            NpcCharacter npc = CreateNpc();
            npc.ArmFromItemForTests(
                WeaponSlot.Npc0,
                Fist(instanceId: 0, physicalInit: true),
                wireSlot: 0);

            Assert.AreEqual(0, npc.BuildWeaponInstanceMessages().Count);
        }

        static NpcCharacter CreateNpc()
            => new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 7001 },
                new StubItemBuilder());

        static Item MeleeWeapon(int instanceId, int lowId = 1000)
            => Weapon(
                instanceId,
                lowId,
                initiativeType: (int)CharacterStat.MeleeInit,
                damageType: (int)CharacterStat.MeleeAC);

        static Item Fist(int instanceId, bool physicalInit = false)
            => Weapon(
                instanceId,
                lowId: 43712,
                initiativeType: physicalInit ? (int)CharacterStat.PhysicalInit : (int)CharacterStat.MeleeInit);

        static Item MaFist(int instanceId)
        {
            Item fist = Weapon(
                instanceId,
                lowId: 211357,
                initiativeType: (int)CharacterStat.MeleeInit);
            fist.Definition.Stats[CharacterStat.MartialArts] = 100;
            return fist;
        }

        static Item Weapon(int instanceId, int lowId, int initiativeType, int damageType = 0)
        {
            var stats = new Dictionary<CharacterStat, int>
            {
                [CharacterStat.InitiativeType] = initiativeType,
                [CharacterStat.ItemClass] = (int)ItemClass.Weapon,
                [CharacterStat.MinDamage] = 2,
                [CharacterStat.MaxDamage] = 18
            };
            if (damageType > 0)
                stats[CharacterStat.DamageType] = damageType;

            return new Item
            {
                InstanceId = instanceId,
                LowId = lowId,
                HighId = lowId,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = lowId,
                    Name = "Npc Weapon",
                    Quality = 1,
                    Stats = stats
                }
            };
        }
    }

    /// <summary>Test seam so armed NPC hands can be set without a full rebase/catalog.</summary>
    internal static class NpcWeaponTestExtensions
    {
        public static void ArmFromItemForTests(
            this Character character,
            WeaponSlot slot,
            Item item,
            int wireSlot = -1,
            string? sawHash = null)
        {
            var weapon = new CharacterWeapon
            {
                Item = item,
                WireSlot = wireSlot
            };

            if (wireSlot >= 0 && !string.IsNullOrWhiteSpace(sawHash))
            {
                string tagName = sawHash.Trim().ToUpperInvariant();
                if (tagName.Length > 4)
                    tagName = tagName.Substring(0, 4);
                else if (tagName.Length < 4)
                    tagName = tagName.PadRight(4);

                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(tagName);
                weapon.SawTagName = tagName;
                weapon.SawTag = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
            }

            weapon.ConfigureBaseSpeeds(1.0, 1.0);
            character.SetWeapon(slot, weapon);
        }
    }
}
