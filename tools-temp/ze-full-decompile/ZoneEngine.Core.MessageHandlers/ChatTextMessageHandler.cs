using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ChatTextMessageHandler : BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>
{
	public void Send(ICharacter character, string text, byte unknown1 = 0, byte unknown2 = 0, int unknown3 = 0)
	{
		((AbstractMessageHandler<ChatTextMessage>)(object)this).Send(character, Filler(character, text, unknown1, unknown2, unknown3), false);
	}

	private static MessageDataFiller<ChatTextMessage> Filler(ICharacter character, string text, byte unknown1 = 0, byte unknown2 = 0, int unknown3 = 0)
	{
		return delegate(ChatTextMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Text = text;
			x.Unknown1 = unknown1;
			x.Unknown2 = unknown2;
			x.Unknown3 = unknown3;
		};
	}

	public ChatTextMessage Create(ICharacter character, string text, byte unknown1 = 0, byte unknown2 = 0, int unknown3 = 0)
	{
		return ((AbstractMessageHandler<ChatTextMessage>)(object)this).Create(character, Filler(character, text, unknown1, unknown2, unknown3));
	}

	public IMSendAOtomationMessageBodyToClient CreateIM(ICharacter character, string text, byte unknown1 = 0, byte unknown2 = 0, int unknown3 = 0)
	{
		return new IMSendAOtomationMessageBodyToClient
		{
			Body = (MessageBody)(object)((AbstractMessageHandler<ChatTextMessage>)(object)this).Create(character, Filler(character, text.Replace("<", "&lt;").Replace(">", "&gt;"), unknown1, unknown2, unknown3)),
			client = ((IDynel)character).Controller.Client
		};
	}
}
