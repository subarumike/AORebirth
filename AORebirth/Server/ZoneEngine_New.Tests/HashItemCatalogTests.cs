namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class HashItemCatalogTests
    {
        const string CatalogJson =
            """
            {
                "WEAP": {
                    "Children": [ "PSTL", "RIFL", "SMGN" ]
                },
                "PSTL": {
                    "Templates": [254633, 254634, 254635, 254636, 254637, 254638, 254639]
                },
                "RIFL": {
                    "Templates": [257128]
                },
                "MOPA": {
                    "Templates": [42640, 42641]
                }
            }
            """;

        static readonly Dictionary<int, int> SampleQualities = new()
        {
            [42640] = 1,
            [42641] = 400,
            [254633] = 1,
            [254634] = 50,
            [254635] = 100,
            [254636] = 150,
            [254637] = 200,
            [254638] = 250,
            [254639] = 300,
            [257128] = 300
        };

        [TestMethod]
        public void CategoryWeapResolvesToHashInstanceChild()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(CatalogJson, new FixedRandom(0));

            Assert.IsTrue(catalog.TryGetCategory("WEAP", out IReadOnlyList<string> children));
            CollectionAssert.AreEqual(new[] { "PSTL", "RIFL", "SMGN" }, new List<string>(children));
            Assert.IsTrue(catalog.TryResolveInstance("WEAP", out HashInstance instance));
            Assert.AreEqual("PSTL", instance.Hash);
        }

        [TestMethod]
        public void DirectHashInstanceSkipsCategorySelection()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(CatalogJson);

            Assert.IsFalse(catalog.TryGetCategory("PSTL", out _));
            Assert.IsFalse(catalog.TryGetCategory("MOPA", out _));
            Assert.IsTrue(catalog.TryResolveInstance("PSTL", out HashInstance pistol));
            Assert.AreEqual("PSTL", pistol.Hash);
            Assert.IsTrue(catalog.TryResolveInstance("MOPA", out HashInstance mopa));
            Assert.AreEqual("MOPA", mopa.Hash);
        }

        [TestMethod]
        public void ParentDescriptionIsIgnoredAndChildrenWinOverTemplates()
        {
            var catalog = HashItemCatalog.Parse("""
                {"WEAP":{"Description":"Weapons","Children":["PSTL","SMGN"],"Templates":[1]},"PSTL":{"Templates":[254633]}}
                """, new FixedRandom(0));
            Assert.IsTrue(catalog.TryGetCategory("WEAP", out var children));
            CollectionAssert.AreEqual(new[] { "PSTL", "SMGN" }, new List<string>(children));
            Assert.IsTrue(catalog.TryResolveInstance("WEAP", out var instance));
            Assert.AreEqual("PSTL", instance.Hash);
            Assert.IsFalse(catalog.TryResolveInstance("SMGN", out _));
        }

        [TestMethod]
        public void EmptyOrInvalidTemplateIdsDoNotBecomeLeaves()
        {
            var catalog = HashItemCatalog.Parse("""
                {"WEAP":{"Children":["DEAD","ZERO"]},"DEAD":{"Templates":[]},"ZERO":{"Templates":[0,-1]}}
                """);
            Assert.IsTrue(catalog.TryGetCategory("WEAP", out var children));
            CollectionAssert.AreEqual(new[] { "DEAD", "ZERO" }, new List<string>(children));
            Assert.IsFalse(catalog.TryResolveInstance("WEAP", out _));
            Assert.IsFalse(catalog.TryGetInstance("DEAD", out _));
            Assert.IsFalse(catalog.TryGetInstance("ZERO", out _));
        }

        [TestMethod]
        public void UnknownHashIncludingLegacyLootTableFails()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(CatalogJson);

            Assert.IsFalse(catalog.TryResolveInstance("AAAA", out _));
            Assert.IsFalse(catalog.TryResolveInstance("AAAB", out _));
            Assert.IsFalse(catalog.TryResolveInstance("MSTA", out _));

            // A category child with no Templates entry is a dead leaf, not a resolvable item.
            Assert.IsFalse(catalog.TryResolveInstance("SMGN", out _));
            Assert.IsFalse(catalog.TryResolveInstance(string.Empty, out _));
        }

        [TestMethod]
        public void SpawnAllParentResolvesEveryBranch()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(
                """
                {
                    "ALL": { "SpawnAll": true, "Children": ["PSTL", "RIFL", "NEST"] },
                    "NEST": { "Children": ["MOPA", "GONE"] },
                    "ONE": { "Children": ["RIFL", "PSTL"] },
                    "OFF": { "SpawnAll": false, "Children": ["PSTL", "RIFL"] },
                    "PSTL": { "Templates": [1] },
                    "RIFL": { "Templates": [2] },
                    "MOPA": { "Templates": [3] },
                    "LOOP": { "SpawnAll": true, "Children": ["LOOP", "PSTL"] }
                }
                """,
                new FixedRandom(0));

            var all = new List<HashInstance>();
            catalog.CollectSpawns("ALL", all);
            CollectionAssert.AreEqual(new[] { "PSTL", "RIFL", "MOPA" }, all.ConvertAll(item => item.Hash));

            var one = new List<HashInstance>();
            catalog.CollectSpawns("ONE", one);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual("RIFL", one[0].Hash);

            var off = new List<HashInstance>();
            catalog.CollectSpawns("OFF", off);
            Assert.AreEqual(1, off.Count);
            Assert.AreEqual("PSTL", off[0].Hash);

            var loop = new List<HashInstance>();
            catalog.CollectSpawns("LOOP", loop);
            Assert.AreEqual(1, loop.Count);
            Assert.AreEqual("PSTL", loop[0].Hash);

            Assert.IsTrue(catalog.TryResolveInstance("ALL", out HashInstance single));
            Assert.AreEqual("PSTL", single.Hash);
            Assert.IsTrue(catalog.CanResolveItem("ALL"));
            Assert.IsFalse(catalog.CanResolveItem("GONE"));
        }

        [TestMethod]
        public void SpawnAllLootMintsEveryBranch()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(
                """
                {
                    "ALL": { "SpawnAll": true, "Children": ["PSTL", "RIFL"] },
                    "PSTL": { "Templates": [1] },
                    "RIFL": { "Templates": [2] }
                }
                """,
                new FixedRandom(0));
            var minter = new HashItemMinter(new StubGameData(catalog), new StubCatalog(), new StubItemBuilder());
            var items = new List<Item>();

            minter.MintSpawns("ALL", 10, ItemSource.Loot, items);

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(1, items[0].LowId);
            Assert.AreEqual(2, items[1].LowId);
        }

        [TestMethod]
        public void ClampQualityFloorsBelowOne()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(CatalogJson);
            Assert.IsTrue(catalog.TryGetInstance("PSTL", out HashInstance pistol));
            Assert.AreEqual(1, HashItemCatalog.ClampQuality(pistol, 0));
            Assert.AreEqual(150, HashItemCatalog.ClampQuality(pistol, 150));
            Assert.AreEqual(500, HashItemCatalog.ClampQuality(pistol, 500));

            Assert.IsTrue(catalog.TryGetInstance("RIFL", out HashInstance rifle));
            Assert.AreEqual(1, HashItemCatalog.ClampQuality(rifle, 1));
            Assert.AreEqual(300, HashItemCatalog.ClampQuality(rifle, 300));
        }

        [TestMethod]
        public void SelectIdsUsesSinglePairAndMultiBand()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(CatalogJson);
            int QualityOf(int id) => SampleQualities[id];

            Assert.IsTrue(catalog.TryGetInstance("RIFL", out HashInstance rifle));
            Assert.IsTrue(HashItemCatalog.TrySelectIds(rifle, 300, QualityOf, out int rifleLow, out int rifleHigh));
            Assert.AreEqual(257128, rifleLow);
            Assert.AreEqual(257128, rifleHigh);

            Assert.IsTrue(catalog.TryGetInstance("MOPA", out HashInstance mopa));
            Assert.IsTrue(HashItemCatalog.TrySelectIds(mopa, 200, QualityOf, out int mopaLow, out int mopaHigh));
            Assert.AreEqual(42640, mopaLow);
            Assert.AreEqual(42641, mopaHigh);

            Assert.IsTrue(catalog.TryGetInstance("PSTL", out HashInstance pistol));
            Assert.IsTrue(HashItemCatalog.TrySelectIds(pistol, 75, QualityOf, out int midLow, out int midHigh));
            Assert.AreEqual(254634, midLow);
            Assert.AreEqual(254635, midHigh);

            Assert.IsTrue(HashItemCatalog.TrySelectIds(pistol, 1, QualityOf, out int lowLow, out int lowHigh));
            Assert.AreEqual(254633, lowLow);
            Assert.AreEqual(254634, lowHigh);

            Assert.IsTrue(HashItemCatalog.TrySelectIds(pistol, 300, QualityOf, out int highLow, out int highHigh));
            Assert.AreEqual(254638, highLow);
            Assert.AreEqual(254639, highHigh);
        }

        sealed class FixedRandom : Random
        {
            readonly int _value;

            public FixedRandom(int value)
            {
                _value = value;
            }

            public override int Next(int maxValue)
                => _value;
        }
    }
}
