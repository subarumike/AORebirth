using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class MissionStateResult
{
	public MissionStateRecord Record { get; private set; }

	public IList<AreteRecordedAction> RecordedActions { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public MissionStateResult(MissionStateRecord record, IEnumerable<AreteRecordedAction> recordedActions, AreteValidationResult validation)
	{
		Record = record;
		RecordedActions = new List<AreteRecordedAction>(recordedActions ?? Enumerable.Empty<AreteRecordedAction>());
		Validation = validation ?? new AreteValidationResult();
	}
}
