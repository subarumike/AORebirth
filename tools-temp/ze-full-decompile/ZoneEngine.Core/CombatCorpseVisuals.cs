using System.Collections.Generic;

namespace ZoneEngine.Core;

public static class CombatCorpseVisuals
{
	public static Dictionary<int, int> BuildMonsterDataToCorpseCatMeshMap()
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>
		{
			{ 247831, 247826 },
			{ 247832, 247821 },
			{ 31114, 31102 },
			{ 17649, 15215 },
			{ 30379, 26978 },
			{ 203748, 5921 }
		};
		foreach (KeyValuePair<int, int> item in CombatTestMobArchetype.CorpseVisualMappings())
		{
			dictionary[item.Key] = item.Value;
		}
		return dictionary;
	}

	public static int CorpseCatMeshFor(int catMesh, int monsterData, IDictionary<int, int> monsterDataToCorpseCatMesh)
	{
		if (IsUsableVisualId(catMesh))
		{
			return catMesh;
		}
		if (monsterDataToCorpseCatMesh != null && monsterDataToCorpseCatMesh.TryGetValue(monsterData, out var value))
		{
			return value;
		}
		return monsterData;
	}

	public static int CorpseMonsterDataFor(int monsterData, int corpseCatMesh)
	{
		return IsUsableVisualId(monsterData) ? monsterData : corpseCatMesh;
	}

	public static int DeathAnimationKeyFor(int corpseAnimationKey, int itemAnimation, int defaultAnimationKey)
	{
		if (IsUsableVisualId(corpseAnimationKey))
		{
			return corpseAnimationKey;
		}
		if (IsUsableVisualId(itemAnimation))
		{
			return itemAnimation;
		}
		return defaultAnimationKey;
	}

	public static bool IsUsableVisualId(int value)
	{
		return value > 0 && value != 1234567890;
	}
}
