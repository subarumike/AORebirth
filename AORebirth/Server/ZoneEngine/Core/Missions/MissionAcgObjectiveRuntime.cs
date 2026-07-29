namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;

    #endregion

    /// <summary>
    /// Durable exact-objective registry. Every lookup is accepted mission + owner + live PF2 +
    /// runtime identity; no type-only or newest-mission resolution is provided.
    /// </summary>
    internal static class MissionAcgObjectiveRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionAcgObjectiveRecord> ByAccepted =
            new Dictionary<int, MissionAcgObjectiveRecord>();

        private static readonly Dictionary<string, MissionAcgObjectiveRecord> ByRuntime =
            new Dictionary<string, MissionAcgObjectiveRecord>(StringComparer.Ordinal);

        private static MissionAcgLayoutCatalog catalog;

        private static MissionAcgObjectiveStore store;

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

                catalog = layoutCatalog;
                store = new MissionAcgObjectiveStore(missionStateDirectory, catalog);
                MissionAcgObjectiveLoadResult loaded = store.LoadAll();
                if (!loaded.IsValid)
                {
                    throw new InvalidOperationException(
                        "Mission objective restoration failed closed: "
                        + string.Join(" | ", loaded.Diagnostics));
                }

                var bindingByAccepted = new Dictionary<int, MissionAcgBindingRecord>();
                for (int i = 0; i < bindings.Count; i++)
                {
                    bindingByAccepted.Add(
                        bindings[i].Binding.AcceptedQuestIdentity.Instance,
                        bindings[i]);
                }

                for (int i = 0; i < loaded.Records.Count; i++)
                {
                    MissionAcgObjectiveRecord record = loaded.Records[i];
                    MissionAcgBindingRecord bindingRecord;
                    if (!bindingByAccepted.TryGetValue(
                            record.Binding.AcceptedQuestIdentity.Instance,
                            out bindingRecord)
                        || !Matches(bindingRecord, record))
                    {
                        throw new InvalidOperationException(
                            "Mission objective has no matching durable accepted binding at "
                            + record.RecordPath
                            + ".");
                    }

                    MissionAcgObjectiveLifecycle restoredLifecycle =
                        LifecycleForBinding(
                            bindingRecord,
                            record.State.Lifecycle,
                            record.State.Phase);
                    if (restoredLifecycle != record.State.Lifecycle)
                    {
                        MissionAcgObjectiveRecord reconciled;
                        string reconcileFailure;
                        if (!store.TryReplace(
                            record.WithState(
                                record.State.Copy(lifecycle: restoredLifecycle)),
                            out reconciled,
                            out reconcileFailure))
                        {
                            throw new InvalidOperationException(
                                "Mission objective lifecycle reconciliation failed at "
                                + record.RecordPath
                                + ": "
                                + reconcileFailure);
                        }

                        record = reconciled;
                    }

                    AddIndexes(record);
                }

                for (int i = 0; i < bindings.Count; i++)
                {
                    MissionAcgBindingRecord bindingRecord = bindings[i];
                    if (!bindingRecord.State.ReservesPlayfield
                        || ByAccepted.ContainsKey(
                            bindingRecord.Binding.AcceptedQuestIdentity.Instance))
                    {
                        continue;
                    }

                    MissionAcgObjectiveRecord created;
                    string failure;
                    if (!TryCreateForBindingLocked(
                        bindingRecord,
                        out created,
                        out failure))
                    {
                        throw new InvalidOperationException(
                            "Mission objective deterministic restoration failed for accepted quest "
                            + bindingRecord.Binding.AcceptedQuestIdentity
                            + ": "
                            + failure);
                    }
                }

                initialized = true;
            }
        }

        internal static bool TryCreateForBinding(
            MissionAcgBindingRecord bindingRecord,
            out MissionAcgObjectiveRecord persisted,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return TryCreateForBindingLocked(
                    bindingRecord,
                    out persisted,
                    out failure);
            }
        }

        internal static bool TryGetByAccepted(
            int ownerInstance,
            int acceptedQuestInstance,
            out MissionAcgObjectiveRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return ByAccepted.TryGetValue(acceptedQuestInstance, out record)
                       && record.Binding.OwnerIdentity.Instance == ownerInstance;
            }
        }

        internal static bool TryResolveRuntime(
            int ownerInstance,
            int allocatedLivePlayfield2,
            MissionAcgIdentityRecord runtimeIdentity,
            out MissionAcgObjectiveRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                record = null;
                return runtimeIdentity != null
                       && ByRuntime.TryGetValue(
                           RuntimeKey(allocatedLivePlayfield2, runtimeIdentity),
                           out record)
                       && record.Binding.OwnerIdentity.Instance == ownerInstance
                       && record.Binding.AllocatedLivePlayfield2
                          == allocatedLivePlayfield2;
            }
        }

        internal static bool TryResolveReturnItem(
            int ownerInstance,
            MissionAcgIdentityRecord missionItemIdentity,
            MissionAcgIdentityRecord issuingTerminalIdentity,
            out MissionAcgObjectiveRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                record = null;
                foreach (MissionAcgObjectiveRecord candidate in ByAccepted.Values)
                {
                    if (candidate.Binding.OwnerIdentity.Instance != ownerInstance
                        || candidate.Binding.MissionType != MissionRollType.FindItemReturn
                        || candidate.State.MissionItemIdentity == null
                        || missionItemIdentity == null
                        || !candidate.State.MissionItemIdentity.Equals(missionItemIdentity)
                        || candidate.Binding.IssuingTerminalIdentity == null
                        || issuingTerminalIdentity == null
                        || !candidate.Binding.IssuingTerminalIdentity.Equals(
                            issuingTerminalIdentity))
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

        internal static IList<MissionAcgObjectiveRecord> GetOwnedCompletionWork(
            int ownerInstance)
        {
            EnsureInitialized();
            lock (Sync)
            {
                var records = new List<MissionAcgObjectiveRecord>();
                foreach (MissionAcgObjectiveRecord record in ByAccepted.Values)
                {
                    if (record.Binding.OwnerIdentity.Instance == ownerInstance
                        && MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                            record.State))
                    {
                        records.Add(record);
                    }
                }

                return records.AsReadOnly();
            }
        }

        internal static bool TryReplaceState(
            MissionAcgObjectiveRecord record,
            MissionAcgObjectiveState state,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                updated = null;
                failure = string.Empty;
                MissionAcgObjectiveRecord current;
                if (record == null
                    || !ByAccepted.TryGetValue(
                        record.Binding.AcceptedQuestIdentity.Instance,
                        out current)
                    || !string.Equals(
                        current.RecordPath,
                        record.RecordPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Objective record is not active.";
                    return false;
                }

                if (!MissionAcgCompletionRules.CanReplace(
                    current.State,
                    state,
                    out failure))
                {
                    return false;
                }

                if (!store.TryReplace(
                    current.WithState(state),
                    out updated,
                    out failure))
                {
                    return false;
                }

                ReplaceIndexes(updated);
                return true;
            }
        }

        internal static bool TrySetMissionItem(
            MissionAcgObjectiveRecord record,
            MissionAcgIdentityRecord itemIdentity,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = null;
            failure = string.Empty;
            if (record == null || itemIdentity == null)
            {
                failure = "Exact mission item identity is required.";
                return false;
            }

            if (record.State.MissionItemIdentity != null)
            {
                if (record.State.MissionItemIdentity.Equals(itemIdentity))
                {
                    updated = record;
                    return true;
                }

                failure = "Mission item identity is immutable after assignment.";
                return false;
            }

            return TryReplaceState(
                record,
                record.State.Copy(
                    lifecycle: MissionAcgObjectiveLifecycle.ItemPossessed,
                    missionItemIdentity: itemIdentity),
                out updated,
                out failure);
        }

        internal static bool TrySetLifecycle(
            MissionAcgObjectiveRecord record,
            MissionAcgObjectiveLifecycle lifecycle,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Objective record is required.";
                return false;
            }

            if ((lifecycle == MissionAcgObjectiveLifecycle.Abandoned
                 || lifecycle == MissionAcgObjectiveLifecycle.Expired)
                && record.State.Phase
                   >= MissionAcgCompletionPhase.RewardClaimStarted)
            {
                failure =
                    "Completion already owns the durable completion-versus-cleanup race.";
                return false;
            }

            if (record.State.Lifecycle == MissionAcgObjectiveLifecycle.Completed
                || record.State.Lifecycle == MissionAcgObjectiveLifecycle.CleanupCompleted
                || record.State.Lifecycle == MissionAcgObjectiveLifecycle.Invalid)
            {
                failure = "Objective lifecycle is terminal.";
                return false;
            }

            return TryReplaceState(
                record,
                record.State.Copy(lifecycle: lifecycle),
                out updated,
                out failure);
        }

        internal static bool TryDeleteAfterFailedAcceptance(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                MissionAcgObjectiveRecord record;
                if (acceptedQuestIdentity == null
                    || !ByAccepted.TryGetValue(
                        acceptedQuestIdentity.Instance,
                        out record))
                {
                    return true;
                }

                if (!store.TryDelete(acceptedQuestIdentity, out failure))
                {
                    return false;
                }

                RemoveIndexes(record);
                return true;
            }
        }

        private static bool TryCreateForBindingLocked(
            MissionAcgBindingRecord bindingRecord,
            out MissionAcgObjectiveRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (bindingRecord == null)
            {
                failure = "Durable binding is required.";
                return false;
            }

            MissionAcgObjectiveRecord existing;
            if (ByAccepted.TryGetValue(
                bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                out existing))
            {
                if (!Matches(bindingRecord, existing))
                {
                    failure = "Existing objective differs from accepted binding.";
                    return false;
                }

                persisted = existing;
                return true;
            }

            MissionAcgLayoutBundle bundle =
                catalog.FindByLayoutId(bindingRecord.Binding.SelectedBundleId);
            if (bundle == null || bundle.ObjectiveSlots.Count != 1)
            {
                failure = "Selected bundle must contain one exact objective slot.";
                return false;
            }

            MissionAcgMaterializedInstance materialized;
            if (!MissionAcgRuntimeMaterializer.TryMaterialize(
                bindingRecord,
                bundle,
                null,
                DateTime.UtcNow,
                out materialized,
                out failure))
            {
                return false;
            }

            MissionAcgObjectiveSlotRecord slot = bundle.ObjectiveSlots[0];
            MissionAcgRuntimeObject runtimeObject = null;
            for (int i = 0; i < materialized.Objects.Count; i++)
            {
                if (materialized.Objects[i].Identity.CapturedIdentity.Equals(
                    slot.CapturedIdentity))
                {
                    runtimeObject = materialized.Objects[i];
                    break;
                }
            }

            if (runtimeObject == null)
            {
                failure = "Captured objective has no deterministic runtime identity.";
                return false;
            }

            MissionRollType type = bindingRecord.Binding.MissionType;
            int missionItemTemplate =
                type == MissionRollType.RepairMachine
                    ? MissionAcgObjectiveContract.RepairComponentTemplateId
                    : type == MissionRollType.FindItemReturn
                    ? slot.TemplateId
                    : 0;
            int machineTemplate =
                type == MissionRollType.RepairMachine
                    ? MissionAcgObjectiveContract.RepairMachineTemplateId
                    : 0;
            var objectiveBinding =
                new MissionAcgObjectiveBinding(
                    MissionAcgObjectiveBinding.CurrentFormatVersion,
                    bindingRecord.Binding.AcceptedQuestIdentity,
                    bindingRecord.Binding.OwnerIdentity,
                    bindingRecord.Binding.TeamIdentity,
                    bindingRecord.Binding.ExplicitNoTeam,
                    type,
                    bindingRecord.Binding.AllocatedLivePlayfield2,
                    bundle.LayoutId,
                    bundle.GeneratorPayloadSha256,
                    bundle.BuildingIdentity,
                    slot.Slot,
                    slot.CapturedIdentity,
                    runtimeObject.Identity.RuntimeIdentity,
                    slot.TemplateId,
                    slot.Name,
                    MissionAcgObjectiveContract.InteractionFor(type),
                    type == MissionRollType.FindItemReturn
                        ? bindingRecord.Binding.IssuingTerminalIdentity
                        : null,
                    missionItemTemplate,
                    machineTemplate);
            DateTime now = DateTime.UtcNow;
            MissionAcgObjectiveLifecycle initialLifecycle =
                LifecycleForBinding(
                    bindingRecord,
                    MissionAcgObjectiveLifecycle.Reserved,
                    MissionAcgCompletionPhase.None);
            var state =
                new MissionAcgObjectiveState(
                    initialLifecycle,
                    MissionAcgCompletionPhase.None,
                    null,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    MissionAcgGrantState.NotStarted,
                    MissionAcgGrantState.NotStarted,
                    MissionAcgGrantState.NotStarted,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    false,
                    false,
                    false,
                    false,
                    false,
                    now);
            if (!store.TryCreate(
                new MissionAcgObjectiveRecord(objectiveBinding, state, string.Empty),
                out persisted,
                out failure))
            {
                return false;
            }

            AddIndexes(persisted);
            return true;
        }

        private static bool Matches(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveRecord objectiveRecord)
        {
            return bindingRecord.Binding.AcceptedQuestIdentity.Equals(
                       objectiveRecord.Binding.AcceptedQuestIdentity)
                   && bindingRecord.Binding.OwnerIdentity.Equals(
                       objectiveRecord.Binding.OwnerIdentity)
                   && bindingRecord.Binding.AllocatedLivePlayfield2
                      == objectiveRecord.Binding.AllocatedLivePlayfield2
                   && string.Equals(
                       bindingRecord.Binding.SelectedBundleId,
                       objectiveRecord.Binding.BundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       bindingRecord.Binding.SelectedBundlePayloadSha256,
                       objectiveRecord.Binding.BundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && bindingRecord.Binding.AcgBuildingIdentity.Equals(
                       objectiveRecord.Binding.BuildingIdentity)
                   && bindingRecord.Binding.MissionType
                      == objectiveRecord.Binding.MissionType;
        }

        private static MissionAcgObjectiveLifecycle LifecycleForBinding(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveLifecycle current,
            MissionAcgCompletionPhase phase)
        {
            if (phase >= MissionAcgCompletionPhase.RewardClaimStarted)
            {
                return current;
            }

            switch (bindingRecord.State.LifecycleState)
            {
                case MissionAcgLifecycleState.Abandoned:
                    return MissionAcgObjectiveLifecycle.Abandoned;
                case MissionAcgLifecycleState.Expired:
                    return MissionAcgObjectiveLifecycle.Expired;
                case MissionAcgLifecycleState.Cleaned:
                    return MissionAcgObjectiveLifecycle.CleanupCompleted;
                case MissionAcgLifecycleState.Invalid:
                    return MissionAcgObjectiveLifecycle.Invalid;
                default:
                    return current;
            }
        }

        private static void AddIndexes(MissionAcgObjectiveRecord record)
        {
            ByAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance, record);
            ByRuntime.Add(
                RuntimeKey(
                    record.Binding.AllocatedLivePlayfield2,
                    record.Binding.RuntimeObjectiveIdentity),
                record);
        }

        private static void ReplaceIndexes(MissionAcgObjectiveRecord record)
        {
            ByAccepted[record.Binding.AcceptedQuestIdentity.Instance] = record;
            ByRuntime[
                RuntimeKey(
                    record.Binding.AllocatedLivePlayfield2,
                    record.Binding.RuntimeObjectiveIdentity)] = record;
        }

        private static void RemoveIndexes(MissionAcgObjectiveRecord record)
        {
            ByAccepted.Remove(record.Binding.AcceptedQuestIdentity.Instance);
            ByRuntime.Remove(
                RuntimeKey(
                    record.Binding.AllocatedLivePlayfield2,
                    record.Binding.RuntimeObjectiveIdentity));
        }

        private static string RuntimeKey(
            int playfield,
            MissionAcgIdentityRecord identity)
        {
            return playfield + "|" + identity.Type + ":" + identity.Instance;
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
