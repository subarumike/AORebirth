namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldHydrationCharacterizationTests
    {
        [TestMethod]
        public void CurrentPlayfieldMaterializationOrderRemainsExplicitAndStable()
        {
            string root = FindRepositoryRoot();
            string source = ReadRepositoryFile(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs");

            AssertOrdered(
                source,
                "this.MaterializeDbMobSpawns(",
                "registerContent(playfieldIdentity);",
                "this.MaterializeVendors(",
                "this.MaterializeStaticDynels(",
                "refreshDynelRegistry();");
        }

        [TestMethod]
        public void CurrentLegacySourcePrecedenceRemainsCharacterized()
        {
            string root = FindRepositoryRoot();
            string loader = ReadRepositoryFile(
                root,
                @"AORebirth\Libraries\Source\PlayfieldLoader\PlayfieldLoader.cs");
            string contentData = ReadRepositoryFile(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs");

            AssertOrdered(
                loader,
                "MessagePackZip.UncompressData<PlayfieldData>(fname)",
                "TeleportDao.Instance.GetWhere(",
                "resolvedDestinationPlayfieldId == SubwayPlayfieldId",
                "ShouldSynthesizeReverseProxyExit(");
            AssertOrdered(
                contentData,
                "PlayfieldLoader.PFData.TryGetValue(",
                "this.isPrivateCityPlayfieldCandidate(",
                "MissionInstanceService.IsMissionInstancePlayfield(",
                "playfieldIdentity.Instance == 7001",
                "IsLuxuryApartmentPlayfield(",
                "return PlayfieldLoader.PFData[playfieldIdentity.Instance].Statels;");
        }

        [TestMethod]
        public void CurrentRuntimeCreationUsesOneOwnedLegacyFactory()
        {
            string root = FindRepositoryRoot();
            string zoneServer = ReadRepositoryFile(
                root,
                @"AORebirth\Server\ZoneEngine\Core\ZoneServer.cs");
            string registry = ReadRepositoryFile(
                root,
                @"AORebirth\Server\ZoneEngine\Core\RuntimeOwnershipRegistry.cs");

            Assert.AreEqual(
                1,
                CountOccurrences(
                    zoneServer,
                    "new RuntimeOwnershipRegistry<int, IPlayfield>(this.CreateOwnedPlayfield)"),
                "ZoneServer must retain one owned playfield factory registration.");
            Assert.AreEqual(
                1,
                CountOccurrences(zoneServer, "new Playfield("),
                "The characterized production source must contain one legacy Playfield construction site.");
            AssertOrdered(
                registry,
                "if (this.runtimes.TryGetValue(key, out runtime))",
                "runtime = this.runtimeFactory(key);",
                "this.runtimes.Add(key, runtime);");
        }

        private static string ReadRepositoryFile(string root, string relativePath)
        {
            return File.ReadAllText(Path.Combine(root, relativePath)).Replace("\r\n", "\n");
        }

        private static void AssertOrdered(string source, params string[] markers)
        {
            int previous = -1;
            foreach (string marker in markers)
            {
                int current = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
                Assert.IsTrue(current >= 0, "Missing characterized source marker: " + marker);
                Assert.IsTrue(current > previous, "Characterized source order changed at: " + marker);
                previous = current;
            }
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(current, @"AORebirth\Server\ZoneEngine")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }
}

