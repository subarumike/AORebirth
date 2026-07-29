using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class AddPetMessageHandler : BaseMessageHandler<AddPetMessage, AddPetMessageHandler>
{
	public void SendAddPet(ICharacter owner, Identity petIdentity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<AddPetMessage>)(object)this).Send(owner, AddPetFiller(owner, petIdentity), false);
	}

	private MessageDataFiller<AddPetMessage> AddPetFiller(ICharacter owner, Identity petIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(AddPetMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)owner).Identity;
			((N3Message)x).Unknown = 1;
			x.PetIdentity = petIdentity;
		};
	}
}
