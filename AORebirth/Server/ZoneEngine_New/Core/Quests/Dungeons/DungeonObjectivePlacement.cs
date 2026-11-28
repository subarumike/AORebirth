namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.GameData;
using AORebirth.World.Collision;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.WorldSimulation;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using Vector3 = System.Numerics.Vector3;

public interface IMissionDungeonObjectivePlacement
{
    bool TrySelect(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
        out int roomIndex, out AoVector3 position, Func<int, Vector3, string?>? validatePoint = null);

    Func<int, Vector3, string?> CreatePhysicalValidator(DungeonWorldLayout dungeon, DungeonLayout layout,
        PlayfieldDistrictsData? districts, PlayfieldWorldSimulation world, CollisionSphereData? player,
        MissionChestPlacementPolicy? policy);

    bool ValidateFrozen(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
        int roomIndex, Vector3 position, Func<int, Vector3, string?> validatePoint, out string failure);
}

/// <summary>Chooses objective points from original district positions in connected placed rooms.</summary>
public sealed class DungeonObjectivePlacement : IMissionDungeonObjectivePlacement
{
    readonly record struct Candidate(int Room, int Distance, Vector3 Position);

    internal static bool TrySelect(string gameDataRoot, DungeonLayout layout, IZoneLogger logger,
        out int roomIndex, out AoVector3 position)
    {
        var dungeon = DungeonPlayfieldBinder.TryBuild(gameDataRoot, 0, layout.Generator, logger);
        var districts = QuestDungeonPlayfield.LoadStyleDistricts(gameDataRoot, layout.Generator.Style, logger);
        if (dungeon != null)
            return new DungeonObjectivePlacement().TrySelect(dungeon, layout, districts, out roomIndex, out position);
        roomIndex = -1;
        position = new AoVector3();
        return false;
    }

    public bool TrySelect(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
        out int roomIndex, out AoVector3 position, Func<int, Vector3, string?>? validatePoint = null)
    {
        roomIndex = -1;
        position = new AoVector3();
        var random = new Random(unchecked(layout.Seed * 15485863));
        foreach (var distance in Candidates(dungeon, layout, districts).GroupBy(row => row.Distance)
            .OrderByDescending(group => group.Key))
        {
            Candidate[] choices = distance.ToArray();
            int start = random.Next(choices.Length);
            for (int offset = 0; offset < choices.Length; offset++)
            {
                Candidate selected = choices[(start + offset) % choices.Length];
                if (validatePoint?.Invoke(selected.Room, selected.Position) != null)
                    continue;
                roomIndex = selected.Room;
                position = new AoVector3(selected.Position.X, selected.Position.Y, selected.Position.Z);
                return true;
            }
        }
        return false;
    }

    public Func<int, Vector3, string?> CreatePhysicalValidator(DungeonWorldLayout dungeon, DungeonLayout layout,
        PlayfieldDistrictsData? districts, PlayfieldWorldSimulation world, CollisionSphereData? player,
        MissionChestPlacementPolicy? policy)
        => MissionChestPlacement.CreateObjectiveValidator(dungeon, layout, districts, world, player, policy);

    public bool ValidateFrozen(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
        int roomIndex, Vector3 position, Func<int, Vector3, string?> validatePoint, out string failure)
    {
        ArgumentNullException.ThrowIfNull(validatePoint);
        int[] distances = RoomDistances(dungeon, layout);
        string? reason = roomIndex < 0 || roomIndex >= distances.Length || distances[roomIndex] <= 0
            ? "room is not connected to the entrance"
            : !Candidates(dungeon, districts, distances, roomIndex).Any(candidate => candidate.Position == position)
                ? "position is not an exact retained district point"
                : validatePoint(roomIndex, position);
        failure = reason == null ? string.Empty : FormattableString.Invariant(
            $"Accepted objective room {roomIndex} at ({position.X:R}, {position.Y:R}, {position.Z:R}): {reason}.");
        return reason == null;
    }

    static IEnumerable<Candidate> Candidates(DungeonWorldLayout dungeon, DungeonLayout layout,
        PlayfieldDistrictsData? districts)
        => Candidates(dungeon, districts, RoomDistances(dungeon, layout));

    static IEnumerable<Candidate> Candidates(DungeonWorldLayout dungeon, PlayfieldDistrictsData? districts,
        int[] distances, int? selectedRoom = null)
    {
        if (districts?.Districts == null)
            yield break;
        var byIndex = new Dictionary<int, PlayfieldDistrictEntry>();
        foreach (var district in districts.Districts)
            if (district != null)
                byIndex[district.DistrictIndex] = district;
        foreach (var placement in dungeon.Placements)
        {
            if (distances[placement.Index] <= 0 || (selectedRoom.HasValue && placement.Index != selectedRoom))
                continue;
            int zone = placement.Template.InstanceId;
            int districtIndex = districts.ZoneToDistrictMap is { } map && zone >= 0 && zone < map.Length ? map[zone] : zone;
            if (!byIndex.TryGetValue(districtIndex, out var district))
                continue;
            foreach (var point in district.SpawnPoints ?? [])
            {
                if (point?.Position is not { Length: >= 3 } raw)
                    continue;
                var placed = placement.TransformTemplatePoint(new Vector3(raw[0], raw[1], raw[2]));
                if (float.IsFinite(placed.X) && float.IsFinite(placed.Y) && float.IsFinite(placed.Z))
                    yield return new Candidate(placement.Index, distances[placement.Index], placed);
            }
        }
    }

    internal static int[] RoomDistances(DungeonWorldLayout dungeon, DungeonLayout layout)
    {
        int count = dungeon.Placements.Count;
        var links = new List<int>[count];
        for (int i = 0; i < count; i++)
            links[i] = [];
        foreach (var door in dungeon.Doors)
        {
            if (door.IsExit || door.Room < 0 || door.Room >= count || door.LinkedRoom < 0 || door.LinkedRoom >= count)
                continue;
            links[door.Room].Add(door.LinkedRoom);
            links[door.LinkedRoom].Add(door.Room);
        }
        foreach (var transition in layout.FloorTransitions)
        {
            links[transition.From.RoomIndex].Add(transition.To.RoomIndex);
            links[transition.To.RoomIndex].Add(transition.From.RoomIndex);
        }
        var distance = new int[count];
        Array.Fill(distance, -1);
        if (count == 0)
            return distance;
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
        return distance;
    }
}
