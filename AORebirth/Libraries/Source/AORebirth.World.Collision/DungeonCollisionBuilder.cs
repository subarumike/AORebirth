namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AODB.Common.RDBObjects;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = System.Numerics.Vector3;

    /// <summary>
    /// Assembles dungeon collision from a style playfield and an ACG room list.
    /// </summary>
    public static class DungeonCollisionBuilder
    {
        public static PlayfieldCollisionSet Build(string gameDataRoot, AcgBuildingGeneratorData generator)
            => BuildLayout(gameDataRoot, generator).Collision;

        public static PlayfieldCollisionSet Build(
            string gameDataRoot,
            int style,
            IReadOnlyList<DungeonRoomSpec> rooms,
            int widthBlocks,
            int heightBlocks,
            int floorHeight)
            => BuildLayout(
                DungeonStyleCatalog.Load(gameDataRoot, style),
                rooms,
                widthBlocks,
                heightBlocks,
                floorHeight,
                playfieldId: 0).Collision;

        public static PlayfieldCollisionSet Build(
            DungeonStyleCatalog catalog,
            IReadOnlyList<DungeonRoomSpec> rooms,
            int widthBlocks,
            int heightBlocks,
            int floorHeight)
            => BuildLayout(catalog, rooms, widthBlocks, heightBlocks, floorHeight, playfieldId: 0).Collision;

        public static bool TryBuildStatic(
            string gameDataRoot,
            int playfieldId,
            out DungeonWorldLayout? layout)
        {
            layout = null;
            if (!DungeonStyleCatalog.TryLoad(gameDataRoot, playfieldId, out DungeonStyleCatalog? catalog)
                || catalog == null)
                return false;

            layout = BuildStatic(catalog, playfieldId);
            return layout.Rooms.Count > 0;
        }

        public static DungeonWorldLayout BuildLayout(string gameDataRoot, AcgBuildingGeneratorData generator)
        {
            ArgumentNullException.ThrowIfNull(generator);
            return BuildLayout(
                DungeonStyleCatalog.Load(gameDataRoot, generator.Style),
                MapRooms(generator.Rooms),
                generator.Width,
                generator.Height,
                generator.RoomsPerFloor,
                playfieldId: 0);
        }

        public static DungeonWorldLayout BuildStatic(DungeonStyleCatalog catalog, int playfieldId = 0)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            var rooms = new List<DungeonRoomBounds>(catalog.Rooms.Count);
            var meshes = new List<CollisionTriangleMesh>();
            for (int i = 0; i < catalog.Rooms.Count; i++)
            {
                StyleRoomTemplate template = catalog.Rooms[i];
                int facing = template.TemplateFacing & 3;
                AppendPlacedRoom(
                    catalog,
                    template,
                    template.TemplatePos,
                    facing,
                    floorHeight: 0,
                    i,
                    template.InstanceId,
                    meshes,
                    rooms);
            }

            int id = playfieldId > 0 ? playfieldId : catalog.StyleId;
            return new DungeonWorldLayout(new PlayfieldCollisionSet(id, meshes, terrain: null), rooms);
        }

        public static DungeonWorldLayout BuildLayout(
            DungeonStyleCatalog catalog,
            IReadOnlyList<DungeonRoomSpec> rooms,
            int widthBlocks,
            int heightBlocks,
            int floorHeight,
            int playfieldId)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(rooms);
            if (widthBlocks < 0 || heightBlocks < 0)
                throw new ArgumentOutOfRangeException(nameof(widthBlocks));

            var meshes = new List<CollisionTriangleMesh>();
            var bounds = new List<DungeonRoomBounds>(rooms.Count);
            int minFloor = DungeonRoomPlacer.MinFloor(rooms);
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomSpec spec = rooms[i] ?? throw new ArgumentNullException(nameof(rooms));
                StyleRoomTemplate template = LookupTemplate(catalog, spec.RoomId);
                if (!template.IsAcgSizeValid)
                {
                    throw new InvalidDataException(
                        "Style room "
                        + spec.RoomId.ToString(CultureInfo.InvariantCulture)
                        + " has invalid ACG size "
                        + template.NumTilesX.ToString(CultureInfo.InvariantCulture)
                        + "x"
                        + template.NumTilesZ.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }

                Vector3 placed = DungeonRoomPlacer.Place(
                    template,
                    spec,
                    catalog.TileSize,
                    widthBlocks,
                    heightBlocks,
                    floorHeight,
                    minFloor);
                AppendPlacedRoom(
                    catalog,
                    template,
                    placed,
                    spec.Facing & 3,
                    floorHeight,
                    i,
                    i,
                    meshes,
                    bounds);
            }

            int id = playfieldId > 0 ? playfieldId : catalog.StyleId;
            return new DungeonWorldLayout(new PlayfieldCollisionSet(id, meshes, terrain: null), bounds);
        }

        static void AppendPlacedRoom(
            DungeonStyleCatalog catalog,
            StyleRoomTemplate template,
            Vector3 placed,
            int facing,
            float floorHeight,
            int meshIndex,
            int cellId,
            List<CollisionTriangleMesh> meshes,
            List<DungeonRoomBounds> rooms)
        {
            rooms.Add(
                DungeonRoomBounds.FromPlaced(
                    cellId,
                    template,
                    placed,
                    facing,
                    catalog.TileSize,
                    floorHeight));

            if (catalog.Gnda.Length == 0 || catalog.Dcga.Length == 0)
                return;

            float yOffset = DungeonRoomPlacer.ComputeYOffset(
                catalog.Gnda,
                catalog.Dcga,
                catalog.MapWidth,
                catalog.MapHeight,
                template,
                catalog.HeightScale);
            CollisionTriangleMesh? ground = DungeonGroundMesher.TryBuild(
                catalog.Gnda,
                catalog.Dcga,
                catalog.MapWidth,
                catalog.MapHeight,
                template,
                placed,
                facing,
                catalog.TileSize,
                catalog.HeightScale,
                yOffset,
                meshIndex);
            if (ground != null)
                meshes.Add(ground);

            AppendRoomSurfaces(catalog, template, placed, facing, meshIndex, meshes);
        }

        static StyleRoomTemplate LookupTemplate(DungeonStyleCatalog catalog, ushort roomId)
        {
            for (int i = 0; i < catalog.Rooms.Count; i++)
            {
                if (catalog.Rooms[i].InstanceId == roomId)
                    return catalog.Rooms[i];
            }

            if (roomId < catalog.Rooms.Count)
                return catalog.Rooms[roomId];

            throw new InvalidDataException(
                "RoomId "
                + roomId.ToString(CultureInfo.InvariantCulture)
                + " is outside style catalog size "
                + catalog.Rooms.Count.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        static void AppendRoomSurfaces(
            DungeonStyleCatalog catalog,
            StyleRoomTemplate template,
            Vector3 placed,
            int facing,
            int roomIndex,
            List<CollisionTriangleMesh> meshes)
        {
            if (!catalog.SurfacePayloads.TryGetValue(template.InstanceId, out byte[]? payload)
                || payload == null
                || payload.Length == 0)
                return;

            var raw = new List<CollisionTriangleMesh>();
            try
            {
                SurfaceResource surface = RdbObjectDeserializer.Deserialize<SurfaceResource>(
                    payload,
                    "Surfaces.dat#room" + template.InstanceId.ToString(CultureInfo.InvariantCulture));
                SurfaceResourceNormalizer.AppendMeshes(surface, template.InstanceId, raw);
            }
            catch
            {
                return;
            }

            string source = "surface-room" + roomIndex.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < raw.Count; i++)
            {
                Vector3[] verts = raw[i].Vertices;
                var transformed = new Vector3[verts.Length];
                for (int v = 0; v < verts.Length; v++)
                {
                    transformed[v] = DungeonRoomPlacer.TransformSurfaceVertex(
                        verts[v],
                        template,
                        placed,
                        facing);
                }

                meshes.Add(new CollisionTriangleMesh(transformed, raw[i].Triangles, roomIndex, source));
            }
        }

        static DungeonRoomSpec[] MapRooms(BuildingRoomInfo[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DungeonRoomSpec>();

            var rooms = new DungeonRoomSpec[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                BuildingRoomInfo room = source[i] ?? new BuildingRoomInfo();
                rooms[i] = new DungeonRoomSpec
                {
                    RoomId = room.RoomId,
                    Floor = room.Floor,
                    GridX = room.GridX,
                    GridZ = room.GridZ,
                    Facing = room.Facing
                };
            }

            return rooms;
        }
    }
}
