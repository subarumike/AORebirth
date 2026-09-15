namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldHydrationCharacterizationTests
    {


        [TestMethod]
        public void CurrentLegacySourcePrecedenceRemainsCharacterized()
        {
            string root = FindRepositoryRoot();
            string loader = ReadRepositoryFile(
                root,
                @"AORebirth\Libraries\Source\PlayfieldLoader\PlayfieldLoader.cs");

            AssertOrdered(
                loader,
                "MessagePackZip.UncompressData<PlayfieldData>(fname)",
                "TeleportDao.Instance.GetWhere(",
                "resolvedDestinationPlayfieldId == SubwayPlayfieldId",
                "ShouldSynthesizeReverseProxyExit(");
        }

        [TestMethod]
        public void CurrentRuntimeCreationUsesOneOwnedLegacyFactory()
        {
            string root = FindRepositoryRoot();
            string registry = ReadRepositoryFile(
                root,
                @"Tests\Fixtures\Gameplay\RuntimeOwnershipRegistry.cs");
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
