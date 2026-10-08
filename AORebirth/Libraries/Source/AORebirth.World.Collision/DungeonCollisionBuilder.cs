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
            var listed = new List<KeyValuePair<int, IReadOnlyList<int>>>(catalog.Rooms.Count);
            for (int i = 0; i < catalog.Rooms.Count; i++)
                listed.Add(new KeyValuePair<int, IReadOnlyList<int>>(catalog.Rooms[i].InstanceId, catalog.Rooms[i].LinkedRooms));
            return new DungeonWorldLayout(
                new PlayfieldCollisionSet(id, meshes, terrain: null),
                rooms,
                roomLinks: DungeonWorldLayout.SymmetricLinks(listed));
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
            var templates = new List<StyleRoomTemplate>(rooms.Count);
            var placements = new List<DungeonRoomPlacement>(rooms.Count);
            int minFloor = DungeonRoomPlacer.MinFloor(rooms);
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomSpec spec = rooms[i] ?? throw new ArgumentNullException(nameof(rooms));
                StyleRoomTemplate template = LookupTemplate(catalog, spec.RoomId);
                templates.Add(template);
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
                placements.Add(new DungeonRoomPlacement(i, template, placed, spec.Facing & 3));
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
            return new DungeonWorldLayout(
                new PlayfieldCollisionSet(id, meshes, terrain: null),
                bounds,
                PlaceDoors(rooms, templates, bounds, catalog.TileSize),
                placements);
        }

        /// <summary>Two door connections closer than this (units) are the same doorway.</summary>
        const float DoorMatchDistance = 0.3f;

        /// <summary>
        /// Live doors sit this far (units) off the shared room edge along their heading, just inside the room that owns
        /// them (e.g. 10.001, 19.999, 60.001). Exactly on the edge the client's Door_t::LinkDoorToRooms may find no room
        /// for the door and remove it.
        /// </summary>
        const float DoorEdgeOffset = 0.001f;

        static Vector3 OffsetAlongYaw(float x, float y, float z, int yawDegrees)
        {
            double radians = yawDegrees * Math.PI / 180.0;
            return new Vector3(x + (float)(Math.Sin(radians) * DoorEdgeOffset), y, z + (float)(Math.Cos(radians) * DoorEdgeOffset));
        }

        readonly struct DoorPoint
        {
            public DoorPoint(int room, float x, float z, float y, int yaw)
            {
                Room = room;
                X = x;
                Z = z;
                Y = y;
                Yaw = yaw;
            }

            public int Room { get; }

            public float X { get; }

            public float Z { get; }

            public float Y { get; }

            public int Yaw { get; }

            public bool Meets(in DoorPoint other)
                => MathF.Abs(X - other.X) < DoorMatchDistance && MathF.Abs(Z - other.Z) < DoorMatchDistance;
        }

        /// <summary>
        /// Doors of an ACG layout, in the live order: the exit (the entrance room's connection that meets no other
        /// room), then for each room in generator order, each of its connections (template order) that meets a
        /// connection of an earlier room. A connection sits on the middle of its tile's edge, rotated into place by the
        /// room's facing; unpaired connections other than the exit stay walls.
        /// Verified against every door of the five captured live dungeons (90 of 90, none extra).
        /// </summary>
        static IReadOnlyList<DungeonDoorPlacement> PlaceDoors(
            IReadOnlyList<DungeonRoomSpec> rooms,
            IReadOnlyList<StyleRoomTemplate> templates,
            IReadOnlyList<DungeonRoomBounds> bounds,
            float tileSize)
        {
            var byRoom = new List<DoorPoint>[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                StyleRoomTemplate template = templates[i];
                int facing = rooms[i].Facing & 3;
                int w = template.NumTilesX;
                int h = template.NumTilesZ;
                var points = new List<DoorPoint>(template.DoorPosRots.Count);
                foreach (int posRot in template.DoorPosRots)
                {
                    int tile = posRot >> 2;
                    int rotation = posRot & 3;
                    int cx = w > 0 ? tile % w : 0;
                    int cz = w > 0 ? tile / w : 0;
                    (float u, float v) = rotation switch
                    {
                        0 => (cx + 0.5f, cz + 1f),
                        1 => (cx + 1f, cz + 0.5f),
                        2 => (cx + 0.5f, (float)cz),
                        _ => ((float)cx, cz + 0.5f)
                    };
                    (float x, float z) = facing switch
                    {
                        0 => (u, v),
                        1 => (v, w - u),
                        2 => (w - u, h - v),
                        _ => (h - v, u)
                    };
                    int yaw = (180 + (90 * ((rotation + facing) & 3))) % 360;
                    points.Add(new DoorPoint(i, bounds[i].Min.X + (x * tileSize), bounds[i].Min.Z + (z * tileSize), bounds[i].Min.Y + 1f, yaw));
                }

                byRoom[i] = points;
            }

            var doors = new List<DungeonDoorPlacement>();
            if (rooms.Count == 0)
                return doors;

            foreach (DoorPoint point in byRoom[0])
            {
                if (!MeetsAnyOther(byRoom, point))
                {
                    doors.Add(new DungeonDoorPlacement(OffsetAlongYaw(point.X, point.Y, point.Z, point.Yaw), point.Yaw, room: -1, linkedRoom: 0));
                    break;
                }
            }

            for (int i = 1; i < rooms.Count; i++)
            {
                foreach (DoorPoint point in byRoom[i])
                {
                    for (int j = 0; j < i; j++)
                    {
                        bool paired = false;
                        foreach (DoorPoint other in byRoom[j])
                        {
                            if (!point.Meets(other))
                                continue;

                            doors.Add(new DungeonDoorPlacement(OffsetAlongYaw(point.X, point.Y, point.Z, point.Yaw), point.Yaw, room: i, linkedRoom: j));
                            paired = true;
                            break;
                        }

                        if (paired)
                            break;
                    }
                }
            }

            return doors;
        }

        static bool MeetsAnyOther(List<DoorPoint>[] byRoom, in DoorPoint point)
        {
            for (int j = 0; j < byRoom.Length; j++)
            {
                if (j == point.Room)
                    continue;
                foreach (DoorPoint other in byRoom[j])
                {
                    if (point.Meets(other))
                        return true;
                }
            }

            return false;
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
