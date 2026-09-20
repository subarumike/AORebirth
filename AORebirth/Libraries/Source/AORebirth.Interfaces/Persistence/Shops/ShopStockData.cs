namespace AORebirth.Interfaces.Persistence.Shops
{
    /// <summary>One active stock row whose quality range overlaps its vendor template.</summary>
    public sealed class ShopStockData
    {
        public ShopStockData(
            int stockRowId,
            string stockGroupHash,
            int lowId,
            int highId,
            int minimumQuality,
            int maximumQuality,
            int effectiveMinimumQuality,
            int effectiveMaximumQuality,
            int multipleCount)
        {
            StockRowId = stockRowId;
            StockGroupHash = stockGroupHash;
            LowId = lowId;
            HighId = highId;
            MinimumQuality = minimumQuality;
            MaximumQuality = maximumQuality;
            EffectiveMinimumQuality = effectiveMinimumQuality;
            EffectiveMaximumQuality = effectiveMaximumQuality;
            MultipleCount = multipleCount;
        }

        public int StockRowId { get; }
        public string StockGroupHash { get; }
        public int LowId { get; }
        public int HighId { get; }
        public int MinimumQuality { get; }
        public int MaximumQuality { get; }
        public int EffectiveMinimumQuality { get; }
        public int EffectiveMaximumQuality { get; }
        public int MultipleCount { get; }
    }
}
