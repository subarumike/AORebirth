namespace ZoneEngine.Core.Nascence.Vendors
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture 20260823-205320 Aban Redeemed garden vendor dialogue constants.
    /// </summary>
    internal static class AbanGardenVendorInteractionRules
    {
        internal const int PlayfieldId = 4676;

        internal const string FuriousFistsName = "Or-Mada of the Furious Fists";
        internal const string PreservationName = "Or-Mada of Preservation";
        internal const string FlamingBarrelsName = "Or-Mada of Flaming Barrels";
        internal const string GearAndAmmoName = "Or-Mada of Gear & Ammo";
        /// <summary>Capture display name shared by both Protection NPCs.</summary>
        internal const string ProtectionName = "Or-Mada of Protection";
        internal const string ElMadaName = "El-Mada, Official of Consistency";

        internal const int FuriousFistsInstance = unchecked((int)0x7A2013B7);
        internal const int ProtectionNearPreservationInstance = unchecked((int)0x7A2013B4);
        internal const int PreservationInstance = unchecked((int)0x7A2013B5);
        internal const int FlamingBarrelsInstance = unchecked((int)0x7A2013B8);
        internal const int ProtectionNearGearInstance = unchecked((int)0x7A2013B6);
        internal const int GearAndAmmoInstance = unchecked((int)0x7A2013B9);
        internal const int ElMadaInstance = unchecked((int)0x7A2013BA);

        internal const string FuriousFistsIdentityText = "SimpleChar:7A2013B7";
        internal const string ProtectionNearPreservationIdentityText = "SimpleChar:7A2013B4";
        internal const string PreservationIdentityText = "SimpleChar:7A2013B5";
        internal const string FlamingBarrelsIdentityText = "SimpleChar:7A2013B8";
        internal const string ProtectionNearGearIdentityText = "SimpleChar:7A2013B6";
        internal const string GearAndAmmoIdentityText = "SimpleChar:7A2013B9";
        internal const string ElMadaIdentityText = "SimpleChar:7A2013BA";

        internal const string OrMadaRootNodeId = "or_mada_001";
        internal const string ElMadaRootNodeId = "el_mada_001";

        internal static Identity CreateIdentity(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }
    }
}
