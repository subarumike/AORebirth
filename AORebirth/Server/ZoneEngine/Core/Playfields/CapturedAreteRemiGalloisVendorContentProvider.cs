namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260727-213512: Use Remi Gallois (shop cart) → ShopUpdate
    /// VendingMachine:12E7720C (owner 78E0FC75). No VendingMachineFullUpdate in capture;
    /// runtime uses fallback template 99634 (same as Sarah/Antonio when StaticInstance missing).
    /// </summary>
    internal static class CapturedAreteRemiGalloisVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC75);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720C);

        internal const int CaptureVendorTemplateId = 99634;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const string DisplayName = "Remi Gallois";

        internal const string Evidence = "AOSharpLiveCapture/20260727-213512";

        // Capture shop-updates.csv terminal 12E7720C sequence 66 slots 0..37.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 125219, 125219, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 21605, 21605, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 21609, 21609, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 21601, 21601, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 126757, 126757, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 21613, 21613, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 295765, 295765, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 160224, 160225, 2),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 160224, 160225, 5),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 152154, 152155, 4),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 152154, 152155, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 122924, 122924, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 122924, 122925, 7),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 121969, 121969, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(14, 121969, 121970, 6),
                new CapturedAreteAlexAreaVendorStockDefinition(15, 122121, 122122, 2),
                new CapturedAreteAlexAreaVendorStockDefinition(16, 122121, 122122, 7),
                new CapturedAreteAlexAreaVendorStockDefinition(17, 122425, 122425, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(18, 122425, 122426, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(19, 123267, 123267, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(20, 125043, 125043, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(21, 125043, 125044, 8),
                new CapturedAreteAlexAreaVendorStockDefinition(22, 122216, 122217, 3),
                new CapturedAreteAlexAreaVendorStockDefinition(23, 122216, 122217, 5),
                new CapturedAreteAlexAreaVendorStockDefinition(24, 124910, 124911, 2),
                new CapturedAreteAlexAreaVendorStockDefinition(25, 124910, 124911, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(26, 209283, 209284, 5),
                new CapturedAreteAlexAreaVendorStockDefinition(27, 209283, 209284, 8),
                new CapturedAreteAlexAreaVendorStockDefinition(28, 152339, 152340, 6),
                new CapturedAreteAlexAreaVendorStockDefinition(29, 152339, 152340, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(30, 124276, 124277, 4),
                new CapturedAreteAlexAreaVendorStockDefinition(31, 124276, 124277, 7),
                new CapturedAreteAlexAreaVendorStockDefinition(32, 209269, 209270, 4),
                new CapturedAreteAlexAreaVendorStockDefinition(33, 144101, 144102, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(34, 142836, 142837, 9),
                new CapturedAreteAlexAreaVendorStockDefinition(35, 142837, 142837, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(36, 160288, 160288, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(37, 160288, 160289, 9)
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
