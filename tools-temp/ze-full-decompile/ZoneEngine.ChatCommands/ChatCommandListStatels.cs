using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class ChatCommandListStatels : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Lists all extracted statels in this playfield", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected I4, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		List<StatelData> statels = pFData[((Identity)(ref identity)).Instance].Statels;
		List<MessageBody> list = new List<MessageBody>();
		foreach (StatelData item in statels)
		{
			ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
			identity = item.Identity;
			string text = ((int)((Identity)(ref identity)).Type).ToString("X8");
			identity = item.Identity;
			list.Add((MessageBody)(object)@default.Create(character, text + ":" + ((Identity)(ref identity)).Instance.ToString("X8"), 0, 0));
		}
		((IInstancedEntity)character).Playfield.Publish((object)Bulk.CreateIM(((IDynel)character).Controller.Client, list.ToArray()));
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "statels", "liststatels" };
	}
}
