namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootPoolRollResult
{
	internal SubwayLootPoolCandidate[] GuaranteedCandidates { get; private set; }

	internal SubwayLootPoolCandidate WeightedCandidate { get; private set; }

	internal SubwayLootPoolRollResult(SubwayLootPoolCandidate[] guaranteedCandidates, SubwayLootPoolCandidate weightedCandidate)
	{
		GuaranteedCandidates = guaranteedCandidates ?? new SubwayLootPoolCandidate[0];
		WeightedCandidate = weightedCandidate;
	}
}
