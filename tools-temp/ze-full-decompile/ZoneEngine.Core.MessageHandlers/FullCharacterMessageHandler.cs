using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class FullCharacterMessageHandler : BaseMessageHandler<FullCharacterMessage, FullCharacterMessageHandler>
{
	private static readonly int[] WireManagedXpStatIds = new int[4] { 52, 57, 334, 372 };

	public void Send(ICharacter character)
	{
		CombatXpRuntimeService.LogXpWireSnapshot(character, "FullCharacterMessageHandler", "fullcharacter-send-begin");
		Send(character, character);
	}

	public void Send(ICharacter dataProvider, ICharacter receiver)
	{
		((AbstractMessageHandler<FullCharacterMessage>)(object)this).Send(receiver, Filler(dataProvider), false);
	}

	private static MessageDataFiller<FullCharacterMessage> Filler(ICharacter character)
	{
		return delegate(FullCharacterMessage fullCharacterMessage)
		{
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Expected O, but got Unknown
			List<InventorySlot> list = new List<InventorySlot>();
			foreach (IInventoryPage item2 in InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character))
			{
				foreach (KeyValuePair<int, IItem> item3 in item2.List())
				{
					InventorySlot item = new InventorySlot
					{
						Placement = item3.Key,
						Flags = GetInventorySlotFlags(item3.Value),
						Count = (short)item3.Value.MultipleCount,
						Identity = GetInventorySlotIdentity(item2, item3.Key, item3.Value),
						ItemLowId = item3.Value.LowID,
						ItemHighId = item3.Value.HighID,
						Quality = item3.Value.Quality,
						Unknown = item3.Value.Nothing
					};
					list.Add(item);
				}
			}
			((N3Message)fullCharacterMessage).Identity = ((IEntity)character).Identity;
			fullCharacterMessage.MsgVersion = 26;
			fullCharacterMessage.InventorySlots = list.ToArray();
			fullCharacterMessage.UploadedNanoIds = character.UploadedNanos.Select((IUploadedNanos n) => n.NanoId).ToArray();
			fullCharacterMessage.Unknown2 = (FullCharacterSub[])(object)new FullCharacterSub[0];
			fullCharacterMessage.Unknown3 = 1;
			fullCharacterMessage.Unknown4 = (FullCharacterSub2[])(object)new FullCharacterSub2[0];
			fullCharacterMessage.UnknownI2 = 1;
			fullCharacterMessage.Unknown5 = (FullCharacterSub2[])(object)new FullCharacterSub2[0];
			fullCharacterMessage.UnknownI3 = 1;
			fullCharacterMessage.Unknown6 = (FullCharacterSub2[])(object)new FullCharacterSub2[0];
			IZoneClient client = ((IDynel)character).Controller.Client;
			List<GameTuple<int, uint>> list2 = new List<GameTuple<int, uint>>();
			AddStat3232(client, list2, 7);
			AddStat3232(client, list2, 418);
			AddStat3232(client, list2, 615);
			AddStat3232(client, list2, 616);
			AddStat3232(client, list2, 660);
			AddStat3232(client, list2, 669);
			AddStat3232(client, list2, 592);
			AddStat3232(client, list2, 355);
			AddStat3232(client, list2, 182);
			AddStat3232(client, list2, 579);
			AddStat3232(client, list2, 532);
			AddStat3232(client, list2, 577);
			AddStat3232(client, list2, 521);
			AddStat3232(client, list2, 576);
			AddStat3232(client, list2, 594);
			AddStat3232(client, list2, 595);
			AddStat3232(client, list2, 596);
			AddStat3232(client, list2, 597);
			AddStat3232(client, list2, 673);
			AddStat3232(client, list2, 674);
			AddStat3232(client, list2, 675);
			AddStat3232(client, list2, 676);
			AddStat3232(client, list2, 677);
			AddStat3232(client, list2, 678);
			AddStat3232(client, list2, 679);
			AddStat3232(client, list2, 680);
			AddStat3232(client, list2, 681);
			AddStat3232(client, list2, 682);
			AddStat3232(client, list2, 683);
			AddStat3232(client, list2, 684);
			AddStat3232(client, list2, 649);
			AddStat3232(client, list2, 650);
			AddStat3232(client, list2, 334);
			AddStat3232(client, list2, 0);
			AddStat3232(client, list2, 224);
			AddStat3232(client, list2, 582);
			AddStat3232(client, list2, 583);
			AddStat3232(client, list2, 360);
			AddStat3232(client, list2, 368);
			AddStat3232(client, list2, 168);
			AddStat3232(client, list2, 214);
			AddStat3232(client, list2, 221);
			AddStat3232(client, list2, 191);
			AddStat3232(client, list2, 470);
			AddStat3232(client, list2, 471);
			AddStat3232(client, list2, 472);
			AddStat3232(client, list2, 585);
			AddStat3232(client, list2, 586);
			AddStat3232(client, list2, 256);
			AddStat3232(client, list2, 257);
			AddStat3232(client, list2, 303);
			AddStat3232(client, list2, 432);
			AddStat3232(client, list2, 65);
			AddStat3232(client, list2, 66);
			AddStat3232(client, list2, 67);
			AddStat3232(client, list2, 544);
			AddStat3232(client, list2, 545);
			AddStat3232(client, list2, 617);
			AddStat3232(client, list2, 618);
			AddStat3232(client, list2, 619);
			AddStat3232(client, list2, 198);
			AddStat3232(client, list2, 349);
			AddStat3232(client, list2, 263);
			AddStat3232(client, list2, 264);
			AddStat3232(client, list2, 265);
			AddStat3232(client, list2, 266);
			AddStat3232(client, list2, 668);
			AddStat3232(client, list2, 670);
			AddStat3232(client, list2, 300);
			List<GameTuple<int, uint>> list3 = new List<GameTuple<int, uint>>();
			AddStat3232(client, list3, 68);
			AddStat3232(client, list3, 69);
			AddStat3232(client, list3, 672);
			AddStat3232(client, list3, 349);
			AddStat3232(client, list3, 275);
			AddStat3232(client, list3, 194);
			AddStat3232(client, list3, 27);
			AddStat3232(client, list3, 1);
			AddStat3232(client, list3, 21);
			AddStat3232(client, list3, 20);
			AddStat3232(client, list3, 19);
			AddStat3232(client, list3, 18);
			AddStat3232(client, list3, 17);
			AddStat3232(client, list3, 16);
			AddStat3232(client, list3, 63);
			AddStat3232(client, list3, 62);
			AddStat3232(client, list3, 61);
			AddStat3232(client, list3, 60);
			AddStat3232(client, list3, 51);
			AddStat3232(client, list3, 79);
			AddStat3232(client, list3, 12);
			AddStat3232(client, list3, 156);
			AddStat3232(client, list3, 34);
			AddStat3232(client, list3, 6);
			AddStat3232(client, list3, 4);
			AddStat3232(client, list3, 59);
			AddStat3232(client, list3, 372);
			AddStat3232(client, list3, 350);
			AddStat3232(client, list3, 57);
			AddStat3232(client, list3, 54);
			AddStat3232(client, list3, 52);
			AddStat3232(client, list3, 53);
			AddStat3232(client, list3, 78);
			AddStat3232(client, list3, 72);
			AddStat3232(client, list3, 11);
			AddStat3232(client, list3, 423);
			AddStat3232(client, list3, 58);
			AddStat3232(client, list3, 33);
			AddStat3232(client, list3, 430);
			AddStat3232(client, list3, 117);
			AddStat3232(client, list3, 101);
			AddStat3232(client, list3, 134);
			AddStat3232(client, list3, 133);
			AddStat3232(client, list3, 94);
			AddStat3232(client, list3, 122);
			AddStat3232(client, list3, 121);
			AddStat3232(client, list3, 148);
			AddStat3232(client, list3, 167);
			AddStat3232(client, list3, 140);
			AddStat3232(client, list3, 139);
			AddStat3232(client, list3, 166);
			AddStat3232(client, list3, 165);
			AddStat3232(client, list3, 164);
			AddStat3232(client, list3, 163);
			AddStat3232(client, list3, 162);
			AddStat3232(client, list3, 161);
			AddStat3232(client, list3, 160);
			AddStat3232(client, list3, 159);
			AddStat3232(client, list3, 158);
			AddStat3232(client, list3, 157);
			AddStat3232(client, list3, 3);
			AddStat3232(client, list3, 155);
			AddStat3232(client, list3, 154);
			AddStat3232(client, list3, 153);
			AddStat3232(client, list3, 152);
			AddStat3232(client, list3, 151);
			AddStat3232(client, list3, 150);
			AddStat3232(client, list3, 149);
			AddStat3232(client, list3, 147);
			AddStat3232(client, list3, 146);
			AddStat3232(client, list3, 145);
			AddStat3232(client, list3, 144);
			AddStat3232(client, list3, 143);
			AddStat3232(client, list3, 142);
			AddStat3232(client, list3, 141);
			AddStat3232(client, list3, 138);
			AddStat3232(client, list3, 137);
			AddStat3232(client, list3, 136);
			AddStat3232(client, list3, 135);
			AddStat3232(client, list3, 132);
			AddStat3232(client, list3, 131);
			AddStat3232(client, list3, 130);
			AddStat3232(client, list3, 129);
			AddStat3232(client, list3, 128);
			AddStat3232(client, list3, 127);
			AddStat3232(client, list3, 126);
			AddStat3232(client, list3, 125);
			AddStat3232(client, list3, 124);
			AddStat3232(client, list3, 123);
			AddStat3232(client, list3, 120);
			AddStat3232(client, list3, 119);
			AddStat3232(client, list3, 118);
			AddStat3232(client, list3, 116);
			AddStat3232(client, list3, 115);
			AddStat3232(client, list3, 114);
			AddStat3232(client, list3, 113);
			AddStat3232(client, list3, 112);
			AddStat3232(client, list3, 111);
			AddStat3232(client, list3, 110);
			AddStat3232(client, list3, 109);
			AddStat3232(client, list3, 108);
			AddStat3232(client, list3, 107);
			AddStat3232(client, list3, 106);
			AddStat3232(client, list3, 105);
			AddStat3232(client, list3, 104);
			AddStat3232(client, list3, 103);
			AddStat3232(client, list3, 102);
			AddStat3232(client, list3, 100);
			AddStat3232(client, list3, 62);
			AddStat3232(client, list3, 75);
			AddStat3232(client, list3, 37);
			AddStat3232(client, list3, 215);
			AddStat3232(client, list3, 97);
			AddStat3232(client, list3, 96);
			AddStat3232(client, list3, 95);
			AddStat3232(client, list3, 94);
			AddStat3232(client, list3, 93);
			AddStat3232(client, list3, 92);
			AddStat3232(client, list3, 91);
			AddStat3232(client, list3, 90);
			AddStat3232(client, list3, 199);
			AddStat3232(client, list3, 348);
			AddStat3232(client, list3, 573);
			AddStat3232(client, list3, 389);
			AddStat3232(client, list3, 572);
			AddStat3232(client, list3, 571);
			AddStat3232(client, list3, 570);
			AddStat3232(client, list3, 569);
			AddStat3232(client, list3, 568);
			AddStat3232(client, list3, 567);
			AddStat3232(client, list3, 566);
			AddStat3232(client, list3, 565);
			AddStat3232(client, list3, 564);
			AddStat3232(client, list3, 563);
			AddStat3232(client, list3, 562);
			AddStat3232(client, list3, 561);
			AddStat3232(client, list3, 560);
			AddStat3232(client, list3, 521);
			AddStat3232(client, list3, 607);
			AddStat3232(client, list3, 616);
			AddStat3232(client, list3, 615);
			AddStat3232(client, list3, 169);
			AddStat3232(client, list3, 178);
			AddStat3232(client, list3, 40);
			List<GameTuple<byte, byte>> list4 = new List<GameTuple<byte, byte>>();
			AddStat88(client, list4, 236);
			AddStat88(client, list4, 10);
			AddStat88(client, list4, 174);
			AddStat88(client, list4, 173);
			AddStat88(client, list4, 47);
			AddStat88(client, list4, 89);
			AddStat88(client, list4, 213);
			AddStat88(client, list4, 45);
			List<GameTuple<byte, short>> list5 = new List<GameTuple<byte, short>>();
			AddStat816(client, list5, 238);
			AddStat816(client, list5, 239);
			AddStat816(client, list5, 240);
			AddStat816(client, list5, 241);
			AddStat816(client, list5, 242);
			AddStat816(client, list5, 243);
			AddStat816(client, list5, 246);
			AddStat816(client, list5, 244);
			AddStat816(client, list5, 245);
			AddStat816(client, list5, 247);
			AddStat816(client, list5, 49);
			AddStat816(client, list5, 214);
			AddStat816(client, list5, 221);
			AddStat816(client, list5, 181);
			AddStat816(client, list5, 9);
			AddStat816(client, list5, 237);
			fullCharacterMessage.Stats1 = list2.ToArray();
			fullCharacterMessage.Stats2 = list3.ToArray();
			fullCharacterMessage.Stats3 = list4.ToArray();
			fullCharacterMessage.Stats4 = list5.ToArray();
			fullCharacterMessage.Unknown9 = 0;
			fullCharacterMessage.Unknown10 = 0;
			fullCharacterMessage.Unknown11 = new object[0];
			fullCharacterMessage.Unknown12 = new object[0];
			fullCharacterMessage.Unknown13 = new object[0];
		};
	}

	public static InventorySlot[] BuildEquipmentInspectSlots(ICharacter character)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected O, but got Unknown
		List<InventorySlot> list = new List<InventorySlot>();
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return list.ToArray();
		}
		foreach (IInventoryPage item in InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character))
		{
			if (!IsEquipmentInspectPage(item))
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item2 in item.List())
			{
				if (item2.Value != null)
				{
					list.Add(new InventorySlot
					{
						Placement = item2.Key,
						Flags = GetInventorySlotFlags(item2.Value),
						Count = (short)item2.Value.MultipleCount,
						Identity = GetInventorySlotIdentity(item, item2.Key, item2.Value),
						ItemLowId = item2.Value.LowID,
						ItemHighId = item2.Value.HighID,
						Quality = item2.Value.Quality,
						Unknown = item2.Value.Nothing
					});
				}
			}
		}
		return list.ToArray();
	}

	private static bool IsEquipmentInspectPage(IInventoryPage page)
	{
		return page is WeaponInventoryPage || page is ArmorInventoryPage || page is ImplantInventoryPage || page is SocialArmorInventoryPage;
	}

	private static Identity GetInventorySlotIdentity(IInventoryPage page, int placement, IItem item)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected I4, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		if (item == null)
		{
			return Identity.None;
		}
		if (page != null)
		{
			Identity val;
			Identity identity;
			Identity val2;
			if (page is PlayerInventoryPage)
			{
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)50000;
				identity = ((IEntity)page).Identity;
				((Identity)(ref val)).Instance = (int)((Identity)(ref identity)).Type;
				val2 = val;
			}
			else
			{
				_ = ((IEntity)page).Identity;
				if (true)
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)50000;
					identity = ((IEntity)page).Identity;
					((Identity)(ref val)).Instance = ((Identity)(ref identity)).Instance;
					val2 = val;
				}
				else
				{
					val2 = Identity.None;
				}
			}
			val = default(Identity);
			int num;
			if (!(page is PlayerInventoryPage))
			{
				_ = ((IEntity)page).Identity;
				identity = ((IEntity)page).Identity;
				num = (int)((Identity)(ref identity)).Type;
			}
			else
			{
				num = 104;
			}
			((Identity)(ref val)).Type = (IdentityType)num;
			((Identity)(ref val)).Instance = placement;
			Identity val3 = val;
			Identity result = default(Identity);
			if (((Identity)(ref val2)).Instance != 0 && InventoryItemRules.TryEnsureMailForbiddenContainerIdentity(item, val2, val3, ref result))
			{
				return result;
			}
		}
		if (IsCombatWeaponItem(page, item, placement))
		{
			return WeaponItemIdentity.GetOrCreate(item);
		}
		return item.Identity;
	}

	private static short GetInventorySlotFlags(IItem item)
	{
		if (item == null)
		{
			return 0;
		}
		int num = item.Flags;
		if ((num & 0xA0) == 0)
		{
			num = 161;
		}
		return (short)num;
	}

	private static bool IsCombatWeaponItem(IInventoryPage page, IItem item, int slot)
	{
		if (page is WeaponInventoryPage)
		{
			return slot == 6 || slot == 8;
		}
		return item.ItemActions.Any((AOAction x) => (int)x.ActionType == 8) || HasWeaponStats(item);
	}

	private static bool HasWeaponStats(IItem item)
	{
		return NormalizeWeaponValue(item.GetAttribute(286)) > 0 || NormalizeWeaponValue(item.GetAttribute(285)) > 0;
	}

	private static int NormalizeWeaponValue(int value)
	{
		if (value <= 0 || value == 1234567890)
		{
			return 0;
		}
		return value;
	}

	private static void AddStat3232(IZoneClient client, IList<GameTuple<int, uint>> list, int statId)
	{
		for (int i = 0; i < WireManagedXpStatIds.Length; i++)
		{
			if (WireManagedXpStatIds[i] == statId)
			{
				return;
			}
		}
		GameTuple<int, uint> val = new GameTuple<int, uint>
		{
			Value1 = statId,
			Value2 = ((IStats)client.Controller.Character).Stats[statId].BaseValue
		};
		CombatXpRuntimeService.LogXpWireOutbound("FullCharacterMessageHandler", "fullcharacter-add-stat", client.Controller.Character, statId, val.Value2, "FullCharacter");
		list.Add(val);
	}

	private static void AddStat816(IZoneClient client, IList<GameTuple<byte, short>> list, int statId)
	{
		if (statId > 255)
		{
			Console.WriteLine("AddStat816 statId(" + statId + ") > 255");
		}
		GameTuple<byte, short> item = new GameTuple<byte, short>
		{
			Value1 = (byte)statId,
			Value2 = (short)((IStats)client.Controller.Character).Stats[statId].BaseValue
		};
		list.Add(item);
	}

	private static void AddStat88(IZoneClient client, IList<GameTuple<byte, byte>> list, int statId)
	{
		if (statId > 255)
		{
			Console.WriteLine("AddStat88 statId(" + statId + ") > 255");
		}
		GameTuple<byte, byte> item = new GameTuple<byte, byte>
		{
			Value1 = (byte)statId,
			Value2 = (byte)((IStats)client.Controller.Character).Stats[statId].BaseValue
		};
		list.Add(item);
	}
}
