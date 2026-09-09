namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Trade;

    [TestClass]
    public sealed class ShopStockTests
    {
        const string TemplatesJson =
            """
            {
                "WEAP": {
                    "PSTL": { "Description": "Pistols", "ParentHash": "WEPN" },
                    "RIFL": { "Description": "Rifles", "ParentHash": "WEPN" }
                }
            }
            """;

        const string InstancesJson =
            """
            {
                "MOPA": { "TemplateId": [42640], "MinLevel": 1, "MaxLevel": 400 },
                "PSTL": { "TemplateId": [254633], "MinLevel": 1, "MaxLevel": 300 },
                "RIFL": { "TemplateId": [257128], "MinLevel": 300, "MaxLevel": 300 }
            }
            """;

        [TestMethod]
        public void ExpandAllStocksEveryLeafUnderTheCategory()
        {
            ShopStock stock = new();
            stock.EnsureFresh(Definition(Entry("WEAP", flags: 1, repeats: 0, chance: 0)), Minter(), new Random(1));

            Assert.AreEqual(2, stock.Slots.Count);
            CollectionAssert.AreEquivalent(
                new[] { 254633, 257128 },
                new List<int> { stock.Slots[0].LowId, stock.Slots[1].LowId });
        }

        [TestMethod]
        public void RandomOneHonoursRepeatsAndIgnoresZeroChance()
        {
            ShopStock stock = new();
            stock.EnsureFresh(
                Definition(
                    Entry("MOPA", flags: 0, repeats: 3, chance: 100),
                    Entry("PSTL", flags: 0, repeats: 5, chance: 0)),
                Minter(),
                new Random(1));

            Assert.AreEqual(3, stock.Slots.Count);
            foreach (ShopStockSlot slot in stock.Slots)
                Assert.AreEqual(42640, slot.LowId);
        }

        [TestMethod]
        public void EntryWithNoRepeatsIsSkippedUnlessItExpands()
        {
            ShopStock stock = new();
            stock.EnsureFresh(Definition(Entry("MOPA", flags: 0, repeats: 0, chance: 100)), Minter(), new Random(1));

            Assert.AreEqual(0, stock.Slots.Count);
            Assert.IsTrue(stock.IsGenerated);
        }

        [TestMethod]
        public void QualityIsClampedToTheHashInstanceBand()
        {
            ShopStock stock = new();
            stock.EnsureFresh(
                Definition(Entry("RIFL", flags: 0, repeats: 1, chance: 100, minLevel: 1, maxLevel: 50)),
                Minter(),
                new Random(1));

            Assert.AreEqual(1, stock.Slots.Count);
            Assert.AreEqual(300, stock.Slots[0].Quality);
        }

        [TestMethod]
        public void GeneratedStockIsCappedSoTheClientPaneCannotOverflow()
        {
            var entries = new List<VendingMachineStockEntry>();
            for (int i = 0; i < VendingMachineDefinition.MaxSlots + 10; i++)
                entries.Add(Entry("MOPA", flags: 0, repeats: 1, chance: 100));

            ShopStock stock = new();
            stock.EnsureFresh(new VendingMachineDefinition { Inventory = entries }, Minter(), new Random(1));

            Assert.AreEqual(VendingMachineDefinition.MaxSlots, stock.Slots.Count);
        }

        [TestMethod]
        public void StockIsNotRerolledWhileAShopperHasTheWindowOpen()
        {
            VendingMachineDefinition definition = Definition(Entry("MOPA", flags: 0, repeats: 1, chance: 100));
            ShopStock stock = new();
            stock.EnsureFresh(definition, Minter(), new Random(1));
            stock.OpenTrade();

            ShopStockSlot before = stock.Slots[0];
            stock.EnsureFresh(definition, Minter(), new Random(2));

            Assert.AreEqual(1, stock.Slots.Count);
            Assert.AreEqual(before.Quality, stock.Slots[0].Quality);
            Assert.IsFalse(stock.IsIdleExpired());
        }

        [TestMethod]
        public void OpenTradeCountNeverGoesNegative()
        {
            ShopStock stock = new();
            stock.CloseTrade();
            stock.CloseTrade();

            Assert.AreEqual(0, stock.OpenTrades);
        }

        [TestMethod]
        public void ResetDropsEveryOutstandingShopper()
        {
            ShopStock stock = new();
            stock.OpenTrade();
            stock.OpenTrade();
            stock.Reset();

            Assert.AreEqual(0, stock.OpenTrades);
        }

        [TestMethod]
        public void ShopUpdateCarriesOneSlotPerStockedItem()
        {
            ShopStock stock = new();
            stock.EnsureFresh(Definition(Entry("MOPA", flags: 0, repeats: 2, chance: 100)), Minter(), new Random(1));

            var shopIdentity = new Identity { Type = IdentityType.VendingMachine, Instance = 7 };
            ShopUpdateMessage message = stock.BuildShopUpdate(shopIdentity);

            Assert.AreEqual(shopIdentity, message.Identity);
            Assert.AreEqual(2, message.VendingMachineSlots.Length);
            Assert.AreEqual(42640, message.VendingMachineSlots[0].ItemLowId);
        }

        static VendingMachineDefinition Definition(params VendingMachineStockEntry[] entries)
            => new() { Inventory = new List<VendingMachineStockEntry>(entries) };

        static VendingMachineStockEntry Entry(
            string hash,
            int flags,
            int repeats,
            int chance,
            int minLevel = 1,
            int maxLevel = 400)
            => new()
            {
                Hash = hash,
                Flags = flags,
                Repeats = repeats,
                Chance = chance,
                MinLevel = minLevel,
                MaxLevel = maxLevel
            };

        static HashItemMinter Minter()
            => new(
                new StubGameData(HashItemCatalog.Parse(TemplatesJson, InstancesJson)),
                new StubCatalog(),
                new StubItemBuilder());
    }
}
