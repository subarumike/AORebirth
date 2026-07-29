namespace AORebirth.Core.Playfields;

internal static class MissionInstanceLootCatalog
{
	internal sealed class LootDrop
	{
		public int MonsterData;

		public int LowId;

		public int HighId;

		public int Quality;
	}

	internal static readonly LootDrop[] CapturedDrops = new LootDrop[4]
	{
		new LootDrop
		{
			MonsterData = 26137,
			LowId = 130209,
			HighId = 130210,
			Quality = 154
		},
		new LootDrop
		{
			MonsterData = 26135,
			LowId = 142916,
			HighId = 142917,
			Quality = 137
		},
		new LootDrop
		{
			MonsterData = 26090,
			LowId = 121905,
			HighId = 121906,
			Quality = 127
		},
		new LootDrop
		{
			MonsterData = 26139,
			LowId = 101406,
			HighId = 101334,
			Quality = 146
		}
	};

	internal static readonly LootDrop FindItemA = new LootDrop
	{
		MonsterData = 0,
		LowId = 100010,
		HighId = 100010,
		Quality = 1
	};

	internal static readonly LootDrop FindItemB = new LootDrop
	{
		MonsterData = 0,
		LowId = 165839,
		HighId = 165840,
		Quality = 1
	};

	internal static LootDrop ResolveFindItemDrop(int salt)
	{
		return ((salt & 1) == 0) ? FindItemA : FindItemB;
	}

	internal static bool TryGetDrop(int monsterData, out LootDrop drop)
	{
		drop = null;
		for (int i = 0; i < CapturedDrops.Length; i++)
		{
			if (CapturedDrops[i].MonsterData == monsterData)
			{
				drop = CapturedDrops[i];
				return true;
			}
		}
		return false;
	}
}
