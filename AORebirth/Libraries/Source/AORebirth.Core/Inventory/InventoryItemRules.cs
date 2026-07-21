namespace AORebirth.Core.Inventory
{
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class InventoryItemRules
    {
        private const int BookOfKnowledgeTemplateId = 99302;

        public static bool IsSameTemplate(IItem left, IItem right)
        {
            return left != null
                   && right != null
                   && IsSameTemplateIdPair(left.LowID, left.HighID, right.LowID, right.HighID);
        }

        public static bool IsSameTemplateIdPair(int leftLowId, int leftHighId, int rightLowId, int rightHighId)
        {
            return leftLowId == rightLowId && leftHighId == rightHighId;
        }

        public static bool IsUniqueFlags(int flags)
        {
            return (flags & (int)ItemFlags.Unique) != 0;
        }

        public static bool IsUnique(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            ItemTemplate lowTemplate;
            if (ItemLoader.ItemList.TryGetValue(item.LowID, out lowTemplate)
                && lowTemplate.Stats.ContainsKey(0)
                && IsUniqueFlags(lowTemplate.Stats[0]))
            {
                return true;
            }

            ItemTemplate highTemplate;
            if (ItemLoader.ItemList.TryGetValue(item.HighID, out highTemplate)
                && highTemplate.Stats.ContainsKey(0)
                && IsUniqueFlags(highTemplate.Stats[0]))
            {
                return true;
            }

            return IsUniqueFlags(item.GetAttribute(0));
        }

        /// <summary>
        /// True only for real bags (legacy backpack template / Book of Knowledge / Backpack identity).
        /// Do NOT treat bare <see cref="IdentityType.Container"/> as a bag — armor and jewelry often
        /// carry that identity and would wrongly be blocked from going into backpacks (same trap as
        /// mail/GMI; see <see cref="IsMailForbiddenContainerItem"/>).
        /// </summary>
        public static bool IsBackpackContainerItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.Identity != null && item.Identity.Type == IdentityType.Backpack)
            {
                return true;
            }

            return IsBackpackContainerTemplate(item);
        }

        /// <summary>
        /// Items live mail Feedback_MailNoChests should block (Item field + send): real backpacks/bags
        /// ONLY — Backpack identity, legacy backpack template, or Book of Knowledge. Same conservative
        /// set as <see cref="IsGmiForbiddenContainerItem"/>.
        /// Do NOT use bare <see cref="IdentityType.Container"/> or bare ItemClass==2: many yes-drop
        /// items (armor especially) carry a Container identity and ItemClass 2, so either check would
        /// wrongly flag armor parts as containers and block them from mail.
        /// </summary>
        public static bool IsMailForbiddenContainerItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.Identity != null && item.Identity.Type == IdentityType.Backpack)
            {
                return true;
            }

            return IsBackpackContainerTemplate(item);
        }

        /// <summary>
        /// GMI deposit: block real backpacks/bags only (legacy backpack template / backpack identity / Book of Knowledge).
        /// Do not treat bare IdentityType.Container as forbidden — many yes-drop items carry that identity wrongly / broadly.
        /// Do not use bare ItemClass==2 — that is also Armor in AO data.
        /// </summary>
        public static bool IsGmiForbiddenContainerItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.Identity != null && item.Identity.Type == IdentityType.Backpack)
            {
                return true;
            }

            return IsBackpackContainerTemplate(item);
        }

        public static bool HasSameUniqueItem(IItem candidate, IEnumerable<IItem> existingItems)
        {
            if (!IsUnique(candidate) || existingItems == null)
            {
                return false;
            }

            foreach (IItem existingItem in existingItems)
            {
                if (existingItem != null
                    && !object.ReferenceEquals(existingItem, candidate)
                    && IsSameTemplate(existingItem, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryEnsureBackpackContainerIdentity(
            IItem item,
            Identity ownerIdentity,
            Identity itemPosition,
            out Identity containerIdentity)
        {
            containerIdentity = Identity.None;

            if (item == null)
            {
                return false;
            }

            if ((item.Identity != null) && (item.Identity.Type == IdentityType.Container))
            {
                containerIdentity = item.Identity;
                return true;
            }

            if (!IsBackpackContainerTemplate(item))
            {
                return false;
            }

            return AssignContainerIdentity(item, ownerIdentity, itemPosition, out containerIdentity);
        }

        /// <summary>
        /// Wire IdentityType.Container for any mail-blocked bag so the client
        /// Feedback_MailNoChests dialog rejects the mail Item field (same chrome as NoDrop).
        /// </summary>
        public static bool TryEnsureMailForbiddenContainerIdentity(
            IItem item,
            Identity ownerIdentity,
            Identity itemPosition,
            out Identity containerIdentity)
        {
            containerIdentity = Identity.None;

            if (item == null || !IsMailForbiddenContainerItem(item))
            {
                return false;
            }

            if ((item.Identity != null) && (item.Identity.Type == IdentityType.Container))
            {
                containerIdentity = item.Identity;
                return true;
            }

            return AssignContainerIdentity(item, ownerIdentity, itemPosition, out containerIdentity);
        }

        public static bool IsLegacyBackpackTemplate(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            const int BackpackCanFlags =
                (int)(CanFlags.Carry | CanFlags.Wear | CanFlags.Use);

            if (item.GetAttribute((int)StatIds.can) != BackpackCanFlags)
            {
                return false;
            }

            if (item.GetAttribute((int)StatIds.itemclass) != 2)
            {
                return false;
            }

            if (item.GetAttribute((int)StatIds.placement) != 8)
            {
                return false;
            }

            return item.Events.Any(
                x => x.EventType == EventType.OnWear
                     && x.Functions.Any(y => y.FunctionType == (int)FunctionType.BackMesh));
        }

        private static bool AssignContainerIdentity(
            IItem item,
            Identity ownerIdentity,
            Identity itemPosition,
            out Identity containerIdentity)
        {
            containerIdentity = CreateLegacyBackpackContainerIdentity(ownerIdentity, itemPosition, item);

            Item concreteItem = item as Item;
            if (concreteItem != null)
            {
                concreteItem.Identity = containerIdentity;
            }

            return true;
        }

        private static bool IsBackpackContainerTemplate(IItem item)
        {
            return IsLegacyBackpackTemplate(item)
                   || IsBookOfKnowledgeContainerTemplate(item);
        }

        private static bool IsBookOfKnowledgeContainerTemplate(IItem item)
        {
            return IsSameTemplateIdPair(
                item.LowID,
                item.HighID,
                BookOfKnowledgeTemplateId,
                BookOfKnowledgeTemplateId);
        }

        private static Identity CreateLegacyBackpackContainerIdentity(
            Identity ownerIdentity,
            Identity itemPosition,
            IItem item)
        {
            unchecked
            {
                uint hash = 2166136261;
                hash = MixBackpackContainerIdentity(hash, (uint)ownerIdentity.Instance);
                hash = MixBackpackContainerIdentity(hash, (uint)itemPosition.Type);
                hash = MixBackpackContainerIdentity(hash, (uint)itemPosition.Instance);
                hash = MixBackpackContainerIdentity(hash, (uint)item.LowID);
                hash = MixBackpackContainerIdentity(hash, (uint)item.HighID);
                hash = MixBackpackContainerIdentity(hash, (uint)item.Quality);

                int instance = (int)(hash & 0x7fffffff);
                if (instance == 0)
                {
                    instance = 1;
                }

                return new Identity { Type = IdentityType.Container, Instance = instance };
            }
        }

        private static uint MixBackpackContainerIdentity(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619;
            }
        }
    }
}
