namespace AORebirth.World.Collision
{
    using System;

    using Vector3 = System.Numerics.Vector3;

    /// <summary>
    /// Where one template room was placed in a dungeon: the template, its placed origin and facing. Anything authored
    /// in the style playfield's world space for that room (surfaces, district spawn points) moves into the dungeon
    /// with <see cref="TransformTemplatePoint"/>, the same transform the room's collision surfaces use.
    /// </summary>
    public sealed class DungeonRoomPlacement
    {
        public DungeonRoomPlacement(int index, StyleRoomTemplate template, Vector3 placed, int facing)
        {
            Index = index;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Placed = placed;
            Facing = facing & 3;
        }

        /// <summary>Room (cell) index in the layout.</summary>
        public int Index { get; }

        public StyleRoomTemplate Template { get; }

        public Vector3 Placed { get; }

        public int Facing { get; }

        /// <summary>A point in the style playfield's world space, moved and turned into this placed room.</summary>
        public Vector3 TransformTemplatePoint(Vector3 templatePoint)
            => DungeonRoomPlacer.TransformSurfaceVertex(templatePoint, Template, Placed, Facing);
    }
}
