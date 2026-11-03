namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    public sealed record StaticObjectCondition(int Operator, int Value1, int Value2);

    public sealed record StaticObjectTransition(int TargetState, IReadOnlyList<StaticObjectCondition> Criteria);

    public sealed record StaticObjectSignalTimer(int SignalMask, int OddPulseCount, IReadOnlyList<StaticObjectCondition> Criteria);

    public sealed record StaticObjectState(
        IReadOnlyList<ItemSpell> EntrySpells,
        IReadOnlyList<StaticObjectSignalTimer> SignalTimers,
        IReadOnlyList<StaticObjectTransition> Transitions);

    public sealed record StaticObjectStateMachine(
        int Id,
        int InitialState,
        IReadOnlyDictionary<int, StaticObjectState> States,
        string InitialStateCompatibility,
        bool SerializeQueuedSignals = false);

    public enum StaticObjectDoorOperation
    {
        Unlock,
        Open,
        Close,
        Lock
    }

    /// <summary>Explicit server compatibility policy. Signal masks are not item Flags bits.</summary>
    public sealed record StaticObjectSignalPolicy(
        int SignalMask,
        IReadOnlyList<StaticObjectDoorOperation> DoorOperations,
        bool? UseEnabled,
        string Compatibility);

    /// <summary>
    /// ObjectStateMachines.json contains reviewed state-machine exports and explicit server compatibility policy.
    /// Ordered transitions, entry actions and consumable signal criteria retain their authored order. Native
    /// 54001 countdowns count odd-second pulses; they are separate from ItemSpell's centisecond delays.
    /// </summary>
    public sealed class StaticObjectStateMachineCatalog
    {
        public const string FileName = "ObjectStateMachines.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter<StaticObjectDoorOperation>(allowIntegerValues: false) }
        };

        readonly IReadOnlyDictionary<int, StaticObjectStateMachine> _machines;

        public static StaticObjectStateMachineCatalog Empty { get; } = new([], []);

        public StaticObjectStateMachineCatalog(
            IEnumerable<StaticObjectStateMachine> machines,
            IEnumerable<StaticObjectSignalPolicy> signalPolicies)
        {
            ArgumentNullException.ThrowIfNull(machines);
            ArgumentNullException.ThrowIfNull(signalPolicies);
            var byId = new Dictionary<int, StaticObjectStateMachine>();
            foreach (StaticObjectStateMachine machine in machines)
            {
                if (machine.Id <= 0 || !machine.States.ContainsKey(machine.InitialState)
                    || string.IsNullOrWhiteSpace(machine.InitialStateCompatibility))
                    throw new InvalidDataException("A state machine needs a positive resource id, an existing initial state and its binding classification.");
                if (!byId.TryAdd(machine.Id, machine))
                    throw new InvalidDataException($"Duplicate state-machine resource {machine.Id}.");
                foreach (KeyValuePair<int, StaticObjectState> state in machine.States)
                {
                    if (state.Key < 0)
                        throw new InvalidDataException($"State machine {machine.Id} has a negative state id.");
                    foreach (StaticObjectTransition transition in state.Value.Transitions)
                    {
                        if (!machine.States.ContainsKey(transition.TargetState))
                            throw new InvalidDataException($"State machine {machine.Id}, state {state.Key}, targets missing state {transition.TargetState}.");
                    }
                }
            }

            var policies = new Dictionary<int, StaticObjectSignalPolicy>();
            foreach (StaticObjectSignalPolicy policy in signalPolicies)
            {
                if (policy.SignalMask == 0 || string.IsNullOrWhiteSpace(policy.Compatibility)
                    || (policy.DoorOperations.Count == 0 && policy.UseEnabled == null))
                    throw new InvalidDataException("A signal policy needs a nonzero mask, operations and a compatibility classification.");
                if (!policies.TryAdd(policy.SignalMask, policy))
                    throw new InvalidDataException($"Duplicate signal policy {policy.SignalMask}.");
            }

            _machines = new ReadOnlyDictionary<int, StaticObjectStateMachine>(byId);
            SignalPolicies = new ReadOnlyDictionary<int, StaticObjectSignalPolicy>(policies);
        }

        public int Count => _machines.Count;

        public IReadOnlyDictionary<int, StaticObjectSignalPolicy> SignalPolicies { get; }

        public bool TryGet(int resourceId, out StaticObjectStateMachine machine)
            => _machines.TryGetValue(resourceId, out machine!);

        public static StaticObjectStateMachineCatalog Load(string root, IZoneLogger? logger = null)
        {
            string path = GameDataPaths.ResolveFile(root, FileName);
            if (!File.Exists(path))
            {
                logger?.Warn($"{FileName} not found at {path}; authored object state machines and compatibility signal policies are unavailable.");
                return Empty;
            }

            try
            {
                CatalogFile file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException("The catalog is null.");
                if (file.SchemaVersion != 1 || file.Machines == null || file.SignalPolicies == null)
                    throw new InvalidDataException("Expected schemaVersion 1, machines and signalPolicies.");

                var machines = new List<StaticObjectStateMachine>();
                foreach (MachineRow row in file.Machines)
                {
                    var states = new Dictionary<int, StaticObjectState>();
                    foreach (StateRow state in row.States ?? throw new InvalidDataException($"State machine {row.Id} has no states."))
                    {
                        var spells = new List<ItemSpell>();
                        foreach (SpellRow spell in state.EntrySpells ?? throw new InvalidDataException("EntrySpells is null."))
                            spells.Add(ReadSpell(spell));
                        var timers = new List<StaticObjectSignalTimer>();
                        foreach (TimerRow timer in state.SignalTimers ?? throw new InvalidDataException("SignalTimers is null."))
                            timers.Add(new(timer.SignalMask, timer.OddPulseCount, ReadConditions(timer.Criteria)));
                        var transitions = new List<StaticObjectTransition>();
                        foreach (TransitionRow transition in state.Transitions ?? throw new InvalidDataException("Transitions is null."))
                            transitions.Add(new(transition.TargetState, ReadConditions(transition.Criteria)));
                        if (!states.TryAdd(state.Id, new(spells.AsReadOnly(), timers.AsReadOnly(), transitions.AsReadOnly())))
                            throw new InvalidDataException($"State machine {row.Id} repeats state {state.Id}.");
                    }
                    machines.Add(new(row.Id, row.InitialState, new ReadOnlyDictionary<int, StaticObjectState>(states), row.InitialStateCompatibility, row.SerializeQueuedSignals));
                }

                var policies = new List<StaticObjectSignalPolicy>();
                foreach (PolicyRow row in file.SignalPolicies)
                    policies.Add(new(row.SignalMask, (row.DoorOperations ?? throw new InvalidDataException("DoorOperations is null.")).AsReadOnly(), row.UseEnabled, row.Compatibility));
                var catalog = new StaticObjectStateMachineCatalog(machines, policies);
                logger?.Info($"GameData object state machines={catalog.Count}, signal policies={catalog.SignalPolicies.Count} from {path}");
                return catalog;
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or ArgumentException)
            {
                throw new InvalidDataException($"Invalid object state-machine catalog {path}: {exception.Message}", exception);
            }
        }

        static IReadOnlyList<StaticObjectCondition> ReadConditions(List<StaticObjectCondition>? conditions)
            => (conditions ?? throw new InvalidDataException("Criteria is null.")).AsReadOnly();

        static ItemSpell ReadSpell(SpellRow row)
        {
            if (row.FunctionType <= 0 || row.TickInterval == int.MinValue)
                throw new InvalidDataException("An entry spell has an invalid function or interval.");
            var spell = new ItemSpell
            {
                FunctionType = row.FunctionType,
                Target = row.Target,
                TickCount = row.TickCount,
                TickInterval = unchecked((uint)row.TickInterval),
                Metadata35 = row.Metadata35,
                Requirements = row.Requirements ?? throw new InvalidDataException("Spell requirements are null.")
            };
            foreach (JsonElement argument in row.Arguments ?? throw new InvalidDataException("Spell arguments are null."))
            {
                if (argument.ValueKind == JsonValueKind.Number && argument.TryGetInt32(out int number))
                    spell.Arguments.Add(number);
                else if (argument.ValueKind == JsonValueKind.String)
                    spell.Arguments.Add(argument.GetString()!);
                else
                    throw new InvalidDataException("Entry spell arguments must be signed int32 values or strings.");
            }
            return spell;
        }

        sealed class CatalogFile
        {
            public int SchemaVersion { get; set; }
            public List<MachineRow>? Machines { get; set; }
            public List<PolicyRow>? SignalPolicies { get; set; }
        }

        sealed class MachineRow
        {
            public int Id { get; set; }
            public int InitialState { get; set; }
            public string InitialStateCompatibility { get; set; } = string.Empty;
            public bool SerializeQueuedSignals { get; set; }
            public List<StateRow>? States { get; set; }
        }

        sealed class StateRow
        {
            public int Id { get; set; }
            public List<SpellRow>? EntrySpells { get; set; }
            public List<TimerRow>? SignalTimers { get; set; }
            public List<TransitionRow>? Transitions { get; set; }
        }

        sealed class SpellRow
        {
            public int FunctionType { get; set; }
            public int Target { get; set; }
            public int TickCount { get; set; }
            public int TickInterval { get; set; }
            public int Metadata35 { get; set; } = -1;
            public List<JsonElement>? Arguments { get; set; }
            public List<ItemRequirement>? Requirements { get; set; }
        }

        sealed class TimerRow
        {
            public int SignalMask { get; set; }
            public int OddPulseCount { get; set; }
            public List<StaticObjectCondition>? Criteria { get; set; }
        }

        sealed class TransitionRow
        {
            public int TargetState { get; set; }
            public List<StaticObjectCondition>? Criteria { get; set; }
        }

        sealed class PolicyRow
        {
            public int SignalMask { get; set; }
            public List<StaticObjectDoorOperation>? DoorOperations { get; set; }
            public bool? UseEnabled { get; set; }
            public string Compatibility { get; set; } = string.Empty;
        }
    }
}
