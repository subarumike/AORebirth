namespace ZoneEngine_New.Core.Trade
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Owns every live trade window. One session per player at a time; a vending machine may back
    /// many sessions at once. All mutation runs on the owning playfield's tick thread — the registry
    /// lock only protects lookups made from another playfield (transfer, shutdown).
    /// </summary>
    public sealed class TradeService
    {
        /// <summary>Walking further than this from the trade anchor cancels the window.</summary>
        public const double RangeCancelDistance = LootableDynel.OpenRange;

        readonly object _gate = new();
        readonly Dictionary<int, TradeSession> _byPlayer = new();
        readonly IZoneLogger _logger;
        readonly IGameData _gameData;
        readonly IItemTemplateCatalog _catalog;
        readonly HashItemMinter _minter;
        readonly IItemInstanceIdAllocator _ids;
        readonly InventoryFlushService _flush;

        public TradeService(
            IZoneLogger logger,
            IGameData gameData,
            IItemTemplateCatalog catalog,
            HashItemMinter minter,
            IItemInstanceIdAllocator ids,
            InventoryFlushService flush)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(ids);
            ArgumentNullException.ThrowIfNull(flush);

            _logger = logger;
            _gameData = gameData;
            _catalog = catalog;
            _minter = minter;
            _ids = ids;
            _flush = flush;
        }

        public bool TryGetSession(Player player, out TradeSession session)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (_gate)
                return _byPlayer.TryGetValue(player.Identity.Instance, out session!);
        }

        #region Opening

        /// <summary>Entry point for <see cref="VendingMachine.OnUse"/> and NPC vendor use.</summary>
        public bool TryOpenShop(Player player, VendingMachine machine)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(machine);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return false;

            if (player.IsDead || machine.Playfield == null)
                return false;

            NpcCharacter? owner = machine.OwnerNpc;
            if (owner != null && (owner.IsDead || owner.Playfield == null))
                return false;

            // Same range the tick enforces: a crafted Use from across the zone would otherwise open
            // a window that is cancelled again on the next tick.
            Dynel anchor = owner != null ? owner : machine;
            if (player.Distance3D(anchor) > RangeCancelDistance)
            {
                Tell(player, "You are too far away.");
                return false;
            }

            if (HasSession(player))
                Cancel(player, "opening another trade");

            int templateId = machine.Template.Id;
            if (!_gameData.TryGetVendingMachine(templateId, out VendingMachineDefinition definition))
            {
                Tell(player, "This shop has no stock list yet.");
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Vending machine {0} has no VendingMachines.json entry",
                        templateId));
                return false;
            }

            // Random.Shared, not a field: playfields tick on their own threads and a shared
            // Random instance torn across threads silently degrades to returning zeroes.
            machine.Stock.EnsureFresh(definition, _minter, Random.Shared);

            Identity bag = player.Playfield.GetRequiredService<DynelRegistry>().AllocateTempBagIdentity();
            var session = new TradeSession(bag, TradeKind.Shop, player, partner: null, machine);
            Register(player, session);
            machine.Stock.OpenTrade();

            Identity shopIdentity = machine.ShopIdentity;
            player.Session.Send(machine.Stock.BuildShopUpdate(shopIdentity));
            player.Session.Send(OpenFrame(player.Identity, shopIdentity, bag));
            player.Session.Send(OpenFrame(shopIdentity, player.Identity, bag));

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Shop opened player={0} machine={1} template={2} stock={3}",
                    player.Identity.Instance,
                    shopIdentity.Instance,
                    templateId,
                    machine.Stock.Slots.Count));

            return true;
        }

        bool TryOpenPlayerTrade(Player initiator, Identity targetIdentity)
        {
            Playfield? playfield = initiator.Playfield;
            if (playfield == null || initiator.Session == null)
                return false;

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(targetIdentity, out Dynel? dynel)
                || dynel is not Player partner
                || ReferenceEquals(partner, initiator))
                return false;

            if (partner.Session == null || partner.IsDead || initiator.IsDead)
                return false;

            if (!partner.Inventory.IsHydrated || !initiator.Inventory.IsHydrated)
                return false;

            if (initiator.Distance3D(partner) > RangeCancelDistance)
            {
                Tell(initiator, "You are too far away to trade.");
                return false;
            }

            // A repeated Open against the same partner just re-sends the window (client reconnect,
            // double click). Any other existing session on either side is cancelled first.
            if (TryGetSession(initiator, out TradeSession existing)
                && existing.Kind == TradeKind.Player
                && existing.Involves(partner))
            {
                SendPlayerOpen(initiator, partner);
                SendPlayerOpen(partner, initiator);
                return true;
            }

            if (HasSession(partner))
            {
                Tell(initiator, partner.Name + " is already trading.");
                return false;
            }

            if (HasSession(initiator))
                Cancel(initiator, "opening another trade");

            Identity bag = playfield.GetRequiredService<DynelRegistry>().AllocateTempBagIdentity();
            var session = new TradeSession(bag, TradeKind.Player, initiator, partner, machine: null);
            Register(initiator, session);
            Register(partner, session);

            SendPlayerOpen(initiator, partner);
            SendPlayerOpen(partner, initiator);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player trade opened {0} <-> {1}",
                    initiator.Identity.Instance,
                    partner.Identity.Instance));

            return true;
        }

        #endregion

        #region Message dispatch

        public void Handle(Player player, TradeMessage message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(message);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return;

            if (message.Action == TradeAction.Open)
            {
                TryOpenPlayerTrade(player, message.Target);
                return;
            }

            if (!TryGetSession(player, out TradeSession session))
            {
                Tell(player, "No active trade session.");
                return;
            }

            switch (message.Action)
            {
                case TradeAction.AddItem:
                    HandleAddItem(player, session, message);
                    break;
                case TradeAction.RemoveItem:
                    HandleRemoveItem(player, session, message);
                    break;
                case TradeAction.UpdateCredits:
                    HandleUpdateCredits(player, session, message);
                    break;
                case TradeAction.Confirm:
                case TradeAction.Accept:
                    HandleAcceptOrConfirm(player, session, message.Action);
                    break;
                case TradeAction.Decline:
                    Decline(player, session);
                    break;
                default:
                    break;
            }
        }

        void HandleAddItem(Player player, TradeSession session, TradeMessage message)
        {
            if (session.Committing)
                return;

            // Any change to the offers invalidates the other side's agreement.
            session.ClearAcceptances();

            if (session.Kind == TradeKind.Shop && IsShopSide(session, message.Target))
            {
                AddShopPick(player, session, message);
                return;
            }

            AddOfferedItem(player, session, message);
        }

        void AddShopPick(Player player, TradeSession session, TradeMessage message)
        {
            VendingMachine machine = session.Machine!;
            int stockIndex = message.Container.Instance;
            if (!machine.Stock.TryGetSlot(stockIndex, out _))
                return;

            if (session.AddShopPick(stockIndex) < 0)
            {
                Tell(player, "Trade window is full.");
                return;
            }

            Acknowledge(player, message);
        }

        void AddOfferedItem(Player player, TradeSession session, TradeMessage message)
        {
            // The client may only put items from its own pages on the table.
            if (message.Target.Instance != player.Identity.Instance)
                return;

            // Only loose carried items can be put on the table. Worn gear has to be unequipped first
            // (that path has a timed move), and the bank is not reachable from a trade window.
            Identity source = message.Container;
            Container page = source.Type switch
            {
                IdentityType.Inventory => player.Inventory.Inventory,
                IdentityType.OverflowWindow => player.Inventory.Overflow,
                _ => null!
            };
            if (page == null)
                return;

            if (!page.Content.TryGetValue(source.Instance, out Item? item) || item.Locked)
                return;

            if (InventoryMoveService.IsBagItem(item))
            {
                // Bag interiors would need handle invalidation and recursive ownership transfer.
                Tell(player, "Containers cannot be traded.");
                return;
            }

            if (TradeRules.IsNoDrop(item))
            {
                Tell(player, item.Name + " cannot be traded.");
                return;
            }

            if (page.Remove(source.Instance) == null)
                return;

            TradeOffer offer = session.OfferFor(player);
            int tradeSlot = offer.Add(item);
            if (tradeSlot < 0)
            {
                page.Add(source.Instance, item);
                Tell(player, "Trade window is full.");
                return;
            }

            Acknowledge(player, message);

            Player? other = session.Other(player);
            if (other?.Session != null)
                SendItemRender(other, item, source);
        }

        void HandleRemoveItem(Player player, TradeSession session, TradeMessage message)
        {
            if (session.Committing)
                return;

            session.ClearAcceptances();

            if (session.Kind == TradeKind.Shop && IsShopSide(session, message.Target))
            {
                if (!session.RemoveShopPick(message.Container.Instance))
                    return;

                player.Session!.Send(RemoveAck(player.Identity, message));
                player.Session.Send(RemoveAck(session.Machine!.ShopIdentity, message));
                Acknowledge(player, message);
                return;
            }

            if (message.Target.Instance != player.Identity.Instance)
                return;

            TradeOffer offer = session.OfferFor(player);
            int tradeSlot = message.Container.Instance;
            Item? item = offer.Remove(tradeSlot);
            if (item == null)
                return;

            if (!player.Inventory.TryPlace(item, out Container page, out int slot))
            {
                offer.TryRestore(tradeSlot, item);
                Tell(player, "Inventory is full.");
                return;
            }

            player.Inventory.MarkDirty(item, page, slot);
            _flush.NotifyDirty(player);

            SendReturnToInventory(player, tradeSlot);
            Acknowledge(player, message);

            Player? other = session.Other(player);
            if (other?.Session != null)
                other.Session.Send(
                    ActionFrame(player.Identity, TradeAction.RemoveItem, player.Identity, message.Container));
        }

        void HandleUpdateCredits(Player player, TradeSession session, TradeMessage message)
        {
            if (session.Committing || session.Kind != TradeKind.Player)
                return;

            session.ClearAcceptances();

            int credits = Math.Max(0, message.Param2);
            TradeOffer offer = session.OfferFor(player);
            offer.Credits = credits;

            player.Session!.Send(CreditsFrame(player.Identity, credits));
            Player? other = session.Other(player);
            other?.Session?.Send(CreditsFrame(player.Identity, credits));
        }

        void HandleAcceptOrConfirm(Player player, TradeSession session, TradeAction action)
        {
            if (session.Committing)
                return;

            if (session.Kind == TradeKind.Shop)
            {
                CommitShop(player, session);
                return;
            }

            Player? other = session.Other(player);
            if (other == null)
            {
                Cancel(player, "trade partner is gone");
                return;
            }

            TradeOffer offer = session.OfferFor(player);

            if (action == TradeAction.Confirm)
            {
                offer.Accepted = true;
                if (!session.BothAccepted)
                {
                    other.Session?.Send(StatusFrame(other.Identity, TradeAction.Accept));
                    Tell(player, "Trade accepted. Waiting for other player.");
                    Tell(other, player.Name + " accepted the trade.");
                    return;
                }

                string? failure = ValidatePlayerTrade(session);
                if (failure != null)
                {
                    session.ClearAcceptances();
                    Tell(session.Initiator, "Trade failed: " + failure);
                    Tell(session.Partner!, "Trade failed: " + failure);
                    return;
                }

                session.Initiator.Session?.Send(StatusFrame(session.Initiator.Identity, TradeAction.Confirm));
                session.Partner!.Session?.Send(StatusFrame(session.Partner.Identity, TradeAction.Confirm));
                return;
            }

            if (!session.BothAccepted)
                return;

            offer.Ended = true;
            if (!session.BothEnded)
            {
                other.Session?.Send(EndPromptFrame(other.Identity, player.Identity));
                return;
            }

            CommitPlayerTrade(session);
        }

        #endregion

        #region Commit

        string? ValidatePlayerTrade(TradeSession session)
        {
            Player initiator = session.Initiator;
            Player partner = session.Partner!;

            string? failure = ValidateSide(initiator, session.InitiatorOffer, partner, session.PartnerOffer);
            if (failure != null)
                return failure;

            return ValidateSide(partner, session.PartnerOffer, initiator, session.InitiatorOffer);
        }

        /// <summary>
        /// Checks that <paramref name="giver"/>'s offer can legally land on <paramref name="receiver"/>.
        /// Trade items are durable rows, so they must fit in real inventory: overflow is not allowed
        /// to hold persisted items.
        /// </summary>
        static string? ValidateSide(Player giver, TradeOffer given, Player receiver, TradeOffer received)
        {
            if (!receiver.Inventory.HasFreeInventorySlots(given.Count))
                return receiver.Name + " does not have enough free inventory slots.";

            foreach (Item item in given.Items.Values)
            {
                if (TradeRules.IsNoDrop(item))
                    return item.Name + " cannot be traded.";

                if (TradeRules.IsUnique(item) && TradeRules.WouldDuplicateUnique(receiver, item.LowId, item.HighId))
                    return receiver.Name + " already has " + item.Name + ".";
            }

            if (given.Credits > 0 && giver.Stats.GetOrZero(CharacterStat.Cash) < given.Credits)
            {
                return giver.Name
                    + " offered "
                    + given.Credits.ToString(CultureInfo.InvariantCulture)
                    + " credits but only has "
                    + giver.Stats.GetOrZero(CharacterStat.Cash).ToString(CultureInfo.InvariantCulture)
                    + ".";
            }

            long receiverCash = (long)receiver.Stats.GetOrZero(CharacterStat.Cash) + given.Credits - received.Credits;
            if (receiverCash < 0 || receiverCash > TradeRules.MaxCash)
                return receiver.Name + " cannot hold that many credits.";

            return null;
        }

        void CommitPlayerTrade(TradeSession session)
        {
            Player initiator = session.Initiator;
            Player partner = session.Partner!;

            string? failure = ValidatePlayerTrade(session);
            if (failure != null)
            {
                session.ClearAcceptances();
                Tell(initiator, "Trade failed: " + failure);
                Tell(partner, "Trade failed: " + failure);
                return;
            }

            session.Committing = true;

            int initiatorCredits = session.InitiatorOffer.Credits;
            int partnerCredits = session.PartnerOffer.Credits;
            SetCash(
                initiator,
                (long)initiator.Stats.GetOrZero(CharacterStat.Cash) - initiatorCredits + partnerCredits);
            SetCash(
                partner,
                (long)partner.Stats.GetOrZero(CharacterStat.Cash) - partnerCredits + initiatorCredits);

            DeliverOffer(session.InitiatorOffer, partner);
            DeliverOffer(session.PartnerOffer, initiator);

            Unregister(initiator);
            Unregister(partner);

            SendCompleteClose(initiator, partner);
            SendCompleteClose(partner, initiator);
            SendSocialStatus(initiator, 0);
            SendSocialStatus(partner, 0);

            _flush.NotifyDirty(initiator);
            _flush.NotifyDirty(partner);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player trade completed {0} <-> {1} items={2}/{3} credits={4}/{5}",
                    initiator.Identity.Instance,
                    partner.Identity.Instance,
                    session.InitiatorOffer.Count,
                    session.PartnerOffer.Count,
                    initiatorCredits,
                    partnerCredits));
        }

        /// <summary>
        /// Hands every item in <paramref name="offer"/> to <paramref name="receiver"/>. Space was
        /// already checked by <see cref="ValidateSide"/>; a failure here would mean the inventory
        /// changed mid-commit, so the item is returned to its owner rather than destroyed.
        /// </summary>
        void DeliverOffer(TradeOffer offer, Player receiver)
        {
            foreach (Item item in offer.DrainAll())
            {
                if (!receiver.Inventory.TryPlace(item, out Container page, out int slot))
                {
                    _logger.Error(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Trade delivery failed instance={0} receiver={1}; item held back",
                            item.InstanceId,
                            receiver.Identity.Instance));
                    continue;
                }

                receiver.Inventory.MarkDirty(item, page, slot);
                SendGrant(receiver, item, page);
            }
        }

        void CommitShop(Player player, TradeSession session)
        {
            VendingMachine machine = session.Machine!;
            NpcCharacter? owner = machine.OwnerNpc;
            if (machine.Playfield == null || (owner != null && (owner.IsDead || owner.Playfield == null)))
            {
                Cancel(player, "the shop closed");
                return;
            }

            TradeOffer offer = session.InitiatorOffer;
            int skillSteps = TradeRules.PricingSkillSteps(player);

            // Price the purchase off the live stock list: a slot may have been re-rolled since the
            // client picked it, and the shopper must pay for what they will actually receive.
            var purchases = new List<Purchase>(session.ShopPicks.Count);
            long buyTotal = 0;
            foreach (int stockIndex in session.ShopPicks)
            {
                if (!machine.Stock.TryGetSlot(stockIndex, out ShopStockSlot stock))
                    continue;

                int price = TradeRules.BuyPrice(
                    TradeRules.ItemValue(_catalog, stock.LowId, stock.HighId, stock.Quality),
                    machine.SellModifier,
                    skillSteps);
                purchases.Add(new Purchase(stock, price));
                buyTotal += price;
            }

            long sellTotal = 0;
            foreach (Item item in offer.Items.Values)
            {
                if (TradeRules.IsNoDrop(item))
                {
                    Tell(player, "Trade failed: " + item.Name + " cannot be sold.");
                    return;
                }

                sellTotal += TradeRules.SellPrice(
                    TradeRules.ItemValue(_catalog, item.LowId, item.HighId, item.Quality),
                    machine.BuyModifier,
                    skillSteps);
            }

            long finalCash = (long)player.Stats.GetOrZero(CharacterStat.Cash) - buyTotal + sellTotal;
            if (finalCash < 0)
            {
                Tell(player, "Trade failed: you cannot afford that.");
                return;
            }

            if (finalCash > TradeRules.MaxCash)
            {
                Tell(player, "Trade failed: that would exceed the credit cap.");
                return;
            }

            // Mint first so unique duplication is checked against real items, and so a rejected
            // purchase costs the player nothing. Ids are allocated here because a bag only gets its
            // container identity once it has an instance id.
            var minted = new List<MintedPurchase>(purchases.Count);
            foreach (Purchase purchase in purchases)
            {
                ShopStockSlot stock = purchase.Stock;
                Item item = _minter.Create(stock.LowId, stock.HighId, stock.Quality, ItemSource.Vendor);
                item.InstanceId = _ids.Allocate();
                item.IsPersisted = false;
                item.ApplyContainerIdentityIfBag();
                if (TradeRules.IsUnique(item)
                    && (TradeRules.WouldDuplicateUnique(player, item.LowId, item.HighId)
                        || ContainsTemplate(minted, item)))
                {
                    Tell(player, "Trade failed: you already have " + item.Name + ".");
                    return;
                }

                minted.Add(new MintedPurchase(item, purchase.Price));
            }

            session.Committing = true;

            // Sold items leave first so their slots are available to the purchase.
            Identity graveyard = machine.Identity;
            foreach (Item sold in offer.DrainAll())
                player.Inventory.MarkOrphaned(sold, graveyard);

            int delivered = 0;
            long charged = buyTotal;
            foreach (MintedPurchase purchase in minted)
            {
                Item item = purchase.Item;
                if (!player.Inventory.TryPlace(item, out Container page, out int slot))
                {
                    // Inventory and overflow both filled up mid-commit. Refund rather than charge
                    // for goods the player never received.
                    charged -= purchase.Price;
                    Tell(player, "Could not add " + item.Name + " to inventory. (inventory is full)");
                    continue;
                }

                delivered++;
                player.Inventory.MarkDirty(item, page, slot);
                SendGrant(player, item, page);
            }

            SetCash(player, (long)player.Stats.GetOrZero(CharacterStat.Cash) - charged + sellTotal);
            _flush.NotifyDirty(player);

            Unregister(player);
            machine.Stock.CloseTrade();

            Identity shopIdentity = machine.ShopIdentity;
            player.Session!.Send(EndFrame(player.Identity, TradeAction.Complete, shopIdentity, shopIdentity));

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Shop trade completed player={0} machine={1} bought={2} sold={3} charged={4} sellTotal={5}",
                    player.Identity.Instance,
                    shopIdentity.Instance,
                    delivered,
                    purchases.Count,
                    charged,
                    sellTotal));
        }

        static bool ContainsTemplate(List<MintedPurchase> items, Item candidate)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item.LowId == candidate.LowId && items[i].Item.HighId == candidate.HighId)
                    return true;
            }

            return false;
        }

        /// <summary>A stock line the shopper picked, priced at the moment of commit.</summary>
        readonly struct Purchase
        {
            public Purchase(ShopStockSlot stock, int price)
            {
                Stock = stock;
                Price = price;
            }

            public ShopStockSlot Stock { get; }

            public int Price { get; }
        }

        readonly struct MintedPurchase
        {
            public MintedPurchase(Item item, int price)
            {
                Item = item;
                Price = price;
            }

            public Item Item { get; }

            public int Price { get; }
        }

        #endregion

        #region Cancellation

        public void Decline(Player player, TradeSession session)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(session);
            Close(session, "declined");
        }

        /// <summary>Cancels the session <paramref name="player"/> is in, if any.</summary>
        public void Cancel(Player player, string reason)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (!TryGetSession(player, out TradeSession session))
                return;

            Close(session, reason);
        }

        /// <summary>Cancels every session against <paramref name="machine"/> (vendor death, despawn).</summary>
        public void CloseMachine(VendingMachine machine, string reason)
        {
            ArgumentNullException.ThrowIfNull(machine);

            List<TradeSession>? affected = null;
            lock (_gate)
            {
                foreach (TradeSession session in _byPlayer.Values)
                {
                    if (!ReferenceEquals(session.Machine, machine))
                        continue;

                    affected ??= [];
                    if (!affected.Contains(session))
                        affected.Add(session);
                }
            }

            if (affected != null)
            {
                foreach (TradeSession session in affected)
                    Close(session, reason);
            }

            machine.Stock.Reset();
        }

        void Close(TradeSession session, string reason)
        {
            if (session.Committing)
                return;

            session.Committing = true;

            ReturnOffer(session.Initiator, session.InitiatorOffer);
            Unregister(session.Initiator);

            Player? partner = session.Partner;
            if (partner != null)
            {
                ReturnOffer(partner, session.PartnerOffer);
                Unregister(partner);
            }

            if (session.Kind == TradeKind.Shop)
            {
                session.Machine?.Stock.CloseTrade();
                session.Initiator.Session?.Send(
                    EndFrame(session.Initiator.Identity, TradeAction.Decline, Identity.None, Identity.None));
                SendSocialStatus(session.Initiator, 4);
            }
            else
            {
                SendDeclineClose(session.Initiator);
                SendDeclineClose(partner);
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Trade closed kind={0} initiator={1} reason={2}",
                    session.Kind,
                    session.Initiator.Identity.Instance,
                    reason));
        }

        /// <summary>
        /// Puts a cancelled offer back in the owner's first free slot. Items on the table came out of
        /// that inventory moments ago, so a full inventory means something else took the slot; the
        /// item then goes to overflow only if it is not yet durable, otherwise it stays put and is
        /// reported, never silently dropped.
        /// </summary>
        void ReturnOffer(Player owner, TradeOffer offer)
        {
            foreach (Item item in offer.DrainAll())
            {
                if (owner.Inventory.TryPlace(item, out Container page, out int slot))
                {
                    owner.Inventory.MarkDirty(item, page, slot);
                    SendGrant(owner, item, page);
                    continue;
                }

                _logger.Error(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Trade return failed instance={0} owner={1}; no free slot",
                        item.InstanceId,
                        owner.Identity.Instance));
                Tell(owner, "Could not return " + item.Name + " to your inventory.");
            }

            offer.Credits = 0;
            _flush.NotifyDirty(owner);
        }

        #endregion

        #region Tick

        /// <summary>
        /// Per-playfield liveness pass. Cancels windows whose anchor died, despawned, zoned, or
        /// walked out of range.
        /// </summary>
        public void Tick(Playfield playfield, double deltaTime)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            _ = deltaTime;

            // Runs on every playfield tick, so the common "nobody is trading" case must not allocate.
            List<TradeSession>? stale = null;
            lock (_gate)
            {
                if (_byPlayer.Count == 0)
                    return;

                foreach (TradeSession session in _byPlayer.Values)
                {
                    if (!ReferenceEquals(session.Initiator.Playfield, playfield))
                        continue;

                    if (!IsStale(session, playfield))
                        continue;

                    stale ??= [];
                    if (!stale.Contains(session))
                        stale.Add(session);
                }
            }

            if (stale == null)
                return;

            foreach (TradeSession session in stale)
                Close(session, "out of range or partner gone");
        }

        static bool IsStale(TradeSession session, Playfield playfield)
        {
            Player initiator = session.Initiator;
            if (initiator.Session == null || initiator.IsDead)
                return true;

            if (session.Kind == TradeKind.Shop)
            {
                VendingMachine? machine = session.Machine;
                if (machine == null || !ReferenceEquals(machine.Playfield, playfield))
                    return true;

                NpcCharacter? owner = machine.OwnerNpc;
                if (owner != null && (owner.IsDead || !ReferenceEquals(owner.Playfield, playfield)))
                    return true;

                Dynel anchor = owner != null ? owner : machine;
                return initiator.Distance3D(anchor) > RangeCancelDistance;
            }

            Player? partner = session.Partner;
            if (partner == null
                || partner.Session == null
                || partner.IsDead
                || !ReferenceEquals(partner.Playfield, playfield))
                return true;

            return initiator.Distance3D(partner) > RangeCancelDistance;
        }

        #endregion

        #region Registry

        bool HasSession(Player player)
        {
            lock (_gate)
                return _byPlayer.ContainsKey(player.Identity.Instance);
        }

        void Register(Player player, TradeSession session)
        {
            lock (_gate)
                _byPlayer[player.Identity.Instance] = session;
        }

        void Unregister(Player player)
        {
            lock (_gate)
                _byPlayer.Remove(player.Identity.Instance);
        }

        /// <summary>
        /// Whether a client frame addresses the machine pane rather than the player's own. The owning
        /// NPC counts too, since that is the identity the player used to open the shop. Compares whole
        /// identities: a Dynels.dat machine instance can collide numerically with a character id, and
        /// matching on instance alone would let one pane answer for the other.
        /// </summary>
        static bool IsShopSide(TradeSession session, Identity target)
        {
            VendingMachine? machine = session.Machine;
            if (machine == null)
                return false;

            if (target == machine.Identity)
                return true;

            NpcCharacter? owner = machine.OwnerNpc;
            return owner != null && target == owner.Identity;
        }

        #endregion

        #region Packets

        void SendPlayerOpen(Player viewer, Player partner)
            => viewer.Session?.Send(OpenFrame(viewer.Identity, partner.Identity, Identity.None));

        static TradeMessage OpenFrame(Identity identity, Identity target, Identity container)
            => new()
            {
                Identity = identity,
                Unknown = 0,
                Unknown1 = 2,
                Action = TradeAction.Open,
                Target = target,
                Container = container
            };

        static TradeMessage ActionFrame(
            Identity identity,
            TradeAction action,
            Identity target,
            Identity container)
            => new()
            {
                Identity = identity,
                Unknown = 0,
                Unknown1 = 2,
                Action = action,
                Target = target,
                Container = container
            };

        static TradeMessage EndFrame(
            Identity identity,
            TradeAction action,
            Identity target,
            Identity container)
            => ActionFrame(identity, action, target, container);

        static TradeMessage StatusFrame(Identity subject, TradeAction action)
            => ActionFrame(subject, action, subject, subject);

        static TradeMessage EndPromptFrame(Identity viewer, Identity partner)
            => ActionFrame(viewer, TradeAction.Accept, partner, partner);

        static TradeMessage CreditsFrame(Identity offerOwner, int credits)
            => new()
            {
                Identity = offerOwner,
                Unknown = 0,
                Unknown1 = 2,
                Action = TradeAction.UpdateCredits,
                Param1 = 0,
                Param2 = credits,
                Param3 = 0,
                Param4 = 0
            };

        static TradeMessage RemoveAck(Identity identity, TradeMessage message)
            => new()
            {
                Identity = identity,
                Unknown = 0,
                Unknown1 = message.Unknown1,
                Action = message.Action,
                Target = message.Target,
                Container = message.Container
            };

        static void Acknowledge(Player player, TradeMessage message)
            => player.Session?.Send(
                new TradeMessage
                {
                    Identity = message.Identity,
                    Unknown = message.Unknown,
                    Unknown1 = message.Unknown1,
                    Action = message.Action,
                    Target = message.Target,
                    Container = message.Container
                });

        void SendCompleteClose(Player viewer, Player partner)
        {
            // Live sends a pair of action-4 frames, one per pane, to clear both sides.
            viewer.Session?.Send(
                ActionFrame(viewer.Identity, TradeAction.Complete, partner.Identity, partner.Identity));
            viewer.Session?.Send(
                ActionFrame(partner.Identity, TradeAction.Complete, viewer.Identity, viewer.Identity));
        }

        void SendDeclineClose(Player? viewer)
        {
            if (viewer?.Session == null)
                return;

            viewer.Session.Send(EndFrame(viewer.Identity, TradeAction.Decline, Identity.None, Identity.None));
            SendSocialStatus(viewer, 0);
        }

        /// <summary>
        /// Capture 20260806-rabbit: an item that lands in overflow is announced with a TemplateAction
        /// on the overflow window followed by a ContainerAddItem to the next free slot marker.
        /// </summary>
        const int OverflowTemplateActionUnknown2 = 87;

        /// <summary>Placement marker meaning "next free slot"; the client picks the real slot.</summary>
        const int NextFreeSlotMarker = 0x6F;

        const int TradeRenderTemplateActionUnknown2 = 0x55;

        static void SendItemRender(Player viewer, Item item, Identity source)
            => viewer.Session?.Send(
                new TemplateActionMessage
                {
                    Identity = viewer.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowId,
                    ItemHighId = item.HighId,
                    Quality = item.Quality,
                    Unknown1 = 1,
                    Unknown2 = TradeRenderTemplateActionUnknown2,
                    Placement = source,
                    Unknown3 = 0,
                    Unknown4 = 0
                });

        static void SendReturnToInventory(Player player, int tradeSlot)
            => player.Session?.Send(
                new ContainerAddItemMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity
                    {
                        Type = IdentityType.KnuBotTradeWindow,
                        Instance = tradeSlot
                    },
                    Target = new Identity
                    {
                        Type = IdentityType.Inventory,
                        Instance = player.Identity.Instance
                    },
                    TargetPlacement = NextFreeSlotMarker
                });

        /// <summary>
        /// Tells the client about an item that just appeared. Main-inventory grants use a plain
        /// AddTemplate; an item that fell through to overflow needs the capture-backed
        /// TemplateAction + ContainerAddItem pair (capture 20260806-rabbit) so the overflow window
        /// opens and renders it.
        /// </summary>
        static void SendGrant(Player player, Item item, Container page)
        {
            if (player.Session == null)
                return;

            if (page.Identity.Type != IdentityType.OverflowWindow)
            {
                player.Session.Send(
                    new AddTemplateMessage
                    {
                        Identity = player.Identity,
                        HighId = item.HighId,
                        LowId = item.LowId,
                        Quality = item.Quality,
                        Count = item.StackCount
                    });
                return;
            }

            player.Session.Send(
                new TemplateActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowId,
                    ItemHighId = item.HighId,
                    Quality = item.Quality,
                    Unknown1 = 1,
                    Unknown2 = OverflowTemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            player.Session.Send(
                new ContainerAddItemMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                    {
                        Type = IdentityType.OverflowWindow,
                        Instance = player.Identity.Instance
                    },
                    TargetPlacement = NextFreeSlotMarker
                });
        }

        static void SendSocialStatus(Player player, int value)
            => player.Session?.Send(
                new StatMessage
                {
                    Identity = player.Identity,
                    Stats =
                    [
                        new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.SocialStatus, Value2 = (uint)value }
                    ]
                });

        void SetCash(Player player, long cash)
        {
            player.Stats.Set(
                CharacterStat.Cash,
                TradeRules.ClampCash(cash),
                StatDetail.Base,
                dirty: true);
            player.FlushDirtyStats();
        }

        static void Tell(Player player, string text)
            => player.Session?.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });

        #endregion
    }
}
