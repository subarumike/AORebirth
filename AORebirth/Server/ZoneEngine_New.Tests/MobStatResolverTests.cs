namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Mobs;

    [TestClass]
    public sealed class MobStatResolverTests
    {
        const int Health = 1;
        const int Breed = 4;
        const int CurrentHealth = 27;
        const int Level = 54;
        const int Skill = 100;
        const int Mesh = 359;

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [TestMethod]
        public void NoFamilyUsesTemplateStatsAndOptionalLevelOverride()
        {
            MobTemplate template = Vendor();

            Dictionary<int, int> raw = MobStatResolver.Resolve(template, level: null);
            Assert.AreEqual(1649, raw[Health]);
            Assert.AreEqual(40, raw[Level]);
            Assert.AreEqual(1, raw[Breed]);

            Dictionary<int, int> leveled = MobStatResolver.Resolve(template, level: 55);
            Assert.AreEqual(1649, leveled[Health]);
            Assert.AreEqual(55, leveled[Level]);
        }

        [TestMethod]
        public void FamilyCurveLerpsInsideSegmentsAndHoldsTheBreakpoint()
        {
            MobTemplate template = ScalingMob();
            NpcFamilyStatTemplate family = GenericFamily();

            Dictionary<int, int> atOne = MobStatResolver.Resolve(template, 1, family);
            Assert.AreEqual(12, atOne[Health]);
            Assert.AreEqual(9, atOne[Skill]);
            Assert.AreEqual(1, atOne[Level]);
            Assert.AreEqual(6, atOne[Breed]);
            Assert.AreEqual(26902, atOne[Mesh]);

            Dictionary<int, int> midLow = MobStatResolver.Resolve(template, 100, family);
            Assert.AreEqual(2493, midLow[Health]);
            Assert.AreEqual(84, midLow[Skill]);
            Assert.AreEqual(100, midLow[Level]);

            Dictionary<int, int> at200 = MobStatResolver.Resolve(template, 200, family);
            Assert.AreEqual(5000, at200[Health]);
            Assert.AreEqual(160, at200[Skill]);

            Dictionary<int, int> at201 = MobStatResolver.Resolve(template, 201, family);
            Assert.AreEqual(5200, at201[Health]);
            Assert.AreEqual(170, at201[Skill]);

            Dictionary<int, int> midHigh = MobStatResolver.Resolve(template, 225, family);
            Assert.AreEqual(6571, midHigh[Health]);
            Assert.AreEqual(194, midHigh[Skill]);
            Assert.AreEqual(225, midHigh[Level]);
        }

        [TestMethod]
        public void LevelsOutsideTheCurveClampToTheEndpoints()
        {
            MobTemplate template = ScalingMob();
            NpcFamilyStatTemplate family = Family(new Dictionary<int, Dictionary<int, int>>
            {
                [Health] = new() { [10] = 100, [60] = 900 }
            });

            Dictionary<int, int> below = MobStatResolver.Resolve(template, 5, family);
            Assert.AreEqual(100, below[Health]);
            Assert.AreEqual(5, below[Level]);

            Dictionary<int, int> above = MobStatResolver.Resolve(template, 120, family);
            Assert.AreEqual(900, above[Health]);
            Assert.AreEqual(120, above[Level]);
        }

        [TestMethod]
        public void TemplateStatsBeatTheFamilyCurve()
        {
            MobTemplate boss = ScalingMob();
            boss.Stats[Health] = 40000;
            boss.Stats[CurrentHealth] = 40000;

            Dictionary<int, int> resolved = MobStatResolver.Resolve(boss, 25, GenericFamily());
            Assert.AreEqual(40000, resolved[Health]);
            Assert.AreEqual(40000, resolved[CurrentHealth]);

            // Stats the boss does not override still come from the family.
            Assert.AreEqual(27, resolved[Skill]);
            Assert.AreEqual(25, resolved[Level]);
        }

        [TestMethod]
        public void CurvesWithDifferentKeypointCountsDoNotInterfere()
        {
            MobTemplate template = ScalingMob();
            NpcFamilyStatTemplate family = Family(new Dictionary<int, Dictionary<int, int>>
            {
                [Health] = new() { [1] = 12, [200] = 5000, [201] = 5200, [250] = 8000 },
                [Skill] = new() { [1] = 8, [250] = 1200 },
                [Breed] = new() { [1] = 3 }
            });

            Dictionary<int, int> resolved = MobStatResolver.Resolve(template, 125, family);
            Assert.AreEqual(3120, resolved[Health]);
            Assert.AreEqual(602, resolved[Skill]);

            // A single-keypoint curve is a constant, and the template still overrides it.
            Assert.AreEqual(6, resolved[Breed]);
        }

        [TestMethod]
        public void MissingLevelFallsBackToTemplateMinLevel()
        {
            MobTemplate template = ScalingMob();
            NpcFamilyStatTemplate family = GenericFamily();

            template.MinLevel = 201;
            Dictionary<int, int> atMin = MobStatResolver.Resolve(template, level: null, family);
            Assert.AreEqual(5200, atMin[Health]);
            Assert.AreEqual(201, atMin[Level]);

            template.MinLevel = 0;
            Dictionary<int, int> atFloor = MobStatResolver.Resolve(template, level: null, family);
            Assert.AreEqual(12, atFloor[Health]);
            Assert.AreEqual(1, atFloor[Level]);
        }

        [TestMethod]
        public void FamilyCatalogSkipsMalformedEntriesAndReportsThem()
        {
            List<string> errors = new();
            NpcFamilyStatCatalog catalog = NpcFamilyStatCatalog.Build(
                new Dictionary<int, NpcFamilyStatTemplateData>
                {
                    [1] = new()
                    {
                        Name = "Good",
                        StatCurves = new Dictionary<int, Dictionary<int, int>>
                        {
                            [Health] = new() { [1] = 10, [50] = 500 }
                        }
                    },
                    [0] = new() { Name = "Bad id" },
                    [7] = new() { Name = "No curves" }
                },
                errors.Add);

            Assert.AreEqual(1, catalog.Count);
            Assert.AreEqual(2, errors.Count);
            Assert.IsTrue(catalog.TryGet(1, out NpcFamilyStatTemplate good));
            Assert.AreEqual("Good", good.Name);
            Assert.IsFalse(catalog.TryGet(7, out _));
            Assert.IsFalse(catalog.TryGet(0, out _));
        }

        [TestMethod]
        public void CoverageValidationWarnsWhenSpawnRangeExceedsTheCurve()
        {
            NpcFamilyStatCatalog catalog = NpcFamilyStatCatalog.Build(
                new Dictionary<int, NpcFamilyStatTemplateData>
                {
                    [1] = new()
                    {
                        StatCurves = new Dictionary<int, Dictionary<int, int>>
                        {
                            [Health] = new() { [10] = 100, [60] = 900 }
                        }
                    }
                });

            List<string> warnings = new();
            catalog.ValidateCoverage(1, minLevel: 10, maxLevel: 60, "AAAA", warnings.Add);
            Assert.AreEqual(0, warnings.Count);

            catalog.ValidateCoverage(1, minLevel: 1, maxLevel: 200, "AAAA", warnings.Add);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "AAAA");
        }

        [TestMethod]
        public void JsonFamilyDeserializesAndResolves()
        {
            const string familyJson =
                """
                {
                  "1": {
                    "Name": "Generic Humanoid",
                    "StatCurves": {
                      "1":   { "1": 12,   "200": 5000 },
                      "100": { "1": 9,    "200": 160 }
                    }
                  }
                }
                """;

            const string templateJson =
                """
                {
                  "Hash": "AAAA",
                  "Stats": { "4": 6, "359": 26902 },
                  "NpcFamily": 1,
                  "MinLevel": 1,
                  "MaxLevel": 200
                }
                """;

            Dictionary<int, NpcFamilyStatTemplateData>? families =
                JsonSerializer.Deserialize<Dictionary<int, NpcFamilyStatTemplateData>>(familyJson, JsonOptions);
            Assert.IsNotNull(families);

            NpcFamilyStatCatalog catalog = NpcFamilyStatCatalog.Build(families);
            Assert.IsTrue(catalog.TryGet(1, out NpcFamilyStatTemplate family));
            Assert.AreEqual("Generic Humanoid", family.Name);

            MobTemplate? template = JsonSerializer.Deserialize<MobTemplate>(templateJson, JsonOptions);
            Assert.IsNotNull(template);
            Assert.AreEqual(1, template.NpcFamily);
            Assert.IsTrue(template.Attackable);

            Dictionary<int, int> mid = MobStatResolver.Resolve(template, 100, family);
            Assert.AreEqual(2493, mid[Health]);
            Assert.AreEqual(6, mid[Breed]);
            Assert.AreEqual(26902, mid[Mesh]);
        }

        [TestMethod]
        public void JsonTexturesDeserializeByPlace()
        {
            const string json =
                """
                {
                  "Hash": "EQVE",
                  "Textures": {
                    "1": 30862,
                    "3": 30839,
                    "4": 30886,
                    "2": 40903
                  }
                }
                """;

            MobTemplate? template = JsonSerializer.Deserialize<MobTemplate>(json, JsonOptions);
            Assert.IsNotNull(template);
            Assert.AreEqual(4, template.Textures.Count);
            Assert.AreEqual(30862, template.Textures[1]);
            Assert.AreEqual(30839, template.Textures[3]);
            Assert.AreEqual(30886, template.Textures[4]);
            Assert.AreEqual(40903, template.Textures[2]);
        }

        static MobTemplate Vendor()
        {
            return new MobTemplate
            {
                Hash = "EQVE",
                MinLevel = 40,
                MaxLevel = 40,
                Stats = new Dictionary<int, int>
                {
                    [Health] = 1649,
                    [Breed] = 1,
                    [CurrentHealth] = 1649,
                    [Level] = 40
                }
            };
        }

        static MobTemplate ScalingMob()
        {
            return new MobTemplate
            {
                Hash = "AAAA",
                NpcFamily = 1,
                MinLevel = 1,
                MaxLevel = 250,
                Stats = new Dictionary<int, int>
                {
                    [Breed] = 6,
                    [Mesh] = 26902
                }
            };
        }

        static NpcFamilyStatTemplate GenericFamily()
        {
            return Family(new Dictionary<int, Dictionary<int, int>>
            {
                [Health] = new() { [1] = 12, [200] = 5000, [201] = 5200, [250] = 8000 },
                [CurrentHealth] = new() { [1] = 12, [200] = 5000, [201] = 5200, [250] = 8000 },
                [Skill] = new() { [1] = 9, [200] = 160, [201] = 170, [250] = 220 }
            });
        }

        static NpcFamilyStatTemplate Family(Dictionary<int, Dictionary<int, int>> curves)
        {
            NpcFamilyStatCatalog catalog = NpcFamilyStatCatalog.Build(
                new Dictionary<int, NpcFamilyStatTemplateData>
                {
                    [1] = new() { Name = "Generic Humanoid", StatCurves = curves }
                });

            Assert.IsTrue(catalog.TryGet(1, out NpcFamilyStatTemplate family));
            return family;
        }
    }
}
