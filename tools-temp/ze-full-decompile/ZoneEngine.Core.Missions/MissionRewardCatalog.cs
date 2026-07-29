using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Missions;

internal static class MissionRewardCatalog
{
	private sealed class RewardItem
	{
		public int LowId;

		public int HighId;

		public int LowQl;

		public int HighQl;

		public string Name;

		public bool IsNano;
	}

	private sealed class MaliEntry
	{
		public MaliKey Key { get; set; }

		public int[] Value { get; set; }
	}

	private sealed class MaliKey
	{
		public int LowId { get; set; }

		public int HighId { get; set; }

		public int LowQl { get; set; }

		public int HighQl { get; set; }

		public string[] Tags { get; set; }

		public string Name { get; set; }
	}

	private const int NanoQlTolerance = 10;

	private static readonly object InitLock = new object();

	private static List<RewardItem> items;

	private static List<RewardItem> nanoItems;

	private static List<RewardItem> otherItems;

	private static bool loadAttempted;

	private static string lastLoadError;

	internal static string LastLoadError
	{
		get
		{
			EnsureLoaded();
			return lastLoadError;
		}
	}

	internal static int ItemCount
	{
		get
		{
			EnsureLoaded();
			return (items != null) ? items.Count : 0;
		}
	}

	public static bool TryPickReward(int missionQuality, Random rng, out QuestItemShort reward, out string itemName, out bool isNano)
	{
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		reward = null;
		itemName = null;
		isNano = false;
		EnsureLoaded();
		if (rng == null || missionQuality <= 0 || items == null || items.Count == 0)
		{
			return false;
		}
		RewardItem rewardItem = PickFrom(otherItems, missionQuality, 0, rng);
		if (rewardItem == null)
		{
			rewardItem = PickFrom(nanoItems, missionQuality, 10, rng);
			if (rewardItem != null)
			{
				isNano = true;
			}
		}
		if (rewardItem == null)
		{
			rewardItem = PickFrom(items, missionQuality, 0, rng);
		}
		if (rewardItem == null)
		{
			return false;
		}
		int quality = ResolveRewardQuality(rewardItem, missionQuality, isNano || rewardItem.IsNano, rng);
		reward = new QuestItemShort
		{
			LowId = rewardItem.LowId,
			HighId = rewardItem.HighId,
			Quality = quality,
			Unknown1 = 0
		};
		itemName = rewardItem.Name;
		isNano = rewardItem.IsNano;
		return true;
	}

	private static int ResolveRewardQuality(RewardItem item, int missionQuality, bool allowNanoBand, Random rng)
	{
		if (!allowNanoBand)
		{
			return Clamp(missionQuality, item.LowQl, item.HighQl);
		}
		int num = Math.Max(item.LowQl, missionQuality - 10);
		int num2 = Math.Min(item.HighQl, missionQuality + 10);
		if (num > num2)
		{
			return Clamp(missionQuality, item.LowQl, item.HighQl);
		}
		return rng.Next(num, num2 + 1);
	}

	private static RewardItem PickFrom(List<RewardItem> pool, int missionQuality, int tolerance, Random rng)
	{
		if (pool == null || pool.Count == 0)
		{
			return null;
		}
		int num = missionQuality - tolerance;
		int num2 = missionQuality + tolerance;
		List<RewardItem> list = new List<RewardItem>();
		for (int i = 0; i < pool.Count; i++)
		{
			RewardItem rewardItem = pool[i];
			if (rewardItem.LowQl <= num2 && rewardItem.HighQl >= num && (tolerance != 0 || (missionQuality >= rewardItem.LowQl && missionQuality <= rewardItem.HighQl)))
			{
				list.Add(rewardItem);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		return list[rng.Next(list.Count)];
	}

	private static int Clamp(int value, int min, int max)
	{
		if (value < min)
		{
			return min;
		}
		return (value > max) ? max : value;
	}

	private static void EnsureLoaded()
	{
		if (loadAttempted)
		{
			return;
		}
		lock (InitLock)
		{
			if (loadAttempted)
			{
				return;
			}
			loadAttempted = true;
			items = new List<RewardItem>();
			nanoItems = new List<RewardItem>();
			otherItems = new List<RewardItem>();
			string text = FindRewardsDirectory();
			if (text == null)
			{
				lastLoadError = "MissionRewards directory not found";
				return;
			}
			string[] array = new string[5] { "ItemDb_Clusters.json", "ItemDB_Implants.json", "ItemDb_Nanos.json", "ItemDb_Refined.json", "ItemDb_Rest.json" };
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			};
			int num = 0;
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				string path = Path.Combine(text, text2);
				if (!File.Exists(path))
				{
					continue;
				}
				try
				{
					string input = File.ReadAllText(path);
					List<MaliEntry> list = javaScriptSerializer.Deserialize<List<MaliEntry>>(input);
					if (list == null)
					{
						continue;
					}
					bool flag = text2.IndexOf("Nano", StringComparison.OrdinalIgnoreCase) >= 0;
					foreach (MaliEntry item in list)
					{
						if (item != null && item.Key != null && item.Key.LowId > 0)
						{
							RewardItem rewardItem = new RewardItem
							{
								LowId = item.Key.LowId,
								HighId = ((item.Key.HighId > 0) ? item.Key.HighId : item.Key.LowId),
								LowQl = item.Key.LowQl,
								HighQl = ((item.Key.HighQl > 0) ? item.Key.HighQl : item.Key.LowQl),
								Name = (item.Key.Name ?? string.Empty),
								IsNano = (flag || HasNanoTag(item.Key.Tags))
							};
							items.Add(rewardItem);
							if (rewardItem.IsNano)
							{
								nanoItems.Add(rewardItem);
							}
							else
							{
								otherItems.Add(rewardItem);
							}
						}
					}
					num++;
				}
				catch (Exception ex)
				{
					lastLoadError = text2 + ": " + ex.Message;
				}
			}
			if (items.Count == 0 && string.IsNullOrEmpty(lastLoadError))
			{
				lastLoadError = "No reward items loaded from " + text;
			}
			else if (items.Count > 0)
			{
				lastLoadError = null;
				MissionDiagnostics.Log("REWARD-CATALOG loaded files={0} items={1} nanos={2} other={3} dir={4}", num, items.Count, nanoItems.Count, otherItems.Count, text);
			}
		}
	}

	private static bool HasNanoTag(string[] tags)
	{
		if (tags == null)
		{
			return false;
		}
		for (int i = 0; i < tags.Length; i++)
		{
			if (string.Equals(tags[i], "nano", StringComparison.OrdinalIgnoreCase) || string.Equals(tags[i], "crystal", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static string FindRewardsDirectory()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		string[] array = new string[4]
		{
			Path.Combine(baseDirectory, "XML Data", "MissionRewards"),
			Path.Combine(baseDirectory, "MissionRewards"),
			Path.Combine(Directory.GetCurrentDirectory(), "XML Data", "MissionRewards"),
			Path.Combine(Directory.GetCurrentDirectory(), "MissionRewards")
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		return null;
	}
}
