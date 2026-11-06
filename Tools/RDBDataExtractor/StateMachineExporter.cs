namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AODB.Common.RDBObjects;
    using AODB.Common.Spells;
    using MsgPack;

    /// <summary>Offline conversion of reviewed type-1000015 snapshots; never opens the client or its database.</summary>
    internal static class StateMachineExporter
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true
        };

        internal static int Export(string profilePath, string gameDataRoot, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(gameDataRoot))
                throw new ArgumentException("--state-machine-profile requires an explicit --gamedata-dir.");
            profilePath = Path.GetFullPath(profilePath);
            Profile profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(profilePath), JsonOptions)
                ?? throw new InvalidDataException("The state-machine profile is null.");
            if (profile.SchemaVersion != 1 || profile.Machines == null || profile.SignalPolicies == null)
                throw new InvalidDataException("The profile needs schemaVersion 1, machines and signalPolicies.");

            string outputPath = Path.Combine(Path.GetFullPath(gameDataRoot), "ObjectStateMachines.json");
            if (string.Equals(profilePath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The private conversion profile must remain outside the runtime output.");
            var machines = new List<object>();
            var ids = new HashSet<int>();
            foreach (MachineSource source in profile.Machines.OrderBy(row => row.Id))
            {
                if (source.Id <= 0 || !ids.Add(source.Id) || string.IsNullOrWhiteSpace(source.InitialStateCompatibility))
                    throw new InvalidDataException("Each machine needs a unique positive id and explicit initial-state compatibility classification.");
                string snapshot = Path.GetFullPath(source.SnapshotPath, Path.GetDirectoryName(profilePath));
                if (string.Equals(snapshot, outputPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A resource snapshot cannot be the runtime output.");
                byte[] bytes = File.ReadAllBytes(snapshot);
                string digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (!string.Equals(digest, source.SnapshotSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"State-machine {source.Id} snapshot does not match its reviewed SHA256.");

                var machine = new StateMachine();
                using (var stream = new MemoryStream(bytes, writable: false))
                using (var reader = new BinaryReader(stream))
                {
                    machine.Deserialize(reader);
                    if (!machine.ReadComplete || stream.Position != stream.Length)
                        throw new InvalidDataException($"State-machine {source.Id} was not completely decoded: {machine.ReadStopReason}.");
                }
                if (!machine.States.ContainsKey(source.InitialState))
                    throw new InvalidDataException($"State-machine {source.Id} has no initial state {source.InitialState}.");

                var states = new List<object>();
                foreach (KeyValuePair<int, StateObject> pair in machine.States)
                {
                    StateObject state = pair.Value;
                    // Sleeping/animation-completion state graphs need their own reviewed executor; never drop that bit.
                    if (state.Flag3C)
                        throw new InvalidDataException($"State-machine {source.Id}, state {pair.Key}, uses unsupported Flag3C.");
                    var timers = new List<object>();
                    foreach (StateEntry entry in state.Entries)
                    {
                        if (entry.Key1 != 54001 || entry.Value2C != 0)
                            throw new InvalidDataException($"State-machine {source.Id} has an unsupported entry program.");
                        foreach (StateEntryRecord record in entry.Records)
                        {
                            if (record.I00 != 0 || record.I04 != 0 || record.I08 != 0 || record.I0C != 0
                                || record.I10 != 0 || record.I14 != 0 || !float.IsFinite(record.F1C)
                                || (double)record.F1C < int.MinValue || (double)record.F1C >= (double)int.MaxValue + 1)
                                throw new InvalidDataException($"State-machine {source.Id} has an unsupported signal timer record.");
                            timers.Add(new
                            {
                                SignalMask = record.I18,
                                OddPulseCount = (int)record.F1C,
                                Criteria = ReadConditions(entry.Criteria)
                            });
                        }
                    }
                    states.Add(new
                    {
                        Id = pair.Key,
                        EntrySpells = state.Spells.Select(ReadSpell).ToArray(),
                        SignalTimers = timers,
                        Transitions = state.Transitions.Select(transition => new
                        {
                            TargetState = transition.Target,
                            Criteria = ReadConditions(transition.Criteria)
                        }).ToArray()
                    });
                }
                machines.Add(new
                {
                    source.Id,
                    source.InitialState,
                    source.InitialStateCompatibility,
                    source.SerializeQueuedSignals,
                    States = states
                });
                Console.WriteLine($"State-machine {source.Id}: states={machine.States.Count}, snapshotSha256={digest}");
            }

            var masks = new HashSet<int>();
            foreach (SignalPolicy policy in profile.SignalPolicies)
            {
                if (policy.SignalMask == 0 || !masks.Add(policy.SignalMask) || string.IsNullOrWhiteSpace(policy.Compatibility)
                    || policy.DoorOperations == null || (policy.DoorOperations.Count == 0 && policy.UseEnabled == null)
                    || policy.DoorOperations.Any(operation => operation is not ("Unlock" or "Open" or "Close" or "Lock")))
                    throw new InvalidDataException("A signal policy is missing its classification, repeats a mask or uses unsupported operations.");
            }
            string json = JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                Machines = machines,
                SignalPolicies = profile.SignalPolicies
            }, JsonOptions) + "\n";
            byte[] output = new UTF8Encoding(false).GetBytes(json);
            if (File.Exists(outputPath) && !overwrite)
                throw new IOException("ObjectStateMachines.json already exists; use --overwrite only for an approved replacement.");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, output);
            Console.WriteLine($"Exported ObjectStateMachines.json: machines={machines.Count}, signalPolicies={masks.Count}, sha256={Convert.ToHexStringLower(SHA256.HashData(output))}");
            return 0;
        }

        static object[] ReadConditions(List<Criterion> conditions)
            => conditions.Select(condition => (object)new
            {
                condition.Operator,
                condition.Value1,
                condition.Value2
            }).ToArray();

        static object ReadSpell(SpellData spell)
        {
            if (spell.SpecialCriteria.Count != 0 || spell.Expression != null)
                throw new InvalidDataException($"Function {spell.TypeId} has unsupported special criteria or an expression.");
            var function = ItemRdbMapper.ToFunction(spell);
            return new
            {
                function.FunctionType,
                function.Target,
                TickCount = spell.TryGetStat(3, out int count) ? count : function.TickCount,
                TickInterval = unchecked((int)function.TickInterval),
                Metadata35 = spell.TryGetStat(35, out int metadata35) ? metadata35 : -1,
                Arguments = function.Arguments.Values.Select(ReadArgument).ToArray(),
                Requirements = function.Requirements.Select(requirement => new
                {
                    ChildOperator = (int)requirement.ChildOperator,
                    Operator = (int)requirement.Operator,
                    StatNumber = requirement.Statnumber,
                    Target = (int)requirement.Target,
                    requirement.Value
                }).ToArray()
            };
        }

        static object ReadArgument(MessagePackObject value)
        {
            if (value.IsTypeOf<string>() == true)
                return value.AsString().TrimEnd('\0');
            if (value.IsTypeOf<int>() == true)
                return value.AsInt32();
            throw new InvalidDataException("A state-machine entry action has an unsupported compound argument.");
        }

        sealed class Profile
        {
            public int SchemaVersion { get; set; }
            public List<MachineSource> Machines { get; set; }
            public List<SignalPolicy> SignalPolicies { get; set; }
        }

        sealed class MachineSource
        {
            public int Id { get; set; }
            public string SnapshotPath { get; set; }
            public string SnapshotSha256 { get; set; }
            public int InitialState { get; set; }
            public string InitialStateCompatibility { get; set; }
            public bool SerializeQueuedSignals { get; set; }
        }

        sealed class SignalPolicy
        {
            public int SignalMask { get; set; }
            public List<string> DoorOperations { get; set; }
            public bool? UseEnabled { get; set; }
            public string Compatibility { get; set; }
        }
    }
}
