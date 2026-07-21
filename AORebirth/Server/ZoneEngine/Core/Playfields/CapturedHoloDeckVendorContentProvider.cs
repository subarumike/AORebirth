namespace ZoneEngine.Core.Playfields
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Capture-backed ICC Holodeck reward terminal (PF 7001).
    /// Capture 20260719-155043: VendingMachine:12E559EA template 303217, 13 shop slots.
    /// </summary>
    internal static class CapturedHoloDeckVendorContentProvider
    {
        internal const int HoloDeckPlayfieldId = 7001;

        internal const int VendorTemplateId = 303217;

        internal const int SourceVendorInstance = 0x12E559EA;

        internal const float X = 186.9554f;

        internal const float Y = 1.209999f;

        internal const float Z = 201.3939f;

        internal const float HeadingX = 0.0f;

        internal const float HeadingY = 0.9999946f;

        internal const float HeadingZ = 0.0f;

        internal const float HeadingW = 0.003283689f;

        private static readonly ReadOnlyCollection<CapturedHoloDeckVendorStockDefinition> CapturedStock =
            new ReadOnlyCollection<CapturedHoloDeckVendorStockDefinition>(
                new[]
                    {
                        new CapturedHoloDeckVendorStockDefinition(0, 303231, 303231, 198),
                        new CapturedHoloDeckVendorStockDefinition(1, 303236, 303236, 1),
                        new CapturedHoloDeckVendorStockDefinition(2, 303225, 303225, 1),
                        new CapturedHoloDeckVendorStockDefinition(3, 303227, 303227, 1),
                        new CapturedHoloDeckVendorStockDefinition(4, 303233, 303233, 1),
                        new CapturedHoloDeckVendorStockDefinition(5, 303219, 303219, 1),
                        new CapturedHoloDeckVendorStockDefinition(6, 303220, 303220, 1),
                        new CapturedHoloDeckVendorStockDefinition(7, 303222, 303222, 1),
                        new CapturedHoloDeckVendorStockDefinition(8, 303221, 303221, 1),
                        new CapturedHoloDeckVendorStockDefinition(9, 303234, 303234, 1),
                        new CapturedHoloDeckVendorStockDefinition(10, 303232, 303232, 1),
                        new CapturedHoloDeckVendorStockDefinition(11, 303228, 303228, 1),
                        new CapturedHoloDeckVendorStockDefinition(12, 303223, 303223, 1),
                    });

        internal static ReadOnlyCollection<CapturedHoloDeckVendorStockDefinition> Stock
        {
            get
            {
                return CapturedStock;
            }
        }
    }

    internal sealed class CapturedHoloDeckVendorStockDefinition
    {
        internal CapturedHoloDeckVendorStockDefinition(int slot, int lowId, int highId, int quality)
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
