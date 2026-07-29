using AORebirth.Core.Playfields;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields.Content;

public sealed class PlayfieldContentRegistration
{
	private readonly Playfield playfield;

	private readonly Identity playfieldIdentity;

	public Playfield Playfield => playfield;

	public Identity PlayfieldIdentity => playfieldIdentity;

	public PlayfieldContentRegistration(Playfield playfield, Identity playfieldIdentity)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		this.playfield = playfield;
		this.playfieldIdentity = playfieldIdentity;
	}

	public void RegisterCapturedNpcSpawns()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		playfield.SpawnCapturedNpcContent(playfieldIdentity);
	}
}
