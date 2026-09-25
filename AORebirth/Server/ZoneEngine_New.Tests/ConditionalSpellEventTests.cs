namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class ConditionalSpellEventTests
    {
        const int GardenKey = 214789;
        const int OtherInsignia = 226073;
        const int WrongInsignia = 214840;
        const int OutsideRange = 1000;

        [TestMethod]
        public void RandomRollIsSharedAcrossLaterFunctions()
        {
            ItemTemplate button = FailButton();
            AssertBranch(button, roll: 9, CharacterStat.Health);
            AssertBranch(button, roll: 10, CharacterStat.Strength);
            AssertBranch(button, roll: 19, CharacterStat.Strength);
            AssertBranch(button, roll: 20, CharacterStat.Agility);
            AssertBranch(button, roll: 29, CharacterStat.Agility);
            AssertBranch(button, roll: 30, CharacterStat.Stamina);
        }

        [TestMethod]
        public void UseItemOnItemMatchesTheUsedTemplateAndDestroysTheKey()
        {
            ItemTemplate statue = ThrakStatue();
            Player player = PlayerWithExpansion();
            (Item key, int slot) = Give(player, GardenKey);

            Assert.IsTrue(RunOnStatue(statue, player, key, slot));
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.Strength));
            Assert.AreNotEqual(1, player.Stats.Get(CharacterStat.Agility));
            Assert.AreNotEqual(1, player.Stats.Get(CharacterStat.Stamina));
            Assert.IsFalse(player.Inventory.Inventory.Content.ContainsKey(slot));
        }

        [TestMethod]
        public void UseItemOnItemRejectsTheWrongInsigniaWithoutDestroyingIt()
        {
            ItemTemplate statue = ThrakStatue();
            Player player = PlayerWithExpansion();
            (Item item, int slot) = Give(player, WrongInsignia);

            Assert.IsTrue(RunOnStatue(statue, player, item, slot));
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.Agility));
            Assert.AreNotEqual(1, player.Stats.Get(CharacterStat.Strength));
            Assert.AreSame(item, player.Inventory.Inventory.Content[slot]);
        }

        [TestMethod]
        public void UseItemOnItemRejectsAnItemOutsideTheInsigniaRange()
        {
            ItemTemplate statue = ThrakStatue();
            Player player = PlayerWithExpansion();
            (Item item, int slot) = Give(player, OutsideRange);

            Assert.IsTrue(RunOnStatue(statue, player, item, slot));
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.Stamina));
            Assert.AreSame(item, player.Inventory.Inventory.Content[slot]);
        }

        [TestMethod]
        public void UseItemOnItemActionRequiresTheExpansionBit()
        {
            ItemTemplate statue = ThrakStatue();
            Player player = TestWorld.CreatePlayer(1);
            player.Stats.Set(CharacterStat.Expansion, 0);
            Assert.IsFalse(statue.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.UseItemOnItem));

            player.Stats.Set(CharacterStat.Expansion, 2);
            Assert.IsTrue(statue.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.UseItemOnItem));

            player.Stats.Set(CharacterStat.Side, 1);
            Assert.IsFalse(statue.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.UseItemOnItem));
        }

        static void AssertBranch(ItemTemplate button, int roll, CharacterStat expected)
        {
            var player = TestWorld.CreatePlayer(roll);
            Assert.IsTrue(button.ExecuteOnUseSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder(),
                criteria: new SpellCriteria { NextRoll = () => roll }));

            Assert.AreEqual(1, player.Stats.Get(CharacterStat.CurrentNano), "opening line did not run");
            foreach (CharacterStat stat in new[] { CharacterStat.Health, CharacterStat.Strength, CharacterStat.Agility, CharacterStat.Stamina })
            {
                if (stat == expected)
                    Assert.AreEqual(1, player.Stats.Get(stat), stat.ToString());
                else
                    Assert.AreNotEqual(1, player.Stats.Get(stat), stat.ToString());
            }
        }

        static bool RunOnStatue(ItemTemplate statue, Player player, Item used, int slot)
            => statue.ExecuteSpells(
                EventType.OnUseItemOn,
                player,
                new StubInventoryRepository(),
                new StubItemBuilder(),
                new SpellCriteria
                {
                    SecondaryItemTemplate = used.LowId,
                    Subject = used,
                    SubjectSlot = new Identity { Type = IdentityType.Inventory, Instance = slot }
                });

        static Player PlayerWithExpansion()
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Stats.Set(CharacterStat.Expansion, 2);
            return player;
        }

        static (Item Item, int Slot) Give(Player player, int lowId)
        {
            Item item = TestWorld.CreateItem(lowId: lowId, highId: lowId, instanceId: lowId);
            int slot = player.Inventory.Inventory.FindFreeSlot();
            Assert.IsTrue(player.Inventory.Inventory.Add(slot, item));
            return (item, slot);
        }

        static ItemTemplate FailButton()
        {
            return new ItemTemplate
            {
                Id = 292862,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        Set(CharacterStat.CurrentNano, Leaf(CharacterStat.Rnd, Operator.GreaterThan, -1)),
                        Set(CharacterStat.Health, Leaf(CharacterStat.LastRnd, Operator.LessThan, 10)),
                        Set(
                            CharacterStat.Strength,
                            Leaf(CharacterStat.LastRnd, Operator.GreaterThan, 9),
                            Leaf(CharacterStat.LastRnd, Operator.LessThan, 20),
                            Link(Operator.And)),
                        Set(
                            CharacterStat.Agility,
                            Leaf(CharacterStat.LastRnd, Operator.GreaterThan, 19),
                            Leaf(CharacterStat.LastRnd, Operator.LessThan, 30),
                            Link(Operator.And)),
                        Set(CharacterStat.Stamina, Leaf(CharacterStat.LastRnd, Operator.GreaterThan, 29))
                    ]
                }
            };
        }

        static ItemTemplate ThrakStatue()
        {
            return new ItemTemplate
            {
                Id = 222955,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUseItemOn] =
                    [
                        Set(
                            CharacterStat.Strength,
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, GardenKey),
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, OtherInsignia),
                            Link(Operator.Or),
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, 226994),
                            Link(Operator.Or),
                            Leaf(CharacterStat.Expansion, Operator.BitAnd, 2),
                            Link(Operator.And)),
                        Set(
                            CharacterStat.Agility,
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, WrongInsignia),
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, 224052),
                            Link(Operator.Or, (int)CharacterStat.SecondaryItemTemplate)),
                        Set(
                            CharacterStat.Stamina,
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.LessThan, 214781),
                            Leaf(CharacterStat.SecondaryItemTemplate, Operator.GreaterThan, 226995),
                            Link(Operator.Or, (int)CharacterStat.SecondaryItemTemplate)),
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.DestroyItem,
                            Requirements = [Leaf(CharacterStat.SecondaryItemTemplate, Operator.EqualTo, GardenKey)]
                        }
                    ]
                },
                Actions =
                [
                    new ItemAction
                    {
                        ActionType = (int)ActionType.UseItemOnItem,
                        Requirements =
                        [
                            Leaf(CharacterStat.Side, Operator.Unequal, 1),
                            Leaf(CharacterStat.Expansion, Operator.BitAnd, 2),
                            Link(Operator.And)
                        ]
                    }
                ]
            };
        }

        static ItemSpell Set(CharacterStat stat, params ItemRequirement[] requirements)
        {
            return new ItemSpell
            {
                FunctionType = (int)FunctionType.Set,
                Arguments = new List<object> { (int)stat, 1 },
                Requirements = new List<ItemRequirement>(requirements)
            };
        }

        static ItemRequirement Leaf(CharacterStat stat, Operator op, int value)
            => new()
            {
                StatNumber = (int)stat,
                Operator = (int)op,
                Value = value
            };

        static ItemRequirement Link(Operator op, int stat = 0)
            => new()
            {
                StatNumber = stat,
                Operator = (int)op
            };
    }
}
