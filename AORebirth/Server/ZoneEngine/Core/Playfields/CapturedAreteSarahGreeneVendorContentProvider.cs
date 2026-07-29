namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260726-sara-greene-vendor: Use Sarah Greene (shop cart) → ShopUpdate
    /// VendingMachine:12E7720A (owner 78E0FC69), template StaticInstance=295748.
    /// </summary>
    internal static class CapturedAreteSarahGreeneVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC69);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720A);

        internal const int CaptureVendorTemplateId = 295748;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const string DisplayName = "Sarah Greene";

        internal const string Evidence = "AOSharpLiveCapture/20260726-sara-greene-vendor";

        // Capture shop-updates.csv terminal 12E7720A sequence 74 slots 0..21.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 162294, 162294, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 162293, 162293, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 162290, 162290, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 162289, 162289, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 162292, 162292, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 162291, 162291, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 248273, 248273, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 248269, 248269, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 248277, 248277, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 248271, 248271, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 248275, 248275, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 234050, 234051, 11),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 234061, 234062, 2),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 234061, 234062, 6),
                new CapturedAreteAlexAreaVendorStockDefinition(14, 234065, 234066, 13),
                new CapturedAreteAlexAreaVendorStockDefinition(15, 234066, 234066, 15),
                new CapturedAreteAlexAreaVendorStockDefinition(16, 234057, 234058, 4),
                new CapturedAreteAlexAreaVendorStockDefinition(17, 234057, 234058, 5),
                new CapturedAreteAlexAreaVendorStockDefinition(18, 234059, 234060, 8),
                new CapturedAreteAlexAreaVendorStockDefinition(19, 234060, 234060, 15),
                new CapturedAreteAlexAreaVendorStockDefinition(20, 234063, 234064, 11),
                new CapturedAreteAlexAreaVendorStockDefinition(21, 234063, 234064, 13)
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
