namespace AORebirth.DungeonGenerator
{
    /// <summary>
    /// A door after placement, in floor-local half-tile units (one half tile is one world unit, a block is ten), and
    /// its world facing: 0 +Z, 1 +X, 2 -Z, 3 -X.
    /// </summary>
    public readonly record struct PlacedDoor(int X, int Z, int Facing)
    {
        /// <summary>The block across the door (the one it opens into).</summary>
        public (int X, int Z) FarBlock => Facing switch
        {
            0 => (Geometry.FloorDiv(X, 10), Geometry.FloorDiv(Z + 1, 10)),
            1 => (Geometry.FloorDiv(X + 1, 10), Geometry.FloorDiv(Z, 10)),
            2 => (Geometry.FloorDiv(X, 10), Geometry.FloorDiv(Z - 1, 10)),
            _ => (Geometry.FloorDiv(X - 1, 10), Geometry.FloorDiv(Z, 10))
        };

        /// <summary>The block on the door's own side.</summary>
        public (int X, int Z) NearBlock => Facing switch
        {
            0 => (Geometry.FloorDiv(X, 10), Geometry.FloorDiv(Z - 1, 10)),
            1 => (Geometry.FloorDiv(X - 1, 10), Geometry.FloorDiv(Z, 10)),
            2 => (Geometry.FloorDiv(X, 10), Geometry.FloorDiv(Z + 1, 10)),
            _ => (Geometry.FloorDiv(X + 1, 10), Geometry.FloorDiv(Z, 10))
        };

        public bool Meets(PlacedDoor other) => other.X == X && other.Z == Z && other.Facing == ((Facing + 2) & 3);
    }

    /// <summary>A template room placed on a floor: block position (floor-local, min corner), quarter turns, footprint and doors.</summary>
    public sealed class PlacedRoom
    {
        internal PlacedRoom(RoomShape shape, int floor, int blockX, int blockZ, int rotation)
        {
            Shape = shape;
            Floor = floor;
            BlockX = blockX;
            BlockZ = blockZ;
            Rotation = rotation & 3;
            (BlocksX, BlocksZ) = Geometry.RotatedBlocks(shape, Rotation);
            Cells = Geometry.PlaceFootprint(shape, Rotation, blockX, blockZ);
            Doors = Geometry.PlaceDoors(shape, Rotation, blockX, blockZ);
        }

        public RoomShape Shape { get; }

        public int Floor { get; }

        public int BlockX { get; }

        public int BlockZ { get; }

        public int Rotation { get; }

        /// <summary>Bounding size after rotation, in blocks.</summary>
        public int BlocksX { get; }

        public int BlocksZ { get; }

        /// <summary>Blocks the room really fills.</summary>
        public IReadOnlyList<(int X, int Z)> Cells { get; }

        public IReadOnlyList<PlacedDoor> Doors { get; }
    }

    /// <summary>
    /// Room placement maths, identical to the client's builder (Gamecode.dll FUN_100ca6f8) and the server's
    /// DungeonCollisionBuilder: a quarter turn maps a room-local point (x, z) of a w x h room to
    /// r0 (x, z), r1 (z, w - x), r2 (w - x, h - z), r3 (h - z, x), and turns each door's facing by the same amount.
    /// </summary>
    public static class Geometry
    {
        public static int FloorDiv(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

        public static (int X, int Z) Rotate(int x, int z, int w, int h, int rotation) => (rotation & 3) switch
        {
            0 => (x, z),
            1 => (z, w - x),
            2 => (w - x, h - z),
            _ => (h - z, x)
        };

        public static (int X, int Z) RotatedBlocks(RoomShape shape, int rotation)
            => (rotation & 1) == 0 ? (shape.BlocksX, shape.BlocksZ) : (shape.BlocksZ, shape.BlocksX);

        public static List<(int X, int Z)> PlaceFootprint(RoomShape shape, int rotation, int blockX, int blockZ)
        {
            var cells = new List<(int, int)>();
            int w2 = shape.BlocksX * 2;
            int h2 = shape.BlocksZ * 2;
            for (int i = 0; i < shape.BlocksX; i++)
            {
                for (int j = 0; j < shape.BlocksZ; j++)
                {
                    if (!shape.Footprint[i, j])
                        continue;
                    (int cx, int cz) = Rotate((2 * i) + 1, (2 * j) + 1, w2, h2, rotation);
                    cells.Add((blockX + ((cx - 1) / 2), blockZ + ((cz - 1) / 2)));
                }
            }

            return cells;
        }

        /// <summary>A door sits on the middle of its tile's edge: facing 0 the high-z edge, 1 high-x, 2 low-z, 3 low-x.</summary>
        public static List<PlacedDoor> PlaceDoors(RoomShape shape, int rotation, int blockX, int blockZ)
        {
            var doors = new List<PlacedDoor>(shape.Doors.Count);
            int w = shape.TilesX * 2;
            int h = shape.TilesZ * 2;
            foreach (RoomDoor door in shape.Doors)
            {
                (int u, int v) = door.Facing switch
                {
                    0 => ((2 * door.TileX) + 1, (2 * door.TileZ) + 2),
                    1 => ((2 * door.TileX) + 2, (2 * door.TileZ) + 1),
                    2 => ((2 * door.TileX) + 1, 2 * door.TileZ),
                    _ => (2 * door.TileX, (2 * door.TileZ) + 1)
                };
                (int x, int z) = Rotate(u, v, w, h, rotation);
                doors.Add(new PlacedDoor((blockX * 10) + x, (blockZ * 10) + z, (door.Facing + rotation) & 3));
            }

            return doors;
        }

        /// <summary>Unit direction of a facing, in (x, z).</summary>
        public static (int X, int Z) Direction(int facing) => (facing & 3) switch
        {
            0 => (0, 1),
            1 => (1, 0),
            2 => (0, -1),
            _ => (-1, 0)
        };
    }
}
