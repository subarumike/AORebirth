namespace ZoneEngine_New.Tests
{
    using System.Linq;
    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine_New.Core.Mobs;

    [TestClass]
    public sealed class CapturedNpcCombatResolverTests
    {
        private static CapturedEnemyCombatProfileDefinition Profile()
            => CapturedEnemyCombatGeneratedProfiles.Create().Single(p => p.ProfileId == "002f6bffabdaa21f-744247ca24ac9229");
        private static CapturedNpcCombatEvidence Evidence(CapturedEnemyCombatProfileDefinition p)
            => new(p.ResourceId, p.Name, p.MonsterData, p.Level, p.SourceIdentities[0], p.ProfileId);

        [TestMethod]
        public void ExactOriginalSourceAndSelectorResolveCheckedInProductionProfile()
        {
            var p = Profile();
            Assert.IsTrue(CapturedNpcCombatResolver.TryResolve(Evidence(p), out var actual, out string failure), failure);
            Assert.AreEqual(p.ProfileId, actual.ProfileId);
        }

        [TestMethod]
        public void SameNameAndLevelWithoutOriginalSourceDoNotAuthorizeProfile()
        {
            var p = Profile();
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(Evidence(p) with { SourceIdentity = 0 }, out _, out _));
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(Evidence(p) with { SourceIdentity = 12345 }, out _, out _));
        }

        [TestMethod]
        public void NoNearestLevelResourceAppearanceOrSelectorFallback()
        {
            var key = Evidence(Profile());
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(key with { Level = key.Level + 1 }, out _, out _));
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(key with { ResourceId = key.ResourceId + 1 }, out _, out _));
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(key with { MonsterData = key.MonsterData + 1 }, out _, out _));
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(key with { ProfileSelector = "missing" }, out _, out _));
        }

        [TestMethod]
        public void AmbiguousExactSourcesFailClosed()
        {
            var p = Profile();
            Assert.IsFalse(CapturedNpcCombatResolver.TryResolve(new[] { p, p }, Evidence(p), out _, out _));
        }
    }
}
