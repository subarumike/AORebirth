namespace AORebirth.DungeonGenerator
{
    using System.Text.Json;

    /// <summary>
    /// GameData/DungeonEntrances.json for one style: which rooms are entrances and which of each room's doors is the
    /// way in, and which rooms are main halls. Nothing in the client or style data marks the way in (the client treats
    /// whichever door is left unpaired as the exit); the entrance room is modelled with a black plug in that doorway,
    /// so it has to face outside. The first room past the entrance is always a main hall.
    /// </summary>
    public sealed record DungeonEntranceMap(IReadOnlyDictionary<int, int> ExitDoors, IReadOnlySet<int> MainHalls)
    {
        public const string FileName = "DungeonEntrances.json";

        sealed class EntrancesFile
        {
            public List<StyleRow> Styles { get; set; } = [];
        }

        sealed class StyleRow
        {
            public int Style { get; set; }

            public List<EntranceRow> Entrances { get; set; } = [];

            public List<int> MainHalls { get; set; } = [];
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
            var exits = new Dictionary<int, int>();
            var mainHalls = new HashSet<int>();
            if (!File.Exists(path))
                return new DungeonEntranceMap(exits, mainHalls);

            EntrancesFile? file = JsonSerializer.Deserialize<EntrancesFile>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
            }

            return new DungeonEntranceMap(exits, mainHalls);
        }
    }
}
