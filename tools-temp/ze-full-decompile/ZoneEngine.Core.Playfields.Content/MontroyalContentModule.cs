using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class MontroyalContentModule : IPlayfieldContentModule
{
	private const int MontroyalPlayfieldInstance = 655;

	public bool Supports(Identity playfieldIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref playfieldIdentity)).Type != 51101 && (int)((Identity)(ref playfieldIdentity)).Type != 40016)
		{
			return false;
		}
		return ((Identity)(ref playfieldIdentity)).Instance == 655;
	}

	public void Register(PlayfieldContentRegistration registration)
	{
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		return false;
	}
}
