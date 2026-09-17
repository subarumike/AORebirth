namespace AORebirth.World.Pathfinding
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>Recast bake parameters in world units unless noted as voxels.</summary>
    public sealed class NavMeshBuildSettings
    {
        public const int CurrentVersion = 1;

        public const string ConfigFileName = "NavAgent.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public int SchemaVersion { get; init; } = CurrentVersion;

        public float CellSize { get; init; } = 0.3f;

        public float CellHeight { get; init; } = 0.2f;

        public float AgentRadius { get; init; } = 0.5f;

        public float AgentHeight { get; init; } = 1.8f;

        public float AgentMaxClimb { get; init; } = 1.5f;

        public float AgentMaxSlope { get; init; } = 45f;

        public int TileSizeVoxels { get; init; } = 48;

        public int RegionMinSize { get; init; } = 8;

        public int RegionMergeSize { get; init; } = 20;

        public float EdgeMaxLen { get; init; } = 12f;

        public float EdgeMaxError { get; init; } = 1.3f;

        public int VertsPerPoly { get; init; } = 6;

        public float DetailSampleDist { get; init; } = 6f;

        public float DetailSampleMaxError { get; init; } = 1f;

        public static NavMeshBuildSettings CreateDefault() => new();

        public static string DefaultPathBesideGameData(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            string? parent = Directory.GetParent(Path.GetFullPath(gameDataRoot))?.FullName;
            if (string.IsNullOrEmpty(parent))
                throw new InvalidDataException("Could not resolve Config next to GameData: " + gameDataRoot);

            return Path.Combine(parent, "Config", ConfigFileName);
        }

        public static NavMeshBuildSettings Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("NavMesh agent config was not found.", path);

            NavMeshBuildSettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<NavMeshBuildSettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("NavMesh agent config is invalid: " + path, exception);
            }

            if (settings == null)
                throw new InvalidDataException("NavMesh agent config is empty: " + path);

            settings.Validate();
            return settings;
        }

        public void Validate()
        {
            if (SchemaVersion != CurrentVersion)
            {
                throw new InvalidDataException(
                    "NavMesh agent config schemaVersion must be "
                    + CurrentVersion
                    + " but was "
                    + SchemaVersion
                    + ".");
            }

            if (CellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(CellSize));
            if (CellHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(CellHeight));
            if (AgentRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(AgentRadius));
            if (AgentHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(AgentHeight));
            if (AgentMaxClimb < 0f)
                throw new ArgumentOutOfRangeException(nameof(AgentMaxClimb));
            if (TileSizeVoxels < 8)
                throw new ArgumentOutOfRangeException(nameof(TileSizeVoxels));
            if (VertsPerPoly < 3)
                throw new ArgumentOutOfRangeException(nameof(VertsPerPoly));
        }
    }
}
