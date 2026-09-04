namespace AORebirth.Core.Entities
{
    using AORebirth.Core.Items;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public class NpcCharacter : Character
    {
        /// <summary>
        /// Generic Monster Distance Weapon — used when an NPC has no usable WeaponPage item.
        /// </summary>
        public const int DefaultWeaponTemplateId = 44008;

        public NpcCharacter(Identity parent, Identity identity, IController controller)
            : base(parent, identity, controller)
        {
        }

        public static IItem TryCreateDefaultWeaponItem()
        {
            if (ItemLoader.ItemList == null
                || !ItemLoader.ItemList.ContainsKey(DefaultWeaponTemplateId))
            {
                return null;
            }

            ItemTemplate template = ItemLoader.ItemList[DefaultWeaponTemplateId];
            int quality = template != null && template.Quality > 0 ? template.Quality : 1;
            return new Item(quality, DefaultWeaponTemplateId, DefaultWeaponTemplateId)
                   {
                       MultipleCount = 1
                   };
        }
    }
}
