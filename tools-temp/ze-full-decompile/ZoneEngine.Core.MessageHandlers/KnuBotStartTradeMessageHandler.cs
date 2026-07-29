using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
internal class KnuBotStartTradeMessageHandler : BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>
{
	public void Send(ICharacter character, Identity knubotTarget, string message, int itemSlots)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotStartTradeMessage>)(object)this).Send(character, StartTrade(character, knubotTarget, message, itemSlots), false);
	}

	private MessageDataFiller<KnuBotStartTradeMessage> StartTrade(ICharacter character, Identity knubotTarget, string message, int itemSlots)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotStartTradeMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Message = message;
			x.NumberOfItemSlotsInTradeWindow = itemSlots;
			x.Target = knubotTarget;
			x.Unknown1 = 2;
		};
	}
}
