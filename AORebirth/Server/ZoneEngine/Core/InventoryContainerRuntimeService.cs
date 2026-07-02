namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public sealed class InventoryContainerRuntimeService
    {
        public static readonly InventoryContainerRuntimeService Default = new InventoryContainerRuntimeService();

        private InventoryContainerRuntimeService()
        {
        }

        public void OpenBank(ICharacter character)
        {
            BankMessageHandler.Default.Send(character);
        }

        public BankSlot[] ResolveBankSlots(ICharacter character)
        {
            return character.BaseInventory.Pages[(int)IdentityType.Bank].ToInventoryArray();
        }

        public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
            {
                case InventoryContainerInteractionRouteMode.InventoryItem:
                    client.Controller.UseItem(target);
                    GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    return true;

                case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
                    if (client.Controller.TryUseBackpackContainer(target))
                    {
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;

                case InventoryContainerInteractionRouteMode.BackpackContainer:
                    IInventoryPage backpackPage;
                    if (client.Controller.Character.BaseInventory.TryGetBackpackPage(target, out backpackPage))
                    {
                        BackpackContainerActionMessageHandler.Default.SendClose(client.Controller.Character, target);
                        client.Controller.Character.BaseInventory.MarkBackpackClosed(target);
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;
            }

            return false;
        }

        public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            IItem item =
                Pool.Instance.GetObject<IInventoryPage>(
                    new Identity()
                    {
                        Type = (IdentityType)client.Controller.Character.Identity.Instance,
                        Instance = (int)message.Target[0].Type
                    })[message.Target[0].Instance];
            client.Controller.Character.Stats[StatIds.secondaryitemtemplate].Value = item.LowID;
            //client.Controller.Character.Stats[StatIds.secondaryitemtype]
            if (Pool.Instance.Contains(message.Target[1]))
            {
                StaticDynel temp =
                    Pool.Instance.GetObject<StaticDynel>(
                        client.Controller.Character.Playfield.Identity,
                        message.Target[1]);
                if (temp != null)
                {
                    Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                    if (ev != null)
                    {
                        ev.Perform(client.Controller.Character, temp);
                    }
                }
            }
            else
            {
                client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);
            }

            return true;
        }

        public bool TryMoveBackpackItemToInventory(ICharacter character, ClientMoveItemToInventoryMessage message)
        {
            if (message.SourceContainer.Type != IdentityType.Backpack)
            {
                return false;
            }

            int handle = DecodeBackpackHandle(message.SourceContainer);
            int fromPlacement = DecodeBackpackSlot(message.SourceContainer);

            IInventoryPage backpackPage;
            if (!character.BaseInventory.TryGetBackpackPageByHandle(handle, out backpackPage))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move because handle is unknown char={0} source={1} handle={2} targetPlacement={3}",
                        character.Identity,
                        message.SourceContainer,
                        handle,
                        message.TargetPlacement));
                return true;
            }

            IItem itemFrom = backpackPage[fromPlacement];
            if (itemFrom == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move because source slot is empty char={0} source={1} slot={2} targetPlacement={3}",
                        character.Identity,
                        message.SourceContainer,
                        fromPlacement,
                        message.TargetPlacement));
                return true;
            }

            IInventoryPage inventoryPage;
            IInventoryPage receivingPage = this.GetTargetPage(character, message.TargetPlacement);
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage)
                || receivingPage == null
                || !object.ReferenceEquals(receivingPage, inventoryPage))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move for non-inventory target char={0} source={1} targetPlacement={2}",
                        character.Identity,
                        message.SourceContainer,
                        message.TargetPlacement));
                return true;
            }

            int toPlacement = message.TargetPlacement;
            if (toPlacement == (int)IdentityType.TradeWindow)
            {
                toPlacement = receivingPage.FindFreeSlot();
            }

            if (toPlacement < 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move because inventory is full char={0} source={1} targetPlacement={2}",
                        character.Identity,
                        message.SourceContainer,
                        message.TargetPlacement));
                return true;
            }

            try
            {
                InventoryError addError = receivingPage.Add(toPlacement, itemFrom);
                if (addError != InventoryError.OK)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "Rejected ClientMoveItemToInventory backpack move add failed char={0} source={1} targetPlacement={2} resolvedTarget={3} error={4}",
                            character.Identity,
                            message.SourceContainer,
                            message.TargetPlacement,
                            toPlacement,
                            addError));
                    return true;
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move add threw char={0} source={1} targetPlacement={2} resolvedTarget={3} error={4}",
                        character.Identity,
                        message.SourceContainer,
                        message.TargetPlacement,
                        toPlacement,
                        exception.Message));
                return true;
            }

            try
            {
                backpackPage.Remove(fromPlacement);
            }
            catch (Exception exception)
            {
                this.TryRemoveInventoryRollback(receivingPage, toPlacement);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientMoveItemToInventory backpack move remove source threw char={0} source={1} slot={2} targetPlacement={3} error={4}",
                        character.Identity,
                        message.SourceContainer,
                        fromPlacement,
                        message.TargetPlacement,
                        exception.Message));
                return true;
            }

            this.SendMoveItemToInventoryAck(character, message.SourceContainer, message.TargetPlacement);
            this.PersistClientMoveItemToInventory(character, "backpack move");
            return true;
        }

        private IInventoryPage GetTargetPage(ICharacter character, int targetPlacement)
        {
            if (targetPlacement == (int)IdentityType.TradeWindow)
            {
                return character.BaseInventory.Pages[character.BaseInventory.StandardPage];
            }

            try
            {
                return character.BaseInventory.PageFromSlot(targetPlacement);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void SendMoveItemToInventoryAck(ICharacter character, Identity sourceContainer, int targetPlacement)
        {
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    SourceContainer = sourceContainer,
                    Target = character.Identity,
                    TargetPlacement = targetPlacement,
                    Unknown = 0
                });
        }

        public void PersistClientMoveItemToInventory(ICharacter character, string reason)
        {
            character.BaseInventory.Write();
            LogUtil.Debug(
                DebugInfoDetail.Database,
                string.Format("Persisted inventory after ClientMoveItemToInventory {0} char={1}", reason, character.Identity));
        }

        private static int DecodeBackpackHandle(Identity sourceContainer)
        {
            return (int)(((uint)sourceContainer.Instance >> 16) & 0xffff);
        }

        private static int DecodeBackpackSlot(Identity sourceContainer)
        {
            return (int)((uint)sourceContainer.Instance & 0xffff);
        }

        private void TryRemoveInventoryRollback(IInventoryPage inventoryPage, int inventorySlot)
        {
            try
            {
                if (inventoryPage[inventorySlot] != null)
                {
                    inventoryPage.Remove(inventorySlot);
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory backpack move rollback failed slot={0} error={1}",
                        inventorySlot,
                        exception.Message));
            }
        }
    }
}
