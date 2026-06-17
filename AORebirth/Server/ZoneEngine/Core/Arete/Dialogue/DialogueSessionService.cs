namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class DialogueSessionService
    {
        private readonly AreteNoOpActionRecorder actionRecorder;
        private readonly AreteNoOpConditionEvaluator conditionEvaluator;
        private readonly DialogueContentRegistry registry;

        public DialogueSessionService(DialogueContentRegistry registry)
            : this(registry, new AreteNoOpConditionEvaluator(), new AreteNoOpActionRecorder())
        {
        }

        public DialogueSessionService(
            DialogueContentRegistry registry,
            AreteNoOpConditionEvaluator conditionEvaluator,
            AreteNoOpActionRecorder actionRecorder)
        {
            this.registry = registry;
            this.conditionEvaluator = conditionEvaluator ?? new AreteNoOpConditionEvaluator();
            this.actionRecorder = actionRecorder ?? new AreteNoOpActionRecorder();
        }

        public DialogueSessionResult StartSession(string npcIdentity)
        {
            var validation = new AreteValidationResult();
            DialogueNpcEntry npc = this.ResolveNpc(npcIdentity, validation);
            if (!validation.IsValid)
            {
                return this.CreateResult(null, null, null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            if (string.IsNullOrWhiteSpace(npc.RootNodeId))
            {
                validation.AddError(npc.NpcIdentity, "missing start dialogue node");
                return this.CreateResult(null, npc, null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            DialogueNode startNode = FindNode(npc, npc.RootNodeId);
            if (startNode == null)
            {
                validation.AddError(npc.NpcIdentity, "start dialogue node '" + npc.RootNodeId + "' was not found");
                return this.CreateResult(null, npc, null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            var session = new DialogueSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                NpcIdentity = npc.NpcIdentity,
                CurrentNodeId = startNode.Id,
                IsActive = true
            };

            IList<AreteRecordedAction> recordedActions = this.actionRecorder.RecordDialogueActions(startNode.EnterActions);
            return this.CreateResult(session, npc, startNode, recordedActions, validation);
        }

        public IList<DialogueOption> ListAvailableOptions(DialogueSession session)
        {
            DialogueNpcEntry npc;
            DialogueNode currentNode;
            if (!this.TryResolveSessionNode(session, out npc, out currentNode))
            {
                return new List<DialogueOption>();
            }

            return this.ListAvailableOptions(currentNode);
        }

        public DialogueSessionResult SelectOption(DialogueSession session, int optionIndex)
        {
            var validation = new AreteValidationResult();
            DialogueNpcEntry npc;
            DialogueNode currentNode;

            if (!this.TryResolveSessionNode(session, validation, out npc, out currentNode))
            {
                return this.CreateResult(session, npc, currentNode, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            DialogueOption selectedOption = this.ListAvailableOptions(currentNode)
                .FirstOrDefault(option => option != null && option.Index == optionIndex);

            if (selectedOption == null)
            {
                validation.AddError(session.NpcIdentity, "dialogue option was not available");
                return this.CreateResult(session, npc, currentNode, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            var recordedActions = new List<AreteRecordedAction>(
                this.actionRecorder.RecordDialogueActions(selectedOption.Actions));

            if (IsCloseTransition(selectedOption))
            {
                session.IsActive = false;
                return this.CreateResult(session, npc, currentNode, recordedActions, validation);
            }

            if (string.IsNullOrWhiteSpace(selectedOption.NextNodeId))
            {
                validation.AddError(session.NpcIdentity, "missing dialogue node target");
                return this.CreateResult(session, npc, currentNode, recordedActions, validation);
            }

            string nextNodeId = ResolveSpecialNodeTarget(selectedOption.NextNodeId, npc, currentNode);
            DialogueNode nextNode = FindNode(npc, nextNodeId);
            if (nextNode == null)
            {
                validation.AddError(
                    session.NpcIdentity,
                    "dialogue node target '" + selectedOption.NextNodeId + "' was not found");

                return this.CreateResult(session, npc, currentNode, recordedActions, validation);
            }

            session.CurrentNodeId = nextNode.Id;
            foreach (AreteRecordedAction recordedAction in this.actionRecorder.RecordDialogueActions(nextNode.EnterActions))
            {
                recordedActions.Add(recordedAction);
            }

            return this.CreateResult(session, npc, nextNode, recordedActions, validation);
        }

        public DialogueSessionResult EndSession(DialogueSession session)
        {
            var validation = new AreteValidationResult();
            if (session == null)
            {
                validation.AddError("dialogueSession", "dialogue session is missing");
                return this.CreateResult(null, null, null, Enumerable.Empty<AreteRecordedAction>(), validation);
            }

            session.IsActive = false;
            return this.CreateResult(session, null, null, Enumerable.Empty<AreteRecordedAction>(), validation);
        }

        private DialogueNpcEntry ResolveNpc(string npcIdentity, AreteValidationResult validation)
        {
            if (this.registry == null)
            {
                validation.AddError("dialogueSession", "dialogue registry is missing");
                return null;
            }

            if (string.IsNullOrWhiteSpace(npcIdentity))
            {
                validation.AddError("dialogueSession", "missing NPC identity");
                return null;
            }

            DialogueNpcEntry npc;
            if (!this.registry.TryGetNpc(npcIdentity, out npc))
            {
                validation.AddError(npcIdentity, "dialogue NPC was not found");
                return null;
            }

            return npc;
        }

        private bool TryResolveSessionNode(DialogueSession session, out DialogueNpcEntry npc, out DialogueNode currentNode)
        {
            return this.TryResolveSessionNode(session, new AreteValidationResult(), out npc, out currentNode);
        }

        private bool TryResolveSessionNode(
            DialogueSession session,
            AreteValidationResult validation,
            out DialogueNpcEntry npc,
            out DialogueNode currentNode)
        {
            npc = null;
            currentNode = null;

            if (session == null)
            {
                validation.AddError("dialogueSession", "dialogue session is missing");
                return false;
            }

            if (!session.IsActive)
            {
                validation.AddError(session.NpcIdentity, "dialogue session is not active");
                return false;
            }

            npc = this.ResolveNpc(session.NpcIdentity, validation);
            if (!validation.IsValid)
            {
                return false;
            }

            currentNode = FindNode(npc, session.CurrentNodeId);
            if (currentNode == null)
            {
                validation.AddError(session.NpcIdentity, "current dialogue node '" + session.CurrentNodeId + "' was not found");
                return false;
            }

            return true;
        }

        private DialogueSessionResult CreateResult(
            DialogueSession session,
            DialogueNpcEntry npc,
            DialogueNode currentNode,
            IEnumerable<AreteRecordedAction> recordedActions,
            AreteValidationResult validation)
        {
            IEnumerable<DialogueOption> availableOptions =
                session != null && session.IsActive && currentNode != null
                    ? this.ListAvailableOptions(currentNode)
                    : Enumerable.Empty<DialogueOption>();

            return new DialogueSessionResult(session, currentNode, availableOptions, recordedActions, validation);
        }

        private IList<DialogueOption> ListAvailableOptions(DialogueNode node)
        {
            var availableOptions = new List<DialogueOption>();
            foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
            {
                if (option != null && this.conditionEvaluator.AreDialogueConditionsSatisfied(option.Conditions))
                {
                    availableOptions.Add(option);
                }
            }

            return availableOptions;
        }

        private static DialogueNode FindNode(DialogueNpcEntry npc, string nodeId)
        {
            if (npc == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return (npc.Nodes ?? Enumerable.Empty<DialogueNode>())
                .FirstOrDefault(node => node != null && string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveSpecialNodeTarget(string targetNodeId, DialogueNpcEntry npc, DialogueNode currentNode)
        {
            if (string.Equals(targetNodeId, "root", StringComparison.OrdinalIgnoreCase))
            {
                return npc.RootNodeId;
            }

            if (string.Equals(targetNodeId, "self", StringComparison.OrdinalIgnoreCase))
            {
                return currentNode.Id;
            }

            return targetNodeId;
        }

        private static bool IsCloseTransition(DialogueOption option)
        {
            if (option == null)
            {
                return false;
            }

            if (string.Equals(option.NextNodeId, "close", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.NextNodeId, "end", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (DialogueAction action in option.Actions ?? Enumerable.Empty<DialogueAction>())
            {
                if (action != null
                    && (string.Equals(action.Type, "closeDialogue", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(action.Type, "endDialogue", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
