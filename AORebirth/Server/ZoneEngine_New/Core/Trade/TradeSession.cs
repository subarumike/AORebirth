namespace ZoneEngine_New.Core.Trade
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    public enum TradeKind
    {
        /// <summary>Two players swapping offers.</summary>
        Player = 0,

        /// <summary>A player buying from / selling to a vending machine.</summary>
        Shop = 1
    }

    /// <summary>One side of a trade: the items that character has put on the table plus their credits.</summary>
    public sealed class TradeOffer
    {
        public const int Capacity = 6;

        readonly Dictionary<int, Item> _items = new();

        public IReadOnlyDictionary<int, Item> Items => _items;

        public int Credits { get; set; }

        /// <summary>Set by TradeAction.Confirm — this side is happy with the offers.</summary>
        public bool Accepted { get; set; }

        /// <summary>Set by TradeAction.Accept/End — this side dismissed the final confirm dialog.</summary>
        public bool Ended { get; set; }

        public int Count => _items.Count;

        public int Add(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);

            for (int slot = 0; slot < Capacity; slot++)
            {
                if (_items.ContainsKey(slot))
                    continue;

                _items[slot] = item;
                item.Locked = true;
                return slot;
            }

            return -1;
        }

        public bool TryRestore(int slot, Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (slot < 0 || slot >= Capacity || _items.ContainsKey(slot))
                return false;

            _items[slot] = item;
            item.Locked = true;
            return true;
        }

        public Item? Remove(int slot)
        {
            if (!_items.Remove(slot, out Item? item))
                return null;

            item.Locked = false;
            return item;
        }

        public Item[] DrainAll()
        {
            var drained = new Item[_items.Count];
            _items.Values.CopyTo(drained, 0);
            _items.Clear();
            for (int i = 0; i < drained.Length; i++)
                drained[i].Locked = false;

            return drained;
        }
    }

    /// <summary>
    /// A live trade window. Player trades hold two <see cref="TradeOffer"/>s; shop trades use
    /// <see cref="Initiator"/>'s offer for what the player is selling and <see cref="ShopPicks"/>
    /// for the stock indices they intend to buy.
    /// </summary>
    public sealed class TradeSession
    {
        readonly List<int> _shopPicks = new();

        public TradeSession(
            Identity bagIdentity,
            TradeKind kind,
            Player initiator,
            Player? partner,
            VendingMachine? machine)
        {
            ArgumentNullException.ThrowIfNull(initiator);

            BagIdentity = bagIdentity;
            Kind = kind;
            Initiator = initiator;
            Partner = partner;
            Machine = machine;
            InitiatorOffer = new TradeOffer();
            PartnerOffer = new TradeOffer();
        }

        public Identity BagIdentity { get; }

        public TradeKind Kind { get; }

        public Player Initiator { get; }

        /// <summary>Other player for <see cref="TradeKind.Player"/>; null for shop trades.</summary>
        public Player? Partner { get; }

        /// <summary>Machine for <see cref="TradeKind.Shop"/>; null for player trades.</summary>
        public VendingMachine? Machine { get; }

        public TradeOffer InitiatorOffer { get; }

        public TradeOffer PartnerOffer { get; }

        /// <summary>Stock indices the shopper has moved into the buy pane, in pane order.</summary>
        public IReadOnlyList<int> ShopPicks => _shopPicks;

        /// <summary>Set once the commit begins so a duplicate Confirm cannot run it twice.</summary>
        public bool Committing { get; set; }

        /// <summary>The dynel the window is anchored to, used for the range check.</summary>
        public Dynel? Anchor => Kind == TradeKind.Shop ? (Dynel?)Machine : Partner;

        public bool Involves(Player player)
            => ReferenceEquals(Initiator, player) || ReferenceEquals(Partner, player);

        public TradeOffer OfferFor(Player player)
            => ReferenceEquals(Partner, player) ? PartnerOffer : InitiatorOffer;

        public Player? Other(Player player)
        {
            if (Kind != TradeKind.Player)
                return null;

            return ReferenceEquals(Initiator, player) ? Partner : Initiator;
        }

        public bool BothAccepted => InitiatorOffer.Accepted && (Kind == TradeKind.Shop || PartnerOffer.Accepted);

        public bool BothEnded => InitiatorOffer.Ended && (Kind == TradeKind.Shop || PartnerOffer.Ended);

        public void ClearAcceptances()
        {
            InitiatorOffer.Accepted = false;
            InitiatorOffer.Ended = false;
            PartnerOffer.Accepted = false;
            PartnerOffer.Ended = false;
        }

        public int AddShopPick(int stockIndex)
        {
            if (_shopPicks.Count >= TradeOffer.Capacity)
                return -1;

            _shopPicks.Add(stockIndex);
            return _shopPicks.Count - 1;
        }

        /// <summary>
        /// Removes the pane entry at <paramref name="paneSlot"/>. The client addresses buy-pane
        /// entries by their position, so later picks shift down.
        /// </summary>
        public bool RemoveShopPick(int paneSlot)
        {
            if (paneSlot < 0 || paneSlot >= _shopPicks.Count)
                return false;

            _shopPicks.RemoveAt(paneSlot);
            return true;
        }

        public void ClearShopPicks() => _shopPicks.Clear();
    }
}
