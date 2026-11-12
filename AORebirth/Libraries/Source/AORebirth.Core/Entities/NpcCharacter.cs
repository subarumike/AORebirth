namespace AORebirth.Core.Entities
{
    using AORebirth.Core.GameData;
    using AORebirth.Core.Items;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public class NpcCharacter : Character
    {
        public NpcCharacter(Identity parent, Identity identity, IController controller)
            : base(parent, identity, controller)
        {
        }

        public static IItem TryCreateDefaultWeaponItem()
        {
            if (ItemLoader.ItemList == null)
            {
                return null;
            }

            int templateId = ItemBehaviorBindings.Current.NpcFallbackWeaponTemplateId;
            if (!ItemLoader.ItemList.TryGetValue(templateId, out ItemTemplate template))
            {
                return null;
            }

            int quality = template != null && template.Quality > 0 ? template.Quality : 1;
            return new Item(quality, templateId, templateId)
                   {
                       MultipleCount = 1
                   };
        }
    }
}
