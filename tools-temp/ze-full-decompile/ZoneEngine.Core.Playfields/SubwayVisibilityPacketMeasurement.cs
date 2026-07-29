namespace ZoneEngine.Core.Playfields;

internal static class SubwayVisibilityPacketMeasurement
{
	internal static int MeasureSerializedBytes(byte[] payload)
	{
		return (payload != null) ? payload.Length : 0;
	}
}
