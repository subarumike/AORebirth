using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class ObjectivePlaybackObservationResult
{
	public ObjectivePlaybackObservation Observation { get; private set; }

	public IList<ObjectiveProgressRecord> MatchedProgress { get; private set; }

	public IList<ObjectiveProgressRecord> IgnoredProgress { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public ObjectivePlaybackObservationResult(ObjectivePlaybackObservation observation, IEnumerable<ObjectiveProgressRecord> matchedProgress, IEnumerable<ObjectiveProgressRecord> ignoredProgress, AreteValidationResult validation)
	{
		Observation = observation;
		MatchedProgress = new List<ObjectiveProgressRecord>(matchedProgress ?? Enumerable.Empty<ObjectiveProgressRecord>());
		IgnoredProgress = new List<ObjectiveProgressRecord>(ignoredProgress ?? Enumerable.Empty<ObjectiveProgressRecord>());
		Validation = validation ?? new AreteValidationResult();
	}
}
