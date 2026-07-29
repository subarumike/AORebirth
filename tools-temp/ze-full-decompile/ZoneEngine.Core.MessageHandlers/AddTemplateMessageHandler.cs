using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class AddTemplateMessageHandler : BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>
{
	public void Send(ICharacter character, Item item)
	{
		WeaponItemFullUpdate.SendWeaponDefinition(character, (IItem)(object)item);
		((AbstractMessageHandler<AddTemplateMessage>)(object)this).Send(character, AddItem(character, item), false);
	}

	private static MessageDataFiller<AddTemplateMessage> AddItem(ICharacter character, Item item)
	{
		return delegate(AddTemplateMessage x)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Unknown = 0;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.HighId = item.HighID;
			x.LowId = item.LowID;
			x.Quality = item.Quality;
			x.Count = item.MultipleCount;
		};
	}
}
