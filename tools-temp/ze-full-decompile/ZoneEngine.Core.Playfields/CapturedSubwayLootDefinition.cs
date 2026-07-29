using AORebirth.Core.Playfields;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayLootDefinition
{
	public string ExactName { get; private set; }

	public int MonsterData { get; private set; }

	public int NpcFamily { get; private set; }

	public int LowId { get; private set; }

	public int HighId { get; private set; }

	public int Quality { get; private set; }

	public int Slot { get; private set; }

	public int Quantity { get; private set; }

	public int RuntimeWeight { get; private set; }

	public int ObservedBasisPoints { get; private set; }

	public int ObservedCount { get; private set; }

	public int ObservedCorpses { get; private set; }

	public OrdinaryEnemyLootLinkageEvidence LinkageEvidence { get; private set; }

	public OrdinaryEnemyLootProbabilityEvidence ProbabilityEvidence { get; private set; }

	public string EvidenceReference { get; private set; }

	public CapturedSubwayLootDefinition(string exactName, int monsterData, int npcFamily, int lowId, int highId, int quality, int slot, int quantity, int runtimeWeight, int observedBasisPoints, int observedCount, int observedCorpses, OrdinaryEnemyLootLinkageEvidence linkageEvidence, OrdinaryEnemyLootProbabilityEvidence probabilityEvidence, string evidenceReference)
	{
		ExactName = exactName;
		MonsterData = monsterData;
		NpcFamily = npcFamily;
		LowId = lowId;
		HighId = highId;
		Quality = quality;
		Slot = slot;
		Quantity = quantity;
		RuntimeWeight = runtimeWeight;
		ObservedBasisPoints = observedBasisPoints;
		ObservedCount = observedCount;
		ObservedCorpses = observedCorpses;
		LinkageEvidence = linkageEvidence;
		ProbabilityEvidence = probabilityEvidence;
		EvidenceReference = evidenceReference ?? string.Empty;
	}
}
