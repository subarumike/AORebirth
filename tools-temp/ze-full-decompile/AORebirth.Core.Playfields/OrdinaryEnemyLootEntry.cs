namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyLootEntry
{
	internal int LowId { get; private set; }

	internal int HighId { get; private set; }

	internal int QualityLevel { get; private set; }

	internal int Quality => QualityLevel;

	internal int Slot { get; private set; }

	internal int Quantity { get; private set; }

	internal int Weight { get; private set; }

	internal int DropChanceBasisPoints { get; private set; }

	internal int BasisPoints => DropChanceBasisPoints;

	internal OrdinaryEnemyLootEvidence Evidence { get; private set; }

	internal OrdinaryEnemyLootLinkageEvidence LinkageEvidence { get; private set; }

	internal OrdinaryEnemyLootProbabilityEvidence ProbabilityEvidence { get; private set; }

	internal int ObservedCount { get; private set; }

	internal int ObservedCorpses { get; private set; }

	internal string EvidenceReference { get; private set; }

	internal OrdinaryEnemyLootEntry(int lowId, int highId, int qualityLevel, int slot, int quantity, int weight, int dropChanceBasisPoints, OrdinaryEnemyLootEvidence evidence, OrdinaryEnemyLootLinkageEvidence linkageEvidence, OrdinaryEnemyLootProbabilityEvidence probabilityEvidence, int observedCount, int observedCorpses, string evidenceReference)
	{
		LowId = lowId;
		HighId = highId;
		QualityLevel = qualityLevel;
		Slot = slot;
		Quantity = quantity;
		Weight = weight;
		DropChanceBasisPoints = dropChanceBasisPoints;
		Evidence = evidence;
		LinkageEvidence = linkageEvidence;
		ProbabilityEvidence = probabilityEvidence;
		ObservedCount = observedCount;
		ObservedCorpses = observedCorpses;
		EvidenceReference = evidenceReference ?? string.Empty;
	}
}
