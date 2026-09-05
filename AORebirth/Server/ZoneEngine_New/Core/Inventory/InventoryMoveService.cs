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
        const int MissingEquipDelay = 1234567890;
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
            lock (_gate)
                _pending.Remove(characterId);
        }

        public void Tick(Playfield playfield, double deltaTime)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            if (deltaTime <= 0)
                return;

            List<PendingEquip> due = [];
            lock (_gate)
            {
                if (_pending.Count == 0)
                    return;

                List<int> remove = [];
                foreach (KeyValuePair<int, PendingEquip> pair in _pending)
                {
                    PendingEquip pending = pair.Value;
                    if (!ReferenceEquals(pending.Player.Playfield, playfield))
                        continue;

                    pending.RemainingSeconds -= deltaTime;
                    if (pending.RemainingSeconds > 0)
                        continue;

                    due.Add(pending);
                    remove.Add(pair.Key);
                }

                foreach (int id in remove)
                    _pending.Remove(id);
            }

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

            bool sourceIsWear = IsWearPage(sourcePage.Identity.Type);
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

            bool touchesEquipment = sourceIsWear || destIsWear;
            if (touchesEquipment)
            {
                if (destIsWear && !MeetsEquipRequirements(player, item, destPage.Identity.Type))
                    return;

                if (sourceIsWear
                    && destIsWear
                    && destPage.Content.TryGetValue(destSlot, out Item? swapped)
                    && !MeetsEquipRequirements(player, swapped, sourcePage.Identity.Type))
                {
                    return;
                }

                double delaySeconds = ResolveEquipDelaySeconds(item, destPage.Identity.Type == IdentityType.SocialPage);
                if (sourceIsWear && destIsWear && destPage.Content.TryGetValue(destSlot, out Item? other))
                    delaySeconds += ResolveEquipDelaySeconds(other, sourcePage.Identity.Type == IdentityType.SocialPage);

                var pending = new PendingEquip(
                    player,
                    message.SourceContainer,
                    sourcePage,
                    sourceSlot,
                    destPage,
                    destSlot,
                    item,
                    lootSource,
                    delaySeconds,
                    ackTargetPlacement: destSlot);

                lock (_gate)
                    _pending[player.Identity.Instance] = pending;

                if (delaySeconds <= 0)
                {
                    CancelPending(player.Identity.Instance);
                    CompletePending(pending);
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

                return true;
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

                    return page.Content.TryGetValue(slot, out item!);
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
                    return page.Content.TryGetValue(slot, out item!);
                }

                return false;
            }

            if (!player.Inventory.TryGetItem(source.Type, source.Instance, out item))
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
            Player player = pending.Player;
            if (!player.Inventory.IsHydrated || player.IsDead)
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

        static bool MeetsEquipRequirements(Player player, Item item, IdentityType wearPage)
        {
            ActionType needed = wearPage == IdentityType.WeaponPage
                ? ActionType.ToWield
                : ActionType.ToWear;

            ItemAction? action = null;
            foreach (ItemAction candidate in item.Definition.Actions)
            {
                if (candidate.ActionType == (int)needed)
                {
                    action = candidate;
                    break;
                }
            }

            if (action == null)
                return true;

            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (!EvaluateRequirement(player, requirement))
                    return false;
            }

            return true;
        }

        static bool EvaluateRequirement(Player player, ItemRequirement requirement)
        {
            int statValue = player.Stats.Get((CharacterStat)requirement.StatNumber);
            int required = requirement.Value;
            return (Operator)requirement.Operator switch
            {
                Operator.EqualTo => statValue == required,
                Operator.GreaterThan => statValue > required,
                Operator.LessThan => statValue < required,
                Operator.BitAnd => (statValue & required) != 0,
                Operator.NotBitAnd => (statValue & required) == 0,
                _ => true
            };
        }

        static double ResolveEquipDelaySeconds(Item item, bool isSocial)
        {
            if (isSocial)
                return DefaultEquipDelay * 0.01;

            int delay = item.GetStat(CharacterStat.EquipDelay);
            if (delay == MissingEquipDelay || delay <= 0)
                delay = DefaultEquipDelay;

            return delay * 0.01;
        }

        static bool IsWearPage(IdentityType type)
            => type is IdentityType.WeaponPage
                or IdentityType.ArmorPage
                or IdentityType.ImplantPage
                or IdentityType.SocialPage;

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
                LootSource = lootSource;
                RemainingSeconds = remainingSeconds;
                AckTargetPlacement = ackTargetPlacement;
                LockedInstanceId = item.InstanceId;
            }

            public Player Player { get; }

            public Identity AckSource { get; }

            public Container SourcePage { get; }

            public int SourceSlot { get; }

            public Container DestPage { get; }

            public int DestSlot { get; }

            public Item Item { get; }

            public LootableDynel? LootSource { get; }

            public double RemainingSeconds { get; set; }

            public int AckTargetPlacement { get; }

            public int LockedInstanceId { get; }
        }
    }
}
