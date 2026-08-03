namespace AORebirth.Tools.DatabasePreflight
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;

    using AORebirth.Database;

    using MySqlConnector;

    using Utility.Config;

    internal enum DatabasePreflightExitCode
    {
        Success = 0,
        MissingConnectionString = 10,
        InvalidConnectionString = 11,
        NetworkFailure = 12,
        AuthenticationFailure = 13,
        WrongDatabase = 14,
        MissingSchema = 15,
        ReadFailure = 16,
        OnlineCharactersPresent = 17,
        InternalFailure = 18
    }

    internal static class DatabaseSchemaContract
    {
        internal const string ExpectedDatabase = "cellao_codex_clean";

        internal static readonly string[] RequiredTables =
        {
            "characterstimers",
            "characters",
            "charactersactivenanos",
            "charactersmeshs",
            "charactersuploadednanos",
            "charactersperks",
            "instanceditems",
            "itemnames",
            "items",
            "login",
            "missionaccountflags",
            "missionflags",
            "missionobjectiveobservations",
            "missionobjectiveprogress",
            "missionrewardledger",
            "missionstates",
            "mobdroptable",
            "mobspawns",
            "mobspawnsactivenanos",
            "mobspawnsinventory",
            "mobspawnsmeshs",
            "mobspawnsuploadednanos",
            "mobspawns_stats",
            "mobtemplate",
            "organizations",
            "proxydestinations",
            "receivedmessages",
            "shopinventorytemplates",
            "staticdynels",
            "stats",
            "teleports",
            "tradeskill",
            "vendors",
            "vendortemplate"
        };

        internal static bool IsRequiredTable(string tableName)
        {
            foreach (string requiredTable in RequiredTables)
            {
                if (string.Equals(requiredTable, tableName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class DatabasePreflightFailureException : Exception
    {
        internal DatabasePreflightFailureException(DatabasePreflightExitCode exitCode)
            : base("Database preflight operation failed.")
        {
            this.ExitCode = exitCode;
        }

        internal DatabasePreflightExitCode ExitCode { get; private set; }
    }

    internal interface IDatabasePreflightDataSourceFactory
    {
        IDatabasePreflightDataSource Create(string validatedConnectionString);
    }

    internal interface IDatabasePreflightDataSource : IDisposable
    {
        void Open();

        string GetCurrentDatabase();

        ISet<string> GetExistingTables(string databaseName);

        bool HasCharactersOnlineColumn(string databaseName);

        void VerifyReadAccess(string tableName);

        long CountOnlineCharacters();
    }

    internal static class DatabasePreflightCommand
    {
        internal static int Run(
            string connectionString,
            IDatabasePreflightDataSourceFactory dataSourceFactory,
            TextWriter output)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Fail(output, DatabasePreflightExitCode.MissingConnectionString);
            }

            MySqlConnectionStringBuilder builder;
            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch
            {
                return Fail(output, DatabasePreflightExitCode.InvalidConnectionString);
            }

            if (string.IsNullOrWhiteSpace(builder.Server))
            {
                return Fail(output, DatabasePreflightExitCode.InvalidConnectionString);
            }

            if (!string.Equals(builder.Database, DatabaseSchemaContract.ExpectedDatabase, StringComparison.Ordinal))
            {
                return Fail(output, DatabasePreflightExitCode.WrongDatabase);
            }

            output.WriteLine("[Database Preflight] PASS: MySQL connection is present and well formed.");

            IDatabasePreflightDataSource dataSource;
            try
            {
                dataSource = dataSourceFactory.Create(connectionString);
            }
            catch
            {
                return Fail(output, DatabasePreflightExitCode.InternalFailure);
            }

            if (dataSource == null)
            {
                return Fail(output, DatabasePreflightExitCode.InternalFailure);
            }

            using (dataSource)
            {
                DatabasePreflightExitCode openResult = Invoke(dataSource.Open);
                if (openResult != DatabasePreflightExitCode.Success)
                {
                    return Fail(output, openResult);
                }

                output.WriteLine("[Database Preflight] PASS: host is reachable and MySQL authentication succeeded.");

                string currentDatabase;
                try
                {
                    currentDatabase = dataSource.GetCurrentDatabase();
                }
                catch (DatabasePreflightFailureException failure)
                {
                    return Fail(output, NormalizeFailure(failure.ExitCode));
                }
                catch
                {
                    return Fail(output, DatabasePreflightExitCode.ReadFailure);
                }

                if (!string.Equals(
                    currentDatabase,
                    DatabaseSchemaContract.ExpectedDatabase,
                    StringComparison.Ordinal))
                {
                    return Fail(output, DatabasePreflightExitCode.WrongDatabase);
                }

                output.WriteLine("[Database Preflight] PASS: expected database identity is active.");

                ISet<string> existingTables;
                bool hasOnlineColumn;
                try
                {
                    existingTables = dataSource.GetExistingTables(DatabaseSchemaContract.ExpectedDatabase);
                    hasOnlineColumn = dataSource.HasCharactersOnlineColumn(DatabaseSchemaContract.ExpectedDatabase);
                }
                catch (DatabasePreflightFailureException failure)
                {
                    return Fail(output, NormalizeFailure(failure.ExitCode));
                }
                catch
                {
                    return Fail(output, DatabasePreflightExitCode.ReadFailure);
                }

                if (existingTables == null || !hasOnlineColumn)
                {
                    return Fail(output, DatabasePreflightExitCode.MissingSchema);
                }

                foreach (string requiredTable in DatabaseSchemaContract.RequiredTables)
                {
                    if (!existingTables.Contains(requiredTable))
                    {
                        return Fail(output, DatabasePreflightExitCode.MissingSchema);
                    }
                }

                output.WriteLine(
                    "[Database Preflight] PASS: all {0} required tables and characters.Online are present.",
                    DatabaseSchemaContract.RequiredTables.Length);

                foreach (string requiredTable in DatabaseSchemaContract.RequiredTables)
                {
                    DatabasePreflightExitCode readResult = Invoke(
                        delegate
                        {
                            dataSource.VerifyReadAccess(requiredTable);
                        });
                    if (readResult != DatabasePreflightExitCode.Success)
                    {
                        return Fail(output, readResult);
                    }
                }

                output.WriteLine("[Database Preflight] PASS: read access is available for every required table.");

                long onlineCharacters;
                try
                {
                    onlineCharacters = dataSource.CountOnlineCharacters();
                }
                catch (DatabasePreflightFailureException failure)
                {
                    return Fail(output, NormalizeFailure(failure.ExitCode));
                }
                catch
                {
                    return Fail(output, DatabasePreflightExitCode.ReadFailure);
                }

                if (onlineCharacters != 0)
                {
                    return Fail(output, DatabasePreflightExitCode.OnlineCharactersPresent);
                }

                output.WriteLine("[Database Preflight] PASS: online-character count is zero.");
                output.WriteLine("[Database Preflight] PASS.");
                return (int)DatabasePreflightExitCode.Success;
            }
        }

        private static DatabasePreflightExitCode Invoke(Action action)
        {
            try
            {
                action();
                return DatabasePreflightExitCode.Success;
            }
            catch (DatabasePreflightFailureException failure)
            {
                return NormalizeFailure(failure.ExitCode);
            }
            catch
            {
                return DatabasePreflightExitCode.InternalFailure;
            }
        }

        private static DatabasePreflightExitCode NormalizeFailure(DatabasePreflightExitCode exitCode)
        {
            switch (exitCode)
            {
                case DatabasePreflightExitCode.NetworkFailure:
                case DatabasePreflightExitCode.AuthenticationFailure:
                case DatabasePreflightExitCode.WrongDatabase:
                case DatabasePreflightExitCode.MissingSchema:
                case DatabasePreflightExitCode.ReadFailure:
                case DatabasePreflightExitCode.InternalFailure:
                    return exitCode;
                default:
                    return DatabasePreflightExitCode.InternalFailure;
            }
        }

        private static int Fail(TextWriter output, DatabasePreflightExitCode exitCode)
        {
            output.WriteLine(
                "[Database Preflight] FAIL ({0}): {1}",
                (int)exitCode,
                GetSafeFailureText(exitCode));
            return (int)exitCode;
        }

        private static string GetSafeFailureText(DatabasePreflightExitCode exitCode)
        {
            switch (exitCode)
            {
                case DatabasePreflightExitCode.MissingConnectionString:
                    return "no MySQL connection was found in AO_REBIRTH_MYSQL_CONNECTION or Config.xml.";
                case DatabasePreflightExitCode.InvalidConnectionString:
                    return "the connection string format is invalid.";
                case DatabasePreflightExitCode.NetworkFailure:
                    return "the MySQL endpoint could not be reached.";
                case DatabasePreflightExitCode.AuthenticationFailure:
                    return "MySQL authentication was rejected.";
                case DatabasePreflightExitCode.WrongDatabase:
                    return "the configured or active database is not the required database.";
                case DatabasePreflightExitCode.MissingSchema:
                    return "a required schema object is missing.";
                case DatabasePreflightExitCode.ReadFailure:
                    return "a required read-only database query failed.";
                case DatabasePreflightExitCode.OnlineCharactersPresent:
                    return "characters.Online contains one or more nonzero rows.";
                default:
                    return "an internal preflight contract failed.";
            }
        }
    }

    internal sealed class ProductionDatabasePreflightDataSourceFactory : IDatabasePreflightDataSourceFactory
    {
        public IDatabasePreflightDataSource Create(string validatedConnectionString)
        {
            return new ProductionDatabasePreflightDataSource(validatedConnectionString);
        }
    }

    internal sealed class ProductionDatabasePreflightDataSource : IDatabasePreflightDataSource
    {
        private readonly string validatedConnectionString;
        private IDbConnection connection;

        internal ProductionDatabasePreflightDataSource(string validatedConnectionString)
        {
            this.validatedConnectionString = validatedConnectionString;
        }

        public void Open()
        {
            try
            {
                Config currentConfig = ConfigReadWrite.Instance.CurrentConfig;
                if (currentConfig == null
                    || !string.Equals(currentConfig.SQLType, "MySql", StringComparison.Ordinal)
                    || !string.Equals(
                        currentConfig.MysqlConnection,
                        this.validatedConnectionString,
                        StringComparison.Ordinal))
                {
                    throw new DatabasePreflightFailureException(DatabasePreflightExitCode.InternalFailure);
                }

                this.connection = Connector.GetConnection();
            }
            catch (DatabasePreflightFailureException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new DatabasePreflightFailureException(ClassifyOpenFailure(exception));
            }
        }

        public string GetCurrentDatabase()
        {
            object value = this.ExecuteScalar("SELECT DATABASE()", null, null);
            return value == null || value == DBNull.Value
                       ? string.Empty
                       : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public ISet<string> GetExistingTables(string databaseName)
        {
            this.EnsureOpen();
            try
            {
                using (IDbCommand command = this.connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES "
                        + "WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'";
                    AddParameter(command, "@schema", databaseName);

                    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture));
                        }
                    }

                    return result;
                }
            }
            catch (Exception exception)
            {
                throw QueryFailure(exception);
            }
        }

        public bool HasCharactersOnlineColumn(string databaseName)
        {
            object value = this.ExecuteScalar(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS "
                + "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'characters' AND COLUMN_NAME = 'Online'",
                "@schema",
                databaseName);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture) == 1;
        }

        public void VerifyReadAccess(string tableName)
        {
            if (!DatabaseSchemaContract.IsRequiredTable(tableName))
            {
                throw new DatabasePreflightFailureException(DatabasePreflightExitCode.InternalFailure);
            }

            this.ExecuteScalar("SELECT 1 FROM `" + tableName + "` LIMIT 0", null, null);
        }

        public long CountOnlineCharacters()
        {
            object value = this.ExecuteScalar(
                "SELECT COUNT(*) FROM characters WHERE Online <> 0",
                null,
                null);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            if (this.connection != null)
            {
                this.connection.Dispose();
                this.connection = null;
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static DatabasePreflightExitCode ClassifyOpenFailure(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                var mysqlException = current as MySqlException;
                if (mysqlException != null)
                {
                    switch (mysqlException.ErrorCode)
                    {
                        case MySqlErrorCode.AccessDenied:
                        case MySqlErrorCode.DatabaseAccessDenied:
                            return DatabasePreflightExitCode.AuthenticationFailure;
                        case MySqlErrorCode.NoDatabaseSelected:
                        case MySqlErrorCode.UnknownDatabase:
                        case MySqlErrorCode.NoSuchDb:
                            return DatabasePreflightExitCode.WrongDatabase;
                        case MySqlErrorCode.ConnectionCountError:
                        case MySqlErrorCode.UnableToConnectToHost:
                        case MySqlErrorCode.HandshakeError:
                        case MySqlErrorCode.ServerShutdown:
                        case MySqlErrorCode.IPSocketError:
                        case MySqlErrorCode.CommandTimeoutExpired:
                            return DatabasePreflightExitCode.NetworkFailure;
                    }
                }

                if (current is SocketException || current is TimeoutException)
                {
                    return DatabasePreflightExitCode.NetworkFailure;
                }

                current = current.InnerException;
            }

            return DatabasePreflightExitCode.InternalFailure;
        }

        private static DatabasePreflightFailureException QueryFailure(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                var mysqlException = current as MySqlException;
                if (mysqlException != null)
                {
                    int errorCode = (int)mysqlException.ErrorCode;
                    if (errorCode == 1054 || errorCode == 1146)
                    {
                        return new DatabasePreflightFailureException(DatabasePreflightExitCode.MissingSchema);
                    }
                }

                current = current.InnerException;
            }

            return new DatabasePreflightFailureException(DatabasePreflightExitCode.ReadFailure);
        }

        private object ExecuteScalar(string commandText, string parameterName, object parameterValue)
        {
            this.EnsureOpen();
            try
            {
                using (IDbCommand command = this.connection.CreateCommand())
                {
                    command.CommandText = commandText;
                    if (parameterName != null)
                    {
                        AddParameter(command, parameterName, parameterValue);
                    }

                    return command.ExecuteScalar();
                }
            }
            catch (Exception exception)
            {
                throw QueryFailure(exception);
            }
        }

        private void EnsureOpen()
        {
            if (this.connection == null || this.connection.State != ConnectionState.Open)
            {
                throw new DatabasePreflightFailureException(DatabasePreflightExitCode.InternalFailure);
            }
        }
    }
}
