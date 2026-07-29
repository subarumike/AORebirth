using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class NascenceLifeContentModule : IPlayfieldContentModule
{
	internal const int FrontierPlayfieldId = 4310;

	internal const int WildsPlayfieldId = 4311;

	internal const int CorePlayfieldId = 4312;

	internal const int Nascence4313PlayfieldId = 4313;

	public bool Supports(Identity playfieldIdentity)
	{
		int instance = ((Identity)(ref playfieldIdentity)).Instance;
		return instance == 4310 || instance == 4311 || instance == 4312 || instance == 4313;
	}

	public void Register(PlayfieldContentRegistration registration)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (registration != null && Supports(registration.PlayfieldIdentity))
		{
			Identity playfieldIdentity = registration.PlayfieldIdentity;
			LogUtil.Debug((DebugInfoDetail)128, "NascenceLifeContentModule RegisterCapturedNpcSpawns pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			registration.RegisterCapturedNpcSpawns();
		}
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
