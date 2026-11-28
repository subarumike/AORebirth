namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;

using AORebirth.Interfaces.Persistence.Shops;
using AORebirth.World.Collision;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using Utility.GameData.Missions;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Metrics;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;

/// <summary>
/// A quest's ACG dungeon, generated from the quest's seed when someone enters. It holds no persistent world
/// state: when released it is simply built again from the same seed, doors included. Its doors are placed from
/// the layout's room connections, and its NPC spawn points from the style's district spawn points, moved and
/// turned into each placed room. A kill-target quest's target is placed at a spawn point in one of the rooms farthest
/// from the entrance.
/// </summary>
public sealed class QuestDungeonPlayfield : Playfield
{
    PlayfieldWorldSimulation? _simulation;
    AORebirth.World.Collision.DungeonWorldLayout? _chestDungeon;
    bool _chestsReady;
    Exception? _chestPopulationFailure;
    readonly MissionItemObjectiveBinding? _objectiveItem;
    readonly MissionNpcObjectiveBinding? _objectiveNpc;
    readonly bool _spawnNpcObjective;
    readonly IMissionDungeonPopulationModule _population;
    readonly IMissionDungeonObjectivePlacement _objectives;
    readonly QuestDungeonParameters? _acceptedParameters;
    readonly Func<Player, MissionObjectiveItem, bool>? _collectObjective;

    public QuestDungeonPlayfield(int playfieldId, string questId, DungeonLayout layout, MissionEntrancePlacement entrance,
        string? targetHash, int quality, IZoneLogger logger, IMessageRouter router, PlayfieldManager manager, PlayerHydrator hydrator, IGameData data,
        IItemBuilder items, HashItemMinter hashItems, IInventoryRepository inventory, IItemInstanceIdAllocator ids,
        InventoryMoveService moves, InventoryFlushService flush, TradeService trades, CharacterSnapshotService snapshot,
        IPlayfieldMetricsRegistry metrics, IShopDao shopDao, MissionItemObjectiveBinding? objectiveItem = null,
        Func<Player, MissionObjectiveItem, bool>? collectObjective = null, MissionNpcObjectiveBinding? npcObjective = null,
        MissionTokenProgress? tokenProgress = null, bool spawnNpcObjective = true, MissionChestPopulation? chestPopulation = null,
        IMissionDungeonPopulationModule? population = null, IMissionDungeonObjectivePlacement? objectives = null,
        QuestDungeonParameters? acceptedParameters = null)
        : base(new Identity { Type = IdentityType.Playfield, Instance = playfieldId }, logger, router,
            manager, hydrator, data, items, hashItems, inventory, ids, moves, flush, trades, snapshot, metrics, shopDao)
    {
        QuestId = questId ?? throw new ArgumentNullException(nameof(questId));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Entrance = entrance ?? throw new ArgumentNullException(nameof(entrance));
        TargetHash = string.IsNullOrWhiteSpace(targetHash) ? null : targetHash;
        Quality = Math.Max(0, quality);
        if (objectiveItem != null && (!objectiveItem.IsValid
            || !string.Equals(objectiveItem.QuestId, QuestId, StringComparison.Ordinal) || collectObjective == null))
            throw new ArgumentException("Invalid objective for this quest dungeon.", nameof(objectiveItem));
        _objectiveItem = objectiveItem;
        _collectObjective = collectObjective;
        if (npcObjective != null && (!npcObjective.IsValid
            || !string.Equals(npcObjective.QuestId, QuestId, StringComparison.Ordinal)))
            throw new ArgumentException("Invalid NPC objective for this quest dungeon.", nameof(npcObjective));
        _objectiveNpc = npcObjective;
        _spawnNpcObjective = spawnNpcObjective;
        _population = population ?? MissionDungeonPopulationModule.Shared;
        _objectives = objectives ?? new DungeonObjectivePlacement();
        _acceptedParameters = acceptedParameters;
        TokenProgress = tokenProgress;
        TokenProgress?.Attach(this, logger);
        ChestPopulation = chestPopulation;
    }

    public string QuestId { get; }

    public DungeonLayout Layout { get; }

    public MissionTokenProgress? TokenProgress { get; }

    public MissionChestPopulation? ChestPopulation { get; }

    /// <summary>Where the dungeon is entered from, and where its occupants return to.</summary>
    public MissionEntrancePlacement Entrance { get; }

    /// <summary>The NPC hash a kill-target quest wants killed here; null when the quest has no kill target.</summary>
    public string? TargetHash { get; }

    /// <summary>The mission's quality level (0 when unknown); locked doors use it as their lock difficulty.</summary>
    public int Quality { get; }

    public override void Build()
    {
        if (IsBuilt) return;
        var locality = GetRequiredService<PlayfieldLocality>();
        DungeonWorldLayout? dungeon = Layout.Content != null ? Layout.Geometry : DungeonPlayfieldBinder.TryBuild(
            GameData.RootPath, Identity.Instance, Layout.Generator, Logger);
        _chestDungeon = dungeon;
        if (dungeon == null && (Layout.Content != null || TokenProgress != null))
            throw new InvalidDataException("The mission dungeon cannot initialize without its generated geometry.");
        if (dungeon != null)
            locality.ApplyDungeonRooms(dungeon);

        var geometry = DungeonPlayfieldBinder.WithDungeonCollision(Identity.Instance, Geometry, dungeon);
        if (geometry.Collision?.HasCollision == true)
        {
            _simulation = PlayfieldWorldSimulation.Create(
                Identity.Instance, geometry, MetaData, DestinationsCatalog.Instance, GameData, Logger, ItemTemplates);
            RegisterWorldServices(_simulation);
        }

        if (dungeon != null)
        {
            var districts = Layout.Content != null ? Layout.Districts
                : LoadStyleDistricts(GameData.RootPath, Layout.Generator.Style, Logger);
            int[] roomDistances = DungeonObjectivePlacement.RoomDistances(dungeon, Layout);
            ValidateFrozenObjective(dungeon, districts);
            SpawnDoors(dungeon.Doors, locality);
            SpawnFloorTransitions(locality);
            _population.Populate(this, dungeon, districts, _objectiveNpc, _spawnNpcObjective, GameData, Logger, roomDistances);
            SpawnObjectiveItem(roomDistances, locality);
        }

        EnsureChestPopulation();

        TokenProgress?.Seal();

        MarkBuilt();
    }

    /// <summary>Called during build and on the existing destination admission tick before visibility activates.</summary>
    internal bool EnsureChestPopulation()
    {
        if (_chestsReady || ChestPopulation == null || IsDisposed)
            return true;
        if (_chestPopulationFailure != null)
            throw new InvalidDataException("The mission chest population requires fresh dungeon reconstruction.", _chestPopulationFailure);
        if (_chestDungeon == null)
            return ChestPopulation.Defer("dungeon-geometry-unavailable");
        if (_simulation == null)
            return ChestPopulation.Defer("world-simulation-unavailable");
        try
        {
            _chestsReady = ChestPopulation.Populate(this, _chestDungeon, _simulation);
            return _chestsReady;
        }
        catch (Exception exception)
        {
            _chestPopulationFailure = exception;
            ChestPopulation.Retire();
            throw;
        }
    }

    static readonly ConcurrentDictionary<(string Root, int Style), PlayfieldDistrictsData?> StyleDistricts = new();

    static readonly JsonSerializerOptions DistrictJsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static PlayfieldDistrictsData? LoadStyleDistricts(string root, int style, IZoneLogger logger)
        => StyleDistricts.GetOrAdd((root, style), key =>
        {
            string path = Path.Combine(key.Root, GameDataPaths.PlayfieldDistrictsRelativePath(key.Style));
            if (!File.Exists(path))
            {
                logger.Warn("No Districts.json for dungeon style " + key.Style + " at " + path + "; dungeon has no NPC spawns");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<PlayfieldDistrictsData>(File.ReadAllText(path), DistrictJsonOptions);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to read " + path);
                return null;
            }
        });

    void ValidateFrozenObjective(DungeonWorldLayout dungeon, PlayfieldDistrictsData? districts)
    {
        if (Layout.Content == null)
            return;
        if (_acceptedParameters == null || _simulation == null)
            throw new InvalidDataException("The accepted objective cannot be validated without its binding and collision surface.");

        var validatePoint = _objectives.CreatePhysicalValidator(dungeon, Layout, districts, _simulation,
            Layout.PlayerCollisionSphere, Layout.ChestPolicy?.Placement);
        if (_acceptedParameters.ObjectiveNpc is { } npc)
            Validate(npc.RoomIndex, npc.X, npc.Y, npc.Z);
        else if (_acceptedParameters.ObjectiveItem is { } item)
            Validate(item.RoomIndex, item.X, item.Y, item.Z);
        else
            throw new InvalidDataException("The accepted mission is missing its frozen NPC or item objective.");

        void Validate(int room, float x, float y, float z)
        {
            var position = new System.Numerics.Vector3(x, y, z);
            if (!_objectives.ValidateFrozen(dungeon, Layout, districts, room, position, validatePoint, out string failure))
                throw new InvalidDataException($"Mission {QuestId} cannot restore its accepted objective in room {room} at {position}: {failure}");
        }
    }

    void SpawnObjectiveItem(int[] roomDistances, PlayfieldLocality locality)
    {
        if (_objectiveItem == null)
            return;
        if (_objectiveItem.RoomIndex >= roomDistances.Length || roomDistances[_objectiveItem.RoomIndex] <= 0)
            throw new InvalidDataException("Accepted objective room is unavailable.");
        var item = new MissionObjectiveItem(_objectiveItem,
            GetRequiredService<IItemBuilder>().CreateTemplate(_objectiveItem.LowId, _objectiveItem.HighId, _objectiveItem.Quality),
            _collectObjective!)
        {
            Playfield = this,
            Position = new AORebirth.Core.Vector.Vector3(_objectiveItem.X, _objectiveItem.Y, _objectiveItem.Z),
            Rotation = new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1)
        };
        if (!GetRequiredService<DynelRegistry>().TryRegister(item))
            throw new InvalidDataException("Accepted objective identity is already registered.");
        locality.RegisterDynel(item);
    }

    void SpawnFloorTransitions(PlayfieldLocality locality)
    {
        var registry = GetRequiredService<DynelRegistry>();
        foreach (var transition in Layout.FloorTransitions)
        {
            SpawnEndpoint(transition.From, transition.To);
            SpawnEndpoint(transition.To, transition.From);
        }

        void SpawnEndpoint(AORebirth.DungeonGenerator.DungeonTransitionEndpoint source,
            AORebirth.DungeonGenerator.DungeonTransitionEndpoint target)
        {
            foreach (int templateId in new[] { source.PlatformTemplateId, source.ButtonTemplateId })
            {
                if (!ItemTemplates.TryGet(templateId, out ItemTemplate? template) || template == null)
                    throw new InvalidDataException("Dungeon floor transition item template is unavailable: " + templateId);
                var dynel = new DungeonFloorTransitionDynel(registry.AllocateStaticDynelIdentity(),
                    template, new AORebirth.Core.Vector.Vector3(target.Position.X, target.Position.Y, target.Position.Z))
                {
                    Playfield = this,
                    Position = new AORebirth.Core.Vector.Vector3(source.Position.X, source.Position.Y, source.Position.Z),
                    // Platform/button updates retain world identity heading even in rotated captured rooms.
                    Rotation = new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1)
                };
                registry.Register(dynel);
                locality.RegisterDynel(dynel);
            }
        }
    }

    /// <summary>First door instance; doors are numbered in layout order, exit first, as live dungeons do.</summary>
    const int FirstDoorInstance = 0x10000001;

    /// <summary>
    /// One Door per layout doorway, from the style's door templates (the exit has its own). The exit always starts
    /// open; interior doors roll locked (<see cref="AORebirth.DungeonGenerator.DungeonDoorLocks"/>) or open from the
    /// dungeon seed, so a rebuilt dungeon gets the same doors.
    /// </summary>
    void SpawnDoors(IReadOnlyList<AORebirth.World.Collision.DungeonDoorPlacement> placements, PlayfieldLocality locality)
    {
        DungeonDoorStyle? style = Layout.Doors;
        if (style == null || placements.Count == 0)
            return;

        var registry = GetRequiredService<DynelRegistry>();
        for (int i = 0; i < placements.Count; i++)
        {
            AORebirth.World.Collision.DungeonDoorPlacement placement = placements[i];
            int templateId = placement.IsExit ? style.ExitTemplate : style.DoorTemplate;
            if (!ItemTemplates.TryGet(templateId, out ItemTemplate? template) || template == null)
            {
                if (Layout.Content != null)
                    throw new InvalidDataException("Accepted dungeon door template is unavailable: " + templateId);
                Logger.Warn("Dungeon door template " + templateId + " is not in items.dat; door skipped");
                continue;
            }

            int flags = template.Stats.TryGetValue(CharacterStat.Flags, out int baseFlags) ? baseFlags : 0;
            flags &= ~(Door.OpenFlag | Door.LockedFlag);
            if (placement.IsExit)
                flags |= Door.OpenFlag;
            else
            {
                // Same seed and door index as the generator (and its viewer), so both agree on which doors are locked.
                var roll = new Random(unchecked((Layout.Seed * 397) ^ (i + 1)));
                if (AORebirth.DungeonGenerator.DungeonDoorLocks.IsLocked(Layout.Seed, i, style.LockedChancePercent))
                    flags |= Door.LockedFlag;
                else if (roll.Next(100) < style.OpenChancePercent)
                    flags |= Door.OpenFlag;
            }

            var door = new Door(new Identity { Type = IdentityType.Door, Instance = FirstDoorInstance + i }, templateId, flags,
                (placement.Room << 16) | (placement.LinkedRoom & 0xFFFF), lockDifficulty: Math.Max(1, Quality))
            {
                Playfield = this,
                Position = new AORebirth.Core.Vector.Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z),
                Rotation = Door.Heading(placement.YawDegrees)
            };
            registry.Register(door);
            locality.RegisterDynel(door);
            if (_simulation != null && !_simulation.RegisterDoorBlocker(door))
                Logger.Warn("Dungeon door template " + templateId + " has no collision sphere; it does not block sight");

            if (placement.IsExit)
                RegisterExit(door);
        }
    }

    /// <summary>
    /// The exit door's walk-out volume: back outside the ACG entrance, a portal exit's clearance in front of the
    /// entrance along its heading, so the character lands clear of the entrance's own walk-in trigger.
    /// </summary>
    void RegisterExit(Door exit)
    {
        if (_simulation == null)
        {
            Logger.Warn("Quest dungeon " + Identity.Instance + " has no world simulation; its exit door has no walk-out");
            return;
        }

        var entrancePosition = new AORebirth.Core.Vector.Vector3(Entrance.LocalX, Entrance.LocalY, Entrance.LocalZ);
        // Raw placement components are WXYZ: exact-identity comparisons against the current Dynels reader
        // match all 2,242 catalog rows. The engine's Quaternion constructor takes XYZW.
        var entranceHeading = new AORebirth.Core.Vector.Quaternion(
            Entrance.RotationComponent1, Entrance.RotationComponent2, Entrance.RotationComponent3, Entrance.RotationComponent0);
        AORebirth.Core.Vector.Vector3 landing = PortalDoorLandingResolver.LandingInFront(
            entrancePosition, entranceHeading, PortalDoorLandingResolver.ExitDoorClearance);
        _simulation.RegisterDungeonExit(exit.Position.xf, exit.Position.yf, exit.Position.zf, exit.Identity.Instance,
            Entrance.PlayfieldId, landing, entranceHeading, exit.TemplateId, exit.Rotation);
    }

    protected override void OnDispose()
    {
        ChestPopulation?.Retire();
        _simulation?.Dispose();
        _simulation = null;
        base.OnDispose();
    }

    /// <summary>The captured ACG PlayfieldAnarchyF shape: building identity plus the generator the client builds from.</summary>
    public override PlayfieldAnarchyFMessage CreatePlayfieldAnarchyFMessage(Vector3 coordinates) => new()
    {
        Identity = new Identity { Type = IdentityType.Playfield2, Instance = Identity.Instance },
        CharacterCoordinates = coordinates,
        PlayfieldId1 = Layout.Generator.Identity,
        Unknown3 = 0,
        Unknown4 = 0,
        PlayfieldId2 = new Identity { Type = IdentityType.Playfield2, Instance = Identity.Instance },
        GeneratorPayload = Layout.GeneratorPayload,
        PlayfieldX = -1,
        PlayfieldZ = -1
    };
}
