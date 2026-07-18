namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    /// <summary>
    /// Authoritative player-scoped mission state service. The repository, not this service, owns durable state.
    /// This foundation deliberately emits no client mission-journal packets.
    /// </summary>
    public sealed class PersistentMissionService
    {
        private readonly Dictionary<string, MissionDefinition> definitions;
        private readonly IMissionRepository repository;
        private readonly Func<long> utcNowTicks;

        public PersistentMissionService(
            IMissionRepository repository,
            IEnumerable<MissionDefinition> definitions)
            : this(repository, definitions, () => DateTime.UtcNow.Ticks)
        {
        }

        public PersistentMissionService(
            IMissionRepository repository,
            IEnumerable<MissionDefinition> definitions,
            Func<long> utcNowTicks)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.utcNowTicks = utcNowTicks ?? throw new ArgumentNullException("utcNowTicks");
            this.definitions = ValidateAndIndexDefinitions(definitions);
        }

        public MissionStateRecord GetMission(int characterId, string questId)
        {
            MissionKey key;
            return TryCreateKey(characterId, questId, out key) ? this.repository.GetMission(key) : null;
        }

        public IList<MissionStateRecord> GetMissions(int characterId)
        {
            return characterId > 0 ? this.repository.GetMissions(characterId) : new List<MissionStateRecord>();
        }

        public MissionObjectiveProgressRecord GetObjective(
            int characterId,
            string questId,
            string objectiveId)
        {
            if (characterId <= 0 || string.IsNullOrWhiteSpace(questId)
                || string.IsNullOrWhiteSpace(objectiveId))
            {
                return null;
            }

            MissionCharacterSnapshot snapshot = this.repository.ReadCharacter(characterId);
            return snapshot == null
                       ? null
                       : snapshot.Objectives.FirstOrDefault(
                           objective => string.Equals(
                               objective.QuestId,
                               questId.Trim(),
                               StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(
                                            objective.ObjectiveId,
                                            objectiveId.Trim(),
                                            StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetDefinition(string questId, out MissionDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                definition = null;
                return false;
            }

            return this.definitions.TryGetValue(questId.Trim(), out definition);
        }

        public MissionOperationResult OfferMission(int characterId, string questId)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord existing = transaction.GetMission(key);
                    if (existing != null)
                    {
                        if (existing.State == MissionLifecycleState.Offered
                            || existing.State == MissionLifecycleState.Active
                            || existing.State == MissionLifecycleState.Completed)
                        {
                            return Result(MissionOperationStatus.AlreadyApplied, existing, null, "Mission was already offered.");
                        }

                        return Result(MissionOperationStatus.Rejected, existing, null, "Terminal missions are not repeatable.");
                    }

                    foreach (string prerequisiteQuestId in definition.PrerequisiteQuestIds)
                    {
                        MissionStateRecord prerequisite = transaction.GetMission(
                            new MissionKey(characterId, prerequisiteQuestId));
                        if (prerequisite == null || prerequisite.State != MissionLifecycleState.Completed)
                        {
                            return Result(
                                MissionOperationStatus.Rejected,
                                null,
                                null,
                                "Mission prerequisite is not completed for this character: " + prerequisiteQuestId);
                        }
                    }

                    var record = new MissionStateRecord
                                 {
                                     CharacterId = characterId,
                                     QuestId = definition.QuestId,
                                     State = MissionLifecycleState.Offered,
                                     CurrentStepId = definition.InitialStepId,
                                     OfferedAtUtcTicks = now,
                                     CreatedAtUtcTicks = now,
                                     UpdatedAtUtcTicks = now
                                 };
                    transaction.SaveMission(key, record);

                    foreach (MissionObjectiveDefinition objective in definition.Objectives.Where(value => value.IsResolved))
                    {
                        var objectiveKey = new MissionObjectiveKey(key, objective.ObjectiveId);
                        transaction.SaveObjective(
                            objectiveKey,
                            new MissionObjectiveProgressRecord
                            {
                                CharacterId = characterId,
                                QuestId = definition.QuestId,
                                ObjectiveId = objective.ObjectiveId,
                                Progress = 0,
                                RequiredCount = objective.RequiredCount,
                                CreatedAtUtcTicks = now,
                                UpdatedAtUtcTicks = now
                            });
                    }

                    return Result(MissionOperationStatus.Applied, record, null, "Mission offered.");
                });
        }

        public MissionOperationResult AcceptMission(int characterId, string questId)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord record = transaction.GetMission(key);
                    if (record == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission has not been offered.");
                    }

                    if (record.State == MissionLifecycleState.Active
                        || record.State == MissionLifecycleState.Completed)
                    {
                        return Result(MissionOperationStatus.AlreadyApplied, record, null, "Mission was already accepted.");
                    }

                    if (record.State != MissionLifecycleState.Offered)
                    {
                        return Result(MissionOperationStatus.Rejected, record, null, "Mission cannot be accepted from its current state.");
                    }

                    record.State = MissionLifecycleState.Active;
                    record.AcceptedAtUtcTicks = now;
                    record.UpdatedAtUtcTicks = now;
                    transaction.SaveMission(key, record);
                    return Result(MissionOperationStatus.Applied, record, null, "Mission accepted.");
                });
        }

        public MissionOperationResult ChangeStep(int characterId, string questId, string stepId)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            if (string.IsNullOrWhiteSpace(stepId)
                || !definition.StepIds.Contains(stepId.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Mission step is not defined.");
            }

            string normalizedStepId = stepId.Trim();
            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord record = transaction.GetMission(key);
                    if (record == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
                    }

                    if (record.State != MissionLifecycleState.Active)
                    {
                        return Result(MissionOperationStatus.Rejected, record, null, "Only active missions can change step.");
                    }

                    if (string.Equals(record.CurrentStepId, normalizedStepId, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result(MissionOperationStatus.AlreadyApplied, record, null, "Mission is already on that step.");
                    }

                    record.CurrentStepId = normalizedStepId;
                    record.UpdatedAtUtcTicks = now;
                    transaction.SaveMission(key, record);
                    return Result(MissionOperationStatus.Applied, record, null, "Mission step changed.");
                });
        }

        public MissionOperationResult ObserveObjective(MissionObjectiveObservation observation)
        {
            if (observation == null || observation.Amount <= 0 || string.IsNullOrWhiteSpace(observation.ObjectiveId)
                || string.IsNullOrWhiteSpace(observation.ObservationKey)
                || string.IsNullOrWhiteSpace(observation.EventType))
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Objective observation is incomplete.");
            }

            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(
                observation.CharacterId,
                observation.QuestId,
                out key,
                out definition);
            if (invalid != null)
            {
                return invalid;
            }

            MissionObjectiveDefinition objectiveDefinition = definition.Objectives.FirstOrDefault(
                value => string.Equals(value.ObjectiveId, observation.ObjectiveId, StringComparison.OrdinalIgnoreCase));
            if (objectiveDefinition == null || !objectiveDefinition.IsResolved || objectiveDefinition.RequiredCount <= 0)
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Objective behavior is unresolved.");
            }

            long now = this.Now();
            return this.repository.Execute(
                observation.CharacterId,
                transaction =>
                {
                    MissionStateRecord mission = transaction.GetMission(key);
                    if (mission == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
                    }

                    if (mission.State != MissionLifecycleState.Active)
                    {
                        return Result(MissionOperationStatus.Rejected, mission, null, "Only active missions accept observations.");
                    }

                    if (!string.Equals(
                        mission.CurrentStepId,
                        objectiveDefinition.StepId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return Result(MissionOperationStatus.Rejected, mission, null, "Objective is not part of the current step.");
                    }

                    var objectiveKey = new MissionObjectiveKey(key, objectiveDefinition.ObjectiveId);
                    MissionObjectiveProgressRecord progress = transaction.GetObjective(objectiveKey);
                    if (progress == null)
                    {
                        return Result(MissionOperationStatus.Unresolved, mission, null, "Objective progress was not initialized.");
                    }

                    if (progress.Progress >= progress.RequiredCount)
                    {
                        return Result(MissionOperationStatus.AlreadyApplied, mission, progress, "Objective was already completed.");
                    }

                    bool inserted = transaction.TryAddObservation(
                        new MissionObjectiveObservationRecord
                        {
                            CharacterId = observation.CharacterId,
                            QuestId = key.QuestId,
                            ObjectiveId = objectiveDefinition.ObjectiveId,
                            ObservationKey = observation.ObservationKey.Trim(),
                            EventType = observation.EventType.Trim(),
                            SourceIdentity = observation.SourceIdentity,
                            TargetIdentity = observation.TargetIdentity,
                            ObservedAtUtcTicks = now
                        });
                    if (!inserted)
                    {
                        return Result(
                            MissionOperationStatus.DuplicateObservation,
                            mission,
                            progress,
                            "Duplicate objective observation was ignored.");
                    }

                    progress.Progress = Math.Min(progress.RequiredCount, progress.Progress + observation.Amount);
                    progress.LastObservationKey = observation.ObservationKey.Trim();
                    progress.UpdatedAtUtcTicks = now;
                    transaction.SaveObjective(objectiveKey, progress);
                    return Result(MissionOperationStatus.Applied, mission, progress, "Objective progress updated.");
                });
        }

        public MissionOperationResult CompleteMission(int characterId, string questId)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction => this.CompleteWithinTransaction(transaction, key, definition, now));
        }

        public MissionOperationResult CompleteAndActivateNextMission(
            int characterId,
            string questId,
            string nextQuestId)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            MissionKey nextKey;
            MissionDefinition nextDefinition;
            invalid = this.ResolveMutation(characterId, nextQuestId, out nextKey, out nextDefinition);
            if (invalid != null)
            {
                return invalid;
            }

            if (key.Equals(nextKey))
            {
                return Result(
                    MissionOperationStatus.Unresolved,
                    null,
                    null,
                    "Mission completion cannot activate the same mission.");
            }

            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord current = transaction.GetMission(key);
                    if (current == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
                    }

                    if (current.State != MissionLifecycleState.Active
                        && current.State != MissionLifecycleState.Completed)
                    {
                        return Result(
                            MissionOperationStatus.Rejected,
                            current,
                            null,
                            "Only an active or completed mission can activate its handoff.");
                    }

                    if (current.State == MissionLifecycleState.Active)
                    {
                        foreach (MissionObjectiveDefinition objectiveDefinition in definition.Objectives)
                        {
                            if (!objectiveDefinition.IsResolved || objectiveDefinition.RequiredCount <= 0)
                            {
                                return Result(
                                    MissionOperationStatus.Unresolved,
                                    current,
                                    null,
                                    "Mission has unresolved objective behavior.");
                            }

                            MissionObjectiveProgressRecord objective = transaction.GetObjective(
                                new MissionObjectiveKey(key, objectiveDefinition.ObjectiveId));
                            if (objective == null || objective.Progress < objective.RequiredCount)
                            {
                                return Result(
                                    MissionOperationStatus.Rejected,
                                    current,
                                    objective,
                                    "Mission objectives are incomplete.");
                            }
                        }
                    }

                    MissionStateRecord next = transaction.GetMission(nextKey);
                    if (next != null
                        && next.State != MissionLifecycleState.Offered
                        && next.State != MissionLifecycleState.Active
                        && next.State != MissionLifecycleState.Completed)
                    {
                        return Result(
                            MissionOperationStatus.Rejected,
                            current,
                            null,
                            "The next mission is in a non-repeatable terminal state.");
                    }

                    foreach (string prerequisiteQuestId in nextDefinition.PrerequisiteQuestIds)
                    {
                        if (string.Equals(prerequisiteQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        MissionStateRecord prerequisite = transaction.GetMission(
                            new MissionKey(characterId, prerequisiteQuestId));
                        if (prerequisite == null || prerequisite.State != MissionLifecycleState.Completed)
                        {
                            return Result(
                                MissionOperationStatus.Rejected,
                                current,
                                null,
                                "Next-mission prerequisite is not completed for this character: "
                                + prerequisiteQuestId);
                        }
                    }

                    bool changed = false;
                    if (current.State == MissionLifecycleState.Active)
                    {
                        current.State = MissionLifecycleState.Completed;
                        current.CompletedAtUtcTicks = now;
                        current.UpdatedAtUtcTicks = now;
                        transaction.SaveMission(key, current);
                        changed = true;
                    }

                    if (next == null)
                    {
                        next = new MissionStateRecord
                               {
                                   CharacterId = characterId,
                                   QuestId = nextDefinition.QuestId,
                                   State = MissionLifecycleState.Active,
                                   CurrentStepId = nextDefinition.InitialStepId,
                                   OfferedAtUtcTicks = now,
                                   AcceptedAtUtcTicks = now,
                                   CreatedAtUtcTicks = now,
                                   UpdatedAtUtcTicks = now
                               };
                        transaction.SaveMission(nextKey, next);
                        foreach (MissionObjectiveDefinition objective in nextDefinition.Objectives.Where(
                                     value => value.IsResolved))
                        {
                            transaction.SaveObjective(
                                new MissionObjectiveKey(nextKey, objective.ObjectiveId),
                                new MissionObjectiveProgressRecord
                                {
                                    CharacterId = characterId,
                                    QuestId = nextDefinition.QuestId,
                                    ObjectiveId = objective.ObjectiveId,
                                    Progress = 0,
                                    RequiredCount = objective.RequiredCount,
                                    CreatedAtUtcTicks = now,
                                    UpdatedAtUtcTicks = now
                                });
                        }

                        changed = true;
                    }
                    else if (next.State == MissionLifecycleState.Offered)
                    {
                        next.State = MissionLifecycleState.Active;
                        next.AcceptedAtUtcTicks = now;
                        next.UpdatedAtUtcTicks = now;
                        transaction.SaveMission(nextKey, next);
                        changed = true;
                    }

                    return Result(
                        changed ? MissionOperationStatus.Applied : MissionOperationStatus.AlreadyApplied,
                        current,
                        null,
                        changed
                            ? "Mission completed and next mission activated in one repository transaction."
                            : "Mission completion and next-mission activation were already durable.");
                });
        }

        public MissionOperationResult CompleteMissionWithAccountFlag(
            int characterId,
            string accountKey,
            string questId,
            string accountFlagKey,
            string value)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(accountFlagKey))
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Stable account key and account flag key are required.");
            }

            string normalizedAccountKey = accountKey.Trim();
            string normalizedFlagKey = accountFlagKey.Trim();
            long now = this.Now();
            return this.repository.Execute(
                characterId,
                normalizedAccountKey,
                transaction =>
                {
                    MissionAccountFlagRecord flag = transaction.GetAccountFlag(
                        normalizedAccountKey,
                        normalizedFlagKey);
                    if (flag != null
                        && (!string.Equals(flag.SourceQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(flag.Value, value, StringComparison.Ordinal)))
                    {
                        return Result(
                            MissionOperationStatus.Rejected,
                            transaction.GetMission(key),
                            null,
                            "Account flag conflicts with an existing durable value.");
                    }

                    MissionOperationResult completion = this.CompleteWithinTransaction(
                        transaction,
                        key,
                        definition,
                        now);
                    if (!completion.Succeeded)
                    {
                        return completion;
                    }

                    if (flag != null)
                    {
                        return Result(
                            completion.Status == MissionOperationStatus.Applied
                                ? MissionOperationStatus.Applied
                                : MissionOperationStatus.AlreadyApplied,
                            completion.Mission,
                            null,
                            "Mission completion and account flag were already durable.");
                    }

                    transaction.SaveAccountFlag(
                        normalizedAccountKey,
                        new MissionAccountFlagRecord
                        {
                            AccountKey = normalizedAccountKey,
                            FlagKey = normalizedFlagKey,
                            Value = value,
                            SourceQuestId = key.QuestId,
                            CreatedAtUtcTicks = now,
                            UpdatedAtUtcTicks = now
                        });
                    return Result(
                        MissionOperationStatus.Applied,
                        completion.Mission,
                        null,
                        "Mission completed and account access flag persisted.");
                });
        }

        public MissionOperationResult SetAccountFlag(
            int characterId,
            string accountKey,
            string sourceQuestId,
            string accountFlagKey,
            string value)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(
                characterId,
                sourceQuestId,
                out key,
                out definition);
            if (invalid != null)
            {
                return invalid;
            }

            if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(accountFlagKey))
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Stable account key and account flag key are required.");
            }

            string normalizedAccountKey = accountKey.Trim();
            string normalizedFlagKey = accountFlagKey.Trim();
            long now = this.Now();
            return this.repository.Execute(
                characterId,
                normalizedAccountKey,
                transaction =>
                {
                    MissionStateRecord mission = transaction.GetMission(key);
                    if (mission == null || mission.State != MissionLifecycleState.Completed)
                    {
                        return Result(
                            MissionOperationStatus.Rejected,
                            mission,
                            null,
                            "Source mission must be completed before granting account access.");
                    }

                    MissionAccountFlagRecord existing = transaction.GetAccountFlag(
                        normalizedAccountKey,
                        normalizedFlagKey);
                    if (existing != null)
                    {
                        if (string.Equals(existing.SourceQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(existing.Value, value, StringComparison.Ordinal))
                        {
                            return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Account flag already exists.");
                        }

                        return Result(MissionOperationStatus.Rejected, mission, null, "Account flag conflicts with an existing value.");
                    }

                    transaction.SaveAccountFlag(
                        normalizedAccountKey,
                        new MissionAccountFlagRecord
                        {
                            AccountKey = normalizedAccountKey,
                            FlagKey = normalizedFlagKey,
                            Value = value,
                            SourceQuestId = key.QuestId,
                            CreatedAtUtcTicks = now,
                            UpdatedAtUtcTicks = now
                        });
                    return Result(MissionOperationStatus.Applied, mission, null, "Account flag persisted.");
                });
        }

        public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
        {
            if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(flagKey))
            {
                return null;
            }

            return this.repository.GetAccountFlag(accountKey.Trim(), flagKey.Trim());
        }

        public MissionOperationResult SetFlag(
            int characterId,
            string questId,
            string flagKey,
            string value)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            if (string.IsNullOrWhiteSpace(flagKey))
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Mission flag key is required.");
            }

            string normalizedFlagKey = flagKey.Trim();
            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord mission = transaction.GetMission(key);
                    if (mission == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
                    }

                    MissionFlagRecord flag = transaction.GetFlag(key, normalizedFlagKey);
                    if (flag != null && string.Equals(flag.Value, value, StringComparison.Ordinal))
                    {
                        return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission flag already has that value.");
                    }

                    if (flag == null)
                    {
                        flag = new MissionFlagRecord
                               {
                                   CharacterId = characterId,
                                   QuestId = key.QuestId,
                                   FlagKey = normalizedFlagKey,
                                   CreatedAtUtcTicks = now
                               };
                    }

                    flag.Value = value;
                    flag.UpdatedAtUtcTicks = now;
                    transaction.SaveFlag(key, flag);
                    return Result(MissionOperationStatus.Applied, mission, null, "Mission flag persisted.");
                });
        }

        public MissionFlagRecord GetFlag(int characterId, string questId, string flagKey)
        {
            MissionKey key;
            if (!TryCreateKey(characterId, questId, out key) || string.IsNullOrWhiteSpace(flagKey))
            {
                return null;
            }

            return this.repository.Execute(
                characterId,
                transaction => transaction.GetFlag(key, flagKey.Trim()));
        }

        public MissionOperationResult FailMission(int characterId, string questId)
        {
            return this.SetTerminalState(characterId, questId, MissionLifecycleState.Failed);
        }

        public MissionOperationResult AbandonMission(int characterId, string questId)
        {
            return this.SetTerminalState(characterId, questId, MissionLifecycleState.Abandoned);
        }

        public MissionReloadResult ReloadForLogin(int characterId)
        {
            return this.Reload(characterId, MissionReloadReason.Login);
        }

        public MissionReloadResult ReloadForReconnect(int characterId)
        {
            return this.Reload(characterId, MissionReloadReason.Reconnect);
        }

        public MissionReloadResult ReloadForZoning(int characterId)
        {
            return this.Reload(characterId, MissionReloadReason.Zoning);
        }

        public MissionReloadResult ReloadAfterZoneEngineRestart(int characterId)
        {
            return this.Reload(characterId, MissionReloadReason.ZoneEngineRestart);
        }

        public MissionReloadResult Reload(int characterId, MissionReloadReason reason)
        {
            if (characterId <= 0)
            {
                return new MissionReloadResult
                       {
                           CharacterId = characterId,
                           Reason = reason,
                           Snapshot = new MissionCharacterSnapshot(
                               characterId,
                               null,
                               null,
                               null,
                               null),
                           ClientJournalReconciliationSupported = false
                       };
            }

            return new MissionReloadResult
                   {
                       CharacterId = characterId,
                       Reason = reason,
                       Snapshot = this.repository.ReadCharacter(characterId),
                       ClientJournalReconciliationSupported = false
                   };
        }

        private MissionOperationResult SetTerminalState(
            int characterId,
            string questId,
            MissionLifecycleState terminalState)
        {
            MissionKey key;
            MissionDefinition definition;
            MissionOperationResult invalid = this.ResolveMutation(characterId, questId, out key, out definition);
            if (invalid != null)
            {
                return invalid;
            }

            long now = this.Now();
            return this.repository.Execute(
                characterId,
                transaction =>
                {
                    MissionStateRecord record = transaction.GetMission(key);
                    if (record == null)
                    {
                        return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
                    }

                    if (record.State == terminalState)
                    {
                        return Result(MissionOperationStatus.AlreadyApplied, record, null, "Mission is already in that terminal state.");
                    }

                    bool transitionAllowed = terminalState == MissionLifecycleState.Failed
                                                 ? record.State == MissionLifecycleState.Active
                                                 : record.State == MissionLifecycleState.Offered
                                                   || record.State == MissionLifecycleState.Active;
                    if (!transitionAllowed)
                    {
                        return Result(MissionOperationStatus.Rejected, record, null, "Invalid terminal mission transition.");
                    }

                    record.State = terminalState;
                    record.UpdatedAtUtcTicks = now;
                    if (terminalState == MissionLifecycleState.Failed)
                    {
                        record.FailedAtUtcTicks = now;
                    }
                    else
                    {
                        record.AbandonedAtUtcTicks = now;
                    }

                    transaction.SaveMission(key, record);
                    return Result(MissionOperationStatus.Applied, record, null, "Mission entered terminal state.");
                });
        }

        private MissionOperationResult CompleteWithinTransaction(
            IMissionRepositoryTransaction transaction,
            MissionKey key,
            MissionDefinition definition,
            long now)
        {
            MissionStateRecord record = transaction.GetMission(key);
            if (record == null)
            {
                return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
            }

            if (record.State == MissionLifecycleState.Completed)
            {
                return Result(MissionOperationStatus.AlreadyApplied, record, null, "Mission was already completed.");
            }

            if (record.State != MissionLifecycleState.Active)
            {
                return Result(MissionOperationStatus.Rejected, record, null, "Only active missions can complete.");
            }

            foreach (MissionObjectiveDefinition objectiveDefinition in definition.Objectives)
            {
                if (!objectiveDefinition.IsResolved || objectiveDefinition.RequiredCount <= 0)
                {
                    return Result(MissionOperationStatus.Unresolved, record, null, "Mission has unresolved objective behavior.");
                }

                MissionObjectiveProgressRecord objective = transaction.GetObjective(
                    new MissionObjectiveKey(key, objectiveDefinition.ObjectiveId));
                if (objective == null || objective.Progress < objective.RequiredCount)
                {
                    return Result(MissionOperationStatus.Rejected, record, objective, "Mission objectives are incomplete.");
                }
            }

            record.State = MissionLifecycleState.Completed;
            record.CompletedAtUtcTicks = now;
            record.UpdatedAtUtcTicks = now;
            transaction.SaveMission(key, record);
            return Result(MissionOperationStatus.Applied, record, null, "Mission completed.");
        }

        private MissionOperationResult ResolveMutation(
            int characterId,
            string questId,
            out MissionKey key,
            out MissionDefinition definition)
        {
            if (!TryCreateKey(characterId, questId, out key))
            {
                definition = null;
                return Result(MissionOperationStatus.Unresolved, null, null, "Stable character and quest identities are required.");
            }

            if (!this.definitions.TryGetValue(key.QuestId, out definition) || definition == null
                || !definition.IsResolved)
            {
                return Result(MissionOperationStatus.Unresolved, null, null, "Mission definition is unresolved.");
            }

            return null;
        }

        private long Now()
        {
            long value = this.utcNowTicks();
            if (value <= 0)
            {
                throw new InvalidOperationException("Mission clock returned an invalid UTC tick value.");
            }

            return value;
        }

        private static bool TryCreateKey(int characterId, string questId, out MissionKey key)
        {
            if (characterId <= 0 || string.IsNullOrWhiteSpace(questId))
            {
                key = default(MissionKey);
                return false;
            }

            key = new MissionKey(characterId, questId);
            return true;
        }

        private static MissionOperationResult Result(
            MissionOperationStatus status,
            MissionStateRecord mission,
            MissionObjectiveProgressRecord objective,
            string message)
        {
            return new MissionOperationResult
                   {
                       Status = status,
                       Mission = mission == null ? null : mission.Clone(),
                       Objective = objective == null ? null : objective.Clone(),
                       Message = message
                   };
        }

        private static Dictionary<string, MissionDefinition> ValidateAndIndexDefinitions(
            IEnumerable<MissionDefinition> definitions)
        {
            var indexed = new Dictionary<string, MissionDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (MissionDefinition definition in definitions ?? Enumerable.Empty<MissionDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId))
                {
                    throw new InvalidOperationException("Mission definitions require a quest identity.");
                }

                definition.QuestId = definition.QuestId.Trim();
                if (indexed.ContainsKey(definition.QuestId))
                {
                    throw new InvalidOperationException("Duplicate mission definition: " + definition.QuestId);
                }

                definition.StepIds = (definition.StepIds ?? new string[0])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList();
                if (definition.StepIds.Count != definition.StepIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    throw new InvalidOperationException("Duplicate mission step identity: " + definition.QuestId);
                }

                definition.PrerequisiteQuestIds = (definition.PrerequisiteQuestIds ?? new string[0])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList();
                if (definition.PrerequisiteQuestIds.Count
                    != definition.PrerequisiteQuestIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    throw new InvalidOperationException("Duplicate mission prerequisite: " + definition.QuestId);
                }

                definition.Objectives = (definition.Objectives ?? new MissionObjectiveDefinition[0]).ToList();
                if (definition.IsResolved)
                {
                    if (string.IsNullOrWhiteSpace(definition.InitialStepId)
                        || !definition.StepIds.Contains(definition.InitialStepId.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Resolved mission initial step is invalid: " + definition.QuestId);
                    }

                    definition.InitialStepId = definition.InitialStepId.Trim();
                }

                var objectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MissionObjectiveDefinition objective in definition.Objectives)
                {
                    if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId)
                        || !objectiveIds.Add(objective.ObjectiveId.Trim()))
                    {
                        throw new InvalidOperationException("Missing or duplicate mission objective: " + definition.QuestId);
                    }

                    objective.ObjectiveId = objective.ObjectiveId.Trim();
                    if (objective.IsResolved
                        && (objective.RequiredCount <= 0 || string.IsNullOrWhiteSpace(objective.StepId)
                            || !definition.StepIds.Contains(objective.StepId.Trim(), StringComparer.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("Resolved mission objective is invalid: " + objective.ObjectiveId);
                    }

                    if (!string.IsNullOrWhiteSpace(objective.StepId))
                    {
                        objective.StepId = objective.StepId.Trim();
                    }
                }

                indexed.Add(definition.QuestId, definition);
            }

            foreach (MissionDefinition definition in indexed.Values)
            {
                foreach (string prerequisite in definition.PrerequisiteQuestIds)
                {
                    if (string.Equals(prerequisite, definition.QuestId, StringComparison.OrdinalIgnoreCase)
                        || !indexed.ContainsKey(prerequisite))
                    {
                        throw new InvalidOperationException(
                            "Mission prerequisite is missing or self-referential: " + definition.QuestId);
                    }
                }
            }

            return indexed;
        }
    }
}
