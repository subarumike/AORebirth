namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;

    [TestClass]
    public sealed class HashItemCatalogTests
    {
        const string TemplatesJson =
            """
            {
                "WEAP": {
                    "PSTL": { "Description": "Pistols", "Hash": "PSTL", "ParentHash": "WEPN" },
                    "RIFL": { "Description": "Rifles", "Hash": "RIFL", "ParentHash": "WEPN" },
                    "SMGN": { "Description": "Sub Machine Guns", "Hash": "MSTA", "ParentHash": "WEPN" }
                }
            }
            """;

        const string InstancesJson =
            """
            {
                "MOPA": { "TemplateId": [42640, 42641], "MinLevel": 1, "MaxLevel": 400 },
                "PSTL": { "TemplateId": [254633, 254634, 254635, 254636, 254637, 254638, 254639], "MinLevel": 1, "MaxLevel": 300 },
                "RIFL": { "TemplateId": [257128], "MinLevel": 300, "MaxLevel": 300 }
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
            HashItemCatalog catalog = HashItemCatalog.Parse(TemplatesJson, InstancesJson, new FixedRandom(0));

            Assert.IsTrue(catalog.TryGetCategory("WEAP", out IReadOnlyList<string> children));
            CollectionAssert.AreEqual(new[] { "PSTL", "RIFL", "MSTA" }, new List<string>(children));
            Assert.IsTrue(catalog.TryResolveInstance("WEAP", out HashInstance instance));
            Assert.AreEqual("PSTL", instance.Hash);
        }

        [TestMethod]
        public void DirectHashInstanceSkipsCategorySelection()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(TemplatesJson, InstancesJson);

            Assert.IsFalse(catalog.TryGetCategory("PSTL", out _));
            Assert.IsFalse(catalog.TryGetCategory("MOPA", out _));
            Assert.IsTrue(catalog.TryResolveInstance("PSTL", out HashInstance pistol));
            Assert.AreEqual("PSTL", pistol.Hash);
            Assert.IsTrue(catalog.TryResolveInstance("MOPA", out HashInstance mopa));
            Assert.AreEqual("MOPA", mopa.Hash);
        }

        [TestMethod]
        public void PropertyKeyFormatResolvesOnlyRealInstanceLeavesAndSkipsScalarMetadata()
        {
            var catalog = HashItemCatalog.Parse("""
                {"WEAP":{"Description":"Weapons","ParentHash":"ROOT","PSTL":{},"SMGN":{}}}
                """, InstancesJson, new FixedRandom(0));
            Assert.IsTrue(catalog.TryGetCategory("WEAP", out var children));
            CollectionAssert.AreEqual(new[] { "PSTL", "SMGN" }, new List<string>(children));
            Assert.IsTrue(catalog.TryResolveInstance("WEAP", out var instance));
            Assert.AreEqual("PSTL", instance.Hash); Assert.IsFalse(catalog.TryResolveInstance("SMGN", out _));
        }

        [TestMethod]
        public void ExplicitAliasIsPreservedAndMalformedHashDoesNotAcquireAPropertyFallback()
        {
            var catalog = HashItemCatalog.Parse("""
                {"WEAP":{"SMGN":{"Hash":"MSTA"},"PSTL":{"Hash":17},"RIFL":{"Hash":""}}}
                """, InstancesJson);
            Assert.IsTrue(catalog.TryGetCategory("WEAP", out var children));
            CollectionAssert.AreEqual(new[] { "MSTA" }, new List<string>(children));
            Assert.IsFalse(catalog.TryResolveInstance("WEAP", out _));
        }

        [TestMethod]
        public void UnknownHashIncludingLegacyLootTableFails()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(TemplatesJson, InstancesJson);

            Assert.IsFalse(catalog.TryResolveInstance("AAAA", out _));
            Assert.IsFalse(catalog.TryResolveInstance("AAAB", out _));
            Assert.IsFalse(catalog.TryResolveInstance("MSTA", out _));
            Assert.IsFalse(catalog.TryResolveInstance(string.Empty, out _));
        }

        [TestMethod]
        public void ClampQualityUsesInstanceMinMax()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(TemplatesJson, InstancesJson);
            Assert.IsTrue(catalog.TryGetInstance("PSTL", out HashInstance pistol));
            Assert.AreEqual(1, HashItemCatalog.ClampQuality(pistol, 0));
            Assert.AreEqual(150, HashItemCatalog.ClampQuality(pistol, 150));
            Assert.AreEqual(300, HashItemCatalog.ClampQuality(pistol, 500));

            Assert.IsTrue(catalog.TryGetInstance("RIFL", out HashInstance rifle));
            Assert.AreEqual(300, HashItemCatalog.ClampQuality(rifle, 1));
            Assert.AreEqual(300, HashItemCatalog.ClampQuality(rifle, 300));
        }

        [TestMethod]
        public void SelectIdsUsesSinglePairAndMultiBand()
        {
            HashItemCatalog catalog = HashItemCatalog.Parse(TemplatesJson, InstancesJson);
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
