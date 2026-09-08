namespace ZoneEngine_New.Core.Inventory
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// A worn-item slot on Weapons, Armor, Implant, or Social.
    /// Placement is page-local and overlaps across pages, so the page is required.
    /// </summary>
    public readonly struct EquipSlot
    {
        public EquipSlot(IdentityType page, int placement)
        {
            Page = page;
            Placement = placement;
        }

        public IdentityType Page { get; }

        public int Placement { get; }
    }
}
