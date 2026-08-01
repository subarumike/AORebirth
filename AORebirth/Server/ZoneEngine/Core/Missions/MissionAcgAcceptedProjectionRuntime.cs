namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    internal static class MissionAcgAcceptedProjectionRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionAcgAcceptedProjection> ByAcceptedQuest =
            new Dictionary<int, MissionAcgAcceptedProjection>();

        private static readonly Dictionary<string, MissionAcgAcceptedProjection> ByOwnerOffer =
            new Dictionary<string, MissionAcgAcceptedProjection>(StringComparer.Ordinal);

        private static MissionAcgAcceptedProjectionStore store;

        private static bool initialized;

        internal static bool IsInitialized
        {
            get
            {
                lock (Sync)
                {
                    return initialized;
                }
            }
        }

        internal static IList<MissionAcgBindingRecord> Initialize(
            string missionStateDirectory,
            MissionAcgLayoutCatalog catalog,
            IList<MissionAcgBindingRecord> persistedBindings)
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return GetPendingReservations_NoLock(persistedBindings);
                }

                store = new MissionAcgAcceptedProjectionStore(
                    missionStateDirectory,
                    catalog);
                MissionAcgAcceptedProjectionLoadResult loaded = store.LoadAll();
                if (!loaded.IsValid)
                {
                    throw new InvalidOperationException(
                        "Accepted generated-mission projection restoration failed closed: "
                        + string.Join(" | ", loaded.Diagnostics));
                }

                var bindingsByAccepted = new Dictionary<int, MissionAcgBindingRecord>();
                if (persistedBindings != null)
                {
                    for (int i = 0; i < persistedBindings.Count; i++)
                    {
                        MissionAcgBindingRecord binding = persistedBindings[i];
                        if (binding != null)
                        {
                            bindingsByAccepted.Add(
                                binding.Binding.AcceptedQuestIdentity.Instance,
                                binding);
                        }
                    }
                }

                for (int i = 0; i < loaded.Projections.Count; i++)
                {
                    MissionAcgAcceptedProjection projection = loaded.Projections[i];
                    MissionAcgBindingRecord binding;
                    bool hasPersistedBinding = bindingsByAccepted.TryGetValue(
                        projection.Binding.AcceptedQuestIdentity.Instance,
                        out binding);
                    if ((int)projection.AcceptancePhase
                            >= (int)MissionAcgAcceptancePhase.BindingPersisted
                        && !hasPersistedBinding)
                    {
                        throw new InvalidOperationException(
                            "Accepted projection "
                            + projection.Binding.AcceptedQuestIdentity.Instance
                            + " requires a missing durable ACG binding.");
                    }

                    if (hasPersistedBinding
                        && !BindingsMatch(projection.Binding, binding.Binding))
                    {
                        throw new InvalidOperationException(
                            "Accepted projection "
                            + projection.Binding.AcceptedQuestIdentity.Instance
                            + " does not match its durable ACG binding.");
                    }

                    if (hasPersistedBinding
                        && (projection.LifecycleState
                                != binding.State.LifecycleState
                            || projection.CleanupState
                                != binding.State.CleanupState))
                    {
                        if ((int)projection.LifecycleState
                                > (int)binding.State.LifecycleState
                            || (int)projection.CleanupState
                                > (int)binding.State.CleanupState)
                        {
                            throw new InvalidOperationException(
                                "Accepted projection lifecycle is ahead of its binding for quest "
                                + projection.Binding.AcceptedQuestIdentity.Instance
                                + ".");
                        }

                        MissionAcgAcceptedProjection reconciled;
                        string reconcileFailure;
                        if (!store.TryReplace(
                            projection.WithLifecycle(
                                binding.State.LifecycleState,
                                binding.State.CleanupState,
                                binding.State.LastUpdatedUtc),
                            out reconciled,
                            out reconcileFailure))
                        {
                            throw new InvalidOperationException(
                                "Accepted projection lifecycle reconciliation failed closed for quest "
                                + projection.Binding.AcceptedQuestIdentity.Instance
                                + ": "
                                + reconcileFailure);
                        }

                        projection = reconciled;
                    }

                    AddIndexes_NoLock(projection);
                }

                initialized = true;
                return GetPendingReservations_NoLock(persistedBindings);
            }
        }

        internal static bool TryCreate(
            MissionAcgAcceptedProjection projection,
            out MissionAcgAcceptedProjection persisted,
            out string failure)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                if (ByAcceptedQuest.ContainsKey(
                        projection.Binding.AcceptedQuestIdentity.Instance)
                    || ByOwnerOffer.ContainsKey(OwnerOfferKey(projection.Binding)))
                {
                    persisted = null;
                    failure = "Accepted mission already exists for this quest or owner/offer.";
                    return false;
                }

                if (!store.TryCreate(projection, out persisted, out failure))
                {
                    return false;
                }

                AddIndexes_NoLock(persisted);
                return true;
            }
        }

        internal static bool TryReplace(
            MissionAcgAcceptedProjection projection,
            out MissionAcgAcceptedProjection persisted,
            out string failure)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                MissionAcgAcceptedProjection current;
                if (!ByAcceptedQuest.TryGetValue(
                        projection.Binding.AcceptedQuestIdentity.Instance,
                        out current)
                    || !string.Equals(
                        OwnerOfferKey(current.Binding),
                        OwnerOfferKey(projection.Binding),
                        StringComparison.Ordinal))
                {
                    persisted = null;
                    failure = "Accepted projection is not registered for replacement.";
                    return false;
                }

                if (!store.TryReplace(projection, out persisted, out failure))
                {
                    return false;
                }

                ByAcceptedQuest[persisted.Binding.AcceptedQuestIdentity.Instance] = persisted;
                ByOwnerOffer[OwnerOfferKey(persisted.Binding)] = persisted;
                return true;
            }
        }

        internal static bool TryAdvancePhase(
            MissionAcgAcceptedProjection projection,
            MissionAcgAcceptancePhase phase,
            DateTime nowUtc,
            out MissionAcgAcceptedProjection updated,
            out string failure)
        {
            try
            {
                return TryReplace(
                    projection.WithPhase(phase, nowUtc),
                    out updated,
                    out failure);
            }
            catch (Exception ex)
            {
                updated = null;
                failure = ex.Message;
                return false;
            }
        }

        internal static bool TrySetObjective(
            MissionAcgAcceptedProjection projection,
            MissionAcgIdentityRecord objectiveIdentity,
            DateTime nowUtc,
            out MissionAcgAcceptedProjection updated,
            out string failure)
        {
            try
            {
                return TryReplace(
                    projection.WithObjective(objectiveIdentity, nowUtc),
                    out updated,
                    out failure);
            }
            catch (Exception ex)
            {
                updated = null;
                failure = ex.Message;
                return false;
            }
        }

        internal static bool TrySetArtifact(
            MissionAcgAcceptedProjection projection,
            MissionAcgIdentityRecord artifactIdentity,
            DateTime nowUtc,
            out MissionAcgAcceptedProjection updated,
            out string failure)
        {
            try
            {
                return TryReplace(
                    projection.WithArtifact(artifactIdentity, nowUtc),
                    out updated,
                    out failure);
            }
            catch (Exception ex)
            {
                updated = null;
                failure = ex.Message;
                return false;
            }
        }

        internal static void OnBindingStateChanged(MissionAcgBindingRecord bindingRecord)
        {
            if (bindingRecord == null)
            {
                return;
            }

            lock (Sync)
            {
                if (!initialized)
                {
                    return;
                }

                MissionAcgAcceptedProjection projection;
                if (!ByAcceptedQuest.TryGetValue(
                        bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        out projection))
                {
                    return;
                }

                if (projection.LifecycleState == bindingRecord.State.LifecycleState
                    && projection.CleanupState == bindingRecord.State.CleanupState)
                {
                    return;
                }

                MissionAcgAcceptedProjection persisted;
                string failure;
                if (!store.TryReplace(
                        projection.WithLifecycle(
                            bindingRecord.State.LifecycleState,
                            bindingRecord.State.CleanupState,
                            bindingRecord.State.LastUpdatedUtc),
                        out persisted,
                        out failure))
                {
                    throw new InvalidOperationException(
                        "Accepted projection lifecycle persistence failed closed for quest "
                        + bindingRecord.Binding.AcceptedQuestIdentity.Instance
                        + ": "
                        + failure);
                }

                ByAcceptedQuest[persisted.Binding.AcceptedQuestIdentity.Instance] = persisted;
                ByOwnerOffer[OwnerOfferKey(persisted.Binding)] = persisted;
            }
        }

        internal static void ReconcileObjectiveArtifacts()
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                var replacements = new List<MissionAcgAcceptedProjection>();
                foreach (MissionAcgAcceptedProjection projection in ByAcceptedQuest.Values)
                {
                    MissionAcgObjectiveRecord objective;
                    bool hasObjective = MissionAcgObjectiveRuntime.TryGetByAccepted(
                        projection.Binding.OwnerIdentity.Instance,
                        projection.Binding.AcceptedQuestIdentity.Instance,
                        out objective);
                    if ((int)projection.AcceptancePhase
                            >= (int)MissionAcgAcceptancePhase.ObjectivePersisted
                        && !hasObjective)
                    {
                        throw new InvalidOperationException(
                            "Accepted projection requires a missing objective binding for quest "
                            + projection.Binding.AcceptedQuestIdentity.Instance
                            + ".");
                    }

                    if (!hasObjective)
                    {
                        continue;
                    }

                    MissionAcgAcceptedProjection reconciled = projection;
                    if (projection.RuntimeObjectiveIdentity == null)
                    {
                        reconciled = projection.WithObjective(
                            objective.Binding.RuntimeObjectiveIdentity,
                            objective.State.UpdatedUtc);
                    }
                    else if (!projection.RuntimeObjectiveIdentity.Equals(
                        objective.Binding.RuntimeObjectiveIdentity))
                    {
                        throw new InvalidOperationException(
                            "Accepted projection objective identity mismatch for quest "
                            + projection.Binding.AcceptedQuestIdentity.Instance
                            + ".");
                    }

                    if (objective.State.MissionItemIdentity == null)
                    {
                        if (projection.Binding.MissionType
                                == MissionRollType.RepairMachine
                            && (int)projection.AcceptancePhase
                                >= (int)MissionAcgAcceptancePhase.ArtifactsGranted)
                        {
                            throw new InvalidOperationException(
                                "Repair accepted projection requires its missing objective component for quest "
                                + projection.Binding.AcceptedQuestIdentity.Instance
                                + ".");
                        }

                        if (!object.ReferenceEquals(reconciled, projection))
                        {
                            replacements.Add(reconciled);
                        }

                        continue;
                    }

                    if (reconciled.MissionArtifactIdentity != null)
                    {
                        if (!reconciled.MissionArtifactIdentity.Equals(
                            objective.State.MissionItemIdentity))
                        {
                            throw new InvalidOperationException(
                                "Accepted projection artifact does not match objective state for quest "
                                + projection.Binding.AcceptedQuestIdentity.Instance
                                + ".");
                        }

                        if (!object.ReferenceEquals(reconciled, projection))
                        {
                            replacements.Add(reconciled);
                        }

                        continue;
                    }

                    replacements.Add(
                        reconciled.WithArtifact(
                            objective.State.MissionItemIdentity,
                            objective.State.UpdatedUtc));
                }

                for (int i = 0; i < replacements.Count; i++)
                {
                    MissionAcgAcceptedProjection persisted;
                    string failure;
                    if (!store.TryReplace(
                        replacements[i],
                        out persisted,
                        out failure))
                    {
                        throw new InvalidOperationException(
                            "Accepted projection artifact reconciliation failed closed for quest "
                            + replacements[i].Binding.AcceptedQuestIdentity.Instance
                            + ": "
                            + failure);
                    }

                    ByAcceptedQuest[persisted.Binding.AcceptedQuestIdentity.Instance] = persisted;
                    ByOwnerOffer[OwnerOfferKey(persisted.Binding)] = persisted;
                }
            }
        }

        internal static bool TryGetByAcceptedQuest(
            int acceptedQuestInstance,
            out MissionAcgAcceptedProjection projection)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                return ByAcceptedQuest.TryGetValue(acceptedQuestInstance, out projection);
            }
        }

        internal static bool TryGetByOwnerOffer(
            int ownerType,
            int ownerInstance,
            int originalOfferType,
            int originalOfferInstance,
            out MissionAcgAcceptedProjection projection)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                return ByOwnerOffer.TryGetValue(
                    OwnerOfferKey(
                        ownerType,
                        ownerInstance,
                        originalOfferType,
                        originalOfferInstance),
                    out projection);
            }
        }

        internal static bool IsOriginalOfferIdentityInUse(int offerInstance)
        {
            lock (Sync)
            {
                if (!initialized)
                {
                    return false;
                }

                foreach (MissionAcgAcceptedProjection projection in ByAcceptedQuest.Values)
                {
                    if (projection.Binding.OriginalOfferIdentity.Instance == offerInstance)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static IList<MissionAcgAcceptedProjection> GetOwned(
            int ownerInstance)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                var owned = new List<MissionAcgAcceptedProjection>();
                foreach (MissionAcgAcceptedProjection projection in ByAcceptedQuest.Values)
                {
                    if (projection.Binding.OwnerIdentity.Instance == ownerInstance)
                    {
                        owned.Add(projection);
                    }
                }

                return owned.AsReadOnly();
            }
        }

        private static IList<MissionAcgBindingRecord> GetPendingReservations_NoLock(
            IList<MissionAcgBindingRecord> persistedBindings)
        {
            var persisted = new HashSet<int>();
            if (persistedBindings != null)
            {
                for (int i = 0; i < persistedBindings.Count; i++)
                {
                    if (persistedBindings[i] != null)
                    {
                        persisted.Add(
                            persistedBindings[i].Binding.AcceptedQuestIdentity.Instance);
                    }
                }
            }

            var pending = new List<MissionAcgBindingRecord>();
            foreach (MissionAcgAcceptedProjection projection in ByAcceptedQuest.Values)
            {
                if (!persisted.Contains(projection.Binding.AcceptedQuestIdentity.Instance)
                    && projection.AcceptancePhase == MissionAcgAcceptancePhase.OfferClaimed)
                {
                    pending.Add(
                        new MissionAcgBindingRecord(
                            projection.Binding,
                            new MissionAcgInstanceState(
                                projection.LifecycleState,
                                projection.CleanupState,
                                projection.UpdatedUtc,
                                null),
                            string.Empty));
                }
            }

            return pending.AsReadOnly();
        }

        private static void AddIndexes_NoLock(MissionAcgAcceptedProjection projection)
        {
            int accepted = projection.Binding.AcceptedQuestIdentity.Instance;
            string ownerOffer = OwnerOfferKey(projection.Binding);
            if (ByAcceptedQuest.ContainsKey(accepted)
                || ByOwnerOffer.ContainsKey(ownerOffer))
            {
                throw new InvalidOperationException(
                    "Duplicate accepted mission projection identity was loaded.");
            }

            ByAcceptedQuest.Add(accepted, projection);
            ByOwnerOffer.Add(ownerOffer, projection);
        }

        private static string OwnerOfferKey(MissionAcgInstanceBinding binding)
        {
            return OwnerOfferKey(
                binding.OwnerIdentity.Type,
                binding.OwnerIdentity.Instance,
                binding.OriginalOfferIdentity.Type,
                binding.OriginalOfferIdentity.Instance);
        }

        private static string OwnerOfferKey(
            int ownerType,
            int ownerInstance,
            int originalOfferType,
            int originalOfferInstance)
        {
            return ownerType
                   + ":"
                   + ownerInstance
                   + "|"
                   + originalOfferType
                   + ":"
                   + originalOfferInstance;
        }

        private static bool BindingsMatch(
            MissionAcgInstanceBinding left,
            MissionAcgInstanceBinding right)
        {
            return left.BindingFormatVersion == right.BindingFormatVersion
                   && left.AcceptedQuestIdentity.Equals(right.AcceptedQuestIdentity)
                   && left.OriginalOfferIdentity.Equals(right.OriginalOfferIdentity)
                   && left.OwnerIdentity.Equals(right.OwnerIdentity)
                   && Equals(left.TeamIdentity, right.TeamIdentity)
                   && left.ExplicitNoTeam == right.ExplicitNoTeam
                   && left.MissionType == right.MissionType
                   && left.MissionQuality == right.MissionQuality
                   && left.DeterministicSeed == right.DeterministicSeed
                   && left.MissionKeyIdentity.Equals(right.MissionKeyIdentity)
                   && left.ExteriorEntranceIdentity.Equals(right.ExteriorEntranceIdentity)
                   && left.ExteriorEntranceLow == right.ExteriorEntranceLow
                   && left.ExteriorEntranceHigh == right.ExteriorEntranceHigh
                   && left.ExteriorX.Equals(right.ExteriorX)
                   && left.ExteriorY.Equals(right.ExteriorY)
                   && left.ExteriorZ.Equals(right.ExteriorZ)
                   && left.IssuingTerminalIdentity.Equals(right.IssuingTerminalIdentity)
                   && string.Equals(left.SelectedBundleId, right.SelectedBundleId, StringComparison.Ordinal)
                   && string.Equals(
                       left.SelectedBundlePayloadSha256,
                       right.SelectedBundlePayloadSha256,
                       StringComparison.Ordinal)
                   && left.AcgBuildingIdentity.Equals(right.AcgBuildingIdentity)
                   && left.AllocatedLivePlayfield2 == right.AllocatedLivePlayfield2
                   && left.AcceptedUtc == right.AcceptedUtc
                   && left.ExpiryUtc == right.ExpiryUtc;
        }

        private static void EnsureInitialized_NoLock()
        {
            if (!initialized || store == null)
            {
                throw new InvalidOperationException(
                    "Accepted generated-mission projection runtime is not initialized.");
            }
        }
    }
}
