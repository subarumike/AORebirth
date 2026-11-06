namespace ZoneEngine_New.Core.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text.Json;

    using AORebirth.Modules;
    using AORebirth.Modules.Commands;
    using AORebirth.Modules.Content;
    using AORebirth.Modules.Events;
    using AORebirth.Modules.Network;
    using AORebirth.Modules.World;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Loads the external modules named in Config/Modules.json once at startup, each into its own load context, and
    /// holds what they registered. With no Modules.json nothing is loaded and every hook point is a no-op.
    /// Modules are trusted code running in the zone process; there is no unloading or hot reload.
    /// </summary>
    public sealed class ZoneModuleHost
    {
        private static readonly int ApiMajorVersion = typeof(IZoneModule).Assembly.GetName().Version?.Major ?? 0;

        private readonly IZoneLogger _logger;
        private readonly IKnubotScripts _knubot;
        private readonly IZoneGameData _gameData;
        private readonly List<LoadedModule> _modules = new();
        private readonly ZoneModuleActions _actions;
        private readonly ModuleMessageObservers _messages;
        private Dictionary<string, List<string>> _capabilities = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string ModuleId, ModuleGmCommand Command)> _gmCommands = new();
        private readonly HashSet<string> _required = new(StringComparer.OrdinalIgnoreCase);
        private string? _initializing;
        private IConnectionFilterProvider? _connectionFilter;
        private string? _connectionFilterOwner;
        private IHandoffHook? _handoffHook;
        private string? _handoffHookOwner;

        private ZoneModuleHost(IZoneLogger logger, IKnubotScripts knubot, IZoneGameData gameData,
            Func<Playfield.PlayfieldManager> playfields, Func<ZoneEngine_New.Core.Accounts.AccountSanctionService>? accounts,
            string? gameDataRoot, Func<ZoneEngine_New.Core.Lifecycle.ShutdownScheduler>? shutdown)
        {
            _logger = logger;
            _knubot = knubot;
            _gameData = gameData;
            Events = new ZoneModuleEvents(logger);
            World = new ZoneModuleWorld(playfields);
            _actions = new ZoneModuleActions(playfields, accounts, gameDataRoot, logger, shutdown);
            _messages = new ModuleMessageObservers(logger);
        }

        public ZoneModuleEvents Events { get; }

        /// <summary>The world view every playfield tick publishes into while a module reads it.</summary>
        internal ZoneModuleWorld World { get; }

        public int Count => _modules.Count;

        /// <summary>The message observers for new connections, or null when no module observes messages.</summary>
        public ModuleMessageObservers? MessageObservers => _messages.HasInbound || _messages.HasOutbound ? _messages : null;

        public static ZoneModuleHost Load(
            string gameDataRoot,
            IZoneLogger logger,
            Func<Knubot.KnubotCatalog> knubot,
            Func<Playfield.PlayfieldManager> playfields,
            Func<ZoneEngine_New.Core.GameData.GameDataReloadRegistry>? gameData = null,
            Func<ZoneEngine_New.Core.Accounts.AccountSanctionService>? accounts = null,
            Func<ZoneEngine_New.Core.Lifecycle.ShutdownScheduler>? shutdown = null)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(knubot);
            ArgumentNullException.ThrowIfNull(playfields);
            var host = new ZoneModuleHost(logger, new ModuleKnubotScripts(knubot),
                new ModuleGameData(gameData ?? (() => new ZoneEngine_New.Core.GameData.GameDataReloadRegistry([]))), playfields,
                accounts, gameDataRoot, shutdown);
            string? settingsPath = ModulesSettings.ResolvePath(gameDataRoot);
            if (settingsPath == null)
            {
                logger.Info("Modules: no " + ModulesSettings.ConfigFileName + "; no modules loaded.");
                return host;
            }

            ModulesSettings settings = ModulesSettings.Load(settingsPath);
            string modulesDirectory = settings.ResolveModulesDirectory(settingsPath);
            HashSet<string> required = host._required;
            required.UnionWith(settings.Required);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            host._capabilities = LoadCapabilities(settings, logger, host._actions.Operations.Select(x => x.Name));
            logger.Info($"Modules: config={settingsPath} directory={modulesDirectory} enabled={settings.Enabled.Count}");

            foreach (string id in settings.Enabled)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    continue;

                try
                {
                    host.LoadModule(id, modulesDirectory);
                }
                catch (Exception exception)
                {
                    host.Forget(id);
                    logger.Error(exception, "Module " + id + " failed to load.");
                    if (required.Contains(id))
                        throw new InvalidOperationException("Required module " + id + " failed to load.", exception);
                }
            }

            return host;
        }

        /// <summary>The registered connection filter for a new zone connection, or null when no module filters traffic.</summary>
        public IConnectionFilter? CreateConnectionFilter(Guid sessionId, EndPoint? remoteEndPoint)
        {
            IConnectionFilterProvider? provider = _connectionFilter;
            return provider?.Create(new ConnectionInfo(sessionId, remoteEndPoint));
        }

        /// <summary>Whether a module registered a handoff hook.</summary>
        public bool HasHandoffHook => _handoffHook != null;

        /// <summary>The module that registered the handoff hook, or null.</summary>
        public string? HandoffHookOwner => _handoffHookOwner;

        /// <summary>The module's decision, or admit when there is no hook. A hook that throws refuses the handoff.</summary>
        public HandoffDecision DecideHandoff(HandoffRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            IHandoffHook? hook = _handoffHook;
            if (hook == null)
                return HandoffDecision.Admit();

            try
            {
                return hook.Decide(request);
            }
            catch (Exception exception)
            {
                // A broken hook must not let clients past whatever it was guarding.
                _logger.Error(exception, "Module " + _handoffHookOwner + " handoff hook failed; refusing the handoff.");
                return HandoffDecision.Refuse("hook failed");
            }
        }

        /// <summary>
        /// The module GM commands, checked against the built-in names when the GM command dispatcher is built (before the
        /// zone accepts connections). A module whose command clashes is shut down and unloaded, as if it had failed to
        /// load; when it is Required, zone startup fails.
        /// </summary>
        internal IReadOnlyList<(string ModuleId, ModuleGmCommand Command)> BindGmCommands(IEnumerable<string> builtInNames)
        {
            var builtIns = new HashSet<string>(builtInNames, StringComparer.OrdinalIgnoreCase);
            foreach (string moduleId in _gmCommands.Where(x => builtIns.Contains(x.Command.Name)).Select(x => x.ModuleId)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            {
                string names = string.Join(", ", _gmCommands
                    .Where(x => string.Equals(x.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase) && builtIns.Contains(x.Command.Name))
                    .Select(x => "." + x.Command.Name));
                var failure = new InvalidOperationException("Module " + moduleId + " GM command clashes with a built-in: " + names);
                _logger.Error(failure, "Module " + moduleId + " failed to start; unloading it.");
                int index = _modules.FindIndex(x => string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    ShutdownModule(_modules[index]);
                    _modules.RemoveAt(index);
                }

                Forget(moduleId);
                if (_required.Contains(moduleId))
                    throw new InvalidOperationException("Required module " + moduleId + " failed to start.", failure);
            }

            foreach ((string moduleId, ModuleGmCommand command) in _gmCommands)
                _logger.Info(string.Format(CultureInfo.InvariantCulture, "Module {0} GM command .{1} gmLevel={2}",
                    moduleId, command.Name, command.RequiredGmLevel));
            return _gmCommands.ToArray();
        }

        /// <summary>Drops everything <paramref name="moduleId"/> registered.</summary>
        private void Forget(string moduleId)
        {
            Events.RemoveModule(moduleId);
            _messages.RemoveModule(moduleId);
            _gmCommands.RemoveAll(x => string.Equals(x.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_connectionFilterOwner, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                _connectionFilter = null;
                _connectionFilterOwner = null;
            }

            if (string.Equals(_handoffHookOwner, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                _handoffHook = null;
                _handoffHookOwner = null;
            }
        }

        private void ShutdownModule(LoadedModule module)
        {
            try
            {
                foreach (IZoneModule entry in module.Entries)
                    entry.Shutdown();
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Module " + module.Id + " failed to shut down.");
            }
        }

        public void Shutdown()
        {
            for (int i = _modules.Count - 1; i >= 0; i--)
                ShutdownModule(_modules[i]);

            _modules.Clear();
        }

        private void LoadModule(string id, string modulesDirectory)
        {
            string moduleDirectory = Path.GetFullPath(Path.Combine(modulesDirectory, id));
            string manifestPath = Path.Combine(moduleDirectory, ModuleManifest.FileName);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Module manifest not found.", manifestPath);

            ModuleManifest manifest;
            using (FileStream stream = File.OpenRead(manifestPath))
                manifest = JsonSerializer.Deserialize<ModuleManifest>(stream, ModulesSettings.JsonOptions)
                    ?? throw new InvalidDataException("Empty module manifest: " + manifestPath);
            if (!string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Module manifest id '{manifest.Id}' does not match enabled id '{id}'.");
            if (string.IsNullOrWhiteSpace(manifest.Assembly))
                throw new InvalidDataException("Module manifest names no assembly: " + manifestPath);

            string assemblyPath = Path.GetFullPath(Path.Combine(moduleDirectory, manifest.Assembly));
            if (!assemblyPath.StartsWith(moduleDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Module assembly must be inside the module folder: " + manifest.Assembly);
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException("Module assembly not found.", assemblyPath);

            var context = new ModuleLoadContext(id, assemblyPath);
            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
            CheckApiVersion(assembly);

            Type[] entryTypes = assembly.GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IZoneModule).IsAssignableFrom(type))
                .ToArray();
            if (entryTypes.Length == 0)
                throw new InvalidDataException("Module assembly has no public IZoneModule: " + assemblyPath);

            _capabilities.TryGetValue(manifest.Id, out List<string>? granted);
            var gate = new CapabilityGate(manifest.Id, granted, _logger);
            CheckOperations(manifest, gate);
            var moduleContext = new ZoneModuleContext(this, manifest.Id, moduleDirectory, gate);
            var entries = new List<IZoneModule>(entryTypes.Length);
            _initializing = manifest.Id;
            try
            {
                foreach (Type type in entryTypes)
                {
                    var entry = (IZoneModule)(Activator.CreateInstance(type)
                        ?? throw new InvalidOperationException("Could not create " + type.FullName));
                    entry.Initialize(moduleContext);
                    entries.Add(entry);
                }
            }
            finally
            {
                _initializing = null;
            }

            _modules.Add(new LoadedModule(manifest.Id, entries));
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Module loaded id={0} version={1} assembly={2} sha256={3} entries={4} connectionFilter={5} handoffHook={6}",
                manifest.Id,
                string.IsNullOrWhiteSpace(manifest.Version) ? "?" : manifest.Version,
                Path.GetFileName(assemblyPath),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
                entries.Count,
                string.Equals(_connectionFilterOwner, manifest.Id, StringComparison.OrdinalIgnoreCase) ? "yes" : "no",
                string.Equals(_handoffHookOwner, manifest.Id, StringComparison.OrdinalIgnoreCase) ? "yes" : "no"));
        }

        private static void CheckApiVersion(Assembly assembly)
        {
            AssemblyName? api = assembly.GetReferencedAssemblies().FirstOrDefault(reference =>
                string.Equals(reference.Name, ModuleLoadContext.SharedAssemblyName, StringComparison.OrdinalIgnoreCase));
            if (api == null)
                throw new InvalidDataException("Module does not reference " + ModuleLoadContext.SharedAssemblyName + ".");
            if ((api.Version?.Major ?? -1) != ApiMajorVersion)
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture,
                    "Module was built against module API {0}; this engine provides {1}.", api.Version, ApiMajorVersion));
        }

        /// <summary>
        /// The operations module.json says the module needs (module API 1.5) must exist on this engine and be granted, or
        /// the module does not load.
        /// </summary>
        private void CheckOperations(ModuleManifest manifest, CapabilityGate gate)
        {
            var offered = new HashSet<string>(_actions.Operations.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            foreach (string operation in manifest.Operations ?? new List<string>())
            {
                if (!offered.Contains(operation))
                    throw new InvalidDataException("Module needs operation '" + operation + "', which this engine does not offer.");
                if (!gate.AllowsOperation(operation))
                    throw new InvalidDataException("Module needs operation '" + operation
                        + "', which Config/Modules.json Capabilities does not grant it.");
            }
        }

        private static Dictionary<string, List<string>> LoadCapabilities(ModulesSettings settings, IZoneLogger logger,
            IEnumerable<string> operations)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleCapabilities.ReadWorld, ModuleCapabilities.RefreshNpc, ModuleCapabilities.SendMessages,
                ModuleCapabilities.GameDataReload, ModuleCapabilities.RespawnNpc, ModuleCapabilities.ObserveMessages,
                ModuleCapabilities.ReadSessions, ModuleCapabilities.ObserveActions, ModuleCapabilities.ObserveVisibility,
                ModuleCapabilities.ObserveChat, ModuleCapabilities.ReadChatText, ModuleCapabilities.ReadSanctions
            };
            known.UnionWith(operations);
            var capabilities = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, List<string>? names) in settings.Capabilities ?? new Dictionary<string, List<string>>())
            {
                var granted = new List<string>();
                foreach (string name in names ?? new List<string>())
                {
                    if (known.Contains(name))
                        granted.Add(name);
                    else
                        logger.Warn("Modules: unknown capability '" + name + "' for module " + id + " ignored.");
                }

                capabilities[id] = granted;
                logger.Info("Modules: " + id + " capabilities=" + (granted.Count == 0 ? "none" : string.Join(",", granted)));
            }

            return capabilities;
        }

        private void RegisterGmCommand(string moduleId, ModuleGmCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(command.Execute);
            if (!string.Equals(_initializing, moduleId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GM commands can only be registered during Initialize.");
            string name = command.Name ?? string.Empty;
            if (name.Length == 0 || name.Length > 32 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                throw new ArgumentException("A GM command name is 1-32 letters, digits, '-' or '_', without the dot: '" + name + "'.");
            if (command.RequiredGmLevel < 0)
                throw new ArgumentException("RequiredGmLevel cannot be negative for ." + name + ".");
            (string ModuleId, ModuleGmCommand Command) owner = _gmCommands.FirstOrDefault(x =>
                string.Equals(x.Command.Name, name, StringComparison.OrdinalIgnoreCase));
            if (owner.Command != null)
                throw new InvalidOperationException("GM command ." + name + " is already registered by module " + owner.ModuleId + ".");
            _gmCommands.Add((moduleId, command));
        }

        private void RegisterConnectionFilter(string moduleId, IConnectionFilterProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (_connectionFilter != null)
                throw new InvalidOperationException(
                    "Module " + _connectionFilterOwner + " already registered the connection filter; only one is allowed.");
            _connectionFilter = provider;
            _connectionFilterOwner = moduleId;
        }

        private void RegisterMessageObserver(string moduleId, IMessageObserver observer, MessageSubscription subscription)
        {
            if (!string.Equals(_initializing, moduleId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Message observers can only be registered during Initialize.");
            _messages.Add(moduleId, observer, subscription);
        }

        private void RegisterVisibilityObserver(string moduleId, IVisibilityObserver observer, VisibilitySubscription subscription)
        {
            if (!string.Equals(_initializing, moduleId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Visibility observers can only be registered during Initialize.");
            Events.Visibility.Add(moduleId, observer, subscription);
        }

        private void RegisterHandoffHook(string moduleId, IHandoffHook hook)
        {
            ArgumentNullException.ThrowIfNull(hook);
            if (!string.Equals(_initializing, moduleId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A handoff hook can only be registered during Initialize.");
            if (_handoffHook != null)
                throw new InvalidOperationException(
                    "Module " + _handoffHookOwner + " already registered the handoff hook; only one is allowed.");
            _handoffHook = hook;
            _handoffHookOwner = moduleId;
        }

        private sealed record LoadedModule(string Id, List<IZoneModule> Entries);

        private sealed class ZoneModuleContext : IZoneModuleContext
        {
            private readonly ZoneModuleHost _host;
            private readonly CapabilityGate _gate;

            public ZoneModuleContext(ZoneModuleHost host, string moduleId, string moduleDirectory, CapabilityGate gate)
            {
                _host = host;
                _gate = gate;
                ModuleId = moduleId;
                ModuleDirectory = moduleDirectory;
                Log = new ModuleLogger(host._logger, moduleId);
                Events = new ModuleEventsView(host.Events, moduleId, gate);
                Knubot = host._knubot;
                World = new GatedWorld(host.World, gate);
                Actions = new GatedActions(host._actions, gate);
                GameData = new GatedGameData(host._gameData, gate);
            }

            public IZoneWorld World { get; }

            public IZoneGameData GameData { get; }

            public IZoneActions Actions { get; }

            public string ModuleId { get; }

            public string ModuleDirectory { get; }

            public IModuleLogger Log { get; }

            public IZoneEvents Events { get; }

            public IKnubotScripts Knubot { get; }

            public IZoneClock Clock => ModuleClock.Instance;

            public T? GetConfig<T>() where T : class
            {
                string path = Path.Combine(ModuleDirectory, "config.json");
                if (!File.Exists(path))
                    return null;
                using FileStream stream = File.OpenRead(path);
                return JsonSerializer.Deserialize<T>(stream, ModulesSettings.JsonOptions);
            }

            public void RegisterConnectionFilter(IConnectionFilterProvider provider)
                => _host.RegisterConnectionFilter(ModuleId, provider);

            public void RegisterGmCommand(ModuleGmCommand command) => _host.RegisterGmCommand(ModuleId, command);

            public void RegisterHandoffHook(IHandoffHook hook) => _host.RegisterHandoffHook(ModuleId, hook);

            public void RegisterMessageObserver(IMessageObserver observer, MessageSubscription subscription)
            {
                _gate.Require(ModuleCapabilities.ObserveMessages);
                _host.RegisterMessageObserver(ModuleId, observer, subscription);
            }

            public void RegisterVisibilityObserver(IVisibilityObserver observer, VisibilitySubscription subscription)
            {
                _gate.Require(ModuleCapabilities.ObserveVisibility);
                _host.RegisterVisibilityObserver(ModuleId, observer, subscription);
            }
        }

        private sealed class ModuleEventsView : IZoneEvents
        {
            private readonly ZoneModuleEvents _events;
            private readonly string _moduleId;
            private readonly CapabilityGate _gate;

            // A module without read-chat-text gets its PlayerSpoke handler wrapped so the text is stripped; the wrapper is
            // kept to unsubscribe it.
            private readonly Dictionary<Action<PlayerSpokeEvent>, Action<PlayerSpokeEvent>> _chatWrappers = new();

            public ModuleEventsView(ZoneModuleEvents events, string moduleId, CapabilityGate gate)
            {
                _events = events;
                _moduleId = moduleId;
                _gate = gate;
            }

            public event Action<PlayerActionEvent> PlayerActionAttempted
            {
                add { _gate.Require(ModuleCapabilities.ObserveActions); _events.PlayerActionAttempted.Add(_moduleId, value); }
                remove => _events.PlayerActionAttempted.Remove(_moduleId, value);
            }

            public event Action<PlayerActionEvent> PlayerActionResolved
            {
                add { _gate.Require(ModuleCapabilities.ObserveActions); _events.PlayerActionResolved.Add(_moduleId, value); }
                remove => _events.PlayerActionResolved.Remove(_moduleId, value);
            }

            public event Action<CooldownEvent> CooldownStarted
            {
                add { _gate.Require(ModuleCapabilities.ObserveActions); _events.CooldownStarted.Add(_moduleId, value); }
                remove => _events.CooldownStarted.Remove(_moduleId, value);
            }

            public event Action<CooldownEvent> CooldownEnded
            {
                add { _gate.Require(ModuleCapabilities.ObserveActions); _events.CooldownEnded.Add(_moduleId, value); }
                remove => _events.CooldownEnded.Remove(_moduleId, value);
            }

            public event Action<PlayerMovementCorrectedEvent> PlayerMovementCorrected
            {
                add => _events.PlayerMovementCorrected.Add(_moduleId, value);
                remove => _events.PlayerMovementCorrected.Remove(_moduleId, value);
            }

            public event Action<ItemMovedEvent> ItemMoved
            {
                add => _events.ItemMoved.Add(_moduleId, value);
                remove => _events.ItemMoved.Remove(_moduleId, value);
            }

            public event Action<PlayerSpokeEvent> PlayerSpoke
            {
                add
                {
                    ArgumentNullException.ThrowIfNull(value);
                    _gate.Require(ModuleCapabilities.ObserveChat);
                    if (_gate.Allows(ModuleCapabilities.ReadChatText))
                    {
                        _events.PlayerSpoke.Add(_moduleId, value);
                        return;
                    }

                    Action<PlayerSpokeEvent> withoutText = spoke => value(new PlayerSpokeEvent
                    {
                        CharacterId = spoke.CharacterId,
                        PlayfieldId = spoke.PlayfieldId,
                        Timestamp = spoke.Timestamp,
                        RequestTimestamp = spoke.RequestTimestamp,
                        Channel = spoke.Channel,
                        Length = spoke.Length
                    });
                    lock (_chatWrappers)
                        _chatWrappers[value] = withoutText;
                    _events.PlayerSpoke.Add(_moduleId, withoutText);
                }
                remove
                {
                    lock (_chatWrappers)
                    {
                        if (_chatWrappers.Remove(value, out Action<PlayerSpokeEvent>? wrapper))
                        {
                            _events.PlayerSpoke.Remove(_moduleId, wrapper);
                            return;
                        }
                    }

                    _events.PlayerSpoke.Remove(_moduleId, value);
                }
            }

            public event Action<CombatResolvedEvent> CombatResolved
            {
                add => _events.CombatResolved.Add(_moduleId, value);
                remove => _events.CombatResolved.Remove(_moduleId, value);
            }

            public event Action<NpcKilledEvent> NpcKilled
            {
                add => _events.NpcKilled.Add(_moduleId, value);
                remove => _events.NpcKilled.Remove(_moduleId, value);
            }

            public event Action<PlayerDiedEvent> PlayerDied
            {
                add => _events.PlayerDied.Add(_moduleId, value);
                remove => _events.PlayerDied.Remove(_moduleId, value);
            }

            public event Action<PlayerPlayfieldEvent> PlayerEnteredPlayfield
            {
                add => _events.PlayerEnteredPlayfield.Add(_moduleId, value);
                remove => _events.PlayerEnteredPlayfield.Remove(_moduleId, value);
            }

            public event Action<PlayerPlayfieldEvent> PlayerLeftPlayfield
            {
                add => _events.PlayerLeftPlayfield.Add(_moduleId, value);
                remove => _events.PlayerLeftPlayfield.Remove(_moduleId, value);
            }

            public event Action<PlayerSessionEvent> PlayerLoggedIn
            {
                add => _events.PlayerLoggedIn.Add(_moduleId, value);
                remove => _events.PlayerLoggedIn.Remove(_moduleId, value);
            }

            public event Action<PlayerSessionEvent> PlayerLoggedOut
            {
                add => _events.PlayerLoggedOut.Add(_moduleId, value);
                remove => _events.PlayerLoggedOut.Remove(_moduleId, value);
            }

            public event Action<NpcSpawnEvent> NpcSpawned
            {
                add => _events.NpcSpawned.Add(_moduleId, value);
                remove => _events.NpcSpawned.Remove(_moduleId, value);
            }

            public event Action<NpcSpawnEvent> NpcDespawned
            {
                add => _events.NpcDespawned.Add(_moduleId, value);
                remove => _events.NpcDespawned.Remove(_moduleId, value);
            }

            public event Action<KnubotChatOpenedEvent> KnubotChatOpened
            {
                add => _events.KnubotChatOpened.Add(_moduleId, value);
                remove => _events.KnubotChatOpened.Remove(_moduleId, value);
            }
        }

        private sealed class ModuleLogger : IModuleLogger
        {
            private readonly IZoneLogger _logger;
            private readonly string _prefix;

            public ModuleLogger(IZoneLogger logger, string moduleId)
            {
                _logger = logger;
                _prefix = "[" + moduleId + "] ";
            }

            public void Debug(string message) => _logger.Debug(_prefix + message);

            public void Info(string message) => _logger.Info(_prefix + message);

            public void Warn(string message) => _logger.Warn(_prefix + message);

            public void Error(string message) => _logger.Error(_prefix + message);

            public void Error(Exception exception, string message) => _logger.Error(exception, _prefix + message);
        }
    }
}
