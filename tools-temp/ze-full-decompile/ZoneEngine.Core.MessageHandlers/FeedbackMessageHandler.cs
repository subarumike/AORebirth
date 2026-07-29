using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class FeedbackMessageHandler : BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>
{
	public void Send(ICharacter character, int categoryId, int messageId)
	{
		CombatXpRuntimeService.LogXpWireFeedbackOutbound("FeedbackMessageHandler", "feedback-send", character, categoryId, messageId);
		((AbstractMessageHandler<FeedbackMessage>)(object)this).Send(character, Filler(character, categoryId, messageId), false);
	}

	private static MessageDataFiller<FeedbackMessage> Filler(ICharacter character, int categoryId, int messageId)
	{
		return delegate(FeedbackMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 1;
			x.Unknown1 = 0;
			x.CategoryId = categoryId;
			x.MessageId = messageId;
		};
	}
}
