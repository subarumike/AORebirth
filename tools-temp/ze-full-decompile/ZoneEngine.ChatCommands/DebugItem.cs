using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class DebugItem : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(int));
		return AOChatCommand.CheckArgumentHelper(list, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Syntax: .debugitem <itemid>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		if (ItemLoader.ItemList.ContainsKey(int.Parse(args[1])))
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, ItemLoader.ItemList[int.Parse(args[1])].DebugString(), 0, 0));
		}
		else
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "No item with id " + args[1], 0, 0));
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "debugitem" };
	}
}
