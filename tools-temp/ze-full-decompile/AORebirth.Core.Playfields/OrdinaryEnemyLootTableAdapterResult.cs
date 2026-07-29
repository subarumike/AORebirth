using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyLootTableAdapterResult
{
	internal LootTableDefinition Table { get; private set; }

	internal LootAssignmentDefinition Assignment { get; private set; }

	internal OrdinaryEnemyLootTableAdapterResult(LootTableDefinition table, LootAssignmentDefinition assignment)
	{
		if (table == null)
		{
			throw new ArgumentNullException("table");
		}
		if (assignment == null)
		{
			throw new ArgumentNullException("assignment");
		}
		Table = table;
		Assignment = assignment;
	}
}
