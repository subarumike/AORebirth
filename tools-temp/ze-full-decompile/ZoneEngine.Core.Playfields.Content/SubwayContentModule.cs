using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class SubwayContentModule : IPlayfieldContentModule
{
	private const int SubwayPlayfieldInstance = 127;

	public bool Supports(Identity playfieldIdentity)
	{
		return ((Identity)(ref playfieldIdentity)).Instance == 127;
	}

	public void Register(PlayfieldContentRegistration registration)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if (registration != null && Supports(registration.PlayfieldIdentity))
		{
			registration.RegisterCapturedNpcSpawns();
		}
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
