namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Builds a placed height mesh from one style-room GNDA slice.
    /// Indoor corners follow N3 <c>FUN_1001769d</c> when tilemap+0x18 != 0:
    /// the vertex at tile corner (x, z) is GNDA[x-1, z-1] times height scale.
    /// </summary>
    public static class DungeonGroundMesher
    {
        public static CollisionTriangleMesh? TryBuild(
            byte[] gnda,
            byte[] dcga,
            int mapWidth,
            int mapHeight,
            StyleRoomTemplate room,
            Vector3 placed,
            int facing,
            float tileSize,
            float heightScale,
            float yOffset,
            int roomIndex)
        {
            ArgumentNullException.ThrowIfNull(gnda);
            ArgumentNullException.ThrowIfNull(dcga);
            ArgumentNullException.ThrowIfNull(room);
            if (tileSize <= 0f)
                tileSize = 2f;
            if (heightScale <= 0f)
                heightScale = 1f;

            var vertices = new List<Vector3>();
            var triangles = new List<CollisionTriangle>();
            float startX = -room.LocalOrigin.X * tileSize - 1f;
            float startZ = -room.LocalOrigin.Z * tileSize - 1f;
            int maxX = Math.Min(room.TileMaxX, mapWidth);
            int maxZ = Math.Min(room.TileMaxZ, mapHeight);

            for (int z = room.TileMinZ; z < maxZ; z++)
            {
                int localZ = z - room.TileMinZ;
                int row = z * mapWidth;
                for (int x = room.TileMinX; x < maxX; x++)
                {
                    if (x < 0 || z < 0)
                        continue;

                    int index = row + x;
                    if (index < 0 || index >= dcga.Length || index >= gnda.Length)
                        continue;
                    if ((dcga[index] & 0x7F) == 0)
                        continue;

                    int localX = x - room.TileMinX;
                    float x0 = startX + localX * tileSize;
                    float z0 = startZ + localZ * tileSize;
                    float x1 = x0 + tileSize;
                    float z1 = z0 + tileSize;
                    float y00 = SampleHeight(gnda, mapWidth, x - 1, z - 1, heightScale) - yOffset;
                    float y10 = SampleHeight(gnda, mapWidth, x, z - 1, heightScale) - yOffset;
                    float y11 = SampleHeight(gnda, mapWidth, x, z, heightScale) - yOffset;
                    float y01 = SampleHeight(gnda, mapWidth, x - 1, z, heightScale) - yOffset;

                    int baseIndex = vertices.Count;
                    vertices.Add(PlaceLocal(new Vector3(x0, y00, z0), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x1, y10, z0), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x1, y11, z1), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x0, y01, z1), placed, facing));
                    triangles.Add(new CollisionTriangle(baseIndex, baseIndex + 2, baseIndex + 1));
                    triangles.Add(new CollisionTriangle(baseIndex, baseIndex + 3, baseIndex + 2));
                }
            }

            if (triangles.Count == 0)
                return null;

            return new CollisionTriangleMesh(
                vertices.ToArray(),
                triangles.ToArray(),
                cellId: roomIndex,
                source: "ground-room" + roomIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Indoor sample from N3 <c>FUN_10016454</c> / <c>FUN_1001769d</c>.
        /// Coordinates below 1 clamp to tile 0.
        /// </summary>
        static float SampleHeight(byte[] gnda, int mapWidth, int x, int z, float heightScale)
        {
            if (x < 1)
                x = 0;
            if (z < 1)
                z = 0;
            if (mapWidth <= 0)
                return 0f;

            int index = z * mapWidth + x;
            if ((uint)index >= (uint)gnda.Length)
                return 0f;

            return gnda[index] * heightScale;
        }

        static Vector3 PlaceLocal(Vector3 local, Vector3 placed, int facing)
        {
            return placed + DungeonRoomPlacer.RotateY(local, facing);
        }
    }
}
