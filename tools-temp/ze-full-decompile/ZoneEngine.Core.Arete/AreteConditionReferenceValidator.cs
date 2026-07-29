using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public static class AreteConditionReferenceValidator
{
	private static readonly HashSet<string> SupportedConditionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AlwaysTrue", "AlwaysFalse", "MissionOffered", "MissionActive", "MissionCompleted", "MissionNotStarted" };

	private static readonly HashSet<string> MissionConditionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MissionOffered", "MissionActive", "MissionCompleted", "MissionNotStarted" };

	public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> dialoguePacks, IEnumerable<QuestContentPack> questPacks, DialogueContentRegistry dialogueRegistry, QuestContentRegistry questRegistry)
	{
		AreteValidationResult result = new AreteValidationResult();
		ValidateDialoguePacks(result, dialoguePacks, questRegistry);
		ValidateQuestPacks(result, questPacks, questRegistry);
		return result;
	}

	private static void ValidateDialoguePacks(AreteValidationResult result, IEnumerable<DialogueContentPack> dialoguePacks, QuestContentRegistry questRegistry)
	{
		int num = 0;
		foreach (DialogueContentPack item in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
		{
			string text = "dialoguePack[" + num + "]";
			if (item == null)
			{
				num++;
				continue;
			}
			int num2 = 0;
			IEnumerable<DialogueNpcEntry> npcs = item.Npcs;
			foreach (DialogueNpcEntry item2 in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
			{
				string text2 = text + ".npc[" + num2 + "]";
				if (item2 == null)
				{
					num2++;
					continue;
				}
				ValidateDialogueConditions(result, item2.Conditions, text2 + ".condition", questRegistry);
				int num3 = 0;
				IEnumerable<DialogueNode> nodes = item2.Nodes;
				foreach (DialogueNode item3 in nodes ?? Enumerable.Empty<DialogueNode>())
				{
					string text3 = text2 + ".node[" + num3 + "]";
					if (item3 == null)
					{
						num3++;
						continue;
					}
					int num4 = 0;
					IEnumerable<DialogueOption> options = item3.Options;
					foreach (DialogueOption item4 in options ?? Enumerable.Empty<DialogueOption>())
					{
						string text4 = text3 + ".option[" + num4 + "]";
						if (item4 != null)
						{
							ValidateDialogueConditions(result, item4.Conditions, text4 + ".condition", questRegistry);
						}
						num4++;
					}
					num3++;
				}
				num2++;
			}
			num++;
		}
	}

	private static void ValidateQuestPacks(AreteValidationResult result, IEnumerable<QuestContentPack> questPacks, QuestContentRegistry questRegistry)
	{
		int num = 0;
		foreach (QuestContentPack item in questPacks ?? Enumerable.Empty<QuestContentPack>())
		{
			string text = "questPack[" + num + "]";
			if (item == null)
			{
				num++;
				continue;
			}
			int num2 = 0;
			IEnumerable<QuestDefinition> quests = item.Quests;
			foreach (QuestDefinition item2 in quests ?? Enumerable.Empty<QuestDefinition>())
			{
				string text2 = text + ".quest[" + num2 + "]";
				if (item2 == null)
				{
					num2++;
					continue;
				}
				ValidateQuestConditions(result, item2.Conditions, text2 + ".condition", questRegistry);
				int num3 = 0;
				IEnumerable<QuestStep> steps = item2.Steps;
				foreach (QuestStep item3 in steps ?? Enumerable.Empty<QuestStep>())
				{
					string text3 = text2 + ".step[" + num3 + "]";
					if (item3 == null)
					{
						num3++;
						continue;
					}
					ValidateQuestConditions(result, item3.Conditions, text3 + ".condition", questRegistry);
					int num4 = 0;
					IEnumerable<QuestObjective> objectives = item3.Objectives;
					foreach (QuestObjective item4 in objectives ?? Enumerable.Empty<QuestObjective>())
					{
						string text4 = text3 + ".objective[" + num4 + "]";
						if (item4 != null)
						{
							ValidateQuestConditions(result, item4.Conditions, text4 + ".condition", questRegistry);
						}
						num4++;
					}
					num3++;
				}
				num2++;
			}
			num++;
		}
	}

	private static void ValidateDialogueConditions(AreteValidationResult result, IEnumerable<DialogueCondition> conditions, string locationPrefix, QuestContentRegistry questRegistry)
	{
		int num = 0;
		foreach (DialogueCondition item in conditions ?? Enumerable.Empty<DialogueCondition>())
		{
			string location = locationPrefix + "[" + num + "]";
			if (item == null)
			{
				result.AddError(location, "dialogue condition is null");
			}
			else
			{
				ValidateCondition(result, location, item.Type, item.QuestId, questRegistry);
			}
			num++;
		}
	}

	private static void ValidateQuestConditions(AreteValidationResult result, IEnumerable<QuestCondition> conditions, string locationPrefix, QuestContentRegistry questRegistry)
	{
		int num = 0;
		foreach (QuestCondition item in conditions ?? Enumerable.Empty<QuestCondition>())
		{
			string location = locationPrefix + "[" + num + "]";
			if (item == null)
			{
				result.AddError(location, "quest condition is null");
			}
			else
			{
				ValidateCondition(result, location, item.Type, item.QuestId, questRegistry);
			}
			num++;
		}
	}

	private static void ValidateCondition(AreteValidationResult result, string location, string conditionType, string questId, QuestContentRegistry questRegistry)
	{
		if (string.IsNullOrWhiteSpace(conditionType))
		{
			result.AddError(location, "missing condition type");
		}
		else if (!SupportedConditionTypes.Contains(conditionType))
		{
			result.AddError(location, "unsupported condition type '" + conditionType + "'");
		}
		else if (MissionConditionTypes.Contains(conditionType))
		{
			QuestDefinition quest;
			if (string.IsNullOrWhiteSpace(questId))
			{
				result.AddError(location, "missing mission id for condition '" + conditionType + "'");
			}
			else if (questRegistry == null)
			{
				result.AddError(location, "quest registry is missing");
			}
			else if (!questRegistry.TryGetQuest(questId, out quest))
			{
				result.AddError(location, "mission id '" + questId + "' was not found");
			}
		}
	}
}
