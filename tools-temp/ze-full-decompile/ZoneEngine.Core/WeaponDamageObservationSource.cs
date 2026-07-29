namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationSource
{
	public string ObservationId { get; set; }

	public WeaponDamageObservationSourceKind SourceKind { get; set; }

	public string CaptureDate { get; set; }

	public string Environment { get; set; }

	public string PacketEvidenceReference { get; set; }

	public string LogEvidenceReference { get; set; }

	public string TimingReference { get; set; }

	public DamageEvidenceClassification Classification { get; set; }

	public WeaponDamageObservationSource()
	{
		ObservationId = string.Empty;
		CaptureDate = string.Empty;
		Environment = string.Empty;
		PacketEvidenceReference = string.Empty;
		LogEvidenceReference = string.Empty;
		TimingReference = string.Empty;
		Classification = DamageEvidenceClassification.Unknown;
		SourceKind = WeaponDamageObservationSourceKind.RepositorySynthetic;
	}
}
