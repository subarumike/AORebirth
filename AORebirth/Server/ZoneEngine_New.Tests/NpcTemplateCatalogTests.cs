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
            Assert.IsTrue(aaaa.Attackable);
            Assert.IsFalse(aaaa.UnresolvedPlaceholder);

            Assert.IsTrue(catalog.CanResolve("ZZZZ"));
            Assert.IsTrue(catalog.TryResolve("ZZZZ", 1, out MobTemplate fallback));
            Assert.AreEqual("AAAA", fallback.Hash);
            Assert.AreEqual("To Be Determined", fallback.Name);
            Assert.IsTrue(fallback.Attackable);
            Assert.IsFalse(fallback.UnresolvedPlaceholder);

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
