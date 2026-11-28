namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using AORebirth.Core.GameData;
using AORebirth.World.Collision;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;

using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

public interface IMissionDungeonPopulationModule
{
    void Populate(QuestDungeonPlayfield world, DungeonWorldLayout dungeon, PlayfieldDistrictsData? districts,
        MissionNpcObjectiveBinding? objectiveNpc, bool spawnNpcObjective, IGameData gameData, IZoneLogger logger, int[] roomDistances);
}

/// <summary>Registers the accepted objective and the ordinary district population with the existing one-shot spawn service.</summary>
internal sealed class MissionDungeonPopulationModule : IMissionDungeonPopulationModule
{
    internal static IMissionDungeonPopulationModule Shared { get; } = new MissionDungeonPopulationModule();

    public void Populate(QuestDungeonPlayfield world, DungeonWorldLayout dungeon, PlayfieldDistrictsData? districts,
        MissionNpcObjectiveBinding? objectiveNpc, bool spawnNpcObjective, IGameData gameData, IZoneLogger logger, int[] roomDistances)
    {
        if (roomDistances.Length != dungeon.Placements.Count)
            throw new InvalidDataException("The dungeon population room graph does not match the layout.");

        NpcTemplateCatalog? retainedTemplates = world.Layout.NpcTemplates;
        if (world.Layout.Content != null && retainedTemplates == null)
            throw new InvalidDataException("The accepted dungeon has no retained NPC templates.");

        if (objectiveNpc != null)
        {
            world.TokenProgress?.RegisterObjective(objectiveNpc, gameData, retainedTemplates);
            SpawnObjectiveNpc(world, objectiveNpc, spawnNpcObjective, gameData, logger, roomDistances);
        }
        AddDistrictSpawns(world, dungeon, districts, objectiveNpc, gameData, logger, roomDistances);
    }

    /// <summary>
    /// The style playfield's Districts.json maps each template room (zone) to a district whose spawn points are in
    /// the style's world space; each placed room moves its district's points into the dungeon with the room's own
    /// transform, and they become ordinary hash spawn points of this playfield. Each room uses a random share of its
    /// points, at least one, rolled from the dungeon seed so a rebuilt dungeon spawns in the same places. Quest dungeon
    /// NPCs do not respawn once killed.
    /// </summary>
    static void AddDistrictSpawns(QuestDungeonPlayfield world, DungeonWorldLayout dungeon,
        PlayfieldDistrictsData? districts, MissionNpcObjectiveBinding? npcObjective, IGameData gameData,
        IZoneLogger logger, int[] roomDistances)
    {
        IReadOnlyList<DungeonRoomPlacement> placements = dungeon.Placements;
        if (placements.Count == 0)
            return;

        bool frozen = world.Layout.Content != null;
        if (districts?.Districts == null)
        {
            if (frozen)
                throw new InvalidDataException("The accepted dungeon has no retained NPC district data.");
            return;
        }

        var byIndex = new Dictionary<int, PlayfieldDistrictEntry>();
        foreach (PlayfieldDistrictEntry district in districts.Districts)
        {
            if (district != null)
                byIndex[district.DistrictIndex] = district;
        }

        // Each placed room's district and its usable spawn points.
        var rooms = new List<(DungeonRoomPlacement Placement, PlayfieldDistrictEntry District, List<PlayfieldDistrictSpawnPoint> Points)>();
        foreach (DungeonRoomPlacement placement in placements)
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

        var spawns = world.GetRequiredService<HashSpawnSystem>();
        (int Room, PlayfieldDistrictSpawnPoint Point)? target = npcObjective != null || world.TargetHash == null
            ? null : PlaceTarget(world, rooms, spawns, roomDistances, logger);

        DungeonPopulationPolicy population = world.Layout.Population;
        NpcTemplateCatalog? retainedTemplates = world.Layout.NpcTemplates;
        bool populationValid = retainedTemplates == null ? population.IsValid(gameData) : population.IsValid(retainedTemplates.HasTemplate);
        if (!populationValid)
        {
            if (frozen)
                throw new InvalidDataException("The accepted dungeon NPC population policy or its required templates are unavailable.");
            logger.Warn("Quest dungeon " + world.QuestId + ": ordinary NPC policy is unavailable; no placeholder population selected");
            return;
        }

        int added = 0;
        int skipped = 0;
        foreach ((DungeonRoomPlacement placement, PlayfieldDistrictEntry district, List<PlayfieldDistrictSpawnPoint> points) in rooms)
        {
            // The target's point is taken; the target counts as that room's NPC.
            bool holdsFrozenTarget = npcObjective?.RoomIndex == placement.Index;
            bool holdsTarget = holdsFrozenTarget || target is { } held && held.Room == placement.Index;
            var candidates = points.FindAll(point =>
                !(target is { } legacyTarget && legacyTarget.Room == placement.Index && ReferenceEquals(point, legacyTarget.Point))
                && !(holdsFrozenTarget && IsObjectiveNpcPoint(npcObjective!, placement, point)));
            if (candidates.Count == 0)
                continue;

            var roll = new Random(unchecked((world.Layout.Seed * 7919) ^ ((placement.Index + 1) * 104729)));
            roll.Shuffle(CollectionsMarshal.AsSpan(candidates));
            int wanted = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (roll.Next(100) < population.SpawnPointChancePercent)
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
                string hash = population.RegularNpcHashes[roll.Next(population.RegularNpcHashes.Length)];
                int pointIndex = Array.IndexOf(district.SpawnPoints!, point);
                string pointKey = "r" + placement.Index.ToString(CultureInfo.InvariantCulture) + "p" + pointIndex.ToString(CultureInfo.InvariantCulture);
                int? transformSeed = frozen ? unchecked((((world.Layout.Seed * 397) ^ placement.Index) * 397) ^ pointIndex) : null;
                MissionTokenProgress.SpawnPoint? frozenPoint = null;
                if (world.TokenProgress != null && district.RespawnChance > 0)
                {
                    frozenPoint = world.TokenProgress.ResolvePoint(pointKey, hash, Math.Max(1, world.Quality),
                        gameData, retainedTemplates, transformSeed);
                    if (frozenPoint == null)
                    {
                        if (retainedTemplates != null && !world.TokenProgress.HasStoredCensus)
                            throw MissingRetainedFamily(hash);
                        continue;
                    }
                    hash = frozenPoint.Hash;
                }
                MobTemplate[]? templates = frozenPoint?.Templates;
                if (retainedTemplates != null && templates == null && district.RespawnChance > 0)
                {
                    var branches = new List<MobTemplate>();
                    retainedTemplates.CollectSpawns(hash, world.Quality, branches, transformSeed is int seed ? new Random(seed) : null);
                    templates = branches.Where(template => !template.UnresolvedPlaceholder && NpcTemplateValidation.CanSpawn(template)).ToArray();
                    if (templates.Length == 0)
                        throw MissingRetainedFamily(hash);
                }
                if (spawns.AddPlacedDistrictPoint(hash, district,
                        new Vector3(placed.X, placed.Y, placed.Z), point.Radius,
                        useDistrictLevels: false, respawns: false, requestedLevel: frozenPoint?.Level ?? (world.Quality > 0 ? world.Quality : null),
                        frozenTemplates: templates,
                        onBranchSpawned: frozenPoint == null ? null : (actor, branch) => world.TokenProgress!.BindActor(pointKey, actor, branch),
                        isBranchKilled: frozenPoint == null ? null : branch => world.TokenProgress!.IsKilled(pointKey, branch),
                        initialTransformSeed: transformSeed, useRetainedTemplates: retainedTemplates != null && templates != null))
                {
                    if (frozenPoint != null)
                    {
                        world.TokenProgress!.RegisterPoint(frozenPoint);
                    }
                    placedInRoom++;
                    added++;
                }
                else
                    skipped++;
            }
        }

        logger.Info("Quest dungeon " + world.QuestId + " spawn points=" + added + " skipped=" + skipped
            + " requestedNpcLevel=" + world.Quality + " families=" + string.Join(",", population.RegularNpcHashes));
    }

    static InvalidDataException MissingRetainedFamily(string hash)
        => new("The accepted dungeon has no spawnable retained NPC family '" + hash + "'.");

    static bool IsObjectiveNpcPoint(MissionNpcObjectiveBinding objectiveNpc, DungeonRoomPlacement placement,
        PlayfieldDistrictSpawnPoint point)
    {
        System.Numerics.Vector3 placed = placement.TransformTemplatePoint(
            new System.Numerics.Vector3(point.Position[0], point.Position[1], point.Position[2]));
        return placed.X == objectiveNpc.X && placed.Y == objectiveNpc.Y && placed.Z == objectiveNpc.Z;
    }

    static void SpawnObjectiveNpc(QuestDungeonPlayfield world, MissionNpcObjectiveBinding objectiveNpc, bool spawnNpcObjective,
        IGameData gameData, IZoneLogger logger, int[] roomDistances)
    {
        if (!spawnNpcObjective || world.TokenProgress?.IsKilled("objective", 0) == true)
            return;
        if (objectiveNpc.RoomIndex >= roomDistances.Length || roomDistances[objectiveNpc.RoomIndex] <= 0)
            throw new InvalidDataException("Accepted NPC objective room is unavailable.");
        bool resolved = world.Layout.NpcTemplates is { } retained
            ? retained.TryResolve(objectiveNpc.NpcHash, objectiveNpc.Quality, out MobTemplate template)
            : gameData.TryResolveMobTemplate(objectiveNpc.NpcHash, objectiveNpc.Quality, out template);
        if (!resolved || template.UnresolvedPlaceholder || !NpcTemplateValidation.CanSpawn(template)
            || !string.Equals(template.Hash, objectiveNpc.NpcHash, StringComparison.Ordinal)
            || (objectiveNpc.MissionType == 0 && template.Has(NpcFeature.NoCombat)))
            throw new InvalidDataException("Accepted NPC objective template is unavailable.");

        int? objectiveScale = objectiveNpc.ScalePercent;
        NpcCharacter npc = world.GetRequiredService<SpawnService>().SpawnMob(template,
            new Vector3(objectiveNpc.X, objectiveNpc.Y, objectiveNpc.Z),
            new Quaternion(0, 0, 0, 1), objectiveNpc.Quality,
            SpawnSource.HashSpawn, spawnHash: objectiveNpc.NpcHash,
            configure: actor =>
            {
                if (objectiveScale is int scale)
                {
                    actor.Stats.Set(CharacterStat.Scale, scale);
                }
            },
            fixedIdentity: objectiveNpc.Identity);
        world.TokenProgress?.BindActor("objective", npc, 0);
        logger.Info(string.Format(CultureInfo.InvariantCulture,
            "Quest dungeon {0} objective NPC {1}:{2} hash={3} in room {4} at ({5:0.#}, {6:0.#}, {7:0.#}) scale={8}",
            world.QuestId, (int)npc.Identity.Type, npc.Identity.Instance, objectiveNpc.NpcHash,
            objectiveNpc.RoomIndex, npc.Position.xf, npc.Position.yf, npc.Position.zf,
            npc.Stats.GetOrZero(CharacterStat.Scale)));
    }

    /// <summary>
    /// Places the kill target at a spawn point of a room as far from the entrance (in doors) as the layout goes,
    /// picked from the seed so a rebuilt dungeon puts it in the same place. Only rooms reachable from the entrance
    /// count. Falls back to nearer rooms when the farthest have no spawnable point. The target spawns once.
    /// </summary>
    static (int Room, PlayfieldDistrictSpawnPoint Point)? PlaceTarget(QuestDungeonPlayfield world,
        List<(DungeonRoomPlacement Placement, PlayfieldDistrictEntry District, List<PlayfieldDistrictSpawnPoint> Points)> rooms,
        HashSpawnSystem spawns, int[] distance, IZoneLogger logger)
    {
        var roll = new Random(unchecked(world.Layout.Seed * 31337));
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
            (DungeonRoomPlacement placement, PlayfieldDistrictEntry district, List<PlayfieldDistrictSpawnPoint> points) = rooms[i];
            var shuffled = new List<PlayfieldDistrictSpawnPoint>(points);
            roll.Shuffle(CollectionsMarshal.AsSpan(shuffled));
            foreach (PlayfieldDistrictSpawnPoint point in shuffled)
            {
                System.Numerics.Vector3 placed = placement.TransformTemplatePoint(
                    new System.Numerics.Vector3(point.Position[0], point.Position[1], point.Position[2]));
                if (!spawns.AddPlacedDistrictPoint(world.TargetHash!, district, new Vector3(placed.X, placed.Y, placed.Z),
                        point.Radius, useDistrictLevels: false, respawns: false))
                    continue;

                logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Quest dungeon {0} kill target {1} in room {2} ({3}), {4} doors from the entrance, at ({5:0.#}, {6:0.#}, {7:0.#})",
                    world.QuestId, world.TargetHash, placement.Index, placement.Template.Name, distance[placement.Index], placed.X, placed.Y, placed.Z));
                return (placement.Index, point);
            }
        }

        logger.Warn("Quest dungeon " + world.QuestId + ": kill target " + world.TargetHash + " could not be placed (unspawnable hash or no reachable spawn point)");
        return null;
    }
}
