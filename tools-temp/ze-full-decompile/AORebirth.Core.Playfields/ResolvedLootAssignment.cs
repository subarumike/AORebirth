namespace AORebirth.Core.Playfields;

internal sealed class ResolvedLootAssignment
{
	internal LootAssignmentDefinition Assignment { get; set; }

	internal LootTableDefinition Table { get; set; }

	internal int Specificity { get; set; }
}
