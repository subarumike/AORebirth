namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;
    using System.Runtime.CompilerServices;
    using AORebirth.Interfaces.Persistence.Missions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionDaoArchitectureTests
    {
        [TestMethod]
        public void SavedMissionSnapshotsPreserveOwnerVersionAndNullStepWithoutAliasing()
        {
            var saved = new MissionStateData
            {
                CharacterId = 71, QuestId = "saved.quest", CurrentStepId = null,
                Version = 9, CreatedAtUtcTicks = 12, UpdatedAtUtcTicks = 14,
                State = MissionLifecycleState.Active
            };
            var copy = saved.Clone();
            Assert.AreNotSame(saved, copy);
            Assert.AreEqual(71, copy.CharacterId);
            Assert.AreEqual("saved.quest", copy.QuestId);
            Assert.IsNull(copy.CurrentStepId);
            Assert.AreEqual(9L, copy.Version);
            Assert.AreEqual(12L, copy.CreatedAtUtcTicks);
            Assert.AreEqual(14L, copy.UpdatedAtUtcTicks);
            Assert.AreEqual(MissionLifecycleState.Active, copy.State);
            copy.Version = 10;
            Assert.AreEqual(9L, saved.Version);
        }

        [TestMethod]
        public void SavedMissionRecoveryUsesPersistenceContractWithoutProviderOrGameplayOrchestration()
        {
            string root = FindRepositoryRoot();
            string recovery = File.ReadAllText(Path.Combine(root,
                @"AORebirth\Server\ZoneEngine_New\Core\Characters\SavedMissionLocation.cs"));
            string contract = File.ReadAllText(Path.Combine(root,
                @"AORebirth\Libraries\Source\AORebirth.Interfaces\Persistence\Missions\IMissionDao.cs"));
            foreach (string source in new[] { recovery, contract })
            foreach (string forbidden in new[] { "System.Data", "Dapper", "IDbConnection", "MySql", "SELECT ",
                "MissionDaoRepositoryAdapter", "MissionRuntimeService", "MissionOfferStore" })
                Assert.IsFalse(source.Contains(forbidden), forbidden);
            string provider = File.ReadAllText(Path.Combine(root,
                @"AORebirth\Libraries\Source\AORebirth.Database\Domain\Missions\MySqlMissionDao.cs"));
            StringAssert.Contains(provider, "FOR UPDATE");
            StringAssert.Contains(provider, "connection.BeginTransaction()");
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            for (var directory = new FileInfo(sourcePath).Directory; directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }
    }
}
