namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260801-burger-vendor: GenericCmd Use Barry (shop cart)
    /// → ShopUpdate VendingMachine:12E7720E (owner 78E0FC7D, StaticInstance 121036).
    /// itemnames DB: slot2 130623 = Bronto Burger; slot0 130621 = A Beer Jug.
    /// </summary>
    internal static class CapturedAreteBarryFoodVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC7D);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720E);

        internal const int CaptureVendorTemplateId = 121036;

        internal const int RuntimeVendorTemplateFallbackId = 121036;

        internal const int BrontoBurgerItemId = 130623;

        internal const string DisplayName = "Barry the Food Vendor";

        internal const string Evidence = "AOSharpLiveCapture/20260801-burger-vendor";

        // Capture shop-updates.csv terminal 12E7720E sequence 586 slots 0..9.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 130621, 130621, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 130593, 130593, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 130623, 130623, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 130624, 130624, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 130581, 130581, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 130612, 130612, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 130625, 130625, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 130606, 130606, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 130602, 130602, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 130603, 130603, 1)
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
