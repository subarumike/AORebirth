using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class ObjectivePlaybackReplayResult
{
	public IList<ObjectivePlaybackObservationResult> ObservationResults { get; private set; }

	public IList<ObjectiveProgressRecord> Progress { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public ObjectivePlaybackReplayResult(IEnumerable<ObjectivePlaybackObservationResult> observationResults, IEnumerable<ObjectiveProgressRecord> progress, AreteValidationResult validation)
	{
		ObservationResults = new List<ObjectivePlaybackObservationResult>(observationResults ?? Enumerable.Empty<ObjectivePlaybackObservationResult>());
		Progress = new List<ObjectiveProgressRecord>(progress ?? Enumerable.Empty<ObjectiveProgressRecord>());
		Validation = validation ?? new AreteValidationResult();
	}
}
