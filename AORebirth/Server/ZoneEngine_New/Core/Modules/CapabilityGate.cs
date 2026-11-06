namespace ZoneEngine_New.Core.Modules
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using AORebirth.Modules.World;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// One module's grants from the Modules.json "Capabilities" map. Without an entry everything is allowed (module API
    /// 1.1 behaviour); with one, a member it does not list throws <see cref="UnauthorizedAccessException"/>, logged the
    /// first time per capability. An operation's capability is its name (module API 1.5); the operations that replace
    /// 1.2/1.3 actions are also granted by the old capability name.
    /// </summary>
    internal sealed class CapabilityGate
    {
        /// <summary>The pre-1.5 capability that also grants each operation.</summary>
        internal static readonly IReadOnlyDictionary<string, string> LegacyOperationCapabilities =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [OperationNames.NpcRefresh] = ModuleCapabilities.RefreshNpc,
                [OperationNames.NpcRespawn] = ModuleCapabilities.RespawnNpc,
                [OperationNames.PlayerMessage] = ModuleCapabilities.SendMessages,
                [OperationNames.PlayerFeedback] = ModuleCapabilities.SendMessages
            };

        private readonly string _moduleId;
        private readonly HashSet<string>? _granted;
        private readonly IZoneLogger _logger;
        private readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.OrdinalIgnoreCase);

        public CapabilityGate(string moduleId, IEnumerable<string>? granted, IZoneLogger logger)
        {
            _moduleId = moduleId;
            _granted = granted == null ? null : new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        public string ModuleId => _moduleId;

        public bool Allows(string capability) => _granted == null || _granted.Contains(capability);

        /// <summary>Whether the module may invoke <paramref name="operation"/>: its name, or the old capability for it.</summary>
        public bool AllowsOperation(string operation)
            => Allows(operation)
                || (LegacyOperationCapabilities.TryGetValue(operation, out string? legacy) && Allows(legacy));

        public void RequireOperation(string operation)
        {
            if (!AllowsOperation(operation))
                Require(operation);
        }

        internal void Log(string message) => _logger.Info(message);

        public void Require(string capability)
        {
            if (_granted == null || _granted.Contains(capability))
                return;

            string message = "Module " + _moduleId + " is not granted the '" + capability + "' capability (Config/Modules.json Capabilities).";
            if (_reported.TryAdd(capability, 0))
                _logger.Warn(message);
            throw new UnauthorizedAccessException(message);
        }
    }

    internal sealed class GatedWorld(IZoneWorld world, CapabilityGate gate) : IZoneWorld
    {
        public IReadOnlyList<PlayerInfo> Players()
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Players();
        }

        public PlayerInfo? Player(int characterId)
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Player(characterId);
        }

        public IReadOnlyList<int> Playfields()
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Playfields();
        }

        public IReadOnlyList<NpcInfo> Npcs(int playfieldId)
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Npcs(playfieldId);
        }

        public NpcInfo? Npc(int playfieldId, int instance)
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Npc(playfieldId, instance);
        }

        public int? Stat(EntityRef entity, int statId)
        {
            gate.Require(ModuleCapabilities.ReadWorld);
            return world.Stat(entity, statId);
        }

        public SessionInfo? Session(int characterId)
        {
            gate.Require(ModuleCapabilities.ReadSessions);
            return world.Session(characterId);
        }

        public IReadOnlyList<CooldownInfo> Cooldowns(int characterId)
        {
            gate.Require(ModuleCapabilities.ObserveActions);
            return world.Cooldowns(characterId);
        }

        public IReadOnlyDictionary<int, MessageCount>? MessageCounts(Guid sessionId)
        {
            gate.Require(ModuleCapabilities.ReadSessions);
            return world.MessageCounts(sessionId);
        }
    }

    internal sealed class GatedActions(IZoneActions actions, CapabilityGate gate) : IZoneActions
    {
        public Task<bool> RefreshNpc(int playfieldId, int instance)
        {
            gate.Require(ModuleCapabilities.RefreshNpc);
            return actions.RefreshNpc(playfieldId, instance);
        }

        public Task<bool> SendSystemMessage(int characterId, string text)
        {
            gate.Require(ModuleCapabilities.SendMessages);
            return actions.SendSystemMessage(characterId, text);
        }

        public Task<bool> SendFeedback(int characterId, int feedbackId)
        {
            gate.Require(ModuleCapabilities.SendMessages);
            return actions.SendFeedback(characterId, feedbackId);
        }

        public Task<bool> RespawnNpc(int playfieldId, int instance)
        {
            gate.Require(ModuleCapabilities.RespawnNpc);
            return actions.RespawnNpc(playfieldId, instance);
        }

        public Task<IReadOnlyList<AccountSanctionInfo>> AccountSanctions(string account, int limit = 20)
        {
            gate.Require(ModuleCapabilities.ReadSanctions);
            return actions.AccountSanctions(account, limit);
        }

        public IReadOnlyList<OperationDescriptor> Operations
            => actions.Operations.Select(x => x with { Granted = gate.AllowsOperation(x.Name) }).ToArray();

        public async Task<OperationResult> Invoke(string operation, OperationTarget target, OperationArgs args)
        {
            if (!string.IsNullOrEmpty(operation) && actions.Operations.Any(x => string.Equals(x.Name, operation, StringComparison.OrdinalIgnoreCase)))
                gate.RequireOperation(operation);

            // Account sanctions record which module issued them; the module cannot name itself otherwise.
            OperationResult result = actions is ZoneModuleActions engine
                ? await engine.Invoke(operation, target, args, "module:" + gate.ModuleId).ConfigureAwait(false)
                : await actions.Invoke(operation, target, args).ConfigureAwait(false);
            gate.Log(string.Format(CultureInfo.InvariantCulture,
                "Module {0} operation {1} target={2}:{3}{4} args=[{5}] result={6}{7}",
                gate.ModuleId, operation, target.Kind, target.Kind == TargetKind.Account ? target.Account : target.Id.ToString(CultureInfo.InvariantCulture),
                target.Kind == TargetKind.Npc ? "@" + target.PlayfieldId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                args?.ToString() ?? string.Empty, result.Code, result.Ok || result.Message.Length == 0 ? string.Empty : " (" + result.Message + ")"));
            return result;
        }
    }
}
