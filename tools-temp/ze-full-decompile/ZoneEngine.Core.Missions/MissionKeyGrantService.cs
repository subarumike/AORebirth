using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Missions;

internal static class MissionKeyGrantService
{
	private const int MissionKeyIdentityType = 51053;

	private const int MissionKeyTemplateId = 28577;

	private const int MissionKeyStateMachineType = 1000015;

	private const byte MissionKeyUnknown2 = 113;

	private const byte MissionKeyOverflowSlot = 111;

	private const int RepairItemLowId = 87810;

	private const int RepairItemHighId = 87810;

	private const int RepairItemFallbackLowId = 95576;

	private const int RepairItemFallbackHighId = 95576;

	private const int RepairItemQuality = 1;

	private const byte RepairItemOverflowSlot = 112;

	private const string RepairItemDisplayName = "Mission Repair Kit";

	private const uint MissionKeyFlags = 2147484165u;

	private const int MissionKeyDeleteAction = 47;

	private static int missionKeyInstanceSeed = Math.Max(16150634, (int)(DateTime.UtcNow.Ticks & 0x3FFFFFFF));

	public static bool TryGrantMissionKey(IZoneClient client, ICharacter character, string keyName, out int keyInstance, out InventoryError inventoryError)
	{
		return TryGrantItem(client, character, 28577, 28577, 1, keyName, 111, out keyInstance, out inventoryError);
	}

	public static bool TryGrantRepairItem(IZoneClient client, ICharacter character, int quality, out int itemInstance, out InventoryError inventoryError)
	{
		int quality2 = ((quality <= 0) ? 1 : quality);
		if (!TryResolveRepairTemplateIds(out var lowId, out var highId))
		{
			itemInstance = 0;
			inventoryError = (InventoryError)(-1);
			LogUtil.Debug((DebugInfoDetail)128, "MissionKeyGrant repair kit templates missing from items.dat");
			return false;
		}
		return TryGrantItem(client, character, lowId, highId, quality2, "Mission Repair Kit", 112, out itemInstance, out inventoryError);
	}

	public static bool IsRepairTool(IItem item)
	{
		if (item == null)
		{
			return false;
		}
		return (item.LowID == 87810 && item.HighID == 87810) || (item.LowID == 95576 && item.HighID == 95576);
	}

	public static bool HasRepairTool(ICharacter character)
	{
		IItem found;
		return TryFindRepairTool(character, out found);
	}

	public static bool TryConsumeRepairTool(IZoneClient client, ICharacter character, IItem repairItem)
	{
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Expected O, but got Unknown
		if (client == null || character == null || ((IItemContainer)character).BaseInventory == null || repairItem == null || !IsRepairTool(repairItem))
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				IItem value = item.Value;
				if (value == null || !IsRepairTool(value))
				{
					continue;
				}
				_ = value.Identity;
				Identity identity = value.Identity;
				int instance = ((Identity)(ref identity)).Instance;
				identity = repairItem.Identity;
				if (instance != ((Identity)(ref identity)).Instance)
				{
					continue;
				}
				try
				{
					page.Value.Remove(item.Key);
					((IItemContainer)character).BaseInventory.Write();
				}
				catch
				{
					return false;
				}
				client.SendCompressed((MessageBody)new DespawnMessage
				{
					Identity = value.Identity,
					Unknown = 1
				});
				return true;
			}
		}
		return false;
	}

	private static bool TryResolveRepairTemplateIds(out int lowId, out int highId)
	{
		if (ItemLoader.ItemList.ContainsKey(87810) && ItemLoader.ItemList.ContainsKey(87810))
		{
			lowId = 87810;
			highId = 87810;
			return true;
		}
		if (ItemLoader.ItemList.ContainsKey(95576) && ItemLoader.ItemList.ContainsKey(95576))
		{
			lowId = 95576;
			highId = 95576;
			return true;
		}
		lowId = 0;
		highId = 0;
		return false;
	}

	private static bool TryFindRepairTool(ICharacter character, out IItem found)
	{
		found = null;
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				IItem value = item.Value;
				if (value != null && IsRepairTool(value))
				{
					found = value;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryGrantItem(IZoneClient client, ICharacter character, int lowId, int highId, int quality, string itemName, byte overflowSlot, out int itemInstance, out InventoryError inventoryError)
	{
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected I4, but got Unknown
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Expected O, but got Unknown
		itemInstance = 0;
		inventoryError = (InventoryError)(-1);
		if (client == null || character == null || ((IItemContainer)character).BaseInventory == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(((IItemContainer)character).BaseInventory.StandardPage, out var value))
		{
			return false;
		}
		int num = value.FindFreeSlot();
		if (num == -1)
		{
			inventoryError = (InventoryError)2;
			return false;
		}
		if (!ItemLoader.ItemList.ContainsKey(lowId) || !ItemLoader.ItemList.ContainsKey(highId))
		{
			inventoryError = (InventoryError)(-1);
			LogUtil.Debug((DebugInfoDetail)128, "MissionKeyGrant missing item template '" + itemName + "' low=" + lowId + " high=" + highId);
			return false;
		}
		Item val;
		try
		{
			val = CreateItem(lowId, highId, quality);
		}
		catch (Exception ex)
		{
			inventoryError = (InventoryError)(-1);
			LogUtil.ErrorException(ex);
			return false;
		}
		inventoryError = (InventoryError)(int)value.Add(num, (IItem)(object)val);
		if (inventoryError)
		{
			return false;
		}
		try
		{
			if (!((IItemContainer)character).BaseInventory.Write())
			{
				TryRemoveInventorySlot(value, num);
				inventoryError = (InventoryError)(-1);
				return false;
			}
		}
		catch
		{
			TryRemoveInventorySlot(value, num);
			inventoryError = (InventoryError)(-1);
			return false;
		}
		Identity val2 = val.Identity;
		itemInstance = ((Identity)(ref val2)).Instance;
		client.SendCompressed((MessageBody)(object)CreateItemMessage(character, val.Identity, itemName, overflowSlot, lowId, highId, quality));
		ContainerAddItemMessage val3 = new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0
		};
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		((Identity)(ref val2)).Instance = 0;
		val3.SourceContainer = val2;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		Identity identity = ((IEntity)character).Identity;
		((Identity)(ref val2)).Instance = ((Identity)(ref identity)).Instance;
		val3.Target = val2;
		val3.TargetPlacement = overflowSlot;
		client.SendCompressed((MessageBody)val3);
		return true;
	}

	public static bool HasMissionKey(ICharacter character)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				IItem value = item.Value;
				if (value != null && value.LowID == 28577 && value.HighID == 28577)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool TryRemoveMissionKey(IZoneClient client, ICharacter character, int keyInstance)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Expected O, but got Unknown
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Expected O, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Invalid comparison between Unknown and I4
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51053;
		((Identity)(ref val)).Instance = keyInstance;
		Identity val2 = val;
		bool flag = false;
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				IItem value = item.Value;
				if (value == null)
				{
					continue;
				}
				_ = value.Identity;
				if (value.LowID != 28577 || value.HighID != 28577)
				{
					continue;
				}
				val = value.Identity;
				if ((int)((Identity)(ref val)).Type != 51053)
				{
					continue;
				}
				val = value.Identity;
				if (((Identity)(ref val)).Instance != keyInstance)
				{
					continue;
				}
				try
				{
					page.Value.Remove(item.Key);
					((IItemContainer)character).BaseInventory.Write();
					flag = true;
				}
				catch
				{
				}
				break;
			}
			if (flag)
			{
				break;
			}
		}
		client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)47,
			Unknown1 = 0,
			Target = val2,
			Parameter1 = 1,
			Parameter2 = 0,
			Unknown2 = 0
		});
		client.SendCompressed((MessageBody)new DespawnMessage
		{
			Identity = val2,
			Unknown = 1
		});
		return flag;
	}

	private static Item CreateItem(int lowId, int highId, int quality)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected I4, but got Unknown
		Item val = new Item(quality, lowId, highId);
		Identity identity = default(Identity);
		((Identity)(ref identity)).Type = (IdentityType)51053;
		((Identity)(ref identity)).Instance = CreateMissionKeyInstance();
		val.Identity = identity;
		val.Flags = 1;
		Item val2 = val;
		GameTuple<CharacterStat, uint>[] array = CreateItemStats(lowId, highId, quality);
		foreach (GameTuple<CharacterStat, uint> val3 in array)
		{
			val2.SetAttribute((int)val3.Value1, (int)val3.Value2);
		}
		val2.MultipleCount = 1;
		return val2;
	}

	private static int CreateMissionKeyInstance()
	{
		int num = Interlocked.Increment(ref missionKeyInstanceSeed) & 0x7FFFFFFF;
		return (num == 0) ? CreateMissionKeyInstance() : num;
	}

	private static SimpleItemFullUpdateMessage CreateItemMessage(ICharacter character, Identity itemIdentity, string itemName, byte overflowSlot, int lowId, int highId, int quality)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected I4, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Expected O, but got Unknown
		SimpleItemFullUpdateMessage val = new SimpleItemFullUpdateMessage();
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = ((Identity)(ref itemIdentity)).Type;
		((Identity)(ref val2)).Instance = ((Identity)(ref itemIdentity)).Instance;
		((N3Message)val).Identity = val2;
		((N3Message)val).Unknown = 0;
		val.MsgVersion = 11;
		val2 = ((IEntity)character).Identity;
		val.Identitytype = (int)((Identity)(ref val2)).Type;
		val2 = ((IEntity)character).Identity;
		val.Instance = ((Identity)(ref val2)).Instance;
		val2 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		val.Playfield = ((Identity)(ref val2)).Instance;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)1000015;
		((Identity)(ref val2)).Instance = 0;
		val.Unknown1 = val2;
		val.Unknown2 = 113;
		val.Unknown3 = overflowSlot;
		val.Stats = CreateItemStats(lowId, highId, quality);
		val.Name = TerminatedName(itemName);
		return val;
	}

	private static string TerminatedName(string keyName)
	{
		if (string.IsNullOrEmpty(keyName))
		{
			return string.Empty;
		}
		return keyName + "\0";
	}

	private static GameTuple<CharacterStat, uint>[] CreateItemStats(int lowId, int highId, int quality)
	{
		return new GameTuple<CharacterStat, uint>[6]
		{
			MissionKeyStat((CharacterStat)0, 2147484165u),
			MissionKeyStat((CharacterStat)23, lowId),
			MissionKeyStat((CharacterStat)701, quality),
			MissionKeyStat((CharacterStat)702, lowId),
			MissionKeyStat((CharacterStat)703, highId),
			MissionKeyStat((CharacterStat)412, 1)
		};
	}

	private static GameTuple<CharacterStat, uint> MissionKeyStat(CharacterStat stat, int value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return MissionKeyStat(stat, (uint)value);
	}

	private static GameTuple<CharacterStat, uint> MissionKeyStat(CharacterStat stat, uint value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return new GameTuple<CharacterStat, uint>
		{
			Value1 = stat,
			Value2 = value
		};
	}

	private static void TryRemoveInventorySlot(IInventoryPage inventoryPage, int inventorySlot)
	{
		try
		{
			if (inventoryPage != null && inventoryPage[inventorySlot] != null)
			{
				inventoryPage.Remove(inventorySlot);
			}
		}
		catch
		{
		}
	}
}
