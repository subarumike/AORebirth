namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LegacyGameplaySourceTests
    {
        [TestMethod]
        public void LogicalContractIncludesRuntimeAndEveryExtractedPureFragment()
        {
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            string directory = Path.Combine(root, "AORebirth", "Server", "ZoneEngine", "Core", "Playfields");
            string source = LegacyGameplaySource.ReadAllText(Path.Combine(directory, "CapturedEnemyCombatContract.cs"));
            foreach (string fragment in new[] { "CapturedEnemyCombatContract.cs", "CapturedEnemyCombatData.cs",
                "CapturedEnemyCombatSequenceData.cs", "CapturedEnemyCombatContract.Data.cs" })
                Assert.IsTrue(source.Contains(File.ReadAllText(Path.Combine(directory, fragment))));
            string[] logical = LegacyGameplaySource.LogicalPaths(new[]
            {
                Path.Combine(directory, "CapturedEnemyCombatContract.cs"),
                Path.Combine(directory, "CapturedEnemyCombatData.cs"),
                Path.Combine(directory, "CapturedEnemyCombatSequenceData.cs"),
                Path.Combine(directory, "CapturedEnemyCombatContract.Data.cs")
            });
            CollectionAssert.AreEqual(new[] { Path.Combine(directory, "CapturedEnemyCombatContract.cs") }, logical);
        }

        [TestMethod]
        public void UnrelatedSourceIsUnchangedByLogicalPartialReader()
        {
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            string path = Path.Combine(root, "AORebirth", "Server", "ZoneEngine", "Core", "NpcAiProfile.cs");
            Assert.AreEqual(File.ReadAllText(path), LegacyGameplaySource.ReadAllText(path));
        }
    }
}
