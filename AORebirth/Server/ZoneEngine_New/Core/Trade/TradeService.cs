namespace ZoneEngine_New.Core.Trade
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Helpers;
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
        readonly ITradePersistence _persistence;

        public TradeService(
            IZoneLogger logger,
            IGameData gameData,
            IItemTemplateCatalog catalog,
            HashItemMinter minter,
            IItemInstanceIdAllocator ids,
            InventoryFlushService flush,
            ITradePersistence persistence)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(ids);
            ArgumentNullException.ThrowIfNull(flush);
            ArgumentNullException.ThrowIfNull(persistence);

            _logger = logger;
            _gameData = gameData;
            _catalog = catalog;
            _minter = minter;
            _ids = ids;
            _flush = flush;
            _persistence = persistence;
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
            if (player.GetEdgeDistanceTo(anchor) > RangeCancelDistance)
            {
                ClientFeedback.Send(player, "Feedback_TooFarAway");
                return false;
            }

            if (HasSession(player))
                Cancel(player, "opening another trade");

            int templateId = machine.Template.Id;
            if (!string.IsNullOrEmpty(machine.Stock.ConfigurationUnavailableReason))
            {
                Tell(player, "This shop has unresolved stock data.");
                _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                    "Vending machine {0} stock unavailable: {1}",
                    machine.ShopIdentity.Instance,
                    machine.Stock.ConfigurationUnavailableReason));
                return false;
            }
            if (machine.Stock.IsConfiguredSnapshot)
            {
                // Configured stock belongs only to its exact live accepted vendor, never to
                // a matching template, nearby actor, or stale replacement identity.
                if (!IsCurrentAcceptedShop(player, machine))
                    return false;
                machine.Stock.EnsureConfiguredFresh(Random.Shared);
            }
            else
            {
                if (!_gameData.TryGetVendingMachine(templateId, out VendingMachineDefinition definition))
                {
                    Tell(player, "This shop has no stock list yet.");
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Vending machine {0} has no VendingMachines.json entry", templateId));
                    return false;
                }
                machine.Stock.EnsureFresh(definition, _minter, Random.Shared);
            }

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

            if (initiator.GetEdgeDistanceTo(partner) > RangeCancelDistance)
            {
                ClientFeedback.Send(initiator, "Feedback_TargetOutsideRangeForTrade");
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
                ClientFeedback.Send(initiator, "Feedback_TargetIsAlreadyInATrade");
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

            // A Knubot window is driven by the KnuBot trade messages only.
            if (session.Kind == TradeKind.Knubot)
                return;

            if (session.Machine?.Stock.IsConfiguredSnapshot == true
                && (session.AcceptedShopTransport == null || !IsCurrentAcceptedShop(player, session.Machine, session.AcceptedShopTransport)))
            {
                Cancel(player, "accepted vendor ownership changed");
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

        static bool IsCurrentAcceptedShop(Player player, VendingMachine machine,
            ZoneEngine_New.Core.Network.IZoneSession? expectedTransport = null)
        {
            var transport = player.Session;
            var field = player.Playfield;
            var owner = machine.OwnerNpc;
            return transport != null && transport.State == ZoneEngine_New.Core.Network.SessionState.InPlay
                && ReferenceEquals(transport.Player, player) && (expectedTransport == null || ReferenceEquals(transport, expectedTransport))
                && field != null && !field.IsDisposed && !player.IsDead && !player.IsPersistenceQuarantined && player.Inventory.IsHydrated
                && (owner == null || (!owner.IsDead && ReferenceEquals(owner.Shop, machine) && ReferenceEquals(owner.Playfield, field)))
                && ReferenceEquals(machine.Playfield, field)
                && player.GetEdgeDistanceTo(owner != null ? owner : machine) <= RangeCancelDistance
                && field.GetRequiredService<DynelRegistry>().TryGet(player.Identity, out var currentPlayer)
                && ReferenceEquals(currentPlayer, player)
                && field.GetRequiredService<ZoneEngine_New.Core.Mobs.NpcContentActivationService>()
                    .TryGetShopBinding(machine, out _);
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
                ClientFeedback.Send(player, "Feedback_CantTradeMoreItems");
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

            if (!page.Content.TryGetValue(source.Instance, out Item? item) || player.Inventory.IsLockedForTransfer(item))
                return;

            if (InventoryMoveService.IsBagItem(item))
            {
                // Bag interiors would need handle invalidation and recursive ownership transfer.
                ClientFeedback.Send(player, "Feedback_ItemCantBeTraded");
                return;
            }

            if (TradeRules.IsNoDrop(item))
            {
                ClientFeedback.Send(player, "Feedback_NoDropItemCantBeTraded");
                return;
            }

            if (page.Remove(source.Instance) == null)
                return;

            TradeOffer offer = session.OfferFor(player);
            int tradeSlot = offer.Add(item);
            if (tradeSlot < 0)
            {
                page.Add(source.Instance, item);
                ClientFeedback.Send(player, "Feedback_CantTradeMoreItems");
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
                ClientFeedback.Send(player, "Feedback_InventoryFull");
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
                    TellTradeFailure(session.Initiator, failure);
                    TellTradeFailure(session.Partner!, failure);
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
            if (session.Committing)
                return;
            Player initiator = session.Initiator;
            Player partner = session.Partner!;

            string? failure = ValidatePlayerTrade(session);
            if (failure != null)
            {
                session.ClearAcceptances();
                TellTradeFailure(initiator, failure);
                TellTradeFailure(partner, failure);
                return;
            }

            session.Committing = true;

            int initiatorCredits = session.InitiatorOffer.Credits;
            int partnerCredits = session.PartnerOffer.Credits;
            bool durable = false;
            try
            {
                _flush.WithExclusivePlayers(initiator, partner, () =>
                {
                    var deliveries = new List<Delivery>();
                    PlanDeliveries(session.InitiatorOffer.Items.Values, partner, deliveries);
                    PlanDeliveries(session.PartnerOffer.Items.Values, initiator, deliveries);
                    int initiatorCash = checked(initiator.Stats.GetOrZero(CharacterStat.Cash) - initiatorCredits + partnerCredits);
                    int partnerCash = checked(partner.Stats.GetOrZero(CharacterStat.Cash) - partnerCredits + initiatorCredits);
                    PersistPlan(initiator, initiatorCash, partner, partnerCash, deliveries, [], Identity.None);
                    durable = true;
                    session.InitiatorOffer.DrainAll();
                    session.PartnerOffer.DrainAll();
                    ApplyDeliveries(deliveries);
                    SetCash(initiator, initiatorCash);
                    SetCash(partner, partnerCash);
                    foreach (Delivery delivery in deliveries)
                        SendDelivery(delivery);
                });
            }
            catch (Exception exception)
            {
                FailedCommit(session, exception, durable);
                return;
            }

            Unregister(initiator);
            Unregister(partner);

            SendCompleteClose(initiator, partner);
            SendCompleteClose(partner, initiator);
            SendSocialStatus(initiator, 0);
            SendSocialStatus(partner, 0);

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

        // Reserve only durable main-inventory locations. Offers remain untouched until COMMIT.
        static void PlanDeliveries(IEnumerable<Item> items, Player receiver, List<Delivery> deliveries)
        {
            if (!TryPlanDeliveries(items, receiver, deliveries))
                throw new InvalidOperationException("Inventory has no durable location for the trade.");
        }

        /// <summary>
        /// Plans each item in send order, as the client will place them: a stackable item that fits whole onto an
        /// existing stack merges into it (the client parks the AddTemplate copy in its first free slot and folds it
        /// in on the JoinItems echo, freeing that slot again); anything else takes the first free slot.
        /// </summary>
        static bool TryPlanDeliveries(IEnumerable<Item> items, Player receiver, List<Delivery> deliveries)
        {
            Container page = receiver.Inventory.Inventory;
            var occupied = new HashSet<int>(page.Content.Keys);
            var planned = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
            foreach (Delivery delivery in deliveries)
            {
                if (!ReferenceEquals(delivery.Receiver, receiver))
                    continue;
                if (delivery.MergeTarget == null)
                    occupied.Add(delivery.Slot);
                else
                    planned[delivery.MergeTarget] = planned.GetValueOrDefault(delivery.MergeTarget) + delivery.Item.StackCount;
            }

            foreach (Item item in items)
            {
                int slot = page.Offset;
                while (slot < page.Offset + page.Capacity && occupied.Contains(slot)) slot++;
                if (slot == page.Offset + page.Capacity)
                    return false;

                if (InventoryStacking.TryFindStackTarget(page, item, planned, out int targetSlot, out Item target))
                {
                    planned[target] = planned.GetValueOrDefault(target) + item.StackCount;
                    deliveries.Add(new Delivery(item, receiver, page, slot, target, targetSlot));
                    continue;
                }

                occupied.Add(slot);
                deliveries.Add(new Delivery(item, receiver, page, slot));
            }

            return true;
        }

        static void ApplyDeliveries(List<Delivery> deliveries)
        {
            foreach (Delivery delivery in deliveries)
            {
                if (delivery.MergeTarget != null)
                {
                    if (!delivery.Page.Content.TryGetValue(delivery.MergeSlot, out Item? current)
                        || !ReferenceEquals(current, delivery.MergeTarget))
                        throw new InvalidOperationException("Planned trade stack changed after durable commit.");
                    delivery.MergeTarget.StackCount += delivery.Item.StackCount;
                    continue;
                }

                if (!delivery.Page.Add(delivery.Slot, delivery.Item))
                    throw new InvalidOperationException("Reserved trade location changed after durable commit.");
                delivery.Item.IsPersisted = true;
            }
        }

        static void SendDelivery(Delivery delivery)
        {
            if (delivery.MergeTarget != null)
                InventoryStacking.SendGrantAndJoin(delivery.Receiver, delivery.Item, delivery.Page, delivery.MergeSlot, delivery.Slot);
            else
                SendGrant(delivery.Receiver, delivery.Item, delivery.Page);
        }

        /// <summary>
        /// One item reaching a receiver. Slot is its own main-inventory slot, or for a merge (MergeTarget set) the
        /// free slot the client parks the copy in before folding it into the stack at MergeSlot.
        /// </summary>
        sealed record Delivery(Item Item, Player Receiver, Container Page, int Slot, Item? MergeTarget = null, int MergeSlot = 0);

        void PersistPlan(Player first, int firstCash, Player? second, int secondCash,
            List<Delivery> deliveries, IReadOnlyList<Item> retired, Identity graveyard)
        {
            var players = second == null ? new[] { first } : new[] { first, second };
            var pending = players.Select(p => p.Inventory.TakeDirty()).ToArray();
            var nanos = players.Select(p => p.DrainDirtyUploadedNanos()).ToArray();
            try
            {
                var inserts = new Dictionary<int, ItemInstanceRecord>();
                var updates = new Dictionary<int, ItemLocationUpdate>();
                foreach (var flush in pending)
                {
                    if (flush == null) continue;
                    foreach (var insert in flush.Inserts) inserts[insert.InstanceId] = insert;
                    foreach (var update in flush.Updates) updates[update.InstanceId] = update;
                }
                var stackTotals = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
                foreach (Delivery delivery in deliveries)
                {
                    if (delivery.MergeTarget == null)
                    {
                        Place(delivery.Item, delivery.Page.Identity, delivery.Slot);
                        continue;
                    }

                    // A merged item gives up its own row (a traded stack is retired, a minted one never written)
                    // and its count lands on the stack's row.
                    if (delivery.Item.IsPersisted)
                        Place(delivery.Item, new Identity { Type = IdentityType.None, Instance = delivery.Receiver.Identity.Instance },
                            delivery.Item.InstanceId);
                    else
                    {
                        inserts.Remove(delivery.Item.InstanceId);
                        updates.Remove(delivery.Item.InstanceId);
                    }

                    stackTotals[delivery.MergeTarget] = stackTotals.GetValueOrDefault(delivery.MergeTarget, delivery.MergeTarget.StackCount)
                        + delivery.Item.StackCount;
                }

                foreach (Delivery delivery in deliveries)
                {
                    if (delivery.MergeTarget is not Item target || !stackTotals.TryGetValue(target, out int total))
                        continue;
                    if (target.InstanceId <= 0 || !target.IsPersisted || total > InventoryStacking.MaxStackCount)
                        throw new InvalidOperationException("Invalid trade stack merge.");
                    updates[target.InstanceId] = new ItemLocationUpdate(target.InstanceId, (int)delivery.Page.Identity.Type,
                        delivery.Page.Identity.Instance, delivery.MergeSlot, total);
                    inserts.Remove(target.InstanceId);
                }

                foreach (Item item in retired)
                {
                    inserts.Remove(item.InstanceId);
                    updates.Remove(item.InstanceId);
                    if (item.IsPersisted) Place(item, graveyard, item.InstanceId);
                }
                var characters = new List<TradeCharacterWrite> { new(first.Identity.Instance, firstCash, nanos[0]) };
                if (second != null) characters.Add(new(second.Identity.Instance, secondCash, nanos[1]));
                _persistence.Persist(new TradePersistenceBatch(inserts.Values.ToArray(), updates.Values.ToArray(), characters));
                foreach (var flush in pending) flush?.MarkNewlyPersisted();

                void Place(Item item, Identity container, int slot)
                {
                    if (item.InstanceId <= 0) throw new InvalidOperationException("Trade item has no durable instance id.");
                    inserts.Remove(item.InstanceId);
                    updates.Remove(item.InstanceId);
                    if (item.IsPersisted)
                        updates[item.InstanceId] = new ItemLocationUpdate(item.InstanceId, (int)container.Type, container.Instance, slot);
                    else
                        inserts[item.InstanceId] = new ItemInstanceRecord
                        {
                            InstanceId = item.InstanceId, ContainerType = (int)container.Type,
                            ContainerInstance = container.Instance, ContainerPlacement = slot,
                            ItemType = item.Identity.Type != IdentityType.None ? (int)item.Identity.Type : item.Definition.ItemType,
                            LowId = item.LowId, HighId = item.HighId, Quality = item.Quality,
                            StackCount = item.StackCount, Source = item.Source
                        };
                }
            }
            catch (DatabaseCommitOutcomeUnknownException)
            {
                // Do not requeue a transaction whose outcome is unknown.
                foreach (Player player in players) player.QuarantinePersistence();
                throw;
            }
            catch
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (pending[i] != null) players[i].Inventory.RestoreDirty(pending[i]!);
                    players[i].RestoreDirtyUploadedNanos(nanos[i]);
                }
                throw;
            }
        }

        void FailedCommit(TradeSession session, Exception exception, bool durable)
        {
            _logger.Error(exception, "Trade persistence failed; completion was not acknowledged.");
            if (durable || exception is DatabaseCommitOutcomeUnknownException
                || session.Initiator.IsPersistenceQuarantined || session.Partner?.IsPersistenceQuarantined == true)
            {
                session.Initiator.QuarantinePersistence();
                session.Partner?.QuarantinePersistence();
                Unregister(session.Initiator);
                if (session.Partner != null) Unregister(session.Partner);
                session.Machine?.Stock.CloseTrade();
                session.Initiator.Session?.Close();
                session.Partner?.Session?.Close();
                return;
            }
            session.Committing = false;
            session.ClearAcceptances();
            _flush.NotifyDirty(session.Initiator);
            if (session.Partner != null) _flush.NotifyDirty(session.Partner);
            Tell(session.Initiator, "Trade failed: persistence was not committed. Please try again.");
            if (session.Partner != null) Tell(session.Partner, "Trade failed: persistence was not committed. Please try again.");
        }

        void CommitShop(Player player, TradeSession session)
        {
            if (session.Committing)
                return;
            VendingMachine machine = session.Machine!;
            if (machine.Stock.IsConfiguredSnapshot && (!ReferenceEquals(player, session.Initiator)
                || session.AcceptedShopTransport == null || !IsCurrentAcceptedShop(player, machine, session.AcceptedShopTransport)))
            {
                Cancel(player, "accepted vendor ownership changed");
                return;
            }
            NpcCharacter? owner = machine.OwnerNpc;
            if (machine.Playfield == null || (owner != null && (owner.IsDead || owner.Playfield == null)))
            {
                Cancel(player, "the shop closed");
                return;
            }

            TradeOffer offer = session.InitiatorOffer;
            int skillSteps = TradeRules.PricingSkillSteps(player, machine.PricingSkill);

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
                    ClientFeedback.Send(player, "Feedback_NoDropItemCantBeTraded");
                    return;
                }

                sellTotal += TradeRules.SellPrice(
                    TradeRules.StackValue(item, TradeRules.ItemValue(_catalog, item.LowId, item.HighId, item.Quality)),
                    machine.BuyModifier,
                    skillSteps);
            }

            long finalCash = (long)player.Stats.GetOrZero(CharacterStat.Cash) - buyTotal + sellTotal;
            if (finalCash < 0)
            {
                ClientFeedback.Send(player, "Feedback_NotEnoughCredits");
                return;
            }

            if (finalCash > TradeRules.MaxCash)
            {
                Tell(player, "Trade failed: that would exceed the credit cap.");
                return;
            }

            // Mint first so unique duplication is checked against real items, and so a rejected
            // purchase costs the player nothing.
            var minted = new List<MintedPurchase>(purchases.Count);
            foreach (Purchase purchase in purchases)
            {
                ShopStockSlot stock = purchase.Stock;
                Item item = _minter.Create(stock.LowId, stock.HighId, stock.Quality, ItemSource.Vendor);
                if (TradeRules.IsUnique(item)
                    && (TradeRules.WouldDuplicateUnique(player, item.LowId, item.HighId)
                        || ContainsTemplate(minted, item)))
                {
                    ClientFeedback.Send(player, "Feedback_AlreadyGotUniqueItem");
                    return;
                }

                minted.Add(new MintedPurchase(item, purchase.Price));
            }

            // The same stackable line picked several times arrives as one stack, a new one past 50000. Ids are
            // allocated after that because a bag only gets its container identity once it has an instance id.
            List<Item> bought = ConsolidateStacks(minted.Select(p => p.Item).ToList(), template => _minter.Create(
                template.LowId, template.HighId, template.Quality, ItemSource.Vendor));
            foreach (Item item in bought)
                item.AssignInstanceId(_ids.Allocate());

            // Overflow is intentionally memory-only; accepting money for a purchase there would
            // lose the paid-for item on restart. Require real inventory (or a stack to join) up front.
            if (!TryPlanDeliveries(bought, player, new List<Delivery>()))
            {
                ClientFeedback.Send(player, "Feedback_NoRoomInInventory");
                return;
            }

            session.Committing = true;
            bool durable = false;
            int soldCount = offer.Count;
            try
            {
                _flush.WithExclusivePlayers(player, null, () =>
                {
                    var deliveries = new List<Delivery>();
                    PlanDeliveries(bought, player, deliveries);
                    PersistPlan(player, checked((int)finalCash), null, 0, deliveries, offer.Items.Values.ToArray(), machine.Identity);
                    durable = true;
                    offer.DrainAll();
                    ApplyDeliveries(deliveries);
                    SetCash(player, finalCash);
                    foreach (Delivery delivery in deliveries) SendDelivery(delivery);
                });
            }
            catch (Exception exception)
            {
                FailedCommit(session, exception, durable);
                return;
            }

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
                    minted.Count,
                    soldCount,
                    buyTotal,
                    sellTotal));
        }

        /// <summary>
        /// Folds stackable items of the same low/high/QL into as few stacks as the 50000 cap allows, in order of
        /// first appearance. The total count is unchanged; <paramref name="mint"/> supplies an extra item only when
        /// a group's total needs more stacks than it has items.
        /// </summary>
        static List<Item> ConsolidateStacks(List<Item> items, Func<Item, Item> mint)
        {
            var result = new List<Item>(items.Count);
            var groups = new Dictionary<(int, int, int), List<Item>>();
            var order = new List<(int, int, int)>();
            foreach (Item item in items)
            {
                if (!InventoryStacking.IsStackable(item))
                {
                    result.Add(item);
                    continue;
                }

                var key = (item.LowId, item.HighId, item.Quality);
                if (!groups.TryGetValue(key, out List<Item>? group))
                {
                    groups[key] = group = [];
                    order.Add(key);
                }

                group.Add(item);
            }

            foreach (var key in order)
            {
                List<Item> group = groups[key];
                long remaining = 0;
                foreach (Item item in group)
                    remaining += item.StackCount;

                int index = 0;
                while (remaining > 0)
                {
                    Item stack = index < group.Count ? group[index] : mint(group[0]);
                    index++;
                    int count = (int)Math.Min(remaining, InventoryStacking.MaxStackCount);
                    stack.StackCount = count;
                    remaining -= count;
                    result.Add(stack);
                }
            }

            return result;
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

        #region Knubot

        /// <summary>
        /// Opens the "Give Item" window for <paramref name="npc"/>. The client moves each item into its own
        /// KnuBot trade container as it sends it (Gamecode.dll N3Msg_NPCChatAddTradeItem), so the server mirrors
        /// that container: items leave the player's page and sit locked in <see cref="TradeSession.InitiatorOffer"/>
        /// at the index the client used, the first free one. Nothing is written until the trade commits, so an
        /// interrupted trade leaves every item at its stored location.
        /// </summary>
        public bool TryOpenKnubot(Player player, NpcCharacter npc, string message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(npc);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated || player.IsDead
                || npc.IsDead || !ReferenceEquals(npc.Playfield, player.Playfield))
                return false;

            if (player.GetEdgeDistanceTo(npc) > RangeCancelDistance)
            {
                ClientFeedback.Send(player, "Feedback_TooFarAway");
                return false;
            }

            if (TryGetSession(player, out TradeSession existing))
            {
                if (existing.Kind == TradeKind.Knubot && ReferenceEquals(existing.Npc, npc))
                    return true;

                Close(existing, "opening another trade");
            }

            Register(player, new TradeSession(Identity.None, TradeKind.Knubot, player, partner: null, machine: null, npc));

            // The client rejects an empty text (length must be 1..10000).
            player.Session.Send(new KnuBotStartTradeMessage
            {
                Identity = player.Identity,
                Unknown1 = 2,
                Target = npc.Identity,
                NumberOfItemSlotsInTradeWindow = TradeOffer.Capacity,
                Message = string.IsNullOrWhiteSpace(message) ? "Trade" : message
            });

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot trade opened player={0} npc={1}",
                player.Identity.Instance, npc.Identity.Instance));
            return true;
        }

        /// <summary>The Knubot trade <paramref name="player"/> has open with <paramref name="npc"/>, if any.</summary>
        public bool TryGetKnubot(Player player, NpcCharacter npc, out TradeSession session)
            => TryGetSession(player, out session) && session.Kind == TradeKind.Knubot && ReferenceEquals(session.Npc, npc)
                && !session.Committing;

        /// <summary>An item put into or taken out of the Give Item window.</summary>
        public void HandleKnubot(Player player, KnuBotTradeMessage message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(message);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated
                || !TryGetSession(player, out TradeSession session) || session.Kind != TradeKind.Knubot || session.Committing
                || session.Npc == null || message.Target != session.Npc.Identity
                || player.GetEdgeDistanceTo(session.Npc) > RangeCancelDistance)
                return;

            if (message.Action == KnuBotTradeAction.Add)
                AddKnubotItem(player, session.InitiatorOffer, message.Container);
            else if (message.Action == KnuBotTradeAction.Remove)
                RemoveKnubotItem(player, session.InitiatorOffer, message.Container.Instance);
        }

        void AddKnubotItem(Player player, TradeOffer offer, Identity source)
        {
            Container page = source.Type switch
            {
                IdentityType.Inventory => player.Inventory.Inventory,
                IdentityType.OverflowWindow => player.Inventory.Overflow,
                _ => null!
            };

            // The client has already moved the item into the first free index of its trade container.
            int clientSlot = FirstFreeOfferSlot(offer);
            if (page == null || !page.Content.TryGetValue(source.Instance, out Item? item) || player.Inventory.IsLockedForTransfer(item))
            {
                // Nothing the server can take: undo the client's move so its view matches.
                if (clientSlot >= 0)
                    SendReturnToInventory(player, clientSlot);
                return;
            }

            // No-drop items are allowed: the NPC only consumes them, they never reach another player.
            string? refusal = InventoryMoveService.IsBagItem(item) ? "Feedback_ItemCantBeTraded"
                : clientSlot < 0 ? "Feedback_CantTradeMoreItems"
                : null;
            if (refusal != null)
            {
                ClientFeedback.Send(player, refusal);
                if (clientSlot >= 0)
                    UndoKnubotAdd(player, page, source.Instance, item, clientSlot);
                return;
            }

            if (page.Remove(source.Instance) == null)
                return;

            offer.Add(item);
        }

        /// <summary>
        /// The client sends the item back to the first free main-inventory slot; the server re-homes it the same way
        /// so both agree where it is.
        /// </summary>
        void UndoKnubotAdd(Player player, Container page, int slot, Item item, int clientSlot)
        {
            if (TryPlaceMain(player, item, out Container target, out int targetSlot, removeFrom: page, removeSlot: slot))
            {
                player.Inventory.MarkDirty(item, target, targetSlot);
                _flush.NotifyDirty(player);
            }

            SendReturnToInventory(player, clientSlot);
        }

        void RemoveKnubotItem(Player player, TradeOffer offer, int tradeSlot)
        {
            Item? item = offer.Remove(tradeSlot);
            if (item == null)
                return;

            // The client only takes an item back into main inventory; with no room it leaves it in the window.
            if (!TryPlaceMain(player, item, out Container page, out int slot))
            {
                offer.TryRestore(tradeSlot, item);
                ClientFeedback.Send(player, "Feedback_InventoryFull");
                return;
            }

            player.Inventory.MarkDirty(item, page, slot);
            _flush.NotifyDirty(player);
        }

        /// <summary>
        /// The NPC keeps everything in the window and takes <paramref name="credits"/>, which the client already took
        /// off its own Cash when it accepted. Returns false when nothing changed hands; the caller then ends the
        /// window with <see cref="EndKnubot(Player, NpcCharacter, int, string)"/>.
        /// </summary>
        public bool TryCommitKnubot(Player player, NpcCharacter npc, int credits)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(npc);

            if (!TryGetKnubot(player, npc, out TradeSession session) || credits < 0
                || player.Stats.GetOrZero(CharacterStat.Cash) < credits)
                return false;

            TradeOffer offer = session.InitiatorOffer;
            Item[] given = offer.Items.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
            int finalCash = player.Stats.GetOrZero(CharacterStat.Cash) - credits;

            session.Committing = true;
            bool durable = false;
            try
            {
                _flush.WithExclusivePlayers(player, null, () =>
                {
                    PersistPlan(player, finalCash, null, 0, [], given, npc.Identity);
                    durable = true;
                    offer.DrainAll();
                    SetCash(player, finalCash);
                });
            }
            catch (Exception exception)
            {
                FailedCommit(session, exception, durable);
                return false;
            }

            Unregister(player);

            // An empty list removes every item still in the client's window and closes it.
            player.Session?.Send(RejectedItemsFrame(player, npc, [], 0));

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot trade completed player={0} npc={1} items={2} credits={3}",
                player.Identity.Instance, npc.Identity.Instance, given.Length, credits));
            return true;
        }

        /// <summary>Ends <paramref name="player"/>'s Knubot trade with <paramref name="npc"/>, handing everything back.</summary>
        public void EndKnubot(Player player, NpcCharacter npc, int refundCredits, string reason)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(npc);

            if (TryGetKnubot(player, npc, out TradeSession session))
                EndKnubot(session, refundCredits, reason);
        }

        /// <summary>
        /// Hands every item back in window order, as the client does: each goes to the first free main-inventory
        /// slot and is listed in KnuBotRejectedItems; one that no longer fits is left off the list (the client drops
        /// its copy) and goes to overflow if it may. <paramref name="refundCredits"/> gives back what the client took
        /// off its Cash when it accepted.
        /// </summary>
        void EndKnubot(TradeSession session, int refundCredits, string reason)
        {
            if (session.Committing)
                return;

            session.Committing = true;
            Player player = session.Initiator;
            TradeOffer offer = session.InitiatorOffer;
            var returned = new List<KnuBotRejectedItem>(offer.Count);
            var overflowed = new List<(Item Item, Container Page)>();
            foreach (int tradeSlot in offer.Items.Keys.OrderBy(x => x).ToArray())
            {
                Item item = offer.Remove(tradeSlot)!;
                if (TryPlaceMain(player, item, out Container page, out int slot))
                {
                    player.Inventory.MarkDirty(item, page, slot);
                    returned.Add(RejectedItem(item));
                }
                else if (player.Inventory.TryPlace(item, out page, out slot))
                {
                    player.Inventory.MarkDirty(item, page, slot);
                    overflowed.Add((item, page));
                }
                else
                {
                    // A stored item may not live in overflow; its row never moved, so it is back at its old slot on relog.
                    _logger.Error(string.Format(CultureInfo.InvariantCulture,
                        "Knubot trade return failed instance={0} owner={1}; no free slot", item.InstanceId, player.Identity.Instance));
                    ClientFeedback.Send(player, "Feedback_NoRoomInInventory");
                }
            }

            _flush.NotifyDirty(player);
            Unregister(player);

            if (session.Npc != null)
                player.Session?.Send(RejectedItemsFrame(player, session.Npc, returned.ToArray(), Math.Max(0, refundCredits)));
            foreach ((Item item, Container page) in overflowed)
                SendGrant(player, item, page);

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot trade closed player={0} returned={1} overflow={2} reason={3}",
                player.Identity.Instance, returned.Count, overflowed.Count, reason));
        }

        static int FirstFreeOfferSlot(TradeOffer offer)
        {
            for (int slot = 0; slot < TradeOffer.Capacity; slot++)
            {
                if (!offer.Items.ContainsKey(slot))
                    return slot;
            }

            return -1;
        }

        /// <summary>
        /// Places <paramref name="item"/> in the first free main-inventory slot, the slot the client picks for an item
        /// coming out of its trade window. <paramref name="removeFrom"/> frees the item's current slot first.
        /// </summary>
        static bool TryPlaceMain(Player player, Item item, out Container page, out int slot,
            Container? removeFrom = null, int removeSlot = 0)
        {
            page = player.Inventory.Inventory;
            removeFrom?.Remove(removeSlot);
            slot = page.FindFreeSlot();
            if (slot >= 0 && page.Add(slot, item))
                return true;

            removeFrom?.Add(removeSlot, item);
            return false;
        }

        /// <summary>How the client finds the item in its window: instanced items by identity, others by template and QL.</summary>
        static KnuBotRejectedItem RejectedItem(Item item) => item.Identity.Type != IdentityType.None
            ? new KnuBotRejectedItem { LowId = (int)item.Identity.Type, HighId = item.Identity.Instance, Quality = -1 }
            : new KnuBotRejectedItem { LowId = item.LowId, HighId = item.HighId, Quality = item.Quality };

        static KnuBotRejectedItemsMessage RejectedItemsFrame(Player player, NpcCharacter npc, KnuBotRejectedItem[] items, int credits)
            => new()
            {
                Identity = player.Identity,
                Unknown1 = 2,
                Target = npc.Identity,
                Items = items,
                Credits = credits
            };

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

            if (session.Kind == TradeKind.Knubot)
            {
                EndKnubot(session, 0, reason);
                return;
            }

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
                ClientFeedback.Send(owner, "Feedback_NoRoomInInventory");
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
                if (machine.Stock.IsConfiguredSnapshot && (session.AcceptedShopTransport == null
                    || !IsCurrentAcceptedShop(initiator, machine, session.AcceptedShopTransport)))
                    return true;

                NpcCharacter? owner = machine.OwnerNpc;
                if (owner != null && (owner.IsDead || !ReferenceEquals(owner.Playfield, playfield)))
                    return true;

                Dynel anchor = owner != null ? owner : machine;
                return initiator.GetEdgeDistanceTo(anchor) > RangeCancelDistance;
            }

            if (session.Kind == TradeKind.Knubot)
            {
                NpcCharacter? npc = session.Npc;
                return npc == null || npc.IsDead || !ReferenceEquals(npc.Playfield, playfield)
                    || initiator.GetEdgeDistanceTo(npc) > RangeCancelDistance;
            }

            Player? partner = session.Partner;
            if (partner == null
                || partner.Session == null
                || partner.IsDead
                || !ReferenceEquals(partner.Playfield, playfield))
                return true;

            return initiator.GetEdgeDistanceTo(partner) > RangeCancelDistance;
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

        /// <summary>Placement marker meaning "next free slot"; the client picks the real slot.</summary>
        const int NextFreeSlotMarker = 0x6F;

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
                    Action = TemplateActionType.TradeRender,
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
                    Action = TemplateActionType.Overflow,
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

        static void TellTradeFailure(Player player, string failure)
        {
            if (failure.EndsWith("cannot be traded.", StringComparison.Ordinal))
                ClientFeedback.Send(player, "Feedback_NoDropItemCantBeTraded");
            else if (failure.EndsWith("does not have enough free inventory slots.", StringComparison.Ordinal))
                ClientFeedback.Send(player, "Feedback_NoRoomInInventory");
            else if (failure.Contains(" already has ", StringComparison.Ordinal))
                ClientFeedback.Send(player, "Feedback_AlreadyGotUniqueItem");
            else
                Tell(player, "Trade failed: " + failure);
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
