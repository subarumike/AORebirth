namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A generated dungeon layout: the generator the client builds the building from, the arrival point, the seed it
    /// came from (door states roll from it), and the style's doors (null when the style has none configured).
    /// </summary>
    public sealed record DungeonLayout(AcgBuildingGeneratorData Generator, byte[] GeneratorPayload, Vector3 Spawn, int Seed, DungeonDoorStyle? Doors)
    {
        public IReadOnlyList<AORebirth.DungeonGenerator.DungeonFloorTransition> FloorTransitions { get; init; } = [];
        public int? BossRoomIndex { get; init; }
        public DungeonPopulationPolicy Population { get; init; } = new();
    }

    /// <summary>GameData/DungeonDoorStyles.json for one ACG style: door item templates and spawn-state chances.</summary>
    public sealed record DungeonDoorStyle(int DoorTemplate, int ExitTemplate, int OpenChancePercent, int LockedChancePercent);

    /// <summary>Editable mission token content, frozen with each newly accepted dungeon.</summary>
    public sealed class MissionTokenRewardPolicy
    {
        public Dictionary<int, int> SidedItems { get; set; } = [];
        public int ItemQuality { get; set; }
        public int FeedbackStartingKill { get; set; }
        public int FeedbackMessageId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => SidedItems != null && SidedItems.Count == 2
            && SidedItems.TryGetValue((int)Side.Clan, out int clan) && clan > 0
            && SidedItems.TryGetValue((int)Side.Omni, out int omni) && omni > 0 && clan != omni
            && ItemQuality > 0 && FeedbackStartingKill > 0 && FeedbackMessageId > 0;

        public MissionTokenRewardPolicy Clone() => new()
        {
            SidedItems = new Dictionary<int, int>(SidedItems), ItemQuality = ItemQuality,
            FeedbackStartingKill = FeedbackStartingKill, FeedbackMessageId = FeedbackMessageId
        };
    }

    /// <summary>
    /// Builds a quest dungeon's ACG building generator from its stored seed and frozen layout plan. The same plan, seed, generator version and
    /// building identity always give the same layout, so a dungeon released while empty comes back identical.
    /// Layouts are built procedurally with AORebirth.DungeonGenerator. Version 1 (captured layouts picked by seed)
    /// was removed; quests stored with it build procedurally too.
    /// </summary>
    public sealed class DungeonLayoutGenerator
    {
        public const string DoorStylesFileName = "DungeonDoorStyles.json";

        public const int CurrentVersion = 3;
        public const string GenerationFileName = "DungeonGeneration.json";

        /// <summary>The removed captured-layout version; its stored seeds now build procedurally.</summary>
        const int RetiredCapturedVersion = 1;

        // Immutable version-1/2 compatibility contract for already accepted seed-only records.
        // New missions select their style and dimensions from editable GameData.
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
        GenerationPolicy? _policy;

        sealed class GenerationPolicy
        {
            public int FloorHeight { get; set; }
            public int LockedDoorChancePercent { get; set; }
            public DungeonPopulationPolicy? Population { get; set; }
            public DungeonFindItemPolicy? FindItem { get; set; }
            public DungeonFindItemPolicy? ReturnItem { get; set; }
            public DungeonRepairMachinePolicy? RepairMachine { get; set; }
            public DungeonNpcObjectivePolicy? NpcObjectives { get; set; }
            public MissionChestPolicy? Chests { get; set; }
            public MissionTrapPolicy? Traps { get; set; }
            public MissionTokenRewardPolicy? TokenReward { get; set; }
            public List<GenerationProfile> Profiles { get; set; } = [];
        }

        sealed class GenerationProfile
        {
            public bool Team { get; set; }
            public int MinQuality { get; set; }
            public int MaxQuality { get; set; }
            public int[] Styles { get; set; } = [];
            public int Floors { get; set; }
            public int MinFloorSize { get; set; }
            public int MaxFloorSize { get; set; }
        }

        internal DungeonFindItemPolicy? FindItemPolicy => _policy?.FindItem;
        internal DungeonFindItemPolicy? ReturnItemPolicy => _policy?.ReturnItem;
        internal DungeonRepairMachinePolicy? RepairMachinePolicy => _policy?.RepairMachine;
        internal DungeonNpcObjectivePolicy? NpcObjectivePolicy => _policy?.NpcObjectives;

        public MissionChestPolicy? ChestPolicy => _policy?.Chests;

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
            }
            catch (Exception exception)
            {
                _generator = null;
                logger.Error(exception, "Procedural dungeon resources failed to initialize");
            }

            LoadDoorStyles(Path.Combine(gameData.RootPath, DoorStylesFileName), logger);
            try
            {
                _policy = JsonSerializer.Deserialize<GenerationPolicy>(File.ReadAllText(Path.Combine(gameData.RootPath, GenerationFileName)),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (_policy == null || _policy.Population?.IsValid(gameData) != true || _policy.FloorHeight is <= 0 or > 255
                    || (_policy.TokenReward != null && !_policy.TokenReward.IsValid)
                    || (_policy.Chests != null && !_policy.Chests.IsValid)
                    || (_policy.Traps != null && !_policy.Traps.IsValid)
                    || _policy.LockedDoorChancePercent is < 0 or > 100 || _policy.Profiles.Count == 0
                    || _policy.Profiles.Any(p => p.MinQuality < 1 || p.MinQuality > p.MaxQuality
                        || p.Styles.Length == 0 || p.Styles.Any(s => s <= 0) || p.Floors < (p.Team ? 2 : 1) || p.Floors > 8
                        || (!p.Team && p.Floors != 1) || p.MinFloorSize < 3 || p.MaxFloorSize > 40 || p.MinFloorSize > p.MaxFloorSize))
                    throw new InvalidDataException("Invalid dungeon generation policy.");
                // Coverage must be unambiguous: missing bands are explicitly unsupported, overlapping ones are a data error.
                for (int i = 0; i < _policy.Profiles.Count; i++)
                    for (int j = i + 1; j < _policy.Profiles.Count; j++)
                        if (_policy.Profiles[i].Team == _policy.Profiles[j].Team
                            && _policy.Profiles[i].MinQuality <= _policy.Profiles[j].MaxQuality
                            && _policy.Profiles[j].MinQuality <= _policy.Profiles[i].MaxQuality)
                            throw new InvalidDataException("Overlapping dungeon generation profiles.");
            }
            catch (Exception exception)
            {
                _policy = null;
                logger.Error(exception, "Failed to load " + GenerationFileName + "; new mission acceptance is unavailable");
            }
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
        public bool IsAvailable => _generator != null && _policy != null;

        /// <summary>Freeze new layout choices before the existing acceptance transaction. No capture data is read here.</summary>
        public bool ConfigureNewDungeon(QuestDungeonParameters parameters, bool team)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (_generator == null || _policy == null || (parameters.GeneratorVersion != 0 && parameters.GeneratorVersion != CurrentVersion))
                return false;
            GenerationProfile? profile = _policy.Profiles.SingleOrDefault(p => p.Team == team
                && parameters.Quality >= p.MinQuality && parameters.Quality <= p.MaxQuality);
            if (profile == null) return false;
            try
            {
                var random = new Random(parameters.Seed);
                int style = profile.Styles[random.Next(profile.Styles.Length)];
                var rooms = _generator.LoadStyle(style);
                parameters.GeneratorVersion = CurrentVersion;
                parameters.IsTeam = team;
                parameters.TokenReward = _policy.TokenReward?.Clone();
                parameters.Traps = _policy.Traps?.Enabled == true ? _policy.Traps.Clone() : null;
                parameters.LayoutPlan = new DungeonLayoutPlan
                {
                    StyleId = style, FloorCount = profile.Floors,
                    FloorSize = random.Next(profile.MinFloorSize, profile.MaxFloorSize + 1),
                    FloorHeight = _policy.FloorHeight, BossFloor = team,
                    DescendingFloors = rooms.Transitions?.DescendingFloors ?? false,
                    LockedDoorChancePercent = _policy.LockedDoorChancePercent
                };
                return parameters.LayoutPlan.IsValid;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Dungeon generation profile could not be resolved");
                return false;
            }
        }

        public bool TryGenerate(QuestDungeonParameters parameters, out DungeonLayout layout)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (parameters.GeneratorVersion is 1 or 2)
                return TryGenerate(parameters.Seed, parameters.GeneratorVersion, parameters.EntranceInstance, out layout);
            layout = null!;
            DungeonLayoutPlan? plan = parameters.LayoutPlan;
            if (parameters.GeneratorVersion != CurrentVersion || _generator == null || plan?.IsValid != true
                || parameters.IsTeam != plan.BossFloor) return false;
            try
            {
                var result = _generator.Generate(new AORebirth.DungeonGenerator.DungeonGenerationRequest
                {
                    StyleId = plan.StyleId, Seed = parameters.Seed, FloorCount = plan.FloorCount,
                    FloorSize = plan.FloorSize, FloorHeight = plan.FloorHeight, BossFloor = plan.BossFloor,
                    DescendingFloors = plan.DescendingFloors, UseFloorTransitions = true,
                    LockedDoorChancePercent = plan.LockedDoorChancePercent, BuildingInstance = parameters.EntranceInstance
                });
                DungeonDoorStyle? doors = _doorStyles.TryGetValue(plan.StyleId, out var style)
                    ? style with { LockedChancePercent = plan.LockedDoorChancePercent } : null;
                layout = new DungeonLayout(result.Generator, result.Generator.ToByteArray(),
                    new Vector3(result.Spawn.X, result.Spawn.Y, result.Spawn.Z), parameters.Seed, doors)
                {
                    FloorTransitions = result.Transitions, BossRoomIndex = result.BossRoomIndex,
                    Population = _policy?.Population ?? new DungeonPopulationPolicy()
                };
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Procedural dungeon generation failed for seed " + parameters.Seed.ToString(CultureInfo.InvariantCulture));
                return false;
            }
        }

        /// <summary>
        /// The layout for <paramref name="seed"/> at <paramref name="version"/>, stamped with the dungeon's building
        /// identity. False for an unknown version or when the generator did not load.
        /// </summary>
        public bool TryGenerate(int seed, int version, int buildingInstance, out DungeonLayout layout)
        {
            layout = null!;
            if ((version != 2 && version != RetiredCapturedVersion) || _generator == null)
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
            layout = new DungeonLayout(generator, generator.ToByteArray(), built.Spawn, seed, doors)
            {
                Population = _policy?.Population ?? new DungeonPopulationPolicy()
            };
            return true;
        }
    }
}
