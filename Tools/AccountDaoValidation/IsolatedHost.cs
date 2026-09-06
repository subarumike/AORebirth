// Test-only infrastructure hosting. Never reads application configuration or credentials.
// The production DAO and legacy SQL/mapper sources are linked unchanged by this project.
namespace AORebirth.Database
{
    using System;
    using System.Data;

    internal static class Connector
    {
        internal static Func<IDbConnection> TestConnectionFactory;

        public static IDbConnection GetConnection()
        {
            if (TestConnectionFactory == null)
                throw new InvalidOperationException("Account tests require an explicit disposable factory.");
            return TestConnectionFactory();
        }
    }
}

namespace Utility
{
    using System;

    internal static class LogUtil
    {
        internal static int ErrorCount;
        public static void ErrorException(Exception exception)
        {
            ErrorCount++;
            Console.Error.WriteLine("ACCOUNT_TEST_HOST_LOG=" + exception.GetType().Name);
        }
    }
}

namespace Utility.Config
{
    internal sealed class ConfigReadWrite
    {
        internal static readonly ConfigReadWrite Instance = new ConfigReadWrite();
        internal readonly FixtureConfiguration CurrentConfig = new FixtureConfiguration();
    }

    internal sealed class FixtureConfiguration
    {
        internal string SQLType = "MySql";
    }
}
