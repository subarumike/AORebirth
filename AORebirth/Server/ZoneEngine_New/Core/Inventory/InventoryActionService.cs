namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;

    /// <summary>Plan first, commit all durable rows, then publish one inventory mutation.</summary>
    public sealed partial class InventoryActionService
    {
        readonly IInventoryMutationPersistence _persistence;
        readonly InventoryFlushService _flush;
        readonly IItemInstanceIdAllocator _ids;
        readonly IZoneLogger _logger;

        public InventoryActionService(IInventoryMutationPersistence persistence, InventoryFlushService flush,
            IItemInstanceIdAllocator ids, IZoneLogger logger)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Handle(Player player, CharacterActionMessage message)
        {
            if (player.Session?.State != SessionState.InPlay || player.IsDead || player.IsPersistenceQuarantined)
                return;
            if (!TryResolveOwnedSlot(player, message.Target, out _, out Item item)) return;
            if (message.Action == CharacterActionType.DeleteItem
                && TryDelete(player, message.Target, item.InstanceId))
            {
                player.Session?.Send(new CharacterActionMessage
                {
                    Identity = player.Identity, Action = message.Action, Target = message.Target,
                    Unknown = message.Unknown, Unknown1 = message.Unknown1, Unknown2 = message.Unknown2,
                    Parameter1 = 0, Parameter2 = 0
                });
            }
            else if (message.Action == CharacterActionType.Split)
            {
                // The supported Legacy path does not send a fabricated split acknowledgement.
                TrySplit(player, message.Target, item.InstanceId, message.Parameter2);
            }
        }

        public bool TryDelete(Player player, Identity slot, int expectedInstanceId)
            => TryRetireItem(player, slot, expectedInstanceId, finalStats: null);

        /// <summary>
        /// Retires one owned inventory item (mail attach / discard). Optional <paramref name="finalStats"/>
        /// commits in the same inventory mutation (e.g. postage debit).
        /// </summary>
        public bool TryRetireItem(
            Player player,
            Identity slot,
            int expectedInstanceId,
            IReadOnlyList<StatRecord>? finalStats)
        {
            if (!TryResolveOwnedSlot(player, slot, out Container page, out Item item)
                || item.InstanceId != expectedInstanceId) return false;
            if (InventoryMoveService.IsBagItem(item)
                && player.Inventory.TryGetBackpackPage(item.Identity, out Container bagPage)
                && bagPage.Content.Count != 0) return false;
            var graveyard = new Identity { Type = IdentityType.None, Instance = player.Identity.Instance };
            bool result = TryCommit(player,
                [new InventoryRowChange(item, graveyard, item.InstanceId, item.StackCount, Retired: true)],
                () => IsCurrent(page, slot.Instance, item, expectedInstanceId),
                () =>
                {
                    page.Content.Remove(slot.Instance);
                    if (finalStats != null)
                    {
                        foreach (StatRecord stat in finalStats)
                            player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base, dirty: true);
                        player.FlushDirtyStats();
                    }
                },
                finalStats: finalStats);
            if (result && page.Identity.Type.IsWearPage())
            {
                player.Rebase();
                player.OnEquipmentChanged(new EquipSlot(page.Identity.Type, slot.Instance));
            }
            return result;
        }

        /// <summary>Persists only character stats through the inventory mutation boundary (cash postage, COD).</summary>
        public bool TryApplyStats(Player player, IReadOnlyList<StatRecord> finalStats)
        {
            if (player.Session?.State != SessionState.InPlay || player.IsPersistenceQuarantined || player.IsDead
                || finalStats == null || finalStats.Count == 0)
                return false;

            return TryCommit(player, [],
                () => true,
                () =>
                {
                    foreach (StatRecord stat in finalStats)
                        player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base, dirty: true);
                    player.FlushDirtyStats();
                },
                finalStats: finalStats);
        }

        public bool TrySplit(Player player, Identity slot, int expectedInstanceId, int amount)
        {
            if (player.Session?.State != SessionState.InPlay || player.IsPersistenceQuarantined || player.IsDead
                || !TryResolveOwnedSlot(player, slot, out Container page, out Item item)
                || item.InstanceId != expectedInstanceId || page.Identity.Type.IsWearPage()
                || page.Identity.Type == IdentityType.OverflowWindow || InventoryMoveService.IsBagItem(item)
                || !item.Can(CanFlags.Stackable) || item.Can(CanFlags.CantSplit)
                || amount <= 0 || amount >= item.StackCount) return false;
            int destination = page.FindFreeSlot();
            if (destination < 0) return false;
            int originalCount = item.StackCount;
            int newId = _ids.Allocate();
            if (newId <= 0 || newId == item.InstanceId) throw new InvalidOperationException("Invalid split instance allocation.");
            var split = new Item
            {
                InstanceId = newId, Identity = item.Identity.Type == IdentityType.None ? Identity.None
                    : new Identity { Type = item.Identity.Type, Instance = newId },
                LowId = item.LowId, HighId = item.HighId, Quality = item.Quality,
                StackCount = amount, Source = item.Source, Definition = item.Definition
            };
            return TryCommit(player,
                [new InventoryRowChange(item, page.Identity, slot.Instance, originalCount - amount),
                 new InventoryRowChange(split, page.Identity, destination, amount)],
                () => IsCurrent(page, slot.Instance, item, expectedInstanceId)
                    && item.StackCount == originalCount && !page.Content.ContainsKey(destination),
                () =>
                {
                    item.StackCount = originalCount - amount;
                    if (!page.Add(destination, split)) throw new InvalidOperationException("Reserved split slot changed.");
                });
        }

        public static bool TryResolveOwnedSlot(Player player, Identity slot, out Container page, out Item item)
        {
            page = null!; item = null!;
            if (!player.Inventory.IsHydrated) return false;
            page = slot.Type switch
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
            // Backpack actions have a separate packed handle/slot contract. Do not guess it here.
            return page != null && slot.Instance >= page.Offset && slot.Instance < page.Offset + page.Capacity
                && page.Content.TryGetValue(slot.Instance, out item!) && !item.Locked && item.InstanceId > 0;
        }

        static bool IsCurrent(Container page, int slot, Item expected, int instance)
            => !expected.Locked && expected.InstanceId == instance
                && page.Content.TryGetValue(slot, out Item? current) && ReferenceEquals(current, expected);

        internal bool TryCommit(Player player, IReadOnlyList<InventoryRowChange> changes,
            Func<bool> validate, Action publish, IReadOnlyList<StatRecord>? finalStats = null)
        {
            bool committed = false;
            bool successful = false;
            try
            {
                _flush.WithExclusivePlayers(player, null, () =>
                {
                    if (player.Session?.State != SessionState.InPlay || player.IsPersistenceQuarantined || !validate()) return;
                    PlayerInventory.InventoryDirtyFlush? pending = player.Inventory.TakeDirty();
                    int[] nanos = player.DrainDirtyUploadedNanos();
                    try
                    {
                        var inserts = (pending?.Inserts ?? []).ToDictionary(r => r.InstanceId);
                        var locations = (pending?.Updates ?? []).ToDictionary(r => r.InstanceId);
                        var stacks = new List<ItemStackUpdate>();
                        if (changes.Select(c => c.Item.InstanceId).Distinct().Count() != changes.Count)
                            throw new InvalidOperationException("One mutation may write an instance only once.");
                        foreach (InventoryRowChange change in changes)
                        {
                            Item item = change.Item;
                            if (item.InstanceId <= 0 || change.FinalCount <= 0)
                                throw new InvalidOperationException("Invalid inventory mutation identity or count.");
                            inserts.Remove(item.InstanceId);
                            locations.Remove(item.InstanceId);
                            if (change.Retired && !item.IsPersisted) continue;
                            if (change.Container.Type == IdentityType.OverflowWindow)
                                throw new InvalidOperationException("Durable inventory cannot target memory-only overflow.");
                            if (item.IsPersisted)
                            {
                                locations[item.InstanceId] = new ItemLocationUpdate(item.InstanceId,
                                    (int)change.Container.Type, change.Container.Instance, change.Placement);
                                if (change.FinalCount != item.StackCount)
                                    stacks.Add(new ItemStackUpdate(item.InstanceId, item.StackCount, change.FinalCount));
                            }
                            else inserts[item.InstanceId] = item.ToRecord(change.Container, change.Placement, change.FinalCount);
                        }
                        _persistence.Persist(new InventoryMutationBatch(player.Identity.Instance,
                            inserts.Values.ToArray(), locations.Values.ToArray(), stacks,
                            nanos)
                        {
                            FinalStats = finalStats ?? [],
                            EmptyContainersBeforeRetire = changes.Where(change => change.Retired
                                && InventoryMoveService.IsBagItem(change.Item)).Select(change => change.Item.InstanceId).ToArray()
                        });
                        committed = true;
                        pending?.MarkNewlyPersisted();
                        foreach (InventoryRowChange change in changes)
                            if (!change.Retired) change.Item.IsPersisted = true;
                        publish();
                        successful = true;
                    }
                    catch (DatabaseCommitOutcomeUnknownException)
                    {
                        player.QuarantinePersistence();
                        throw;
                    }
                    catch
                    {
                        if (!committed)
                        {
                            if (pending != null) player.Inventory.RestoreDirty(pending);
                            player.RestoreDirtyUploadedNanos(nanos);
                        }
                        throw;
                    }
                });
            }
            catch (Exception exception)
            {
                if (committed || exception is DatabaseCommitOutcomeUnknownException || player.IsPersistenceQuarantined)
                {
                    player.QuarantinePersistence();
                    player.Session?.Close();
                }
                else _flush.NotifyDirty(player);
                _logger.Error(exception, "Inventory action failed; no success acknowledgement was sent.");
            }
            return successful;
        }
    }

    internal sealed record InventoryRowChange(Item Item, Identity Container, int Placement, int FinalCount, bool Retired = false);
}
