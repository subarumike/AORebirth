using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;

namespace ZoneEngine.Core;

public static class CombatMobLootCatalog
{
	public static CombatLootTableEntry[] BuildEntries(IEnumerable<DBMobTemplate> mobTemplates, IEnumerable<DBMobDroptable> dropTable)
	{
		if (mobTemplates == null || dropTable == null)
		{
			return new CombatLootTableEntry[0];
		}
		Dictionary<string, List<DBMobDroptable>> dropsByHash = dropTable.Where((DBMobDroptable x) => x != null && !string.IsNullOrWhiteSpace(x.Hash)).GroupBy((DBMobDroptable x) => x.Hash.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, DBMobDroptable> x) => x.Key, (IGrouping<string, DBMobDroptable> x) => x.ToList(), StringComparer.OrdinalIgnoreCase);
		List<CombatLootTableEntry> list = new List<CombatLootTableEntry>();
		foreach (DBMobTemplate item in mobTemplates.Where(HasDropHashes))
		{
			string[] array = SplitLootField(item.DropHashes, ',');
			string[] values = SplitLootField(item.DropSlots, ',');
			string[] values2 = SplitLootField(item.DropRates, ',');
			for (int i = 0; i < array.Length; i++)
			{
				CombatLootItemTemplate[] array2 = ExpandDropHashExpression(array[i], dropsByHash).ToArray();
				if (array2.Length != 0)
				{
					int num = ParseDropRateBasisPoints(values2, i);
					list.Add(new CombatLootTableEntry
					{
						ExactName = item.Name,
						MobTemplateHash = item.Hash,
						MonsterData = item.MonsterData,
						NpcFamily = item.NPCFamily,
						Slot = ParseIntAt(values, i, i),
						DropChanceBasisPoints = num,
						DropChancePercent = num / 100,
						ItemTemplates = array2
					});
				}
			}
		}
		return list.ToArray();
	}

	private static bool HasDropHashes(DBMobTemplate template)
	{
		return template != null && !string.IsNullOrWhiteSpace(template.DropHashes);
	}

	private static IEnumerable<CombatLootItemTemplate> ExpandDropHashExpression(string expression, IDictionary<string, List<DBMobDroptable>> dropsByHash)
	{
		string[] array = SplitLootField(expression, '+');
		foreach (string dropHash in array)
		{
			if (!dropsByHash.TryGetValue(dropHash, out var rows))
			{
				continue;
			}
			foreach (DBMobDroptable row in rows)
			{
				yield return new CombatLootItemTemplate
				{
					LowId = row.LowId,
					HighId = row.HighId,
					MinQuality = row.MinQl,
					MaxQuality = row.MaxQl,
					RangeCheck = row.RangeCheck,
					DropGroupHash = row.Hash
				};
			}
			rows = null;
		}
	}

	private static string[] SplitLootField(string value, char separator)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new string[0];
		}
		return (from x in value.Split(new char[1] { separator }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).ToArray();
	}

	private static int ParseDropRateBasisPoints(string[] values, int index)
	{
		int num = ParseIntAt(values, index, 10000);
		if (num < 0)
		{
			return 0;
		}
		return (num > 10000) ? 10000 : num;
	}

	private static int ParseIntAt(string[] values, int index, int defaultValue)
	{
		if (values == null || index < 0 || index >= values.Length)
		{
			return defaultValue;
		}
		int result;
		return int.TryParse(values[index], out result) ? result : defaultValue;
	}
}
