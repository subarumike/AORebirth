using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ClientMoveItemToInventoryMessageHandler : BaseMessageHandler<ClientMoveItemToInventoryMessage, ClientMoveItemToInventoryMessageHandler>
{
	protected override void Read(ClientMoveItemToInventoryMessage message, IZoneClient client)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		LogUtil.Debug((DebugInfoDetail)512, $"ClientMoveItemToInventory received char={((IEntity)character).Identity} source={message.SourceContainer} targetPlacement={message.TargetPlacement}");
		if (!((IInstancedEntity)character).Playfield.TryLootCorpseItem(character, message.SourceContainer, ((IEntity)character).Identity, message.TargetPlacement))
		{
			InventoryContainerRuntimeService.Default.HandleClientMoveItemToInventory(client, message);
		}
	}
}
