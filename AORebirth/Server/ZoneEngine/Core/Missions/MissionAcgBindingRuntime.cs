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
                return allocator;
            }
        }

        internal static void Initialize()
        {
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

                MissionAcgObjectiveLoadResult objectiveProbe =
                    new MissionAcgObjectiveStore(
                        missionStateDirectory,
                        catalog).LoadAll();
                if (!objectiveProbe.IsValid)
                {
                    throw new InvalidOperationException(
                        "Mission objective restoration failed closed before expiry reconciliation: "
                        + string.Join(" | ", objectiveProbe.Diagnostics));
                }

                var completionOwned =
                    new HashSet<int>();
                for (int i = 0; i < objectiveProbe.Records.Count; i++)
                {
                    if (objectiveProbe.Records[i].State.Phase
                        >= MissionAcgCompletionPhase.RewardClaimStarted)
                    {
                        completionOwned.Add(
                            objectiveProbe.Records[i].Binding.AcceptedQuestIdentity.Instance);
                    }
                }

                DateTime now = DateTime.UtcNow;
                for (int i = 0; i < loaded.Records.Count; i++)
                {
                    MissionAcgBindingRecord record = loaded.Records[i];
                    if (record.Binding.ExpiryUtc <= now
                        && !completionOwned.Contains(
                            record.Binding.AcceptedQuestIdentity.Instance)
                        && (record.State.LifecycleState
                            == MissionAcgLifecycleState.Accepted
                            || record.State.LifecycleState
                            == MissionAcgLifecycleState.Active))
                    {
                        MissionAcgInstanceState expired =
                            record.State.Transition(
                                MissionAcgLifecycleState.Expired,
                                MissionAcgCleanupState.KeyRemovalPending,
                                now);
                        MissionAcgBindingRecord updated;
                        string updateFailure;
                        if (!store.TryReplace(
                            record.WithState(expired),
                            out updated,
                            out updateFailure))
                        {
                            throw new InvalidOperationException(
                                "Mission ACG expiry restoration failed for accepted quest "
                                + IdentityKey(record.Binding.AcceptedQuestIdentity)
                                + " at "
                                + record.RecordPath
                                + ": "
                                + updateFailure);
                        }

                        record = updated;
                    }

                    AddIndexes(record);
                }

                MissionAcgRuntimeManager.Initialize(
                    new List<MissionAcgBindingRecord>(ByAcceptedInstance.Values).AsReadOnly(),
                    catalog,
                    missionStateDirectory);
                MissionAcgObjectiveRuntime.Initialize(
                    new List<MissionAcgBindingRecord>(ByAcceptedInstance.Values).AsReadOnly(),
                    catalog,
                    missionStateDirectory);
                MissionAcgOperationalRuntime.Initialize(
                    new List<MissionAcgBindingRecord>(ByAcceptedInstance.Values).AsReadOnly(),
                    catalog,
                    missionStateDirectory);
                initialized = true;
            }
        }

        internal static bool TryPersistNew(
            MissionAcgBindingRecord record,
            out MissionAcgBindingRecord persisted,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                persisted = null;
                failure = string.Empty;
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
                return true;
            }
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
            lock (Sync)
            {
                updated = null;
                failure = string.Empty;
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
                MissionAcgOperationalRuntime.OnBindingStateChanged(updated);
                MissionAcgRuntimeManager.OnBindingStateChanged(updated);
                if (updated.State.LifecycleState == MissionAcgLifecycleState.Cleaned
                    && updated.State.CleanupState == MissionAcgCleanupState.Completed)
                {
                    allocator.ReleaseAfterCleanup(updated);
                    ByLivePlayfield.Remove(updated.Binding.AllocatedLivePlayfield2);
                }

                return true;
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
                            == MissionAcgLifecycleState.CleanupPending))
                    {
                        records.Add(record);
                    }
                }

                return records.AsReadOnly();
            }
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
