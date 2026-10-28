namespace ZoneEngine_New
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Runtime.InteropServices;

    using AORebirth.Database.Schema;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;

    using Microsoft.Extensions.DependencyInjection;

    using NLog;
    using MySqlConnector;

    using Utility;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.WorldSimulation;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Trade;
    using ZoneEngine_New.Core.DebugMcp;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.World;

    using ConfigReadWrite = Utility.Config.ConfigReadWrite;

    public static class Program
    {
        private static volatile bool exited;
        private static ServiceProvider? rootServices;
        private static ZoneNetworkHost? networkHost;
        private static DebugMcpHost? debugMcp;
        private static PlayfieldManager? playfieldManager;
        private static IChatEngineLink? chatEngineLink;
        private static WorldLink? worldLink;
        private static ZoneEngine_New.Core.Modules.ZoneModuleHost? modules;
        private static int shutdownStarted;
        private static volatile bool shutdownFailed;
        private static readonly ManualResetEventSlim shutdownCompleted = new(false);

        private static int Main(string[] args)
        {
            // AODB playfield RDB parsers read strings with Windows-1252.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Windows maps console close to SIGHUP and logoff/shutdown to SIGTERM, then ends the
            // process as soon as the handler returns, so the save must finish inside the handler.
            using PosixSignalRegistration termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, TerminationRequested);
            using PosixSignalRegistration hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, TerminationRequested);
            AppDomain.CurrentDomain.ProcessExit += ProcessExiting;
            Console.CancelKeyPress += ConsoleCancelKeyPress;
            try
            {
                RuntimeStartup.ValidateArguments(args);
                RedirectConsoleLog(args, "/stdout-log", "--stdout-log", false);
                RedirectConsoleLog(args, "/stderr-log", "--stderr-log", true);
                RuntimeStartup.ValidateConfiguration();
                if (HasArgument(args, "--validate-database"))
                    return CheckDatabase();

                bool skipPlayfieldPackagePin = RuntimeStartup.SkipPlayfieldPackagePin(args);
                if (HasArgument(args, "--validate-startup"))
                {
                    RuntimeStartup.ValidatePackage(AppContext.BaseDirectory, skipPlayfieldPackagePin);
                    Console.WriteLine("ZONEENGINE_NEW_STARTUP_VALIDATION_OK");
                    return 0;
                }
                if (CheckDatabase() != 0)
                    return 2;
                RuntimeStartup.ValidatePackage(AppContext.BaseDirectory, skipPlayfieldPackagePin);
                ZoneInstanceOptions instance = ZoneInstanceOptions.Configure(args);

                if (!InitializeLogging(instance))
                    return 1;
                OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Green);
                rootServices = BuildRootServices();
                IZoneLogger logger = rootServices.GetRequiredService<IZoneLogger>();
                IGameData gameData = rootServices.GetRequiredService<IGameData>();
                DestinationsCatalog.Instance.ConfigureRoot(gameData.RootPath);
                LogLocalityStartup();
                logger.Info(
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "GameData ready root={0} mobs={1} hashTemplates={2} hashInstances={3} monsterData={4}",
                        gameData.RootPath,
                        gameData.MobTemplateCount,
                        gameData.HashTemplateCount,
                        gameData.HashInstanceCount,
                        gameData.MonsterDataCount));
                // NPC scripts compile now, so a bad program is reported at startup rather than at the first spawn.
                rootServices.GetRequiredService<ZoneEngine_New.Core.NpcScripts.NpcScriptCatalog>();
                // External modules load before any playfield or connection can raise into them, and before World
                // registration, which says whether a module's handoff hook decides on logins to this zone.
                modules = rootServices.GetRequiredService<ZoneEngine_New.Core.Modules.ZoneModuleHost>();
                // World first: no playfield builds and no client connects before this instance is registered.
                if (!ConnectWorld(rootServices, logger, instance))
                    return 1;
                chatEngineLink = rootServices.GetRequiredService<IChatEngineLink>();
                chatEngineLink.Start();
                playfieldManager = rootServices.GetRequiredService<PlayfieldManager>();
                // Module GM commands are checked against the built-ins here, before any connection.
                _ = rootServices.GetRequiredService<GmCommandDispatcher>();
                _ = rootServices.GetRequiredService<InventoryFlushService>();
                // Subscribes to quest endings and starts the dungeon expiry and idle-release sweep.
                _ = rootServices.GetRequiredService<ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonService>();
                networkHost = rootServices.GetRequiredService<ZoneNetworkHost>();
                networkHost.Start();
                try
                {
                    debugMcp = DebugMcpHost.Start(rootServices, logger);
                }
                catch (Exception)
                {
                    Shutdown();
                    throw;
                }
                CompactStartupHeap(logger);
                RuntimeStartup.NotifyService("READY=1\nSTATUS=ZoneEngine_New ready");
                Console.WriteLine("ZONEENGINE_NEW_READY");
                logger.Info("ZoneEngine_New root container started.");
                StartShutdownFileWatcher(args);
                CommandLoop(args);
                return Shutdown() ? 0 : 3;
            }
            catch (Exception exception)
            {
                // Configuration and connector exception messages can contain credentials.
                Console.Error.WriteLine("ZONEENGINE_NEW_STARTUP_FAILED type=" + exception.GetType().Name);
                if (exception is StartupValidationException)
                    Console.Error.WriteLine("CONFIGURATION_VALIDATION_FAILED " + exception.Message);
                if (exception is InvalidDataException || exception is FileNotFoundException || exception is DirectoryNotFoundException)
                    Console.Error.WriteLine("PACKAGE_VALIDATION_FAILED " + exception.Message);
                return 1;
            }
            finally
            {
                exited = true;
                Shutdown();
                Console.CancelKeyPress -= ConsoleCancelKeyPress;
                AppDomain.CurrentDomain.ProcessExit -= ProcessExiting;
            }
        }

        private static void TerminationRequested(PosixSignalContext context)
        {
            context.Cancel = true;
            exited = true;
            try { Console.WriteLine("ZoneEngine_New termination requested signal=" + context.Signal + "; saving players."); }
            catch (Exception) { }
            Shutdown();
        }

        private static void ProcessExiting(object? sender, EventArgs e)
        {
            exited = true;
            Shutdown();
        }

        /// <summary>
        /// Registers with World and waits until it accepts this instance. World is required: without it no character
        /// can be leased, so the game port stays closed.
        /// </summary>
        private static bool ConnectWorld(ServiceProvider services, IZoneLogger logger, ZoneInstanceOptions instance)
        {
            worldLink = services.GetRequiredService<WorldLink>();
            worldLink.Attach(services.GetRequiredService<WorldZoneBridge>());
            if (modules != null)
                worldLink.AttachModules(modules);
            logger.Info(modules?.HasHandoffHook == true
                ? "Handoff hook registered by module " + modules.HandoffHookOwner + "; World asks it at character select."
                : "No handoff hook registered.");
            worldLink.Start();
            logger.Info("Connecting to World at " + worldLink.Url + " as " + instance.Name + " (" + instance.AdvertisedAddress + ":" + instance.Port + ")");
            DateTime nextNotice = DateTime.UtcNow.AddSeconds(5);
            while (!worldLink.WaitUntilRegistered(TimeSpan.FromMilliseconds(500)))
            {
                if (exited) return false;
                if (worldLink.FatalRegistrationError is string fatal)
                {
                    Console.Error.WriteLine("ZONEENGINE_NEW_STARTUP_FAILED " + fatal);
                    return false;
                }
                if (DateTime.UtcNow < nextNotice) continue;
                Console.WriteLine("ZoneEngine_New waiting for World at " + worldLink.Url + "...");
                nextNotice = DateTime.UtcNow.AddSeconds(10);
            }

            if (!DynelRegistry.ConfigureNpcRange(worldLink.NpcIdBase, worldLink.NpcIdCount))
                logger.Error("World gave a different NPC id range than the one in use");
            GameClock.SetOffset(worldLink.GameClockOffsetSeconds);
            logger.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "World registered instance={0} npcIds={1}..{2}", instance.Name, worldLink.NpcIdBase,
                worldLink.NpcIdBase + (long)worldLink.NpcIdCount - 1));
            return true;
        }

        private static int CheckDatabase()
        {
            SchemaCheckResult result = DatabaseSchemaReadiness.Check(MySqlConnectionSettings.GetRequiredConnectionString(),
                Environment.GetEnvironmentVariable("AO_REBIRTH_EXPECTED_DATABASE"));
            Console.WriteLine(result.State.ToString());
            Console.WriteLine(result.Message);
            return result.IsCurrent ? 0 : 2;
        }

        private static void RedirectConsoleLog(string[] args, string first, string second, bool error)
        {
            string? path = GetEitherArgumentValue(args, first, second);
            if (string.IsNullOrWhiteSpace(path)) return;
            var stream = new StreamWriter(new FileStream(Path.GetFullPath(path), FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
            if (error) Console.SetError(stream); else Console.SetOut(stream);
        }

        private static ServiceProvider BuildRootServices()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddSingleton<IZoneLogger, NLogZoneLogger>();
            services.AddSingleton<WorldLink>();
            services.AddSingleton<ZoneRedirects>();
            services.AddSingleton<ZoneLoginThrottle>();
            services.AddSingleton<WorldZoneBridge>();
            services.AddSingleton<CrossInstanceTransfer>();
            services.AddSingleton<WorldCommands>();
            services.AddSingleton<AORebirth.Interfaces.Persistence.Characters.ICharacterPersistenceDao>(_ =>
                new AORebirth.Database.Domain.Characters.MySqlCharacterPersistenceDao(() =>
                    new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString())));
            services.AddSingleton<AORebirth.Interfaces.Persistence.Characters.ICharacterDao>(_ =>
                new AORebirth.Database.Domain.Characters.MySqlCharacterDao(() =>
                    new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString())));
            services.AddSingleton<AORebirth.Interfaces.Persistence.Shops.IShopDao>(_ =>
                new AORebirth.Database.Domain.Shops.MySqlShopDao(() =>
                    new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString())));
            services.AddSingleton<ICharacterRepository, MySqlCharacterRepository>();
            services.AddSingleton<IStatRepository, MySqlStatRepository>();
            services.AddSingleton<MySqlInventoryRepository>();
            services.AddSingleton<IInventoryRepository>(provider => provider.GetRequiredService<MySqlInventoryRepository>());
            services.AddSingleton<MySqlUploadedNanoRepository>();
            services.AddSingleton<IUploadedNanoRepository>(provider => provider.GetRequiredService<MySqlUploadedNanoRepository>());
            services.AddSingleton<MySqlActiveNanoRepository>();
            services.AddSingleton<IActiveNanoRepository>(provider => provider.GetRequiredService<MySqlActiveNanoRepository>());
            services.AddSingleton<ISkillLockRepository, MySqlSkillLockRepository>();
            services.AddSingleton<ITrainedPerkRepository, MySqlTrainedPerkRepository>();
            services.AddSingleton<IPerkLockRepository, MySqlPerkLockRepository>();
            services.AddSingleton<IResearchRepository, MySqlResearchRepository>();
            services.AddSingleton<INanoLineLockRepository, MySqlNanoLineLockRepository>();
            services.AddSingleton<IPetNameRepository, MySqlPetNameRepository>();
            services.AddSingleton<ICharacterCoalesceCommit, MySqlCharacterCoalesceCommit>();
            services.AddSingleton<IItemInstanceIdAllocator, ItemInstanceIdAllocator>();
            services.AddSingleton<IItemTemplateCatalog, ItemTemplateCatalog>();
            services.AddSingleton<IItemBuilder, ItemBuilder>();
            services.AddSingleton(provider =>
            {
                using var connection = new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString());
                connection.Open();
                var catalog = new TeleportDestinationCatalog(
                    AORebirth.Database.Dao.TeleportRoutingDao.ReadSnapshot(connection),
                    provider.GetRequiredService<IZoneLogger>().Warn);
                provider.GetRequiredService<IZoneLogger>().Info($"Teleport DAO routing snapshot routes={catalog.Count}");
                return catalog;
            });
            services.AddSingleton<IGameData, GameDataStore>();
            services.AddSingleton(provider =>
            {
                string path = ZoneEngine_New.Core.Configuration.GameplaySettings.ResolvePath(
                    provider.GetRequiredService<IGameData>().RootPath);
                var settings = ZoneEngine_New.Core.Configuration.GameplaySettings.Load(path);
                CashLimit.Configure(settings.Inventory.MaxCredits);
                MySqlMissionDao.MaxCash = settings.Inventory.MaxCredits;
                provider.GetRequiredService<IZoneLogger>().Info(
                    $"Gameplay config stuckCooldownSeconds={settings.StuckCooldownSeconds} stuckLocation=pf {settings.StuckLocation!.Playfield} ({settings.StuckLocation.X}, {settings.StuckLocation.Y}, {settings.StuckLocation.Z}) xpKillPercentPerLevelDifference={settings.Xp.KillPercentPerLevelDifference} sideXpBonusPercent={settings.Xp.SideBonusPercent} halloweenEnabled={settings.Events.Halloween.Enabled} from {path}");
                return settings;
            });
            services.AddSingleton(provider =>
            {
                string path = ZoneEngine_New.Core.Configuration.GmPermissions.ResolvePath(
                    provider.GetRequiredService<IGameData>().RootPath);
                var permissions = ZoneEngine_New.Core.Configuration.GmPermissions.Load(path);
                provider.GetRequiredService<IZoneLogger>().Info(
                    $"GM permissions commands={permissions.Commands.Count} actions={permissions.Actions.Count} from {path}");
                return permissions;
            });
            services.AddSingleton(provider => ZoneEngine_New.Core.Modules.ZoneModuleHost.Load(
                provider.GetRequiredService<IGameData>().RootPath, provider.GetRequiredService<IZoneLogger>(),
                () => provider.GetRequiredService<ZoneEngine_New.Core.Knubot.KnubotCatalog>(),
                () => provider.GetRequiredService<PlayfieldManager>(),
                () => provider.GetRequiredService<ZoneEngine_New.Core.GameData.GameDataReloadRegistry>(),
                () => new ZoneEngine_New.Core.Data.MySqlLoginFlagsRepository()));
            services.AddSingleton<ZoneEngine_New.Core.Characters.StuckService>();
            services.AddSingleton<HashItemMinter>();
            services.AddSingleton<PlayerHydrator>();
            services.AddSingleton<ICharacterHydrationService, CharacterHydrationService>();
            services.AddSingleton<CharacterSnapshotService>();
            services.AddSingleton<IPlayfieldMetricsRegistry, PlayfieldMetricsRegistry>();
            services.AddSingleton<PlayfieldManager>();
            // Explicit Lazy so TeleportCommand can break the PlayfieldManager <-> command handler cycle.
            services.AddSingleton(provider => new Lazy<PlayfieldManager>(provider.GetRequiredService<PlayfieldManager>));
            services.AddSingleton<InventoryFlushService>();
            services.AddSingleton<IInventoryMutationPersistence, MySqlInventoryMutationPersistence>();
            services.AddSingleton<InventoryActionService>();
            services.AddSingleton(provider =>
            {
                var logger = provider.GetRequiredService<IZoneLogger>();
                return ZoneEngine_New.Core.Tradeskills.TradeskillCatalog.Load(
                    provider.GetRequiredService<IGameData>().RootPath, text => logger.Info("Tradeskills: " + text));
            });
            services.AddSingleton<ZoneEngine_New.Core.Tradeskills.TradeskillService>();
            services.AddSingleton(_ => new MySqlMissionDao(() =>
            {
                var connection = new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString());
                try { connection.Open(); return connection; }
                catch { connection.Dispose(); throw; }
            }));
            services.AddSingleton<IMissionDao>(provider => provider.GetRequiredService<MySqlMissionDao>());
            services.AddSingleton<IGeneratedMissionDao>(provider => provider.GetRequiredService<MySqlMissionDao>());
            services.AddSingleton<GeneratedMissionService>();
            services.AddSingleton(_ => AuthoredQuestCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Content")));
            services.AddSingleton<AuthoredQuestService>();
            services.AddSingleton<ItemPackageService>();
            services.AddSingleton<InventoryMoveService>();
            services.AddSingleton<ITradePersistence, MySqlTradePersistence>();
            services.AddSingleton<TradeService>();
            services.AddSingleton<ZoneMessageCodec>();

            //Chat
            services.AddSingleton<IChatEngineLink, WorldChatLink>();
            services.AddSingleton(provider => new Lazy<TeamService>(provider.GetRequiredService<TeamService>));
            services.AddSingleton(provider => new TeamService(
                provider.GetRequiredService<IChatEngineLink>(), dispatchOnOwner: (player, action) =>
                    player.Playfield?.DispatchPlayerProjection(player, action),
                world: provider.GetRequiredService<WorldLink>()));
            services.AddSingleton<VicinityChatRelay>();

            //Quests
            services.AddSingleton<ZoneEngine_New.Core.Quests.QuestCatalog>();
            services.AddSingleton<ICharacterQuestStore, MySqlCharacterQuestStore>();
            services.AddSingleton<ZoneEngine_New.Core.Quests.QuestService>();
            services.AddSingleton(_ => Utility.GameData.Missions.MissionDestinationCatalog.Load(
                AORebirth.Core.GameData.GameDataPaths.ResolveRuntimeRoot()));
            services.AddSingleton<ZoneEngine_New.Core.Quests.Dungeons.DungeonLayoutGenerator>();
            services.AddSingleton<ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonService>();

            //NPC scripts (GameData/NpcScripts)
            services.AddSingleton<ZoneEngine_New.Core.NpcScripts.NpcScriptCatalog>();

            //Knubot
            services.AddSingleton<ZoneEngine_New.Core.Knubot.KnubotCatalog>();
            services.AddSingleton<ZoneEngine_New.Core.Knubot.KnubotEffectServices>();
            services.AddSingleton<ZoneEngine_New.Core.Knubot.KnubotService>();

            //Reloadable GameData (module API 1.3): opt-in, one registration per catalog.
            services.AddSingleton<ZoneEngine_New.Core.GameData.IReloadableGameData, ZoneEngine_New.Core.GameData.MonsterCombatRulesReload>();
            services.AddSingleton<ZoneEngine_New.Core.GameData.IReloadableGameData, ZoneEngine_New.Core.GameData.NpcTemplatesReload>();
            services.AddSingleton<ZoneEngine_New.Core.GameData.IReloadableGameData, ZoneEngine_New.Core.GameData.NpcScriptsReload>();
            services.AddSingleton<ZoneEngine_New.Core.GameData.IReloadableGameData, ZoneEngine_New.Core.GameData.ItemTemplatesReload>();
            services.AddSingleton<ZoneEngine_New.Core.GameData.IReloadableGameData, ZoneEngine_New.Core.GameData.KnubotReload>();
            services.AddSingleton<ZoneEngine_New.Core.GameData.GameDataReloadRegistry>();

            //Commands
            services.AddSingleton<IGmCommand, SpawnCommand>();
            services.AddSingleton<IGmCommand, TeleportCommand>();
            services.AddSingleton<IGmCommand, SetCommand>();
            services.AddSingleton<IGmCommand, GetCommand>();
            services.AddSingleton<ZoneEngine_New.Core.Commands.Setups.AoSetupsClient>();
            services.AddSingleton<ZoneEngine_New.Core.Commands.Setups.GiveSetupService>();
            services.AddSingleton<IGmCommand, GiveCommand>();
            services.AddSingleton<IGmCommand, NpcCommand>();
            services.AddSingleton<IGmCommand, ChestCommand>();
            services.AddSingleton<IGmCommand, ServerStatsCommand>();
            services.AddSingleton<IGmCommand, QuestsCommand>();
            services.AddSingleton<IGmCommand, DiagCommand>();
            services.AddSingleton<IGmCommand, DebugCommand>();
            services.AddSingleton<IGmCommand, OnlineCommand>();
            services.AddSingleton<IGmCommand, GameTimeCommand>();
            services.AddSingleton<IGmCommand, WeatherCommand>();
            services.AddSingleton<IGmCommand, DespawnCommand>();
            services.AddSingleton<IGmCommand, VisibilityCommand>();
            services.AddSingleton<ZoneEngine_New.Core.Lifecycle.IEngineShutdown, ProgramShutdown>();
            services.AddSingleton<ZoneEngine_New.Core.Lifecycle.ShutdownScheduler>();
            services.AddSingleton<IGmCommand, ShutdownCommand>();
            services.AddSingleton<GmCommandDispatcher>();
            services.AddSingleton(provider => new Lazy<GmCommandDispatcher>(provider.GetRequiredService<GmCommandDispatcher>));
            services.AddSingleton<ZoneLoginHandler>();
            services.AddSingleton<IZoneAdmissionGate, ZoneAdmissionGate>();

            //Networking
            AddMessageHandler<CharDCMoveMessageHandler>(services);
            AddMessageHandler<CharacterActionMessageHandler>(services);
            AddMessageHandler<CharInPlayMessageHandler>(services);
            AddMessageHandler<LookAtMessageHandler>(services);
            AddMessageHandler<AttackMessageHandler>(services);
            AddMessageHandler<CharSecSpecAttackMessageHandler>(services);
            AddMessageHandler<StopFightMessageHandler>(services);
            AddMessageHandler<SocialActionCmdMessageHandler>(services);
            AddMessageHandler<FollowTargetMessageHandler>(services);
            AddMessageHandler<GenericCmdMessageHandler>(services);
            AddMessageHandler<ClientMoveItemToInventoryMessageHandler>(services);
            AddMessageHandler<ClientGetItemMessageHandler>(services);
            AddMessageHandler<DropTemplateMessageHandler>(services);
            AddMessageHandler<ClientContainerAddItemMessageHandler>(services);
            AddMessageHandler<TradeMessageHandler>(services);
            AddMessageHandler<KnuBotOpenChatWindowMessageHandler>(services);
            AddMessageHandler<KnuBotAnswerMessageHandler>(services);
            AddMessageHandler<KnuBotCloseChatWindowMessageHandler>(services);
            AddMessageHandler<KnuBotStartTradeMessageHandler>(services);
            AddMessageHandler<KnuBotTradeMessageHandler>(services);
            AddMessageHandler<KnuBotFinishTradeMessageHandler>(services);
            AddMessageHandler<RaidCmdMessageHandler>(services);
            AddMessageHandler<TeamChatMessageHandler>(services);
            AddMessageHandler<QuestAlternativeMessageHandler>(services);
            AddMessageHandler<CreateQuestMessageHandler>(services);
            AddMessageHandler<QuestMessageHandler>(services);
            AddMessageHandler<PetCommandMessageHandler>(services);
            AddMessageHandler<TextMessageHandler>(services);
            AddMessageHandler<SkillMessageHandler>(services);
            AddMessageHandler<SetStatMessageHandler>(services);
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

        private static bool Shutdown()
        {
            if (Interlocked.Exchange(ref shutdownStarted, 1) != 0)
            {
                // Returning early would let Main or a console event end the process mid-save.
                shutdownCompleted.Wait();
                return !shutdownFailed;
            }

            try
            {
                ShutdownCore();
            }
            finally
            {
                shutdownCompleted.Set();
            }

            return !shutdownFailed;
        }

        private static void ShutdownCore()
        {
            // Read-only validators must never send a service stop notification.
            if (rootServices != null)
            {
                try { RuntimeStartup.NotifyService("STOPPING=1"); }
                catch (Exception) { Console.Error.WriteLine("ZONEENGINE_NEW_STOP_NOTIFICATION_FAILED"); }
            }

            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception exception)
                {
                    shutdownFailed = true;
                    Console.Error.WriteLine("ZONEENGINE_NEW_SHUTDOWN_FAILED type=" + exception.GetType().Name);
                }
            }
            Cleanup(() => debugMcp?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            debugMcp = null;
            Cleanup(() => networkHost?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            networkHost = null;
            Cleanup(() => rootServices?.GetService<ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonService>()?.Dispose());
            Cleanup(() => playfieldManager?.Dispose());
            playfieldManager = null;
            Cleanup(() => modules?.Shutdown());
            modules = null;
            Cleanup(() => rootServices?.GetRequiredService<TeamService>().Shutdown());
            Cleanup(() => chatEngineLink?.Dispose());
            chatEngineLink = null;
            // After every logout: their lease releases go out before the link closes.
            Cleanup(() => worldLink?.FlushReleases(TimeSpan.FromSeconds(3)));
            Cleanup(() => worldLink?.Dispose());
            worldLink = null;
            Cleanup(() => rootServices?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            rootServices = null;
            Cleanup(LogManager.Shutdown);
        }

        private static bool InitializeLogging(ZoneInstanceOptions instance)
        {
            try
            {
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                ApplyGreenConsoleLogColors();
                LogUtil.ApplyConfiguredDebugDetails();
                // Instances beside the configured one share the folder: each needs its own file.
                Utility.Config.Config? config = ConfigReadWrite.Instance.CurrentConfig;
                bool primary = config == null || instance.Port == config.ZonePort;
                LogUtil.SetupFileLogging(primary
                    ? "${basedir}/ZoneEngine_NewLog.txt"
                    : "${basedir}/ZoneEngine_New." + instance.Name + ".Log.txt", LogLevel.Trace);
                DebugMcpLogBuffer.Shared.Install();
                ZoneEngine_New.Core.Logging.AsyncLogging.Install();
                LogActiveConfiguration();
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Error initializing NLog.");
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

        /// <summary>
        /// One compacting collection before the engine takes players. Loading GameData leaves the large-object heap
        /// mostly holes (measured 653 MB free of 777 MB), and the GC never compacts the LOH on its own; that memory
        /// stayed committed for the process lifetime on the memory-tight production host.
        /// </summary>
        private static void CompactStartupHeap(IZoneLogger logger)
        {
            long before = GC.GetGCMemoryInfo().TotalCommittedBytes;
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            long after = GC.GetGCMemoryInfo().TotalCommittedBytes;
            logger.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Startup heap compaction: committed {0:F0} MB -> {1:F0} MB (server GC {2})",
                before / 1048576.0, after / 1048576.0, System.Runtime.GCSettings.IsServerGC));
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
                                return;
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

        /// <summary>
        /// GM-scheduled shutdown (<c>.shutdown</c>): the same save-and-stop as a console exit, then the process ends.
        /// The interactive command loop may be blocked reading the console, so the flag alone would not stop it.
        /// </summary>
        private sealed class ProgramShutdown : ZoneEngine_New.Core.Lifecycle.IEngineShutdown
        {
            public void Shutdown(string reason)
            {
                try { Console.WriteLine("ZoneEngine_New shutdown requested: " + reason + "; saving players."); }
                catch (Exception) { }
                exited = true;
                bool ok = Program.Shutdown();
                Environment.Exit(ok ? 0 : 3);
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
