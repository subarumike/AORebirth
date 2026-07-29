using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedThrakGardenVendorRuntimeDefinition
{
	internal Identity PlayfieldIdentity { get; private set; }

	internal Identity NpcIdentity { get; private set; }

	internal Identity VendorIdentity { get; private set; }

	internal CapturedThrakGardenVendorDefinition Content { get; private set; }

	internal CapturedThrakGardenVendorRuntimeDefinition(Identity playfieldIdentity, Identity npcIdentity, Identity vendorIdentity, CapturedThrakGardenVendorDefinition content)
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
