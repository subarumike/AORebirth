using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public static class CombatCorpseRules
{
	private sealed class ObservedCorpseCreditRule
	{
		public string Name { get; private set; }

		public int MonsterData { get; private set; }

		public int MinCredits { get; private set; }

		public int MaxCredits { get; private set; }

		public ObservedCorpseCreditRule(string name, int monsterData, int minCredits, int maxCredits)
		{
			Name = name;
			MonsterData = monsterData;
			MinCredits = minCredits;
			MaxCredits = maxCredits;
		}

		public bool Matches(string targetName, int monsterData)
		{
			if (monsterData != 0 && MonsterData == monsterData)
			{
				return true;
			}
			return string.Equals(NormalizeName(targetName), Name, StringComparison.OrdinalIgnoreCase);
		}

		private static string NormalizeName(string targetName)
		{
			if (string.IsNullOrWhiteSpace(targetName))
			{
				return string.Empty;
			}
			string text = targetName.Trim();
			return text.StartsWith("Codex Test ", StringComparison.OrdinalIgnoreCase) ? text.Substring("Codex Test ".Length) : text;
		}
	}

	private static readonly ObservedCorpseCreditRule[] ObservedCreditRules = new ObservedCorpseCreditRule[6]
	{
		new ObservedCorpseCreditRule("Beach Leet", 17655, 1, 1),
		new ObservedCorpseCreditRule("Island Reet", 30365, 5, 5),
		new ObservedCorpseCreditRule("Shore Snake", 30252, 5, 5),
		new ObservedCorpseCreditRule("Surf Lizard", 22794, 1, 1),
		new ObservedCorpseCreditRule("Cliff Malle", 17660, 3, 3),
		new ObservedCorpseCreditRule("Reef Salamander", 30354, 23, 29)
	};

	public const int CorpseInventorySlots = 21;

	public const int MoveToInventoryPlacement = 111;

	public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(3.0);

	public static readonly TimeSpan EmptyCorpseLifetime = TimeSpan.FromSeconds(3.0);

	public static readonly TimeSpan RegularLootCorpseLifetime = TimeSpan.FromMinutes(4.0);

	public static readonly TimeSpan MajorBossCorpseLifetime = TimeSpan.FromMinutes(30.0);

	public static bool TryGetObservedCreditRange(string name, int monsterData, out int minimumCredits, out int maximumCredits)
	{
		ObservedCorpseCreditRule observedCorpseCreditRule = ObservedCreditRules.FirstOrDefault((ObservedCorpseCreditRule value) => value.Matches(name, monsterData));
		if (observedCorpseCreditRule == null)
		{
			minimumCredits = 0;
			maximumCredits = 0;
			return false;
		}
		minimumCredits = observedCorpseCreditRule.MinCredits;
		maximumCredits = observedCorpseCreditRule.MaxCredits;
		return true;
	}

	public static CombatCorpseLootClass LootClassFor(int unlootedItemCount, int unlootedCredits, bool isMajorBoss)
	{
		if (unlootedItemCount <= 0 && unlootedCredits <= 0)
		{
			return CombatCorpseLootClass.Empty;
		}
		return (!isMajorBoss) ? CombatCorpseLootClass.RegularLoot : CombatCorpseLootClass.MajorBoss;
	}

	public static TimeSpan LifetimeFor(CombatCorpseLootClass lootClass)
	{
		return lootClass switch
		{
			CombatCorpseLootClass.MajorBoss => MajorBossCorpseLifetime, 
			CombatCorpseLootClass.RegularLoot => RegularLootCorpseLifetime, 
			_ => EmptyCorpseLifetime, 
		};
	}

	public static bool ShouldDrop(int dropChancePercent, Func<int, int> nextRandom)
	{
		if (dropChancePercent <= 0)
		{
			return false;
		}
		if (dropChancePercent >= 100)
		{
			return true;
		}
		if (nextRandom == null)
		{
			throw new ArgumentNullException("nextRandom");
		}
		return nextRandom(100) < dropChancePercent;
	}

	public static bool ShouldDropBasisPoints(int dropChanceBasisPoints, Func<int, int> nextRandom)
	{
		if (dropChanceBasisPoints <= 0)
		{
			return false;
		}
		if (dropChanceBasisPoints >= 10000)
		{
			return true;
		}
		if (nextRandom == null)
		{
			throw new ArgumentNullException("nextRandom");
		}
		return nextRandom(10000) < dropChanceBasisPoints;
	}

	public static T FindLootItem<T>(IEnumerable<T> lootItems, int requestedLootSlot, Func<T, int> slotSelector, Func<T, bool> lootedSelector) where T : class
	{
		if (lootItems == null)
		{
			return null;
		}
		List<T> list = lootItems.Where((T x) => !lootedSelector(x)).ToList();
		T val = list.FirstOrDefault((T x) => slotSelector(x) == requestedLootSlot);
		if (val != null)
		{
			return val;
		}
		T val2 = list.FirstOrDefault((T x) => slotSelector(x) + 1 == requestedLootSlot);
		if (val2 != null)
		{
			return val2;
		}
		if (list.Count == 1 && requestedLootSlot <= 1)
		{
			return list[0];
		}
		return null;
	}

	public static short InventoryEntryCountFor(int multipleCount)
	{
		if (multipleCount <= 0 || multipleCount == 1234567890)
		{
			return 1;
		}
		return (multipleCount > 32767) ? short.MaxValue : ((short)multipleCount);
	}

	public static int RollObservedCredits(string targetName, int monsterData, Func<int, int> nextRandom)
	{
		ObservedCorpseCreditRule observedCorpseCreditRule = ObservedCreditRules.FirstOrDefault((ObservedCorpseCreditRule x) => x.Matches(targetName, monsterData));
		if (observedCorpseCreditRule == null)
		{
			return 0;
		}
		if (observedCorpseCreditRule.MaxCredits <= observedCorpseCreditRule.MinCredits)
		{
			return observedCorpseCreditRule.MinCredits;
		}
		if (nextRandom == null)
		{
			throw new ArgumentNullException("nextRandom");
		}
		return observedCorpseCreditRule.MinCredits + nextRandom(observedCorpseCreditRule.MaxCredits - observedCorpseCreditRule.MinCredits + 1);
	}
}
