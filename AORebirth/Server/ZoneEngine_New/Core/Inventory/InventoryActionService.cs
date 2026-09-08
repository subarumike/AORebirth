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
        readonly IItemTemplateCatalog _catalog;
        readonly IItemBuilder _items;

        public InventoryActionService(IInventoryMutationPersistence persistence, InventoryFlushService flush,
            IItemInstanceIdAllocator ids, IZoneLogger logger, IItemTemplateCatalog catalog, IItemBuilder items)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _items = items ?? throw new ArgumentNullException(nameof(items));
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
        {
            if (!TryResolveOwnedSlot(player, slot, out Container page, out Item item)
                || item.InstanceId != expectedInstanceId || IsPermanentGardenKey(item)) return false;
            if (InventoryMoveService.IsBagItem(item)
                && player.Inventory.TryGetBackpackPage(item.Identity, out Container bagPage)
                && bagPage.Content.Count != 0) return false;
            var graveyard = new Identity { Type = IdentityType.None, Instance = player.Identity.Instance };
            bool result = TryCommit(player,
                [new InventoryRowChange(item, graveyard, item.InstanceId, item.StackCount, Retired: true)],
                () => IsCurrent(page, slot.Instance, item, expectedInstanceId),
                () => page.Content.Remove(slot.Instance));
            if (result && page.Identity.Type.IsWearPage())
            {
                player.Rebase();
                player.OnEquipmentChanged(new EquipSlot(page.Identity.Type, slot.Instance));
            }
            return result;
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

        // These are accepted permanent passage keys, not arbitrary exemptions inferred from names.
        // Legacy NascenceStatueTeleportCatalog.IsPermanentGardenPassageItem and Thrak key rules.
        public static bool IsPermanentGardenKey(Item item)
            => item.LowId is 226994 or 226824 || item.HighId is 226994 or 226824;

        /// <summary>
        /// Capture-backed standalone package 301782 -> 301749 (20260806-rabbit).
        /// Mirrors the accepted Legacy main-inventory storage + Overflow presentation contract,
        /// but never consumes a package when the durable grant fails.
        /// </summary>
        public bool TryOpenQuabbit(Player player, Identity slot, Item sealedItem)
        {
            const int sealedId = 301782, openedId = 301749;
            if (player.Session?.State != SessionState.InPlay || player.IsPersistenceQuarantined || player.IsDead
                || (sealedItem.LowId != sealedId && sealedItem.HighId != sealedId)
                || sealedItem.StackCount != 1 || !TryResolveOwnedSlot(player, slot, out Container source, out Item current)
                || !ReferenceEquals(current, sealedItem) || !_catalog.TryGet(openedId, out _)) return false;
            bool alreadyOwned = player.Inventory.Inventory.Content.Values.Concat(player.Inventory.Overflow.Content.Values)
                .Any(i => i.LowId == openedId || i.HighId == openedId);
            Container destination = player.Inventory.Inventory;
            int destinationSlot = alreadyOwned ? -1 : destination.FindFreeSlot();
            if (!alreadyOwned && destinationSlot < 0) return false;
            Item? opened = alreadyOwned ? null : _items.Create(openedId, openedId, 1, ItemSource.Other, instanceId: _ids.Allocate());
            var changes = new List<InventoryRowChange>
            {
                new(sealedItem, new Identity { Type = IdentityType.None, Instance = player.Identity.Instance },
                    sealedItem.InstanceId, sealedItem.StackCount, Retired: true)
            };
            if (opened != null) changes.Add(new InventoryRowChange(opened, destination.Identity, destinationSlot, opened.StackCount));
            return TryCommit(player, changes,
                () => IsCurrent(source, slot.Instance, sealedItem, sealedItem.InstanceId)
                    && (opened == null || !destination.Content.ContainsKey(destinationSlot)),
                () =>
                {
                    if (opened != null)
                    {
                        if (!destination.Add(destinationSlot, opened)) throw new InvalidOperationException("Reserved Quabbit grant slot changed.");
                        player.Session?.Send(new TemplateActionMessage
                        {
                            Identity = player.Identity, ItemLowId = openedId, ItemHighId = openedId,
                            Quality = 1, Unknown1 = 1, Unknown2 = 87,
                            Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 }
                        });
                        player.Session?.Send(new ContainerAddItemMessage
                        {
                            Identity = player.Identity, SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                            Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance }, TargetPlacement = 0x6f
                        });
                    }
                    source.Content.Remove(slot.Instance);
                    player.Session?.Send(new TemplateActionMessage
                    {
                        Identity = player.Identity, ItemLowId = sealedId, ItemHighId = sealedId,
                        Quality = sealedItem.Quality > 0 ? sealedItem.Quality : 1, Unknown1 = 1,
                        Unknown2 = 3, Placement = slot, Unknown3 = 50000, Unknown4 = player.Identity.Instance
                    });
                    player.Session?.Send(new CharacterActionMessage
                    {
                        Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
                    });
                });
        }

        /// <summary>
        /// Existing UploadNano OnUse contract: consume one crystal and store all newly uploaded
        /// programs together. Mixed/specialized package effects are not silently approximated.
        /// </summary>
        public bool TryUseNanoCrystal(Player player, Identity slot, Item item)
        {
            if (!item.Can(CanFlags.Consume) || IsPermanentGardenKey(item)
                || !TryResolveOwnedSlot(player, slot, out Container page, out Item current)
                || !ReferenceEquals(current, item) || item.StackCount <= 0
                || !item.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count == 0
                || !item.Definition.MeetsActionRequirements(s => player.Stats.Get(s), ActionType.ToUse)) return false;
            var nanoIds = new List<int>();
            foreach (ItemSpell spell in spells)
            {
                if (spell.FunctionType != (int)FunctionType.UploadNano
                    || !ItemUseFunctions.CanExecute(player, spell)
                    || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int nanoId) || nanoId <= 0)
                    return false;
                if (!nanoIds.Contains(nanoId)) nanoIds.Add(nanoId);
            }
            int before = item.StackCount;
            bool retired = before == 1;
            Identity destination = retired ? new Identity { Type = IdentityType.None, Instance = player.Identity.Instance } : page.Identity;
            bool applied = TryCommit(player,
                [new InventoryRowChange(item, destination, retired ? item.InstanceId : slot.Instance, retired ? before : before - 1, retired)],
                () => IsCurrent(page, slot.Instance, item, item.InstanceId) && item.StackCount == before,
                () =>
                {
                    if (retired) page.Content.Remove(slot.Instance);
                    else item.StackCount = before - 1;
                    player.Session?.Send(new TemplateActionMessage
                    {
                        Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId,
                        Quality = item.Quality, Placement = slot, Unknown1 = 1, Unknown2 = 3
                    });
                    if (retired) player.Session?.Send(new CharacterActionMessage
                    {
                        Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
                    });
                    foreach (int nanoId in nanoIds)
                    {
                        if (!player.TryAddUploadedNano(nanoId)) continue;
                        player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity, Action = CharacterActionType.UploadNano,
                            Target = player.Identity, Parameter1 = (int)IdentityType.NanoProgram, Parameter2 = nanoId
                        });
                    }
                }, nanoIds);
            return applied;
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
            Func<bool> validate, Action publish, IReadOnlyList<int>? additionalNanoIds = null,
            IReadOnlyList<StatRecord>? finalStats = null)
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
                            else inserts[item.InstanceId] = ToRecord(item, change.Container, change.Placement, change.FinalCount);
                        }
                        _persistence.Persist(new InventoryMutationBatch(player.Identity.Instance,
                            inserts.Values.ToArray(), locations.Values.ToArray(), stacks,
                            additionalNanoIds == null ? nanos : nanos.Concat(additionalNanoIds).Distinct().ToArray())
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

        public static ItemInstanceRecord ToRecord(Item item, Identity container, int placement, int count)
            => new()
            {
                InstanceId = item.InstanceId, ContainerType = (int)container.Type,
                ContainerInstance = container.Instance, ContainerPlacement = placement,
                ItemType = item.Identity.Type != IdentityType.None ? (int)item.Identity.Type : item.Definition.ItemType,
                LowId = item.LowId, HighId = item.HighId, Quality = item.Quality, StackCount = count, Source = item.Source
            };
    }

    internal sealed record InventoryRowChange(Item Item, Identity Container, int Placement, int FinalCount, bool Retired = false);
}
