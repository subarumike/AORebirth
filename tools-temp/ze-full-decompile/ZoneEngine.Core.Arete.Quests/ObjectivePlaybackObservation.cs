using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class ObjectivePlaybackObservation
{
	public string ObservationType { get; set; }

	public string EvidenceReference { get; set; }

	public string TargetName { get; set; }

	public string TargetIdentity { get; set; }

	public string CapturedSignal { get; set; }

	public string ActionName { get; set; }

	public IDictionary<string, string> Parameters { get; set; }

	public ObjectivePlaybackObservation()
	{
		Parameters = new Dictionary<string, string>();
	}
}
