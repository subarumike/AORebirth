using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core;

public class TradeSkill
{
	private static TradeSkill instance;

	public Dictionary<int, string> ItemNames = new Dictionary<int, string>();

	private readonly List<TradeSkillEntry> tradeSkillList = new List<TradeSkillEntry>();

	public static TradeSkill Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new TradeSkill();
			}
			return instance;
		}
	}

	static TradeSkill()
	{
	}

	private TradeSkill()
	{
		CacheItemNames();
		Console.WriteLine("Cached " + ItemNames.Count + " item names");
		CacheTradeSkills();
		Console.WriteLine("\rCached " + tradeSkillList.Count + " trade skill entries");
	}

	public string GetItemName(int lid, int hid, int ql)
	{
		try
		{
			string result = ItemNames[lid];
			string result2 = ItemNames[hid];
			int quality = ItemLoader.ItemList[lid].Quality;
			int quality2 = ItemLoader.ItemList[hid].Quality;
			if (ql > (quality2 - quality) / 2 + quality)
			{
				return result2;
			}
			return result;
		}
		catch (Exception)
		{
			return "NoName";
		}
	}

	public TradeSkillEntry GetTradeSkillEntry(int id1, int id2)
	{
		TradeSkillEntry tradeSkillEntry = tradeSkillList.FirstOrDefault((TradeSkillEntry x) => x.ID1 == id1 && x.ID2 == id2);
		if (tradeSkillEntry != null)
		{
			return tradeSkillEntry;
		}
		return ThrakGardenKeyCombineRules.TryMatch(id1, id2);
	}

	public int SourceProcessesCount(int id)
	{
		return tradeSkillList.Count((TradeSkillEntry x) => x.ID1 == id);
	}

	public int TargetProcessesCount(int id)
	{
		return tradeSkillList.Count((TradeSkillEntry x) => x.ID2 == id);
	}

	private void CacheItemNames()
	{
		foreach (DBItemName item in ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).GetAll((object)null))
		{
			ItemNames.Add(item.Id, item.Name);
		}
	}

	private void CacheTradeSkills()
	{
		int num = 0;
		tradeSkillList.Clear();
		foreach (DBTradeSkill item in ((Dao<DBTradeSkill, TradeSkillDao>)(object)Dao<DBTradeSkill, TradeSkillDao>.Instance).GetAll((object)null))
		{
			tradeSkillList.Add(TradeSkillEntry.ConvertFromDB(item));
			num++;
			if (num % 1000 == 0)
			{
				Console.Write("\rCached {0} trade skill entries", num);
			}
		}
	}
}
