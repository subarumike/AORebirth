#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ChatEngine
{
    #region Usings ...

    using System;
    using System.Data;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    #if AOREBIRTH_LINUX
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    #endif

    using AORebirth.Database;
    using AORebirth.Communication.ISComV2Server;
    using AORebirth.Communication.Messages;

    using ChatEngine.CoreServer;

    using locales;

    #if !AOREBIRTH_LINUX
    using NBug;
    using NBug.Properties;
    #endif

    using NLog;

    using Utility;

    #if !AOREBIRTH_LINUX
    using ZoneEngine.Core.Playfields;
    #endif

    using Config = Utility.Config.ConfigReadWrite;

    #endregion

    /// <summary>
    /// Program class for ChatEngine
    /// </summary>
    internal class Program
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        public static ISComV2Server ISCom;

        /// <summary>
        /// </summary>
        private static ChatServer chatServer;

        /// <summary>
        /// </summary>
        #if !AOREBIRTH_LINUX
        private static ConsoleText ct;
        #endif

        /// <summary>
        /// </summary>
        private static readonly ServerConsoleCommands consoleCommands = new ServerConsoleCommands();

        private const bool TcpEnable = true;

        private const bool UdpEnable = false;

        private static volatile bool exited = false;

        private static int cleanupStarted;

        private static int shutdownRequested;

        private static StreamWriter headlessErrorWriter;

        private static StreamWriter headlessOutputWriter;

        private static TextWriter originalErrorWriter;

        private static TextWriter originalOutputWriter;

        #if AOREBIRTH_LINUX
        private static PosixSignalRegistration sigIntRegistration;

        private static PosixSignalRegistration sigTermRegistration;
        #endif

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeConsoleCommands()
        {
            consoleCommands.Engine = "Chat";
            consoleCommands.AddEntry("start", StartServer);
            consoleCommands.AddEntry("running", IsServerRunning);
            consoleCommands.AddEntry("stop", StopServer);
            consoleCommands.AddEntry("exit", ShutDownServer);
            consoleCommands.AddEntry("quit", ShutDownServer);
            consoleCommands.AddEntry("debug", SetDebug);
            return true;
        }

        private static void SetDebug(string[] obj)
        {
            {
                if (obj.Length == 1)
                {
                    LogUtil.Toggle("");
                }
                else
                {
                    for (int i = 1; i < obj.Length; i++)
                    {
                        LogUtil.Toggle(obj[i]);
                    }
                }
            }
        }

        private static void IsServerRunning(string[] obj)
        {
            Colouring.Push(ConsoleColor.White);
            if (chatServer.IsRunning)
            {
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
            }
            else
            {
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
            }

            Colouring.Pop();
        }

        private static void ShowCommandHelp()
        {
            Colouring.Push(ConsoleColor.White);
            Console.WriteLine(locales.ServerConsoleAvailableCommands);
            Console.WriteLine("---------------------------");
            Console.WriteLine(consoleCommands.HelpAll());
            Console.WriteLine("---------------------------");
            Console.WriteLine();
            Colouring.Pop();
        }

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        private static string GetArgumentValue(string[] args, string argument)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool HasArgument(string[] args, string argument)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEitherArgument(string[] args, string first, string second)
        {
            return HasArgument(args, first) || HasArgument(args, second);
        }

        private static string GetEitherArgumentValue(string[] args, string first, string second)
        {
            string value = GetArgumentValue(args, first);
            return value ?? GetArgumentValue(args, second);
        }

        private static void CreateParentDirectory(string fileName)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void ConfigureHeadlessConsoleLogging(string[] args)
        {
            originalOutputWriter = Console.Out;
            originalErrorWriter = Console.Error;

            string stdoutLog = GetEitherArgumentValue(args, "/stdout-log", "--stdout-log");
            if (!string.IsNullOrWhiteSpace(stdoutLog))
            {
                CreateParentDirectory(stdoutLog);
                headlessOutputWriter = new StreamWriter(
                    new FileStream(stdoutLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                headlessOutputWriter.AutoFlush = true;
                Console.SetOut(headlessOutputWriter);
            }

            string stderrLog = GetEitherArgumentValue(args, "/stderr-log", "--stderr-log");
            if (!string.IsNullOrWhiteSpace(stderrLog))
            {
                CreateParentDirectory(stderrLog);
                headlessErrorWriter = new StreamWriter(
                    new FileStream(stderrLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                headlessErrorWriter.AutoFlush = true;
                Console.SetError(headlessErrorWriter);
            }
        }

        private static void FlushHeadlessConsoleLogging()
        {
            if (headlessOutputWriter != null)
            {
                headlessOutputWriter.Flush();
            }

            if (headlessErrorWriter != null)
            {
                headlessErrorWriter.Flush();
            }
        }

        private static void CloseHeadlessConsoleLogging()
        {
            FlushHeadlessConsoleLogging();

            if (headlessOutputWriter != null)
            {
                if (originalOutputWriter != null)
                {
                    Console.SetOut(originalOutputWriter);
                }

                headlessOutputWriter.Dispose();
                headlessOutputWriter = null;
            }

            if (headlessErrorWriter != null)
            {
                if (originalErrorWriter != null)
                {
                    Console.SetError(originalErrorWriter);
                }

                headlessErrorWriter.Dispose();
                headlessErrorWriter = null;
            }
        }

        private static void StartShutdownFileWatcher(string[] args)
        {
            string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
            if (string.IsNullOrWhiteSpace(shutdownFile))
            {
                return;
            }

            Thread shutdownThread = new Thread(
                () =>
                    {
                        while (!exited)
                        {
                            if (File.Exists(shutdownFile))
                            {
                                ConsumeShutdownFile(shutdownFile);
                                #if AOREBIRTH_LINUX
                                RequestShutdown("shutdown file");
                                #else
                                Console.WriteLine("Shutdown file requested.");
                                ShutDownServer(null);
                                FlushHeadlessConsoleLogging();
                                Environment.Exit(0);
                                #endif
                            }

                            Thread.Sleep(1000);
                        }
                    });

            shutdownThread.IsBackground = true;
            shutdownThread.Start();
        }

        private static bool RunHeadless(string[] args)
        {
            if (exited)
            {
                return true;
            }

            Console.WriteLine("Starting ChatEngine in headless mode.");
            StartServer(null);

            if (chatServer == null || !chatServer.IsRunning || !chatServer.TCPEnabled)
            {
                Console.Error.WriteLine("ChatEngine failed to start its TCP listener.");
                RequestShutdown("chat listener startup failure");
                return false;
            }

            if (ISCom == null || !ISCom.IsRunning || !ISCom.TCPEnabled)
            {
                Console.Error.WriteLine("ChatEngine failed to start its ISCom TCP listener.");
                RequestShutdown("ISCom listener startup failure");
                return false;
            }

            #if AOREBIRTH_LINUX
            NotifySystemd("READY=1\nSTATUS=ChatEngine listeners are ready");
            #endif

            string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
            while (!exited)
            {
                if (!string.IsNullOrWhiteSpace(shutdownFile) && File.Exists(shutdownFile))
                {
                    ConsumeShutdownFile(shutdownFile);
                    RequestShutdown("shutdown file");
                }

                Thread.Sleep(1000);
            }

            return true;
        }

        private static void ConsumeShutdownFile(string shutdownFile)
        {
            try
            {
                File.Delete(shutdownFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Unable to remove shutdown file: " + e.Message);
            }
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

        private static void RegisterShutdownSignals()
        {
            Console.CancelKeyPress += ConsoleCancelKeyPress;

            #if AOREBIRTH_LINUX
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
            #endif
        }

        private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            RequestShutdown("console cancel");
        }

        private static void UnregisterShutdownSignals()
        {
            Console.CancelKeyPress -= ConsoleCancelKeyPress;

            #if AOREBIRTH_LINUX
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
            #endif
        }

        #if AOREBIRTH_LINUX
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
            catch (Exception e)
            {
                Console.Error.WriteLine("systemd notification failed: " + e.Message);
            }
        }
        #endif

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        private static void CommandLoop(string[] args)
        {
            bool processedargs = false;
            Console.WriteLine(locales.ZoneEngineConsoleCommands);

            while (!exited)
            {
                if (!processedargs)
                {
                    if (HasArgument(args, "/autostart"))
                    {
                        Console.WriteLine(locales.ServerConsoleAutostart);
                        StartServer(null);
                    }

                    processedargs = true;
                }

                Console.Write(Environment.NewLine + "{0} >>", locales.ServerConsoleCommand);
                string consoleCommand = Console.ReadLine();

                if (consoleCommand != null)
                {
                    if (!consoleCommands.Execute(consoleCommand))
                    {
                        ShowCommandHelp();
                    }
                }
            }
        }

        private static void ShutDownServer(string[] obj)
        {
            exited = true;

            if (chatServer != null)
            {
                try
                {
                    if (chatServer.IsRunning && chatServer.TCPEnabled)
                    {
                        chatServer.TCPEnabled = false;
                    }

                    chatServer.Stop();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Chat server shutdown failed: " + e.Message);
                }
            }

            if (ISCom != null)
            {
                try
                {
                    ISCom.Stop();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("ISCom shutdown failed: " + e.Message);
                }
            }
        }

        private static void CompleteShutdown()
        {
            if (Interlocked.Exchange(ref cleanupStarted, 1) != 0)
            {
                return;
            }

            #if AOREBIRTH_LINUX
            NotifySystemd("STOPPING=1\nSTATUS=ChatEngine is stopping");
            #endif
            ShutDownServer(null);
            try
            {
                UnregisterShutdownSignals();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Signal cleanup failed: " + e.Message);
            }

            if (ISCom != null)
            {
                try
                {
                    ISCom.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("ISCom disposal failed: " + e.Message);
                }

                ISCom = null;
            }

            if (chatServer != null)
            {
                try
                {
                    chatServer.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Chat server disposal failed: " + e.Message);
                }

                chatServer = null;
            }

            #if AOREBIRTH_LINUX
            AppDomain.CurrentDomain.UnhandledException -= LinuxUnhandledException;
            TaskScheduler.UnobservedTaskException -= LinuxUnobservedTaskException;
            #endif

            try
            {
                LogManager.Shutdown();
            }
            finally
            {
                CloseHeadlessConsoleLogging();
            }
        }

        private static void StopServer(string[] obj)
        {
            if (!chatServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
                Colouring.Pop();
            }
            else
            {
                chatServer.Stop();
            }
        }

        private static void StartServer(string[] obj)
        {
            if (chatServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
                Colouring.Pop();
            }

            chatServer.Start(TcpEnable, UdpEnable);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool Initialize()
        {
            try
            {
                chatServer = new ChatServer();

                if (!InitializeLogAndBug())
                {
                    return false;
                }

                if (!InitializeTCP())
                {
                    return false;
                }

                if (!InitializeISCom())
                {
                    return false;
                }

                if (!InitializeConsoleCommands())
                {
                    return false;
                }

                #if !AOREBIRTH_LINUX
                PlayfieldLoader.CacheAllPlayfieldData();
                #endif
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeISCom()
        {
            try
            {
                ISCom = new ISComV2Server();
                ISCom.DataReceived += chatServer.ISComDataReceived;
                ISCom.TcpEndPoint = new IPEndPoint(
                    GetISComListenAddress(Config.Instance.CurrentConfig),
                    Config.Instance.CurrentConfig.CommPort);

                // Prove DynamicMessage can resolve Zone→Chat owner pet SystemChatMessage.
                Type systemChatType = typeof(SystemChatMessage);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ISCom ready; SystemChatMessage type="
                    + systemChatType.FullName
                    + " asm="
                    + systemChatType.Assembly.GetName().Name);

                ISCom.Start(true, false);
                if (!ISCom.IsRunning || !ISCom.TCPEnabled)
                {
                    throw new InvalidOperationException("ISCom TCP listener did not start.");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ISCom initialization failed: " + e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeLogAndBug()
        {
            try
            {
                // Setup and enable NLog logging.
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                #if !AOREBIRTH_LINUX
                LogUtil.SetupFileLogging("${basedir}/ChatEngineLog.txt", LogLevel.Trace);

                // NBug initialization
                SettingsOverride.LoadCustomSettings("NBug.ChatEngine.config");
                Settings.WriteLogToDisk = true;
                AppDomain.CurrentDomain.UnhandledException += Handler.UnhandledException;
                TaskScheduler.UnobservedTaskException += Handler.UnobservedTaskException;
                #else
                AppDomain.CurrentDomain.UnhandledException += LinuxUnhandledException;
                TaskScheduler.UnobservedTaskException += LinuxUnobservedTaskException;
                #endif
            }
            catch (Exception e)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error occured while initalizing NLog/NBug");
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            return true;
        }

        #if AOREBIRTH_LINUX
        private static void LinuxUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            Logger logger = LogManager.GetCurrentClassLogger();
            if (exception != null)
            {
                logger.Fatal(exception, "Unhandled ChatEngine exception");
                Console.Error.WriteLine(exception);
            }
            else
            {
                logger.Fatal("Unhandled ChatEngine exception: {0}", e.ExceptionObject);
                Console.Error.WriteLine(e.ExceptionObject);
            }

            LogManager.Flush();
        }

        private static void LinuxUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogManager.GetCurrentClassLogger().Error(e.Exception, "Unobserved ChatEngine task exception");
            Console.Error.WriteLine(e.Exception);
            LogManager.Flush();
        }
        #endif

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeTCP()
        {
            int Port = Convert.ToInt32(Config.Instance.CurrentConfig.ChatPort);
            try
            {
                chatServer.TcpEndPoint = new IPEndPoint(
                    GetChatListenAddress(Config.Instance.CurrentConfig),
                    Port);

                chatServer.MaximumPendingConnections = 100;
            }
            catch (Exception e)
            {
                Console.WriteLine(locales.ErrorIPAddressParseFailed);
                Console.Write(e.Message);
                return false;
            }

            return true;
        }

        private static string GetConfiguredConfigPath()
        {
            #if AOREBIRTH_LINUX
            string configuredPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }
            #endif

            return "Config.xml";
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

        private static void ValidateConfigurationValues(Utility.Config.Config configuration)
        {
            IPAddress configuredAddress;
            if (string.IsNullOrWhiteSpace(configuration.ListenIP)
                || !IPAddress.TryParse(configuration.ListenIP, out configuredAddress))
            {
                throw new InvalidDataException("ListenIP must be a valid IP address.");
            }

            GetChatListenAddress(configuration);

            if (configuration.ChatPort < 1 || configuration.ChatPort > 65535)
            {
                throw new InvalidDataException("ChatPort must be between 1 and 65535.");
            }

            if (configuration.CommPort < 1 || configuration.CommPort > 65535)
            {
                throw new InvalidDataException("CommPort must be between 1 and 65535.");
            }

            if (configuration.ChatPort == configuration.CommPort)
            {
                throw new InvalidDataException("ChatPort and CommPort must be distinct.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Locale))
            {
                throw new InvalidDataException("Locale must be configured.");
            }

            string requiredSqlType = Environment.GetEnvironmentVariable("AO_REBIRTH_REQUIRED_SQL_TYPE");

            #if AOREBIRTH_LINUX
            if (configuration.LogChat)
            {
                throw new InvalidDataException(
                    "LogChat must remain disabled for the first Linux deployment milestone.");
            }

            if (!string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The first Linux deployment milestone supports only the MySql provider.");
            }

            if (!string.Equals(requiredSqlType, "MySql", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "AO_REBIRTH_REQUIRED_SQL_TYPE must be MySql for the Linux deployment profile.");
            }
            #endif

            string connectionString;
            if (configuration.SQLType == "MySql")
            {
                string environmentConnection = Environment.GetEnvironmentVariable(
                    "AO_REBIRTH_MYSQL_CONNECTION");
                #if AOREBIRTH_LINUX
                if (string.IsNullOrWhiteSpace(environmentConnection))
                {
                    throw new InvalidDataException(
                        "AO_REBIRTH_MYSQL_CONNECTION is required by the Linux MySQL deployment profile.");
                }

                #endif

                connectionString = string.IsNullOrWhiteSpace(environmentConnection)
                    ? configuration.MysqlConnection
                    : environmentConnection;
                configuration.MysqlConnection = connectionString;
            }
            else if (configuration.SQLType == "MsSql")
            {
                connectionString = configuration.MsSqlConnection;
            }
            else if (configuration.SQLType == "PostgreSQL")
            {
                connectionString = configuration.PostgreConnection;
            }
            else
            {
                throw new InvalidDataException("SQLType must be MySql, MsSql, or PostgreSQL.");
            }

            if (string.IsNullOrWhiteSpace(connectionString)
                || connectionString.IndexOf("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidDataException("The selected database connection string is not configured.");
            }

            if (!string.IsNullOrWhiteSpace(requiredSqlType)
                && !string.Equals(configuration.SQLType, requiredSqlType, StringComparison.Ordinal))
            {
                throw new InvalidDataException("SQLType does not match the required deployment provider.");
            }

            ValidateProviderConnection(configuration.SQLType, connectionString);
        }

        private static void ValidateProviderConnection(string sqlType, string connectionString)
        {
            try
            {
                IDbConnection connection;
                if (sqlType == "MySql")
                {
                    connection = new MySQLConnector(connectionString).GetConnection();
                }
                else if (sqlType == "MsSql")
                {
                    connection = new MSSqlConnector(connectionString).GetConnection();
                }
                else
                {
                    connection = new NpgsqlConnector(connectionString).GetConnection();
                }

                using (connection)
                {
                    if (connection.State != ConnectionState.Closed)
                    {
                        throw new InvalidOperationException(
                            "Startup validation must not open a database connection.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidDataException("The selected database connection string syntax is invalid.", e);
            }
        }

        private static IPAddress GetISComListenAddress(Utility.Config.Config configuration)
        {
            string listenIP = configuration.ListenIP;
            #if AOREBIRTH_LINUX
            listenIP = Environment.GetEnvironmentVariable("AO_REBIRTH_ISCOM_LISTEN_IP");
            if (string.IsNullOrWhiteSpace(listenIP))
            {
                listenIP = configuration.ISCommLocalIP;
            }

            if (string.IsNullOrWhiteSpace(listenIP))
            {
                listenIP = "127.0.0.1";
            }
            #endif

            IPAddress address;
            if (!IPAddress.TryParse(listenIP, out address))
            {
                throw new InvalidDataException("The ISCom listen address is invalid.");
            }

            #if AOREBIRTH_LINUX
            if (!IPAddress.IsLoopback(address))
            {
                throw new InvalidDataException(
                    "The first Linux deployment requires a loopback-only ISCom listen address.");
            }
            #endif

            return address;
        }

        private static IPAddress GetChatListenAddress(Utility.Config.Config configuration)
        {
            string listenIP = configuration.ListenIP;
            #if AOREBIRTH_LINUX
            listenIP = Environment.GetEnvironmentVariable("AO_REBIRTH_CHAT_LISTEN_IP");
            if (string.IsNullOrWhiteSpace(listenIP))
            {
                listenIP = "127.0.0.1";
            }
            #endif

            IPAddress address;
            if (!IPAddress.TryParse(listenIP, out address))
            {
                throw new InvalidDataException("The Chat listen address is invalid.");
            }

            return address;
        }

        private static int ValidateStartup()
        {
            ChatServer validationChatServer = null;
            ISComV2Server validationISCom = null;

            try
            {
                Utility.Config.Config configuration = LoadStrictConfiguration();
                validationChatServer = new ChatServer();
                validationChatServer.TcpEndPoint = new IPEndPoint(
                    GetChatListenAddress(configuration),
                    configuration.ChatPort);
                validationChatServer.MaximumPendingConnections = 100;

                validationISCom = new ISComV2Server();
                validationISCom.TcpEndPoint = new IPEndPoint(
                    GetISComListenAddress(configuration),
                    configuration.CommPort);
                validationISCom.DataReceived += validationChatServer.ISComDataReceived;

                if (validationChatServer.Channels.Count != 8
                    || validationChatServer.ConnectedClients.Count != 0
                    || validationChatServer.IsRunning
                    || validationChatServer.TCPEnabled
                    || validationISCom.IsRunning
                    || validationISCom.TCPEnabled)
                {
                    throw new InvalidOperationException("Offline ChatEngine topology validation failed.");
                }

                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                LogManager.GetCurrentClassLogger().Debug("ChatEngine startup logging validation.");
                LogManager.Flush();

                Console.WriteLine(
                    "CHATENGINE_VALIDATION_OK mode=startup channels=8 provider="
                    + configuration.SQLType
                    + " nbug=disabled listeners=0");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("CHATENGINE_VALIDATION_FAILED mode=startup error=" + e.Message);
                return 1;
            }
            finally
            {
                if (validationISCom != null)
                {
                    validationISCom.Dispose();
                }

                if (validationChatServer != null)
                {
                    validationChatServer.Dispose();
                }

                LogManager.Shutdown();
            }
        }

        private static int ValidateLifecycle(string[] args)
        {
            bool headlessLoggingConfigured = false;
            try
            {
                ConfigureHeadlessConsoleLogging(args);
                headlessLoggingConfigured = true;
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                RegisterShutdownSignals();
                Console.WriteLine("CHATENGINE_LIFECYCLE_READY listeners=0 database=closed");

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

                Console.WriteLine("CHATENGINE_LIFECYCLE_STOPPED status=clean");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("CHATENGINE_LIFECYCLE_FAILED error=" + e.Message);
                return 1;
            }
            finally
            {
                CompleteShutdown();
                if (headlessLoggingConfigured)
                {
                    CloseHeadlessConsoleLogging();
                }
            }
        }

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">
        /// Command line parameters
        /// </param>
        private static int Main(string[] args)
        {
            if (HasEitherArgument(args, "/validate-startup", "--validate-startup"))
            {
                return ValidateStartup();
            }

            if (HasEitherArgument(args, "/validate-lifecycle", "--validate-lifecycle"))
            {
                return ValidateLifecycle(args);
            }

            bool headless = HasEitherArgument(args, "/headless", "--headless");
            #if AOREBIRTH_LINUX
            if (!headless)
            {
                Console.Error.WriteLine("ChatEngine Linux service mode requires --headless.");
                return 2;
            }
            #endif

            try
            {
                if (headless)
                {
                    ConfigureHeadlessConsoleLogging(args);
                    RegisterShutdownSignals();
                }

                #if AOREBIRTH_LINUX
                LoadStrictConfiguration();
                #endif

                #if !AOREBIRTH_LINUX
                ct = new ConsoleText();
                #endif

                OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Yellow);

                Console.WriteLine();

                Console.WriteLine(locales.ServerConsoleMainText, DateTime.Now.Year);

                if (exited)
                {
                    return 0;
                }

                if (!Initialize())
                {
                    Console.WriteLine("Error occured while initilizing. Please check in log.");
                    return 1;
                }

                if (exited)
                {
                    return 0;
                }

                if (headless)
                {
                    return RunHeadless(args) ? 0 : 1;
                }

                StartShutdownFileWatcher(args);
                CommandLoop(args);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ChatEngine startup failed: " + e.Message);
                return 1;
            }
            finally
            {
                CompleteShutdown();
            }
        }

        #endregion
    }
}
