using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class MissionInstanceContentModule : IPlayfieldContentModule
{
	public bool Supports(Identity playfieldIdentity)
	{
		return MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref playfieldIdentity)).Instance);
	}

	public void Register(PlayfieldContentRegistration registration)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (registration != null && Supports(registration.PlayfieldIdentity))
		{
			Identity playfieldIdentity = registration.PlayfieldIdentity;
			LogUtil.Debug((DebugInfoDetail)128, "MissionInstanceContentModule RegisterCapturedNpcSpawns pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			registration.RegisterCapturedNpcSpawns();
		}
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
