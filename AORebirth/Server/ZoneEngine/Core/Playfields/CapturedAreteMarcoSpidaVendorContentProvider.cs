namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260721-nanoprogramsvendor: Marco Spida Use → ShopUpdate VendingMachine:12E77212.
    /// Capture 20260721-nano-enforcer-arete: buy slot4 crystal 248259 → Use unpacks Enforcer nanos.
    /// </summary>
    internal static class CapturedAreteMarcoSpidaVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC81);

        internal const int SourceVendorInstance = unchecked((int)0x12E77212);

        internal const int CaptureVendorTemplateId = 248371;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const int EnforcerNanoCrystalItemId = 248259;

        internal const int DoctorNanoCrystalItemId = 248258;

        // Capture 20260730-212921: Bureaucrat Nanoprogram Container open.
        internal const int BureaucratNanoCrystalItemId = 248257;

        // Capture 20260730-212921: tip complete grants Overflow 223373 QL25
        // Nano Crystal (Composite Attribute Boost).
        internal const int BuyNanoTipRewardItemId = 223373;

        internal const int BuyNanoTipRewardQuality = 25;

        internal const string DisplayName = "Marco Spida";

        internal const string Evidence = "AOSharpLiveCapture/20260721-nanoprogramsvendor";

        internal const string EnforcerPackageEvidence = "AOSharpLiveCapture/20260721-nano-enforcer-arete";

        internal const string DoctorPackageEvidence = "AOSharpLiveCapture/20260721-nanoprogramsvendor";

        // Capture shop-updates.csv terminal 12E77212 slots 0..13.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 248255, 248255, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 248256, 248256, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 248257, 248257, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 248258, 248258, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 248259, 248259, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 248260, 248260, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 248261, 248261, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 300892, 300892, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 248262, 248262, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 248263, 248263, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 248264, 248264, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 300893, 300893, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 248265, 248265, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 248266, 248266, 1)
            };

        // Capture 20260721-nano-enforcer-arete events.log TemplateAction Overflow after Use Inventory crystal.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent EnforcerPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                EnforcerNanoCrystalItemId,
                "Enforcer",
                EnforcerPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43379, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(55762, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(49821, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(100202, 17)
                });

        // Capture 20260721-nanoprogramsvendor: AddItem slot3=248258 → Use unpacks Doctor nanos.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent DoctorPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                DoctorNanoCrystalItemId,
                "Doctor",
                DoctorPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43384, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(42423, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(99589, 7),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43960, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43978, 4)
                });

        // Capture 20260730-212921: Use 248257 → Overflow Winter's Bite, Momentary Daze,
        // Distracted Gaze QL10, Temporary Glamor QL20, Limited Worker-Droid, Faithful Worker-Droid QL4.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent BureaucratPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                BureaucratNanoCrystalItemId,
                "Bureaucrat",
                "AOSharpLiveCapture/20260730-212921",
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(29625, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43381, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(30110, 10),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(99212, 20),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(46430, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(46438, 4)
                });

        private static readonly CapturedAreteMarcoSpidaNanoPackageContent[] CapturedPackages =
            {
                EnforcerPackage,
                DoctorPackage,
                BureaucratPackage
            };

        internal static ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition> Stock
        {
            get
            {
                return new ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition>(CapturedStock);
            }
        }

        internal static bool IsCapturedNanoCrystalItemId(int itemId)
        {
            for (int i = 0; i < CapturedStock.Length; i++)
            {
                if (CapturedStock[i].LowId == itemId || CapturedStock[i].HighId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetNanoPackage(int crystalItemId, out CapturedAreteMarcoSpidaNanoPackageContent package)
        {
            for (int i = 0; i < CapturedPackages.Length; i++)
            {
                if (CapturedPackages[i].CrystalItemId == crystalItemId)
                {
                    package = CapturedPackages[i];
                    return true;
                }
            }

            package = null;
            return false;
        }
    }

    internal sealed class CapturedAreteMarcoSpidaNanoPackageContent
    {
        internal CapturedAreteMarcoSpidaNanoPackageContent(
            int crystalItemId,
            string displayName,
            string evidence,
            CapturedAreteMarcoSpidaNanoPackageContentEntry[] contents)
        {
            this.CrystalItemId = crystalItemId;
            this.DisplayName = displayName;
            this.Evidence = evidence;
            this.Contents = contents ?? new CapturedAreteMarcoSpidaNanoPackageContentEntry[0];
        }

        internal int CrystalItemId { get; private set; }

        internal string DisplayName { get; private set; }

        internal string Evidence { get; private set; }

        internal CapturedAreteMarcoSpidaNanoPackageContentEntry[] Contents { get; private set; }
    }

    internal sealed class CapturedAreteMarcoSpidaNanoPackageContentEntry
    {
        internal CapturedAreteMarcoSpidaNanoPackageContentEntry(int itemId, int quality)
        {
            this.ItemId = itemId;
            this.Quality = quality;
        }

        internal int ItemId { get; private set; }

        internal int Quality { get; private set; }
    }
}
