namespace ZoneEngine.Core;

internal sealed class CapturedBureaucratShellDisplay
{
	public int DisplayItemLowId { get; private set; }

	public int DisplayItemHighId { get; private set; }

	public int DisplayQuality { get; private set; }

	public CapturedBureaucratShellDisplay(int displayItemLowId, int displayItemHighId, int displayQuality)
	{
		DisplayItemLowId = displayItemLowId;
		DisplayItemHighId = displayItemHighId;
		DisplayQuality = displayQuality;
	}
}
