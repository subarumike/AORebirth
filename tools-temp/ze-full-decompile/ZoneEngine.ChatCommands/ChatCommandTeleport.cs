using System;
using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class ChatCommandTeleport : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(float));
		list.Add(typeof(float));
		list.Add(typeof(int));
		List<Type> list2 = list;
		bool flag = AOChatCommand.CheckArgumentHelper(list2, args);
		list2.Clear();
		list2.Add(typeof(float));
		list2.Add(typeof(float));
		list2.Add(typeof(string));
		list2.Add(typeof(float));
		list2.Add(typeof(int));
		return flag | AOChatCommand.CheckArgumentHelper(list2, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Teleports you\r\nUsage: /tp [float] [float] [int] (X, Z, Playfield)\r\nOr:    /tp [float] [float] y [float] [int] (X, Z, Y, Playfield)", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Expected O, but got Unknown
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Expected O, but got Unknown
		List<Type> list = new List<Type>();
		list.Add(typeof(float));
		list.Add(typeof(float));
		list.Add(typeof(int));
		List<Type> list2 = list;
		Coordinate val = new Coordinate();
		Identity val2 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int num = ((Identity)(ref val2)).Instance;
		if (AOChatCommand.CheckArgumentHelper(list2, args))
		{
			val = new Coordinate(float.Parse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture), ((IDynel)character).Coordinates().y, float.Parse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture));
			num = int.Parse(args[3]);
		}
		list2.Clear();
		list2.Add(typeof(float));
		list2.Add(typeof(float));
		list2.Add(typeof(string));
		list2.Add(typeof(float));
		list2.Add(typeof(int));
		if (AOChatCommand.CheckArgumentHelper(list2, args))
		{
			val = new Coordinate(float.Parse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture), float.Parse(args[4], NumberStyles.Any, CultureInfo.InvariantCulture), float.Parse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture));
			num = int.Parse(args[5]);
		}
		if (!Playfields.ValidPlayfield(num))
		{
			BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(character, 110, 188845972);
			return;
		}
		IPlayfield playfield = ((IInstancedEntity)character).Playfield;
		Character val3 = (Character)character;
		Coordinate obj = val;
		Quaternion heading = ((IDynel)character).Heading;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)51101;
		((Identity)(ref val2)).Instance = num;
		playfield.Teleport((Dynel)val3, obj, (IQuaternion)(object)heading, val2);
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "tp", "teleport" };
	}
}
