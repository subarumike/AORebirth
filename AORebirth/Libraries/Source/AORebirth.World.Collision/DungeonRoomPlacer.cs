namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Client ACG room placement and <c>CalculateRoomHeights</c> Y-offset math.
    /// Grounded in Gamecode <c>FUN_100ca4d2</c> and N3 <c>RDBPlayfield_t::CalculateRoomHeights</c>.
    /// </summary>
    public static class DungeonRoomPlacer
    {
        public const int BlockWorldSize = 10;

        public static int MinFloor(IReadOnlyList<DungeonRoomSpec> rooms)
        {
            ArgumentNullException.ThrowIfNull(rooms);
            if (rooms.Count == 0)
                return 0;

            int min = rooms[0].Floor;
            for (int i = 1; i < rooms.Count; i++)
            {
                if (rooms[i].Floor < min)
                    min = rooms[i].Floor;
            }

            return min;
        }

        /// <summary>
        /// Minimum GNDA height in the room rect among tiles whose DCGA type <c>&amp; 0x7F != 0</c>.
        /// </summary>
        public static float ComputeYOffset(
            byte[] gnda,
            byte[] dcga,
            int mapWidth,
            int mapHeight,
            StyleRoomTemplate room,
            float heightScale)
        {
            ArgumentNullException.ThrowIfNull(gnda);
            ArgumentNullException.ThrowIfNull(dcga);
            ArgumentNullException.ThrowIfNull(room);
            if (mapWidth <= 0 || mapHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (heightScale <= 0f)
                heightScale = 1f;

            float minHeight = 65536f;
            bool found = false;
            int maxX = Math.Min(room.TileMaxX, mapWidth);
            int maxZ = Math.Min(room.TileMaxZ, mapHeight);
            int minX = Math.Max(room.TileMinX, 0);
            int minZ = Math.Max(room.TileMinZ, 0);
            for (int z = minZ; z < maxZ; z++)
            {
                int row = z * mapWidth;
                for (int x = minX; x < maxX; x++)
                {
                    int index = row + x;
                    if (index < 0 || index >= dcga.Length || index >= gnda.Length)
                        continue;
                    if ((dcga[index] & 0x7F) == 0)
                        continue;

                    float height = gnda[index] * heightScale;
                    if (height < minHeight)
                        minHeight = height;
                    found = true;
                }
            }

            return found ? minHeight : 0f;
        }

        public static Vector3 Place(
            StyleRoomTemplate template,
            DungeonRoomSpec spec,
            float tileSize,
            int widthBlocks,
            int heightBlocks,
            int floorHeight,
            int minFloor)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(spec);
            if (tileSize <= 0f)
                tileSize = 2f;

            int facing = spec.Facing & 3;
            int numTilesX = template.NumTilesX;
            int numTilesZ = template.NumTilesZ;
            int effectiveTiles = (facing & 1) != 0 ? numTilesX : numTilesZ;

            Vector3 offset = template.LocalOrigin * tileSize;
            offset += new Vector3(1f, 0f, 1f);
            offset = RotateY(offset, facing);
            ApplyFacingSizeBump(ref offset, facing, numTilesX, numTilesZ, tileSize);

            int roundedTileSize = (int)MathF.Round(tileSize);
            var grid = new Vector3(
                spec.GridX * BlockWorldSize,
                0f,
                heightBlocks * BlockWorldSize - spec.GridZ * BlockWorldSize - roundedTileSize * effectiveTiles);

            Vector3 placed = grid + offset;
            int floorDelta = spec.Floor - minFloor;
            if (floorHeight == 0)
                placed.X += floorDelta * (widthBlocks * BlockWorldSize + 100);

            placed.Y = template.TemplatePos.Y + floorDelta * floorHeight;
            return placed;
        }

        public static Vector3 RotateY(Vector3 value, int facingQuadrant)
        {
            return (facingQuadrant & 3) switch
            {
                1 => new Vector3(value.Z, value.Y, -value.X),
                2 => new Vector3(-value.X, value.Y, -value.Z),
                3 => new Vector3(-value.Z, value.Y, value.X),
                _ => value
            };
        }

        public static Vector3 TransformSurfaceVertex(
            Vector3 templateVertex,
            StyleRoomTemplate template,
            Vector3 placed,
            int facing)
        {
            ArgumentNullException.ThrowIfNull(template);
            Vector3 local = templateVertex - template.TemplatePos;
            int delta = (facing - template.TemplateFacing) & 3;
            return placed + RotateY(local, delta);
        }

        static void ApplyFacingSizeBump(
            ref Vector3 offset,
            int facing,
            int numTilesX,
            int numTilesZ,
            float tileSize)
        {
            if (facing == 1)
                offset.Z += numTilesX * tileSize;
            else if (facing == 2)
            {
                offset.X += numTilesX * tileSize;
                offset.Z += numTilesZ * tileSize;
            }
            else if (facing == 3)
                offset.X += numTilesZ * tileSize;
        }
    }
}
