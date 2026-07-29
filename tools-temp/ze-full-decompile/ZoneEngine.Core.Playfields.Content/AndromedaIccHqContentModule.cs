using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class AndromedaIccHqContentModule : IPlayfieldContentModule
{
	private const int AndromedaPlayfieldInstance = 655;

	public bool Supports(Identity playfieldIdentity)
	{
		return ((Identity)(ref playfieldIdentity)).Instance == 655;
	}

	public void Register(PlayfieldContentRegistration registration)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (registration != null && Supports(registration.PlayfieldIdentity))
		{
			Identity playfieldIdentity = registration.PlayfieldIdentity;
			LogUtil.Debug((DebugInfoDetail)128, "AndromedaIccHqContentModule RegisterCapturedNpcSpawns pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			registration.RegisterCapturedNpcSpawns();
		}
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
