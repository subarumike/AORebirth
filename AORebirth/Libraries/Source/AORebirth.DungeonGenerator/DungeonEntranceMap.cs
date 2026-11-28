namespace AORebirth.DungeonGenerator
{
    using System.Text.Json;
    using System.Numerics;

    /// <summary>An original room's floor-platform position relative to its template origin.</summary>
    public sealed record DungeonTransitionRoom(int Room, Vector3 LocalPosition, int PlatformTemplateId,
        int ButtonTemplateId, int BossButtonTemplateId = 0);

    /// <summary>Editable, capture-backed platform roles for a style. These are not NPC or loot definitions.</summary>
    public sealed record DungeonTransitionMap(
        IReadOnlyList<DungeonTransitionRoom> ArrivalRooms,
        IReadOnlyList<DungeonTransitionRoom> DepartureRooms,
        IReadOnlyList<DungeonTransitionRoom> BossRooms,
        bool DescendingFloors);

    /// <summary>
    /// GameData/DungeonEntrances.json for one style: which rooms are entrances and which of each room's doors is the
    /// way in, and which rooms are main halls. Nothing in the client or style data marks the way in (the client treats
    /// whichever door is left unpaired as the exit); the entrance room is modelled with a black plug in that doorway,
    /// so it has to face outside. The first room past the entrance is always a main hall.
    /// </summary>
    public sealed record DungeonEntranceMap(IReadOnlyDictionary<int, int> ExitDoors, IReadOnlySet<int> MainHalls)
    {
        public const string FileName = "DungeonEntrances.json";

        public DungeonTransitionMap? Transitions { get; init; }

        sealed class EntrancesFile
        {
            public List<StyleRow> Styles { get; set; } = [];
        }

        sealed class StyleRow
        {
            public int Style { get; set; }

            public List<EntranceRow> Entrances { get; set; } = [];

            public List<int> MainHalls { get; set; } = [];

            public TransitionRows? FloorTransitions { get; set; }
        }

        sealed class TransitionRows
        {
            public bool DescendingFloors { get; set; }
            public List<TransitionRoomRow> ArrivalRooms { get; set; } = [];
            public List<TransitionRoomRow> DepartureRooms { get; set; } = [];
            public List<TransitionRoomRow> BossRooms { get; set; } = [];
        }

        sealed class TransitionRoomRow
        {
            public int Room { get; set; }
            public float[] Position { get; set; } = [];
            public int PlatformTemplateId { get; set; }
            public int ButtonTemplateId { get; set; }
            public int BossButtonTemplateId { get; set; }
        }

        sealed class EntranceRow
        {
            public int Room { get; set; }

            public int ExitDoor { get; set; }
        }

        /// <summary>The entrances (room id to exit door) and main halls of <paramref name="styleId"/>; empty when unlisted.</summary>
        public static DungeonEntranceMap Load(string gameDataRoot, int styleId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            string path = Path.Combine(gameDataRoot, FileName);
            if (!File.Exists(path))
                return new DungeonEntranceMap(new Dictionary<int, int>(), new HashSet<int>());

            return Parse(JsonSerializer.Deserialize<EntrancesFile>(File.ReadAllText(path), JsonOptions), styleId);
        }

        static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public static DungeonEntranceMap Parse(ReadOnlySpan<byte> data, int styleId)
            => Parse(JsonSerializer.Deserialize<EntrancesFile>(data, JsonOptions), styleId);

        static DungeonEntranceMap Parse(EntrancesFile? file, int styleId)
        {
            var exits = new Dictionary<int, int>();
            var mainHalls = new HashSet<int>();
            DungeonTransitionMap? transitions = null;
            foreach (StyleRow style in file?.Styles ?? [])
            {
                if (style?.Style != styleId)
                    continue;
                foreach (EntranceRow entrance in style.Entrances ?? [])
                {
                    if (entrance != null && entrance.ExitDoor >= 0)
                        exits[entrance.Room] = entrance.ExitDoor;
                }

                foreach (int room in style.MainHalls ?? [])
                    mainHalls.Add(room);

                if (style.FloorTransitions is TransitionRows roles)
                {
                    if (transitions != null)
                        throw new InvalidDataException("Duplicate floor transition map for style " + styleId + ".");
                    transitions = new DungeonTransitionMap(ReadTransitionRooms(roles.ArrivalRooms),
                        ReadTransitionRooms(roles.DepartureRooms), ReadTransitionRooms(roles.BossRooms), roles.DescendingFloors);
                }
            }

            return new DungeonEntranceMap(exits, mainHalls) { Transitions = transitions };
        }

        static IReadOnlyList<DungeonTransitionRoom> ReadTransitionRooms(List<TransitionRoomRow>? rows)
        {
            var result = new List<DungeonTransitionRoom>();
            var seen = new HashSet<int>();
            foreach (TransitionRoomRow row in rows ?? [])
            {
                if (row == null || row.Room < 0 || !seen.Add(row.Room) || row.Position == null || row.Position.Length != 3
                    || row.Position.Any(value => !float.IsFinite(value))
                    || row.PlatformTemplateId <= 0 || row.ButtonTemplateId <= 0 || row.BossButtonTemplateId < 0)
                    throw new InvalidDataException("Invalid or duplicate floor transition room in " + FileName + ".");
                result.Add(new DungeonTransitionRoom(row.Room,
                    new Vector3(row.Position[0], row.Position[1], row.Position[2]), row.PlatformTemplateId,
                    row.ButtonTemplateId, row.BossButtonTemplateId));
            }

            return result.AsReadOnly();
        }
    }
}
