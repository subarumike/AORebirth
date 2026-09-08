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
    /// Authoritative ClientMoveItemToInventory, ClientContainerAddItem, and delayed equip/unequip.
    /// </summary>
    public sealed class InventoryMoveService
    {
        const int DefaultEquipDelay = 20;

        private readonly object _gate = new();
        private readonly Dictionary<int, PendingEquip> _pending = new();
        private readonly IZoneLogger _logger;
        private readonly InventoryActionService _actions;

        public InventoryMoveService(IZoneLogger logger, InventoryFlushService flush, InventoryActionService actions)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(flush);
            _logger = logger;
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
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
            _actions.Tick(playfield);
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

            destPage.Content.TryGetValue(destSlot, out Item? destOccupant);
            if (destOccupant != null && destOccupant.Locked)
                return;

            // Occupied wear or unequip-onto-occupied bag is a swap. Bag-to-bag stays empty-only.
            if (destOccupant != null && !destIsWear && !sourceIsWear)
                return;

            bool touchesEquipment = sourceIsWear || destIsWear;
            if (touchesEquipment)
            {
                TryBeginEquipmentMove(
                    player,
                    message.SourceContainer,
                    player.Identity,
                    sourcePage,
                    sourceSlot,
                    destPage,
                    destSlot,
                    item,
                    destOccupant,
                    lootSource);
                return;
            }

            if (!ApplyMove(player, sourcePage, sourceSlot, destPage, destSlot, item, lootSource))
                return;

            SendAck(player, message.SourceContainer, player.Identity, destSlot);
        }

        public void Handle(Player player, ClientContainerAddItemMessage message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(message);

            if (!player.Inventory.IsHydrated || player.Session == null || player.Playfield == null)
                return;

            if (!TryResolveAddSource(
                    player,
                    message.Source,
                    out Container sourcePage,
                    out int sourceSlot,
                    out Item item))
                return;

            if (item.Locked)
                return;

            // TODO: Block temporary items on ClientContainerAddItem
            if (IsBagItem(item))
                return;

            if (!TryResolveAddTarget(player, message.Target, out Container destPage))
                return;

            if (ReferenceEquals(sourcePage, destPage))
                return;

            if ((destPage.Flags & ContainerFlags.CanAdd) == 0)
                return;

            int destSlot = destPage.FindFreeSlot();
            if (destSlot < 0)
                return;

            if (sourcePage.Identity.Type.IsWearPage())
            {
                TryBeginEquipmentMove(
                    player,
                    message.Source,
                    message.Target,
                    sourcePage,
                    sourceSlot,
                    destPage,
                    destSlot,
                    item,
                    destOccupant: null,
                    lootSource: null);
                return;
            }

            if (!ApplyMove(player, sourcePage, sourceSlot, destPage, destSlot, item, lootSource: null))
                return;

            SendAck(player, message.Source, message.Target, destSlot);
        }

        void TryBeginEquipmentMove(
            Player player,
            Identity ackSource,
            Identity ackTarget,
            Container sourcePage,
            int sourceSlot,
            Container destPage,
            int destSlot,
            Item item,
            Item? destOccupant,
            LootableDynel? lootSource)
        {
            if (HasPending(player.Identity.Instance))
                return;

            bool destIsWear = destPage.Identity.Type.IsWearPage();
            bool sourceIsWear = sourcePage.Identity.Type.IsWearPage();
            if (destIsWear && !MeetsEquipRequirements(player, item, destPage, destSlot))
                return;

            if (sourceIsWear
                && destOccupant != null
                && !MeetsEquipRequirements(player, destOccupant, sourcePage, sourceSlot))
                return;

            if (!MeetsWeaponHandPairing(player, item, destPage, destSlot, sourcePage, sourceSlot))
                return;

            double delaySeconds = ResolveEquipDelaySeconds(item, destPage.Identity.Type == IdentityType.SocialPage);
            if (destOccupant != null)
                delaySeconds += ResolveEquipDelaySeconds(destOccupant, sourcePage.Identity.Type == IdentityType.SocialPage);

            var pending = new PendingEquip(
                player,
                ackSource,
                ackTarget,
                sourcePage,
                sourceSlot,
                destPage,
                destSlot,
                item,
                destOccupant,
                lootSource,
                delaySeconds,
                destSlot);

            if (delaySeconds <= 0)
            {
                CompletePending(pending);
                return;
            }

            SetMoveLock(pending, true);
            lock (_gate)
            {
                if (_pending.TryAdd(player.Identity.Instance, pending))
                    return;
            }

            SetMoveLock(pending, false);
        }

        static bool TryResolveAddSource(
            Player player,
            Identity source,
            out Container page,
            out int slot,
            out Item item)
        {
            page = null!;
            slot = -1;
            item = null!;

            if (source.Type == IdentityType.Backpack || source.Type == IdentityType.Container)
                return TryResolveOwnedBackpackSource(player, source, out page, out slot, out item);

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
                IdentityType.BankByRef when player.Inventory.Bank.IsHydrated => player.Inventory.Bank,
                IdentityType.OverflowWindow => player.Inventory.Overflow,
                _ => null!
            };

            return page != null && (page.Flags & ContainerFlags.CanRemove) != 0;
        }

        static bool TryResolveOwnedBackpackSource(
            Player player,
            Identity source,
            out Container page,
            out int slot,
            out Item item)
        {
            page = null!;
            slot = DecodeBackpackSlot(source);
            item = null!;

            int handle = DecodeBackpackHandle(source);
            if (handle != 0 && player.Inventory.TryGetOwnedBackpackPageByHandle(handle, out page))
                return TryReadRemovableSlot(page, slot, out item);

            if (handle == 0
                && source.Instance > 0
                && player.Inventory.TryGetOwnedBackpackPageByHandle(source.Instance, out page))
            {
                slot = 0;
                return TryReadRemovableSlot(page, slot, out item);
            }

            if (handle == 0
                && source.Type == IdentityType.Container
                && player.Inventory.TryGetUniqueOwnedBackpackSlot(source.Instance, out page, out item))
            {
                slot = source.Instance;
                return (page.Flags & ContainerFlags.CanRemove) != 0 && !item.Locked;
            }

            return false;
        }

        static bool TryReadRemovableSlot(Container page, int slot, out Item item)
        {
            item = null!;
            if ((page.Flags & ContainerFlags.CanRemove) == 0)
                return false;

            return page.Content.TryGetValue(slot, out item!) && !item.Locked;
        }

        static bool TryResolveAddTarget(Player player, Identity target, out Container destPage)
        {
            destPage = null!;

            if (target.Type == IdentityType.Container)
                return player.Inventory.TryGetOwnedBackpackPage(target, out destPage);

            if (target.Type != IdentityType.Bank && target.Type != IdentityType.BankByRef)
                return false;

            if (target.Instance != player.Identity.Instance)
                return false;

            if (!player.Inventory.Bank.IsHydrated)
                return false;

            destPage = player.Inventory.Bank;
            return true;
        }

        public static bool IsBagItem(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Identity.Type == IdentityType.Container)
                return true;

            return item.Definition != null && item.Definition.ItemType == (int)IdentityType.Backpack;
        }

        bool IsBlockedByPending(Player player, ClientMoveItemToInventoryMessage message)
        {
            lock (_gate)
            {
                if (!_pending.TryGetValue(player.Identity.Instance, out PendingEquip? pending))
                    return false;

                if (message.SourceContainer.Type == IdentityType.Backpack
                    || message.SourceContainer.Type == IdentityType.Container)
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

            if (source.Type == IdentityType.Backpack || source.Type == IdentityType.Container)
            {
                if (TryResolveOwnedBackpackSource(player, source, out page, out slot, out item))
                    return true;

                if (source.Type != IdentityType.Backpack)
                    return false;

                int handle = DecodeBackpackHandle(source);
                slot = DecodeBackpackSlot(source);

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
                IdentityType.OverflowWindow => player.Inventory.Overflow,
                IdentityType.BankByRef => player.Inventory.Bank,
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

            // The occupant of the destination was requirement-checked when the move began, and an
            // empty destination carries no lock. Anything that arrived since (trade return, trade
            // delivery, shop purchase, loot) would otherwise be swapped onto a wear page unchecked.
            Item? occupant = pending.DestPage.Content.GetValueOrDefault(pending.DestSlot);
            if (!ReferenceEquals(occupant, pending.SwappedItem))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Pending equip aborted; destination changed char={0} slot={1}",
                        player.Identity.Instance,
                        pending.DestSlot));
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

            SendUnequipActions(player, pending);
            player.Rebase();
            SendAck(player, pending.AckSource, pending.AckTarget, pending.AckTargetPlacement);
            NotifyEquipmentChanged(player, pending);
        }

        static void SendUnequipActions(Player player, PendingEquip pending)
        {
            if (pending.SourcePage.Identity.Type.IsWearPage())
                SendUnequipAction(player, pending.SourceSlot);

            if (pending.SwappedItem != null && pending.DestPage.Identity.Type.IsWearPage())
                SendUnequipAction(player, pending.DestSlot);
        }

        static void SendUnequipAction(Player player, int slot)
        {
            player.Session?.Send(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.Unknown3,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = slot,
                    Unknown2 = 0
                });
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
            if (ReferenceEquals(sourcePage, destPage) && sourceSlot == destSlot)
                return true;
            if (sourceSlot < sourcePage.Offset || sourceSlot >= sourcePage.Offset + sourcePage.Capacity
                || destSlot < destPage.Offset || destSlot >= destPage.Offset + destPage.Capacity
                || destPage.Identity.Type == IdentityType.OverflowWindow)
                return false;
            if (lootSource != null && Trade.TradeRules.IsUnique(item)
                && Trade.TradeRules.WouldDuplicateUnique(player, item.LowId, item.HighId))
                return false;
            var changes = new List<InventoryRowChange>
            {
                new(item, destPage.Identity, destSlot, item.StackCount)
            };
            if (existingDest != null)
                changes.Add(new InventoryRowChange(existingDest, sourcePage.Identity, sourceSlot, existingDest.StackCount));
            bool committed = _actions.TryCommit(player, changes,
                () => !item.Locked && existingDest?.Locked != true
                    && sourcePage.Content.TryGetValue(sourceSlot, out Item? current) && ReferenceEquals(current, item)
                    && ReferenceEquals(destPage.Content.GetValueOrDefault(destSlot), existingDest),
                () =>
                {
                    sourcePage.Content.Remove(sourceSlot);
                    if (existingDest != null)
                    {
                        destPage.Content.Remove(destSlot);
                        if (!sourcePage.Add(sourceSlot, existingDest))
                            throw new InvalidOperationException("Reserved inventory swap source changed.");
                    }
                    if (!destPage.Add(destSlot, item))
                        throw new InvalidOperationException("Reserved inventory move destination changed.");
                });
            if (committed) lootSource?.NotifyLootChanged();
            return committed;
        }

        static void SetMoveLock(PendingEquip pending, bool locked)
        {
            pending.Item.Locked = locked;
            if (pending.SwappedItem != null)
                pending.SwappedItem.Locked = locked;
        }

        static void SendAck(Player player, Identity sourceContainer, Identity target, int targetPlacement)
        {
            player.Session?.Send(
                new ContainerAddItemMessage
                {
                    Identity = player.Identity,
                    SourceContainer = sourceContainer,
                    Target = target,
                    TargetPlacement = targetPlacement,
                    Unknown = 0
                });
        }

        static bool MeetsEquipRequirements(Player player, Item item, Container wearPage, int destSlot)
        {
            // Slot bits are page-local. ItemClass must match the wear page first so a crafted
            // ClientMoveItemToInventory cannot land a weapon on armor/implant/social slots.
            // Can.Wear is not required; some legal weapons (e.g. 121564) omit it.
            if (!FitsWearPage(item, wearPage))
                return false;
            if (!FitsWearSlot(item, wearPage, destSlot))
                return false;

            ActionType needed = wearPage.Identity.Type == IdentityType.WeaponPage
                ? ActionType.ToWield
                : ActionType.ToWear;

            return item.Definition.MeetsActionRequirements(stat => player.Stats.Get(stat), needed);
        }

        static bool FitsWearPage(Item item, Container wearPage)
        {
            var itemClass = (ItemClass)item.GetStat(CharacterStat.ItemClass);
            return wearPage.Identity.Type switch
            {
                IdentityType.WeaponPage => itemClass is ItemClass.Weapon or ItemClass.Utility,
                IdentityType.ArmorPage => itemClass == ItemClass.Armor,
                IdentityType.ImplantPage => itemClass == ItemClass.Implant,
                IdentityType.SocialPage => itemClass == ItemClass.Armor,
                _ => false
            };
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
                if (destPage.Content.TryGetValue(destSlot, out Item? swapped))
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
                Identity ackTarget,
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
                AckTarget = ackTarget;
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

            public Identity AckTarget { get; }

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
