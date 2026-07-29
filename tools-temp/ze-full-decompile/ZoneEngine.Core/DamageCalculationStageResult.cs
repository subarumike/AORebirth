namespace ZoneEngine.Core;

public sealed class DamageCalculationStageResult
{
	public string Stage { get; set; }

	public DamageCalculationStageStatus Status { get; set; }

	public int Input { get; set; }

	public int Output { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public string Note { get; set; }

	public DamageCalculationStageResult()
	{
		Stage = string.Empty;
		Status = DamageCalculationStageStatus.Skipped;
		Input = 0;
		Output = 0;
		EvidenceClassification = DamageEvidenceClassification.Unknown;
		Note = string.Empty;
	}
}
