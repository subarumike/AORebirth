namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Owns materialized ACG interiors and all instance-scoped runtime registries.
    /// </summary>
    internal static class MissionAcgRuntimeManager
    {
        private static readonly object Sync = new object();

        private static readonly MissionAcgRuntimeRegistry Registry =
            new MissionAcgRuntimeRegistry();

        private static readonly Dictionary<int, HashSet<int>> SentPlayfieldsByCharacter =
            new Dictionary<int, HashSet<int>>();

        private static MissionAcgLayoutCatalog catalog;

        private static MissionAcgRuntimeStateStore store;

        private static bool initialized;

        internal static void Initialize(
            IList<MissionAcgBindingRecord> bindings,
            MissionAcgLayoutCatalog layoutCatalog,
            string missionStateDirectory)
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                if (bindings == null || layoutCatalog == null)
                {
                    throw new ArgumentNullException(
                        bindings == null ? "bindings" : "layoutCatalog");
                }

                catalog = layoutCatalog;
                store = new MissionAcgRuntimeStateStore(missionStateDirectory);
                for (int i = 0; i < bindings.Count; i++)
                {
                    MissionAcgBindingRecord record = bindings[i];
                    if (ShouldMaterialize(record))
                    {
                        MissionAcgMaterializedInstance ignored;
                        string failure;
                        if (!TryMaterializeLocked(record, out ignored, out failure))
                        {
                            throw new InvalidOperationException(
                                "Mission ACG runtime restoration failed for accepted quest "
                                + record.Binding.AcceptedQuestIdentity
                                + ": "
                                + failure);
                        }
                    }
                    else if (ShouldCleanupRuntime(record))
                    {
                        string cleanupFailure;
                        if (!store.TryDelete(
                            record.Binding.AcceptedQuestIdentity,
                            out cleanupFailure))
                        {
                            throw new InvalidOperationException(
                                "Mission ACG stale runtime cleanup failed for accepted quest "
                                + record.Binding.AcceptedQuestIdentity
                                + ": "
                                + cleanupFailure);
                        }
                    }
                }

                initialized = true;
            }
        }

        internal static bool TryGetOrMaterialize(
            MissionAcgBindingRecord record,
            out MissionAcgMaterializedInstance instance,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return TryMaterializeLocked(record, out instance, out failure);
            }
        }

        internal static bool TryResolveByPlayfield(
            int allocatedLivePlayfield2,
            out MissionAcgMaterializedInstance instance)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return Registry.TryGetByPlayfield(
                    allocatedLivePlayfield2,
                    out instance);
            }
        }

        internal static bool TryResolveObject(
            int ownerInstance,
            int allocatedLivePlayfield2,
            Identity runtimeIdentity,
            out MissionAcgMaterializedInstance instance,
            out MissionAcgRuntimeObject runtimeObject)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return Registry.TryResolveObject(
                    ownerInstance,
                    allocatedLivePlayfield2,
                    (int)runtimeIdentity.Type,
                    runtimeIdentity.Instance,
                    DateTime.UtcNow,
                    out instance,
                    out runtimeObject);
            }
        }

        internal static bool IsRuntimeIdentityCandidate(
            int allocatedLivePlayfield2,
            Identity runtimeIdentity)
        {
            EnsureInitialized();
            if (runtimeIdentity == null)
            {
                return false;
            }

            int encodedPlayfield;
            int ordinal;
            if (!MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                runtimeIdentity.Instance,
                out encodedPlayfield,
                out ordinal)
                || encodedPlayfield != allocatedLivePlayfield2)
            {
                return false;
            }

            lock (Sync)
            {
                MissionAcgMaterializedInstance instance;
                if (!Registry.TryGetByPlayfield(
                    allocatedLivePlayfield2,
                    out instance))
                {
                    return false;
                }

                for (int i = 0; i < instance.State.IdentityEntries.Count; i++)
                {
                    if (instance.State.IdentityEntries[i].RuntimeIdentity.Type
                        == (int)runtimeIdentity.Type)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static bool TryToggleDoor(
            MissionAcgMaterializedInstance instance,
            int runtimeInstance,
            out bool isOpen,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                isOpen = false;
                failure = string.Empty;
                if (!IsRegistered(instance))
                {
                    failure = "Mission instance is not active.";
                    return false;
                }

                MissionAcgRuntimeDoorState state;
                if (!instance.State.TryGetDoor(runtimeInstance, out state)
                    || state.IsLocked)
                {
                    failure = state == null ? "Door is not registered." : "Door is locked.";
                    return false;
                }

                state.Toggle();
                instance.State.Touch(DateTime.UtcNow);
                if (!store.TryWrite(instance.State, true, out failure))
                {
                    state.Toggle();
                    return false;
                }

                isOpen = state.IsOpen;
                return true;
            }
        }

        internal static bool TryOpenChest(
            MissionAcgMaterializedInstance instance,
            int runtimeInstance,
            out bool wasAlreadyOpen,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                wasAlreadyOpen = false;
                failure = string.Empty;
                if (!IsRegistered(instance))
                {
                    failure = "Mission instance is not active.";
                    return false;
                }

                MissionAcgRuntimeChestState state;
                if (!instance.State.TryGetChest(runtimeInstance, out state))
                {
                    failure = "Chest is not registered.";
                    return false;
                }

                wasAlreadyOpen = state.IsOpen;
                if (wasAlreadyOpen)
                {
                    return true;
                }

                state.Open();
                instance.State.Touch(DateTime.UtcNow);
                if (!store.TryWrite(instance.State, true, out failure))
                {
                    state.SetOpen(false);
                    failure =
                        "Chest state persistence failed after opening; instance is fail-closed: "
                        + failure;
                    return false;
                }

                return true;
            }
        }

        internal static void ClearSent(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Sync)
            {
                SentPlayfieldsByCharacter.Remove(character.Identity.Instance);
            }
        }

        internal static bool SendForCharacter(IZoneClient client, ICharacter character)
        {
            EnsureInitialized();
            if (client == null
                || character == null
                || character.Playfield == null)
            {
                return false;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return false;
            }

            MissionAcgMaterializedInstance instance;
            lock (Sync)
            {
                if (!Registry.TryGetByPlayfield(
                    character.Playfield.Identity.Instance,
                    out instance)
                    || instance.BindingRecord.Binding.OwnerIdentity.Instance
                       != character.Identity.Instance)
                {
                    return false;
                }

                HashSet<int> sent;
                if (!SentPlayfieldsByCharacter.TryGetValue(
                    character.Identity.Instance,
                    out sent))
                {
                    sent = new HashSet<int>();
                    SentPlayfieldsByCharacter.Add(character.Identity.Instance, sent);
                }

                if (!sent.Add(instance.BindingRecord.Binding.AllocatedLivePlayfield2))
                {
                    return true;
                }
            }

            int packetCount = 0;
            for (int i = 0; i < instance.Objects.Count; i++)
            {
                MissionAcgRuntimeObject runtimeObject = instance.Objects[i];
                if (!runtimeObject.HasPacket)
                {
                    continue;
                }

                zoneClient.SendCompressed(runtimeObject.CopyPacket());
                packetCount++;
            }

            MissionDiagnostics.Log(
                "ACG-MATERIALIZE-SEND char={0} accepted={1}:{2} bundle={3} building={4}:{5} livePf2={6} objects={7} packets={8}",
                character.Identity.Instance,
                instance.BindingRecord.Binding.AcceptedQuestIdentity.Type,
                instance.BindingRecord.Binding.AcceptedQuestIdentity.Instance,
                instance.Bundle.LayoutId,
                instance.Bundle.BuildingIdentity.Type,
                instance.Bundle.BuildingIdentity.Instance,
                instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                instance.Objects.Count,
                packetCount);
            return true;
        }

        internal static bool Cleanup(
            MissionAcgBindingRecord record,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                MissionAcgMaterializedInstance instance;
                if (Registry.TryGetByAcceptedQuest(
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out instance))
                {
                    Registry.Remove(
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.Binding.AllocatedLivePlayfield2);
                }

                foreach (HashSet<int> sent in SentPlayfieldsByCharacter.Values)
                {
                    sent.Remove(record.Binding.AllocatedLivePlayfield2);
                }

                return store.TryDelete(
                    record.Binding.AcceptedQuestIdentity,
                    out failure);
            }
        }

        internal static void OnBindingStateChanged(MissionAcgBindingRecord record)
        {
            if (!initialized || record == null)
            {
                return;
            }

            lock (Sync)
            {
                MissionAcgMaterializedInstance instance;
                if (Registry.TryGetByAcceptedQuest(
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out instance))
                {
                    instance.UpdateBindingRecord(record);
                }
            }

            if (record.State.LifecycleState == MissionAcgLifecycleState.Abandoned
                || record.State.LifecycleState == MissionAcgLifecycleState.Expired
                || record.State.LifecycleState == MissionAcgLifecycleState.CleanupPending
                || record.State.LifecycleState == MissionAcgLifecycleState.Cleaned
                || record.State.LifecycleState == MissionAcgLifecycleState.Invalid)
            {
                string failure;
                if (!Cleanup(record, out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-RUNTIME-CLEANUP-FAIL accepted={0}:{1} livePf2={2} reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.Binding.AllocatedLivePlayfield2,
                        failure);
                }
            }
        }

        private static bool TryMaterializeLocked(
            MissionAcgBindingRecord record,
            out MissionAcgMaterializedInstance instance,
            out string failure)
        {
            instance = null;
            failure = string.Empty;
            if (record == null || !ShouldMaterialize(record))
            {
                failure = "Binding lifecycle does not allow runtime materialization.";
                return false;
            }

            if (Registry.TryGetByAcceptedQuest(
                record.Binding.AcceptedQuestIdentity.Instance,
                out instance))
            {
                return true;
            }

            MissionAcgLayoutBundle bundle =
                catalog.FindByLayoutId(record.Binding.SelectedBundleId);
            if (bundle == null)
            {
                failure = "Binding references a missing layout bundle.";
                return false;
            }

            MissionAcgRuntimeState restored;
            bool exists;
            if (!store.TryLoad(
                record.Binding,
                bundle,
                out restored,
                out exists,
                out failure))
            {
                return false;
            }

            if (!MissionAcgRuntimeMaterializer.TryMaterialize(
                record,
                bundle,
                restored,
                DateTime.UtcNow,
                out instance,
                out failure))
            {
                return false;
            }

            if (!exists && !store.TryWrite(instance.State, false, out failure))
            {
                instance = null;
                return false;
            }

            if (!Registry.TryAdd(instance, out failure))
            {
                instance = null;
                return false;
            }

            return true;
        }

        private static bool ShouldMaterialize(MissionAcgBindingRecord record)
        {
            return record != null
                   && (record.State.LifecycleState
                       == MissionAcgLifecycleState.Accepted
                       || record.State.LifecycleState
                       == MissionAcgLifecycleState.Active)
                   && record.State.CleanupState == MissionAcgCleanupState.None
                   && record.Binding.ExpiryUtc > DateTime.UtcNow;
        }

        private static bool ShouldCleanupRuntime(MissionAcgBindingRecord record)
        {
            return record != null
                   && (record.State.LifecycleState
                       == MissionAcgLifecycleState.Abandoned
                       || record.State.LifecycleState
                       == MissionAcgLifecycleState.Expired
                       || record.State.LifecycleState
                       == MissionAcgLifecycleState.CleanupPending
                       || record.State.LifecycleState
                       == MissionAcgLifecycleState.Cleaned
                       || record.State.LifecycleState
                       == MissionAcgLifecycleState.Invalid);
        }

        private static bool IsRegistered(MissionAcgMaterializedInstance instance)
        {
            if (instance == null)
            {
                return false;
            }

            MissionAcgMaterializedInstance registered;
            return Registry.TryGetByPlayfield(
                instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                out registered)
                   && object.ReferenceEquals(registered, instance);
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                MissionAcgBindingRuntime.Initialize();
            }
        }
    }
}
