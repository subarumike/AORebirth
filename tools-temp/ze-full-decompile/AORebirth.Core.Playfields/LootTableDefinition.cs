namespace AORebirth.Core.Playfields;

internal sealed class LootTableDefinition
{
	internal string LootTableKey { get; set; }

	internal string DisplayName { get; set; }

	internal LootTableType TableType { get; set; }

	internal LootGroupDefinition[] RollGroups { get; set; }

	internal ObservedCorpseSnapshotDefinition[] ObservedCorpseSnapshots { get; set; }

	internal CreditsPolicyDefinition CreditsPolicy { get; set; }

	internal string QualityPolicy { get; set; }

	internal string Evidence { get; set; }

	internal LootEvidenceConfidence Confidence { get; set; }

	internal bool ItemPoolUnresolved { get; set; }

	internal bool Enabled { get; set; }
}
