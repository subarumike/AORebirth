using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class InventoryUpdatedMessageHandler : BaseMessageHandler<InventoryUpdatedMessage, InventoryUpdatedMessageHandler>
{
	public void Send(ICharacter character, Identity shopIdentity)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<InventoryUpdatedMessage>)(object)this).Send(character, Filler(shopIdentity), false);
	}

	private MessageDataFiller<InventoryUpdatedMessage> Filler(Identity shopIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(InventoryUpdatedMessage x)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			x.Unknown1 = 5;
			((N3Message)x).Identity = shopIdentity;
		};
	}
}
