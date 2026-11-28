namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    using ProceduralGenerator = AORebirth.DungeonGenerator.DungeonGenerator;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A generated dungeon layout: the generator the client builds the building from, the arrival point, the seed it
    /// came from (door states roll from it), and the style's doors (null when the style has none configured).
    /// </summary>
    public sealed record DungeonLayout(AcgBuildingGeneratorData Generator, byte[] GeneratorPayload, Vector3 Spawn, int Seed, DungeonDoorStyle? Doors)
    {
        public IReadOnlyList<AORebirth.DungeonGenerator.DungeonFloorTransition> FloorTransitions { get; init; } = [];
        public int? BossRoomIndex { get; init; }
        public DungeonContentReference? Content { get; init; }
        public string? ResourceRoot { get; init; }
        public DungeonWorldLayout? Geometry { get; init; }
        public AORebirth.DungeonGenerator.StyleRoomSet? RoomSet { get; init; }
        public PlayfieldDistrictsData? Districts { get; init; }
        public CollisionSphereData? PlayerCollisionSphere { get; init; }
        internal NpcTemplateCatalog? NpcTemplates { get; init; }
        public DungeonFindItemPolicy? FindItemPolicy { get; init; }
        public DungeonFindItemPolicy? ReturnItemPolicy { get; init; }
        public DungeonRepairMachinePolicy? RepairMachinePolicy { get; init; }
        public DungeonNpcObjectivePolicy? NpcObjectivePolicy { get; init; }
        public MissionChestPolicy? ChestPolicy { get; init; }
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
    /// Layouts are built procedurally with AORebirth.DungeonGenerator. Historical captured missions require
    /// their original persisted bundle binding and cannot be reconstructed from a seed-only record.
    /// </summary>
    public sealed class DungeonLayoutGenerator : IMissionDungeonGenerator
    {
        public const string DoorStylesFileName = "DungeonDoorStyles.json";

        public const int CurrentVersion = 3;
        public const string GenerationFileName = "DungeonGeneration.json";

        // Immutable version-2 compatibility contract for already accepted seed-only records.
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

        readonly ConcurrentDictionary<(int Seed, int Building), Built> _built = new();
        readonly ProceduralGenerator? _generator;
        readonly RetainedDungeonContent _retained;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly bool _requireRetainedContent;
        readonly string _legacyRoot;
        readonly DungeonContentIndex? _activeContent;
        readonly ContentSnapshot<DungeonGenerationContent>? _current;

        DungeonGenerationPolicy? Policy => _current?.Value.Policy;

        public DungeonFindItemPolicy? FindItemPolicy => Policy?.FindItem;
        public DungeonFindItemPolicy? ReturnItemPolicy => Policy?.ReturnItem;
        public DungeonRepairMachinePolicy? RepairMachinePolicy => Policy?.RepairMachine;
        public DungeonNpcObjectivePolicy? NpcObjectivePolicy => Policy?.NpcObjectives;
        public MissionChestPolicy? ChestPolicy => Policy?.Chests?.Clone();

        public DungeonLayoutGenerator(IGameData gameData, IZoneLogger logger)
            : this(gameData, logger, requireRetainedContent: true)
        {
        }

        internal DungeonLayoutGenerator(IGameData gameData, IZoneLogger logger, bool requireRetainedContent)
        {
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requireRetainedContent = requireRetainedContent;
            _legacyRoot = gameData.RootPath;
            try
            {
                _generator = new ProceduralGenerator(gameData.RootPath);
            }
            catch (ArgumentException exception)
            {
                _logger.Error(exception, "Procedural dungeon resources failed to initialize");
            }

            _retained = new RetainedDungeonContent(gameData.RootPath);

            try
            {
                GameDataContentProvider provider = GameDataContentProvider.ForBaseRoot(gameData.RootPath);
                _current = DungeonGenerationContent.Register(provider).Current;
                if (_current.Value.Policy?.Population?.IsValid(gameData) != true)
                    _logger.Warn("Dungeon generation is missing a validated ordinary NPC policy.");
                if (requireRetainedContent)
                {
                    _activeContent = DungeonContentIndex.Register(provider).Current.Value;
                    if (_activeContent.Bundles.IsEmpty)
                        _logger.Warn("New mission acceptance requires " + RetainedDungeonContent.CurrentFile
                            + " and its retained original dungeon resources.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
            {
                _logger.Error(exception, "Dungeon content could not be validated. New mission acceptance is unavailable.");
            }
        }

        /// <summary>True when a current policy and, for production, retained asset identities are available.</summary>
        public bool IsAvailable => _generator != null && Policy?.Population?.IsValid(_gameData) == true
            && (!_requireRetainedContent || _activeContent is { Bundles.IsEmpty: false });

        /// <summary>Freeze the selected profile and exact retained resources before the existing acceptance transaction.</summary>
        public bool ConfigureNewDungeon(QuestDungeonParameters parameters, bool team)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (!IsAvailable || (parameters.GeneratorVersion != 0 && parameters.GeneratorVersion != CurrentVersion))
                return false;

            DungeonGenerationPolicy policy = Policy!;
            DungeonGenerationProfile? profile = FindProfile(policy, parameters.Quality, team);
            if (profile == null)
                return false;

            try
            {
                var choices = SelectProfileChoices(parameters.Seed, profile);
                FrozenDungeonStyle? retained = null;
                if (_requireRetainedContent)
                {
                    if (!_activeContent!.Bundles.TryGetValue(choices.Style, out string? identity))
                        throw new InvalidDataException($"Missing retained dungeon revision for style {choices.Style}.");
                    retained = _retained.Resolve(new DungeonContentReference(
                        DungeonContentReference.SupportedSchemaVersion, choices.Style, identity));
                    ValidateCurrentDependency(retained.GenerationSha, GenerationFileName);
                    ValidateCurrentDependency(retained.DoorSha, DoorStylesFileName);
                }

                var rooms = retained?.RoomSet ?? _generator!.LoadStyle(choices.Style);
                parameters.GeneratorVersion = CurrentVersion;
                parameters.IsTeam = team;
                parameters.TokenReward = policy.TokenReward?.Clone();
                parameters.LayoutPlan = BuildPlan(choices, rooms, policy, profile);
                parameters.Content = retained?.Reference;
                return parameters.LayoutPlan.IsValid;
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
            {
                _logger.Error(exception, "Dungeon generation profile could not be resolved");
                return false;
            }
        }

        void ValidateCurrentDependency(string expected, string path)
        {
            if (!_current!.Files.Any(file => file.RelativePath == path && file.Sha256 == expected))
                throw new InvalidDataException($"Retained dungeon bundle does not match the active '{path}' snapshot.");
        }

        static DungeonGenerationProfile? FindProfile(DungeonGenerationPolicy policy, int quality, bool team)
            => policy.Profiles.SingleOrDefault(profile => profile.Team == team
                && quality >= profile.MinQuality && quality <= profile.MaxQuality);

        static (int Style, int FloorSize) SelectProfileChoices(int seed, DungeonGenerationProfile profile)
        {
            var random = new Random(seed);
            return (profile.Styles[random.Next(profile.Styles.Length)],
                random.Next(profile.MinFloorSize, profile.MaxFloorSize + 1));
        }

        static DungeonLayoutPlan BuildPlan((int Style, int FloorSize) choices, AORebirth.DungeonGenerator.StyleRoomSet rooms,
            DungeonGenerationPolicy policy, DungeonGenerationProfile profile) => new()
        {
            StyleId = choices.Style,
            FloorCount = profile.Floors,
            FloorSize = choices.FloorSize,
            FloorHeight = policy.FloorHeight,
            BossFloor = profile.Team,
            DescendingFloors = rooms.Transitions?.DescendingFloors ?? false,
            LockedDoorChancePercent = policy.LockedDoorChancePercent
        };

        static bool MatchesProfile(DungeonLayoutPlan actual, DungeonLayoutPlan expected)
            => actual.StyleId == expected.StyleId && actual.FloorCount == expected.FloorCount
                && actual.FloorSize == expected.FloorSize && actual.FloorHeight == expected.FloorHeight
                && actual.BossFloor == expected.BossFloor && actual.DescendingFloors == expected.DescendingFloors
                && actual.LockedDoorChancePercent == expected.LockedDoorChancePercent;

        DungeonDoorStyle? LegacyDoor(int style) => _current?.Value.Doors.TryGetValue(style, out var door) == true
            ? door : null;

        static DungeonPopulationPolicy Population(DungeonGenerationPolicy? policy) => policy?.Population is { } population
            ? new DungeonPopulationPolicy
            {
                SpawnPointChancePercent = population.SpawnPointChancePercent,
                RegularNpcHashes = (string[])population.RegularNpcHashes.Clone()
            }
            : new DungeonPopulationPolicy();

        public bool TryGenerate(QuestDungeonParameters parameters, out DungeonLayout layout)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (parameters.GeneratorVersion == 2)
                return TryGenerate(parameters.Seed, parameters.GeneratorVersion, parameters.EntranceInstance, out layout);
            layout = null!;
            if (parameters.GeneratorVersion == 1)
            {
                _logger.Warn(FormattableString.Invariant(
                    $"Cannot reconstruct version 1 mission playfield {parameters.DungeonPlayfield}: the stored seed has no verified captured-layout bundle binding."));
                return false;
            }
            DungeonLayoutPlan? plan = parameters.LayoutPlan;
            if (parameters.GeneratorVersion != CurrentVersion || plan?.IsValid != true
                || parameters.IsTeam != plan.BossFloor || (_generator == null && parameters.Content == null))
                return false;

            try
            {
                FrozenDungeonStyle? retained = parameters.Content is { } content ? _retained.Resolve(content) : null;
                DungeonGenerationPolicy? policy = retained?.Policy ?? Policy;
                if (retained != null)
                {
                    DungeonGenerationProfile profile = FindProfile(retained.Policy, parameters.Quality, parameters.IsTeam)
                        ?? throw new InvalidDataException("The accepted dungeon has no matching retained profile.");
                    if (retained.Reference.StyleId != plan.StyleId || retained.Policy.Population?.IsValid(retained.NpcTemplates.HasTemplate) != true
                        || !MatchesProfile(plan, BuildPlan(SelectProfileChoices(parameters.Seed, profile),
                            retained.RoomSet, retained.Policy, profile)))
                        throw new InvalidDataException("The accepted dungeon plan or NPC references do not match retained content "
                            + retained.Reference.Sha256 + ".");
                }

                var request = new AORebirth.DungeonGenerator.DungeonGenerationRequest
                {
                    StyleId = plan.StyleId, Seed = parameters.Seed, FloorCount = plan.FloorCount,
                    FloorSize = plan.FloorSize, FloorHeight = plan.FloorHeight, BossFloor = plan.BossFloor,
                    DescendingFloors = plan.DescendingFloors, UseFloorTransitions = true,
                    LockedDoorChancePercent = plan.LockedDoorChancePercent, BuildingInstance = parameters.EntranceInstance
                };
                var result = retained is null
                    ? _generator!.Generate(request) : ProceduralGenerator.Generate(request, retained.RoomSet);
                var doors = retained?.Doors ?? LegacyDoor(plan.StyleId);
                DungeonWorldLayout? geometry = retained == null ? null : DungeonCollisionBuilder.BuildLayout(retained.Catalog, result.Generator);
                if (geometry is not null && !geometry.Collision.HasCollision)
                    throw new InvalidDataException("Accepted dungeon does not have supported original collision geometry.");

                layout = new DungeonLayout(result.Generator, result.Generator.ToByteArray(),
                    new Vector3(result.Spawn.X, result.Spawn.Y, result.Spawn.Z), parameters.Seed,
                    doors is null ? null : doors with { LockedChancePercent = plan.LockedDoorChancePercent })
                {
                    FloorTransitions = result.Transitions, BossRoomIndex = result.BossRoomIndex,
                    Population = Population(policy),
                    Content = retained?.Reference, ResourceRoot = retained?.Root ?? _legacyRoot, Geometry = geometry, Districts = retained?.Districts,
                    RoomSet = retained?.RoomSet,
                    PlayerCollisionSphere = retained?.PlayerSphere, NpcTemplates = retained?.NpcTemplates,
                    FindItemPolicy = policy?.FindItem, ReturnItemPolicy = policy?.ReturnItem, RepairMachinePolicy = policy?.RepairMachine,
                    NpcObjectivePolicy = policy?.NpcObjectives, ChestPolicy = policy?.Chests?.Clone()
                };
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
            {
                _logger.Error(exception, $"Dungeon reconstruction failed for playfield {parameters.DungeonPlayfield}, "
                    + $"seed {parameters.Seed}, retained revision {parameters.Content?.Sha256 ?? "historical"}.");
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
            if (version != 2 || _generator == null)
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
            DungeonDoorStyle? doors = LegacyDoor(generator.Style) is { } style
                ? style with { LockedChancePercent = TemporaryLockedDoorChancePercent }
                : null;
            layout = new DungeonLayout(generator, generator.ToByteArray(), built.Spawn, seed, doors)
            {
                Population = Population(Policy), ResourceRoot = _legacyRoot
            };
            return true;
        }
    }
}
