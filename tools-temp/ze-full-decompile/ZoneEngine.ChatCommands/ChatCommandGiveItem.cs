using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandGiveItem : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(int));
		list.Add(typeof(int));
		return AOChatCommand.CheckArgumentHelper(list, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: Select target and /command giveitem id ql\r\nIt doesn't matter if high or low id is given", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Invalid comparison between Unknown and I4
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Expected O, but got Unknown
		IInstancedEntity val = null;
		if ((val = ((IInstancedEntity)character).Playfield.FindByIdentity(target)) == null)
		{
			val = (IInstancedEntity)(object)character;
		}
		IItemContainer val2 = (IItemContainer)(object)((val is IItemContainer) ? val : null);
		if (val2 != null)
		{
			if (!int.TryParse(args[1], out var result))
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "LowId is no number", 0, 0));
				return;
			}
			if (!int.TryParse(args[2], out var result2))
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "QualityLevel is no number", 0, 0));
				return;
			}
			int num = result;
			result = ItemLoader.ItemList[result].GetLowId(result2);
			int num2;
			if (result != -1)
			{
				num2 = ItemLoader.ItemList[result].GetHighId(result2);
			}
			else
			{
				result = num;
				num2 = result;
			}
			Item val3 = new Item(result2, result, num2);
			if (ItemLoader.ItemList[result].IsStackable())
			{
				val3.MultipleCount = ItemLoader.ItemList[result].getItemAttribute(212);
			}
			InventoryError val4 = val2.BaseInventory.TryAdd((IItem)(object)val3);
			if ((int)val4 > 0)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Could not add to inventory. (" + ((object)(InventoryError)(ref val4)).ToString() + ")", 0, 0));
				return;
			}
			val2.BaseInventory.Write();
			if (val is Character)
			{
				BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send((ICharacter)val, val3);
			}
		}
		else
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Target has no Inventory.", 0, 0));
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		List<string> list = new List<string>();
		list.Add("giveitem");
		return list;
	}
}
