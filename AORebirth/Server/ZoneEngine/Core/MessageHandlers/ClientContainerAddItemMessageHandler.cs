namespace ZoneEngine.Core.MessageHandlers
{
    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientContainerAddItemMessageHandler :
        BaseMessageHandler<ClientContainerAddItemMessage, ClientContainerAddItemMessageHandler>
    {
        protected override void Read(ClientContainerAddItemMessage message, IZoneClient client)
        {
            ICharacter character = client != null && client.Controller != null
                ? client.Controller.Character
                : null;

            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.TryMoveInventoryItemToBackpack(character, message))
            {
                return;
            }

            if (!this.IsInventoryToBankDeposit(message))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "Unhandled ClientContainerAddItem char={0} source={1} target={2}",
                        character.Identity,
                        message.Source,
                        message.Target));
                return;
            }

            if (message.Target.Instance != character.Identity.Instance)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit for mismatched target char={0} target={1} source={2}",
                        character.Identity,
                        message.Target,
                        message.Source));
                return;
            }

            IInventoryPage inventoryPage;
            IInventoryPage bankPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage)
                || !character.BaseInventory.Pages.TryGetValue((int)IdentityType.Bank, out bankPage))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit because inventory pages are missing char={0}",
                        character.Identity));
                return;
            }

            int sourceSlot = message.Source.Instance;
            if (!inventoryPage.ValidSlot(sourceSlot))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit for invalid source slot char={0} source={1}",
                        character.Identity,
                        message.Source));
                return;
            }

            IItem item = inventoryPage[sourceSlot];
            if (item == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit because source slot is empty char={0} source={1}",
                        character.Identity,
                        message.Source));
                return;
            }

            int bankSlot = bankPage.FindFreeSlot();
            if (bankSlot < 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit because bank is full char={0} source={1}",
                        character.Identity,
                        message.Source));
                return;
            }

            try
            {
                InventoryError addError = bankPage.Add(bankSlot, item);
                if (addError != InventoryError.OK)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "Rejected ClientContainerAddItem bank deposit add failed char={0} source={1} bankSlot={2} error={3}",
                            character.Identity,
                            message.Source,
                            bankSlot,
                            addError));
                    return;
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit add threw char={0} source={1} bankSlot={2} error={3}",
                        character.Identity,
                        message.Source,
                        bankSlot,
                        exception.Message));
                return;
            }

            try
            {
                inventoryPage.Remove(sourceSlot);
            }
            catch (Exception exception)
            {
                this.TryRemoveBankRollback(bankPage, bankSlot);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem bank deposit remove source threw char={0} source={1} bankSlot={2} error={3}",
                        character.Identity,
                        message.Source,
                        bankSlot,
                        exception.Message));
                return;
            }

            character.Send(
                new ContainerAddItemMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        SourceContainer = message.Source,
                        Target = message.Target,
                        TargetPlacement = bankSlot
                    });

            character.BaseInventory.Write();
            LogUtil.Debug(
                DebugInfoDetail.Database,
                string.Format(
                    "Persisted inventory after ClientContainerAddItem bank deposit char={0} source={1} bankSlot={2}",
                    character.Identity,
                    message.Source,
                    bankSlot));
        }

        private bool IsInventoryToBankDeposit(ClientContainerAddItemMessage message)
        {
            return message.Source.Type == IdentityType.Inventory
                   && message.Target.Type == IdentityType.IncomingTradeWindow;
        }

        private void TryRemoveBankRollback(IInventoryPage bankPage, int bankSlot)
        {
            try
            {
                if (bankPage[bankSlot] != null)
                {
                    bankPage.Remove(bankSlot);
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientContainerAddItem bank deposit rollback failed bankSlot={0} error={1}",
                        bankSlot,
                        exception.Message));
            }
        }

    }
}
