using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandSet : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(int));
		list.Add(typeof(uint));
		bool flag = AOChatCommand.CheckArgumentHelper(list, args);
		list.Clear();
		list.Add(typeof(string));
		list.Add(typeof(uint));
		return flag | AOChatCommand.CheckArgumentHelper(list, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Syntax: /set <stat name|stat id> <new stat value>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref target)).Instance == 0)
		{
			Identity identity = ((IEntity)character).Identity;
			((Identity)(ref target)).Type = ((Identity)(ref identity)).Type;
			identity = ((IEntity)character).Identity;
			((Identity)(ref target)).Instance = ((Identity)(ref identity)).Instance;
		}
		int result = 1234567890;
		if (!int.TryParse(args[1], out result))
		{
			try
			{
				result = StatNamesDefaults.GetStatNumber(args[1].ToLower());
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
		uint value = 1234567890u;
		try
		{
			value = (uint)int.Parse(args[2]);
		}
		catch
		{
			try
			{
				value = uint.Parse(args[2]);
			}
			catch (FormatException)
			{
			}
			catch (OverflowException)
			{
			}
		}
		Character @object = Pool.Instance.GetObject<Character>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
		if (@object != null)
		{
			uint baseValue;
			try
			{
				baseValue = ((Dynel)@object).Stats[result].BaseValue;
				((Dynel)@object).Stats[result].Value = (int)value;
			}
			catch
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Unknown StatId " + result, 0, 0));
				return;
			}
			string text = string.Empty;
			if (@object != null)
			{
				text = ((INamedEntity)@object).Name + " ";
			}
			((Dynel)@object).Controller.SendChangedStats();
			string[] obj3 = new string[11]
			{
				"Dynel ", text, "(", null, null, null, null, null, null, null,
				null
			};
			IdentityType type = ((Identity)(ref target)).Type;
			obj3[3] = ((object)(IdentityType)(ref type)).ToString();
			obj3[4] = ":";
			obj3[5] = ((Identity)(ref target)).Instance.ToString();
			obj3[6] = "): Stat ";
			obj3[7] = StatNamesDefaults.GetStatName(result);
			obj3[8] = " (";
			obj3[9] = result.ToString();
			obj3[10] = ") =";
			string text2 = string.Concat(obj3);
			text2 = text2 + " Old: " + baseValue;
			text2 = text2 + " New: " + value;
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, text2, 0, 0));
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		List<string> list = new List<string>();
		list.Add("set");
		return list;
	}
}
