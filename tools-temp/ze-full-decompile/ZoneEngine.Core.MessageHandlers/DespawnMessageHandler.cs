using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class DespawnMessageHandler : BaseMessageHandler<DespawnMessage, DespawnMessageHandler>
{
	public void Send(ICharacter character, Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<DespawnMessage>)(object)this).Send(character, Filler(identity), false);
	}

	private static MessageDataFiller<DespawnMessage> Filler(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(DespawnMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = identity;
			((N3Message)x).Unknown = 1;
		};
	}

	public DespawnMessage Create(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return ((AbstractMessageHandler<DespawnMessage>)(object)this).Create((ICharacter)null, Filler(identity));
	}
}
