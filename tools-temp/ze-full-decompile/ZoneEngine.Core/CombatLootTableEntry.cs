using System;

namespace ZoneEngine.Core;

public sealed class CombatLootTableEntry
{
	public string ExactName { get; set; }

	public string MobTemplateHash { get; set; }

	public int MonsterData { get; set; }

	public int NpcFamily { get; set; }

	public int Slot { get; set; }

	public int DropChancePercent { get; set; }

	public int DropChanceBasisPoints { get; set; }

	public int Quality { get; set; }

	public int[] ItemTemplateIds { get; set; }

	public CombatLootItemTemplate[] ItemTemplates { get; set; }

	public int EffectiveDropChanceBasisPoints
	{
		get
		{
			if (DropChanceBasisPoints > 0)
			{
				return DropChanceBasisPoints;
			}
			return DropChancePercent * 100;
		}
	}

	public bool Matches(string targetName, int monsterData, int npcFamily)
	{
		if (!string.IsNullOrEmpty(ExactName) && !string.Equals(targetName, ExactName, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (MonsterData != 0 && monsterData != MonsterData)
		{
			return false;
		}
		if (NpcFamily != 0 && npcFamily != NpcFamily)
		{
			return false;
		}
		return true;
	}
}
