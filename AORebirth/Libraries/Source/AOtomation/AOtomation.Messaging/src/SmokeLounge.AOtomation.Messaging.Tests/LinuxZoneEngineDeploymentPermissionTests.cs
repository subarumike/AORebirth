namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LinuxZoneEngineDeploymentPermissionTests
    {
        [TestMethod]
        public void ZoneEngineLiveUpgradeValidatesApphostBeforeCurrentPromotion()
        {
            string source = ReadRepositoryFile(@"LinuxBuild\deployment\zone-stage9\upgrade-live-service.sh");

            StringAssert.Contains(source, "readonly APPHOST_MODE=\"750\"");
            StringAssert.Contains(source, "chown -R root:\"${SERVICE_GROUP}\" \"${release_path}\"");
            StringAssert.Contains(source, "chmod \"0${APPHOST_MODE}\" \"${release_path}/${APPHOST_NAME}\"");
            StringAssert.Contains(source, "validate_release_runtime \"${release_staging}\"");
            StringAssert.Contains(source, "validate_release_runtime \"${release_target}\"");
            StringAssert.Contains(source, "validate_release_runtime \"${rollback_target}\"");
            StringAssert.Contains(source, "runuser -u \"${SERVICE_USER}\" -g \"${SERVICE_GROUP}\" -- test -x \"${apphost}\"");
            StringAssert.Contains(source, "verify_no_online_characters");
            StringAssert.Contains(source, "mv -fT -- \"${current_swap}\" \"${CURRENT_LINK}\"");
            StringAssert.Contains(source, "new ${SERVICE_NAME} release failed to start; rollback target restored");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "AI_START_HERE.md")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Repository root not found.");
            return null;
        }
    }
}
