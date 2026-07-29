namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayLootEvidenceDefinition
{
	public int LowId { get; private set; }

	public int HighId { get; private set; }

	public int Quality { get; private set; }

	public int ObservedCount { get; private set; }

	public int ObservedCorpses { get; private set; }

	public int ObservedBasisPoints { get; private set; }

	public CapturedSubwayLootEvidenceDefinition(int lowId, int highId, int quality, int observedCount, int observedCorpses, int observedBasisPoints)
	{
		LowId = lowId;
		HighId = highId;
		Quality = quality;
		ObservedCount = observedCount;
		ObservedCorpses = observedCorpses;
		ObservedBasisPoints = observedBasisPoints;
	}
}
