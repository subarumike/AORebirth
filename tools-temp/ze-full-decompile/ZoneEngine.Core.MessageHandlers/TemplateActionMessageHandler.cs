using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class TemplateActionMessageHandler : BaseMessageHandler<TemplateActionMessage, TemplateActionMessageHandler>
{
	private static MessageDataFiller<TemplateActionMessage> Filler(ICharacter character, Item item, int container, int placement)
	{
		return delegate(TemplateActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.ItemHighId = item.HighID;
			x.ItemLowId = item.LowID;
			x.Quality = item.Quality;
			Identity placement2 = default(Identity);
			((Identity)(ref placement2)).Type = (IdentityType)container;
			((Identity)(ref placement2)).Instance = placement;
			x.Placement = placement2;
			x.Unknown1 = 1;
			x.Unknown2 = 3;
		};
	}

	public void Send(ICharacter character, Item item, int container, int placement)
	{
		((AbstractMessageHandler<TemplateActionMessage>)(object)this).Send(character, Filler(character, item, container, placement), false);
	}
}
