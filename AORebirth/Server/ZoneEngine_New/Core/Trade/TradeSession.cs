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
        Shop = 1,

        /// <summary>A player handing items to a Knubot NPC ("Give Item" window).</summary>
        Knubot = 2
    }

    /// <summary>One side of a trade: the items that character has put on the table plus their credits.</summary>
    public sealed class TradeOffer
    {
        public const int Capacity = 20;

        readonly Dictionary<int, Item> _items = new();

        /// <param name="slots">The window's slots, 0 to <see cref="Capacity"/>: a Knubot window has only as many as its
        /// largest trade needs (0 for a credits-only window).</param>
        public TradeOffer(int slots = Capacity)
        {
            Slots = Math.Clamp(slots, 0, Capacity);
        }

        /// <summary>How many items this side of the window holds.</summary>
        public int Slots { get; }

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

            for (int slot = 0; slot < Slots; slot++)
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
            if (slot < 0 || slot >= Slots || _items.ContainsKey(slot))
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
    /// for the stock indices they intend to buy; Knubot trades use <see cref="Initiator"/>'s offer for
    /// what the player is giving <see cref="Npc"/>.
    /// </summary>
    public sealed class TradeSession
    {
        readonly List<int> _shopPicks = new();

        public TradeSession(
            Identity bagIdentity,
            TradeKind kind,
            Player initiator,
            Player? partner,
            VendingMachine? machine,
            NpcCharacter? npc = null,
            int initiatorSlots = TradeOffer.Capacity)
        {
            ArgumentNullException.ThrowIfNull(initiator);

            BagIdentity = bagIdentity;
            Kind = kind;
            Initiator = initiator;
            Partner = partner;
            Machine = machine;
            Npc = npc;
            AcceptedShopTransport = machine?.Stock.IsConfiguredSnapshot == true ? initiator.Session : null;
            InitiatorOffer = new TradeOffer(initiatorSlots);
            PartnerOffer = new TradeOffer();
        }

        public Identity BagIdentity { get; }

        public TradeKind Kind { get; }

        public Player Initiator { get; }

        /// <summary>Other player for <see cref="TradeKind.Player"/>; null for shop trades.</summary>
        public Player? Partner { get; }

        /// <summary>Machine for <see cref="TradeKind.Shop"/>; null for player trades.</summary>
        public VendingMachine? Machine { get; }

        /// <summary>NPC for <see cref="TradeKind.Knubot"/>; null otherwise.</summary>
        public NpcCharacter? Npc { get; }
        internal ZoneEngine_New.Core.Network.IZoneSession? AcceptedShopTransport { get; }

        public TradeOffer InitiatorOffer { get; }
        internal bool UsesAuthoredTurnIn { get; init; }

        public TradeOffer PartnerOffer { get; }

        /// <summary>Stock indices the shopper has moved into the buy pane, in pane order.</summary>
        public IReadOnlyList<int> ShopPicks => _shopPicks;

        /// <summary>Set once the commit begins so a duplicate Confirm cannot run it twice.</summary>
        public bool Committing { get; set; }

        /// <summary>The dynel the window is anchored to, used for the range check.</summary>
        public Dynel? Anchor => Kind switch
        {
            TradeKind.Shop => Machine,
            TradeKind.Knubot => Npc,
            _ => Partner
        };

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

        /// <summary>
        /// Queues one pick of a stock line. The buy pane holds <see cref="TradeOffer.Capacity"/> items; a stackable line
        /// (ammo) picked again is the same stack, so it takes one item of that however often it is picked, while every
        /// other pick is an item of its own. -1 when the pane is full.
        /// </summary>
        public int AddShopPick(int stockIndex, bool stackable = false)
        {
            if (stackable)
                _stackableShopLines.Add(stockIndex);
            bool newEntry = !stackable || !_shopPicks.Contains(stockIndex);
            if (newEntry && CountShopPaneItems() >= TradeOffer.Capacity)
                return -1;

            _shopPicks.Add(stockIndex);
            return _shopPicks.Count - 1;
        }

        readonly HashSet<int> _stackableShopLines = new();

        /// <summary>Items in the buy pane as the capacity counts them: each stackable line once, every other pick once.</summary>
        int CountShopPaneItems()
        {
            int items = 0;
            var stackableSeen = new HashSet<int>();
            for (int i = 0; i < _shopPicks.Count; i++)
            {
                int index = _shopPicks[i];
                if (_stackableShopLines.Contains(index))
                {
                    if (stackableSeen.Add(index))
                        items++;
                }
                else
                {
                    items++;
                }
            }

            return items;
        }

        /// <summary>
        /// Removes one queued pick of <paramref name="stockIndex"/>. The client names a buy-pane entry by the same stock
        /// identity it added (container 111:stock index, also when the slot was queued several times), not by its pane
        /// position. Queued copies of one stock slot are identical, so the last one goes.
        /// </summary>
        public bool RemoveShopPick(int stockIndex)
        {
            int at = _shopPicks.LastIndexOf(stockIndex);
            if (at < 0)
                return false;

            _shopPicks.RemoveAt(at);
            return true;
        }

        public void ClearShopPicks() => _shopPicks.Clear();
    }
}
