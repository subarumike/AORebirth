using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class TradeMessageHandler : BaseMessageHandler<TradeMessage, TradeMessageHandler>
{
	public TradeMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = false;
	}

	protected override void Read(TradeMessage message, IZoneClient client)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected I4, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Invalid comparison between Unknown and I4
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Invalid comparison between Unknown and I4
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Invalid comparison between Unknown and I4
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Expected I4, but got Unknown
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Invalid comparison between Unknown and I4
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0712: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Unknown result type (might be due to invalid IL or missing references)
		//IL_071a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0724: Invalid comparison between Unknown and I4
		//IL_0bee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bf3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bf6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c00: Invalid comparison between Unknown and I4
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Invalid comparison between Unknown and I4
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_0497: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0728: Unknown result type (might be due to invalid IL or missing references)
		//IL_072d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0730: Unknown result type (might be due to invalid IL or missing references)
		//IL_073a: Invalid comparison between Unknown and I4
		//IL_0c04: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c09: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c0c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c16: Invalid comparison between Unknown and I4
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_051f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0524: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		//IL_077d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0784: Unknown result type (might be due to invalid IL or missing references)
		//IL_05aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_056d: Unknown result type (might be due to invalid IL or missing references)
		//IL_058e: Unknown result type (might be due to invalid IL or missing references)
		//IL_060c: Unknown result type (might be due to invalid IL or missing references)
		//IL_061f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0624: Unknown result type (might be due to invalid IL or missing references)
		//IL_0633: Unknown result type (might be due to invalid IL or missing references)
		//IL_0663: Unknown result type (might be due to invalid IL or missing references)
		//IL_0689: Unknown result type (might be due to invalid IL or missing references)
		//IL_085d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0862: Unknown result type (might be due to invalid IL or missing references)
		//IL_0878: Unknown result type (might be due to invalid IL or missing references)
		//IL_087d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0961: Unknown result type (might be due to invalid IL or missing references)
		//IL_0966: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b0f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b16: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a78: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a82: Expected O, but got Unknown
		//IL_0aae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ab3: Unknown result type (might be due to invalid IL or missing references)
		((IClient)client).Server.Info((IClient)(object)client, "Trade action={0}({1}) identity={2} unknown={3} marker={4} p1={5} p2={6} p3={7} p4={8} target={9} container={10}", new object[11]
		{
			message.Action,
			(int)message.Action,
			((N3Message)message).Identity,
			((N3Message)message).Unknown,
			message.Unknown1,
			message.Param1,
			message.Param2,
			message.Param3,
			message.Param4,
			message.Target,
			message.Container
		});
		string[] obj = new string[18]
		{
			"TRADE_RX char=", null, null, null, null, null, null, null, null, null,
			null, null, null, null, null, null, null, null
		};
		Identity sourceContainer = ((IEntity)client.Controller.Character).Identity;
		obj[1] = ((Identity)(ref sourceContainer)).ToString(true);
		obj[2] = " name=";
		obj[3] = ((INamedEntity)client.Controller.Character).Name;
		obj[4] = " action=";
		TradeAction action = message.Action;
		obj[5] = ((object)(TradeAction)(ref action)).ToString();
		obj[6] = " p1=";
		obj[7] = message.Param1.ToString();
		obj[8] = " p2=";
		obj[9] = message.Param2.ToString();
		obj[10] = " p3=";
		obj[11] = message.Param3.ToString();
		obj[12] = " p4=";
		obj[13] = message.Param4.ToString();
		obj[14] = " target=";
		sourceContainer = message.Target;
		obj[15] = ((Identity)(ref sourceContainer)).ToString(true);
		obj[16] = " container=";
		sourceContainer = message.Container;
		obj[17] = ((Identity)(ref sourceContainer)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		if ((int)message.Action != 1 && (int)message.Action != 3 && (int)message.Action != 7 && (int)message.Action != 2)
		{
			IItemContainer @object = Pool.Instance.GetObject<IItemContainer>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity, message.Target);
		}
		TradeAction action2 = message.Action;
		TradeAction val = action2;
		switch ((int)val)
		{
		case 0:
			if (!ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(client.Controller.Character, message.Target) && !TryStartPlayerTrade(client.Controller.Character, message.Target))
			{
				ICharacter object3 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity, message.Target);
				if (object3 != null && ((IDynel)object3).Controller is NPCController nPCController)
				{
					nPCController.Trade(((N3Message)message).Identity);
				}
			}
			break;
		case 5:
		{
			if (client.Controller.Character.ShoppingBag == null)
			{
				LogUtil.Debug((DebugInfoDetail)32768, "Trade AddItem ignored because character has no active trade bag.");
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "No active trade session.", 0, 0);
				break;
			}
			TemporaryBag shoppingBag3 = client.Controller.Character.ShoppingBag;
			sourceContainer = shoppingBag3.Vendor;
			bool flag = (int)((Identity)(ref sourceContainer)).Type == 51035;
			IItemContainer val3 = ResolveTradeIssuer(client.Controller.Character, message.Target);
			if (val3 != null && InventoryContainerRuntimeService.Default.TryGetTradeAddItem(val3, message, out var item) && (flag || val3 is Vendor || !TryAddPlayerTradeItem(client.Controller.Character, val3, shoppingBag3, message)))
			{
				if (val3 is Vendor)
				{
					InventoryContainerRuntimeService.Default.AddVendorPurchaseOffer(shoppingBag3, message, item);
				}
				else
				{
					InventoryContainerRuntimeService.Default.AddVendorSaleOffer(shoppingBag3, message, val3);
				}
				AcknowledgeTradeAction(client.Controller.Character, message);
			}
			break;
		}
		case 6:
		{
			if (client.Controller.Character.ShoppingBag == null)
			{
				LogUtil.Debug((DebugInfoDetail)32768, "Trade RemoveItem ignored because character has no active trade bag.");
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "No active trade session.", 0, 0);
				break;
			}
			IItemContainer val4 = ResolveTradeIssuer(client.Controller.Character, message.Target);
			if (val4 == null)
			{
				break;
			}
			TemporaryBag shoppingBag4 = client.Controller.Character.ShoppingBag;
			if (TryRemovePlayerTradeItem(client.Controller.Character, val4, shoppingBag4, message))
			{
				break;
			}
			IItem val5;
			if (val4 is Vendor)
			{
				InventoryContainerRuntimeService default2 = InventoryContainerRuntimeService.Default;
				sourceContainer = message.Container;
				val5 = default2.GetVendorTradeItem(val4, ((Identity)(ref sourceContainer)).Instance);
			}
			else
			{
				IItem[] soldItems3 = shoppingBag4.GetSoldItems();
				sourceContainer = message.Container;
				val5 = soldItems3[((Identity)(ref sourceContainer)).Instance];
			}
			if (val5 == null)
			{
				break;
			}
			if (val4 is Vendor)
			{
				InventoryContainerRuntimeService.Default.RemoveVendorPurchaseOffer(shoppingBag4, message);
				((AbstractMessageHandler<TradeMessage>)(object)this).Send(client.Controller.Character, AcknowledgeRemove(shoppingBag4.Shopper, message), false);
				((AbstractMessageHandler<TradeMessage>)(object)this).Send(client.Controller.Character, AcknowledgeRemove(shoppingBag4.Vendor, message), false);
			}
			else
			{
				Identity target = message.Target;
				sourceContainer = message.Container;
				IItem val6 = shoppingBag4.Remove(target, ((Identity)(ref sourceContainer)).Instance);
				if (val6 != null)
				{
					InventoryItemAddResult inventoryItemAddResult2 = InventoryContainerRuntimeService.Default.TryAddStandardInventoryItem((IItemContainer)(object)client.Controller.Character, val6);
					if (inventoryItemAddResult2.Succeeded)
					{
						ContainerAddItemMessageHandler default3 = BaseMessageHandler<ContainerAddItemMessage, ContainerAddItemMessageHandler>.Default;
						ICharacter character4 = client.Controller.Character;
						sourceContainer = default(Identity);
						((Identity)(ref sourceContainer)).Type = (IdentityType)108;
						Identity container = message.Container;
						((Identity)(ref sourceContainer)).Instance = ((Identity)(ref container)).Instance;
						default3.Send(character4, sourceContainer, 111);
					}
					else if (inventoryItemAddResult2.Status != InventoryItemAddStatus.Failed)
					{
					}
					BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(client.Controller.Character, shoppingBag4.Vendor);
					BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(client.Controller.Character, ((IEntity)client.Controller.Character).Identity);
				}
			}
			AcknowledgeTradeAction(client.Controller.Character, message);
			break;
		}
		case 1:
		case 3:
		{
			if (client.Controller.Character.ShoppingBag == null)
			{
				LogUtil.Debug((DebugInfoDetail)32768, "Trade Accept ignored because character has no active trade bag.");
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "No active trade session.", 0, 0);
				break;
			}
			TemporaryBag shoppingBag2 = client.Controller.Character.ShoppingBag;
			sourceContainer = shoppingBag2.Shopper;
			if ((int)((Identity)(ref sourceContainer)).Type == 50000)
			{
				sourceContainer = shoppingBag2.Vendor;
				if ((int)((Identity)(ref sourceContainer)).Type == 50000 && TryEndPlayerTrade(client.Controller.Character, shoppingBag2, message))
				{
					break;
				}
			}
			Vendor object2 = Pool.Instance.GetObject<Vendor>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity, shoppingBag2.Vendor);
			IItemContainer character2 = (IItemContainer)(object)client.Controller.Character;
			if (character2 == null || object2 == null || shoppingBag2 == null)
			{
				break;
			}
			IItem[] boughtItems;
			try
			{
				boughtItems = shoppingBag2.GetBoughtItems();
			}
			catch (Exception ex)
			{
				LogUtil.ErrorException(ex);
				break;
			}
			IItem[] array = boughtItems;
			IItem[] soldItems2 = shoppingBag2.GetSoldItems();
			int num = CalculateVendorBuyTotal(client.Controller.Character, object2, array);
			int num2 = CalculateVendorSellTotal(client.Controller.Character, object2, soldItems2);
			int num3 = num - num2;
			int cash = GetCash(client.Controller.Character);
			long num4 = (long)cash - (long)num3;
			string[] obj2 = new string[18]
			{
				"Vendor trade cash summary shopper=", null, null, null, null, null, null, null, null, null,
				null, null, null, null, null, null, null, null
			};
			sourceContainer = ((IEntity)client.Controller.Character).Identity;
			obj2[1] = ((Identity)(ref sourceContainer)).ToString(true);
			obj2[2] = " vendor=";
			sourceContainer = ((PooledObject)object2).Identity;
			obj2[3] = ((Identity)(ref sourceContainer)).ToString(true);
			obj2[4] = " boughtItems=";
			obj2[5] = array.Length.ToString();
			obj2[6] = " soldItems=";
			obj2[7] = soldItems2.Length.ToString();
			obj2[8] = " buyTotal=";
			obj2[9] = num.ToString();
			obj2[10] = " sellTotal=";
			obj2[11] = num2.ToString();
			obj2[12] = " cashDelta=";
			obj2[13] = num3.ToString();
			obj2[14] = " cashBefore=";
			obj2[15] = cash.ToString();
			obj2[16] = " cashAfterRaw=";
			obj2[17] = num4.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj2));
			if (num4 < 0 || num4 > 999999999)
			{
				string[] obj3 = new string[10] { "Vendor trade rejected because cash would be invalid shopper=", null, null, null, null, null, null, null, null, null };
				sourceContainer = ((IEntity)client.Controller.Character).Identity;
				obj3[1] = ((Identity)(ref sourceContainer)).ToString(true);
				obj3[2] = " cashBefore=";
				obj3[3] = cash.ToString();
				obj3[4] = " cashDelta=";
				obj3[5] = num3.ToString();
				obj3[6] = " cashAfterRaw=";
				obj3[7] = num4.ToString();
				obj3[8] = " cap=";
				obj3[9] = 999999999.ToString();
				LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj3));
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "Trade failed: credits are missing or credit cap would be exceeded.", 0, 0);
				break;
			}
			if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(client.Controller.Character, array.Length))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "Could not add item to inventory. (inventory is full)", 0, 0);
				break;
			}
			IItem[] array2 = array;
			foreach (IItem val2 in array2)
			{
				InventoryItemAddResult inventoryItemAddResult = InventoryContainerRuntimeService.Default.TryAddStandardInventoryItem(character2, val2);
				if (inventoryItemAddResult.Succeeded)
				{
					BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(client.Controller.Character, (Item)val2);
				}
				else if (inventoryItemAddResult.Status == InventoryItemAddStatus.Failed)
				{
					ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
					ICharacter character3 = client.Controller.Character;
					InventoryError inventoryError = inventoryItemAddResult.InventoryError;
					@default.Send(character3, "Could not add item to inventory. (" + ((object)(InventoryError)(ref inventoryError)).ToString() + ")", 0, 0);
				}
			}
			SetCash(client.Controller.Character, CashStatRules.Clamp(num4));
			Send(client.Controller.Character, (TradeAction)4, shoppingBag2.Vendor, shoppingBag2.Vendor);
			client.Controller.SendChangedStats();
			((PooledObject)shoppingBag2).Dispose();
			break;
		}
		case 7:
			if (client.Controller.Character.ShoppingBag == null)
			{
				LogUtil.Debug((DebugInfoDetail)32768, "Trade UpdateCredits ignored because character has no active trade bag.");
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "No active trade session.", 0, 0);
			}
			else if (!TrySetPlayerTradeCredits(client.Controller.Character, client.Controller.Character.ShoppingBag, message))
			{
			}
			break;
		case 2:
		{
			IItemContainer character = (IItemContainer)(object)client.Controller.Character;
			TemporaryBag shoppingBag = client.Controller.Character.ShoppingBag;
			if (shoppingBag != null)
			{
				sourceContainer = shoppingBag.Shopper;
				if ((int)((Identity)(ref sourceContainer)).Type == 50000)
				{
					sourceContainer = shoppingBag.Vendor;
					if ((int)((Identity)(ref sourceContainer)).Type == 50000 && TryDeclinePlayerTrade(client.Controller.Character, shoppingBag))
					{
						break;
					}
				}
				SendVendorShopDeclineClose(client.Controller.Character);
				try
				{
					IItem[] soldItems = shoppingBag.GetSoldItems();
					InventoryContainerRuntimeService.Default.ReturnItemsToStandardInventoryUnchecked(character, soldItems);
					break;
				}
				finally
				{
					((PooledObject)shoppingBag).Dispose();
				}
			}
			SendVendorShopDeclineClose(client.Controller.Character);
			break;
		}
		case 4:
			break;
		}
	}

	private bool TryStartPlayerTrade(ICharacter initiator, Identity targetIdentity)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Expected O, but got Unknown
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		if (initiator == null || ((IInstancedEntity)initiator).Playfield == null)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)initiator).Playfield).Identity, targetIdentity);
		if (@object != null)
		{
			Identity val = ((IEntity)@object).Identity;
			if (!((object)(Identity)(ref val)).Equals((object)((IEntity)initiator).Identity))
			{
				if (!(((IDynel)@object).Controller is PlayerController))
				{
					return false;
				}
				if (TryRefreshExistingPlayerTrade(initiator, @object))
				{
					return true;
				}
				TryClearStalePlayerTrade(initiator, @object);
				TryClearStalePlayerTrade(@object, initiator);
				if (initiator.ShoppingBag != null || @object.ShoppingBag != null)
				{
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(initiator, "Trade target is already trading.", 0, 0);
					return true;
				}
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)51047;
				((Identity)(ref val)).Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, (IdentityType)51047);
				Identity val2 = val;
				TemporaryBag shoppingBag = (initiator.ShoppingBag = new TemporaryBag(((IEntity)initiator).Identity, val2, ((IEntity)initiator).Identity, ((IEntity)@object).Identity, 64));
				@object.ShoppingBag = shoppingBag;
				SendPlayerTradeStart(initiator, @object);
				SendPlayerTradeStart(@object, initiator);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(initiator, "Trade started with " + ((INamedEntity)@object).Name + ".", 0, 0);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(@object, ((INamedEntity)initiator).Name + " started a trade with you.", 0, 0);
				string[] obj = new string[6] { "Player trade opened shopper=", null, null, null, null, null };
				val = ((IEntity)initiator).Identity;
				obj[1] = ((Identity)(ref val)).ToString(true);
				obj[2] = " target=";
				val = ((IEntity)@object).Identity;
				obj[3] = ((Identity)(ref val)).ToString(true);
				obj[4] = " bag=";
				obj[5] = ((Identity)(ref val2)).ToString(true);
				LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
				return true;
			}
		}
		return false;
	}

	private IItemContainer ResolveTradeIssuer(ICharacter character, Identity target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			if (((object)(Identity)(ref identity)).Equals((object)target))
			{
				return (IItemContainer)(object)character;
			}
		}
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return null;
		}
		return Pool.Instance.GetObject<IItemContainer>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
	}

	private bool TryRefreshExistingPlayerTrade(ICharacter initiator, ICharacter target)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		TemporaryBag val = initiator.ShoppingBag ?? target.ShoppingBag;
		if (val == null)
		{
			return false;
		}
		if (initiator.ShoppingBag != val || target.ShoppingBag != val || !IsTradeBetween(val, initiator, target))
		{
			return false;
		}
		SendPlayerTradeStart(initiator, target);
		SendPlayerTradeStart(target, initiator);
		string[] obj = new string[6] { "Player trade refreshed shopper=", null, null, null, null, null };
		Identity identity = ((IEntity)initiator).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = " target=";
		identity = ((IEntity)target).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " bag=";
		identity = ((PooledObject)val).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		return true;
	}

	private bool TryClearStalePlayerTrade(ICharacter character, ICharacter requestedPartner)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || requestedPartner == null || character.ShoppingBag == null)
		{
			return false;
		}
		TemporaryBag shoppingBag = character.ShoppingBag;
		if (!IsTradeBetween(shoppingBag, character, requestedPartner) || requestedPartner.ShoppingBag == shoppingBag)
		{
			return false;
		}
		ReturnAllPlayerTradeOffers(shoppingBag, "stale player trade cleanup");
		string[] obj = new string[6] { "Player trade stale bag cleared character=", null, null, null, null, null };
		Identity identity = ((IEntity)character).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = " requestedPartner=";
		identity = ((IEntity)requestedPartner).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " bag=";
		identity = ((PooledObject)shoppingBag).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		((PooledObject)shoppingBag).Dispose();
		return true;
	}

	private static bool IsTradeBetween(TemporaryBag tradeBag, ICharacter first, ICharacter second)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		if (tradeBag == null || first == null || second == null)
		{
			return false;
		}
		Identity val = tradeBag.Shopper;
		int result;
		if (((object)(Identity)(ref val)).Equals((object)((IEntity)first).Identity))
		{
			val = tradeBag.Vendor;
			if (((object)(Identity)(ref val)).Equals((object)((IEntity)second).Identity))
			{
				result = 1;
				goto IL_00a2;
			}
		}
		val = tradeBag.Shopper;
		if (((object)(Identity)(ref val)).Equals((object)((IEntity)second).Identity))
		{
			val = tradeBag.Vendor;
			result = (((object)(Identity)(ref val)).Equals((object)((IEntity)first).Identity) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		goto IL_00a2;
		IL_00a2:
		return (byte)result != 0;
	}

	private void SendPlayerTradeStart(ICharacter viewer, ICharacter partner)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeStart(viewer, ((IEntity)partner).Identity), false);
	}

	private MessageDataFiller<TradeMessage> PlayerTradeStart(ICharacter character, Identity partner)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			x.Action = (TradeAction)0;
			x.Container = Identity.None;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Target = partner;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
		};
	}

	private bool TryAddPlayerTradeItem(ICharacter character, IItemContainer issuer, TemporaryBag shoppingBag, TradeMessage message)
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerTrade(character, shoppingBag, out var otherCharacter))
		{
			return false;
		}
		if (issuer != null)
		{
			Identity val = ((IEntity)issuer).Identity;
			if (((object)(Identity)(ref val)).Equals((object)((IEntity)character).Identity))
			{
				if (!InventoryContainerRuntimeService.Default.HasInventoryPage(issuer, message.Container))
				{
					val = message.Container;
					LogUtil.Debug((DebugInfoDetail)32768, "Player trade add ignored because source page is missing: " + ((object)(Identity)(ref val)).ToString());
					return true;
				}
				IItem val2;
				try
				{
					val2 = InventoryContainerRuntimeService.Default.RemoveInventoryItem(issuer, message.Container);
				}
				catch (ArgumentOutOfRangeException ex)
				{
					val = message.Container;
					LogUtil.Debug((DebugInfoDetail)32768, "Player trade add ignored because source slot is empty: " + ((Identity)(ref val)).ToString(true) + " reason=" + ex.Message);
					SendPlayerTradeInventoryInvalidation(character, otherCharacter);
					return true;
				}
				if (val2 == null)
				{
					val = message.Container;
					LogUtil.Debug((DebugInfoDetail)32768, "Player trade add ignored because source slot returned no item: " + ((Identity)(ref val)).ToString(true));
					SendPlayerTradeInventoryInvalidation(character, otherCharacter);
					return true;
				}
				int num = shoppingBag.AddPlayerOffer(((IEntity)character).Identity, val2);
				if (num < 0)
				{
					InventoryContainerRuntimeService.Default.RestoreInventoryItem(issuer, message.Container, val2);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Trade window is full.", 0, 0);
					return true;
				}
				AcknowledgeTradeAction(character, message);
				SendPlayerTradeItemRender(otherCharacter, val2, message.Container);
				InventoryContainerRuntimeService.Default.PersistCharacterInventory(character, "player trade add");
				string[] obj = new string[14]
				{
					"Player trade add owner=", null, null, null, null, null, null, null, null, null,
					null, null, null, null
				};
				val = ((IEntity)character).Identity;
				obj[1] = ((Identity)(ref val)).ToString(true);
				obj[2] = " other=";
				val = ((IEntity)otherCharacter).Identity;
				obj[3] = ((Identity)(ref val)).ToString(true);
				obj[4] = " source=";
				val = message.Container;
				obj[5] = ((Identity)(ref val)).ToString(true);
				obj[6] = " tradeSlot=";
				obj[7] = num.ToString();
				obj[8] = " item=";
				obj[9] = val2.LowID.ToString();
				obj[10] = "/";
				obj[11] = val2.HighID.ToString();
				obj[12] = ":";
				obj[13] = val2.Quality.ToString();
				LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
				return true;
			}
		}
		LogUtil.Debug((DebugInfoDetail)32768, "Player trade add ignored because source is not the sender inventory.");
		return true;
	}

	private bool TryRemovePlayerTradeItem(ICharacter character, IItemContainer issuer, TemporaryBag shoppingBag, TradeMessage message)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Invalid comparison between Unknown and I4
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerTrade(character, shoppingBag, out var otherCharacter))
		{
			return false;
		}
		if (issuer != null)
		{
			Identity val = ((IEntity)issuer).Identity;
			if (((object)(Identity)(ref val)).Equals((object)((IEntity)character).Identity))
			{
				val = message.Container;
				int instance = ((Identity)(ref val)).Instance;
				IItem val2 = shoppingBag.RemovePlayerOffer(((IEntity)character).Identity, instance);
				if (val2 == null)
				{
					val = message.Container;
					LogUtil.Debug((DebugInfoDetail)32768, "Player trade remove ignored because trade slot is empty: " + ((object)(Identity)(ref val)).ToString());
					return true;
				}
				int num = InventoryContainerRuntimeService.Default.FindFreeStandardInventorySlot((IItemContainer)(object)character);
				if (num < 0)
				{
					shoppingBag.AddPlayerOffer(((IEntity)character).Identity, val2);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Inventory is full.", 0, 0);
					return true;
				}
				InventoryError val3 = InventoryContainerRuntimeService.Default.AddToStandardInventoryPage((IItemContainer)(object)character, num, val2);
				if ((int)val3 > 0)
				{
					shoppingBag.AddPlayerOffer(((IEntity)character).Identity, val2);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Could not return trade item. (" + ((object)(InventoryError)(ref val3)).ToString() + ")", 0, 0);
					return true;
				}
				InventoryContainerRuntimeService.Default.SendTradeWindowMoveToInventory(character, (IdentityType)108, instance, 111);
				AcknowledgeTradeAction(character, message);
				SendPlayerTradeAction(otherCharacter, (TradeAction)6, ((IEntity)character).Identity, message.Container);
				BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(character, ((IEntity)otherCharacter).Identity);
				BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(character, ((IEntity)character).Identity);
				InventoryContainerRuntimeService.Default.PersistCharacterInventory(character, "player trade remove");
				return true;
			}
		}
		return true;
	}

	private bool TrySetPlayerTradeCredits(ICharacter character, TemporaryBag shoppingBag, TradeMessage message)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerTrade(character, shoppingBag, out var otherCharacter))
		{
			return false;
		}
		int num = Math.Max(0, message.Param2);
		shoppingBag.SetPlayerTradeCredits(((IEntity)character).Identity, num);
		string[] array = new string[22];
		array[0] = "TRADE_CREDIT_SET char=";
		Identity val = ((IEntity)character).Identity;
		array[1] = ((Identity)(ref val)).ToString(true);
		array[2] = " name=";
		array[3] = ((INamedEntity)character).Name;
		array[4] = " other=";
		val = ((IEntity)otherCharacter).Identity;
		array[5] = ((Identity)(ref val)).ToString(true);
		array[6] = " action=";
		TradeAction action = message.Action;
		array[7] = ((object)(TradeAction)(ref action)).ToString();
		array[8] = " p1=";
		array[9] = message.Param1.ToString();
		array[10] = " p2=";
		array[11] = message.Param2.ToString();
		array[12] = " p3=";
		array[13] = message.Param3.ToString();
		array[14] = " p4=";
		array[15] = message.Param4.ToString();
		array[16] = " target=";
		val = message.Target;
		array[17] = ((Identity)(ref val)).ToString(true);
		array[18] = " container=";
		val = message.Container;
		array[19] = ((Identity)(ref val)).ToString(true);
		array[20] = " storedCredits=";
		array[21] = num.ToString();
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(array));
		SendPlayerTradeCredits(character, ((IEntity)character).Identity, num);
		SendPlayerTradeCredits(otherCharacter, ((IEntity)character).Identity, num);
		string[] obj = new string[6] { "Player trade credits owner=", null, null, null, null, null };
		val = ((IEntity)character).Identity;
		obj[1] = ((Identity)(ref val)).ToString(true);
		obj[2] = " other=";
		val = ((IEntity)otherCharacter).Identity;
		obj[3] = ((Identity)(ref val)).ToString(true);
		obj[4] = " credits=";
		obj[5] = num.ToString();
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		return true;
	}

	private bool TryEndPlayerTrade(ICharacter character, TemporaryBag shoppingBag, TradeMessage message)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerTrade(character, shoppingBag, out var otherCharacter))
		{
			return false;
		}
		if ((int)message.Action == 3)
		{
			if (!shoppingBag.MarkPlayerTradeAccepted(((IEntity)character).Identity))
			{
				SendPlayerTradeStatus(otherCharacter, (TradeAction)1, ((IEntity)otherCharacter).Identity);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Trade accepted. Waiting for other player.", 0, 0);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(otherCharacter, ((INamedEntity)character).Name + " accepted the trade.", 0, 0);
				return true;
			}
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
			ICharacter object2 = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
			if (@object == null || object2 == null)
			{
				((PooledObject)shoppingBag).Dispose();
				return true;
			}
			string playerTradeCompletionFailure = GetPlayerTradeCompletionFailure(@object, object2, shoppingBag);
			if (playerTradeCompletionFailure != null)
			{
				shoppingBag.ClearPlayerTradeAcceptances();
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(@object, "Trade failed: " + playerTradeCompletionFailure, 0, 0);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(object2, "Trade failed: " + playerTradeCompletionFailure, 0, 0);
				return true;
			}
			SendPlayerTradeStatus(@object, (TradeAction)3, ((IEntity)@object).Identity);
			SendPlayerTradeStatus(object2, (TradeAction)3, ((IEntity)object2).Identity);
			return true;
		}
		shoppingBag.MarkPlayerTradeEnded(((IEntity)character).Identity);
		if (!shoppingBag.IsPlayerTradeReady())
		{
			Identity identity = ((IEntity)character).Identity;
			string text = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)otherCharacter).Identity;
			LogUtil.Debug((DebugInfoDetail)32768, "Player trade End stored until both players confirm character=" + text + " partner=" + ((Identity)(ref identity)).ToString(true));
			return true;
		}
		if (!shoppingBag.IsPlayerTradeEnded())
		{
			SendPlayerTradeEndPrompt(otherCharacter, character);
			return true;
		}
		return CompletePlayerTrade(shoppingBag);
	}

	private bool CompletePlayerTrade(TemporaryBag shoppingBag)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
		ICharacter object2 = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
		if (@object == null || object2 == null)
		{
			((PooledObject)shoppingBag).Dispose();
			return true;
		}
		string playerTradeCompletionFailure = GetPlayerTradeCompletionFailure(@object, object2, shoppingBag);
		if (playerTradeCompletionFailure != null)
		{
			shoppingBag.ClearPlayerTradeAcceptances();
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(@object, "Trade failed: " + playerTradeCompletionFailure, 0, 0);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(object2, "Trade failed: " + playerTradeCompletionFailure, 0, 0);
			return true;
		}
		if (!shoppingBag.TryBeginPlayerTradeCompletion())
		{
			return true;
		}
		if (!TransferPlayerTradeCredits(@object, object2, shoppingBag))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(@object, "Trade failed: credit transfer could not be completed.", 0, 0);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(object2, "Trade failed: credit transfer could not be completed.", 0, 0);
			ReturnAllPlayerTradeOffers(shoppingBag, "player trade credit failure");
			((PooledObject)shoppingBag).Dispose();
			return true;
		}
		InventoryContainerRuntimeService.Default.TransferPlayerTradeOffers(@object, object2, shoppingBag);
		InventoryContainerRuntimeService.Default.TransferPlayerTradeOffers(object2, @object, shoppingBag);
		SendPlayerTradeCompleteClose(@object, object2);
		SendPlayerTradeCompleteClose(object2, @object);
		SendPlayerTradeSocialStatus(@object);
		SendPlayerTradeSocialStatus(object2);
		InventoryContainerRuntimeService.Default.PersistCharacterInventory(@object, "player trade complete");
		InventoryContainerRuntimeService.Default.PersistCharacterInventory(object2, "player trade complete");
		string[] obj = new string[6] { "Player trade completed shopper=", null, null, null, null, null };
		Identity identity = ((IEntity)@object).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = " vendor=";
		identity = ((IEntity)object2).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " bag=";
		identity = ((PooledObject)shoppingBag).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		((PooledObject)shoppingBag).Dispose();
		return true;
	}

	private bool TryDeclinePlayerTrade(ICharacter character, TemporaryBag shoppingBag)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerTrade(character, shoppingBag, out var otherCharacter))
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
		ICharacter object2 = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
		if (@object != null)
		{
			InventoryContainerRuntimeService.Default.ReturnPlayerTradeOffers(@object, shoppingBag);
			InventoryContainerRuntimeService.Default.PersistCharacterInventory(@object, "player trade decline");
		}
		if (object2 != null)
		{
			InventoryContainerRuntimeService.Default.ReturnPlayerTradeOffers(object2, shoppingBag);
			InventoryContainerRuntimeService.Default.PersistCharacterInventory(object2, "player trade decline");
		}
		SendPlayerTradeDeclineClose(character, otherCharacter);
		((PooledObject)shoppingBag).Dispose();
		return true;
	}

	private bool IsPlayerTrade(ICharacter character, TemporaryBag shoppingBag, out ICharacter otherCharacter)
	{
		otherCharacter = GetOtherPlayerTradeCharacter(character, shoppingBag);
		return otherCharacter != null && ((IDynel)character).Controller is PlayerController && ((IDynel)otherCharacter).Controller is PlayerController;
	}

	private void SendPlayerTradeStatus(ICharacter viewer, TradeAction action, Identity subject)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null)
		{
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeClose(subject, action, subject, subject), false);
		}
	}

	private void SendPlayerTradeEndPrompt(ICharacter viewer, ICharacter partner)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null && partner != null)
		{
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeClose(((IEntity)viewer).Identity, (TradeAction)1, ((IEntity)partner).Identity, ((IEntity)partner).Identity), false);
		}
	}

	private void SendPlayerTradeAction(ICharacter viewer, TradeAction action, Identity offerOwner, Identity source)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null && ((IDynel)viewer).Controller.Client != null)
		{
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeAction(viewer, action, offerOwner, source), false);
		}
	}

	private void SendPlayerTradeCredits(ICharacter viewer, Identity offerOwner, int credits)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null && ((IDynel)viewer).Controller.Client != null)
		{
			string[] obj = new string[12]
			{
				"TRADE_CREDIT_SEND viewer=", null, null, null, null, null, null, null, null, null,
				null, null
			};
			Identity identity = ((IEntity)viewer).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " name=";
			obj[3] = ((INamedEntity)viewer).Name;
			obj[4] = " offerOwner=";
			obj[5] = ((Identity)(ref offerOwner)).ToString(true);
			obj[6] = " action=";
			TradeAction val = (TradeAction)7;
			obj[7] = ((object)(TradeAction)(ref val)).ToString();
			obj[8] = " p1=0 p2=";
			obj[9] = credits.ToString();
			obj[10] = " p3=0 p4=0 storedCredits=";
			obj[11] = credits.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeCredits(offerOwner, credits), false);
		}
	}

	private void SendPlayerTradeItemRender(ICharacter viewer, IItem item, Identity source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		Item val = (Item)(object)((item is Item) ? item : null);
		if (viewer != null && val != null)
		{
			((IDynel)viewer).Send((MessageBody)new TemplateActionMessage
			{
				Identity = ((IEntity)viewer).Identity,
				ItemHighId = val.HighID,
				ItemLowId = val.LowID,
				Quality = val.Quality,
				Unknown = 0,
				Unknown1 = 1,
				Unknown2 = 85,
				Placement = source,
				Unknown3 = 0,
				Unknown4 = 0
			}, false);
		}
	}

	private void SendPlayerTradeCompleteClose(ICharacter viewer, ICharacter partner)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null && partner != null)
		{
			string[] obj = new string[14]
			{
				"TRADE_COMPLETE_SEND viewer=", null, null, null, null, null, null, null, null, null,
				null, null, null, null
			};
			Identity identity = ((IEntity)viewer).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " name=";
			obj[3] = ((INamedEntity)viewer).Name;
			obj[4] = " partner=";
			identity = ((IEntity)partner).Identity;
			obj[5] = ((Identity)(ref identity)).ToString(true);
			obj[6] = " frameIdentity=";
			identity = ((IEntity)viewer).Identity;
			obj[7] = ((Identity)(ref identity)).ToString(true);
			obj[8] = " action=";
			TradeAction val = (TradeAction)4;
			obj[9] = ((object)(TradeAction)(ref val)).ToString();
			obj[10] = " target=";
			identity = ((IEntity)partner).Identity;
			obj[11] = ((Identity)(ref identity)).ToString(true);
			obj[12] = " container=";
			identity = ((IEntity)partner).Identity;
			obj[13] = ((Identity)(ref identity)).ToString(true);
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeClose(((IEntity)viewer).Identity, (TradeAction)4, ((IEntity)partner).Identity, ((IEntity)partner).Identity), false);
			string[] obj2 = new string[14]
			{
				"TRADE_COMPLETE_SEND viewer=", null, null, null, null, null, null, null, null, null,
				null, null, null, null
			};
			identity = ((IEntity)viewer).Identity;
			obj2[1] = ((Identity)(ref identity)).ToString(true);
			obj2[2] = " name=";
			obj2[3] = ((INamedEntity)viewer).Name;
			obj2[4] = " partner=";
			identity = ((IEntity)partner).Identity;
			obj2[5] = ((Identity)(ref identity)).ToString(true);
			obj2[6] = " frameIdentity=";
			identity = ((IEntity)partner).Identity;
			obj2[7] = ((Identity)(ref identity)).ToString(true);
			obj2[8] = " action=";
			val = (TradeAction)4;
			obj2[9] = ((object)(TradeAction)(ref val)).ToString();
			obj2[10] = " target=";
			identity = ((IEntity)viewer).Identity;
			obj2[11] = ((Identity)(ref identity)).ToString(true);
			obj2[12] = " container=";
			identity = ((IEntity)viewer).Identity;
			obj2[13] = ((Identity)(ref identity)).ToString(true);
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj2));
			((AbstractMessageHandler<TradeMessage>)(object)this).Send(viewer, PlayerTradeClose(((IEntity)partner).Identity, (TradeAction)4, ((IEntity)viewer).Identity, ((IEntity)viewer).Identity), false);
		}
	}

	private void SendPlayerTradeDeclineClose(ICharacter first, ICharacter second)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		Send(first, (TradeAction)2, Identity.None, Identity.None);
		Send(second, (TradeAction)2, Identity.None, Identity.None);
		SendPlayerTradeInventoryInvalidation(first, second);
		SendPlayerTradeInventoryInvalidation(second, first);
		SendPlayerTradeSocialStatus(first);
		SendPlayerTradeSocialStatus(second);
	}

	private void SendPlayerTradeInventoryInvalidation(ICharacter viewer, ICharacter partner)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (viewer != null)
		{
			BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(viewer, ((IEntity)viewer).Identity);
			if (partner != null)
			{
				BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>.Default.Send(viewer, ((IEntity)partner).Identity);
			}
		}
	}

	private void SendPlayerTradeSocialStatus(ICharacter character)
	{
		if (character != null)
		{
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 521, 0u);
		}
	}

	private MessageDataFiller<TradeMessage> PlayerTradeClose(Identity messageIdentity, TradeAction tradeAction, Identity target, Identity container)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			x.Action = tradeAction;
			x.Container = container;
			x.Target = target;
			((N3Message)x).Identity = messageIdentity;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
		};
	}

	private MessageDataFiller<TradeMessage> PlayerTradeAction(ICharacter viewer, TradeAction action, Identity offerOwner, Identity source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = offerOwner;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
			x.Action = action;
			x.Target = offerOwner;
			x.Container = source;
		};
	}

	private MessageDataFiller<TradeMessage> PlayerTradeCredits(Identity offerOwner, int credits)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = offerOwner;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
			x.Action = (TradeAction)7;
			x.Param1 = 0;
			x.Param2 = credits;
			x.Param3 = 0;
			x.Param4 = 0;
		};
	}

	private void SendPlayerTradeItemDefinition(ICharacter viewer, TemporaryBag shoppingBag, Identity offerOwner, int tradeSlot)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		IInventoryPage playerOfferPage = shoppingBag.GetPlayerOfferPage(offerOwner);
		if (playerOfferPage != null)
		{
			IItem obj = playerOfferPage[tradeSlot];
			Item val = (Item)(object)((obj is Item) ? obj : null);
			if (val != null)
			{
				BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(viewer, val);
			}
		}
	}

	private void SendPlayerTradeInventoryUpdate(ICharacter viewer, TemporaryBag shoppingBag, Identity offerOwner, IdentityType displayContainerType)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		IInventoryPage playerOfferPage = shoppingBag.GetPlayerOfferPage(offerOwner);
		if (playerOfferPage != null && ((IDynel)viewer).Controller.Client != null)
		{
			InventoryEntry[] array = playerOfferPage.List().Select(CreatePlayerTradeInventoryEntry).ToArray();
			IZoneClient client = ((IDynel)viewer).Controller.Client;
			InventoryUpdateMessage val = new InventoryUpdateMessage
			{
				Identity = ((IEntity)viewer).Identity,
				Unknown = 1,
				NumberOfSlots = playerOfferPage.MaxSlots,
				Unknown1 = 2,
				Entries = array
			};
			Identity bagIdentity = default(Identity);
			((Identity)(ref bagIdentity)).Type = displayContainerType;
			Identity identity = ((PooledObject)shoppingBag).Identity;
			((Identity)(ref bagIdentity)).Instance = ((Identity)(ref identity)).Instance;
			val.BagIdentity = bagIdentity;
			val.SlotnumberInMainInventory = 0;
			val.Unknown2 = 1;
			client.SendCompressed((MessageBody)val);
			string[] obj = new string[10] { "Player trade inventory update viewer=", null, null, null, null, null, null, null, null, null };
			bagIdentity = ((IEntity)viewer).Identity;
			obj[1] = ((Identity)(ref bagIdentity)).ToString(true);
			obj[2] = " owner=";
			obj[3] = ((Identity)(ref offerOwner)).ToString(true);
			obj[4] = " bag=";
			obj[5] = ((object)(IdentityType)(ref displayContainerType)).ToString();
			obj[6] = ":";
			bagIdentity = ((PooledObject)shoppingBag).Identity;
			obj[7] = ((Identity)(ref bagIdentity)).Instance.ToString();
			obj[8] = " entries=";
			obj[9] = array.Length.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
		}
	}

	private static InventoryEntry CreatePlayerTradeInventoryEntry(KeyValuePair<int, IItem> item)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		return new InventoryEntry
		{
			Slotnumber = item.Key,
			UnknownFlags = 161,
			Unknown1 = (short)item.Value.MultipleCount,
			Identity = Identity.None,
			LowId = item.Value.LowID,
			HighId = item.Value.HighID,
			Quality = item.Value.Quality,
			Unknown2 = 0
		};
	}

	private string GetPlayerTradeCompletionFailure(ICharacter shopper, ICharacter vendor, TemporaryBag shoppingBag)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		int neededSlots = shoppingBag.GetPlayerOffers(((IEntity)shopper).Identity).Length;
		int neededSlots2 = shoppingBag.GetPlayerOffers(((IEntity)vendor).Identity).Length;
		if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(vendor, neededSlots))
		{
			return ((INamedEntity)vendor).Name + " does not have enough free inventory slots.";
		}
		if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(shopper, neededSlots2))
		{
			return ((INamedEntity)shopper).Name + " does not have enough free inventory slots.";
		}
		string playerTradeCreditFailure = GetPlayerTradeCreditFailure(shopper, shoppingBag);
		if (playerTradeCreditFailure != null)
		{
			return playerTradeCreditFailure;
		}
		return GetPlayerTradeCreditFailure(vendor, shoppingBag);
	}

	private string GetPlayerTradeCreditFailure(ICharacter character, TemporaryBag shoppingBag)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		int playerTradeCredits = shoppingBag.GetPlayerTradeCredits(((IEntity)character).Identity);
		int cash = GetCash(character);
		if (playerTradeCredits <= 0 || cash >= playerTradeCredits)
		{
			return null;
		}
		return ((INamedEntity)character).Name + " offered " + playerTradeCredits + " credits but only has " + cash + ".";
	}

	private bool TransferPlayerTradeCredits(ICharacter shopper, ICharacter vendor, TemporaryBag shoppingBag)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_037f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		int playerTradeCredits = shoppingBag.GetPlayerTradeCredits(((IEntity)shopper).Identity);
		int playerTradeCredits2 = shoppingBag.GetPlayerTradeCredits(((IEntity)vendor).Identity);
		if (playerTradeCredits <= 0 && playerTradeCredits2 <= 0)
		{
			return true;
		}
		int cash = GetCash(shopper);
		int cash2 = GetCash(vendor);
		long num = (long)cash + (long)cash2;
		Identity identity;
		if (playerTradeCredits > cash || playerTradeCredits2 > cash2)
		{
			string[] obj = new string[12]
			{
				"Player trade credits rejected during commit shopper=", null, null, null, null, null, null, null, null, null,
				null, null
			};
			identity = ((IEntity)shopper).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " vendor=";
			identity = ((IEntity)vendor).Identity;
			obj[3] = ((Identity)(ref identity)).ToString(true);
			obj[4] = " shopperCredits=";
			obj[5] = playerTradeCredits.ToString();
			obj[6] = " vendorCredits=";
			obj[7] = playerTradeCredits2.ToString();
			obj[8] = " shopperCash=";
			obj[9] = cash.ToString();
			obj[10] = " vendorCash=";
			obj[11] = cash2.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
			return false;
		}
		long num2 = (long)cash - (long)playerTradeCredits + playerTradeCredits2;
		long num3 = (long)cash2 - (long)playerTradeCredits2 + playerTradeCredits;
		if (num2 > 999999999 || num3 > 999999999 || num2 < 0 || num3 < 0)
		{
			string[] obj2 = new string[10] { "Player trade credits rejected because cash cap would be exceeded shopper=", null, null, null, null, null, null, null, null, null };
			identity = ((IEntity)shopper).Identity;
			obj2[1] = ((Identity)(ref identity)).ToString(true);
			obj2[2] = " vendor=";
			identity = ((IEntity)vendor).Identity;
			obj2[3] = ((Identity)(ref identity)).ToString(true);
			obj2[4] = " shopperFinalRaw=";
			obj2[5] = num2.ToString();
			obj2[6] = " vendorFinalRaw=";
			obj2[7] = num3.ToString();
			obj2[8] = " cap=";
			obj2[9] = 999999999.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj2));
			return false;
		}
		int num4 = CashStatRules.Clamp(num2);
		int num5 = CashStatRules.Clamp(num3);
		long num6 = (long)num4 + (long)num5;
		if (num6 != num)
		{
			string[] obj3 = new string[8] { "Player trade credits rejected because totals differ shopper=", null, null, null, null, null, null, null };
			identity = ((IEntity)shopper).Identity;
			obj3[1] = ((Identity)(ref identity)).ToString(true);
			obj3[2] = " vendor=";
			identity = ((IEntity)vendor).Identity;
			obj3[3] = ((Identity)(ref identity)).ToString(true);
			obj3[4] = " startingTotal=";
			obj3[5] = num.ToString();
			obj3[6] = " finalTotal=";
			obj3[7] = num6.ToString();
			LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj3));
			return false;
		}
		SetCash(shopper, num4);
		SetCash(vendor, num5);
		((IDatabaseObject)((IStats)shopper).Stats).Write();
		((IDatabaseObject)((IStats)vendor).Stats).Write();
		string[] obj4 = new string[16]
		{
			"Player trade credits committed shopper=", null, null, null, null, null, null, null, null, null,
			null, null, null, null, null, null
		};
		identity = ((IEntity)shopper).Identity;
		obj4[1] = ((Identity)(ref identity)).ToString(true);
		obj4[2] = " vendor=";
		identity = ((IEntity)vendor).Identity;
		obj4[3] = ((Identity)(ref identity)).ToString(true);
		obj4[4] = " shopperCredits=";
		obj4[5] = playerTradeCredits.ToString();
		obj4[6] = " vendorCredits=";
		obj4[7] = playerTradeCredits2.ToString();
		obj4[8] = " shopperCashBefore=";
		obj4[9] = cash.ToString();
		obj4[10] = " vendorCashBefore=";
		obj4[11] = cash2.ToString();
		obj4[12] = " shopperCashAfter=";
		obj4[13] = num4.ToString();
		obj4[14] = " vendorCashAfter=";
		obj4[15] = num5.ToString();
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj4));
		string[] obj5 = new string[20]
		{
			"TRADE_CREDIT_COMMIT shopper=", null, null, null, null, null, null, null, null, null,
			null, null, null, null, null, null, null, null, null, null
		};
		identity = ((IEntity)shopper).Identity;
		obj5[1] = ((Identity)(ref identity)).ToString(true);
		obj5[2] = " shopperName=";
		obj5[3] = ((INamedEntity)shopper).Name;
		obj5[4] = " vendor=";
		identity = ((IEntity)vendor).Identity;
		obj5[5] = ((Identity)(ref identity)).ToString(true);
		obj5[6] = " vendorName=";
		obj5[7] = ((INamedEntity)vendor).Name;
		obj5[8] = " shopperCredits=";
		obj5[9] = playerTradeCredits.ToString();
		obj5[10] = " vendorCredits=";
		obj5[11] = playerTradeCredits2.ToString();
		obj5[12] = " shopperCashBefore=";
		obj5[13] = cash.ToString();
		obj5[14] = " vendorCashBefore=";
		obj5[15] = cash2.ToString();
		obj5[16] = " shopperCashAfter=";
		obj5[17] = num4.ToString();
		obj5[18] = " vendorCashAfter=";
		obj5[19] = num5.ToString();
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj5));
		return true;
	}

	private int CalculateVendorBuyTotal(ICharacter shopper, Vendor vendor, IEnumerable<IItem> items)
	{
		int num = 0;
		foreach (IItem item in items)
		{
			int num2 = CalculateVendorBuyPrice(shopper, vendor, item);
			num = CashStatRules.Clamp((long)num + (long)num2);
		}
		return num;
	}

	private int CalculateVendorSellTotal(ICharacter shopper, Vendor vendor, IEnumerable<IItem> items)
	{
		int num = 0;
		foreach (IItem item in items)
		{
			int num2 = CalculateVendorSellPrice(shopper, vendor, item);
			num = CashStatRules.Clamp((long)num + (long)num2);
		}
		return num;
	}

	private int CalculateVendorBuyPrice(ICharacter shopper, Vendor vendor, IItem item)
	{
		int num = CalculateVendorItemValue(item);
		int vendorPricingSkillSteps = GetVendorPricingSkillSteps(shopper, vendor);
		int value = ((Dynel)vendor).Stats[(StatIds)427].Value;
		int num2 = Math.Max(0, 100 - vendorPricingSkillSteps);
		int num3 = Math.Max(0, (int)Math.Round((double)(num * value * num2) / 10000.0));
		LogVendorPrice("buy-from-vendor", shopper, vendor, item, num, value, vendorPricingSkillSteps, num3);
		return num3;
	}

	private int CalculateVendorSellPrice(ICharacter shopper, Vendor vendor, IItem item)
	{
		int num = CalculateVendorItemValue(item);
		int vendorPricingSkillSteps = GetVendorPricingSkillSteps(shopper, vendor);
		int value = ((Dynel)vendor).Stats[(StatIds)426].Value;
		int num2 = Math.Max(0, (int)Math.Floor((double)(num * value * (100 + vendorPricingSkillSteps)) / 10000.0));
		LogVendorPrice("sell-to-vendor", shopper, vendor, item, num, value, vendorPricingSkillSteps, num2);
		return num2;
	}

	private int GetVendorPricingSkillSteps(ICharacter shopper, Vendor vendor)
	{
		return Math.Max(0, ((IStats)shopper).Stats[(StatIds)161].Value / 40);
	}

	private int CalculateVendorItemValue(IItem item)
	{
		if (!ItemLoader.ItemList.TryGetValue(item.LowID, out var value) || !ItemLoader.ItemList.TryGetValue(item.HighID, out var value2))
		{
			return Math.Max(0, item.GetAttribute(74));
		}
		int quality = value.Quality;
		int quality2 = value2.Quality;
		int num = Math.Max(0, value.getItemAttribute(74));
		int num2 = Math.Max(0, value2.getItemAttribute(74));
		if (quality == quality2)
		{
			return num;
		}
		if (num2 == 0)
		{
			return num2;
		}
		double x = item.Quality - quality;
		double x2 = quality2 - quality;
		double a = (double)num + Math.Pow(x, 2.0) * (double)(num2 - num) / Math.Pow(x2, 2.0);
		return Math.Max(0, (int)Math.Round(a));
	}

	private void LogVendorPrice(string direction, ICharacter shopper, Vendor vendor, IItem item, int value, int modifier, int skillSteps, int price)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		string[] obj = new string[20]
		{
			"Vendor price ", direction, " shopper=", null, null, null, null, null, null, null,
			null, null, null, null, null, null, null, null, null, null
		};
		Identity identity = ((IEntity)shopper).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " vendor=";
		identity = ((PooledObject)vendor).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		obj[6] = " item=";
		obj[7] = item.LowID.ToString();
		obj[8] = "/";
		obj[9] = item.HighID.ToString();
		obj[10] = ":";
		obj[11] = item.Quality.ToString();
		obj[12] = " value=";
		obj[13] = value.ToString();
		obj[14] = " modifier=";
		obj[15] = modifier.ToString();
		obj[16] = " skillSteps=";
		obj[17] = skillSteps.ToString();
		obj[18] = " price=";
		obj[19] = price.ToString();
		LogUtil.Debug((DebugInfoDetail)32768, string.Concat(obj));
	}

	private static int GetCash(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)61].BaseValue;
		return CashStatRules.Clamp(baseValue);
	}

	private static void SetCash(ICharacter character, int cash)
	{
		((IStats)character).Stats[(StatIds)61].Set((uint)CashStatRules.Clamp(cash), false);
	}

	private void ReturnAllPlayerTradeOffers(TemporaryBag shoppingBag, string reason)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
		ICharacter object2 = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
		if (@object != null)
		{
			InventoryContainerRuntimeService.Default.ReturnPlayerTradeOffers(@object, shoppingBag);
			InventoryContainerRuntimeService.Default.PersistCharacterInventory(@object, reason);
		}
		if (object2 != null)
		{
			InventoryContainerRuntimeService.Default.ReturnPlayerTradeOffers(object2, shoppingBag);
			InventoryContainerRuntimeService.Default.PersistCharacterInventory(object2, reason);
		}
	}

	private ICharacter GetOtherPlayerTradeCharacter(ICharacter character, TemporaryBag shoppingBag)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || shoppingBag == null)
		{
			return null;
		}
		Identity identity = ((IEntity)character).Identity;
		Identity val = (((object)(Identity)(ref identity)).Equals((object)shoppingBag.Shopper) ? shoppingBag.Vendor : shoppingBag.Shopper);
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(val);
		if (@object != null && @object.ShoppingBag == shoppingBag)
		{
			return @object;
		}
		return null;
	}

	private MessageDataFiller<TradeMessage> AcknowledgeRemove(Identity identity, TradeMessage message)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = identity;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = message.Unknown1;
			x.Action = message.Action;
			x.Target = message.Target;
			x.Container = message.Container;
		};
	}

	private void Send(ICharacter character, TradeAction tradeAction, Identity identity1, Identity identity2)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(character, EndTrade(character, tradeAction, identity1, identity2), false);
	}

	private void SendVendorShopDeclineClose(ICharacter character)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		Send(character, (TradeAction)2, Identity.None, Identity.None);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 521, 4u);
		Playfield.ArmPostZoneCollisionGrace(character);
	}

	private MessageDataFiller<TradeMessage> EndTrade(ICharacter character, TradeAction tradeAction, Identity identity1, Identity identity2)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			x.Action = tradeAction;
			x.Container = identity2;
			x.Target = identity1;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
		};
	}

	private void AcknowledgeTradeAction(ICharacter character, TradeMessage message)
	{
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(character, AcknowledgeFiller(message), false);
	}

	private MessageDataFiller<TradeMessage> AcknowledgeFiller(TradeMessage message)
	{
		return delegate(TradeMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			x.Target = message.Target;
			x.Action = message.Action;
			x.Container = message.Container;
			x.Unknown1 = message.Unknown1;
			((N3Message)x).Unknown = ((N3Message)message).Unknown;
			((N3Message)x).Identity = ((N3Message)message).Identity;
		};
	}

	public void Send(ICharacter character, TemporaryBag tempBag)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(character, TemporaryBagHandle(character, tempBag.Shopper, tempBag.Vendor, ((PooledObject)tempBag).Identity), false);
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(character, TemporaryBagHandle(character, tempBag.Vendor, tempBag.Shopper, ((PooledObject)tempBag).Identity), false);
	}

	private MessageDataFiller<TradeMessage> TemporaryBagHandle(ICharacter character, Identity identity1, Identity identity2, Identity bagIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = identity1;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
			x.Action = (TradeAction)0;
			x.Target = identity2;
			x.Container = bagIdentity;
		};
	}

	public void Send(ICharacter character, Identity targetIdentity, Identity containerIdentity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<TradeMessage>)(object)this).Send(character, ShopTrade(character, targetIdentity, containerIdentity), false);
	}

	private MessageDataFiller<TradeMessage> ShopTrade(ICharacter character, Identity targetIdentity, Identity containerIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return delegate(TradeMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Container = containerIdentity;
			x.Target = targetIdentity;
			x.Unknown1 = 2;
			x.Action = (TradeAction)0;
		};
	}
}
