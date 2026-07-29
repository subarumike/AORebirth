using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete.Dialogue;

public static class DialogueActionReferenceValidator
{
	private static readonly HashSet<string> SupportedActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OfferMission", "AcceptMission", "CompleteMission", "FailMission", "AbandonMission", "EndDialogue" };

	private static readonly HashSet<string> MissionActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OfferMission", "AcceptMission", "CompleteMission", "FailMission", "AbandonMission" };

	public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> dialoguePacks, QuestContentRegistry questRegistry)
	{
		AreteValidationResult result = new AreteValidationResult();
		int num = 0;
		foreach (DialogueContentPack item in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
		{
			string packLocation = "dialoguePack[" + num + "]";
			ValidatePack(result, item, packLocation, questRegistry);
			num++;
		}
		return result;
	}

	private static void ValidatePack(AreteValidationResult result, DialogueContentPack pack, string packLocation, QuestContentRegistry questRegistry)
	{
		if (pack == null)
		{
			result.AddError(packLocation, "content pack is null");
			return;
		}
		int num = 0;
		IEnumerable<DialogueNpcEntry> npcs = pack.Npcs;
		foreach (DialogueNpcEntry item in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
		{
			string npcLocation = packLocation + ".npc[" + num + "]";
			ValidateNpc(result, item, npcLocation, questRegistry);
			num++;
		}
	}

	private static void ValidateNpc(AreteValidationResult result, DialogueNpcEntry npc, string npcLocation, QuestContentRegistry questRegistry)
	{
		if (npc == null)
		{
			result.AddError(npcLocation, "npc entry is null");
			return;
		}
		ValidateActions(result, npc.Actions, npcLocation + ".action", questRegistry);
		int num = 0;
		IEnumerable<DialogueNode> nodes = npc.Nodes;
		foreach (DialogueNode item in nodes ?? Enumerable.Empty<DialogueNode>())
		{
			string nodeLocation = npcLocation + ".node[" + num + "]";
			ValidateNode(result, item, nodeLocation, questRegistry);
			num++;
		}
	}

	private static void ValidateNode(AreteValidationResult result, DialogueNode node, string nodeLocation, QuestContentRegistry questRegistry)
	{
		if (node == null)
		{
			result.AddError(nodeLocation, "dialogue node is null");
			return;
		}
		ValidateActions(result, node.EnterActions, nodeLocation + ".enterAction", questRegistry);
		int num = 0;
		IEnumerable<DialogueOption> options = node.Options;
		foreach (DialogueOption item in options ?? Enumerable.Empty<DialogueOption>())
		{
			string text = nodeLocation + ".option[" + num + "]";
			if (item == null)
			{
				result.AddError(text, "dialogue option is null");
				num++;
			}
			else
			{
				ValidateActions(result, item.Actions, text + ".action", questRegistry);
				num++;
			}
		}
	}

	private static void ValidateActions(AreteValidationResult result, IEnumerable<DialogueAction> actions, string actionLocationPrefix, QuestContentRegistry questRegistry)
	{
		int num = 0;
		foreach (DialogueAction item in actions ?? Enumerable.Empty<DialogueAction>())
		{
			string actionLocation = actionLocationPrefix + "[" + num + "]";
			ValidateAction(result, item, actionLocation, questRegistry);
			num++;
		}
	}

	private static void ValidateAction(AreteValidationResult result, DialogueAction action, string actionLocation, QuestContentRegistry questRegistry)
	{
		if (action == null)
		{
			result.AddError(actionLocation, "dialogue action is null");
		}
		else if (string.IsNullOrWhiteSpace(action.Type))
		{
			result.AddError(actionLocation, "missing dialogue action type");
		}
		else if (!SupportedActionTypes.Contains(action.Type))
		{
			result.AddError(actionLocation, "unsupported dialogue action type '" + action.Type + "'");
		}
		else if (MissionActionTypes.Contains(action.Type))
		{
			QuestDefinition quest;
			if (string.IsNullOrWhiteSpace(action.QuestId))
			{
				result.AddError(actionLocation, "missing mission id for dialogue action '" + action.Type + "'");
			}
			else if (questRegistry == null)
			{
				result.AddError(actionLocation, "quest registry is missing");
			}
			else if (!questRegistry.TryGetQuest(action.QuestId, out quest))
			{
				result.AddError(actionLocation, "mission id '" + action.QuestId + "' was not found");
			}
		}
	}
}
