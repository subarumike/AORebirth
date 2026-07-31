namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;

    internal enum MissionAcgTokenProgressEventPhase
    {
        NotObserved = 0,
        Validated = 1,
        DurablyApplied = 2,
        ClientUpdatePending = 3,
        ClientUpdateSent = 4,
        TerminalFailure = 5
    }

    /// <summary>
    /// Durable audit entry for one exact countable ambient-NPC death.
    /// </summary>
    internal sealed class MissionAcgTokenProgressDeathEvent
    {
        internal MissionAcgTokenProgressDeathEvent(
            string eventId,
            MissionAcgIdentityRecord sourceRuntimeIdentity,
            MissionAcgIdentityRecord actorIdentity,
            int capturedSlot,
            int spawnGeneration,
            int sequence,
            int appliedCountBefore,
            int appliedCountAfter,
            int percentBefore,
            int percentAfter,
            DateTime observedUtc,
            DateTime updatedUtc,
            MissionAcgTokenProgressEventPhase phase,
            bool wasDurablyApplied,
            string lastFailure)
        {
            if (string.IsNullOrWhiteSpace(eventId)
                || eventId.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException(
                    "Deterministic token event id is required.",
                    "eventId");
            }

            RequireIdentity(sourceRuntimeIdentity, "sourceRuntimeIdentity");
            RequireIdentity(actorIdentity, "actorIdentity");
            if (capturedSlot < 0
                || spawnGeneration < 1
                || sequence <= 0)
            {
                throw new ArgumentException(
                    "Token-progress death ownership is invalid.");
            }

            if (appliedCountBefore < 0
                || appliedCountAfter != appliedCountBefore + 1)
            {
                throw new ArgumentException(
                    "One ambient-death event must describe one progress-count advance.");
            }

            if (percentBefore < 0
                || percentBefore > 100
                || percentAfter < percentBefore
                || percentAfter > 100)
            {
                throw new ArgumentException(
                    "Token-progress percentages are invalid.");
            }

            DateTime observed = RequireUtc(observedUtc, "observedUtc");
            DateTime updated = RequireUtc(updatedUtc, "updatedUtc");
            if (updated < observed)
            {
                throw new ArgumentException(
                    "Token-progress event update precedes observation.",
                    "updatedUtc");
            }

            if (!Enum.IsDefined(typeof(MissionAcgTokenProgressEventPhase), phase)
                || phase == MissionAcgTokenProgressEventPhase.NotObserved)
            {
                throw new ArgumentOutOfRangeException("phase");
            }

            bool phaseRequiresApplication =
                phase == MissionAcgTokenProgressEventPhase.DurablyApplied
                || phase == MissionAcgTokenProgressEventPhase.ClientUpdatePending
                || phase == MissionAcgTokenProgressEventPhase.ClientUpdateSent;
            if (phase != MissionAcgTokenProgressEventPhase.TerminalFailure
                && phaseRequiresApplication != wasDurablyApplied)
            {
                throw new ArgumentException(
                    "Token-progress phase and durable-application flag disagree.");
            }

            string failure = (lastFailure ?? string.Empty).Trim();
            if (phase == MissionAcgTokenProgressEventPhase.TerminalFailure)
            {
                if (failure.Length == 0)
                {
                    throw new ArgumentException(
                        "Terminal token-progress failure requires a diagnostic.",
                        "lastFailure");
                }
            }
            else if (failure.Length != 0)
            {
                throw new ArgumentException(
                    "Only terminal token-progress failure may contain a diagnostic.",
                    "lastFailure");
            }

            this.EventId = eventId.Trim();
            this.SourceRuntimeIdentity = sourceRuntimeIdentity;
            this.ActorIdentity = actorIdentity;
            this.CapturedSlot = capturedSlot;
            this.SpawnGeneration = spawnGeneration;
            this.Sequence = sequence;
            this.AppliedCountBefore = appliedCountBefore;
            this.AppliedCountAfter = appliedCountAfter;
            this.PercentBefore = percentBefore;
            this.PercentAfter = percentAfter;
            this.ObservedUtc = observed;
            this.UpdatedUtc = updated;
            this.Phase = phase;
            this.WasDurablyApplied = wasDurablyApplied;
            this.LastFailure = failure;
        }

        internal string EventId { get; private set; }

        internal MissionAcgIdentityRecord SourceRuntimeIdentity { get; private set; }

        internal MissionAcgIdentityRecord ActorIdentity { get; private set; }

        internal int CapturedSlot { get; private set; }

        internal int SpawnGeneration { get; private set; }

        internal int Sequence { get; private set; }

        internal int AppliedCountBefore { get; private set; }

        internal int AppliedCountAfter { get; private set; }

        internal int PercentBefore { get; private set; }

        internal int PercentAfter { get; private set; }

        internal DateTime ObservedUtc { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal MissionAcgTokenProgressEventPhase Phase { get; private set; }

        internal bool WasDurablyApplied { get; private set; }

        internal string LastFailure { get; private set; }

        internal bool IsTerminal
        {
            get
            {
                return this.Phase == MissionAcgTokenProgressEventPhase.ClientUpdateSent
                       || this.Phase == MissionAcgTokenProgressEventPhase.TerminalFailure;
            }
        }

        internal MissionAcgTokenProgressDeathEvent Advance(
            MissionAcgTokenProgressEventPhase nextPhase,
            DateTime updatedUtc,
            string lastFailure)
        {
            DateTime updated = RequireUtc(updatedUtc, "updatedUtc");
            if (updated < this.UpdatedUtc)
            {
                throw new ArgumentException(
                    "Token-progress event update time cannot regress.",
                    "updatedUtc");
            }

            if (!CanAdvance(this.Phase, nextPhase))
            {
                throw new InvalidOperationException(
                    "Token-progress event phase cannot advance from "
                    + this.Phase
                    + " to "
                    + nextPhase
                    + ".");
            }

            bool applied =
                this.WasDurablyApplied
                || nextPhase == MissionAcgTokenProgressEventPhase.DurablyApplied;
            return new MissionAcgTokenProgressDeathEvent(
                this.EventId,
                this.SourceRuntimeIdentity,
                this.ActorIdentity,
                this.CapturedSlot,
                this.SpawnGeneration,
                this.Sequence,
                this.AppliedCountBefore,
                this.AppliedCountAfter,
                this.PercentBefore,
                this.PercentAfter,
                this.ObservedUtc,
                updated,
                nextPhase,
                applied,
                lastFailure);
        }

        internal static bool CanAdvance(
            MissionAcgTokenProgressEventPhase current,
            MissionAcgTokenProgressEventPhase next)
        {
            if (!Enum.IsDefined(typeof(MissionAcgTokenProgressEventPhase), current)
                || !Enum.IsDefined(typeof(MissionAcgTokenProgressEventPhase), next)
                || current == MissionAcgTokenProgressEventPhase.NotObserved
                || current == MissionAcgTokenProgressEventPhase.ClientUpdateSent
                || current == MissionAcgTokenProgressEventPhase.TerminalFailure)
            {
                return false;
            }

            if (next == MissionAcgTokenProgressEventPhase.TerminalFailure)
            {
                return true;
            }

            return (current == MissionAcgTokenProgressEventPhase.Validated
                    && next == MissionAcgTokenProgressEventPhase.DurablyApplied)
                   || (current == MissionAcgTokenProgressEventPhase.DurablyApplied
                       && next
                       == MissionAcgTokenProgressEventPhase.ClientUpdatePending)
                   || (current
                       == MissionAcgTokenProgressEventPhase.ClientUpdatePending
                       && next == MissionAcgTokenProgressEventPhase.ClientUpdateSent);
        }

        private static void RequireIdentity(
            MissionAcgIdentityRecord identity,
            string parameter)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException(
                    "A concrete identity is required.",
                    parameter);
            }
        }

        private static DateTime RequireUtc(DateTime value, string parameter)
        {
            if (value == DateTime.MinValue || value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "A concrete UTC timestamp is required.",
                    parameter);
            }

            return value;
        }
    }

    /// <summary>
    /// Immutable mission/objective ownership plus append-only token-progress audit.
    /// One instance is persisted per accepted generated mission.
    /// </summary>
    internal sealed class MissionAcgTokenProgressState
    {
        internal const int CurrentFormatVersion = 1;

        private readonly Dictionary<string, MissionAcgTokenProgressDeathEvent>
            eventsById;

        internal MissionAcgTokenProgressState(
            int formatVersion,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveBinding objectiveBinding,
            int totalCountableAmbientSlots,
            int initialPercent,
            int appliedCount,
            int percent,
            MissionAcgLifecycleState lifecycle,
            MissionAcgLifecycleState terminalReason,
            string lifecycleDiagnostic,
            DateTime createdUtc,
            DateTime updatedUtc,
            IEnumerable<MissionAcgTokenProgressDeathEvent> deathEvents)
        {
            if (formatVersion != CurrentFormatVersion)
            {
                throw new ArgumentOutOfRangeException("formatVersion");
            }

            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }

            if (objectiveBinding == null)
            {
                throw new ArgumentNullException("objectiveBinding");
            }

            ValidateObjectiveOwnership(binding, objectiveBinding);
            int expectedInitialPercent =
                totalCountableAmbientSlots == 0 ? 100 : 0;
            if (totalCountableAmbientSlots < 0
                || initialPercent != expectedInitialPercent
                || appliedCount < 0
                || percent < initialPercent
                || percent > 100)
            {
                throw new ArgumentException(
                    "Token-progress aggregate values are invalid.");
            }

            if (appliedCount > totalCountableAmbientSlots)
            {
                throw new ArgumentException(
                    "Applied token-progress count exceeds the frozen ambient slot count.");
            }

            ValidateLifecycle(lifecycle, terminalReason, lifecycleDiagnostic);
            DateTime created = RequireUtc(createdUtc, "createdUtc");
            DateTime updated = RequireUtc(updatedUtc, "updatedUtc");
            if (updated < created)
            {
                throw new ArgumentException(
                    "Token-progress update precedes creation.",
                    "updatedUtc");
            }

            var events =
                new List<MissionAcgTokenProgressDeathEvent>(
                    deathEvents
                    ?? new MissionAcgTokenProgressDeathEvent[0]);
            events.Sort(
                delegate(
                    MissionAcgTokenProgressDeathEvent left,
                    MissionAcgTokenProgressDeathEvent right)
                {
                    if (ReferenceEquals(left, null))
                    {
                        return ReferenceEquals(right, null) ? 0 : -1;
                    }

                    return ReferenceEquals(right, null)
                               ? 1
                               : left.Sequence.CompareTo(right.Sequence);
                });

            this.eventsById =
                new Dictionary<string, MissionAcgTokenProgressDeathEvent>(
                    StringComparer.Ordinal);
            var sourceGenerations = new HashSet<string>(StringComparer.Ordinal);
            int aggregateCount = 0;
            int aggregatePercent = initialPercent;
            bool hasUnappliedValidatedEvent = false;
            for (int i = 0; i < events.Count; i++)
            {
                MissionAcgTokenProgressDeathEvent current = events[i];
                if (current == null)
                {
                    throw new ArgumentException(
                        "Token-progress event collection contains null.");
                }

                if (current.Sequence != i + 1)
                {
                    throw new ArgumentException(
                        "Token-progress event sequence is not contiguous.");
                }

                string expectedEventId =
                    BuildEventId(
                        binding,
                        current.SourceRuntimeIdentity,
                        current.CapturedSlot,
                        current.SpawnGeneration);
                if (!string.Equals(
                        current.EventId,
                        expectedEventId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Token-progress event id is not deterministic.");
                }

                if (!binding.ExplicitNoTeam
                    || !current.ActorIdentity.Equals(binding.OwnerIdentity))
                {
                    throw new ArgumentException(
                        "Token-progress death actor does not match the explicit "
                        + "solo mission owner.");
                }

                if (this.eventsById.ContainsKey(current.EventId)
                    || !sourceGenerations.Add(
                        SourceGenerationKey(
                            current.SourceRuntimeIdentity,
                            current.CapturedSlot,
                            current.SpawnGeneration)))
                {
                    throw new ArgumentException(
                        "Duplicate token-progress death event ownership.");
                }

                if (hasUnappliedValidatedEvent)
                {
                    throw new ArgumentException(
                        "An unapplied validated token-progress event must be last.");
                }

                if (current.AppliedCountBefore != aggregateCount
                    || current.PercentBefore != aggregatePercent
                    || current.PercentAfter
                       != CalculatePercent(
                           current.AppliedCountAfter,
                           totalCountableAmbientSlots))
                {
                    throw new ArgumentException(
                        "Token-progress event chain or percentage formula is invalid.");
                }

                if (current.AppliedCountAfter > totalCountableAmbientSlots)
                {
                    throw new ArgumentException(
                        "Token-progress event exceeds the frozen ambient slot count.");
                }

                if (current.ObservedUtc < created
                    || current.UpdatedUtc > updated)
                {
                    throw new ArgumentException(
                        "Token-progress event timestamps are outside state bounds.");
                }

                if (current.WasDurablyApplied)
                {
                    aggregateCount = current.AppliedCountAfter;
                    aggregatePercent = current.PercentAfter;
                }
                else if (current.Phase
                         == MissionAcgTokenProgressEventPhase.Validated)
                {
                    hasUnappliedValidatedEvent = true;
                }

                this.eventsById.Add(current.EventId, current);
            }

            if (aggregateCount != appliedCount || aggregatePercent != percent)
            {
                throw new ArgumentException(
                    "Token-progress aggregate does not match durable event history.");
            }

            this.FormatVersion = formatVersion;
            this.Binding = binding;
            this.ObjectiveBinding = objectiveBinding;
            this.TotalCountableAmbientSlots = totalCountableAmbientSlots;
            this.InitialPercent = initialPercent;
            this.AppliedCount = appliedCount;
            this.Percent = percent;
            this.Lifecycle = lifecycle;
            this.TerminalReason = terminalReason;
            this.LifecycleDiagnostic =
                (lifecycleDiagnostic ?? string.Empty).Trim();
            this.CreatedUtc = created;
            this.UpdatedUtc = updated;
            this.DeathEvents =
                new ReadOnlyCollection<MissionAcgTokenProgressDeathEvent>(events);
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgInstanceBinding Binding { get; private set; }

        internal MissionAcgObjectiveBinding ObjectiveBinding { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity
        {
            get
            {
                return this.Binding.AcceptedQuestIdentity;
            }
        }

        internal int TotalCountableAmbientSlots { get; private set; }

        internal int InitialPercent { get; private set; }

        internal int AppliedCount { get; private set; }

        internal int Percent { get; private set; }

        internal MissionAcgLifecycleState Lifecycle { get; private set; }

        internal MissionAcgLifecycleState TerminalReason { get; private set; }

        internal string LifecycleDiagnostic { get; private set; }

        internal DateTime CreatedUtc { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal IList<MissionAcgTokenProgressDeathEvent> DeathEvents
        {
            get;
            private set;
        }

        internal bool CanAcceptDeaths
        {
            get
            {
                return this.Lifecycle == MissionAcgLifecycleState.Active
                       && this.TerminalReason == 0
                       && this.Binding.ExplicitNoTeam;
            }
        }

        internal static MissionAcgTokenProgressState Create(
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveBinding objectiveBinding,
            int totalCountableAmbientSlots,
            MissionAcgLifecycleState lifecycle,
            DateTime createdUtc)
        {
            return new MissionAcgTokenProgressState(
                CurrentFormatVersion,
                binding,
                objectiveBinding,
                totalCountableAmbientSlots,
                totalCountableAmbientSlots == 0 ? 100 : 0,
                0,
                totalCountableAmbientSlots == 0 ? 100 : 0,
                lifecycle,
                0,
                string.Empty,
                createdUtc,
                createdUtc,
                new MissionAcgTokenProgressDeathEvent[0]);
        }

        internal static MissionAcgTokenProgressState CreateInvalid(
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveBinding objectiveBinding,
            int totalCountableAmbientSlots,
            string diagnostic,
            DateTime createdUtc)
        {
            return new MissionAcgTokenProgressState(
                CurrentFormatVersion,
                binding,
                objectiveBinding,
                totalCountableAmbientSlots,
                totalCountableAmbientSlots == 0 ? 100 : 0,
                0,
                totalCountableAmbientSlots == 0 ? 100 : 0,
                MissionAcgLifecycleState.Invalid,
                MissionAcgLifecycleState.Invalid,
                diagnostic,
                createdUtc,
                createdUtc,
                new MissionAcgTokenProgressDeathEvent[0]);
        }

        internal static int CalculatePercent(
            int appliedCount,
            int totalCountableAmbientSlots)
        {
            if (appliedCount < 0
                || totalCountableAmbientSlots < 0
                || appliedCount > totalCountableAmbientSlots)
            {
                throw new ArgumentOutOfRangeException("appliedCount");
            }

            if (totalCountableAmbientSlots == 0)
            {
                return 100;
            }

            return (int)Math.Min(
                100L,
                ((long)appliedCount * 100L) / totalCountableAmbientSlots);
        }

        internal static string BuildEventId(
            MissionAcgInstanceBinding binding,
            MissionAcgIdentityRecord sourceRuntimeIdentity,
            int capturedSlot,
            int spawnGeneration)
        {
            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }

            if (sourceRuntimeIdentity == null
                || sourceRuntimeIdentity.Type == 0
                || sourceRuntimeIdentity.Instance == 0
                || capturedSlot < 0
                || spawnGeneration < 1)
            {
                throw new ArgumentException(
                    "Token-progress event ownership is incomplete.");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "token-death-v1/{0:X8}/{1:X8}/{2:X8}/{3:D8}/{4:D8}/{5:X8}/{6:X8}",
                binding.AcceptedQuestIdentity.Type,
                binding.AcceptedQuestIdentity.Instance,
                binding.AllocatedLivePlayfield2,
                capturedSlot,
                spawnGeneration,
                sourceRuntimeIdentity.Type,
                sourceRuntimeIdentity.Instance);
        }

        internal MissionAcgTokenProgressEventPhase PhaseFor(
            MissionAcgIdentityRecord sourceRuntimeIdentity,
            int capturedSlot,
            int spawnGeneration)
        {
            MissionAcgTokenProgressDeathEvent progressEvent;
            return this.TryGetEvent(
                       sourceRuntimeIdentity,
                       capturedSlot,
                       spawnGeneration,
                       out progressEvent)
                       ? progressEvent.Phase
                       : MissionAcgTokenProgressEventPhase.NotObserved;
        }

        internal bool TryGetEvent(
            MissionAcgIdentityRecord sourceRuntimeIdentity,
            int capturedSlot,
            int spawnGeneration,
            out MissionAcgTokenProgressDeathEvent progressEvent)
        {
            progressEvent = null;
            if (sourceRuntimeIdentity == null)
            {
                return false;
            }

            string eventId;
            try
            {
                eventId =
                    BuildEventId(
                        this.Binding,
                        sourceRuntimeIdentity,
                        capturedSlot,
                        spawnGeneration);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return this.eventsById.TryGetValue(eventId, out progressEvent);
        }

        internal MissionAcgTokenProgressState AddValidatedDeath(
            MissionAcgIdentityRecord sourceRuntimeIdentity,
            MissionAcgIdentityRecord actorIdentity,
            int capturedSlot,
            int spawnGeneration,
            DateTime observedUtc)
        {
            if (!this.CanAcceptDeaths)
            {
                throw new InvalidOperationException(
                    "Token-progress lifecycle cannot accept new deaths.");
            }

            MissionAcgTokenProgressDeathEvent existing;
            if (this.TryGetEvent(
                    sourceRuntimeIdentity,
                    capturedSlot,
                    spawnGeneration,
                    out existing))
            {
                throw new InvalidOperationException(
                    "Token-progress death is already indexed.");
            }

            if (this.DeathEvents.Count > 0)
            {
                MissionAcgTokenProgressDeathEvent last =
                    this.DeathEvents[this.DeathEvents.Count - 1];
                if (last.Phase == MissionAcgTokenProgressEventPhase.Validated)
                {
                    throw new InvalidOperationException(
                        "Prior validated token-progress death is not resolved.");
                }
            }

            if (this.AppliedCount >= this.TotalCountableAmbientSlots)
            {
                throw new InvalidOperationException(
                    "All frozen countable ambient slots are already applied.");
            }

            DateTime observed = RequireUtc(observedUtc, "observedUtc");
            if (observed < this.UpdatedUtc)
            {
                throw new ArgumentException(
                    "Token-progress observation time cannot regress.",
                    "observedUtc");
            }

            int appliedCountAfter = this.AppliedCount + 1;
            var progressEvent =
                new MissionAcgTokenProgressDeathEvent(
                    BuildEventId(
                        this.Binding,
                        sourceRuntimeIdentity,
                        capturedSlot,
                        spawnGeneration),
                    sourceRuntimeIdentity,
                    actorIdentity,
                    capturedSlot,
                    spawnGeneration,
                    this.DeathEvents.Count + 1,
                    this.AppliedCount,
                    appliedCountAfter,
                    this.Percent,
                    CalculatePercent(
                        appliedCountAfter,
                        this.TotalCountableAmbientSlots),
                    observed,
                    observed,
                    MissionAcgTokenProgressEventPhase.Validated,
                    false,
                    string.Empty);
            var events =
                new List<MissionAcgTokenProgressDeathEvent>(this.DeathEvents)
                {
                    progressEvent
                };
            return this.Copy(
                this.AppliedCount,
                this.Percent,
                this.Lifecycle,
                this.TerminalReason,
                this.LifecycleDiagnostic,
                observed,
                events);
        }

        internal MissionAcgTokenProgressState AdvanceDeath(
            string eventId,
            MissionAcgTokenProgressEventPhase nextPhase,
            DateTime updatedUtc,
            string lastFailure)
        {
            MissionAcgTokenProgressDeathEvent existing;
            if (string.IsNullOrWhiteSpace(eventId)
                || !this.eventsById.TryGetValue(eventId, out existing))
            {
                throw new InvalidOperationException(
                    "Token-progress death event is not indexed.");
            }

            MissionAcgTokenProgressDeathEvent advanced =
                existing.Advance(nextPhase, updatedUtc, lastFailure);
            var events =
                new List<MissionAcgTokenProgressDeathEvent>(this.DeathEvents.Count);
            for (int i = 0; i < this.DeathEvents.Count; i++)
            {
                events.Add(
                    this.DeathEvents[i].Sequence == existing.Sequence
                        ? advanced
                        : this.DeathEvents[i]);
            }

            int appliedCount = this.AppliedCount;
            int percent = this.Percent;
            if (!existing.WasDurablyApplied && advanced.WasDurablyApplied)
            {
                if (existing.AppliedCountBefore != this.AppliedCount
                    || existing.PercentBefore != this.Percent)
                {
                    throw new InvalidOperationException(
                        "Token-progress aggregate changed before durable application.");
                }

                appliedCount = advanced.AppliedCountAfter;
                percent = advanced.PercentAfter;
            }

            return this.Copy(
                appliedCount,
                percent,
                this.Lifecycle,
                this.TerminalReason,
                this.LifecycleDiagnostic,
                updatedUtc,
                events);
        }

        internal MissionAcgTokenProgressState WithLifecycle(
            MissionAcgLifecycleState lifecycle,
            DateTime updatedUtc,
            string diagnostic)
        {
            if (!CanTransition(this.Lifecycle, lifecycle))
            {
                throw new InvalidOperationException(
                    "Token-progress lifecycle cannot advance from "
                    + this.Lifecycle
                    + " to "
                    + lifecycle
                    + ".");
            }

            MissionAcgLifecycleState terminalReason = this.TerminalReason;
            if (IsTerminalOutcome(lifecycle))
            {
                if (terminalReason != 0 && terminalReason != lifecycle)
                {
                    throw new InvalidOperationException(
                        "Token-progress terminal reason cannot be replaced.");
                }

                terminalReason = lifecycle;
            }
            else if (lifecycle == MissionAcgLifecycleState.Invalid
                     && terminalReason == 0)
            {
                terminalReason = MissionAcgLifecycleState.Invalid;
            }

            string nextDiagnostic =
                lifecycle == MissionAcgLifecycleState.Invalid
                    ? (string.IsNullOrWhiteSpace(diagnostic)
                           ? this.LifecycleDiagnostic
                           : diagnostic)
                    : string.Empty;
            return this.Copy(
                this.AppliedCount,
                this.Percent,
                lifecycle,
                terminalReason,
                nextDiagnostic,
                updatedUtc,
                this.DeathEvents);
        }

        internal bool Matches(
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveBinding objectiveBinding,
            out string failure)
        {
            failure = string.Empty;
            if (!BindingEquals(this.Binding, binding))
            {
                failure = "Token-progress state does not match its mission binding.";
                return false;
            }

            if (!ObjectiveEquals(this.ObjectiveBinding, objectiveBinding))
            {
                failure = "Token-progress state does not match its objective binding.";
                return false;
            }

            return true;
        }

        internal static bool CanTransition(
            MissionAcgLifecycleState current,
            MissionAcgLifecycleState next)
        {
            if (!Enum.IsDefined(typeof(MissionAcgLifecycleState), current)
                || !Enum.IsDefined(typeof(MissionAcgLifecycleState), next))
            {
                return false;
            }

            if (current == next)
            {
                return true;
            }

            switch (current)
            {
                case MissionAcgLifecycleState.Reserved:
                    return next == MissionAcgLifecycleState.Accepted
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Accepted:
                    return next == MissionAcgLifecycleState.Active
                           || next == MissionAcgLifecycleState.CompletionStarted
                           || next == MissionAcgLifecycleState.Abandoned
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Active:
                    return next == MissionAcgLifecycleState.CompletionStarted
                           || next == MissionAcgLifecycleState.Abandoned
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.CompletionStarted:
                    return next == MissionAcgLifecycleState.Completed
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Completed:
                case MissionAcgLifecycleState.Abandoned:
                case MissionAcgLifecycleState.Expired:
                    return next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.CleanupPending:
                    return next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Cleaned:
                case MissionAcgLifecycleState.Invalid:
                    return false;
                default:
                    return false;
            }
        }

        private MissionAcgTokenProgressState Copy(
            int appliedCount,
            int percent,
            MissionAcgLifecycleState lifecycle,
            MissionAcgLifecycleState terminalReason,
            string lifecycleDiagnostic,
            DateTime updatedUtc,
            IEnumerable<MissionAcgTokenProgressDeathEvent> events)
        {
            return new MissionAcgTokenProgressState(
                this.FormatVersion,
                this.Binding,
                this.ObjectiveBinding,
                this.TotalCountableAmbientSlots,
                this.InitialPercent,
                appliedCount,
                percent,
                lifecycle,
                terminalReason,
                lifecycleDiagnostic,
                this.CreatedUtc,
                updatedUtc,
                events);
        }

        private static void ValidateLifecycle(
            MissionAcgLifecycleState lifecycle,
            MissionAcgLifecycleState terminalReason,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(MissionAcgLifecycleState), lifecycle))
            {
                throw new ArgumentOutOfRangeException("lifecycle");
            }

            bool hasTerminalReason = terminalReason != 0;
            if (hasTerminalReason && !IsRetainedTerminalReason(terminalReason))
            {
                throw new ArgumentOutOfRangeException("terminalReason");
            }

            if (IsTerminalOutcome(lifecycle))
            {
                if (terminalReason != lifecycle)
                {
                    throw new ArgumentException(
                        "Token-progress lifecycle and terminal reason disagree.");
                }
            }
            else if (lifecycle == MissionAcgLifecycleState.CleanupPending
                     || lifecycle == MissionAcgLifecycleState.Cleaned)
            {
                // Acceptance rollback may enter cleanup without a terminal mission
                // outcome. When an outcome exists it remains retained for audit.
            }
            else if (lifecycle == MissionAcgLifecycleState.Invalid)
            {
                if (!hasTerminalReason)
                {
                    throw new ArgumentException(
                        "Invalid token-progress lifecycle requires a terminal reason.");
                }
            }
            else if (hasTerminalReason)
            {
                throw new ArgumentException(
                    "Nonterminal token-progress lifecycle has a terminal reason.");
            }

            string value = (diagnostic ?? string.Empty).Trim();
            if ((lifecycle == MissionAcgLifecycleState.Invalid)
                != (value.Length != 0))
            {
                throw new ArgumentException(
                    "Invalid token-progress lifecycle requires one retained diagnostic.");
            }
        }

        private static bool IsTerminalOutcome(MissionAcgLifecycleState value)
        {
            return value == MissionAcgLifecycleState.Completed
                   || value == MissionAcgLifecycleState.Abandoned
                   || value == MissionAcgLifecycleState.Expired;
        }

        private static bool IsRetainedTerminalReason(
            MissionAcgLifecycleState value)
        {
            return IsTerminalOutcome(value)
                   || value == MissionAcgLifecycleState.Invalid;
        }

        private static void ValidateObjectiveOwnership(
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveBinding objective)
        {
            if (!objective.AcceptedQuestIdentity.Equals(
                    binding.AcceptedQuestIdentity)
                || !objective.OwnerIdentity.Equals(binding.OwnerIdentity)
                || !EqualIdentity(objective.TeamIdentity, binding.TeamIdentity)
                || objective.ExplicitNoTeam != binding.ExplicitNoTeam
                || objective.MissionType != binding.MissionType
                || objective.AllocatedLivePlayfield2
                   != binding.AllocatedLivePlayfield2
                || !string.Equals(
                    objective.BundleId,
                    binding.SelectedBundleId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    objective.BundlePayloadSha256,
                    binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !objective.BuildingIdentity.Equals(
                    binding.AcgBuildingIdentity))
            {
                throw new ArgumentException(
                    "Objective ownership does not match the mission binding.",
                    "objectiveBinding");
            }
        }

        private static bool BindingEquals(
            MissionAcgInstanceBinding left,
            MissionAcgInstanceBinding right)
        {
            return left != null
                   && right != null
                   && left.BindingFormatVersion == right.BindingFormatVersion
                   && left.AcceptedQuestIdentity.Equals(right.AcceptedQuestIdentity)
                   && left.OriginalOfferIdentity.Equals(right.OriginalOfferIdentity)
                   && left.OwnerIdentity.Equals(right.OwnerIdentity)
                   && EqualIdentity(left.TeamIdentity, right.TeamIdentity)
                   && left.ExplicitNoTeam == right.ExplicitNoTeam
                   && left.MissionType == right.MissionType
                   && left.MissionQuality == right.MissionQuality
                   && left.DeterministicSeed == right.DeterministicSeed
                   && left.MissionKeyIdentity.Equals(right.MissionKeyIdentity)
                   && left.ExteriorEntranceIdentity.Equals(
                       right.ExteriorEntranceIdentity)
                   && left.ExteriorEntranceLow == right.ExteriorEntranceLow
                   && left.ExteriorEntranceHigh == right.ExteriorEntranceHigh
                   && left.ExteriorX.Equals(right.ExteriorX)
                   && left.ExteriorY.Equals(right.ExteriorY)
                   && left.ExteriorZ.Equals(right.ExteriorZ)
                   && left.IssuingTerminalIdentity.Equals(
                       right.IssuingTerminalIdentity)
                   && string.Equals(
                       left.SelectedBundleId,
                       right.SelectedBundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       left.SelectedBundlePayloadSha256,
                       right.SelectedBundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && left.AcgBuildingIdentity.Equals(right.AcgBuildingIdentity)
                   && left.AllocatedLivePlayfield2
                      == right.AllocatedLivePlayfield2
                   && left.AcceptedUtc == right.AcceptedUtc.ToUniversalTime()
                   && left.ExpiryUtc == right.ExpiryUtc.ToUniversalTime();
        }

        private static bool ObjectiveEquals(
            MissionAcgObjectiveBinding left,
            MissionAcgObjectiveBinding right)
        {
            return left != null
                   && right != null
                   && left.FormatVersion == right.FormatVersion
                   && left.AcceptedQuestIdentity.Equals(
                       right.AcceptedQuestIdentity)
                   && left.OwnerIdentity.Equals(right.OwnerIdentity)
                   && EqualIdentity(left.TeamIdentity, right.TeamIdentity)
                   && left.ExplicitNoTeam == right.ExplicitNoTeam
                   && left.MissionType == right.MissionType
                   && left.AllocatedLivePlayfield2
                      == right.AllocatedLivePlayfield2
                   && string.Equals(
                       left.BundleId,
                       right.BundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       left.BundlePayloadSha256,
                       right.BundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && left.BuildingIdentity.Equals(right.BuildingIdentity)
                   && left.CapturedObjectiveSlot == right.CapturedObjectiveSlot
                   && left.CapturedObjectiveIdentity.Equals(
                       right.CapturedObjectiveIdentity)
                   && left.RuntimeObjectiveIdentity.Equals(
                       right.RuntimeObjectiveIdentity)
                   && left.ObjectiveTemplateId == right.ObjectiveTemplateId
                   && string.Equals(
                       left.ObjectiveName,
                       right.ObjectiveName,
                       StringComparison.Ordinal)
                   && left.RequiredInteraction == right.RequiredInteraction
                   && EqualIdentity(
                       left.IssuingTerminalIdentity,
                       right.IssuingTerminalIdentity)
                   && left.RequiredMissionItemTemplateId
                      == right.RequiredMissionItemTemplateId
                   && left.RequiredMachineTemplateId
                      == right.RequiredMachineTemplateId;
        }

        private static bool EqualIdentity(
            MissionAcgIdentityRecord left,
            MissionAcgIdentityRecord right)
        {
            return ReferenceEquals(left, right)
                   || (left != null && left.Equals(right));
        }

        private static string SourceGenerationKey(
            MissionAcgIdentityRecord identity,
            int capturedSlot,
            int spawnGeneration)
        {
            return identity.Type.ToString("X8", CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture)
                   + ":"
                   + capturedSlot.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + spawnGeneration.ToString(CultureInfo.InvariantCulture);
        }

        private static DateTime RequireUtc(DateTime value, string parameter)
        {
            if (value == DateTime.MinValue || value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "A concrete UTC timestamp is required.",
                    parameter);
            }

            return value;
        }
    }

    internal sealed class MissionAcgTokenProgressRecord
    {
        internal MissionAcgTokenProgressRecord(
            MissionAcgTokenProgressState state,
            string recordPath)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            this.State = state;
            this.RecordPath = recordPath ?? string.Empty;
        }

        internal MissionAcgTokenProgressState State { get; private set; }

        internal string RecordPath { get; private set; }

        internal MissionAcgTokenProgressRecord WithState(
            MissionAcgTokenProgressState state)
        {
            return new MissionAcgTokenProgressRecord(state, this.RecordPath);
        }
    }
}
