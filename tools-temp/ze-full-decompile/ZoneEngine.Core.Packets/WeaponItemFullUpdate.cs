using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Packets;

public static class WeaponItemFullUpdate
{
	private const int MissingItemStatValue = 1234567890;

	public static void Send(IZoneClient client)
	{
		ICharacter character = client.Controller.Character;
		if (character != null && ((IItemContainer)character).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			WeaponItemFullUpdateMessage val = CreateForSlot(character, value, 6);
			if (val != null)
			{
				((IDynel)character).Send((MessageBody)(object)val, false);
				LogWeaponDefinition("sent-slot", character, null, val, announceToPlayfield: false);
			}
			WeaponItemFullUpdateMessage val2 = CreateForSlot(character, value, 8);
			if (val2 != null)
			{
				((IDynel)character).Send((MessageBody)(object)val2, false);
				LogWeaponDefinition("sent-slot", character, null, val2, announceToPlayfield: false);
			}
		}
	}

	public static void SendWeaponDefinitions(ICharacter character, bool announceToPlayfield = false)
	{
		WeaponItemFullUpdateMessage[] array = CreateWeaponDefinitionMessages(character);
		foreach (WeaponItemFullUpdateMessage val in array)
		{
			((IDynel)character).Send((MessageBody)(object)val, announceToPlayfield);
			LogWeaponDefinition("sent", character, null, val, announceToPlayfield);
		}
	}

	public static void SendWeaponDefinition(ICharacter character, IItem item)
	{
		if (character == null || item == null)
		{
			return;
		}
		foreach (IInventoryPage item2 in InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character))
		{
			for (int i = item2.FirstSlotNumber; i < item2.FirstSlotNumber + item2.MaxSlots; i++)
			{
				if (item2[i] == item)
				{
					WeaponItemFullUpdateMessage val = CreateForSlot(character, item2, i);
					if (val != null)
					{
						((IDynel)character).Send((MessageBody)(object)val, false);
						LogWeaponDefinition("sent-single", character, null, val, announceToPlayfield: false);
					}
					return;
				}
			}
		}
	}

	public static WeaponItemFullUpdateMessage[] CreateWeaponDefinitionMessages(ICharacter character)
	{
		if (character == null)
		{
			return (WeaponItemFullUpdateMessage[])(object)new WeaponItemFullUpdateMessage[0];
		}
		List<WeaponItemFullUpdateMessage> list = new List<WeaponItemFullUpdateMessage>();
		foreach (IInventoryPage item in InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character))
		{
			for (int i = item.FirstSlotNumber; i < item.FirstSlotNumber + item.MaxSlots; i++)
			{
				WeaponItemFullUpdateMessage val = CreateForSlot(character, item, i);
				if (val != null)
				{
					list.Add(val);
				}
			}
		}
		return list.ToArray();
	}

	public static WeaponItemFullUpdateMessage CreateRightHandWeaponDefinitionMessage(ICharacter character)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return null;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			return null;
		}
		return CreateForSlot(character, value, 6);
	}

	public static void SendRightHandWeaponDefinition(ICharacter character, bool announceToPlayfield = false)
	{
		WeaponItemFullUpdateMessage val = CreateRightHandWeaponDefinitionMessage(character);
		if (val != null)
		{
			((IDynel)character).Send((MessageBody)(object)val, announceToPlayfield);
			LogWeaponDefinition("sent-right-hand", character, null, val, announceToPlayfield);
		}
	}

	internal static void LogObserverWeaponDefinition(ICharacter owner, ICharacter recipient, WeaponItemFullUpdateMessage message)
	{
		LogWeaponDefinition("visibility-sync", owner, recipient, message, announceToPlayfield: false);
	}

	private static WeaponItemFullUpdateMessage CreateForSlot(ICharacter character, IInventoryPage page, int slot)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		IItem val = page[slot];
		if (val == null || !IsCombatWeaponItem(page, val, slot))
		{
			return null;
		}
		int quality = NormalizeValue(val.Quality);
		int lowId = NormalizeValue(val.LowID);
		int highId = NormalizeValue(val.HighID);
		int multipleCount = ((val.MultipleCount <= 0) ? 1 : val.MultipleCount);
		Identity orCreate = WeaponItemIdentity.GetOrCreate(val);
		WeaponItemFullUpdateMessage val2 = new WeaponItemFullUpdateMessage
		{
			Identity = orCreate,
			Unknown = 0,
			Unknown1 = 11
		};
		Identity val3 = default(Identity);
		((Identity)(ref val3)).Type = (IdentityType)50000;
		Identity identity = ((IEntity)character).Identity;
		((Identity)(ref val3)).Instance = ((Identity)(ref identity)).Instance;
		val2.Owner = val3;
		val3 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		val2.PlayfieldId = ((Identity)(ref val3)).Instance;
		val3 = default(Identity);
		((Identity)(ref val3)).Type = (IdentityType)1000015;
		((Identity)(ref val3)).Instance = 0;
		val2.StateMachine = val3;
		val2.Unknown2 = (short)(0x100 | (slot & 0xFF));
		val2.Stats = BuildStats(val, quality, lowId, highId, multipleCount);
		val2.Unknown3 = 0;
		return val2;
	}

	private static GameTuple<CharacterStat, uint>[] BuildStats(IItem item, int quality, int lowId, int highId, int multipleCount)
	{
		List<GameTuple<CharacterStat, uint>> list = new List<GameTuple<CharacterStat, uint>>
		{
			StatTuple((CharacterStat)0, (uint)NormalizeFlags(item.Flags)),
			StatTuple((CharacterStat)23, (uint)lowId),
			StatTuple((CharacterStat)701, (uint)quality),
			StatTuple((CharacterStat)702, (uint)lowId),
			StatTuple((CharacterStat)703, (uint)highId),
			StatTuple((CharacterStat)412, (uint)multipleCount),
			StatTuple((CharacterStat)26, ResolveEnergy(item))
		};
		AddStatIfPresent(list, (CharacterStat)294, item.GetAttribute(294));
		AddStatIfPresent(list, (CharacterStat)210, item.GetAttribute(210));
		return list.ToArray();
	}

	private static void AddStatIfPresent(ICollection<GameTuple<CharacterStat, uint>> stats, CharacterStat stat, int value)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (value != 1234567890)
		{
			stats.Add(StatTuple(stat, (uint)value));
		}
	}

	private static uint ResolveEnergy(IItem item)
	{
		int attribute = item.GetAttribute(26);
		if (attribute == 1234567890)
		{
			return uint.MaxValue;
		}
		return (uint)attribute;
	}

	private static GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return new GameTuple<CharacterStat, uint>
		{
			Value1 = stat,
			Value2 = value
		};
	}

	private static bool IsCombatWeaponItem(IInventoryPage page, IItem item, int slot)
	{
		if (page is WeaponInventoryPage)
		{
			return slot == 6 || slot == 8;
		}
		return item.ItemActions.Any((AOAction x) => (int)x.ActionType == 8) || HasWeaponStats(item);
	}

	private static bool IsWeaponItem(IInventoryPage page, IItem item)
	{
		return IsCombatWeaponItem(page, item, 6);
	}

	private static bool HasWeaponStats(IItem item)
	{
		return NormalizeValue(item.GetAttribute(286)) > 0 || NormalizeValue(item.GetAttribute(285)) > 0;
	}

	private static int NormalizeFlags(int flags)
	{
		return (flags > 0 && flags != 1234567890) ? flags : 1027;
	}

	private static int NormalizeValue(int value)
	{
		if (value <= 0 || value == 1234567890)
		{
			return 0;
		}
		return value;
	}

	private static void LogWeaponDefinition(string phase, ICharacter owner, ICharacter recipient, WeaponItemFullUpdateMessage message, bool announceToPlayfield)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		if (ShouldLogWeaponDefinition(owner))
		{
			int num = message.Unknown2 & 0xFF;
			object obj;
			if (recipient != null)
			{
				Identity identity = ((IEntity)recipient).Identity;
				obj = ((object)(Identity)(ref identity)).ToString();
			}
			else
			{
				obj = "none";
			}
			string text = (string)obj;
			LogUtil.Debug((DebugInfoDetail)512, $"WeaponItemFullUpdate {phase} owner={((owner == null) ? Identity.None : ((IEntity)owner).Identity)} recipient={text} weapon={((N3Message)message).Identity} slot={num} ownerField={message.Owner} playfield={message.PlayfieldId} stats={((message.Stats != null) ? message.Stats.Length : 0)} announce={(announceToPlayfield ? 1 : 0)}");
		}
	}

	private static bool ShouldLogWeaponDefinition(ICharacter owner)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (owner != null && ((IInstancedEntity)owner).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
			if (((Identity)(ref identity)).Instance == 127 && ((IStats)owner).Stats[(StatIds)359].Value == 26092)
			{
				result = (string.Equals(((INamedEntity)owner).Name, "Thief", StringComparison.Ordinal) ? 1 : 0);
				goto IL_0053;
			}
		}
		result = 0;
		goto IL_0053;
		IL_0053:
		return (byte)result != 0;
	}
}
