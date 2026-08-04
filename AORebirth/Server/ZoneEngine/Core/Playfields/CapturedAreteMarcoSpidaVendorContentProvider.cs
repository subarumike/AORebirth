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

        // Capture 20260801-191821: Keeper Nanoprogram Container open (shop slot 7).
        internal const int KeeperNanoCrystalItemId = 300892;

        // Capture 20260802-MP-nano-package: Metaphysicist Nanoprogram Container (shop slot 9).
        internal const int MetaphysicistNanoCrystalItemId = 248263;

        // Capture 20260802-advy-nano-package: Adventurer Nanoprogram Container (shop slot 0).
        internal const int AdventurerNanoCrystalItemId = 248255;

        // Capture 20260802-fixer-nano-pack: Fixer Nanoprogram Container (shop slot 6).
        internal const int FixerNanoCrystalItemId = 248261;

        // Capture 20260802-shade-nano-pack: Shade Nanoprogram Container (shop slot 11).
        internal const int ShadeNanoCrystalItemId = 300893;

        // Capture 20260804-ma-nano-pack: Martial Artist Nanoprogram Container (shop slot 8).
        internal const int MartialArtistNanoCrystalItemId = 248262;

        // Capture 20260802-NT-nano-pack: Nanotechnician Nanoprogram Container (shop slot 10).
        internal const int NanotechnicianNanoCrystalItemId = 248264;

        // Capture 20260730-212921: tip complete grants Overflow 223373 QL25
        // Nano Crystal (Composite Attribute Boost).
        internal const int BuyNanoTipRewardItemId = 223373;

        internal const int BuyNanoTipRewardQuality = 25;

        internal const string DisplayName = "Marco Spida";

        internal const string Evidence = "AOSharpLiveCapture/20260721-nanoprogramsvendor";

        internal const string EnforcerPackageEvidence = "AOSharpLiveCapture/20260721-nano-enforcer-arete";

        internal const string DoctorPackageEvidence = "AOSharpLiveCapture/20260721-nanoprogramsvendor";

        internal const string KeeperPackageEvidence = "AOSharpLiveCapture/20260801-191821";

        internal const string MetaphysicistPackageEvidence = "AOSharpLiveCapture/20260802-MP-nano-package";

        internal const string AdventurerPackageEvidence = "AOSharpLiveCapture/20260802-advy-nano-package";

        internal const string FixerPackageEvidence = "AOSharpLiveCapture/20260802-fixer-nano-pack";

        internal const string ShadePackageEvidence = "AOSharpLiveCapture/20260802-shade-nano-pack";

        internal const string MartialArtistPackageEvidence = "AOSharpLiveCapture/20260804-ma-nano-pack";

        internal const string NanotechnicianPackageEvidence = "AOSharpLiveCapture/20260802-NT-nano-pack";

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

        // Capture 20260801-191821: Use Inventory:46 crystal 300892 → Overflow
        // 210298 QL1, 210590 QL2 (Adaptive Tone of Clarity), 210612 QL8 (Vengeance of the Loyal),
        // then TemplateAction Unknown2=3 delete crystal, then tip reward 223373 QL25.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent KeeperPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                KeeperNanoCrystalItemId,
                "Keeper",
                KeeperPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(210298, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(210590, 2),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(210612, 8)
                });

        // Capture 20260802-MP-nano-package: Use Inventory:004F crystal 248263 → Overflow
        // 29193 QL1, 99133 QL7, 156142 QL4, 125751 QL17, then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent MetaphysicistPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                MetaphysicistNanoCrystalItemId,
                "Metaphysicist",
                MetaphysicistPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(29193, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(99133, 7),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(156142, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(125751, 17)
                });

        // Capture 20260802-advy-nano-package: Use Inventory:0046 crystal 248255 → Overflow
        // 161504 QL1, 28751 QL4, 28742 QL1, then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent AdventurerPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                AdventurerNanoCrystalItemId,
                "Adventurer",
                AdventurerPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(161504, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(28751, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(28742, 1)
                });

        // Capture 20260802-fixer-nano-pack: Use Inventory:0046 crystal 248261 → Overflow
        // 43380 QL1, 162715 QL7, 85273 QL4, 85279 QL7, 297633 QL10, then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent FixerPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                FixerNanoCrystalItemId,
                "Fixer",
                FixerPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43380, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(162715, 7),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(85273, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(85279, 7),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(297633, 10)
                });

        // Capture 20260802-shade-nano-pack: Use Inventory:0049 crystal 300893 → Overflow
        // 210354 QL4, 297333 QL1, 211155 QL1, then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent ShadePackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                ShadeNanoCrystalItemId,
                "Shade",
                ShadePackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(210354, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(297333, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(211155, 1)
                });


        // Capture 20260804-ma-nano-pack: Use Inventory:0045 crystal 248262 → Overflow
        // 43382 QL1, 81848 QL10, 82071 QL10, 28952 QL4, then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent MartialArtistPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                MartialArtistNanoCrystalItemId,
                "Martial Artist",
                MartialArtistPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(43382, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(81848, 10),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(82071, 10),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(28952, 4)
                });

        // Capture 20260802-NT-nano-pack: Trade AddItem slot10 → Use Inventory:0048 crystal 248264 → Overflow
        // 42111 QL1, 28834 QL4, 83975 QL4, 90409 QL1, 45990 QL4, 45965 QL1,
        // then FormatFeedback unique already-owned (Ice Flechette, Worn Cyberdeck; no Overflow IDs),
        // then TemplateAction Unknown2=3 delete.
        private static readonly CapturedAreteMarcoSpidaNanoPackageContent NanotechnicianPackage =
            new CapturedAreteMarcoSpidaNanoPackageContent(
                NanotechnicianNanoCrystalItemId,
                "Nanotechnician",
                NanotechnicianPackageEvidence,
                new[]
                {
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(42111, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(28834, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(83975, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(90409, 1),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(45990, 4),
                    new CapturedAreteMarcoSpidaNanoPackageContentEntry(45965, 1)
                });

        private static readonly CapturedAreteMarcoSpidaNanoPackageContent[] CapturedPackages =
            {
                EnforcerPackage,
                DoctorPackage,
                BureaucratPackage,
                KeeperPackage,
                MetaphysicistPackage,
                AdventurerPackage,
                FixerPackage,
                ShadePackage,
                MartialArtistPackage,
                NanotechnicianPackage
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
