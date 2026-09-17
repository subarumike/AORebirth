namespace AORebirth.World.Collision
{
    using System;
    using System.Numerics;

    /// <summary>World AABB for one placed dungeon room (static Rooms.json or ACG Place).</summary>
    public readonly struct DungeonRoomBounds
    {
        public DungeonRoomBounds(int index, Vector3 min, Vector3 max)
        {
            if (max.X < min.X || max.Y < min.Y || max.Z < min.Z)
                throw new ArgumentOutOfRangeException(nameof(max), "Room AABB is inverted.");

            Index = index;
            Min = min;
            Max = max;
            Vector3 size = max - min;
            Volume = Math.Max(size.X, 0.001f) * Math.Max(size.Y, 0.001f) * Math.Max(size.Z, 0.001f);
        }

        public int Index { get; }

        public Vector3 Min { get; }

        public Vector3 Max { get; }

        public float Volume { get; }

        public bool Contains(Vector3 position)
            => position.X >= Min.X
                && position.X <= Max.X
                && position.Y >= Min.Y
                && position.Y <= Max.Y
                && position.Z >= Min.Z
                && position.Z <= Max.Z;

        public float DistanceSquared(Vector3 position)
        {
            float dx = 0f;
            if (position.X < Min.X)
                dx = Min.X - position.X;
            else if (position.X > Max.X)
                dx = position.X - Max.X;

            float dy = 0f;
            if (position.Y < Min.Y)
                dy = Min.Y - position.Y;
            else if (position.Y > Max.Y)
                dy = position.Y - Max.Y;

            float dz = 0f;
            if (position.Z < Min.Z)
                dz = Min.Z - position.Z;
            else if (position.Z > Max.Z)
                dz = position.Z - Max.Z;

            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        public static DungeonRoomBounds FromPlaced(
            int index,
            StyleRoomTemplate template,
            Vector3 placed,
            int facing,
            float tileSize,
            float floorHeight)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (tileSize <= 0f)
                tileSize = 2f;

            float startX = -template.LocalOrigin.X * tileSize - 1f;
            float startZ = -template.LocalOrigin.Z * tileSize - 1f;
            float endX = startX + (template.NumTilesX * tileSize);
            float endZ = startZ + (template.NumTilesZ * tileSize);

            Vector3 c0 = PlaceCorner(startX, startZ, placed, facing);
            Vector3 c1 = PlaceCorner(endX, startZ, placed, facing);
            Vector3 c2 = PlaceCorner(endX, endZ, placed, facing);
            Vector3 c3 = PlaceCorner(startX, endZ, placed, facing);

            float minX = MathF.Min(MathF.Min(c0.X, c1.X), MathF.Min(c2.X, c3.X));
            float maxX = MathF.Max(MathF.Max(c0.X, c1.X), MathF.Max(c2.X, c3.X));
            float minZ = MathF.Min(MathF.Min(c0.Z, c1.Z), MathF.Min(c2.Z, c3.Z));
            float maxZ = MathF.Max(MathF.Max(c0.Z, c1.Z), MathF.Max(c2.Z, c3.Z));

            float yExtent = floorHeight > 0f ? floorHeight : Math.Max(16f, tileSize * 8f);
            return new DungeonRoomBounds(
                index,
                new Vector3(minX, placed.Y - 1f, minZ),
                new Vector3(maxX, placed.Y + yExtent, maxZ));
        }

        static Vector3 PlaceCorner(float localX, float localZ, Vector3 placed, int facing)
            => placed + DungeonRoomPlacer.RotateY(new Vector3(localX, 0f, localZ), facing);
    }
}
