#if MISSION_DAO_ISOLATED_TESTS
// Only for the opt-in source-isolated test build. SQL still uses the real MySQL
// provider and disposable schema. These two host dependencies are deliberately
// unavailable: this mode does not validate application configuration or logging.
namespace AORebirth.Database
{
    using System;
    using System.Data;

    internal static class Connector
    {
        internal static Func<IDbConnection> TestConnectionFactory;

        public static IDbConnection GetConnection()
        {
            if (TestConnectionFactory != null)
            {
                return TestConnectionFactory();
            }

            throw new InvalidOperationException("Isolated mission tests require an injected connection factory.");
        }
    }
}

namespace Utility
{
    using System;

    internal static class LogUtil
    {
        public static void ErrorException(Exception exception)
        {
            // Never emit provider messages, which may contain connection details.
            Console.Error.WriteLine("MISSION_TEST_HOST_LOG=" + exception.GetType().Name);
        }
    }
}
#endif
