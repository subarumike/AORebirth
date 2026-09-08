namespace ZoneEngine_New.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Mobs;

    [TestClass]
    public sealed class NpcTemplateLevelPolicyTests
    {
        [TestMethod]
        public void ExactLevelPreservesAllTemplateStats()
        {
            MobTemplate template = Template();
            NpcTemplateLevelPolicy.RequireExactLevel(template, 7);
            Assert.AreEqual(7, template.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(123, template.Stats[(int)CharacterStat.MaxHealth]);
        }

        [TestMethod]
        public void NoOverrideRetainsExistingTemplateBehavior()
        {
            NpcTemplateLevelPolicy.RequireExactLevel(Template(), null);
        }

        [DataTestMethod]
        [DataRow(6)]
        [DataRow(8)]
        [DataRow(20)]
        public void PlacementRangeDoesNotAuthorizeOtherLevelStats(int level)
        {
            MobTemplate template = Template();
            Assert.ThrowsException<InvalidOperationException>(() => NpcTemplateLevelPolicy.RequireExactLevel(template, level));
            Assert.AreEqual(7, template.Stats[(int)CharacterStat.Level]);
            Assert.AreEqual(123, template.Stats[(int)CharacterStat.MaxHealth]);
        }

        [TestMethod]
        public void FixedPlacementLevelWithoutStatVariantStillFailsClosed()
        {
            MobTemplate template = Template();
            template.MinLevel = template.MaxLevel = 7;
            template.Stats.Remove((int)CharacterStat.Level);
            Assert.ThrowsException<InvalidOperationException>(() => NpcTemplateLevelPolicy.RequireExactLevel(template, 7));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void NonPositiveOverridesAreRejected(int level)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => NpcTemplateLevelPolicy.RequireExactLevel(Template(), level));
        }

        private static MobTemplate Template() => new()
        {
            Hash = "level-policy-fixture", MinLevel = 1, MaxLevel = 20,
            Stats = { [(int)CharacterStat.Level] = 7, [(int)CharacterStat.MaxHealth] = 123 }
        };
    }
}
