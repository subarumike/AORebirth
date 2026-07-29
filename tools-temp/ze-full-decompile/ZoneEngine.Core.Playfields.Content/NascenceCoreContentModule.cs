using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class NascenceCoreContentModule : IPlayfieldContentModule
{
	public bool Supports(Identity playfieldIdentity)
	{
		return false;
	}

	public void Register(PlayfieldContentRegistration registration)
	{
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
