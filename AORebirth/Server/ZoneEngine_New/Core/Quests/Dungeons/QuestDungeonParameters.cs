namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System.Text.Json;

    /// <summary>
    /// What a quest stores to recreate its dungeon (generatedquests.AcgBuildingGeneratorJson): the layout seed and
    /// generator version, never the generated layout itself, plus the entrance it is reached through.
    /// </summary>
    public sealed class QuestDungeonParameters
    {
        static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public int Seed { get; set; }

        public int GeneratorVersion { get; set; }

        /// <summary>Playfield2 id of this quest's dungeon (<see cref="QuestDungeonIds"/>).</summary>
        public int DungeonPlayfield { get; set; }

        /// <summary>MissionEntrance (0xDAC6) instance the dungeon is entered through.</summary>
        public int EntranceInstance { get; set; }

        public int EntrancePlayfield { get; set; }

        /// <summary>The entrance's name; the key is "Mission key to" this.</summary>
        public string EntranceName { get; set; } = string.Empty;

        public float EntranceX { get; set; }

        public float EntranceY { get; set; }

        public float EntranceZ { get; set; }

        /// <summary>Identity type of the entrance playfield in the offered quest action (journal waypoint).</summary>
        public int DestinationType { get; set; }

        /// <summary>Building template pair the offer carried for the entrance (journal action).</summary>
        public int BuildingLowId { get; set; }

        public int BuildingHighId { get; set; }

        /// <summary>Terminal roll type: 0 kill, 1 find person, 2 find item, 3 repair, 4 find and return.</summary>
        public int MissionType { get; set; }

        public int MissionIconId { get; set; }

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static bool TryParse(string? json, out QuestDungeonParameters parameters)
        {
            parameters = null!;
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                QuestDungeonParameters? parsed = JsonSerializer.Deserialize<QuestDungeonParameters>(json, JsonOptions);
                if (parsed == null || parsed.GeneratorVersion <= 0 || !QuestDungeonIds.IsDungeonPlayfield(parsed.DungeonPlayfield))
                    return false;

                parameters = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
