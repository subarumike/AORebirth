using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class RemovePetMessageHandler : BaseMessageHandler<RemovePetMessage, RemovePetMessageHandler>
{
	public void SendRemovePet(ICharacter owner, Identity petIdentity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<RemovePetMessage>)(object)this).Send(owner, RemovePetFiller(owner, petIdentity), false);
	}

	private MessageDataFiller<RemovePetMessage> RemovePetFiller(ICharacter owner, Identity petIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(RemovePetMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)owner).Identity;
			((N3Message)x).Unknown = 0;
			x.PetIdentity = petIdentity;
		};
	}
}
