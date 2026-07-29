using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class MissionDefinition
{
	public string QuestId { get; set; }

	public string InitialStepId { get; set; }

	public bool IsResolved { get; set; }

	public IList<string> StepIds { get; set; }

	public IList<string> PrerequisiteQuestIds { get; set; }

	public IList<MissionObjectiveDefinition> Objectives { get; set; }

	public MissionDefinition()
	{
		StepIds = new string[0];
		PrerequisiteQuestIds = new string[0];
		Objectives = new MissionObjectiveDefinition[0];
	}
}
