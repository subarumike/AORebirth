namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A generated dungeon layout: the generator the client builds the building from, the arrival point, the seed it
    /// came from (door states roll from it), and the style's doors (null when the style has none configured).
    /// </summary>
    public sealed record DungeonLayout(AcgBuildingGeneratorData Generator, byte[] GeneratorPayload, Vector3 Spawn, int Seed, DungeonDoorStyle? Doors);

    /// <summary>GameData/DungeonDoorStyles.json for one ACG style: door item templates and spawn-state chances.</summary>
    public sealed record DungeonDoorStyle(int DoorTemplate, int ExitTemplate, int OpenChancePercent, int LockedChancePercent);

    /// <summary>
    /// Builds a quest dungeon's ACG building generator from its stored seed. The same seed, generator version and
    /// building identity always give the same layout, so a dungeon released while empty comes back identical.
    /// Layouts are built procedurally with AORebirth.DungeonGenerator. Version 1 (captured layouts picked by seed)
    /// was removed; quests stored with it build procedurally too.
    /// </summary>
    public sealed class DungeonLayoutGenerator
    {
        public const string DoorStylesFileName = "DungeonDoorStyles.json";

        public const int CurrentVersion = 2;

        /// <summary>The removed captured-layout version; its stored seeds now build procedurally.</summary>
        const int RetiredCapturedVersion = 1;

        // TEMPORARY: every accepted mission gets the same procedural dungeon shape until quest parameters (mission
        // type, difficulty, playfield) choose the style, floors, size and lock chance.
        const int TemporaryStyle = 324;
        const int TemporaryFloors = 2;
        const int TemporaryFloorSize = 16;
        const int TemporaryLockedDoorChancePercent = 5;

        /// <summary>Generated layouts kept for reuse (accept, entry and login all resolve the same seed).</summary>
        const int MaxCachedLayouts = 512;

        /// <summary>Building identity type of a quest dungeon (ACG building, as in the captured PlayfieldAnarchyF).</summary>
        public const int BuildingIdentityType = 0xC7A1;

        sealed record Built(AcgBuildingGeneratorData Generator, Vector3 Spawn);

        readonly Dictionary<int, DungeonDoorStyle> _doorStyles = new();
        readonly ConcurrentDictionary<(int Seed, int Building), Built> _built = new();
        readonly AORebirth.DungeonGenerator.DungeonGenerator? _generator;
        readonly IZoneLogger _logger;

        sealed class DoorStylesFile
        {
            public int OpenChancePercent { get; set; }

            public int LockedChancePercent { get; set; }

            public List<DoorStyleRow> Styles { get; set; } = [];
        }

        sealed class DoorStyleRow
        {
            public int Style { get; set; }

            public int DoorTemplate { get; set; }

            public int ExitTemplate { get; set; }
        }

        public DungeonLayoutGenerator(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;

            try
            {
                _generator = new AORebirth.DungeonGenerator.DungeonGenerator(gameData.RootPath);
                _generator.LoadStyle(TemporaryStyle);
            }
            catch (Exception exception)
            {
                _generator = null;
                logger.Error(exception, "Procedural dungeon style " + TemporaryStyle + " failed to load; new quest dungeons cannot be generated");
            }

            LoadDoorStyles(Path.Combine(gameData.RootPath, DoorStylesFileName), logger);
        }

        void LoadDoorStyles(string path, IZoneLogger logger)
        {
            if (!File.Exists(path))
            {
                logger.Warn(DoorStylesFileName + " not found at " + path + "; quest dungeons will have no doors");
                return;
            }

            try
            {
                DoorStylesFile? file = JsonSerializer.Deserialize<DoorStylesFile>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                int open = Math.Clamp(file?.OpenChancePercent ?? 0, 0, 100);
                int locked = Math.Clamp(file?.LockedChancePercent ?? 0, 0, 100);
                foreach (DoorStyleRow row in file?.Styles ?? [])
                {
                    if (row.Style > 0 && row.DoorTemplate > 0 && row.ExitTemplate > 0)
                        _doorStyles[row.Style] = new DungeonDoorStyle(row.DoorTemplate, row.ExitTemplate, open, locked);
                }

                logger.Info(string.Format(CultureInfo.InvariantCulture, "GameData dungeon door styles={0} from {1}", _doorStyles.Count, path));
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to load " + path);
            }
        }

        /// <summary>True when layouts of <see cref="CurrentVersion"/> can be generated.</summary>
        public bool IsAvailable => _generator != null;

        /// <summary>
        /// The layout for <paramref name="seed"/> at <paramref name="version"/>, stamped with the dungeon's building
        /// identity. False for an unknown version or when the generator did not load.
        /// </summary>
        public bool TryGenerate(int seed, int version, int buildingInstance, out DungeonLayout layout)
        {
            layout = null!;
            if ((version != CurrentVersion && version != RetiredCapturedVersion) || _generator == null)
                return false;

            if (!_built.TryGetValue((seed, buildingInstance), out Built? built))
            {
                try
                {
                    AORebirth.DungeonGenerator.DungeonGenerationResult result = _generator.Generate(new AORebirth.DungeonGenerator.DungeonGenerationRequest
                    {
                        StyleId = TemporaryStyle,
                        Seed = seed,
                        FloorCount = TemporaryFloors,
                        FloorSize = TemporaryFloorSize,
                        LockedDoorChancePercent = TemporaryLockedDoorChancePercent,
                        BuildingInstance = buildingInstance
                    });
                    built = new Built(result.Generator, new Vector3(result.Spawn.X, result.Spawn.Y, result.Spawn.Z));
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Procedural dungeon generation failed for seed " + seed.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                if (_built.Count >= MaxCachedLayouts)
                    _built.Clear();
                _built[(seed, buildingInstance)] = built;
            }

            AcgBuildingGeneratorData generator = built.Generator;
            DungeonDoorStyle? doors = _doorStyles.TryGetValue(generator.Style, out DungeonDoorStyle? style)
                ? style with { LockedChancePercent = TemporaryLockedDoorChancePercent }
                : null;
            layout = new DungeonLayout(generator, generator.ToByteArray(), built.Spawn, seed, doors);
            return true;
        }
    }
}
