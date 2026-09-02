namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldLocalitySafetyTests
    {
        [TestMethod]
        public void CellHeatSchedulingIsFailSafeAndExplicitlyOptIn()
        {
            string settings = ReadRepositoryFile(
                @"AORebirth\Libraries\Source\Utility\Config\LocalitySettings.cs");
            string locality = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocality.cs");
            string config = ReadRepositoryFile(@"AORebirth\Config\Config.xml");
            string example = ReadRepositoryFile(@"AORebirth\Config\Config.example.xml");

            StringAssert.Contains(settings, "public bool EnableCellHeatScheduling { get; set; }");
            StringAssert.Contains(
                locality,
                "bool enableCellHeatScheduling = settings != null && settings.EnableCellHeatScheduling;");
            StringAssert.Contains(config, "<EnableCellHeatScheduling>false</EnableCellHeatScheduling>");
            StringAssert.Contains(example, "<EnableCellHeatScheduling>false</EnableCellHeatScheduling>");
            Assert.IsFalse(
                config.Contains("<EnableCellHeatScheduling>true</EnableCellHeatScheduling>"),
                "The checked-in runtime configuration must keep cell heat scheduling disabled by default.");
        }

        [TestMethod]
        public void DisabledHeatSchedulingTicksEveryCharacterBeforeReturning()
        {
            string locality = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocality.cs");
            string tick = ExtractBlock(locality, "internal void Tick(double deltaTime)");

            int safetyIndex = tick.IndexOf(
                "if (!this.policy.EnableCellHeatScheduling)",
                StringComparison.Ordinal);
            int allCharactersIndex = tick.IndexOf(
                "this.tickCallbacks.GetAllCharacters()",
                StringComparison.Ordinal);
            int processIndex = tick.IndexOf(
                "this.ProcessDynelTick(character, deltaTime);",
                StringComparison.Ordinal);
            int returnIndex = tick.IndexOf("return;", processIndex, StringComparison.Ordinal);
            int schedulerIndex = tick.IndexOf("this.heatScheduler.Tick(", StringComparison.Ordinal);

            Assert.IsTrue(safetyIndex >= 0, "The full-rate safety gate must exist.");
            Assert.IsTrue(allCharactersIndex > safetyIndex, "The safety path must enumerate every character.");
            Assert.IsTrue(processIndex > allCharactersIndex, "Every character must use the existing tick pipeline.");
            Assert.IsTrue(returnIndex > processIndex, "The safety path must return after full-rate ticking.");
            Assert.IsTrue(schedulerIndex > returnIndex, "The heat scheduler must remain unreachable while disabled.");
        }

        [TestMethod]
        public void FullRateFallbackPreservesCharacterLifecyclePipeline()
        {
            string locality = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocality.cs");
            string process = ExtractBlock(
                locality,
                "private void ProcessDynelTick(ICharacter dynel, double deltaTime)");

            StringAssert.Contains(process, "this.tickCallbacks.ProcessDeadNpcDespawn(dynel)");
            StringAssert.Contains(process, "dynel.DoNotDoTimers");
            StringAssert.Contains(process, "this.tickCallbacks.ProcessCharacterTick(dynel, deltaTime);");
            StringAssert.Contains(process, "this.tickCallbacks.ProcessNpcPatrolTick(dynel);");
            StringAssert.Contains(process, "this.tickCallbacks.ProcessFollow(dynel);");
            StringAssert.Contains(process, "this.tickCallbacks.ProcessPlayerCollision(dynel);");
        }

        [TestMethod]
        public void MissingExternalGameDataKeepsTheSafeFallbackAndDoesNotEnterSourceInventory()
        {
            string loader = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\GameData\GameDataLoader.cs");
            string project = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string ensureRoot = ExtractBlock(loader, "internal static void EnsureRootExists()");
            StringAssert.Contains(ensureRoot, "!Directory.Exists(GameDataRootPath)");
            StringAssert.Contains(ensureRoot, "!Directory.Exists(PlayfieldsRootPath)");
            StringAssert.Contains(ensureRoot, "locality will use the safe indoor fallback");
            Assert.IsFalse(
                ensureRoot.Contains("throw new DirectoryNotFoundException"),
                "An externally provisioned GameData tree must not block startup when absent.");

            StringAssert.Contains(project, "<Target Name=\"CopyExtractedGameData\"");
            StringAssert.Contains(project, "<ExtractedGameDataFiles Include=\"..\\..\\GameData\\**\\*\"");
            Assert.IsFalse(
                project.Contains("<Content Include=\"..\\..\\GameData\\**\\*\"")
                || project.Contains("<Compile Include=\"..\\..\\GameData\\**\\*\""),
                "External generated data must not be represented as governed source inventory.");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), relativePath));
        }

        private static string ExtractBlock(string text, string signature)
        {
            int start = text.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing source signature: " + signature);

            int open = text.IndexOf('{', start);
            Assert.IsTrue(open >= 0, "Missing opening brace for: " + signature);

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail("Unterminated source block: " + signature);
            return string.Empty;
        }
    }
}
