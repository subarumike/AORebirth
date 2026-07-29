namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260726-Antonio-Stacklund: Use Antonio (shop cart) → ShopUpdate
    /// VendingMachine:12E7720D (owner 78E0FC7C), template StaticInstance=248368.
    /// </summary>
    internal static class CapturedAreteAntonioStacklundVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC7C);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720D);

        internal const int CaptureVendorTemplateId = 248368;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const string DisplayName = "Antonio Stacklund";

        internal const string Evidence = "AOSharpLiveCapture/20260726-Antonio-Stacklund";

        // Capture shop-updates.csv terminal 12E7720D slots 0..15.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 248306, 248306, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 150922, 150922, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 121569, 121569, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 248340, 248340, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 248338, 248338, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 121567, 121567, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 121568, 121568, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 121570, 121570, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 121571, 121571, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 121564, 121564, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 218403, 218403, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 248339, 248339, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 218395, 218395, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 218406, 218406, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(14, 121565, 121565, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(15, 218404, 218404, 1)
            };

        internal static ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition> Stock
        {
            get
            {
                return new ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition>(CapturedStock);
            }
        }
    }
}
