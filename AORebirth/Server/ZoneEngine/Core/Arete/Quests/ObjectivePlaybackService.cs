namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class ObjectivePlaybackService
    {
        private const string KillCountObjectiveType = "CapturedKillCountObjective";
        private const string UseInteractObjectiveType = "CapturedUseInteractObjective";
        private const string TalkToNpcObjectiveType = "CapturedTalkToNpcObjective";

        private readonly Dictionary<string, ObjectiveProgressRecord> progressByKey =
            new Dictionary<string, ObjectiveProgressRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly QuestContentRegistry registry;

        public ObjectivePlaybackService(QuestContentRegistry registry)
        {
            this.registry = registry;
        }

        public ObjectivePlaybackObservationResult Observe(ObjectivePlaybackObservation observation)
        {
            var validation = new AreteValidationResult();
            var matchedProgress = new List<ObjectiveProgressRecord>();
            var ignoredProgress = new List<ObjectiveProgressRecord>();

            if (observation == null)
            {
                validation.AddError("objectivePlayback", "observation is missing");
                return new ObjectivePlaybackObservationResult(
                    observation,
                    matchedProgress,
                    ignoredProgress,
                    validation);
            }

            if (string.IsNullOrWhiteSpace(observation.ObservationType))
            {
                validation.AddError("objectivePlayback", "observation type is missing");
                return new ObjectivePlaybackObservationResult(
                    observation,
                    matchedProgress,
                    ignoredProgress,
                    validation);
            }

            foreach (ObjectiveBinding binding in this.GetSupportedObjectiveBindings())
            {
                if (!IsRelevant(binding.Objective, observation))
                {
                    continue;
                }

                ObjectiveProgressRecord progress = this.GetOrCreateProgress(
                    binding.Quest,
                    binding.Objective);

                if (this.Matches(binding, observation))
                {
                    progress.MatchedEvidenceCount++;
                    progress.LastMatchedEvidenceReference = observation.EvidenceReference;

                    if (!progress.Completed)
                    {
                        progress.CurrentCount = Math.Min(
                            progress.CurrentCount + 1,
                            progress.RequiredCount);
                        progress.Completed = progress.CurrentCount >= progress.RequiredCount;
                    }

                    matchedProgress.Add(progress);
                }
                else
                {
                    progress.IgnoredEvidenceCount++;
                    ignoredProgress.Add(progress);
                }
            }

            return new ObjectivePlaybackObservationResult(
                observation,
                matchedProgress,
                ignoredProgress,
                validation);
        }

        public ObjectivePlaybackReplayResult ReplayStoredObjectiveEvidence()
        {
            var validation = new AreteValidationResult();
            var results = new List<ObjectivePlaybackObservationResult>();

            foreach (ObjectiveBinding binding in this.GetSupportedObjectiveBindings())
            {
                IList<ObjectivePlaybackObservation> observations =
                    this.CreateObservationsFromStoredEvidence(binding, validation);

                foreach (ObjectivePlaybackObservation observation in observations)
                {
                    ObjectivePlaybackObservationResult result = this.Observe(observation);
                    results.Add(result);
                    validation.AddErrors(result.Validation);
                }
            }

            return new ObjectivePlaybackReplayResult(results, this.GetAllProgress(), validation);
        }

        public ObjectiveProgressRecord GetProgress(string missionId, string objectiveId)
        {
            ObjectiveProgressRecord progress;
            if (this.progressByKey.TryGetValue(MakeKey(missionId, objectiveId), out progress))
            {
                return progress;
            }

            QuestDefinition quest;
            QuestObjective objective;
            if (this.TryFindObjective(missionId, objectiveId, out quest, out objective))
            {
                return this.GetOrCreateProgress(quest, objective);
            }

            return null;
        }

        public IList<ObjectiveProgressRecord> GetAllProgress()
        {
            foreach (ObjectiveBinding binding in this.GetSupportedObjectiveBindings())
            {
                this.GetOrCreateProgress(binding.Quest, binding.Objective);
            }

            return this.progressByKey.Values
                .OrderBy(progress => progress.MissionId)
                .ThenBy(progress => progress.ObjectiveId)
                .ToList();
        }

        private IList<ObjectivePlaybackObservation> CreateObservationsFromStoredEvidence(
            ObjectiveBinding binding,
            AreteValidationResult validation)
        {
            var observations = new List<ObjectivePlaybackObservation>();
            QuestAction evidenceAction = binding.EvidenceAction;
            IDictionary<string, string> parameters =
                evidenceAction == null ? null : evidenceAction.Parameters;

            if (IsObjectiveType(binding.Objective, KillCountObjectiveType))
            {
                string targetName = GetParameter(parameters, "targetName");
                string deathSignal = GetParameter(parameters, "deathSignal");
                IList<string> references = SplitEvidenceReferences(
                    GetParameter(parameters, "observedDeathReferences"));

                if (references.Count == 0)
                {
                    validation.AddError(binding.Objective.ObjectiveId, "missing stored death evidence references");
                    return observations;
                }

                foreach (string reference in references)
                {
                    observations.Add(
                        new ObjectivePlaybackObservation
                        {
                            ObservationType = ObjectivePlaybackObservationTypes.EnemyDeathObserved,
                            EvidenceReference = reference,
                            TargetName = targetName,
                            CapturedSignal = deathSignal
                        });
                }
            }
            else if (IsObjectiveType(binding.Objective, UseInteractObjectiveType))
            {
                string reference = FirstEvidenceReference(GetParameter(parameters, "usePacketReferences"));
                if (string.IsNullOrWhiteSpace(reference))
                {
                    validation.AddError(binding.Objective.ObjectiveId, "missing stored use-interaction evidence reference");
                    return observations;
                }

                observations.Add(
                    new ObjectivePlaybackObservation
                    {
                        ObservationType = ObjectivePlaybackObservationTypes.UseInteractionObserved,
                        EvidenceReference = reference,
                        TargetName = GetParameter(parameters, "targetName"),
                        TargetIdentity = GetParameter(parameters, "targetIdentityCandidate"),
                        CapturedSignal = GetParameter(parameters, "useSignal"),
                        ActionName = "Use"
                    });
            }
            else if (IsObjectiveType(binding.Objective, TalkToNpcObjectiveType))
            {
                string reference = FirstEvidenceReference(GetParameter(parameters, "talkPacketReferences"));
                if (string.IsNullOrWhiteSpace(reference))
                {
                    validation.AddError(binding.Objective.ObjectiveId, "missing stored NPC talk evidence reference");
                    return observations;
                }

                observations.Add(
                    new ObjectivePlaybackObservation
                    {
                        ObservationType = ObjectivePlaybackObservationTypes.NpcTalkObserved,
                        EvidenceReference = reference,
                        TargetName = GetParameter(parameters, "targetName"),
                        TargetIdentity = FirstNonEmpty(
                            GetParameter(parameters, "targetIdentity"),
                            binding.Objective.TargetIdentity),
                        CapturedSignal = GetParameter(parameters, "talkSignal")
                    });
            }

            return observations;
        }

        private IEnumerable<ObjectiveBinding> GetSupportedObjectiveBindings()
        {
            if (this.registry == null)
            {
                return Enumerable.Empty<ObjectiveBinding>();
            }

            var bindings = new List<ObjectiveBinding>();
            foreach (QuestDefinition quest in this.registry.GetQuests())
            {
                if (quest == null)
                {
                    continue;
                }

                foreach (QuestStep step in quest.Steps ?? Enumerable.Empty<QuestStep>())
                {
                    if (step == null)
                    {
                        continue;
                    }

                    foreach (QuestObjective objective in step.Objectives ?? Enumerable.Empty<QuestObjective>())
                    {
                        if (objective == null || !IsSupportedObjective(objective))
                        {
                            continue;
                        }

                        bindings.Add(
                            new ObjectiveBinding(
                                quest,
                                step,
                                objective,
                                FindObjectiveEvidenceAction(step, objective)));
                    }
                }
            }

            return bindings;
        }

        private bool Matches(ObjectiveBinding binding, ObjectivePlaybackObservation observation)
        {
            if (IsObjectiveType(binding.Objective, KillCountObjectiveType))
            {
                string targetName = GetParameter(
                    binding.EvidenceAction == null ? null : binding.EvidenceAction.Parameters,
                    "targetName");

                return string.Equals(
                    targetName,
                    observation.TargetName,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (IsObjectiveType(binding.Objective, UseInteractObjectiveType))
            {
                return IsUseSignal(observation.CapturedSignal)
                       || string.Equals(observation.ActionName, "Use", StringComparison.OrdinalIgnoreCase);
            }

            if (IsObjectiveType(binding.Objective, TalkToNpcObjectiveType))
            {
                string targetIdentity = FirstNonEmpty(
                    binding.Objective.TargetIdentity,
                    GetParameter(
                        binding.EvidenceAction == null ? null : binding.EvidenceAction.Parameters,
                        "targetIdentity"));

                return string.Equals(
                    targetIdentity,
                    observation.TargetIdentity,
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private ObjectiveProgressRecord GetOrCreateProgress(
            QuestDefinition quest,
            QuestObjective objective)
        {
            string key = MakeKey(quest == null ? null : quest.QuestId, objective == null ? null : objective.ObjectiveId);
            ObjectiveProgressRecord progress;
            if (this.progressByKey.TryGetValue(key, out progress))
            {
                return progress;
            }

            progress = new ObjectiveProgressRecord
            {
                MissionId = quest == null ? null : quest.QuestId,
                ObjectiveId = objective == null ? null : objective.ObjectiveId,
                ObjectiveType = objective == null ? null : objective.Type,
                RequiredCount = EffectiveRequiredCount(objective)
            };

            this.progressByKey[key] = progress;
            return progress;
        }

        private bool TryFindObjective(
            string missionId,
            string objectiveId,
            out QuestDefinition quest,
            out QuestObjective objective)
        {
            quest = null;
            objective = null;

            if (this.registry == null || string.IsNullOrWhiteSpace(missionId) || string.IsNullOrWhiteSpace(objectiveId))
            {
                return false;
            }

            if (!this.registry.TryGetQuest(missionId, out quest) || quest == null)
            {
                return false;
            }

            objective = (quest.Steps ?? Enumerable.Empty<QuestStep>())
                .Where(step => step != null)
                .SelectMany(step => step.Objectives ?? Enumerable.Empty<QuestObjective>())
                .FirstOrDefault(
                    candidate => candidate != null
                                 && string.Equals(
                                     candidate.ObjectiveId,
                                     objectiveId,
                                     StringComparison.OrdinalIgnoreCase));

            return objective != null;
        }

        private static QuestAction FindObjectiveEvidenceAction(
            QuestStep step,
            QuestObjective objective)
        {
            if (step == null || objective == null)
            {
                return null;
            }

            return (step.Actions ?? Enumerable.Empty<QuestAction>())
                .FirstOrDefault(
                    action => action != null
                              && string.Equals(
                                  GetParameter(action.Parameters, "eventKind"),
                                  "objective-trigger-evidence",
                                  StringComparison.OrdinalIgnoreCase)
                              && string.Equals(
                                  GetParameter(action.Parameters, "objectiveId"),
                                  objective.ObjectiveId,
                                  StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRelevant(
            QuestObjective objective,
            ObjectivePlaybackObservation observation)
        {
            if (IsObjectiveType(objective, KillCountObjectiveType))
            {
                return string.Equals(
                    observation.ObservationType,
                    ObjectivePlaybackObservationTypes.EnemyDeathObserved,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (IsObjectiveType(objective, UseInteractObjectiveType))
            {
                return string.Equals(
                    observation.ObservationType,
                    ObjectivePlaybackObservationTypes.UseInteractionObserved,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (IsObjectiveType(objective, TalkToNpcObjectiveType))
            {
                return string.Equals(
                    observation.ObservationType,
                    ObjectivePlaybackObservationTypes.NpcTalkObserved,
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsSupportedObjective(QuestObjective objective)
        {
            return IsObjectiveType(objective, KillCountObjectiveType)
                   || IsObjectiveType(objective, UseInteractObjectiveType)
                   || IsObjectiveType(objective, TalkToNpcObjectiveType);
        }

        private static bool IsObjectiveType(QuestObjective objective, string objectiveType)
        {
            return objective != null
                   && string.Equals(objective.Type, objectiveType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUseSignal(string capturedSignal)
        {
            return string.Equals(capturedSignal, "GenericCmd Action=Use", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(capturedSignal, "Use", StringComparison.OrdinalIgnoreCase);
        }

        private static int EffectiveRequiredCount(QuestObjective objective)
        {
            if (objective != null && objective.RequiredCount > 0)
            {
                return objective.RequiredCount;
            }

            return 1;
        }

        private static string GetParameter(
            IDictionary<string, string> parameters,
            string key)
        {
            string value;
            if (parameters != null
                && !string.IsNullOrWhiteSpace(key)
                && parameters.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        private static IList<string> SplitEvidenceReferences(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            string currentPrefix = null;
            var references = new List<string>();

            foreach (string rawReference in value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(reference => reference.Trim())
                .Where(reference => reference.Length > 0))
            {
                string reference = rawReference;
                int lastColon = reference.LastIndexOf(':');
                bool hasFilePrefix = lastColon > 0
                                     && reference.Substring(0, lastColon)
                                         .IndexOf(".log", StringComparison.OrdinalIgnoreCase) >= 0;

                if (hasFilePrefix)
                {
                    currentPrefix = reference.Substring(0, lastColon + 1);
                }
                else if (currentPrefix != null && IsLineNumberReference(reference))
                {
                    reference = currentPrefix + reference;
                }

                references.Add(reference);
            }

            return references;
        }

        private static string FirstEvidenceReference(string value)
        {
            return SplitEvidenceReferences(value).FirstOrDefault();
        }

        private static bool IsLineNumberReference(string value)
        {
            int unused;
            return int.TryParse(value, out unused);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string MakeKey(string missionId, string objectiveId)
        {
            return (missionId ?? string.Empty) + "|" + (objectiveId ?? string.Empty);
        }

        private sealed class ObjectiveBinding
        {
            public ObjectiveBinding(
                QuestDefinition quest,
                QuestStep step,
                QuestObjective objective,
                QuestAction evidenceAction)
            {
                this.Quest = quest;
                this.Step = step;
                this.Objective = objective;
                this.EvidenceAction = evidenceAction;
            }

            public QuestDefinition Quest { get; private set; }

            public QuestStep Step { get; private set; }

            public QuestObjective Objective { get; private set; }

            public QuestAction EvidenceAction { get; private set; }
        }
    }
}
