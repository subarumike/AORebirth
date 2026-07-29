using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandGiveCredits : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(int));
		return AOChatCommand.CheckArgumentHelper(list, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Syntax: /command givecredits <amount>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		if (!int.TryParse(args[1], out var result) || result <= 0)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Credit amount must be a positive number.", 0, 0));
			return;
		}
		int num = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
		int num2 = CashStatRules.Clamp((long)num + (long)result);
		((IStats)character).Stats[(StatIds)61].Set((uint)num2, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num2);
		((IDatabaseObject)((IStats)character).Stats).Write();
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Credits added. Old: " + num + " New: " + num2, 0, 0));
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "givecredits" };
	}
}
