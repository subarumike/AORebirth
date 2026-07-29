using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Thrak.Vendors;

internal static class ThrakGardenVendorInteractionRules
{
	internal const int PlayfieldId = 4677;

	internal const string FuriousFistsName = "Craig-Or of the Furious Fists";

	internal const string PreservationName = "Craig-Or of Preservation";

	internal const string FlamingBarrelsName = "Craig-Or of Flaming Barrels";

	internal const string GearAndAmmoName = "Craig-Or of Gear & Ammo";

	internal const string ProtectionName = "Craig-Or of Protection";

	internal const string SonLenName = "Son-Len, Official of Power";

	internal const int FuriousFistsInstance = 2037747519;

	internal const int PreservationInstance = 2037747518;

	internal const int FlamingBarrelsInstance = 2037747515;

	internal const int GearAndAmmoInstance = 2037747516;

	internal const int ProtectionInstance = 2037747517;

	internal const int SonLenInstance = 2037747520;

	internal const string FuriousFistsIdentityText = "SimpleChar:79758F3F";

	internal const string PreservationIdentityText = "SimpleChar:79758F3E";

	internal const string FlamingBarrelsIdentityText = "SimpleChar:79758F3B";

	internal const string GearAndAmmoIdentityText = "SimpleChar:79758F3C";

	internal const string ProtectionIdentityText = "SimpleChar:79758F3D";

	internal const string SonLenIdentityText = "SimpleChar:79758F40";

	internal const string CraigOrRootNodeId = "craig_or_001";

	internal static Identity CreateIdentity(int instance)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Identity result = default(Identity);
		((Identity)(ref result)).Type = (IdentityType)50000;
		((Identity)(ref result)).Instance = instance;
		return result;
	}
}
