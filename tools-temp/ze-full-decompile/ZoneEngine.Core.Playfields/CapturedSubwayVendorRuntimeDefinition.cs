using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorRuntimeDefinition
{
	internal Identity PlayfieldIdentity { get; private set; }

	internal Identity NpcIdentity { get; private set; }

	internal Identity VendorIdentity { get; private set; }

	internal CapturedSubwayVendorDefinition Content { get; private set; }

	internal CapturedSubwayVendorRuntimeDefinition(Identity playfieldIdentity, Identity npcIdentity, Identity vendorIdentity, CapturedSubwayVendorDefinition content)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		PlayfieldIdentity = playfieldIdentity;
		NpcIdentity = npcIdentity;
		VendorIdentity = vendorIdentity;
		Content = content;
	}
}
