namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>Builds a placed height mesh from one style-room GNDA slice.</summary>
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
                    float y = gnda[index] * heightScale - yOffset;

                    int baseIndex = vertices.Count;
                    vertices.Add(PlaceLocal(new Vector3(x0, y, z0), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x1, y, z0), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x1, y, z1), placed, facing));
                    vertices.Add(PlaceLocal(new Vector3(x0, y, z1), placed, facing));
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

        static Vector3 PlaceLocal(Vector3 local, Vector3 placed, int facing)
        {
            return placed + DungeonRoomPlacer.RotateY(local, facing);
        }
    }
}
