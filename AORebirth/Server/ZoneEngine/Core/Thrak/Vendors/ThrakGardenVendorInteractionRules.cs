namespace ZoneEngine.Core.Thrak.Vendors
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture 20260718-210135 Thrak Omni garden vendor dialogue constants.
    /// </summary>
    internal static class ThrakGardenVendorInteractionRules
    {
        internal const int PlayfieldId = 4677;

        internal const string FuriousFistsName = "Craig-Or of the Furious Fists";
        internal const string PreservationName = "Craig-Or of Preservation";
        internal const string FlamingBarrelsName = "Craig-Or of Flaming Barrels";
        internal const string GearAndAmmoName = "Craig-Or of Gear & Ammo";
        internal const string ProtectionName = "Craig-Or of Protection";
        internal const string SonLenName = "Son-Len, Official of Power";

        internal const string SonLenNoKeyChatLine1 = "You do not have the key to this garden.";

        internal const string SonLenNoKeyChatLine2 =
            "I would recommend you to return when you have the blessing of the Divine.";

        internal const int FuriousFistsInstance = unchecked((int)0x79758F3F);
        internal const int PreservationInstance = unchecked((int)0x79758F3E);
        internal const int FlamingBarrelsInstance = unchecked((int)0x79758F3B);
        internal const int GearAndAmmoInstance = unchecked((int)0x79758F3C);
        internal const int ProtectionInstance = unchecked((int)0x79758F3D);
        internal const int SonLenInstance = unchecked((int)0x79758F40);

        internal const string FuriousFistsIdentityText = "SimpleChar:79758F3F";
        internal const string PreservationIdentityText = "SimpleChar:79758F3E";
        internal const string FlamingBarrelsIdentityText = "SimpleChar:79758F3B";
        internal const string GearAndAmmoIdentityText = "SimpleChar:79758F3C";
        internal const string ProtectionIdentityText = "SimpleChar:79758F3D";
        internal const string SonLenIdentityText = "SimpleChar:79758F40";

        internal const string CraigOrRootNodeId = "craig_or_001";

        internal static Identity CreateIdentity(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }
    }
}
