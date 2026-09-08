namespace ZoneEngine_New.Core.Data
{
    using System;

    using Utility.Config;

    internal static class MySqlConnectionSettings
    {
        public static string GetRequiredConnectionString()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            Config? config = ConfigReadWrite.Instance.CurrentConfig;
            string? connectionString = config?.MysqlConnection;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "MysqlConnection is not configured. Set AO_REBIRTH_MYSQL_CONNECTION or MysqlConnection in config.");

            return connectionString;
        }
    }
}
