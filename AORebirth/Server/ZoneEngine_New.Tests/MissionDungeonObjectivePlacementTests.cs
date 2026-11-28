namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.WorldSimulation;
using AoVector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
[DoNotParallelize]
public sealed class MissionDungeonObjectivePlacementTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(324, 1, 818302858, 12)]
    [DataRow(341, 2, 34719395, 8)]
    public void RealDungeonReconstructionsRetainExactReachableObjective(int style, int floors, int seed, int size)
    {
        var module = new DungeonObjectivePlacement();
        GameDataStore data = MissionChestPlacementTests.Data.Value;
        MissionChestPlacementPolicy policy = MissionChestPlacementTests.ReadPolicy().Placement!;
        var districts = QuestDungeonPlayfield.LoadStyleDistricts(data.RootPath, style, new StubLogger());
        Assert.IsNotNull(districts);
        var (layout, dungeon) = MissionChestPlacementTests.Build(style, floors, seed, size);
        using var world = MissionChestPlacementTests.World(dungeon);
        using var process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var elapsed = Stopwatch.StartNew();
        var physical = module.CreatePhysicalValidator(dungeon, layout, districts, world, data.PlayerCollisionSphere, policy);
        var rejected = new Dictionary<string, (int Count, Vector3 Point, float? Floor)>();
        bool selected = module.TrySelect(dungeon, layout, districts, out int room, out AoVector3 point, RecordRejection);
        foreach (var (reason, details) in rejected)
            TestContext.WriteLine("Rejected {0} candidate(s): {1}; example point {2}, floor {3}",
                details.Count, reason, details.Point, details.Floor);
        Assert.IsTrue(selected, "The selected real layout must have a physically reachable original district point.");

        string? RecordRejection(int candidateRoom, Vector3 candidate)
        {
            string? reason = physical(candidateRoom, candidate);
            if (reason == null)
                return null;
            if (rejected.TryGetValue(reason, out var existing))
                rejected[reason] = (existing.Count + 1, existing.Point, existing.Floor);
            else
            {
                bool hasFloor = world.TryRaycastDown(new AoVector3(candidate.X, candidate.Y + policy.FloorProbeLift, candidate.Z),
                    policy.FloorProbeLift + policy.MaxFloorDrop, out AoVector3 hit);
                rejected[reason] = (1, candidate, hasFloor ? hit.yf : null);
            }
            return reason;
        }
        elapsed.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        Assert.IsTrue(room > 0);
        Assert.IsTrue(DungeonObjectivePlacement.RoomDistances(dungeon, layout)[room] > 0);
        Assert.IsTrue(world.TryRaycastDown(new AoVector3(point.xf, point.yf + policy.FloorProbeLift, point.zf),
            policy.FloorProbeLift + policy.MaxFloorDrop, out AoVector3 floor));
        Assert.IsTrue(point.yf >= floor.yf - policy.MaxWalkStep && point.yf <= floor.yf + policy.MaxWalkStep);
        Assert.AreEqual(floors > 1, layout.FloorTransitions.Count > 0);

        var (restoredLayout, restoredDungeon) = MissionChestPlacementTests.Build(style, floors, seed, size);
        using var restoredWorld = MissionChestPlacementTests.World(restoredDungeon);
        var restoredPhysical = module.CreatePhysicalValidator(restoredDungeon, restoredLayout, districts, restoredWorld,
            data.PlayerCollisionSphere, policy);
        Vector3 frozen = new(point.xf, point.yf, point.zf);
        Assert.IsTrue(module.ValidateFrozen(restoredDungeon, restoredLayout, districts, room, frozen, restoredPhysical,
            out string failure), failure);
        Assert.IsTrue(module.TrySelect(restoredDungeon, restoredLayout, districts, out int replayRoom, out var replay,
            restoredPhysical));
        Assert.AreEqual(room, replayRoom);
        Assert.AreEqual(frozen, new Vector3(replay.xf, replay.yf, replay.zf));

        Vector3 aboveStep = frozen with { Y = MathF.BitIncrement(floor.yf + policy.MaxWalkStep) };
        Assert.AreEqual("position height is outside the supported floor step", restoredPhysical(room, aboveStep),
            "The next representable height above the configured step must remain ineligible.");
        Vector3 altered = frozen + Vector3.UnitY * (policy.FloorProbeLift + policy.MaxFloorDrop + policy.MaxWalkStep);
        Assert.IsNotNull(restoredPhysical(room, altered), "An airborne position must fail physical validation.");
        Assert.IsFalse(module.ValidateFrozen(restoredDungeon, restoredLayout, districts, room, altered, restoredPhysical,
            out string rejection));
        StringAssert.Contains(rejection, "not an exact retained district point");
        TestContext.WriteLine("Objective placement style {0}, floors {1}, seed {2}, room {3}, position {4}, elapsed {5:F2} ms, allocated {6} bytes, CPU {7:F2} ms",
            style, floors, seed, room, frozen, elapsed.Elapsed.TotalMilliseconds, allocated, cpuMs);
    }

    [TestMethod]
    [DataRow(324, 1, 818302858, 12)]
    [DataRow(341, 2, 34719395, 8)]
    public void RecordLegacyAndSharedGeometryObjectivePlacementCost(int style, int floors, int seed, int size)
    {
        var module = new DungeonObjectivePlacement();
        GameDataStore data = MissionChestPlacementTests.Data.Value;
        IZoneLogger logger = new StubLogger();
        var (layout, dungeon) = MissionChestPlacementTests.Build(style, floors, seed, size);
        var districts = QuestDungeonPlayfield.LoadStyleDistricts(data.RootPath, style, logger);
        const int iterations = 3;
        var baseline = Measure(() =>
        {
            Assert.IsTrue(DungeonObjectivePlacement.TrySelect(data.RootPath, layout, logger, out int room, out var point));
            return (room, new Vector3(point.xf, point.yf, point.zf));
        }, iterations);
        var shared = Measure(() =>
        {
            Assert.IsTrue(module.TrySelect(dungeon, layout, districts, out int room, out var point));
            return (room, new Vector3(point.xf, point.yf, point.zf));
        }, iterations);
        Assert.AreEqual(baseline.Position, shared.Position);
        TestContext.WriteLine(
            "Objective geometry reuse: style {0}, floors {1}, seed {2}, iterations {3}; baseline wall {4:F2} ms, CPU {5:F2} ms, allocated {6} bytes; shared wall {7:F2} ms, CPU {8:F2} ms, allocated {9} bytes",
            style, floors, seed, iterations, baseline.WallMs, baseline.CpuMs, baseline.Bytes,
            shared.WallMs, shared.CpuMs, shared.Bytes);

        static ((int Room, Vector3 Point) Position, double WallMs, double CpuMs, long Bytes)
            Measure(Func<(int Room, Vector3 Point)> place, int iterations)
        {
            _ = place();
            using var process = Process.GetCurrentProcess();
            TimeSpan cpu = process.TotalProcessorTime;
            long before = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            (int, Vector3) position = default;
            for (int run = 0; run < iterations; run++)
                position = place();
            timer.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            return (position, timer.Elapsed.TotalMilliseconds, (process.TotalProcessorTime - cpu).TotalMilliseconds, bytes);
        }
    }

    [TestMethod]
    [DataRow(324, 1, 818302858, 12)]
    [DataRow(341, 2, 34719395, 8)]
    public void PlacementQueryResultsAgreeWithFullCollisionQueries(int style, int floors, int seed, int size)
    {
        var (layout, dungeon) = MissionChestPlacementTests.Build(style, floors, seed, size);
        using var world = MissionChestPlacementTests.World(dungeon);
        var random = new Random(seed);
        var origin = new N3Lite.Vec3(layout.Spawn.xf, layout.Spawn.yf + 2f, layout.Spawn.zf);
        float radius = MissionChestPlacementTests.Data.Value.PlayerCollisionSphere!.Radius;
        int blocked = 0;
        int clear = 0;
        for (int i = 0; i < 48; i++)
        {
            var destination = new N3Lite.Vec3(origin.X + random.Next(-18, 19), origin.Y + random.Next(-8, 9),
                origin.Z + random.Next(-18, 19));
            bool full = world.TrySphereCast(origin, destination, radius, firstHitOnly: false, out _, out _);
            bool early = world.TrySphereCast(origin, destination, radius, firstHitOnly: true, out _, out _);
            Assert.AreEqual(full, early, $"Sweep {i}, style {style}");
            if (full)
                blocked++;
            else
                clear++;

            var reference = new AoVector3(destination.X, destination.Y, destination.Z);
            bool referenceFloor = world.TryRaycastDown(reference, 12f, out AoVector3 hit);
            Assert.AreEqual(referenceFloor, world.TryRaycastDown(destination, 12f, out float height));
            if (referenceFloor)
                Assert.AreEqual(hit.yf, height);
        }

        Assert.IsTrue(blocked > 0 && clear > 0, "The real layout must exercise both sweep outcomes.");
    }

    [TestMethod]
    public void PhysicalValidatorRejectsMissingCollisionInsteadOfUsingUnverifiedPoint()
    {
        var module = new DungeonObjectivePlacement();
        GameDataStore data = MissionChestPlacementTests.Data.Value;
        var (layout, dungeon) = MissionChestPlacementTests.Build(324, 1, 818302858, 12);
        var districts = QuestDungeonPlayfield.LoadStyleDistricts(data.RootPath, 324, new StubLogger());
        using var world = PlayfieldWorldSimulation.Create(1, new PlayfieldGeometryData(), null,
            new DestinationsCatalog(), data, new StubLogger());
        var validate = module.CreatePhysicalValidator(dungeon, layout, districts, world, data.PlayerCollisionSphere,
            MissionChestPlacementTests.ReadPolicy().Placement);
        Assert.IsTrue(module.TrySelect(dungeon, layout, districts, out int room, out var point));
        Assert.IsFalse(module.TrySelect(dungeon, layout, districts, out _, out _, validate));
        Assert.IsFalse(module.ValidateFrozen(dungeon, layout, districts, room, new Vector3(point.xf, point.yf, point.zf),
            validate, out string rejection));
        StringAssert.Contains(rejection, "collision data or placement policy is unavailable");
    }
}
