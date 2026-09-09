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
    public sealed class SpecialAttackWeaponTests
    {
        [TestMethod]
        public void NpcWeaponsEmitTemplateHashSawEntries()
        {
            NpcCharacter npc = CreateNpc();
            npc.ArmFromItemForTests(
                WeaponSlot.Npc0,
                Weapon(201059, 201060, "StickToHead_001"),
                wireSlot: 0,
                sawHash: "EPAH");
            npc.ArmFromItemForTests(
                WeaponSlot.Npc1,
                Weapon(201056, 201057, "Arms_001"),
                wireSlot: 1,
                sawHash: "AZUS");

            SpecialAttack[] specials = npc.BuildSpecialAttackWeaponMessage().Specials;

            Assert.AreEqual(2, specials.Length);
            Assert.AreEqual(201059, specials[0].Unknown1);
            Assert.AreEqual(201060, specials[0].Unknown2);
            Assert.AreEqual(0x45504148, specials[0].Unknown3);
            Assert.AreEqual("EPAH", specials[0].Unknown4);
            Assert.AreEqual(201056, specials[1].Unknown1);
            Assert.AreEqual(0x415A5553, specials[1].Unknown3);
            Assert.AreEqual("AZUS", specials[1].Unknown4);
        }

        [TestMethod]
        public void NpcAttackInfoInstanceMatchesSawTag()
        {
            Item item = Weapon(144742, 144743, "SingleBreedMonsterWeapon_001");
            var armed = new CharacterWeapon { Item = item, WireSlot = 0, SawTag = 0x53495731, SawTagName = "SIW1" };

            Assert.AreEqual(
                0x53495731,
                AttackInfoRules.ResolveWeaponInstance(armed, item, attackerIsPlayer: false));
            Assert.AreEqual(
                0,
                AttackInfoRules.ResolveWeaponSlot(armed, WeaponSlot.Npc0, item, attackerIsPlayer: false));
        }

        [TestMethod]
        public void NpcTagBackedWeaponsDoNotEmitWifu()
        {
            NpcCharacter npc = CreateNpc();
            npc.ArmFromItemForTests(
                WeaponSlot.Npc0,
                Weapon(144742, 144743, "SingleBreedMonsterWeapon_001", instanceId: 99),
                wireSlot: 0,
                sawHash: "SIW1");

            Assert.AreEqual(0, npc.BuildWeaponInstanceMessages().Count);
            Assert.IsFalse(
                AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(
                    npc.Weapons[WeaponSlot.Npc0].Item,
                    npc.Weapons[WeaponSlot.Npc0]));
        }

        [TestMethod]
        public void PlayerUnarmedAlwaysEmitsMaatBrawDiitEvenWithoutMartialArtsStat()
        {
            // Catalog fists often lack MartialArts>0; combat-start SAW must still advertise specials.
            Item fist = new()
            {
                InstanceId = 0,
                LowId = 43712,
                HighId = 43713,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = 43712,
                    Name = "Fist",
                    Quality = 1,
                    Stats = new Dictionary<CharacterStat, int>
                    {
                        [CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit,
                        [CharacterStat.ItemClass] = (int)ItemClass.Weapon
                    }
                }
            };

            var player = new Player(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 42 },
                new StubLogger(),
                new StubItemBuilder());
            player.ArmFromItemForTests(WeaponSlot.MainHand, fist);
            player.Weapons[WeaponSlot.MainHand].IsSyntheticFist = true;

            SpecialAttack[] specials = player.BuildSpecialAttackWeaponMessage().Specials;
            CollectionAssert.AreEquivalent(
                new[] { "MAAT", "BRAW", "DIIT" },
                specials.Select(s => s.Unknown4).ToArray());
        }

        [TestMethod]
        public void PlayerMaFistStillEmitsMaatBrawDiit()
        {
            // Fist with MartialArts > 0 drives MA specials; no NPC wire tags.
            Item fist = new()
            {
                InstanceId = 0,
                LowId = 211357,
                HighId = 211358,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = 211357,
                    Name = "MA Fist",
                    Quality = 1,
                    Stats = new Dictionary<CharacterStat, int>
                    {
                        [CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit,
                        [CharacterStat.ItemClass] = (int)ItemClass.Weapon,
                        [CharacterStat.MartialArts] = 100,
                        [CharacterStat.Can] = (int)(CanFlags.Brawl | CanFlags.Dimach)
                    }
                }
            };

            var player = new Player(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 42 },
                new StubLogger(),
                new StubItemBuilder());
            player.ArmFromItemForTests(WeaponSlot.MainHand, fist);

            SpecialAttack[] specials = player.BuildSpecialAttackWeaponMessage().Specials;
            CollectionAssert.AreEquivalent(
                new[] { "MAAT", "BRAW", "DIIT" },
                specials.Select(s => s.Unknown4).ToArray());
        }

        static NpcCharacter CreateNpc()
            => new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 7002 },
                new StubItemBuilder());

        static Item Weapon(int lowId, int highId, string name, int instanceId = 55)
            => new()
            {
                InstanceId = instanceId,
                LowId = lowId,
                HighId = highId,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = lowId,
                    Name = name,
                    Quality = 1,
                    Stats = new Dictionary<CharacterStat, int>
                    {
                        [CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit,
                        [CharacterStat.ItemClass] = (int)ItemClass.Weapon,
                        [CharacterStat.MinDamage] = 2,
                        [CharacterStat.MaxDamage] = 10
                    }
                }
            };
    }
}
