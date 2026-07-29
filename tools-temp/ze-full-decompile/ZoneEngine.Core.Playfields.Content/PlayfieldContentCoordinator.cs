using AORebirth.Core.Playfields;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class PlayfieldContentCoordinator
{
	private readonly IPlayfieldContentModule[] modules;

	public PlayfieldContentCoordinator(params IPlayfieldContentModule[] modules)
	{
		this.modules = modules ?? new IPlayfieldContentModule[0];
	}

	public void RegisterContent(Playfield playfield, Identity playfieldIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		PlayfieldContentRegistration registration = new PlayfieldContentRegistration(playfield, playfieldIdentity);
		IPlayfieldContentModule[] array = modules;
		foreach (IPlayfieldContentModule playfieldContentModule in array)
		{
			if (playfieldContentModule.Supports(playfieldIdentity))
			{
				playfieldContentModule.Register(registration);
			}
		}
	}

	public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
	{
		IPlayfieldContentModule[] array = modules;
		foreach (IPlayfieldContentModule playfieldContentModule in array)
		{
			if (playfieldContentModule.ShouldSuppressDbMobSpawn(playfieldInstance, mobSpawnId))
			{
				return true;
			}
		}
		return false;
	}
}
