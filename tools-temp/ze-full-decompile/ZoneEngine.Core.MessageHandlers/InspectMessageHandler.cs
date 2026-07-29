using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class InspectMessageHandler : BaseMessageHandler<InspectMessage, InspectMessageHandler>
{
	public void Send(ICharacter viewer, ICharacter target)
	{
		if (viewer != null && target != null)
		{
			((AbstractMessageHandler<InspectMessage>)(object)this).Send(viewer, Fill(viewer, target), false);
		}
	}

	private static MessageDataFiller<InspectMessage> Fill(ICharacter viewer, ICharacter target)
	{
		InventorySlot[] items = FullCharacterMessageHandler.BuildEquipmentInspectSlots(target);
		return delegate(InspectMessage message)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)message).Identity = ((IEntity)viewer).Identity;
			((N3Message)message).Unknown = 0;
			message.Target = ((IEntity)target).Identity;
			message.Items = (InventorySlot[])(((object)items) ?? ((object)new InventorySlot[0]));
		};
	}
}
