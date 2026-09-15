namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OfficialPlayfieldPlacementBuildContractTests
    {


        [TestMethod]
        public void WindowsBuildGuardsContentArchitectureWithoutPinningEditableWorldData()
        {
            string wrapper = ReadRepositoryFile(@"tools\build_aorebirth_debug.cmd");

            StringAssert.Contains(wrapper, @"Tools\run_newengine_content_architecture_guard.cmd --check");
            Assert.IsFalse(wrapper.Contains("--validate-official-placements"));
            string project = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj");
            Assert.IsFalse(project.Contains("OfficialPlayfieldPlacementCatalog.cs"));
            Assert.IsFalse(project.Contains("Content/Official/PlayfieldPlacements"));
        }

        [TestMethod]
        public void WindowsAcceptanceRequiresContentArchitectureGuard()
        {
            string wrapper = ReadRepositoryFile(@"Tools\accept_windows_source.cmd");

            StringAssert.Contains(wrapper, @"Tools\run_newengine_content_architecture_guard.cmd --check");
            StringAssert.Contains(wrapper, "if errorlevel 1 goto :content_failed");
            StringAssert.Contains(wrapper, "CONTENT_ARCHITECTURE_GUARD=FAIL");
            StringAssert.Contains(wrapper, ">> \"%EVIDENCE%\" echo CONTENT_ARCHITECTURE_GUARD=PASS");
            Assert.IsFalse(wrapper.Contains("PLACEMENT_CORPUS=PASS"));
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static int CountOccurrences(string value, string expected)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(expected, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += expected.Length;
            }

            return count;
        }
    }
}
