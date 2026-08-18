namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DailyLoginPathContractTests
    {
        [TestMethod]
        public void DailyLoginClaimRootsArePlatformScoped()
        {
            string source = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\DailyLoginRewardRuntime.cs");

            StringAssert.Contains(source, "AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS");
            StringAssert.Contains(source, "AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON");
            StringAssert.Contains(source, "AO_REBIRTH_ZONE_STATE_DIR");
            StringAssert.Contains(source, "Path.Combine(zoneStateRoot.Trim(), \"daily-login\", \"claims\")");
            StringAssert.Contains(source, "if (IsWindowsRuntime())");

            Assert.IsFalse(
                source.Contains("private static readonly string[] ClaimRoots"),
                "DailyLogin claim roots must not be a single unconditional Windows-only array.");
            Assert.IsFalse(
                source.Contains("foreach (string root in ClaimRoots)"),
                "DailyLogin claim IO must resolve platform-scoped roots before read/write/delete.");
            Assert.IsFalse(
                source.Contains("foreach (string path in RewardsJsonPaths)"),
                "DailyLogin rewards JSON reads must resolve platform-scoped paths.");
        }

        [TestMethod]
        public void FixedPhasefrontRandomRewardsDoNotUseScaleRelations()
        {
            string source = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\DailyLoginRewardRuntime.cs");

            StringAssert.Contains(source, "private static bool IsFixedPhasefrontRandomRewardDay(int day)");
            StringAssert.Contains(source, "return day == 1 || day == 28;");
            StringAssert.Contains(source, "if (IsFixedPhasefrontRandomRewardDay(day))");
            StringAssert.Contains(source, "grantItem = new Item(quality, itemId, itemId) { MultipleCount = stackCount };");
            StringAssert.Contains(source, "else if (day == 10");
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

                current = Directory.GetParent(current) == null ? null : Directory.GetParent(current).FullName;
            }

            Assert.Fail("Repository root not found.");
            return null;
        }
    }
}
