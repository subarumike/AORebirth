namespace AORebirth.Core.Playfields;

internal sealed class LootGroupDefinition
{
	internal string LootGroupKey { get; set; }

	internal LootRollMode RollMode { get; set; }

	internal int RollCount { get; set; }

	internal int EmptyWeight { get; set; }

	internal int DropChanceBasisPoints { get; set; }

	internal LootEntryDefinition[] Entries { get; set; }

	internal string[] Conditions { get; set; }
}
