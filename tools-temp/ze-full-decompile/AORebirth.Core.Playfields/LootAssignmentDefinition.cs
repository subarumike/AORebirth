namespace AORebirth.Core.Playfields;

internal sealed class LootAssignmentDefinition
{
	internal string AssignmentKey { get; set; }

	internal LootAssignmentTargetType TargetType { get; set; }

	internal string TargetKey { get; set; }

	internal string LootTableKey { get; set; }

	internal int? PlayfieldId { get; set; }

	internal string EncounterKey { get; set; }

	internal int? MinimumLevel { get; set; }

	internal int? MaximumLevel { get; set; }

	internal int Priority { get; set; }

	internal string[] Conditions { get; set; }

	internal string Evidence { get; set; }

	internal LootEvidenceConfidence Confidence { get; set; }

	internal bool Enabled { get; set; }
}
