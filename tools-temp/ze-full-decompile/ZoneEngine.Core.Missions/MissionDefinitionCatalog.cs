using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Missions;

internal static class MissionDefinitionCatalog
{
	internal const string RexB18CQuestId = "Mission:5514B18C";

	internal const string RexB18DQuestId = "Mission:5514B18D";

	internal const string RexB18EQuestId = "Mission:5514B18E";

	internal const string RexB18FQuestId = "Mission:5514B18F";

	internal const string RexB194QuestId = "Mission:5514B194";

	internal const string RexB196QuestId = "Mission:5514B196";

	internal const string RexFlintQuestId = "Mission:5514B198";

	internal const string RexB199QuestId = "Mission:5514B199";

	internal const string RexB19AQuestId = "Mission:5514B19A";

	internal const string RexFlintFindBioQuestId = "Mission:5514B19B";

	internal const string RexFlintDeliverBioQuestId = "Mission:5514B19C";

	internal const string RexFlintSurveillanceUplinkQuestId = "Mission:5514B19D";

	internal const string RexFlintPlantBugQuestId = "Mission:5514B19E";

	internal const string RexFlintDeliverHc12BillQuestId = "Mission:5514B19F";

	internal const string RexFlintKneecappingQuestId = "Mission:5514B1A0";

	internal const string RexFlintReportToAlexQuestId = "Mission:555B4365";

	internal const string RexFlintTalkToStanQuestId = "Mission:555B4366";

	internal const string RexFlintTradeskillNanoSensorQuestId = "Mission:555B4367";

	internal const string WindcallerKarrecQuestId = "Mission:55579381";

	internal static IList<MissionDefinition> Build(QuestContentRegistry questRegistry)
	{
		if (questRegistry == null)
		{
			throw new ArgumentNullException("questRegistry");
		}
		List<MissionDefinition> list = new List<MissionDefinition>();
		foreach (QuestDefinition quest in questRegistry.GetQuests())
		{
			if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
			{
				continue;
			}
			IList<string> list2 = (from step in quest.Steps ?? new QuestStep[0]
				where step != null && !string.IsNullOrWhiteSpace(step.StepId)
				select step.StepId.Trim()).ToList();
			IList<MissionObjectiveDefinition> list3 = (quest.Steps ?? new QuestStep[0]).Where((QuestStep step) => step != null).SelectMany((QuestStep step) => from objective in step.Objectives ?? new QuestObjective[0]
				where objective != null && !string.IsNullOrWhiteSpace(objective.ObjectiveId)
				select new MissionObjectiveDefinition
				{
					ObjectiveId = objective.ObjectiveId.Trim(),
					StepId = ((step.StepId == null) ? null : step.StepId.Trim()),
					RequiredCount = ResolveRequiredCount(quest.QuestId, objective),
					IsResolved = (ResolveRequiredCount(quest.QuestId, objective) > 0)
				}).ToList();
			list.Add(new MissionDefinition
			{
				QuestId = quest.QuestId.Trim(),
				InitialStepId = ((quest.InitialStepId == null) ? null : quest.InitialStepId.Trim()),
				IsResolved = (list2.Count > 0 && list3.All((MissionObjectiveDefinition objective) => objective.IsResolved)),
				StepIds = list2,
				PrerequisiteQuestIds = ResolvePrerequisites(quest.QuestId),
				Objectives = list3
			});
		}
		AddHandoffDefinitionIfMissing(list, "Mission:5514B18F", "talk_to_marcus", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B194", "captured_preview", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B196", "return_to_marcus", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B198", "talk_to_flint_novak", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B199", "use_stim_wounded_dockworker", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19A", "return_marcus_stim", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19B", "kill_junkyard_robots", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19C", "deliver_bio_com", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19D", "use_sectec_monitor", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19E", "plant_rc_p_device", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B19F", "deliver_hc12_bill", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:5514B1A0", "kneecapping_tip", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:555B4365", "report_to_alex", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:555B4366", "talk_to_stan_goodman", new string[0]);
		AddHandoffDefinitionIfMissing(list, "Mission:555B4367", "tradeskill_assemble_nano_sensor", new string[0]);
		return list;
	}

	private static int ResolveRequiredCount(string questId, QuestObjective objective)
	{
		if (objective.RequiredCount > 0)
		{
			return objective.RequiredCount;
		}
		if (string.Equals(questId, "Mission:5514B18D", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B18E", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B194", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B196", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B199", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B19A", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B19C", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B19D", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B19E", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B19F", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:5514B1A0", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:555B4365", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:555B4366", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:555B4367", StringComparison.OrdinalIgnoreCase))
		{
			return 1;
		}
		if (string.Equals(questId, "Mission:5514B19B", StringComparison.OrdinalIgnoreCase))
		{
			return 7;
		}
		return 0;
	}

	private static IList<string> ResolvePrerequisites(string questId)
	{
		if (string.Equals(questId, "Mission:5514B18D", StringComparison.OrdinalIgnoreCase))
		{
			return new string[1] { "Mission:5514B18C" };
		}
		if (string.Equals(questId, "Mission:5514B18E", StringComparison.OrdinalIgnoreCase))
		{
			return new string[1] { "Mission:5514B18D" };
		}
		return new string[0];
	}

	private static void AddHandoffDefinitionIfMissing(ICollection<MissionDefinition> definitions, string questId, string initialStepId, IList<string> prerequisiteQuestIds)
	{
		if (!definitions.Any((MissionDefinition definition) => string.Equals(definition.QuestId, questId, StringComparison.OrdinalIgnoreCase)))
		{
			definitions.Add(new MissionDefinition
			{
				QuestId = questId,
				InitialStepId = initialStepId,
				IsResolved = true,
				StepIds = new string[1] { initialStepId },
				PrerequisiteQuestIds = prerequisiteQuestIds,
				Objectives = new MissionObjectiveDefinition[1]
				{
					new MissionObjectiveDefinition
					{
						ObjectiveId = "mission_" + questId.Replace("Mission:", string.Empty).ToLowerInvariant() + "_objective_questfullupdate",
						StepId = initialStepId,
						RequiredCount = 1,
						IsResolved = true
					}
				}
			});
		}
	}
}
