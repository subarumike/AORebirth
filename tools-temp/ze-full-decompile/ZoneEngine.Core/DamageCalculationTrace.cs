using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class DamageCalculationTrace
{
	public IList<DamageCalculationStageResult> Stages { get; private set; }

	public DamageCalculationTrace()
	{
		Stages = new List<DamageCalculationStageResult>();
	}

	public void Add(string stage, DamageCalculationStageStatus status, int input, int output, DamageEvidenceClassification evidenceClassification, string note)
	{
		Stages.Add(new DamageCalculationStageResult
		{
			Stage = stage,
			Status = status,
			Input = input,
			Output = output,
			EvidenceClassification = evidenceClassification,
			Note = (note ?? string.Empty)
		});
	}
}
