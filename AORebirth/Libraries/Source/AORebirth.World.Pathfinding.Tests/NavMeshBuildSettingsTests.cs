namespace AORebirth.World.Pathfinding.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class NavMeshBuildSettingsTests
    {
        [TestMethod]
        public void Load_CheckedInConfig_IsValid()
        {
            string path = Path.Combine(FindAoRebirthRoot(), "Config", NavMeshBuildSettings.ConfigFileName);
            Assert.IsTrue(File.Exists(path), "Expected " + path);

            NavMeshBuildSettings settings = NavMeshBuildSettings.Load(path);

            Assert.AreEqual(NavMeshBuildSettings.CurrentVersion, settings.SchemaVersion);
        }

        [TestMethod]
        public void Load_MissingFile_Throws()
        {
            Assert.ThrowsException<FileNotFoundException>(
                () => NavMeshBuildSettings.Load(Path.Combine(Path.GetTempPath(), "missing-navmesh.json")));
        }

        static string FindAoRebirthRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string config = Path.Combine(dir, "AORebirth", "Config", NavMeshBuildSettings.ConfigFileName);
                if (File.Exists(config))
                    return Path.Combine(dir, "AORebirth");

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException("Could not locate AORebirth/Config/NavAgent.json from " + AppContext.BaseDirectory);
        }
    }
}
