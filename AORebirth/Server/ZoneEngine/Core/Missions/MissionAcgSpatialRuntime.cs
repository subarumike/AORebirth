namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Mission-scoped Stage 6 spatial authority. Every decision resolves through the allocated
    /// PF2, its durable binding, the exact immutable bundle, and the instance runtime identity.
    /// </summary>
    internal static class MissionAcgSpatialRuntime
    {
        private const double DurablePositionDistance = 2.0d;

        private static readonly TimeSpan DurablePositionInterval = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan DiagnosticThrottle = TimeSpan.FromSeconds(5);

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, SpatialEntry> ByAccepted =
            new Dictionary<int, SpatialEntry>();

        private static readonly Dictionary<int, SpatialEntry> ByPlayfield =
            new Dictionary<int, SpatialEntry>();

        private static readonly HashSet<int> InvalidAccepted = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> NextDiagnosticUtc =
            new Dictionary<int, DateTime>();

        private static MissionAcgLayoutCatalog catalog;

        private static MissionAcgSpatialStateStore store;

        private static bool initialized;

        internal static void Initialize(
            IList<MissionAcgBindingRecord> bindings,
            MissionAcgLayoutCatalog layoutCatalog,
            string missionStateDirectory)
        {
            lock (Sync)
            {
                ByAccepted.Clear();
                ByPlayfield.Clear();
                InvalidAccepted.Clear();
                NextDiagnosticUtc.Clear();
                catalog = layoutCatalog
                          ?? throw new ArgumentNullException("layoutCatalog");
                store = new MissionAcgSpatialStateStore(missionStateDirectory);
                initialized = true;

                foreach (MissionAcgBindingRecord record in
                    bindings ?? new MissionAcgBindingRecord[0])
                {
                    if (!record.State.ReservesPlayfield)
                    {
                        continue;
                    }

                    SpatialEntry ignored;
                    string failure;
                    if (!TryEnsureEntryLocked(record, out ignored, out failure))
                    {
                        InvalidAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance);
                        MissionDiagnostics.Log(
                            "ACG-SPATIAL-RESTORE-BLOCK accepted={0}:{1} bundle={2} livePf2={3} reason={4}",
                            record.Binding.AcceptedQuestIdentity.Type,
                            record.Binding.AcceptedQuestIdentity.Instance,
                            record.Binding.SelectedBundleId,
                            record.Binding.AllocatedLivePlayfield2,
                            failure);
                    }
                }
            }
        }

        internal static bool TryEnsureState(
            MissionAcgBindingRecord record,
            out MissionAcgSpatialState state,
            out MissionAcgSpatialEnvelope envelope,
            out string failure)
        {
            state = null;
            envelope = null;
            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry = null;
                if (!TryEnsureEntryLocked(record, out entry, out failure))
                {
                    return false;
                }

                state = entry.State;
                envelope = entry.Envelope;
                return true;
            }
        }

        internal static bool TryResolveEntryPosition(
            MissionAcgBindingRecord record,
            MissionAcgMaterializedInstance instance,
            out MissionAcgPointRecord position,
            out string failure)
        {
            position = null;
            MissionAcgSpatialState state;
            MissionAcgSpatialEnvelope envelope;
            if (!TryEnsureState(record, out state, out envelope, out failure)
                || instance == null
                || instance.Spawn == null)
            {
                return false;
            }

            if (state.HasLastValidPlayerPosition
                && envelope.Contains(state.LastValidPlayerPosition))
            {
                position =
                    new MissionAcgPointRecord(
                        state.LastValidPlayerPosition.X,
                        state.LastValidPlayerPosition.Y,
                        state.LastValidPlayerPosition.Z);
                return true;
            }

            if (!envelope.Contains(instance.Spawn))
            {
                failure = "Captured mission spawn is outside its derived spatial envelope.";
                return false;
            }

            position =
                new MissionAcgPointRecord(
                    instance.Spawn.X,
                    instance.Spawn.Y,
                    instance.Spawn.Z);
            return true;
        }

        /// <summary>
        /// Returns true when the proposed position is accepted. Non-mission movement passes
        /// through unchanged. A rejected mission move returns a same-instance restoration point.
        /// </summary>
        internal static bool TryValidatePlayerMove(
            ICharacter character,
            Coordinate proposed,
            out Coordinate accepted,
            out string failure)
        {
            accepted = CopyCoordinate(proposed);
            failure = string.Empty;
            if (character == null
                || character.Playfield == null
                || !MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                    character.Playfield.Identity.Instance))
            {
                return true;
            }

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (!ByPlayfield.TryGetValue(
                        character.Playfield.Identity.Instance,
                        out entry)
                    || InvalidAccepted.Contains(
                        entry.Record.Binding.AcceptedQuestIdentity.Instance))
                {
                    accepted = SafeCurrentOrSpawn(character, entry);
                    failure = "Mission spatial ownership is unavailable.";
                    LogRejected(
                        entry,
                        character,
                        "player-move",
                        proposed,
                        null,
                        -1.0d,
                        -1.0d,
                        failure,
                        accepted);
                    return false;
                }

                MissionAcgBindingRecord record = entry.Record;
                if (record.Binding.OwnerIdentity.Instance != character.Identity.Instance
                    || !record.State.CanEnter(DateTime.UtcNow, record.Binding.ExpiryUtc)
                    || entry.State.CleanupState != MissionAcgSpatialCleanupState.Active)
                {
                    accepted = SafeCurrentOrSpawn(character, entry);
                    failure = "Owner, PF2, expiry, or lifecycle is not active.";
                    LogRejected(
                        entry,
                        character,
                        "player-move",
                        proposed,
                        null,
                        -1.0d,
                        -1.0d,
                        failure,
                        accepted);
                    return false;
                }

                if (!IsFinite(proposed)
                    || !entry.Envelope.Contains(proposed.x, proposed.y, proposed.z))
                {
                    accepted = SafeCurrentOrSpawn(character, entry);
                    failure = "Proposed position is non-finite or outside the captured envelope.";
                    LogRejected(
                        entry,
                        character,
                        "player-move",
                        proposed,
                        null,
                        Distance(entry.LastAcceptedPosition, proposed),
                        entry.Envelope.MaximumInternalDistance,
                        failure,
                        accepted);
                    return false;
                }

                double delta = Distance(entry.LastAcceptedPosition, proposed);
                if (double.IsNaN(delta)
                    || double.IsInfinity(delta)
                    || delta > entry.Envelope.MaximumInternalDistance)
                {
                    accepted = SafeCurrentOrSpawn(character, entry);
                    failure = "Movement delta exceeds the complete captured envelope.";
                    LogRejected(
                        entry,
                        character,
                        "player-move",
                        proposed,
                        null,
                        delta,
                        entry.Envelope.MaximumInternalDistance,
                        failure,
                        accepted);
                    return false;
                }

                MissionAcgPointRecord previousAccepted = entry.LastAcceptedPosition;
                var next =
                    new MissionAcgPointRecord(proposed.x, proposed.y, proposed.z);
                entry.LastAcceptedPosition = next;
                accepted = CopyCoordinate(proposed);

                if (!ShouldPersist(entry.State, next, DateTime.UtcNow))
                {
                    return true;
                }

                MissionAcgSpatialState replacement =
                    entry.State.WithLastValidPlayerPosition(next, DateTime.UtcNow);
                string persistenceFailure;
                if (!store.TryWrite(replacement, true, out persistenceFailure))
                {
                    entry.LastAcceptedPosition = previousAccepted;
                    accepted = ToCoordinate(previousAccepted);
                    failure = "Durable player-position update failed: " + persistenceFailure;
                    InvalidAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance);
                    LogRejected(
                        entry,
                        character,
                        "player-move",
                        proposed,
                        null,
                        delta,
                        entry.Envelope.MaximumInternalDistance,
                        failure,
                        accepted);
                    return false;
                }

                entry.State = replacement;
                return true;
            }
        }

        internal static bool TryValidateInteraction(
            ICharacter character,
            MissionAcgMaterializedInstance instance,
            MissionAcgRuntimeObject runtimeObject,
            double maximumDistance,
            string actionType,
            out string failure)
        {
            failure = string.Empty;
            if (character == null
                || character.Playfield == null
                || instance == null
                || runtimeObject == null
                || maximumDistance < 0.0d)
            {
                failure = "Interaction context is incomplete.";
                return false;
            }

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (!ByPlayfield.TryGetValue(
                        character.Playfield.Identity.Instance,
                        out entry)
                    || !ReferenceMatches(entry.Record, instance.BindingRecord)
                    || entry.Record.Binding.OwnerIdentity.Instance
                       != character.Identity.Instance
                    || !entry.Record.State.CanEnter(
                        DateTime.UtcNow,
                        entry.Record.Binding.ExpiryUtc)
                    || entry.State.CleanupState != MissionAcgSpatialCleanupState.Active)
                {
                    failure = "Interaction owner, PF2, binding, or lifecycle does not match.";
                    LogRejected(
                        entry,
                        character,
                        actionType,
                        ToCoordinate(character),
                        runtimeObject.Position,
                        -1.0d,
                        maximumDistance,
                        failure,
                        SafeCurrentOrSpawn(character, entry));
                    return false;
                }

                MissionAcgRuntimeObject exactObject;
                MissionAcgMaterializedInstance exactInstance;
                if (!MissionAcgRuntimeManager.TryResolveObject(
                        character.Identity.Instance,
                        character.Playfield.Identity.Instance,
                        ToIdentity(runtimeObject.Identity.RuntimeIdentity),
                        out exactInstance,
                        out exactObject)
                    || !ReferenceMatches(entry.Record, exactInstance.BindingRecord)
                    || !exactObject.Identity.RuntimeIdentity.Equals(
                        runtimeObject.Identity.RuntimeIdentity))
                {
                    failure = "Runtime identity is stale or belongs to another instance.";
                    LogRejected(
                        entry,
                        character,
                        actionType,
                        ToCoordinate(character),
                        runtimeObject.Position,
                        -1.0d,
                        maximumDistance,
                        failure,
                        SafeCurrentOrSpawn(character, entry));
                    return false;
                }

                Coordinate source = ToCoordinate(character);
                double distance = Distance(source, exactObject.Position);
                bool exactOwnership =
                    entry.Envelope.Contains(source.x, source.y, source.z)
                    && entry.Envelope.Contains(exactObject.Position);
                MissionAcgLineOfSightDecision lineOfSight =
                    MissionAcgLineOfSightPolicy.Evaluate(
                        false,
                        exactOwnership,
                        IsFinite(source) && MissionAcgSpatialValidator.IsFinite(
                            exactObject.Position),
                        exactOwnership);
                if (lineOfSight
                    != MissionAcgLineOfSightDecision.AllowedRangeAndOwnershipOnly
                    || distance > maximumDistance)
                {
                    failure =
                        lineOfSight
                        == MissionAcgLineOfSightDecision.AllowedRangeAndOwnershipOnly
                            ? "Interaction exceeds the production interaction range."
                            : "Interaction segment is spatially invalid.";
                    LogRejected(
                        entry,
                        character,
                        actionType,
                        source,
                        exactObject.Position,
                        distance,
                        maximumDistance,
                        failure,
                        SafeCurrentOrSpawn(character, entry));
                    return false;
                }

                return true;
            }
        }

        internal static bool TryValidateObjectiveRuntimeInteraction(
            ICharacter character,
            MissionAcgBindingRecord binding,
            MissionAcgIdentityRecord runtimeIdentity,
            double maximumDistance,
            string actionType,
            out string failure)
        {
            failure = string.Empty;
            if (character == null
                || character.Playfield == null
                || binding == null
                || runtimeIdentity == null)
            {
                failure = "Objective spatial context is incomplete.";
                return false;
            }

            MissionAcgMaterializedInstance instance;
            MissionAcgRuntimeObject runtimeObject;
            if (!MissionAcgRuntimeManager.TryResolveObject(
                character.Identity.Instance,
                character.Playfield.Identity.Instance,
                ToIdentity(runtimeIdentity),
                out instance,
                out runtimeObject))
            {
                failure = "Objective runtime identity is not registered in this instance.";
                return false;
            }

            return TryValidateInteraction(
                character,
                instance,
                runtimeObject,
                maximumDistance,
                actionType,
                out failure);
        }

        internal static bool TryValidateCombatPair(
            ICharacter first,
            ICharacter second,
            out string failure)
        {
            failure = string.Empty;
            int firstPf = ResolvePlayfield(first);
            int secondPf = ResolvePlayfield(second);
            bool firstManaged =
                firstPf > 0
                && MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(firstPf);
            bool secondManaged =
                secondPf > 0
                && MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(secondPf);
            if (!firstManaged && !secondManaged)
            {
                return true;
            }

            bool firstBound =
                firstPf > 0 && MissionAcgBindingRuntime.IsBoundLivePlayfield(firstPf);
            bool secondBound =
                secondPf > 0 && MissionAcgBindingRuntime.IsBoundLivePlayfield(secondPf);

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry = null;
                if (!firstBound
                    || !secondBound
                    || firstPf != secondPf
                    || !ByPlayfield.TryGetValue(firstPf, out entry)
                    || entry.State.CleanupState != MissionAcgSpatialCleanupState.Active
                    || !entry.Record.State.CanEnter(
                        DateTime.UtcNow,
                        entry.Record.Binding.ExpiryUtc))
                {
                    failure = "Combat participants do not share one active mission PF2.";
                    LogCombatRejected(entry, first, second, failure);
                    return false;
                }

                int owner = entry.Record.Binding.OwnerIdentity.Instance;
                bool firstOwner = first != null && first.Identity.Instance == owner;
                bool secondOwner = second != null && second.Identity.Instance == owner;
                bool firstNpc =
                    first != null
                    && MissionAcgOperationalRuntime.IsOperationalNpc(
                        firstPf,
                        first.Identity);
                bool secondNpc =
                    second != null
                    && MissionAcgOperationalRuntime.IsOperationalNpc(
                        secondPf,
                        second.Identity);
                if (!(firstOwner && secondNpc) && !(secondOwner && firstNpc))
                {
                    failure = "Combat does not connect the exact owner to an operational actor.";
                    LogCombatRejected(entry, first, second, failure);
                    return false;
                }

                Coordinate firstPosition = ToCoordinate(first);
                Coordinate secondPosition = ToCoordinate(second);
                if (!IsFinite(firstPosition)
                    || !IsFinite(secondPosition)
                    || !entry.Envelope.Contains(
                        firstPosition.x,
                        firstPosition.y,
                        firstPosition.z)
                    || !entry.Envelope.Contains(
                        secondPosition.x,
                        secondPosition.y,
                        secondPosition.z)
                    || first.Stats[StatIds.health].Value <= 0
                    || second.Stats[StatIds.health].Value <= 0)
                {
                    if (firstNpc)
                    {
                        RestoreNpcToCapturedSlot(entry, first);
                    }

                    if (secondNpc)
                    {
                        RestoreNpcToCapturedSlot(entry, second);
                    }

                    failure = "Combat position, health, or captured envelope is invalid.";
                    LogCombatRejected(entry, first, second, failure);
                    return false;
                }

                MissionAcgLineOfSightDecision lineOfSight =
                    MissionAcgLineOfSightPolicy.Evaluate(false, true, true, true);
                if (lineOfSight
                    != MissionAcgLineOfSightDecision.AllowedRangeAndOwnershipOnly)
                {
                    failure = "Mission LOS policy returned an unexpected decision.";
                    LogCombatRejected(entry, first, second, failure);
                    return false;
                }

                // Current generated-mission combat contracts require production range and exact
                // ownership, not authoritative geometry. Distance is never reported as clear LOS;
                // a future geometry-required contract evaluates unresolved and must fail closed.
                return true;
            }
        }

        internal static bool RequiresStationaryNpc(
            ICharacter npc,
            ICharacter target,
            out string reason)
        {
            reason = string.Empty;
            if (npc == null || npc.Playfield == null)
            {
                return false;
            }

            int livePf2 = npc.Playfield.Identity.Instance;
            if (!MissionAcgOperationalRuntime.IsOperationalNpc(livePf2, npc.Identity))
            {
                return false;
            }

            string failure;
            if (target != null && !TryValidateCombatPair(npc, target, out failure))
            {
                reason = failure;
                return true;
            }

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (ByPlayfield.TryGetValue(livePf2, out entry))
                {
                    RestoreNpcToCapturedSlot(entry, npc);
                }
            }

            reason = "Generated mission PF2 has no proven navigation graph.";
            return true;
        }

        internal static bool TryValidateExitPosition(
            ICharacter character,
            MissionAcgBindingRecord binding,
            double maximumDistance,
            out string failure)
        {
            failure = string.Empty;
            if (character == null
                || character.Playfield == null
                || binding == null
                || character.Playfield.Identity.Instance
                   != binding.Binding.AllocatedLivePlayfield2
                || character.Identity.Instance != binding.Binding.OwnerIdentity.Instance)
            {
                failure = "Exit owner or PF2 does not match.";
                return false;
            }

            MissionAcgMaterializedInstance instance;
            if (!MissionAcgRuntimeManager.TryResolveByPlayfield(
                binding.Binding.AllocatedLivePlayfield2,
                out instance)
                || instance.Exit == null)
            {
                failure = "Exact captured exit is unavailable.";
                return false;
            }

            MissionAcgRuntimeObject exitObject = null;
            for (int i = 0; i < instance.Objects.Count; i++)
            {
                MissionAcgRuntimeObject candidate = instance.Objects[i];
                if (candidate.Identity.Kind == MissionAcgRuntimeObjectKind.Exit
                    && candidate.Identity.CapturedIdentity.Equals(
                        instance.Exit.CapturedIdentity))
                {
                    exitObject = candidate;
                    break;
                }
            }

            if (exitObject == null)
            {
                failure = "Exact runtime exit identity is unavailable.";
                return false;
            }

            return TryValidateInteraction(
                character,
                instance,
                exitObject,
                maximumDistance,
                "exit",
                out failure);
        }

        internal static void FlushPlayerPosition(ICharacter character)
        {
            if (character == null || character.Playfield == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (!ByPlayfield.TryGetValue(
                        character.Playfield.Identity.Instance,
                        out entry)
                    || entry.Record.Binding.OwnerIdentity.Instance
                       != character.Identity.Instance)
                {
                    return;
                }

                MissionAcgSpatialState replacement =
                    entry.State.WithLastValidPlayerPosition(
                        entry.LastAcceptedPosition,
                        DateTime.UtcNow);
                string failure;
                if (store.TryWrite(replacement, true, out failure))
                {
                    entry.State = replacement;
                }
                else
                {
                    MissionDiagnostics.Log(
                        "ACG-SPATIAL-FLUSH-FAIL accepted={0}:{1} livePf2={2} reason={3}",
                        entry.Record.Binding.AcceptedQuestIdentity.Type,
                        entry.Record.Binding.AcceptedQuestIdentity.Instance,
                        entry.Record.Binding.AllocatedLivePlayfield2,
                        failure);
                }
            }
        }

        internal static void OnBindingStateChanged(MissionAcgBindingRecord record)
        {
            if (record == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (ByAccepted.TryGetValue(
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out entry))
                {
                    entry.Record = record;
                }
            }

            if (RequiresCleanup(record))
            {
                Cleanup(record);
            }
        }

        internal static bool Cleanup(MissionAcgBindingRecord record)
        {
            EnsureInitialized();
            lock (Sync)
            {
                SpatialEntry entry;
                if (!ByAccepted.TryGetValue(
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out entry))
                {
                    string absentFailure;
                    return store.TryDelete(
                        record.Binding.AcceptedQuestIdentity,
                        out absentFailure);
                }

                string failure;
                MissionAcgSpatialState pending = entry.State.BeginCleanup(DateTime.UtcNow);
                if (!store.TryWrite(pending, true, out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-SPATIAL-CLEANUP-FAIL accepted={0}:{1} livePf2={2} phase=pending reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.Binding.AllocatedLivePlayfield2,
                        failure);
                    return false;
                }

                MissionAcgSpatialState completed =
                    pending.CompleteCleanup(DateTime.UtcNow);
                if (!store.TryWrite(completed, true, out failure)
                    || !store.TryDelete(
                        record.Binding.AcceptedQuestIdentity,
                        out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-SPATIAL-CLEANUP-FAIL accepted={0}:{1} livePf2={2} phase=complete reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.Binding.AllocatedLivePlayfield2,
                        failure);
                    return false;
                }

                ByAccepted.Remove(record.Binding.AcceptedQuestIdentity.Instance);
                ByPlayfield.Remove(record.Binding.AllocatedLivePlayfield2);
                InvalidAccepted.Remove(record.Binding.AcceptedQuestIdentity.Instance);
                NextDiagnosticUtc.Remove(record.Binding.OwnerIdentity.Instance);
                return true;
            }
        }

        internal static bool HasRuntimeState(MissionAcgBindingRecord record)
        {
            if (record == null)
            {
                return true;
            }

            EnsureInitialized();
            lock (Sync)
            {
                return ByAccepted.ContainsKey(
                           record.Binding.AcceptedQuestIdentity.Instance)
                       || ByPlayfield.ContainsKey(
                           record.Binding.AllocatedLivePlayfield2);
            }
        }

        private static bool TryEnsureEntryLocked(
            MissionAcgBindingRecord record,
            out SpatialEntry entry,
            out string failure)
        {
            entry = null;
            failure = string.Empty;
            if (record == null || !record.State.ReservesPlayfield)
            {
                failure = "Binding does not reserve an active PF2.";
                return false;
            }

            if (ByAccepted.TryGetValue(
                record.Binding.AcceptedQuestIdentity.Instance,
                out entry))
            {
                if (!ReferenceMatches(entry.Record, record))
                {
                    failure = "Accepted quest spatial ownership changed.";
                    return false;
                }

                entry.Record = record;
                return true;
            }

            if (ByPlayfield.ContainsKey(record.Binding.AllocatedLivePlayfield2))
            {
                failure = "Allocated PF2 already has another spatial owner.";
                return false;
            }

            MissionAcgLayoutBundle bundle =
                catalog.FindByLayoutId(record.Binding.SelectedBundleId);
            if (bundle == null
                || !string.Equals(
                    bundle.GeneratorPayloadSha256,
                    record.Binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !bundle.BuildingIdentity.Equals(record.Binding.AcgBuildingIdentity))
            {
                failure = "Binding bundle, payload hash, or building identity is invalid.";
                return false;
            }

            MissionAcgSpatialEnvelope envelope;
            if (!MissionAcgSpatialEnvelope.TryDerive(bundle, out envelope, out failure))
            {
                return false;
            }

            MissionAcgMaterializedInstance instance;
            string materializationFailure;
            if (!MissionAcgRuntimeManager.TryGetOrMaterialize(
                record,
                out instance,
                out materializationFailure))
            {
                failure = "Runtime materialization is unavailable: " + materializationFailure;
                return false;
            }

            MissionAcgSpatialState restored;
            bool exists;
            if (!store.TryLoad(record.Binding, out restored, out exists, out failure))
            {
                return false;
            }

            if (exists
                && (!envelope.Contains(restored.LastValidPlayerPosition)
                    || restored.CleanupState != MissionAcgSpatialCleanupState.Active))
            {
                failure = "Restored player position or cleanup state is not spatially active.";
                return false;
            }

            MissionAcgSpatialState state =
                restored
                ?? new MissionAcgSpatialState(
                    MissionAcgSpatialState.CurrentFormatVersion,
                    record.Binding.AcceptedQuestIdentity,
                    record.Binding.OwnerIdentity,
                    record.Binding.AllocatedLivePlayfield2,
                    record.Binding.SelectedBundleId,
                    record.Binding.SelectedBundlePayloadSha256,
                    record.Binding.AcgBuildingIdentity,
                    false,
                    instance.Spawn,
                    MissionAcgSpatialCleanupState.Active,
                    DateTime.UtcNow);
            if (!exists && !store.TryWrite(state, false, out failure))
            {
                return false;
            }

            entry = new SpatialEntry(record, instance, envelope, state);
            ByAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance, entry);
            ByPlayfield.Add(record.Binding.AllocatedLivePlayfield2, entry);
            return true;
        }

        private static bool ShouldPersist(
            MissionAcgSpatialState state,
            MissionAcgPointRecord next,
            DateTime nowUtc)
        {
            return !state.HasLastValidPlayerPosition
                   || Distance(state.LastValidPlayerPosition, next)
                      >= DurablePositionDistance
                   || nowUtc - state.UpdatedUtc >= DurablePositionInterval;
        }

        private static void RestoreNpcToCapturedSlot(
            SpatialEntry entry,
            ICharacter npc)
        {
            if (entry == null || npc == null)
            {
                return;
            }

            MissionAcgRuntimeObject runtimeObject;
            MissionAcgMaterializedInstance instance;
            if (!MissionAcgRuntimeManager.TryResolveObject(
                entry.Record.Binding.OwnerIdentity.Instance,
                entry.Record.Binding.AllocatedLivePlayfield2,
                npc.Identity,
                out instance,
                out runtimeObject)
                || runtimeObject.Position == null)
            {
                return;
            }

            Coordinate current = ToCoordinate(npc);
            if (!IsFinite(current)
                || !entry.Envelope.Contains(current.x, current.y, current.z))
            {
                npc.Coordinates(ToCoordinate(runtimeObject.Position));
            }
        }

        private static Coordinate SafeCurrentOrSpawn(
            ICharacter character,
            SpatialEntry entry)
        {
            if (entry != null
                && entry.LastAcceptedPosition != null
                && entry.Envelope.Contains(entry.LastAcceptedPosition))
            {
                return ToCoordinate(entry.LastAcceptedPosition);
            }

            if (entry != null
                && entry.Instance != null
                && entry.Instance.Spawn != null)
            {
                return ToCoordinate(entry.Instance.Spawn);
            }

            Coordinate current = ToCoordinate(character);
            return IsFinite(current) ? current : new Coordinate();
        }

        private static void LogCombatRejected(
            SpatialEntry entry,
            ICharacter first,
            ICharacter second,
            string failure)
        {
            ICharacter actor = first ?? second;
            Coordinate source = ToCoordinate(first);
            Coordinate target = ToCoordinate(second);
            MissionAcgPointRecord targetRecord =
                IsFinite(target)
                    ? new MissionAcgPointRecord(target.x, target.y, target.z)
                    : null;
            LogRejected(
                entry,
                actor,
                "combat",
                source,
                targetRecord,
                Distance(source, target),
                entry == null ? -1.0d : entry.Envelope.MaximumInternalDistance,
                failure,
                SafeCurrentOrSpawn(actor, entry));
        }

        private static void LogRejected(
            SpatialEntry entry,
            ICharacter actor,
            string actionType,
            Coordinate source,
            MissionAcgPointRecord target,
            double measuredDistance,
            double applicableLimit,
            string reason,
            Coordinate restoration)
        {
            int actorInstance = actor == null ? 0 : actor.Identity.Instance;
            DateTime now = DateTime.UtcNow;
            DateTime next;
            if (NextDiagnosticUtc.TryGetValue(actorInstance, out next) && next > now)
            {
                return;
            }

            NextDiagnosticUtc[actorInstance] = now + DiagnosticThrottle;
            MissionDiagnostics.Log(
                "ACG-SPATIAL-REJECT accepted={0}:{1} bundle={2} livePf2={3} actor={4} action={5} source=({6},{7},{8}) target=({9},{10},{11}) distance={12} limit={13} reason={14} restore=({15},{16},{17})",
                entry == null ? 0 : entry.Record.Binding.AcceptedQuestIdentity.Type,
                entry == null ? 0 : entry.Record.Binding.AcceptedQuestIdentity.Instance,
                entry == null ? string.Empty : entry.Record.Binding.SelectedBundleId,
                entry == null ? 0 : entry.Record.Binding.AllocatedLivePlayfield2,
                actorInstance,
                actionType ?? string.Empty,
                source == null ? 0.0f : source.x,
                source == null ? 0.0f : source.y,
                source == null ? 0.0f : source.z,
                target == null ? 0.0f : target.X,
                target == null ? 0.0f : target.Y,
                target == null ? 0.0f : target.Z,
                measuredDistance,
                applicableLimit,
                reason ?? string.Empty,
                restoration == null ? 0.0f : restoration.x,
                restoration == null ? 0.0f : restoration.y,
                restoration == null ? 0.0f : restoration.z);
        }

        private static bool ReferenceMatches(
            MissionAcgBindingRecord first,
            MissionAcgBindingRecord second)
        {
            return first != null
                   && second != null
                   && first.Binding.AcceptedQuestIdentity.Equals(
                       second.Binding.AcceptedQuestIdentity)
                   && first.Binding.AllocatedLivePlayfield2
                      == second.Binding.AllocatedLivePlayfield2
                   && string.Equals(
                       first.Binding.SelectedBundleId,
                       second.Binding.SelectedBundleId,
                       StringComparison.Ordinal);
        }

        private static bool RequiresCleanup(MissionAcgBindingRecord record)
        {
            return record.State.CleanupState != MissionAcgCleanupState.None
                   || record.State.LifecycleState == MissionAcgLifecycleState.Completed
                   || record.State.LifecycleState == MissionAcgLifecycleState.Abandoned
                   || record.State.LifecycleState == MissionAcgLifecycleState.Expired
                   || record.State.LifecycleState == MissionAcgLifecycleState.CleanupPending
                   || record.State.LifecycleState == MissionAcgLifecycleState.Cleaned
                   || record.State.LifecycleState == MissionAcgLifecycleState.Invalid;
        }

        private static int ResolvePlayfield(ICharacter character)
        {
            return character == null || character.Playfield == null
                       ? 0
                       : character.Playfield.Identity.Instance;
        }

        private static bool IsFinite(Coordinate coordinate)
        {
            return coordinate != null
                   && !float.IsNaN(coordinate.x)
                   && !float.IsInfinity(coordinate.x)
                   && !float.IsNaN(coordinate.y)
                   && !float.IsInfinity(coordinate.y)
                   && !float.IsNaN(coordinate.z)
                   && !float.IsInfinity(coordinate.z);
        }

        private static Coordinate CopyCoordinate(Coordinate coordinate)
        {
            return coordinate == null
                       ? new Coordinate()
                       : new Coordinate
                             {
                                 x = coordinate.x,
                                 y = coordinate.y,
                                 z = coordinate.z
                             };
        }

        private static Coordinate ToCoordinate(ICharacter character)
        {
            if (character == null)
            {
                return new Coordinate();
            }

            Coordinate current = character.Coordinates();
            return CopyCoordinate(current);
        }

        private static Coordinate ToCoordinate(MissionAcgPointRecord point)
        {
            return point == null
                       ? new Coordinate()
                       : new Coordinate { x = point.X, y = point.Y, z = point.Z };
        }

        private static double Distance(Coordinate first, Coordinate second)
        {
            if (!IsFinite(first) || !IsFinite(second))
            {
                return double.NaN;
            }

            double dx = first.x - second.x;
            double dy = first.y - second.y;
            double dz = first.z - second.z;
            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        private static double Distance(
            Coordinate first,
            MissionAcgPointRecord second)
        {
            return Distance(first, ToCoordinate(second));
        }

        private static double Distance(
            MissionAcgPointRecord first,
            Coordinate second)
        {
            return Distance(ToCoordinate(first), second);
        }

        private static double Distance(
            MissionAcgPointRecord first,
            MissionAcgPointRecord second)
        {
            return Distance(ToCoordinate(first), ToCoordinate(second));
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord identity)
        {
            return identity == null
                       ? Identity.None
                       : new Identity
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

            if (!initialized)
            {
                throw new InvalidOperationException(
                    "Mission ACG spatial runtime is not initialized.");
            }
        }

        private sealed class SpatialEntry
        {
            internal SpatialEntry(
                MissionAcgBindingRecord record,
                MissionAcgMaterializedInstance instance,
                MissionAcgSpatialEnvelope envelope,
                MissionAcgSpatialState state)
            {
                this.Record = record;
                this.Instance = instance;
                this.Envelope = envelope;
                this.State = state;
                this.LastAcceptedPosition =
                    new MissionAcgPointRecord(
                        state.LastValidPlayerPosition.X,
                        state.LastValidPlayerPosition.Y,
                        state.LastValidPlayerPosition.Z);
            }

            internal MissionAcgBindingRecord Record { get; set; }

            internal MissionAcgMaterializedInstance Instance { get; private set; }

            internal MissionAcgSpatialEnvelope Envelope { get; private set; }

            internal MissionAcgSpatialState State { get; set; }

            internal MissionAcgPointRecord LastAcceptedPosition { get; set; }
        }
    }
}
