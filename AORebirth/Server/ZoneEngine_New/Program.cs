namespace ZoneEngine_New
{
    using System;
    using System.IO;
    using System.Threading;

    using Microsoft.Extensions.DependencyInjection;

    using NLog;

    using Utility;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using ConfigReadWrite = Utility.Config.ConfigReadWrite;

    public static class Program
    {
        private static volatile bool exited;
        private static ServiceProvider? rootServices;
        private static ZoneNetworkHost? networkHost;
        private static PlayfieldManager? playfieldManager;
        private static IChatEngineLink? chatEngineLink;
        private static int shutdownStarted;

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += ConsoleCancelKeyPress;

            OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Green);
            Console.WriteLine();

            // Match legacy ZoneEngine: keep console foreground green for the session.
            Colouring.Push(ConsoleColor.Green);
            Console.WriteLine("ZoneEngine_New (root DI + PlayfieldManager + ZoneLogin)");

            if (!InitializeLogging())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Failed to initialize logging.");
                Colouring.Pop();
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
                return;
            }

            try
            {
                GameDataLoader.EnsureRootExists();
                LogLocalityStartup();
                rootServices = BuildRootServices();
                _ = rootServices.GetRequiredService<IMobTemplateCatalog>();
                chatEngineLink = rootServices.GetRequiredService<IChatEngineLink>();
                chatEngineLink.Start();
                playfieldManager = rootServices.GetRequiredService<PlayfieldManager>();
                networkHost = rootServices.GetRequiredService<ZoneNetworkHost>();
                networkHost.Start();

                IZoneLogger logger = rootServices.GetRequiredService<IZoneLogger>();
                logger.Info("ZoneEngine_New root container started.");
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Startup failed: " + exception.Message);
                Colouring.Pop();
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
                Shutdown();
                return;
            }

            StartShutdownFileWatcher(args);
            CommandLoop(args);
            Shutdown();
        }

        private static ServiceProvider BuildRootServices()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddSingleton<IZoneLogger, NLogZoneLogger>();
            services.AddSingleton<ICharacterRepository, MySqlCharacterRepository>();
            services.AddSingleton<IStatRepository, MySqlStatRepository>();
            services.AddSingleton<IInventoryRepository, MySqlInventoryRepository>();
            services.AddSingleton<IItemNameRepository, MySqlItemNameRepository>();
            services.AddSingleton<IItemTemplateCatalog, ItemTemplateCatalog>();
            services.AddSingleton<IItemBuilder, ItemBuilder>();
            services.AddSingleton<IMobTemplateCatalog, MobTemplateCatalog>();
            services.AddSingleton<PlayerHydrator>();
            services.AddSingleton<ICharacterHydrationService, CharacterHydrationService>();
            services.AddSingleton<PlayfieldManager>();
            services.AddSingleton<ZoneMessageCodec>();
            services.AddSingleton<IChatEngineLink, IsComChatEngineLink>();
            services.AddSingleton<VicinityChatRelay>();
            services.AddSingleton<IGmCommand, SpawnCommand>();
            services.AddSingleton<IGmCommand, TeleportCommand>();
            services.AddSingleton<GmCommandDispatcher>();
            services.AddSingleton<ZoneLoginHandler>();
            AddMessageHandler<CharDCMoveMessageHandler>(services);
            AddMessageHandler<CharacterActionMessageHandler>(services);
            AddMessageHandler<CharInPlayMessageHandler>(services);
            AddMessageHandler<LookAtMessageHandler>(services);
            AddMessageHandler<TextMessageHandler>(services);
            services.AddSingleton<IMessageRouter, MessageRouter>();
            services.AddSingleton<ZoneMessageDispatcher>();
            services.AddSingleton<ZoneNetworkHost>();

            return services.BuildServiceProvider();
        }

        private static void AddMessageHandler<THandler>(IServiceCollection services)
            where THandler : class, IMessageHandler
        {
            services.AddSingleton<IMessageHandler, THandler>();
        }

        private static void Shutdown()
        {
            if (Interlocked.Exchange(ref shutdownStarted, 1) != 0)
            {
                return;
            }

            if (networkHost != null)
            {
                networkHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            networkHost = null;

            playfieldManager?.Dispose();
            playfieldManager = null;

            chatEngineLink?.Dispose();
            chatEngineLink = null;

            if (rootServices != null)
            {
                rootServices.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            rootServices = null;

            LogManager.Configuration = null;
        }

        private static bool InitializeLogging()
        {
            try
            {
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                ApplyGreenConsoleLogColors();
                LogUtil.ApplyConfiguredDebugDetails();
                LogUtil.SetupFileLogging("${basedir}/ZoneEngine_NewLog.txt", LogLevel.Trace);
                LogActiveConfiguration();
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error initializing NLog");
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zone engine logs green; playfield-scoped logs (Playfield.*) gray.
        /// Warn stays yellow, Error/Fatal stay red.
        /// </summary>
        private static void ApplyGreenConsoleLogColors()
        {
            if (LogManager.Configuration?.FindTargetByName("console") is not NLog.Targets.ColoredConsoleTarget console)
                return;

            console.UseDefaultRowHighlightingRules = false;
            console.RowHighlightingRules.Clear();
            console.RowHighlightingRules.Add(
                new NLog.Targets.ConsoleRowHighlightingRule(
                    "level >= LogLevel.Error",
                    NLog.Targets.ConsoleOutputColor.Red,
                    NLog.Targets.ConsoleOutputColor.NoChange));
            console.RowHighlightingRules.Add(
                new NLog.Targets.ConsoleRowHighlightingRule(
                    "level == LogLevel.Warn",
                    NLog.Targets.ConsoleOutputColor.Yellow,
                    NLog.Targets.ConsoleOutputColor.NoChange));
            console.RowHighlightingRules.Add(
                new NLog.Targets.ConsoleRowHighlightingRule(
                    "starts-with(logger, 'Playfield')",
                    NLog.Targets.ConsoleOutputColor.Gray,
                    NLog.Targets.ConsoleOutputColor.NoChange));
            console.RowHighlightingRules.Add(
                new NLog.Targets.ConsoleRowHighlightingRule(
                    "level <= LogLevel.Info",
                    NLog.Targets.ConsoleOutputColor.Green,
                    NLog.Targets.ConsoleOutputColor.NoChange));
            LogManager.ReconfigExistingLoggers();
        }

        private static void LogActiveConfiguration()
        {
            string configPath = Path.GetFullPath(ConfigReadWrite.ResolvedConfigPath);
            string? configuredPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            string source = string.IsNullOrWhiteSpace(configuredPath)
                ? "default"
                : "AO_REBIRTH_CONFIG_PATH";

            Console.WriteLine("ZoneEngine_New configuration: " + configPath + " (source=" + source + ")");
        }

        private static void LogLocalityStartup()
        {
            string configPath = Path.GetFullPath(ConfigReadWrite.ResolvedConfigPath);
            Utility.Config.LocalitySettings? settings =
                ConfigReadWrite.Instance.CurrentConfig == null
                    ? null
                    : ConfigReadWrite.Instance.CurrentConfig.Locality;
            LocalityPolicy policy = LocalityPolicy.FromConfig(settings);
            string message = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Locality: Cell Heat Scheduling is {0} (config={1}, EnableCellHeatScheduling={2})",
                policy.EnableCellHeatScheduling ? "enabled" : "disabled",
                configPath,
                settings == null ? "<null Locality>" : settings.EnableCellHeatScheduling.ToString());

            Console.WriteLine(message);
            LogUtil.Debug(DebugInfoDetail.Engine, message);
        }

        private static void StartShutdownFileWatcher(string[] args)
        {
            string? shutdownFile = GetEitherArgumentValue(args, "/shutdown-file", "--shutdown-file");
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
                                exited = true;
                                Shutdown();
                                Environment.Exit(0);
                            }

                            Thread.Sleep(1000);
                        }
                    })
            {
                IsBackground = true
            };

            shutdownThread.Start();
        }

        private static void CommandLoop(string[] args)
        {
            if (HasArgument(args, "/autostart"))
            {
                Console.WriteLine("ZoneEngine_New /autostart accepted (network already listening).");
            }

            if (HasArgument(args, "/headless") || HasArgument(args, "--headless"))
            {
                Console.WriteLine("ZoneEngine_New headless lifecycle active.");
                while (!exited)
                {
                    Thread.Sleep(250);
                }

                return;
            }

            while (!exited)
            {
                Console.Write("> ");
                string? line = Console.ReadLine();
                if (line == null)
                {
                    break;
                }

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                switch (parts[0].ToLowerInvariant())
                {
                    case "exit":
                    case "quit":
                        exited = true;
                        return;

                    case "debug":
                        if (parts.Length == 1)
                        {
                            LogUtil.Toggle(string.Empty);
                        }
                        else
                        {
                            for (int i = 1; i < parts.Length; i++)
                            {
                                LogUtil.Toggle(parts[i]);
                            }
                        }

                        break;

                    default:
                        Console.WriteLine("Commands: debug [flags], exit, quit");
                        break;
                }
            }
        }

        private static void ConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            exited = true;
        }

        private static bool HasArgument(string[] args, string argument)
        {
            foreach (string candidate in args)
            {
                if (string.Equals(candidate, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? GetEitherArgumentValue(string[] args, string first, string second)
        {
            string? value = GetArgumentValue(args, first);
            return string.IsNullOrWhiteSpace(value) ? GetArgumentValue(args, second) : value;
        }

        private static string? GetArgumentValue(string[] args, string argument)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
