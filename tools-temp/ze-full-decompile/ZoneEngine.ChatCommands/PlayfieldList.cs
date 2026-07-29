using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.ChatCommands;

public class PlayfieldList : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Lists all playfields and their id's", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<Identity, string> dictionary = ((Playfield)(object)((IInstancedEntity)character).Playfield).ListAvailablePlayfields();
		List<MessageBody> list = new List<MessageBody>();
		foreach (KeyValuePair<Identity, string> item in dictionary)
		{
			ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
			Identity key = item.Key;
			list.Add((MessageBody)(object)@default.Create(character, ((Identity)(ref key)).Instance.ToString().PadLeft(8) + ": " + item.Value, 0, 0));
		}
		((IInstancedEntity)character).Playfield.Publish((object)Bulk.CreateIM(((IDynel)character).Controller.Client, list.ToArray()));
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "pflist", "playfieldlist" };
	}
}
