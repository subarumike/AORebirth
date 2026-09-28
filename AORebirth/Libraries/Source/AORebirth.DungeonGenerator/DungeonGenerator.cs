namespace AORebirth.DungeonGenerator
{
    using System.Collections.Concurrent;
    using Vector3 = System.Numerics.Vector3;

    using AORebirth.World.Collision;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>What to build.</summary>
    public sealed class DungeonGenerationRequest
    {
        /// <summary>Style (template) playfield whose rooms are the building blocks, e.g. 324 AutocontentClan(dung).</summary>
        public int StyleId { get; init; }

        public int Seed { get; init; }

        /// <summary>Floors (1-8). Each is its own cluster of rooms; the client lays floors side by side.</summary>
        public int FloorCount { get; init; } = 1;

        /// <summary>
        /// Target rooms per floor (3-40), capping rooms included; it also sizes the grid. Mainhalls fill several
        /// blocks, so the block area varies with what the seed picks.
        /// </summary>
        public int FloorSize { get; init; } = 12;

        /// <summary>Chance (0-100) that an interior door spawns locked; see <see cref="DungeonDoorLocks"/>.</summary>
        public int LockedDoorChancePercent { get; init; }

        /// <summary>Building identity instance stamped into the generator (the ACG entrance it is entered from).</summary>
        public int BuildingInstance { get; init; }
    }

    /// <summary>A generated dungeon: the generator the client builds from, plus the placed rooms for tools and checks.</summary>
    public sealed class DungeonGenerationResult
    {
        public required DungeonGenerationRequest Request { get; init; }

        public required AcgBuildingGeneratorData Generator { get; init; }

        public required IReadOnlyList<PlacedRoom> Rooms { get; init; }

        public int GridWidth { get; init; }

        public int GridHeight { get; init; }

        /// <summary>Arrival point: just inside the way-in door (floor 0 world coordinates).</summary>
        public Vector3 Spawn { get; init; }

        /// <summary>Generation attempts used (retries run with a seed derived from the request's).</summary>
        public int Attempts { get; init; }

        public required DungeonValidation Validation { get; init; }
    }

    /// <summary>Deterministic door locks for a generated dungeon: the same seed, door and chance always agree.</summary>
    public static class DungeonDoorLocks
    {
        /// <summary>True when door <paramref name="doorIndex"/> (0 is the way in, never locked) spawns locked.</summary>
        public static bool IsLocked(int seed, int doorIndex, int chancePercent)
        {
            if (doorIndex <= 0 || chancePercent <= 0)
                return false;
            if (chancePercent >= 100)
                return true;

            ulong z = unchecked(((ulong)(uint)seed << 32) ^ (uint)doorIndex ^ 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            return (int)(z % 100UL) < chancePercent;
        }
    }

    /// <summary>
    /// Builds a random ACG dungeon from a style playfield's rooms. The result is laid out on the client's block grid
    /// so that it passes the client's own legality test (see <see cref="DungeonLayoutValidator"/>): every door pairs
    /// with a door facing it, except doors that face an empty block (sealed) and the single way in.
    /// <para>
    /// Floor 0 starts from an entrance room on the grid's west edge, its way-in door facing out of the grid. Other
    /// floors start from a start room in the middle. Rooms with two or more doors are fitted onto random open
    /// doorways until the floor reaches its size; then every remaining doorway is capped with a one-door room where
    /// one fits. A candidate fits only when its footprint is free, none of its doors face out of the grid, and every
    /// door it touches pairs up. Floors are placed side by side by the client (generator floor height 0); moving
    /// between them (teleporters) is not placed here.
    /// </para>
    /// </summary>
    public sealed class DungeonGenerator
    {
        public const int MinFloorSize = 3;

        public const int MaxFloorSize = 40;

        public const int MaxFloors = 8;

        /// <summary>The client's grid indices are bytes below 33.</summary>
        public const int MaxGridBlocks = 32;

        const int MaxAttempts = 200;

        static readonly ConcurrentDictionary<(string Root, int Style), StyleRoomSet> StyleCache = new();

        readonly string _gameDataRoot;

        public DungeonGenerator(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            _gameDataRoot = gameDataRoot;
        }

        /// <summary>The style's rooms as building blocks (loaded once per style).</summary>
        public StyleRoomSet LoadStyle(int styleId)
            => StyleCache.GetOrAdd((_gameDataRoot, styleId), key => StyleRoomSet.Load(key.Root, key.Style));

        public DungeonGenerationResult Generate(DungeonGenerationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            int floors = Math.Clamp(request.FloorCount, 1, MaxFloors);
            int floorSize = Math.Clamp(request.FloorSize, MinFloorSize, MaxFloorSize);
            int grid = GridSize(floorSize);
            StyleRoomSet style = LoadStyle(request.StyleId);

            DungeonGenerationResult? best = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var random = new Random(unchecked(request.Seed + (attempt * 7919)));
                var builder = new LayoutBuilder(style, grid, grid, random);
                if (!builder.TryBuild(floors, floorSize))
                    continue;

                DungeonValidation validation = DungeonLayoutValidator.Validate(builder.Rooms, grid, grid);
                if (!validation.IsValid)
                    continue;

                best = Complete(request, style, builder, grid, attempt + 1, validation);
                break;
            }

            return best ?? throw new InvalidOperationException(
                "No valid dungeon for style " + request.StyleId + " seed " + request.Seed + " after " + MaxAttempts + " attempts.");
        }

        /// <summary>Grid side in blocks: room to grow the target room count, within the client's limit.</summary>
        public static int GridSize(int floorSize) => Math.Clamp(6 + floorSize, 10, MaxGridBlocks);

        static DungeonGenerationResult Complete(DungeonGenerationRequest request, StyleRoomSet style, LayoutBuilder builder,
            int grid, int attempts, DungeonValidation validation)
        {
            // Live generators list rooms breadth first from the entrance: by floor, then by depth. The entrance stays first.
            List<PlacedRoom> ordered = Enumerable.Range(0, builder.Rooms.Count)
                .OrderBy(i => builder.Rooms[i].Floor).ThenBy(i => builder.Depths[i]).ThenBy(i => i)
                .Select(i => builder.Rooms[i]).ToList();
            var infos = new BuildingRoomInfo[ordered.Count];
            for (int i = 0; i < infos.Length; i++)
            {
                PlacedRoom room = ordered[i];
                infos[i] = new BuildingRoomInfo
                {
                    RoomId = (ushort)room.Shape.RoomId,
                    Floor = (sbyte)room.Floor,
                    GridX = (byte)room.BlockX,
                    // The client's grid z counts down from the far edge: world block z = height - gridZ - depth.
                    GridZ = (byte)(grid - room.BlockZ - room.BlocksZ),
                    Facing = (byte)room.Rotation
                };
            }

            var generator = new AcgBuildingGeneratorData
            {
                Identity = new Identity { Type = (IdentityType)0xC7A1, Instance = request.BuildingInstance },
                DbObjectVersion = 1,
                Version = 3,
                Width = (ushort)grid,
                Height = (ushort)grid,
                // The client uses this as the floor height. Live single-floor dungeons send 64; with more floors it
                // stays 0 so the client lays each floor beside the previous one rather than above it.
                RoomsPerFloor = (ushort)(request.FloorCount > 1 ? 0 : 64),
                Style = request.StyleId,
                AmbientRed = 100,
                AmbientGreen = 100,
                AmbientBlue = 100,
                Rooms = infos
            };

            PlacedRoom entrance = ordered[0];
            PlacedDoor way = entrance.Doors[builder.WayInDoor];
            (int dx, int dz) = Geometry.Direction((way.Facing + 2) & 3);
            const float Inside = 2.5f;
            var spawn = new Vector3(way.X + (dx * Inside), entrance.Shape.Template.TemplatePos.Y + 0.01f, way.Z + (dz * Inside));

            return new DungeonGenerationResult
            {
                Request = request,
                Generator = generator,
                Rooms = ordered,
                GridWidth = grid,
                GridHeight = grid,
                Spawn = spawn,
                Attempts = attempts,
                Validation = validation
            };
        }

        /// <summary>One layout attempt.</summary>
        sealed class LayoutBuilder
        {
            readonly StyleRoomSet _style;
            readonly int _width;
            readonly int _height;
            readonly Random _random;
            readonly List<PlacedRoom> _rooms = [];
            readonly List<int> _depths = [];

            /// <summary>Chance (percent) that growth extends the deepest open doorway rather than any open one: long wings.</summary>
            const int ExtendWingPercent = 70;

            // Per floor: which room fills each block, and the doorways still open (keyed by position and facing).
            readonly List<int[,]> _occupancy = [];
            readonly List<Dictionary<(int X, int Z, int Facing), (int Room, int Door)>> _open = [];

            public LayoutBuilder(StyleRoomSet style, int width, int height, Random random)
            {
                _style = style;
                _width = width;
                _height = height;
                _random = random;
            }

            public List<PlacedRoom> Rooms => _rooms;

            /// <summary>Doors from the floor's first room, per room.</summary>
            public List<int> Depths => _depths;

            /// <summary>Index of the entrance room's way-in door.</summary>
            public int WayInDoor { get; private set; } = -1;

            public bool TryBuild(int floors, int floorSize)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    var occupancy = new int[_width, _height];
                    for (int x = 0; x < _width; x++)
                        for (int z = 0; z < _height; z++)
                            occupancy[x, z] = -1;
                    _occupancy.Add(occupancy);
                    _open.Add([]);

                    int first = _rooms.Count;
                    if (!(floor == 0 ? TryPlaceEntrance() : TryPlaceStart(floor)) || !TryPlaceMainHalls(floor, first, floorSize))
                        return false;

                    Grow(floor, first, floorSize);
                    Cap(floor);
                    if (MainHallsOn(floor, first) != MainHallQuota(floorSize))
                        return false;
                }

                return WayInDoor >= 0;
            }

            /// <summary>
            /// Main halls per floor: one per ten rooms, rounded, at least one. Live dungeons match it: 10, 11 and 13
            /// rooms had one main hall; 17, 20 and 24 rooms had two.
            /// </summary>
            internal static int MainHallQuota(int floorSize) => Math.Max(1, (int)Math.Round(floorSize / 10.0, MidpointRounding.AwayFromZero));

            int MainHallsOn(int floor, int first)
            {
                int count = 0;
                for (int i = first; i < _rooms.Count; i++)
                {
                    if (_rooms[i].Floor == floor && _style.IsMainHall(_rooms[i].Shape))
                        count++;
                }

                return count;
            }

            bool TryPlaceEntrance()
            {
                var options = new List<(RoomShape Shape, int Rotation, int Door)>();
                foreach (RoomShape shape in _style.Entrances)
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        // Only the entrance's mapped exit door may be the way in (its doorway is modelled with the
                        // outside plug), and it must face west (-X) off the grid's west edge: the room's min-x side.
                        int d = shape.ExitDoor;
                        PlacedDoor door = Geometry.PlaceDoors(shape, rotation, 0, 0)[d];
                        if (door.Facing == 3 && door.X == 0)
                            options.Add((shape, rotation, d));
                    }

                Shuffle(options);
                foreach ((RoomShape shape, int rotation, int door) in options)
                {
                    (_, int depth) = Geometry.RotatedBlocks(shape, rotation);
                    int z = _random.Next(Math.Max(1, _height - depth + 1));
                    if (TryFit(0, shape, rotation, 0, z, wayInDoor: door, out Fit fit))
                    {
                        Commit(fit);
                        WayInDoor = door;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// The first room past the floor's first room is always a main hall (on floor 0 every inward doorway of the
            /// entrance opens into one), and the floor's other main halls hang off that first one, as in live layouts.
            /// </summary>
            bool TryPlaceMainHalls(int floor, int root, int floorSize)
            {
                var rootSlots = SlotsOf(floor, root);
                if (rootSlots.Count == 0)
                    return false;
                if (floor > 0)
                    rootSlots = [rootSlots[_random.Next(rootSlots.Count)]];

                int firstHall = -1;
                foreach (var slot in rootSlots)
                {
                    if (!_open[floor].ContainsKey(slot))
                        continue;
                    if (!TryFillSlot(floor, slot, _style.MainHalls))
                        return false;
                    if (firstHall < 0)
                        firstHall = _rooms.Count - 1;
                }

                int quota = MainHallQuota(floorSize);
                while (firstHall >= 0 && MainHallsOn(floor, root) < quota)
                {
                    var slots = SlotsOf(floor, firstHall);
                    Shuffle(slots);
                    if (!slots.Any(slot => TryFillSlot(floor, slot, _style.MainHalls)))
                        return false;
                }

                return firstHall >= 0;
            }

            List<(int X, int Z, int Facing)> SlotsOf(int floor, int room)
                => _open[floor].Where(pair => pair.Value.Room == room).Select(pair => pair.Key).ToList();

            bool TryPlaceStart(int floor)
            {
                var pool = _style.Starts.Count > 0 ? _style.Starts : _style.Growth;
                var options = new List<(RoomShape Shape, int Rotation)>();
                foreach (RoomShape shape in pool)
                    for (int rotation = 0; rotation < 4; rotation++)
                        options.Add((shape, rotation));
                Shuffle(options);
                foreach ((RoomShape shape, int rotation) in options)
                {
                    (int w, int h) = Geometry.RotatedBlocks(shape, rotation);
                    if (TryFit(floor, shape, rotation, (_width - w) / 2, (_height - h) / 2, wayInDoor: -1, out Fit fit))
                    {
                        Commit(fit);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Grows the floor into wings: most steps extend the deepest open doorway, so branches run out from the main
            /// hall instead of filling in around it; the rest pick any open doorway so wings also fork. Rooms with doors
            /// (hubs, start rooms included) grow the floor.
            /// </summary>
            void Grow(int floor, int first, int floorSize)
            {
                int stalls = 0;
                while (_rooms.Count - first < floorSize && _open[floor].Count > 0 && stalls < 64)
                {
                    List<(int X, int Z, int Facing)> slots;
                    bool extendWing = _random.Next(100) < ExtendWingPercent;
                    if (extendWing)
                    {
                        int deepest = _open[floor].Values.Max(owner => _depths[owner.Room]);
                        slots = _open[floor].Where(pair => _depths[pair.Value.Room] == deepest).Select(pair => pair.Key).ToList();
                    }
                    else
                        slots = _open[floor].Keys.ToList();
                    var slot = slots[_random.Next(slots.Count)];

                    // Near the target, cap some doorways so the floor stops branching; a wing being extended rarely ends.
                    bool cap = _rooms.Count - first >= floorSize - 1 || _random.Next(100) < (extendWing ? 10 : 35);
                    if (TryFillSlot(floor, slot, cap ? _style.Terminators : _style.Hubs)
                        || (!cap && TryFillSlot(floor, slot, _style.Terminators)))
                        stalls = 0;
                    else
                    {
                        // Nothing fits here: leave it facing empty space (the client seals it).
                        _open[floor].Remove(slot);
                        stalls++;
                    }
                }
            }

            void Cap(int floor)
            {
                foreach (var slot in _open[floor].Keys.ToList())
                {
                    if (_open[floor].ContainsKey(slot) && !TryFillSlot(floor, slot, _style.Terminators))
                        _open[floor].Remove(slot);
                }
            }

            bool TryFillSlot(int floor, (int X, int Z, int Facing) slot, IReadOnlyList<RoomShape> pool)
            {
                int wanted = (slot.Facing + 2) & 3;
                var options = new List<Fit>();
                foreach (RoomShape shape in pool)
                {
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        List<PlacedDoor> local = Geometry.PlaceDoors(shape, rotation, 0, 0);
                        foreach (PlacedDoor door in local)
                        {
                            if (door.Facing != wanted)
                                continue;
                            int dx = slot.X - door.X;
                            int dz = slot.Z - door.Z;
                            if (dx % 10 != 0 || dz % 10 != 0)
                                continue;
                            if (TryFit(floor, shape, rotation, dx / 10, dz / 10, wayInDoor: -1, out Fit fit))
                                options.Add(fit);
                        }
                    }
                }

                if (options.Count == 0)
                    return false;

                Commit(options[_random.Next(options.Count)]);
                return true;
            }

            sealed record Fit(PlacedRoom Room, List<(int X, int Z, int Facing)> Pairs, List<(int X, int Z, int Facing, int Door)> NewSlots, int Depth);

            bool TryFit(int floor, RoomShape shape, int rotation, int blockX, int blockZ, int wayInDoor, out Fit fit)
            {
                fit = null!;
                var room = new PlacedRoom(shape, floor, blockX, blockZ, rotation);
                int[,] occupancy = _occupancy[floor];
                var cells = new HashSet<(int, int)>();
                foreach ((int x, int z) in room.Cells)
                {
                    if (x < 0 || z < 0 || x >= _width || z >= _height || occupancy[x, z] >= 0)
                        return false;
                    cells.Add((x, z));
                }

                var open = _open[floor];
                var pairs = new List<(int, int, int)>();
                var newSlots = new List<(int, int, int, int)>();
                for (int d = 0; d < room.Doors.Count; d++)
                {
                    PlacedDoor door = room.Doors[d];
                    (int fx, int fz) = door.FarBlock;
                    if (cells.Contains((fx, fz)))
                        return false;
                    bool inGrid = fx >= 0 && fz >= 0 && fx < _width && fz < _height;
                    if (!inGrid)
                    {
                        if (d != wayInDoor)
                            return false;
                        continue;
                    }

                    if (occupancy[fx, fz] >= 0)
                    {
                        var partner = (door.X, door.Z, (door.Facing + 2) & 3);
                        if (!open.ContainsKey(partner))
                            return false;
                        pairs.Add(partner);
                    }
                    else
                        newSlots.Add((door.X, door.Z, door.Facing, d));
                }

                // Depth: one past the nearest room it joins (rooms are listed by it, and wings grow from the deepest).
                int depth = pairs.Count == 0 ? 0 : pairs.Min(pair => _depths[open[pair].Room]) + 1;

                // Every open doorway this room covers must be answered by one of its doors.
                foreach (var slot in open.Keys)
                {
                    var target = new PlacedDoor(slot.X, slot.Z, slot.Facing).FarBlock;
                    if (cells.Contains(target) && !pairs.Contains(slot))
                        return false;
                }

                fit = new Fit(room, pairs, newSlots, depth);
                return true;
            }

            void Commit(Fit fit)
            {
                int index = _rooms.Count;
                _rooms.Add(fit.Room);
                _depths.Add(fit.Depth);
                int[,] occupancy = _occupancy[fit.Room.Floor];
                foreach ((int x, int z) in fit.Room.Cells)
                    occupancy[x, z] = index;
                var open = _open[fit.Room.Floor];
                foreach (var pair in fit.Pairs)
                    open.Remove(pair);
                foreach ((int x, int z, int facing, int door) in fit.NewSlots)
                    open[(x, z, facing)] = (index, door);
            }

            void Shuffle<T>(List<T> items)
            {
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }
            }
        }
    }

    /// <summary>A style playfield's rooms sorted by what the generator uses them for.</summary>
    public sealed class StyleRoomSet
    {
        StyleRoomSet(int styleId, IReadOnlyList<RoomShape> all, IReadOnlySet<int> mainHalls)
        {
            StyleId = styleId;
            All = all;
            Entrances = all.Where(room => room.Role == RoomRole.Entrance).ToList();
            Starts = all.Where(room => room.Role == RoomRole.Start).ToList();
            Growth = all.Where(room => room.Role == RoomRole.Growth).ToList();
            Terminators = all.Where(room => room.Role == RoomRole.Terminator).ToList();
            MainHalls = all.Where(room => room.Role == RoomRole.Growth && mainHalls.Contains(room.RoomId)).ToList();

            // Main halls are placed only by the per-floor quota, never as ordinary growth.
            Growth = Growth.Where(room => !mainHalls.Contains(room.RoomId)).ToList();

            // Live single-floor layouts use start rooms as ordinary hubs too.
            Hubs = Growth.Concat(Starts).ToList();
        }

        public int StyleId { get; }

        public IReadOnlyList<RoomShape> All { get; }

        public IReadOnlyList<RoomShape> Entrances { get; }

        public IReadOnlyList<RoomShape> Starts { get; }

        public IReadOnlyList<RoomShape> Growth { get; }

        public IReadOnlyList<RoomShape> Terminators { get; }

        /// <summary>Rooms that grow a floor: growth rooms and start rooms.</summary>
        public IReadOnlyList<RoomShape> Hubs { get; }

        /// <summary>Main halls (GameData/DungeonEntrances.json): the room past the entrance is always one of these.</summary>
        public IReadOnlyList<RoomShape> MainHalls { get; }

        public bool IsMainHall(RoomShape shape) => MainHalls.Contains(shape);

        public static StyleRoomSet Load(string gameDataRoot, int styleId)
        {
            DungeonStyleCatalog catalog = DungeonStyleCatalog.Load(gameDataRoot, styleId);
            DungeonEntranceMap map = DungeonEntranceMap.Load(gameDataRoot, styleId);
            var shapes = catalog.Rooms
                .Select(room => RoomShape.FromTemplate(room, catalog.Dcga, catalog.MapWidth, catalog.MapHeight).WithEntranceMap(map.ExitDoors))
                .ToList();
            var set = new StyleRoomSet(styleId, shapes, map.MainHalls);
            if (set.Entrances.Count == 0 || set.MainHalls.Count == 0 || set.Growth.Count == 0 || set.Terminators.Count == 0)
                throw new InvalidDataException("Style " + styleId + " has no entrance or main hall (" + DungeonEntranceMap.FileName
                    + "), growth or one-door rooms to build with.");
            return set;
        }
    }
}
