namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using AORebirth.Core.GameData;
using AORebirth.DungeonGenerator;
using AORebirth.World.Collision;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.WorldSimulation;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using Vector3 = System.Numerics.Vector3;

public sealed record MissionChestPosition(int RoomIndex, float X, float Y, float Z, int YawDegrees);

internal readonly record struct MissionChestRouteStep(Vector3 Point, bool Transition);

internal sealed record MissionChestProbeFailure(string Constraint, Vector3 From, Vector3 To,
    Vector3? Hit = null, Vector3? Normal = null, string? MeshSource = null, int? MeshCell = null,
    float? Distance = null, float? Limit = null);
internal sealed record MissionChestCandidateFailure(int Room, string Stage, Vector3 Point, MissionChestProbeFailure? Probe);
internal sealed record MissionChestGraphCut(int FromRoom, int ToRoom, Vector3 From, Vector3 To, bool ExplicitLink,
    bool? CachedWalk, bool ForwardWalk, bool ReverseWalk, MissionChestProbeFailure? ForwardFailure,
    MissionChestProbeFailure? ReverseFailure);
internal sealed record MissionChestDoorAnchors(int Room, int LinkedRoom, Vector3 Position, Vector3[] First, Vector3[] Second);
internal sealed record MissionChestSearchPass(int Pass, bool CoverSections, int Placed, MissionChestPlacementDiagnostics Details);

internal sealed class MissionChestPlacementDiagnostics
{
    public bool CaptureRouteWitnesses { get; init; } = true;
    public bool CaptureFailureDetails { get; init; }
    public Dictionary<int, Dictionary<string, int>> CandidateStages { get; } = new();
    public List<MissionChestCandidateFailure> CandidateFailures { get; } = new();
    public List<MissionChestGraphCut> GraphCuts { get; } = new();
    public List<MissionChestDoorAnchors> DoorAnchors { get; } = new();
    public int[] InitialReachableRooms { get; internal set; } = [];
    public Vector3? StartPoint { get; internal set; }
    public List<MissionChestSearchPass> SearchDetails { get; } = new();
    public int TotalCells { get; internal set; }
    public int EligibleRooms { get; internal set; }
    public int Requested { get; internal set; }
    public Dictionary<int, float> ReferenceHeights { get; } = new();
    public Dictionary<int, float[]> WallSliceHeights { get; } = new();
    public Dictionary<int, int> StructuralAnchorCounts { get; } = new();
    public Dictionary<int, int> RawStructuralAnchorCounts { get; } = new();
    public Dictionary<int, List<Vector3>> RawStructuralAnchorPoints { get; } = new();
    public Dictionary<int, List<Vector3>> StructuralAnchorPoints { get; } = new();
    public int LocalHeightSeeds { get; internal set; }
    public int OffReferencePlacements { get; internal set; }
    public Dictionary<int, int> CandidateCounts { get; } = new();
    public Dictionary<int, int> WallSectionCounts { get; } = new();
    public Dictionary<int, int> WallSectionsTried { get; } = new();
    public Dictionary<int, int> ReachableCandidateCounts { get; } = new();
    public Dictionary<int, int> AnchorCounts { get; } = new();
    public Dictionary<int, int> ReachableAnchorCounts { get; } = new();
    public Dictionary<int, float?> LandingHeightOffsets { get; } = new();
    public Dictionary<string, int> Rejections { get; } = new();
    public Dictionary<int, IReadOnlyList<MissionChestRouteStep>> Routes { get; } = new();
    public List<IReadOnlyList<MissionChestRouteStep>> RequiredRoutes { get; } = new();
    public List<IReadOnlyList<Vector3>> RequiredRouteTargets { get; } = new();
    public int InitiallyReachableAnchorGroups { get; internal set; }
    public int PreservedAnchorGroups { get; internal set; }
    public int LostAnchorSamples { get; internal set; }
    public int SearchPasses { get; internal set; }
    public int PrimaryPlaced { get; internal set; }
    public int? FallbackPlaced { get; internal set; }
    public int? RecoveryPrimaryPlaced { get; internal set; }
    public int? RecoveryFallbackPlaced { get; internal set; }

    internal void CopyFrom(MissionChestPlacementDiagnostics source)
    {
        TotalCells = source.TotalCells; EligibleRooms = source.EligibleRooms; Requested = source.Requested;
        LocalHeightSeeds = source.LocalHeightSeeds; OffReferencePlacements = source.OffReferencePlacements;
        InitialReachableRooms = source.InitialReachableRooms; StartPoint = source.StartPoint;
        CandidateStages.Clear();
        foreach (var pair in source.CandidateStages)
        {
            CandidateStages.Add(pair.Key, new Dictionary<string, int>(pair.Value));
        }
        CandidateFailures.Clear(); CandidateFailures.AddRange(source.CandidateFailures);
        GraphCuts.Clear(); GraphCuts.AddRange(source.GraphCuts);
        DoorAnchors.Clear(); DoorAnchors.AddRange(source.DoorAnchors);
        InitiallyReachableAnchorGroups = source.InitiallyReachableAnchorGroups;
        PreservedAnchorGroups = source.PreservedAnchorGroups; LostAnchorSamples = source.LostAnchorSamples;
        Copy(source.ReferenceHeights, ReferenceHeights); Copy(source.CandidateCounts, CandidateCounts);
        Copy(source.WallSliceHeights, WallSliceHeights); Copy(source.StructuralAnchorCounts, StructuralAnchorCounts);
        Copy(source.RawStructuralAnchorCounts, RawStructuralAnchorCounts);
        RawStructuralAnchorPoints.Clear();
        foreach (var pair in source.RawStructuralAnchorPoints)
        {
            RawStructuralAnchorPoints.Add(pair.Key, new List<Vector3>(pair.Value));
        }
        StructuralAnchorPoints.Clear();
        foreach (var pair in source.StructuralAnchorPoints)
        {
            StructuralAnchorPoints.Add(pair.Key, new List<Vector3>(pair.Value));
        }
        Copy(source.WallSectionCounts, WallSectionCounts); Copy(source.WallSectionsTried, WallSectionsTried);
        Copy(source.ReachableCandidateCounts, ReachableCandidateCounts); Copy(source.AnchorCounts, AnchorCounts);
        Copy(source.ReachableAnchorCounts, ReachableAnchorCounts); Copy(source.LandingHeightOffsets, LandingHeightOffsets);
        Copy(source.Rejections, Rejections); Copy(source.Routes, Routes);
        RequiredRoutes.Clear(); RequiredRoutes.AddRange(source.RequiredRoutes);
        RequiredRouteTargets.Clear(); RequiredRouteTargets.AddRange(source.RequiredRouteTargets);

        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        {
            to.Clear();
            foreach (var pair in from)
            {
                to.Add(pair.Key, pair.Value);
            }
        }
    }
}

/// <summary>
/// A bounded placement-time connectivity check, using the existing collision queries and frozen room/floor links.
/// It does not bake, load, or replace runtime navigation. Rejected or unproven positions are never substituted.
/// </summary>
public static class MissionChestPlacement
{
    internal const int MaximumWalkSamplesPerLink = 4096;

    internal static bool TryGetWalkSampleCount(float distance, float spacing, out int count)
    {
        count = 0;
        if (!float.IsFinite(distance) || distance < 0 || !float.IsFinite(spacing) || spacing <= 0)
            return false;
        float required = MathF.Ceiling(distance / spacing);
        if (!float.IsFinite(required) || required > MaximumWalkSamplesPerLink)
            return false;
        count = Math.Max(1, (int)required);
        return true;
    }

    public static IReadOnlyList<MissionChestPosition> Plan(DungeonWorldLayout dungeon, DungeonLayout layout,
        PlayfieldWorldSimulation simulation, IGameData gameData, IItemTemplateCatalog templates,
        IReadOnlyList<Vector3> exclusions, MissionChestPolicy policy, Random random)
        => Plan(dungeon, layout, simulation, gameData, templates, exclusions, policy, random, null);

    internal static IReadOnlyList<MissionChestPosition> Plan(DungeonWorldLayout dungeon, DungeonLayout layout,
        PlayfieldWorldSimulation simulation, IGameData gameData, IItemTemplateCatalog templates,
        IReadOnlyList<Vector3> exclusions, MissionChestPolicy policy, Random random,
        MissionChestPlacementDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(dungeon);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(exclusions);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(random);
        if (simulation.Surface == null || policy.Placement?.IsValid != true
            || !TryGetChestSphere(gameData, templates, policy, out CollisionSphereData chest)
            || gameData.PlayerCollisionSphere is not { Radius: > 0, CenterY: > 0 } player
            || !float.IsFinite(player.Radius) || !float.IsFinite(player.CenterY))
            return [];
        string? historicalRoot = layout.Content == null ? gameData.RootPath : null;
        StyleRoomSet styles = historicalRoot != null ? StyleRoomSet.Load(historicalRoot, layout.Generator.Style)
            : layout.RoomSet ?? throw new InvalidDataException("Accepted dungeon room resources are unavailable.");
        var shapes = styles.All.ToDictionary(shape => shape.RoomId);
        var transitions = layout.FloorTransitions.SelectMany(link => new[] { link.From.RoomIndex, link.To.RoomIndex }).ToHashSet();
        var eligible = dungeon.Placements.Where(placed => shapes.TryGetValue(placed.Template.InstanceId, out RoomShape? shape)
                && shape.Role is not (RoomRole.Entrance or RoomRole.Start) && !styles.IsMainHall(shape)
                && !transitions.Contains(placed.Index) && !policy.Placement.ExcludedRoomNameParts.Any(part =>
                    shape.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(placed => placed.Index).ToHashSet();
        int wanted = policy.RollChestCount(eligible.Count, random);
        if (diagnostics != null)
        {
            diagnostics.TotalCells = dungeon.Rooms.Count;
            diagnostics.EligibleRooms = eligible.Count;
            diagnostics.Requested = wanted;
        }
        if (wanted == 0)
            return [];
        var districts = historicalRoot != null
            ? QuestDungeonPlayfield.LoadStyleDistricts(historicalRoot, layout.Generator.Style, new NLogZoneLogger())
            : layout.Districts ?? throw new InvalidDataException("Accepted dungeon chest placement districts are unavailable.");
        var searchDetails = diagnostics == null ? null : new List<MissionChestSearchPass>();
        MissionChestPlacementDiagnostics? primaryDiagnostics = diagnostics == null ? null
            : new() { CaptureRouteWitnesses = diagnostics.CaptureRouteWitnesses, CaptureFailureDetails = diagnostics.CaptureFailureDetails };
        var primary = new PlacementRun(dungeon, layout, simulation, districts, exclusions, policy.Placement,
            chest, player, random, eligible, primaryDiagnostics, coverSections: false).Select(wanted);
        if (primaryDiagnostics != null)
        {
            searchDetails!.Add(new MissionChestSearchPass(1, false, primary.Count, primaryDiagnostics));
        }
        var result = primary;
        MissionChestPlacementDiagnostics? selectedDiagnostics = primaryDiagnostics;
        int? fallbackCount = null;
        int? recoveryPrimaryCount = null;
        int? recoveryFallbackCount = null;
        if (primary.Count < wanted)
        {
            // A second bounded search can recover missed wall sections or walking anchors.
            // Keep one complete validated population; never combine unchecked positions or reroll the count.
            MissionChestPlacementDiagnostics? fallbackDiagnostics = diagnostics == null ? null
                : new() { CaptureRouteWitnesses = diagnostics.CaptureRouteWitnesses, CaptureFailureDetails = diagnostics.CaptureFailureDetails };
            var fallback = new PlacementRun(dungeon, layout, simulation, districts, exclusions, policy.Placement,
                chest, player, random, eligible, fallbackDiagnostics, coverSections: true).Select(wanted);
            fallbackCount = fallback.Count;
            if (fallbackDiagnostics != null)
            {
                searchDetails!.Add(new MissionChestSearchPass(2, true, fallback.Count, fallbackDiagnostics));
            }
            if (fallback.Count > primary.Count)
            {
                result = fallback;
                selectedDiagnostics = fallbackDiagnostics;
            }
        }
        if (primary.Count == 0 && fallbackCount == 0)
        {
            // A zero-only recovery repeats the existing bounded pair once. Keep one complete
            // validated result and stop as soon as this recovery finds a reachable chest.
            MissionChestPlacementDiagnostics? recoveryDiagnostics = diagnostics == null ? null
                : new() { CaptureRouteWitnesses = diagnostics.CaptureRouteWitnesses, CaptureFailureDetails = diagnostics.CaptureFailureDetails };
            var recovery = new PlacementRun(dungeon, layout, simulation, districts, exclusions, policy.Placement,
                chest, player, random, eligible, recoveryDiagnostics, coverSections: false).Select(wanted);
            recoveryPrimaryCount = recovery.Count;
            if (recoveryDiagnostics != null)
            {
                searchDetails!.Add(new MissionChestSearchPass(3, false, recovery.Count, recoveryDiagnostics));
            }
            if (recovery.Count == 0)
            {
                recoveryDiagnostics = diagnostics == null ? null
                    : new() { CaptureRouteWitnesses = diagnostics.CaptureRouteWitnesses, CaptureFailureDetails = diagnostics.CaptureFailureDetails };
                recovery = new PlacementRun(dungeon, layout, simulation, districts, exclusions, policy.Placement,
                    chest, player, random, eligible, recoveryDiagnostics, coverSections: true).Select(wanted);
                recoveryFallbackCount = recovery.Count;
                if (recoveryDiagnostics != null)
                {
                    searchDetails!.Add(new MissionChestSearchPass(4, true, recovery.Count, recoveryDiagnostics));
                }
            }
            if (recovery.Count > 0)
            {
                result = recovery;
                selectedDiagnostics = recoveryDiagnostics;
            }
        }
        if (diagnostics != null)
        {
            diagnostics.CopyFrom(selectedDiagnostics!);
            diagnostics.TotalCells = dungeon.Rooms.Count; diagnostics.EligibleRooms = eligible.Count; diagnostics.Requested = wanted;
            diagnostics.SearchPasses = 1 + (fallbackCount.HasValue ? 1 : 0)
                + (recoveryPrimaryCount.HasValue ? 1 : 0) + (recoveryFallbackCount.HasValue ? 1 : 0);
            diagnostics.PrimaryPlaced = primary.Count; diagnostics.FallbackPlaced = fallbackCount;
            diagnostics.RecoveryPrimaryPlaced = recoveryPrimaryCount;
            diagnostics.RecoveryFallbackPlaced = recoveryFallbackCount;
            diagnostics.SearchDetails.Clear(); diagnostics.SearchDetails.AddRange(searchDetails!);
        }
        return result;
    }

    public static bool TryGetChestSphere(IGameData gameData, IItemTemplateCatalog templates,
        MissionChestPolicy policy, out CollisionSphereData sphere)
    {
        sphere = null!;
        ChestSpawnDefinition? appearance = policy.SpawnDefinition;
        if (appearance == null)
            return false;
        appearance.Validate();
        uint rawId = appearance.Stats.Single(stat => stat.StatId == (int)CharacterStat.StaticInstance).Value;
        if (rawId == 0 || rawId > int.MaxValue || !templates.TryGet((int)rawId, out ItemTemplate template))
            return false;
        int Stat(CharacterStat stat, int fallback)
        {
            ChestSpawnStat? placed = appearance.Stats.FirstOrDefault(value => value.StatId == (int)stat);
            return placed != null ? unchecked((int)placed.Value)
                : template.Stats.TryGetValue(stat, out int value) ? value : fallback;
        }
        if (!gameData.TryGetDynelCollisionSphere((int)rawId, Stat(CharacterStat.Mesh, 0), Stat(CharacterStat.Scale, 100),
                out float radius, out float centerY)
            || !float.IsFinite(radius) || !float.IsFinite(centerY) || radius <= 0 || centerY <= 0)
            return false;
        sphere = new CollisionSphereData { Radius = radius, CenterY = centerY };
        return true;
    }

    internal static Func<int, Vector3, string?> CreateObjectiveValidator(DungeonWorldLayout dungeon,
        DungeonLayout layout, PlayfieldDistrictsData? districts, PlayfieldWorldSimulation simulation,
        CollisionSphereData? player, MissionChestPlacementPolicy? policy)
    {
        if (simulation.Surface == null || policy?.IsValid != true
            || player is not { Radius: > 0, CenterY: > 0 }
            || !float.IsFinite(player.Radius) || !float.IsFinite(player.CenterY))
            return (_, _) => "collision data or placement policy is unavailable";

        var route = new Lazy<(PlacementRun Run, bool[] Reachable)>(() =>
        {
            // Use the existing walking simulation without placing or reserving a chest.
            var run = new PlacementRun(dungeon, layout, simulation, districts, [], policy, player, player,
                new Random(layout.Seed), [], diagnostics: null, coverSections: true);
            return (run, run.BuildObjectiveRoutes());
        });
        return (room, point) =>
        {
            (PlacementRun run, bool[] reachable) = route.Value;
            return run.ObjectiveFailure(room, point, reachable);
        };
    }

    sealed class PlacementRun
    {
        readonly DungeonWorldLayout _dungeon;
        readonly DungeonLayout _layout;
        readonly PlayfieldWorldSimulation _world;
        readonly PlayfieldDistrictsData? _districts;
        readonly IReadOnlyList<Vector3> _exclusions;
        readonly MissionChestPlacementPolicy _policy;
        readonly CollisionSphereData _chest;
        readonly CollisionSphereData _player;
        readonly Random _random;
        readonly HashSet<int> _eligible;
        readonly MissionChestPlacementDiagnostics? _diagnostics;
        readonly bool _coverSections;
        readonly List<Node> _nodes = [];
        readonly Dictionary<int, List<int>> _rooms = new();
        readonly Dictionary<int, List<(int Node, bool Transition)>> _links = new();
        readonly Dictionary<long, bool> _walks = new();
        readonly List<Vector3> _doorPoints = [];
        readonly List<Vector3> _selected = [];
        readonly List<int> _chosen = [];
        readonly List<int[]> _requiredAnchorGroups = [];
        readonly Dictionary<int, HashSet<int>> _roomLinks = new();
        readonly Dictionary<CollisionTriangleMesh, (Vector3 Min, Vector3 Max)> _meshBounds = new();
        readonly List<(Vector3 Source, int Landing)> _floorTravel = [];
        int _start = -1;
        MissionChestProbeFailure? _lastProbeFailure;
        const int MaximumCandidateFailureDetails = 128;
        const int MaximumGraphCuts = 64;

        internal PlacementRun(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldWorldSimulation world,
            PlayfieldDistrictsData? districts, IReadOnlyList<Vector3> exclusions, MissionChestPlacementPolicy policy,
            CollisionSphereData chest, CollisionSphereData player, Random random, HashSet<int> eligible,
            MissionChestPlacementDiagnostics? diagnostics, bool coverSections)
        {
            _dungeon = dungeon; _layout = layout; _world = world; _districts = districts; _exclusions = exclusions;
            _policy = policy; _chest = chest; _player = player; _random = random;
            _eligible = eligible; _diagnostics = diagnostics;
            _coverSections = coverSections;
            foreach (CollisionTriangleMesh mesh in dungeon.Collision.SurfaceMeshes)
            {
                Vector3 min = new(float.PositiveInfinity), max = new(float.NegativeInfinity);
                foreach (Vector3 vertex in mesh.Vertices)
                {
                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }
                _meshBounds[mesh] = (min, max);
            }
        }

        internal IReadOnlyList<MissionChestPosition> Select(int wanted)
        {
            AddAnchors();
            if (_start < 0)
                return [];
            AddWalkAnchors();
            AddCandidates();
            bool[] initial = Flood();
            if (_diagnostics != null)
            {
                _diagnostics.StartPoint = _nodes[_start].Point;
                _diagnostics.InitialReachableRooms = _nodes.Select((node, index) => (node, index))
                    .Where(pair => initial[pair.index]).Select(pair => pair.node.Room).Distinct().Order().ToArray();
                if (_diagnostics.CaptureFailureDetails)
                {
                    CaptureGraphCuts(initial);
                }
            }
            int[] candidates = Enumerable.Range(0, _nodes.Count)
                .Where(index => _nodes[index].Candidate && initial[_nodes[index].Approach]).ToArray();
            if (_diagnostics != null)
            {
                foreach (var group in _nodes.Select((node, index) => (node, index)).Where(pair => !pair.node.Candidate)
                             .GroupBy(pair => pair.node.Room))
                {
                    _diagnostics.AnchorCounts[group.Key] = group.Count();
                    _diagnostics.ReachableAnchorCounts[group.Key] = group.Count(pair => initial[pair.index]);
                }
                foreach (var group in candidates.GroupBy(index => _nodes[index].Room))
                {
                    _diagnostics.ReachableCandidateCounts[group.Key] = group.Count();
                }
            }
            _random.Shuffle(candidates);
            var rooms = candidates.GroupBy(index => _nodes[index].Room)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(index => _nodes[index].Corner).ToArray());
            foreach (var pair in _dungeon.RoomLinks)
                foreach (int linked in pair.Value)
                    RoomLink(pair.Key, linked);
            foreach (var transition in _layout.FloorTransitions)
                RoomLink(transition.From.RoomIndex, transition.To.RoomIndex);
            var result = new List<MissionChestPosition>();
            var selectedRooms = new List<int>();
            var floorCounts = new Dictionary<int, int>();
            var distances = new List<Dictionary<int, int>>();
            while (rooms.Count > 0 && result.Count < wanted)
            {
                // Random first room / tie breaks, then spread over floors and the existing room-link graph.
                int room = rooms.Keys.OrderBy(room => selectedRooms.Any(other =>
                        _roomLinks.TryGetValue(room, out var links) && links.Contains(other)))
                    .ThenBy(room => floorCounts.GetValueOrDefault(Floor(room)))
                    .ThenByDescending(room => distances.Count == 0 ? 0 : distances.Min(map => map.GetValueOrDefault(room, 0)))
                    .ThenByDescending(room => _selected.Count == 0 ? 0 : _selected.Min(point =>
                        Vector3.DistanceSquared(point, _nodes[rooms[room][0]].Point)))
                    .ThenBy(_ => _random.Next()).First();
                int[] options = rooms[room];
                rooms.Remove(room); // A room is considered once, even when every candidate is rejected.
                foreach (int index in options)
                {
                    Node node = _nodes[index];
                    if (_selected.Any(point => Near(point, node.Point,
                            MathF.Max(_policy.ChestClearance, 2 * _chest.Radius + _policy.CollisionPadding))))
                    {
                        Reject("selected-chest-clearance");
                        continue;
                    }
                    _selected.Add(node.Point);
                    _chosen.Add(index);
                    bool[] reachable = Flood();
                    // Alternative standing points at one doorway are one access requirement.
                    // Preserve a route to each door side, not every peripheral sample around it.
                    bool routesClear = _requiredAnchorGroups.All(group => !group.Any(anchor => initial[anchor])
                        || group.Any(anchor => reachable[anchor]));
                    if (!routesClear || !_chosen.All(chosen => HasApproach(chosen, reachable)))
                    {
                        _selected.RemoveAt(_selected.Count - 1);
                        _chosen.RemoveAt(_chosen.Count - 1);
                        Reject("selected-route-preservation");
                        continue;
                    }
                    int yaw = ((int)MathF.Round(MathF.Atan2(node.Inward.X, node.Inward.Z) * 180 / MathF.PI) + 360) % 360;
                    result.Add(new MissionChestPosition(room, node.Point.X, node.Point.Y, node.Point.Z, yaw));
                    if (_diagnostics != null && _diagnostics.ReferenceHeights.TryGetValue(room, out float reference)
                        && (node.Point.Y > reference + _policy.FloorProbeLift || node.Point.Y < reference - _policy.MaxFloorDrop))
                    {
                        _diagnostics.OffReferencePlacements++;
                    }
                    selectedRooms.Add(room);
                    floorCounts[Floor(room)] = floorCounts.GetValueOrDefault(Floor(room)) + 1;
                    distances.Add(RoomDistances(room));
                    break;
                }
            }
            CaptureRoutes(initial);
            return result;
        }

        void Reject(string reason)
        {
            if (_diagnostics != null)
            {
                _diagnostics.Rejections[reason] = _diagnostics.Rejections.GetValueOrDefault(reason) + 1;
            }
        }

        void CaptureRoutes(bool[] initial)
        {
            if (_diagnostics == null)
                return;
            Dictionary<int, (int From, bool Transition)>? parents = _diagnostics.CaptureRouteWitnesses ? new() : null;
            bool[] reachable = Flood(parents);
            foreach (int chosen in _diagnostics.CaptureRouteWitnesses ? _chosen : Enumerable.Empty<int>())
            {
                Node chest = _nodes[chosen];
                if (!reachable[chest.Approach])
                    continue;
                _diagnostics.Routes[chest.Room] = Path(chest.Approach);
            }
            foreach (int[] group in _requiredAnchorGroups.Where(group => group.Any(anchor => initial[anchor])))
            {
                _diagnostics.InitiallyReachableAnchorGroups++;
                int target = Array.FindIndex(group, anchor => reachable[anchor]);
                if (target >= 0)
                {
                    _diagnostics.PreservedAnchorGroups++;
                    if (_diagnostics.CaptureRouteWitnesses)
                    {
                        _diagnostics.RequiredRoutes.Add(Path(group[target]));
                        _diagnostics.RequiredRouteTargets.Add(group.Select(anchor => _nodes[anchor].Point).ToArray());
                    }
                }
            }
            _diagnostics.LostAnchorSamples = _requiredAnchorGroups.SelectMany(group => group).Distinct()
                .Count(anchor => initial[anchor] && !reachable[anchor]);

            IReadOnlyList<MissionChestRouteStep> Path(int current)
            {
                var path = new List<MissionChestRouteStep>();
                while (current != _start)
                {
                    var parent = parents![current];
                    path.Add(new MissionChestRouteStep(_nodes[current].Point, parent.Transition));
                    current = parent.From;
                }
                path.Add(new MissionChestRouteStep(_nodes[_start].Point, false));
                path.Reverse();
                return path;
            }
        }

        int Floor(int room) => _layout.Generator.Rooms[room].Floor;

        void RoomLink(int first, int second)
        {
            if (!_roomLinks.TryGetValue(first, out var links))
            {
                _roomLinks[first] = links = [];
            }
            links.Add(second);
            if (!_roomLinks.TryGetValue(second, out links))
            {
                _roomLinks[second] = links = [];
            }
            links.Add(first);
        }

        Dictionary<int, int> RoomDistances(int start)
        {
            var distances = new Dictionary<int, int> { [start] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.TryDequeue(out int room))
                if (_roomLinks.TryGetValue(room, out var links))
                {
                    foreach (int next in links)
                        if (distances.TryAdd(next, distances[room] + 1))
                        {
                            queue.Enqueue(next);
                        }
                }
            return distances;
        }

        internal bool[] BuildObjectiveRoutes()
        {
            AddAnchors();
            if (_start < 0)
                return [];
            AddWalkAnchors();
            return Flood();
        }

        internal string? ObjectiveFailure(int room, Vector3 point, bool[] reachable)
        {
            if ((uint)room >= (uint)_dungeon.Rooms.Count)
                return "room geometry is missing";
            if (!Project(point, out Vector3 floor))
                return "position has no collision floor within the placement probe";
            if (!OwnFloor(room, floor) || !_dungeon.Rooms[room].Contains(floor))
                return "collision floor does not belong to the retained room";
            if (!AtSupportedHeight(point.Y, floor.Y))
                return "position height is outside the supported floor step";
            if (!Allowed(point))
                return "position occupies entrance, doorway or floor-transition clearance";
            if (!Occupies(floor, _player))
                return "objective position has insufficient player collision clearance";
            if (reachable.Length == 0)
                return "dungeon entrance has no supported walking anchor";
            if (!_rooms.TryGetValue(room, out var nodes))
                return "room has no walking anchors";

            float separation = _player.Radius + _policy.CollisionPadding;
            Vector3 target = point + Vector3.UnitY * _player.CenterY;
            foreach (int node in nodes)
                if (reachable[node] && IsObjectiveApproach(_nodes[node].Point))
                    return null;

            // Reuse the same floor, sweep and room-link checks for short approaches that fall between graph anchors.
            foreach (Vector3 direction in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ })
            {
                Vector3 sample = floor + direction * (separation + _player.Radius);
                if (!Project(sample, out Vector3 approach) || !IsObjectiveApproach(approach)
                    || !Occupies(approach, _player))
                    continue;
                foreach (int node in nodes)
                    if (reachable[node] && CanWalkPoints(_nodes[node].Point, approach, room))
                        return null;
            }
            return "no physically connected approach within interaction range";

            bool IsObjectiveApproach(Vector3 approach)
            {
                Vector2 offset = new(point.X - approach.X, point.Z - approach.Z);
                return offset.LengthSquared() >= separation * separation
                    && AtSupportedHeight(approach.Y, floor.Y)
                    && Vector3.Distance(point, approach) <= LootableDynel.OpenRange
                    && OwnFloor(room, approach)
                    && _world.HasLineOfSight(ToAo(approach + Vector3.UnitY * _player.CenterY), ToAo(target));
            }
        }

        // Compare rounded world-coordinate bounds so subtraction cannot reject a point exactly at the authored step.
        bool AtSupportedHeight(float height, float support)
            => height >= support - _policy.MaxWalkStep && height <= support + _policy.MaxWalkStep;

        void AddAnchors()
        {
            _start = Add(0, ToNumerics(_layout.Spawn));
            if (_start >= 0)
            {
                _requiredAnchorGroups.Add([_start]);
            }
            var districts = _districts;
            if (districts?.Districts != null)
            {
                var byIndex = districts.Districts.Where(value => value != null).ToDictionary(value => value.DistrictIndex);
                foreach (DungeonRoomPlacement placed in _dungeon.Placements)
                {
                    int zone = placed.Template.InstanceId;
                    int mapped = districts.ZoneToDistrictMap is { } map && zone >= 0 && zone < map.Length ? map[zone] : zone;
                    if (!byIndex.TryGetValue(mapped, out PlayfieldDistrictEntry? district))
                        continue;
                    foreach (PlayfieldDistrictSpawnPoint point in district.SpawnPoints ?? [])
                        if (point?.Position is { Length: >= 3 } raw)
                            Add(placed.Index, placed.TransformTemplatePoint(new Vector3(raw[0], raw[1], raw[2])));
                }
            }
            foreach (DungeonDoorPlacement door in _dungeon.Doors)
            {
                _doorPoints.Add(door.Position);
                if (door.IsExit)
                    continue;
                float radians = door.YawDegrees * MathF.PI / 180;
                var offset = new Vector3(MathF.Sin(radians), 0, MathF.Cos(radians))
                    * MathF.Max(1, _player.Radius + _policy.CollisionPadding * 2);
                int[] first = DoorApproaches(door.Room, door.Position, offset);
                int[] second = DoorApproaches(door.LinkedRoom, door.Position, offset);
                if (_diagnostics?.CaptureFailureDetails == true)
                {
                    _diagnostics.DoorAnchors.Add(new MissionChestDoorAnchors(door.Room, door.LinkedRoom, door.Position,
                        first.Select(index => _nodes[index].Point).ToArray(), second.Select(index => _nodes[index].Point).ToArray()));
                }
                foreach (int a in first)
                    foreach (int b in second)
                        Link(a, b, transition: false);
                foreach (int node in first.Concat(second))
                {
                    _doorPoints.Add(new Vector3(door.Position.X, _nodes[node].Point.Y, door.Position.Z));
                }
                if (first.Length > 0)
                {
                    _requiredAnchorGroups.Add(first);
                }
                if (second.Length > 0)
                {
                    _requiredAnchorGroups.Add(second);
                }
            }
            foreach (var transition in _layout.FloorTransitions)
            {
                _doorPoints.Add(transition.From.Position);
                _doorPoints.Add(transition.To.Position);
                int from = Landing(transition.From.RoomIndex, transition.From.Position);
                int to = Landing(transition.To.RoomIndex, transition.To.Position);
                // Runtime platforms are used from interaction range and land at the opposite endpoint.
                // Never substitute a nearby destination approach for that supported landing.
                if (to >= 0)
                {
                    _floorTravel.Add((transition.From.Position, to));
                }
                if (from >= 0)
                {
                    _floorTravel.Add((transition.To.Position, from));
                }
                if (from >= 0)
                {
                    _requiredAnchorGroups.Add([from]);
                }
                if (to >= 0)
                {
                    _requiredAnchorGroups.Add([to]);
                }
            }
        }

        int Landing(int room, Vector3 target)
        {
            bool projected = Project(target, out Vector3 supported);
            if (_diagnostics != null)
            {
                _diagnostics.LandingHeightOffsets[room] = projected ? supported.Y - target.Y : null;
            }
            return projected && MathF.Abs(supported.Y - target.Y) <= _policy.MaxWalkStep ? Add(room, supported) : -1;
        }

        void AddWalkAnchors()
        {
            // GNDA ground can lie below an authored catwalk. Cover actual upward room surfaces
            // as well as structural ground features and random interiors, without changing validation.
            foreach (var group in _dungeon.Collision.SurfaceMeshes.Where(mesh =>
                         mesh.Source?.StartsWith("ground-room", StringComparison.Ordinal) == true && mesh.CellId.HasValue)
                         .GroupBy(mesh => mesh.CellId!.Value))
            {
                var triangles = group.SelectMany(mesh => mesh.Triangles.Select(triangle =>
                    (A: mesh.Vertices[triangle.A], B: mesh.Vertices[triangle.B], C: mesh.Vertices[triangle.C]))).ToArray();
                if (triangles.Length == 0)
                    continue;
                float groundMinimum = triangles.Min(triangle => MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y)));
                float groundMaximum = triangles.Max(triangle => MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y)));
                var centres = triangles.Select(triangle => ((triangle.A + triangle.B + triangle.C) / 3,
                    Raw: false, Raised: false)).ToList();
                foreach (CollisionTriangleMesh mesh in _dungeon.Collision.SurfaceMeshes.Where(mesh => mesh.CellId == group.Key
                             && mesh.Source?.StartsWith("ground-room", StringComparison.Ordinal) != true))
                    foreach (CollisionTriangle triangle in mesh.Triangles)
                    {
                        Vector3 a = mesh.Vertices[triangle.A], b = mesh.Vertices[triangle.B], c = mesh.Vertices[triangle.C];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        Vector3 centre = (a + b + c) / 3;
                        if (normal.LengthSquared() > 1e-8f && Vector3.Normalize(normal).Y > 0.3f
                            && centre.Y >= groundMinimum - _policy.MaxWalkStep && centre.Y <= groundMaximum + _policy.MaxWalkStep)
                        {
                            bool raised = TryOwnFloor(group.Key, centre, out Vector3 ground, checkHeight: false)
                                && centre.Y - ground.Y > _policy.MaxWalkStep;
                            centres.Add((centre, true, raised));
                        }
                    }
                var attemptedCentres = new bool[centres.Count];
                float[] uncovered = centres.Select(centre => _rooms.TryGetValue(group.Key, out var nodes) && nodes.Count > 0
                    ? nodes.Min(index => Vector3.DistanceSquared(centre.Item1, _nodes[index].Point)) : float.PositiveInfinity).ToArray();
                int added = 0;
                for (int attempt = 0; attempt < _policy.CandidateAttemptsPerRoom && added < _policy.CandidatesPerRoom * 5; attempt++)
                {
                    bool structural = attempt % 2 == 0;
                    int centreIndex = -1;
                    if (structural)
                    {
                        bool raisedAvailable = centres.Where((centre, index) => centre.Raised && !attemptedCentres[index]).Any();
                        bool preferRaw = (attempt / 2) % 2 == 1;
                        int start = _random.Next(centres.Count);
                        for (int preference = 0; preference < 2 && centreIndex < 0; preference++)
                            for (int offset = 0; offset < centres.Count; offset++)
                            {
                                int index = (start + offset) % centres.Count;
                                bool preferred = raisedAvailable ? centres[index].Raised
                                    : centres[index].Raw == (preference == 0 ? preferRaw : !preferRaw);
                                if (!attemptedCentres[index] && preferred
                                    && (centreIndex < 0 || uncovered[index] > uncovered[centreIndex]))
                                {
                                    centreIndex = index;
                                }
                            }
                        structural = centreIndex >= 0;
                        if (structural)
                        {
                            attemptedCentres[centreIndex] = true;
                        }
                    }
                    Vector3 point;
                    if (structural)
                    {
                        point = centres[centreIndex].Item1;
                    }
                    else
                    {
                        var triangle = triangles[_random.Next(triangles.Length)];
                        float u = (float)_random.NextDouble(), v = (float)_random.NextDouble();
                        if (u + v > 1) { u = 1 - u; v = 1 - v; }
                        point = triangle.A + u * (triangle.B - triangle.A) + v * (triangle.C - triangle.A);
                    }
                    int node = Add(group.Key, point);
                    if (node < 0)
                        continue;
                    added++;
                    for (int index = 0; index < centres.Count; index++)
                    {
                        uncovered[index] = MathF.Min(uncovered[index], Vector3.DistanceSquared(centres[index].Item1, _nodes[node].Point));
                    }
                    if (structural && _diagnostics != null)
                    {
                        _diagnostics.StructuralAnchorCounts[group.Key] = _diagnostics.StructuralAnchorCounts.GetValueOrDefault(group.Key) + 1;
                        if (centres[centreIndex].Raw)
                        {
                            _diagnostics.RawStructuralAnchorCounts[group.Key] = _diagnostics.RawStructuralAnchorCounts.GetValueOrDefault(group.Key) + 1;
                        }
                        if (_diagnostics.CaptureRouteWitnesses)
                        {
                            if (!_diagnostics.StructuralAnchorPoints.TryGetValue(group.Key, out var points))
                            {
                                _diagnostics.StructuralAnchorPoints.Add(group.Key, points = []);
                            }
                            points.Add(_nodes[node].Point);
                            if (centres[centreIndex].Raw)
                            {
                                if (!_diagnostics.RawStructuralAnchorPoints.TryGetValue(group.Key, out var rawPoints))
                                {
                                    _diagnostics.RawStructuralAnchorPoints.Add(group.Key, rawPoints = []);
                                }
                                rawPoints.Add(_nodes[node].Point);
                            }
                        }
                    }
                }
            }
        }

        int[] DoorApproaches(int room, Vector3 position, Vector3 offset)
        {
            if (room < 0 || room >= _dungeon.Rooms.Count)
                return [];
            var result = new List<int>();
            Vector3 side = Vector3.Normalize(new Vector3(offset.Z, 0, -offset.X)) * (2 * _player.Radius);
            foreach (float depth in new[] { 0.5f, 1f, 2f })
                foreach (float sign in new[] { -1f, 1f })
                    foreach (float lateral in new[] { 0f, -1f, 1f })
                    {
                        Vector3 source = position + offset * depth * sign + side * lateral;
                        if (!TryOwnFloor(room, source, out Vector3 ground, checkHeight: false) || !Project(ground, out Vector3 supported))
                            continue;
                        int node = Add(room, supported);
                        if (node >= 0)
                        {
                            result.Add(node);
                        }
                    }
            return result.ToArray();
        }

        void AddCandidates()
        {
            foreach (DungeonRoomPlacement placed in _dungeon.Placements)
            {
                int room = placed.Index;
                if (!_eligible.Contains(room))
                    continue;
                float referenceY = placed.Placed.Y;
                if (_rooms.TryGetValue(room, out List<int>? anchors) && anchors.Count > 0)
                {
                    float[] heights = anchors.Select(index => _nodes[index].Point.Y).Order().ToArray();
                    referenceY = heights[heights.Length / 2];
                }
                if (_diagnostics != null)
                {
                    _diagnostics.ReferenceHeights[room] = referenceY;
                }
                // Length-weighted random ordering keeps long sections likely, but does not spend
                // a bounded search repeatedly drawing the same unsuitable section with replacement.
                float[] groundHeights = _dungeon.Collision.SurfaceMeshes.Where(mesh => mesh.CellId == room
                        && mesh.Source?.StartsWith("ground-room", StringComparison.Ordinal) == true)
                    .SelectMany(mesh => mesh.Vertices).Select(point => point.Y).ToArray();
                var sliceHeights = new List<float> { referenceY };
                if (groundHeights.Length > 0)
                {
                    foreach (float height in new[] { groundHeights.Min(), groundHeights.Max() })
                        if (!sliceHeights.Any(existing => MathF.Abs(existing - height) <= 0.01f))
                        {
                            sliceHeights.Add(height);
                        }
                }
                if (_diagnostics != null)
                {
                    _diagnostics.WallSliceHeights[room] = sliceHeights.ToArray();
                }
                Wall[] walls = sliceHeights.SelectMany(height => Walls(room, height + _chest.CenterY))
                    .DistinctBy(wall => (wall.TriangleA, wall.TriangleB, wall.TriangleC)).ToArray();
                if (_coverSections)
                {
                    walls = walls.OrderBy(wall => -Math.Log(1 - _random.NextDouble()) / Vector3.Distance(wall.A, wall.B)).ToArray();
                }
                else
                {
                    _random.Shuffle(walls);
                }
                if (_diagnostics != null)
                {
                    _diagnostics.WallSectionCounts[room] = walls.Length;
                }
                if (walls.Length == 0)
                {
                    _lastProbeFailure = null;
                    Vector3 reference = new(placed.Placed.X, referenceY, placed.Placed.Z);
                    ProbeFailure("no-wall-at-reference-height", reference, reference + Vector3.UnitY * _chest.CenterY);
                    CandidateReject(room, "no-wall-at-reference-height", reference);
                    continue;
                }
                float wallLength = walls.Sum(wall => Vector3.Distance(wall.A, wall.B));
                var tried = _diagnostics == null ? null : new HashSet<Wall>();
                int added = 0;
                for (int attempt = 0; attempt < _policy.CandidateAttemptsPerRoom && added < _policy.CandidatesPerRoom; attempt++)
                {
                    Wall wall = _coverSections ? walls[attempt % walls.Length] : walls[^1];
                    if (!_coverSections)
                    {
                        float roll = (float)_random.NextDouble() * wallLength;
                        foreach (Wall section in walls)
                        {
                            roll -= Vector3.Distance(section.A, section.B);
                            if (roll > 0)
                                continue;
                            wall = section;
                            break;
                        }
                    }
                    if (_diagnostics != null)
                    {
                        tried!.Add(wall);
                        _diagnostics.WallSectionsTried[room] = tried.Count;
                    }
                    float fraction = (float)_random.NextDouble();
                    // Bias towards section ends (corners), with collision/side clearance deciding suitability.
                    fraction = attempt % 3 == 0 ? fraction : (attempt % 2 == 0 ? fraction * fraction : 1 - fraction * fraction);
                    foreach (Vector3 inward in new[] { wall.Inward, -wall.Inward })
                    {
                        float offset = _chest.Radius + _policy.CollisionPadding * (1 + (float)_random.NextDouble());
                        _lastProbeFailure = null;
                        if (!TryLocalWallPoint(room, wall, fraction, inward, offset, out Vector3 point, out string failedStage))
                        {
                            CandidateReject(room, failedStage, point);
                            continue;
                        }
                        if (_diagnostics != null && (point.Y > referenceY + _policy.FloorProbeLift
                            || point.Y < referenceY - _policy.MaxFloorDrop))
                        {
                            _diagnostics.LocalHeightSeeds++;
                        }
                        _lastProbeFailure = null;
                        if (!Project(point, out point) || !OwnFloor(room, point))
                        {
                            CandidateReject(room, "wall-floor-support", point);
                            continue;
                        }
                        _lastProbeFailure = null;
                        if (!Allowed(point))
                        {
                            CandidateReject(room, "entry-door-objective-clearance", point);
                            continue;
                        }
                        _lastProbeFailure = null;
                        if (!Occupies(point, _chest, exact: true))
                        {
                            CandidateReject(room, "chest-collision-support", point);
                            continue;
                        }
                        _lastProbeFailure = null;
                        if (!WallClear(room, point) || !BoundaryWall(room, point, inward))
                        {
                            CandidateReject(room, "boundary-wall", point);
                            continue;
                        }
                        float approachDistance = _chest.Radius + _player.Radius + 2 * _policy.CollisionPadding;
                        float heading = MathF.Round(MathF.Atan2(inward.X, inward.Z) * 180 / MathF.PI) * MathF.PI / 180;
                        Vector3 facing = new(MathF.Sin(heading), 0, MathF.Cos(heading));
                        Vector3 approach = point + facing * approachDistance;
                        _lastProbeFailure = null;
                        if (!Project(approach, out approach) || MathF.Abs(approach.Y - point.Y) > _policy.MaxWalkStep)
                        {
                            if (_lastProbeFailure == null)
                            {
                                ProbeFailure("approach-step-height", point, approach, approach,
                                    distance: MathF.Abs(approach.Y - point.Y), limit: _policy.MaxWalkStep);
                            }
                            CandidateReject(room, "approach-floor", point);
                            continue;
                        }
                        Vector3 tangent = new(facing.Z, 0, -facing.X);
                        float width = MathF.Max(_policy.DoorClearance, _chest.Radius + _player.Radius + _policy.CollisionPadding);
                        // Require an open room-side pocket, rather than a narrow strip beside a passage wall.
                        _lastProbeFailure = null;
                        if (!Pocket(approach, point.Y) || !Pocket(approach + facing * width, point.Y)
                            || !Pocket(approach + tangent * width, point.Y) || !Pocket(approach - tangent * width, point.Y))
                        {
                            CandidateReject(room, "room-side-pocket", point);
                            continue;
                        }
                        _lastProbeFailure = null;
                        int front = Add(room, approach);
                        if (front < 0)
                        {
                            CandidateReject(room, "approach-occupancy", point);
                            continue;
                        }
                        bool corner = walls.Any(other => MathF.Abs(Vector3.Dot(other.Inward, inward)) < 0.7f
                            && DistanceToSegment(point, other.A, other.B) <= 3 * offset
                            && BoundaryWall(room, point, Vector3.Dot(point - other.A, other.Inward) >= 0
                                ? other.Inward : -other.Inward, 3 * offset));
                        if (Add(room, point, candidate: true, facing, front, corner) < 0)
                            continue;
                        added++;
                        CandidateStage(room, "accepted");
                        break;

                        bool Pocket(Vector3 probe, float height)
                        {
                            if (!Project(probe, out Vector3 supported))
                                return false;
                            if (MathF.Abs(supported.Y - height) > _policy.MaxWalkStep)
                            {
                                ProbeFailure("pocket-step-height", approach, probe, supported,
                                    distance: MathF.Abs(supported.Y - height), limit: _policy.MaxWalkStep);
                                return false;
                            }
                            return OwnFloor(room, supported) && Occupies(supported, _player)
                                && CanWalkPoints(approach, supported, room);
                        }
                    }
                }
                if (_diagnostics != null)
                {
                    _diagnostics.CandidateCounts[room] = added;
                }
            }
        }

        void CandidateStage(int room, string stage)
        {
            if (_diagnostics?.CaptureFailureDetails != true)
                return;
            if (!_diagnostics.CandidateStages.TryGetValue(room, out var stages))
            {
                _diagnostics.CandidateStages.Add(room, stages = new());
            }
            stages[stage] = stages.GetValueOrDefault(stage) + 1;
        }

        void CandidateReject(int room, string stage, Vector3 point)
        {
            Reject(stage);
            CandidateStage(room, stage);
            if (_diagnostics?.CaptureFailureDetails == true
                && _diagnostics.CandidateFailures.Count < MaximumCandidateFailureDetails
                && !_diagnostics.CandidateFailures.Any(value => value.Room == room && value.Stage == stage))
            {
                _diagnostics.CandidateFailures.Add(new MissionChestCandidateFailure(room, stage, point, _lastProbeFailure));
            }
        }

        void ProbeFailure(string constraint, Vector3 from, Vector3 to, Vector3? hit = null, Vector3? normal = null,
            string? meshSource = null, int? meshCell = null, float? distance = null, float? limit = null)
        {
            if (_diagnostics?.CaptureFailureDetails == true)
            {
                _lastProbeFailure = new MissionChestProbeFailure(constraint, from, to, hit, normal, meshSource, meshCell, distance, limit);
            }
        }

        bool OwnFloor(int room, Vector3 point, bool checkHeight = true)
            => TryOwnFloor(room, point, out _, checkHeight);

        bool TryOwnFloor(int room, Vector3 point, out Vector3 floor, bool checkHeight = true)
        {
            floor = default;
            Vector3? matchingXZ = null;
            // The generated ground triangles follow DCGA's occupied tiles, including nonrectangular rooms.
            foreach (CollisionTriangleMesh mesh in _dungeon.Collision.SurfaceMeshes.Where(mesh =>
                         mesh.CellId == room && mesh.Source?.StartsWith("ground-room", StringComparison.Ordinal) == true))
                foreach (CollisionTriangle triangle in mesh.Triangles)
                {
                    Vector3 a = mesh.Vertices[triangle.A], b = mesh.Vertices[triangle.B], c = mesh.Vertices[triangle.C];
                    Vector2 ab = new(b.X - a.X, b.Z - a.Z), ac = new(c.X - a.X, c.Z - a.Z);
                    Vector2 delta = new(point.X - a.X, point.Z - a.Z);
                    float determinant = ab.X * ac.Y - ab.Y * ac.X;
                    if (MathF.Abs(determinant) < 1e-8f)
                        continue;
                    float u = (delta.X * ac.Y - delta.Y * ac.X) / determinant;
                    float v = (ab.X * delta.Y - ab.Y * delta.X) / determinant;
                    if (u >= -0.001f && v >= -0.001f && u + v <= 1.001f)
                    {
                        matchingXZ = new Vector3(point.X, a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y), point.Z);
                    }
                    if (u >= -0.001f && v >= -0.001f && u + v <= 1.001f
                        && (!checkHeight || MathF.Abs(point.Y - (a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y))) <= 0.01f))
                    {
                        floor = new Vector3(point.X, a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y), point.Z);
                        return true;
                    }
                }
            ProbeFailure(matchingXZ.HasValue ? "own-floor-height" : "own-floor-xz", point, point, matchingXZ,
                meshCell: room, distance: matchingXZ.HasValue ? MathF.Abs(point.Y - matchingXZ.Value.Y) : null, limit: 0.01f);
            return false;
        }

        IEnumerable<Wall> Walls(int room, float height)
        {
            foreach (CollisionTriangleMesh mesh in _dungeon.Collision.SurfaceMeshes.Where(mesh => mesh.CellId == room))
                foreach (CollisionTriangle triangle in mesh.Triangles)
                    if (TrySliceWall(mesh.Vertices[triangle.A], mesh.Vertices[triangle.B], mesh.Vertices[triangle.C], height, out Wall wall))
                    {
                        yield return wall;
                    }
        }

        static bool TrySliceWall(Vector3 a, Vector3 b, Vector3 c, float height, out Wall wall)
        {
            wall = null!;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-8f || MathF.Abs(Vector3.Normalize(normal).Y) > 0.3f)
                return false;
            var cuts = new List<Vector3>(3);
            Cut(a, b); Cut(b, c); Cut(c, a);
            if (cuts.Count < 2 || Vector3.DistanceSquared(cuts[0], cuts[1]) < 0.04f)
                return false;
            Vector3 inward = Vector3.Normalize(new Vector3(normal.X, 0, normal.Z));
            wall = new Wall(cuts[0], cuts[1], inward, a, b, c);
            return true;

            void Cut(Vector3 first, Vector3 second)
            {
                if ((first.Y < height && second.Y < height) || (first.Y > height && second.Y > height)
                    || MathF.Abs(second.Y - first.Y) < 1e-6f)
                    return;
                Vector3 cut = Vector3.Lerp(first, second, (height - first.Y) / (second.Y - first.Y));
                if (!cuts.Any(point => Vector3.DistanceSquared(point, cut) < 1e-8f))
                {
                    cuts.Add(cut);
                }
            }
        }

        bool TryLocalWallPoint(int room, Wall wall, float fraction, Vector3 inward, float offset,
            out Vector3 point, out string failedStage)
        {
            Wall local = wall;
            point = default;
            failedStage = "wall-floor-support";
            // A section cut at a room median can move horizontally when the real floor has another height.
            // Re-slice the same physical triangle, keeping the random fraction and clearance offset.
            for (int refinement = 0; refinement < 2; refinement++)
            {
                Vector3 contact = Vector3.Lerp(local.A, local.B, fraction);
                point = new Vector3(contact.X, contact.Y - _chest.CenterY, contact.Z) + inward * offset;
                if (!TryOwnFloor(room, point, out Vector3 ground, checkHeight: false))
                    return false;
                if (!TrySliceWall(wall.TriangleA, wall.TriangleB, wall.TriangleC, ground.Y + _chest.CenterY, out local))
                {
                    failedStage = "local-wall-slice";
                    ProbeFailure("wall-no-local-slice", point, ground + Vector3.UnitY * _chest.CenterY);
                    return false;
                }
            }
            Vector3 finalContact = Vector3.Lerp(local.A, local.B, fraction);
            point = new Vector3(finalContact.X, finalContact.Y - _chest.CenterY, finalContact.Z) + inward * offset;
            if (!TryOwnFloor(room, point, out Vector3 localGround, checkHeight: false))
                return false;
            point = localGround;
            return true;
        }

        bool WallClear(int room, Vector3 point)
        {
            Wall[] walls = Walls(room, point.Y + _chest.CenterY).ToArray();
            float distance = walls.Length == 0 ? float.PositiveInfinity : walls.Min(wall => DistanceToSegment(point, wall.A, wall.B));
            bool clear = distance >= _chest.Radius + _policy.CollisionPadding - 0.001f
                && distance <= _chest.Radius + 2 * _policy.CollisionPadding + 0.001f;
            if (!clear)
            {
                ProbeFailure("wall-clearance", point, point, distance: distance,
                    limit: _chest.Radius + 2 * _policy.CollisionPadding + 0.001f);
            }
            return clear;
        }

        bool BoundaryWall(int room, Vector3 point, Vector3 inward, float? maximumDistance = null)
        {
            // A prop's vertical side is not a room wall: require a standing-height continuation and
            // a boundary of this room's occupied floor on the back side. Interior floor remains ineligible.
            float height = point.Y + MathF.Max(2 * _player.CenterY, _chest.CenterY + _chest.Radius);
            if (!Walls(room, height).Any(wall => MathF.Abs(Vector3.Dot(wall.Inward, inward)) > 0.9f
                    && DistanceToSegment(point, wall.A, wall.B) <= (maximumDistance ?? _chest.Radius + 2 * _policy.CollisionPadding + 0.03f)))
            {
                ProbeFailure("standing-wall-continuation", point, point + Vector3.UnitY * (height - point.Y));
                return false;
            }
            float stride = _policy.WalkSampleSpacing;
            float wallDistance = Walls(room, point.Y + _chest.CenterY)
                .Where(wall => MathF.Abs(Vector3.Dot(wall.Inward, inward)) > 0.9f)
                .Select(wall => DistanceToSegment(point, wall.A, wall.B)).DefaultIfEmpty(float.PositiveInfinity).Min();
            if (!float.IsFinite(wallDistance))
            {
                ProbeFailure("boundary-wall-distance", point, point);
                return false;
            }
            DungeonRoomBounds bounds = _dungeon.Rooms[room];
            float span = new Vector2(bounds.Max.X - bounds.Min.X, bounds.Max.Z - bounds.Min.Z).Length();
            if (!TryGetWalkSampleCount(span, stride, out int steps))
                return false;
            // Continue to the actual occupied-floor perimeter. A single blocked point behind a cabinet
            // is insufficient: any usable room floor before the perimeter identifies an interior prop.
            for (int step = 0; step <= steps; step++)
            {
                Vector3 back = point - inward * (wallDistance + _policy.CollisionPadding + stride * step);
                if (!TryOwnFloor(room, back, out Vector3 supported, checkHeight: false))
                    return true;
                if (Occupies(supported, _player))
                {
                    ProbeFailure("boundary-interior-walkable-floor", point, back, supported);
                    return false;
                }
            }
            return false;
        }

        static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector2 start = new(a.X, a.Z), segment = new(b.X - a.X, b.Z - a.Z), delta = new(point.X - a.X, point.Z - a.Z);
            float t = segment.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(delta, segment) / segment.LengthSquared(), 0, 1) : 0;
            return (delta - segment * t).Length();
        }

        bool Allowed(Vector3 point)
            => !Near(point, ToNumerics(_layout.Spawn), _policy.EntryClearance)
                && !_exclusions.Any(other => Near(point, other, _policy.ObjectiveClearance))
                && !_doorPoints.Any(other => Near(point, other, _policy.DoorClearance));

        int Add(int room, Vector3 point, bool candidate = false, Vector3 inward = default, int approach = -1, bool corner = false)
        {
            if (room < 0 || room >= _dungeon.Rooms.Count || !Project(point, out point)
                || !_dungeon.Rooms[room].Contains(point) || (!candidate && !Occupies(point, _player)))
                return -1;
            int id = _nodes.Count;
            _nodes.Add(new Node(room, point, candidate, inward, approach, corner));
            if (!_rooms.TryGetValue(room, out List<int>? nodes))
            {
                nodes = [];
                _rooms.Add(room, nodes);
            }
            nodes.Add(id);
            return id;
        }

        void Link(int first, int second, bool transition)
        {
            if (first < 0 || second < 0)
                return;
            if (!_links.TryGetValue(first, out List<(int, bool)>? from))
            {
                from = [];
                _links.Add(first, from);
            }
            if (!_links.TryGetValue(second, out List<(int, bool)>? to))
            {
                to = [];
                _links.Add(second, to);
            }
            from.Add((second, transition));
            to.Add((first, transition));
        }

        bool[] Flood(Dictionary<int, (int From, bool Transition)>? parents = null)
        {
            var visited = new bool[_nodes.Count];
            if (Blocked(_nodes[_start].Point))
                return visited;
            var queue = new Queue<int>();
            visited[_start] = true;
            queue.Enqueue(_start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in _rooms[_nodes[current].Room])
                    Visit(next, transition: false);
                if (_links.TryGetValue(current, out var links))
                    foreach ((int next, bool transition) in links)
                        Visit(next, transition);
                foreach (var travel in _floorTravel)
                    if (Vector3.Distance(_nodes[current].Point, travel.Source) <= LootableDynel.OpenRange
                        && _world.HasLineOfSight(ToAo(_nodes[current].Point + Vector3.UnitY * _player.CenterY),
                            ToAo(travel.Source + Vector3.UnitY * _player.CenterY)))
                    {
                        Visit(travel.Landing, transition: true);
                    }

                void Visit(int next, bool transition)
                {
                    if (visited[next] || _nodes[next].Candidate || Blocked(_nodes[next].Point))
                        return;
                    Vector3 a = _nodes[current].Point, b = _nodes[next].Point;
                    if (!transition && (BlockedSegment(a, b) || !CanWalk(current, next)))
                        return;
                    visited[next] = true;
                    if (parents != null)
                    {
                        parents[next] = (current, transition);
                    }
                    queue.Enqueue(next);
                }
            }
            return visited;
        }

        void CaptureGraphCuts(bool[] reachable)
        {
            var pairs = new HashSet<(int From, int To)>();
            foreach (var link in _links)
                foreach (var edge in link.Value)
                    if (reachable[link.Key] && !reachable[edge.Node] && !edge.Transition)
                    {
                        pairs.Add((link.Key, edge.Node));
                    }
            foreach (var room in _rooms.Values)
            {
                var frontier = from first in room where reachable[first] && !_nodes[first].Candidate
                               from second in room where !reachable[second] && !_nodes[second].Candidate
                               orderby Vector3.DistanceSquared(_nodes[first].Point, _nodes[second].Point)
                               select (From: first, To: second);
                foreach (var pair in frontier.Take(8))
                {
                    pairs.Add(pair);
                }
            }
            foreach (var pair in pairs.OrderBy(pair => _nodes[pair.From].Room == _nodes[pair.To].Room)
                         .ThenBy(pair => Vector3.DistanceSquared(_nodes[pair.From].Point, _nodes[pair.To].Point)).Take(MaximumGraphCuts))
            {
                Node from = _nodes[pair.From], to = _nodes[pair.To];
                long key = ((long)Math.Min(pair.From, pair.To) << 32) | (uint)Math.Max(pair.From, pair.To);
                bool? cached = _walks.TryGetValue(key, out bool value) ? value : null;
                _lastProbeFailure = null;
                bool forward = CanWalkPoints(from.Point, to.Point);
                MissionChestProbeFailure? forwardFailure = _lastProbeFailure;
                _lastProbeFailure = null;
                bool reverse = CanWalkPoints(to.Point, from.Point);
                bool linked = _links.TryGetValue(pair.From, out var links) && links.Any(edge => edge.Node == pair.To);
                _diagnostics!.GraphCuts.Add(new MissionChestGraphCut(from.Room, to.Room, from.Point, to.Point,
                    linked, cached, forward, reverse, forwardFailure, _lastProbeFailure));
            }
        }

        bool CanWalk(int first, int second)
        {
            long key = ((long)Math.Min(first, second) << 32) | (uint)Math.Max(first, second);
            if (_walks.TryGetValue(key, out bool result))
                return result;
            Vector3 from = _nodes[first].Point, to = _nodes[second].Point;
            result = CanWalkPoints(from, to);
            _walks.Add(key, result);
            return result;
        }

        bool CanWalkPoints(Vector3 from, Vector3 to, int? requiredRoom = null)
        {
            if (!TryGetWalkSampleCount(Vector3.Distance(from, to), _policy.WalkSampleSpacing, out int steps))
                return false;
            Vector3 previous = from;
            for (int step = 1; step <= steps; step++)
            {
                Vector3 expected = Vector3.Lerp(from, to, step / (float)steps);
                // Follow supported terrain step by step; a curved floor need not follow the endpoint chord's Y.
                expected.Y = previous.Y;
                if (!Project(expected, out Vector3 point))
                    return false;
                if (MathF.Abs(point.Y - previous.Y) > _policy.MaxWalkStep)
                {
                    ProbeFailure("walk-step-height", previous, expected, point,
                        distance: MathF.Abs(point.Y - previous.Y), limit: _policy.MaxWalkStep);
                    return false;
                }
                if ((requiredRoom.HasValue && !OwnFloor(requiredRoom.Value, point)) || !ClearWalkSegment(previous, point))
                    return false;
                previous = point;
            }
            bool arrived = MathF.Abs(previous.Y - to.Y) <= _policy.MaxWalkStep;
            if (!arrived)
            {
                ProbeFailure("walk-target-height", previous, to, previous,
                    distance: MathF.Abs(previous.Y - to.Y), limit: _policy.MaxWalkStep);
            }
            return arrived;
        }


        bool HasApproach(int candidate, bool[] reachable)
        {
            Node node = _nodes[candidate];
            Vector3 chest = node.Point;
            Vector3 centre = chest + Vector3.UnitY * _chest.CenterY;
            Vector3 point = _nodes[node.Approach].Point;
            return reachable[node.Approach] && Vector3.Distance(point, chest) <= LootableDynel.OpenRange
                && _world.HasLineOfSight(ToAo(point + Vector3.UnitY * _player.CenterY), ToAo(centre));
        }

        bool Project(Vector3 source, out Vector3 floor)
        {
            floor = default;
            if (!float.IsFinite(source.X) || !float.IsFinite(source.Y) || !float.IsFinite(source.Z)
                || !_world.TryRaycastDown(ToAo(source + Vector3.UnitY * _policy.FloorProbeLift),
                    _policy.FloorProbeLift + _policy.MaxFloorDrop, out AoVector3 hit))
            {
                ProbeFailure("floor-ray", source, source + Vector3.UnitY * _policy.FloorProbeLift,
                    limit: _policy.FloorProbeLift + _policy.MaxFloorDrop);
                return false;
            }
            floor = ToNumerics(hit);
            return true;
        }

        bool Occupies(Vector3 feet, CollisionSphereData sphere, bool exact = false)
        {
            float radius = sphere.Radius + _policy.CollisionPadding;
            Vector3 centre = feet + Vector3.UnitY * (sphere.CenterY + 2 * _policy.CollisionPadding);
            // Probe the occupied sphere itself; sweeping its centre by another radius would double wall clearance.
            foreach (Vector3 axis in new[] { Vector3.UnitX, Vector3.UnitZ })
                if (!CastClear(centre - axis * _policy.CollisionPadding,
                        centre + axis * MathF.Max(0.001f, _policy.CollisionPadding), radius))
                    return false;
            foreach (Vector3 offset in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ })
            {
                if (!Project(feet + offset * sphere.Radius, out Vector3 support))
                    return false;
                if (MathF.Abs(support.Y - feet.Y) > _policy.MaxWalkStep)
                {
                    ProbeFailure("sphere-support-step", feet, feet + offset * sphere.Radius, support,
                        distance: MathF.Abs(support.Y - feet.Y), limit: _policy.MaxWalkStep);
                    return false;
                }
            }
            return (!exact || StaticSphereClear(feet + Vector3.UnitY * sphere.CenterY, sphere.Radius))
                && StaticSphereClear(centre, radius);
        }

        bool StaticSphereClear(Vector3 centre, float radius)
        {
            foreach (CollisionTriangleMesh mesh in _dungeon.Collision.SurfaceMeshes)
            {
                var bounds = _meshBounds[mesh];
                if (centre.X < bounds.Min.X - radius || centre.X > bounds.Max.X + radius
                    || centre.Y < bounds.Min.Y - radius || centre.Y > bounds.Max.Y + radius
                    || centre.Z < bounds.Min.Z - radius || centre.Z > bounds.Max.Z + radius)
                    continue;
                foreach (CollisionTriangle triangle in mesh.Triangles)
                {
                    Vector3 a = mesh.Vertices[triangle.A], b = mesh.Vertices[triangle.B], c = mesh.Vertices[triangle.C];
                    Vector3 min = Vector3.Min(a, Vector3.Min(b, c)) - new Vector3(radius);
                    Vector3 max = Vector3.Max(a, Vector3.Max(b, c)) + new Vector3(radius);
                    if (centre.X < min.X || centre.X > max.X || centre.Y < min.Y || centre.Y > max.Y
                        || centre.Z < min.Z || centre.Z > max.Z)
                        continue;
                    float toleranceRadius = MathF.Max(0, radius - 0.001f); // Stable tangency at upper-floor float coordinates.
                    Vector3 closest = ClosestTrianglePoint(centre, a, b, c);
                    float distanceSquared = Vector3.DistanceSquared(centre, closest);
                    if (distanceSquared < toleranceRadius * toleranceRadius)
                    {
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        ProbeFailure("static-sphere-triangle", centre, closest, closest,
                            normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : null,
                            mesh.Source, mesh.CellId, MathF.Sqrt(distanceSquared), toleranceRadius);
                        return false;
                    }
                }
            }
            return true;
        }

        static Vector3 ClosestTrianglePoint(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = point - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0)
                return a;
            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3)
                return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
                return a + ab * (d1 / (d1 - d3));
            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6)
                return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
                return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float sum = va + vb + vc;
            return MathF.Abs(sum) > 1e-12f ? a + ab * (vb / sum) + ac * (vc / sum) : a;
        }

        bool ClearWalkSegment(Vector3 from, Vector3 to)
            => CastClear(from + Vector3.UnitY * (_player.CenterY + _policy.CollisionPadding),
                to + Vector3.UnitY * (_player.CenterY + _policy.CollisionPadding), _player.Radius);

        bool CastClear(Vector3 from, Vector3 to, float radius)
        {
            if (_world.TrySphereCast(ToAo(from), ToAo(to), radius, out AoVector3 rest, out AoVector3 normal))
            {
                ProbeFailure("sphere-cast", from, to, ToNumerics(rest), ToNumerics(normal), limit: radius);
                return false;
            }
            if (_world.TrySphereCast(ToAo(to), ToAo(from), radius, out rest, out normal))
            {
                ProbeFailure("sphere-cast-reverse", to, from, ToNumerics(rest), ToNumerics(normal), limit: radius);
                return false;
            }
            return true;
        }

        bool Blocked(Vector3 point)
            => _selected.Any(chest => Near(chest, point, _chest.Radius + _player.Radius + _policy.CollisionPadding));

        bool BlockedSegment(Vector3 from, Vector3 to)
        {
            foreach (Vector3 chest in _selected)
            {
                if (MathF.Abs(chest.Y - from.Y) > _chest.Radius * 2 + _policy.MaxWalkStep
                    && MathF.Abs(chest.Y - to.Y) > _chest.Radius * 2 + _policy.MaxWalkStep)
                    continue;
                var a = new Vector2(from.X, from.Z);
                var b = new Vector2(to.X, to.Z);
                var c = new Vector2(chest.X, chest.Z);
                Vector2 segment = b - a;
                float factor = segment.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(c - a, segment) / segment.LengthSquared(), 0, 1) : 0;
                if (Vector2.Distance(c, a + segment * factor) < _chest.Radius + _player.Radius + _policy.CollisionPadding)
                    return true;
            }
            return false;
        }

        bool Near(Vector3 first, Vector3 second, float distance)
            => MathF.Abs(first.Y - second.Y) <= _chest.Radius * 2 + _policy.MaxWalkStep
                && Vector2.DistanceSquared(new Vector2(first.X, first.Z), new Vector2(second.X, second.Z)) < distance * distance;

        sealed record Node(int Room, Vector3 Point, bool Candidate, Vector3 Inward, int Approach, bool Corner);
        sealed record Wall(Vector3 A, Vector3 B, Vector3 Inward, Vector3 TriangleA, Vector3 TriangleB, Vector3 TriangleC);
    }

    static AoVector3 ToAo(Vector3 point) => new(point.X, point.Y, point.Z);
    static Vector3 ToNumerics(AoVector3 point) => new(point.xf, point.yf, point.zf);
}
