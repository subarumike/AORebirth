using System.Collections.ObjectModel;

namespace ZoneEngine.Core.Playfields;

internal static class CapturedAreteAlexAreaVendorContentProvider
{
	internal const int AreteLandingPlayfieldId = 6553;

	internal const int RuntimeVendorTemplateFallbackId = 99634;

	private static readonly CapturedAreteAlexAreaVendorDefinition[] VendorDefinitions = new CapturedAreteAlexAreaVendorDefinition[2]
	{
		new CapturedAreteAlexAreaVendorDefinition("Junk Shop", 317157897, 297281, 3527.349f, 5.189701f, 858.1189f, new CapturedAreteAlexAreaVendorStockDefinition[10]
		{
			new CapturedAreteAlexAreaVendorStockDefinition(0, 156020, 156021, 5),
			new CapturedAreteAlexAreaVendorStockDefinition(1, 247123, 247123, 100),
			new CapturedAreteAlexAreaVendorStockDefinition(2, 164557, 164558, 15),
			new CapturedAreteAlexAreaVendorStockDefinition(3, 156024, 156025, 5),
			new CapturedAreteAlexAreaVendorStockDefinition(4, 161699, 161699, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(5, 150922, 150922, 10),
			new CapturedAreteAlexAreaVendorStockDefinition(6, 156016, 156017, 5),
			new CapturedAreteAlexAreaVendorStockDefinition(7, 156016, 156017, 10),
			new CapturedAreteAlexAreaVendorStockDefinition(8, 156016, 156017, 15),
			new CapturedAreteAlexAreaVendorStockDefinition(9, 247110, 247110, 100)
		}),
		new CapturedAreteAlexAreaVendorDefinition("ICC Ammunition", 317157893, 297459, 3527.521f, 5.109988f, 863.9226f, new CapturedAreteAlexAreaVendorStockDefinition[13]
		{
			new CapturedAreteAlexAreaVendorStockDefinition(0, 266845, 266845, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(1, 125219, 125219, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(2, 273501, 273501, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(3, 273496, 273496, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(4, 273502, 273502, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(5, 273503, 273503, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(6, 273504, 273504, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(7, 273500, 273500, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(8, 21605, 21605, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(9, 21609, 21609, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(10, 21601, 21601, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(11, 126757, 126757, 1),
			new CapturedAreteAlexAreaVendorStockDefinition(12, 21613, 21613, 1)
		})
	};

	internal static ReadOnlyCollection<CapturedAreteAlexAreaVendorDefinition> Vendors => new ReadOnlyCollection<CapturedAreteAlexAreaVendorDefinition>(VendorDefinitions);
}
