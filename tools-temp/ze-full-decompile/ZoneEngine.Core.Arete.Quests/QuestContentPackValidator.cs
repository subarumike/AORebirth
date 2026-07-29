using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public static class QuestContentPackValidator
{
	public static AreteValidationResult Validate(IEnumerable<QuestContentPack> packs)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<QuestContentPack> list = new List<QuestContentPack>(packs ?? Enumerable.Empty<QuestContentPack>());
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, HashSet<string>> stepsByQuest = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (QuestContentPack item in list)
		{
			string text = "questPack[" + num + "]";
			string packId = GetPackId(item);
			if (string.IsNullOrWhiteSpace(packId))
			{
				areteValidationResult.AddError(text, "missing quest content pack id");
			}
			else if (!hashSet.Add(packId))
			{
				areteValidationResult.AddError(text, "duplicate quest content pack id '" + packId + "'");
			}
			ValidateQuests(areteValidationResult, item, text, questIds, stepsByQuest);
			num++;
		}
		ValidateLinks(areteValidationResult, list, questIds, stepsByQuest);
		return areteValidationResult;
	}

	private static void ValidateQuests(AreteValidationResult result, QuestContentPack pack, string packLocation, HashSet<string> questIds, Dictionary<string, HashSet<string>> stepsByQuest)
	{
		if (pack == null)
		{
			result.AddError(packLocation, "content pack is null");
			return;
		}
		int num = 0;
		IEnumerable<QuestDefinition> quests = pack.Quests;
		foreach (QuestDefinition item in quests ?? Enumerable.Empty<QuestDefinition>())
		{
			string text = packLocation + ".quest[" + num + "]";
			if (item == null)
			{
				result.AddError(text, "quest definition is null");
				num++;
				continue;
			}
			if (string.IsNullOrWhiteSpace(item.QuestId))
			{
				result.AddError(text, "missing quest id");
			}
			else if (!questIds.Add(item.QuestId))
			{
				result.AddError(text, "duplicate quest id '" + item.QuestId + "'");
			}
			ValidateSteps(result, item, text, stepsByQuest);
			num++;
		}
	}

	private static void ValidateSteps(AreteValidationResult result, QuestDefinition quest, string questLocation, Dictionary<string, HashSet<string>> stepsByQuest)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> objectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		IEnumerable<QuestStep> steps = quest.Steps;
		foreach (QuestStep item in steps ?? Enumerable.Empty<QuestStep>())
		{
			string text = questLocation + ".step[" + num + "]";
			if (item == null)
			{
				result.AddError(text, "quest step is null");
				num++;
				continue;
			}
			if (string.IsNullOrWhiteSpace(item.StepId))
			{
				result.AddError(text, "missing quest step id");
			}
			else if (!hashSet.Add(item.StepId))
			{
				result.AddError(text, "duplicate quest step id '" + item.StepId + "'");
			}
			ValidateObjectives(result, item, text, objectiveIds);
			num++;
		}
		if (!string.IsNullOrWhiteSpace(quest.QuestId))
		{
			stepsByQuest[quest.QuestId] = hashSet;
		}
		if (!string.IsNullOrWhiteSpace(quest.InitialStepId) && !hashSet.Contains(quest.InitialStepId))
		{
			result.AddError(questLocation, "initial quest step id '" + quest.InitialStepId + "' was not found");
		}
	}

	private static void ValidateObjectives(AreteValidationResult result, QuestStep step, string stepLocation, HashSet<string> objectiveIds)
	{
		int num = 0;
		IEnumerable<QuestObjective> objectives = step.Objectives;
		foreach (QuestObjective item in objectives ?? Enumerable.Empty<QuestObjective>())
		{
			string location = stepLocation + ".objective[" + num + "]";
			if (item == null)
			{
				result.AddError(location, "quest objective is null");
				num++;
				continue;
			}
			if (string.IsNullOrWhiteSpace(item.ObjectiveId))
			{
				result.AddError(location, "missing quest objective id");
			}
			else if (!objectiveIds.Add(item.ObjectiveId))
			{
				result.AddError(location, "duplicate quest objective id '" + item.ObjectiveId + "'");
			}
			num++;
		}
	}

	private static void ValidateLinks(AreteValidationResult result, IEnumerable<QuestContentPack> packs, HashSet<string> questIds, Dictionary<string, HashSet<string>> stepsByQuest)
	{
		int num = 0;
		foreach (QuestContentPack item in packs ?? Enumerable.Empty<QuestContentPack>())
		{
			if (item == null)
			{
				num++;
				continue;
			}
			int num2 = 0;
			IEnumerable<QuestChainLinkMetadata> links = item.Links;
			foreach (QuestChainLinkMetadata item2 in links ?? Enumerable.Empty<QuestChainLinkMetadata>())
			{
				string linkLocation = "questPack[" + num + "].link[" + num2 + "]";
				ValidateLinkEndpoint(result, item2, linkLocation, "from", questIds, stepsByQuest);
				ValidateLinkEndpoint(result, item2, linkLocation, "to", questIds, stepsByQuest);
				num2++;
			}
			num++;
		}
	}

	private static void ValidateLinkEndpoint(AreteValidationResult result, QuestChainLinkMetadata link, string linkLocation, string side, HashSet<string> questIds, Dictionary<string, HashSet<string>> stepsByQuest)
	{
		if (link == null)
		{
			result.AddError(linkLocation, "quest chain link is null");
			return;
		}
		string text = ((side == "from") ? link.FromQuestId : link.ToQuestId);
		string text2 = ((side == "from") ? link.FromStepId : link.ToStepId);
		if (string.IsNullOrWhiteSpace(text))
		{
			result.AddError(linkLocation, "missing " + side + " quest id");
		}
		else if (!questIds.Contains(text))
		{
			result.AddError(linkLocation, side + " quest id '" + text + "' was not found");
		}
		else if (!string.IsNullOrWhiteSpace(text2) && (!stepsByQuest.ContainsKey(text) || !stepsByQuest[text].Contains(text2)))
		{
			result.AddError(linkLocation, side + " quest step id '" + text2 + "' was not found");
		}
	}

	private static string GetPackId(QuestContentPack pack)
	{
		if (pack == null || pack.Identity == null)
		{
			return string.Empty;
		}
		return pack.Identity.Id;
	}
}
