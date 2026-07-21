namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260721-loralei: Use Lorelei → ShopUpdate VendingMachine:12E7720B (owner 78E0FC6B).
    /// Slot 31 = Tasty Peanut Butter Cookie 297370 (Lost Pet lure).
    /// </summary>
    internal static class CapturedAreteLoreleiVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC6B);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720B);

        internal const int CaptureVendorTemplateId = 297371;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const int PeanutButterCookieItemId = 297370;

        internal const string DisplayName = "Lorelei the Bartender";

        internal const string Evidence = "AOSharpLiveCapture/20260721-loralei";

        // Capture shop-updates.csv terminal 12E7720B sequence 536 slots 0..37.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 130621, 130621, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 130622, 130622, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 130620, 130620, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 130618, 130618, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 282068, 282068, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 130593, 130593, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 130588, 130588, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 130605, 130605, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 130610, 130610, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 130612, 130612, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 130611, 130611, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 282067, 282067, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 282066, 282066, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 282065, 282065, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(14, 130599, 130599, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(15, 130617, 130617, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(16, 130606, 130606, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(17, 130619, 130619, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(18, 130602, 130602, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(19, 282070, 282070, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(20, 130604, 130604, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(21, 130595, 130595, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(22, 130591, 130591, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(23, 130587, 130587, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(24, 130592, 130592, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(25, 130590, 130590, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(26, 130596, 130596, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(27, 130608, 130608, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(28, 130598, 130598, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(29, 130597, 130597, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(30, 130609, 130609, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(31, 297370, 297370, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(32, 130600, 130600, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(33, 130594, 130594, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(34, 130589, 130589, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(35, 282075, 282075, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(36, 130607, 130607, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(37, 282069, 282069, 1)
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
