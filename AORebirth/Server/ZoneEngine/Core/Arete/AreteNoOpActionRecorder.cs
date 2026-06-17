namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Collections.Generic;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class AreteRecordedAction
    {
        public string SourceType { get; set; }

        public string ActionType { get; set; }

        public string QuestId { get; set; }

        public string StepId { get; set; }

        public string TargetNodeId { get; set; }

        public string TargetIdentity { get; set; }

        public string Text { get; set; }

        public bool WasApplied { get; set; }

        public bool MutatedCharacterState { get; set; }
    }

    public sealed class AreteNoOpActionRecorder
    {
        public IList<AreteRecordedAction> RecordDialogueActions(IEnumerable<DialogueAction> actions)
        {
            var recordedActions = new List<AreteRecordedAction>();
            foreach (DialogueAction action in actions ?? new DialogueAction[0])
            {
                if (action != null)
                {
                    recordedActions.Add(this.Record(action));
                }
            }

            return recordedActions;
        }

        public IList<AreteRecordedAction> RecordQuestActions(IEnumerable<QuestAction> actions)
        {
            var recordedActions = new List<AreteRecordedAction>();
            foreach (QuestAction action in actions ?? new QuestAction[0])
            {
                if (action != null)
                {
                    recordedActions.Add(this.Record(action));
                }
            }

            return recordedActions;
        }

        public AreteRecordedAction Record(DialogueAction action)
        {
            return new AreteRecordedAction
            {
                SourceType = "dialogue",
                ActionType = action == null ? null : action.Type,
                QuestId = action == null ? null : action.QuestId,
                TargetNodeId = action == null ? null : action.TargetNodeId,
                Text = action == null ? null : action.Text,
                WasApplied = false,
                MutatedCharacterState = false
            };
        }

        public AreteRecordedAction Record(QuestAction action)
        {
            return new AreteRecordedAction
            {
                SourceType = "quest",
                ActionType = action == null ? null : action.Type,
                QuestId = action == null ? null : action.QuestId,
                StepId = action == null ? null : action.StepId,
                TargetIdentity = action == null ? null : action.TargetIdentity,
                WasApplied = false,
                MutatedCharacterState = false
            };
        }

        public AreteRecordedAction RecordMissionStateAction(string actionType, string questId, string stepId)
        {
            return new AreteRecordedAction
            {
                SourceType = "missionState",
                ActionType = actionType,
                QuestId = questId,
                StepId = stepId,
                WasApplied = false,
                MutatedCharacterState = false
            };
        }
    }
}
