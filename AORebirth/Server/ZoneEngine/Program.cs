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

namespace ZoneEngine
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Communication.ISComV2Client;
    using AORebirth.Communication.Messages;
    using AORebirth.Core.Actions;
    using AORebirth.Core.Events;
    using AORebirth.Core.Items;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Playfields.OfficialPlacements;
    using AORebirth.Database;

    using locales;

#if !AOREBIRTH_LINUX
    using NBug;
    using NBug.Properties;
#endif

    using NLog;

    using Utility;
    using Utility.Config;
    using Utility.Network;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Script;

    #endregion

    /// <summary>
    /// Program Class for ZoneEngine
    /// </summary>
    internal class Program
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        public static ISComV2Client ISComClient;

        /// <summary>
        /// </summary>
        public static ZoneServer zoneServer;

        /// <summary>
        /// </summary>
        private static readonly ServerConsoleCommands consoleCommands = new ServerConsoleCommands();

        /// <summary>
        /// </summary>
        private static bool exited = false;

        private static StreamWriter headlessErrorWriter;

        private static StreamWriter headlessOutputWriter;

        #endregion

        #region Methods

        /// <summary>
        /// Check the database
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void CheckDatabase(string[] parts)
        {
            Misc.CheckDatabase();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool CheckZoneServerCreation()
        {
            try
            {
                zoneServer = new ZoneServer();
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return false;
            }

            return true;
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

        private static bool HasEitherArgument(string[] args, string firstArgument, string secondArgument)
        {
            return HasArgument(args, firstArgument) || HasArgument(args, secondArgument);
        }

        private static string GetEitherArgumentValue(string[] args, string firstArgument, string secondArgument)
        {
            string value = GetArgumentValue(args, firstArgument);
            return string.IsNullOrWhiteSpace(value) ? GetArgumentValue(args, secondArgument) : value;
        }

        private static void CreateParentDirectoryIfNeeded(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void ConfigureHeadlessConsoleLogging(string[] args)
        {
            string stdoutLog = GetEitherArgumentValue(args, "/stdout-log", "--stdout-log");
            if (!string.IsNullOrWhiteSpace(stdoutLog))
            {
                CreateParentDirectoryIfNeeded(stdoutLog);
                headlessOutputWriter = new StreamWriter(
                    new FileStream(stdoutLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                headlessOutputWriter.AutoFlush = true;
                Console.SetOut(headlessOutputWriter);
            }

            string stderrLog = GetEitherArgumentValue(args, "/stderr-log", "--stderr-log");
            if (!string.IsNullOrWhiteSpace(stderrLog))
            {
                CreateParentDirectoryIfNeeded(stderrLog);
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
                                Console.WriteLine("Shutdown file requested.");
                                ShutDownServer(null);
                                FlushHeadlessConsoleLogging();
                                Environment.Exit(0);
                            }

                            Thread.Sleep(1000);
                        }
                    });

            shutdownThread.IsBackground = true;
            shutdownThread.Start();
        }

        private static void RunHeadless(string[] args)
        {
            Console.WriteLine("Starting ZoneEngine in headless mode.");
            StartTheServer();

            string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
            while (!exited)
            {
                if (!string.IsNullOrWhiteSpace(shutdownFile) && File.Exists(shutdownFile))
                {
                    Console.WriteLine("Headless shutdown requested.");
                    ShutDownServer(null);
                    FlushHeadlessConsoleLogging();
                    Environment.Exit(0);
                }

                Thread.Sleep(1000);
            }
        }

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
                        StartTheServer();
                    }

                    processedargs = true;
                }

                string consoleCommand = Console.ReadLine();

                if (consoleCommand != null)
                {
                    if (!consoleCommands.Execute(consoleCommand))
                    {
                        ShowCommandHelp();
                    }
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender">
        /// </param>
        /// <param name="e">
        /// </param>
        private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (zoneServer != null)
            {
                exited = true;
                MissionAcgExpiryRuntime.Stop();
                ISComClient.ShutDown();
                zoneServer.DisconnectAllClients();
                LogUtil.Debug(DebugInfoDetail.Engine, "Shutting down ZoneEngine hard");
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="messageobject">
        /// </param>
        private static void ISComClientOnReceiveData(object sender, DynamicMessage messageobject)
        {
            zoneServer.ProcessISComMessage(messageobject);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool ISComInitialization()
        {
            int port;
            IPAddress chatEngineIp;
            try
            {
                ISComClient = new ISComV2Client();
                string chatip = ConfigReadWrite.Instance.CurrentConfig.ChatIP;
                chatEngineIp = IPAddress.Parse(chatip);
                port = ConfigReadWrite.Instance.CurrentConfig.CommPort;
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return false;
            }

            try
            {
                ISComClient.OnReceiveData += ISComClientOnReceiveData;
                // Configure + quiet watch. Dial only when ChatEngine is listening (pets need
                // type-35 owner NpcMessage via CE — not Vicinity). No refuse spam if CE is down.
                ISComClient.Configure(chatEngineIp, port);
                ISComClient.TryLinkIfChatEngineListening();
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Initializing methods go here
        /// </summary>
        /// <returns>
        /// true if ok
        /// </returns>
        private static bool Initialize()
        {
            Console.WriteLine();
            Colouring.Push(ConsoleColor.Green);

            if (!InitializeGameFunctions())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorInitializingGamefunctions);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!InitializeLogAndBug())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorInitializingNLogNBug);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!CheckZoneServerCreation())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorCreatingZoneServerInstance);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!ISComInitialization())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorInitializingISCom);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!InizializeTCPIP())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorTCPIPSetup);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!Misc.CheckDatabase())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorInitializingDatabase);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            try
            {
                AreteFrameworkRegistries contentRegistries =
                    AreteFrameworkBootstrap.InitializeCheckedInContent();
                MissionRuntime.Initialize(contentRegistries);
                MissionAcgBindingRuntime.Initialize();
            }
            catch (Exception exception)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Persistent mission initialization failed: " + exception.Message);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            // Local debug restarts can kill ZoneEngine before player logout saves the offline flag.
            Misc.LogOffAll();

            Colouring.Push(ConsoleColor.Green);
            if (!LoadItemsAndNanos())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorLoadingItemsNanos);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();

            Colouring.Push(ConsoleColor.Green);
            if (!LoadTradeSkills())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("No locale yet: Error reading trade skills");
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();

            if (!InitializeConsoleCommands())
            {
                return false;
            }

            Colouring.Pop();

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeConsoleCommands()
        {
            consoleCommands.Engine = "Zone";

            consoleCommands.AddEntry("start", StartServer);
            consoleCommands.AddEntry("startm", StartServerMultipleScriptDlls);
            consoleCommands.AddEntry("running", IsServerRunning);
            consoleCommands.AddEntry("ping", PingChatServer);

            consoleCommands.AddEntry("stop", StopServer);

            consoleCommands.AddEntry("exit", ShutDownServer);
            consoleCommands.AddEntry("quit", ShutDownServer);

            consoleCommands.AddEntry("check", CheckDatabase);
            consoleCommands.AddEntry("updatedb", CheckDatabase);

            consoleCommands.AddEntry("online", ShowOnlineCharacters);
            consoleCommands.AddEntry("ls", ListAvailableScripts);

            consoleCommands.AddEntry("debug", SetDebug);

            return true;
        }

        private static void SetDebug(string[] obj)
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

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeGameFunctions()
        {
            try
            {
                Colouring.Push(ConsoleColor.Green);
                Console.WriteLine(
                    "{0} Game functions loaded",
                    FunctionCollection.Instance.NumberofRegisteredFunctions());
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();
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
                // Setup and enable NLog logging to file
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                LogUtil.SetupFileLogging("${basedir}/ZoneEngineLog.txt", LogLevel.Trace);

#if !AOREBIRTH_LINUX
                // NBug initialization
                SettingsOverride.LoadCustomSettings("NBug.ZoneEngine.config");
                Settings.WriteLogToDisk = true;
                AppDomain.CurrentDomain.UnhandledException += Handler.UnhandledException;
                TaskScheduler.UnobservedTaskException += Handler.UnobservedTaskException;
#endif
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorInitializingNLogNBug);
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InizializeTCPIP()
        {
            int Port = Convert.ToInt32(ConfigReadWrite.Instance.CurrentConfig.ZonePort);
            try
            {
                EngineBindPolicy bindPolicy = EngineBindPolicy.ResolveFromEnvironment();
                Console.WriteLine("ZoneEngine bind policy: " + bindPolicy.Mode);
                Console.WriteLine("ZoneEngine listener: " + bindPolicy.AddressText + ":" + Port);
                zoneServer.TcpEndPoint = new IPEndPoint(bindPolicy.Address, Port);

                zoneServer.MaximumPendingConnections = 100;
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorIPAddressParseFailed);
                Console.Write(e.Message);
                Colouring.Pop();
                Console.ReadKey();

                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void IsServerRunning(string[] parts)
        {
            Colouring.Push(ConsoleColor.White);
            if (zoneServer.IsRunning)
            {
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
            }
            else
            {
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
            }

            Colouring.Pop();
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void ListAvailableScripts(string[] parts)
        {
            // list all available scripts, dont remove it since it does what it should
            Colouring.Push(ConsoleColor.White);
            Console.WriteLine(locales.ServerConsoleAvailableScripts + ":");

            string[] files = Directory.GetFiles(
                "Scripts" + Path.DirectorySeparatorChar,
                "*.cs",
                SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Console.WriteLine(locales.ServerConsoleNoScriptsFound);
                return;
            }

            Colouring.Push(ConsoleColor.Green);
            foreach (string s in files)
            {
                Console.WriteLine(s);
            }

            Colouring.Pop();
        }

        /// <summary>
        /// Load items and Nanos into static lists
        /// </summary>
        /// <returns>
        /// true if ok
        /// </returns>
        private static bool LoadItemsAndNanos()
        {
            Colouring.Push(ConsoleColor.Green);
            try
            {

                Console.WriteLine(locales.ItemLoaderLoadedItems, ItemLoader.CacheAllItems());
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                Colouring.Pop();
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorReadingItemsFile);
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();

            Colouring.Push(ConsoleColor.Green);
            try
            {
                Console.WriteLine(locales.NanoLoaderLoadedNanos, NanoLoader.CacheAllNanos());
                Console.WriteLine();
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                Colouring.Pop();
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ErrorReadingNanosFile);
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();

            Colouring.Push(ConsoleColor.Green);
            try
            {
                Console.WriteLine("Loaded {0} Playfields", PlayfieldLoader.CacheAllPlayfieldData());
                Console.WriteLine();
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                Colouring.Pop();
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error reading statels.dat");
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool LoadTradeSkills()
        {
            try
            {
                int temp = TradeSkill.Instance.ItemNames.Count;
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);

                return false;
            }

            return true;
        }

        #if AOREBIRTH_LINUX
        private static string GetConfiguredConfigPath()
        {
            string configuredPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            return string.IsNullOrWhiteSpace(configuredPath) ? "Config.xml" : configuredPath;
        }

        private static Utility.Config.Config LoadStrictConfiguration()
        {
            string fullPath = Path.GetFullPath(GetConfiguredConfigPath());
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

            Utility.Config.Config configuration = ConfigReadWrite.Instance.CurrentConfig;
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

            GetZoneBindPolicy();
            GetChatEngineAddress(configuration);

            if (configuration.ZonePort < 1 || configuration.ZonePort > 65535)
            {
                throw new InvalidDataException("ZonePort must be between 1 and 65535.");
            }

            if (configuration.CommPort < 1 || configuration.CommPort > 65535)
            {
                throw new InvalidDataException("CommPort must be between 1 and 65535.");
            }

            if (configuration.ZonePort == configuration.CommPort)
            {
                throw new InvalidDataException("ZonePort and CommPort must be distinct.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Locale))
            {
                throw new InvalidDataException("Locale must be configured.");
            }

            if (!string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The first Linux deployment milestone supports only the MySql provider.");
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

            configuration.MysqlConnection = connectionString;
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

        private static EngineBindPolicy GetZoneBindPolicy()
        {
            return EngineBindPolicy.ResolveFromEnvironment();
        }

        private static IPAddress GetChatEngineAddress(Utility.Config.Config configuration)
        {
            string chatIP = Environment.GetEnvironmentVariable("AO_REBIRTH_CHAT_LISTEN_IP");
            if (string.IsNullOrWhiteSpace(chatIP))
            {
                chatIP = configuration.ChatIP;
            }

            IPAddress address;
            if (!IPAddress.TryParse(chatIP, out address))
            {
                throw new InvalidDataException("The ChatEngine address is invalid.");
            }

            if (!IPAddress.IsLoopback(address))
            {
                throw new InvalidDataException(
                    "The first Linux deployment requires a loopback-only ChatEngine address.");
            }

            return address;
        }

        #endif

        private static int ValidateOfficialPlacements(string[] args)
        {
            try
            {
                string sourceSha = GetEitherArgumentValue(args, "/source-sha", "--source-sha");
                string buildPlatform = GetEitherArgumentValue(
                    args,
                    "/build-platform",
                    "--build-platform");
                string placementManifestOutput = GetEitherArgumentValue(
                    args,
                    "/placement-manifest-output",
                    "--placement-manifest-output");
                string placementProvenanceOutput = GetEitherArgumentValue(
                    args,
                    "/placement-provenance-output",
                    "--placement-provenance-output");

                string corpusRoot = OfficialPlayfieldPlacementCatalog.ResolveRuntimeCorpusRoot(
                    AppDomain.CurrentDomain.BaseDirectory);
                var catalog = new OfficialPlayfieldPlacementCatalog(corpusRoot);
                catalog.WriteValidationArtifacts(
                    sourceSha,
                    buildPlatform,
                    placementManifestOutput,
                    placementProvenanceOutput);

                OfficialPlayfieldPlacementCorpusMetrics metrics = catalog.Manifest.Metrics;
                Console.WriteLine(
                    "OFFICIAL_PLACEMENT_VALIDATION_OK sourceSha={0} buildPlatform={1} resources={2} districts={3} placements={4} uniqueAcgHash={5} authorized={6}",
                    sourceSha.ToLowerInvariant(),
                    buildPlatform.ToLowerInvariant(),
                    metrics.ResourceCount.Value,
                    metrics.DistrictCount.Value,
                    metrics.PlacementCount.Value,
                    metrics.UniqueAcgHashCount.Value,
                    metrics.RuntimeActivationAuthorizedCount.Value);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "OFFICIAL_PLACEMENT_VALIDATION_FAILED: " + exception.Message);
                return 1;
            }
        }

        #if AOREBIRTH_LINUX

        private static void ValidateRequiredRuntimeAssets()
        {
            string[] relativePaths =
                {
                    "Config.xml",
                    "items.dat",
                    "nanos.dat",
                    "playfields.dat",
                    "XML Data/Stats.xml",
                    "XML Data/Playfields.xml",
                    "Scripts/KnuBotFlappy.cs",
                    "Scripts/InfoBot.cs",
                    "Scripts/KnuBotItemGiver.cs",
                    "Scripts/PerkResetService.cs",
                    "Content/Captured/Arete/cleaning_robot_patrol_replay.csv",
                    "Content/Captured/Subway/pf127-geometry.json",
                    "Content/Official/TempleOfThreeWinds/pf1931-dungeon-geometry.json"
                };

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string relativePath in relativePaths)
            {
                string fullPath = Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Required ZoneEngine runtime asset is missing: " + relativePath);
                }
            }
        }

        private static int ValidateStartup()
        {
            ZoneServer validationZoneServer = null;

            try
            {
                Utility.Config.Config configuration = LoadStrictConfiguration();
                ValidateRequiredRuntimeAssets();

                validationZoneServer = new ZoneServer();
                EngineBindPolicy bindPolicy = GetZoneBindPolicy();
                validationZoneServer.TcpEndPoint = new IPEndPoint(
                    bindPolicy.Address,
                    configuration.ZonePort);
                validationZoneServer.MaximumPendingConnections = 100;

                if (validationZoneServer.IsRunning
                    || validationZoneServer.TCPEnabled
                    || validationZoneServer.UDPEnabled
                    || validationZoneServer.Clients.Count != 0)
                {
                    throw new InvalidOperationException("Offline ZoneEngine topology validation failed.");
                }

                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                LogManager.GetCurrentClassLogger().Debug("ZoneEngine startup logging validation.");
                LogManager.Flush();

                Console.WriteLine(
                    "ZONEENGINE_VALIDATION_OK mode=startup provider="
                    + configuration.SQLType
                    + " bindPolicy="
                    + bindPolicy.Mode
                    + " address="
                    + bindPolicy.AddressText
                    + " listeners=0 assets=ok");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ZONEENGINE_VALIDATION_FAILED mode=startup error=" + e.Message);
                return 1;
            }
            finally
            {
                if (validationZoneServer != null)
                {
                    validationZoneServer.Dispose();
                }

                LogManager.Shutdown();
            }
        }

        private static int ValidateDatabase()
        {
            string[] requiredTables =
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
            string[] allowedExtensionTables =
                {
                    "account_external_mappings",
                    "account_email_verification_tokens",
                    "account_game_mappings",
                    "account_identities",
                    "account_password_reset_tokens",
                    "account_provisioning_jobs"
                };

            try
            {
                Utility.Config.Config configuration = LoadStrictConfiguration();
                if (!string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Linux database readiness gate requires MySql.");
                }

                string expectedDatabase = Environment.GetEnvironmentVariable("AO_REBIRTH_EXPECTED_DATABASE");
                if (string.IsNullOrWhiteSpace(expectedDatabase))
                {
                    throw new InvalidDataException(
                        "AO_REBIRTH_EXPECTED_DATABASE is required by the Linux deployment profile.");
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

                    if (!string.Equals(activeDatabase, expectedDatabase, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "The connected database does not match AO_REBIRTH_EXPECTED_DATABASE.");
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

                    var allowedTables = new HashSet<string>(
                        requiredTables.Concat(allowedExtensionTables),
                        StringComparer.Ordinal);

                    foreach (string tableName in requiredTables)
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

                    foreach (string tableName in actualTables)
                    {
                        if (!allowedTables.Contains(tableName))
                        {
                            throw new InvalidDataException("Unexpected database table: " + tableName);
                        }
                    }

                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "Id",
                        "int",
                        "int",
                        "NO",
                        null,
                        "auto_increment",
                        1);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "CharacterId",
                        "int",
                        "int",
                        "NO",
                        null,
                        string.Empty,
                        2);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "NanoId",
                        "int",
                        "int unsigned",
                        "NO",
                        null,
                        string.Empty,
                        3);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "Strain",
                        "int",
                        "int unsigned",
                        "NO",
                        null,
                        string.Empty,
                        4);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "NanoInstance",
                        "int",
                        "int",
                        "NO",
                        "0",
                        string.Empty,
                        5);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "DurationCentiseconds",
                        "int",
                        "int",
                        "NO",
                        "0",
                        string.Empty,
                        6);
                    ValidateRequiredDatabaseColumn(
                        connection,
                        "charactersactivenanos",
                        "ExpiresAtUtcTicks",
                        "bigint",
                        "bigint",
                        "NO",
                        "0",
                        string.Empty,
                        7);

                    long activeNanoColumnCount;
                    long activeNanoIndexCount;
                    long activeNanoPrimaryKeyCount;
                    string activeNanoTableContract;
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM information_schema.columns "
                            + "WHERE table_schema=DATABASE() AND table_name='charactersactivenanos'";
                        activeNanoColumnCount = Convert.ToInt64(command.ExecuteScalar());
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM information_schema.statistics "
                            + "WHERE table_schema=DATABASE() AND table_name='charactersactivenanos'";
                        activeNanoIndexCount = Convert.ToInt64(command.ExecuteScalar());
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM information_schema.statistics "
                            + "WHERE table_schema=DATABASE() AND table_name='charactersactivenanos' "
                            + "AND index_name='PRIMARY' AND non_unique=0 AND seq_in_index=1 "
                            + "AND column_name='Id'";
                        activeNanoPrimaryKeyCount = Convert.ToInt64(command.ExecuteScalar());
                    }

                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CONCAT(engine, '|', table_collation) "
                            + "FROM information_schema.tables "
                            + "WHERE table_schema=DATABASE() AND table_name='charactersactivenanos' "
                            + "AND table_type='BASE TABLE'";
                        activeNanoTableContract = Convert.ToString(command.ExecuteScalar());
                    }

                    if (activeNanoColumnCount != 7
                        || activeNanoIndexCount != 1
                        || activeNanoPrimaryKeyCount != 1
                        || !string.Equals(
                            activeNanoTableContract,
                            "InnoDB|latin1_swedish_ci",
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("charactersactivenanos table contract mismatch.");
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

                    long onlineCharacterCount;
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online <> 0";
                        onlineCharacterCount = Convert.ToInt64(command.ExecuteScalar());
                    }

                    if (onlineCharacterCount != 0)
                    {
                        throw new InvalidDataException(
                            "ZoneEngine database readiness requires zero online characters.");
                    }

                    Console.WriteLine(
                        "ZONEENGINE_DATABASE_OK provider=MySql database="
                        + activeDatabase
                        + " requiredTables="
                        + requiredTables.Length
                        + " visibleTables="
                        + actualTables.Count
                        + " onlineCharacters=0 listeners=0");
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ZONEENGINE_DATABASE_FAILED error=" + e.Message);
                return 1;
            }
        }

        private static void ValidateRequiredDatabaseColumn(
            IDbConnection connection,
            string tableName,
            string columnName,
            string expectedDataType,
            string expectedColumnType,
            string expectedNullable,
            string expectedDefault,
            string expectedExtra,
            int expectedOrdinalPosition)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT data_type, column_type, is_nullable, column_default, "
                    + "extra, generation_expression, ordinal_position "
                    + "FROM information_schema.columns "
                    + "WHERE table_schema=DATABASE() AND table_name=@tableName "
                    + "AND column_name=@columnName";

                IDbDataParameter tableParameter = command.CreateParameter();
                tableParameter.ParameterName = "@tableName";
                tableParameter.Value = tableName;
                command.Parameters.Add(tableParameter);

                IDbDataParameter columnParameter = command.CreateParameter();
                columnParameter.ParameterName = "@columnName";
                columnParameter.Value = columnName;
                command.Parameters.Add(columnParameter);

                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidDataException(
                            tableName + "." + columnName + " schema contract mismatch: column is missing.");
                    }

                    string actualDataType = Convert.ToString(reader.GetValue(0));
                    string actualColumnType = Convert.ToString(reader.GetValue(1));
                    string actualNullable = Convert.ToString(reader.GetValue(2));
                    string actualDefault = reader.IsDBNull(3)
                        ? null
                        : Convert.ToString(reader.GetValue(3));
                    string actualExtra = Convert.ToString(reader.GetValue(4));
                    string actualGenerationExpression = Convert.ToString(reader.GetValue(5));
                    int actualOrdinalPosition = Convert.ToInt32(reader.GetValue(6));

                    if (!string.Equals(actualDataType, expectedDataType, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(actualColumnType, expectedColumnType, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(actualNullable, expectedNullable, StringComparison.Ordinal)
                        || !string.Equals(actualDefault, expectedDefault, StringComparison.Ordinal)
                        || !string.Equals(actualExtra, expectedExtra, StringComparison.Ordinal)
                        || actualGenerationExpression.Length != 0
                        || actualOrdinalPosition != expectedOrdinalPosition
                        || reader.Read())
                    {
                        throw new InvalidDataException(
                            tableName + "." + columnName + " schema contract mismatch.");
                    }
                }
            }
        }

        private static int ValidateLifecycle(string[] args)
        {
            bool headlessLoggingConfigured = false;
            try
            {
                exited = false;
                ConfigureHeadlessConsoleLogging(args);
                headlessLoggingConfigured = true;
                LoadStrictConfiguration();
                ValidateRequiredRuntimeAssets();

                string shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
                if (string.IsNullOrWhiteSpace(shutdownFile))
                {
                    throw new InvalidDataException("Lifecycle validation requires --shutdown-file.");
                }

                Console.WriteLine("ZONEENGINE_LIFECYCLE_READY listeners=0 database=closed");

                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                while (!exited)
                {
                    if (File.Exists(shutdownFile))
                    {
                        try
                        {
                            File.Delete(shutdownFile);
                        }
                        catch (IOException)
                        {
                        }

                        exited = true;
                        break;
                    }

                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException("Lifecycle validation timed out waiting for shutdown file.");
                    }

                    Thread.Sleep(100);
                }

                Console.WriteLine("ZONEENGINE_LIFECYCLE_STOPPED status=clean");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ZONEENGINE_LIFECYCLE_FAILED error=" + e.Message);
                return 1;
            }
            finally
            {
                LogManager.Shutdown();
                if (headlessLoggingConfigured)
                {
                    FlushHeadlessConsoleLogging();
                }
            }
        }
        #endif

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">
        /// Command line parameters
        /// </param>
        private static void Main(string[] args)
        {
            if (HasEitherArgument(
                args,
                "/validate-official-placements",
                "--validate-official-placements"))
            {
                Environment.ExitCode = ValidateOfficialPlacements(args);
                return;
            }

            #if AOREBIRTH_LINUX
            if (HasEitherArgument(args, "/validate-startup", "--validate-startup"))
            {
                Environment.ExitCode = ValidateStartup();
                return;
            }

            if (HasEitherArgument(args, "/recover-stale-online", "--recover-stale-online"))
            {
                string recoveryLockFile = GetEitherArgumentValue(
                    args,
                    "/recovery-lock-file",
                    "--recovery-lock-file");
                if (string.IsNullOrWhiteSpace(recoveryLockFile))
                {
                    recoveryLockFile = "/run/ao-rebirth-zoneengine/stale-online-recovery.lock";
                }

                Environment.ExitCode = StaleOnlineRecoveryCommand.Run(
                    recoveryLockFile,
                    Convert.ToInt32(ConfigReadWrite.Instance.CurrentConfig.ZonePort));
                return;
            }

            if (HasEitherArgument(args, "/validate-database", "--validate-database"))
            {
                Environment.ExitCode = ValidateDatabase();
                return;
            }

            if (HasEitherArgument(args, "/validate-lifecycle", "--validate-lifecycle"))
            {
                Environment.ExitCode = ValidateLifecycle(args);
                return;
            }
            #endif

            bool headless = HasEitherArgument(args, "/headless", "--headless");
            #if AOREBIRTH_LINUX
            if (!headless)
            {
                Console.Error.WriteLine("ZoneEngine Linux service mode requires --headless.");
                Environment.ExitCode = 2;
                return;
            }
            #endif

            if (headless)
            {
                ConfigureHeadlessConsoleLogging(args);
            }

            Console.CancelKeyPress += ConsoleCancelKeyPress;

            OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Green);

            Console.WriteLine();
            Console.WriteLine(locales.ServerConsoleMainText, DateTime.Now.Year);

            if (!Initialize())
            {
                Console.WriteLine(locales.ErrorInitializingEngine);
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
            else
            {
                if (headless)
                {
                    RunHeadless(args);
                    LogManager.Configuration = null;
                    FlushHeadlessConsoleLogging();
                    return;
                }

                StartShutdownFileWatcher(args);
#if DEBUG
                StartTheServer();
#endif
                CommandLoop(args);
            }

            // NLog<->Mono lockup fix
            LogManager.Configuration = null;
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void PingChatServer(string[] parts)
        {
            // ChatCom.Server.Ping();
            Console.WriteLine("Ping is disabled till we can do it");
        }

        /// <summary>
        /// </summary>
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
        /// <param name="parts">
        /// </param>
        private static void ShowOnlineCharacters(string[] parts)
        {
            if (zoneServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.White);

                // TODO: Check all clients inside playfields
                lock (zoneServer.Clients)
                {
                    foreach (ZoneClient c in zoneServer.Clients)
                    {
                        Console.WriteLine(
                            "Character " + c.Controller.Character.Name + " online in PF "
                            + c.Controller.Character.Playfield.Identity.Instance);
                    }
                }

                Colouring.Pop();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void ShutDownServer(string[] parts)
        {
            MissionAcgExpiryRuntime.Stop();
            if (zoneServer.IsRunning)
            {
                zoneServer.Stop();
            }

            ISComClient.ShutDown();
            exited = true;
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void StartServer(string[] parts)
        {
            if (zoneServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
                Colouring.Pop();
            }
            else
            {
                // TODO: Add Sql Check.
                StartTheServer();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void StartServerMultipleScriptDlls(string[] parts)
        {
            // Multiple dll compile
            if (zoneServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
                Colouring.Pop();
            }
            else
            {
                // TODO: Add Sql Check.
                StartTheServer();
            }
        }

        /// <summary>
        /// </summary>
        private static void StartTheServer()
        {
            // TODO: Read playfield data, check which playfields have to be created, and create them
            // TODO: Cache neccessary Spawns and Mobs
            // TODO: Cache neccessary Doors 
            // TODO: Cache neccessary statels
            // TODO: Cache Vendors

            // Console.WriteLine(Core.Playfields.Playfields.Instance.playfields[0].name);

            ScriptCompiler.Instance.Compile(true);
            Console.WriteLine(ScriptCompiler.Instance.AddScriptMembers() + " chat commands loaded");
            zoneServer.Start(true, false);
            MissionAcgExpiryRuntime.Start();
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void StopServer(string[] parts)
        {
            if (!zoneServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
                Colouring.Pop();
            }
            else
            {
                MissionAcgExpiryRuntime.Stop();
                zoneServer.Stop();
            }
        }

        #endregion
    }
}
