using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandTerminate : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: /terminate — Yes confirmation suicides, moves uninsured XP to UnsavedXP pool (level under 220), dies at Insurance bind.", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		if (!(((IInstancedEntity)character).Playfield is Playfield playfield))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Terminate failed: not on a ZoneEngine playfield.", 0, 0);
			return;
		}
		bool flag = ((IStats)character).Stats[(StatIds)34].Value != 0 || ((IStats)character).Stats[(StatIds)27].Value <= 0;
		if (!flag)
		{
			CombatXpRuntimeService.ApplyDeathUninsuredXpLoss(character);
		}
		playfield.ForcePlayerDeath(character);
		if (flag)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Terminate: resent death state. Use Die when the corpse UI appears.", 0, 0);
		}
	}

	public override int GMLevelNeeded()
	{
		return 0;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "terminate" };
	}
}
