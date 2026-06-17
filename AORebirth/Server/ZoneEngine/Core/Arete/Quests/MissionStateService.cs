namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class MissionStateService
    {
        private readonly AreteNoOpActionRecorder actionRecorder;
        private readonly QuestContentRegistry registry;
        private readonly MissionStateStore store;

        public MissionStateService(QuestContentRegistry registry)
            : this(registry, new MissionStateStore(), new AreteNoOpActionRecorder())
        {
        }

        public MissionStateService(
            QuestContentRegistry registry,
            MissionStateStore store,
            AreteNoOpActionRecorder actionRecorder)
        {
            this.registry = registry;
            this.store = store ?? new MissionStateStore();
            this.actionRecorder = actionRecorder ?? new AreteNoOpActionRecorder();
        }

        public MissionStateStore Store
        {
            get
            {
                return this.store;
            }
        }

        public MissionStateResult GetMissionState(string questId)
        {
            var validation = new AreteValidationResult();
            QuestDefinition quest = this.ResolveQuest(questId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            MissionStateRecord record = this.store.GetOrCreate(quest.QuestId);
            return new MissionStateResult(record, Enumerable.Empty<AreteRecordedAction>(), validation);
        }

        public MissionStateResult OfferMission(string questId)
        {
            var validation = new AreteValidationResult();
            QuestDefinition quest = this.ResolveQuest(questId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            this.ValidateChainPrerequisites(quest.QuestId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(this.store.GetOrCreate(quest.QuestId), Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            MissionStateRecord record = this.store.GetOrCreate(quest.QuestId);
            if (record.State != AreteMissionState.NotStarted)
            {
                validation.AddError(quest.QuestId, "mission cannot be offered from state '" + record.State + "'");
                return new MissionStateResult(record, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            record.State = AreteMissionState.Offered;
            record.CurrentStepId = quest.InitialStepId;
            record.LastTransition = "offerMission";

            return this.CreateResult(record, "offerMission", validation);
        }

        public MissionStateResult AcceptMission(string questId)
        {
            var validation = new AreteValidationResult();
            QuestDefinition quest = this.ResolveQuest(questId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            MissionStateRecord record = this.store.GetOrCreate(quest.QuestId);
            if (record.State != AreteMissionState.Offered)
            {
                validation.AddError(quest.QuestId, "mission is not offered");
                return new MissionStateResult(record, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            record.State = AreteMissionState.Active;
            record.CurrentStepId = string.IsNullOrWhiteSpace(record.CurrentStepId) ? quest.InitialStepId : record.CurrentStepId;
            record.LastTransition = "acceptMission";

            return this.CreateResult(record, "acceptMission", validation);
        }

        public MissionStateResult CompleteMission(string questId)
        {
            var validation = new AreteValidationResult();
            QuestDefinition quest = this.ResolveQuest(questId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            MissionStateRecord record = this.store.GetOrCreate(quest.QuestId);
            if (record.State != AreteMissionState.Active)
            {
                validation.AddError(quest.QuestId, "mission is not active");
                return new MissionStateResult(record, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            record.State = AreteMissionState.Completed;
            record.LastTransition = "completeMission";

            return this.CreateResult(record, "completeMission", validation);
        }

        public MissionStateResult FailMission(string questId)
        {
            return this.SetTerminalState(questId, AreteMissionState.Failed, "failMission");
        }

        public MissionStateResult AbandonMission(string questId)
        {
            return this.SetTerminalState(questId, AreteMissionState.Abandoned, "abandonMission");
        }

        private MissionStateResult SetTerminalState(
            string questId,
            AreteMissionState terminalState,
            string transitionName)
        {
            var validation = new AreteValidationResult();
            QuestDefinition quest = this.ResolveQuest(questId, validation);
            if (!validation.IsValid)
            {
                return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            MissionStateRecord record = this.store.GetOrCreate(quest.QuestId);
            if (record.State != AreteMissionState.Offered && record.State != AreteMissionState.Active)
            {
                validation.AddError(quest.QuestId, "mission is not offered or active");
                return new MissionStateResult(record, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            record.State = terminalState;
            record.LastTransition = transitionName;

            return this.CreateResult(record, transitionName, validation);
        }

        private QuestDefinition ResolveQuest(string questId, AreteValidationResult validation)
        {
            if (this.registry == null)
            {
                validation.AddError("missionState", "quest registry is missing");
                return null;
            }

            if (string.IsNullOrWhiteSpace(questId))
            {
                validation.AddError("missionState", "missing mission id");
                return null;
            }

            QuestDefinition quest;
            if (!this.registry.TryGetQuest(questId, out quest))
            {
                validation.AddError(questId, "mission was not found");
                return null;
            }

            return quest;
        }

        private void ValidateChainPrerequisites(string questId, AreteValidationResult validation)
        {
            foreach (QuestChainLinkMetadata link in this.registry.GetLinksTo(questId))
            {
                if (link == null || string.IsNullOrWhiteSpace(link.FromQuestId))
                {
                    continue;
                }

                MissionStateRecord prerequisite = this.store.GetOrCreate(link.FromQuestId);
                if (prerequisite.State != AreteMissionState.Completed)
                {
                    validation.AddError(
                        questId,
                        "mission prerequisite is not completed: '" + link.FromQuestId + "'");
                }
            }
        }

        private MissionStateResult CreateResult(
            MissionStateRecord record,
            string actionType,
            AreteValidationResult validation)
        {
            IList<AreteRecordedAction> recordedActions = new List<AreteRecordedAction>
            {
                this.actionRecorder.RecordMissionStateAction(actionType, record.QuestId, record.CurrentStepId)
            };

            return new MissionStateResult(record, recordedActions, validation);
        }
    }
}
