using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyLevelCreditRule
{
	internal int EnemyLevel { get; private set; }

	internal int MinimumCredits { get; private set; }

	internal int MaximumCredits { get; private set; }

	internal int ObservedCorpses { get; private set; }

	internal string Evidence { get; private set; }

	internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }

	internal OrdinaryEnemyLevelCreditRule(int enemyLevel, int minimumCredits, int maximumCredits, int observedCorpses, string evidence, OrdinaryEnemyEvidenceState evidenceState = OrdinaryEnemyEvidenceState.Observed)
	{
		if (enemyLevel <= 0)
		{
			throw new ArgumentOutOfRangeException("enemyLevel");
		}
		if (minimumCredits < 0 || maximumCredits < minimumCredits)
		{
			throw new ArgumentOutOfRangeException("minimumCredits");
		}
		if (evidenceState != OrdinaryEnemyEvidenceState.Observed && evidenceState != OrdinaryEnemyEvidenceState.Policy)
		{
			throw new ArgumentOutOfRangeException("evidenceState");
		}
		if ((evidenceState == OrdinaryEnemyEvidenceState.Observed && observedCorpses <= 0) || (evidenceState == OrdinaryEnemyEvidenceState.Policy && observedCorpses < 0))
		{
			throw new ArgumentOutOfRangeException("observedCorpses");
		}
		if (string.IsNullOrWhiteSpace(evidence))
		{
			throw new ArgumentException("Credit evidence is required.", "evidence");
		}
		EnemyLevel = enemyLevel;
		MinimumCredits = minimumCredits;
		MaximumCredits = maximumCredits;
		ObservedCorpses = observedCorpses;
		Evidence = evidence;
		EvidenceState = evidenceState;
	}
}
