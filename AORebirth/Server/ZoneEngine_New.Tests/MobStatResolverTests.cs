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
        public void NoBandsUsesRootStatsAndOptionalLevelOverride()
        {
            MobTemplate template = RootOnly();

            Dictionary<int, int> raw = MobStatResolver.Resolve(template, level: null);
            Assert.AreEqual(1649, raw[Health]);
            Assert.AreEqual(40, raw[Level]);
            Assert.AreEqual(1, raw[Breed]);

            Dictionary<int, int> leveled = MobStatResolver.Resolve(template, level: 55);
            Assert.AreEqual(1649, leveled[Health]);
            Assert.AreEqual(55, leveled[Level]);
        }

        [TestMethod]
        public void SingleBandOverlaysWithoutLerp()
        {
            MobTemplate template = RootOnly();
            template.StatBands.Add(Band(25, health: 900, currentHealth: 900, level: 25, skill: 40));

            Dictionary<int, int> resolved = MobStatResolver.Resolve(template, level: 80);
            Assert.AreEqual(900, resolved[Health]);
            Assert.AreEqual(40, resolved[Skill]);
            Assert.AreEqual(80, resolved[Level]);
            Assert.AreEqual(1, resolved[Breed]);
        }

        [TestMethod]
        public void FourBandsLerpInsideSegmentsAndHoldTheBreakpoint()
        {
            MobTemplate template = FourBandTemplate();

            Dictionary<int, int> atOne = MobStatResolver.Resolve(template, 1);
            Assert.AreEqual(12, atOne[Health]);
            Assert.AreEqual(9, atOne[Skill]);
            Assert.AreEqual(1, atOne[Level]);
            Assert.AreEqual(6, atOne[Breed]);
            Assert.AreEqual(26902, atOne[Mesh]);

            Dictionary<int, int> midLow = MobStatResolver.Resolve(template, 100);
            Assert.AreEqual(2493, midLow[Health]);
            Assert.AreEqual(84, midLow[Skill]);
            Assert.AreEqual(100, midLow[Level]);

            Dictionary<int, int> at200 = MobStatResolver.Resolve(template, 200);
            Assert.AreEqual(5000, at200[Health]);
            Assert.AreEqual(160, at200[Skill]);

            Dictionary<int, int> at201 = MobStatResolver.Resolve(template, 201);
            Assert.AreEqual(5200, at201[Health]);
            Assert.AreEqual(170, at201[Skill]);

            Dictionary<int, int> midHigh = MobStatResolver.Resolve(template, 225);
            Assert.AreEqual(6571, midHigh[Health]);
            Assert.AreEqual(194, midHigh[Skill]);
            Assert.AreEqual(225, midHigh[Level]);
        }

        [TestMethod]
        public void OutsideTheBandsExtrapolatesTheNearestSegment()
        {
            MobTemplate template = FourBandTemplate();

            Dictionary<int, int> below = MobStatResolver.Resolve(template, 0);
            Assert.AreEqual(0, below[Health]);
            Assert.AreEqual(8, below[Skill]);
            Assert.AreEqual(0, below[Level]);

            Dictionary<int, int> above = MobStatResolver.Resolve(template, 300);
            Assert.AreEqual(10857, above[Health]);
            Assert.AreEqual(271, above[Skill]);
            Assert.AreEqual(300, above[Level]);
        }

        [TestMethod]
        public void OneSidedBandKeysStayConstant()
        {
            MobTemplate template = new()
            {
                Stats = new Dictionary<int, int> { [Breed] = 6 },
                StatBands =
                {
                    Band(1, health: 10, currentHealth: 10, level: 1, skill: 5),
                    new MobStatBand
                    {
                        Level = 10,
                        Stats = new Dictionary<int, int>
                        {
                            [Health] = 100,
                            [CurrentHealth] = 100,
                            [Level] = 10
                        }
                    }
                }
            };

            Dictionary<int, int> mid = MobStatResolver.Resolve(template, 5);
            Assert.AreEqual(50, mid[Health]);
            Assert.AreEqual(5, mid[Skill]);
        }

        [TestMethod]
        public void MissingLevelUsesTemplateMinThenFirstBand()
        {
            MobTemplate template = FourBandTemplate();
            template.MinLevel = 201;

            Dictionary<int, int> atMin = MobStatResolver.Resolve(template, level: null);
            Assert.AreEqual(5200, atMin[Health]);
            Assert.AreEqual(201, atMin[Level]);

            template.MinLevel = 0;
            Dictionary<int, int> atFirst = MobStatResolver.Resolve(template, level: null);
            Assert.AreEqual(12, atFirst[Health]);
            Assert.AreEqual(1, atFirst[Level]);
        }

        [TestMethod]
        public void JsonBandsDeserializeAndResolve()
        {
            const string json =
                """
                {
                  "Hash": "AAAA",
                  "Stats": { "4": 6, "359": 26902 },
                  "StatBands": [
                    { "Level": 1,   "Stats": { "1": 12,   "54": 1 } },
                    { "Level": 200, "Stats": { "1": 5000, "54": 200 } }
                  ],
                  "MinLevel": 1,
                  "MaxLevel": 200
                }
                """;

            MobTemplate? template = JsonSerializer.Deserialize<MobTemplate>(json, JsonOptions);
            Assert.IsNotNull(template);
            Assert.AreEqual(2, template.StatBands.Count);
            Assert.IsTrue(template.Attackable);

            Dictionary<int, int> mid = MobStatResolver.Resolve(template, 100);
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

        static MobTemplate RootOnly()
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

        static MobTemplate FourBandTemplate()
        {
            return new MobTemplate
            {
                Hash = "AAAA",
                MinLevel = 1,
                MaxLevel = 250,
                Stats = new Dictionary<int, int>
                {
                    [Breed] = 6,
                    [Mesh] = 26902
                },
                StatBands =
                {
                    Band(1, 12, 12, 1, 9),
                    Band(200, 5000, 5000, 200, 160),
                    Band(201, 5200, 5200, 201, 170),
                    Band(250, 8000, 8000, 250, 220)
                }
            };
        }

        static MobStatBand Band(int bandLevel, int health, int currentHealth, int level, int skill)
        {
            return new MobStatBand
            {
                Level = bandLevel,
                Stats = new Dictionary<int, int>
                {
                    [Health] = health,
                    [CurrentHealth] = currentHealth,
                    [Level] = level,
                    [Skill] = skill
                }
            };
        }
    }
}
