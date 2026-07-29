using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
internal class KnuBotAppendTextMessageHandler : BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>
{
	public void Send(ICharacter character, Identity knubotTarget, string text)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Send(character, knubotTarget, text, 0);
	}

	public void Send(ICharacter character, Identity knubotTarget, string text, int unknown2)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotAppendTextMessage>)(object)this).Send(character, KnuBotAppendText(character, knubotTarget, text, unknown2), false);
	}

	private MessageDataFiller<KnuBotAppendTextMessage> KnuBotAppendText(ICharacter character, Identity knubotTarget, string text, int unknown2)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotAppendTextMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Target = knubotTarget;
			x.Text = text;
			x.Unknown1 = 2;
			x.Unknown2 = unknown2;
		};
	}
}
