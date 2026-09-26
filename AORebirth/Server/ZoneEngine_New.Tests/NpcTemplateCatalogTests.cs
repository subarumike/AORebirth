namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;

    using AORebirth.Enums;

    [TestClass]
    public sealed class NpcTemplateCatalogTests
    {
        const string SampleJson =
            """
            {
                "LEAF": {
                    "Templates": [
                        {
                            "Name": "BandOne",
                            "Level": 1,
                            "Stats": { "1": 10, "54": 1 },
                            "Equipment": [[10, 11]],
                            "LootTable": [{ "Hash": "AAA1", "Repeats": 1, "Chance": 100, "LevelMod": 0 }]
                        },
                        {
                            "Name": "BandThirty",
                            "Level": 30,
                            "Stats": { "1": 100, "54": 30 }
                        },
                        {
                            "Name": "BandFifty",
                            "Level": 50,
                            "Stats": { "1": 200, "54": 50 },
                            "Equipment": [[50, 51]],
                            "LootTable": [{ "Hash": "LOW", "Repeats": 1, "Chance": 10, "LevelMod": 1 }]
                        },
                        {
                            "Name": "BandNinety",
                            "Level": 90,
                            "Stats": { "1": 400, "54": 90 },
                            "Equipment": [[90, 91]],
                            "LootTable": [{ "Hash": "HIGH", "Repeats": 1, "Chance": 20, "LevelMod": 2 }]
                        }
                    ]
                },
                "FAM": { "Children": ["LEAF"] },
                "CYCLE": { "Children": ["CYCLE"] },
                "EMPTY": { "Children": [] }
            }
            """;

        [TestMethod]
        public void InterpolatesBetweenAdjacentBands()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson);

            Assert.IsTrue(catalog.TryResolve("LEAF", 70, out MobTemplate template));
            Assert.AreEqual("LEAF", template.Hash);
            Assert.AreEqual(70, template.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(300, template.Stats[1]);
            Assert.AreEqual(1, template.MinLevel);
            Assert.AreEqual(90, template.MaxLevel);
            Assert.AreEqual("BandFifty", template.Name);
            Assert.AreEqual(1, template.Equipment.Count);
            CollectionAssert.AreEqual(new[] { 50, 51 }, template.Equipment[0]);
            Assert.AreEqual(1, template.ItemTable.Count);
            Assert.AreEqual("LOW", template.ItemTable[0].Hash);
        }

        [TestMethod]
        public void ClampsBelowMinAndAboveMax()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson);

            Assert.IsTrue(catalog.TryResolve("LEAF", 0, out MobTemplate low));
            Assert.AreEqual(1, low.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(10, low.Stats[1]);
            Assert.AreEqual("BandOne", low.Name);

            Assert.IsTrue(catalog.TryResolve("LEAF", 500, out MobTemplate high));
            Assert.AreEqual(90, high.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(400, high.Stats[1]);
            Assert.AreEqual("BandNinety", high.Name);
        }

        [TestMethod]
        public void ExactBandMatchUsesThatTemplate()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson);

            Assert.IsTrue(catalog.TryResolve("LEAF", 50, out MobTemplate template));
            Assert.AreEqual(50, template.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(200, template.Stats[1]);
            Assert.AreEqual("BandFifty", template.Name);
        }

        [TestMethod]
        public void FamilyPicksChildAndNeverReadsParentStats()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson, new FixedRandom(0));

            Assert.IsTrue(catalog.CanResolve("FAM"));
            Assert.IsFalse(catalog.TryGetLeaf("FAM", out _));
            Assert.IsTrue(catalog.TryResolve("FAM", 70, out MobTemplate template));
            Assert.AreEqual("LEAF", template.Hash);
            Assert.AreEqual(300, template.Stats[1]);
        }

        [TestMethod]
        public void SpawnAllFamilyResolvesEveryBranch()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(
                """
                {
                    "LEAF": {
                        "Templates": [ { "Name": "Leaf", "Level": 1, "Stats": { "54": 1 } } ]
                    },
                    "OTHER": {
                        "Templates": [ { "Name": "Other", "Level": 1, "Stats": { "54": 1 } } ]
                    },
                    "PACK": { "SpawnAll": true, "Children": ["LEAF", "NEST", "MISSING"] },
                    "NEST": { "Children": ["OTHER", "LEAF"] },
                    "ONE": { "Children": ["OTHER", "LEAF"] },
                    "INNER": { "SpawnAll": true, "Children": ["OTHER"] },
                    "OUTER": { "SpawnAll": true, "Children": ["INNER", "LEAF"] },
                    "LOOP": { "SpawnAll": true, "Children": ["LOOP", "LEAF"] }
                }
                """,
                new FixedRandom(0));

            var pack = new List<MobTemplate>();
            catalog.CollectSpawns("PACK", 1, pack);
            CollectionAssert.AreEqual(new[] { "LEAF", "OTHER" }, pack.ConvertAll(template => template.Hash));

            var one = new List<MobTemplate>();
            catalog.CollectSpawns("ONE", 1, one);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual("OTHER", one[0].Hash);

            var outer = new List<MobTemplate>();
            catalog.CollectSpawns("OUTER", 1, outer);
            CollectionAssert.AreEqual(new[] { "OTHER", "LEAF" }, outer.ConvertAll(template => template.Hash));

            var loop = new List<MobTemplate>();
            catalog.CollectSpawns("LOOP", 1, loop);
            Assert.AreEqual(1, loop.Count);
            Assert.AreEqual("LEAF", loop[0].Hash);

            Assert.IsTrue(catalog.TryResolve("PACK", 1, out MobTemplate single));
            Assert.AreEqual("LEAF", single.Hash);
        }

        [TestMethod]
        public void FamilyPicksOnlyChildrenCoveringRequestedLevel()
        {
            const string json =
                """
                {
                    "LEET": {
                        "Templates": [
                            { "Name": "Leet", "Level": 1, "Stats": { "54": 1 } },
                            { "Name": "Leet", "Level": 5, "Stats": { "54": 5 } }
                        ]
                    },
                    "PHEAR": {
                        "Templates": [ { "Name": "Phear Leet", "Level": 17, "Stats": { "54": 17 } } ]
                    },
                    "BIG": {
                        "Templates": [ { "Name": "Big Leet", "Level": 30, "Stats": { "54": 30 } } ]
                    },
                    "NEST": { "Children": ["BIG"] },
                    "FAM": { "Children": ["PHEAR", "NEST", "LEET"] }
                }
                """;

            for (int roll = 0; roll < 3; roll++)
            {
                NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(json, new FixedRandom(roll));

                var low = new List<MobTemplate>();
                catalog.CollectSpawns("FAM", 1, low);
                Assert.AreEqual(1, low.Count);
                Assert.AreEqual("LEET", low[0].Hash);
                Assert.AreEqual(1, low[0].Stats[(int)CharacterStat.Level]);

                Assert.IsTrue(catalog.TryResolve("FAM", 17, out MobTemplate mid));
                Assert.AreEqual("PHEAR", mid.Hash);

                // No child covers 10: nearest range wins (LEET max 5 is 5 away, PHEAR 17 is 7 away).
                Assert.IsTrue(catalog.TryResolve("FAM", 10, out MobTemplate gap));
                Assert.AreEqual("LEET", gap.Hash);
                Assert.AreEqual(5, gap.Stats[(int)CharacterStat.Level]);

                Assert.IsTrue(catalog.TryResolve("FAM", 200, out MobTemplate high));
                Assert.AreEqual("BIG", high.Hash);
            }
        }

        [TestMethod]
        public void UnknownAndCyclicFamiliesFail()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson);

            Assert.IsFalse(catalog.CanResolve("MISSING"));
            Assert.IsFalse(catalog.CanResolve("CYCLE"));
            Assert.IsFalse(catalog.CanResolve("EMPTY"));
            Assert.IsFalse(catalog.TryResolve("MISSING", 10, out _));
            Assert.IsFalse(catalog.TryResolve("CYCLE", 10, out _));
        }

        [TestMethod]
        public void MaterializeCopiesEquipmentAndIdentityFields()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(
                """
                {
                    "LEAF": {
                        "Templates": [
                            {
                                "Name": "Editable Fixture",
                                "Level": 1,
                                "TemplateId": 43296,
                                "HasHeadMesh": false,
                                "Attackable": true,
                                "KnuBotId": 1131,
                                "Equipment": [[120912, 120912], [120915, 120915]],
                                "LootTable": [{ "Hash": "LEAF", "Repeats": 1, "Chance": 100, "LevelMod": 25 }]
                            },
                            {
                                "Name": "Editable Fixture",
                                "Level": 250,
                                "TemplateId": 43296,
                                "KnuBotId": 1131,
                                "Equipment": [[120912, 120912], [120915, 120915]]
                            }
                        ]
                    }
                }
                """);

            Assert.IsTrue(catalog.TryResolve("LEAF", 25, out MobTemplate template));
            Assert.AreEqual("LEAF", template.Hash);
            Assert.AreEqual("Editable Fixture", template.Name);
            Assert.AreEqual(43296, template.TemplateId);
            Assert.AreEqual(1131, template.KnuBotId);
            Assert.IsTrue(template.Attackable);
            Assert.AreEqual(1, template.MinLevel);
            Assert.AreEqual(250, template.MaxLevel);
            Assert.AreEqual(2, template.Equipment.Count);
            CollectionAssert.AreEqual(new[] { 120912, 120912 }, template.Equipment[0]);
            CollectionAssert.AreEqual(new[] { 120915, 120915 }, template.Equipment[1]);
            Assert.AreEqual(1, template.ItemTable.Count);
            Assert.AreEqual("LEAF", template.ItemTable[0].Hash);
        }

        [TestMethod]
        public void MissingHashFallsBackToAaaa()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(
                """
                {
                    "AAAA": {
                        "Templates": [
                            { "Name": "To Be Determined", "Level": 1, "Attackable": true }
                        ]
                    },
                    "CYCLE": { "Children": ["CYCLE"] }
                }
                """);

            Assert.IsTrue(catalog.CanResolve("AAAA"));
            Assert.IsTrue(catalog.TryResolve("AAAA", 1, out MobTemplate aaaa));
            Assert.AreEqual("AAAA", aaaa.Hash);
            Assert.AreEqual("To Be Determined", aaaa.Name);
            Assert.IsFalse(aaaa.Attackable);
            Assert.IsTrue(aaaa.UnresolvedPlaceholder);

            Assert.IsTrue(catalog.CanResolve("ZZZZ"));
            Assert.IsTrue(catalog.TryResolve("ZZZZ", 1, out MobTemplate fallback));
            Assert.AreEqual("AAAA", fallback.Hash);
            Assert.AreEqual("To Be Determined", fallback.Name);
            Assert.IsFalse(fallback.Attackable);
            Assert.IsTrue(fallback.UnresolvedPlaceholder);

            Assert.IsFalse(catalog.CanResolve("CYCLE"));
            Assert.IsFalse(catalog.TryResolve("CYCLE", 1, out _));
        }

        [TestMethod]
        public void LootTableMapsOntoItemTable()
        {
            NpcTemplateCatalog catalog = NpcTemplateCatalog.Parse(SampleJson);

            Assert.IsTrue(catalog.TryResolve("LEAF", 1, out MobTemplate template));
            Assert.AreEqual(1, template.ItemTable.Count);
            Assert.AreEqual("AAA1", template.ItemTable[0].Hash);
            Assert.AreEqual(100, template.ItemTable[0].Chance);
        }

        [TestMethod]
        public void WearBonusesApplyModifyFromEquipment()
        {
            var stats = new StatCollection();
            stats.Set(CharacterStat.Strength, 10);

            var page = new Container(IdentityType.WeaponPage, 0, 50);
            Item item = new()
            {
                LowId = 1,
                HighId = 1,
                Quality = 10,
                Definition = new ItemTemplate
                {
                    Id = 1,
                    Quality = 10,
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell
                            {
                                FunctionType = (int)FunctionType.Modify,
                                Arguments = [(int)CharacterStat.Strength, 5]
                            }
                        ]
                    }
                }
            };
            Assert.IsTrue(page.Add(0, item));

            stats.ClearBonuses(dirty: true);
            WearBonusApplier.ApplyContainer(page, includeWield: true, stats);
            Assert.AreEqual(15, stats.GetOrZero(CharacterStat.Strength));
        }

        [TestMethod]
        public void WearBonusesSkipHealthModify()
        {
            var stats = new StatCollection();
            stats.Set(CharacterStat.Health, 100);

            var page = new Container(IdentityType.ArmorPage, 0, 50);
            Item item = new()
            {
                LowId = 1,
                HighId = 1,
                Quality = 10,
                Definition = new ItemTemplate
                {
                    Id = 1,
                    Quality = 10,
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell
                            {
                                FunctionType = (int)FunctionType.Modify,
                                Arguments = [(int)CharacterStat.Health, 40]
                            },
                            new ItemSpell
                            {
                                FunctionType = (int)FunctionType.Modify,
                                Arguments = [(int)CharacterStat.MaxHealth, 25]
                            }
                        ]
                    }
                }
            };
            Assert.IsTrue(page.Add(0, item));

            stats.ClearBonuses(dirty: true);
            WearBonusApplier.ApplyContainer(page, includeWield: false, stats);
            Assert.AreEqual(0, stats.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            Assert.AreEqual(100, stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(25, stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Bonus));
        }

        sealed class FixedRandom : Random
        {
            readonly int _value;

            public FixedRandom(int value)
            {
                _value = value;
            }

            public override int Next(int maxValue)
                => Math.Min(_value, maxValue - 1);
        }
    }
}
