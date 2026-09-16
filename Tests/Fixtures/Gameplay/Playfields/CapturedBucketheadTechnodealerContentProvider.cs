namespace ZoneEngine.Core.Playfields
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Capture 20260723-061619: CastNano 300439 SpawnMonster2("BKTH",220,600)
    /// → SimpleChar Buckethead Technodealer + linked VendingMachine template 99566 (46 slots).
    /// </summary>
    internal static class CapturedBucketheadTechnodealerContentProvider
    {
        internal const string MobHash = "BKTH";

        internal const int SummonNanoId = 300439;

        // Crystal item 300440 uploads nano 300439 (items.dat UploadNano).
        // Characters may incorrectly have 300440 stored as uploaded nano id.
        internal const int PremiumCrystalItemId = 300440;

        internal const int VendorTemplateFallbackId = 99634;

        internal const string DisplayName = "Buckethead Technodealer";

        internal const int VendorTemplateId = 99566;

        internal const int MonsterData = 43352;

        internal const int Level = 220;

        internal const int Health = 101861;

        internal const int MonsterScale = 50;

        internal const int VisualFlags = 31;

        internal const int RunSpeed = 749;

        // SCFU CharacterFlags from capture.
        internal const int CharacterFlags = 271061505;

        internal const string Evidence = "AOSharpLiveCapture/20260723-114826";

        private static readonly ReadOnlyCollection<CapturedBucketheadTechnodealerStockDefinition> CapturedStock =
            new ReadOnlyCollection<CapturedBucketheadTechnodealerStockDefinition>(
                new[]
                {
                    Row(0, 87814, 87814, 200),
                    Row(1, 266845, 266845, 1),
                    Row(2, 125219, 125219, 1),
                    Row(3, 273501, 273501, 1),
                    Row(4, 273496, 273496, 1),
                    Row(5, 273502, 273502, 1),
                    Row(6, 273503, 273503, 1),
                    Row(7, 273504, 273504, 1),
                    Row(8, 273500, 273500, 1),
                    Row(9, 21605, 21605, 1),
                    Row(10, 21609, 21609, 1),
                    Row(11, 21601, 21601, 1),
                    Row(12, 126757, 126757, 1),
                    Row(13, 21613, 21613, 1),
                    Row(14, 87810, 87814, 25),
                    Row(15, 87810, 87814, 50),
                    Row(16, 87810, 87814, 75),
                    Row(17, 87810, 87814, 100),
                    Row(18, 87810, 87814, 125),
                    Row(19, 87810, 87814, 150),
                    Row(20, 291082, 291083, 25),
                    Row(21, 291082, 291083, 50),
                    Row(22, 291082, 291083, 75),
                    Row(23, 291083, 291083, 100),
                    Row(24, 291083, 291084, 125),
                    Row(25, 291083, 291084, 150),
                    Row(26, 291084, 291084, 200),
                    Row(27, 291084, 293296, 225),
                    Row(28, 291084, 293296, 250),
                    Row(29, 291084, 293296, 275),
                    Row(30, 293296, 293296, 300),
                    Row(31, 291043, 291044, 25),
                    Row(32, 291043, 291044, 50),
                    Row(33, 291043, 291044, 75),
                    Row(34, 291043, 291044, 100),
                    Row(35, 291043, 291044, 125),
                    Row(36, 291043, 291044, 150),
                    Row(37, 291044, 291044, 200),
                    Row(38, 291044, 291045, 225),
                    Row(39, 291044, 291045, 250),
                    Row(40, 291044, 291045, 275),
                    Row(41, 291044, 291045, 300),
                    Row(42, 95577, 95577, 1),
                    Row(43, 28564, 28564, 1),
                    Row(44, 150922, 150922, 10),
                    Row(45, 99228, 99228, 1)
                });

        internal static ReadOnlyCollection<CapturedBucketheadTechnodealerStockDefinition> Stock
        {
            get { return CapturedStock; }
        }

        private static CapturedBucketheadTechnodealerStockDefinition Row(
            int slot,
            int lowId,
            int highId,
            int quality)
        {
            return new CapturedBucketheadTechnodealerStockDefinition(slot, lowId, highId, quality);
        }
    }

    internal sealed class CapturedBucketheadTechnodealerStockDefinition
    {
        internal CapturedBucketheadTechnodealerStockDefinition(int slot, int lowId, int highId, int quality)
        {
            this.Slot = slot;
            this.LowId = lowId;
            this.HighId = highId;
            this.Quality = quality;
        }

        internal int Slot { get; private set; }

        internal int LowId { get; private set; }

        internal int HighId { get; private set; }

        internal int Quality { get; private set; }
    }
}
