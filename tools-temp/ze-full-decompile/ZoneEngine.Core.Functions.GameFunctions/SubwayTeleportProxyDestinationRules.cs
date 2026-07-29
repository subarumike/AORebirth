using AORebirth.Core.Vector;

namespace ZoneEngine.Core.Functions.GameFunctions;

public static class SubwayTeleportProxyDestinationRules
{
	public const int CapturedSubwayPlayfieldId = 127;

	public const int CapturedEntranceDoorInstance = -1073348481;

	public const float CapturedEntranceLandingX = 65.80835f;

	public const float CapturedEntranceLandingY = 115.6148f;

	public const float CapturedEntranceLandingZ = 318.9879f;

	public const float CapturedEntranceHeadingX = 0f;

	public const float CapturedEntranceHeadingY = 0.7071124f;

	public const float CapturedEntranceHeadingZ = 0f;

	public const float CapturedEntranceHeadingW = 0.7071012f;

	public const int CapturedMainExitPlayfieldId = 655;

	public const uint CapturedMainExitExternalDoorInstance = 3222930063u;

	public const float CapturedMainExitLandingX = 3304.028f;

	public const float CapturedMainExitLandingY = 35.11f;

	public const float CapturedMainExitLandingZ = 837.9951f;

	public const float CapturedMainExitHeadingX = 0f;

	public const float CapturedMainExitHeadingY = -0.4771534f;

	public const float CapturedMainExitHeadingZ = 0f;

	public const float CapturedMainExitHeadingW = 0.87882f;

	public static bool TryResolveDestinationOverride(int destinationPlayfieldId, int destinationDoorInstance, out Coordinate destination, out Quaternion heading)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		if (destinationPlayfieldId == 127 && destinationDoorInstance == -1073348481)
		{
			destination = new Coordinate(65.80835f, 115.6148f, 318.9879f);
			heading = new Quaternion(0.0, 0.7071123719215393, 0.0, 0.7071012258529663);
			return true;
		}
		destination = null;
		heading = null;
		return false;
	}

	public static bool TryResolveMainExitOverride(int destinationPlayfieldId, uint externalDoorInstance, out Coordinate destination, out Quaternion heading)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		if (destinationPlayfieldId == 655 && externalDoorInstance == 3222930063u)
		{
			destination = new Coordinate(3304.028f, 35.11f, 837.9951f);
			heading = new Quaternion(0.0, -0.47715339064598083, 0.0, 0.87882000207901);
			return true;
		}
		destination = null;
		heading = null;
		return false;
	}
}
