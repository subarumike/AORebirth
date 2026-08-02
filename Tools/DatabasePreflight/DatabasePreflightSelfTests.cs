namespace AORebirth.Tools.DatabasePreflight
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using MySqlConnector;

    internal static class DatabasePreflightSelfTests
    {
        private static string SafeValidConnection
        {
            get
            {
                return BuildSafeConnection(DatabaseSchemaContract.ExpectedDatabase);
            }
        }

        internal static int Run(TextWriter output)
        {
            try
            {
                VerifySchemaContract();
                VerifyMissingEnvironmentExit();
                VerifyInvalidFormatExit();
                VerifyNetworkExit();
                VerifyAuthenticationExit();
                VerifyConfiguredWrongDatabaseExit();
                VerifyActiveWrongDatabaseExit();
                VerifyMissingSchemaExit();
                VerifyReadFailureExit();
                VerifyOnlineCharactersExit();
                VerifyInternalFailureAndRedaction();
                VerifySuccessAndCompleteReadCoverage();
            }
            catch (InvalidOperationException exception)
            {
                output.WriteLine("[Database Preflight Self-Test] FAIL: " + exception.Message);
                return (int)DatabasePreflightExitCode.InternalFailure;
            }
            catch
            {
                output.WriteLine("[Database Preflight Self-Test] FAIL: unexpected deterministic test failure.");
                return (int)DatabasePreflightExitCode.InternalFailure;
            }

            output.WriteLine("[Database Preflight Self-Test] PASS: all exit codes and read-only contracts.");
            return (int)DatabasePreflightExitCode.Success;
        }

        private static void VerifySchemaContract()
        {
            Require(DatabaseSchemaContract.RequiredTables.Length == 34, "required table count is not 34");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in DatabaseSchemaContract.RequiredTables)
            {
                Require(names.Add(table), "required table list contains a duplicate");
            }
        }

        private static void VerifyMissingEnvironmentExit()
        {
            var factory = new FakeDataSourceFactory(new FakeDataSource());
            int result = RunCase(null, factory, out _);
            Require(result == 10, "missing environment did not return 10");
            Require(factory.CreateCalls == 0, "missing environment reached the data source");
        }

        private static void VerifyInvalidFormatExit()
        {
            var factory = new FakeDataSourceFactory(new FakeDataSource());
            int result = RunCase("not-a-connection-string", factory, out _);
            Require(result == 11, "invalid connection string did not return 11");
            Require(factory.CreateCalls == 0, "invalid connection string reached the data source");
        }

        private static void VerifyNetworkExit()
        {
            var source = new FakeDataSource { OpenFailure = DatabasePreflightExitCode.NetworkFailure };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 12, "network failure did not return 12");
        }

        private static void VerifyAuthenticationExit()
        {
            var source = new FakeDataSource { OpenFailure = DatabasePreflightExitCode.AuthenticationFailure };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 13, "authentication failure did not return 13");
        }

        private static void VerifyConfiguredWrongDatabaseExit()
        {
            var factory = new FakeDataSourceFactory(new FakeDataSource());
            int result = RunCase(BuildSafeConnection("wrong_database"), factory, out _);
            Require(result == 14, "configured wrong database did not return 14");
            Require(factory.CreateCalls == 0, "configured wrong database reached the data source");
        }

        private static void VerifyActiveWrongDatabaseExit()
        {
            var source = new FakeDataSource { CurrentDatabase = "wrong_database" };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 14, "active wrong database did not return 14");
        }

        private static void VerifyMissingSchemaExit()
        {
            var source = new FakeDataSource();
            source.ExistingTables.Remove("characters");
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 15, "missing schema did not return 15");
        }

        private static void VerifyReadFailureExit()
        {
            var source = new FakeDataSource { FailReadAccess = true };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 16, "read failure did not return 16");
        }

        private static void VerifyOnlineCharactersExit()
        {
            var source = new FakeDataSource { OnlineCharacters = 1 };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out _);
            Require(result == 17, "online-character guard did not return 17");
        }

        private static void VerifyInternalFailureAndRedaction()
        {
            const string sensitiveMarker = "SENSITIVE_MARKER";
            var source = new FakeDataSource { UnexpectedOpenMessage = sensitiveMarker };
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out string text);
            Require(result == 18, "internal failure did not return 18");
            Require(text.IndexOf(sensitiveMarker, StringComparison.Ordinal) < 0, "exception output was not redacted");
            Require(text.IndexOf(SafeValidConnection, StringComparison.Ordinal) < 0, "connection string was written to output");
        }

        private static void VerifySuccessAndCompleteReadCoverage()
        {
            var source = new FakeDataSource();
            int result = RunCase(SafeValidConnection, new FakeDataSourceFactory(source), out string text);
            Require(result == 0, "successful preflight did not return 0");
            Require(source.ReadTables.Count == 34, "successful preflight did not read-probe all 34 tables");
            for (int index = 0; index < DatabaseSchemaContract.RequiredTables.Length; index++)
            {
                Require(
                    string.Equals(
                        source.ReadTables[index],
                        DatabaseSchemaContract.RequiredTables[index],
                        StringComparison.Ordinal),
                    "read-probe order diverged from the schema contract");
            }

            Require(text.IndexOf(SafeValidConnection, StringComparison.Ordinal) < 0, "successful output exposed connection data");
        }

        private static int RunCase(
            string connectionString,
            IDatabasePreflightDataSourceFactory factory,
            out string output)
        {
            using (var writer = new StringWriter())
            {
                int result = DatabasePreflightCommand.Run(connectionString, factory, writer);
                output = writer.ToString();
                return result;
            }
        }

        private static string BuildSafeConnection(string databaseName)
        {
            var builder = new MySqlConnectionStringBuilder();
            builder.Server = "localhost";
            builder.Database = databaseName;
            return builder.ConnectionString;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeDataSourceFactory : IDatabasePreflightDataSourceFactory
        {
            private readonly IDatabasePreflightDataSource source;

            internal FakeDataSourceFactory(IDatabasePreflightDataSource source)
            {
                this.source = source;
            }

            internal int CreateCalls { get; private set; }

            public IDatabasePreflightDataSource Create(string validatedConnectionString)
            {
                this.CreateCalls++;
                return this.source;
            }
        }

        private sealed class FakeDataSource : IDatabasePreflightDataSource
        {
            internal FakeDataSource()
            {
                this.CurrentDatabase = DatabaseSchemaContract.ExpectedDatabase;
                this.ExistingTables = new HashSet<string>(
                    DatabaseSchemaContract.RequiredTables,
                    StringComparer.OrdinalIgnoreCase);
                this.HasOnlineColumn = true;
                this.ReadTables = new List<string>();
            }

            internal DatabasePreflightExitCode OpenFailure { get; set; }

            internal string UnexpectedOpenMessage { get; set; }

            internal string CurrentDatabase { get; set; }

            internal ISet<string> ExistingTables { get; private set; }

            internal bool HasOnlineColumn { get; set; }

            internal bool FailReadAccess { get; set; }

            internal long OnlineCharacters { get; set; }

            internal List<string> ReadTables { get; private set; }

            public void Open()
            {
                if (this.UnexpectedOpenMessage != null)
                {
                    throw new InvalidOperationException(this.UnexpectedOpenMessage);
                }

                if (this.OpenFailure != DatabasePreflightExitCode.Success)
                {
                    throw new DatabasePreflightFailureException(this.OpenFailure);
                }
            }

            string IDatabasePreflightDataSource.GetCurrentDatabase()
            {
                return this.CurrentDatabase;
            }

            public ISet<string> GetExistingTables(string databaseName)
            {
                return this.ExistingTables;
            }

            public bool HasCharactersOnlineColumn(string databaseName)
            {
                return this.HasOnlineColumn;
            }

            public void VerifyReadAccess(string tableName)
            {
                if (this.FailReadAccess)
                {
                    throw new DatabasePreflightFailureException(DatabasePreflightExitCode.ReadFailure);
                }

                this.ReadTables.Add(tableName);
            }

            public long CountOnlineCharacters()
            {
                return this.OnlineCharacters;
            }

            public void Dispose()
            {
            }
        }
    }
}
