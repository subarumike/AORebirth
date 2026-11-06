namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Modules.Events;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Modules;

    /// <summary>
    /// The overflow window's side of giving a player an item: the client message that puts an item there, keeping the
    /// window to its Gameplay.json capacity (the oldest goes), and sending the saved window again to a fresh client.
    /// </summary>
    public static class OverflowGrants
    {
        /// <summary>ContainerAddItem placement for "the next free slot" (the client numbers overflow first-free).</summary>
        const int NextFreeSlotMarker = 0x6F;

        /// <summary>
        /// The overflow window's size in its InventoryUpdate: retail sends 20 (capture 2026-10-18T20:05:29Z, a declined
        /// trade landing in overflow). A window holding a later slot is sent large enough to include it.
        /// </summary>
        const int WindowSlots = 20;

        /// <summary>SlotnumberInMainInventory of the overflow window's InventoryUpdate (retail: 0x6E, its own type).</summary>
        const int WindowPlacement = 0x6E;

        /// <summary>
        /// Opens (or refreshes) the client's overflow window with everything in it: InventoryUpdate of the overflow page,
        /// as retail sends after a declined trade's item lands there (BagIdentity = the player, slot number 0x6E).
        /// Without it the client never shows the window.
        /// </summary>
        public static void SendWindow(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (player.Session == null || !player.Inventory.IsHydrated)
                return;

            Container overflow = player.Inventory.Overflow;
            InventoryUpdateMessage window = overflow.BuildInventoryUpdateMessage(player.Identity, player.Identity, WindowPlacement);
            int highest = overflow.Content.Count == 0 ? -1 : overflow.Content.Keys.Max();
            window.NumberOfSlots = Math.Max(WindowSlots, highest + 1);
            player.Session.Send(window);
        }

        /// <summary>
        /// A declined trade's item from trade-window slot <paramref name="tradeSlot"/> lands in overflow slot
        /// <paramref name="overflowSlot"/>: retail's ContainerAddItem from the trade window (type 0x6C) into the overflow
        /// window (capture 2026-10-18T20:05:29Z). Follow with <see cref="Trim"/>, which sends the window.
        /// </summary>
        public static void SendFromTrade(Player player, int tradeSlot, int overflowSlot)
        {
            ArgumentNullException.ThrowIfNull(player);
            player.Session?.Send(new ContainerAddItemMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                SourceContainer = new Identity { Type = IdentityType.KnuBotTradeWindow, Instance = tradeSlot },
                Target = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                TargetPlacement = overflowSlot
            });
        }

        /// <summary>How many items <paramref name="player"/>'s overflow keeps (Gameplay.json inventory.overflowCapacity).</summary>
        public static int Capacity(Player player)
            => player.Playfield?.GetService<GameplaySettings>()?.Inventory.OverflowCapacity ?? GameplayInventory.DefaultOverflowCapacity;

        /// <summary>
        /// Tells the client <paramref name="item"/> arrived in its overflow window: TemplateAction Overflow, then
        /// ContainerAddItem into the window's next free slot.
        /// </summary>
        public static void Send(Player player, Item item)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(item);
            if (player.Session == null)
                return;

            player.Session.Send(new TemplateActionMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                ItemLowId = item.LowId,
                ItemHighId = item.HighId,
                Quality = item.Quality,
                Count = item.StackCount,
                Action = TemplateActionType.Overflow,
                Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                Unknown3 = 0,
                Unknown4 = 0
            });
            player.Session.Send(new ContainerAddItemMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance },
                TargetPlacement = NextFreeSlotMarker
            });
        }

        /// <summary>
        /// Keeps <paramref name="player"/>'s overflow to its capacity and shows it: the oldest items past it are deleted
        /// (their rows retired) and removed from the client's window with DeleteItem, the reply the client gets for
        /// deleting an overflow item itself; then the window is sent (<see cref="SendWindow"/>). Call after putting items
        /// there and telling the client.
        /// </summary>
        public static void Trim(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            var graveyard = new Identity { Type = IdentityType.None, Instance = player.Identity.Instance };
            List<(Item Item, int Slot)> removed = player.Inventory.TrimOverflow(Capacity(player), graveyard);
            foreach ((Item gone, int slot) in removed)
            {
                ModuleHooks.ItemMoved(player, gone, ModuleHooks.Place(ItemPlaceKind.Inventory), ModuleHooks.Place(ItemPlaceKind.Destroyed));
                player.Session?.Send(new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.DeleteItem,
                    Unknown1 = 0,
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = slot },
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
            }

            if (removed.Count > 0)
                player.Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(player);
            if (player.Inventory.IsHydrated && player.Inventory.Overflow.Content.Count > 0)
                SendWindow(player);
        }

        /// <summary>
        /// A client that just logged in (or reconnected) has an empty overflow window: it gets the saved one again, packed
        /// from slot 0, in one InventoryUpdate. Trims a window loaded over capacity first.
        /// </summary>
        public static void Resend(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!player.Inventory.IsHydrated || player.Inventory.Overflow.Content.Count == 0)
                return;

            foreach ((Item gone, int _) in player.Inventory.TrimOverflow(Capacity(player),
                         new Identity { Type = IdentityType.None, Instance = player.Identity.Instance }))
                ModuleHooks.ItemMoved(player, gone, ModuleHooks.Place(ItemPlaceKind.Inventory), ModuleHooks.Place(ItemPlaceKind.Destroyed));
            player.Inventory.PackOverflow();
            // The whole window in one InventoryUpdate, as retail shows overflow.
            SendWindow(player);
            player.Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(player);
        }
    }
}
