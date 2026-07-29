namespace ZoneEngine.Core.Arete;

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
