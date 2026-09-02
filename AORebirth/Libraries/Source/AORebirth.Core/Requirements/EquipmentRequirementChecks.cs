namespace AORebirth.Core.Requirements
{
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Worn/wielded template checks for item action requirements.
    /// </summary>
    internal static class EquipmentRequirementChecks
    {
        internal static bool HasWornItemTemplate(Character character, int templateId)
        {
            if (character == null || templateId <= 0)
            {
                return false;
            }

            return PageContainsTemplate(character, (int)IdentityType.ArmorPage, templateId)
                   || PageContainsTemplate(character, (int)IdentityType.ImplantPage, templateId)
                   || PageContainsTemplate(character, (int)IdentityType.SocialPage, templateId);
        }

        internal static bool HasWieldedItemTemplate(Character character, int templateId)
        {
            if (character == null || templateId <= 0)
            {
                return false;
            }

            return PageContainsTemplate(character, (int)IdentityType.WeaponPage, templateId);
        }

        private static bool PageContainsTemplate(Character character, int pageType, int templateId)
        {
            IInventoryPage page;
            if (character.BaseInventory == null
                || !character.BaseInventory.Pages.TryGetValue(pageType, out page))
            {
                return false;
            }

            BaseInventoryPage concretePage = page as BaseInventoryPage;
            if (concretePage == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IItem> kv in concretePage.List())
            {
                IItem item = kv.Value;
                if (item != null && (item.LowID == templateId || item.HighID == templateId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
