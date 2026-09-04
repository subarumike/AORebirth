namespace ZoneEngine_New.Core.Data
{
    using System;

    using Utility.Config;

    internal static class MySqlConnectionSettings
    {
        public static string GetRequiredConnectionString()
        {
            Config? config = ConfigReadWrite.Instance.CurrentConfig;
            string? connectionString = config?.MysqlConnection;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("MysqlConnection is not configured.");
            }

            return connectionString;
        }
    }
}
