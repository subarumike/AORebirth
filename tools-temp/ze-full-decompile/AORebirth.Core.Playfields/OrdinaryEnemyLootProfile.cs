namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyLootProfile
{
	internal OrdinaryEnemyLootEvidence Evidence { get; private set; }

	internal OrdinaryEnemyLootEntry[] Entries { get; private set; }

	internal OrdinaryEnemyLootPoolMode PoolMode { get; private set; }

	internal int EmptyWeight { get; private set; }

	internal bool ItemPoolComplete { get; private set; }

	internal int ObservedCompleteInventories { get; private set; }

	internal int ObservedEmptyInventories { get; private set; }

	internal string ItemEvidenceReference { get; private set; }

	internal OrdinaryEnemyEvidenceState CreditEvidence { get; private set; }

	internal int? MinimumCredits { get; private set; }

	internal int? MaximumCredits { get; private set; }

	internal OrdinaryEnemyLevelCreditRule[] LevelCreditRules { get; private set; }

	internal int[] ObservedCreditOutcomes { get; private set; }

	internal string CreditEvidenceReference { get; private set; }

	internal OrdinaryEnemyLootProfile(OrdinaryEnemyLootEvidence evidence, OrdinaryEnemyLootEntry[] entries, OrdinaryEnemyEvidenceState creditEvidence, int? minimumCredits, int? maximumCredits)
		: this(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, entries != null && entries.Length != 0, 0, 0, string.Empty, creditEvidence, minimumCredits, maximumCredits, new OrdinaryEnemyLevelCreditRule[0])
	{
	}

	internal OrdinaryEnemyLootProfile(OrdinaryEnemyLootEvidence evidence, OrdinaryEnemyLootEntry[] entries, OrdinaryEnemyEvidenceState creditEvidence, int? minimumCredits, int? maximumCredits, OrdinaryEnemyLevelCreditRule[] levelCreditRules)
		: this(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, entries != null && entries.Length != 0, 0, 0, string.Empty, creditEvidence, minimumCredits, maximumCredits, levelCreditRules)
	{
	}

	internal OrdinaryEnemyLootProfile(OrdinaryEnemyLootEvidence evidence, OrdinaryEnemyLootEntry[] entries, OrdinaryEnemyLootPoolMode poolMode, int emptyWeight, bool itemPoolComplete, int observedCompleteInventories, int observedEmptyInventories, string itemEvidenceReference, OrdinaryEnemyEvidenceState creditEvidence, int? minimumCredits, int? maximumCredits, OrdinaryEnemyLevelCreditRule[] levelCreditRules)
		: this(evidence, entries, poolMode, emptyWeight, itemPoolComplete, observedCompleteInventories, observedEmptyInventories, itemEvidenceReference, creditEvidence, minimumCredits, maximumCredits, levelCreditRules, new int[0], string.Empty)
	{
	}

	internal OrdinaryEnemyLootProfile(OrdinaryEnemyLootEvidence evidence, OrdinaryEnemyLootEntry[] entries, OrdinaryEnemyLootPoolMode poolMode, int emptyWeight, bool itemPoolComplete, int observedCompleteInventories, int observedEmptyInventories, string itemEvidenceReference, OrdinaryEnemyEvidenceState creditEvidence, int? minimumCredits, int? maximumCredits, OrdinaryEnemyLevelCreditRule[] levelCreditRules, int[] observedCreditOutcomes, string creditEvidenceReference)
	{
		Evidence = evidence;
		Entries = entries ?? new OrdinaryEnemyLootEntry[0];
		PoolMode = poolMode;
		EmptyWeight = emptyWeight;
		ItemPoolComplete = itemPoolComplete;
		ObservedCompleteInventories = observedCompleteInventories;
		ObservedEmptyInventories = observedEmptyInventories;
		ItemEvidenceReference = itemEvidenceReference ?? string.Empty;
		CreditEvidence = creditEvidence;
		MinimumCredits = minimumCredits;
		MaximumCredits = maximumCredits;
		LevelCreditRules = levelCreditRules ?? new OrdinaryEnemyLevelCreditRule[0];
		ObservedCreditOutcomes = observedCreditOutcomes ?? new int[0];
		CreditEvidenceReference = creditEvidenceReference ?? string.Empty;
	}
}
