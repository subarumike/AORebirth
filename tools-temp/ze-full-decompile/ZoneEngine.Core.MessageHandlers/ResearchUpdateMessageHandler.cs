using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ResearchUpdateMessageHandler : BaseMessageHandler<ResearchUpdateMessage, ResearchUpdateMessageHandler>
{
	public void Send(ICharacter character, ResearchUpdateEntry[] entries)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<ResearchUpdateMessage>)(object)this).Send(character, Filler(((IEntity)character).Identity, entries), false);
	}

	private MessageDataFiller<ResearchUpdateMessage> Filler(Identity identity, ResearchUpdateEntry[] entries)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(ResearchUpdateMessage x)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			x.Entries = entries;
			((N3Message)x).Identity = identity;
			x.Unknown1 = 1;
			((N3Message)x).Unknown = 1;
		};
	}
}
