using System.Collections.Generic;

namespace ZoneEngine.Core;

public static class CombatTestLootCatalog
{
	public static CombatLootTableEntry[] BuildEntries()
	{
		List<CombatLootTableEntry> list = new List<CombatLootTableEntry>();
		CombatTestMobArchetype.Entry[] all = CombatTestMobArchetype.All;
		foreach (CombatTestMobArchetype.Entry entry in all)
		{
			list.Add(new CombatLootTableEntry
			{
				ExactName = entry.DisplayName,
				MonsterData = entry.MonsterData,
				DropChancePercent = 100,
				Quality = 1,
				ItemTemplateIds = new int[1] { 27350 }
			});
			list.Add(new CombatLootTableEntry
			{
				ExactName = entry.DisplayName,
				MonsterData = entry.MonsterData,
				DropChancePercent = 100,
				Quality = 1,
				ItemTemplateIds = new int[5] { 27351, 85534, 85521, 273496, 273500 }
			});
			list.Add(new CombatLootTableEntry
			{
				ExactName = entry.DisplayName,
				MonsterData = entry.MonsterData,
				DropChancePercent = 100,
				Quality = 1,
				ItemTemplateIds = new int[1] { 27352 }
			});
		}
		return list.ToArray();
	}
}
