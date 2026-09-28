namespace AORebirth.DungeonGenerator
{
    using AORebirth.World.Collision;

    /// <summary>What a template room is used for when building a layout.</summary>
    public enum RoomRole
    {
        /// <summary>Two or more doors: grows the layout.</summary>
        Growth,

        /// <summary>Exactly one door: caps an open doorway.</summary>
        Terminator,

        /// <summary>A room listed in GameData/DungeonEntrances.json: the only rooms that may hold the dungeon's way in.</summary>
        Entrance,

        /// <summary>A "startroom": seeds every floor after the first.</summary>
        Start,

        /// <summary>No doors (boss rooms reached by teleport) or unusable size: never placed here.</summary>
        Unused
    }

    /// <summary>One door of a room template, in the room's own frame.</summary>
    public readonly record struct RoomDoor(int TileX, int TileZ, int Facing);

    /// <summary>
    /// A template room as a building block: its block footprint (from the style tilemap, so L- and T-shaped mainhalls
    /// keep their real shape) and its doors. One block is five tiles; rooms are always whole blocks.
    /// </summary>
    public sealed class RoomShape
    {
        public const int TilesPerBlock = 5;

        RoomShape(StyleRoomTemplate template, bool[,] footprint, RoomDoor[] doors, RoomRole role, int exitDoor = -1)
        {
            Template = template;
            Footprint = footprint;
            Doors = doors;
            Role = role;
            ExitDoor = exitDoor;
        }

        public StyleRoomTemplate Template { get; }

        /// <summary>Generator room id (the template's index in the style).</summary>
        public int RoomId => Template.InstanceId;

        public string Name => Template.Name;

        public int TilesX => Template.NumTilesX;

        public int TilesZ => Template.NumTilesZ;

        public int BlocksX => TilesX / TilesPerBlock;

        public int BlocksZ => TilesZ / TilesPerBlock;

        /// <summary>[blockX, blockZ]: true where the room really fills the block.</summary>
        public bool[,] Footprint { get; }

        public IReadOnlyList<RoomDoor> Doors { get; }

        public RoomRole Role { get; }

        /// <summary>For an entrance, the door that must be the dungeon's way in; -1 otherwise.</summary>
        public int ExitDoor { get; }

        public int FootprintBlocks
        {
            get
            {
                int count = 0;
                foreach (bool filled in Footprint)
                    if (filled) count++;
                return count;
            }
        }

        /// <summary>
        /// Builds a room from its template. A block is filled when any of its 25 tiles is non-zero in the style's
        /// occupancy layer (the extracted DCGA.png, unflipped: every one of style 324's 55 rooms is non-empty, the 8
        /// mainhalls are the only partial ones, and all 177 doors sit on filled blocks; GNDA.png is ground height and
        /// reads floor tiles as empty). Without a tilemap the whole bounding box counts.
        /// </summary>
        public static RoomShape FromTemplate(StyleRoomTemplate template, byte[] occupancy, int mapWidth, int mapHeight)
        {
            ArgumentNullException.ThrowIfNull(template);
            int bx = template.NumTilesX / TilesPerBlock;
            int bz = template.NumTilesZ / TilesPerBlock;
            var footprint = new bool[Math.Max(bx, 0), Math.Max(bz, 0)];
            bool haveMap = occupancy != null && occupancy.Length >= mapWidth * mapHeight && mapWidth > 0;
            for (int i = 0; i < bx; i++)
            {
                for (int j = 0; j < bz; j++)
                {
                    if (!haveMap)
                    {
                        footprint[i, j] = true;
                        continue;
                    }

                    for (int t = 0; t < TilesPerBlock * TilesPerBlock && !footprint[i, j]; t++)
                    {
                        int x = template.TileMinX + (i * TilesPerBlock) + (t % TilesPerBlock);
                        int z = template.TileMinZ + (j * TilesPerBlock) + (t / TilesPerBlock);
                        if (x >= 0 && z >= 0 && x < mapWidth && z < mapHeight && occupancy![(z * mapWidth) + x] != 0)
                            footprint[i, j] = true;
                    }
                }
            }

            var doors = new RoomDoor[template.DoorPosRots.Count];
            for (int d = 0; d < doors.Length; d++)
            {
                int posRot = template.DoorPosRots[d];
                int tile = posRot >> 2;
                doors[d] = new RoomDoor(template.NumTilesX > 0 ? tile % template.NumTilesX : 0,
                    template.NumTilesX > 0 ? tile / template.NumTilesX : 0, posRot & 3);
            }

            return new RoomShape(template, footprint, doors, Classify(template, doors.Length, bx, bz));
        }

        /// <summary>
        /// Applies the style's entrance map: a listed room becomes an entrance with its mapped exit door; a room named
        /// as an entrance but not listed is left unused, since its plugged doorway would seal whatever it faced.
        /// </summary>
        internal RoomShape WithEntranceMap(IReadOnlyDictionary<int, int> exitDoors)
        {
            if (exitDoors.TryGetValue(RoomId, out int exit) && exit < Doors.Count && Doors.Count >= 2
                && Template.IsAcgSizeValid && BlocksX > 0 && BlocksZ > 0)
                return new RoomShape(Template, Footprint, (RoomDoor[])Doors, RoomRole.Entrance, exit);
            return Role == RoomRole.Entrance ? new RoomShape(Template, Footprint, (RoomDoor[])Doors, RoomRole.Unused) : this;
        }

        static RoomRole Classify(StyleRoomTemplate template, int doorCount, int bx, int bz)
        {
            if (!template.IsAcgSizeValid || bx <= 0 || bz <= 0 || doorCount == 0)
                return RoomRole.Unused;

            string name = template.Name ?? string.Empty;
            if (name.EndsWith("_entrance", StringComparison.OrdinalIgnoreCase) && doorCount >= 2)
                return RoomRole.Entrance;
            if (name.Contains("startroom", StringComparison.OrdinalIgnoreCase))
                return RoomRole.Start;
            return doorCount == 1 ? RoomRole.Terminator : RoomRole.Growth;
        }
    }
}
