namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LegacyGameplaySourceTests
    {
        [TestMethod]
        public void HistoricalCatalogIncludesEveryRetainedSharedFragment()
        {
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            string directory = Path.Combine(root, "Tests", "Fixtures", "Gameplay", "Playfields");
            string owner = Path.Combine(directory, "CapturedEnemyCombatProfileCatalog.cs");
            string source = LegacyGameplaySource.ReadAllText(owner);
            foreach (string fragment in new[] { "CapturedEnemyCombatProfileCatalog.cs", "CapturedEnemyCombatProfileData.cs",
                "CapturedEnemyCombatProfileMatching.cs" })
                Assert.IsTrue(source.Contains(File.ReadAllText(LegacyGameplaySource.ResolveFragment(owner, fragment))));
            string[] logical = LegacyGameplaySource.LogicalPaths(new[]
            {
                owner,
                LegacyGameplaySource.ResolveFragment(owner, "CapturedEnemyCombatProfileData.cs"),
                LegacyGameplaySource.ResolveFragment(owner, "CapturedEnemyCombatProfileMatching.cs")
            });
            CollectionAssert.AreEqual(new[] { owner }, logical);
        }

        [TestMethod]
        public void UnrelatedSourceIsUnchangedByLogicalPartialReader()
        {
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            string path = Path.Combine(root, "AORebirth", "Server", "ZoneEngine_New", "SharedGameplay", "Combat", "NpcAiProfile.cs");
            Assert.AreEqual(File.ReadAllText(path), LegacyGameplaySource.ReadAllText(path));
        }
    }
}
