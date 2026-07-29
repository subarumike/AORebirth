using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core.PacketHandlers;

public static class TradeSkillReceiver
{
	private static readonly List<TradeSkillInfo> TradeSkillInfos = new List<TradeSkillInfo>();

	public static string SuccessMessage(Item sourceItem, Item targetItem, Item newItem)
	{
		return $"You combined \"{TradeSkill.Instance.GetItemName(sourceItem.LowID, sourceItem.HighID, sourceItem.Quality)}\" with \"{TradeSkill.Instance.GetItemName(targetItem.LowID, targetItem.HighID, targetItem.Quality)}\" and the result is a quality level {newItem.Quality} \"{TradeSkill.Instance.GetItemName(newItem.LowID, newItem.HighID, newItem.Quality)}\".";
	}

	public static void TradeSkillBuildPressed(IZoneClient client, int quality)
	{
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Expected O, but got Unknown
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Invalid comparison between Unknown and I4
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Expected O, but got Unknown
		TradeSkillInfo tradeSkillSource = client.Controller.Character.TradeSkillSource;
		TradeSkillInfo tradeSkillTarget = client.Controller.Character.TradeSkillTarget;
		Item tradeSkillItem = InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, tradeSkillSource);
		Item tradeSkillItem2 = InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, tradeSkillTarget);
		if (tradeSkillItem == null || tradeSkillItem2 == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "It is not possible to assemble those two items. Maybe the order was wrong?", 0, 0);
			return;
		}
		TradeSkillEntry tradeSkillEntry = TradeSkill.Instance.GetTradeSkillEntry(tradeSkillItem.HighID, tradeSkillItem2.HighID);
		if (tradeSkillEntry != null)
		{
			int val = 1;
			if (ItemLoader.ItemList.ContainsKey(tradeSkillEntry.ResultHighId))
			{
				val = ItemLoader.ItemList[tradeSkillEntry.ResultHighId].Quality;
			}
			quality = Math.Min(quality, val);
			if (!WindowBuild(client, quality, tradeSkillEntry, tradeSkillItem, tradeSkillItem2))
			{
				return;
			}
			Item item = new Item(quality, tradeSkillEntry.ResultLowId, tradeSkillEntry.ResultHighId);
			InventoryError val2 = InventoryContainerRuntimeService.Default.AddTradeSkillResultItem(client.Controller.Character, item);
			if ((int)val2 == 0)
			{
				BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(client.Controller.Character, item);
				if ((tradeSkillEntry.DeleteFlag & 1) == 1)
				{
					InventoryContainerRuntimeService.Default.RemoveTradeSkillItem(client.Controller.Character, tradeSkillSource);
					BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(client.Controller.Character, tradeSkillSource.Container, tradeSkillSource.Placement);
				}
				if ((tradeSkillEntry.DeleteFlag & 2) == 2)
				{
					InventoryContainerRuntimeService.Default.RemoveTradeSkillItem(client.Controller.Character, tradeSkillTarget);
					BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(client.Controller.Character, tradeSkillTarget.Container, tradeSkillTarget.Placement);
				}
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, SuccessMessage(tradeSkillItem, tradeSkillItem2, new Item(quality, tradeSkillEntry.ResultLowId, tradeSkillEntry.ResultHighId)), 0, 0);
				IStat obj = ((IStats)client.Controller.Character).Stats[(StatIds)52];
				obj.Value += CalculateXP(quality, tradeSkillEntry);
			}
		}
		else
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "It is not possible to assemble those two items. Maybe the order was wrong?", 0, 0);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "No combination found!", 0, 0);
		}
	}

	public static void TradeSkillSourceChanged(IZoneClient client, int container, int placement)
	{
		if (container != 0 && placement != 0)
		{
			Item val = InventoryContainerRuntimeService.Default.SetTradeSkillSource(client.Controller.Character, container, placement);
			TradeSkillPacket.SendSource(client.Controller.Character, TradeSkill.Instance.SourceProcessesCount(val.HighID));
			TradeSkillChanged(client);
		}
		else
		{
			InventoryContainerRuntimeService.Default.ClearTradeSkillSource(client.Controller.Character);
		}
	}

	public static void TradeSkillTargetChanged(IZoneClient client, int container, int placement)
	{
		if (container != 0 && placement != 0)
		{
			Item val = InventoryContainerRuntimeService.Default.SetTradeSkillTarget(client.Controller.Character, container, placement);
			TradeSkillPacket.SendTarget(client.Controller.Character, TradeSkill.Instance.TargetProcessesCount(val.HighID));
			TradeSkillChanged(client);
		}
		else
		{
			InventoryContainerRuntimeService.Default.ClearTradeSkillTarget(client.Controller.Character);
		}
	}

	private static int CalculateXP(int quality, TradeSkillEntry ts)
	{
		int quality2 = ItemLoader.ItemList[ts.ResultLowId].Quality;
		int quality3 = ItemLoader.ItemList[ts.ResultHighId].Quality;
		if (quality3 == quality2)
		{
			return ts.MaxXP;
		}
		return (int)Math.Floor((double)((ts.MaxXP - ts.MinXP) / (quality3 - quality2)) * (double)(quality - quality2) + (double)ts.MinXP);
	}

	private static void TradeSkillChanged(IZoneClient client)
	{
		TradeSkillInfo tradeSkillSource = client.Controller.Character.TradeSkillSource;
		TradeSkillInfo tradeSkillTarget = client.Controller.Character.TradeSkillTarget;
		if (tradeSkillSource == null || tradeSkillTarget == null)
		{
			return;
		}
		Item tradeSkillItem = InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, tradeSkillSource);
		Item tradeSkillItem2 = InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, tradeSkillTarget);
		TradeSkillEntry tradeSkillEntry = TradeSkill.Instance.GetTradeSkillEntry(tradeSkillItem.HighID, tradeSkillItem2.HighID);
		if (tradeSkillEntry != null)
		{
			if (tradeSkillEntry.ValidateRange(tradeSkillItem.Quality, tradeSkillItem2.Quality))
			{
				foreach (TradeSkillSkill skill in tradeSkillEntry.Skills)
				{
					int num = (int)Math.Ceiling((decimal)skill.Percent / 100m * (decimal)tradeSkillItem2.Quality);
					if (num > ((IStats)client.Controller.Character).Stats[skill.StatId].Value)
					{
						TradeSkillPacket.SendRequirement(client.Controller.Character, skill.StatId, num);
					}
				}
				int num2 = 0;
				int val = 0;
				if (tradeSkillEntry.IsImplant)
				{
					if (tradeSkillItem2.Quality >= 250)
					{
						val = 5;
					}
					else if (tradeSkillItem2.Quality >= 201)
					{
						val = 4;
					}
					else if (tradeSkillItem2.Quality >= 150)
					{
						val = 3;
					}
					else if (tradeSkillItem2.Quality >= 100)
					{
						val = 2;
					}
					else if (tradeSkillItem2.Quality >= 50)
					{
						val = 1;
					}
				}
				foreach (TradeSkillSkill skill2 in tradeSkillEntry.Skills)
				{
					if (skill2.SkillPerBump != 0)
					{
						num2 = Math.Min(Convert.ToInt32(((decimal)((IStats)client.Controller.Character).Stats[skill2.StatId].Value - (decimal)skill2.Percent / 100m * (decimal)tradeSkillItem2.Quality) / (decimal)skill2.SkillPerBump), val);
					}
				}
				TradeSkillPacket.SendResult(client.Controller.Character, tradeSkillItem2.Quality, Math.Min(tradeSkillItem2.Quality + num2, ItemLoader.ItemList[tradeSkillEntry.ResultHighId].Quality), tradeSkillEntry.ResultLowId, tradeSkillEntry.ResultHighId);
			}
			else
			{
				TradeSkillPacket.SendOutOfRange(client.Controller.Character, Convert.ToInt32(Math.Round((double)tradeSkillItem2.Quality - (double)(tradeSkillEntry.QLRangePercent * tradeSkillItem2.Quality / 100))));
			}
		}
		else
		{
			TradeSkillPacket.SendNotTradeskill(client.Controller.Character);
		}
	}

	private static bool WindowBuild(IZoneClient client, int desiredQuality, TradeSkillEntry ts, Item sourceItem, Item targetItem)
	{
		if (ts.MinTargetQL < targetItem.Quality && ts.MinTargetQL != 0)
		{
			return false;
		}
		if (!ts.ValidateRange(sourceItem.Quality, targetItem.Quality))
		{
			return false;
		}
		foreach (TradeSkillSkill skill in ts.Skills)
		{
			if (((IStats)client.Controller.Character).Stats[skill.StatId].Value < Convert.ToInt32((decimal)skill.Percent / 100m * (decimal)targetItem.Quality))
			{
				return false;
			}
		}
		return true;
	}
}
