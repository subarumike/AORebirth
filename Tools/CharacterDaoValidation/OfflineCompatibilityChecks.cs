namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Data;
    using System.IO;
    using AORebirth.Database;
    using AORebirth.LinuxBuild.Stage8OfflineSmokeTests;
    using SmokeLounge.AOtomation.Messaging.Tests;

    internal static partial class Program
    {
        // Execute the linked, unchanged test sources. This is bounded offline/source
        // compatibility evidence, not full MSTest execution or engine acceptance.
        private static void OfflineChecks()
        {
            string repositoryRoot = Stage8RepositoryRootResolver.ResolveExplicit(Directory.GetCurrentDirectory());
            Func<IDbConnection> previousFactory = Connector.TestConnectionFactory;
            int connectionAttempts = 0;
            Connector.TestConnectionFactory = () =>
            {
                connectionAttempts++;
                throw new InvalidOperationException("Offline compatibility tests must not acquire a database connection.");
            };
            try
            {
                RunOfflineCase(
                    () => StaleOnlineRecoveryTests.Run(repositoryRoot),
                    "unchanged-stale-online-recovery-11-cases");
                RunOfflineCase(
                    LoginHandoffLifecycleTests.Run,
                    "unchanged-login-handoff-lifecycle-13-cases");

                var hydration = new LoginSessionHydrationSafetyContractTests();
                RunOfflineCase(
                    hydration.InventoryHydrationStateDistinguishesLoadedEmptyFromFailed,
                    "hydration-loaded-empty-versus-failure-source-contract");
                RunOfflineCase(
                    hydration.InventoryPersistenceFailsClosedBeforeDestructiveRewrite,
                    "hydration-destructive-rewrite-fail-closed-source-contract");
                RunOfflineCase(
                    hydration.GmiMissingOptionalSchemaSkipsLoginPendingWithdrawalProcessing,
                    "hydration-optional-gmi-schema-source-contract");
                RunOfflineCase(
                    hydration.ClientConnectedUsesTransferAwareMasterSessionSemantics,
                    "hydration-transfer-aware-session-source-contract");
                RunOfflineCase(
                    hydration.CrashReconnectCancelsLogoutTimerBeforeInventoryReloadAndRejectsZombieInventory,
                    "hydration-reconnect-ownership-source-contract");
                Require(connectionAttempts == 0, "offline-compatibility-no-database-acquisition");
            }
            finally
            {
                Connector.TestConnectionFactory = previousFactory;
            }
            Console.WriteLine("CHARACTER_DAO_LEGACY_OFFLINE_CASES=29");
            Console.WriteLine("CHARACTER_DAO_HYDRATION_VALIDATION=FIVE_UNCHANGED_SOURCE_CONTRACT_METHODS_NOT_FULL_MSTEST");
        }

        private static void RunOfflineCase(Action test, string name)
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                // Keep labels useful without printing provider/configuration details.
                throw new CheckFailure(category + ":" + name + ":" + exception.GetType().Name);
            }
            Require(true, name);
        }
    }
}

// Minimal test-host compatibility for the unchanged source-contract methods above.
// These are real throwing assertions, not substitutes for any production behavior.
// No MSTest discovery, lifecycle, or runner equivalence is claimed.
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class TestClassAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class TestMethodAttribute : Attribute
    {
    }

    internal static class Assert
    {
        internal static void IsTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }

    internal static class StringAssert
    {
        internal static void Contains(string value, string substring)
        {
            if (value == null || substring == null || value.IndexOf(substring, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Expected source text was not found.");
        }
    }
}
