#region License

// Copyright (c) 2015-2026 AORebirth

#endregion

namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    /// <summary>
    /// Durable generated-mission token progress. The exact persisted operational
    /// death is the recovery source; process-local registrations are routing aids only.
    /// </summary>
    internal static class MissionAcgTokenProgressRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionAcgTokenProgressRecord>
            ByAccepted =
                new Dictionary<int, MissionAcgTokenProgressRecord>();

        private static readonly Dictionary<int, int> RegisteredPlayfields =
            new Dictionary<int, int>();

        private static readonly HashSet<int> InvalidAccepted =
            new HashSet<int>();

        private static MissionAcgTokenProgressStore store;

        private static bool initialized;

        private static bool restorationFailed;

        internal static void Initialize(
            IList<MissionAcgBindingRecord> bindings,
            string missionStateDirectory)
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                store =
                    new MissionAcgTokenProgressStore(missionStateDirectory);
                ByAccepted.Clear();
                RegisteredPlayfields.Clear();
                InvalidAccepted.Clear();
                restorationFailed = false;

                var bindingByAccepted =
                    new Dictionary<int, MissionAcgBindingRecord>();
                IList<MissionAcgBindingRecord> restoredBindings =
                    bindings ?? new MissionAcgBindingRecord[0];
                for (int i = 0; i < restoredBindings.Count; i++)
                {
                    MissionAcgBindingRecord binding = restoredBindings[i];
                    int accepted =
                        binding.Binding.AcceptedQuestIdentity.Instance;
                    if (bindingByAccepted.ContainsKey(accepted))
                    {
                        restorationFailed = true;
                        MissionDiagnostics.Log(
                            "ACG-TOKEN-RESTORE-FAIL accepted={0}:{1} path={2} reason=duplicate-binding",
                            binding.Binding.AcceptedQuestIdentity.Type,
                            accepted,
                            binding.RecordPath);
                        continue;
                    }

                    bindingByAccepted.Add(accepted, binding);
                }

                MissionAcgTokenProgressLoadResult loaded = store.LoadAll();
                if (!loaded.IsValid)
                {
                    restorationFailed = true;
                    for (int i = 0; i < loaded.Diagnostics.Count; i++)
                    {
                        MissionDiagnostics.Log(
                            "ACG-TOKEN-RESTORE-FAIL accepted=unknown path={0}",
                            loaded.Diagnostics[i]);
                    }
                }

                for (int i = 0; i < loaded.Records.Count; i++)
                {
                    MissionAcgTokenProgressRecord record = loaded.Records[i];
                    int accepted =
                        record.State.AcceptedQuestIdentity.Instance;
                    MissionAcgBindingRecord binding;
                    MissionAcgObjectiveRecord objective;
                    string failure =
                        "Duplicate, orphan, or ownership-mismatched token progress.";
                    if (ByAccepted.ContainsKey(accepted)
                        || !bindingByAccepted.TryGetValue(
                            accepted,
                            out binding)
                        || !MissionAcgObjectiveRuntime.TryGetByAccepted(
                            record.State.Binding.OwnerIdentity.Instance,
                            accepted,
                            out objective)
                        || !record.State.Matches(
                            binding.Binding,
                            objective.Binding,
                            out failure))
                    {
                        restorationFailed = true;
                        InvalidAccepted.Add(accepted);
                        MissionDiagnostics.Log(
                            "ACG-TOKEN-RESTORE-FAIL accepted={0}:{1} path={2} reason={3}",
                            record.State.AcceptedQuestIdentity.Type,
                            accepted,
                            record.RecordPath,
                            string.IsNullOrWhiteSpace(failure)
                                ? "duplicate, orphan, or ownership mismatch"
                                : failure);
                        continue;
                    }

                    ByAccepted.Add(accepted, record);
                }

                initialized = true;

                for (int i = 0; i < restoredBindings.Count; i++)
                {
                    MissionAcgBindingRecord binding = restoredBindings[i];
                    int accepted =
                        binding.Binding.AcceptedQuestIdentity.Instance;
                    MissionAcgTokenProgressRecord current;
                    string failure;
                    if (ByAccepted.TryGetValue(accepted, out current)
                        && !TryMirrorBindingLifecycleLocked(
                            current,
                            binding.State.LifecycleState,
                            out failure))
                    {
                        InvalidAccepted.Add(accepted);
                        MissionDiagnostics.Log(
                            "ACG-TOKEN-RESTORE-FAIL accepted={0}:{1} path={2} reason={3}",
                            binding.Binding.AcceptedQuestIdentity.Type,
                            accepted,
                            current.RecordPath,
                            failure);
                    }
                }

                if (!restorationFailed)
                {
                    for (int i = 0; i < restoredBindings.Count; i++)
                    {
                        MissionAcgBindingRecord binding = restoredBindings[i];
                        if (!IsProgressRestorable(
                                binding.State.LifecycleState))
                        {
                            continue;
                        }

                        MissionAcgObjectiveRecord objective;
                        string failure =
                            "Exact objective state is unavailable.";
                        if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                                binding.Binding.OwnerIdentity.Instance,
                                binding.Binding.AcceptedQuestIdentity.Instance,
                                out objective)
                            || !TryEnsureStateLocked(
                                binding,
                                objective,
                                true,
                                out failure))
                        {
                            InvalidAccepted.Add(
                                binding.Binding.AcceptedQuestIdentity.Instance);
                            MissionDiagnostics.Log(
                                "ACG-TOKEN-RESTORE-FAIL accepted={0}:{1} path={2} reason={3}",
                                binding.Binding.AcceptedQuestIdentity.Type,
                                binding.Binding.AcceptedQuestIdentity.Instance,
                                store.PathFor(
                                    binding.Binding.AcceptedQuestIdentity),
                                failure);
                        }
                    }
                }
            }
        }

        internal static bool TryEnsureState(
            MissionAcgBindingRecord binding,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgObjectiveRecord objective;
                if (!TryResolveExactObjectiveLocked(
                        binding,
                        out objective,
                        out failure))
                {
                    return false;
                }

                return TryEnsureStateLocked(
                    binding,
                    objective,
                    true,
                    out failure);
            }
        }

        internal static bool RegisterCharacter(
            MissionAcgBindingRecord binding,
            int characterInstance,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                if (!TryValidateSoloBinding(binding, out failure)
                    || characterInstance
                       != binding.Binding.OwnerIdentity.Instance)
                {
                    if (string.IsNullOrWhiteSpace(failure))
                    {
                        failure =
                            "Only the exact solo owner can register token progress.";
                    }

                    return false;
                }

                MissionAcgObjectiveRecord objective;
                if (!TryResolveExactObjectiveLocked(
                        binding,
                        out objective,
                        out failure)
                    || !TryEnsureStateLocked(
                        binding,
                        objective,
                        true,
                        out failure))
                {
                    return false;
                }

                int livePlayfield =
                    binding.Binding.AllocatedLivePlayfield2;
                int accepted =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                int existing;
                if (RegisteredPlayfields.TryGetValue(
                        livePlayfield,
                        out existing)
                    && existing != accepted)
                {
                    failure =
                        "Live PF2 already has another token-progress registration.";
                    return false;
                }

                RegisteredPlayfields[livePlayfield] = accepted;
                return true;
            }
        }

        internal static bool TryObserveDeath(
            ICharacter attacker,
            ICharacter victim)
        {
            if (attacker == null
                || victim == null
                || victim.Playfield == null)
            {
                return false;
            }

            int livePlayfield = victim.Playfield.Identity.Instance;
            MissionAcgBindingRecord binding;
            MissionAcgObjectiveRecord objective;
            string failure;
            if (!MissionAcgAllocationService.IsAllocatableRange(livePlayfield)
                || attacker.Playfield == null
                || attacker.Playfield.Identity.Instance != livePlayfield
                || !MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                    livePlayfield,
                    out binding)
                || !TryValidateSoloBinding(binding, out failure)
                || attacker.Identity.Instance
                   != binding.Binding.OwnerIdentity.Instance
                || !MissionAcgObjectiveRuntime.TryGetByAccepted(
                    binding.Binding.OwnerIdentity.Instance,
                    binding.Binding.AcceptedQuestIdentity.Instance,
                    out objective))
            {
                return false;
            }

            if (!MissionInstanceMobCombat.IsAggressive(victim.Identity))
            {
                return false;
            }

            int capturedSlot;
            int spawnGeneration;
            if (!MissionAcgOperationalRuntime.TryResolveTokenProgressSource(
                    binding,
                    victim.Identity,
                    out capturedSlot,
                    out spawnGeneration,
                    out failure))
            {
                return false;
            }

            MissionAcgBindingRecord claimedBinding;
            MissionAcgObjectiveRecord claimedObjective;
            if (!MissionAcgExpiryRuntime.TryClaimTokenProgress(
                    binding,
                    objective,
                    out claimedBinding,
                    out claimedObjective,
                    out failure))
            {
                return false;
            }

            string eventId = string.Empty;
            try
            {
                lock (Sync)
                {
                    if (!TryEnsureStateLocked(
                            claimedBinding,
                            claimedObjective,
                            true,
                            out failure)
                        || !TryApplyDeathLocked(
                            claimedBinding,
                            claimedObjective,
                            victim.Identity,
                            attacker.Identity,
                            capturedSlot,
                            spawnGeneration,
                            out eventId,
                            out failure))
                    {
                        MissionDiagnostics.Log(
                            "ACG-TOKEN-REJECT char={0} accepted={1}:{2} livePf2={3} runtime={4}:{5} reason={6}",
                            attacker.Identity.Instance,
                            claimedBinding.Binding.AcceptedQuestIdentity.Type,
                            claimedBinding.Binding.AcceptedQuestIdentity.Instance,
                            claimedBinding.Binding.AllocatedLivePlayfield2,
                            victim.Identity.Type,
                            victim.Identity.Instance,
                            failure);
                        return false;
                    }
                }
            }
            finally
            {
                MissionAcgExpiryRuntime.ReleaseTokenProgressClaim(
                    claimedBinding.Binding.AcceptedQuestIdentity.Instance);
            }

            TryFlushPendingFeedback(
                attacker,
                claimedBinding.Binding.AcceptedQuestIdentity.Instance);
            return true;
        }

        internal static bool SealGeneratedProgress(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                if (!TryValidateExactObjective(
                        binding,
                        objective,
                        out failure)
                    || !TryEnsureStateLocked(
                        binding,
                        objective,
                        true,
                        out failure))
                {
                    return false;
                }

                int accepted =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                MissionAcgTokenProgressRecord current;
                if (!ByAccepted.TryGetValue(accepted, out current)
                    || current.State.Lifecycle
                       == MissionAcgLifecycleState.Invalid)
                {
                    failure =
                        "Exact valid token-progress state is required before completion.";
                    return false;
                }

                if (current.State.Lifecycle
                    == MissionAcgLifecycleState.CompletionStarted
                    || current.State.Lifecycle
                       == MissionAcgLifecycleState.Completed)
                {
                    return true;
                }

                MissionAcgTokenProgressState sealedState;
                try
                {
                    sealedState =
                        current.State.WithLifecycle(
                            MissionAcgLifecycleState.CompletionStarted,
                            DateTime.UtcNow,
                            "Token progress sealed before objective verification.");
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    return false;
                }

                return TryReplaceLocked(
                    current,
                    sealedState,
                    out failure);
            }
        }

        /// <summary>
        /// Resolves the immutable, durably sealed progress owned by one exact
        /// accepted generated mission. Completion callers must supply the same
        /// binding and objective records; no player-, type-, or newest-mission
        /// lookup participates in this path.
        /// </summary>
        internal static bool TryGetSealedProgress(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            out MissionAcgTokenProgressState progress,
            out string failure)
        {
            progress = null;
            failure = string.Empty;
            EnsureInitialized();
            lock (Sync)
            {
                if (restorationFailed
                    || !TryValidateExactObjective(
                        binding,
                        objective,
                        out failure))
                {
                    if (string.IsNullOrWhiteSpace(failure))
                    {
                        failure =
                            "Token-progress restoration failed closed.";
                    }

                    return false;
                }

                int accepted =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                MissionAcgTokenProgressRecord current;
                if (InvalidAccepted.Contains(accepted)
                    || !ByAccepted.TryGetValue(accepted, out current)
                    || current == null
                    || current.State == null)
                {
                    failure =
                        "Exact valid token-progress state is unavailable for this accepted quest.";
                    return false;
                }

                if (!current.State.Matches(
                        binding.Binding,
                        objective.Binding,
                        out failure))
                {
                    return false;
                }

                if (current.State.Lifecycle
                        != MissionAcgLifecycleState.CompletionStarted
                    && current.State.Lifecycle
                       != MissionAcgLifecycleState.Completed)
                {
                    failure =
                        "Token progress has not been durably sealed for completion.";
                    return false;
                }

                progress = current.State;
                return true;
            }
        }

        internal static void OnBindingStateChanged(
            MissionAcgBindingRecord binding)
        {
            if (binding == null || binding.Binding == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                int accepted =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                MissionAcgTokenProgressRecord current;
                if (!ByAccepted.TryGetValue(accepted, out current)
                    || current.State.Lifecycle
                       == MissionAcgLifecycleState.Invalid
                    || current.State.Lifecycle
                       == binding.State.LifecycleState)
                {
                    return;
                }

                if (!MissionAcgTokenProgressState.CanTransition(
                        current.State.Lifecycle,
                        binding.State.LifecycleState))
                {
                    MissionDiagnostics.Log(
                        "ACG-TOKEN-LIFECYCLE-REJECT accepted={0}:{1} path={2} from={3} to={4}",
                        binding.Binding.AcceptedQuestIdentity.Type,
                        accepted,
                        current.RecordPath,
                        current.State.Lifecycle,
                        binding.State.LifecycleState);
                    return;
                }

                string failure;
                MissionAcgTokenProgressState next;
                try
                {
                    next =
                        current.State.WithLifecycle(
                            binding.State.LifecycleState,
                            DateTime.UtcNow,
                            "Mirrored durable mission lifecycle.");
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    MissionDiagnostics.Log(
                        "ACG-TOKEN-LIFECYCLE-FAIL accepted={0}:{1} path={2} reason={3}",
                        binding.Binding.AcceptedQuestIdentity.Type,
                        accepted,
                        current.RecordPath,
                        failure);
                    return;
                }

                if (!TryReplaceLocked(current, next, out failure))
                {
                    InvalidAccepted.Add(accepted);
                    MissionDiagnostics.Log(
                        "ACG-TOKEN-LIFECYCLE-FAIL accepted={0}:{1} path={2} reason={3}",
                        binding.Binding.AcceptedQuestIdentity.Type,
                        accepted,
                        current.RecordPath,
                        failure);
                }
            }
        }

        internal static void TryResumePendingClientUpdates(
            ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            EnsureInitialized();
            var pendingAccepted = new List<int>();
            lock (Sync)
            {
                foreach (KeyValuePair<int, MissionAcgTokenProgressRecord> entry
                    in ByAccepted)
                {
                    MissionAcgTokenProgressState state = entry.Value.State;
                    if (state.Binding.OwnerIdentity.Instance
                        != character.Identity.Instance)
                    {
                        continue;
                    }

                    for (int i = 0; i < state.DeathEvents.Count; i++)
                    {
                        MissionAcgTokenProgressDeathEvent progressEvent =
                            state.DeathEvents[i];
                        if (progressEvent.Phase
                            == MissionAcgTokenProgressEventPhase
                                .ClientUpdatePending)
                        {
                            pendingAccepted.Add(entry.Key);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < pendingAccepted.Count; i++)
            {
                TryFlushPendingFeedback(
                    character,
                    pendingAccepted[i]);
            }
        }

        internal static void ClearPlayfieldRegistration(int livePlayfield)
        {
            lock (Sync)
            {
                RegisteredPlayfields.Remove(livePlayfield);
            }
        }

        internal static bool HasPlayfieldRegistration(int livePlayfield)
        {
            lock (Sync)
            {
                return RegisteredPlayfields.ContainsKey(livePlayfield);
            }
        }

        private static bool TryEnsureStateLocked(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            bool reconcile,
            out string failure)
        {
            failure = string.Empty;
            if (restorationFailed)
            {
                failure =
                    "Token-progress restoration failed closed.";
                return false;
            }

            if (!TryValidateExactObjective(
                    binding,
                    objective,
                    out failure))
            {
                return false;
            }

            int accepted =
                binding.Binding.AcceptedQuestIdentity.Instance;
            if (InvalidAccepted.Contains(accepted))
            {
                failure =
                    "Token-progress state is invalid for this accepted quest.";
                return false;
            }

            MissionAcgOperationalState operationalState;
            IList<MissionAcgNpcRuntimeState> sources;
            if (!MissionAcgOperationalRuntime.TryEnsureState(
                    binding,
                    out operationalState,
                    out failure)
                || !MissionAcgOperationalRuntime.TryGetTokenProgressSources(
                    binding,
                    out sources,
                    out failure))
            {
                return false;
            }

            MissionAcgTokenProgressRecord current;
            if (!ByAccepted.TryGetValue(accepted, out current))
            {
                bool priorDeath = false;
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i].LifeState
                        == MissionAcgNpcLifeState.Dead)
                    {
                        priorDeath = true;
                        break;
                    }
                }

                MissionAcgTokenProgressState state =
                    priorDeath
                        ? MissionAcgTokenProgressState.CreateInvalid(
                            binding.Binding,
                            objective.Binding,
                            sources.Count,
                            "Legacy active mission has a dead Ambient source but no token-progress sidecar.",
                            DateTime.UtcNow)
                        : MissionAcgTokenProgressState.Create(
                            binding.Binding,
                            objective.Binding,
                            sources.Count,
                            binding.State.LifecycleState,
                            DateTime.UtcNow);
                MissionAcgTokenProgressRecord persisted;
                if (!store.TryCreate(state, out persisted, out failure))
                {
                    InvalidAccepted.Add(accepted);
                    return false;
                }

                ByAccepted.Add(accepted, persisted);
                current = persisted;
                if (priorDeath)
                {
                    InvalidAccepted.Add(accepted);
                    failure =
                        "Legacy active token progress is ambiguous and was rejected.";
                    return false;
                }
            }

            string matchFailure;
            if (!current.State.Matches(
                    binding.Binding,
                    objective.Binding,
                    out matchFailure)
                || current.State.TotalCountableAmbientSlots
                   != sources.Count)
            {
                return TryInvalidateLocked(
                    current,
                    string.IsNullOrWhiteSpace(matchFailure)
                        ? "Frozen Ambient denominator no longer matches operational state."
                        : matchFailure,
                    out failure);
            }

            if (current.State.Lifecycle
                == MissionAcgLifecycleState.Invalid)
            {
                InvalidAccepted.Add(accepted);
                failure =
                    current.State.LifecycleDiagnostic;
                return false;
            }

            if (!TryMirrorBindingLifecycleLocked(
                    current,
                    binding.State.LifecycleState,
                    out failure))
            {
                InvalidAccepted.Add(accepted);
                return false;
            }

            current = ByAccepted[accepted];
            if (!TryValidateEventSources(
                    current,
                    sources,
                    out failure))
            {
                string sourceFailure = failure;
                return TryInvalidateLocked(
                    current,
                    sourceFailure,
                    out failure);
            }

            if (reconcile
                && !TryReconcileStateLocked(
                    binding,
                    objective,
                    sources,
                    out failure))
            {
                return false;
            }

            return true;
        }

        private static bool TryMirrorBindingLifecycleLocked(
            MissionAcgTokenProgressRecord current,
            MissionAcgLifecycleState bindingLifecycle,
            out string failure)
        {
            failure = string.Empty;
            if (current == null
                || current.State == null
                || current.State.Lifecycle == bindingLifecycle
                || current.State.Lifecycle
                   == MissionAcgLifecycleState.Invalid)
            {
                return current != null
                       && current.State != null
                       && current.State.Lifecycle
                          != MissionAcgLifecycleState.Invalid;
            }

            if (current.State.Lifecycle
                    == MissionAcgLifecycleState.CompletionStarted
                && (bindingLifecycle == MissionAcgLifecycleState.Accepted
                    || bindingLifecycle
                       == MissionAcgLifecycleState.Active))
            {
                // Completion sealing is intentionally durable before the binding
                // and objective transition. Never regress that crash boundary.
                return true;
            }

            if (!MissionAcgTokenProgressState.CanTransition(
                    current.State.Lifecycle,
                    bindingLifecycle))
            {
                failure =
                    "Token-progress lifecycle cannot reconcile from "
                    + current.State.Lifecycle
                    + " to durable binding state "
                    + bindingLifecycle
                    + ".";
                return false;
            }

            MissionAcgTokenProgressState mirrored;
            try
            {
                mirrored =
                    current.State.WithLifecycle(
                        bindingLifecycle,
                        DateTime.UtcNow,
                        "Reconciled durable binding lifecycle on restore.");
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }

            return TryReplaceLocked(current, mirrored, out failure);
        }

        private static bool TryApplyDeathLocked(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            Identity sourceRuntimeIdentity,
            Identity actorIdentity,
            int capturedSlot,
            int spawnGeneration,
            out string eventId,
            out string failure)
        {
            eventId = string.Empty;
            failure = string.Empty;
            int accepted =
                binding.Binding.AcceptedQuestIdentity.Instance;
            MissionAcgTokenProgressRecord current;
            if (!ByAccepted.TryGetValue(accepted, out current)
                || !current.State.CanAcceptDeaths)
            {
                failure =
                    "Token-progress lifecycle cannot accept a death event.";
                return false;
            }

            var source =
                new MissionAcgIdentityRecord(
                    (int)sourceRuntimeIdentity.Type,
                    sourceRuntimeIdentity.Instance);
            var actor =
                new MissionAcgIdentityRecord(
                    (int)actorIdentity.Type,
                    actorIdentity.Instance);
            MissionAcgTokenProgressDeathEvent existing;
            if (!current.State.TryGetEvent(
                    source,
                    capturedSlot,
                    spawnGeneration,
                    out existing))
            {
                MissionAcgTokenProgressState validated;
                try
                {
                    validated =
                        current.State.AddValidatedDeath(
                            source,
                            actor,
                            capturedSlot,
                            spawnGeneration,
                            DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    return false;
                }

                if (!TryReplaceLocked(
                        current,
                        validated,
                        out failure))
                {
                    return false;
                }

                current = ByAccepted[accepted];
                if (!current.State.TryGetEvent(
                        source,
                        capturedSlot,
                        spawnGeneration,
                        out existing))
                {
                    failure =
                        "Durable token event could not be resolved after validation.";
                    return false;
                }
            }
            else if (!existing.ActorIdentity.Equals(actor))
            {
                failure =
                    "Duplicate token event actor does not match its exact owner.";
                return false;
            }

            eventId = existing.EventId;
            return TryResumeEventLocked(
                accepted,
                eventId,
                out failure);
        }

        private static bool TryReconcileStateLocked(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            IList<MissionAcgNpcRuntimeState> sources,
            out string failure)
        {
            failure = string.Empty;
            int accepted =
                binding.Binding.AcceptedQuestIdentity.Instance;
            MissionAcgTokenProgressRecord current =
                ByAccepted[accepted];

            for (int i = 0; i < current.State.DeathEvents.Count; i++)
            {
                MissionAcgTokenProgressDeathEvent progressEvent =
                    current.State.DeathEvents[i];
                if ((progressEvent.Phase
                     == MissionAcgTokenProgressEventPhase.Validated
                     || progressEvent.Phase
                        == MissionAcgTokenProgressEventPhase
                            .DurablyApplied)
                    && !TryResumeEventLocked(
                        accepted,
                        progressEvent.EventId,
                        out failure))
                {
                    return false;
                }
            }

            current = ByAccepted[accepted];
            for (int i = 0; i < sources.Count; i++)
            {
                MissionAcgNpcRuntimeState source = sources[i];
                if (source.LifeState != MissionAcgNpcLifeState.Dead)
                {
                    continue;
                }

                MissionAcgTokenProgressDeathEvent existing;
                if (current.State.TryGetEvent(
                        source.RuntimeIdentity,
                        source.CapturedSlot,
                        source.SpawnGeneration,
                        out existing))
                {
                    continue;
                }

                if (!current.State.CanAcceptDeaths)
                {
                    failure =
                        "A dead Ambient source is missing from sealed token progress.";
                    return false;
                }

                string eventId;
                if (!TryApplyDeathLocked(
                        binding,
                        objective,
                        ToIdentity(source.RuntimeIdentity),
                        ToIdentity(binding.Binding.OwnerIdentity),
                        source.CapturedSlot,
                        source.SpawnGeneration,
                        out eventId,
                        out failure))
                {
                    return false;
                }

                current = ByAccepted[accepted];
            }

            return true;
        }

        private static bool TryResumeEventLocked(
            int accepted,
            string eventId,
            out string failure)
        {
            failure = string.Empty;
            while (true)
            {
                MissionAcgTokenProgressRecord current;
                if (!ByAccepted.TryGetValue(accepted, out current))
                {
                    failure =
                        "Token-progress record disappeared during recovery.";
                    return false;
                }

                MissionAcgTokenProgressDeathEvent progressEvent = null;
                for (int i = 0; i < current.State.DeathEvents.Count; i++)
                {
                    if (string.Equals(
                        current.State.DeathEvents[i].EventId,
                        eventId,
                        StringComparison.Ordinal))
                    {
                        progressEvent = current.State.DeathEvents[i];
                        break;
                    }
                }

                if (progressEvent == null)
                {
                    failure =
                        "Token-progress event disappeared during recovery.";
                    return false;
                }

                MissionAcgTokenProgressEventPhase nextPhase;
                if (progressEvent.Phase
                    == MissionAcgTokenProgressEventPhase.Validated)
                {
                    nextPhase =
                        MissionAcgTokenProgressEventPhase.DurablyApplied;
                }
                else if (progressEvent.Phase
                         == MissionAcgTokenProgressEventPhase
                             .DurablyApplied)
                {
                    nextPhase =
                        MissionAcgTokenProgressEventPhase
                            .ClientUpdatePending;
                }
                else
                {
                    return true;
                }

                MissionAcgTokenProgressState next;
                try
                {
                    next =
                        current.State.AdvanceDeath(
                            eventId,
                            nextPhase,
                            DateTime.UtcNow,
                            string.Empty);
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                    return false;
                }

                if (!TryReplaceLocked(current, next, out failure))
                {
                    return false;
                }
            }
        }

        private static bool TrySendPendingFeedback(
            ICharacter character,
            int accepted,
            string eventId)
        {
            int percent;
            lock (Sync)
            {
                MissionAcgTokenProgressRecord current;
                MissionAcgTokenProgressDeathEvent progressEvent;
                if (character == null
                    || !ByAccepted.TryGetValue(accepted, out current)
                    || current.State.Binding.OwnerIdentity.Instance
                       != character.Identity.Instance
                    || !TryFindEvent(
                        current.State,
                        eventId,
                        out progressEvent)
                    || progressEvent.Phase
                       != MissionAcgTokenProgressEventPhase
                           .ClientUpdatePending)
                {
                    return false;
                }

                percent = progressEvent.PercentAfter;
            }

            try
            {
                character.Send(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        Unknown2 = 0,
                        FormattedMessage =
                            TokenBoardRuntime.ToYellowSystemFeedback(
                                percent >= 100
                                    ? "Mission chance of token reward upped to 100% due to your heroic effort."
                                    : string.Format(
                                        "Mission chance of token reward upped to {0}%.",
                                        percent))
                    });
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "ACG-TOKEN-FEEDBACK-PENDING char={0} accepted={1} event={2} reason={3}",
                    character.Identity.Instance,
                    accepted,
                    eventId,
                    ex.Message);
                return false;
            }

            lock (Sync)
            {
                MissionAcgTokenProgressRecord current;
                MissionAcgTokenProgressDeathEvent progressEvent;
                if (!ByAccepted.TryGetValue(accepted, out current)
                    || !TryFindEvent(
                        current.State,
                        eventId,
                        out progressEvent)
                    || progressEvent.Phase
                       == MissionAcgTokenProgressEventPhase
                           .ClientUpdateSent)
                {
                    return true;
                }

                if (progressEvent.Phase
                    != MissionAcgTokenProgressEventPhase
                        .ClientUpdatePending)
                {
                    return false;
                }

                string failure;
                MissionAcgTokenProgressState sent =
                    current.State.AdvanceDeath(
                        eventId,
                        MissionAcgTokenProgressEventPhase.ClientUpdateSent,
                        DateTime.UtcNow,
                        string.Empty);
                if (!TryReplaceLocked(current, sent, out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-TOKEN-FEEDBACK-PENDING char={0} accepted={1} event={2} reason={3}",
                        character.Identity.Instance,
                        accepted,
                        eventId,
                        failure);
                    return false;
                }

                MissionDiagnostics.Log(
                    "ACG-TOKEN-PCT char={0} accepted={1}:{2} livePf2={3} percent={4} delivery=server-sent",
                    character.Identity.Instance,
                    current.State.AcceptedQuestIdentity.Type,
                    current.State.AcceptedQuestIdentity.Instance,
                    current.State.Binding.AllocatedLivePlayfield2,
                    percent);
                return true;
            }
        }

        private static void TryFlushPendingFeedback(
            ICharacter character,
            int accepted)
        {
            while (true)
            {
                string eventId = string.Empty;
                int sequence = int.MaxValue;
                lock (Sync)
                {
                    MissionAcgTokenProgressRecord current;
                    if (character == null
                        || !ByAccepted.TryGetValue(accepted, out current)
                        || current.State.Binding.OwnerIdentity.Instance
                           != character.Identity.Instance)
                    {
                        return;
                    }

                    for (int i = 0; i < current.State.DeathEvents.Count; i++)
                    {
                        MissionAcgTokenProgressDeathEvent progressEvent =
                            current.State.DeathEvents[i];
                        if (progressEvent.Phase
                                == MissionAcgTokenProgressEventPhase
                                    .ClientUpdatePending
                            && progressEvent.Sequence < sequence)
                        {
                            sequence = progressEvent.Sequence;
                            eventId = progressEvent.EventId;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(eventId)
                    || !TrySendPendingFeedback(
                        character,
                        accepted,
                        eventId))
                {
                    return;
                }
            }
        }

        private static bool TryResolveExactObjectiveLocked(
            MissionAcgBindingRecord binding,
            out MissionAcgObjectiveRecord objective,
            out string failure)
        {
            objective = null;
            failure = string.Empty;
            if (!TryValidateSoloBinding(binding, out failure)
                || !MissionAcgObjectiveRuntime.TryGetByAccepted(
                    binding.Binding.OwnerIdentity.Instance,
                    binding.Binding.AcceptedQuestIdentity.Instance,
                    out objective))
            {
                if (string.IsNullOrWhiteSpace(failure))
                {
                    failure =
                        "Exact objective binding is required for token progress.";
                }

                return false;
            }

            return TryValidateExactObjective(
                binding,
                objective,
                out failure);
        }

        private static bool TryValidateExactObjective(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            failure = string.Empty;
            if (!TryValidateSoloBinding(binding, out failure)
                || objective == null
                || objective.Binding == null
                || !objective.Binding.AcceptedQuestIdentity.Equals(
                    binding.Binding.AcceptedQuestIdentity)
                || !objective.Binding.OwnerIdentity.Equals(
                    binding.Binding.OwnerIdentity)
                || !objective.Binding.ExplicitNoTeam
                || objective.Binding.TeamIdentity != null
                || objective.Binding.MissionType
                   != binding.Binding.MissionType
                || objective.Binding.AllocatedLivePlayfield2
                   != binding.Binding.AllocatedLivePlayfield2)
            {
                if (string.IsNullOrWhiteSpace(failure))
                {
                    failure =
                        "Token progress requires the exact solo mission objective.";
                }

                return false;
            }

            return true;
        }

        private static bool TryValidateSoloBinding(
            MissionAcgBindingRecord binding,
            out string failure)
        {
            failure = string.Empty;
            if (binding == null
                || binding.Binding == null
                || binding.State == null
                || !binding.Binding.ExplicitNoTeam
                || binding.Binding.TeamIdentity != null
                || !MissionAcgAllocationService.IsAllocatableRange(
                    binding.Binding.AllocatedLivePlayfield2))
            {
                failure =
                    "Generated token progress currently requires an exact solo binding.";
                return false;
            }

            return true;
        }

        private static bool TryValidateEventSources(
            MissionAcgTokenProgressRecord record,
            IList<MissionAcgNpcRuntimeState> sources,
            out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < record.State.DeathEvents.Count; i++)
            {
                MissionAcgTokenProgressDeathEvent progressEvent =
                    record.State.DeathEvents[i];
                MissionAcgNpcRuntimeState source = null;
                for (int j = 0; j < sources.Count; j++)
                {
                    if (sources[j].RuntimeIdentity.Equals(
                            progressEvent.SourceRuntimeIdentity)
                        && sources[j].CapturedSlot
                           == progressEvent.CapturedSlot
                        && sources[j].SpawnGeneration
                           == progressEvent.SpawnGeneration)
                    {
                        source = sources[j];
                        break;
                    }
                }

                if (source == null
                    || source.Role != MissionAcgNpcRole.Ambient
                    || !source.IsMaterializable
                    || source.CleanupCompleted
                    || source.LifeState != MissionAcgNpcLifeState.Dead)
                {
                    failure =
                        "Persisted token event does not match an exact dead Ambient source.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryInvalidateLocked(
            MissionAcgTokenProgressRecord current,
            string diagnostic,
            out string failure)
        {
            failure =
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "Token-progress state failed exact validation."
                    : diagnostic;
            int accepted =
                current.State.AcceptedQuestIdentity.Instance;
            InvalidAccepted.Add(accepted);
            if (current.State.Lifecycle
                == MissionAcgLifecycleState.Invalid)
            {
                return false;
            }

            try
            {
                MissionAcgTokenProgressState invalid =
                    current.State.WithLifecycle(
                        MissionAcgLifecycleState.Invalid,
                        DateTime.UtcNow,
                        failure);
                string persistFailure;
                if (!TryReplaceLocked(
                        current,
                        invalid,
                        out persistFailure))
                {
                    failure =
                        failure + " Durable invalidation failed: "
                        + persistFailure;
                }
            }
            catch (Exception ex)
            {
                failure =
                    failure + " Durable invalidation failed: " + ex.Message;
            }

            return false;
        }

        private static bool TryReplaceLocked(
            MissionAcgTokenProgressRecord current,
            MissionAcgTokenProgressState state,
            out string failure)
        {
            MissionAcgTokenProgressRecord persisted;
            if (!store.TryReplace(
                    current.WithState(state),
                    out persisted,
                    out failure))
            {
                return false;
            }

            ByAccepted[state.AcceptedQuestIdentity.Instance] = persisted;
            return true;
        }

        private static bool TryFindEvent(
            MissionAcgTokenProgressState state,
            string eventId,
            out MissionAcgTokenProgressDeathEvent progressEvent)
        {
            progressEvent = null;
            if (state == null || string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }

            for (int i = 0; i < state.DeathEvents.Count; i++)
            {
                if (string.Equals(
                    state.DeathEvents[i].EventId,
                    eventId,
                    StringComparison.Ordinal))
                {
                    progressEvent = state.DeathEvents[i];
                    return true;
                }
            }

            return false;
        }

        private static bool IsProgressRestorable(
            MissionAcgLifecycleState lifecycle)
        {
            return lifecycle == MissionAcgLifecycleState.Accepted
                   || lifecycle == MissionAcgLifecycleState.Active
                   || lifecycle
                      == MissionAcgLifecycleState.CompletionStarted;
        }

        private static Identity ToIdentity(
            MissionAcgIdentityRecord identity)
        {
            return new Identity
                   {
                       Type = (IdentityType)identity.Type,
                       Instance = identity.Instance
                   };
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
