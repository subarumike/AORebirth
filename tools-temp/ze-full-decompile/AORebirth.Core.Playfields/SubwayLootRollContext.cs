using System;

namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootRollContext
{
	internal int PlayfieldId { get; private set; }

	internal string EnemyName { get; private set; }

	internal string EnemyTypeKey { get; private set; }

	internal int MonsterData { get; private set; }

	internal int EnemyLevel { get; private set; }

	internal int PlayerLevel { get; private set; }

	internal bool IsNamed { get; private set; }

	internal bool IsBoss { get; private set; }

	internal SubwayLootRollContext(int playfieldId, string enemyName, string enemyTypeKey, int monsterData, int enemyLevel, int playerLevel, bool isNamed, bool isBoss)
	{
		if (playfieldId <= 0)
		{
			throw new ArgumentOutOfRangeException("playfieldId");
		}
		if (string.IsNullOrWhiteSpace(enemyName))
		{
			throw new ArgumentException("Enemy name is required.", "enemyName");
		}
		if (!IsSafeEnemyTypeKey(enemyTypeKey))
		{
			throw new ArgumentException("Enemy type key must start with a lowercase letter and contain only lowercase letters, digits, or underscores.", "enemyTypeKey");
		}
		if (monsterData <= 0)
		{
			throw new ArgumentOutOfRangeException("monsterData");
		}
		if (enemyLevel <= 0)
		{
			throw new ArgumentOutOfRangeException("enemyLevel");
		}
		if (playerLevel <= 0)
		{
			throw new ArgumentOutOfRangeException("playerLevel");
		}
		PlayfieldId = playfieldId;
		EnemyName = enemyName;
		EnemyTypeKey = enemyTypeKey;
		MonsterData = monsterData;
		EnemyLevel = enemyLevel;
		PlayerLevel = playerLevel;
		IsNamed = isNamed;
		IsBoss = isBoss;
	}

	private static bool IsSafeEnemyTypeKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value[0] < 'a' || value[0] > 'z')
		{
			return false;
		}
		for (int i = 1; i < value.Length; i++)
		{
			char c = value[i];
			if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '_')
			{
				return false;
			}
		}
		return true;
	}
}
