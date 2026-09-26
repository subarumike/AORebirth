namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Shops;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Trade;

    [TestClass]
    public sealed class ShopDaoConnectionTests
    {
        [TestMethod]
        public void ExactCorrelationBuildsEveryDaoStockRowWithoutTheGeneratedStockCap()
        {
            var hashCatalog = HashItemCatalog.Parse("""
                {"PAIR":{"Templates":[100,200]}}
                """);
            var gameData = new StubGameData(hashCatalog);
            var items = new StubCatalog().Add(100, 1).Add(200, 400);
            var rows = Enumerable.Range(1, 70)
                .Select(id => new ShopStockData(id, "ABCD", 100, 200, 1, 400, 25, 50, 1))
                .ToList();
            ShopVendorData vendor = Vendor(rows);

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsTrue(built, failure);
            Assert.AreEqual(70, ranges.Length);
            var stock = new ShopStock();
            stock.SetConfiguredRanges(ranges, new Random(1));
            ShopStockSlot[] slots = stock.Slots.ToArray();
            Assert.AreEqual(70, slots.Length);
            Assert.IsTrue(slots.All(slot => slot.LowId == 100 && slot.HighId == 200));
            Assert.IsTrue(slots.All(slot => slot.ItemHash == "PAIR"));
            Assert.IsTrue(slots.All(slot => slot.Quality >= 25 && slot.Quality <= 50));
        }

        [TestMethod]
        public void MissingCorrelationKeepsTheValidDaoRowAndAvailableHashMetadata()
        {
            var hashCatalog = HashItemCatalog.Parse("""
                {"PAIR":{"Templates":[100,200]}}
                """);
            var gameData = new StubGameData(hashCatalog);
            var items = new StubCatalog().Add(100, 1).Add(200, 400).Add(300, 1).Add(301, 400);
            ShopVendorData vendor = Vendor(new List<ShopStockData>
            {
                new(1, "ABCD", 100, 200, 1, 400, 1, 50, 1),
                new(2, "ABCD", 300, 301, 1, 400, 1, 50, 1)
            });

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsTrue(built, failure);
            Assert.AreEqual(2, ranges.Length);
            var stock = new ShopStock();
            stock.SetConfiguredRanges(ranges, new Random(1));
            ShopStockSlot[] slots = stock.Slots.ToArray();
            Assert.AreEqual(2, slots.Length);
            Assert.AreEqual((100, 200, "PAIR"), (slots[0].LowId, slots[0].HighId, slots[0].ItemHash));
            Assert.AreEqual((300, 301, string.Empty), (slots[1].LowId, slots[1].HighId, slots[1].ItemHash));
            Assert.IsTrue(slots.All(slot => slot.Quality >= 1 && slot.Quality <= 50));
        }

        [TestMethod]
        public void RuntimeVendingIdentityMapsToThePreservedDatabaseVendorIdentity()
        {
            Assert.AreEqual(
                42926084,
                NpcContentActivationService.DatabaseVendorId(655, unchecked((int)0xC0040235)));
        }

        [TestMethod]
        public void AmbiguousCorrelationKeepsTheValidDaoRowWithoutHashMetadata()
        {
            HashItemCatalog hashCatalog = HashItemCatalog.Parse("""
                {"ONE":{"Templates":[100,200]},"TWO":{"Templates":[100,200]}}
                """);

            Assert.IsFalse(hashCatalog.TryGetAssignedHash(100, 200, out _));
            var gameData = new StubGameData(hashCatalog);
            var items = new StubCatalog().Add(100, 1).Add(200, 400);
            ShopVendorData vendor = Vendor(new List<ShopStockData>
            {
                new(1, "ABCD", 100, 200, 1, 400, 1, 50, 1)
            });

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsTrue(built, failure);
            Assert.AreEqual(1, ranges.Length);
            var stock = new ShopStock();
            stock.SetConfiguredRanges(ranges, new Random(1));
            Assert.AreEqual(string.Empty, stock.Slots.Single().ItemHash);
        }

        [TestMethod]
        public void MixedCorrelationsRetainAllNinetyEligibleDaoRows()
        {
            var hashCatalog = HashItemCatalog.Parse("""
                {
                  "PAIR":{"Templates":[100,200]},
                  "AMBIGUOUS_ONE":{"Templates":[400,401]},
                  "AMBIGUOUS_TWO":{"Templates":[400,401]}
                }
                """);
            var gameData = new StubGameData(hashCatalog);
            var items = new StubCatalog()
                .Add(100, 1).Add(200, 400)
                .Add(300, 1).Add(301, 400)
                .Add(400, 1).Add(401, 400);
            var rows = Enumerable.Range(1, 90)
                .Select(id => id <= 9
                    ? new ShopStockData(id, "ABCD", 100, 200, 1, 400, 25, 50, 1)
                    : id <= 81
                        ? new ShopStockData(id, "ABCD", 300, 301, 1, 400, 25, 50, 1)
                        : new ShopStockData(id, "ABCD", 400, 401, 1, 400, 25, 50, 1))
                .ToList();
            ShopVendorData vendor = Vendor(rows);

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsTrue(built, failure);
            Assert.AreEqual(90, ranges.Length);
            var stock = new ShopStock();
            stock.SetConfiguredRanges(ranges, new Random(1));
            ShopStockSlot[] slots = stock.Slots.ToArray();
            Assert.AreEqual(90, slots.Length);
            Assert.AreEqual(9, slots.Count(slot => slot.ItemHash == "PAIR"));
            Assert.AreEqual(81, slots.Count(slot => slot.ItemHash == string.Empty));
        }

        [TestMethod]
        public void MissingCorrelationDoesNotBypassEndpointValidation()
        {
            var gameData = new StubGameData(HashItemCatalog.Parse("{}"));
            var items = new StubCatalog().Add(300, 1);
            ShopVendorData vendor = Vendor(new List<ShopStockData>
            {
                new(2, "ABCD", 300, 301, 1, 400, 1, 50, 1)
            });

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsFalse(built);
            Assert.AreEqual(0, ranges.Length);
            StringAssert.Contains(failure, "Stock row 2");
            StringAssert.Contains(failure, "300/301");
        }

        [TestMethod]
        public void MissingCorrelationDoesNotBypassQualityValidation()
        {
            var gameData = new StubGameData(HashItemCatalog.Parse("{}"));
            var items = new StubCatalog().Add(300, 1).Add(301, 400);
            ShopVendorData vendor = Vendor(new List<ShopStockData>
            {
                new(2, "ABCD", 300, 301, 1, 400, 0, 50, 1)
            });

            bool built = NpcContentActivationService.TryBuildDatabaseStock(
                vendor, gameData, items, out ShopStockRange[] ranges, out string failure);

            Assert.IsFalse(built);
            Assert.AreEqual(0, ranges.Length);
            StringAssert.Contains(failure, "Stock row 2");
            StringAssert.Contains(failure, "invalid effective QL range");
        }

        static ShopVendorData Vendor(IList<ShopStockData> stock)
            => new(
                vendorId: 42926084,
                playfieldId: 655,
                vendorName: "Test vendor",
                placedTemplateId: 297424,
                vendorTemplateHash: "TEST",
                vendorTemplateRowId: 1,
                vendorItemTemplateId: 297424,
                stockGroupHash: "ABCD",
                minimumQuality: 1,
                maximumQuality: 50,
                buyModifier: 0.04f,
                sellModifier: 1.05f,
                pricingSkill: 161,
                stock: stock);
    }
}
