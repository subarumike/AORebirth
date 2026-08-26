namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OfficialPlayfieldPlacementBuildContractTests
    {
        [TestMethod]
        public void WindowsProjectPackagesExactlyOneTrackedOfficialCorpus()
        {
            string project = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj").Replace('\\', '/');

            StringAssert.Contains(
                project,
                "../../../docs/generated/playfields/official-placement-corpus-manifest.json");
            StringAssert.Contains(
                project,
                "../../../docs/generated/playfields/official-placement-index.json");
            StringAssert.Contains(
                project,
                "../../../docs/generated/playfields/official-placement-summary.json");
            StringAssert.Contains(
                project,
                "../../../docs/generated/playfields/official-acghash-inventory.json");
            Assert.IsFalse(project.Contains("../../../docs/generated/playfields/placements/*.json"));
            Assert.AreEqual(
                630,
                CountOccurrences(
                    project,
                    "../../../docs/generated/playfields/placements/pf_"));
            Assert.AreEqual(
                630,
                CountOccurrences(
                    project,
                    "Content/Official/PlayfieldPlacements/placements/pf_"));
            Assert.IsFalse(project.Contains("official-playfield-reconciliation.json"));
        }

        [TestMethod]
        public void WindowsBuildExercisesTheBuiltZoneEnginePlacementMode()
        {
            string wrapper = ReadRepositoryFile(@"tools\build_aorebirth_debug.cmd");

            StringAssert.Contains(wrapper, "git rev-parse HEAD");
            StringAssert.Contains(wrapper, @"%ZONE_OUTPUT%\ZoneEngine.exe");
            StringAssert.Contains(wrapper, "--validate-official-placements");
            StringAssert.Contains(wrapper, "--source-sha \"%SOURCE_SHA%\"");
            StringAssert.Contains(wrapper, "--placement-manifest-output \"%PLACEMENT_MANIFEST%\"");
            StringAssert.Contains(wrapper, "--placement-provenance-output \"%PLACEMENT_PROVENANCE%\"");
            StringAssert.Contains(wrapper, "--build-platform windows");
            StringAssert.Contains(
                wrapper,
                @"%PLACEMENT_OUTPUT%\official-placement-build-manifest.json");
            StringAssert.Contains(
                wrapper,
                @"%PLACEMENT_OUTPUT%\PLACEMENT_PROVENANCE.env");
        }

        [TestMethod]
        public void WindowsAcceptanceFailsClosedOnPlacementProvenance()
        {
            string wrapper = ReadRepositoryFile(@"Tools\accept_windows_source.cmd");

            StringAssert.Contains(
                wrapper,
                "if /i not \"%PLACEMENT_SOURCE_SHA%\"==\"%ACTUAL_SHA%\" goto :placement_failed");
            StringAssert.Contains(
                wrapper,
                "if /i not \"%PLACEMENT_BUILD_PLATFORM%\"==\"windows\" goto :placement_failed");
            StringAssert.Contains(wrapper, "PLACEMENT_BUILD_MANIFEST_SHA256_ASSIGNMENTS");
            StringAssert.Contains(wrapper, "certutil.exe -hashfile \"%PLACEMENT_MANIFEST%\" SHA256");
            StringAssert.Contains(
                wrapper,
                "if not \"%PLACEMENT_ACTUAL_BUILD_MANIFEST_SHA256%\"==\"%PLACEMENT_BUILD_MANIFEST_SHA256%\" goto :placement_failed");
            StringAssert.Contains(wrapper, "PLACEMENT_CORPUS=FAIL");
            StringAssert.Contains(
                wrapper,
                ">> \"%EVIDENCE%\" echo PLACEMENT_BUILD_MANIFEST_SHA256=%PLACEMENT_BUILD_MANIFEST_SHA256%");
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
