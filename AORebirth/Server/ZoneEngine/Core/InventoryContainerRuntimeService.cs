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

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.Functions.GameFunctions;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Thrak.Quests;

    using MsgPack;

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

        /// <summary>
        /// Live Feedback_MailNoChests rejects mail Item-field drops when the bag is a
        /// Container dynel bound like an opened backpack (ChestItemFullUpdate + open).
        /// Publish that bind then close immediately so the Item field blocks without
        /// leaving backpack windows open. Capture: 20260715-100540.
        /// </summary>
        public void PublishMailBlockedContainerLinks(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            IInventoryPage inventoryPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage)
                || inventoryPage == null)
            {
                return;
            }

            int published = 0;
            foreach (KeyValuePair<int, IItem> entry in inventoryPage.List())
            {
                Item item = entry.Value as Item;
                if (item == null)
                {
                    continue;
                }

                int placement = entry.Key;
                if (placement < inventoryPage.FirstSlotNumber)
                {
                    placement = inventoryPage.FirstSlotNumber + placement;
                }

                Identity itemPosition = new Identity
                {
                    Type = IdentityType.Inventory,
                    Instance = placement
                };
                Identity containerIdentity;
                if (!InventoryItemRules.TryEnsureMailForbiddenContainerIdentity(
                        item,
                        character.Identity,
                        itemPosition,
                        out containerIdentity))
                {
                    continue;
                }

                IInventoryPage backpackPage = character.BaseInventory.GetOrCreateBackpackPage(containerIdentity);
                bool isEmpty = !backpackPage.List().Any();
                int openHandle = InventoryUpdateMessageHandler.Default.ReserveBackpackInventoryHandle();

                // Match live open bind path so client treats the inv icon as Container (0xC749).
                if (isEmpty)
                {
                    int introduceHandle = InventoryUpdateMessageHandler.Default.ReserveBackpackInventoryHandle();
                    InventoryUpdateMessageHandler.Default.SendContainerIntroduce(
                        character,
                        backpackPage,
                        introduceHandle);
                    ChestItemFullUpdateMessageHandler.Default.Send(
                        character,
                        item,
                        itemPosition,
                        backpackPage.Identity);
                    InventoryUpdateMessageHandler.Default.SendFreshContainerOpen(
                        character,
                        backpackPage,
                        openHandle);
                }
                else
                {
                    ChestItemFullUpdateMessageHandler.Default.Send(
                        character,
                        item,
                        itemPosition,
                        backpackPage.Identity);
                    InventoryUpdateMessageHandler.Default.SendContainerOpen(
                        character,
                        backpackPage,
                        openHandle);
                }

                BackpackContainerActionMessageHandler.Default.SendClose(character, containerIdentity);
                character.BaseInventory.MarkBackpackClosed(containerIdentity);
                published++;
            }

            if (published > 0)
            {
                InventoryUpdateMessageHandler.Default.Send(character, inventoryPage);
            }

            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.Server.Info(
                    character.Controller.Client,
                    "Mail container guard: published {0} Container link(s) for Feedback_MailNoChests",
                    published);
            }
        }

        /// <summary>
        /// Client often drops FullCharacter bag contents on login/death until CharInPlay
        /// (which this fork frequently never receives). Re-push inventory pages so items
        /// show without requiring a zone hop.
        /// </summary>
        public void ResyncCharacterInventoryToClient(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            int[] pageTypes =
                {
                    (int)IdentityType.Inventory,
                    (int)IdentityType.WeaponPage,
                    (int)IdentityType.ArmorPage,
                    (int)IdentityType.ImplantPage,
                    (int)IdentityType.SocialPage
                };

            for (int i = 0; i < pageTypes.Length; i++)
            {
                IInventoryPage page;
                if (!character.BaseInventory.Pages.TryGetValue(pageTypes[i], out page) || page == null)
                {
                    continue;
                }

                InventoryUpdateMessageHandler.Default.Send(character, page);
            }

            this.PublishMailBlockedContainerLinks(character);
        }

        /// <summary>
        /// Delayed bag resync after login/death FullCharacter — client UI is not always ready
        /// for the immediate InventoryUpdate.
        /// </summary>
        public void SchedulePostLoginInventoryResync(IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(600);
                    ICharacter character = client.Controller != null ? client.Controller.Character : null;
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    try
                    {
                        this.ResyncCharacterInventoryToClient(character);
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Error,
                            "Post-login inventory resync failed char="
                            + character.Identity.Instance
                            + " err="
                            + ex.Message);
                    }
                });
        }

        public void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
        {
            if (PetBureaucratGuardianAppearance.IsGuardianPet(character))
            {
                return;
            }

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

        public IItem GetKnuBotTradeItem(ICharacter character, IdentityType container, int slotNumber)
        {
            return character.BaseInventory.Pages[(int)container][slotNumber];
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

        public bool VendorShopNeedsDatabaseEntry(Vendor vendor)
        {
            return vendor.BaseInventory.Pages[vendor.BaseInventory.StandardPage].List().Count == 0
                   && string.IsNullOrEmpty(vendor.TemplateHash);
        }

        public IInventoryPage GetVendorStandardInventoryPage(Vendor vendor)
        {
            return vendor.BaseInventory.Pages[vendor.BaseInventory.StandardPage];
        }

        public void AddVendorPurchaseOffer(TemporaryBag shoppingBag, TradeMessage message, IItem item)
        {
            shoppingBag.Add(
                new Identity { Instance = message.Container.Instance },
                CloneShopItem(item));
        }

        public void AddVendorSaleOffer(TemporaryBag shoppingBag, TradeMessage message, IItemContainer issuer)
        {
            shoppingBag.Add(
                message.Target,
                this.RemoveInventoryItem(issuer, message.Container));
        }

        public void RemoveVendorPurchaseOffer(TemporaryBag shoppingBag, TradeMessage message)
        {
            shoppingBag.Remove(
                new Identity { Instance = message.Container.Instance },
                message.Container.Instance);
        }

        public InventoryItemAddResult TryAddStandardInventoryItem(IItemContainer owner, IItem item)
        {
            int targetSlot = this.FindFreeStandardInventorySlot(owner);
            if (targetSlot < 0)
            {
                return InventoryItemAddResult.NoFreeSlot();
            }

            InventoryError error = this.AddToStandardInventoryPage(owner, targetSlot, item);
            return error == InventoryError.OK
                       ? InventoryItemAddResult.Success(targetSlot)
                       : InventoryItemAddResult.Failed(targetSlot, error);
        }

        public void ReturnItemsToStandardInventoryUnchecked(IItemContainer owner, IEnumerable<IItem> items)
        {
            foreach (IItem item in items)
            {
                int nextSlot = this.FindFreeStandardInventorySlot(owner);
                if (nextSlot != -1)
                {
                    this.AddToStandardInventoryPageUnchecked(owner, nextSlot, item);
                }
            }
        }

        public Item GetTradeSkillItem(ICharacter character, TradeSkillInfo info)
        {
            if (character == null || info == null || character.BaseInventory == null)
            {
                return null;
            }

            try
            {
                return character.BaseInventory.GetItemInContainer(info.Container, info.Placement);
            }
            catch (Exception)
            {
                // GetItemInContainer throws on empty/wrong slot; UseItemOnItem must not crash.
                return null;
            }
        }

        public InventoryError AddTradeSkillResultItem(ICharacter character, Item item)
        {
            return character.BaseInventory.TryAdd(item);
        }

        public void RemoveTradeSkillItem(ICharacter character, TradeSkillInfo info)
        {
            character.BaseInventory.RemoveItem(info.Container, info.Placement);
        }

        public Item SetTradeSkillSource(ICharacter character, int container, int placement)
        {
            character.TradeSkillSource = new TradeSkillInfo(0, container, placement);
            return this.GetTradeSkillItem(character, character.TradeSkillSource);
        }

        public Item SetTradeSkillTarget(ICharacter character, int container, int placement)
        {
            character.TradeSkillTarget = new TradeSkillInfo(1, container, placement);
            return this.GetTradeSkillItem(character, character.TradeSkillTarget);
        }

        public void ClearTradeSkillSource(ICharacter character)
        {
            character.TradeSkillSource = null;
        }

        public void ClearTradeSkillTarget(ICharacter character)
        {
            character.TradeSkillTarget = null;
        }

        public bool HasInventoryPage(IItemContainer owner, Identity container)
        {
            return owner.BaseInventory.Pages.ContainsKey((int)container.Type);
        }

        public IItem RemoveInventoryItem(IItemContainer owner, Identity container)
        {
            return owner.BaseInventory.RemoveItem((int)container.Type, container.Instance);
        }

        public InventoryError RestoreInventoryItem(IItemContainer owner, Identity container, IItem item)
        {
            return owner.BaseInventory.AddToPage((int)container.Type, container.Instance, item);
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

            if (PetShellItemService.Default.TryUsePetShell(character, itemPosition, item))
            {
                return true;
            }

            if (PetShellItemService.IsPetShellItem(item))
            {
                return true;
            }

            // Capture 20260806-rabbit: Use sealed Quabbit (301782) → grant opened 301749 + consume sealed.
            if (SealedQuabbitOpenRuntime.TryHandleUse(character, itemPosition, item))
            {
                return true;
            }

            if (this.TryUseHealthAndNanoRecharger(character, itemPosition, item))
            {
                return true;
            }

            if (this.TryUseHealthAndNanoStim(character, itemPosition, item))
            {
                return true;
            }

            // Capture 20260721-lockpick: Use sealed Lock Pick package (295999) → grant 95577 + tip.
            if (StanGoodmanQuestRuntime.TryHandleSealedLockpickUse(character, itemPosition, item))
            {
                return true;
            }

            // Capture 20260721-nano-enforcer-arete: Use Marco Enforcer crystal → Overflow nanos + tip.
            if (CapturedAreteMarcoSpidaNanoPackageRuntime.TryHandleCrystalUse(character, itemPosition, item))
            {
                return true;
            }

            // Capture 20260721-nanoprogramsvendor: Use Marco nano crystal → complete tip 555BE9F4.
            StanGoodmanQuestRuntime.TryCompleteBuyNanoTipOnCrystalUse(character, item);

            // Capture 20260723-123341: every token-board Use → FormatFeedback first, then upgrade funcs.
            if (TokenBoardRuntime.TryHandleUse(character, itemPosition, item))
            {
                return true;
            }

            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);

            // Sacred Thrak garden key (226994) is permanent — never consumed on Use.
            bool isSacredGardenKey =
                ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(item.LowID, item.HighID);

            if (!isSacredGardenKey
                && ItemLoader.ItemList[item.HighID].IsConsumable()
                && !this.IsHealthAndNanoRecharger(item))
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

        private bool TryUseHealthAndNanoStim(ICharacter character, Identity itemPosition, Item item)
        {
            if (!this.IsHealthAndNanoStim(item))
            {
                return false;
            }

            Character concrete = character as Character;
            if (concrete == null)
            {
                return false;
            }

            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);

            int healthAmount;
            int nanoAmount;
            int lockStatId = (int)StatIds.firstaid;
            int lockDurationSeconds = 40;
            this.ResolveVitalItemEffects(
                item,
                (int)StatIds.firstaid,
                40,
                ResolveHealthAndNanoStimAmount(item.Quality),
                out healthAmount,
                out nanoAmount,
                out lockStatId,
                out lockDurationSeconds);

            this.ApplyVitalRestore(concrete, healthAmount, nanoAmount);

            FunctionCollection.Instance.CallFunction(
                (int)FunctionType.LockSkill,
                concrete,
                concrete,
                concrete,
                new MessagePackObject[] { lockStatId, lockDurationSeconds });

            // Stims are consumed; rechargers are not.
            this.ConsumeInventoryStackItem(character, itemPosition, item);

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "HealthAndNanoStim used char={0} item={1}/{2} ql={3} heal={4} nano={5} lockStat={6} lockSecs={7}",
                    character.Identity.ToString(true),
                    item.LowID,
                    item.HighID,
                    item.Quality,
                    healthAmount,
                    nanoAmount,
                    lockStatId,
                    lockDurationSeconds));

            return true;
        }

        private bool TryUseHealthAndNanoRecharger(ICharacter character, Identity itemPosition, Item item)
        {
            if (!this.IsHealthAndNanoRecharger(item))
            {
                return false;
            }

            Character concrete = character as Character;
            if (concrete == null)
            {
                return false;
            }

            // Item OnUse Hit/LockSkill functions carry Sitting/InDuel requirements that our
            // requirement runtime often fails even when sitting. Apply QL-interpolated Hits and
            // Treatment lock directly. Never consume — rechargers are reusable.
            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);

            int healthAmount;
            int nanoAmount;
            int lockStatId = (int)StatIds.treatment;
            int lockDurationSeconds = 15;
            this.ResolveVitalItemEffects(
                item,
                (int)StatIds.treatment,
                15,
                ResolveHealthAndNanoRechargerAmount(item.Quality),
                out healthAmount,
                out nanoAmount,
                out lockStatId,
                out lockDurationSeconds);

            this.ApplyVitalRestore(concrete, healthAmount, nanoAmount);

            FunctionCollection.Instance.CallFunction(
                (int)FunctionType.LockSkill,
                concrete,
                concrete,
                concrete,
                new MessagePackObject[] { lockStatId, lockDurationSeconds });

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "HealthAndNanoRecharger used char={0} item={1}/{2} ql={3} heal={4} nano={5} lockStat={6} lockSecs={7}",
                    character.Identity.ToString(true),
                    item.LowID,
                    item.HighID,
                    item.Quality,
                    healthAmount,
                    nanoAmount,
                    lockStatId,
                    lockDurationSeconds));

            return true;
        }

        private void ResolveVitalItemEffects(
            Item item,
            int defaultLockStatId,
            int defaultLockDurationSeconds,
            int fallbackAmount,
            out int healthAmount,
            out int nanoAmount,
            out int lockStatId,
            out int lockDurationSeconds)
        {
            healthAmount = 0;
            nanoAmount = 0;
            lockStatId = defaultLockStatId;
            lockDurationSeconds = defaultLockDurationSeconds;

            foreach (Event itemEvent in item.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function itemFunction in itemEvent.Functions)
                {
                    MessagePackObject[] arguments = itemFunction.Arguments.Values.ToArray();
                    if (itemFunction.FunctionType == (int)FunctionType.Hit && arguments.Length >= 2)
                    {
                        int statNumber = arguments[0].AsInt32();
                        int delta = Math.Abs(hit.ResolveHitDelta(arguments));
                        if (statNumber == (int)StatIds.health || statNumber == (int)StatIds.life)
                        {
                            healthAmount = Math.Max(healthAmount, delta);
                        }
                        else if (statNumber == (int)StatIds.currentnano
                                 || statNumber == (int)StatIds.nanoenergypool
                                 || statNumber == (int)StatIds.maxnanoenergy)
                        {
                            nanoAmount = Math.Max(nanoAmount, delta);
                        }

                        continue;
                    }

                    if (itemFunction.FunctionType == (int)FunctionType.LockSkill)
                    {
                        int parsedStat;
                        int parsedDuration;
                        if (lockskill.TryReadArguments(arguments, out parsedStat, out parsedDuration))
                        {
                            lockStatId = parsedStat;
                            lockDurationSeconds = parsedDuration;
                        }
                    }
                }
            }

            if (healthAmount <= 0)
            {
                healthAmount = fallbackAmount;
            }

            if (nanoAmount <= 0)
            {
                nanoAmount = fallbackAmount;
            }
        }

        private void ApplyVitalRestore(Character character, int healthAmount, int nanoAmount)
        {
            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int currentHealth = character.Stats[StatIds.health].Value;
            int healthApplied = Math.Min(Math.Max(0, healthAmount), Math.Max(0, maxLife - currentHealth));
            if (healthApplied > 0)
            {
                int newHealth = currentHealth + healthApplied;
                character.Stats[StatIds.health].Value = newHealth;
                character.Stats[StatIds.health].BaseValue = (uint)newHealth;
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.health, (uint)newHealth);
                ChatTextMessageHandler.Default.Send(
                    character,
                    string.Format("You healed yourself for {0} points.", healthApplied));
            }

            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);
            int currentNano = character.Stats[StatIds.currentnano].Value;
            int nanoApplied = Math.Min(Math.Max(0, nanoAmount), Math.Max(0, maxNano - currentNano));
            if (nanoApplied > 0)
            {
                int newNano = currentNano + nanoApplied;
                character.Stats[StatIds.currentnano].Value = newNano;
                character.Stats[StatIds.currentnano].BaseValue = (uint)newNano;
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.currentnano, (uint)newNano);
            }

            if (character.Controller != null)
            {
                character.Controller.SendChangedStats();
            }
        }

        private static int ResolveHealthAndNanoRechargerAmount(int quality)
        {
            // Live AO: QL1 = 200, QL100 = 5000 (linear between templates 291082/291083).
            const int amountQl1 = 200;
            const int amountQl100 = 5000;
            int ql = Math.Max(1, Math.Min(100, quality));
            if (ql <= 1)
            {
                return amountQl1;
            }

            return amountQl1 + ((amountQl100 - amountQl1) * (ql - 1) / 99);
        }

        private static int ResolveHealthAndNanoStimAmount(int quality)
        {
            // Live AO: QL1 = 30, QL200 = 2400 (linear between templates 291043/291044).
            const int amountQl1 = 30;
            const int amountQl200 = 2400;
            int ql = Math.Max(1, Math.Min(200, quality));
            if (ql <= 1)
            {
                return amountQl1;
            }

            return amountQl1 + ((amountQl200 - amountQl1) * (ql - 1) / 199);
        }

        private bool IsHealthAndNanoRecharger(Item item)
        {
            const int rechargerLow = 291082;
            const int rechargerHigh = 291083;

            return item.LowID == rechargerLow
                   || item.HighID == rechargerLow
                   || item.LowID == rechargerHigh
                   || item.HighID == rechargerHigh;
        }

        private bool IsHealthAndNanoStim(Item item)
        {
            const int stimLow = 291043;
            const int stimHigh = 291044;

            return item.LowID == stimLow
                   || item.HighID == stimLow
                   || item.LowID == stimHigh
                   || item.HighID == stimHigh;
        }

        private void ConsumeInventoryStackItem(ICharacter character, Identity itemPosition, Item item)
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
                return;
            }

            IInventoryPage page;
            if (character.BaseInventory.Pages.TryGetValue((int)itemPosition.Type, out page))
            {
                page.Write();
            }
        }

        public bool DeleteInventoryItemAction(ICharacter character, CharacterActionMessage message)
        {
            if (character == null || message == null || message.Target == null)
            {
                return false;
            }

            IItem existing = null;
            try
            {
                IInventoryPage page;
                if (character.BaseInventory != null
                    && character.BaseInventory.Pages.TryGetValue((int)message.Target.Type, out page)
                    && page != null)
                {
                    existing = page[message.Target.Instance];
                }
            }
            catch (Exception)
            {
            }

            // Client may send DeleteItem when using a CanFlags-consumable template; sacred key stays.
            if (existing != null
                && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(existing.LowID, existing.HighID))
            {
                return false;
            }

            // Parameter1/2 sometimes carry the template ids on client-initiated deletes.
            if (ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(message.Parameter1, message.Parameter2))
            {
                return false;
            }

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
            return true;
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
                    if (this.UseInventoryItem(client.Controller.Character, target))
                    {
                        ICharacter usedCharacter = client.Controller.Character;
                        GenericCmdMessageHandler.Default.Acknowledge(usedCharacter, message);

                        // Capture 20260723-123341: AppearanceUpdate after Use ACK when Shouldermesh ran.
                        Character concrete = usedCharacter as Character;
                        if (concrete != null && concrete.ChangedAppearance)
                        {
                            AppearanceUpdateMessageHandler.Default.Send(concrete);
                            concrete.ChangedAppearance = false;
                        }
                    }
                    else
                    {
                        // Prefer Denied so client does not consume Stack/Empty charges on failed uses
                        // (e.g. Treatment skill locked).
                        GenericCmdMessageHandler.Default.AcknowledgeDenied(client.Controller.Character, message);
                    }

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

            if (message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            IInventoryPage sourcePage = null;
            ICharacter character = client.Controller.Character;
            if (character.BaseInventory == null
                || !character.BaseInventory.Pages.TryGetValue((int)message.Target[0].Type, out sourcePage)
                || sourcePage == null)
            {
                sourcePage =
                    Pool.Instance.GetObject<IInventoryPage>(
                        new Identity()
                        {
                            Type = (IdentityType)character.Identity.Instance,
                            Instance = (int)message.Target[0].Type
                        });
            }

            if (sourcePage == null)
            {
                return false;
            }

            IItem item = sourcePage[message.Target[0].Instance];
            if (item == null)
            {
                return false;
            }

            character.Stats[StatIds.secondaryitemtemplate].Value = item.LowID;
            //client.Controller.Character.Stats[StatIds.secondaryitemtype]
            character.DoNotDoTimers = false;
            try
            {
                character.Stats[StatIds.expansion].Value =
                    character.Stats[StatIds.expansion].Value | 2;
            }
            catch
            {
            }

            if (Pool.Instance.Contains(message.Target[1]))
            {
                StaticDynel temp =
                    Pool.Instance.GetObject<StaticDynel>(
                        character.Playfield.Identity,
                        message.Target[1]);
                if (temp == null)
                {
                    return false;
                }

                Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                if (ev == null)
                {
                    return false;
                }

                int playfieldId = character.Playfield.Identity.Instance;
                bool zoneStatueUse =
                    NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(playfieldId)
                    && message.Target[1].Type == IdentityType.Terminal;

                // Consume insignias used on a Shadowlands zone statue BEFORE the teleport.
                // Real AO order is DeleteItem → N3Teleport (capture 20260716-using insignia).
                // The teleport reloads the character's inventory, so consuming afterwards operated on a
                // stale item reference and never removed the insignia.
                // Sacred Thrak garden key (226994) is permanent and must NOT be consumed.
                if (zoneStatueUse
                    && !ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(item.LowID, item.HighID))
                {
                    Item concreteItem = item as Item;
                    if (concreteItem != null)
                    {
                        int statueTemplateId = temp.Template != null ? temp.Template.ID : 0;
                        this.ConsumeInventoryStackItem(
                            character,
                            message.Target[0],
                            concreteItem);
                        client.Server.Info(
                            client,
                            "Shadowlands statue insignia consumed char={0} item={1} statue={2} slot={3}",
                            character.Identity,
                            item.LowID,
                            statueTemplateId,
                            message.Target[0]);
                    }
                }

                ev.Perform(character, temp);

                return true;
            }

            // Pool miss: zone-return statues belong to Nascence handler (consume + catalog teleport).
            if (character.Playfield != null
                && NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(
                    character.Playfield.Identity.Instance)
                && message.Target[1].Type == IdentityType.Terminal)
            {
                return false;
            }

            client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);
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
            Identity ackSourceContainer = message.SourceContainer;
            IItem itemFrom = sendingPage[fromPlacement];
            if (itemFrom == null
                && this.TryResolveCarriedItemForImplantEquip(
                    character,
                    message.TargetPlacement,
                    ref sendingPage,
                    ref fromPlacement,
                    out itemFrom))
            {
                ackSourceContainer = new Identity
                {
                    Type = (IdentityType)sendingPage.Identity.Type,
                    Instance = fromPlacement
                };
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory remapped empty source to carried item={0}/{1} page={2} slot={3} character={4}",
                        itemFrom.LowID,
                        itemFrom.HighID,
                        sendingPage.GetType().Name,
                        fromPlacement,
                        character.Identity));
            }

            if (itemFrom == null)
            {
                // Client phantom HUD/equipment: slot looks filled client-side but server page is empty.
                // ACK + UnEquip so the client clears the stuck visual and unequip can proceed.
                if (sendingPage is WeaponInventoryPage || this.IsAppearanceEquipmentPage(sendingPage))
                {
                    IInventoryPage phantomTarget =
                        this.ResolveMoveTargetPage(character, message.TargetPlacement);
                    int phantomAck = message.TargetPlacement;
                    if (phantomTarget != null)
                    {
                        int phantomSlot = this.ResolveConcreteTargetSlot(
                            phantomTarget,
                            message.TargetPlacement);
                        if (phantomSlot >= 0)
                        {
                            phantomAck = phantomSlot;
                        }
                    }

                    UnEquip.Send(client, sendingPage, fromPlacement);
                    this.SendMoveItemToInventoryAck(
                        character,
                        message.SourceContainer,
                        phantomAck);
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "ClientMoveItemToInventory cleared phantom equip source={0} targetPlacement={1} character={2}",
                            message.SourceContainer,
                            message.TargetPlacement,
                            character.Identity));
                    return true;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory source slot is empty source={0} targetPlacement={1} character={2}",
                        message.SourceContainer,
                        message.TargetPlacement,
                        character.Identity));
                ChatTextMessageHandler.Default.Send(
                    character,
                    "That item is not in the expected inventory slot. Move it into your inventory and try again.");
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

            int toPlacement = this.ResolveConcreteTargetSlot(receivingPage, message.TargetPlacement);
            int ackTargetPlacement = toPlacement >= 0 ? toPlacement : message.TargetPlacement;

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

                if (receivingPage.NeedsItemCheck
                    && !this.CanEquipToPage(character, receivingPage, itemFrom, toPlacement, itemTo))
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
                    ChatTextMessageHandler.Default.Send(
                        character,
                        "You do not meet the requirements to equip that item.");
                    return true;
                }

                if (itemTo != null)
                {
                    if (affectsAppearance)
                    {
                        this.WaitForEquipVisualSync(itemFrom, itemTo, receivingPage is SocialArmorInventoryPage);
                    }

                    if (VehicleHudWearRuntime.IsVehicleItem(itemTo))
                    {
                        VehicleHudWearRuntime.NoteUnequipped(character, itemTo);
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
                    ackSourceContainer,
                    ackTargetPlacement);
                Equip.Send(client, receivingPage, toPlacement);

                // WIFU must follow the hand move. Sending before Equip used the bag slot in Unknown2,
                // so the client never treated the gun as worn and hid Actions→Reload.
                if (toPlacement == (int)WeaponSlots.Righthand
                    || toPlacement == (int)WeaponSlots.LeftHand)
                {
                    WeaponItemFullUpdate.SendWeaponDefinition(character, itemFrom);
                }

                if (VehicleHudWearRuntime.IsVehicleItem(itemFrom))
                {
                    // Save prior MonsterScale/morph before OnWear ChangeVariable/MonsterShape.
                    VehicleHudWearRuntime.NoteEquipped(character, itemFrom, toPlacement);
                }

                character.CalculateSkills();
                this.EnsureWeaponVisualMeshes(character, true);
                this.PersistClientMoveItemToInventory(character, "equip");
                if (receivingPage is ImplantInventoryPage)
                {
                    DoctorMasonQuestRuntime.OnGiftImplantEquipped(
                        character,
                        itemFrom.LowID,
                        itemFrom.HighID);
                }

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
                if (VehicleHudWearRuntime.IsVehicleItem(itemFrom))
                {
                    VehicleHudWearRuntime.NoteUnequipped(character, itemFrom);
                }

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

            int toPlacement = this.ResolveConcreteTargetSlot(receivingPage, message.TargetPlacement);

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

        private bool TryResolveCarriedItemForImplantEquip(
            ICharacter character,
            int targetPlacement,
            ref IInventoryPage sendingPage,
            ref int fromPlacement,
            out IItem itemFrom)
        {
            itemFrom = null;
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage targetPage = this.ResolveMoveTargetPage(character, targetPlacement);
            if (!(targetPage is ImplantInventoryPage))
            {
                return false;
            }

            // Capture 20260721-184426: ClientMoveItemToInventory Inventory:slot → ImplantPage:0x2B.
            // Quest gift packets advertise Overflow while TryAdd lands on Inventory — client/server
            // slot can diverge. Recover Mason gift 295706 from carried pages.
            IInventoryPage inventory;
            IInventoryPage overflow;
            character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventory);
            character.BaseInventory.Pages.TryGetValue((int)IdentityType.OverflowWindow, out overflow);

            int foundSlot;
            IItem foundItem;
            if (this.TryFindGiftImplantOnPage(inventory, out foundSlot, out foundItem))
            {
                sendingPage = inventory;
                fromPlacement = foundSlot;
                itemFrom = foundItem;
                return true;
            }

            if (this.TryFindGiftImplantOnPage(overflow, out foundSlot, out foundItem))
            {
                sendingPage = overflow;
                fromPlacement = foundSlot;
                itemFrom = foundItem;
                return true;
            }

            return false;
        }

        private bool TryFindGiftImplantOnPage(
            IInventoryPage page,
            out int slot,
            out IItem item)
        {
            slot = -1;
            item = null;
            if (page == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IItem> entry in page.List())
            {
                if (entry.Value == null)
                {
                    continue;
                }

                if (entry.Value.LowID == 295706 || entry.Value.HighID == 295706)
                {
                    slot = entry.Key;
                    item = entry.Value;
                    return true;
                }
            }

            return false;
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
            // Client often sends page-type markers (Inventory=0x68, Overflow=0x6E, 0x6F) instead of
            // a concrete inventory slot when unequipping HUD/weapons into the bag.
            if (targetPlacement == (int)IdentityType.TradeWindow
                || targetPlacement == (int)IdentityType.Inventory
                || targetPlacement == (int)IdentityType.OverflowWindow
                || targetPlacement == 0x6F)
            {
                return character.BaseInventory.Pages[character.BaseInventory.StandardPage];
            }

            try
            {
                return character.BaseInventory.PageFromSlot(targetPlacement);
            }
            catch (Exception)
            {
                // Fallback: treat unknown high placements as "into inventory".
                if (targetPlacement > 94)
                {
                    IInventoryPage inventory;
                    if (character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventory))
                    {
                        return inventory;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Normalize unequip/equip destination when the client sent a page marker or an occupied slot.
        /// Weapon/armor/implant concrete slots must HotSwap when occupied — never spill to Hud1
        /// via FindFreeSlot (RH occupied + r-click wield was parking the new weapon on Hud1).
        /// </summary>
        private int ResolveConcreteTargetSlot(IInventoryPage receivingPage, int requestedPlacement)
        {
            if (receivingPage == null)
            {
                return -1;
            }

            bool isPageMarker = requestedPlacement == (int)IdentityType.TradeWindow
                                || requestedPlacement == (int)IdentityType.Inventory
                                || requestedPlacement == (int)IdentityType.OverflowWindow
                                || requestedPlacement == 0x6F
                                || requestedPlacement < receivingPage.FirstSlotNumber
                                || requestedPlacement >= receivingPage.FirstSlotNumber + receivingPage.MaxSlots;

            if (!isPageMarker)
            {
                // Equipment pages: honor the client's exact slot so occupied RH/LH HotSwap.
                if (receivingPage is IEquipmentPage)
                {
                    return requestedPlacement;
                }

                try
                {
                    if (receivingPage[requestedPlacement] == null)
                    {
                        return requestedPlacement;
                    }
                }
                catch (Exception)
                {
                }
            }

            return receivingPage.FindFreeSlot();
        }

        private bool CanEquipToPage(
            ICharacter character,
            IInventoryPage page,
            IItem item,
            int targetSlot,
            IItem currentlyEquipped)
        {
            // Capture 20260721-Mason: after surgery clinic, gift leg 295706 equips to ImplantPage:002B.
            // Clinic nano Treatment can still be short if OnUse missed; allow the quest gift while
            // implant access is active so Install tip can complete.
            if (page is ImplantInventoryPage
                && DoctorMasonQuestRuntime.ShouldAllowGiftImplantEquip(character, item))
            {
                return true;
            }

            AOAction action = null;
            if ((page is ArmorInventoryPage) || (page is ImplantInventoryPage))
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWear);
            }
            else if (page is WeaponInventoryPage)
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWield);
                if (VehicleHudWearRuntime.IsVehicleItem(item)
                    && !VehicleHudWearRuntime.AllowsWeaponSlot(item, targetSlot))
                {
                    return false;
                }

                if (VehicleHudWearRuntime.IsVehicleItem(item))
                {
                    return VehicleHudWearRuntime.EvaluateVehicleWieldRequirements(
                        character,
                        item,
                        currentlyEquipped,
                        () => action == null || action.CheckRequirements(character));
                }
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

        public bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            return InventoryItemRules.HasSameUniqueItem(
                item,
                character.BaseInventory.Pages.Values.SelectMany(page => page.List()).Select(existing => existing.Value));
        }

        public bool HasCharacterInventory(ICharacter character)
        {
            return character != null && character.BaseInventory != null;
        }

        public bool CharacterHasItemInCarriedInventory(ICharacter source, int itemId)
        {
            IInventoryPage page;
            if (source.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out page)
                && InventoryPageHasItem(page, itemId))
            {
                return true;
            }

            return source.BaseInventory.Pages.TryGetValue((int)IdentityType.OverflowWindow, out page)
                   && InventoryPageHasItem(page, itemId);
        }

        public int CountCharacterItemInCarriedInventory(ICharacter source, int itemId)
        {
            if (source == null || source.BaseInventory == null)
            {
                return 0;
            }

            int count = 0;
            IInventoryPage page;
            if (source.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out page))
            {
                count += CountInventoryPageItems(page, itemId);
            }

            if (source.BaseInventory.Pages.TryGetValue((int)IdentityType.OverflowWindow, out page))
            {
                count += CountInventoryPageItems(page, itemId);
            }

            return count;
        }

        public QuestRewardInventoryGrantResult TryGrantQuestRewardItem(ICharacter source, Item item)
        {
            InventoryError inventoryError = source.BaseInventory.TryAdd(item);
            if (inventoryError != InventoryError.OK)
            {
                return QuestRewardInventoryGrantResult.InventoryAddFailed(inventoryError);
            }

            try
            {
                bool persisted = source.BaseInventory.Write();
                if (!persisted)
                {
                    RollBackQuestRewardItem(source, item);
                    return QuestRewardInventoryGrantResult.PersistReturnedFalse();
                }
            }
            catch (Exception e)
            {
                RollBackQuestRewardItem(source, item);
                return QuestRewardInventoryGrantResult.PersistFailed(e.Message);
            }

            return QuestRewardInventoryGrantResult.Succeeded();
        }

        private static void RollBackQuestRewardItem(ICharacter source, IItem item)
        {
            foreach (IInventoryPage page in source.BaseInventory.Pages.Values)
            {
                foreach (KeyValuePair<int, IItem> entry in page.List().ToList())
                {
                    if (object.ReferenceEquals(entry.Value, item))
                    {
                        page.Remove(entry.Key);
                        return;
                    }
                }
            }
        }

        public CorpseLootInventoryTransferResult TryAddCorpseLootItem(
            ICharacter looter,
            IItem item,
            int targetPlacement)
        {
            var result = new CorpseLootInventoryTransferResult();
            int targetPageNumber;
            int targetSlot;
            if (!this.TryResolveCorpseLootTargetSlot(looter, targetPlacement, out targetPageNumber, out targetSlot))
            {
                result.Status = CorpseLootInventoryTransferStatus.NoFreeSlot;
                return result;
            }

            result.TargetPageNumber = targetPageNumber;
            result.TargetSlot = targetSlot;

            InventoryError inventoryError;
            try
            {
                inventoryError = looter.BaseInventory.AddToPage(targetPageNumber, targetSlot, item);
            }
            catch (Exception e)
            {
                result.Status = CorpseLootInventoryTransferStatus.AddFailed;
                result.ExceptionMessage = e.Message;
                return result;
            }

            result.InventoryError = inventoryError;
            if (inventoryError != InventoryError.OK)
            {
                result.Status = CorpseLootInventoryTransferStatus.AddRejected;
                return result;
            }

            looter.BaseInventory.Write();
            result.Status = CorpseLootInventoryTransferStatus.Success;
            return result;
        }

        private static int DecodeBackpackHandle(Identity sourceContainer)
        {
            return (int)(((uint)sourceContainer.Instance >> 16) & 0xffff);
        }

        private bool TryResolveCorpseLootTargetSlot(
            ICharacter looter,
            int targetPlacement,
            out int targetPageNumber,
            out int targetSlot)
        {
            targetPageNumber = -1;
            targetSlot = -1;

            if (targetPlacement == CombatCorpseRules.MoveToInventoryPlacement)
            {
                targetPageNumber = looter.BaseInventory.StandardPage;
                IInventoryPage targetPage = looter.BaseInventory.Pages[targetPageNumber];
                targetSlot = targetPage.FindFreeSlot();
                return targetSlot >= 0;
            }

            try
            {
                IInventoryPage targetPage = looter.BaseInventory.PageFromSlot(targetPlacement);
                if (targetPage == null)
                {
                    return false;
                }

                foreach (KeyValuePair<int, IInventoryPage> page in looter.BaseInventory.Pages)
                {
                    if (object.ReferenceEquals(page.Value, targetPage))
                    {
                        targetPageNumber = page.Key;
                        targetSlot = targetPlacement;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                targetPageNumber = looter.BaseInventory.StandardPage;
                IInventoryPage targetPage = looter.BaseInventory.Pages[targetPageNumber];
                targetSlot = targetPage.FindFreeSlot();
                return targetSlot >= 0;
            }
        }

        private static bool InventoryPageHasItem(IInventoryPage page, int itemId)
        {
            foreach (KeyValuePair<int, IItem> itemEntry in page.List())
            {
                IItem item = itemEntry.Value;
                if (item != null && (item.LowID == itemId || item.HighID == itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountInventoryPageItems(IInventoryPage page, int itemId)
        {
            int count = 0;
            foreach (KeyValuePair<int, IItem> itemEntry in page.List())
            {
                IItem item = itemEntry.Value;
                if (item != null && (item.LowID == itemId || item.HighID == itemId))
                {
                    count++;
                }
            }

            return count;
        }

        private static IItem CloneShopItem(IItem item)
        {
            Item concreteItem = item as Item;
            if (concreteItem == null)
            {
                return item;
            }

            Item copy = new Item(concreteItem.Quality, concreteItem.LowID, concreteItem.HighID);
            copy.MultipleCount = concreteItem.MultipleCount;
            return copy;
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
                // Replace when mesh differs — stale mesh must not block weapon visuals.
                if (existing.Mesh > 0
                    && existing.Mesh != 1234567890
                    && existing.Mesh == meshId)
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

    public sealed class CorpseLootInventoryTransferResult
    {
        public CorpseLootInventoryTransferResult()
        {
            this.Status = CorpseLootInventoryTransferStatus.NoFreeSlot;
            this.TargetPageNumber = -1;
            this.TargetSlot = -1;
            this.InventoryError = InventoryError.OK;
        }

        public CorpseLootInventoryTransferStatus Status { get; set; }

        public int TargetPageNumber { get; set; }

        public int TargetSlot { get; set; }

        public InventoryError InventoryError { get; set; }

        public string ExceptionMessage { get; set; }

        public bool Succeeded
        {
            get
            {
                return this.Status == CorpseLootInventoryTransferStatus.Success;
            }
        }
    }

    public enum CorpseLootInventoryTransferStatus
    {
        Success = 0,
        NoFreeSlot = 1,
        AddFailed = 2,
        AddRejected = 3
    }

    public sealed class QuestRewardInventoryGrantResult
    {
        private QuestRewardInventoryGrantResult()
        {
        }

        public QuestRewardInventoryGrantStatus Status { get; private set; }

        public InventoryError InventoryError { get; private set; }

        public string ExceptionMessage { get; private set; }

        public static QuestRewardInventoryGrantResult Succeeded()
        {
            return new QuestRewardInventoryGrantResult
                   {
                       Status = QuestRewardInventoryGrantStatus.Success,
                       InventoryError = InventoryError.OK
                   };
        }

        public static QuestRewardInventoryGrantResult InventoryAddFailed(InventoryError inventoryError)
        {
            return new QuestRewardInventoryGrantResult
                   {
                       Status = QuestRewardInventoryGrantStatus.InventoryAddFailed,
                       InventoryError = inventoryError
                   };
        }

        public static QuestRewardInventoryGrantResult PersistFailed(string exceptionMessage)
        {
            return new QuestRewardInventoryGrantResult
                   {
                       Status = QuestRewardInventoryGrantStatus.PersistFailed,
                       InventoryError = InventoryError.OK,
                       ExceptionMessage = exceptionMessage
                   };
        }

        public static QuestRewardInventoryGrantResult PersistReturnedFalse()
        {
            return new QuestRewardInventoryGrantResult
                   {
                       Status = QuestRewardInventoryGrantStatus.PersistReturnedFalse,
                       InventoryError = InventoryError.OK
                   };
        }
    }

    public enum QuestRewardInventoryGrantStatus
    {
        Success = 0,
        InventoryAddFailed = 1,
        PersistFailed = 2,
        PersistReturnedFalse = 3
    }

    public sealed class InventoryItemAddResult
    {
        private InventoryItemAddResult()
        {
            this.TargetSlot = -1;
            this.InventoryError = InventoryError.OK;
        }

        public InventoryItemAddStatus Status { get; private set; }

        public int TargetSlot { get; private set; }

        public InventoryError InventoryError { get; private set; }

        public bool Succeeded
        {
            get
            {
                return this.Status == InventoryItemAddStatus.Success;
            }
        }

        public static InventoryItemAddResult Success(int targetSlot)
        {
            return new InventoryItemAddResult
                   {
                       Status = InventoryItemAddStatus.Success,
                       TargetSlot = targetSlot,
                       InventoryError = InventoryError.OK
                   };
        }

        public static InventoryItemAddResult NoFreeSlot()
        {
            return new InventoryItemAddResult
                   {
                       Status = InventoryItemAddStatus.NoFreeSlot,
                       InventoryError = InventoryError.OK
                   };
        }

        public static InventoryItemAddResult Failed(int targetSlot, InventoryError inventoryError)
        {
            return new InventoryItemAddResult
                   {
                       Status = InventoryItemAddStatus.Failed,
                       TargetSlot = targetSlot,
                       InventoryError = inventoryError
                   };
        }
    }

    public enum InventoryItemAddStatus
    {
        Success = 0,
        NoFreeSlot = 1,
        Failed = 2
    }
}
