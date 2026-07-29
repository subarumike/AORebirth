using System;
using System.Collections.Generic;
using System.Globalization;

namespace AORebirth.Core.Playfields;

internal static class SubwayLootPoolRules
{
	internal const int SubwayPlayfieldId = 127;

	internal static SubwayLootPoolSelectionPlan BuildSelectionPlan(SubwayLootRollContext context)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		if (context.PlayfieldId != 127)
		{
			throw new ArgumentException("Subway loot rules only accept playfield 127.", "context");
		}
		if (context.IsBoss)
		{
			return DedicatedPlan(context, SubwayLootPoolKind.Boss, "boss");
		}
		if (context.IsNamed)
		{
			return DedicatedPlan(context, SubwayLootPoolKind.Named, "named");
		}
		return new SubwayLootPoolSelectionPlan(context, new SubwayLootPoolReference[2]
		{
			new SubwayLootPoolReference(PoolKey(context.PlayfieldId, "dungeon", null), SubwayLootPoolKind.Dungeon),
			new SubwayLootPoolReference(PoolKey(context.PlayfieldId, "enemy", context.EnemyTypeKey), SubwayLootPoolKind.EnemyType)
		});
	}

	internal static SubwayLootPoolRollResult Roll(SubwayLootPoolDefinition pool, Func<int, int> nextRandom)
	{
		if (pool == null)
		{
			throw new ArgumentNullException("pool");
		}
		if (nextRandom == null)
		{
			throw new ArgumentNullException("nextRandom");
		}
		List<SubwayLootPoolCandidate> list = new List<SubwayLootPoolCandidate>();
		List<SubwayLootPoolCandidate> list2 = new List<SubwayLootPoolCandidate>();
		long num = pool.EmptyWeight;
		SubwayLootPoolCandidate[] candidates = pool.Candidates;
		foreach (SubwayLootPoolCandidate subwayLootPoolCandidate in candidates)
		{
			if (subwayLootPoolCandidate.ExplicitlyGuaranteed)
			{
				list.Add(subwayLootPoolCandidate);
			}
			else if (subwayLootPoolCandidate.Weight > 0)
			{
				list2.Add(subwayLootPoolCandidate);
				num += subwayLootPoolCandidate.Weight;
			}
		}
		if (num > int.MaxValue)
		{
			throw new InvalidOperationException("Loot pool weight exceeds Int32 range.");
		}
		if (num <= 0)
		{
			return new SubwayLootPoolRollResult(list.ToArray(), null);
		}
		int num2 = nextRandom((int)num);
		if (num2 < 0 || num2 >= num)
		{
			throw new InvalidOperationException("Loot random source returned an invalid value.");
		}
		if (num2 < pool.EmptyWeight)
		{
			return new SubwayLootPoolRollResult(list.ToArray(), null);
		}
		int num3 = num2 - pool.EmptyWeight;
		foreach (SubwayLootPoolCandidate item in list2)
		{
			if (num3 < item.Weight)
			{
				return new SubwayLootPoolRollResult(list.ToArray(), item);
			}
			num3 -= item.Weight;
		}
		throw new InvalidOperationException("Loot pool weights did not resolve a candidate.");
	}

	private static SubwayLootPoolSelectionPlan DedicatedPlan(SubwayLootRollContext context, SubwayLootPoolKind kind, string category)
	{
		return new SubwayLootPoolSelectionPlan(context, new SubwayLootPoolReference[1]
		{
			new SubwayLootPoolReference(PoolKey(context.PlayfieldId, category, context.EnemyTypeKey), kind)
		});
	}

	private static string PoolKey(int playfieldId, string category, string enemyTypeKey)
	{
		return (!string.IsNullOrEmpty(enemyTypeKey)) ? string.Format(CultureInfo.InvariantCulture, "subway.{0}.{1}.{2}", playfieldId, category, enemyTypeKey) : string.Format(CultureInfo.InvariantCulture, "subway.{0}.{1}", playfieldId, category);
	}
}
