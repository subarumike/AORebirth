namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Stacking rules shared by split/join, vendor purchases and item grants. A gained stackable item is added
    /// onto an existing main-inventory stack of the same low/high/QL when the whole amount fits; otherwise it
    /// takes a slot of its own.
    /// </summary>
    internal static class InventoryStacking
    {
        /// <summary>Largest stack the client accepts (N3Msg_JoinItems refuses joins past 50000).</summary>
        public const int MaxStackCount = 50000;

        /// <summary>Stackable template, not a bag, not unique (two instances of a unique item are never made).</summary>
        public static bool IsStackable(Item item)
            => item.Can(CanFlags.Stackable)
                && !InventoryMoveService.IsBagItem(item)
                && !Trade.TradeRules.IsUnique(item);

        public static bool SameStack(Item a, Item b)
            => a.LowId == b.LowId && a.HighId == b.HighId && a.Quality == b.Quality;

        /// <summary>
        /// The first main-inventory stack (lowest slot) that can take all of <paramref name="incoming"/>.
        /// <paramref name="planned"/> holds counts already promised to stacks in the same transaction.
        /// </summary>
        public static bool TryFindStackTarget(
            Container page,
            Item incoming,
            IReadOnlyDictionary<Item, int>? planned,
            out int slot,
            out Item target)
        {
            slot = 0;
            target = null!;
            if (!IsStackable(incoming) || incoming.StackCount <= 0)
                return false;

            foreach (KeyValuePair<int, Item> pair in page.Content)
            {
                Item candidate = pair.Value;
                if (ReferenceEquals(candidate, incoming) || candidate.Locked || candidate.InstanceId <= 0
                    || !candidate.IsPersisted || !IsStackable(candidate) || !SameStack(candidate, incoming))
                    continue;

                int count = candidate.StackCount + (planned != null && planned.TryGetValue(candidate, out int extra) ? extra : 0);
                if ((long)count + incoming.StackCount > MaxStackCount)
                    continue;

                if (target == null || pair.Key < slot)
                {
                    slot = pair.Key;
                    target = candidate;
                }
            }

            return target != null;
        }

        /// <summary>
        /// Grant path for items announced with AddTemplate and saved by the inventory flush: when a stack can take
        /// all of <paramref name="incoming"/>, it is added there and the client is shown the same result. It places
        /// the AddTemplate copy in its first free slot itself, then merges it on the JoinItems echo (the merge a
        /// player's own join waits for). Needs one free slot for that copy. False leaves the caller to place it.
        /// </summary>
        public static bool TryGrantOntoStack(Player player, Item incoming, InventoryFlushService? flush)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(incoming);

            PlayerInventory inventory = player.Inventory;
            Container page = inventory.Inventory;
            if (!inventory.IsHydrated || !TryFindStackTarget(page, incoming, null, out int targetSlot, out Item target))
                return false;

            int clientSlot = page.FindFreeSlot();
            if (clientSlot < 0)
                return false;

            target.StackCount += incoming.StackCount;
            inventory.MarkDirty(target, page, targetSlot);
            flush?.NotifyDirty(player);
            SendGrantAndJoin(player, incoming, page, targetSlot, clientSlot);
            return true;
        }

        /// <summary>AddTemplate for the incoming copy, then the JoinItems echo folding it into the target stack.</summary>
        public static void SendGrantAndJoin(Player player, Item incoming, Container page, int targetSlot, int clientSlot)
        {
            if (player.Session == null)
                return;

            player.Session.Send(new AddTemplateMessage
            {
                Identity = player.Identity,
                HighId = incoming.HighId,
                LowId = incoming.LowId,
                Quality = incoming.Quality,
                Count = incoming.StackCount
            });
            player.Session.Send(new CharacterActionMessage
            {
                Identity = player.Identity,
                Action = CharacterActionType.JoinItems,
                Target = new Identity { Type = page.Identity.Type, Instance = targetSlot },
                Parameter1 = (int)page.Identity.Type,
                Parameter2 = clientSlot
            });
        }

        /// <summary>
        /// Wire value for an inventory Count (a 16-bit field): stacks up to <see cref="MaxStackCount"/> go out as
        /// their unsigned 16-bit pattern.
        /// </summary>
        public static short WireCount(int count)
            => unchecked((short)(ushort)Math.Clamp(count, 1, MaxStackCount));
    }
}
