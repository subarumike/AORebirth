namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    /// <summary>Collision meshes plus per-room AABBs for one dungeon (static or ACG instance).</summary>
    public sealed class DungeonWorldLayout
    {
        public DungeonWorldLayout(
            PlayfieldCollisionSet collision,
            IReadOnlyList<DungeonRoomBounds> rooms)
        {
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        public PlayfieldCollisionSet Collision { get; }

        public IReadOnlyList<DungeonRoomBounds> Rooms { get; }
    }
}
