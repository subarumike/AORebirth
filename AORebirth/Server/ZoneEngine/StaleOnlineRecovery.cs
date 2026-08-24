namespace ZoneEngine
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;

    using AORebirth.Database;

    public sealed class StaleOnlineRecoveryRow
    {
        public StaleOnlineRecoveryRow(int characterId, int online)
        {
            this.CharacterId = characterId;
            this.Online = online;
        }

        public int CharacterId { get; private set; }

        public int Online { get; private set; }
    }

    public interface IStaleOnlineRecoveryStore : IDisposable
    {
        string DatabaseName { get; }

        IReadOnlyList<StaleOnlineRecoveryRow> ReadNonzeroRows();

        int ClearRows(IReadOnlyList<int> characterIds);

        long CountNonzeroRows();

        void Commit();
    }

    public interface IStaleOnlineRecoveryRuntime
    {
        DateTime UtcNow { get; }

        string ExpectedDatabase { get; }

        IDisposable AcquireProcessLock();

        bool IsOtherZoneEngineProcessRunning();

        bool IsPortListening(int port);

        IDisposable ReservePort(int port);

        IStaleOnlineRecoveryStore OpenStore();

        void Audit(string message);
    }

    public static class StaleOnlineRecovery
    {
        public static int Execute(IStaleOnlineRecoveryRuntime runtime, int zonePort)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            string timestamp = runtime.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            string expectedDatabase = runtime.ExpectedDatabase;
            if (string.IsNullOrWhiteSpace(expectedDatabase))
            {
                runtime.Audit(
                    Prefix(timestamp, "unknown")
                    + " error=MissingExpectedDatabase RECOVERY_ALLOWED=NO DATABASE_VALIDATION_ALLOWED=NO");
                return 1;
            }

            try
            {
                using (runtime.AcquireProcessLock())
                {
                    bool processDetected = runtime.IsOtherZoneEngineProcessRunning();
                    bool listenerDetected = runtime.IsPortListening(zonePort);
                    runtime.Audit(
                        Prefix(timestamp, expectedDatabase)
                        + " processDetected=" + YesNo(processDetected)
                        + " port" + zonePort + "ListenerDetected=" + YesNo(listenerDetected));

                    if (processDetected || listenerDetected)
                    {
                        runtime.Audit(
                            Prefix(timestamp, expectedDatabase)
                            + " RECOVERY_ALLOWED=NO DATABASE_VALIDATION_ALLOWED=NO");
                        return 1;
                    }

                    using (runtime.ReservePort(zonePort))
                    using (IStaleOnlineRecoveryStore store = runtime.OpenStore())
                    {
                        if (!string.Equals(store.DatabaseName, expectedDatabase, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("Connected database does not match expected database.");
                        }

                        IReadOnlyList<StaleOnlineRecoveryRow> rows = store.ReadNonzeroRows();
                        string characterIds = rows.Count == 0
                            ? "none"
                            : string.Join(",", rows.Select(row => row.CharacterId.ToString(CultureInfo.InvariantCulture)));
                        string oldValues = rows.Count == 0
                            ? "none"
                            : string.Join(",", rows.Select(
                                row => row.CharacterId.ToString(CultureInfo.InvariantCulture)
                                    + ":"
                                    + row.Online.ToString(CultureInfo.InvariantCulture)));

                        runtime.Audit(
                            Prefix(timestamp, store.DatabaseName)
                            + " staleRows=" + rows.Count.ToString(CultureInfo.InvariantCulture)
                            + " characterIds=" + characterIds
                            + " oldOnlineValues=" + oldValues);

                        if (rows.Count == 0)
                        {
                            runtime.Audit(
                                Prefix(timestamp, store.DatabaseName)
                                + " cleanupRequired=NO rowsUpdated=0 postUpdateNonzero=not-required"
                                + " RECOVERY_ALLOWED=NOT_REQUIRED DATABASE_VALIDATION_ALLOWED=YES");
                            return 0;
                        }

                        int updated = store.ClearRows(rows.Select(row => row.CharacterId).ToArray());
                        if (updated != rows.Count)
                        {
                            throw new InvalidDataException("Bounded stale Online update count mismatch.");
                        }

                        long postUpdateCount = store.CountNonzeroRows();
                        if (postUpdateCount != 0)
                        {
                            throw new InvalidDataException("Stale Online post-update verification failed.");
                        }

                        store.Commit();
                        runtime.Audit(
                            Prefix(timestamp, store.DatabaseName)
                            + " cleanupRequired=YES rowsUpdated=" + updated.ToString(CultureInfo.InvariantCulture)
                            + " postUpdateNonzero=" + postUpdateCount.ToString(CultureInfo.InvariantCulture)
                            + " RECOVERY_ALLOWED=YES DATABASE_VALIDATION_ALLOWED=YES");
                        return 0;
                    }
                }
            }
            catch (Exception exception)
            {
                StackFrame[] frames = new StackTrace(exception, true).GetFrames();
                StackFrame sourceFrame = frames == null
                    ? null
                    : frames.FirstOrDefault(frame => frame.GetFileLineNumber() != 0);
                string exceptionSource = sourceFrame == null
                    ? exception.Source ?? "unknown"
                    : sourceFrame.GetFileName() ?? exception.Source ?? "unknown";
                int exceptionLine = sourceFrame == null ? 0 : sourceFrame.GetFileLineNumber();
                runtime.Audit(
                    Prefix(timestamp, expectedDatabase)
                    + " error=" + exception.GetType().Name
                    + " exceptionMessage=" + AuditQuoted(exception.Message)
                    + " exceptionSource=" + AuditQuoted(exceptionSource)
                    + " exceptionLine=" + exceptionLine.ToString(CultureInfo.InvariantCulture)
                    + " exceptionStack=" + AuditQuoted(exception.StackTrace)
                    + " RECOVERY_ALLOWED=NO DATABASE_VALIDATION_ALLOWED=NO");
                return 1;
            }
        }

        private static string AuditQuoted(string value)
        {
            string escaped = (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return "\"" + escaped + "\"";
        }

        private static string Prefix(string timestamp, string database)
        {
            return "ZONEENGINE_STALE_ONLINE_RECOVERY timestamp=" + timestamp + " database=" + database;
        }

        private static string YesNo(bool value)
        {
            return value ? "YES" : "NO";
        }
    }

    internal static class StaleOnlineRecoveryCommand
    {
        public static int Run(string lockFile, int zonePort)
        {
            var runtime = new SystemStaleOnlineRecoveryRuntime(lockFile);
            return StaleOnlineRecovery.Execute(runtime, zonePort);
        }
    }

    internal sealed class SystemStaleOnlineRecoveryRuntime : IStaleOnlineRecoveryRuntime
    {
        private readonly string lockFile;

        public SystemStaleOnlineRecoveryRuntime(string lockFile)
        {
            if (string.IsNullOrWhiteSpace(lockFile))
            {
                throw new ArgumentException("Recovery lock file is required.", "lockFile");
            }

            this.lockFile = lockFile;
        }

        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public string ExpectedDatabase
        {
            get { return Environment.GetEnvironmentVariable("AO_REBIRTH_EXPECTED_DATABASE"); }
        }

        public IDisposable AcquireProcessLock()
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(this.lockFile));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("Recovery lock directory is unavailable.");
            }

            return new FileStream(
                this.lockFile,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.WriteThrough);
        }

        public bool IsOtherZoneEngineProcessRunning()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    if (process.Id == currentProcessId)
                    {
                        continue;
                    }

                    try
                    {
                        if (string.Equals(process.ProcessName, "ZoneEngine", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (Win32Exception)
                    {
                        throw;
                    }
                }
            }

            return false;
        }

        public bool IsPortListening(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }

        public IDisposable ReservePort(int port)
        {
            var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                reservation.ExclusiveAddressUse = true;
                reservation.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                reservation.Bind(new IPEndPoint(IPAddress.Any, port));
                return reservation;
            }
            catch
            {
                reservation.Dispose();
                throw;
            }
        }

        public IStaleOnlineRecoveryStore OpenStore()
        {
            return new AdoNetStaleOnlineRecoveryStore(Connector.GetConnection());
        }

        public void Audit(string message)
        {
            Console.WriteLine(message);
        }
    }

    internal sealed class AdoNetStaleOnlineRecoveryStore : IStaleOnlineRecoveryStore
    {
        private readonly IDbConnection connection;
        private readonly IDbTransaction transaction;
        private bool committed;

        public AdoNetStaleOnlineRecoveryStore(IDbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            this.connection = connection;
            if (this.connection.State != ConnectionState.Open)
            {
                this.connection.Open();
            }

            this.transaction = this.connection.BeginTransaction(IsolationLevel.Serializable);
        }

        public string DatabaseName
        {
            get
            {
                using (IDbCommand command = this.CreateCommand("SELECT DATABASE()"))
                {
                    return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }
        }

        public IReadOnlyList<StaleOnlineRecoveryRow> ReadNonzeroRows()
        {
            var rows = new List<StaleOnlineRecoveryRow>();
            using (IDbCommand command = this.CreateCommand(
                "SELECT Id, Online FROM characters "
                + "WHERE Online IS NOT NULL AND Online <> 0 ORDER BY Id FOR UPDATE"))
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(
                        new StaleOnlineRecoveryRow(
                            Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
                }
            }

            return rows;
        }

        public int ClearRows(IReadOnlyList<int> characterIds)
        {
            if (characterIds == null)
            {
                throw new ArgumentNullException("characterIds");
            }

            if (characterIds.Count == 0)
            {
                return 0;
            }

            using (IDbCommand command = this.connection.CreateCommand())
            {
                command.Transaction = this.transaction;
                var parameterNames = new List<string>();
                for (int index = 0; index < characterIds.Count; index++)
                {
                    string parameterName = "@characterId" + index.ToString(CultureInfo.InvariantCulture);
                    IDbDataParameter parameter = command.CreateParameter();
                    parameter.ParameterName = parameterName;
                    parameter.Value = characterIds[index];
                    command.Parameters.Add(parameter);
                    parameterNames.Add(parameterName);
                }

                command.CommandText =
                    "UPDATE characters SET Online=0 WHERE Online IS NOT NULL AND Online <> 0 AND Id IN ("
                    + string.Join(",", parameterNames)
                    + ")";
                return command.ExecuteNonQuery();
            }
        }

        public long CountNonzeroRows()
        {
            using (IDbCommand command = this.CreateCommand(
                "SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online <> 0"))
            {
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public void Commit()
        {
            this.transaction.Commit();
            this.committed = true;
        }

        public void Dispose()
        {
            if (!this.committed)
            {
                try
                {
                    this.transaction.Rollback();
                }
                catch
                {
                }
            }

            this.transaction.Dispose();
            this.connection.Dispose();
        }

        private IDbCommand CreateCommand(string commandText)
        {
            IDbCommand command = this.connection.CreateCommand();
            command.Transaction = this.transaction;
            command.CommandText = commandText;
            return command;
        }
    }
}
