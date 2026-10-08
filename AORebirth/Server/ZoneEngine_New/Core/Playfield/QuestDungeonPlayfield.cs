namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;

using AORebirth.Interfaces.Persistence.Shops;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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

    public QuestDungeonPlayfield(int playfieldId, string questId, DungeonLayout layout, MissionEntrance entrance,
        string? targetHash, int quality, IZoneLogger logger, IMessageRouter router, PlayfieldManager manager, PlayerHydrator hydrator, IGameData data,
        IItemBuilder items, HashItemMinter hashItems, IInventoryRepository inventory, IItemInstanceIdAllocator ids,
        InventoryMoveService moves, InventoryFlushService flush, TradeService trades, CharacterSnapshotService snapshot,
        IPlayfieldMetricsRegistry metrics, IShopDao shopDao)
        : base(new Identity { Type = IdentityType.Playfield, Instance = playfieldId }, logger, router,
            manager, hydrator, data, items, hashItems, inventory, ids, moves, flush, trades, snapshot, metrics, shopDao)
    {
        QuestId = questId ?? throw new ArgumentNullException(nameof(questId));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Entrance = entrance ?? throw new ArgumentNullException(nameof(entrance));
        TargetHash = string.IsNullOrWhiteSpace(targetHash) ? null : targetHash;
        Quality = Math.Max(0, quality);
    }

    public string QuestId { get; }

    public DungeonLayout Layout { get; }

    /// <summary>Where the dungeon is entered from, and where its occupants return to.</summary>
    public MissionEntrance Entrance { get; }

    /// <summary>The NPC hash a kill-target quest wants killed here; null when the quest has no kill target.</summary>
    public string? TargetHash { get; }

    /// <summary>The mission's quality level (0 when unknown); locked doors use it as their lock difficulty.</summary>
    public int Quality { get; }

    public override void Build()
    {
        if (IsBuilt) return;
        var locality = GetRequiredService<PlayfieldLocality>();
        AORebirth.World.Collision.DungeonWorldLayout? dungeon = DungeonPlayfieldBinder.TryBuild(
            GameData.RootPath, Identity.Instance, Layout.Generator, Logger);
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
            SpawnDoors(dungeon.Doors, locality);
            AddDistrictSpawns(dungeon);
        }

        MarkBuilt();
    }

    /// <summary>
    /// TEMPORARY: every district spawn point spawns this hash until mission content (type, faction, level) picks what
    /// lives in the dungeon.
    /// </summary>
    const string TemporaryNpcHash = "ZK3X";

    /// <summary>
    /// TEMPORARY: chance that each district spawn point of a room is used. Every room still gets at least one NPC when
    /// its district has any spawn point.
    /// </summary>
    const int TemporarySpawnPointChancePercent = 35;

    static readonly ConcurrentDictionary<(string Root, int Style), PlayfieldDistrictsData?> StyleDistricts = new();

    static readonly JsonSerializerOptions DistrictJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The style playfield's Districts.json maps each template room (zone) to a district whose spawn points are in
    /// the style's world space; each placed room moves its district's points into the dungeon with the room's own
    /// transform, and they become ordinary hash spawn points of this playfield. Each room uses a random share of its
    /// points, at least one, rolled from the dungeon seed so a rebuilt dungeon spawns in the same places. Quest dungeon
    /// NPCs do not respawn once killed.
    /// </summary>
    void AddDistrictSpawns(AORebirth.World.Collision.DungeonWorldLayout dungeon)
    {
        IReadOnlyList<AORebirth.World.Collision.DungeonRoomPlacement> placements = dungeon.Placements;
        if (placements.Count == 0)
            return;

        PlayfieldDistrictsData? districts = LoadStyleDistricts(GameData.RootPath, Layout.Generator.Style);
        if (districts?.Districts == null)
            return;

        var byIndex = new Dictionary<int, PlayfieldDistrictEntry>();
        foreach (PlayfieldDistrictEntry district in districts.Districts)
        {
            if (district != null)
                byIndex[district.DistrictIndex] = district;
        }

        // Each placed room's district and its usable spawn points.
        var rooms = new List<(AORebirth.World.Collision.DungeonRoomPlacement Placement, PlayfieldDistrictEntry District, List<PlayfieldDistrictSpawnPoint> Points)>();
        foreach (AORebirth.World.Collision.DungeonRoomPlacement placement in placements)
        {
            int zone = placement.Template.InstanceId;
            int districtIndex = districts.ZoneToDistrictMap is { } map && zone >= 0 && zone < map.Length ? map[zone] : zone;
            if (!byIndex.TryGetValue(districtIndex, out PlayfieldDistrictEntry? district))
                continue;

            var points = new List<PlayfieldDistrictSpawnPoint>();
            foreach (PlayfieldDistrictSpawnPoint point in district.SpawnPoints ?? [])
            {
                if (point?.Position is { Length: >= 3 })
                    points.Add(point);
            }

            if (points.Count > 0)
                rooms.Add((placement, district, points));
        }

        var spawns = GetRequiredService<HashSpawnSystem>();
        (int Room, PlayfieldDistrictSpawnPoint Point)? target = TargetHash == null ? null : PlaceTarget(dungeon, rooms, spawns);

        int added = 0;
        int skipped = 0;
        foreach ((AORebirth.World.Collision.DungeonRoomPlacement placement, PlayfieldDistrictEntry district, List<PlayfieldDistrictSpawnPoint> points) in rooms)
        {
            // The target's point is taken; the target counts as that room's NPC.
            bool holdsTarget = target is { } held && held.Room == placement.Index;
            var candidates = holdsTarget ? points.FindAll(point => !ReferenceEquals(point, target!.Value.Point)) : new List<PlayfieldDistrictSpawnPoint>(points);
            if (candidates.Count == 0)
                continue;

            var roll = new Random(unchecked((Layout.Seed * 7919) ^ ((placement.Index + 1) * 104729)));
            roll.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(candidates));
            int wanted = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (roll.Next(100) < TemporarySpawnPointChancePercent)
                    wanted++;
            }

            if (!holdsTarget)
                wanted = Math.Max(1, wanted);
            int placedInRoom = 0;
            foreach (PlayfieldDistrictSpawnPoint point in candidates)
            {
                if (placedInRoom >= wanted)
                    break;

                float[] position = point.Position;
                System.Numerics.Vector3 placed = placement.TransformTemplatePoint(
                    new System.Numerics.Vector3(position[0], position[1], position[2]));
                if (spawns.AddPlacedDistrictPoint(TemporaryNpcHash, district,
                        new AORebirth.Core.Vector.Vector3(placed.X, placed.Y, placed.Z), point.Radius,
                        useDistrictLevels: false, respawns: false))
                {
                    placedInRoom++;
                    added++;
                }
                else
                    skipped++;
            }
        }

        Logger.Info("Quest dungeon " + QuestId + " spawn points=" + added + " skipped=" + skipped);
    }

    /// <summary>
    /// Places the kill target at a spawn point of a room as far from the entrance (in doors) as the layout goes,
    /// picked from the seed so a rebuilt dungeon puts it in the same place. Only rooms reachable from the entrance
    /// count. Falls back to nearer rooms when the farthest have no spawnable point. The target spawns once.
    /// </summary>
    (int Room, PlayfieldDistrictSpawnPoint Point)? PlaceTarget(AORebirth.World.Collision.DungeonWorldLayout dungeon,
        List<(AORebirth.World.Collision.DungeonRoomPlacement Placement, PlayfieldDistrictEntry District, List<PlayfieldDistrictSpawnPoint> Points)> rooms,
        HashSpawnSystem spawns)
    {
        int count = dungeon.Placements.Count;
        var links = new List<int>[count];
        for (int i = 0; i < count; i++)
            links[i] = [];
        foreach (AORebirth.World.Collision.DungeonDoorPlacement door in dungeon.Doors)
        {
            if (door.IsExit || door.Room < 0 || door.Room >= count || door.LinkedRoom < 0 || door.LinkedRoom >= count)
                continue;
            links[door.Room].Add(door.LinkedRoom);
            links[door.LinkedRoom].Add(door.Room);
        }

        var distance = new int[count];
        Array.Fill(distance, -1);
        distance[0] = 0;
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int room = queue.Dequeue();
            foreach (int next in links[room])
            {
                if (distance[next] >= 0)
                    continue;
                distance[next] = distance[room] + 1;
                queue.Enqueue(next);
            }
        }

        var roll = new Random(unchecked(Layout.Seed * 31337));
        var byDistance = new List<int>();
        for (int i = 0; i < rooms.Count; i++)
        {
            if (distance[rooms[i].Placement.Index] > 0)
                byDistance.Add(i);
        }

        // Farthest first; rooms at the same distance in a seeded order.
        var order = byDistance.Select(i => (Index: i, Tie: roll.Next())).OrderByDescending(pair => distance[rooms[pair.Index].Placement.Index])
            .ThenBy(pair => pair.Tie).Select(pair => pair.Index).ToList();
        foreach (int i in order)
        {
            (AORebirth.World.Collision.DungeonRoomPlacement placement, PlayfieldDistrictEntry district, List<PlayfieldDistrictSpawnPoint> points) = rooms[i];
            var shuffled = new List<PlayfieldDistrictSpawnPoint>(points);
            roll.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shuffled));
            foreach (PlayfieldDistrictSpawnPoint point in shuffled)
            {
                System.Numerics.Vector3 placed = placement.TransformTemplatePoint(
                    new System.Numerics.Vector3(point.Position[0], point.Position[1], point.Position[2]));
                if (!spawns.AddPlacedDistrictPoint(TargetHash!, district, new AORebirth.Core.Vector.Vector3(placed.X, placed.Y, placed.Z),
                        point.Radius, useDistrictLevels: false, respawns: false))
                    continue;

                Logger.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Quest dungeon {0} kill target {1} in room {2} ({3}), {4} doors from the entrance, at ({5:0.#}, {6:0.#}, {7:0.#})",
                    QuestId, TargetHash, placement.Index, placement.Template.Name, distance[placement.Index], placed.X, placed.Y, placed.Z));
                return (placement.Index, point);
            }
        }

        Logger.Warn("Quest dungeon " + QuestId + ": kill target " + TargetHash + " could not be placed (unspawnable hash or no reachable spawn point)");
        return null;
    }

    PlayfieldDistrictsData? LoadStyleDistricts(string root, int style)
        => StyleDistricts.GetOrAdd((root, style), key =>
        {
            string path = Path.Combine(key.Root, GameDataPaths.PlayfieldDistrictsRelativePath(key.Style));
            if (!File.Exists(path))
            {
                Logger.Warn("No Districts.json for dungeon style " + key.Style + " at " + path + "; dungeon has no NPC spawns");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<PlayfieldDistrictsData>(File.ReadAllText(path), DistrictJsonOptions);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to read " + path);
                return null;
            }
        });

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

        var entrancePosition = new AORebirth.Core.Vector.Vector3(Entrance.X, Entrance.Y, Entrance.Z);
        var entranceHeading = new AORebirth.Core.Vector.Quaternion(Entrance.HeadingX, Entrance.HeadingY, Entrance.HeadingZ, Entrance.HeadingW);
        AORebirth.Core.Vector.Vector3 landing = PortalDoorLandingResolver.LandingInFront(
            entrancePosition, entranceHeading, PortalDoorLandingResolver.ExitDoorClearance);
        _simulation.RegisterDungeonExit(exit.Position.xf, exit.Position.yf, exit.Position.zf, exit.Identity.Instance,
            Entrance.Playfield, landing, entranceHeading, exit.TemplateId, exit.Rotation);
    }

    protected override void OnDispose()
    {
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
