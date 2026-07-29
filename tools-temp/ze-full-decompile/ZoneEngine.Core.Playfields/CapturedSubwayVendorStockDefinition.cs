namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorStockDefinition
{
	internal int Slot { get; private set; }

	internal int LowId { get; private set; }

	internal int HighId { get; private set; }

	internal int Quality { get; private set; }

	internal CapturedSubwayVendorStockDefinition(int slot, int lowId, int highId, int quality)
	{
		Slot = slot;
		LowId = lowId;
		HighId = highId;
		Quality = quality;
	}
}
