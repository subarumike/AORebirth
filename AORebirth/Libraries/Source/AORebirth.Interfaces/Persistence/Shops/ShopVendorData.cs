namespace AORebirth.Interfaces.Persistence.Shops
{
    using System.Collections.Generic;

    /// <summary>One placed vendor joined to its vendor template and active eligible stock rows.</summary>
    public sealed class ShopVendorData
    {
        public ShopVendorData(
            int vendorId,
            int playfieldId,
            string vendorName,
            int placedTemplateId,
            string vendorTemplateHash,
            int vendorTemplateRowId,
            int vendorItemTemplateId,
            string stockGroupHash,
            int minimumQuality,
            int maximumQuality,
            float buyModifier,
            float sellModifier,
            int pricingSkill,
            IList<ShopStockData> stock)
        {
            VendorId = vendorId;
            PlayfieldId = playfieldId;
            VendorName = vendorName;
            PlacedTemplateId = placedTemplateId;
            VendorTemplateHash = vendorTemplateHash;
            VendorTemplateRowId = vendorTemplateRowId;
            VendorItemTemplateId = vendorItemTemplateId;
            StockGroupHash = stockGroupHash;
            MinimumQuality = minimumQuality;
            MaximumQuality = maximumQuality;
            BuyModifier = buyModifier;
            SellModifier = sellModifier;
            PricingSkill = pricingSkill;
            Stock = stock;
        }

        public int VendorId { get; }
        public int PlayfieldId { get; }
        public string VendorName { get; }
        public int PlacedTemplateId { get; }
        public string VendorTemplateHash { get; }
        public int VendorTemplateRowId { get; }
        public int VendorItemTemplateId { get; }
        public string StockGroupHash { get; }
        public int MinimumQuality { get; }
        public int MaximumQuality { get; }
        public float BuyModifier { get; }
        public float SellModifier { get; }
        public int PricingSkill { get; }
        public IList<ShopStockData> Stock { get; }
    }
}
