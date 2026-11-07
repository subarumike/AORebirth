namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.DailyLogin;

    [TestClass]
    [DoNotParallelize]
    public sealed class DailyLoginBridgePathsTests
    {
        private const string IsolatedVariable = "AO_REBIRTH_DAILY_LOGIN_ISOLATED_ROOTS";
        private const string ClaimsVariable = "AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS";
        private const string StateVariable = "AO_REBIRTH_ZONE_STATE_DIR";
        private const string RewardsVariable = "AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON";

        private static readonly string ClaimsRoot = Path.Combine(Path.GetTempPath(), "aorebirth-explicit-claims");
        private static readonly string StateRoot = Path.Combine(Path.GetTempPath(), "aorebirth-zone-state");

        [TestMethod]
        public void IsolatedClaimsUseOnlyTheConfiguredRoot()
        {
            WithSettings("1", ClaimsRoot, () =>
                CollectionAssert.AreEqual(new[] { ClaimsRoot }, DailyLoginBridgePaths.Resolve().ClaimsRoots));
        }

        [TestMethod]
        public void IsolatedClaimsRejectAnEmptyConfiguration()
        {
            WithSettings("1", null, () =>
                Assert.ThrowsExactly<InvalidOperationException>(() => DailyLoginBridgePaths.Resolve()));
        }

        [TestMethod]
        public void DefaultClaimsStillCombineExistingRoots()
        {
            WithSettings(null, ClaimsRoot, () =>
            {
                string[] roots = DailyLoginBridgePaths.Resolve().ClaimsRoots;
                CollectionAssert.Contains(roots, ClaimsRoot);
                CollectionAssert.Contains(roots, Path.Combine(StateRoot, "daily-login", "claims"));
                if (OperatingSystem.IsWindows())
                    CollectionAssert.Contains(roots, @"C:\xampp\htdocs\daily\data\claims");
            });
        }

        private static void WithSettings(string? isolated, string? claims, Action verify)
        {
            string[] variables = [IsolatedVariable, ClaimsVariable, StateVariable, RewardsVariable];
            string?[] previous = Array.ConvertAll(variables, Environment.GetEnvironmentVariable);
            string rewardsFile = Path.GetTempFileName();
            try
            {
                Environment.SetEnvironmentVariable(IsolatedVariable, isolated);
                Environment.SetEnvironmentVariable(ClaimsVariable, claims);
                Environment.SetEnvironmentVariable(StateVariable, StateRoot);
                Environment.SetEnvironmentVariable(RewardsVariable, rewardsFile);
                verify();
            }
            finally
            {
                for (int index = 0; index < variables.Length; index++)
                    Environment.SetEnvironmentVariable(variables[index], previous[index]);
                File.Delete(rewardsFile);
            }
        }
    }
}
