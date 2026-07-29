namespace ZoneEngine.Core;

public sealed class WeaponDamageInputIssue
{
	public WeaponDamageInputIssueKind Kind { get; set; }

	public string InputName { get; set; }

	public string Detail { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public WeaponDamageInputIssue()
	{
		InputName = string.Empty;
		Detail = string.Empty;
		EvidenceClassification = DamageEvidenceClassification.Unknown;
	}
}
