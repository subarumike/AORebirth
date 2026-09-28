namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    /// <summary>Collision meshes plus per-room AABBs for one dungeon (static or ACG instance).</summary>
    public sealed class DungeonWorldLayout
    {
        public DungeonWorldLayout(
            PlayfieldCollisionSet collision,
            IReadOnlyList<DungeonRoomBounds> rooms,
            IReadOnlyList<DungeonDoorPlacement>? doors = null,
            IReadOnlyList<DungeonRoomPlacement>? placements = null)
        {
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            Doors = doors ?? Array.Empty<DungeonDoorPlacement>();
            Placements = placements ?? Array.Empty<DungeonRoomPlacement>();
        }

        public PlayfieldCollisionSet Collision { get; }

        public IReadOnlyList<DungeonRoomBounds> Rooms { get; }

        /// <summary>Doors of a generated (ACG) layout, exit first; empty for static playfields.</summary>
        public IReadOnlyList<DungeonDoorPlacement> Doors { get; }

        /// <summary>How each room of a generated (ACG) layout was placed, in room order; empty for static playfields.</summary>
        public IReadOnlyList<DungeonRoomPlacement> Placements { get; }
    }
}
