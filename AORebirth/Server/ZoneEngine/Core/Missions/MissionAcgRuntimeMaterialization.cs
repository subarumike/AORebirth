namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    #endregion

    internal enum MissionAcgRuntimeObjectKind
    {
        Door = 1,
        Exit = 2,
        Chest = 3,
        MissionTerminal = 4,
        RepairMachine = 5,
        StaticObjective = 6,
        ObjectiveNpc = 7,
        AmbientNpc = 8
    }

    internal sealed class MissionAcgRuntimeIdentityEntry
    {
        internal MissionAcgRuntimeIdentityEntry(
            MissionAcgIdentityRecord capturedIdentity,
            MissionAcgIdentityRecord runtimeIdentity,
            MissionAcgRuntimeObjectKind kind,
            int slot)
        {
            if (capturedIdentity == null || runtimeIdentity == null)
            {
                throw new ArgumentNullException(
                    capturedIdentity == null ? "capturedIdentity" : "runtimeIdentity");
            }

            this.CapturedIdentity = capturedIdentity;
            this.RuntimeIdentity = runtimeIdentity;
            this.Kind = kind;
            this.Slot = slot;
        }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal MissionAcgIdentityRecord RuntimeIdentity { get; private set; }

        internal MissionAcgRuntimeObjectKind Kind { get; private set; }

        internal int Slot { get; private set; }
    }

    internal sealed class MissionAcgRuntimeObject
    {
        private readonly byte[] packet;

        internal MissionAcgRuntimeObject(
            MissionAcgRuntimeIdentityEntry identity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            string name,
            byte[] packet)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }

            this.Identity = identity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.Name = name ?? string.Empty;
            this.packet = packet == null ? new byte[0] : (byte[])packet.Clone();
        }

        internal MissionAcgRuntimeIdentityEntry Identity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal string Name { get; private set; }

        internal bool HasPacket
        {
            get
            {
                return this.packet.Length != 0;
            }
        }

        internal byte[] CopyPacket()
        {
            return (byte[])this.packet.Clone();
        }
    }

    internal sealed class MissionAcgRuntimeDoorState
    {
        internal MissionAcgRuntimeDoorState(int runtimeInstance, bool isOpen, bool isLocked)
        {
            this.RuntimeInstance = runtimeInstance;
            this.IsOpen = isOpen;
            this.IsLocked = isLocked;
        }

        internal int RuntimeInstance { get; private set; }

        internal bool IsOpen { get; private set; }

        internal bool IsLocked { get; private set; }

        internal void Toggle()
        {
            if (!this.IsLocked)
            {
                this.IsOpen = !this.IsOpen;
            }
        }

        internal void SetLocked(bool isLocked)
        {
            this.IsLocked = isLocked;
            if (isLocked)
            {
                this.IsOpen = false;
            }
        }
    }

    internal sealed class MissionAcgRuntimeChestState
    {
        internal MissionAcgRuntimeChestState(int runtimeInstance, bool isOpen)
        {
            this.RuntimeInstance = runtimeInstance;
            this.IsOpen = isOpen;
        }

        internal int RuntimeInstance { get; private set; }

        internal bool IsOpen { get; private set; }

        internal void Open()
        {
            this.IsOpen = true;
        }

        internal void SetOpen(bool isOpen)
        {
            this.IsOpen = isOpen;
        }
    }

    /// <summary>
    /// Mutable instance state persisted separately from immutable capture evidence and binding identity.
    /// </summary>
    internal sealed class MissionAcgRuntimeState
    {
        internal const int CurrentFormatVersion = 1;

        private readonly Dictionary<int, MissionAcgRuntimeDoorState> doors;

        private readonly Dictionary<int, MissionAcgRuntimeChestState> chests;

        internal MissionAcgRuntimeState(
            int formatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            string bundleId,
            string bundlePayloadSha256,
            MissionAcgIdentityRecord buildingIdentity,
            int allocatedLivePlayfield2,
            IEnumerable<MissionAcgRuntimeIdentityEntry> identityEntries,
            IEnumerable<MissionAcgRuntimeDoorState> doorStates,
            IEnumerable<MissionAcgRuntimeChestState> chestStates,
            DateTime lastUpdatedUtc)
        {
            if (formatVersion != CurrentFormatVersion)
            {
                throw new ArgumentOutOfRangeException("formatVersion");
            }

            if (acceptedQuestIdentity == null || buildingIdentity == null)
            {
                throw new ArgumentNullException(
                    acceptedQuestIdentity == null
                        ? "acceptedQuestIdentity"
                        : "buildingIdentity");
            }

            if (string.IsNullOrWhiteSpace(bundleId)
                || string.IsNullOrWhiteSpace(bundlePayloadSha256)
                || allocatedLivePlayfield2 <= 0
                || lastUpdatedUtc == DateTime.MinValue)
            {
                throw new ArgumentException("Runtime state identity is incomplete.");
            }

            var identities = new List<MissionAcgRuntimeIdentityEntry>();
            var captured = new HashSet<string>(StringComparer.Ordinal);
            var runtime = new HashSet<string>(StringComparer.Ordinal);
            foreach (MissionAcgRuntimeIdentityEntry entry in identityEntries)
            {
                if (entry == null
                    || !captured.Add(IdentityKey(entry.CapturedIdentity))
                    || !runtime.Add(IdentityKey(entry.RuntimeIdentity)))
                {
                    throw new ArgumentException(
                        "Runtime identity map contains null or duplicate identities.",
                        "identityEntries");
                }

                identities.Add(entry);
            }

            this.doors = new Dictionary<int, MissionAcgRuntimeDoorState>();
            foreach (MissionAcgRuntimeDoorState door in doorStates)
            {
                if (door == null || this.doors.ContainsKey(door.RuntimeInstance))
                {
                    throw new ArgumentException("Door state contains duplicates.", "doorStates");
                }

                this.doors.Add(door.RuntimeInstance, door);
            }

            this.chests = new Dictionary<int, MissionAcgRuntimeChestState>();
            foreach (MissionAcgRuntimeChestState chest in chestStates)
            {
                if (chest == null || this.chests.ContainsKey(chest.RuntimeInstance))
                {
                    throw new ArgumentException("Chest state contains duplicates.", "chestStates");
                }

                this.chests.Add(chest.RuntimeInstance, chest);
            }

            this.FormatVersion = formatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.BundleId = bundleId.Trim();
            this.BundlePayloadSha256 = bundlePayloadSha256.Trim().ToLowerInvariant();
            this.BuildingIdentity = buildingIdentity;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.IdentityEntries = identities.AsReadOnly();
            this.LastUpdatedUtc =
                lastUpdatedUtc.Kind == DateTimeKind.Utc
                    ? lastUpdatedUtc
                    : lastUpdatedUtc.ToUniversalTime();
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal string BundleId { get; private set; }

        internal string BundlePayloadSha256 { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal ReadOnlyCollection<MissionAcgRuntimeIdentityEntry> IdentityEntries
        {
            get;
            private set;
        }

        internal DateTime LastUpdatedUtc { get; private set; }

        internal IEnumerable<MissionAcgRuntimeDoorState> DoorStates
        {
            get
            {
                return this.doors.Values;
            }
        }

        internal IEnumerable<MissionAcgRuntimeChestState> ChestStates
        {
            get
            {
                return this.chests.Values;
            }
        }

        internal bool TryGetDoor(int runtimeInstance, out MissionAcgRuntimeDoorState state)
        {
            return this.doors.TryGetValue(runtimeInstance, out state);
        }

        internal bool TryGetChest(int runtimeInstance, out MissionAcgRuntimeChestState state)
        {
            return this.chests.TryGetValue(runtimeInstance, out state);
        }

        internal void Touch(DateTime nowUtc)
        {
            this.LastUpdatedUtc =
                nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }

    internal sealed class MissionAcgMaterializedInstance
    {
        internal MissionAcgMaterializedInstance(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgLayoutBundle bundle,
            MissionAcgRuntimeState state,
            IEnumerable<MissionAcgRuntimeObject> objects)
        {
            this.BindingRecord = bindingRecord;
            this.Bundle = bundle;
            this.State = state;
            this.Objects =
                new List<MissionAcgRuntimeObject>(objects).AsReadOnly();
        }

        internal MissionAcgBindingRecord BindingRecord { get; private set; }

        internal MissionAcgLayoutBundle Bundle { get; private set; }

        internal MissionAcgRuntimeState State { get; private set; }

        internal ReadOnlyCollection<MissionAcgRuntimeObject> Objects { get; private set; }

        internal void UpdateBindingRecord(MissionAcgBindingRecord record)
        {
            if (record == null
                || !record.Binding.AcceptedQuestIdentity.Equals(
                    this.BindingRecord.Binding.AcceptedQuestIdentity)
                || record.Binding.AllocatedLivePlayfield2
                   != this.BindingRecord.Binding.AllocatedLivePlayfield2
                || !string.Equals(
                    record.Binding.SelectedBundleId,
                    this.BindingRecord.Binding.SelectedBundleId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Cannot replace a materialized instance with another binding.");
            }

            this.BindingRecord = record;
        }

        internal MissionAcgPointRecord Spawn
        {
            get
            {
                return this.Bundle.EntryPoint;
            }
        }

        internal MissionAcgExitRecord Exit
        {
            get
            {
                return this.Bundle.Exit;
            }
        }
    }

    /// <summary>
    /// Instance-scoped registry. Every lookup includes owner, allocated PF2, and runtime identity.
    /// </summary>
    internal sealed class MissionAcgRuntimeRegistry
    {
        private readonly Dictionary<int, MissionAcgMaterializedInstance> byLivePlayfield =
            new Dictionary<int, MissionAcgMaterializedInstance>();

        private readonly Dictionary<int, MissionAcgMaterializedInstance> byAcceptedQuest =
            new Dictionary<int, MissionAcgMaterializedInstance>();

        internal bool TryAdd(
            MissionAcgMaterializedInstance instance,
            out string failure)
        {
            failure = string.Empty;
            if (instance == null)
            {
                failure = "Materialized instance is required.";
                return false;
            }

            int playfield = instance.BindingRecord.Binding.AllocatedLivePlayfield2;
            int accepted = instance.BindingRecord.Binding.AcceptedQuestIdentity.Instance;
            if (this.byLivePlayfield.ContainsKey(playfield)
                || this.byAcceptedQuest.ContainsKey(accepted))
            {
                failure = "Duplicate runtime PF2 or accepted mission ownership.";
                return false;
            }

            this.byLivePlayfield.Add(playfield, instance);
            this.byAcceptedQuest.Add(accepted, instance);
            return true;
        }

        internal bool TryGetByPlayfield(
            int allocatedLivePlayfield2,
            out MissionAcgMaterializedInstance instance)
        {
            return this.byLivePlayfield.TryGetValue(
                allocatedLivePlayfield2,
                out instance);
        }

        internal bool TryGetByAcceptedQuest(
            int acceptedQuestInstance,
            out MissionAcgMaterializedInstance instance)
        {
            return this.byAcceptedQuest.TryGetValue(
                acceptedQuestInstance,
                out instance);
        }

        internal bool TryResolveObject(
            int ownerInstance,
            int allocatedLivePlayfield2,
            int runtimeType,
            int runtimeInstance,
            DateTime nowUtc,
            out MissionAcgMaterializedInstance instance,
            out MissionAcgRuntimeObject runtimeObject)
        {
            runtimeObject = null;
            if (!this.byLivePlayfield.TryGetValue(
                allocatedLivePlayfield2,
                out instance)
                || instance.BindingRecord.Binding.OwnerIdentity.Instance != ownerInstance
                || !instance.BindingRecord.State.CanEnter(
                    nowUtc,
                    instance.BindingRecord.Binding.ExpiryUtc))
            {
                instance = null;
                return false;
            }

            for (int i = 0; i < instance.Objects.Count; i++)
            {
                MissionAcgRuntimeObject candidate = instance.Objects[i];
                if (candidate.Identity.RuntimeIdentity.Type == runtimeType
                    && candidate.Identity.RuntimeIdentity.Instance == runtimeInstance)
                {
                    runtimeObject = candidate;
                    return true;
                }
            }

            instance = null;
            return false;
        }

        internal bool Remove(
            int acceptedQuestInstance,
            int allocatedLivePlayfield2)
        {
            MissionAcgMaterializedInstance accepted;
            MissionAcgMaterializedInstance playfield;
            bool hasAccepted =
                this.byAcceptedQuest.TryGetValue(
                    acceptedQuestInstance,
                    out accepted);
            bool hasPlayfield =
                this.byLivePlayfield.TryGetValue(
                    allocatedLivePlayfield2,
                    out playfield);
            if (hasAccepted != hasPlayfield
                || (hasAccepted && !object.ReferenceEquals(accepted, playfield)))
            {
                throw new InvalidOperationException(
                    "Runtime registry ownership indexes are inconsistent.");
            }

            if (!hasAccepted)
            {
                return false;
            }

            this.byAcceptedQuest.Remove(acceptedQuestInstance);
            this.byLivePlayfield.Remove(allocatedLivePlayfield2);
            return true;
        }
    }

    internal static class MissionAcgRuntimeMaterializer
    {
        private const int RuntimeIdentityBase = unchecked((int)0x60000000);

        private const int BrokenMachineTemplateId = 0x027B47;

        private sealed class IdentitySeed
        {
            internal MissionAcgIdentityRecord Captured;
            internal MissionAcgRuntimeObjectKind Kind;
            internal int Slot;
        }

        internal static bool TryMaterialize(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgLayoutBundle bundle,
            MissionAcgRuntimeState restoredState,
            DateTime nowUtc,
            out MissionAcgMaterializedInstance instance,
            out string failure)
        {
            instance = null;
            failure = string.Empty;
            if (!ValidateAtomicRelationship(bindingRecord, bundle, out failure))
            {
                return false;
            }

            List<IdentitySeed> seeds = CollectIdentitySeeds(bundle);
            if (seeds.Count == 0 || seeds.Count > 255)
            {
                failure = "Captured layout has an unsupported runtime identity count.";
                return false;
            }

            List<MissionAcgRuntimeIdentityEntry> identities =
                CreateIdentityMap(bindingRecord.Binding, seeds);
            if (restoredState != null
                && !ValidateRestoredState(
                    bindingRecord.Binding,
                    bundle,
                    restoredState,
                    identities,
                    out failure))
            {
                return false;
            }

            MissionAcgRuntimeState state =
                restoredState
                ?? CreateInitialState(bindingRecord.Binding, bundle, identities, nowUtc);
            var byCaptured = new Dictionary<string, MissionAcgRuntimeIdentityEntry>(
                StringComparer.Ordinal);
            for (int i = 0; i < identities.Count; i++)
            {
                byCaptured.Add(IdentityKey(identities[i].CapturedIdentity), identities[i]);
            }

            var objects = new List<MissionAcgRuntimeObject>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bundle.Dynels.Count; i++)
            {
                MissionAcgDynelRecord dynel = bundle.Dynels[i];
                MissionAcgRuntimeIdentityEntry identity =
                    byCaptured[IdentityKey(dynel.CapturedIdentity)];
                MissionAcgRuntimeObjectKind kind =
                    ResolveDynelKind(bundle, dynel);
                AddObject(
                    objects,
                    added,
                    identity,
                    kind,
                    dynel.Position,
                    dynel.Heading,
                    dynel.TemplateId,
                    dynel.Name,
                    dynel.Wire == null
                        ? new byte[0]
                        : RetargetWire(
                            dynel.Wire,
                            bindingRecord.Binding,
                            byCaptured));
            }

            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                MissionAcgNpcSlotRecord npc = bundle.NpcSlots[i];
                MissionAcgRuntimeIdentityEntry identity =
                    byCaptured[IdentityKey(npc.CapturedIdentity)];
                AddObject(
                    objects,
                    added,
                    identity,
                    identity.Kind,
                    npc.Position,
                    npc.Heading,
                    npc.TemplateId,
                    npc.Name,
                    RetargetOpaqueCapturedPacket(
                        npc.CopyRawPacket(),
                        npc.CapturedIdentity,
                        npc.CapturedPlayfield2,
                        bundle.CapturedPlayerIdentity,
                        bindingRecord.Binding,
                        identity.RuntimeIdentity));
            }

            for (int i = 0; i < bundle.ObjectiveSlots.Count; i++)
            {
                MissionAcgObjectiveSlotRecord objective = bundle.ObjectiveSlots[i];
                MissionAcgRuntimeIdentityEntry identity =
                    byCaptured[IdentityKey(objective.CapturedIdentity)];
                AddObject(
                    objects,
                    added,
                    identity,
                    identity.Kind,
                    objective.Position,
                    objective.Heading,
                    objective.TemplateId,
                    objective.Name,
                    RetargetOpaqueCapturedPacket(
                        objective.CopyRawPacket(),
                        objective.CapturedIdentity,
                        objective.CapturedPlayfield2,
                        bundle.CapturedPlayerIdentity,
                        bindingRecord.Binding,
                        identity.RuntimeIdentity));
            }

            instance = new MissionAcgMaterializedInstance(
                bindingRecord,
                bundle,
                state,
                objects);
            return true;
        }

        internal static bool TryReverseRuntimeInstance(
            int runtimeInstance,
            out int allocatedLivePlayfield2,
            out int localOrdinal)
        {
            allocatedLivePlayfield2 = 0;
            localOrdinal = 0;
            if ((runtimeInstance & unchecked((int)0xF0000000)) != RuntimeIdentityBase)
            {
                return false;
            }

            int encoded = runtimeInstance & 0x0FFFFFFF;
            int playfieldOffset = (encoded >> 8) & 0xFFFF;
            localOrdinal = encoded & 0xFF;
            if (localOrdinal == 0)
            {
                return false;
            }

            allocatedLivePlayfield2 =
                MissionAcgAllocationService.MinimumLivePlayfield2
                + playfieldOffset;
            return allocatedLivePlayfield2
                       >= MissionAcgAllocationService.MinimumLivePlayfield2
                   && allocatedLivePlayfield2
                       <= MissionAcgAllocationService.MaximumLivePlayfield2;
        }

        private static bool ValidateAtomicRelationship(
            MissionAcgBindingRecord record,
            MissionAcgLayoutBundle bundle,
            out string failure)
        {
            failure = string.Empty;
            if (record == null || bundle == null)
            {
                failure = "Binding record and layout bundle are required.";
                return false;
            }

            MissionAcgInstanceBinding binding = record.Binding;
            if (!bundle.IsSelectable
                || !bundle.Completeness.IsSelectionComplete
                || !string.Equals(
                    binding.SelectedBundleId,
                    bundle.LayoutId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    binding.SelectedBundlePayloadSha256,
                    bundle.GeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !binding.AcgBuildingIdentity.Equals(bundle.BuildingIdentity)
                || binding.AllocatedLivePlayfield2
                   == MissionAcgAllocationService.LegacySharedPlayfield2
                || binding.AllocatedLivePlayfield2 == bundle.SourcePlayfield2)
            {
                failure = "Binding and immutable layout bundle do not form one valid atomic instance.";
                return false;
            }

            return true;
        }

        private static List<IdentitySeed> CollectIdentitySeeds(MissionAcgLayoutBundle bundle)
        {
            var values = new Dictionary<string, IdentitySeed>(StringComparer.Ordinal);
            for (int i = 0; i < bundle.Dynels.Count; i++)
            {
                MissionAcgDynelRecord dynel = bundle.Dynels[i];
                AddSeed(
                    values,
                    dynel.CapturedIdentity,
                    ResolveDynelKind(bundle, dynel),
                    dynel.Slot);
            }

            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                MissionAcgNpcSlotRecord npc = bundle.NpcSlots[i];
                AddSeed(
                    values,
                    npc.CapturedIdentity,
                    IsObjective(bundle, npc.CapturedIdentity)
                        ? MissionAcgRuntimeObjectKind.ObjectiveNpc
                        : MissionAcgRuntimeObjectKind.AmbientNpc,
                    npc.Slot);
            }

            for (int i = 0; i < bundle.ObjectiveSlots.Count; i++)
            {
                MissionAcgObjectiveSlotRecord objective = bundle.ObjectiveSlots[i];
                AddSeed(
                    values,
                    objective.CapturedIdentity,
                    objective.TemplateId == BrokenMachineTemplateId
                        ? MissionAcgRuntimeObjectKind.RepairMachine
                        : IsCharacterIdentity(objective.CapturedIdentity)
                        ? MissionAcgRuntimeObjectKind.ObjectiveNpc
                        : MissionAcgRuntimeObjectKind.StaticObjective,
                    objective.Slot);
            }

            if (bundle.Exit != null)
            {
                AddSeed(
                    values,
                    bundle.Exit.CapturedIdentity,
                    MissionAcgRuntimeObjectKind.Exit,
                    0);
            }

            var result = new List<IdentitySeed>(values.Values);
            result.Sort(
                delegate(IdentitySeed left, IdentitySeed right)
                {
                    int type = left.Captured.Type.CompareTo(right.Captured.Type);
                    return type != 0
                               ? type
                               : left.Captured.Instance.CompareTo(right.Captured.Instance);
                });
            return result;
        }

        private static void AddSeed(
            IDictionary<string, IdentitySeed> values,
            MissionAcgIdentityRecord captured,
            MissionAcgRuntimeObjectKind kind,
            int slot)
        {
            if (captured == null)
            {
                throw new InvalidOperationException("Captured runtime object lacks an identity.");
            }

            string key = IdentityKey(captured);
            IdentitySeed existing;
            if (values.TryGetValue(key, out existing))
            {
                if (kind == MissionAcgRuntimeObjectKind.Exit
                    || kind == MissionAcgRuntimeObjectKind.ObjectiveNpc
                    || kind == MissionAcgRuntimeObjectKind.StaticObjective)
                {
                    existing.Kind = kind;
                    existing.Slot = slot;
                }

                return;
            }

            values.Add(
                key,
                new IdentitySeed
                {
                    Captured = captured,
                    Kind = kind,
                    Slot = slot
                });
        }

        private static List<MissionAcgRuntimeIdentityEntry> CreateIdentityMap(
            MissionAcgInstanceBinding binding,
            IList<IdentitySeed> seeds)
        {
            int playfieldOffset =
                binding.AllocatedLivePlayfield2
                - MissionAcgAllocationService.MinimumLivePlayfield2;
            var entries = new List<MissionAcgRuntimeIdentityEntry>(seeds.Count);
            for (int i = 0; i < seeds.Count; i++)
            {
                int ordinal = i + 1;
                int instance =
                    RuntimeIdentityBase
                    | ((playfieldOffset & 0xFFFF) << 8)
                    | ordinal;
                entries.Add(
                    new MissionAcgRuntimeIdentityEntry(
                        seeds[i].Captured,
                        new MissionAcgIdentityRecord(seeds[i].Captured.Type, instance),
                        seeds[i].Kind,
                        seeds[i].Slot));
            }

            return entries;
        }

        private static MissionAcgRuntimeState CreateInitialState(
            MissionAcgInstanceBinding binding,
            MissionAcgLayoutBundle bundle,
            IList<MissionAcgRuntimeIdentityEntry> identities,
            DateTime nowUtc)
        {
            var doors = new List<MissionAcgRuntimeDoorState>();
            var chests = new List<MissionAcgRuntimeChestState>();
            for (int i = 0; i < identities.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry identity = identities[i];
                if (identity.Kind == MissionAcgRuntimeObjectKind.Door
                    || identity.Kind == MissionAcgRuntimeObjectKind.Exit)
                {
                    doors.Add(
                        new MissionAcgRuntimeDoorState(
                            identity.RuntimeIdentity.Instance,
                            false,
                            false));
                }
                else if (identity.Kind == MissionAcgRuntimeObjectKind.Chest)
                {
                    chests.Add(
                        new MissionAcgRuntimeChestState(
                            identity.RuntimeIdentity.Instance,
                            false));
                }
            }

            return new MissionAcgRuntimeState(
                MissionAcgRuntimeState.CurrentFormatVersion,
                binding.AcceptedQuestIdentity,
                bundle.LayoutId,
                bundle.GeneratorPayloadSha256,
                bundle.BuildingIdentity,
                binding.AllocatedLivePlayfield2,
                identities,
                doors,
                chests,
                nowUtc);
        }

        private static bool ValidateRestoredState(
            MissionAcgInstanceBinding binding,
            MissionAcgLayoutBundle bundle,
            MissionAcgRuntimeState state,
            IList<MissionAcgRuntimeIdentityEntry> expected,
            out string failure)
        {
            failure = string.Empty;
            if (!state.AcceptedQuestIdentity.Equals(binding.AcceptedQuestIdentity)
                || !string.Equals(state.BundleId, bundle.LayoutId, StringComparison.Ordinal)
                || !string.Equals(
                    state.BundlePayloadSha256,
                    bundle.GeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !state.BuildingIdentity.Equals(bundle.BuildingIdentity)
                || state.AllocatedLivePlayfield2 != binding.AllocatedLivePlayfield2
                || state.IdentityEntries.Count != expected.Count)
            {
                failure = "Persisted runtime state does not match its binding and bundle.";
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry actual = state.IdentityEntries[i];
                MissionAcgRuntimeIdentityEntry wanted = expected[i];
                if (!actual.CapturedIdentity.Equals(wanted.CapturedIdentity)
                    || !actual.RuntimeIdentity.Equals(wanted.RuntimeIdentity)
                    || actual.Kind != wanted.Kind
                    || actual.Slot != wanted.Slot)
                {
                    failure = "Persisted runtime identity map is not deterministic.";
                    return false;
                }
            }

            return true;
        }

        private static MissionAcgRuntimeObjectKind ResolveDynelKind(
            MissionAcgLayoutBundle bundle,
            MissionAcgDynelRecord dynel)
        {
            if (bundle.Exit != null
                && bundle.Exit.CapturedIdentity.Equals(dynel.CapturedIdentity))
            {
                return MissionAcgRuntimeObjectKind.Exit;
            }

            if (IsObjective(bundle, dynel.CapturedIdentity))
            {
                return dynel.TemplateId == BrokenMachineTemplateId
                           ? MissionAcgRuntimeObjectKind.RepairMachine
                           : MissionAcgRuntimeObjectKind.StaticObjective;
            }

            if (dynel.TemplateId == BrokenMachineTemplateId)
            {
                return MissionAcgRuntimeObjectKind.RepairMachine;
            }

            switch (dynel.Category)
            {
                case MissionAcgWireCategory.Door:
                    return MissionAcgRuntimeObjectKind.Door;
                case MissionAcgWireCategory.Chest:
                    return MissionAcgRuntimeObjectKind.Chest;
                case MissionAcgWireCategory.Terminal:
                    return MissionAcgRuntimeObjectKind.MissionTerminal;
                default:
                    throw new InvalidOperationException("Unsupported captured dynel category.");
            }
        }

        private static bool IsObjective(
            MissionAcgLayoutBundle bundle,
            MissionAcgIdentityRecord identity)
        {
            for (int i = 0; i < bundle.ObjectiveSlots.Count; i++)
            {
                if (bundle.ObjectiveSlots[i].CapturedIdentity.Equals(identity))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCharacterIdentity(MissionAcgIdentityRecord identity)
        {
            return identity != null && identity.Type == 0xC350;
        }

        private static void AddObject(
            ICollection<MissionAcgRuntimeObject> objects,
            ISet<string> added,
            MissionAcgRuntimeIdentityEntry identity,
            MissionAcgRuntimeObjectKind kind,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            string name,
            byte[] packet)
        {
            string key = IdentityKey(identity.RuntimeIdentity);
            if (!added.Add(key))
            {
                return;
            }

            MissionAcgRuntimeIdentityEntry effective =
                identity.Kind == kind
                    ? identity
                    : new MissionAcgRuntimeIdentityEntry(
                        identity.CapturedIdentity,
                        identity.RuntimeIdentity,
                        kind,
                        identity.Slot);
            objects.Add(
                new MissionAcgRuntimeObject(
                    effective,
                    position,
                    heading,
                    templateId,
                    name,
                    packet));
        }

        private static byte[] RetargetWire(
            MissionAcgWireRecord wire,
            MissionAcgInstanceBinding binding,
            IDictionary<string, MissionAcgRuntimeIdentityEntry> byCaptured)
        {
            byte[] packet = wire.CopyPacketBytes();
            MissionAcgRuntimeIdentityEntry runtime =
                byCaptured[IdentityKey(wire.CapturedIdentity)];
            for (int i = 0; i < wire.RetargetSlots.Count; i++)
            {
                MissionAcgRetargetSlotRecord slot = wire.RetargetSlots[i];
                if (MissionAcgHash.ReadInt32BigEndian(packet, slot.ByteOffset)
                    != slot.CapturedValue)
                {
                    throw new InvalidOperationException(
                        "Captured retarget slot no longer matches packet evidence.");
                }

                int value;
                switch (slot.Category)
                {
                    case MissionAcgRetargetCategory.CharacterInstance:
                        value = binding.OwnerIdentity.Instance;
                        break;
                    case MissionAcgRetargetCategory.Playfield2Instance:
                        value = binding.AllocatedLivePlayfield2;
                        break;
                    case MissionAcgRetargetCategory.DynelIdentityType:
                        value = runtime.RuntimeIdentity.Type;
                        break;
                    case MissionAcgRetargetCategory.DynelIdentityInstance:
                        value = runtime.RuntimeIdentity.Instance;
                        break;
                    case MissionAcgRetargetCategory.ParentIdentityType:
                        value =
                            wire.CapturedParentIdentity == null
                                ? slot.CapturedValue
                                : binding.OwnerIdentity.Type;
                        break;
                    case MissionAcgRetargetCategory.ParentIdentityInstance:
                        value =
                            wire.CapturedParentIdentity == null
                                ? slot.CapturedValue
                                : binding.OwnerIdentity.Instance;
                        break;
                    case MissionAcgRetargetCategory.BuildingIdentityType:
                        value = binding.AcgBuildingIdentity.Type;
                        break;
                    case MissionAcgRetargetCategory.BuildingIdentityInstance:
                        value = binding.AcgBuildingIdentity.Instance;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unsupported captured retarget category.");
                }

                WriteInt32BigEndian(packet, slot.ByteOffset, value);
            }

            return packet;
        }

        private static byte[] RetargetOpaqueCapturedPacket(
            byte[] packet,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedPlayer,
            MissionAcgInstanceBinding binding,
            MissionAcgIdentityRecord runtimeIdentity)
        {
            if (packet == null || packet.Length == 0)
            {
                return new byte[0];
            }

            ReplaceInt32(packet, capturedIdentity.Type, runtimeIdentity.Type);
            ReplaceInt32(packet, capturedIdentity.Instance, runtimeIdentity.Instance);
            if (capturedPlayfield2.HasValue)
            {
                ReplaceInt32(
                    packet,
                    capturedPlayfield2.Value,
                    binding.AllocatedLivePlayfield2);
            }

            if (capturedPlayer != null)
            {
                ReplaceInt32(
                    packet,
                    capturedPlayer.Instance,
                    binding.OwnerIdentity.Instance);
            }

            return packet;
        }

        private static void ReplaceInt32(byte[] packet, int from, int to)
        {
            if (from == to)
            {
                return;
            }

            byte b0 = (byte)(from >> 24);
            byte b1 = (byte)(from >> 16);
            byte b2 = (byte)(from >> 8);
            byte b3 = (byte)from;
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == b0
                    && packet[i + 1] == b1
                    && packet[i + 2] == b2
                    && packet[i + 3] == b3)
                {
                    WriteInt32BigEndian(packet, i, to);
                    i += 3;
                }
            }
        }

        private static void WriteInt32BigEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }
}
