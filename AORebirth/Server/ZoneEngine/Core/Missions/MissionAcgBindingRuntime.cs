namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;

    #endregion

    internal static class MissionAcgBindingRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionAcgBindingRecord> ByAcceptedInstance =
            new Dictionary<int, MissionAcgBindingRecord>();

        private static readonly Dictionary<int, MissionAcgBindingRecord> ByKeyInstance =
            new Dictionary<int, MissionAcgBindingRecord>();

        private static readonly Dictionary<int, MissionAcgBindingRecord> ByLivePlayfield =
            new Dictionary<int, MissionAcgBindingRecord>();

        private static MissionAcgLayoutCatalog catalog;

        private static MissionAcgBindingStore store;

        private static MissionAcgAllocationService allocator;

        private static bool initialized;

        private static bool expiryRestorationComplete;

        internal static MissionAcgLayoutCatalog Catalog
        {
            get
            {
                EnsureInitialized();
                return catalog;
            }
        }

        internal static MissionAcgAllocationService Allocator
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                {
                    if (!expiryRestorationComplete)
                    {
                        throw new InvalidOperationException(
                            "Mission ACG allocation is unavailable until "
                            + "durable expiry restoration completes.");
                    }

                    return allocator;
                }
            }
        }

        internal static MissionAcgAllocationService AllocatorDuringExpiryRecovery
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                {
                    return allocator;
                }
            }
        }

        internal static void Initialize()
        {
            IList<MissionAcgBindingRecord> restoredForExpiry = null;
            string restoredMissionStateDirectory = string.Empty;
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
                string missionStateDirectory =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory ?? ".",
                        "mission-state");
                store = new MissionAcgBindingStore(missionStateDirectory, catalog);
                MissionAcgBindingLoadResult loaded = store.LoadAll();
                if (!loaded.IsValid)
                {
                    throw new InvalidOperationException(
                        "Mission ACG binding restoration failed closed: "
                        + string.Join(" | ", loaded.Diagnostics));
                }

                allocator = new MissionAcgAllocationService(catalog);
                string allocationFailure;
                if (!allocator.TryRestore(loaded.Records, out allocationFailure))
                {
                    throw new InvalidOperationException(
                        "Mission ACG allocation restoration failed closed: "
                        + allocationFailure);
                }

                for (int i = 0; i < loaded.Records.Count; i++)
                {
                    AddIndexes(loaded.Records[i]);
                }

                IList<MissionAcgBindingRecord> restored =
                    new List<MissionAcgBindingRecord>(
                        ByAcceptedInstance.Values).AsReadOnly();
                MissionAcgRuntimeManager.Initialize(
                    restored,
                    catalog,
                    missionStateDirectory);
                MissionAcgObjectiveRuntime.Initialize(
                    restored,
                    catalog,
                    missionStateDirectory);
                MissionAcgOperationalRuntime.Initialize(
                    restored,
                    catalog,
                    missionStateDirectory);
                MissionAcgSpatialRuntime.Initialize(
                    restored,
                    catalog,
                    missionStateDirectory);
                initialized = true;
                restoredForExpiry =
                    new List<MissionAcgBindingRecord>(
                        ByAcceptedInstance.Values).AsReadOnly();
                restoredMissionStateDirectory = missionStateDirectory;
            }

            if (restoredForExpiry != null)
            {
                MissionAcgExpiryRuntime.Initialize(
                    restoredForExpiry,
                    restoredMissionStateDirectory);
                lock (Sync)
                {
                    expiryRestorationComplete = true;
                }
            }
        }

        internal static bool TryPersistNew(
            MissionAcgBindingRecord record,
            out MissionAcgBindingRecord persisted,
            out string failure)
        {
            EnsureInitialized();
            bool created = false;
            lock (Sync)
            {
                persisted = null;
                failure = string.Empty;
                if (!expiryRestorationComplete)
                {
                    failure =
                        "Durable expiry restoration must complete before "
                        + "accepting a generated mission.";
                    return false;
                }

                if (ByAcceptedInstance.ContainsKey(
                    record.Binding.AcceptedQuestIdentity.Instance))
                {
                    failure = "Duplicate accepted quest identity.";
                    return false;
                }

                if (ByLivePlayfield.ContainsKey(record.Binding.AllocatedLivePlayfield2))
                {
                    failure = "Duplicate active PF2 ownership.";
                    return false;
                }

                if (!store.TryCreate(record, out persisted, out failure))
                {
                    return false;
                }

                AddIndexes(persisted);
                created = true;
            }

            if (created)
            {
                MissionAcgExpiryRuntime.OnBindingCreated(persisted);
            }

            return created;
        }

        internal static bool TryTransition(
            MissionAcgBindingRecord record,
            MissionAcgLifecycleState lifecycle,
            MissionAcgCleanupState cleanup,
            DateTime nowUtc,
            out MissionAcgBindingRecord updated,
            out string failure)
        {
            EnsureInitialized();
            updated = null;
            failure = string.Empty;
            bool verifyCleanup =
                MissionAcgLifecyclePolicy.RequiresVerifiedRuntimeCleanup(
                    lifecycle,
                    cleanup);
            MissionAcgBindingRecord cleanupRecord = record;
            if (verifyCleanup)
            {
                lock (Sync)
                {
                    if (!TryGetCurrentRecord(record, out cleanupRecord, out failure))
                    {
                        return false;
                    }
                }

                if (!TryVerifyRuntimeCleanup(cleanupRecord, out failure))
                {
                    return false;
                }
            }

            lock (Sync)
            {
                MissionAcgBindingRecord current;
                if (!TryGetCurrentRecord(record, out current, out failure))
                {
                    return false;
                }

                record = current;
                MissionAcgInstanceState state;
                try
                {
                    state = record.State.Transition(lifecycle, cleanup, nowUtc);
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    return false;
                }

                if (!store.TryReplace(
                    record.WithState(state),
                    out updated,
                    out failure))
                {
                    return false;
                }

                ReplaceIndexes(updated);
            }

            MissionAcgSpatialRuntime.OnBindingStateChanged(updated);
            MissionAcgOperationalRuntime.OnBindingStateChanged(updated);
            MissionAcgRuntimeManager.OnBindingStateChanged(updated);
            MissionAcgExpiryRuntime.OnBindingStateChanged(updated);
            return true;
        }

        internal static bool TryCompleteRuntimeCleanup(
            MissionAcgBindingRecord record,
            out string failure)
        {
            EnsureInitialized();
            MissionAcgBindingRecord current;
            lock (Sync)
            {
                if (!TryGetCurrentRecord(record, out current, out failure))
                {
                    return false;
                }
            }

            return TryVerifyRuntimeCleanup(current, out failure);
        }

        internal static bool TryReleaseAfterDurableCleanup(
            MissionAcgBindingRecord record,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                MissionAcgBindingRecord current;
                if (!TryGetCurrentRecord(record, out current, out failure))
                {
                    return false;
                }

                if (objective == null
                    || objective.Binding.AcceptedQuestIdentity.Instance
                       != current.Binding.AcceptedQuestIdentity.Instance
                    || objective.Binding.OwnerIdentity.Instance
                       != current.Binding.OwnerIdentity.Instance
                    || objective.Binding.AllocatedLivePlayfield2
                       != current.Binding.AllocatedLivePlayfield2
                    || !MissionAcgLifecyclePolicy.IsCleanupComplete(
                        current.State,
                        objective.State))
                {
                    failure =
                        "PF2 release requires matching durable binding and objective cleanup.";
                    return false;
                }

                if (!MissionAcgExpiryRuntime.IsReleaseReady(current, out failure))
                {
                    return false;
                }

                bool holdForDurableJournalConfirmation =
                    MissionAcgExpiryRuntime.OwnsCleanup(current);
                MissionAcgBindingRecord mapped;
                if (!ByLivePlayfield.TryGetValue(
                    current.Binding.AllocatedLivePlayfield2,
                    out mapped))
                {
                    if (allocator.IsReservedBy(
                        current.Binding.AllocatedLivePlayfield2,
                        current.Binding.AcceptedQuestIdentity))
                    {
                        return ReleaseCurrentPlayfield(
                            current,
                            holdForDurableJournalConfirmation,
                            out failure);
                    }

                    if (allocator.IsReserved(
                        current.Binding.AllocatedLivePlayfield2))
                    {
                        failure =
                            "PF2 allocator reservation is owned by another accepted mission.";
                        return false;
                    }

                    return true;
                }

                if (mapped.Binding.AcceptedQuestIdentity.Instance
                    != current.Binding.AcceptedQuestIdentity.Instance)
                {
                    failure = "PF2 is owned by another accepted mission.";
                    return false;
                }

                return ReleaseCurrentPlayfield(
                    current,
                    holdForDurableJournalConfirmation,
                    out failure);
            }
        }

        internal static bool TryReleaseFailedAcceptanceAfterCleanup(
            MissionAcgBindingRecord record,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                MissionAcgBindingRecord current;
                if (!TryGetCurrentRecord(record, out current, out failure))
                {
                    return false;
                }

                if (current.State.LifecycleState
                    != MissionAcgLifecycleState.Cleaned
                    || current.State.CleanupState
                    != MissionAcgCleanupState.Completed)
                {
                    failure =
                        "Failed acceptance PF2 release requires terminal binding cleanup.";
                    return false;
                }

                MissionAcgObjectiveRecord objective;
                if (MissionAcgObjectiveRuntime.TryGetByAccepted(
                    current.Binding.OwnerIdentity.Instance,
                    current.Binding.AcceptedQuestIdentity.Instance,
                    out objective))
                {
                    failure =
                        "Failed acceptance PF2 release requires the provisional objective to be absent.";
                    return false;
                }

                MissionAcgBindingRecord mapped;
                if (!ByLivePlayfield.TryGetValue(
                    current.Binding.AllocatedLivePlayfield2,
                    out mapped))
                {
                    if (allocator.IsReservedBy(
                        current.Binding.AllocatedLivePlayfield2,
                        current.Binding.AcceptedQuestIdentity))
                    {
                        return ReleaseCurrentPlayfield(
                            current,
                            false,
                            out failure);
                    }

                    if (allocator.IsReserved(
                        current.Binding.AllocatedLivePlayfield2))
                    {
                        failure =
                            "Failed acceptance PF2 is reserved by another accepted mission.";
                        return false;
                    }

                    return true;
                }

                if (mapped.Binding.AcceptedQuestIdentity.Instance
                    != current.Binding.AcceptedQuestIdentity.Instance)
                {
                    failure =
                        "Failed acceptance PF2 is indexed to another accepted mission.";
                    return false;
                }

                return ReleaseCurrentPlayfield(current, false, out failure);
            }
        }

        internal static IList<MissionAcgBindingRecord> GetSnapshot()
        {
            EnsureInitialized();
            lock (Sync)
            {
                return new List<MissionAcgBindingRecord>(
                    ByAcceptedInstance.Values).AsReadOnly();
            }
        }

        internal static bool TryGetByAcceptedQuest(
            int acceptedQuestInstance,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return ByAcceptedInstance.TryGetValue(
                    acceptedQuestInstance,
                    out record);
            }
        }

        internal static bool TryResolveByAcceptedQuest(
            int ownerInstance,
            int acceptedQuestInstance,
            DateTime nowUtc,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return ByAcceptedInstance.TryGetValue(acceptedQuestInstance, out record)
                       && IsAccessible(record, ownerInstance, nowUtc);
            }
        }

        internal static bool TryGetOwnedByAcceptedQuest(
            int ownerInstance,
            int acceptedQuestInstance,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return ByAcceptedInstance.TryGetValue(acceptedQuestInstance, out record)
                       && record.Binding.OwnerIdentity.Instance == ownerInstance;
            }
        }

        internal static bool TryResolveByMissionKey(
            int ownerInstance,
            int missionKeyInstance,
            DateTime nowUtc,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return MissionAcgBindingResolver.TryResolveByKey(
                    ByAcceptedInstance.Values,
                    ownerInstance,
                    missionKeyInstance,
                    nowUtc,
                    out record);
            }
        }

        internal static bool TryResolveByEntrance(
            int ownerInstance,
            MissionAcgIdentityRecord exteriorEntrance,
            int entranceLow,
            int entranceHigh,
            DateTime nowUtc,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                record = null;
                foreach (MissionAcgBindingRecord candidate in ByAcceptedInstance.Values)
                {
                    MissionAcgInstanceBinding binding = candidate.Binding;
                    if (!IsAccessible(candidate, ownerInstance, nowUtc)
                        || !binding.ExteriorEntranceIdentity.Equals(exteriorEntrance)
                        || binding.ExteriorEntranceLow != entranceLow
                        || binding.ExteriorEntranceHigh != entranceHigh)
                    {
                        continue;
                    }

                    if (record != null)
                    {
                        record = null;
                        return false;
                    }

                    record = candidate;
                }

                return record != null;
            }
        }

        internal static bool TryResolveByExteriorMarker(
            int ownerInstance,
            int exteriorPlayfieldInstance,
            double x,
            double y,
            double z,
            double horizontalRadius,
            double verticalRadius,
            DateTime nowUtc,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return MissionAcgBindingResolver.TryResolveByExteriorMarker(
                    ByAcceptedInstance.Values,
                    ownerInstance,
                    exteriorPlayfieldInstance,
                    x,
                    y,
                    z,
                    horizontalRadius,
                    verticalRadius,
                    nowUtc,
                    out record);
            }
        }

        internal static bool TryResolveByLivePlayfield(
            int livePlayfield2,
            out MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return ByLivePlayfield.TryGetValue(livePlayfield2, out record);
            }
        }

        internal static bool IsBoundLivePlayfield(int livePlayfield2)
        {
            MissionAcgBindingRecord ignored;
            return TryResolveByLivePlayfield(livePlayfield2, out ignored);
        }

        internal static bool HasAnyBindingForOwner(int ownerInstance)
        {
            EnsureInitialized();
            lock (Sync)
            {
                foreach (MissionAcgBindingRecord record in ByAcceptedInstance.Values)
                {
                    if (record.Binding.OwnerIdentity.Instance == ownerInstance
                        && record.State.ReservesPlayfield)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static IList<MissionAcgBindingRecord> GetOwnedCleanupWork(
            int ownerInstance)
        {
            EnsureInitialized();
            lock (Sync)
            {
                var records = new List<MissionAcgBindingRecord>();
                foreach (MissionAcgBindingRecord record in ByAcceptedInstance.Values)
                {
                    if (record.Binding.OwnerIdentity.Instance == ownerInstance
                        && (record.State.LifecycleState
                            == MissionAcgLifecycleState.Expired
                            || record.State.LifecycleState
                            == MissionAcgLifecycleState.Abandoned
                            || record.State.LifecycleState
                            == MissionAcgLifecycleState.Completed
                        || record.State.LifecycleState
                            == MissionAcgLifecycleState.CleanupPending
                        || record.State.LifecycleState
                            == MissionAcgLifecycleState.Cleaned))
                    {
                        records.Add(record);
                    }
                }

                return records.AsReadOnly();
            }
        }

        private static bool TryVerifyRuntimeCleanup(
            MissionAcgBindingRecord record,
            out string failure)
        {
            failure = string.Empty;
            if (!MissionAcgSpatialRuntime.Cleanup(record))
            {
                failure = "Spatial runtime cleanup did not complete.";
                return false;
            }

            string subsystemFailure;
            if (!MissionAcgOperationalRuntime.Cleanup(record, out subsystemFailure))
            {
                failure = "Operational runtime cleanup did not complete: " + subsystemFailure;
                return false;
            }

            if (!MissionAcgRuntimeManager.Cleanup(record, out subsystemFailure))
            {
                failure = "Materialized runtime cleanup did not complete: " + subsystemFailure;
                return false;
            }

            return true;
        }

        private static bool TryGetCurrentRecord(
            MissionAcgBindingRecord supplied,
            out MissionAcgBindingRecord current,
            out string failure)
        {
            current = null;
            failure = string.Empty;
            if (supplied == null
                || !ByAcceptedInstance.TryGetValue(
                    supplied.Binding.AcceptedQuestIdentity.Instance,
                    out current)
                || !HasSameTransitionIdentity(current, supplied))
            {
                failure = "Generated-mission binding record is not active.";
                return false;
            }

            if (!MissionAcgLifecyclePolicy.IsSameBindingStateVersion(
                current.State,
                supplied.State))
            {
                failure =
                    "Generated-mission binding transition rejected a stale state version.";
                return false;
            }

            return true;
        }

        private static bool HasSameTransitionIdentity(
            MissionAcgBindingRecord current,
            MissionAcgBindingRecord supplied)
        {
            return current != null
                   && supplied != null
                   && SameIdentity(
                       current.Binding.AcceptedQuestIdentity,
                       supplied.Binding.AcceptedQuestIdentity)
                   && SameIdentity(
                       current.Binding.OwnerIdentity,
                       supplied.Binding.OwnerIdentity)
                   && SameIdentity(
                       current.Binding.MissionKeyIdentity,
                       supplied.Binding.MissionKeyIdentity)
                   && SameIdentity(
                       current.Binding.AcgBuildingIdentity,
                       supplied.Binding.AcgBuildingIdentity)
                   && current.Binding.AllocatedLivePlayfield2
                      == supplied.Binding.AllocatedLivePlayfield2
                   && string.Equals(
                       current.Binding.SelectedBundleId,
                       supplied.Binding.SelectedBundleId,
                       StringComparison.Ordinal);
        }

        private static bool SameIdentity(
            MissionAcgIdentityRecord first,
            MissionAcgIdentityRecord second)
        {
            return first != null
                   && second != null
                   && first.Type == second.Type
                   && first.Instance == second.Instance;
        }

        private static bool ReleaseCurrentPlayfield(
            MissionAcgBindingRecord current,
            bool holdForDurableJournalConfirmation,
            out string failure)
        {
            failure = string.Empty;
            if (!allocator.ReleaseAfterCleanup(
                current,
                holdForDurableJournalConfirmation))
            {
                failure = "PF2 allocator rejected exact-owner cleanup release.";
                return false;
            }

            MissionAcgBindingRecord mapped;
            if (ByLivePlayfield.TryGetValue(
                    current.Binding.AllocatedLivePlayfield2,
                    out mapped)
                && mapped.Binding.AcceptedQuestIdentity.Instance
                   == current.Binding.AcceptedQuestIdentity.Instance)
            {
                ByLivePlayfield.Remove(
                    current.Binding.AllocatedLivePlayfield2);
            }

            return true;
        }

        private static bool IsAccessible(
            MissionAcgBindingRecord record,
            int ownerInstance,
            DateTime nowUtc)
        {
            // Generated terminal missions currently have durable solo ownership only. Process-local
            // team IDs are intentionally not persisted or inferred.
            return MissionAcgBindingResolver.IsAccessible(
                record,
                ownerInstance,
                nowUtc);
        }

        private static void AddIndexes(MissionAcgBindingRecord record)
        {
            ByAcceptedInstance.Add(record.Binding.AcceptedQuestIdentity.Instance, record);
            ByKeyInstance.Add(record.Binding.MissionKeyIdentity.Instance, record);
            if (record.State.ReservesPlayfield)
            {
                ByLivePlayfield.Add(record.Binding.AllocatedLivePlayfield2, record);
            }
        }

        private static void ReplaceIndexes(MissionAcgBindingRecord record)
        {
            ByAcceptedInstance[record.Binding.AcceptedQuestIdentity.Instance] = record;
            ByKeyInstance[record.Binding.MissionKeyIdentity.Instance] = record;
            if (record.State.ReservesPlayfield)
            {
                ByLivePlayfield[record.Binding.AllocatedLivePlayfield2] = record;
            }
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }
}
