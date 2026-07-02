namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Actions;
    using AORebirth.Core.Components;
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
    using ZoneEngine.Core.Packets;

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

        public IEnumerable<IInventoryPage> CharacterStateInventoryPages(ICharacter character)
        {
            foreach (IInventoryPage page in character.BaseInventory.Pages.Values)
            {
                if (page is BankInventoryPage)
                {
                    continue;
                }

                yield return page;
            }
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

        public bool TryMoveOwnedInventoryItem(
            ICharacter character,
            ClientMoveItemToInventoryMessage message,
            IZoneClient client)
        {
            IInventoryPage sendingPage;
            if (!this.TryResolveMoveSourcePage(
                character,
                message.SourceContainer,
                out sendingPage))
            {
                return false;
            }

            int fromPlacement = message.SourceContainer.Instance;
            IItem itemFrom = sendingPage[fromPlacement];
            if (itemFrom == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory source slot is empty source={0} targetPlacement={1} character={2}",
                        message.SourceContainer,
                        message.TargetPlacement,
                        character.Identity));
                return true;
            }

            if (message.SourceContainer.Type == IdentityType.Inventory)
            {
                Identity backpackContainerIdentity;
                InventoryItemRules.TryEnsureBackpackContainerIdentity(
                    itemFrom,
                    character.Identity,
                    message.SourceContainer,
                    out backpackContainerIdentity);
            }

            IInventoryPage receivingPage = this.ResolveMoveTargetPage(character, message.TargetPlacement);
            if (receivingPage == null)
            {
                return false;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "ClientMoveItemToInventory resolved char={0} fromPage={1} fromSlot={2} toPage={3} rawTarget={4} item={5}/{6} ql={7}",
                    character.Identity,
                    sendingPage.GetType().Name,
                    fromPlacement,
                    receivingPage.GetType().Name,
                    message.TargetPlacement,
                    itemFrom.LowID,
                    itemFrom.HighID,
                    itemFrom.Quality));

            int ackTargetPlacement = message.TargetPlacement;
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
                        "ClientMoveItemToInventory target inventory is full source={0} targetPlacement={1} character={2}",
                        message.SourceContainer,
                        message.TargetPlacement,
                        character.Identity));
                return true;
            }

            IItemSlotHandler equipTo = receivingPage as IItemSlotHandler;
            IItemSlotHandler unequipFrom = sendingPage as IItemSlotHandler;
            IItem itemTo = receivingPage[toPlacement];
            bool affectsAppearance = this.IsAppearanceEquipmentPage(receivingPage)
                                     || this.IsAppearanceEquipmentPage(sendingPage);

            if (equipTo != null)
            {
                if (this.RequiresImplantAccess(receivingPage) && !this.HasImplantAccess(character))
                {
                    this.SendImplantAccessDenied(character);
                    return true;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory equip path char={0} targetSlot={1} itemToPresent={2}",
                        character.Identity,
                        toPlacement,
                        itemTo != null ? 1 : 0));

                if (receivingPage.NeedsItemCheck && !this.CanEquipToPage(character, receivingPage, itemFrom))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "ClientMoveItemToInventory equip requirements failed item={0}/{1}:{2} source={3} targetPlacement={4} character={5}",
                            itemFrom.LowID,
                            itemFrom.HighID,
                            itemFrom.Quality,
                            message.SourceContainer,
                            toPlacement,
                            character.Identity));
                    return true;
                }

                WeaponItemFullUpdate.SendWeaponDefinition(character, itemFrom);

                if (itemTo != null)
                {
                    if (affectsAppearance)
                    {
                        this.WaitForEquipVisualSync(itemFrom, itemTo, receivingPage is SocialArmorInventoryPage);
                    }

                    UnEquip.Send(client, receivingPage, toPlacement);
                    equipTo.HotSwap(sendingPage, fromPlacement, toPlacement);
                }
                else
                {
                    if (affectsAppearance)
                    {
                        this.WaitForEquipVisualSync(itemFrom, null, receivingPage is SocialArmorInventoryPage);
                    }

                    if (sendingPage == receivingPage)
                    {
                        UnEquip.Send(client, sendingPage, fromPlacement);
                    }

                    equipTo.Equip(sendingPage, fromPlacement, toPlacement);
                }

                this.SendMoveItemToInventoryAck(
                    character,
                    message.SourceContainer,
                    ackTargetPlacement);
                Equip.Send(client, receivingPage, toPlacement);
                character.CalculateSkills();
                ClientMoveItemToInventoryMessageHandler.EnsureWeaponVisualMeshes(character, true);
                this.PersistClientMoveItemToInventory(character, "equip");
                return true;
            }

            if (unequipFrom != null)
            {
                if (this.RequiresImplantAccess(sendingPage) && !this.HasImplantAccess(character))
                {
                    this.SendImplantAccessDenied(character);
                    return true;
                }

                if (affectsAppearance)
                {
                    this.WaitForEquipVisualSync(itemFrom, null, sendingPage is SocialArmorInventoryPage);
                }

                UnEquip.Send(client, sendingPage, fromPlacement);
                unequipFrom.Unequip(fromPlacement, receivingPage, toPlacement);
                this.SendMoveItemToInventoryAck(
                    character,
                    message.SourceContainer,
                    ackTargetPlacement);
                character.CalculateSkills();
                ClientMoveItemToInventoryMessageHandler.EnsureWeaponVisualMeshes(character, true);
                this.PersistClientMoveItemToInventory(character, "unequip");
                return true;
            }

            sendingPage.Remove(fromPlacement);
            receivingPage.Add(toPlacement, itemFrom);
            this.SendMoveItemToInventoryAck(
                character,
                message.SourceContainer,
                ackTargetPlacement);
            this.PersistClientMoveItemToInventory(character, "move");
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
            IInventoryPage receivingPage = this.ResolveMoveTargetPage(character, message.TargetPlacement);
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

        public bool TryResolveMoveSourcePage(
            ICharacter character,
            Identity sourceContainer,
            out IInventoryPage sendingPage)
        {
            sendingPage = null;

            if (character.BaseInventory.Pages.ContainsKey((int)sourceContainer.Type))
            {
                sendingPage = character.BaseInventory.Pages[(int)sourceContainer.Type];
                return true;
            }

            try
            {
                sendingPage = character.BaseInventory.PageFromSlot(sourceContainer.Instance);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IInventoryPage ResolveMoveTargetPage(ICharacter character, int targetPlacement)
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

        private bool CanEquipToPage(ICharacter character, IInventoryPage page, IItem item)
        {
            AOAction action = null;
            if ((page is ArmorInventoryPage) || (page is ImplantInventoryPage))
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWear);
            }
            else if (page is WeaponInventoryPage)
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWield);
            }

            return action == null || action.CheckRequirements(character);
        }

        private bool RequiresImplantAccess(IInventoryPage page)
        {
            return page is ImplantInventoryPage;
        }

        private bool HasImplantAccess(ICharacter character)
        {
            Character concreteCharacter = character as Character;
            return concreteCharacter != null && concreteCharacter.HasImplantAccess();
        }

        private void SendImplantAccessDenied(ICharacter character)
        {
            ChatTextMessageHandler.Default.Send(character, "Accessing implants requires technical supervision.");
        }

        private bool IsAppearanceEquipmentPage(IInventoryPage page)
        {
            return page is WeaponInventoryPage || page is ArmorInventoryPage || page is SocialArmorInventoryPage;
        }

        private void WaitForEquipVisualSync(IItem primary, IItem secondary, bool isSocial)
        {
            int delay = this.GetEquipDelay(primary, isSocial);
            if (secondary != null)
            {
                delay += this.GetEquipDelay(secondary, isSocial);
            }

            Thread.Sleep(delay * 10);
        }

        private int GetEquipDelay(IItem item, bool isSocial)
        {
            if (item == null || isSocial)
            {
                return 20;
            }

            int delay = item.GetAttribute(211);
            return delay == 1234567890 ? 20 : delay;
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
