namespace AORebirth.Core.Inventory
{
    using System.Collections.Generic;

    using AORebirth.Core.Items;
    using AORebirth.Enums;

    public static class InventoryItemRules
    {
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
    }
}
