namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwaySourceWeaponEvidenceDefinition
{
	public int SourceInstance { get; private set; }

	public int LowId { get; private set; }

	public int HighId { get; private set; }

	public int Quality { get; private set; }

	public string EvidenceCaptures { get; private set; }

	public CapturedSubwaySourceWeaponEvidenceDefinition(int sourceInstance, int lowId, int highId, int quality, string evidenceCaptures)
	{
		SourceInstance = sourceInstance;
		LowId = lowId;
		HighId = highId;
		Quality = quality;
		EvidenceCaptures = evidenceCaptures;
	}
}
