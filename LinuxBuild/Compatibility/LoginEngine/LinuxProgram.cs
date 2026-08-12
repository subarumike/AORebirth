namespace LoginEngine
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Core.Components;
    using AORebirth.Database;

    using LoginEngine.CoreServer;

    using NLog;

    using Utility;

    using Config = Utility.Config.ConfigReadWrite;

    internal static class Program
    {
        private static readonly string[] RequiredTables =
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

        private static volatile bool exited;

        private static int cleanupStarted;

        private static MemBusAdapter dispatchBus;

        private static LoginServer loginServer;

        private static int shutdownRequested;

        private static volatile bool shutdownDrainFailed;

        private static PosixSignalRegistration sigIntRegistration;

        private static PosixSignalRegistration sigTermRegistration;

        private static int Main(string[] args)
        {
            if (HasEitherArgument(args, "/validate-startup", "--validate-startup"))
            {
                return ValidateStartup();
            }

            if (HasEitherArgument(args, "/validate-database", "--validate-database"))
            {
                return ValidateDatabase();
            }

            if (HasEitherArgument(args, "/validate-lifecycle", "--validate-lifecycle"))
            {
                return ValidateLifecycle(args);
            }

            if (!HasEitherArgument(args, "/headless", "--headless"))
            {
                Console.Error.WriteLine("LoginEngine Linux service mode requires --headless.");
                return 2;
            }

            try
            {
                RegisterShutdownSignals();
                Utility.Config.Config configuration = LoadStrictConfiguration();
                InitializeLogging();
                InitializeServer(configuration);
                ValidateLiveConnection();

                Console.WriteLine("Starting LoginEngine in headless mode.");
                loginServer.Start(true, false);
                if (!loginServer.IsRunning || !loginServer.TCPEnabled)
                {
                    throw new InvalidOperationException("LoginEngine failed to start its TCP listener.");
                }

                NotifySystemd("READY=1\nSTATUS=LoginEngine listener is ready");

                string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
                while (!exited)
                {
                    if (!string.IsNullOrWhiteSpace(shutdownFile) && File.Exists(shutdownFile))
                    {
                        ConsumeShutdownFile(shutdownFile);
                        RequestShutdown("shutdown file");
                    }

                    Thread.Sleep(250);
                }

                CompleteShutdown();
                return shutdownDrainFailed ? 1 : 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("LoginEngine startup failed: " + exception.Message);
                return 1;
            }
            finally
            {
                CompleteShutdown();
            }
        }

        private static void CompleteShutdown()
        {
            if (Interlocked.Exchange(ref cleanupStarted, 1) != 0)
            {
                return;
            }

            NotifySystemd("STOPPING=1\nSTATUS=LoginEngine is stopping");
            exited = true;

            if (loginServer != null)
            {
                try
                {
                    if (loginServer.IsRunning && loginServer.TCPEnabled)
                    {
                        loginServer.TCPEnabled = false;
                    }

                    if (dispatchBus != null)
                    {
                        dispatchBus.StopAcceptingMessages();
                        if (!dispatchBus.WaitForIdle(TimeSpan.FromSeconds(30)))
                        {
                            shutdownDrainFailed = true;
                            Console.Error.WriteLine(
                                "LoginEngine shutdown failed: message dispatch did not drain within 30 seconds.");
                        }
                    }

                    loginServer.Stop();
                    loginServer.Dispose();
                }
                catch (Exception exception)
                {
                    shutdownDrainFailed = true;
                    Console.Error.WriteLine("LoginEngine shutdown failed: " + exception.Message);
                }
            }

            try
            {
                UnregisterShutdownSignals();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Signal cleanup failed: " + exception.Message);
            }

            try
            {
                LogManager.Flush();
                LogManager.Shutdown();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Logging shutdown failed: " + exception.Message);
            }
        }

        private static void ConsumeShutdownFile(string shutdownFile)
        {
            try
            {
                File.Delete(shutdownFile);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Unable to remove shutdown file: " + exception.Message);
            }
        }

        private static string GetConfiguredConfigPath()
        {
            string configuredPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            return string.IsNullOrWhiteSpace(configuredPath) ? "Config.xml" : configuredPath;
        }

        private static string GetEitherArgumentValue(string[] args, string first, string second)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], first, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[index], second, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static IPAddress GetLoginListenAddress(Utility.Config.Config configuration)
        {
            string configuredAddress = Environment.GetEnvironmentVariable("AO_REBIRTH_LOGIN_LISTEN_IP");
            if (string.IsNullOrWhiteSpace(configuredAddress))
            {
                configuredAddress = "127.0.0.1";
            }

            IPAddress address;
            if (!IPAddress.TryParse(configuredAddress, out address))
            {
                throw new InvalidDataException("The LoginEngine listen address is invalid.");
            }

            if (!IPAddress.IsLoopback(address))
            {
                throw new InvalidDataException(
                    "The Stage 7 LoginEngine listener must remain bound to a loopback address.");
            }

            return address;
        }

        private static bool HasEitherArgument(string[] args, string first, string second)
        {
            foreach (string argument in args)
            {
                if (string.Equals(argument, first, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, second, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void InitializeLogging()
        {
            LogUtil.SetupConsoleLogging(LogLevel.Debug);
            AppDomain.CurrentDomain.UnhandledException += LinuxUnhandledException;
            TaskScheduler.UnobservedTaskException += LinuxUnobservedTaskException;
        }

        private static void InitializeServer(Utility.Config.Config configuration)
        {
            var container = new MefContainer();
            object[] handlers = container.GetAllInstances(typeof(IHandleMessage)).ToArray();
            if (handlers.Length != 6)
            {
                throw new InvalidOperationException(
                    "LoginEngine MEF composition expected exactly six message handlers.");
            }

            dispatchBus = container.GetInstance<IBus>() as MemBusAdapter;
            if (dispatchBus == null)
            {
                throw new InvalidOperationException("LoginEngine did not compose its drainable message bus.");
            }

            loginServer = container.GetInstance<LoginServer>();
            loginServer.TcpEndPoint = new IPEndPoint(
                GetLoginListenAddress(configuration),
                configuration.LoginPort);
            loginServer.MaximumPendingConnections = 100;
        }

        private static void LinuxUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;
            Logger logger = LogManager.GetCurrentClassLogger();
            if (exception != null)
            {
                logger.Fatal(exception, "Unhandled LoginEngine exception");
                Console.Error.WriteLine(exception);
            }
            else
            {
                logger.Fatal("Unhandled LoginEngine exception: {0}", args.ExceptionObject);
                Console.Error.WriteLine(args.ExceptionObject);
            }

            LogManager.Flush();
        }

        private static void LinuxUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            LogManager.GetCurrentClassLogger().Error(args.Exception, "Unobserved LoginEngine task exception");
            Console.Error.WriteLine(args.Exception);
            LogManager.Flush();
        }

        private static Utility.Config.Config LoadStrictConfiguration()
        {
            string configuredPath = GetConfiguredConfigPath();
            string fullPath = Path.GetFullPath(configuredPath);
            string directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("Configuration directory does not exist.");
            }

            bool exactCaseMatch = false;
            foreach (string candidate in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.Ordinal))
                {
                    exactCaseMatch = true;
                    break;
                }
            }

            if (!exactCaseMatch)
            {
                throw new FileNotFoundException("Exact-case configuration file was not found: " + fileName);
            }

            Utility.Config.Config configuration = Config.Instance.CurrentConfig;
            if (configuration == null)
            {
                throw new InvalidDataException("Config.xml did not contain a Config document.");
            }

            ValidateConfigurationValues(configuration);
            return configuration;
        }

        private static void NotifySystemd(string state)
        {
            string notifySocket = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
            if (string.IsNullOrWhiteSpace(notifySocket))
            {
                return;
            }

            if (notifySocket[0] == '@')
            {
                notifySocket = "\0" + notifySocket.Substring(1);
            }

            try
            {
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(state);
                using (var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified))
                {
                    socket.SendTo(payload, new UnixDomainSocketEndPoint(notifySocket));
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("systemd notification failed: " + exception.Message);
            }
        }

        private static void RegisterShutdownSignals()
        {
            Console.CancelKeyPress += ConsoleCancelKeyPress;
            sigIntRegistration = PosixSignalRegistration.Create(
                PosixSignal.SIGINT,
                context =>
                    {
                        context.Cancel = true;
                        RequestShutdown("SIGINT");
                    });
            sigTermRegistration = PosixSignalRegistration.Create(
                PosixSignal.SIGTERM,
                context =>
                    {
                        context.Cancel = true;
                        RequestShutdown("SIGTERM");
                    });
        }

        private static void RequestShutdown(string reason)
        {
            if (Interlocked.Exchange(ref shutdownRequested, 1) != 0)
            {
                return;
            }

            exited = true;
            try
            {
                Console.WriteLine("Shutdown requested: " + reason + ".");
            }
            catch
            {
            }
        }

        private static int ValidateDatabase()
        {
            try
            {
                Utility.Config.Config configuration = LoadStrictConfiguration();
                if (!string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Linux database readiness gate requires MySql.");
                }

                using (IDbConnection connection = Connector.GetConnection())
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        throw new InvalidOperationException("The database connection did not open.");
                    }

                    string activeDatabase;
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT DATABASE()";
                        activeDatabase = Convert.ToString(command.ExecuteScalar());
                    }

                    if (string.IsNullOrWhiteSpace(activeDatabase))
                    {
                        throw new InvalidDataException("The connection did not select a database.");
                    }

                    string expectedDatabase = GetExpectedDatabaseName();
                    if (!string.Equals(activeDatabase, expectedDatabase, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The active database does not match the expected database.");
                    }

                    var actualTables = new HashSet<string>(StringComparer.Ordinal);
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT table_name FROM information_schema.tables "
                            + "WHERE table_schema=DATABASE() AND table_type='BASE TABLE'";
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                actualTables.Add(Convert.ToString(reader.GetValue(0)));
                            }
                        }
                    }

                    if (actualTables.Count != RequiredTables.Length)
                    {
                        throw new InvalidDataException(
                            "The active database does not contain the exact governed table set.");
                    }

                    foreach (string tableName in RequiredTables)
                    {
                        if (!actualTables.Contains(tableName))
                        {
                            throw new InvalidDataException("Required database table is missing: " + tableName);
                        }

                        using (IDbCommand command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT 1 FROM `" + tableName + "` LIMIT 0";
                            using (IDataReader reader = command.ExecuteReader())
                            {
                            }
                        }
                    }

                    long onlineColumnCount;
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM information_schema.columns "
                            + "WHERE table_schema=DATABASE() AND table_name='characters' "
                            + "AND column_name='Online'";
                        onlineColumnCount = Convert.ToInt64(command.ExecuteScalar());
                    }

                    if (onlineColumnCount != 1)
                    {
                        throw new InvalidDataException("characters.Online schema contract mismatch.");
                    }

                    Console.WriteLine(
                        "LOGINENGINE_DATABASE_OK provider=MySql requiredTables="
                        + RequiredTables.Length
                        + " visibleTables="
                        + actualTables.Count
                        + " listeners=0");
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("LOGINENGINE_DATABASE_FAILED error=" + exception.Message);
                return 1;
            }
        }

        private static int ValidateLifecycle(string[] args)
        {
            try
            {
                RegisterShutdownSignals();
                Console.WriteLine("LOGINENGINE_VALIDATION_READY mode=lifecycle listeners=0 database=closed");

                string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
                while (!exited)
                {
                    if (!string.IsNullOrWhiteSpace(shutdownFile) && File.Exists(shutdownFile))
                    {
                        ConsumeShutdownFile(shutdownFile);
                        RequestShutdown("shutdown file");
                    }

                    Thread.Sleep(100);
                }

                Console.WriteLine("LOGINENGINE_VALIDATION_OK mode=lifecycle status=clean listeners=0");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("LOGINENGINE_VALIDATION_FAILED mode=lifecycle error=" + exception.Message);
                return 1;
            }
            finally
            {
                CompleteShutdown();
            }
        }

        private static int ValidateStartup()
        {
            LoginServer validationServer = null;

            try
            {
                Utility.Config.Config configuration = LoadStrictConfiguration();
                var container = new MefContainer();
                object[] handlers = container.GetAllInstances(typeof(IHandleMessage)).ToArray();
                if (handlers.Length != 6)
                {
                    throw new InvalidOperationException(
                        "LoginEngine MEF composition expected exactly six message handlers.");
                }

                validationServer = container.GetInstance<LoginServer>();
                validationServer.TcpEndPoint = new IPEndPoint(
                    GetLoginListenAddress(configuration),
                    configuration.LoginPort);
                validationServer.MaximumPendingConnections = 100;

                if (validationServer.IsRunning
                    || validationServer.TCPEnabled
                    || validationServer.UDPEnabled
                    || validationServer.ClientCount != 0)
                {
                    throw new InvalidOperationException("Offline LoginEngine topology validation failed.");
                }

                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                LogManager.GetCurrentClassLogger().Debug("LoginEngine startup logging validation.");
                LogManager.Flush();

                Console.WriteLine(
                    "LOGINENGINE_VALIDATION_OK mode=startup handlers=6 provider="
                    + configuration.SQLType
                    + " nbug=disabled listeners=0");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("LOGINENGINE_VALIDATION_FAILED mode=startup error=" + exception.Message);
                return 1;
            }
            finally
            {
                if (validationServer != null)
                {
                    validationServer.Dispose();
                }

                LogManager.Shutdown();
            }
        }

        private static void ValidateConfigurationValues(Utility.Config.Config configuration)
        {
            IPAddress configuredListenAddress;
            if (string.IsNullOrWhiteSpace(configuration.ListenIP)
                || !IPAddress.TryParse(configuration.ListenIP, out configuredListenAddress))
            {
                throw new InvalidDataException("ListenIP must be a valid IP address.");
            }

            GetLoginListenAddress(configuration);

            if (configuration.LoginPort < 1 || configuration.LoginPort > 65535)
            {
                throw new InvalidDataException("LoginPort must be between 1 and 65535.");
            }

            IPAddress zoneAddress;
            if (string.IsNullOrWhiteSpace(configuration.ZoneIP)
                || !IPAddress.TryParse(configuration.ZoneIP, out zoneAddress)
                || !IPAddress.IsLoopback(zoneAddress))
            {
                throw new InvalidDataException(
                    "ZoneIP must remain a loopback IP address for the Stage 7 deployment profile.");
            }

            if (configuration.ZonePort < 1 || configuration.ZonePort > 65535)
            {
                throw new InvalidDataException("ZonePort must be between 1 and 65535.");
            }

            if (configuration.LoginPort == configuration.ZonePort)
            {
                throw new InvalidDataException("LoginPort and ZonePort must be distinct.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Locale))
            {
                throw new InvalidDataException("Locale must be configured.");
            }

            if (!string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Stage 7 Linux deployment supports only MySql.");
            }

            string requiredSqlType = Environment.GetEnvironmentVariable("AO_REBIRTH_REQUIRED_SQL_TYPE");
            if (!string.Equals(requiredSqlType, "MySql", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "AO_REBIRTH_REQUIRED_SQL_TYPE must be MySql for the Linux deployment profile.");
            }

            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidDataException(
                    "AO_REBIRTH_MYSQL_CONNECTION is required by the Linux MySQL deployment profile.");
            }

            if (connectionString.IndexOf("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidDataException("The selected database connection string is not configured.");
            }

            string expectedDatabase = GetExpectedDatabaseName();
            configuration.MysqlConnection = connectionString;
            ValidateProviderConnection(connectionString, expectedDatabase);
        }

        private static void ValidateLiveConnection()
        {
            using (IDbConnection connection = Connector.GetConnection())
            {
                if (connection.State != ConnectionState.Open)
                {
                    throw new InvalidOperationException("The database connection did not open.");
                }

                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1";
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    {
                        throw new InvalidOperationException("The database connectivity check failed.");
                    }
                }
            }
        }

        private static string GetExpectedDatabaseName()
        {
            string expectedDatabase = Environment.GetEnvironmentVariable(
                "AO_REBIRTH_EXPECTED_DATABASE");
            if (string.IsNullOrWhiteSpace(expectedDatabase))
            {
                throw new InvalidDataException(
                    "AO_REBIRTH_EXPECTED_DATABASE is required by the Linux deployment profile.");
            }

            return expectedDatabase;
        }

        private static void ValidateProviderConnection(string connectionString, string expectedDatabase)
        {
            string configuredDatabase;
            try
            {
                using (IDbConnection connection = new MySQLConnector(connectionString).GetConnection())
                {
                    if (connection.State != ConnectionState.Closed)
                    {
                        throw new InvalidOperationException(
                            "Startup validation must not open a database connection.");
                    }

                    configuredDatabase = connection.Database;
                }
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "The selected database connection string syntax is invalid.",
                    exception);
            }

            if (!string.Equals(configuredDatabase, expectedDatabase, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The configured database does not match AO_REBIRTH_EXPECTED_DATABASE.");
            }
        }

        private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            RequestShutdown("console cancel");
        }

        private static void UnregisterShutdownSignals()
        {
            Console.CancelKeyPress -= ConsoleCancelKeyPress;

            if (sigIntRegistration != null)
            {
                sigIntRegistration.Dispose();
                sigIntRegistration = null;
            }

            if (sigTermRegistration != null)
            {
                sigTermRegistration.Dispose();
                sigTermRegistration = null;
            }
        }
    }
}
