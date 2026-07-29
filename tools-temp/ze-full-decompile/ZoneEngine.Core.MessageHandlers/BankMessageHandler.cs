using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class BankMessageHandler : BaseMessageHandler<BankMessage, BankMessageHandler>
{
	public void Send(ICharacter character)
	{
		((AbstractMessageHandler<BankMessage>)(object)this).Send(character, FillBankMessage(character), false);
	}

	private static MessageDataFiller<BankMessage> FillBankMessage(ICharacter character)
	{
		return delegate(BankMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.BankSlots = InventoryContainerRuntimeService.Default.ResolveBankSlots(character);
		};
	}
}
