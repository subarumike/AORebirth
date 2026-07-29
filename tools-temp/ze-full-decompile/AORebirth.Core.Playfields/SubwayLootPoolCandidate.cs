using System;

namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootPoolCandidate
{
	internal string CandidateKey { get; private set; }

	internal int LowId { get; private set; }

	internal int HighId { get; private set; }

	internal int MinimumQuality { get; private set; }

	internal int MaximumQuality { get; private set; }

	internal int Weight { get; private set; }

	internal int ObservedCount { get; private set; }

	internal int ObservedKills { get; private set; }

	internal bool ExplicitlyGuaranteed { get; private set; }

	internal string Evidence { get; private set; }

	private SubwayLootPoolCandidate(string candidateKey, int lowId, int highId, int minimumQuality, int maximumQuality, int weight, int observedCount, int observedKills, bool explicitlyGuaranteed, string evidence)
	{
		if (string.IsNullOrWhiteSpace(candidateKey))
		{
			throw new ArgumentException("Candidate key is required.", "candidateKey");
		}
		if (lowId <= 0)
		{
			throw new ArgumentOutOfRangeException("lowId");
		}
		if (highId <= 0)
		{
			throw new ArgumentOutOfRangeException("highId");
		}
		if (minimumQuality <= 0 || maximumQuality < minimumQuality)
		{
			throw new ArgumentOutOfRangeException("minimumQuality");
		}
		if (weight < 0)
		{
			throw new ArgumentOutOfRangeException("weight");
		}
		if (string.IsNullOrWhiteSpace(evidence))
		{
			throw new ArgumentException("Evidence is required.", "evidence");
		}
		CandidateKey = candidateKey;
		LowId = lowId;
		HighId = highId;
		MinimumQuality = minimumQuality;
		MaximumQuality = maximumQuality;
		Weight = weight;
		ObservedCount = observedCount;
		ObservedKills = observedKills;
		ExplicitlyGuaranteed = explicitlyGuaranteed;
		Evidence = evidence;
	}

	internal static SubwayLootPoolCandidate FromObservedSample(string candidateKey, int lowId, int highId, int minimumQuality, int maximumQuality, int observedCount, int observedKills, int weight, string evidence)
	{
		if (observedCount <= 0)
		{
			throw new ArgumentOutOfRangeException("observedCount");
		}
		if (observedKills <= 0)
		{
			throw new ArgumentOutOfRangeException("observedKills");
		}
		if (weight <= 0)
		{
			throw new ArgumentOutOfRangeException("weight");
		}
		return new SubwayLootPoolCandidate(candidateKey, lowId, highId, minimumQuality, maximumQuality, weight, observedCount, observedKills, explicitlyGuaranteed: false, evidence);
	}

	internal static SubwayLootPoolCandidate ExplicitGuaranteed(string candidateKey, int lowId, int highId, int minimumQuality, int maximumQuality, string evidence)
	{
		return new SubwayLootPoolCandidate(candidateKey, lowId, highId, minimumQuality, maximumQuality, 0, 0, 0, explicitlyGuaranteed: true, evidence);
	}
}
