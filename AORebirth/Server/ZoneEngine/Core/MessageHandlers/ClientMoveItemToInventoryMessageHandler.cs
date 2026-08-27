namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientMoveItemToInventoryMessageHandler :
        BaseMessageHandler<ClientMoveItemToInventoryMessage, ClientMoveItemToInventoryMessageHandler>
    {
        protected override void Read(ClientMoveItemToInventoryMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "ClientMoveItemToInventory received char={0} source={1} targetPlacement={2}",
                    character.Identity,
                    message.SourceContainer,
                    message.TargetPlacement));

            // Live loot clicks are ClientMoveItemToInventory (not inbound ContainerAddItem).
            // Treasure must win before corpse — same Backpack(handle<<16|slot) packing.
            if (NascenceDungeon1TreasureLootService.TryLootItem(
                client,
                message.SourceContainer,
                character.Identity,
                message.TargetPlacement))
            {
                return;
            }

            if (NascenceDungeon2TreasureLootService.TryLootItem(
                client,
                message.SourceContainer,
                character.Identity,
                message.TargetPlacement))
            {
                return;
            }

            if (character.Playfield.TryLootCorpseItem(
                character,
                message.SourceContainer,
                character.Identity,
                message.TargetPlacement))
            {
                return;
            }

            if (AORebirth.Core.Playfields.Playfield.ClaimsGeneratedMissionCorpseContainer(
                    character.Playfield,
                    message.SourceContainer)
                || (message.SourceContainer.Type == IdentityType.Corpse
                    && ZoneEngine.Core.Missions.MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                        character.Playfield.Identity.Instance)))
            {
                return;
            }

            InventoryContainerRuntimeService.Default.HandleClientMoveItemToInventory(client, message);
        }
    }
}
