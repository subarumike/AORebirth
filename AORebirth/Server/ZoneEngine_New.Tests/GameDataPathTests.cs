namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;

    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;

    [TestClass]
    public sealed class GameDataPathTests
    {
        [TestMethod]
        public void RuntimeRootUsesTheDirectoryBesideTheProcessUnlessOverridden()
        {
            string previous = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
            string overrideRoot = Path.Combine(Path.GetTempPath(), "AORebirthGameDataOverride_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, null);
                Assert.AreEqual(
                    Path.Combine(AppContext.BaseDirectory, GameDataPaths.RootFolderName),
                    GameDataPaths.ResolveRuntimeRoot());

                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, "   ");
                Assert.AreEqual(
                    Path.Combine(AppContext.BaseDirectory, GameDataPaths.RootFolderName),
                    GameDataPaths.ResolveRuntimeRoot());

                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, "  " + overrideRoot + "  ");
                Assert.AreEqual(Path.GetFullPath(overrideRoot), GameDataPaths.ResolveRuntimeRoot());

                string fixture = Path.Combine(Path.GetTempPath(), "AORebirthGameDataFixture_" + Guid.NewGuid().ToString("N"));
                Assert.AreEqual(
                    Path.Combine(fixture, GameDataPaths.RootFolderName),
                    GameDataPaths.Resolve(fixture));

                Directory.CreateDirectory(overrideRoot);
                var store = new GameDataStore(new StubLogger());
                Assert.AreEqual(Path.GetFullPath(overrideRoot), store.RootPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previous);
            }
        }
    }
}
