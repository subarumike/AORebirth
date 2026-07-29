using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ContainerAddItemMessageHandler : BaseMessageHandler<ContainerAddItemMessage, ContainerAddItemMessageHandler>
{
	protected override void Read(ContainerAddItemMessage message, IZoneClient client)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!((IInstancedEntity)client.Controller.Character).Playfield.TryLootCorpseItem(client.Controller.Character, message.SourceContainer, message.Target, message.TargetPlacement))
		{
			InventoryContainerRuntimeService.Default.HandleContainerAddItem(client, message);
		}
	}

	public void Send(ICharacter character, Identity sourceContainer, int slot)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<ContainerAddItemMessage>)(object)this).Send(character, FillContainerAddItem(character, sourceContainer, slot), false);
	}

	private MessageDataFiller<ContainerAddItemMessage> FillContainerAddItem(ICharacter character, Identity sourceContainer, int slot)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(ContainerAddItemMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.SourceContainer = sourceContainer;
			x.TargetPlacement = slot;
			x.Target = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
		};
	}
}
