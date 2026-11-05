namespace ZoneEngine_New.Core.Inventory
{
    using System;

    using AORebirth.Enums;
    using AORebirth.Modules.Events;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Modules;
    using ZoneEngine_New.Core.Playfield;

    using AoVector3 = AORebirth.Core.Vector.Vector3;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    /// <summary>
    /// Dropping inventory items into the world and picking world items up. A drop retires the item's inventory row and
    /// places a temporary <see cref="WorldItem"/> only the dropper can take back; a pickup grants a fresh inventory row.
    /// World spawns (Carbonrich Rock) are ownerless and can be taken by anyone. Both need the template's Carry flag.
    /// </summary>
    public static class WorldItemTransfers
    {
        /// <summary>Every drop attempt locks Computer Literacy this long.</summary>
        public const int DropLockSeconds = 2;

        /// <summary>A dropped item stays in the world this long.</summary>
        public static readonly TimeSpan DroppedItemLifetime = TimeSpan.FromMinutes(2);

        /// <summary>Radius of the sphere swept from the dropper's eyes to the drop point.</summary>
        public const float DropSphereRadius = 0.15f;

        /// <summary>The drop point may be at most this far from the dropper.</summary>
        public const float MaxDropDistance = 10f;

        /// <summary>The sweep runs this far past the requested point, so a point on the surface still hits it.</summary>
        const float DropOvershoot = 1f;

        /// <summary>
        /// The client's DropTemplate: drop the item in <paramref name="slot"/> at <paramref name="requested"/>. Locks
        /// Computer Literacy for <see cref="DropLockSeconds"/> on every attempt. The sweep from the dropper's eyes must hit
        /// something, or the drop is refused.
        /// </summary>
        public static bool TryDrop(Player player, Identity slot, Vector3 requested, InventoryActionService inventoryActions,
            IZoneLogger? logger)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(requested);
            ArgumentNullException.ThrowIfNull(inventoryActions);

            Playfield? playfield = player.Playfield;
            if (playfield == null || player.IsDead || player.IsPersistenceQuarantined || player.Session == null)
                return false;

            DateTime now = DateTime.UtcNow;
            int computerLiteracy = (int)CharacterStat.ComputerLiteracy;
            TimeSpan remaining = player.SkillLocks.Remaining(computerLiteracy, now);
            if (remaining > TimeSpan.Zero)
            {
                player.SendSkillLocked(computerLiteracy, remaining);
                return false;
            }

            // The client scales the duration by SkillLockModifier itself: it is told the unscaled one.
            player.ShowSkillLock(computerLiteracy, DropLockSeconds, player.LockSkill(computerLiteracy, DropLockSeconds, now));

            if (slot.Type != IdentityType.Inventory
                || !InventoryActionService.TryResolveOwnedSlot(player, slot, out _, out Item item))
                return Refuse(player, logger, "item not droppable from " + slot);

            if (DropRefusal(item) is { } feedback)
            {
                Helpers.ClientFeedback.Send(player, feedback);
                return Refuse(player, logger, feedback + " template=" + item.LowId);
            }

            if (!TryPlace(player, playfield, requested, out AoVector3 rest, out AoVector3 normal))
                return Refuse(player, logger, "drop ray hit nothing");

            if (!inventoryActions.TryDelete(player, slot, item.InstanceId, ItemPlaceKind.World))
                return Refuse(player, logger, "inventory row could not be retired");

            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
            });
            playfield.GetRequiredService<SpawnService>().SpawnDroppedItem(item, rest, AlignToSurface(player.Rotation, normal), player.Identity,
                DroppedItemLifetime);
            return true;
        }

        /// <summary>
        /// The client's ClientGetItem on a world item: within reach, Carry-flagged and ownerless or the player's own drop.
        /// The item is granted as a new inventory row first; the world item only leaves once that is durable.
        /// </summary>
        public static bool TryPickup(Player player, WorldItem worldItem, InventoryActionService inventoryActions,
            IItemBuilder items, IZoneLogger? logger)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(worldItem);
            ArgumentNullException.ThrowIfNull(inventoryActions);
            ArgumentNullException.ThrowIfNull(items);

            Playfield? playfield = player.Playfield;
            if (playfield == null || !ReferenceEquals(worldItem.Playfield, playfield) || player.IsDead
                || player.IsPersistenceQuarantined || player.Session == null || !player.Inventory.IsHydrated
                || !worldItem.CanBePickedUpBy(player) || worldItem.GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
                return false;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            if (!registry.TryGet(worldItem.Identity, out Dynel? registered) || !ReferenceEquals(registered, worldItem))
                return false;

            Container page = player.Inventory.Inventory;
            int placement = page.FindFreeSlot();
            if (placement < 0)
                return false;

            Item item = items.CreateWithNewInstance(worldItem.LowId, worldItem.HighId, worldItem.Quality, ItemSource.Other);
            item.StackCount = Math.Max(1, worldItem.StackCount);
            if (UniqueItems.WouldDuplicate(player, item, out string uniqueName))
            {
                Helpers.ClientFeedback.SendFormatted(player, Helpers.ClientFeedback.AlreadyHaveUniqueItem, uniqueName);
                return false;
            }
            bool committed = inventoryActions.TryCommit(player,
                [new InventoryRowChange(item, page.Identity, placement, item.StackCount)],
                () => !page.Content.ContainsKey(placement) && registry.TryGet(worldItem.Identity, out Dynel? still)
                    && ReferenceEquals(still, worldItem),
                () =>
                {
                    if (!page.Add(placement, item))
                        throw new InvalidOperationException("Reserved pickup slot changed.");
                });
            if (!committed)
                return false;

            ModuleHooks.ItemMoved(player, item, ModuleHooks.Place(ItemPlaceKind.World, worldItem.Identity),
                ModuleHooks.Place(ItemPlaceKind.Inventory));

            // AddTemplate lands in the client's first free slot, which is the one reserved here.
            player.Session?.Send(new AddTemplateMessage
            {
                Identity = player.Identity, LowId = item.LowId, HighId = item.HighId, Quality = item.Quality,
                Count = item.StackCount
            });

            // A spawn point's item comes back on its own timer; a drop simply goes.
            if (!playfield.GetRequiredService<HashSpawnSystem>().TryDespawnStaticAsDeath(worldItem))
                playfield.GetRequiredService<SpawnService>().DespawnStatic(worldItem);
            logger?.Info("World item picked up by char=" + player.Identity.Instance + " item=" + worldItem.Identity.Instance
                + " template=" + worldItem.LowId + " count=" + item.StackCount);
            return true;
        }

        /// <summary>
        /// Sweeps a small sphere from the dropper's eyes through the requested point (a little past it, so a point on the
        /// surface still hits) and rests it on what it hits. Too far a point or a miss refuses the drop.
        /// </summary>
        static bool TryPlace(Player player, Playfield playfield, Vector3 requested, out AoVector3 rest,
            out AoVector3 normal)
        {
            rest = player.Position;
            normal = new AoVector3(0, 1, 0);
            if (!float.IsFinite(requested.X) || !float.IsFinite(requested.Y) || !float.IsFinite(requested.Z))
                return false;
            if (playfield.WorldAccess.Instance is not { } world)
                return false;

            AoVector3 feet = player.Position;
            var eye = new AoVector3(feet.x, feet.y + Movement.MovementConfig.LineOfSightEyeHeight, feet.z);
            double dx = requested.X - eye.x, dy = requested.Y - eye.y, dz = requested.Z - eye.z;
            double length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            double horizontal = Math.Sqrt(((requested.X - feet.x) * (requested.X - feet.x)) + ((requested.Z - feet.z) * (requested.Z - feet.z)));
            if (horizontal > MaxDropDistance || length < 1e-3)
                return false;

            double scale = (length + DropOvershoot) / length;
            var end = new AoVector3(eye.x + (dx * scale), eye.y + (dy * scale), eye.z + (dz * scale));
            if (!world.TrySphereCast(eye, end, DropSphereRadius, out AoVector3 centre, out normal))
                return false;

            // The sphere's centre sits off the surface; the item stands on it, so probe back along the normal.
            double probe = DropSphereRadius * 3;
            var below = new AoVector3(centre.x - (normal.x * probe), centre.y - (normal.y * probe), centre.z - (normal.z * probe));
            if (world.TryLineCast(centre, below, out AoVector3 surface, out AoVector3 surfaceNormal))
            {
                rest = surface;
                normal = surfaceNormal;
            }
            else
            {
                rest = new AoVector3(centre.x - (normal.x * DropSphereRadius), centre.y - (normal.y * DropSphereRadius),
                    centre.z - (normal.z * DropSphereRadius));
            }

            return true;
        }

        /// <summary>The dropper's heading, tilted so the item's up axis lies along the surface <paramref name="normal"/>.</summary>
        static Quaternion AlignToSurface(Quaternion heading, AoVector3 normal)
        {
            // Shortest arc from +Y to the normal: axis up x n = (nz, 0, -nx), w = 1 + ny.
            Quaternion tilt = normal.y < -0.9999
                ? new Quaternion(1, 0, 0, 0)
                : new Quaternion(normal.z, 0, -normal.x, 1 + normal.y);
            tilt = (Quaternion)tilt.Normalize();
            return (Quaternion)Quaternion.Hamilton(tilt, heading).Normalize();
        }

        /// <summary>
        /// The client's own drop gate (Gamecode.dll N3Msg_IsDropPossible, 0x10048d42), on the template's stats: the item
        /// needs Carry and flag bit 0x1000 (0x100808b4, else reason 0x12), and must be neither Unique (reason 0xf) nor
        /// NoDrop (reason 0x11). Returns the feedback N3Msg_DropItem shows for the refusal, or null when droppable.
        /// </summary>
        static string? DropRefusal(Item item)
        {
            int flags = item.Definition?.Stats.TryGetValue(CharacterStat.Flags, out int value) == true ? value : 0;
            if (!item.Can(CanFlags.Carry) || (flags & (int)ItemFlags.Stationary) == 0
                || (flags & (int)ItemFlags.Unique) != 0)
                return "Feedback_ItemCantBeDroppedAtThatLocation";
            if ((flags & (int)ItemFlags.NoDrop) != 0)
                return "Feedback_ThisIsANoDropItem";
            return null;
        }

        static bool Refuse(Player player, IZoneLogger? logger, string reason)
        {
            logger?.Info("Drop refused char=" + player.Identity.Instance + " reason=" + reason);
            return false;
        }
    }
}
