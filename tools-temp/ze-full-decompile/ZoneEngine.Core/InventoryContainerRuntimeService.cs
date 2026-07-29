using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using AORebirth.Core.Actions;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Requirements;
using AORebirth.Core.Textures;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.Functions.GameFunctions;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core;

public sealed class InventoryContainerRuntimeService
{
	public static readonly InventoryContainerRuntimeService Default = new InventoryContainerRuntimeService();

	private InventoryContainerRuntimeService()
	{
	}

	public void OpenBank(ICharacter character)
	{
		BaseMessageHandler<BankMessage, BankMessageHandler>.Default.Send(character);
	}

	public BankSlot[] ResolveBankSlots(ICharacter character)
	{
		return ((IItemContainer)character).BaseInventory.Pages[105].ToInventoryArray();
	}

	public IEnumerable<IInventoryPage> CharacterStateInventoryPages(ICharacter character)
	{
		foreach (IInventoryPage page in ((IItemContainer)character).BaseInventory.Pages.Values)
		{
			if (!(page is BankInventoryPage))
			{
				yield return page;
			}
		}
	}

	public void PublishMailBlockedContainerLinks(ICharacter character)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IItemContainer)character).BaseInventory == null || !((IItemContainer)character).BaseInventory.Pages.TryGetValue(104, out var value) || value == null)
		{
			return;
		}
		int num = 0;
		Identity val4 = default(Identity);
		foreach (KeyValuePair<int, IItem> item in value.List())
		{
			IItem value2 = item.Value;
			Item val = (Item)(object)((value2 is Item) ? value2 : null);
			if (val == null)
			{
				continue;
			}
			int num2 = item.Key;
			if (num2 < value.FirstSlotNumber)
			{
				num2 = value.FirstSlotNumber + num2;
			}
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)104;
			((Identity)(ref val2)).Instance = num2;
			Identity val3 = val2;
			if (InventoryItemRules.TryEnsureMailForbiddenContainerIdentity((IItem)(object)val, ((IEntity)character).Identity, val3, ref val4))
			{
				IInventoryPage orCreateBackpackPage = ((IItemContainer)character).BaseInventory.GetOrCreateBackpackPage(val4);
				bool flag = !orCreateBackpackPage.List().Any();
				int handle = BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.ReserveBackpackInventoryHandle();
				if (flag)
				{
					int handle2 = BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.ReserveBackpackInventoryHandle();
					BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendContainerIntroduce(character, orCreateBackpackPage, handle2);
					BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>.Default.Send(character, val, val3, ((IEntity)orCreateBackpackPage).Identity);
					BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendFreshContainerOpen(character, orCreateBackpackPage, handle);
				}
				else
				{
					BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>.Default.Send(character, val, val3, ((IEntity)orCreateBackpackPage).Identity);
					BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendContainerOpen(character, orCreateBackpackPage, handle);
				}
				BaseMessageHandler<ActionMessage, BackpackContainerActionMessageHandler>.Default.SendClose(character, val4);
				((IItemContainer)character).BaseInventory.MarkBackpackClosed(val4);
				num++;
			}
		}
		if (num > 0)
		{
			BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.Send(character, value);
		}
		if (((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
		{
			((IClient)((IDynel)character).Controller.Client).Server.Info((IClient)(object)((IDynel)character).Controller.Client, "Mail container guard: published {0} Container link(s) for Feedback_MailNoChests", new object[1] { num });
		}
	}

	public void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
	{
		if (PetBureaucratGuardianAppearance.IsGuardianPet(character) || !((IItemContainer)character).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			return;
		}
		bool flag = false;
		flag |= EnsureWeaponMesh(character, value, 6, 1, (StatIds)1006, (StatIds)1009);
		if (flag | EnsureWeaponMesh(character, value, 8, 2, (StatIds)1007, (StatIds)1010))
		{
			((IDynel)character).ChangedAppearance = true;
			if (announceAppearanceUpdate)
			{
				((IInstancedEntity)character).Playfield.AnnounceAppearanceUpdate(character);
			}
		}
	}

	public Identity ResolveContainerAddItemTargetIdentity(Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		Identity result = target;
		if ((int)((Identity)(ref result)).Type == 57005)
		{
			((Identity)(ref result)).Type = (IdentityType)50000;
		}
		return result;
	}

	public IInventoryPage ResolveContainerAddItemReceivingPage(IItemContainer itemReceiver, ICharacter character, Identity target, int toPlacement)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		IInventoryPage val = ((toPlacement != 111 || (int)((Identity)(ref target)).Type != 57005) ? itemReceiver.BaseInventory.PageFromSlot(toPlacement) : itemReceiver.BaseInventory.Pages[105]);
		if (val == null || ((object)itemReceiver).GetType() != ((object)character).GetType())
		{
			val = itemReceiver.BaseInventory.Pages[itemReceiver.BaseInventory.StandardPage];
		}
		return val;
	}

	public int ResolveContainerAddItemTargetPlacement(IInventoryPage receivingPage, int toPlacement)
	{
		if (toPlacement == 111)
		{
			return receivingPage.FindFreeSlot();
		}
		return toPlacement;
	}

	public bool TryMoveInventoryItemToBackpack(ICharacter character, ClientContainerAddItemMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Invalid comparison between Unknown and I4
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e6: Expected O, but got Unknown
		//IL_0402: Unknown result type (might be due to invalid IL or missing references)
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		Identity val = message.Source;
		if ((int)((Identity)(ref val)).Type == 104)
		{
			val = message.Target;
			if ((int)((Identity)(ref val)).Type == 51017)
			{
				IInventoryPage val2 = default(IInventoryPage);
				if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(104, out var value) || !((IItemContainer)character).BaseInventory.TryGetBackpackPage(message.Target, ref val2))
				{
					LogUtil.Debug((DebugInfoDetail)4, $"Rejected ClientContainerAddItem backpack move because pages are missing char={((IEntity)character).Identity} source={message.Source} target={message.Target}");
					return true;
				}
				val = message.Source;
				int instance = ((Identity)(ref val)).Instance;
				if (!value.ValidSlot(instance))
				{
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move for invalid source slot char={((IEntity)character).Identity} source={message.Source} target={message.Target}");
					return true;
				}
				IItem val3 = value[instance];
				if (val3 == null)
				{
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move because source slot is empty char={((IEntity)character).Identity} source={message.Source} target={message.Target}");
					return true;
				}
				if (InventoryItemRules.IsBackpackContainerItem(val3))
				{
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move because source item is a container char={((IEntity)character).Identity} source={message.Source} target={message.Target} item={val3.LowID}/{val3.HighID} ql={val3.Quality} itemIdentity={val3.Identity}");
					return true;
				}
				int num = val2.FindFreeSlot();
				if (num < 0)
				{
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move because backpack is full char={((IEntity)character).Identity} source={message.Source} target={message.Target}");
					return true;
				}
				try
				{
					InventoryError val4 = val2.Add(num, val3);
					if ((int)val4 > 0)
					{
						LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move add failed char={((IEntity)character).Identity} source={message.Source} target={message.Target} slot={num} error={val4}");
						return true;
					}
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move add threw char={((IEntity)character).Identity} source={message.Source} target={message.Target} slot={num} error={ex.Message}");
					return true;
				}
				try
				{
					value.Remove(instance);
				}
				catch (Exception ex2)
				{
					TryRemoveBackpackRollback(val2, num);
					LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem backpack move remove source threw char={((IEntity)character).Identity} source={message.Source} target={message.Target} slot={num} error={ex2.Message}");
					return true;
				}
				((IDynel)character).Send((MessageBody)new ContainerAddItemMessage
				{
					Identity = ((IEntity)character).Identity,
					Unknown = 0,
					SourceContainer = message.Source,
					Target = message.Target,
					TargetPlacement = num
				}, false);
				((IItemContainer)character).BaseInventory.Write();
				LogUtil.Debug((DebugInfoDetail)8, $"Persisted inventory after ClientContainerAddItem backpack move char={((IEntity)character).Identity} source={message.Source} target={message.Target} slot={num}");
				return true;
			}
		}
		return false;
	}

	public bool TryDepositInventoryItemToBank(ICharacter character, ClientContainerAddItemMessage message)
	{
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Invalid comparison between Unknown and I4
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Expected O, but got Unknown
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		if (!IsInventoryToBankDeposit(message))
		{
			return false;
		}
		Identity val = message.Target;
		int instance = ((Identity)(ref val)).Instance;
		val = ((IEntity)character).Identity;
		if (instance != ((Identity)(ref val)).Instance)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit for mismatched target char={((IEntity)character).Identity} target={message.Target} source={message.Source}");
			return true;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(104, out var value) || !((IItemContainer)character).BaseInventory.Pages.TryGetValue(105, out var value2))
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit because inventory pages are missing char={((IEntity)character).Identity}");
			return true;
		}
		val = message.Source;
		int instance2 = ((Identity)(ref val)).Instance;
		if (!value.ValidSlot(instance2))
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit for invalid source slot char={((IEntity)character).Identity} source={message.Source}");
			return true;
		}
		IItem val2 = value[instance2];
		if (val2 == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit because source slot is empty char={((IEntity)character).Identity} source={message.Source}");
			return true;
		}
		int num = value2.FindFreeSlot();
		if (num < 0)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit because bank is full char={((IEntity)character).Identity} source={message.Source}");
			return true;
		}
		try
		{
			InventoryError val3 = value2.Add(num, val2);
			if ((int)val3 > 0)
			{
				LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit add failed char={((IEntity)character).Identity} source={message.Source} bankSlot={num} error={val3}");
				return true;
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit add threw char={((IEntity)character).Identity} source={message.Source} bankSlot={num} error={ex.Message}");
			return true;
		}
		try
		{
			value.Remove(instance2);
		}
		catch (Exception ex2)
		{
			TryRemoveBankRollback(value2, num);
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientContainerAddItem bank deposit remove source threw char={((IEntity)character).Identity} source={message.Source} bankSlot={num} error={ex2.Message}");
			return true;
		}
		((IDynel)character).Send((MessageBody)new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			SourceContainer = message.Source,
			Target = message.Target,
			TargetPlacement = num
		}, false);
		((IItemContainer)character).BaseInventory.Write();
		LogUtil.Debug((DebugInfoDetail)8, $"Persisted inventory after ClientContainerAddItem bank deposit char={((IEntity)character).Identity} source={message.Source} bankSlot={num}");
		return true;
	}

	public void HandleClientContainerAddItem(IZoneClient client, ClientContainerAddItemMessage message)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ((client != null && client.Controller != null) ? client.Controller.Character : null);
		if (val != null && ((IItemContainer)val).BaseInventory != null && !TryMoveInventoryItemToBackpack(val, message) && !TryDepositInventoryItemToBank(val, message))
		{
			LogUtil.Debug((DebugInfoDetail)4, $"Unhandled ClientContainerAddItem char={((IEntity)val).Identity} source={message.Source} target={message.Target}");
		}
	}

	public void HandleClientMoveItemToInventory(IZoneClient client, ClientMoveItemToInventoryMessage message)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (!TryMoveBackpackItemToInventory(character, message) && !TryMoveOwnedInventoryItem(character, message, client))
		{
			LogUtil.Debug((DebugInfoDetail)4, $"Unhandled ClientMoveItemToInventory source={message.SourceContainer} targetPlacement={message.TargetPlacement} character={((IEntity)character).Identity}");
		}
	}

	public void HandleKnuBotTradeItemRemove(IZoneClient client, KnuBotTradeMessage message)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		IDictionary<int, IInventoryPage> pages = ((IItemContainer)client.Controller.Character).BaseInventory.Pages;
		Identity container = message.Container;
		IInventoryPage obj = pages[(int)((Identity)(ref container)).Type];
		container = message.Container;
		obj.Remove(((Identity)(ref container)).Instance);
	}

	public IItem GetKnuBotTradeItem(ICharacter character, IdentityType container, int slotNumber)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected I4, but got Unknown
		return ((IItemContainer)character).BaseInventory.Pages[(int)container][slotNumber];
	}

	public bool TryGetTradeAddItem(IItemContainer issuer, TradeMessage message, out IItem item)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected I4, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		item = null;
		Identity val;
		try
		{
			if (issuer is Vendor)
			{
				IInventoryPages baseInventory = issuer.BaseInventory;
				val = message.Container;
				item = (IItem)(object)baseInventory.GetItemInContainer(104, ((Identity)(ref val)).Instance);
			}
			else
			{
				IInventoryPages baseInventory2 = issuer.BaseInventory;
				val = message.Container;
				IdentityType type = ((Identity)(ref val)).Type;
				val = message.Container;
				item = (IItem)(object)baseInventory2.GetItemInContainer((int)type, ((Identity)(ref val)).Instance);
			}
		}
		catch (Exception ex)
		{
			string[] obj = new string[6] { "Trade AddItem lookup failed issuer=", null, null, null, null, null };
			val = ((IEntity)issuer).Identity;
			obj[1] = ((Identity)(ref val)).ToString(true);
			obj[2] = " source=";
			val = message.Container;
			obj[3] = ((Identity)(ref val)).ToString(true);
			obj[4] = " error=";
			obj[5] = ex.Message;
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
			return false;
		}
		return item != null;
	}

	public IItem GetVendorTradeItem(IItemContainer issuer, int slot)
	{
		return (IItem)(object)issuer.BaseInventory.GetItemInContainer(104, slot);
	}

	public bool VendorShopNeedsDatabaseEntry(Vendor vendor)
	{
		return ((Dynel)vendor).BaseInventory.Pages[((Dynel)vendor).BaseInventory.StandardPage].List().Count == 0 && string.IsNullOrEmpty(vendor.TemplateHash);
	}

	public IInventoryPage GetVendorStandardInventoryPage(Vendor vendor)
	{
		return ((Dynel)vendor).BaseInventory.Pages[((Dynel)vendor).BaseInventory.StandardPage];
	}

	public void AddVendorPurchaseOffer(TemporaryBag shoppingBag, TradeMessage message, IItem item)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		Identity container = message.Container;
		((Identity)(ref val)).Instance = ((Identity)(ref container)).Instance;
		shoppingBag.Add(val, CloneShopItem(item));
	}

	public void AddVendorSaleOffer(TemporaryBag shoppingBag, TradeMessage message, IItemContainer issuer)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		shoppingBag.Add(message.Target, RemoveInventoryItem(issuer, message.Container));
	}

	public void RemoveVendorPurchaseOffer(TemporaryBag shoppingBag, TradeMessage message)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		Identity container = message.Container;
		((Identity)(ref val)).Instance = ((Identity)(ref container)).Instance;
		Identity val2 = val;
		val = message.Container;
		shoppingBag.Remove(val2, ((Identity)(ref val)).Instance);
	}

	public InventoryItemAddResult TryAddStandardInventoryItem(IItemContainer owner, IItem item)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		int num = FindFreeStandardInventorySlot(owner);
		if (num < 0)
		{
			return InventoryItemAddResult.NoFreeSlot();
		}
		InventoryError val = AddToStandardInventoryPage(owner, num, item);
		return ((int)val == 0) ? InventoryItemAddResult.Success(num) : InventoryItemAddResult.Failed(num, val);
	}

	public void ReturnItemsToStandardInventoryUnchecked(IItemContainer owner, IEnumerable<IItem> items)
	{
		foreach (IItem item in items)
		{
			int num = FindFreeStandardInventorySlot(owner);
			if (num != -1)
			{
				AddToStandardInventoryPageUnchecked(owner, num, item);
			}
		}
	}

	public Item GetTradeSkillItem(ICharacter character, TradeSkillInfo info)
	{
		return ((IItemContainer)character).BaseInventory.GetItemInContainer(info.Container, info.Placement);
	}

	public InventoryError AddTradeSkillResultItem(ICharacter character, Item item)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		return ((IItemContainer)character).BaseInventory.TryAdd((IItem)(object)item);
	}

	public void RemoveTradeSkillItem(ICharacter character, TradeSkillInfo info)
	{
		((IItemContainer)character).BaseInventory.RemoveItem(info.Container, info.Placement);
	}

	public Item SetTradeSkillSource(ICharacter character, int container, int placement)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		character.TradeSkillSource = new TradeSkillInfo(0, container, placement);
		return ((IItemContainer)character).BaseInventory.GetItemInContainer(container, placement);
	}

	public Item SetTradeSkillTarget(ICharacter character, int container, int placement)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		character.TradeSkillTarget = new TradeSkillInfo(0, container, placement);
		return ((IItemContainer)character).BaseInventory.GetItemInContainer(container, placement);
	}

	public void ClearTradeSkillSource(ICharacter character)
	{
		character.TradeSkillSource = null;
	}

	public void ClearTradeSkillTarget(ICharacter character)
	{
		character.TradeSkillTarget = null;
	}

	public bool HasInventoryPage(IItemContainer owner, Identity container)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected I4, but got Unknown
		return owner.BaseInventory.Pages.ContainsKey((int)((Identity)(ref container)).Type);
	}

	public IItem RemoveInventoryItem(IItemContainer owner, Identity container)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		return owner.BaseInventory.RemoveItem((int)((Identity)(ref container)).Type, ((Identity)(ref container)).Instance);
	}

	public InventoryError RestoreInventoryItem(IItemContainer owner, Identity container, IItem item)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected I4, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		return owner.BaseInventory.AddToPage((int)((Identity)(ref container)).Type, ((Identity)(ref container)).Instance, item);
	}

	public void MoveNonEquipmentContainerItem(ICharacter character, ContainerAddItemMessage message, IInventoryPage sendingPage, IInventoryPage receivingPage, int fromPlacement)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		message.TargetPlacement = receivingPage.FindFreeSlot();
		IItem val = sendingPage.Remove(fromPlacement);
		receivingPage.Add(message.TargetPlacement, val);
		((IDynel)character).Send((MessageBody)(object)message, false);
	}

	public bool MovePlayerControllerContainerItem(ICharacter character, int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (((IItemContainer)character).BaseInventory.Pages.ContainsKey(sourceContainerType))
		{
			IInventoryPage val = ((IItemContainer)character).BaseInventory.Pages[sourceContainerType];
			if (val[sourcePlacement] != null && ((IEntity)character).Identity == target)
			{
				IInventoryPage val2 = ((IItemContainer)character).BaseInventory.PageFromSlot(targetPlacement);
				if (val2 != null)
				{
					IItem val3 = val.Remove(sourcePlacement);
					IItem val4 = val2.Remove(targetPlacement);
					if (val4 != null)
					{
						val.Add(sourcePlacement, val4);
					}
					if (val3 != null)
					{
						val2.Add(targetPlacement, val3);
					}
				}
			}
		}
		return true;
	}

	public bool DeletePlayerControllerContainerItem(ICharacter character, int container, int slotNumber)
	{
		if (((IItemContainer)character).BaseInventory.Pages.ContainsKey(container))
		{
			((IItemContainer)character).BaseInventory.Pages[container].Remove(slotNumber);
		}
		return true;
	}

	public bool TryUseBackpackContainer(ICharacter character, Identity itemPosition)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected I4, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		Item val = null;
		try
		{
			val = ((IItemContainer)character).BaseInventory.GetItemInContainer((int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		}
		catch (Exception)
		{
		}
		return val != null && TryOpenBackpackContainer(character, itemPosition, val);
	}

	public bool TryOpenBackpackContainer(ICharacter character, Identity itemPosition, Item item)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		if (!IsBackpackUseSlot(((Identity)(ref itemPosition)).Type))
		{
			return false;
		}
		if (!TryResolveBackpackContainerIdentity(character, itemPosition, item, out var containerIdentity))
		{
			return false;
		}
		if (!IsItemUsable(item))
		{
			return false;
		}
		if (((IItemContainer)character).BaseInventory.IsBackpackOpen(containerIdentity))
		{
			BaseMessageHandler<ActionMessage, BackpackContainerActionMessageHandler>.Default.SendClose(character, containerIdentity);
			((IItemContainer)character).BaseInventory.MarkBackpackClosed(containerIdentity);
			return true;
		}
		IInventoryPage val = default(IInventoryPage);
		if (((IItemContainer)character).BaseInventory.TryGetBackpackPage(containerIdentity, ref val))
		{
			BaseMessageHandler<ActionMessage, BackpackContainerActionMessageHandler>.Default.SendOpen(character, containerIdentity);
			((IItemContainer)character).BaseInventory.MarkBackpackOpen(containerIdentity);
		}
		else
		{
			val = ((IItemContainer)character).BaseInventory.GetOrCreateBackpackPage(containerIdentity);
			if (val.List().Any())
			{
				int handle = BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.ReserveBackpackInventoryHandle();
				BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>.Default.Send(character, item, itemPosition, ((IEntity)val).Identity);
				BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendContainerOpen(character, val, handle);
			}
			else
			{
				int handle2 = BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.ReserveBackpackInventoryHandle();
				int handle3 = BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.ReserveBackpackInventoryHandle();
				BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendContainerIntroduce(character, val, handle2);
				BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>.Default.Send(character, item, itemPosition, ((IEntity)val).Identity);
				BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>.Default.SendFreshContainerOpen(character, val, handle3);
			}
			((IItemContainer)character).BaseInventory.MarkBackpackOpen(containerIdentity);
		}
		return true;
	}

	public void RegisterBackpackInventoryHandle(ICharacter character, IInventoryPage page, int handle)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		if (character != null && ((IItemContainer)character).BaseInventory != null && page != null)
		{
			_ = ((IEntity)page).Identity;
			Identity identity = ((IEntity)page).Identity;
			if ((int)((Identity)(ref identity)).Type == 51017)
			{
				((IItemContainer)character).BaseInventory.RegisterBackpackHandle(handle, ((IEntity)page).Identity);
			}
		}
	}

	public bool UseInventoryItem(ICharacter character, Identity itemPosition)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected I4, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected I4, but got Unknown
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Expected I4, but got Unknown
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Expected I4, but got Unknown
		Item val = null;
		try
		{
			val = ((IItemContainer)character).BaseInventory.GetItemInContainer((int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		}
		catch (Exception)
		{
		}
		if (val == null)
		{
			Identity val2 = itemPosition;
			throw new NullReferenceException("No item found at " + ((object)(Identity)(ref val2)).ToString());
		}
		if (TryOpenBackpackContainer(character, itemPosition, val))
		{
			return true;
		}
		if (IsUseBlockedBySkillLock(character, val))
		{
			return false;
		}
		if (PetShellItemService.Default.TryUsePetShell(character, itemPosition, val))
		{
			return true;
		}
		if (PetShellItemService.IsPetShellItem(val))
		{
			return true;
		}
		if (TryUseHealthAndNanoRecharger(character, itemPosition, val))
		{
			return true;
		}
		if (TryUseHealthAndNanoStim(character, itemPosition, val))
		{
			return true;
		}
		BaseMessageHandler<TemplateActionMessage, TemplateActionMessageHandler>.Default.Send(character, val, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		if (!ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val.LowID, val.HighID) && ItemLoader.ItemList[val.HighID].IsConsumable() && !IsHealthAndNanoRecharger(val))
		{
			Item obj = val;
			int multipleCount = obj.MultipleCount;
			obj.MultipleCount = multipleCount - 1;
			if (val.MultipleCount <= 0)
			{
				((IItemContainer)character).BaseInventory.RemoveItem((int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
			}
		}
		val.PerformAction(character, (EventType)0, ((Identity)(ref itemPosition)).Instance);
		return true;
	}

	private bool TryUseHealthAndNanoStim(ICharacter character, Identity itemPosition, Item item)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected I4, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		if (!IsHealthAndNanoStim(item))
		{
			return false;
		}
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			return false;
		}
		BaseMessageHandler<TemplateActionMessage, TemplateActionMessageHandler>.Default.Send(character, item, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		int lockStatId = 123;
		int lockDurationSeconds = 40;
		ResolveVitalItemEffects(item, 123, 40, ResolveHealthAndNanoStimAmount(item.Quality), out var healthAmount, out var nanoAmount, out lockStatId, out lockDurationSeconds);
		ApplyVitalRestore(val, healthAmount, nanoAmount);
		FunctionCollection.Instance.CallFunction(53033, (INamedEntity)(object)val, (IEntity)(object)val, (IInstancedEntity)(object)val, (MessagePackObject[])(object)new MessagePackObject[2]
		{
			MessagePackObject.op_Implicit(lockStatId),
			MessagePackObject.op_Implicit(lockDurationSeconds)
		});
		ConsumeInventoryStackItem(character, itemPosition, item);
		object[] array = new object[8];
		Identity identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = item.LowID;
		array[2] = item.HighID;
		array[3] = item.Quality;
		array[4] = healthAmount;
		array[5] = nanoAmount;
		array[6] = lockStatId;
		array[7] = lockDurationSeconds;
		LogUtil.Debug((DebugInfoDetail)512, string.Format("HealthAndNanoStim used char={0} item={1}/{2} ql={3} heal={4} nano={5} lockStat={6} lockSecs={7}", array));
		return true;
	}

	private bool TryUseHealthAndNanoRecharger(ICharacter character, Identity itemPosition, Item item)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected I4, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		if (!IsHealthAndNanoRecharger(item))
		{
			return false;
		}
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			return false;
		}
		BaseMessageHandler<TemplateActionMessage, TemplateActionMessageHandler>.Default.Send(character, item, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		int lockStatId = 124;
		int lockDurationSeconds = 15;
		ResolveVitalItemEffects(item, 124, 15, ResolveHealthAndNanoRechargerAmount(item.Quality), out var healthAmount, out var nanoAmount, out lockStatId, out lockDurationSeconds);
		ApplyVitalRestore(val, healthAmount, nanoAmount);
		FunctionCollection.Instance.CallFunction(53033, (INamedEntity)(object)val, (IEntity)(object)val, (IInstancedEntity)(object)val, (MessagePackObject[])(object)new MessagePackObject[2]
		{
			MessagePackObject.op_Implicit(lockStatId),
			MessagePackObject.op_Implicit(lockDurationSeconds)
		});
		object[] array = new object[8];
		Identity identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = item.LowID;
		array[2] = item.HighID;
		array[3] = item.Quality;
		array[4] = healthAmount;
		array[5] = nanoAmount;
		array[6] = lockStatId;
		array[7] = lockDurationSeconds;
		LogUtil.Debug((DebugInfoDetail)512, string.Format("HealthAndNanoRecharger used char={0} item={1}/{2} ql={3} heal={4} nano={5} lockStat={6} lockSecs={7}", array));
		return true;
	}

	private void ResolveVitalItemEffects(Item item, int defaultLockStatId, int defaultLockDurationSeconds, int fallbackAmount, out int healthAmount, out int nanoAmount, out int lockStatId, out int lockDurationSeconds)
	{
		healthAmount = 0;
		nanoAmount = 0;
		lockStatId = defaultLockStatId;
		lockDurationSeconds = defaultLockDurationSeconds;
		foreach (Event item2 in item.Events.Where((Event x) => (int)x.EventType == 0))
		{
			foreach (Function function in item2.Functions)
			{
				MessagePackObject[] array = function.Arguments.Values.ToArray();
				int statId;
				int durationSeconds;
				if (function.FunctionType == 53002 && array.Length >= 2)
				{
					int num = ((MessagePackObject)(ref array[0])).AsInt32();
					int val = Math.Abs(hit.ResolveHitDelta(array));
					if (num == 27 || num == 1)
					{
						healthAmount = Math.Max(healthAmount, val);
					}
					else if (num == 214 || num == 132 || num == 221)
					{
						nanoAmount = Math.Max(nanoAmount, val);
					}
				}
				else if (function.FunctionType == 53033 && lockskill.TryReadArguments(array, out statId, out durationSeconds))
				{
					lockStatId = statId;
					lockDurationSeconds = durationSeconds;
				}
			}
		}
		if (healthAmount <= 0)
		{
			healthAmount = fallbackAmount;
		}
		if (nanoAmount <= 0)
		{
			nanoAmount = fallbackAmount;
		}
	}

	private void ApplyVitalRestore(Character character, int healthAmount, int nanoAmount)
	{
		int num = Math.Max(1, ((Dynel)character).Stats[(StatIds)1].Value);
		int value = ((Dynel)character).Stats[(StatIds)27].Value;
		int num2 = Math.Min(Math.Max(0, healthAmount), Math.Max(0, num - value));
		if (num2 > 0)
		{
			int num3 = value + num2;
			((Dynel)character).Stats[(StatIds)27].Value = num3;
			((Dynel)character).Stats[(StatIds)27].BaseValue = (uint)num3;
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle((ICharacter)(object)character, 27, (uint)num3);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send((ICharacter)(object)character, $"You healed yourself for {num2} points.", 0, 0);
		}
		int num4 = Math.Max(0, ((Dynel)character).Stats[(StatIds)221].Value);
		int value2 = ((Dynel)character).Stats[(StatIds)214].Value;
		int num5 = Math.Min(Math.Max(0, nanoAmount), Math.Max(0, num4 - value2));
		if (num5 > 0)
		{
			int num6 = value2 + num5;
			((Dynel)character).Stats[(StatIds)214].Value = num6;
			((Dynel)character).Stats[(StatIds)214].BaseValue = (uint)num6;
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle((ICharacter)(object)character, 214, (uint)num6);
		}
		if (((Dynel)character).Controller != null)
		{
			((Dynel)character).Controller.SendChangedStats();
		}
	}

	private static int ResolveHealthAndNanoRechargerAmount(int quality)
	{
		int num = Math.Max(1, Math.Min(100, quality));
		if (num <= 1)
		{
			return 200;
		}
		return 200 + 4800 * (num - 1) / 99;
	}

	private static int ResolveHealthAndNanoStimAmount(int quality)
	{
		int num = Math.Max(1, Math.Min(200, quality));
		if (num <= 1)
		{
			return 30;
		}
		return 30 + 2370 * (num - 1) / 199;
	}

	private bool IsHealthAndNanoRecharger(Item item)
	{
		return item.LowID == 291082 || item.HighID == 291082 || item.LowID == 291083 || item.HighID == 291083;
	}

	private bool IsHealthAndNanoStim(Item item)
	{
		return item.LowID == 291043 || item.HighID == 291043 || item.LowID == 291044 || item.HighID == 291044;
	}

	private void ConsumeInventoryStackItem(ICharacter character, Identity itemPosition, Item item)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected I4, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected I4, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected I4, but got Unknown
		int multipleCount = item.MultipleCount;
		item.MultipleCount = multipleCount - 1;
		IInventoryPage value;
		if (item.MultipleCount <= 0)
		{
			((IItemContainer)character).BaseInventory.RemoveItem((int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		}
		else if (((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref itemPosition)).Type, out value))
		{
			value.Write();
		}
	}

	public bool DeleteInventoryItemAction(ICharacter character, CharacterActionMessage message)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected I4, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected I4, but got Unknown
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Expected I4, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && message != null)
		{
			_ = message.Target;
			if (0 == 0)
			{
				IItem val = null;
				Identity val2;
				try
				{
					if (((IItemContainer)character).BaseInventory != null)
					{
						IDictionary<int, IInventoryPage> pages = ((IItemContainer)character).BaseInventory.Pages;
						val2 = message.Target;
						if (pages.TryGetValue((int)((Identity)(ref val2)).Type, out var value) && value != null)
						{
							IInventoryPage obj = value;
							val2 = message.Target;
							val = obj[((Identity)(ref val2)).Instance];
						}
					}
				}
				catch (Exception)
				{
				}
				if (val != null && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val.LowID, val.HighID))
				{
					return false;
				}
				if (ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(message.Parameter1, message.Parameter2))
				{
					return false;
				}
				ItemDao instance = Dao<DBItem, ItemDao>.Instance;
				val2 = message.Target;
				IdentityType type = ((Identity)(ref val2)).Type;
				val2 = ((IEntity)character).Identity;
				int instance2 = ((Identity)(ref val2)).Instance;
				val2 = message.Target;
				((Dao<DBItem, ItemDao>)(object)instance).Delete((object)new
				{
					containertype = (int)type,
					containerinstance = instance2,
					Id = ((Identity)(ref val2)).Instance
				}, (IDbConnection)null, (IDbTransaction)null);
				IInventoryPages baseInventory = ((IItemContainer)character).BaseInventory;
				val2 = message.Target;
				IdentityType type2 = ((Identity)(ref val2)).Type;
				val2 = message.Target;
				baseInventory.RemoveItem((int)type2, ((Identity)(ref val2)).Instance);
				return true;
			}
		}
		return false;
	}

	public void SplitInventoryItemStackAction(ICharacter character, CharacterActionMessage message)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected I4, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected I4, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected I4, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Expected I4, but got Unknown
		IDictionary<int, IInventoryPage> pages = ((IItemContainer)character).BaseInventory.Pages;
		Identity target = message.Target;
		IInventoryPage obj = pages[(int)((Identity)(ref target)).Type];
		target = message.Target;
		IItem val = obj[((Identity)(ref target)).Instance];
		val.MultipleCount -= message.Parameter2;
		Item val2 = new Item(val.Quality, val.LowID, val.HighID);
		val2.MultipleCount = message.Parameter2;
		IDictionary<int, IInventoryPage> pages2 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		IInventoryPage obj2 = pages2[(int)((Identity)(ref target)).Type];
		IDictionary<int, IInventoryPage> pages3 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		obj2.Add(pages3[(int)((Identity)(ref target)).Type].FindFreeSlot(), (IItem)(object)val2);
		IDictionary<int, IInventoryPage> pages4 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		pages4[(int)((Identity)(ref target)).Type].Write();
	}

	public void MergeInventoryItemStackAction(ICharacter character, CharacterActionMessage message)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected I4, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected I4, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected I4, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected I4, but got Unknown
		IDictionary<int, IInventoryPage> pages = ((IItemContainer)character).BaseInventory.Pages;
		Identity target = message.Target;
		IInventoryPage obj = pages[(int)((Identity)(ref target)).Type];
		target = message.Target;
		IItem obj2 = obj[((Identity)(ref target)).Instance];
		int multipleCount = obj2.MultipleCount;
		IDictionary<int, IInventoryPage> pages2 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		obj2.MultipleCount = multipleCount + pages2[(int)((Identity)(ref target)).Type][message.Parameter2].MultipleCount;
		IDictionary<int, IInventoryPage> pages3 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		pages3[(int)((Identity)(ref target)).Type].Remove(message.Parameter2);
		IDictionary<int, IInventoryPage> pages4 = ((IItemContainer)character).BaseInventory.Pages;
		target = message.Target;
		pages4[(int)((Identity)(ref target)).Type].Write();
	}

	public bool TryRejectInventoryPageAccess(ICharacter character, IInventoryPage page)
	{
		if (RequiresImplantAccess(page) && !HasImplantAccess(character))
		{
			SendImplantAccessDenied(character);
			return true;
		}
		return false;
	}

	public bool CanMoveContainerItemToPage(ICharacter character, IInventoryPage page, IItem item)
	{
		AOAction val = ResolveContainerAddItemAction(page, item);
		return val.CheckRequirements((IInstancedEntity)(object)character);
	}

	public bool ShouldSkipContainerAppearanceUpdate(IInventoryPage receivingPage, IInventoryPage sendingPage)
	{
		return !IsAppearanceEquipmentPage(receivingPage) && !IsAppearanceEquipmentPage(sendingPage);
	}

	public void WaitForContainerHotSwapVisualSync(IItem itemFrom, IItem itemTo, bool skipAppearanceUpdate)
	{
		int num = 20;
		if (!skipAppearanceUpdate)
		{
			num = GetEquipDelay(itemFrom, isSocial: false) + GetEquipDelay(itemTo, isSocial: false);
		}
		Thread.Sleep(num * 10);
	}

	public void WaitForContainerEquipVisualSync(IItem item, IInventoryPage equipmentPage, bool skipAppearanceUpdate)
	{
		if (!skipAppearanceUpdate)
		{
			Thread.Sleep(GetEquipDelay(item, equipmentPage is SocialArmorInventoryPage) * 10);
		}
	}

	public void HandleContainerAddItem(IZoneClient client, ContainerAddItemMessage message)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected I4, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		Pool instance = Pool.Instance;
		Identity identity = ((N3Message)message).Identity;
		Identity val = default(Identity);
		Identity val2 = ((N3Message)message).Identity;
		((Identity)(ref val)).Type = (IdentityType)((Identity)(ref val2)).Instance;
		val2 = message.SourceContainer;
		((Identity)(ref val)).Instance = (int)((Identity)(ref val2)).Type;
		IInventoryPage @object = instance.GetObject<IInventoryPage>(identity, val);
		val = message.SourceContainer;
		int instance2 = ((Identity)(ref val)).Instance;
		Identity target = message.Target;
		int targetPlacement = message.TargetPlacement;
		IItem val3 = @object[instance2];
		target = ResolveContainerAddItemTargetIdentity(target);
		IInstancedEntity obj = ((IInstancedEntity)character).Playfield.FindByIdentity(target);
		IItemContainer val4 = (IItemContainer)(object)((obj is IItemContainer) ? obj : null);
		if (val4 == null)
		{
			val = message.Target;
			IdentityType type = ((Identity)(ref val)).Type;
			string text = ((object)(IdentityType)(ref type)).ToString();
			val = message.Target;
			throw new ArgumentOutOfRangeException("No Entity found: " + text + ":" + ((Identity)(ref val)).Instance);
		}
		IInventoryPage val5 = ResolveContainerAddItemReceivingPage(val4, character, message.Target, targetPlacement);
		if (val5 == null)
		{
			throw new ArgumentOutOfRangeException("No inventorypage found.");
		}
		targetPlacement = ResolveContainerAddItemTargetPlacement(val5, targetPlacement);
		IItem val6;
		try
		{
			val6 = val5[targetPlacement];
		}
		catch (Exception)
		{
			val6 = null;
		}
		((IInstancedEntity)character).DoNotDoTimers = true;
		IItemSlotHandler val7 = (IItemSlotHandler)(object)((val5 is IItemSlotHandler) ? val5 : null);
		IItemSlotHandler val8 = (IItemSlotHandler)(object)((@object is IItemSlotHandler) ? @object : null);
		bool skipAppearanceUpdate = ShouldSkipContainerAppearanceUpdate(val5, @object);
		if (val7 != null)
		{
			if (TryRejectInventoryPageAccess(character, val5))
			{
				((IInstancedEntity)character).DoNotDoTimers = false;
				return;
			}
			if (val6 != null)
			{
				if (val5.NeedsItemCheck && CanMoveContainerItemToPage(character, @object, val3))
				{
					UnEquip.Send(client, val5, targetPlacement);
					WaitForContainerHotSwapVisualSync(val3, val6, skipAppearanceUpdate);
					((IDynel)character).Send((MessageBody)(object)message, false);
					val7.HotSwap(@object, instance2, targetPlacement);
					Equip.Send(client, val5, targetPlacement);
				}
			}
			else if (val5.NeedsItemCheck)
			{
				if (val3 == null)
				{
					throw new NullReferenceException("itemFrom can not be null, possible inventory error");
				}
				if (CanMoveContainerItemToPage(character, val5, val3))
				{
					WaitForContainerEquipVisualSync(val3, val5, skipAppearanceUpdate);
					if (@object == val5)
					{
						UnEquip.Send(client, @object, instance2);
					}
					((IDynel)character).Send((MessageBody)(object)message, false);
					val7.Equip(@object, instance2, targetPlacement);
					Equip.Send(client, val5, targetPlacement);
				}
			}
		}
		else if (val8 != null)
		{
			if (TryRejectInventoryPageAccess(character, @object))
			{
				((IInstancedEntity)character).DoNotDoTimers = false;
				return;
			}
			WaitForContainerEquipVisualSync(val3, @object, skipAppearanceUpdate);
			UnEquip.Send(client, @object, instance2);
			val8.Unequip(instance2, val5, targetPlacement);
			((IDynel)character).Send((MessageBody)(object)message, false);
		}
		else
		{
			MoveNonEquipmentContainerItem(character, message, @object, val5, instance2);
		}
		((IInstancedEntity)character).DoNotDoTimers = false;
		character.CalculateSkills();
	}

	public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
		{
		case InventoryContainerInteractionRouteMode.InventoryItem:
			if (UseInventoryItem(client.Controller.Character, target))
			{
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
			}
			else
			{
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(client.Controller.Character, message);
			}
			return true;
		case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
			if (TryUseBackpackContainer(client.Controller.Character, target))
			{
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
			}
			return true;
		case InventoryContainerInteractionRouteMode.BackpackContainer:
		{
			IInventoryPage val = default(IInventoryPage);
			if (((IItemContainer)client.Controller.Character).BaseInventory.TryGetBackpackPage(target, ref val))
			{
				BaseMessageHandler<ActionMessage, BackpackContainerActionMessageHandler>.Default.SendClose(client.Controller.Character, target);
				((IItemContainer)client.Controller.Character).BaseInventory.MarkBackpackClosed(target);
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
			}
			return true;
		}
		default:
			return false;
		}
	}

	public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Invalid comparison between Unknown and I4
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected I4, but got Unknown
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Invalid comparison between Unknown and I4
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected I4, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_029e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action) != UseItemOnItemInteractionRouteMode.UseItemOnItem)
		{
			return false;
		}
		if (message.Target == null || message.Target.Length < 2)
		{
			return false;
		}
		IInventoryPage value = null;
		ICharacter character = client.Controller.Character;
		Identity val;
		if (((IItemContainer)character).BaseInventory == null || !((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref message.Target[0])).Type, out value) || value == null)
		{
			Pool instance = Pool.Instance;
			val = default(Identity);
			Identity identity = ((IEntity)character).Identity;
			((Identity)(ref val)).Type = (IdentityType)((Identity)(ref identity)).Instance;
			((Identity)(ref val)).Instance = (int)((Identity)(ref message.Target[0])).Type;
			value = instance.GetObject<IInventoryPage>(val);
		}
		if (value == null)
		{
			return false;
		}
		IItem val2 = value[((Identity)(ref message.Target[0])).Instance];
		if (val2 == null)
		{
			return false;
		}
		((IStats)character).Stats[(StatIds)273].Value = val2.LowID;
		((IInstancedEntity)character).DoNotDoTimers = false;
		try
		{
			((IStats)character).Stats[(StatIds)389].Value = ((IStats)character).Stats[(StatIds)389].Value | 2;
		}
		catch
		{
		}
		if (Pool.Instance.Contains(message.Target[1]))
		{
			StaticDynel @object = Pool.Instance.GetObject<StaticDynel>(((IEntity)((IInstancedEntity)character).Playfield).Identity, message.Target[1]);
			if (@object == null)
			{
				return false;
			}
			Event val3 = @object.Events.FirstOrDefault((Event x) => (int)x.EventType == 4);
			if (val3 == null)
			{
				return false;
			}
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			int instance2 = ((Identity)(ref val)).Instance;
			if (NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(instance2) && (int)((Identity)(ref message.Target[1])).Type == 51005 && !ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val2.LowID, val2.HighID))
			{
				Item val4 = (Item)(object)((val2 is Item) ? val2 : null);
				if (val4 != null)
				{
					int num = ((@object.Template != null) ? @object.Template.ID : 0);
					ConsumeInventoryStackItem(character, message.Target[0], val4);
					((IClient)client).Server.Info((IClient)(object)client, "Shadowlands statue insignia consumed char={0} item={1} statue={2} slot={3}", new object[4]
					{
						((IEntity)character).Identity,
						val2.LowID,
						num,
						message.Target[0]
					});
				}
			}
			val3.Perform(character, (IEntity)(object)@object);
			return true;
		}
		if (((IInstancedEntity)character).Playfield != null)
		{
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			if (NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(((Identity)(ref val)).Instance) && (int)((Identity)(ref message.Target[1])).Type == 51005)
			{
				return false;
			}
		}
		client.Controller.UseStatel(message.Target[1], (EventType)4);
		return true;
	}

	public bool TryMoveOwnedInventoryItem(ICharacter character, ClientMoveItemToInventoryMessage message, IZoneClient client)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Invalid comparison between Unknown and I4
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0525: Unknown result type (might be due to invalid IL or missing references)
		//IL_052e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_037c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		if (!TryResolveMoveSourcePage(character, message.SourceContainer, out var sendingPage))
		{
			return false;
		}
		Identity sourceContainer = message.SourceContainer;
		int instance = ((Identity)(ref sourceContainer)).Instance;
		IItem val = sendingPage[instance];
		if (val == null)
		{
			if (sendingPage is WeaponInventoryPage || IsAppearanceEquipmentPage(sendingPage))
			{
				IInventoryPage val2 = ResolveMoveTargetPage(character, message.TargetPlacement);
				int targetPlacement = message.TargetPlacement;
				if (val2 != null)
				{
					int num = ResolveConcreteTargetSlot(val2, message.TargetPlacement);
					if (num >= 0)
					{
						targetPlacement = num;
					}
				}
				UnEquip.Send(client, sendingPage, instance);
				SendMoveItemToInventoryAck(character, message.SourceContainer, targetPlacement);
				LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory cleared phantom equip source={message.SourceContainer} targetPlacement={message.TargetPlacement} character={((IEntity)character).Identity}");
				return true;
			}
			LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory source slot is empty source={message.SourceContainer} targetPlacement={message.TargetPlacement} character={((IEntity)character).Identity}");
			return true;
		}
		sourceContainer = message.SourceContainer;
		if ((int)((Identity)(ref sourceContainer)).Type == 104)
		{
			Identity val3 = default(Identity);
			InventoryItemRules.TryEnsureBackpackContainerIdentity(val, ((IEntity)character).Identity, message.SourceContainer, ref val3);
		}
		IInventoryPage val4 = ResolveMoveTargetPage(character, message.TargetPlacement);
		if (val4 == null)
		{
			return false;
		}
		LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory resolved char={((IEntity)character).Identity} fromPage={((object)sendingPage).GetType().Name} fromSlot={instance} toPage={((object)val4).GetType().Name} rawTarget={message.TargetPlacement} item={val.LowID}/{val.HighID} ql={val.Quality}");
		int num2 = ResolveConcreteTargetSlot(val4, message.TargetPlacement);
		int targetPlacement2 = ((num2 >= 0) ? num2 : message.TargetPlacement);
		if (num2 < 0)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory target inventory is full source={message.SourceContainer} targetPlacement={message.TargetPlacement} character={((IEntity)character).Identity}");
			return true;
		}
		IItemSlotHandler val5 = (IItemSlotHandler)(object)((val4 is IItemSlotHandler) ? val4 : null);
		IItemSlotHandler val6 = (IItemSlotHandler)(object)((sendingPage is IItemSlotHandler) ? sendingPage : null);
		IItem val7 = val4[num2];
		bool flag = IsAppearanceEquipmentPage(val4) || IsAppearanceEquipmentPage(sendingPage);
		if (val5 != null)
		{
			if (RequiresImplantAccess(val4) && !HasImplantAccess(character))
			{
				SendImplantAccessDenied(character);
				return true;
			}
			LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory equip path char={((IEntity)character).Identity} targetSlot={num2} itemToPresent={((val7 != null) ? 1 : 0)}");
			if (val4.NeedsItemCheck && !CanEquipToPage(character, val4, val))
			{
				LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory equip requirements failed item={val.LowID}/{val.HighID}:{val.Quality} source={message.SourceContainer} targetPlacement={num2} character={((IEntity)character).Identity}");
				return true;
			}
			WeaponItemFullUpdate.SendWeaponDefinition(character, val);
			if (val7 != null)
			{
				if (flag)
				{
					WaitForEquipVisualSync(val, val7, val4 is SocialArmorInventoryPage);
				}
				UnEquip.Send(client, val4, num2);
				val5.HotSwap(sendingPage, instance, num2);
			}
			else
			{
				if (flag)
				{
					WaitForEquipVisualSync(val, null, val4 is SocialArmorInventoryPage);
				}
				if (sendingPage == val4)
				{
					UnEquip.Send(client, sendingPage, instance);
				}
				val5.Equip(sendingPage, instance, num2);
			}
			SendMoveItemToInventoryAck(character, message.SourceContainer, targetPlacement2);
			Equip.Send(client, val4, num2);
			character.CalculateSkills();
			EnsureWeaponVisualMeshes(character, announceAppearanceUpdate: true);
			PersistClientMoveItemToInventory(character, "equip");
			return true;
		}
		if (val6 != null)
		{
			if (RequiresImplantAccess(sendingPage) && !HasImplantAccess(character))
			{
				SendImplantAccessDenied(character);
				return true;
			}
			if (flag)
			{
				WaitForEquipVisualSync(val, null, sendingPage is SocialArmorInventoryPage);
			}
			UnEquip.Send(client, sendingPage, instance);
			val6.Unequip(instance, val4, num2);
			SendMoveItemToInventoryAck(character, message.SourceContainer, targetPlacement2);
			character.CalculateSkills();
			EnsureWeaponVisualMeshes(character, announceAppearanceUpdate: true);
			PersistClientMoveItemToInventory(character, "unequip");
			return true;
		}
		sendingPage.Remove(instance);
		val4.Add(num2, val);
		SendMoveItemToInventoryAck(character, message.SourceContainer, targetPlacement2);
		PersistClientMoveItemToInventory(character, "move");
		return true;
	}

	public bool TryMoveBackpackItemToInventory(ICharacter character, ClientMoveItemToInventoryMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Invalid comparison between Unknown and I4
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		Identity sourceContainer = message.SourceContainer;
		if ((int)((Identity)(ref sourceContainer)).Type != 107)
		{
			return false;
		}
		int num = DecodeBackpackHandle(message.SourceContainer);
		int num2 = DecodeBackpackSlot(message.SourceContainer);
		IInventoryPage val = default(IInventoryPage);
		if (!((IItemContainer)character).BaseInventory.TryGetBackpackPageByHandle(num, ref val))
		{
			LogUtil.Debug((DebugInfoDetail)4, $"Rejected ClientMoveItemToInventory backpack move because handle is unknown char={((IEntity)character).Identity} source={message.SourceContainer} handle={num} targetPlacement={message.TargetPlacement}");
			return true;
		}
		IItem val2 = val[num2];
		if (val2 == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientMoveItemToInventory backpack move because source slot is empty char={((IEntity)character).Identity} source={message.SourceContainer} slot={num2} targetPlacement={message.TargetPlacement}");
			return true;
		}
		IInventoryPage val3 = ResolveMoveTargetPage(character, message.TargetPlacement);
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(104, out var value) || val3 == null || val3 != value)
		{
			LogUtil.Debug((DebugInfoDetail)4, $"Rejected ClientMoveItemToInventory backpack move for non-inventory target char={((IEntity)character).Identity} source={message.SourceContainer} targetPlacement={message.TargetPlacement}");
			return true;
		}
		int num3 = ResolveConcreteTargetSlot(val3, message.TargetPlacement);
		if (num3 < 0)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientMoveItemToInventory backpack move because inventory is full char={((IEntity)character).Identity} source={message.SourceContainer} targetPlacement={message.TargetPlacement}");
			return true;
		}
		try
		{
			InventoryError val4 = val3.Add(num3, val2);
			if ((int)val4 > 0)
			{
				LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientMoveItemToInventory backpack move add failed char={((IEntity)character).Identity} source={message.SourceContainer} targetPlacement={message.TargetPlacement} resolvedTarget={num3} error={val4}");
				return true;
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientMoveItemToInventory backpack move add threw char={((IEntity)character).Identity} source={message.SourceContainer} targetPlacement={message.TargetPlacement} resolvedTarget={num3} error={ex.Message}");
			return true;
		}
		try
		{
			val.Remove(num2);
		}
		catch (Exception ex2)
		{
			TryRemoveInventoryRollback(val3, num3);
			LogUtil.Debug((DebugInfoDetail)512, $"Rejected ClientMoveItemToInventory backpack move remove source threw char={((IEntity)character).Identity} source={message.SourceContainer} slot={num2} targetPlacement={message.TargetPlacement} error={ex2.Message}");
			return true;
		}
		SendMoveItemToInventoryAck(character, message.SourceContainer, message.TargetPlacement);
		PersistClientMoveItemToInventory(character, "backpack move");
		return true;
	}

	public bool TryResolveMoveSourcePage(ICharacter character, Identity sourceContainer, out IInventoryPage sendingPage)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected I4, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected I4, but got Unknown
		sendingPage = null;
		if (((IItemContainer)character).BaseInventory.Pages.ContainsKey((int)((Identity)(ref sourceContainer)).Type))
		{
			sendingPage = ((IItemContainer)character).BaseInventory.Pages[(int)((Identity)(ref sourceContainer)).Type];
			return true;
		}
		try
		{
			sendingPage = ((IItemContainer)character).BaseInventory.PageFromSlot(((Identity)(ref sourceContainer)).Instance);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public IInventoryPage ResolveMoveTargetPage(ICharacter character, int targetPlacement)
	{
		if (targetPlacement == 111 || targetPlacement == 104 || targetPlacement == 110 || targetPlacement == 111)
		{
			return ((IItemContainer)character).BaseInventory.Pages[((IItemContainer)character).BaseInventory.StandardPage];
		}
		try
		{
			return ((IItemContainer)character).BaseInventory.PageFromSlot(targetPlacement);
		}
		catch (Exception)
		{
			if (targetPlacement > 94 && ((IItemContainer)character).BaseInventory.Pages.TryGetValue(104, out var value))
			{
				return value;
			}
			return null;
		}
	}

	private int ResolveConcreteTargetSlot(IInventoryPage receivingPage, int requestedPlacement)
	{
		if (receivingPage == null)
		{
			return -1;
		}
		if (requestedPlacement != 111 && requestedPlacement != 104 && requestedPlacement != 110 && requestedPlacement != 111 && requestedPlacement >= receivingPage.FirstSlotNumber && requestedPlacement < receivingPage.FirstSlotNumber + receivingPage.MaxSlots)
		{
			try
			{
				if (receivingPage[requestedPlacement] == null)
				{
					return requestedPlacement;
				}
			}
			catch (Exception)
			{
			}
		}
		return receivingPage.FindFreeSlot();
	}

	private bool CanEquipToPage(ICharacter character, IInventoryPage page, IItem item)
	{
		AOAction val = null;
		if (page is ArmorInventoryPage || page is ImplantInventoryPage)
		{
			val = item.ItemActions.SingleOrDefault((AOAction x) => (int)x.ActionType == 6);
		}
		else if (page is WeaponInventoryPage)
		{
			val = item.ItemActions.SingleOrDefault((AOAction x) => (int)x.ActionType == 8);
		}
		return val == null || val.CheckRequirements((IInstancedEntity)(object)character);
	}

	private bool RequiresImplantAccess(IInventoryPage page)
	{
		return page is ImplantInventoryPage;
	}

	private bool HasImplantAccess(ICharacter character)
	{
		Character val = (Character)(object)((character is Character) ? character : null);
		return val != null && val.HasImplantAccess();
	}

	private void SendImplantAccessDenied(ICharacter character)
	{
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Accessing implants requires technical supervision.", 0, 0);
	}

	private bool IsAppearanceEquipmentPage(IInventoryPage page)
	{
		return page is WeaponInventoryPage || page is ArmorInventoryPage || page is SocialArmorInventoryPage;
	}

	private void WaitForEquipVisualSync(IItem primary, IItem secondary, bool isSocial)
	{
		int num = GetEquipDelay(primary, isSocial);
		if (secondary != null)
		{
			num += GetEquipDelay(secondary, isSocial);
		}
		Thread.Sleep(num * 10);
	}

	private int GetEquipDelay(IItem item, bool isSocial)
	{
		if (item == null || isSocial)
		{
			return 20;
		}
		int attribute = item.GetAttribute(211);
		return (attribute == 1234567890) ? 20 : attribute;
	}

	public void SendMoveItemToInventoryAck(ICharacter character, Identity sourceContainer, int targetPlacement)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		((IDynel)character).Send((MessageBody)new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			SourceContainer = sourceContainer,
			Target = ((IEntity)character).Identity,
			TargetPlacement = targetPlacement,
			Unknown = 0
		}, false);
	}

	public bool HasFreeInventorySlots(ICharacter character, int neededSlots)
	{
		if (neededSlots <= 0)
		{
			return true;
		}
		IInventoryPage val = ((IItemContainer)character).BaseInventory[((IItemContainer)character).BaseInventory.StandardPage];
		int num = 0;
		for (int i = val.FirstSlotNumber; i < val.FirstSlotNumber + val.MaxSlots; i++)
		{
			if (val[i] == null)
			{
				num++;
				if (num >= neededSlots)
				{
					return true;
				}
			}
		}
		return false;
	}

	public int FindFreeStandardInventorySlot(IItemContainer owner)
	{
		return owner.BaseInventory[owner.BaseInventory.StandardPage].FindFreeSlot();
	}

	public InventoryError AddToStandardInventoryPage(IItemContainer owner, int targetSlot, IItem item)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		return owner.BaseInventory.AddToPage(owner.BaseInventory.StandardPage, targetSlot, item);
	}

	public void AddToStandardInventoryPageUnchecked(IItemContainer owner, int targetSlot, IItem item)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		owner.BaseInventory[owner.BaseInventory.StandardPage].Add(targetSlot, item);
	}

	public void SendTradeWindowMoveToInventory(ICharacter character, IdentityType sourceType, int sourceSlot, int targetSlot)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		ContainerAddItemMessage val = new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0
		};
		Identity sourceContainer = default(Identity);
		((Identity)(ref sourceContainer)).Type = sourceType;
		((Identity)(ref sourceContainer)).Instance = sourceSlot;
		val.SourceContainer = sourceContainer;
		val.Target = ((IEntity)character).Identity;
		val.TargetPlacement = targetSlot;
		((IDynel)character).Send((MessageBody)val, false);
	}

	public void ReturnPlayerTradeOffers(ICharacter owner, TemporaryBag shoppingBag)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		IInventoryPage playerOfferPage = shoppingBag.GetPlayerOfferPage(((IEntity)owner).Identity);
		if (playerOfferPage == null)
		{
			return;
		}
		foreach (KeyValuePair<int, IItem> item in playerOfferPage.List().ToList())
		{
			int num = ((IItemContainer)owner).BaseInventory[((IItemContainer)owner).BaseInventory.StandardPage].FindFreeSlot();
			if (num >= 0)
			{
				playerOfferPage.Remove(item.Key);
				((IItemContainer)owner).BaseInventory[((IItemContainer)owner).BaseInventory.StandardPage].Add(num, item.Value);
				string[] obj = new string[14]
				{
					"TRADE_DECLINE_RETURN owner=", null, null, null, null, null, null, null, null, null,
					null, null, null, null
				};
				Identity identity = ((IEntity)owner).Identity;
				obj[1] = ((Identity)(ref identity)).ToString(true);
				obj[2] = " name=";
				obj[3] = ((INamedEntity)owner).Name;
				obj[4] = " sourceSlot=";
				obj[5] = item.Key.ToString();
				obj[6] = " targetSlot=";
				obj[7] = num.ToString();
				obj[8] = " item=";
				obj[9] = item.Value.LowID.ToString();
				obj[10] = "/";
				obj[11] = item.Value.HighID.ToString();
				obj[12] = ":";
				obj[13] = item.Value.Quality.ToString();
				LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
				SendTradeWindowMoveToInventory(owner, (IdentityType)108, item.Key, num);
			}
		}
	}

	public void TransferPlayerTradeOffers(ICharacter from, ICharacter to, TemporaryBag shoppingBag)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Invalid comparison between Unknown and I4
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		IInventoryPage playerOfferPage = shoppingBag.GetPlayerOfferPage(((IEntity)from).Identity);
		foreach (KeyValuePair<int, IItem> item in playerOfferPage.List().ToList())
		{
			int num = ((IItemContainer)to).BaseInventory[((IItemContainer)to).BaseInventory.StandardPage].FindFreeSlot();
			if (num >= 0)
			{
				playerOfferPage.Remove(item.Key);
				InventoryError val = ((IItemContainer)to).BaseInventory.AddToPage(((IItemContainer)to).BaseInventory.StandardPage, num, item.Value);
				if ((int)val == 0)
				{
					string[] obj = new string[14]
					{
						"Player trade transfer committed from=", null, null, null, null, null, null, null, null, null,
						null, null, null, null
					};
					Identity identity = ((IEntity)from).Identity;
					obj[1] = ((Identity)(ref identity)).ToString(true);
					obj[2] = " to=";
					identity = ((IEntity)to).Identity;
					obj[3] = ((Identity)(ref identity)).ToString(true);
					obj[4] = " tradeSlot=";
					obj[5] = item.Key.ToString();
					obj[6] = " targetSlot=";
					obj[7] = num.ToString();
					obj[8] = " item=";
					obj[9] = item.Value.LowID.ToString();
					obj[10] = "/";
					obj[11] = item.Value.HighID.ToString();
					obj[12] = ":";
					obj[13] = item.Value.Quality.ToString();
					LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
					string[] obj2 = new string[18]
					{
						"TRADE_ITEM_COMMIT from=", null, null, null, null, null, null, null, null, null,
						null, null, null, null, null, null, null, null
					};
					identity = ((IEntity)from).Identity;
					obj2[1] = ((Identity)(ref identity)).ToString(true);
					obj2[2] = " fromName=";
					obj2[3] = ((INamedEntity)from).Name;
					obj2[4] = " to=";
					identity = ((IEntity)to).Identity;
					obj2[5] = ((Identity)(ref identity)).ToString(true);
					obj2[6] = " toName=";
					obj2[7] = ((INamedEntity)to).Name;
					obj2[8] = " sourceSlot=";
					obj2[9] = item.Key.ToString();
					obj2[10] = " targetSlot=";
					obj2[11] = num.ToString();
					obj2[12] = " item=";
					obj2[13] = item.Value.LowID.ToString();
					obj2[14] = "/";
					obj2[15] = item.Value.HighID.ToString();
					obj2[16] = ":";
					obj2[17] = item.Value.Quality.ToString();
					LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj2));
				}
				else
				{
					playerOfferPage.Add(item.Key, item.Value);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(to, "Could not receive trade item. (" + ((object)(InventoryError)(ref val)).ToString() + ")", 0, 0);
				}
			}
		}
	}

	public void PersistCharacterInventory(ICharacter character, string reason)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		((IItemContainer)character).BaseInventory.Write();
		Identity identity = ((IEntity)character).Identity;
		LogUtil.Debug((DebugInfoDetail)8, "Persisted inventory after " + reason + " char=" + ((Identity)(ref identity)).ToString(true));
	}

	public void PersistClientMoveItemToInventory(ICharacter character, string reason)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		((IItemContainer)character).BaseInventory.Write();
		LogUtil.Debug((DebugInfoDetail)8, $"Persisted inventory after ClientMoveItemToInventory {reason} char={((IEntity)character).Identity}");
	}

	public bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		return InventoryItemRules.HasSameUniqueItem(item, from existing in ((IItemContainer)character).BaseInventory.Pages.Values.SelectMany((IInventoryPage page) => page.List())
			select existing.Value);
	}

	public bool HasCharacterInventory(ICharacter character)
	{
		return character != null && ((IItemContainer)character).BaseInventory != null;
	}

	public bool CharacterHasItemInCarriedInventory(ICharacter source, int itemId)
	{
		if (((IItemContainer)source).BaseInventory.Pages.TryGetValue(104, out var value) && InventoryPageHasItem(value, itemId))
		{
			return true;
		}
		return ((IItemContainer)source).BaseInventory.Pages.TryGetValue(110, out value) && InventoryPageHasItem(value, itemId);
	}

	public int CountCharacterItemInCarriedInventory(ICharacter source, int itemId)
	{
		if (source == null || ((IItemContainer)source).BaseInventory == null)
		{
			return 0;
		}
		int num = 0;
		if (((IItemContainer)source).BaseInventory.Pages.TryGetValue(104, out var value))
		{
			num += CountInventoryPageItems(value, itemId);
		}
		if (((IItemContainer)source).BaseInventory.Pages.TryGetValue(110, out value))
		{
			num += CountInventoryPageItems(value, itemId);
		}
		return num;
	}

	public QuestRewardInventoryGrantResult TryGrantQuestRewardItem(ICharacter source, Item item)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		InventoryError val = ((IItemContainer)source).BaseInventory.TryAdd((IItem)(object)item);
		if ((int)val > 0)
		{
			return QuestRewardInventoryGrantResult.InventoryAddFailed(val);
		}
		try
		{
			if (!((IItemContainer)source).BaseInventory.Write())
			{
				RollBackQuestRewardItem(source, (IItem)(object)item);
				return QuestRewardInventoryGrantResult.PersistReturnedFalse();
			}
		}
		catch (Exception ex)
		{
			RollBackQuestRewardItem(source, (IItem)(object)item);
			return QuestRewardInventoryGrantResult.PersistFailed(ex.Message);
		}
		return QuestRewardInventoryGrantResult.Succeeded();
	}

	private static void RollBackQuestRewardItem(ICharacter source, IItem item)
	{
		foreach (IInventoryPage value in ((IItemContainer)source).BaseInventory.Pages.Values)
		{
			foreach (KeyValuePair<int, IItem> item2 in value.List().ToList())
			{
				if (item2.Value == item)
				{
					value.Remove(item2.Key);
					return;
				}
			}
		}
	}

	public CorpseLootInventoryTransferResult TryAddCorpseLootItem(ICharacter looter, IItem item, int targetPlacement)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Invalid comparison between Unknown and I4
		CorpseLootInventoryTransferResult corpseLootInventoryTransferResult = new CorpseLootInventoryTransferResult();
		if (!TryResolveCorpseLootTargetSlot(looter, targetPlacement, out var targetPageNumber, out var targetSlot))
		{
			corpseLootInventoryTransferResult.Status = CorpseLootInventoryTransferStatus.NoFreeSlot;
			return corpseLootInventoryTransferResult;
		}
		corpseLootInventoryTransferResult.TargetPageNumber = targetPageNumber;
		corpseLootInventoryTransferResult.TargetSlot = targetSlot;
		InventoryError val;
		try
		{
			val = ((IItemContainer)looter).BaseInventory.AddToPage(targetPageNumber, targetSlot, item);
		}
		catch (Exception ex)
		{
			corpseLootInventoryTransferResult.Status = CorpseLootInventoryTransferStatus.AddFailed;
			corpseLootInventoryTransferResult.ExceptionMessage = ex.Message;
			return corpseLootInventoryTransferResult;
		}
		corpseLootInventoryTransferResult.InventoryError = val;
		if ((int)val > 0)
		{
			corpseLootInventoryTransferResult.Status = CorpseLootInventoryTransferStatus.AddRejected;
			return corpseLootInventoryTransferResult;
		}
		((IItemContainer)looter).BaseInventory.Write();
		corpseLootInventoryTransferResult.Status = CorpseLootInventoryTransferStatus.Success;
		return corpseLootInventoryTransferResult;
	}

	private static int DecodeBackpackHandle(Identity sourceContainer)
	{
		return (((Identity)(ref sourceContainer)).Instance >>> 16) & 0xFFFF;
	}

	private bool TryResolveCorpseLootTargetSlot(ICharacter looter, int targetPlacement, out int targetPageNumber, out int targetSlot)
	{
		targetPageNumber = -1;
		targetSlot = -1;
		if (targetPlacement == 111)
		{
			targetPageNumber = ((IItemContainer)looter).BaseInventory.StandardPage;
			IInventoryPage val = ((IItemContainer)looter).BaseInventory.Pages[targetPageNumber];
			targetSlot = val.FindFreeSlot();
			return targetSlot >= 0;
		}
		try
		{
			IInventoryPage val2 = ((IItemContainer)looter).BaseInventory.PageFromSlot(targetPlacement);
			if (val2 == null)
			{
				return false;
			}
			foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)looter).BaseInventory.Pages)
			{
				if (page.Value == val2)
				{
					targetPageNumber = page.Key;
					targetSlot = targetPlacement;
					return true;
				}
			}
			return false;
		}
		catch (Exception)
		{
			targetPageNumber = ((IItemContainer)looter).BaseInventory.StandardPage;
			IInventoryPage val3 = ((IItemContainer)looter).BaseInventory.Pages[targetPageNumber];
			targetSlot = val3.FindFreeSlot();
			return targetSlot >= 0;
		}
	}

	private static bool InventoryPageHasItem(IInventoryPage page, int itemId)
	{
		foreach (KeyValuePair<int, IItem> item in page.List())
		{
			IItem value = item.Value;
			if (value != null && (value.LowID == itemId || value.HighID == itemId))
			{
				return true;
			}
		}
		return false;
	}

	private static int CountInventoryPageItems(IInventoryPage page, int itemId)
	{
		int num = 0;
		foreach (KeyValuePair<int, IItem> item in page.List())
		{
			IItem value = item.Value;
			if (value != null && (value.LowID == itemId || value.HighID == itemId))
			{
				num++;
			}
		}
		return num;
	}

	private static IItem CloneShopItem(IItem item)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		Item val = (Item)(object)((item is Item) ? item : null);
		if (val == null)
		{
			return item;
		}
		Item val2 = new Item(val.Quality, val.LowID, val.HighID);
		val2.MultipleCount = val.MultipleCount;
		return (IItem)(object)val2;
	}

	private static int DecodeBackpackSlot(Identity sourceContainer)
	{
		return ((Identity)(ref sourceContainer)).Instance & 0xFFFF;
	}

	private static bool IsInventoryToBankDeposit(ClientContainerAddItemMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		Identity val = message.Source;
		int result;
		if ((int)((Identity)(ref val)).Type == 104)
		{
			val = message.Target;
			result = (((int)((Identity)(ref val)).Type == 57005) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static bool IsBackpackUseSlot(IdentityType identityType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Invalid comparison between Unknown and I4
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		return (int)identityType == 104 || (int)identityType == 102 || (int)identityType == 115;
	}

	private static bool TryResolveBackpackContainerIdentity(ICharacter character, Identity itemPosition, Item item, out Identity containerIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		containerIdentity = Identity.None;
		return InventoryItemRules.TryEnsureBackpackContainerIdentity((IItem)(object)item, ((IEntity)character).Identity, itemPosition, ref containerIdentity);
	}

	private static bool IsItemUsable(Item item)
	{
		return (item.GetAttribute(30) & 8) == 8;
	}

	private bool IsUseBlockedBySkillLock(ICharacter characterEntity, Item item)
	{
		Character val = (Character)(object)((characterEntity is Character) ? characterEntity : null);
		if (val == null)
		{
			return false;
		}
		foreach (Event item2 in item.Events.Where((Event x) => (int)x.EventType == 0))
		{
			foreach (Function item3 in item2.Functions.Where((Function x) => x.FunctionType == 53033))
			{
				if (lockskill.TryReadArguments(item3.Arguments.Values.ToArray(), out var statId, out var _))
				{
					int skillLockRemainingSeconds = val.GetSkillLockRemainingSeconds(statId);
					if (skillLockRemainingSeconds > 0)
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillUnavailable((ICharacter)(object)val, statId, skillLockRemainingSeconds);
						return true;
					}
				}
			}
		}
		return false;
	}

	private static bool ItemFunctionRequirementsPass(ICharacter character, Function itemFunction)
	{
		bool flag = true;
		foreach (Requirement requirement in itemFunction.Requirements)
		{
			flag &= requirement.CheckRequirement((IInstancedEntity)(object)character);
			if (!flag)
			{
				break;
			}
		}
		return flag;
	}

	private bool EnsureWeaponMesh(ICharacter character, IInventoryPage weaponPage, int slot, int meshPosition, StatIds meshStat, StatIds overrideTextureStat)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected I4, but got Unknown
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Expected I4, but got Unknown
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Expected I4, but got Unknown
		IItem val = weaponPage[slot];
		if (val == null)
		{
			return false;
		}
		AOMeshs meshAtPosition = ((IDynel)character).MeshLayer.GetMeshAtPosition(meshPosition);
		int num = NormalizeItemVisualValue(val.GetAttribute((int)meshStat));
		if (num <= 0)
		{
			num = NormalizeItemVisualValue(val.GetAttribute(209));
		}
		if (num <= 0)
		{
			bool flag = val.ItemActions.Any((AOAction x) => (int)x.ActionType == 8);
			string text = string.Join(",", (from x in ((IEventHolder)val).Events.Where((Event x) => (int)x.EventType == 14 || (int)x.EventType == 2).SelectMany((Event x) => x.Functions)
				select x.FunctionType.ToString()).ToArray());
			LogUtil.Debug((DebugInfoDetail)512, $"EnsureWeaponMesh skipped: item has no valid mesh stat char={((IEntity)character).Identity} slot={slot} meshStat={meshStat} raw={val.GetAttribute((int)meshStat)} item={val.LowID}/{val.HighID} ql={val.Quality} hasToWield={(flag ? 1 : 0)} wearFuncs=[{text}] meshR={val.GetAttribute(1006)} meshL={val.GetAttribute(1007)} ovR={val.GetAttribute(1009)} ovL={val.GetAttribute(1010)} weaponMeshHolder={val.GetAttribute(209)}");
			return false;
		}
		if (meshAtPosition != null)
		{
			if (meshAtPosition.Mesh > 0 && meshAtPosition.Mesh != 1234567890)
			{
				return false;
			}
			((IDynel)character).MeshLayer.RemoveMesh(meshAtPosition.Position, meshAtPosition.Mesh, meshAtPosition.OverrideTexture, meshAtPosition.Layer);
		}
		int num2 = NormalizeItemVisualValue(val.GetAttribute((int)overrideTextureStat));
		int layer = MeshLayers.GetLayer(slot);
		((IDynel)character).MeshLayer.AddMesh(meshPosition, num, num2, layer);
		((IStats)character).Stats[meshStat].Value = num;
		LogUtil.Debug((DebugInfoDetail)512, $"EnsureWeaponMesh applied char={((IEntity)character).Identity} slot={slot} position={meshPosition} mesh={num} override={num2} layer={layer}");
		return true;
	}

	private static int NormalizeItemVisualValue(int value)
	{
		if (value <= 0 || value == 1234567890)
		{
			return 0;
		}
		return value;
	}

	private static AOAction ResolveContainerAddItemAction(IInventoryPage page, IItem item)
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		AOAction val = null;
		if (page is ArmorInventoryPage || page is ImplantInventoryPage)
		{
			val = item.ItemActions.SingleOrDefault((AOAction x) => (int)x.ActionType == 6);
			if (val == null)
			{
				return new AOAction();
			}
		}
		if (page is WeaponInventoryPage)
		{
			val = item.ItemActions.SingleOrDefault((AOAction x) => (int)x.ActionType == 8);
			if (val == null)
			{
				return new AOAction();
			}
		}
		if (page is PlayerInventoryPage)
		{
			return new AOAction();
		}
		if (page is SocialArmorInventoryPage)
		{
			return new AOAction();
		}
		if (val == null)
		{
			throw new NotSupportedException("No suitable action found for equipping to this page: " + ((object)page).GetType());
		}
		return val;
	}

	private void TryRemoveBankRollback(IInventoryPage bankPage, int bankSlot)
	{
		try
		{
			if (bankPage[bankSlot] != null)
			{
				bankPage.Remove(bankSlot);
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"ClientContainerAddItem bank deposit rollback failed bankSlot={bankSlot} error={ex.Message}");
		}
	}

	private void TryRemoveBackpackRollback(IInventoryPage backpackPage, int backpackSlot)
	{
		try
		{
			if (backpackPage[backpackSlot] != null)
			{
				backpackPage.Remove(backpackSlot);
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"ClientContainerAddItem backpack move rollback failed slot={backpackSlot} error={ex.Message}");
		}
	}

	private void TryRemoveInventoryRollback(IInventoryPage inventoryPage, int inventorySlot)
	{
		try
		{
			if (inventoryPage[inventorySlot] != null)
			{
				inventoryPage.Remove(inventorySlot);
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory backpack move rollback failed slot={inventorySlot} error={ex.Message}");
		}
	}
}
