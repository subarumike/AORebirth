using System;
using System.Collections.ObjectModel;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecNpcContent
{
	internal const int PlayfieldId = 655;

	internal const int KarrecSourceInstance = 2036555963;

	internal const int MaddyCardileSourceInstance = 2036555964;

	internal const int AnnoyingDudeSourceInstance = 2036555965;

	internal const string Evidence = "AOSharpLiveCapture/20260717-223626";

	internal const string KarrecAppearanceEvidence = "AOSharpLiveCapture/20260719-174340+20260719-ICC-Capture";

	private static readonly WindcallerKarrecNpcDefinition KarrecDefinition = CreateKarrec();

	private static readonly WindcallerKarrecNpcDefinition MaddyCardileDefinition = CreateMaddyCardile();

	private static readonly WindcallerKarrecNpcDefinition AnnoyingDudeDefinition = CreateAnnoyingDude();

	private static readonly ReadOnlyCollection<WindcallerKarrecNpcDefinition> CapturedDefinitions = Array.AsReadOnly(new WindcallerKarrecNpcDefinition[3] { KarrecDefinition, AnnoyingDudeDefinition, MaddyCardileDefinition });

	internal static ReadOnlyCollection<WindcallerKarrecNpcDefinition> Definitions => CapturedDefinitions;

	internal static WindcallerKarrecNpcDefinition Karrec => KarrecDefinition;

	internal static WindcallerKarrecNpcDefinition AnnoyingDude => AnnoyingDudeDefinition;

	internal static WindcallerKarrecNpcDefinition MaddyCardile => MaddyCardileDefinition;

	internal static bool TryGetBySourceInstance(int sourceInstance, out WindcallerKarrecNpcDefinition definition)
	{
		foreach (WindcallerKarrecNpcDefinition capturedDefinition in CapturedDefinitions)
		{
			if (capturedDefinition.SourceNpcInstance == sourceInstance)
			{
				definition = capturedDefinition;
				return true;
			}
		}
		definition = null;
		return false;
	}

	private static WindcallerKarrecNpcDefinition CreateKarrec()
	{
		return new WindcallerKarrecNpcDefinition(2036555963, "Windcaller Karrec", 3212.3696f, 35.975f, 788.7493f, 0f, 0f, 0f, 1f, 1576, 0, 1, 1, 2, 1, 40818, 121, 40696, 136, 0, 200, 51008, 515, 277352961, 31, 1, 170552011u, HexToBytes("00000000000000000000000008010001000100010001000000020000"), new WindcallerKarrecNpcTextureDefinition[5]
		{
			new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
			new WindcallerKarrecNpcTextureDefinition(1, 161710, 0),
			new WindcallerKarrecNpcTextureDefinition(2, 161715, 0),
			new WindcallerKarrecNpcTextureDefinition(3, 161705, 0),
			new WindcallerKarrecNpcTextureDefinition(4, 161725, 0)
		}, new WindcallerKarrecNpcMeshDefinition[2]
		{
			new WindcallerKarrecNpcMeshDefinition(0, 20108u, 161720, 2),
			new WindcallerKarrecNpcMeshDefinition(0, 40696u, 0, 4)
		}, new WindcallerKarrecNpcWaypointDefinition[0], new WindcallerKarrecNpcPatrolSegment[0], new WindcallerKarrecNpcActiveNanoDefinition[1]
		{
			new WindcallerKarrecNpcActiveNanoDefinition(53019, 205631, 0, 29050327, 20192939)
		}, "AOSharpLiveCapture/20260719-174340+20260719-ICC-Capture");
	}

	private static WindcallerKarrecNpcDefinition CreateAnnoyingDude()
	{
		return new WindcallerKarrecNpcDefinition(2036555965, "Annoying Dude", 3185.8713f, 35.11f, 963.37897f, 0f, -0.7624053f, 0f, 0.6470998f, 1672, 0, 1, 4, 2, 1, 26103, 104, 40117, 103, 0, 45, 1958, 154, 277352961, 31, 0, 168512203u, HexToBytes("00000000000000000000000002010001000100010001000000020000"), new WindcallerKarrecNpcTextureDefinition[5]
		{
			new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
			new WindcallerKarrecNpcTextureDefinition(1, 247946, 0),
			new WindcallerKarrecNpcTextureDefinition(2, 247981, 0),
			new WindcallerKarrecNpcTextureDefinition(3, 247900, 0),
			new WindcallerKarrecNpcTextureDefinition(4, 248021, 0)
		}, new WindcallerKarrecNpcMeshDefinition[2]
		{
			new WindcallerKarrecNpcMeshDefinition(0, 40117u, 0, 4),
			new WindcallerKarrecNpcMeshDefinition(1, 136570u, 0, 2)
		}, new WindcallerKarrecNpcWaypointDefinition[1]
		{
			new WindcallerKarrecNpcWaypointDefinition(3185.8713f, 35.11f, 963.37897f)
		}, AnnoyingDudePatrol(), new WindcallerKarrecNpcActiveNanoDefinition[0], "AOSharpLiveCapture/20260717-223626");
	}

	private static WindcallerKarrecNpcDefinition CreateMaddyCardile()
	{
		return new WindcallerKarrecNpcDefinition(2036555964, "Maddy Cardile", 3332.3752f, 35.11f, 931.1814f, 0f, -0.5605756f, 0f, 0.82810324f, 1832, 0, 1, 1, 3, 1, 26090, 121, 40647, 103, 0, 200, 365, 515, 277352961, 31, 0, 168520395u, HexToBytes("00000000000000000000000002010001000100010001000000020000"), new WindcallerKarrecNpcTextureDefinition[5]
		{
			new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
			new WindcallerKarrecNpcTextureDefinition(1, 247974, 0),
			new WindcallerKarrecNpcTextureDefinition(2, 248003, 0),
			new WindcallerKarrecNpcTextureDefinition(3, 247927, 0),
			new WindcallerKarrecNpcTextureDefinition(4, 248040, 0)
		}, new WindcallerKarrecNpcMeshDefinition[1]
		{
			new WindcallerKarrecNpcMeshDefinition(0, 40647u, 0, 4)
		}, new WindcallerKarrecNpcWaypointDefinition[1]
		{
			new WindcallerKarrecNpcWaypointDefinition(3332.3752f, 35.11f, 931.1814f)
		}, MaddyCardilePatrol(), new WindcallerKarrecNpcActiveNanoDefinition[0], "AOSharpLiveCapture/20260717-223626");
	}

	private static WindcallerKarrecNpcPatrolSegment[] AnnoyingDudePatrol()
	{
		return new WindcallerKarrecNpcPatrolSegment[16]
		{
			new WindcallerKarrecNpcPatrolSegment(1.2518461, 3185.8713f, 35.11f, 963.37897f, 3183.1382f, 35.11f, 963.90625f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.069998, 3184.431f, 35.11f, 963.6568f, 3179.623f, 35.11f, 966.89526f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.1199181, 3180.6162f, 35.11f, 966.1917f, 3176.2905f, 35.11f, 966.78534f, 24),
			new WindcallerKarrecNpcPatrolSegment(1.1513932, 3177.7588f, 35.11f, 966.6421f, 3175.0254f, 35.11f, 964.8803f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.8999266, 3176.0923f, 35.11f, 965.6956f, 3176.8582f, 35.11f, 960.4279f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.7219631, 3176.593f, 35.11f, 961.5911f, 3179.3623f, 35.11f, 956.97266f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.7397159, 3178.58f, 35.11f, 958.21857f, 3183.0103f, 35.11f, 955.02856f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.6405055, 3181.9949f, 35.11f, 955.7225f, 3188.0889f, 35.11f, 955.11835f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.5103682, 3186.9495f, 35.11f, 955.2f, 3192.253f, 35.11f, 956.6322f, 24),
			new WindcallerKarrecNpcPatrolSegment(4.3023294, 3190.9956f, 35.11f, 956.2637f, 3193.9844f, 35.11f, 963.0764f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.4452929, 3193.509f, 35.11f, 961.86035f, 3196.0657f, 35.11f, 966.2162f, 24),
			new WindcallerKarrecNpcPatrolSegment(1.9668067, 3195.45f, 35.11f, 965.19275f, 3194.4636f, 35.11f, 969.1803f, 24),
			new WindcallerKarrecNpcPatrolSegment(1.8002349, 3194.8572f, 35.11f, 967.97736f, 3191.1982f, 35.11f, 969.3128f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.2201903, 3192.551f, 35.11f, 968.9577f, 3189.8767f, 35.11f, 965.3749f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.1293892, 3190.5933f, 35.11f, 966.593f, 3188.6785f, 35.11f, 963.0809f, 24),
			new WindcallerKarrecNpcPatrolSegment(4.2700076, 3189.2793f, 35.11f, 964.19073f, 3185.707f, 35.11f, 963.352f, 24)
		};
	}

	private static WindcallerKarrecNpcPatrolSegment[] MaddyCardilePatrol()
	{
		return new WindcallerKarrecNpcPatrolSegment[19]
		{
			new WindcallerKarrecNpcPatrolSegment(4.3708276, 3331.092f, 35.11f, 931.6949f, 3328.165f, 35.11f, 938.84814f, 24),
			new WindcallerKarrecNpcPatrolSegment(4.7539665, 3328.6182f, 35.11f, 937.61615f, 3334.118f, 35.11f, 943.11914f, 24),
			new WindcallerKarrecNpcPatrolSegment(6.8341767, 3333.1355f, 35.11f, 942.24005f, 3344.758f, 35.11f, 945.08716f, 24),
			new WindcallerKarrecNpcPatrolSegment(5.5713148, 3343.389f, 35.11f, 944.7704f, 3352.4988f, 35.11f, 941.20355f, 24),
			new WindcallerKarrecNpcPatrolSegment(5.4357095, 3351.2083f, 35.11f, 941.73755f, 3352.7683f, 35.11f, 932.7326f, 24),
			new WindcallerKarrecNpcPatrolSegment(5.2456334, 3352.61f, 35.11f, 933.9278f, 10527f / (float)Math.PI, 35.11f, 924.86646f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.8700161, 3351.1235f, 35.11f, 926.20123f, 3347.5503f, 35.11f, 920.6987f, 24),
			new WindcallerKarrecNpcPatrolSegment(6.8971459, 3348.285f, 35.11f, 921.78394f, 3336.6365f, 35.11f, 918.53046f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.0849565, 3337.8767f, 35.11f, 918.8517f, 3332.9138f, 35.11f, 915.32043f, 24),
			new WindcallerKarrecNpcPatrolSegment(1.5295171, 3334.1016f, 35.11f, 916.20233f, 3331.517f, 35.11f, 913.7632f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.886747, 3332.437f, 35.11f, 914.65466f, 3326.889f, 35.11f, 915.19574f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.6604726, 3328.3193f, 35.11f, 914.97235f, 3323.9749f, 35.11f, 918.013f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.5598438, 3325.0276f, 35.11f, 917.23206f, 3321.2034f, 35.11f, 920.7672f, 24),
			new WindcallerKarrecNpcPatrolSegment(1.9879705, 3322.179f, 35.11f, 919.8518f, 3320.1196f, 35.11f, 923.59863f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.7351056, 3320.714f, 35.11f, 922.4446f, 3326.4133f, 35.11f, 925.5359f, 24),
			new WindcallerKarrecNpcPatrolSegment(4.7399797, 3325.0818f, 35.11f, 924.96295f, 3333.3406f, 35.11f, 924.61884f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.2594041, 3332.1345f, 35.11f, 924.69037f, 3337.286f, 35.11f, 927.9241f, 24),
			new WindcallerKarrecNpcPatrolSegment(2.1707049, 3336.2654f, 35.11f, 927.2335f, 3334.6282f, 35.11f, 930.9468f, 24),
			new WindcallerKarrecNpcPatrolSegment(3.2493866, 3335.3037f, 35.11f, 929.8639f, 3329.789f, 35.11f, 932.21594f, 24)
		};
	}

	private static byte[] HexToBytes(string value)
	{
		byte[] array = new byte[value.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
