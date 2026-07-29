using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class AreteContentModule : IPlayfieldContentModule
{
	private const int PrivateAretePlayfieldInstance = 6553;

	public bool Supports(Identity playfieldIdentity)
	{
		return ((Identity)(ref playfieldIdentity)).Instance == 6553;
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
		if (playfieldInstance != 6553)
		{
			return false;
		}
		switch (mobSpawnId)
		{
		case 2027138231:
		case 2027138245:
		case 2027138246:
		case 2027138249:
		case 2027138259:
			return true;
		default:
			return false;
		}
	}
}
