namespace AORebirth.Core.Playfields;

internal sealed class ObservedCorpseSnapshotDefinition
{
	internal string SnapshotKey { get; set; }

	internal int Credits { get; set; }

	internal LootEntryDefinition[] Entries { get; set; }

	internal LootEvidenceConfidence Evidence { get; set; }

	internal LootEvidenceConfidence SelectionProbabilityEvidence { get; set; }

	internal string EvidenceReference { get; set; }
}
