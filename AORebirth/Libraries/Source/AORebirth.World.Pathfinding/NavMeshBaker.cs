namespace AORebirth.World.Pathfinding
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using AORebirth.World.Collision;

    using DotRecast.Recast;
    using DotRecast.Recast.Geom;
    using DotRecast.Detour;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class NavMeshBaker
    {
        const int WalkablePolyFlags = 1;
        const int MaxPolysPerTile = 32768;

        public static NavMeshBakeResult Bake(PlayfieldCollisionSet collision, NavMeshBuildSettings? settings = null)
        {
            ArgumentNullException.ThrowIfNull(collision);
            settings ??= NavMeshBuildSettings.CreateDefault();
            settings.Validate();

            CollisionMeshFlattener.Flatten(collision, out float[] vertices, out int[] triangles, out int triangleCount);
            if (triangleCount <= 0)
            {
                throw new InvalidDataException(
                    "Playfield "
                    + collision.PlayfieldId
                    + " has no collision triangles to bake.");
            }

            vertices = PadVerticalBounds(vertices, settings);
            var geom = new RcSampleInputGeomProvider(vertices, triangles);
            RcConfig config = CreateConfig(settings);
            var builder = new RcBuilder();
            List<RcBuilderResult> tiles = builder.BuildTiles(geom, config, keepInterResults: false, buildAll: true, threads: 0);

            var navParams = new DtNavMeshParams
            {
                orig = geom.GetMeshBoundsMin(),
                tileWidth = settings.TileSizeVoxels * settings.CellSize,
                tileHeight = settings.TileSizeVoxels * settings.CellSize,
                maxTiles = Math.Max(tiles.Count, 1),
                maxPolys = MaxPolysPerTile
            };

            var mesh = new DtNavMesh();
            mesh.Init(navParams, settings.VertsPerPoly);

            int added = 0;
            int tilesWithPolys = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                RcPolyMesh? polyMesh = tiles[i].Mesh;
                if (polyMesh != null && polyMesh.npolys > 0)
                    tilesWithPolys++;

                DtMeshData? data = CreateTile(tiles[i], config, settings);
                if (data == null)
                    continue;

                DtStatus status = mesh.AddTile(data, 0, 0, out _);
                if (status.Failed())
                {
                    throw new InvalidDataException(
                        "Detour rejected navmesh tile "
                        + tiles[i].TileX
                        + ","
                        + tiles[i].TileZ
                        + " for playfield "
                        + collision.PlayfieldId
                        + ".");
                }

                added++;
            }

            if (added == 0)
            {
                throw new InvalidDataException(
                    "Recast produced no walkable tiles for playfield "
                    + collision.PlayfieldId
                    + " (sourceTriangles="
                    + triangleCount
                    + " recastTiles="
                    + tiles.Count
                    + " tilesWithPolys="
                    + tilesWithPolys
                    + ").");
            }

            return new NavMeshBakeResult(collision.PlayfieldId, triangleCount, added, mesh, settings);
        }

        public static NavMeshBakeResult Bake(string gameDataRoot, int playfieldId, NavMeshBuildSettings? settings = null)
            => Bake(PlayfieldCollisionResolver.Resolve(gameDataRoot, playfieldId), settings);

        public static NavMeshBakeResult Bake(
            string gameDataRoot,
            AcgBuildingGeneratorData generator,
            NavMeshBuildSettings? settings = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            ArgumentNullException.ThrowIfNull(generator);
            return Bake(DungeonCollisionBuilder.Build(gameDataRoot, generator), settings);
        }

        static float[] PadVerticalBounds(float[] vertices, NavMeshBuildSettings settings)
        {
            if (vertices.Length < 3)
                return vertices;

            float maxY = vertices[1];
            for (int i = 1; i < vertices.Length; i += 3)
                maxY = MathF.Max(maxY, vertices[i]);

            float pad = settings.AgentHeight + settings.AgentMaxClimb + (settings.CellHeight * 4f);
            var padded = new float[vertices.Length + 3];
            Array.Copy(vertices, padded, vertices.Length);
            padded[vertices.Length] = vertices[0];
            padded[vertices.Length + 1] = maxY + pad;
            padded[vertices.Length + 2] = vertices[2];
            return padded;
        }

        static RcConfig CreateConfig(NavMeshBuildSettings settings)
        {
            float cell = settings.CellSize;
            float minRegion = settings.RegionMinSize * settings.RegionMinSize * cell * cell;
            float mergeRegion = settings.RegionMergeSize * settings.RegionMergeSize * cell * cell;
            return new RcConfig(
                useTiles: true,
                tileSizeX: settings.TileSizeVoxels,
                tileSizeZ: settings.TileSizeVoxels,
                borderSize: RcConfig.CalcBorder(settings.AgentRadius, cell),
                partition: RcPartition.WATERSHED,
                cellSize: cell,
                cellHeight: settings.CellHeight,
                agentMaxSlope: settings.AgentMaxSlope,
                agentHeight: settings.AgentHeight,
                agentRadius: settings.AgentRadius,
                agentMaxClimb: settings.AgentMaxClimb,
                minRegionArea: minRegion,
                mergeRegionArea: mergeRegion,
                edgeMaxLen: settings.EdgeMaxLen,
                edgeMaxError: settings.EdgeMaxError,
                vertsPerPoly: settings.VertsPerPoly,
                detailSampleDist: settings.DetailSampleDist,
                detailSampleMaxError: settings.DetailSampleMaxError,
                filterLowHangingObstacles: true,
                filterLedgeSpans: true,
                filterWalkableLowHeightSpans: true,
                walkableAreaMod: new RcAreaModification(RcRecast.RC_WALKABLE_AREA),
                buildMeshDetail: true);
        }

        static DtMeshData? CreateTile(RcBuilderResult tile, RcConfig config, NavMeshBuildSettings settings)
        {
            RcPolyMesh? poly = tile.Mesh;
            if (poly == null || poly.nverts <= 0 || poly.npolys <= 0)
                return null;

            if (poly.flags == null || poly.flags.Length < poly.npolys)
                poly.flags = new int[poly.npolys];
            for (int i = 0; i < poly.npolys; i++)
                poly.flags[i] = WalkablePolyFlags;

            RcPolyMeshDetail? detail = tile.MeshDetail;
            var create = new DtNavMeshCreateParams
            {
                verts = poly.verts,
                vertCount = poly.nverts,
                polys = poly.polys,
                polyAreas = poly.areas,
                polyFlags = poly.flags,
                polyCount = poly.npolys,
                nvp = poly.nvp,
                detailMeshes = detail?.meshes,
                detailVerts = detail?.verts,
                detailVertsCount = detail?.nverts ?? 0,
                detailTris = detail?.tris,
                detailTriCount = detail?.ntris ?? 0,
                walkableHeight = settings.AgentHeight,
                walkableRadius = settings.AgentRadius,
                walkableClimb = settings.AgentMaxClimb,
                bmin = poly.bmin,
                bmax = poly.bmax,
                cs = config.Cs,
                ch = config.Ch,
                buildBvTree = true,
                tileX = tile.TileX,
                tileZ = tile.TileZ
            };

            return DtNavMeshBuilder.CreateNavMeshData(create);
        }
    }
}
