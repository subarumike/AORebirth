namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    /// <summary>One MissionEntrance (0xDAC6) dynel: where a quest dungeon is entered, and the name its key carries.</summary>
    public sealed class MissionEntrance
    {
        public int Playfield { get; init; }

        public int Instance { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float Z { get; init; }

        public float HeadingX { get; init; }

        public float HeadingY { get; init; }

        public float HeadingZ { get; init; }

        public float HeadingW { get; init; }

        /// <summary>"a slumhouse", "Hidden Omni-Tek Staging Base": the key is "Mission key to" this.</summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>
    /// GameData/MissionEntrances.json, extracted from every playfield's Dynels.dat. Read-only after load, so it is
    /// shared by all playfields without locking.
    /// </summary>
    public sealed class MissionEntranceCatalog
    {
        public const string FileName = "MissionEntrances.json";

        /// <summary>Terminal roll spots sit exactly on their entrance; this only absorbs float rounding.</summary>
        const float SpotMatchDistance = 1.0f;

        readonly Dictionary<int, MissionEntrance> _byInstance = new();
        readonly Dictionary<int, List<MissionEntrance>> _byPlayfield = new();

        public MissionEntranceCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            string path = Path.Combine(gameData.RootPath, FileName);
            if (!File.Exists(path))
            {
                logger.Warn(FileName + " not found at " + path + "; quest dungeons cannot be entered");
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<MissionEntrance>? entrances = document.RootElement.TryGetProperty("Entrances", out JsonElement list)
                    ? list.Deserialize<List<MissionEntrance>>(options)
                    : null;
                foreach (MissionEntrance entrance in entrances ?? [])
                {
                    if (entrance.Playfield <= 0 || string.IsNullOrWhiteSpace(entrance.Name) || !_byInstance.TryAdd(entrance.Instance, entrance))
                        continue;

                    if (!_byPlayfield.TryGetValue(entrance.Playfield, out List<MissionEntrance>? onPlayfield))
                        _byPlayfield[entrance.Playfield] = onPlayfield = [];
                    onPlayfield.Add(entrance);
                }

                logger.Info(string.Format(CultureInfo.InvariantCulture, "GameData mission entrances={0} from {1}", _byInstance.Count, path));
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to load " + path);
            }
        }

        public bool TryGet(int instance, out MissionEntrance entrance) => _byInstance.TryGetValue(instance, out entrance!);

        /// <summary>The entrance standing on a terminal roll spot (horizontal distance), or false.</summary>
        public bool TryFindAt(int playfield, float x, float z, out MissionEntrance entrance)
        {
            entrance = null!;
            if (!_byPlayfield.TryGetValue(playfield, out List<MissionEntrance>? onPlayfield))
                return false;

            float best = SpotMatchDistance * SpotMatchDistance;
            foreach (MissionEntrance candidate in onPlayfield)
            {
                float dx = candidate.X - x;
                float dz = candidate.Z - z;
                float distance = (dx * dx) + (dz * dz);
                if (distance <= best)
                {
                    best = distance;
                    entrance = candidate;
                }
            }

            return entrance != null;
        }
    }
}
