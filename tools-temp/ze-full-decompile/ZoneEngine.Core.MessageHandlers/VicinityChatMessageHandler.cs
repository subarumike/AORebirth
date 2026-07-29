using System.Collections.Generic;
using System.Linq;
using AORebirth.Communication.Messages;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class VicinityChatMessageHandler : BaseMessageHandler<TextMessage, VicinityChatMessageHandler>
{
	protected override void Read(TextMessage message, IZoneClient client)
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected I4, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Expected I4, but got Unknown
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Expected O, but got Unknown
		if (message.Message.Text.StartsWith("."))
		{
			MessageWrapper<ChatCmdMessage> val = new MessageWrapper<ChatCmdMessage>();
			val.Client = client;
			val.Message = null;
			ChatCmdMessage val2 = new ChatCmdMessage();
			val2.Command = message.Message.Text.TrimStart('.');
			((N3Message)val2).Identity = ((IEntity)client.Controller.Character).Identity;
			val2.Target = ((ITargetingEntity)client.Controller.Character).SelectedTarget;
			val.MessageBody = val2;
			MessageWrapper<ChatCmdMessage> val3 = val;
			((AbstractMessageHandler<ChatCmdMessage>)(object)BaseMessageHandler<ChatCmdMessage, ChatCmdMessageHandler>.Default).Receive(val3);
			return;
		}
		ICharacter character = client.Controller.Character;
		IPlayfield playfield = ((IInstancedEntity)character).Playfield;
		float num = 0f;
		switch ((int)message.Message.Type)
		{
		case 1:
			num = 1.5f;
			break;
		case 0:
			num = 10f;
			break;
		case 2:
			num = 60f;
			break;
		}
		List<IDynel> source = playfield.FindInRange((IDynel)(object)character, num);
		VicinityChatMessage val4 = new VicinityChatMessage
		{
			CharacterIds = source.Select(delegate(IDynel x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				Identity identity2 = ((IEntity)x).Identity;
				return ((Identity)(ref identity2)).Instance;
			}).ToList(),
			MessageType = (int)message.Message.Type,
			Text = message.Message.Text
		};
		Identity identity = ((IEntity)character).Identity;
		val4.SenderId = ((Identity)(ref identity)).Instance;
		VicinityChatMessage val5 = val4;
		Program.ISComClient.Send((MessageBase)(object)val5);
	}
}
