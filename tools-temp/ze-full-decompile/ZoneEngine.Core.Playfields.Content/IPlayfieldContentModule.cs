using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public interface IPlayfieldContentModule
{
	bool Supports(Identity playfieldIdentity);

	void Register(PlayfieldContentRegistration registration);

	bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId);
}
