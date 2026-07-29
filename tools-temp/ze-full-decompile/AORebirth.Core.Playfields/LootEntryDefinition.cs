namespace AORebirth.Core.Playfields;

internal sealed class LootEntryDefinition
{
	internal string SelectionKey { get; set; }

	internal int ItemTemplateId { get; set; }

	internal int HighItemTemplateId { get; set; }

	internal int? FixedQuality { get; set; }

	internal int MinimumQuality { get; set; }

	internal int MaximumQuality { get; set; }

	internal int MinimumQuantity { get; set; }

	internal int MaximumQuantity { get; set; }

	internal int Weight { get; set; }

	internal int DropChanceBasisPoints { get; set; }

	internal bool UniquePerCorpse { get; set; }

	internal LootSemantics Semantics { get; set; }

	internal LootEvidenceConfidence Evidence { get; set; }

	internal string EvidenceReference { get; set; }

	internal string LinkageEvidence { get; set; }

	internal string ProbabilityEvidence { get; set; }
}
