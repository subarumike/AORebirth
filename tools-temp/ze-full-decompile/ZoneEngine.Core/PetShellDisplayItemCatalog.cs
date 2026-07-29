using System.Collections.Generic;
using AORebirth.Core.Items;

namespace ZoneEngine.Core;

internal static class PetShellDisplayItemCatalog
{
	public static void EnsureRegistered(int lowId, int highId, int nanoId = 0)
	{
		EnsureItem(lowId);
		if (highId != lowId)
		{
			EnsureItem(highId);
		}
		string value = ((nanoId > 0) ? PetSummonNanoCatalog.GetBureaucratShellItemName(nanoId) : null);
		if (!string.IsNullOrWhiteSpace(value))
		{
			TradeSkill.Instance.ItemNames[lowId] = value;
			if (highId != lowId)
			{
				TradeSkill.Instance.ItemNames[highId] = value;
			}
		}
	}

	private static void EnsureItem(int itemId)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		if (!ItemLoader.ItemList.ContainsKey(itemId))
		{
			ItemLoader.ItemList[itemId] = new ItemTemplate
			{
				ID = itemId,
				Quality = 1,
				Flags = 0,
				ItemType = 0,
				Stats = new Dictionary<int, int>(),
				Attack = new Dictionary<int, int>(),
				Defend = new Dictionary<int, int>()
			};
		}
	}
}
