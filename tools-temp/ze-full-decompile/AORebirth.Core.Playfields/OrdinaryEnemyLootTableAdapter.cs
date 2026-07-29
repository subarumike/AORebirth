using System;
using System.Globalization;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal static class OrdinaryEnemyLootTableAdapter
{
	internal static OrdinaryEnemyLootTableAdapterResult Build(OrdinaryEnemyProfile profile, string tableKey, string assignmentKey)
	{
		return Build(profile, 0, tableKey, assignmentKey);
	}

	internal static OrdinaryEnemyLootTableAdapterResult Build(OrdinaryEnemyProfile profile, int targetLevel, string tableKey, string assignmentKey)
	{
		if (profile == null)
		{
			throw new ArgumentNullException("profile");
		}
		if (string.IsNullOrWhiteSpace(tableKey))
		{
			throw new ArgumentException("Table key is required.", "tableKey");
		}
		if (string.IsNullOrWhiteSpace(assignmentKey))
		{
			throw new ArgumentException("Assignment key is required.", "assignmentKey");
		}
		OrdinaryEnemyProfileValidator.ValidateLootProfile(profile.ProfileKey, profile.Loot);
		LootGroupDefinition[] rollGroups = BuildGroups(profile.Loot);
		bool flag = profile.Loot.LevelCreditRules.Length != 0 && targetLevel > 0;
		string text = string.Join(",", new string[2]
		{
			profile.Loot.ItemEvidenceReference,
			profile.Loot.CreditEvidenceReference
		}.Where((string value) => !string.IsNullOrWhiteSpace(value)));
		if (string.IsNullOrWhiteSpace(text))
		{
			text = profile.Loot.Evidence.ToString();
		}
		LootEvidenceConfidence confidence = ConfidenceFor(profile.Loot);
		LootTableDefinition table = new LootTableDefinition
		{
			LootTableKey = tableKey,
			DisplayName = profile.DisplayName,
			TableType = LootTableType.EnemyType,
			RollGroups = rollGroups,
			CreditsPolicy = CreditsFor(profile.Loot, targetLevel),
			QualityPolicy = "captured-fixed",
			Evidence = text,
			Confidence = confidence,
			ItemPoolUnresolved = !profile.Loot.ItemPoolComplete,
			Enabled = true
		};
		LootAssignmentDefinition lootAssignmentDefinition = new LootAssignmentDefinition();
		lootAssignmentDefinition.AssignmentKey = assignmentKey;
		lootAssignmentDefinition.TargetType = LootAssignmentTargetType.EnemyType;
		lootAssignmentDefinition.TargetKey = profile.ProfileKey;
		lootAssignmentDefinition.LootTableKey = tableKey;
		lootAssignmentDefinition.MinimumLevel = (flag ? new int?(targetLevel) : null);
		lootAssignmentDefinition.MaximumLevel = (flag ? new int?(targetLevel) : null);
		lootAssignmentDefinition.Priority = 0;
		lootAssignmentDefinition.Evidence = text;
		lootAssignmentDefinition.Confidence = confidence;
		lootAssignmentDefinition.Enabled = true;
		lootAssignmentDefinition.Conditions = new string[0];
		LootAssignmentDefinition assignment = lootAssignmentDefinition;
		return new OrdinaryEnemyLootTableAdapterResult(table, assignment);
	}

	private static LootGroupDefinition[] BuildGroups(OrdinaryEnemyLootProfile loot)
	{
		OrdinaryEnemyLootEntry[] array = loot.Entries ?? new OrdinaryEnemyLootEntry[0];
		if (array.Length == 0)
		{
			return new LootGroupDefinition[0];
		}
		if (loot.PoolMode == OrdinaryEnemyLootPoolMode.WeightedOne)
		{
			return new LootGroupDefinition[1]
			{
				new LootGroupDefinition
				{
					LootGroupKey = "weighted-one",
					RollMode = LootRollMode.WeightedOne,
					RollCount = 1,
					EmptyWeight = loot.EmptyWeight,
					DropChanceBasisPoints = 10000,
					Entries = array.Select(AdaptEntry).ToArray(),
					Conditions = new string[0]
				}
			};
		}
		if (loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries)
		{
			throw new LootDefinitionValidationException("Unsupported ordinary enemy loot pool mode: " + loot.PoolMode);
		}
		return array.Select((OrdinaryEnemyLootEntry entry, int index) => new LootGroupDefinition
		{
			LootGroupKey = "entry." + index.ToString(CultureInfo.InvariantCulture),
			RollMode = ((entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven) ? LootRollMode.Guaranteed : LootRollMode.Independent),
			RollCount = 1,
			EmptyWeight = 0,
			DropChanceBasisPoints = 10000,
			Entries = new LootEntryDefinition[1] { AdaptEntry(entry) },
			Conditions = new string[0]
		}).ToArray();
	}

	private static LootEntryDefinition AdaptEntry(OrdinaryEnemyLootEntry entry)
	{
		bool flag = entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven;
		LootEntryDefinition lootEntryDefinition = new LootEntryDefinition();
		lootEntryDefinition.SelectionKey = string.Format(CultureInfo.InvariantCulture, "slot.{0}.item.{1}.{2}.ql.{3}", entry.Slot, entry.LowId, entry.HighId, entry.QualityLevel);
		lootEntryDefinition.ItemTemplateId = entry.LowId;
		lootEntryDefinition.HighItemTemplateId = entry.HighId;
		lootEntryDefinition.FixedQuality = entry.QualityLevel;
		lootEntryDefinition.MinimumQuality = entry.QualityLevel;
		lootEntryDefinition.MaximumQuality = entry.QualityLevel;
		lootEntryDefinition.MinimumQuantity = entry.Quantity;
		lootEntryDefinition.MaximumQuantity = entry.Quantity;
		lootEntryDefinition.Weight = entry.Weight;
		lootEntryDefinition.DropChanceBasisPoints = entry.DropChanceBasisPoints;
		lootEntryDefinition.UniquePerCorpse = true;
		lootEntryDefinition.Semantics = ((!flag) ? LootSemantics.ObservedAvailable : LootSemantics.GuaranteedProven);
		lootEntryDefinition.Evidence = ((entry.LinkageEvidence != OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence) ? LootEvidenceConfidence.ProvenCapture : LootEvidenceConfidence.ObservedAvailableLoot);
		lootEntryDefinition.EvidenceReference = entry.EvidenceReference;
		lootEntryDefinition.LinkageEvidence = entry.LinkageEvidence.ToString();
		lootEntryDefinition.ProbabilityEvidence = entry.ProbabilityEvidence.ToString();
		return lootEntryDefinition;
	}

	private static CreditsPolicyDefinition CreditsFor(OrdinaryEnemyLootProfile loot, int targetLevel)
	{
		OrdinaryEnemyLevelCreditRule ordinaryEnemyLevelCreditRule = ((targetLevel > 0) ? loot.LevelCreditRules.FirstOrDefault((OrdinaryEnemyLevelCreditRule value) => value.EnemyLevel == targetLevel) : null);
		if (ordinaryEnemyLevelCreditRule != null)
		{
			return CreditsRange(ordinaryEnemyLevelCreditRule.MinimumCredits, ordinaryEnemyLevelCreditRule.MaximumCredits, (ordinaryEnemyLevelCreditRule.EvidenceState == OrdinaryEnemyEvidenceState.Observed) ? LootEvidenceConfidence.ProvenCapture : LootEvidenceConfidence.Inferred);
		}
		if (loot.ObservedCreditOutcomes.Length != 0)
		{
			int[] array = (int[])loot.ObservedCreditOutcomes.Clone();
			return new CreditsPolicyDefinition
			{
				Mode = CreditsPolicyMode.ObservedSamples,
				MinimumCredits = array.Min(),
				MaximumCredits = array.Max(),
				ObservedCredits = array,
				Evidence = LootEvidenceConfidence.ObservedAvailableLoot
			};
		}
		if ((loot.CreditEvidence == OrdinaryEnemyEvidenceState.Observed || loot.CreditEvidence == OrdinaryEnemyEvidenceState.Policy) && loot.MinimumCredits.HasValue && loot.MaximumCredits.HasValue)
		{
			return CreditsRange(loot.MinimumCredits.Value, loot.MaximumCredits.Value, (loot.CreditEvidence == OrdinaryEnemyEvidenceState.Observed) ? LootEvidenceConfidence.ProvenCapture : LootEvidenceConfidence.Inferred);
		}
		return new CreditsPolicyDefinition
		{
			Mode = CreditsPolicyMode.Unresolved,
			Evidence = LootEvidenceConfidence.Unresolved
		};
	}

	private static CreditsPolicyDefinition CreditsRange(int minimum, int maximum, LootEvidenceConfidence evidence)
	{
		return new CreditsPolicyDefinition
		{
			Mode = ((minimum == maximum) ? CreditsPolicyMode.Fixed : CreditsPolicyMode.Range),
			MinimumCredits = minimum,
			MaximumCredits = maximum,
			Evidence = evidence
		};
	}

	private static LootEvidenceConfidence ConfidenceFor(OrdinaryEnemyLootProfile loot)
	{
		if (loot.Entries.Length != 0 && loot.Entries.All((OrdinaryEnemyLootEntry value) => value.LinkageEvidence == OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem || value.LinkageEvidence == OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem))
		{
			return LootEvidenceConfidence.ProvenCapture;
		}
		if (loot.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot)
		{
			return LootEvidenceConfidence.ObservedAvailableLoot;
		}
		return LootEvidenceConfidence.Unresolved;
	}
}
