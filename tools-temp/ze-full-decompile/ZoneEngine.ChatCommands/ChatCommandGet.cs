using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Exceptions;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandGet : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(int));
		List<Type> list2 = list;
		bool flag = AOChatCommand.CheckArgumentHelper(list2, args);
		list2.Clear();
		list2.Add(typeof(string));
		return flag | AOChatCommand.CheckArgumentHelper(list2, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Syntax: /get <stat name|stat id>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref target)).Instance == 0)
		{
			target = ((IEntity)character).Identity;
		}
		if ((int)((Identity)(ref target)).Type != 50000)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Target must be player/monster/NPC", 0, 0));
			return;
		}
		Dynel val = (Dynel)((IInstancedEntity)character).Playfield.FindByIdentity(target);
		if (val != null)
		{
			Character val2 = (Character)val;
			if (!int.TryParse(args[1], out var result))
			{
				try
				{
					result = StatNamesDefaults.GetStatNumber(args[1]);
				}
				catch (Exception)
				{
					result = 1234567890;
				}
			}
			if (result == 1234567890)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Unknown Stat name " + args[1], 0, 0));
				return;
			}
			uint baseValue;
			int value;
			int trickle;
			int modifier;
			int percentageModifier;
			try
			{
				baseValue = ((Dynel)val2).Stats[result].BaseValue;
				value = ((Dynel)val2).Stats[result].Value;
				trickle = ((Dynel)val2).Stats[result].Trickle;
				modifier = ((Dynel)val2).Stats[result].Modifier;
				percentageModifier = ((Dynel)val2).Stats[result].PercentageModifier;
			}
			catch (StatDoesNotExistException)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Unknown Stat Id " + result, 0, 0));
				return;
			}
			string[] obj = new string[10]
			{
				"Character ",
				((Dynel)val2).Name,
				" (",
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			Identity identity = ((PooledObject)val2).Identity;
			obj[3] = ((Identity)(ref identity)).Instance.ToString();
			obj[4] = "): Stat ";
			obj[5] = StatNamesDefaults.GetStatName(result);
			obj[6] = " (";
			obj[7] = result.ToString();
			obj[8] = ") = ";
			obj[9] = baseValue.ToString();
			string text = string.Concat(obj);
			if (baseValue != ((Dynel)val2).Stats[result].Value)
			{
				text = text + "\r\nEffective value Stat " + StatNamesDefaults.GetStatName(result) + " (" + result + ") = " + value;
			}
			text = text + "\r\nTrickle: " + trickle + " Modificator: " + modifier + " Percentage: " + percentageModifier;
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, text, 0, 0));
		}
		else
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Unable to find target.", 0, 0));
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "get" };
	}
}
