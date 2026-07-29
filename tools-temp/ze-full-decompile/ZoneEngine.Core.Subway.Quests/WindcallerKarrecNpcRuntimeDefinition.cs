using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcRuntimeDefinition
{
	internal Identity PlayfieldIdentity { get; private set; }

	internal Identity NpcIdentity { get; private set; }

	internal WindcallerKarrecNpcDefinition Content { get; private set; }

	internal WindcallerKarrecNpcRuntimeDefinition(Identity playfieldIdentity, Identity npcIdentity, WindcallerKarrecNpcDefinition content)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		PlayfieldIdentity = playfieldIdentity;
		NpcIdentity = npcIdentity;
		Content = content;
	}
}
