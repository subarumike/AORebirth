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
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Requirements;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Textures;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Functions.GameFunctions;

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

        public void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
        {
            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return;
            }

            bool changed = false;
            changed |= this.EnsureWeaponMesh(
                character,
                weaponPage,
                (int)WeaponSlots.Righthand,
                1,
                StatIds.weaponmeshright,
                StatIds.overridetextureweaponright);
            changed |= this.EnsureWeaponMesh(
                character,
                weaponPage,
                (int)WeaponSlots.LeftHand,
                2,
                StatIds.weaponmeshleft,
                StatIds.overridetextureweaponleft);

            if (changed)
            {
                character.ChangedAppearance = true;
                if (announceAppearanceUpdate)
                {
                    character.Playfield.AnnounceAppearanceUpdate(character);
                }
            }
        }

        public Identity ResolveContainerAddItemTargetIdentity(Identity target)
        {
            Identity toIdentity = target;
            if (toIdentity.Type == IdentityType.IncomingTradeWindow)
            {
                toIdentity.Type = IdentityType.CanbeAffected;
            }

            return toIdentity;
        }

        public IInventoryPage ResolveContainerAddItemReceivingPage(
            IItemContainer itemReceiver,
            ICharacter character,
            Identity target,
            int toPlacement)
        {
            IInventoryPage receivingPage;
            if ((toPlacement == 0x6f) && (target.Type == IdentityType.IncomingTradeWindow))
            {
                receivingPage = itemReceiver.BaseInventory.Pages[(int)IdentityType.Bank];
            }
            else
            {
                receivingPage = itemReceiver.BaseInventory.PageFromSlot(toPlacement);
            }

            if ((receivingPage == null) || (itemReceiver.GetType() != character.GetType()))
            {
                receivingPage = itemReceiver.BaseInventory.Pages[itemReceiver.BaseInventory.StandardPage];
            }

            return receivingPage;
        }

        public int ResolveContainerAddItemTargetPlacement(IInventoryPage receivingPage, int toPlacement)
        {
            if (toPlacement == 0x6f)
            {
                return receivingPage.FindFreeSlot();
            }

            return toPlacement;
        }

        public bool TryMoveInventoryItemToBackpack(ICharacter character, ClientContainerAddItemMessage message)
        {
            if (message.Source.Type != IdentityType.Inventory || message.Target.Type != IdentityType.Container)
            {
                return false;
            }

            IInventoryPage inventoryPage;
            IInventoryPage backpackPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage)
                || !character.BaseInventory.TryGetBackpackPage(message.Target, out backpackPage))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move because pages are missing char={0} source={1} target={2}",
                        character.Identity,
                        message.Source,
                        message.Target));
                return true;
            }

            int sourceSlot = message.Source.Instance;
            if (!inventoryPage.ValidSlot(sourceSlot))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move for invalid source slot char={0} source={1} target={2}",
                        character.Identity,
                        message.Source,
                        message.Target));
                return true;
            }

            IItem item = inventoryPage[sourceSlot];
            if (item == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move because source slot is empty char={0} source={1} target={2}",
                        character.Identity,
                        message.Source,
                        message.Target));
                return true;
            }

            if (InventoryItemRules.IsBackpackContainerItem(item))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move because source item is a container char={0} source={1} target={2} item={3}/{4} ql={5} itemIdentity={6}",
                        character.Identity,
                        message.Source,
                        message.Target,
                        item.LowID,
                        item.HighID,
                        item.Quality,
                        item.Identity));
                return true;
            }

            int backpackSlot = backpackPage.FindFreeSlot();
            if (backpackSlot < 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move because backpack is full char={0} source={1} target={2}",
                        character.Identity,
                        message.Source,
                        message.Target));
                return true;
            }

            try
            {
                InventoryError addError = backpackPage.Add(backpackSlot, item);
                if (addError != InventoryError.OK)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "Rejected ClientContainerAddItem backpack move add failed char={0} source={1} target={2} slot={3} error={4}",
                            character.Identity,
                            message.Source,
                            message.Target,
                            backpackSlot,
                            addError));
                    return true;
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move add threw char={0} source={1} target={2} slot={3} error={4}",
                        character.Identity,
                        message.Source,
                        message.Target,
                        backpackSlot,
                        exception.Message));
                return true;
            }

            try
            {
                inventoryPage.Remove(sourceSlot);
            }
            catch (Exception exception)
            {
                this.TryRemoveBackpackRollback(backpackPage, backpackSlot);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Rejected ClientContainerAddItem backpack move remove source threw char={0} source={1} target={2} slot={3} error={4}",
                        character.Identity,
                        message.Source,
                        message.Target,
                        backpackSlot,
                        exception.Message));
                return true;
            }

            character.Send(
                new ContainerAddItemMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        SourceContainer = message.Source,
                        Target = message.Target,
                        TargetPlacement = backpackSlot
                    });

            character.BaseInventory.Write();
            LogUtil.Debug(
                DebugInfoDetail.Database,
                string.Format(
                    "Persisted inventory after ClientContainerAddItem backpack move char={0} source={1} target={2} slot={3}",
                    character.Identity,
                    message.Source,
                    message.Target,
                    backpackSlot));
            return true;
        }

        public bool TryDepositInventoryItemToBank(ICharacter character, ClientContainerAddItemMessage message)
        {
            if (!IsInventoryToBankDeposit(message))
            {
                return false;
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
                return true;
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
                return true;
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
                return true;
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
                return true;
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
                return true;
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
                    return true;
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
                return true;
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
                return true;
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
            return true;
        }

        public void HandleClientContainerAddItem(IZoneClient client, ClientContainerAddItemMessage message)
        {
            ICharacter character = client != null && client.Controller != null
                ? client.Controller.Character
                : null;

            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            if (this.TryMoveInventoryItemToBackpack(character, message))
            {
                return;
            }

            if (this.TryDepositInventoryItemToBank(character, message))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Unhandled ClientContainerAddItem char={0} source={1} target={2}",
                    character.Identity,
                    message.Source,
                    message.Target));
        }

        public void HandleClientMoveItemToInventory(IZoneClient client, ClientMoveItemToInventoryMessage message)
        {
            ICharacter character = client.Controller.Character;

            if (this.TryMoveBackpackItemToInventory(character, message))
            {
                return;
            }

            if (this.TryMoveOwnedInventoryItem(character, message, client))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Unhandled ClientMoveItemToInventory source={0} targetPlacement={1} character={2}",
                    message.SourceContainer,
                    message.TargetPlacement,
                    character.Identity));
        }

        public void HandleKnuBotTradeItemRemove(IZoneClient client, KnuBotTradeMessage message)
        {
            client.Controller.Character.BaseInventory.Pages[(int)message.Container.Type].Remove(
                message.Container.Instance);
        }

        public bool TryGetTradeAddItem(IItemContainer issuer, TradeMessage message, out IItem item)
        {
            item = null;

            try
            {
                if (issuer is Vendor)
                {
                    item = issuer.BaseInventory.GetItemInContainer(
                        (int)IdentityType.Inventory,
                        message.Container.Instance);
                }
                else
                {
                    item = issuer.BaseInventory.GetItemInContainer(
                        (int)message.Container.Type,
                        message.Container.Instance);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Trade AddItem lookup failed issuer=" + issuer.Identity.ToString(true)
                    + " source=" + message.Container.ToString(true)
                    + " error=" + ex.Message);
                return false;
            }

            return item != null;
        }

        public IItem GetVendorTradeItem(IItemContainer issuer, int slot)
        {
            return issuer.BaseInventory.GetItemInContainer((int)IdentityType.Inventory, slot);
        }

        public void MoveNonEquipmentContainerItem(
            ICharacter character,
            ContainerAddItemMessage message,
            IInventoryPage sendingPage,
            IInventoryPage receivingPage,
            int fromPlacement)
        {
            message.TargetPlacement = receivingPage.FindFreeSlot();
            IItem item = sendingPage.Remove(fromPlacement);
            receivingPage.Add(message.TargetPlacement, item);
            character.Send(message);
        }

        public bool MovePlayerControllerContainerItem(
            ICharacter character,
            int sourceContainerType,
            int sourcePlacement,
            Identity target,
            int targetPlacement)
        {
            if (character.BaseInventory.Pages.ContainsKey(sourceContainerType))
            {
                IInventoryPage sourcePage = character.BaseInventory.Pages[sourceContainerType];

                if (sourcePage[sourcePlacement] != null)
                {
                    if (character.Identity == target)
                    {
                        IInventoryPage targetPage = character.BaseInventory.PageFromSlot(targetPlacement);
                        if (targetPage != null)
                        {
                            IItem itemSource = sourcePage.Remove(sourcePlacement);
                            IItem itemTarget = targetPage.Remove(targetPlacement);
                            if (itemTarget != null)
                            {
                                sourcePage.Add(sourcePlacement, itemTarget);
                            }

                            if (itemSource != null)
                            {
                                targetPage.Add(targetPlacement, itemSource);
                            }
                        }
                    }
                    else
                    {
                        // Put it into the other players/npcs trade window?
                    }
                }
            }

            return true;
        }

        public bool DeletePlayerControllerContainerItem(ICharacter character, int container, int slotNumber)
        {
            if (character.BaseInventory.Pages.ContainsKey(container))
            {
                character.BaseInventory.Pages[container].Remove(slotNumber);
            }

            return true;
        }

        public bool TryUseBackpackContainer(ICharacter character, Identity itemPosition)
        {
            Item item = null;
            try
            {
                item = character.BaseInventory.GetItemInContainer((int)itemPosition.Type, itemPosition.Instance);
            }
            catch (Exception)
            {
            }

            return item != null && this.TryOpenBackpackContainer(character, itemPosition, item);
        }

        public bool TryOpenBackpackContainer(ICharacter character, Identity itemPosition, Item item)
        {
            if (!IsBackpackUseSlot(itemPosition.Type))
            {
                return false;
            }

            Identity containerIdentity;
            if (!TryResolveBackpackContainerIdentity(character, itemPosition, item, out containerIdentity))
            {
                return false;
            }

            if (!IsItemUsable(item))
            {
                return false;
            }

            if (character.BaseInventory.IsBackpackOpen(containerIdentity))
            {
                BackpackContainerActionMessageHandler.Default.SendClose(character, containerIdentity);
                character.BaseInventory.MarkBackpackClosed(containerIdentity);
                return true;
            }

            IInventoryPage backpackPage;
            bool pageAlreadyKnown = character.BaseInventory.TryGetBackpackPage(containerIdentity, out backpackPage);
            if (pageAlreadyKnown)
            {
                BackpackContainerActionMessageHandler.Default.SendOpen(character, containerIdentity);
                character.BaseInventory.MarkBackpackOpen(containerIdentity);
            }
            else
            {
                backpackPage = character.BaseInventory.GetOrCreateBackpackPage(containerIdentity);

                if (backpackPage.List().Any())
                {
                    int openHandle = InventoryUpdateMessageHandler.Default.ReserveBackpackInventoryHandle();
                    ChestItemFullUpdateMessageHandler.Default.Send(character, item, itemPosition, backpackPage.Identity);
                    InventoryUpdateMessageHandler.Default.SendContainerOpen(character, backpackPage, openHandle);
                }
                else
                {
                    int introduceHandle = InventoryUpdateMessageHandler.Default.ReserveBackpackInventoryHandle();
                    int openHandle = InventoryUpdateMessageHandler.Default.ReserveBackpackInventoryHandle();
                    InventoryUpdateMessageHandler.Default.SendContainerIntroduce(character, backpackPage, introduceHandle);
                    ChestItemFullUpdateMessageHandler.Default.Send(character, item, itemPosition, backpackPage.Identity);
                    InventoryUpdateMessageHandler.Default.SendFreshContainerOpen(character, backpackPage, openHandle);
                }

                character.BaseInventory.MarkBackpackOpen(containerIdentity);
            }

            return true;
        }

        public void RegisterBackpackInventoryHandle(ICharacter character, IInventoryPage page, int handle)
        {
            if ((character == null) || (character.BaseInventory == null) || (page == null)
                || (page.Identity == null) || (page.Identity.Type != IdentityType.Container))
            {
                return;
            }

            character.BaseInventory.RegisterBackpackHandle(handle, page.Identity);
        }

        public bool UseInventoryItem(ICharacter character, Identity itemPosition)
        {
            Item item = null;
            try
            {
                item = character.BaseInventory.GetItemInContainer((int)itemPosition.Type, itemPosition.Instance);
            }
            catch (Exception)
            {
            }

            if (item == null)
            {
                throw new NullReferenceException("No item found at " + itemPosition);
            }

            if (this.TryOpenBackpackContainer(character, itemPosition, item))
            {
                return true;
            }

            if (this.IsUseBlockedBySkillLock(character, item))
            {
                return false;
            }

            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);

            if (ItemLoader.ItemList[item.HighID].IsConsumable())
            {
                item.MultipleCount--;
                if (item.MultipleCount <= 0)
                {
                    character.BaseInventory.RemoveItem(
                        (int)itemPosition.Type,
                        itemPosition.Instance);
                    CharacterActionMessageHandler.Default.SendDeleteItem(
                        character,
                        (int)itemPosition.Type,
                        itemPosition.Instance);
                }
            }

            item.PerformAction(character, EventType.OnUse, itemPosition.Instance);
            return true;
        }

        public void DeleteInventoryItemAction(ICharacter character, CharacterActionMessage message)
        {
            ItemDao.Instance.Delete(
                new
                {
                    containertype = (int)message.Target.Type,
                    containerinstance = character.Identity.Instance,
                    Id = message.Target.Instance
                });

            character.BaseInventory.RemoveItem(
                (int)message.Target.Type,
                message.Target.Instance);
        }

        public void SplitInventoryItemStackAction(ICharacter character, CharacterActionMessage message)
        {
            IItem item = character.BaseInventory.Pages[(int)message.Target.Type][message.Target.Instance];
            item.MultipleCount -= message.Parameter2;
            Item newItem = new Item(item.Quality, item.LowID, item.HighID);
            newItem.MultipleCount = message.Parameter2;

            character.BaseInventory.Pages[(int)message.Target.Type].Add(
                character.BaseInventory.Pages[(int)message.Target.Type].FindFreeSlot(),
                newItem);
            character.BaseInventory.Pages[(int)message.Target.Type].Write();
        }

        public void MergeInventoryItemStackAction(ICharacter character, CharacterActionMessage message)
        {
            character.BaseInventory.Pages[(int)message.Target.Type][message.Target.Instance].MultipleCount +=
                character.BaseInventory.Pages[(int)message.Target.Type][message.Parameter2].MultipleCount;
            character.BaseInventory.Pages[(int)message.Target.Type].Remove(message.Parameter2);
            character.BaseInventory.Pages[(int)message.Target.Type].Write();
        }

        public bool TryRejectInventoryPageAccess(ICharacter character, IInventoryPage page)
        {
            if (this.RequiresImplantAccess(page) && !this.HasImplantAccess(character))
            {
                this.SendImplantAccessDenied(character);
                return true;
            }

            return false;
        }

        public bool CanMoveContainerItemToPage(ICharacter character, IInventoryPage page, IItem item)
        {
            AOAction action = ResolveContainerAddItemAction(page, item);
            return action.CheckRequirements(character);
        }

        public bool ShouldSkipContainerAppearanceUpdate(IInventoryPage receivingPage, IInventoryPage sendingPage)
        {
            return !this.IsAppearanceEquipmentPage(receivingPage)
                   && !this.IsAppearanceEquipmentPage(sendingPage);
        }

        public void WaitForContainerHotSwapVisualSync(
            IItem itemFrom,
            IItem itemTo,
            bool skipAppearanceUpdate)
        {
            int delay = 20;
            if (!skipAppearanceUpdate)
            {
                delay = this.GetEquipDelay(itemFrom, false) + this.GetEquipDelay(itemTo, false);
            }

            Thread.Sleep(delay * 10);
        }

        public void WaitForContainerEquipVisualSync(
            IItem item,
            IInventoryPage equipmentPage,
            bool skipAppearanceUpdate)
        {
            if (skipAppearanceUpdate)
            {
                return;
            }

            Thread.Sleep(this.GetEquipDelay(item, equipmentPage is SocialArmorInventoryPage) * 10);
        }

        public void HandleContainerAddItem(IZoneClient client, ContainerAddItemMessage message)
        {
            ICharacter character = client.Controller.Character;
            IInventoryPage sendingPage = Pool.Instance.GetObject<IInventoryPage>(
                message.Identity,
                new Identity()
                {
                    Type = (IdentityType)message.Identity.Instance,
                    Instance = (int)message.SourceContainer.Type
                });
            int fromPlacement = message.SourceContainer.Instance;
            Identity toIdentity = message.Target;
            int toPlacement = message.TargetPlacement;

            IItem itemFrom = sendingPage[fromPlacement];
            toIdentity = this.ResolveContainerAddItemTargetIdentity(toIdentity);

            IItemContainer itemReceiver = character.Playfield.FindByIdentity(toIdentity) as IItemContainer;
            if (itemReceiver == null)
            {
                throw new ArgumentOutOfRangeException(
                    "No Entity found: " + message.Target.Type.ToString() + ":" + message.Target.Instance);
            }

            IInventoryPage receivingPage =
                this.ResolveContainerAddItemReceivingPage(
                    itemReceiver,
                    character,
                    message.Target,
                    toPlacement);

            if (receivingPage == null)
            {
                throw new ArgumentOutOfRangeException("No inventorypage found.");
            }

            toPlacement = this.ResolveContainerAddItemTargetPlacement(receivingPage, toPlacement);

            IItem itemTo;
            try
            {
                itemTo = receivingPage[toPlacement];
            }
            catch (Exception)
            {
                itemTo = null;
            }

            character.DoNotDoTimers = true;
            IItemSlotHandler equipTo = receivingPage as IItemSlotHandler;
            IItemSlotHandler unequipFrom = sendingPage as IItemSlotHandler;

            bool noAppearanceUpdate = this.ShouldSkipContainerAppearanceUpdate(receivingPage, sendingPage);

            if (equipTo != null)
            {
                if (this.TryRejectInventoryPageAccess(character, receivingPage))
                {
                    character.DoNotDoTimers = false;
                    return;
                }

                if (itemTo != null)
                {
                    if (receivingPage.NeedsItemCheck)
                    {
                        if (this.CanMoveContainerItemToPage(character, sendingPage, itemFrom))
                        {
                            UnEquip.Send(client, receivingPage, toPlacement);
                            this.WaitForContainerHotSwapVisualSync(
                                itemFrom,
                                itemTo,
                                noAppearanceUpdate);

                            character.Send(message);
                            equipTo.HotSwap(sendingPage, fromPlacement, toPlacement);
                            Equip.Send(client, receivingPage, toPlacement);
                        }
                    }
                }
                else
                {
                    if (receivingPage.NeedsItemCheck)
                    {
                        if (itemFrom == null)
                        {
                            throw new NullReferenceException("itemFrom can not be null, possible inventory error");
                        }

                        if (this.CanMoveContainerItemToPage(character, receivingPage, itemFrom))
                        {
                            this.WaitForContainerEquipVisualSync(
                                itemFrom,
                                receivingPage,
                                noAppearanceUpdate);

                            if (sendingPage == receivingPage)
                            {
                                UnEquip.Send(client, sendingPage, fromPlacement);
                            }

                            character.Send(message);
                            equipTo.Equip(sendingPage, fromPlacement, toPlacement);
                            Equip.Send(client, receivingPage, toPlacement);
                        }
                    }
                }
            }
            else
            {
                if (unequipFrom != null)
                {
                    if (this.TryRejectInventoryPageAccess(character, sendingPage))
                    {
                        character.DoNotDoTimers = false;
                        return;
                    }

                    this.WaitForContainerEquipVisualSync(
                        itemFrom,
                        sendingPage,
                        noAppearanceUpdate);

                    UnEquip.Send(client, sendingPage, fromPlacement);
                    unequipFrom.Unequip(fromPlacement, receivingPage, toPlacement);
                    character.Send(message);
                }
                else
                {
                    this.MoveNonEquipmentContainerItem(
                        character,
                        message,
                        sendingPage,
                        receivingPage,
                        fromPlacement);
                }
            }

            character.DoNotDoTimers = false;
            character.CalculateSkills();
        }

        public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
            {
                case InventoryContainerInteractionRouteMode.InventoryItem:
                    this.UseInventoryItem(client.Controller.Character, target);
                    GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    return true;

                case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
                    if (this.TryUseBackpackContainer(client.Controller.Character, target))
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
                this.EnsureWeaponVisualMeshes(character, true);
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
                this.EnsureWeaponVisualMeshes(character, true);
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

        public bool HasFreeInventorySlots(ICharacter character, int neededSlots)
        {
            if (neededSlots <= 0)
            {
                return true;
            }

            IInventoryPage page = character.BaseInventory[character.BaseInventory.StandardPage];
            int freeSlots = 0;
            for (int slot = page.FirstSlotNumber; slot < page.FirstSlotNumber + page.MaxSlots; slot++)
            {
                if (page[slot] == null)
                {
                    freeSlots++;
                    if (freeSlots >= neededSlots)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int FindFreeStandardInventorySlot(IItemContainer owner)
        {
            return owner.BaseInventory[owner.BaseInventory.StandardPage].FindFreeSlot();
        }

        public InventoryError AddToStandardInventoryPage(IItemContainer owner, int targetSlot, IItem item)
        {
            return owner.BaseInventory.AddToPage(owner.BaseInventory.StandardPage, targetSlot, item);
        }

        public void AddToStandardInventoryPageUnchecked(IItemContainer owner, int targetSlot, IItem item)
        {
            owner.BaseInventory[owner.BaseInventory.StandardPage].Add(targetSlot, item);
        }

        public void SendTradeWindowMoveToInventory(
            ICharacter character,
            IdentityType sourceType,
            int sourceSlot,
            int targetSlot)
        {
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer =
                        new Identity
                        {
                            Type = sourceType,
                            Instance = sourceSlot
                        },
                    Target = character.Identity,
                    TargetPlacement = targetSlot
                });
        }

        public void ReturnPlayerTradeOffers(ICharacter owner, TemporaryBag shoppingBag)
        {
            IInventoryPage offerPage = shoppingBag.GetPlayerOfferPage(owner.Identity);
            if (offerPage == null)
            {
                return;
            }

            foreach (KeyValuePair<int, IItem> offer in offerPage.List().ToList())
            {
                int targetSlot = owner.BaseInventory[owner.BaseInventory.StandardPage].FindFreeSlot();
                if (targetSlot < 0)
                {
                    continue;
                }

                offerPage.Remove(offer.Key);
                owner.BaseInventory[owner.BaseInventory.StandardPage].Add(targetSlot, offer.Value);
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "TRADE_DECLINE_RETURN owner=" + owner.Identity.ToString(true)
                    + " name=" + owner.Name
                    + " sourceSlot=" + offer.Key
                    + " targetSlot=" + targetSlot
                    + " item=" + offer.Value.LowID + "/" + offer.Value.HighID + ":" + offer.Value.Quality);
                this.SendTradeWindowMoveToInventory(owner, IdentityType.KnuBotTradeWindow, offer.Key, targetSlot);
            }
        }

        public void TransferPlayerTradeOffers(ICharacter from, ICharacter to, TemporaryBag shoppingBag)
        {
            IInventoryPage offerPage = shoppingBag.GetPlayerOfferPage(from.Identity);
            foreach (KeyValuePair<int, IItem> offer in offerPage.List().ToList())
            {
                int targetSlot = to.BaseInventory[to.BaseInventory.StandardPage].FindFreeSlot();
                if (targetSlot < 0)
                {
                    continue;
                }

                offerPage.Remove(offer.Key);
                InventoryError err = to.BaseInventory.AddToPage(to.BaseInventory.StandardPage, targetSlot, offer.Value);
                if (err == InventoryError.OK)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Shopping,
                        "Player trade transfer committed from=" + from.Identity.ToString(true)
                        + " to=" + to.Identity.ToString(true)
                        + " tradeSlot=" + offer.Key
                        + " targetSlot=" + targetSlot
                        + " item=" + offer.Value.LowID + "/" + offer.Value.HighID + ":" + offer.Value.Quality);

                    LogUtil.Debug(
                        DebugInfoDetail.Shopping,
                        "TRADE_ITEM_COMMIT from=" + from.Identity.ToString(true)
                        + " fromName=" + from.Name
                        + " to=" + to.Identity.ToString(true)
                        + " toName=" + to.Name
                        + " sourceSlot=" + offer.Key
                        + " targetSlot=" + targetSlot
                        + " item=" + offer.Value.LowID + "/" + offer.Value.HighID + ":" + offer.Value.Quality);
                }
                else
                {
                    offerPage.Add(offer.Key, offer.Value);
                    ChatTextMessageHandler.Default.Send(to, "Could not receive trade item. (" + err + ")");
                }
            }
        }

        public void PersistCharacterInventory(ICharacter character, string reason)
        {
            character.BaseInventory.Write();
            LogUtil.Debug(
                DebugInfoDetail.Database,
                "Persisted inventory after " + reason + " char=" + character.Identity.ToString(true));
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

        private static bool IsInventoryToBankDeposit(ClientContainerAddItemMessage message)
        {
            return message.Source.Type == IdentityType.Inventory
                   && message.Target.Type == IdentityType.IncomingTradeWindow;
        }

        private static bool IsBackpackUseSlot(IdentityType identityType)
        {
            return identityType == IdentityType.Inventory
                   || identityType == IdentityType.ArmorPage
                   || identityType == IdentityType.SocialPage;
        }

        private static bool TryResolveBackpackContainerIdentity(
            ICharacter character,
            Identity itemPosition,
            Item item,
            out Identity containerIdentity)
        {
            containerIdentity = Identity.None;

            return InventoryItemRules.TryEnsureBackpackContainerIdentity(
                item,
                character.Identity,
                itemPosition,
                out containerIdentity);
        }

        private static bool IsItemUsable(Item item)
        {
            return (item.GetAttribute((int)StatIds.can) & (int)CanFlags.Use) == (int)CanFlags.Use;
        }

        private bool IsUseBlockedBySkillLock(ICharacter characterEntity, Item item)
        {
            Character character = characterEntity as Character;
            if (character == null)
            {
                return false;
            }

            foreach (Event itemEvent in item.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function itemFunction in itemEvent.Functions.Where(
                    x => x.FunctionType == (int)FunctionType.LockSkill))
                {
                    if (!ItemFunctionRequirementsPass(characterEntity, itemFunction))
                    {
                        continue;
                    }

                    int statId;
                    int durationSeconds;
                    if (!lockskill.TryReadArguments(itemFunction.Arguments.Values.ToArray(), out statId, out durationSeconds))
                    {
                        continue;
                    }

                    int remainingSeconds = character.GetSkillLockRemainingSeconds(statId);
                    if (remainingSeconds <= 0)
                    {
                        continue;
                    }

                    CharacterActionMessageHandler.Default.SendSkillUnavailable(character, statId, remainingSeconds);
                    return true;
                }
            }

            return false;
        }

        private static bool ItemFunctionRequirementsPass(ICharacter character, Function itemFunction)
        {
            bool result = true;
            foreach (Requirement requirement in itemFunction.Requirements)
            {
                result &= requirement.CheckRequirement(character);
                if (!result)
                {
                    break;
                }
            }

            return result;
        }

        private bool EnsureWeaponMesh(
            ICharacter character,
            IInventoryPage weaponPage,
            int slot,
            int meshPosition,
            StatIds meshStat,
            StatIds overrideTextureStat)
        {
            IItem equippedItem = weaponPage[slot];
            if (equippedItem == null)
            {
                return false;
            }

            AOMeshs existing = character.MeshLayer.GetMeshAtPosition(meshPosition);

            int meshId = NormalizeItemVisualValue(equippedItem.GetAttribute((int)meshStat));
            if (meshId <= 0)
            {
                meshId = NormalizeItemVisualValue(equippedItem.GetAttribute(209));
            }

            if (meshId <= 0)
            {
                bool hasToWieldAction = equippedItem.ItemActions.Any(x => x.ActionType == ActionType.ToWield);
                string wearFunctions = string.Join(
                    ",",
                    equippedItem.Events
                        .Where(x => x.EventType == EventType.OnWear || x.EventType == EventType.OnWield)
                        .SelectMany(x => x.Functions)
                        .Select(x => x.FunctionType.ToString())
                        .ToArray());

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "EnsureWeaponMesh skipped: item has no valid mesh stat char={0} slot={1} meshStat={2} raw={3} item={4}/{5} ql={6} hasToWield={7} wearFuncs=[{8}] meshR={9} meshL={10} ovR={11} ovL={12} weaponMeshHolder={13}",
                        character.Identity,
                        slot,
                        meshStat,
                        equippedItem.GetAttribute((int)meshStat),
                        equippedItem.LowID,
                        equippedItem.HighID,
                        equippedItem.Quality,
                        hasToWieldAction ? 1 : 0,
                        wearFunctions,
                        equippedItem.GetAttribute((int)StatIds.weaponmeshright),
                        equippedItem.GetAttribute((int)StatIds.weaponmeshleft),
                        equippedItem.GetAttribute((int)StatIds.overridetextureweaponright),
                        equippedItem.GetAttribute((int)StatIds.overridetextureweaponleft),
                        equippedItem.GetAttribute(209)));
                return false;
            }

            if (existing != null)
            {
                if (existing.Mesh > 0 && existing.Mesh != 1234567890)
                {
                    return false;
                }

                character.MeshLayer.RemoveMesh(existing.Position, existing.Mesh, existing.OverrideTexture, existing.Layer);
            }

            int overrideTexture = NormalizeItemVisualValue(equippedItem.GetAttribute((int)overrideTextureStat));
            int layer = MeshLayers.GetLayer(slot);
            character.MeshLayer.AddMesh(meshPosition, meshId, overrideTexture, layer);
            character.Stats[meshStat].Value = meshId;

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "EnsureWeaponMesh applied char={0} slot={1} position={2} mesh={3} override={4} layer={5}",
                    character.Identity,
                    slot,
                    meshPosition,
                    meshId,
                    overrideTexture,
                    layer));
            return true;
        }

        private static int NormalizeItemVisualValue(int value)
        {
            if (value <= 0 || value == 1234567890)
            {
                return 0;
            }

            return value;
        }

        private static AOAction ResolveContainerAddItemAction(IInventoryPage page, IItem item)
        {
            AOAction action = null;

            if ((page is ArmorInventoryPage) || (page is ImplantInventoryPage))
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWear);
                if (action == null)
                {
                    return new AOAction();
                }
            }

            if (page is WeaponInventoryPage)
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWield);
                if (action == null)
                {
                    return new AOAction();
                }
            }

            if (page is PlayerInventoryPage)
            {
                return new AOAction();
            }

            if (page is SocialArmorInventoryPage)
            {
                return new AOAction();
            }

            if (action == null)
            {
                throw new NotSupportedException(
                    "No suitable action found for equipping to this page: " + page.GetType());
            }

            return action;
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

        private void TryRemoveBackpackRollback(IInventoryPage backpackPage, int backpackSlot)
        {
            try
            {
                if (backpackPage[backpackSlot] != null)
                {
                    backpackPage.Remove(backpackSlot);
                }
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientContainerAddItem backpack move rollback failed slot={0} error={1}",
                        backpackSlot,
                        exception.Message));
            }
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
