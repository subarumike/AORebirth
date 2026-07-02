#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class TradeMessageHandler : BaseMessageHandler<TradeMessage, TradeMessageHandler>
    {
        public TradeMessageHandler()
        {
            this.UpdateCharacterStatsOnReceive = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        protected override void Read(TradeMessage message, IZoneClient client)
        {
            client.Server.Info(
                client,
                "Trade action={0}({1}) identity={2} unknown={3} marker={4} p1={5} p2={6} p3={7} p4={8} target={9} container={10}",
                message.Action,
                (int)message.Action,
                message.Identity,
                message.Unknown,
                message.Unknown1,
                message.Param1,
                message.Param2,
                message.Param3,
                message.Param4,
                message.Target,
                message.Container);

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_RX char=" + client.Controller.Character.Identity.ToString(true)
                + " name=" + client.Controller.Character.Name
                + " action=" + message.Action
                + " p1=" + message.Param1
                + " p2=" + message.Param2
                + " p3=" + message.Param3
                + " p4=" + message.Param4
                + " target=" + message.Target.ToString(true)
                + " container=" + message.Container.ToString(true));

            IItemContainer target;
            if ((message.Action != TradeAction.Accept)
                && (message.Action != TradeAction.Confirm)
                && (message.Action != TradeAction.UpdateCredits)
                && (message.Action != TradeAction.Decline))
            {
                target = Pool.Instance.GetObject<IItemContainer>(
                    client.Controller.Character.Playfield.Identity,
                    message.Target);
            }
            switch (message.Action)
            {
                case TradeAction.Open:
                {
                    if (ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(
                        client.Controller.Character,
                        message.Target))
                    {
                        break;
                    }

                    if (this.TryStartPlayerTrade(client.Controller.Character, message.Target))
                    {
                        break;
                    }

                    ICharacter isnpc =
                        Pool.Instance.GetObject<ICharacter>(
                            client.Controller.Character.Playfield.Identity,
                            message.Target);
                    if (isnpc != null)
                    {
                        NPCController controller = isnpc.Controller as NPCController;
                        if (controller != null)
                        {
                            controller.Trade(message.Identity);
                        }
                    }
                    break;
                }
                case TradeAction.AddItem:
                {
                    if (client.Controller.Character.ShoppingBag == null)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Shopping,
                            "Trade AddItem ignored because character has no active trade bag.");
                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            "No active trade session.");
                        break;
                    }

                    TemporaryBag shoppingBag = client.Controller.Character.ShoppingBag;
                    bool vendorShopBag =
                        shoppingBag.Vendor.Type == IdentityType.VendingMachine;

                    IItemContainer issuer = this.ResolveTradeIssuer(client.Controller.Character, message.Target);

                    if (issuer != null)
                    {
                        IItem item;

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
                            break;
                        }

                        if (item != null)
                        {
                            if (!vendorShopBag
                                && !(issuer is Vendor)
                                && this.TryAddPlayerTradeItem(
                                client.Controller.Character,
                                issuer,
                                shoppingBag,
                                message))
                            {
                                break;
                            }

                            if (issuer is Vendor)
                            {
                                shoppingBag.Add(
                                    new Identity() { Instance = message.Container.Instance },
                                    CloneShopItem(item));
                            }
                            else
                            {
                                shoppingBag.Add(
                                    message.Target,
                                    issuer.BaseInventory.RemoveItem(
                                        (int)message.Container.Type,
                                        message.Container.Instance));
                            }
                            this.AcknowledgeTradeAction(client.Controller.Character, message);
                        }
                    }
                    break;
                }
                case TradeAction.RemoveItem:
                {
                    if (client.Controller.Character.ShoppingBag == null)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Shopping,
                            "Trade RemoveItem ignored because character has no active trade bag.");
                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            "No active trade session.");
                        break;
                    }

                    IItemContainer issuer = this.ResolveTradeIssuer(client.Controller.Character, message.Target);

                    if (issuer != null)
                    {
                        IItem item;

                        TemporaryBag shoppingBag = client.Controller.Character.ShoppingBag;
                        if (this.TryRemovePlayerTradeItem(
                            client.Controller.Character,
                            issuer,
                            shoppingBag,
                            message))
                        {
                            break;
                        }

                        if (issuer is Vendor)
                        {
                            item = issuer.BaseInventory.GetItemInContainer(
                                (int)IdentityType.Inventory,
                                message.Container.Instance);
                        }
                        else
                        {
                            item = shoppingBag.GetSoldItems()[message.Container.Instance];
                        }
                        if (item != null)
                        {
                            if (issuer is Vendor)
                            {
                                shoppingBag.Remove(
                                    new Identity() { Instance = message.Container.Instance },
                                    message.Container.Instance);

                                this.Send(
                                    client.Controller.Character,
                                    this.AcknowledgeRemove(shoppingBag.Shopper, message));
                                this.Send(
                                    client.Controller.Character,
                                    this.AcknowledgeRemove(shoppingBag.Vendor, message));
                            }
                            else
                            {
                                IItem returnedItem = shoppingBag.Remove(message.Target, message.Container.Instance);
                                if (returnedItem != null)
                                {
                                    InventoryError err =
                                        client.Controller.Character.BaseInventory.AddToPage(
                                            client.Controller.Character.BaseInventory.StandardPage,
                                            client.Controller.Character.BaseInventory.Pages[
                                                client.Controller.Character.BaseInventory.StandardPage].FindFreeSlot(),
                                            returnedItem);

                                    if (err == InventoryError.OK)
                                    {
                                        ContainerAddItemMessageHandler.Default.Send(
                                            client.Controller.Character,
                                            new Identity()
                                            {
                                                Type = IdentityType.KnuBotTradeWindow,
                                                Instance = message.Container.Instance
                                            },
                                            0x6f); // 0x6f = Next free slot in main inventory
                                    }
                                    else
                                    {
                                        // Cant return item code here
                                    }

                                    InventoryUpdatedMessageHandler.Default.Send(
                                        client.Controller.Character,
                                        shoppingBag.Vendor);
                                    InventoryUpdatedMessageHandler.Default.Send(
                                        client.Controller.Character,
                                        client.Controller.Character.Identity);
                                }
                            }
                            this.AcknowledgeTradeAction(client.Controller.Character, message);
                        }
                    }
                    break;
                }
                case TradeAction.Accept:
                case TradeAction.Confirm:
                {
                    if (client.Controller.Character.ShoppingBag == null)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Shopping,
                            "Trade Accept ignored because character has no active trade bag.");
                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            "No active trade session.");
                        break;
                    }

                    TemporaryBag shoppingBag = client.Controller.Character.ShoppingBag;
                    bool playerTradeBag =
                        shoppingBag.Shopper.Type == IdentityType.CanbeAffected
                        && shoppingBag.Vendor.Type == IdentityType.CanbeAffected;
                    if (playerTradeBag && this.TryEndPlayerTrade(client.Controller.Character, shoppingBag, message))
                    {
                        break;
                    }

                    Vendor vendor = Pool.Instance.GetObject<Vendor>(
                        client.Controller.Character.Playfield.Identity,
                        shoppingBag.Vendor);
                    IItemContainer issuer = client.Controller.Character;

                    if ((issuer != null) && (vendor != null))
                    {
                        if (shoppingBag != null)
                        {
                            IItem[] items;
                            try
                            {
                                items = shoppingBag.GetBoughtItems();
                            }
                            catch (Exception ex)
                            {
                                LogUtil.ErrorException(ex);
                                break;
                            }

                            IItem[] boughtItems = items;
                            IItem[] soldItems = shoppingBag.GetSoldItems();
                            int buyTotal = this.CalculateVendorBuyTotal(
                                client.Controller.Character,
                                vendor,
                                boughtItems);
                            int sellTotal = this.CalculateVendorSellTotal(
                                client.Controller.Character,
                                vendor,
                                soldItems);
                            int cash = buyTotal - sellTotal;
                            int currentCash = GetCash(client.Controller.Character);
                            long finalCashRaw = (long)currentCash - cash;

                            LogUtil.Debug(
                                DebugInfoDetail.Shopping,
                                "Vendor trade cash summary shopper=" + client.Controller.Character.Identity.ToString(true)
                                + " vendor=" + vendor.Identity.ToString(true)
                                + " boughtItems=" + boughtItems.Length
                                + " soldItems=" + soldItems.Length
                                + " buyTotal=" + buyTotal
                                + " sellTotal=" + sellTotal
                                + " cashDelta=" + cash
                                + " cashBefore=" + currentCash
                                + " cashAfterRaw=" + finalCashRaw);

                            if (finalCashRaw < 0 || finalCashRaw > CashStatRules.ClientSafeMaxCash)
                            {
                                LogUtil.Debug(
                                    DebugInfoDetail.Shopping,
                                    "Vendor trade rejected because cash would be invalid shopper="
                                    + client.Controller.Character.Identity.ToString(true)
                                    + " cashBefore=" + currentCash
                                    + " cashDelta=" + cash
                                    + " cashAfterRaw=" + finalCashRaw
                                    + " cap=" + CashStatRules.ClientSafeMaxCash);
                                ChatTextMessageHandler.Default.Send(
                                    client.Controller.Character,
                                    "Trade failed: credits are missing or credit cap would be exceeded.");
                                break;
                            }

                            if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(
                                    client.Controller.Character,
                                    boughtItems.Length))
                            {
                                ChatTextMessageHandler.Default.Send(
                                    client.Controller.Character,
                                    "Could not add item to inventory. (inventory is full)");
                                break;
                            }

                            foreach (IItem item in boughtItems)
                            {
                                int nextSlot = issuer.BaseInventory[issuer.BaseInventory.StandardPage].FindFreeSlot();
                                if (nextSlot != -1)
                                {
                                    InventoryError err = issuer.BaseInventory.AddToPage(
                                        issuer.BaseInventory.StandardPage,
                                        nextSlot,
                                        item);
                                    if (err == InventoryError.OK)
                                    {
                                        AddTemplateMessageHandler.Default.Send(client.Controller.Character, (Item)item);
                                    }
                                    else
                                    {
                                        ChatTextMessageHandler.Default.Send(
                                            client.Controller.Character,
                                            "Could not add item to inventory. (" + err + ")");
                                    }
                                }
                            }

                            SetCash(
                                client.Controller.Character,
                                CashStatRules.Clamp(finalCashRaw));

                            this.Send(
                                client.Controller.Character,
                                TradeAction.Complete,
                                shoppingBag.Vendor,
                                shoppingBag.Vendor);
                            client.Controller.SendChangedStats();
                            shoppingBag.Dispose();
                        }
                    }
                    break;
                }
                case TradeAction.UpdateCredits:
                {
                    if (client.Controller.Character.ShoppingBag == null)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Shopping,
                            "Trade UpdateCredits ignored because character has no active trade bag.");
                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            "No active trade session.");
                        break;
                    }

                    if (this.TrySetPlayerTradeCredits(
                        client.Controller.Character,
                        client.Controller.Character.ShoppingBag,
                        message))
                    {
                        break;
                    }

                    break;
                }
                case TradeAction.Decline:
                {
                    IItemContainer issuer = client.Controller.Character;
                    TemporaryBag shoppingBag = client.Controller.Character.ShoppingBag;

                    if (shoppingBag != null)
                    {
                        bool playerTradeBag =
                            shoppingBag.Shopper.Type == IdentityType.CanbeAffected
                            && shoppingBag.Vendor.Type == IdentityType.CanbeAffected;
                        if (playerTradeBag && this.TryDeclinePlayerTrade(client.Controller.Character, shoppingBag))
                        {
                            break;
                        }

                        this.SendVendorShopDeclineClose(client.Controller.Character);

                        try
                        {
                            IItem[] items = shoppingBag.GetSoldItems();
                            foreach (IItem item in items)
                            {
                                int nextSlot = issuer.BaseInventory[issuer.BaseInventory.StandardPage].FindFreeSlot();
                                if (nextSlot != -1)
                                {
                                    issuer.BaseInventory[issuer.BaseInventory.StandardPage].Add(nextSlot, item);
                                }
                            }
                        }
                        finally
                        {
                            shoppingBag.Dispose();
                        }
                    }
                    else
                    {
                        this.SendVendorShopDeclineClose(client.Controller.Character);
                    }

                    break;
                }
            }
        }

        private bool TryStartPlayerTrade(ICharacter initiator, Identity targetIdentity)
        {
            if (initiator == null || initiator.Playfield == null)
            {
                return false;
            }

            ICharacter target =
                Pool.Instance.GetObject<ICharacter>(
                    initiator.Playfield.Identity,
                    targetIdentity);
            if (target == null || target.Identity.Equals(initiator.Identity))
            {
                return false;
            }

            if (!(target.Controller is PlayerController))
            {
                return false;
            }

            if (this.TryRefreshExistingPlayerTrade(initiator, target))
            {
                return true;
            }

            this.TryClearStalePlayerTrade(initiator, target);
            this.TryClearStalePlayerTrade(target, initiator);

            if (initiator.ShoppingBag != null || target.ShoppingBag != null)
            {
                ChatTextMessageHandler.Default.Send(initiator, "Trade target is already trading.");
                return true;
            }

            Identity bagIdentity =
                new Identity
                {
                    Type = IdentityType.TempBag,
                    Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, IdentityType.TempBag)
                };

            TemporaryBag tradeBag =
                new TemporaryBag(
                    initiator.Identity,
                    bagIdentity,
                    initiator.Identity,
                    target.Identity,
                    0x40);

            initiator.ShoppingBag = tradeBag;
            target.ShoppingBag = tradeBag;

            this.SendPlayerTradeStart(initiator, target);
            this.SendPlayerTradeStart(target, initiator);

            ChatTextMessageHandler.Default.Send(initiator, "Trade started with " + target.Name + ".");
            ChatTextMessageHandler.Default.Send(target, initiator.Name + " started a trade with you.");

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade opened shopper=" + initiator.Identity.ToString(true)
                + " target=" + target.Identity.ToString(true)
                + " bag=" + bagIdentity.ToString(true));

            return true;
        }

        private IItemContainer ResolveTradeIssuer(ICharacter character, Identity target)
        {
            if (character != null && character.Identity.Equals(target))
            {
                return character;
            }

            if (character == null || character.Playfield == null)
            {
                return null;
            }

            return Pool.Instance.GetObject<IItemContainer>(character.Playfield.Identity, target);
        }

        private bool TryRefreshExistingPlayerTrade(ICharacter initiator, ICharacter target)
        {
            TemporaryBag tradeBag = initiator.ShoppingBag ?? target.ShoppingBag;
            if (tradeBag == null)
            {
                return false;
            }

            if (initiator.ShoppingBag != tradeBag
                || target.ShoppingBag != tradeBag
                || !IsTradeBetween(tradeBag, initiator, target))
            {
                return false;
            }

            this.SendPlayerTradeStart(initiator, target);
            this.SendPlayerTradeStart(target, initiator);

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade refreshed shopper=" + initiator.Identity.ToString(true)
                + " target=" + target.Identity.ToString(true)
                + " bag=" + tradeBag.Identity.ToString(true));

            return true;
        }

        private bool TryClearStalePlayerTrade(ICharacter character, ICharacter requestedPartner)
        {
            if (character == null || requestedPartner == null || character.ShoppingBag == null)
            {
                return false;
            }

            TemporaryBag tradeBag = character.ShoppingBag;
            if (!IsTradeBetween(tradeBag, character, requestedPartner)
                || requestedPartner.ShoppingBag == tradeBag)
            {
                return false;
            }

            this.ReturnAllPlayerTradeOffers(tradeBag, "stale player trade cleanup");

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade stale bag cleared character=" + character.Identity.ToString(true)
                + " requestedPartner=" + requestedPartner.Identity.ToString(true)
                + " bag=" + tradeBag.Identity.ToString(true));

            tradeBag.Dispose();
            return true;
        }

        private static bool IsTradeBetween(TemporaryBag tradeBag, ICharacter first, ICharacter second)
        {
            if (tradeBag == null || first == null || second == null)
            {
                return false;
            }

            return (tradeBag.Shopper.Equals(first.Identity) && tradeBag.Vendor.Equals(second.Identity))
                   || (tradeBag.Shopper.Equals(second.Identity) && tradeBag.Vendor.Equals(first.Identity));
        }

        private void SendPlayerTradeStart(ICharacter viewer, ICharacter partner)
        {
            this.Send(viewer, this.PlayerTradeStart(viewer, partner.Identity));
        }

        private MessageDataFiller PlayerTradeStart(
            ICharacter character,
            Identity partner)
        {
            return x =>
            {
                x.Action = TradeAction.Open;
                x.Container = Identity.None;
                x.Identity = character.Identity;
                x.Target = partner;
                x.Unknown = 0;
                x.Unknown1 = 2;
            };
        }

        private bool TryAddPlayerTradeItem(
            ICharacter character,
            IItemContainer issuer,
            TemporaryBag shoppingBag,
            TradeMessage message)
        {
            ICharacter otherCharacter;
            if (!this.IsPlayerTrade(character, shoppingBag, out otherCharacter))
            {
                return false;
            }

            if (issuer == null || !issuer.Identity.Equals(character.Identity))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade add ignored because source is not the sender inventory.");
                return true;
            }

            if (!issuer.BaseInventory.Pages.ContainsKey((int)message.Container.Type))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade add ignored because source page is missing: " + message.Container);
                return true;
            }

            IItem removedItem;
            try
            {
                removedItem = issuer.BaseInventory.RemoveItem(
                    (int)message.Container.Type,
                    message.Container.Instance);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade add ignored because source slot is empty: "
                    + message.Container.ToString(true)
                    + " reason=" + ex.Message);
                this.SendPlayerTradeInventoryInvalidation(character, otherCharacter);
                return true;
            }

            if (removedItem == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade add ignored because source slot returned no item: "
                    + message.Container.ToString(true));
                this.SendPlayerTradeInventoryInvalidation(character, otherCharacter);
                return true;
            }

            int tradeSlot = shoppingBag.AddPlayerOffer(character.Identity, removedItem);
            if (tradeSlot < 0)
            {
                issuer.BaseInventory.AddToPage(
                    (int)message.Container.Type,
                    message.Container.Instance,
                    removedItem);
                ChatTextMessageHandler.Default.Send(character, "Trade window is full.");
                return true;
            }

            this.AcknowledgeTradeAction(character, message);
            this.SendPlayerTradeItemRender(
                otherCharacter,
                removedItem,
                message.Container);
            InventoryContainerRuntimeService.Default.PersistCharacterInventory(character, "player trade add");

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade add owner=" + character.Identity.ToString(true)
                + " other=" + otherCharacter.Identity.ToString(true)
                + " source=" + message.Container.ToString(true)
                + " tradeSlot=" + tradeSlot
                + " item=" + removedItem.LowID + "/" + removedItem.HighID + ":" + removedItem.Quality);

            return true;
        }

        private bool TryRemovePlayerTradeItem(
            ICharacter character,
            IItemContainer issuer,
            TemporaryBag shoppingBag,
            TradeMessage message)
        {
            ICharacter otherCharacter;
            if (!this.IsPlayerTrade(character, shoppingBag, out otherCharacter))
            {
                return false;
            }

            if (issuer == null || !issuer.Identity.Equals(character.Identity))
            {
                return true;
            }

            int tradeSlot = message.Container.Instance;
            IItem returnedItem = shoppingBag.RemovePlayerOffer(character.Identity, tradeSlot);
            if (returnedItem == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade remove ignored because trade slot is empty: " + message.Container);
                return true;
            }

            int inventorySlot = character.BaseInventory[character.BaseInventory.StandardPage].FindFreeSlot();
            if (inventorySlot < 0)
            {
                shoppingBag.AddPlayerOffer(character.Identity, returnedItem);
                ChatTextMessageHandler.Default.Send(character, "Inventory is full.");
                return true;
            }

            InventoryError err =
                character.BaseInventory.AddToPage(character.BaseInventory.StandardPage, inventorySlot, returnedItem);
            if (err != InventoryError.OK)
            {
                shoppingBag.AddPlayerOffer(character.Identity, returnedItem);
                ChatTextMessageHandler.Default.Send(character, "Could not return trade item. (" + err + ")");
                return true;
            }

            InventoryContainerRuntimeService.Default.SendTradeWindowMoveToInventory(
                character,
                IdentityType.KnuBotTradeWindow,
                tradeSlot,
                0x6f);
            this.AcknowledgeTradeAction(character, message);
            this.SendPlayerTradeAction(
                otherCharacter,
                TradeAction.RemoveItem,
                character.Identity,
                message.Container);
            InventoryUpdatedMessageHandler.Default.Send(character, otherCharacter.Identity);
            InventoryUpdatedMessageHandler.Default.Send(character, character.Identity);
            InventoryContainerRuntimeService.Default.PersistCharacterInventory(character, "player trade remove");

            return true;
        }

        private bool TrySetPlayerTradeCredits(
            ICharacter character,
            TemporaryBag shoppingBag,
            TradeMessage message)
        {
            ICharacter otherCharacter;
            if (!this.IsPlayerTrade(character, shoppingBag, out otherCharacter))
            {
                return false;
            }

            int credits = Math.Max(0, message.Param2);
            shoppingBag.SetPlayerTradeCredits(character.Identity, credits);

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_CREDIT_SET char=" + character.Identity.ToString(true)
                + " name=" + character.Name
                + " other=" + otherCharacter.Identity.ToString(true)
                + " action=" + message.Action
                + " p1=" + message.Param1
                + " p2=" + message.Param2
                + " p3=" + message.Param3
                + " p4=" + message.Param4
                + " target=" + message.Target.ToString(true)
                + " container=" + message.Container.ToString(true)
                + " storedCredits=" + credits);

            this.SendPlayerTradeCredits(character, character.Identity, credits);
            this.SendPlayerTradeCredits(otherCharacter, character.Identity, credits);

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade credits owner=" + character.Identity.ToString(true)
                + " other=" + otherCharacter.Identity.ToString(true)
                + " credits=" + credits);

            return true;
        }

        private bool TryEndPlayerTrade(ICharacter character, TemporaryBag shoppingBag, TradeMessage message)
        {
            ICharacter otherCharacter;
            if (!this.IsPlayerTrade(character, shoppingBag, out otherCharacter))
            {
                return false;
            }

            if (message.Action == TradeAction.Confirm)
            {
                bool readyForEnd = shoppingBag.MarkPlayerTradeAccepted(character.Identity);

                if (!readyForEnd)
                {
                    this.SendPlayerTradeStatus(otherCharacter, TradeAction.Accept, otherCharacter.Identity);
                    ChatTextMessageHandler.Default.Send(character, "Trade accepted. Waiting for other player.");
                    ChatTextMessageHandler.Default.Send(otherCharacter, character.Name + " accepted the trade.");
                    return true;
                }

                ICharacter promptShopper = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
                ICharacter promptVendor = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
                if (promptShopper == null || promptVendor == null)
                {
                    shoppingBag.Dispose();
                    return true;
                }

                string failureReason = this.GetPlayerTradeCompletionFailure(promptShopper, promptVendor, shoppingBag);
                if (failureReason != null)
                {
                    shoppingBag.ClearPlayerTradeAcceptances();
                    ChatTextMessageHandler.Default.Send(promptShopper, "Trade failed: " + failureReason);
                    ChatTextMessageHandler.Default.Send(promptVendor, "Trade failed: " + failureReason);
                    return true;
                }

                this.SendPlayerTradeStatus(promptShopper, TradeAction.Confirm, promptShopper.Identity);
                this.SendPlayerTradeStatus(promptVendor, TradeAction.Confirm, promptVendor.Identity);
                return true;
            }

            shoppingBag.MarkPlayerTradeEnded(character.Identity);

            if (!shoppingBag.IsPlayerTradeReady())
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade End stored until both players confirm character="
                    + character.Identity.ToString(true)
                    + " partner=" + otherCharacter.Identity.ToString(true));
                return true;
            }

            if (!shoppingBag.IsPlayerTradeEnded())
            {
                this.SendPlayerTradeEndPrompt(otherCharacter, character);
                return true;
            }

            return this.CompletePlayerTrade(shoppingBag);
        }

        private bool CompletePlayerTrade(TemporaryBag shoppingBag)
        {
            ICharacter shopper = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
            ICharacter vendor = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
            if (shopper == null || vendor == null)
            {
                shoppingBag.Dispose();
                return true;
            }

            string failureReason = this.GetPlayerTradeCompletionFailure(shopper, vendor, shoppingBag);
            if (failureReason != null)
            {
                shoppingBag.ClearPlayerTradeAcceptances();
                ChatTextMessageHandler.Default.Send(shopper, "Trade failed: " + failureReason);
                ChatTextMessageHandler.Default.Send(vendor, "Trade failed: " + failureReason);
                return true;
            }

            if (!shoppingBag.TryBeginPlayerTradeCompletion())
            {
                return true;
            }

            if (!this.TransferPlayerTradeCredits(shopper, vendor, shoppingBag))
            {
                ChatTextMessageHandler.Default.Send(shopper, "Trade failed: credit transfer could not be completed.");
                ChatTextMessageHandler.Default.Send(vendor, "Trade failed: credit transfer could not be completed.");
                this.ReturnAllPlayerTradeOffers(shoppingBag, "player trade credit failure");
                shoppingBag.Dispose();
                return true;
            }

            this.TransferPlayerTradeOffers(shopper, vendor, shoppingBag);
            this.TransferPlayerTradeOffers(vendor, shopper, shoppingBag);

            this.SendPlayerTradeCompleteClose(shopper, vendor);
            this.SendPlayerTradeCompleteClose(vendor, shopper);
            this.SendPlayerTradeSocialStatus(shopper);
            this.SendPlayerTradeSocialStatus(vendor);
            InventoryContainerRuntimeService.Default.PersistCharacterInventory(shopper, "player trade complete");
            InventoryContainerRuntimeService.Default.PersistCharacterInventory(vendor, "player trade complete");

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade completed shopper=" + shopper.Identity.ToString(true)
                + " vendor=" + vendor.Identity.ToString(true)
                + " bag=" + shoppingBag.Identity.ToString(true));

            shoppingBag.Dispose();
            return true;
        }

        private bool TryDeclinePlayerTrade(ICharacter character, TemporaryBag shoppingBag)
        {
            ICharacter otherCharacter;
            if (!this.IsPlayerTrade(character, shoppingBag, out otherCharacter))
            {
                return false;
            }

            ICharacter shopper = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
            ICharacter vendor = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);
            if (shopper != null)
            {
                this.ReturnPlayerTradeOffers(shopper, shoppingBag);
                InventoryContainerRuntimeService.Default.PersistCharacterInventory(shopper, "player trade decline");
            }

            if (vendor != null)
            {
                this.ReturnPlayerTradeOffers(vendor, shoppingBag);
                InventoryContainerRuntimeService.Default.PersistCharacterInventory(vendor, "player trade decline");
            }

            this.SendPlayerTradeDeclineClose(character, otherCharacter);
            shoppingBag.Dispose();
            return true;
        }

        private bool IsPlayerTrade(ICharacter character, TemporaryBag shoppingBag, out ICharacter otherCharacter)
        {
            otherCharacter = this.GetOtherPlayerTradeCharacter(character, shoppingBag);
            return otherCharacter != null
                   && character.Controller is PlayerController
                   && otherCharacter.Controller is PlayerController;
        }

        private void SendPlayerTradeStatus(
            ICharacter viewer,
            TradeAction action,
            Identity subject)
        {
            if (viewer == null)
            {
                return;
            }

            this.Send(viewer, this.PlayerTradeClose(subject, action, subject, subject));
        }

        private void SendPlayerTradeEndPrompt(ICharacter viewer, ICharacter partner)
        {
            if (viewer == null || partner == null)
            {
                return;
            }

            this.Send(viewer, this.PlayerTradeClose(viewer.Identity, TradeAction.Accept, partner.Identity, partner.Identity));
        }

        private void SendPlayerTradeAction(
            ICharacter viewer,
            TradeAction action,
            Identity offerOwner,
            Identity source)
        {
            if (viewer == null || viewer.Controller.Client == null)
            {
                return;
            }

            // Live player trade offer panes are driven by Trade frames, not InventoryUpdate.
            this.Send(viewer, this.PlayerTradeAction(viewer, action, offerOwner, source));
        }

        private void SendPlayerTradeCredits(ICharacter viewer, Identity offerOwner, int credits)
        {
            if (viewer == null || viewer.Controller.Client == null)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_CREDIT_SEND viewer=" + viewer.Identity.ToString(true)
                + " name=" + viewer.Name
                + " offerOwner=" + offerOwner.ToString(true)
                + " action=" + TradeAction.UpdateCredits
                + " p1=0"
                + " p2=" + credits
                + " p3=0"
                + " p4=0"
                + " storedCredits=" + credits);

            this.Send(viewer, this.PlayerTradeCredits(offerOwner, credits));
        }

        private void SendPlayerTradeItemRender(
            ICharacter viewer,
            IItem item,
            Identity source)
        {
            Item concreteItem = item as Item;
            if (viewer == null || concreteItem == null)
            {
                return;
            }

            viewer.Send(
                new TemplateActionMessage
                {
                    Identity = viewer.Identity,
                    ItemHighId = concreteItem.HighID,
                    ItemLowId = concreteItem.LowID,
                    Quality = concreteItem.Quality,
                    Unknown = 0,
                    Unknown1 = 1,
                    Unknown2 = 0x55,
                    Placement = source,
                    Unknown3 = 0,
                    Unknown4 = 0
                });
        }

        private void SendPlayerTradeCompleteClose(ICharacter viewer, ICharacter partner)
        {
            if (viewer == null || partner == null)
            {
                return;
            }

            // Live complete sends a pair of action-4 frames that clear both player panes.
            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_COMPLETE_SEND viewer=" + viewer.Identity.ToString(true)
                + " name=" + viewer.Name
                + " partner=" + partner.Identity.ToString(true)
                + " frameIdentity=" + viewer.Identity.ToString(true)
                + " action=" + TradeAction.Complete
                + " target=" + partner.Identity.ToString(true)
                + " container=" + partner.Identity.ToString(true));
            this.Send(viewer, this.PlayerTradeClose(viewer.Identity, TradeAction.Complete, partner.Identity, partner.Identity));

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_COMPLETE_SEND viewer=" + viewer.Identity.ToString(true)
                + " name=" + viewer.Name
                + " partner=" + partner.Identity.ToString(true)
                + " frameIdentity=" + partner.Identity.ToString(true)
                + " action=" + TradeAction.Complete
                + " target=" + viewer.Identity.ToString(true)
                + " container=" + viewer.Identity.ToString(true));
            this.Send(viewer, this.PlayerTradeClose(partner.Identity, TradeAction.Complete, viewer.Identity, viewer.Identity));
        }

        private void SendPlayerTradeDeclineClose(ICharacter first, ICharacter second)
        {
            this.Send(first, TradeAction.Decline, Identity.None, Identity.None);
            this.Send(second, TradeAction.Decline, Identity.None, Identity.None);
            this.SendPlayerTradeInventoryInvalidation(first, second);
            this.SendPlayerTradeInventoryInvalidation(second, first);
            this.SendPlayerTradeSocialStatus(first);
            this.SendPlayerTradeSocialStatus(second);
        }

        private void SendPlayerTradeInventoryInvalidation(ICharacter viewer, ICharacter partner)
        {
            if (viewer == null)
            {
                return;
            }

            InventoryUpdatedMessageHandler.Default.Send(viewer, viewer.Identity);
            if (partner != null)
            {
                InventoryUpdatedMessageHandler.Default.Send(viewer, partner.Identity);
            }
        }

        private void SendPlayerTradeSocialStatus(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, 0);
        }

        private MessageDataFiller PlayerTradeClose(
            Identity messageIdentity,
            TradeAction tradeAction,
            Identity target,
            Identity container)
        {
            return x =>
            {
                x.Action = tradeAction;
                x.Container = container;
                x.Target = target;
                x.Identity = messageIdentity;
                x.Unknown = 0;
                x.Unknown1 = 2;
            };
        }

        private MessageDataFiller PlayerTradeAction(
            ICharacter viewer,
            TradeAction action,
            Identity offerOwner,
            Identity source)
        {
            return x =>
            {
                x.Identity = offerOwner;
                x.Unknown = 0;
                x.Unknown1 = 2;
                x.Action = action;
                x.Target = offerOwner;
                x.Container = source;
            };
        }

        private MessageDataFiller PlayerTradeCredits(Identity offerOwner, int credits)
        {
            return x =>
            {
                x.Identity = offerOwner;
                x.Unknown = 0;
                x.Unknown1 = 2;
                x.Action = TradeAction.UpdateCredits;
                x.Param1 = 0;
                x.Param2 = credits;
                x.Param3 = 0;
                x.Param4 = 0;
            };
        }

        private void SendPlayerTradeItemDefinition(
            ICharacter viewer,
            TemporaryBag shoppingBag,
            Identity offerOwner,
            int tradeSlot)
        {
            IInventoryPage offerPage = shoppingBag.GetPlayerOfferPage(offerOwner);
            if (offerPage == null)
            {
                return;
            }

            Item item = offerPage[tradeSlot] as Item;
            if (item != null)
            {
                AddTemplateMessageHandler.Default.Send(viewer, item);
            }
        }

        private void SendPlayerTradeInventoryUpdate(
            ICharacter viewer,
            TemporaryBag shoppingBag,
            Identity offerOwner,
            IdentityType displayContainerType)
        {
            IInventoryPage offerPage = shoppingBag.GetPlayerOfferPage(offerOwner);
            if (offerPage == null || viewer.Controller.Client == null)
            {
                return;
            }

            InventoryEntry[] entries =
                offerPage.List().Select(CreatePlayerTradeInventoryEntry).ToArray();

            viewer.Controller.Client.SendCompressed(
                new InventoryUpdateMessage
                {
                    Identity = viewer.Identity,
                    Unknown = 1,
                    NumberOfSlots = offerPage.MaxSlots,
                    Unknown1 = 2,
                    Entries = entries,
                    BagIdentity =
                        new Identity
                        {
                            Type = displayContainerType,
                            Instance = shoppingBag.Identity.Instance
                        },
                    SlotnumberInMainInventory = 0,
                    Unknown2 = 1
                });

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade inventory update viewer=" + viewer.Identity.ToString(true)
                + " owner=" + offerOwner.ToString(true)
                + " bag=" + displayContainerType + ":" + shoppingBag.Identity.Instance
                + " entries=" + entries.Length);
        }

        private static InventoryEntry CreatePlayerTradeInventoryEntry(KeyValuePair<int, IItem> item)
        {
            return new InventoryEntry
            {
                Slotnumber = item.Key,
                UnknownFlags = 0x00A1,
                Unknown1 = (short)item.Value.MultipleCount,
                Identity = Identity.None,
                LowId = item.Value.LowID,
                HighId = item.Value.HighID,
                Quality = item.Value.Quality,
                Unknown2 = 0
            };
        }

        private string GetPlayerTradeCompletionFailure(ICharacter shopper, ICharacter vendor, TemporaryBag shoppingBag)
        {
            int shopperItems = shoppingBag.GetPlayerOffers(shopper.Identity).Length;
            int vendorItems = shoppingBag.GetPlayerOffers(vendor.Identity).Length;

            if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(vendor, shopperItems))
            {
                return vendor.Name + " does not have enough free inventory slots.";
            }

            if (!InventoryContainerRuntimeService.Default.HasFreeInventorySlots(shopper, vendorItems))
            {
                return shopper.Name + " does not have enough free inventory slots.";
            }

            string shopperCreditFailure = this.GetPlayerTradeCreditFailure(shopper, shoppingBag);
            if (shopperCreditFailure != null)
            {
                return shopperCreditFailure;
            }

            return this.GetPlayerTradeCreditFailure(vendor, shoppingBag);
        }

        private string GetPlayerTradeCreditFailure(ICharacter character, TemporaryBag shoppingBag)
        {
            int offeredCredits = shoppingBag.GetPlayerTradeCredits(character.Identity);
            int availableCredits = GetCash(character);
            if (offeredCredits <= 0 || availableCredits >= offeredCredits)
            {
                return null;
            }

            return character.Name
                   + " offered "
                   + offeredCredits
                   + " credits but only has "
                   + availableCredits
                   + ".";
        }

        private void TransferPlayerTradeOffers(ICharacter from, ICharacter to, TemporaryBag shoppingBag)
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

        private bool TransferPlayerTradeCredits(ICharacter shopper, ICharacter vendor, TemporaryBag shoppingBag)
        {
            int shopperCredits = shoppingBag.GetPlayerTradeCredits(shopper.Identity);
            int vendorCredits = shoppingBag.GetPlayerTradeCredits(vendor.Identity);
            if (shopperCredits <= 0 && vendorCredits <= 0)
            {
                return true;
            }

            int shopperCash = GetCash(shopper);
            int vendorCash = GetCash(vendor);
            long startingTotalCash = (long)shopperCash + vendorCash;

            if (shopperCredits > shopperCash || vendorCredits > vendorCash)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade credits rejected during commit shopper=" + shopper.Identity.ToString(true)
                    + " vendor=" + vendor.Identity.ToString(true)
                    + " shopperCredits=" + shopperCredits
                    + " vendorCredits=" + vendorCredits
                    + " shopperCash=" + shopperCash
                    + " vendorCash=" + vendorCash);
                return false;
            }

            long shopperFinalCashRaw = (long)shopperCash - shopperCredits + vendorCredits;
            long vendorFinalCashRaw = (long)vendorCash - vendorCredits + shopperCredits;
            if (shopperFinalCashRaw > CashStatRules.ClientSafeMaxCash
                || vendorFinalCashRaw > CashStatRules.ClientSafeMaxCash
                || shopperFinalCashRaw < 0
                || vendorFinalCashRaw < 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade credits rejected because cash cap would be exceeded shopper="
                    + shopper.Identity.ToString(true)
                    + " vendor=" + vendor.Identity.ToString(true)
                    + " shopperFinalRaw=" + shopperFinalCashRaw
                    + " vendorFinalRaw=" + vendorFinalCashRaw
                    + " cap=" + CashStatRules.ClientSafeMaxCash);
                return false;
            }

            int shopperFinalCash = CashStatRules.Clamp(shopperFinalCashRaw);
            int vendorFinalCash = CashStatRules.Clamp(vendorFinalCashRaw);
            long finalTotalCash = (long)shopperFinalCash + vendorFinalCash;
            if (finalTotalCash != startingTotalCash)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Shopping,
                    "Player trade credits rejected because totals differ shopper=" + shopper.Identity.ToString(true)
                    + " vendor=" + vendor.Identity.ToString(true)
                    + " startingTotal=" + startingTotalCash
                    + " finalTotal=" + finalTotalCash);
                return false;
            }

            SetCash(shopper, shopperFinalCash);
            SetCash(vendor, vendorFinalCash);

            shopper.Stats.Write();
            vendor.Stats.Write();

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Player trade credits committed shopper=" + shopper.Identity.ToString(true)
                + " vendor=" + vendor.Identity.ToString(true)
                + " shopperCredits=" + shopperCredits
                + " vendorCredits=" + vendorCredits
                + " shopperCashBefore=" + shopperCash
                + " vendorCashBefore=" + vendorCash
                + " shopperCashAfter=" + shopperFinalCash
                + " vendorCashAfter=" + vendorFinalCash);

            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "TRADE_CREDIT_COMMIT shopper=" + shopper.Identity.ToString(true)
                + " shopperName=" + shopper.Name
                + " vendor=" + vendor.Identity.ToString(true)
                + " vendorName=" + vendor.Name
                + " shopperCredits=" + shopperCredits
                + " vendorCredits=" + vendorCredits
                + " shopperCashBefore=" + shopperCash
                + " vendorCashBefore=" + vendorCash
                + " shopperCashAfter=" + shopperFinalCash
                + " vendorCashAfter=" + vendorFinalCash);
            return true;
        }

        private int CalculateVendorBuyTotal(ICharacter shopper, Vendor vendor, IEnumerable<IItem> items)
        {
            int total = 0;
            foreach (IItem item in items)
            {
                int price = this.CalculateVendorBuyPrice(shopper, vendor, item);
                total = CashStatRules.Clamp((long)total + price);
            }

            return total;
        }

        private int CalculateVendorSellTotal(ICharacter shopper, Vendor vendor, IEnumerable<IItem> items)
        {
            int total = 0;
            foreach (IItem item in items)
            {
                int price = this.CalculateVendorSellPrice(shopper, vendor, item);
                total = CashStatRules.Clamp((long)total + price);
            }

            return total;
        }

        private int CalculateVendorBuyPrice(ICharacter shopper, Vendor vendor, IItem item)
        {
            int value = this.CalculateVendorItemValue(item);
            int skillSteps = this.GetVendorPricingSkillSteps(shopper, vendor);
            int sellModifier = (int)vendor.Stats[StatIds.sellmodifier].Value;
            int discountSteps = Math.Max(0, 100 - skillSteps);
            int price = Math.Max(
                0,
                (int)Math.Round(value * sellModifier * discountSteps / 10000.0d));

            this.LogVendorPrice(
                "buy-from-vendor",
                shopper,
                vendor,
                item,
                value,
                sellModifier,
                skillSteps,
                price);
            return price;
        }

        private int CalculateVendorSellPrice(ICharacter shopper, Vendor vendor, IItem item)
        {
            int value = this.CalculateVendorItemValue(item);
            int skillSteps = this.GetVendorPricingSkillSteps(shopper, vendor);
            int buyModifier = (int)vendor.Stats[StatIds.buymodifier].Value;

            // Current-client tooling uses: value * BuyModifier * (100 + CL / 40) / 10000.
            int price = Math.Max(
                0,
                (int)Math.Floor(value * buyModifier * (100 + skillSteps) / 10000.0d));

            this.LogVendorPrice(
                "sell-to-vendor",
                shopper,
                vendor,
                item,
                value,
                buyModifier,
                skillSteps,
                price);
            return price;
        }

        private int GetVendorPricingSkillSteps(ICharacter shopper, Vendor vendor)
        {
            return Math.Max(0, shopper.Stats[StatIds.computerliteracy].Value / 40);
        }

        private int CalculateVendorItemValue(IItem item)
        {
            ItemTemplate lowTemplate;
            ItemTemplate highTemplate;
            if (!ItemLoader.ItemList.TryGetValue(item.LowID, out lowTemplate)
                || !ItemLoader.ItemList.TryGetValue(item.HighID, out highTemplate))
            {
                return Math.Max(0, item.GetAttribute((int)StatIds.price));
            }

            int lowQuality = lowTemplate.Quality;
            int highQuality = highTemplate.Quality;
            int lowValue = Math.Max(0, lowTemplate.getItemAttribute((int)StatIds.price));
            int highValue = Math.Max(0, highTemplate.getItemAttribute((int)StatIds.price));

            if (lowQuality == highQuality)
            {
                return lowValue;
            }

            if (highValue == 0)
            {
                return highValue;
            }

            double qualityDelta = item.Quality - lowQuality;
            double qualityRange = highQuality - lowQuality;
            double scaledValue =
                lowValue
                + Math.Pow(qualityDelta, 2.0d)
                * (highValue - lowValue)
                / Math.Pow(qualityRange, 2.0d);

            return Math.Max(0, (int)Math.Round(scaledValue));
        }

        private void LogVendorPrice(
            string direction,
            ICharacter shopper,
            Vendor vendor,
            IItem item,
            int value,
            int modifier,
            int skillSteps,
            int price)
        {
            LogUtil.Debug(
                DebugInfoDetail.Shopping,
                "Vendor price " + direction
                + " shopper=" + shopper.Identity.ToString(true)
                + " vendor=" + vendor.Identity.ToString(true)
                + " item=" + item.LowID + "/" + item.HighID + ":" + item.Quality
                + " value=" + value
                + " modifier=" + modifier
                + " skillSteps=" + skillSteps
                + " price=" + price);
        }

        private static int GetCash(ICharacter character)
        {
            uint value = character.Stats[StatIds.cash].BaseValue;
            return CashStatRules.Clamp(value);
        }

        private static void SetCash(ICharacter character, int cash)
        {
            character.Stats[StatIds.cash].Set((uint)CashStatRules.Clamp(cash));
        }

        private void ReturnPlayerTradeOffers(ICharacter owner, TemporaryBag shoppingBag)
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
                InventoryContainerRuntimeService.Default.SendTradeWindowMoveToInventory(
                    owner,
                    IdentityType.KnuBotTradeWindow,
                    offer.Key,
                    targetSlot);
            }
        }

        private void ReturnAllPlayerTradeOffers(TemporaryBag shoppingBag, string reason)
        {
            ICharacter shopper = Pool.Instance.GetObject<ICharacter>(shoppingBag.Shopper);
            ICharacter vendor = Pool.Instance.GetObject<ICharacter>(shoppingBag.Vendor);

            if (shopper != null)
            {
                this.ReturnPlayerTradeOffers(shopper, shoppingBag);
                InventoryContainerRuntimeService.Default.PersistCharacterInventory(shopper, reason);
            }

            if (vendor != null)
            {
                this.ReturnPlayerTradeOffers(vendor, shoppingBag);
                InventoryContainerRuntimeService.Default.PersistCharacterInventory(vendor, reason);
            }
        }

        private ICharacter GetOtherPlayerTradeCharacter(ICharacter character, TemporaryBag shoppingBag)
        {
            if (character == null || shoppingBag == null)
            {
                return null;
            }

            Identity otherIdentity =
                character.Identity.Equals(shoppingBag.Shopper)
                    ? shoppingBag.Vendor
                    : shoppingBag.Shopper;

            ICharacter otherCharacter = Pool.Instance.GetObject<ICharacter>(otherIdentity);
            if (otherCharacter != null && otherCharacter.ShoppingBag == shoppingBag)
            {
                return otherCharacter;
            }

            return null;
        }

        private MessageDataFiller AcknowledgeRemove(Identity identity, TradeMessage message)
        {
            return x =>
            {
                x.Identity = identity;
                x.Unknown = 0;
                x.Unknown1 = message.Unknown1;
                x.Action = message.Action;
                x.Target = message.Target;
                x.Container = message.Container;
            };
        }

        private void Send(ICharacter character, TradeAction tradeAction, Identity identity1, Identity identity2)
        {
            this.Send(character, this.EndTrade(character, tradeAction, identity1, identity2));
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

        private void SendVendorShopDeclineClose(ICharacter character)
        {
            this.Send(character, TradeAction.Decline, Identity.None, Identity.None);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, 4);
            AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);
        }

        private MessageDataFiller EndTrade(
            ICharacter character,
            TradeAction tradeAction,
            Identity identity1,
            Identity identity2)
        {
            return x =>
            {
                x.Action = tradeAction;
                x.Container = identity2;
                x.Target = identity1;
                x.Identity = character.Identity;
                x.Unknown = 0;
                x.Unknown1 = 2;
            };
        }

        private void AcknowledgeTradeAction(ICharacter character, TradeMessage message)
        {
            this.Send(character, this.AcknowledgeFiller(message));
        }

        private MessageDataFiller AcknowledgeFiller(TradeMessage message)
        {
            return x =>
            {
                x.Target = message.Target;
                x.Action = message.Action;
                x.Container = message.Container;
                x.Unknown1 = message.Unknown1;
                x.Unknown = message.Unknown;
                x.Identity = message.Identity;
            };
        }

        public void Send(ICharacter character, TemporaryBag tempBag)
        {
            this.Send(character, this.TemporaryBagHandle(character, tempBag.Shopper, tempBag.Vendor, tempBag.Identity));
            this.Send(character, this.TemporaryBagHandle(character, tempBag.Vendor, tempBag.Shopper, tempBag.Identity));
        }

        private MessageDataFiller TemporaryBagHandle(
            ICharacter character,
            Identity identity1,
            Identity identity2,
            Identity bagIdentity)
        {
            return x =>
            {
                x.Identity = identity1;
                x.Unknown = 0;
                x.Unknown1 = 2;
                x.Action = TradeAction.Open;
                x.Target = identity2;
                x.Container = bagIdentity;
            };
        }

        public void Send(ICharacter character, Identity targetIdentity, Identity containerIdentity)
        {
            this.Send(character, this.ShopTrade(character, targetIdentity, containerIdentity));
        }

        private MessageDataFiller ShopTrade(ICharacter character, Identity targetIdentity, Identity containerIdentity)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Container = containerIdentity;
                x.Target = targetIdentity;
                x.Unknown1 = 2;
                x.Action = TradeAction.Open;
            };
        }
    }
}
