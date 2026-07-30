namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Process-wide wall-clock expiry authority for generated terminal missions.
    /// The persisted binding supplies the absolute deadline; this runtime never
    /// recomputes or extends it.
    /// </summary>
    internal static class MissionAcgExpiryRuntime
    {
        private const int ScanPeriodMilliseconds = 1000;
        private const int RetryPersistenceIntervalSeconds = 30;

        private static readonly object Gate = new object();
        private static readonly ManualResetEvent Idle = new ManualResetEvent(true);

        private static readonly Dictionary<int, ExpiryContext> ByAccepted =
            new Dictionary<int, ExpiryContext>();

        private static readonly Dictionary<int, DateTime> RetryAfterUtc =
            new Dictionary<int, DateTime>();

        private static readonly HashSet<int> InFlight = new HashSet<int>();

        private static readonly HashSet<int> ExpiryClaims = new HashSet<int>();

        private static readonly HashSet<int> CompletionClaims = new HashSet<int>();

        private static readonly HashSet<int> CompletionTransitionClaims =
            new HashSet<int>();

        private static readonly HashSet<int> CompletionOwned = new HashSet<int>();

        private static readonly HashSet<int> AbandonmentClaims = new HashSet<int>();

        private static readonly HashSet<int> AbandonmentOwned = new HashSet<int>();

        private static readonly Dictionary<int, MissionAcgBindingRecord>
            PendingBindingUpdates =
                new Dictionary<int, MissionAcgBindingRecord>();

        private static MissionAcgExpiryStateStore store;

        private static Timer timer;

        private static bool initialized;

        private static bool stopping;

        private static int scanRunning;

        internal static void Initialize(
            IList<MissionAcgBindingRecord> bindings,
            string missionStateDirectory)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException("bindings");
            }

            var expiryStore =
                new MissionAcgExpiryStateStore(missionStateDirectory);
            MissionAcgExpiryLoadResult loaded = expiryStore.LoadAll(bindings);
            if (!loaded.IsValid)
            {
                throw new InvalidOperationException(
                    "Mission expiry restoration failed closed: "
                    + string.Join(" | ", loaded.Diagnostics));
            }

            var journalByAccepted =
                new Dictionary<int, MissionAcgExpiryRecord>();
            for (int i = 0; i < loaded.Records.Count; i++)
            {
                MissionAcgExpiryRecord record = loaded.Records[i];
                journalByAccepted.Add(
                    record.State.AcceptedQuestIdentity.Instance,
                    record);
            }

            MissionAcgAllocationService expiryAllocator =
                MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery;
            for (int i = 0; i < bindings.Count; i++)
            {
                MissionAcgBindingRecord binding = bindings[i];
                MissionAcgExpiryRecord journal;
                if (!journalByAccepted.TryGetValue(
                        binding.Binding.AcceptedQuestIdentity.Instance,
                        out journal)
                    || journal.State.IsComplete
                    || binding.State.LifecycleState
                       != MissionAcgLifecycleState.Cleaned)
                {
                    continue;
                }

                if (!journal.State.HasCheckpoint(
                    MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted))
                {
                    throw new InvalidOperationException(
                        "Cleaned expiry binding lacks durable PF2 release intent for "
                        + IdentityKey(binding.Binding.AcceptedQuestIdentity)
                        + " at "
                        + journal.RecordPath
                        + ".");
                }

                string releaseHoldFailure;
                if (!expiryAllocator
                    .TryRestoreReleasePendingJournalConfirmation(
                        binding,
                        out releaseHoldFailure))
                {
                    throw new InvalidOperationException(
                        "Mission expiry PF2 release hold restoration failed for "
                        + IdentityKey(binding.Binding.AcceptedQuestIdentity)
                        + " at "
                        + journal.RecordPath
                        + ": "
                        + releaseHoldFailure);
                }
            }

            lock (Gate)
            {
                if (initialized)
                {
                    return;
                }

                store = expiryStore;
                for (int i = 0; i < bindings.Count; i++)
                {
                    MissionAcgBindingRecord binding = bindings[i];
                    MissionAcgExpiryRecord journal;
                    journalByAccepted.TryGetValue(
                        binding.Binding.AcceptedQuestIdentity.Instance,
                        out journal);
                    if (journal != null)
                    {
                        MissionAcgObjectiveRecord objective;
                        if (MissionAcgObjectiveRuntime.TryGetByAccepted(
                                binding.Binding.OwnerIdentity.Instance,
                                binding.Binding.AcceptedQuestIdentity.Instance,
                                out objective)
                            && MissionAcgExpiryPolicy.IsCompletionOwned(
                                objective.State.Phase))
                        {
                            throw new InvalidOperationException(
                                "Expiry and durable reward claim both own accepted quest "
                                + IdentityKey(
                                    binding.Binding.AcceptedQuestIdentity)
                                + " at "
                                + journal.RecordPath
                                + ".");
                        }
                    }

                    ByAccepted.Add(
                        binding.Binding.AcceptedQuestIdentity.Instance,
                        new ExpiryContext(binding, journal));
                }

                foreach (KeyValuePair<int, MissionAcgBindingRecord> pending
                    in PendingBindingUpdates)
                {
                    ExpiryContext existing;
                    if (ByAccepted.TryGetValue(pending.Key, out existing))
                    {
                        existing.BindingRecord = pending.Value;
                    }
                    else
                    {
                        ByAccepted.Add(
                            pending.Key,
                            new ExpiryContext(pending.Value, null));
                    }
                }

                PendingBindingUpdates.Clear();
                stopping = false;
                initialized = true;
            }

            ProcessAllDue(DateTime.UtcNow);
        }

        internal static void Start()
        {
            lock (Gate)
            {
                if (!initialized || timer != null)
                {
                    return;
                }

                stopping = false;
                timer =
                    new Timer(
                        Scan,
                        null,
                        0,
                        ScanPeriodMilliseconds);
            }
        }

        internal static void Stop()
        {
            Timer current;
            lock (Gate)
            {
                stopping = true;
                current = timer;
                timer = null;
            }

            if (current != null)
            {
                using (var drained = new ManualResetEvent(false))
                {
                    if (current.Dispose(drained))
                    {
                        drained.WaitOne();
                    }
                }
            }

            Idle.WaitOne();
        }

        internal static void OnBindingCreated(MissionAcgBindingRecord record)
        {
            if (record == null)
            {
                return;
            }

            lock (Gate)
            {
                if (!initialized)
                {
                    PendingBindingUpdates[
                        record.Binding.AcceptedQuestIdentity.Instance] = record;
                    return;
                }

                int accepted =
                    record.Binding.AcceptedQuestIdentity.Instance;
                RetryAfterUtc.Remove(accepted);
                CompletionOwned.Remove(accepted);
                CompletionTransitionClaims.Remove(accepted);
                AbandonmentClaims.Remove(accepted);
                AbandonmentOwned.Remove(accepted);
                ExpiryContext existing;
                if (ByAccepted.TryGetValue(accepted, out existing))
                {
                    existing.BindingRecord = record;
                    return;
                }

                ByAccepted.Add(accepted, new ExpiryContext(record, null));
            }
        }

        internal static void OnBindingStateChanged(
            MissionAcgBindingRecord record)
        {
            if (record == null)
            {
                return;
            }

            lock (Gate)
            {
                if (!initialized)
                {
                    PendingBindingUpdates[
                        record.Binding.AcceptedQuestIdentity.Instance] = record;
                    return;
                }

                ExpiryContext context;
                if (ByAccepted.TryGetValue(
                        record.Binding.AcceptedQuestIdentity.Instance,
                        out context))
                {
                    int accepted =
                        record.Binding.AcceptedQuestIdentity.Instance;
                    context.BindingRecord = record;
                    RetryAfterUtc.Remove(accepted);
                    if (record.State.LifecycleState
                        == MissionAcgLifecycleState.Abandoned)
                    {
                        AbandonmentClaims.Remove(accepted);
                        AbandonmentOwned.Add(accepted);
                    }
                }
            }
        }

        internal static bool CanBeginObjectiveAction(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            MissionAcgBindingRecord current;
            if (!TryResolveExactCurrent(
                suppliedBinding,
                objective,
                out current,
                out failure))
            {
                return false;
            }

            bool blocked;
            lock (Gate)
            {
                ExpiryContext context;
                blocked =
                    !initialized
                    || !ByAccepted.TryGetValue(
                        current.Binding.AcceptedQuestIdentity.Instance,
                        out context)
                    || context.Journal != null
                    || ExpiryClaims.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || CompletionTransitionClaims.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || AbandonmentClaims.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || AbandonmentOwned.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || MissionAcgExpiryPolicy.BlocksNewAction(
                        current.State,
                        context == null ? null : context.Journal == null
                            ? null
                            : context.Journal.State,
                        nowUtc,
                        current.Binding.ExpiryUtc);
            }

            if (!blocked)
            {
                return true;
            }

            failure = "Accepted mission is expired or cleanup-owned.";
            QueueImmediate(current.Binding.AcceptedQuestIdentity.Instance);
            return false;
        }

        internal static bool CanContinueCompletion(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            MissionAcgBindingRecord current;
            if (!TryResolveExactCurrent(
                suppliedBinding,
                objective,
                out current,
                out failure))
            {
                return false;
            }

            lock (Gate)
            {
                ExpiryContext context;
                if (!initialized
                    || !ByAccepted.TryGetValue(
                        current.Binding.AcceptedQuestIdentity.Instance,
                        out context)
                    || CompletionTransitionClaims.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || AbandonmentClaims.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || AbandonmentOwned.Contains(
                        current.Binding.AcceptedQuestIdentity.Instance)
                    || !MissionAcgExpiryPolicy.CanContinueCompletion(
                        objective.State.Phase,
                        CompletionClaims.Contains(
                            current.Binding.AcceptedQuestIdentity.Instance),
                        context.Journal == null
                            ? null
                            : context.Journal.State,
                        nowUtc,
                        current.Binding.ExpiryUtc))
                {
                    failure =
                        "Expiry owns the accepted mission before reward claim.";
                    return false;
                }
            }

            return true;
        }

        internal static bool TryClaimCompletionTransition(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            out MissionAcgBindingRecord claimedBinding,
            out string failure)
        {
            claimedBinding = null;
            failure = string.Empty;
            MissionAcgBindingRecord current;
            if (!TryResolveExactCurrent(
                suppliedBinding,
                objective,
                out current,
                out failure))
            {
                return false;
            }

            int accepted =
                current.Binding.AcceptedQuestIdentity.Instance;
            lock (Gate)
            {
                ExpiryContext context;
                if (!initialized
                    || !ByAccepted.TryGetValue(accepted, out context)
                    || !SameBindingIdentity(
                        context.BindingRecord,
                        current)
                    || context.Journal != null
                    || ExpiryClaims.Contains(accepted)
                    || AbandonmentClaims.Contains(accepted)
                    || AbandonmentOwned.Contains(accepted)
                    || CompletionClaims.Contains(accepted)
                    || CompletionOwned.Contains(accepted)
                    || MissionAcgExpiryPolicy.IsDue(
                        DateTime.UtcNow,
                        current.Binding.ExpiryUtc)
                    || (current.State.LifecycleState
                        != MissionAcgLifecycleState.Accepted
                        && current.State.LifecycleState
                           != MissionAcgLifecycleState.Active
                        && current.State.LifecycleState
                           != MissionAcgLifecycleState.CompletionStarted)
                    || objective.State.Phase
                       != MissionAcgCompletionPhase.ObjectiveVerified)
                {
                    failure =
                        "Another terminal lifecycle owns the completion transition.";
                    return false;
                }

                if (!CompletionTransitionClaims.Add(accepted))
                {
                    failure =
                        "A completion transition is already in progress.";
                    return false;
                }
            }

            claimedBinding = current;
            return true;
        }

        internal static void ReleaseCompletionTransitionClaim(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                CompletionTransitionClaims.Remove(acceptedQuestInstance);
            }
        }

        internal static bool TryClaimCompletionReward(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            failure = string.Empty;
            MissionAcgBindingRecord current;
            if (!TryResolveExactCurrent(
                suppliedBinding,
                objective,
                out current,
                out failure))
            {
                return false;
            }

            int accepted =
                current.Binding.AcceptedQuestIdentity.Instance;
            lock (Gate)
            {
                ExpiryContext context;
                if (!initialized
                    || !ByAccepted.TryGetValue(accepted, out context)
                    || context.Journal != null
                    || ExpiryClaims.Contains(accepted)
                    || CompletionOwned.Contains(accepted)
                    || CompletionTransitionClaims.Contains(accepted)
                    || AbandonmentClaims.Contains(accepted)
                    || AbandonmentOwned.Contains(accepted)
                    || MissionAcgExpiryPolicy.IsDue(
                        DateTime.UtcNow,
                        current.Binding.ExpiryUtc)
                    || current.State.LifecycleState
                       != MissionAcgLifecycleState.CompletionStarted
                    || objective.State.Phase
                       != MissionAcgCompletionPhase.RewardCalculationFrozen)
                {
                    failure =
                        "Expiry won before the durable reward-claim boundary.";
                    return false;
                }

                if (!CompletionClaims.Add(accepted))
                {
                    failure =
                        "A reward claim is already in progress for this mission.";
                    return false;
                }
            }

            return true;
        }

        internal static void ConfirmCompletionRewardClaim(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                CompletionClaims.Remove(acceptedQuestInstance);
                CompletionOwned.Add(acceptedQuestInstance);
            }
        }

        internal static void ReleaseCompletionRewardClaim(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                CompletionClaims.Remove(acceptedQuestInstance);
            }
        }

        internal static bool TryClaimAbandonment(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            out bool newlyClaimed,
            out string failure)
        {
            newlyClaimed = false;
            failure = string.Empty;
            MissionAcgBindingRecord current;
            if (!TryResolveExactCurrent(
                suppliedBinding,
                objective,
                out current,
                out failure))
            {
                return false;
            }

            int accepted =
                current.Binding.AcceptedQuestIdentity.Instance;
            lock (Gate)
            {
                ExpiryContext context;
                if (!initialized
                    || !ByAccepted.TryGetValue(accepted, out context)
                    || !SameBindingIdentity(
                        context.BindingRecord,
                        current))
                {
                    failure =
                        "Expiry authority does not own the exact current binding.";
                    return false;
                }

                bool abandonmentOwned =
                    AbandonmentOwned.Contains(accepted);
                bool expiryOwned =
                    context.Journal != null
                    || ExpiryClaims.Contains(accepted);
                bool completionOwned =
                    CompletionClaims.Contains(accepted)
                    || CompletionTransitionClaims.Contains(accepted)
                    || CompletionOwned.Contains(accepted)
                    || MissionAcgExpiryPolicy.IsCompletionOwned(
                        objective.State.Phase);

                bool durableAbandonment =
                    abandonmentOwned
                    || current.State.LifecycleState
                       == MissionAcgLifecycleState.Abandoned
                    || (current.State.LifecycleState
                        == MissionAcgLifecycleState.CleanupPending
                        || current.State.LifecycleState
                           == MissionAcgLifecycleState.Cleaned)
                       && objective.State.Phase
                          < MissionAcgCompletionPhase.RewardClaimStarted
                       && (objective.State.Lifecycle
                           == MissionAcgObjectiveLifecycle.Abandoned
                           || objective.State.Lifecycle
                              == MissionAcgObjectiveLifecycle.CleanupCompleted);
                if (durableAbandonment
                    && !expiryOwned
                    && !completionOwned)
                {
                    AbandonmentOwned.Add(accepted);
                    return true;
                }

                DateTime nowUtc = DateTime.UtcNow;
                if (!MissionAcgExpiryPolicy.CanBeginAbandonment(
                    current.State.LifecycleState,
                    objective.State.Phase,
                    expiryOwned,
                    completionOwned,
                    abandonmentOwned,
                    nowUtc,
                    current.Binding.ExpiryUtc))
                {
                    failure = expiryOwned
                                  ? "Expiry already owns the accepted mission."
                                  : completionOwned
                                        ? "Completion already owns the accepted mission."
                                        : MissionAcgExpiryPolicy.IsDue(
                                            nowUtc,
                                            current.Binding.ExpiryUtc)
                                              ? "Expiry owns the accepted mission at its deadline."
                                              : "Accepted mission is not in an abandonable lifecycle.";
                    return false;
                }

                if (!AbandonmentClaims.Add(accepted))
                {
                    failure =
                        "Another abandonment claim is already in progress.";
                    return false;
                }

                newlyClaimed = true;
                return true;
            }
        }

        internal static void ConfirmAbandonmentClaim(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                AbandonmentClaims.Remove(acceptedQuestInstance);
                AbandonmentOwned.Add(acceptedQuestInstance);
            }
        }

        internal static void ReleaseAbandonmentClaim(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                AbandonmentClaims.Remove(acceptedQuestInstance);
            }
        }

        internal static bool IsReleaseReady(
            MissionAcgBindingRecord binding,
            out string failure)
        {
            failure = string.Empty;
            if (binding == null)
            {
                failure = "Binding is required for PF2 release.";
                return false;
            }

            lock (Gate)
            {
                if (!initialized)
                {
                    failure = "Expiry authority is not initialized.";
                    return false;
                }

                ExpiryContext context;
                if (!ByAccepted.TryGetValue(
                        binding.Binding.AcceptedQuestIdentity.Instance,
                        out context)
                    || context.Journal == null)
                {
                    return true;
                }

                if (!context.Journal.State.MatchesBinding(
                        binding.Binding,
                        out failure)
                    || !MissionAcgExpiryPolicy.HasVerifiedReleaseCheckpoints(
                        context.Journal.State))
                {
                    if (failure.Length == 0)
                    {
                        failure =
                            "Expiry cleanup journal has not verified PF2 release.";
                    }

                    return false;
                }

                return true;
            }
        }

        internal static bool OwnsCleanup(MissionAcgBindingRecord binding)
        {
            if (binding == null)
            {
                return false;
            }

            lock (Gate)
            {
                ExpiryContext context;
                return initialized
                       && ByAccepted.TryGetValue(
                           binding.Binding.AcceptedQuestIdentity.Instance,
                           out context)
                       && context.Journal != null;
            }
        }

        internal static void ProcessForCharacter(
            IZoneClient client,
            ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            List<int> accepted = new List<int>();
            DateTime nowUtc = DateTime.UtcNow;
            lock (Gate)
            {
                if (!initialized || stopping)
                {
                    return;
                }

                foreach (KeyValuePair<int, ExpiryContext> pair in ByAccepted)
                {
                    if (pair.Value.BindingRecord.Binding.OwnerIdentity.Instance
                        == character.Identity.Instance
                        && HasRunnableWork(pair.Value, nowUtc))
                    {
                        RetryAfterUtc.Remove(pair.Key);
                        accepted.Add(pair.Key);
                    }
                }
            }

            for (int i = 0; i < accepted.Count; i++)
            {
                ProcessAccepted(accepted[i]);
            }
        }

        private static void Scan(object ignored)
        {
            if (Interlocked.Exchange(ref scanRunning, 1) != 0)
            {
                return;
            }

            try
            {
                ProcessAllDue(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "ACG-EXPIRY-SCAN-FAIL reason={0}",
                    ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref scanRunning, 0);
            }
        }

        private static void ProcessAllDue(DateTime nowUtc)
        {
            var accepted = new List<int>();
            lock (Gate)
            {
                if (!initialized || stopping)
                {
                    return;
                }

                foreach (KeyValuePair<int, ExpiryContext> pair in ByAccepted)
                {
                    if (!HasRunnableWork(pair.Value, nowUtc))
                    {
                        continue;
                    }

                    DateTime retryAfter;
                    if (RetryAfterUtc.TryGetValue(pair.Key, out retryAfter)
                        && retryAfter > nowUtc)
                    {
                        continue;
                    }

                    accepted.Add(pair.Key);
                }
            }

            for (int i = 0; i < accepted.Count; i++)
            {
                ProcessAccepted(accepted[i]);
            }
        }

        private static void ProcessAccepted(int acceptedQuestInstance)
        {
            ExpiryContext context;
            lock (Gate)
            {
                if (!initialized
                    || stopping
                    || !ByAccepted.TryGetValue(
                        acceptedQuestInstance,
                        out context)
                    || context.Journal != null
                       && context.Journal.State.IsComplete
                    || !InFlight.Add(acceptedQuestInstance))
                {
                    return;
                }

                Idle.Reset();
            }

            try
            {
                ProcessAcceptedCore(acceptedQuestInstance);
            }
            catch (Exception ex)
            {
                RecordRetry(
                    acceptedQuestInstance,
                    ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                lock (Gate)
                {
                    InFlight.Remove(acceptedQuestInstance);
                    ExpiryClaims.Remove(acceptedQuestInstance);
                    if (InFlight.Count == 0)
                    {
                        Idle.Set();
                    }
                }
            }
        }

        private static void ProcessAcceptedCore(int acceptedQuestInstance)
        {
            ExpiryContext context = SnapshotContext(acceptedQuestInstance);
            if (context == null)
            {
                return;
            }

            MissionAcgBindingRecord binding = context.BindingRecord;
            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                binding.Binding.OwnerIdentity.Instance,
                acceptedQuestInstance,
                out objective))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Exact objective record is unavailable.");
                return;
            }

            if (context.Journal == null)
            {
                bool reconstructExpired =
                    binding.State.LifecycleState
                    == MissionAcgLifecycleState.Expired
                    && objective.State.Phase
                       < MissionAcgCompletionPhase.RewardClaimStarted;
                if (!reconstructExpired
                    && !TryClaimExpiry(binding, objective))
                {
                    return;
                }

                if (reconstructExpired)
                {
                    lock (Gate)
                    {
                        ExpiryClaims.Add(acceptedQuestInstance);
                    }
                }

                IZoneClient ignoredClient;
                ICharacter ignoredCharacter;
                bool ownerConnected =
                    TryGetConnectedOwner(
                        binding.Binding.OwnerIdentity.Instance,
                        out ignoredClient,
                        out ignoredCharacter);
                bool requiresOwnerReconciliation =
                    (binding.State.LifecycleState
                     == MissionAcgLifecycleState.Active
                     || reconstructExpired)
                    && !ownerConnected;
                DateTime detected = DateTime.UtcNow;
                if (detected < binding.Binding.ExpiryUtc)
                {
                    detected = binding.Binding.ExpiryUtc;
                }

                MissionAcgExpiryRecord created;
                string createFailure;
                if (!store.TryCreate(
                    MissionAcgExpiryState.Create(
                        binding.Binding,
                        detected,
                        requiresOwnerReconciliation),
                    out created,
                    out createFailure))
                {
                    SetRetryBackoff(
                        acceptedQuestInstance,
                        DateTime.UtcNow);
                    MissionDiagnostics.Log(
                        "ACG-EXPIRY-DETECT-FAIL accepted={0} livePf2={1} reason={2}",
                        IdentityKey(binding.Binding.AcceptedQuestIdentity),
                        binding.Binding.AllocatedLivePlayfield2,
                        createFailure);
                    return;
                }

                UpdateJournal(acceptedQuestInstance, created);
                context = SnapshotContext(acceptedQuestInstance);
                MissionDiagnostics.Log(
                    "ACG-EXPIRY-DETECTED accepted={0} owner={1} livePf2={2} expiryUtc={3:o} reconcileOwner={4} path={5}",
                    IdentityKey(binding.Binding.AcceptedQuestIdentity),
                    IdentityKey(binding.Binding.OwnerIdentity),
                    binding.Binding.AllocatedLivePlayfield2,
                    binding.Binding.ExpiryUtc,
                    requiresOwnerReconciliation,
                    created.RecordPath);
            }
            else
            {
                lock (Gate)
                {
                    ExpiryClaims.Add(acceptedQuestInstance);
                }
            }

            if (!Advance(
                acceptedQuestInstance,
                MissionAcgExpiryCheckpoint.CleanupStarted
                | MissionAcgExpiryCheckpoint.InteractionsBlocked,
                string.Empty))
            {
                return;
            }

            TryAdvanceOccupantEvacuation(
                acceptedQuestInstance,
                binding);
            if (HasConnectedOccupant(
                binding.Binding.AllocatedLivePlayfield2))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Connected mission occupants remain after evacuation attempt.");
                return;
            }

            if (objective.State.Phase
                < MissionAcgCompletionPhase.RewardClaimStarted
                && objective.State.Lifecycle
                   != MissionAcgObjectiveLifecycle.Expired
                && objective.State.Lifecycle
                   != MissionAcgObjectiveLifecycle.CleanupCompleted)
            {
                string objectiveFailure;
                MissionAcgObjectiveRecord expiredObjective;
                if (!MissionAcgObjectiveRuntime.TrySetLifecycle(
                    objective,
                    MissionAcgObjectiveLifecycle.Expired,
                    out expiredObjective,
                    out objectiveFailure))
                {
                    RecordRetry(acceptedQuestInstance, objectiveFailure);
                    return;
                }

                objective = expiredObjective;
            }

            MissionAcgBindingRecord current;
            if (!MissionAcgBindingRuntime.TryGetByAcceptedQuest(
                acceptedQuestInstance,
                out current))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Exact binding disappeared during expiry.");
                return;
            }

            if (current.State.LifecycleState
                == MissionAcgLifecycleState.Reserved
                || current.State.LifecycleState
                   == MissionAcgLifecycleState.Accepted
                || current.State.LifecycleState
                   == MissionAcgLifecycleState.Active
                || current.State.LifecycleState
                   == MissionAcgLifecycleState.CompletionStarted)
            {
                string transitionFailure;
                MissionAcgBindingRecord expired;
                if (!MissionAcgBindingRuntime.TryTransition(
                    current,
                    MissionAcgLifecycleState.Expired,
                    MissionAcgCleanupState.KeyRemovalPending,
                    DateTime.UtcNow,
                    out expired,
                    out transitionFailure))
                {
                    RecordRetry(acceptedQuestInstance, transitionFailure);
                    return;
                }

                current = expired;
            }
            else if (current.State.LifecycleState
                     != MissionAcgLifecycleState.Expired
                     && current.State.LifecycleState
                        != MissionAcgLifecycleState.CleanupPending
                     && current.State.LifecycleState
                        != MissionAcgLifecycleState.Cleaned)
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Binding lifecycle cannot be owned by expiry: "
                    + current.State.LifecycleState
                    + ".");
                return;
            }

            string cleanupFailure;
            if (!MissionAcgBindingRuntime.TryCompleteRuntimeCleanup(
                    current,
                    out cleanupFailure)
                || !MissionInstanceService.ClearGeneratedInstanceProcessState(
                    current))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    cleanupFailure.Length == 0
                        ? "Exact process-local runtime cleanup failed."
                        : cleanupFailure);
                return;
            }

            if (HasResidualRuntimeState(current))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Exact runtime cleanup did not verify all registries absent.");
                return;
            }

            if (!Advance(
                acceptedQuestInstance,
                MissionAcgExpiryCheckpoint.NpcsRemoved
                | MissionAcgExpiryCheckpoint.ContainersRemoved
                | MissionAcgExpiryCheckpoint.CorpsesRemoved
                | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved
                | MissionAcgExpiryCheckpoint.OperationalStateFinalized,
                string.Empty))
            {
                return;
            }

            if (objective.State.Lifecycle
                != MissionAcgObjectiveLifecycle.CleanupCompleted)
            {
                MissionAcgObjectiveRecord cleanedObjective;
                string objectiveCleanupFailure;
                if (!MissionAcgObjectiveRuntime.TryReplaceState(
                    objective,
                    objective.State.Copy(
                        lifecycle:
                            MissionAcgObjectiveLifecycle.CleanupCompleted,
                        objectiveCleanupCompleted: true,
                        missionCleanupCompleted: true),
                    out cleanedObjective,
                    out objectiveCleanupFailure))
                {
                    RecordRetry(
                        acceptedQuestInstance,
                        objectiveCleanupFailure);
                    return;
                }

                objective = cleanedObjective;
            }

            if (!Advance(
                acceptedQuestInstance,
                MissionAcgExpiryCheckpoint.ObjectivesRemoved
                | MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified,
                string.Empty))
            {
                return;
            }

            context = SnapshotContext(acceptedQuestInstance);
            bool inventoryArtifactsRemoved =
                context != null
                && context.Journal != null
                && context.Journal.State.HasCheckpoint(
                    MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved);
            bool clientMissionRemoved =
                context != null
                && context.Journal != null
                && context.Journal.State.HasCheckpoint(
                    MissionAcgExpiryCheckpoint.ClientMissionRemoved);
            IZoneClient ownerClient = null;
            ICharacter ownerCharacter = null;
            if ((!inventoryArtifactsRemoved || !clientMissionRemoved)
                && !TryGetConnectedOwner(
                    current.Binding.OwnerIdentity.Instance,
                    out ownerClient,
                    out ownerCharacter))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Exact inventory and client journal cleanup awaits owner reconnect.");
                return;
            }

            if (!inventoryArtifactsRemoved)
            {
                if (!MissionAcgCompletionJournalService.RemoveExactArtifacts(
                    ownerClient,
                    ownerCharacter,
                    current.Binding,
                    objective,
                    out cleanupFailure))
                {
                    RecordRetry(acceptedQuestInstance, cleanupFailure);
                    return;
                }

                if (!Advance(
                    acceptedQuestInstance,
                    MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved,
                    string.Empty))
                {
                    return;
                }
            }

            if (!clientMissionRemoved)
            {
                if (!MissionAcceptedStore.TryRemoveExactPersisted(
                    ownerCharacter.Identity.Instance,
                    ToIdentity(current.Binding.AcceptedQuestIdentity),
                    out cleanupFailure))
                {
                    RecordRetry(acceptedQuestInstance, cleanupFailure);
                    return;
                }

                MissionCompleteService.SendQuestDelete(
                    ownerCharacter,
                    ToIdentity(current.Binding.AcceptedQuestIdentity));
                if (!Advance(
                    acceptedQuestInstance,
                    MissionAcgExpiryCheckpoint.ClientMissionRemoved,
                    string.Empty))
                {
                    return;
                }
            }

            if (!TryAdvanceOccupantEvacuation(
                acceptedQuestInstance,
                current))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "PF2 release awaits verified occupant evacuation.");
                return;
            }

            context = SnapshotContext(acceptedQuestInstance);
            if (context == null
                || !HasReleasePrerequisites(context.Journal.State))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "Expiry cleanup prerequisites remain incomplete.");
                return;
            }

            if (current.State.LifecycleState
                == MissionAcgLifecycleState.Expired)
            {
                MissionAcgBindingRecord pending;
                if (!MissionAcgBindingRuntime.TryTransition(
                    current,
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    DateTime.UtcNow,
                    out pending,
                    out cleanupFailure))
                {
                    RecordRetry(acceptedQuestInstance, cleanupFailure);
                    return;
                }

                current = pending;
            }

            if (!Advance(
                acceptedQuestInstance,
                MissionAcgExpiryCheckpoint.BindingReleaseReady,
                string.Empty)
                || !Advance(
                    acceptedQuestInstance,
                    MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted,
                    string.Empty))
            {
                return;
            }

            if (current.State.LifecycleState
                != MissionAcgLifecycleState.Cleaned)
            {
                MissionAcgBindingRecord cleaned;
                if (!MissionAcgBindingRuntime.TryTransition(
                    current,
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow,
                    out cleaned,
                    out cleanupFailure))
                {
                    RecordRetry(acceptedQuestInstance, cleanupFailure);
                    return;
                }

                current = cleaned;
            }

            bool noOccupants = !HasConnectedOccupant(
                current.Binding.AllocatedLivePlayfield2);
            bool noResidualContent = !HasResidualRuntimeState(current);
            bool exactOwnership =
                MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery.IsReservedBy(
                    current.Binding.AllocatedLivePlayfield2,
                    current.Binding.AcceptedQuestIdentity);
            bool anyReservation =
                MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery.IsReserved(
                    current.Binding.AllocatedLivePlayfield2);
            bool anyLiveBinding =
                MissionAcgBindingRuntime.IsBoundLivePlayfield(
                    current.Binding.AllocatedLivePlayfield2);
            context = SnapshotContext(acceptedQuestInstance);
            if (context != null
                && !exactOwnership
                && MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(
                    context.Journal.State,
                    noOccupants,
                    noResidualContent,
                    anyReservation,
                    anyLiveBinding))
            {
                if (!Advance(
                    acceptedQuestInstance,
                    MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
                    | MissionAcgExpiryCheckpoint.CleanupComplete,
                    string.Empty,
                    MissionAcgExpiryStatus.Complete))
                {
                    return;
                }

                if (!MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery
                    .ConfirmReleaseAfterDurableJournal(current))
                {
                    MissionDiagnostics.Log(
                        "ACG-EXPIRY-REUSE-HOLD accepted={0} livePf2={1} reason=allocator-tombstone-owner-mismatch",
                        IdentityKey(current.Binding.AcceptedQuestIdentity),
                        current.Binding.AllocatedLivePlayfield2);
                    return;
                }

                MissionDiagnostics.Log(
                    "ACG-EXPIRY-RELEASE-RECOVERED accepted={0} owner={1} livePf2={2} path={3}",
                    IdentityKey(current.Binding.AcceptedQuestIdentity),
                    IdentityKey(current.Binding.OwnerIdentity),
                    current.Binding.AllocatedLivePlayfield2,
                    SnapshotContext(acceptedQuestInstance).Journal.RecordPath);
                return;
            }

            if (context == null
                || !MissionAcgExpiryPolicy.CanReleasePlayfield(
                    context.Journal.State,
                    noOccupants,
                    noResidualContent,
                    exactOwnership))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    "PF2 release verification failed.");
                return;
            }

            if (!MissionAcgBindingRuntime.TryReleaseAfterDurableCleanup(
                current,
                objective,
                out cleanupFailure)
                || MissionAcgBindingRuntime.IsBoundLivePlayfield(
                    current.Binding.AllocatedLivePlayfield2)
                || MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery.IsReserved(
                    current.Binding.AllocatedLivePlayfield2))
            {
                RecordRetry(
                    acceptedQuestInstance,
                    cleanupFailure.Length == 0
                        ? "PF2 release did not verify absent."
                        : cleanupFailure);
                return;
            }

            if (!Advance(
                acceptedQuestInstance,
                MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
                | MissionAcgExpiryCheckpoint.CleanupComplete,
                string.Empty,
                MissionAcgExpiryStatus.Complete))
            {
                return;
            }

            if (!MissionAcgBindingRuntime.AllocatorDuringExpiryRecovery
                .ConfirmReleaseAfterDurableJournal(current))
            {
                MissionDiagnostics.Log(
                    "ACG-EXPIRY-REUSE-HOLD accepted={0} livePf2={1} reason=allocator-tombstone-owner-mismatch",
                    IdentityKey(current.Binding.AcceptedQuestIdentity),
                    current.Binding.AllocatedLivePlayfield2);
                return;
            }

            MissionDiagnostics.Log(
                "ACG-EXPIRY-COMPLETE accepted={0} owner={1} livePf2={2} expiryUtc={3:o} path={4}",
                IdentityKey(current.Binding.AcceptedQuestIdentity),
                IdentityKey(current.Binding.OwnerIdentity),
                current.Binding.AllocatedLivePlayfield2,
                current.Binding.ExpiryUtc,
                SnapshotContext(acceptedQuestInstance).Journal.RecordPath);
        }

        private static bool TryClaimExpiry(
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective)
        {
            int accepted =
                binding.Binding.AcceptedQuestIdentity.Instance;
            lock (Gate)
            {
                if (MissionAcgExpiryPolicy.IsCompletionOwned(
                    objective.State.Phase))
                {
                    CompletionOwned.Add(accepted);
                    return false;
                }

                if (CompletionOwned.Contains(accepted)
                    || CompletionClaims.Contains(accepted)
                    || CompletionTransitionClaims.Contains(accepted)
                    || AbandonmentClaims.Contains(accepted)
                    || AbandonmentOwned.Contains(accepted)
                    || !MissionAcgExpiryPolicy.CanBeginExpiry(
                        binding.State.LifecycleState,
                        objective.State.Phase,
                        CompletionClaims.Contains(accepted),
                        AbandonmentClaims.Contains(accepted)
                        || AbandonmentOwned.Contains(accepted),
                        DateTime.UtcNow,
                        binding.Binding.ExpiryUtc))
                {
                    return false;
                }

                return ExpiryClaims.Add(accepted)
                       || ExpiryClaims.Contains(accepted);
            }
        }

        private static bool Advance(
            int acceptedQuestInstance,
            MissionAcgExpiryCheckpoint checkpoints,
            string failure)
        {
            ExpiryContext context = SnapshotContext(acceptedQuestInstance);
            if (context == null || context.Journal == null)
            {
                return false;
            }

            MissionAcgExpiryStatus status =
                context.Journal.State.Status
                == MissionAcgExpiryStatus.InProgress
                    ? MissionAcgExpiryStatus.InProgress
                    : MissionAcgExpiryStatus.RetryPending;
            return Advance(
                acceptedQuestInstance,
                checkpoints,
                failure,
                status);
        }

        private static bool Advance(
            int acceptedQuestInstance,
            MissionAcgExpiryCheckpoint checkpoints,
            string failure,
            MissionAcgExpiryStatus status)
        {
            ExpiryContext context = SnapshotContext(acceptedQuestInstance);
            if (context == null || context.Journal == null)
            {
                return false;
            }

            MissionAcgExpiryState next;
            try
            {
                next =
                    context.Journal.State.Advance(
                        checkpoints,
                        status,
                        DateTime.UtcNow,
                        failure);
            }
            catch (Exception ex)
            {
                SetRetryBackoff(
                    acceptedQuestInstance,
                    DateTime.UtcNow);
                MissionDiagnostics.Log(
                    "ACG-EXPIRY-JOURNAL-FAIL accepted={0} path={1} reason={2}",
                    acceptedQuestInstance,
                    context.Journal.RecordPath,
                    ex.Message);
                return false;
            }

            MissionAcgExpiryRecord persisted;
            string persistFailure;
            if (!store.TryReplace(
                context.Journal.WithState(next),
                out persisted,
                out persistFailure))
            {
                SetRetryBackoff(
                    acceptedQuestInstance,
                    DateTime.UtcNow);
                MissionDiagnostics.Log(
                    "ACG-EXPIRY-JOURNAL-FAIL accepted={0} path={1} reason={2}",
                    acceptedQuestInstance,
                    context.Journal.RecordPath,
                    persistFailure);
                return false;
            }

            UpdateJournal(acceptedQuestInstance, persisted);
            return true;
        }

        private static void RecordRetry(
            int acceptedQuestInstance,
            string failure)
        {
            ExpiryContext context = SnapshotContext(acceptedQuestInstance);
            string retryReason = failure ?? "Expiry cleanup retry required.";
            DateTime nowUtc = DateTime.UtcNow;
            SetRetryBackoff(acceptedQuestInstance, nowUtc);

            if (context != null
                && context.Journal != null
                && context.Journal.State.Status
                   == MissionAcgExpiryStatus.RetryPending
                && string.Equals(
                    context.Journal.State.LastFailure,
                    retryReason,
                    StringComparison.Ordinal)
                && nowUtc - context.Journal.State.UpdatedUtc
                   < TimeSpan.FromSeconds(RetryPersistenceIntervalSeconds))
            {
                return;
            }

            if (context != null && context.Journal != null)
            {
                Advance(
                    acceptedQuestInstance,
                    MissionAcgExpiryCheckpoint.None,
                    retryReason,
                    MissionAcgExpiryStatus.RetryPending);
                context = SnapshotContext(acceptedQuestInstance);
            }

            MissionDiagnostics.Log(
                "ACG-EXPIRY-RETRY accepted={0} livePf2={1} checkpoint={2} reason={3} path={4}",
                acceptedQuestInstance,
                context == null
                    ? 0
                    : context.BindingRecord.Binding.AllocatedLivePlayfield2,
                context == null || context.Journal == null
                    ? MissionAcgExpiryCheckpoint.None
                    : context.Journal.State.Checkpoints,
                retryReason,
                context == null || context.Journal == null
                    ? string.Empty
                    : context.Journal.RecordPath);
        }

        private static bool TryAdvanceOccupantEvacuation(
            int acceptedQuestInstance,
            MissionAcgBindingRecord binding)
        {
            ExpiryContext context = SnapshotContext(acceptedQuestInstance);
            if (context == null || context.Journal == null)
            {
                return false;
            }

            IList<ICharacter> occupants =
                SnapshotConnectedOccupants(
                    binding.Binding.AllocatedLivePlayfield2);
            for (int i = 0; i < occupants.Count; i++)
            {
                string failure;
                MissionInstanceService.TryEvacuateExpiredMissionOccupant(
                    occupants[i],
                    binding,
                    out failure);
            }

            IZoneClient ownerClient;
            ICharacter ownerCharacter;
            bool ownerConnected =
                TryGetConnectedOwner(
                    binding.Binding.OwnerIdentity.Instance,
                    out ownerClient,
                    out ownerCharacter);
            bool noOccupants =
                !HasConnectedOccupant(
                    binding.Binding.AllocatedLivePlayfield2);
            bool occupantsAlreadyEvacuated =
                context.Journal.State.HasCheckpoint(
                    MissionAcgExpiryCheckpoint.OccupantsEvacuated);
            if (!noOccupants
                || (!occupantsAlreadyEvacuated
                    && context.Journal.State.RequiresOwnerReconciliation
                    && !ownerConnected))
            {
                return false;
            }

            return occupantsAlreadyEvacuated
                   || Advance(
                       acceptedQuestInstance,
                       MissionAcgExpiryCheckpoint.OccupantsEvacuated,
                       string.Empty);
        }

        private static bool HasReleasePrerequisites(
            MissionAcgExpiryState state)
        {
            return state != null
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.OccupantsEvacuated)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.NpcsRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.ObjectivesRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.ContainersRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.CorpsesRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.OperationalStateFinalized)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.ClientMissionRemoved)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified);
        }

        private static bool HasResidualRuntimeState(
            MissionAcgBindingRecord binding)
        {
            return MissionAcgRuntimeManager.HasRuntimeState(binding)
                   || MissionAcgOperationalRuntime.HasRuntimeState(binding)
                   || MissionAcgSpatialRuntime.HasRuntimeState(binding)
                   || MissionInstanceService.HasGeneratedInstanceProcessState(
                       binding);
        }

        private static bool SameBindingIdentity(
            MissionAcgBindingRecord left,
            MissionAcgBindingRecord right)
        {
            return left != null
                   && right != null
                   && left.Binding != null
                   && right.Binding != null
                   && SameIdentity(
                       left.Binding.AcceptedQuestIdentity,
                       right.Binding.AcceptedQuestIdentity)
                   && SameIdentity(
                       left.Binding.OwnerIdentity,
                       right.Binding.OwnerIdentity)
                   && SameIdentity(
                       left.Binding.MissionKeyIdentity,
                       right.Binding.MissionKeyIdentity)
                   && SameIdentity(
                       left.Binding.ExteriorEntranceIdentity,
                       right.Binding.ExteriorEntranceIdentity)
                   && left.Binding.AllocatedLivePlayfield2
                      == right.Binding.AllocatedLivePlayfield2
                   && string.Equals(
                       left.Binding.SelectedBundleId,
                       right.Binding.SelectedBundleId,
                       StringComparison.Ordinal)
                   && left.Binding.AcgBuildingIdentity.Equals(
                       right.Binding.AcgBuildingIdentity);
        }

        private static bool SameIdentity(
            MissionAcgIdentityRecord left,
            MissionAcgIdentityRecord right)
        {
            return left == null
                       ? right == null
                       : right != null
                         && left.Type == right.Type
                         && left.Instance == right.Instance;
        }

        private static bool TryResolveExactCurrent(
            MissionAcgBindingRecord suppliedBinding,
            MissionAcgObjectiveRecord objective,
            out MissionAcgBindingRecord current,
            out string failure)
        {
            current = null;
            failure = string.Empty;
            if (suppliedBinding == null
                || objective == null
                || !MissionAcgBindingRuntime.TryGetByAcceptedQuest(
                    suppliedBinding.Binding.AcceptedQuestIdentity.Instance,
                    out current)
                || current.Binding.OwnerIdentity.Instance
                   != objective.Binding.OwnerIdentity.Instance
                || current.Binding.AcceptedQuestIdentity.Instance
                   != objective.Binding.AcceptedQuestIdentity.Instance
                || current.Binding.AllocatedLivePlayfield2
                   != objective.Binding.AllocatedLivePlayfield2
                || current.Binding.AllocatedLivePlayfield2
                   != suppliedBinding.Binding.AllocatedLivePlayfield2
                || !string.Equals(
                    current.Binding.SelectedBundleId,
                    objective.Binding.BundleId,
                    StringComparison.Ordinal)
                || !current.Binding.AcgBuildingIdentity.Equals(
                    objective.Binding.BuildingIdentity))
            {
                failure =
                    "Exact current binding/objective identity is required.";
                return false;
            }

            return true;
        }

        private static bool TryGetConnectedOwner(
            int ownerInstance,
            out IZoneClient client,
            out ICharacter character)
        {
            client = null;
            character = null;
            if (Program.zoneServer == null)
            {
                return false;
            }

            lock (Program.zoneServer.Clients)
            {
                foreach (ZoneClient candidate in Program.zoneServer.Clients)
                {
                    ICharacter candidateCharacter =
                        candidate == null || candidate.Controller == null
                            ? null
                            : candidate.Controller.Character;
                    if (candidateCharacter != null
                        && candidateCharacter.Identity.Instance
                           == ownerInstance)
                    {
                        client = candidate;
                        character = candidateCharacter;
                        return true;
                    }
                }
            }

            return false;
        }

        private static IList<ICharacter> SnapshotConnectedOccupants(
            int playfield2)
        {
            var occupants = new List<ICharacter>();
            if (Program.zoneServer == null)
            {
                return occupants.AsReadOnly();
            }

            lock (Program.zoneServer.Clients)
            {
                foreach (ZoneClient candidate in Program.zoneServer.Clients)
                {
                    ICharacter character =
                        candidate == null || candidate.Controller == null
                            ? null
                            : candidate.Controller.Character;
                    if (character != null
                        && character.Playfield != null
                        && character.Playfield.Identity.Instance == playfield2)
                    {
                        occupants.Add(character);
                    }
                }
            }

            return occupants.AsReadOnly();
        }

        private static bool HasConnectedOccupant(int playfield2)
        {
            return SnapshotConnectedOccupants(playfield2).Count != 0;
        }

        private static ExpiryContext SnapshotContext(
            int acceptedQuestInstance)
        {
            lock (Gate)
            {
                ExpiryContext context;
                if (!initialized
                    || !ByAccepted.TryGetValue(
                        acceptedQuestInstance,
                        out context))
                {
                    return null;
                }

                return new ExpiryContext(
                    context.BindingRecord,
                    context.Journal);
            }
        }

        private static void UpdateJournal(
            int acceptedQuestInstance,
            MissionAcgExpiryRecord journal)
        {
            lock (Gate)
            {
                ExpiryContext context;
                if (ByAccepted.TryGetValue(
                    acceptedQuestInstance,
                    out context))
                {
                    context.Journal = journal;
                    if (journal != null && journal.State.IsComplete)
                    {
                        RetryAfterUtc.Remove(acceptedQuestInstance);
                    }
                }
            }
        }

        private static void QueueImmediate(int acceptedQuestInstance)
        {
            lock (Gate)
            {
                if (!initialized || stopping)
                {
                    return;
                }

                DateTime retryAfter;
                if (RetryAfterUtc.TryGetValue(
                        acceptedQuestInstance,
                        out retryAfter)
                    && retryAfter > DateTime.UtcNow)
                {
                    return;
                }

                RetryAfterUtc.Remove(acceptedQuestInstance);
            }

            ThreadPool.QueueUserWorkItem(
                ignored => ProcessAccepted(acceptedQuestInstance));
        }

        private static bool HasRunnableWork(
            ExpiryContext context,
            DateTime nowUtc)
        {
            if (context == null || context.BindingRecord == null)
            {
                return false;
            }

            int acceptedQuestInstance =
                context.BindingRecord.Binding.AcceptedQuestIdentity.Instance;
            if (CompletionOwned.Contains(acceptedQuestInstance)
                || CompletionTransitionClaims.Contains(
                    acceptedQuestInstance)
                || AbandonmentClaims.Contains(acceptedQuestInstance)
                || AbandonmentOwned.Contains(acceptedQuestInstance))
            {
                return false;
            }

            if (context.Journal != null)
            {
                return !context.Journal.State.IsComplete
                       && context.Journal.State.Status
                          != MissionAcgExpiryStatus.TerminalFailure;
            }

            MissionAcgLifecycleState lifecycle =
                context.BindingRecord.State.LifecycleState;
            return lifecycle == MissionAcgLifecycleState.Expired
                   || (lifecycle == MissionAcgLifecycleState.Reserved
                    || lifecycle == MissionAcgLifecycleState.Accepted
                    || lifecycle == MissionAcgLifecycleState.Active
                    || lifecycle == MissionAcgLifecycleState.CompletionStarted)
                   && MissionAcgExpiryPolicy.IsDue(
                       nowUtc,
                       context.BindingRecord.Binding.ExpiryUtc);
        }

        private static void SetRetryBackoff(
            int acceptedQuestInstance,
            DateTime nowUtc)
        {
            lock (Gate)
            {
                RetryAfterUtc[acceptedQuestInstance] =
                    nowUtc.AddSeconds(RetryPersistenceIntervalSeconds);
            }
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

        private static string IdentityKey(
            MissionAcgIdentityRecord identity)
        {
            return identity == null
                       ? "<none>"
                       : identity.Type + ":" + identity.Instance;
        }

        private sealed class ExpiryContext
        {
            internal ExpiryContext(
                MissionAcgBindingRecord bindingRecord,
                MissionAcgExpiryRecord journal)
            {
                this.BindingRecord = bindingRecord;
                this.Journal = journal;
            }

            internal MissionAcgBindingRecord BindingRecord { get; set; }

            internal MissionAcgExpiryRecord Journal { get; set; }
        }
    }
}
