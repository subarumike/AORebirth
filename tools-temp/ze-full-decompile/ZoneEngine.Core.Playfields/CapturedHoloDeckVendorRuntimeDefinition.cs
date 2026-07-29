using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedHoloDeckVendorRuntimeDefinition
{
	internal Identity PlayfieldIdentity { get; private set; }

	internal Identity VendorIdentity { get; private set; }

	internal CapturedHoloDeckVendorRuntimeDefinition(Identity playfieldIdentity, Identity vendorIdentity)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		PlayfieldIdentity = playfieldIdentity;
		VendorIdentity = vendorIdentity;
	}
}
