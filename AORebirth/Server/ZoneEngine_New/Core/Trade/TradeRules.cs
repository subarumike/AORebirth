namespace ZoneEngine_New.Core.Trade
{
    using System;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Pure trade arithmetic and eligibility checks. Kept free of packets and playfield state so the
    /// pricing and flag rules can be unit tested directly.
    /// </summary>
    public static class TradeRules
    {
        /// <summary>Largest credit total the client renders correctly.</summary>
        public const int MaxCash = 999999999;

        public static int ClampCash(long cash)
        {
            if (cash < 0)
                return 0;

            return cash > MaxCash ? MaxCash : (int)cash;
        }

        public static bool IsNoDrop(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return (item.Flags & (int)ItemFlags.NoDrop) != 0;
        }

        public static bool IsUnique(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return (item.Flags & (int)ItemFlags.Unique) != 0;
        }

        /// <summary>
        /// True when <paramref name="receiver"/> already holds an item sharing the incoming item's
        /// template. Only meaningful for unique items.
        /// </summary>
        public static bool WouldDuplicateUnique(Player receiver, int lowId, int highId)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            foreach (Item held in receiver.Inventory.EnumerateHeldItems())
            {
                if (held.LowId == lowId || held.LowId == highId
                    || held.HighId == lowId || held.HighId == highId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Quality-interpolated shop value. items.dat stores <c>price</c> on the low and high
        /// template of a family; anything between them scales quadratically with quality, matching
        /// the live client's item tooltips.
        /// </summary>
        public static int ItemValue(IItemTemplateCatalog catalog, int lowId, int highId, int quality)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            bool hasLow = catalog.TryGet(lowId, out ItemTemplate low);
            bool hasHigh = catalog.TryGet(highId, out ItemTemplate high);
            if (!hasLow || !hasHigh)
                return hasLow ? PriceOf(low) : 0;

            int lowValue = PriceOf(low);
            int highValue = PriceOf(high);
            int lowQuality = low.Quality;
            int highQuality = high.Quality;

            if (lowQuality == highQuality || highValue == 0)
                return lowValue;

            double qualityDelta = quality - lowQuality;
            double qualityRange = highQuality - lowQuality;
            double scaled = lowValue
                + Math.Pow(qualityDelta, 2.0d) * (highValue - lowValue) / Math.Pow(qualityRange, 2.0d);

            return Math.Max(0, (int)Math.Round(scaled));
        }

        /// <summary>Computer Literacy above this does not change shop buy/sell prices.</summary>
        public const int MaxPricingComputerLiteracy = 3000;

        /// <summary>Skill discount steps a shopper earns from Computer Literacy.</summary>
        public static int PricingSkillSteps(Player shopper)
        {
            ArgumentNullException.ThrowIfNull(shopper);
            return PricingSkillSteps(shopper.Stats.GetOrZero(CharacterStat.ComputerLiteracy));
        }

        /// <summary>Skill discount steps from a raw Computer Literacy value, clamped to 0-3000.</summary>
        public static int PricingSkillSteps(int computerLiteracy)
        {
            int capped = Math.Clamp(computerLiteracy, 0, MaxPricingComputerLiteracy);
            return capped / 40;
        }

        /// <summary>What the shopper pays the machine for one stocked item.</summary>
        public static int BuyPrice(int itemValue, int sellModifier, int skillSteps)
        {
            int discountSteps = Math.Max(0, 100 - skillSteps);
            return Math.Max(0, (int)Math.Round(itemValue * (double)sellModifier * discountSteps / 10000.0d));
        }

        /// <summary>What the machine pays the shopper for one offered item.</summary>
        public static int SellPrice(int itemValue, int buyModifier, int skillSteps)
            => Math.Max(0, (int)Math.Floor(itemValue * (double)buyModifier * (100 + skillSteps) / 10000.0d));

        // items.dat stat 74 ("price") is CharacterStat.Value in the messaging enum.
        static int PriceOf(ItemTemplate template)
            => template != null && template.Stats.TryGetValue(CharacterStat.Value, out int price)
                ? Math.Max(0, price)
                : 0;
    }
}
