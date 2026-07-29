using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal sealed class LootGenerationResult
{
	internal List<GeneratedLootItem> Items { get; private set; }

	internal int Credits { get; set; }

	internal bool CreditsUnresolved { get; set; }

	internal bool LootUnresolved { get; set; }

	internal List<string> AppliedTableKeys { get; private set; }

	internal List<string> AppliedAssignmentKeys { get; private set; }

	internal List<LootRollEvidence> RollEvidence { get; private set; }

	internal List<string> SkippedEntries { get; private set; }

	internal int Seed { get; set; }

	internal string RegistryVersion { get; set; }

	internal LootGenerationResult()
	{
		Items = new List<GeneratedLootItem>();
		AppliedTableKeys = new List<string>();
		AppliedAssignmentKeys = new List<string>();
		RollEvidence = new List<LootRollEvidence>();
		SkippedEntries = new List<string>();
	}
}
