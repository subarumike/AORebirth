using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueSessionService
{
	private readonly AreteNoOpActionRecorder actionRecorder;

	private readonly AreteNoOpConditionEvaluator conditionEvaluator;

	private readonly DialogueContentRegistry registry;

	public DialogueSessionService(DialogueContentRegistry registry)
		: this(registry, new AreteNoOpConditionEvaluator(), new AreteNoOpActionRecorder())
	{
	}

	public DialogueSessionService(DialogueContentRegistry registry, AreteNoOpConditionEvaluator conditionEvaluator, AreteNoOpActionRecorder actionRecorder)
	{
		this.registry = registry;
		this.conditionEvaluator = conditionEvaluator ?? new AreteNoOpConditionEvaluator();
		this.actionRecorder = actionRecorder ?? new AreteNoOpActionRecorder();
	}

	public DialogueSessionResult StartSession(string npcIdentity)
	{
		return StartSessionAtNode(npcIdentity, null);
	}

	public DialogueSessionResult StartSessionAtNode(string npcIdentity, string requestedStartNodeId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		DialogueNpcEntry dialogueNpcEntry = ResolveNpc(npcIdentity, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return CreateResult(null, null, null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		if (string.IsNullOrWhiteSpace(dialogueNpcEntry.RootNodeId))
		{
			areteValidationResult.AddError(dialogueNpcEntry.NpcIdentity, "missing start dialogue node");
			return CreateResult(null, dialogueNpcEntry, null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		string text = (string.IsNullOrWhiteSpace(requestedStartNodeId) ? dialogueNpcEntry.RootNodeId : requestedStartNodeId);
		DialogueNode dialogueNode = FindNode(dialogueNpcEntry, text);
		if (dialogueNode == null)
		{
			areteValidationResult.AddError(dialogueNpcEntry.NpcIdentity, "start dialogue node '" + text + "' was not found");
			return CreateResult(null, dialogueNpcEntry, null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		DialogueSession session = new DialogueSession
		{
			SessionId = Guid.NewGuid().ToString("N"),
			NpcIdentity = dialogueNpcEntry.NpcIdentity,
			CurrentNodeId = dialogueNode.Id,
			IsActive = true
		};
		IList<AreteRecordedAction> recordedActions = actionRecorder.RecordDialogueActions(dialogueNode.EnterActions);
		return CreateResult(session, dialogueNpcEntry, dialogueNode, recordedActions, areteValidationResult);
	}

	public IList<DialogueOption> ListAvailableOptions(DialogueSession session)
	{
		if (!TryResolveSessionNode(session, out var _, out var currentNode))
		{
			return new List<DialogueOption>();
		}
		return ListAvailableOptions(currentNode);
	}

	public DialogueSessionResult SelectOption(DialogueSession session, int optionIndex)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		if (!TryResolveSessionNode(session, areteValidationResult, out var npc, out var currentNode))
		{
			return CreateResult(session, npc, currentNode, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		DialogueOption dialogueOption = ListAvailableOptions(currentNode).FirstOrDefault((DialogueOption option) => option != null && option.Index == optionIndex);
		if (dialogueOption == null)
		{
			areteValidationResult.AddError(session.NpcIdentity, "dialogue option was not available");
			return CreateResult(session, npc, currentNode, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		List<AreteRecordedAction> list = new List<AreteRecordedAction>(actionRecorder.RecordDialogueActions(dialogueOption.Actions));
		if (IsCloseTransition(dialogueOption))
		{
			session.IsActive = false;
			return CreateResult(session, npc, currentNode, list, areteValidationResult);
		}
		if (string.IsNullOrWhiteSpace(dialogueOption.NextNodeId))
		{
			areteValidationResult.AddError(session.NpcIdentity, "missing dialogue node target");
			return CreateResult(session, npc, currentNode, list, areteValidationResult);
		}
		string nodeId = ResolveSpecialNodeTarget(dialogueOption.NextNodeId, npc, currentNode);
		DialogueNode dialogueNode = FindNode(npc, nodeId);
		if (dialogueNode == null)
		{
			areteValidationResult.AddError(session.NpcIdentity, "dialogue node target '" + dialogueOption.NextNodeId + "' was not found");
			return CreateResult(session, npc, currentNode, list, areteValidationResult);
		}
		session.CurrentNodeId = dialogueNode.Id;
		foreach (AreteRecordedAction item in actionRecorder.RecordDialogueActions(dialogueNode.EnterActions))
		{
			list.Add(item);
		}
		return CreateResult(session, npc, dialogueNode, list, areteValidationResult);
	}

	public DialogueSessionResult EndSession(DialogueSession session)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		if (session == null)
		{
			areteValidationResult.AddError("dialogueSession", "dialogue session is missing");
			return CreateResult(null, null, null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		session.IsActive = false;
		return CreateResult(session, null, null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
	}

	private DialogueNpcEntry ResolveNpc(string npcIdentity, AreteValidationResult validation)
	{
		if (registry == null)
		{
			validation.AddError("dialogueSession", "dialogue registry is missing");
			return null;
		}
		if (string.IsNullOrWhiteSpace(npcIdentity))
		{
			validation.AddError("dialogueSession", "missing NPC identity");
			return null;
		}
		if (!registry.TryGetNpc(npcIdentity, out var npc))
		{
			validation.AddError(npcIdentity, "dialogue NPC was not found");
			return null;
		}
		return npc;
	}

	private bool TryResolveSessionNode(DialogueSession session, out DialogueNpcEntry npc, out DialogueNode currentNode)
	{
		return TryResolveSessionNode(session, new AreteValidationResult(), out npc, out currentNode);
	}

	private bool TryResolveSessionNode(DialogueSession session, AreteValidationResult validation, out DialogueNpcEntry npc, out DialogueNode currentNode)
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
		npc = ResolveNpc(session.NpcIdentity, validation);
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

	private DialogueSessionResult CreateResult(DialogueSession session, DialogueNpcEntry npc, DialogueNode currentNode, IEnumerable<AreteRecordedAction> recordedActions, AreteValidationResult validation)
	{
		IEnumerable<DialogueOption> enumerable;
		if (session == null || !session.IsActive || currentNode == null)
		{
			enumerable = Enumerable.Empty<DialogueOption>();
		}
		else
		{
			IEnumerable<DialogueOption> enumerable2 = ListAvailableOptions(currentNode);
			enumerable = enumerable2;
		}
		IEnumerable<DialogueOption> availableOptions = enumerable;
		return new DialogueSessionResult(session, currentNode, availableOptions, recordedActions, validation);
	}

	private IList<DialogueOption> ListAvailableOptions(DialogueNode node)
	{
		List<DialogueOption> list = new List<DialogueOption>();
		IEnumerable<DialogueOption> options = node.Options;
		foreach (DialogueOption item in options ?? Enumerable.Empty<DialogueOption>())
		{
			if (item != null && conditionEvaluator.AreDialogueConditionsSatisfied(item.Conditions))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static DialogueNode FindNode(DialogueNpcEntry npc, string nodeId)
	{
		if (npc == null || string.IsNullOrWhiteSpace(nodeId))
		{
			return null;
		}
		IEnumerable<DialogueNode> nodes = npc.Nodes;
		return (nodes ?? Enumerable.Empty<DialogueNode>()).FirstOrDefault((DialogueNode node) => node != null && string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));
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
		if (string.Equals(option.NextNodeId, "close", StringComparison.OrdinalIgnoreCase) || string.Equals(option.NextNodeId, "end", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		IEnumerable<DialogueAction> actions = option.Actions;
		foreach (DialogueAction item in actions ?? Enumerable.Empty<DialogueAction>())
		{
			if (item != null && (string.Equals(item.Type, "closeDialogue", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Type, "endDialogue", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}
		return false;
	}
}
