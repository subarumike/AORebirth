namespace ZoneEngine_New.Core.Trade
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>One purchasable line in a vending machine's generated stock.</summary>
    public readonly struct ShopStockSlot
    {
        public ShopStockSlot(int lowId, int highId, int quality)
        {
            LowId = lowId;
            HighId = highId;
            Quality = quality;
        }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }
    }

    /// <summary>
    /// A vending machine's rolled inventory. Stock is shared by every concurrent shopper, so the
    /// pane they all see stays consistent, and it is only re-rolled after the machine has been idle
    /// (no open trade) for <see cref="IdleRefreshMinutes"/>.
    /// </summary>
    public sealed class ShopStock
    {
        public const int IdleRefreshMinutes = 10;

        readonly List<ShopStockSlot> _slots = new();
        int _openTrades;
        long _idleSinceMs = Environment.TickCount64;

        public IReadOnlyList<ShopStockSlot> Slots => _slots.AsReadOnly();

        public bool IsGenerated { get; private set; }
        internal bool IsAcceptedSnapshot { get; private set; }

        /// <summary>An exact accepted vendor adapter installs its entire frozen stock once.</summary>
        internal void SetAcceptedSnapshot(IReadOnlyList<ShopStockSlot> slots)
        {
            ArgumentNullException.ThrowIfNull(slots);
            if (IsGenerated || _openTrades != 0 || slots.Count == 0)
                throw new InvalidOperationException("An accepted shop snapshot must be nonempty and installed before opening.");
            var copy = new List<ShopStockSlot>(slots.Count);
            foreach (var slot in slots)
            {
                if (slot.LowId <= 0 || slot.HighId <= 0 || slot.Quality <= 0)
                    throw new InvalidOperationException("An accepted shop snapshot contains an incomplete item identity.");
                copy.Add(slot);
            }
            _slots.Clear(); _slots.AddRange(copy);
            IsGenerated = true; IsAcceptedSnapshot = true;
        }

        /// <summary>Shoppers currently holding this machine's trade window open.</summary>
        public int OpenTrades => _openTrades;

        /// <summary>
        /// Rolls a fresh stock list when the machine has never been opened, or when nobody has been
        /// trading with it for <see cref="IdleRefreshMinutes"/>. A machine with a live shopper keeps
        /// its list so concurrent panes cannot disagree.
        /// </summary>
        public void EnsureFresh(
            VendingMachineDefinition definition,
            HashItemMinter minter,
            Random random)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(random);

            if (IsAcceptedSnapshot || (IsGenerated && !IsIdleExpired()))
                return;

            Generate(definition, minter, random);
        }

        public bool IsIdleExpired()
        {
            if (_openTrades > 0)
                return false;

            long idleMs = Environment.TickCount64 - _idleSinceMs;
            return idleMs >= IdleRefreshMinutes * 60L * 1000L;
        }

        public void OpenTrade()
        {
            _openTrades++;
        }

        public void CloseTrade()
        {
            if (_openTrades > 0)
                _openTrades--;

            if (_openTrades == 0)
                _idleSinceMs = Environment.TickCount64;
        }

        /// <summary>Cancels every outstanding open trade count (vendor death, machine despawn).</summary>
        public void Reset()
        {
            _openTrades = 0;
            _idleSinceMs = Environment.TickCount64;
        }

        public bool TryGetSlot(int index, out ShopStockSlot slot)
        {
            if (index < 0 || index >= _slots.Count)
            {
                slot = default;
                return false;
            }

            slot = _slots[index];
            return true;
        }

        public ShopUpdateMessage BuildShopUpdate(Identity shopIdentity)
        {
            var slots = new VendingMachineSlot[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                ShopStockSlot stock = _slots[i];
                slots[i] = new VendingMachineSlot
                {
                    ItemLowId = stock.LowId,
                    ItemHighId = stock.HighId,
                    Quality = stock.Quality
                };
            }

            return new ShopUpdateMessage
            {
                Identity = shopIdentity,
                Unknown = 1,
                VendingMachineSlots = slots
            };
        }

        void Generate(VendingMachineDefinition definition, HashItemMinter minter, Random random)
        {
            _slots.Clear();
            List<HashInstance> leaves = new();

            foreach (VendingMachineStockEntry entry in definition.UsableEntries())
            {
                if (_slots.Count >= VendingMachineDefinition.MaxSlots)
                    break;

                (int minQuality, int maxQuality) = VendingMachineDefinition.QualityBand(entry);

                if (entry.Expansion == StockExpansion.ExpandAll)
                {
                    leaves.Clear();
                    minter.CollectLeafInstances(entry.Hash, leaves);
                    for (int i = 0; i < leaves.Count; i++)
                    {
                        if (_slots.Count >= VendingMachineDefinition.MaxSlots)
                            break;

                        AddLeafSlot(minter, leaves[i], random.Next(minQuality, maxQuality + 1));
                    }

                    continue;
                }

                for (int repeat = 0; repeat < entry.Repeats; repeat++)
                {
                    if (_slots.Count >= VendingMachineDefinition.MaxSlots)
                        break;

                    if (entry.Chance < 100 && random.Next(100) >= entry.Chance)
                        continue;

                    int quality = random.Next(minQuality, maxQuality + 1);
                    if (minter.TryRollIds(entry.Hash, quality, out int lowId, out int highId, out int rolledQuality))
                        _slots.Add(new ShopStockSlot(lowId, highId, rolledQuality));
                }
            }

            IsGenerated = true;
            _idleSinceMs = Environment.TickCount64;
        }

        void AddLeafSlot(HashItemMinter minter, HashInstance leaf, int desiredQuality)
        {
            if (minter.TryRollIdsFor(leaf, desiredQuality, out int lowId, out int highId, out int quality))
                _slots.Add(new ShopStockSlot(lowId, highId, quality));
        }
    }
}
