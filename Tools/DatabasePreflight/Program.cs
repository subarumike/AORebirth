namespace AORebirth.Tools.DatabasePreflight
{
    using System;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return DatabasePreflightSelfTests.Run(Console.Out);
            }

            if (args.Length != 0)
            {
                Console.WriteLine("[Database Preflight] FAIL (18): unsupported command arguments.");
                return (int)DatabasePreflightExitCode.InternalFailure;
            }

            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            return DatabasePreflightCommand.Run(
                connectionString,
                new ProductionDatabasePreflightDataSourceFactory(),
                Console.Out);
        }
    }
}
