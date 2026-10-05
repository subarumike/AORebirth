namespace ZoneEngine_New.Core.Configuration
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Config/Gameplay.json: tunable gameplay rules, loaded once at startup. Resolved beside the GameData tree first
    /// (a deployment's own Config folder), else beside the process, where the repo copy is placed by the build.
    /// </summary>
    public sealed class GameplaySettings
    {
        public const int CurrentVersion = 1;

        public const string ConfigFileName = "Gameplay.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public int SchemaVersion { get; init; } = CurrentVersion;

        /// <summary>Seconds a successful /stuck locks the Stability skill, and so how long until the next one.</summary>
        public int StuckCooldownSeconds { get; init; } = 300;

        /// <summary>Where /stuck sends a player.</summary>
        public GameplayLocation? StuckLocation { get; init; }

        public static string ResolvePath(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            string? parent = Directory.GetParent(Path.GetFullPath(gameDataRoot))?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string besideGameData = Path.Combine(parent, "Config", ConfigFileName);
                if (File.Exists(besideGameData))
                    return besideGameData;
            }

            return Path.Combine(AppContext.BaseDirectory, "Config", ConfigFileName);
        }

        public static GameplaySettings Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("Gameplay config was not found.", path);

            GameplaySettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<GameplaySettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Gameplay config is invalid: " + path, exception);
            }

            if (settings == null)
                throw new InvalidDataException("Gameplay config is empty: " + path);

            settings.Validate(path);
            return settings;
        }

        void Validate(string path)
        {
            if (SchemaVersion != CurrentVersion)
                throw new InvalidDataException(path + ": schemaVersion must be " + CurrentVersion + " but was " + SchemaVersion + ".");
            if (StuckCooldownSeconds < 0)
                throw new InvalidDataException(path + ": stuckCooldownSeconds must not be negative.");
            if (StuckLocation is not GameplayLocation stuck || !stuck.IsValid)
                throw new InvalidDataException(path + ": stuckLocation needs a positive playfield and finite x, y, z.");
        }
    }

    /// <summary>A playfield and position in Gameplay.json.</summary>
    public sealed class GameplayLocation
    {
        public int Playfield { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float Z { get; init; }

        public bool IsValid => Playfield > 0 && float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    }
}
