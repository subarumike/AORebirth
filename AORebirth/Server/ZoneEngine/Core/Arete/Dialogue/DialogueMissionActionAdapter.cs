namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class DialogueMissionActionResult
    {
        public DialogueMissionActionResult(
            string actionType,
            string questId,
            bool endedDialogue,
            MissionStateResult missionStateResult,
            AreteRecordedAction recordedAction,
            AreteValidationResult validation)
        {
            this.ActionType = actionType;
            this.QuestId = questId;
            this.EndedDialogue = endedDialogue;
            this.MissionStateResult = missionStateResult;
            this.RecordedAction = recordedAction;
            this.Validation = validation ?? new AreteValidationResult();
        }

        public string ActionType { get; private set; }

        public string QuestId { get; private set; }

        public bool EndedDialogue { get; private set; }

        public MissionStateResult MissionStateResult { get; private set; }

        public AreteRecordedAction RecordedAction { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }

    public sealed class DialogueMissionActionAdapterResult
    {
        public DialogueMissionActionAdapterResult(
            IEnumerable<DialogueMissionActionResult> actionResults,
            bool endedDialogue,
            AreteValidationResult validation)
        {
            this.ActionResults = new List<DialogueMissionActionResult>(
                actionResults ?? Enumerable.Empty<DialogueMissionActionResult>());
            this.EndedDialogue = endedDialogue;
            this.Validation = validation ?? new AreteValidationResult();
        }

        public IList<DialogueMissionActionResult> ActionResults { get; private set; }

        public bool EndedDialogue { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }

    public sealed class DialogueMissionActionAdapter
    {
        private readonly MissionStateService missionStateService;

        public DialogueMissionActionAdapter(MissionStateService missionStateService)
        {
            this.missionStateService = missionStateService;
        }

        public DialogueMissionActionAdapterResult ExecuteAction(DialogueAction action)
        {
            return this.ExecuteActionsForSession(null, new[] { action });
        }

        public DialogueMissionActionAdapterResult ExecuteActions(IEnumerable<DialogueAction> actions)
        {
            return this.ExecuteActionsForSession(null, actions);
        }

        public DialogueMissionActionAdapterResult ExecuteActionsForSession(
            DialogueSession session,
            IEnumerable<DialogueAction> actions)
        {
            var validation = new AreteValidationResult();
            var actionResults = new List<DialogueMissionActionResult>();
            bool endedDialogue = false;

            foreach (DialogueAction action in actions ?? Enumerable.Empty<DialogueAction>())
            {
                DialogueMissionActionResult actionResult = this.ExecuteSingleAction(session, action);
                actionResults.Add(actionResult);
                validation.AddErrors(actionResult.Validation);

                if (actionResult.EndedDialogue)
                {
                    endedDialogue = true;
                }
            }

            return new DialogueMissionActionAdapterResult(actionResults, endedDialogue, validation);
        }

        private DialogueMissionActionResult ExecuteSingleAction(DialogueSession session, DialogueAction action)
        {
            var validation = new AreteValidationResult();
            if (action == null)
            {
                validation.AddError("dialogueAction", "dialogue action is missing");
                return this.CreateActionResult(null, null, false, null, false, validation);
            }

            if (string.IsNullOrWhiteSpace(action.Type))
            {
                validation.AddError("dialogueAction", "missing dialogue action type");
                return this.CreateActionResult(action.Type, action.QuestId, false, null, false, validation);
            }

            if (IsEndDialogueAction(action.Type))
            {
                if (session != null)
                {
                    session.IsActive = false;
                }

                return this.CreateActionResult(action.Type, action.QuestId, true, null, true, validation);
            }

            MissionStateResult missionStateResult = this.ExecuteMissionAction(action, validation);
            bool applied = missionStateResult != null && missionStateResult.IsValid;

            return this.CreateActionResult(
                action.Type,
                action.QuestId,
                false,
                missionStateResult,
                applied,
                validation);
        }

        private MissionStateResult ExecuteMissionAction(DialogueAction action, AreteValidationResult validation)
        {
            if (this.missionStateService == null)
            {
                validation.AddError("dialogueMissionActionAdapter", "mission state service is missing");
                return null;
            }

            MissionStateResult result;
            if (IsAction(action.Type, "OfferMission"))
            {
                result = this.missionStateService.OfferMission(action.QuestId);
            }
            else if (IsAction(action.Type, "AcceptMission"))
            {
                result = this.missionStateService.AcceptMission(action.QuestId);
            }
            else if (IsAction(action.Type, "CompleteMission"))
            {
                result = this.missionStateService.CompleteMission(action.QuestId);
            }
            else if (IsAction(action.Type, "FailMission"))
            {
                result = this.missionStateService.FailMission(action.QuestId);
            }
            else if (IsAction(action.Type, "AbandonMission"))
            {
                result = this.missionStateService.AbandonMission(action.QuestId);
            }
            else
            {
                validation.AddError(
                    "dialogueAction",
                    "unsupported dialogue action type '" + action.Type + "'");

                return null;
            }

            validation.AddErrors(result == null ? null : result.Validation);
            return result;
        }

        private DialogueMissionActionResult CreateActionResult(
            string actionType,
            string questId,
            bool endedDialogue,
            MissionStateResult missionStateResult,
            bool applied,
            AreteValidationResult validation)
        {
            var recordedAction = new AreteRecordedAction
            {
                SourceType = "dialogueMissionAdapter",
                ActionType = actionType,
                QuestId = questId,
                WasApplied = applied,
                MutatedCharacterState = false
            };

            return new DialogueMissionActionResult(
                actionType,
                questId,
                endedDialogue,
                missionStateResult,
                recordedAction,
                validation);
        }

        private static bool IsAction(string actualType, string expectedType)
        {
            return string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEndDialogueAction(string actionType)
        {
            return IsAction(actionType, "EndDialogue");
        }
    }
}
