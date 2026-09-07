namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Authoritative ClientMoveItemToInventory + delayed equip/unequip.
    /// </summary>
    public sealed class InventoryMoveService
    {
        const int DefaultEquipDelay = 20;

        private readonly object _gate = new();
        private readonly Dictionary<int, PendingEquip> _pending = new();
        private readonly IZoneLogger _logger;
        private readonly InventoryFlushService _flush;

        public InventoryMoveService(IZoneLogger logger, InventoryFlushService flush)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(flush);
            _logger = logger;
            _flush = flush;
        }

        public void CancelPending(int characterId)
        {
            PendingEquip? pending;
            lock (_gate)
            {
                if (!_pending.Remove(characterId, out pending))
                    return;
            }

            SetMoveLock(pending, false);
        }

        public bool HasPending(int characterId)
        {
            lock (_gate)
                return _pending.ContainsKey(characterId);
        }

        public void Tick(Playfield playfield, double deltaTime)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            if (deltaTime <= 0)
                return;

            List<PendingEquip> due = [];
            List<PendingEquip> stale = [];
            lock (_gate)
            {
                if (_pending.Count == 0)
                    return;

                List<int> remove = [];
                foreach (KeyValuePair<int, PendingEquip> pair in _pending)
                {
                    PendingEquip pending = pair.Value;
                    if (!ReferenceEquals(pending.OriginPlayfield, playfield))
                        continue;

                    if (!ReferenceEquals(pending.Player.Playfield, playfield))
                    {
                        stale.Add(pending);
                        remove.Add(pair.Key);
                        continue;
                    }

                    pending.RemainingSeconds -= deltaTime;
                    if (pending.RemainingSeconds > 0)
                        continue;

                    due.Add(pending);
                    remove.Add(pair.Key);
                }

                foreach (int id in remove)
                    _pending.Remove(id);
            }

            foreach (PendingEquip pending in stale)
                SetMoveLock(pending, false);

            foreach (PendingEquip pending in due)
                CompletePending(pending);
        }

        public void Handle(Player player, ClientMoveItemToInventoryMessage message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(message);

            if (!player.Inventory.IsHydrated || player.Session == null || player.Playfield == null)
                return;

            if (IsBlockedByPending(player, message))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ClientMoveItemToInventory rejected during pending equip char={0} source={1} target={2}",
                        player.Identity.Instance,
                        message.SourceContainer,
                        message.TargetPlacement));
                return;
            }

            if (!TryResolveSource(
                    player,
                    message.SourceContainer,
                    out Container sourcePage,
                    out int sourceSlot,
                    out Item item,
                    out LootableDynel? lootSource))
            {
                return;
            }

            bool sourceIsWear = sourcePage.Identity.Type.IsWearPage();
            if (!player.Inventory.TryResolveTargetSlot(
                    message.TargetPlacement,
                    out Container destPage,
                    out int destSlot,
                    out bool destIsWear))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ClientMoveItemToInventory unresolved target char={0} target={1}",
                        player.Identity.Instance,
                        message.TargetPlacement));
                return;
            }

            if (destPage.Content.ContainsKey(destSlot) && !(sourceIsWear && destIsWear))
            {
                // Equip-to-occupied wear slot is a swap; bag targets must be empty.
                if (!destIsWear || !sourceIsWear)
                    return;
            }

            if (destPage.Content.TryGetValue(destSlot, out Item? destOccupant) && destOccupant.Locked)
                return;

            bool touchesEquipment = sourceIsWear || destIsWear;
            if (touchesEquipment)
            {
                if (HasPending(player.Identity.Instance))
                    return;
                if (destIsWear && !MeetsEquipRequirements(player, item, destPage, destSlot))
                    return;

                if (sourceIsWear
                    && destIsWear
                    && destPage.Content.TryGetValue(destSlot, out Item? swapped)
                    && !MeetsEquipRequirements(player, swapped, sourcePage, sourceSlot))
                {
                    return;
                }

                if (!MeetsWeaponHandPairing(player, item, destPage, destSlot, sourcePage, sourceSlot))
                    return;

                double delaySeconds = ResolveEquipDelaySeconds(item, destPage.Identity.Type == IdentityType.SocialPage);
                Item? other = null;
                if (sourceIsWear && destIsWear)
                    destPage.Content.TryGetValue(destSlot, out other);
                if (other != null)
                    delaySeconds += ResolveEquipDelaySeconds(other, sourcePage.Identity.Type == IdentityType.SocialPage);

                var pending = new PendingEquip(
                    player,
                    message.SourceContainer,
                    sourcePage,
                    sourceSlot,
                    destPage,
                    destSlot,
                    item,
                    other,
                    lootSource,
                    delaySeconds,
                    ackTargetPlacement: destSlot);

                if (delaySeconds <= 0)
                {
                    CompletePending(pending);
                    return;
                }

                SetMoveLock(pending, true);
                lock (_gate)
                {
                    if (!_pending.TryAdd(player.Identity.Instance, pending))
                    {
                        SetMoveLock(pending, false);
                        return;
                    }
                }
                return;
            }

            if (!ApplyMove(player, sourcePage, sourceSlot, destPage, destSlot, item, lootSource))
                return;

            SendAck(player, message.SourceContainer, destSlot);
        }

        bool IsBlockedByPending(Player player, ClientMoveItemToInventoryMessage message)
        {
            lock (_gate)
            {
                if (!_pending.TryGetValue(player.Identity.Instance, out PendingEquip? pending))
                    return false;

                if (message.SourceContainer.Type == IdentityType.Backpack)
                {
                    int handle = DecodeBackpackHandle(message.SourceContainer);
                    int slot = DecodeBackpackSlot(message.SourceContainer);
                    if (pending.SourcePage.InventoryHandle == handle && pending.SourceSlot == slot)
                        return true;
                }
                else if (message.SourceContainer.Type == pending.SourcePage.Identity.Type
                    && message.SourceContainer.Instance == pending.SourceSlot)
                {
                    return true;
                }

                if (pending.LockedInstanceId > 0
                    && TryPeekSourceInstance(player, message.SourceContainer, out int instanceId)
                    && instanceId == pending.LockedInstanceId)
                {
                    return true;
                }

                if (player.Inventory.TryResolveTargetSlot(
                        message.TargetPlacement,
                        out Container destPage,
                        out int destSlot,
                        out _))
                {
                    if (ReferenceEquals(destPage, pending.DestPage) && destSlot == pending.DestSlot)
                        return true;
                    if (ReferenceEquals(destPage, pending.SourcePage) && destSlot == pending.SourceSlot)
                        return true;
                }

                return false;
            }
        }

        static bool TryPeekSourceInstance(Player player, Identity source, out int instanceId)
        {
            instanceId = 0;
            if (!TryResolveSource(player, source, out _, out _, out Item item, out _))
                return false;

            instanceId = item.InstanceId;
            return instanceId > 0;
        }

        static bool TryResolveSource(
            Player player,
            Identity source,
            out Container page,
            out int slot,
            out Item item,
            out LootableDynel? lootSource)
        {
            page = null!;
            slot = -1;
            item = null!;
            lootSource = null;

            if (source.Type == IdentityType.Backpack)
            {
                int handle = DecodeBackpackHandle(source);
                slot = DecodeBackpackSlot(source);

                if (player.Inventory.TryGetBackpackPageByHandle(handle, out page))
                {
                    if ((page.Flags & ContainerFlags.CanRemove) == 0)
                        return false;

                    return page.Content.TryGetValue(slot, out item!) && !item.Locked;
                }

                Playfield? playfield = player.Playfield;
                if (playfield == null)
                    return false;

                foreach (Dynel dynel in playfield.GetRequiredService<DynelRegistry>().Dynels())
                {
                    if (dynel is not LootableDynel lootable || lootable.InventoryHandle != handle)
                        continue;

                    if (lootable.OpenerIdentity != player.Identity)
                        return false;

                    if ((lootable.Loot.Flags & ContainerFlags.CanRemove) == 0)
                        return false;

                    page = lootable.Loot;
                    lootSource = lootable;
                    return page.Content.TryGetValue(slot, out item!) && !item.Locked;
                }

                return false;
            }

            if (!player.Inventory.TryGetItem(source.Type, source.Instance, out item))
                return false;

            if (item.Locked)
                return false;

            slot = source.Instance;
            page = source.Type switch
            {
                IdentityType.Inventory => player.Inventory.Inventory,
                IdentityType.WeaponPage => player.Inventory.Equipment,
                IdentityType.ArmorPage => player.Inventory.Armor,
                IdentityType.ImplantPage => player.Inventory.Implant,
                IdentityType.SocialPage => player.Inventory.Social,
                IdentityType.Bank => player.Inventory.Bank,
                _ => null!
            };

            return page != null && (page.Flags & ContainerFlags.CanRemove) != 0;
        }

        void CompletePending(PendingEquip pending)
        {
            SetMoveLock(pending, false);

            Player player = pending.Player;
            if (!player.Inventory.IsHydrated
                || player.IsDead
                || player.Session == null
                || !ReferenceEquals(player.Playfield, pending.OriginPlayfield))
                return;

            if (!pending.SourcePage.Content.TryGetValue(pending.SourceSlot, out Item? current)
                || !ReferenceEquals(current, pending.Item))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Pending equip aborted; source changed char={0} slot={1}",
                        player.Identity.Instance,
                        pending.SourceSlot));
                return;
            }

            if (!ApplyMove(
                    player,
                    pending.SourcePage,
                    pending.SourceSlot,
                    pending.DestPage,
                    pending.DestSlot,
                    pending.Item,
                    pending.LootSource))
            {
                return;
            }

            player.Rebase();
            SendAck(player, pending.AckSource, pending.AckTargetPlacement);
            NotifyEquipmentChanged(player, pending);
        }

        static void NotifyEquipmentChanged(Player player, PendingEquip pending)
        {
            if (pending.SourcePage.Identity.Type.IsWearPage())
                player.OnEquipmentChanged(new EquipSlot(pending.SourcePage.Identity.Type, pending.SourceSlot));

            if (pending.DestPage.Identity.Type.IsWearPage()
                && (pending.SourcePage.Identity.Type != pending.DestPage.Identity.Type
                    || pending.SourceSlot != pending.DestSlot))
                player.OnEquipmentChanged(new EquipSlot(pending.DestPage.Identity.Type, pending.DestSlot));
        }

        bool ApplyMove(
            Player player,
            Container sourcePage,
            int sourceSlot,
            Container destPage,
            int destSlot,
            Item item,
            LootableDynel? lootSource)
        {
            Item? existingDest = destPage.Content.GetValueOrDefault(destSlot);

            if (!ReferenceEquals(sourcePage, destPage) || sourceSlot != destSlot)
            {
                if (sourcePage.Remove(sourceSlot) == null)
                    return false;

                if (existingDest != null)
                {
                    destPage.Remove(destSlot);
                    if (!sourcePage.Add(sourceSlot, existingDest))
                    {
                        // Rollback best-effort
                        sourcePage.Add(sourceSlot, item);
                        destPage.Add(destSlot, existingDest);
                        return false;
                    }

                    player.Inventory.MarkDirty(existingDest, sourcePage, sourceSlot);
                }

                if (!destPage.Add(destSlot, item))
                {
                    sourcePage.Add(sourceSlot, item);
                    if (existingDest != null)
                    {
                        sourcePage.Remove(sourceSlot);
                        destPage.Add(destSlot, existingDest);
                    }

                    return false;
                }
            }

            player.Inventory.MarkDirty(item, destPage, destSlot);
            lootSource?.NotifyLootChanged();
            _flush.NotifyDirty(player);
            return true;
        }

        static void SetMoveLock(PendingEquip pending, bool locked)
        {
            pending.Item.Locked = locked;
            if (pending.SwappedItem != null)
                pending.SwappedItem.Locked = locked;
        }

        static void SendAck(Player player, Identity sourceContainer, int targetPlacement)
        {
            player.Session?.Send(
                new ContainerAddItemMessage
                {
                    Identity = player.Identity,
                    SourceContainer = sourceContainer,
                    Target = player.Identity,
                    TargetPlacement = targetPlacement,
                    Unknown = 0
                });
        }

        static bool MeetsEquipRequirements(Player player, Item item, Container wearPage, int destSlot)
        {
            if (!item.Can(CanFlags.Wear))
                return false;

            if (!FitsWearSlot(item, wearPage, destSlot))
                return false;

            ActionType needed = wearPage.Identity.Type == IdentityType.WeaponPage
                ? ActionType.ToWield
                : ActionType.ToWear;

            return item.Definition.MeetsActionRequirements(stat => player.Stats.Get(stat), needed);
        }

        static bool MeetsWeaponHandPairing(
            Player player,
            Item incoming,
            Container destPage,
            int destSlot,
            Container sourcePage,
            int sourceSlot)
        {
            if (!IsWeaponHand(destPage, destSlot) && !IsWeaponHand(sourcePage, sourceSlot))
                return true;

            Item? right = ResolveHandAfterMove(
                player,
                (int)WeaponSlots.Righthand,
                incoming,
                destPage,
                destSlot,
                sourcePage,
                sourceSlot);
            Item? left = ResolveHandAfterMove(
                player,
                (int)WeaponSlots.LeftHand,
                incoming,
                destPage,
                destSlot,
                sourcePage,
                sourceSlot);
            return AreHandsCompatible(right, left);
        }

        static bool IsWeaponHand(Container page, int slot)
            => page.Identity.Type == IdentityType.WeaponPage
                && (slot == (int)WeaponSlots.Righthand || slot == (int)WeaponSlots.LeftHand);

        static Item? ResolveHandAfterMove(
            Player player,
            int handSlot,
            Item incoming,
            Container destPage,
            int destSlot,
            Container sourcePage,
            int sourceSlot)
        {
            if (destPage.Identity.Type == IdentityType.WeaponPage && destSlot == handSlot)
                return incoming;

            if (sourcePage.Identity.Type == IdentityType.WeaponPage && sourceSlot == handSlot)
            {
                if (destPage.Identity.Type == IdentityType.WeaponPage
                    && destPage.Content.TryGetValue(destSlot, out Item? swapped))
                    return swapped;
                return null;
            }

            return player.Inventory.Equipment.Content.GetValueOrDefault(handSlot);
        }

        static bool AreHandsCompatible(Item? right, Item? left)
        {
            if (right == null || left == null)
                return true;

            WeaponFlags rightFlags = right.GetWeaponFlags();
            WeaponFlags leftFlags = left.GetWeaponFlags();
            const WeaponFlags styleMask = WeaponFlags.Melee | WeaponFlags.Ranged | WeaponFlags.Unarmed;
            if ((rightFlags & styleMask) == 0 || (leftFlags & styleMask) == 0)
                return true;

            bool rightTwoHanded = (rightFlags & WeaponFlags.TwoHanded) != 0;
            bool leftTwoHanded = (leftFlags & WeaponFlags.TwoHanded) != 0;
            bool rightOneHanded = (rightFlags & WeaponFlags.OneHanded) != 0;
            bool leftOneHanded = (leftFlags & WeaponFlags.OneHanded) != 0;
            if (rightTwoHanded || leftTwoHanded)
            {
                if (rightTwoHanded && leftTwoHanded)
                    return false;
                if (rightTwoHanded && leftOneHanded)
                    return false;
                if (leftTwoHanded && rightOneHanded)
                    return false;
            }

            bool rightRanged = (rightFlags & WeaponFlags.Ranged) != 0;
            bool leftRanged = (leftFlags & WeaponFlags.Ranged) != 0;
            bool rightMelee = (rightFlags & (WeaponFlags.Melee | WeaponFlags.Unarmed)) != 0;
            bool leftMelee = (leftFlags & (WeaponFlags.Melee | WeaponFlags.Unarmed)) != 0;
            if ((rightRanged && leftMelee) || (leftRanged && rightMelee))
                return false;

            return true;
        }

        /// <summary>
        /// AO Placement/Slot (298) bitfield: allowed when (slotMask &amp; (1 &lt;&lt; relativeSlot)) != 0.
        /// Relative slot is page-local (WeaponSlots / ArmorSlots / ImplantSlots), 1-based.
        /// </summary>
        static bool FitsWearSlot(Item item, Container wearPage, int destSlot)
        {
            int relativeSlot = destSlot - wearPage.Offset + 1;
            if (relativeSlot < 1 || relativeSlot > wearPage.Capacity)
                return false;

            int slotMask = item.GetStat(CharacterStat.Slot);
            if (slotMask <= 0)
                return false;

            return (slotMask & (1 << relativeSlot)) != 0;
        }

        static double ResolveEquipDelaySeconds(Item item, bool isSocial)
        {
            if (isSocial)
                return DefaultEquipDelay * 0.01;

            int delay = StatCollection.Normalize(item.GetStat(CharacterStat.EquipDelay));
            if (delay <= 0)
                delay = DefaultEquipDelay;

            return delay * 0.01;
        }

        static int DecodeBackpackHandle(Identity sourceContainer)
            => (int)(((uint)sourceContainer.Instance >> 16) & 0xffff);

        static int DecodeBackpackSlot(Identity sourceContainer)
            => (int)((uint)sourceContainer.Instance & 0xffff);

        sealed class PendingEquip
        {
            public PendingEquip(
                Player player,
                Identity ackSource,
                Container sourcePage,
                int sourceSlot,
                Container destPage,
                int destSlot,
                Item item,
                Item? swappedItem,
                LootableDynel? lootSource,
                double remainingSeconds,
                int ackTargetPlacement)
            {
                Player = player;
                AckSource = ackSource;
                SourcePage = sourcePage;
                SourceSlot = sourceSlot;
                DestPage = destPage;
                DestSlot = destSlot;
                Item = item;
                SwappedItem = swappedItem;
                LootSource = lootSource;
                RemainingSeconds = remainingSeconds;
                AckTargetPlacement = ackTargetPlacement;
                LockedInstanceId = item.InstanceId;
                OriginPlayfield = player.Playfield!;
            }

            public Player Player { get; }

            public Identity AckSource { get; }

            public Container SourcePage { get; }

            public int SourceSlot { get; }

            public Container DestPage { get; }

            public int DestSlot { get; }

            public Item Item { get; }

            public Item? SwappedItem { get; }

            public LootableDynel? LootSource { get; }

            public double RemainingSeconds { get; set; }

            public int AckTargetPlacement { get; }

            public int LockedInstanceId { get; }

            public Playfield OriginPlayfield { get; }
        }
    }
}
