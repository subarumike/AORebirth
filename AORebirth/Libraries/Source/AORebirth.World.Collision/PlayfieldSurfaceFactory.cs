namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    using N3Lite;
    using N3Lite.Surfaces;

    /// <summary>
    /// A playfield's client-style collision world: the terrain as an <c>n3TilemapSurface_t</c> with a
    /// <c>CellSurface_t</c> of static meshes hung off it, or the cell surface alone when there is no
    /// terrain (dungeons, indoor playfields).
    /// </summary>
    public sealed class PlayfieldSurface
    {
        internal PlayfieldSurface(ISurface root, TilemapSurface? terrain, CellSurface? cells, int triangles, int populatedCells, int outsideTriangles)
        {
            Root = root;
            Terrain = terrain;
            Cells = cells;
            TriangleCount = triangles;
            PopulatedCellCount = populatedCells;
            OutsideTriangleCount = outsideTriangles;
        }

        /// <summary>The surface every vehicle collides against.</summary>
        public ISurface Root { get; }

        public TilemapSurface? Terrain { get; }

        public CellSurface? Cells { get; }

        /// <summary>Static triangles placed into cells, counting a triangle once per cell it touches.</summary>
        public int TriangleCount { get; }

        public int PopulatedCellCount { get; }

        /// <summary>Triangles lying wholly outside the cell grid, which no query can reach.</summary>
        public int OutsideTriangleCount { get; }
    }

    /// <summary>Builds a <see cref="PlayfieldSurface"/> from engine-agnostic collision data.</summary>
    public static class PlayfieldSurfaceFactory
    {
        /// <summary>
        /// Builds the surface for <paramref name="collision"/>, or null when it has no geometry at all.
        /// This stands in for Lost-Eden's Unity-bound <c>PlayfieldTileSurface</c>: the terrain comes from
        /// <see cref="TerrainHeightfield"/> and every cell is filled up front, because the server does
        /// not stream cells by locality.
        /// </summary>
        public static PlayfieldSurface? Build(PlayfieldCollisionSet? collision)
        {
            if (collision == null || !collision.HasCollision)
                return null;

            TilemapSurface? terrain = null;
            float cellSize = DefaultCellSize;
            float extentX = 0f;
            float extentZ = 0f;
            if (collision.Terrain != null && TerrainTileHeightSource.TryCreate(collision.Terrain, out TerrainTileHeightSource? tiles))
            {
                terrain = new TilemapSurface(tiles!);
                cellSize = CellSurface.TilesPerCell * tiles!.TileSize;
                extentX = tiles.Width * tiles.TileSize;
                extentZ = tiles.Height * tiles.TileSize;
            }

            IReadOnlyList<CollisionTriangleMesh> meshes = collision.SurfaceMeshes;
            for (int m = 0; m < meshes.Count; m++)
            {
                foreach (Vector3 v in meshes[m].Vertices)
                {
                    extentX = MathF.Max(extentX, v.X);
                    extentZ = MathF.Max(extentZ, v.Z);
                }
            }

            CellSurface? cells = null;
            int triangles = 0;
            int populated = 0;
            int outside = 0;
            if (meshes.Count > 0 && extentX > 0f && extentZ > 0f)
            {
                // Square cells: GridSpace walks both axes with one cell size.
                int cellsX = Math.Max(1, (int)MathF.Ceiling(extentX / cellSize));
                int cellsZ = Math.Max(1, (int)MathF.Ceiling(extentZ / cellSize));
                cells = new CellSurface(cellsX, cellsZ, cellsX * cellSize, cellsZ * cellSize);
                FillCells(cells, meshes, cellSize, out triangles, out populated, out outside);
            }

            if (terrain != null)
            {
                terrain.Child = cells;
                return new PlayfieldSurface(terrain, terrain, cells, triangles, populated, outside);
            }

            return cells == null
                ? null
                : new PlayfieldSurface(cells, null, cells, triangles, populated, outside);
        }

        /// <summary>Cell size for playfields without a tilemap: stock's 10 tiles at the common scale 4.</summary>
        public const float DefaultCellSize = 40f;

        /// <summary>
        /// Duplicates each triangle into every cell its XZ bounds touch, as stock records are, and
        /// builds one <see cref="TriangleMeshSurface"/> per populated cell. One surface per cell matters:
        /// <see cref="CellSurface.CalculateClosestPoint"/> only asks the first surface in a cell.
        /// </summary>
        static void FillCells(
            CellSurface cells,
            IReadOnlyList<CollisionTriangleMesh> meshes,
            float cellSize,
            out int triangles,
            out int populated,
            out int outside)
        {
            triangles = 0;
            populated = 0;
            outside = 0;
            var perCell = new List<Vec3>?[cells.CellCount];
            float worldX = cells.CellsX * cellSize;
            float worldZ = cells.CellsZ * cellSize;

            for (int m = 0; m < meshes.Count; m++)
            {
                Vector3[] vertices = meshes[m].Vertices;
                CollisionTriangle[] tris = meshes[m].Triangles;
                for (int t = 0; t < tris.Length; t++)
                {
                    CollisionTriangle tri = tris[t];
                    if ((uint)tri.A >= (uint)vertices.Length
                        || (uint)tri.B >= (uint)vertices.Length
                        || (uint)tri.C >= (uint)vertices.Length)
                        continue;

                    Vector3 a = vertices[tri.A];
                    Vector3 b = vertices[tri.B];
                    Vector3 c = vertices[tri.C];
                    float minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
                    float maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
                    float minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
                    float maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));
                    if (maxX < 0f || maxZ < 0f || minX > worldX || minZ > worldZ)
                    {
                        outside++;
                        continue;
                    }

                    int x0 = Math.Clamp((int)MathF.Floor(minX / cellSize), 0, cells.CellsX - 1);
                    int x1 = Math.Clamp((int)MathF.Floor(maxX / cellSize), 0, cells.CellsX - 1);
                    int z0 = Math.Clamp((int)MathF.Floor(minZ / cellSize), 0, cells.CellsZ - 1);
                    int z1 = Math.Clamp((int)MathF.Floor(maxZ / cellSize), 0, cells.CellsZ - 1);
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        for (int cx = x0; cx <= x1; cx++)
                        {
                            List<Vec3> list = perCell[(cz * cells.CellsX) + cx] ??= new List<Vec3>();
                            list.Add(new Vec3(a.X, a.Y, a.Z));
                            list.Add(new Vec3(b.X, b.Y, b.Z));
                            list.Add(new Vec3(c.X, c.Y, c.Z));
                            triangles++;
                        }
                    }
                }
            }

            for (int cell = 0; cell < perCell.Length; cell++)
            {
                List<Vec3>? list = perCell[cell];
                if (list == null)
                    continue;

                Vec3[] verts = list.ToArray();
                var indices = new int[verts.Length];
                for (int i = 0; i < indices.Length; i++)
                    indices[i] = i;

                var surface = new TriangleMeshSurface(verts, indices);
                cells.SetSurfaceForCell(cell, surface);
                populated++;
            }
        }
    }

    /// <summary>
    /// <see cref="ITileHeightSource"/> over a normalized <see cref="TerrainHeightfield"/>. Chunks
    /// share their edge samples, so global sample <c>x</c> lives in chunk <c>x / step</c> at
    /// <c>x % step</c>, with the far edge read from the last chunk's final sample.
    /// </summary>
    public sealed class TerrainTileHeightSource : ITileHeightSource
    {
        readonly float[,]?[,] _chunks;
        readonly int _stepX;
        readonly int _stepZ;
        readonly int _gridX;
        readonly int _gridZ;
        readonly float _heightScale;

        TerrainTileHeightSource(float[,]?[,] chunks, int stepX, int stepZ, float tileSize, float heightScale)
        {
            _chunks = chunks;
            _stepX = stepX;
            _stepZ = stepZ;
            _gridX = chunks.GetLength(0);
            _gridZ = chunks.GetLength(1);
            _heightScale = heightScale;
            TileSize = tileSize;
            Width = _gridX * stepX;
            Height = _gridZ * stepZ;
        }

        public int Width { get; }

        public int Height { get; }

        public float TileSize { get; }

        public static bool TryCreate(TerrainHeightfield terrain, out TerrainTileHeightSource? source)
        {
            ArgumentNullException.ThrowIfNull(terrain);
            source = null;
            if (terrain.Chunks.Count == 0)
                return false;

            float tileSize = terrain.TileSize > 0f ? terrain.TileSize : 1f;
            float heightScale = terrain.HeightScale > 0f ? terrain.HeightScale : 1f;
            float[,] first = terrain.Chunks[0].Heights;
            int stepX = first.GetLength(0) - 1;
            int stepZ = first.GetLength(1) - 1;
            if (stepX < 1 || stepZ < 1)
                return false;

            int gridX = 0;
            int gridZ = 0;
            var placed = new List<(int X, int Z, float[,] Heights)>(terrain.Chunks.Count);
            foreach (TerrainHeightChunk chunk in terrain.Chunks)
            {
                int gx = (int)MathF.Round(chunk.OriginX / (stepX * tileSize));
                int gz = (int)MathF.Round(chunk.OriginZ / (stepZ * tileSize));
                if (gx < 0 || gz < 0)
                    continue;

                placed.Add((gx, gz, chunk.Heights));
                gridX = Math.Max(gridX, gx + 1);
                gridZ = Math.Max(gridZ, gz + 1);
            }

            if (placed.Count == 0)
                return false;

            var chunks = new float[,]?[gridX, gridZ];
            foreach ((int x, int z, float[,] heights) in placed)
                chunks[x, z] = heights;

            source = new TerrainTileHeightSource(chunks, stepX, stepZ, tileSize, heightScale);
            return true;
        }

        public float SampleHeight(int x, int z)
        {
            if (x < 0 || z < 0)
                return 0f;

            int cx = Math.Min(x / _stepX, _gridX - 1);
            int cz = Math.Min(z / _stepZ, _gridZ - 1);
            int lx = x - (cx * _stepX);
            int lz = z - (cz * _stepZ);
            float[,]? heights = _chunks[cx, cz];
            if (heights == null || lx >= heights.GetLength(0) || lz >= heights.GetLength(1))
                return 0f;

            return heights[lx, lz] * _heightScale;
        }
    }
}
