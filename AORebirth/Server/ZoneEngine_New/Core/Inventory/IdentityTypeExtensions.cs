namespace ZoneEngine_New.Core.Inventory
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class IdentityTypeExtensions
    {
        public static bool IsWearPage(this IdentityType type)
            => type is IdentityType.WeaponPage
                or IdentityType.ArmorPage
                or IdentityType.ImplantPage
                or IdentityType.SocialPage;
    }
}
