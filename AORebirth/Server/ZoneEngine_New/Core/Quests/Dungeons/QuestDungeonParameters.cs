namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

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

        /// <summary>Defaults to the implicit MissionEntrance type used by older stored dungeon parameters.</summary>
        public int EntranceType { get; set; } = (int)IdentityType.MissionEntrance;

        /// <summary>MissionEntrance (0xDAC6) instance bits, stored in the protocol's signed integer representation.</summary>
        public int EntranceInstance { get; set; }

        public int EntrancePlayfield { get; set; }

        /// <summary>The entrance's name; the key is "Mission key to" this.</summary>
        public string EntranceName { get; set; } = string.Empty;

        public float EntranceX { get; set; }

        public float EntranceY { get; set; }

        public float EntranceZ { get; set; }

        /// <summary>Identity type of the entrance playfield in the offered quest action (journal waypoint).</summary>
        public int DestinationType { get; set; }

        /// <summary>WorldPos X/Z offsets carried by the offer; historical names retained for stored JSON.</summary>
        public int BuildingLowId { get; set; }

        public int BuildingHighId { get; set; }

        /// <summary>Terminal roll type: 0 kill, 1 find person, 2 find item, 3 repair, 4 find and return.</summary>
        public int MissionType { get; set; }

        public int MissionIconId { get; set; }

        /// <summary>The accepted offer's quality level; locked doors use it as their lock difficulty. 0 on older quests.</summary>
        public int Quality { get; set; }

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static bool TryParse(string? json, out QuestDungeonParameters parameters)
        {
            parameters = null!;
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                QuestDungeonParameters? parsed = JsonSerializer.Deserialize<QuestDungeonParameters>(json, JsonOptions);
                if (parsed == null || parsed.GeneratorVersion <= 0 || !QuestDungeonIds.IsDungeonPlayfield(parsed.DungeonPlayfield)
                    || parsed.EntranceType != (int)IdentityType.MissionEntrance)
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
