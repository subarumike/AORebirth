namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Core.GameData;
using AORebirth.DungeonGenerator;
using AORebirth.World.Collision;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.WorldSimulation;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using Fixture = QuestDungeonDestinationTests.Fixture;
using ObjectiveRun = MissionChestObjectiveIntegrationTests.ObjectiveRun;
using Vector3 = System.Numerics.Vector3;

[TestClass]
[DoNotParallelize]
public sealed class DungeonColdPathPerformanceTests
{
    static readonly JsonSerializerOptions FingerprintOptions = new() { IncludeFields = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MissionDungeonPerformance")]
    [DataRow(324, 1, 9, 125, false)]
    [DataRow(341, 2, 42, 22, true)]
    [DataRow(321, 1, 1, 37, false)]
    [DataRow(351, 1, 2, 160, false)]
    public void MeasureRetainedDungeonConstruction(int style, int floors, int seed, int quality, bool multiFloorProfile)
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_DUNGEON_TEST_ROOT");
        string? restartRoot = Environment.GetEnvironmentVariable("AO_REBIRTH_DUNGEON_TEST_RESTART_ROOT");
        Assert.IsNotNull(root, "Use the existing staged retained-content root.");
        Assert.IsNotNull(restartRoot, "Use the existing staged second revision for reconstruction.");
        _ = MissionChestPlacementTests.Data.Value;
        _ = MissionChestPlacementTests.Templates.Value;

        using var measure = new Measurement();
        string scenario = FormattableString.Invariant($"{style}/{floors}/{seed}/QL{quality}");
        measure.Phase = "first_style_use";
        var active = measure.Measure("generator_startup", () => MissionChestObjectiveIntegrationTests.CreateRetainedGenerator(root));
        string? fingerprint = null;
        for (int sample = 0; sample < 3; sample++)
        {
            measure.Phase = sample == 0 ? "first_style_use" : $"warm_{sample}";
            using var run = new ObjectiveRun(2, retainedLayouts: active, performance: measure,
                seed: seed, quality: quality, useMultiFloorProfile: multiFloorProfile);
            var parameters = run.Fixture.Parameters();
            Assert.AreEqual(style, parameters.LayoutPlan!.StyleId);
            Assert.AreEqual(floors, parameters.LayoutPlan.FloorCount);
            Assert.AreEqual(quality, parameters.Quality);
            Assert.IsNotNull(parameters.Content);
            Assert.IsNotNull(parameters.ObjectiveItem);
            Assert.IsNotNull(run.World.ChestPopulation);
            Assert.IsNull(run.World.ChestPopulation.DeferredReason);
            var census = Census(run.Dao);
            var stable = StableLayoutFingerprint(run.World, parameters, census);
            fingerprint ??= stable;
            Assert.AreEqual(fingerprint, stable);

            var firstDatabase = DatabaseOperations.Capture(run.Fixture, run.Dao);
            TestContext.WriteLine("MISSION_DUNGEON_BENCHMARK_DATABASE " + JsonSerializer.Serialize(new
            {
                scenario, phase = measure.Phase, firstDatabase
            }));

            if (sample != 0)
                continue;
            VerifyFreshRuntimeRestart(run, restartRoot, measure, scenario, stable);
            MeasureConstructionStages(root, run.World.Layout, parameters, measure, scenario);
        }

        TestContext.WriteLine("MISSION_DUNGEON_BENCHMARK_FINGERPRINT " + JsonSerializer.Serialize(new
        {
            scenario, fingerprint
        }));
        foreach (var sample in measure.Samples)
            TestContext.WriteLine("MISSION_DUNGEON_BENCHMARK_SAMPLE " + JsonSerializer.Serialize(new { scenario, sample }));
    }

    void VerifyFreshRuntimeRestart(ObjectiveRun initial, string restartRoot, Measurement measure, string scenario,
        string expectedFingerprint)
    {
        var accepted = initial.Fixture.Parameters();
        byte[][] chestBodies = MissionChestObjectiveIntegrationTests.ChestBodies(initial.World);
        string inventoryBefore = InventoryFingerprint(initial.Dao);
        var databaseBefore = DatabaseOperations.Capture(initial.Fixture, initial.Dao);
        var templates = MissionChestPlacementTests.Templates.Value;
        var ids = new StubInstanceIdAllocator();
        Assert.IsTrue(initial.Fixture.Manager.TryReleaseQuestDungeon(accepted.DungeonPlayfield, initial.Quest.QuestId));

        measure.Phase = "restart_fresh_resolver";
        var generator = measure.Measure("generator_startup", () => MissionChestObjectiveIntegrationTests.CreateRetainedGenerator(restartRoot));
        using var restart = new Fixture(initial.Dao, new ItemBuilder(templates, ids, new StubLogger()), templates: templates,
            inventory: new MissionChestObjectiveIntegrationTests.ObjectiveInventory(initial.Dao), ids: ids,
            playerId: initial.Fixture.Player.Identity.Instance, enableMissionPopulationPersistence: true,
            layouts: generator, performance: measure);
        restart.Store.Generated = initial.Fixture.Store.Generated;
        restart.Store.Character = initial.Fixture.Store.Character;
        foreach (var link in initial.Fixture.Store.Links)
            restart.Store.Links.Add(link.Key, link.Value);
        restart.Store.Keys.AddRange(initial.Fixture.Store.Keys);

        var destination = measure.Measure("full_reconstruction", () => restart.Service.ResolveLogin(accepted.DungeonPlayfield));
        Assert.IsInstanceOfType<QuestDungeonPlayfield>(destination.Playfield);
        var reconstructed = (QuestDungeonPlayfield)destination.Playfield;
        Assert.AreEqual(accepted.Content, reconstructed.Layout.Content);
        Assert.AreEqual(expectedFingerprint, StableLayoutFingerprint(reconstructed, accepted, Census(initial.Dao)));
        MissionChestObjectiveIntegrationTests.AssertObjectiveBinding(reconstructed, accepted);
        byte[][] replayBodies = MissionChestObjectiveIntegrationTests.ChestBodies(reconstructed);
        Assert.AreEqual(chestBodies.Length, replayBodies.Length);
        for (int i = 0; i < chestBodies.Length; i++)
            CollectionAssert.AreEqual(chestBodies[i], replayBodies[i]);
        Assert.AreEqual(inventoryBefore, InventoryFingerprint(initial.Dao));
        restart.AssertNoErrors();

        var database = DatabaseOperations.Capture(restart, initial.Dao) with
        {
            MissionTransactions = initial.Dao.Calls - databaseBefore.MissionTransactions,
            MissionSnapshotReads = initial.Dao.CharacterReads - databaseBefore.MissionSnapshotReads
        };
        TestContext.WriteLine("MISSION_DUNGEON_BENCHMARK_DATABASE " + JsonSerializer.Serialize(new
        {
            scenario, phase = measure.Phase, database, persistedChests = chestBodies.Length,
            chestFingerprint = Fingerprint(chestBodies)
        }));
    }

    void MeasureConstructionStages(string root, DungeonLayout acceptedLayout, QuestDungeonParameters accepted,
        Measurement measure, string scenario)
    {
        var reference = accepted.Content!;
        DungeonLayoutPlan plan = accepted.LayoutPlan!;
        var loader = new RetainedDungeonContent(root);
        DungeonGenerationRequest request = new()
        {
            StyleId = plan.StyleId, Seed = accepted.Seed, FloorCount = plan.FloorCount,
            FloorSize = plan.FloorSize, FloorHeight = plan.FloorHeight, BossFloor = plan.BossFloor,
            DescendingFloors = plan.DescendingFloors, UseFloorTransitions = true,
            LockedDoorChancePercent = plan.LockedDoorChancePercent, BuildingInstance = accepted.EntranceInstance
        };
        var objective = accepted.ObjectiveItem!;
        Vector3 point = new(objective.X, objective.Y, objective.Z);
        for (int sample = 0; sample < 3; sample++)
        {
            measure.Phase = sample == 0 ? "stage_fresh_resolver" : $"stage_warm_{sample}";
            var resources = measure.Measure("retained_archive_resolution", () => loader.Resolve(reference));
            var generated = measure.Measure("procedural_generation", () => DungeonGenerator.Generate(request, resources.RoomSet));
            CollectionAssert.AreEqual(acceptedLayout.GeneratorPayload, generated.Generator.ToByteArray());
            var binding = measure.Measure("original_geometry_binding",
                () => DungeonCollisionBuilder.BuildLayout(resources.Catalog, generated.Generator));
            Assert.IsTrue(binding.Collision.HasCollision);
            using var world = measure.Measure("collision_surface_construction", () => MissionChestPlacementTests.World(binding));
            var layout = acceptedLayout with { Geometry = binding };
            Assert.IsTrue(measure.Measure("standalone_frozen_objective_validation", () =>
            {
                var module = new DungeonObjectivePlacement();
                var validate = module.CreatePhysicalValidator(binding, layout, layout.Districts, world,
                    layout.PlayerCollisionSphere, layout.ChestPolicy?.Placement);
                return module.ValidateFrozen(binding, layout, layout.Districts, objective.RoomIndex, point, validate, out _);
            }));
            var chestPositions = measure.Measure("standalone_seeded_chest_placement", () => MissionChestPlacement.Plan(
                binding, layout, world, MissionChestPlacementTests.Data.Value, MissionChestPlacementTests.Templates.Value,
                [point], layout.ChestPolicy!, new Random(accepted.Seed)));
            TestContext.WriteLine("MISSION_DUNGEON_BENCHMARK_CHEST_PLAN " + JsonSerializer.Serialize(new
            {
                scenario, phase = measure.Phase, count = chestPositions.Count, fingerprint = Fingerprint(chestPositions)
            }));
        }
    }

    static string StableLayoutFingerprint(QuestDungeonPlayfield world, QuestDungeonParameters accepted, string census)
    {
        var dynels = world.GetRequiredService<DynelRegistry>().Dynels();
        var objective = accepted.ObjectiveItem!;
        var doors = dynels.OfType<Door>().OrderBy(door => door.Identity.Instance).Select(door => new
        {
            door.Identity.Instance, door.TemplateId, door.RoomLink, door.IsOpen, door.IsLocked, door.LockDifficulty,
            position = Coordinates(door.Position)
        }).ToArray();
        var transitions = dynels.OfType<DungeonFloorTransitionDynel>().OrderBy(portal => portal.Identity.Instance).Select(portal => new
        {
            portal.Identity.Instance, position = Coordinates(portal.Position), landing = Coordinates(portal.Landing)
        }).ToArray();
        return Fingerprint(new
        {
            world.Layout.GeneratorPayload, world.Layout.Content,
            objective.IdentityType, objective.Instance, objective.RoomIndex, objective.X, objective.Y, objective.Z,
            doors, transitions, census
        });
    }

    static (float, float, float) Coordinates(AoVector3 point) => (point.xf, point.yf, point.zf);

    static string Census(AuthoredMissionTestDao dao) => Fingerprint(dao.Flags.Values
        .Where(flag => flag.FlagKey.StartsWith("mission-token:", StringComparison.Ordinal) &&
            flag.FlagKey.Contains(":spawn:", StringComparison.Ordinal))
        .OrderBy(flag => flag.FlagKey, StringComparer.Ordinal)
        .Select(flag => new { key = flag.FlagKey[flag.FlagKey.IndexOf(":spawn:", StringComparison.Ordinal)..], flag.Value }).ToArray());

    static string InventoryFingerprint(AuthoredMissionTestDao dao) => Fingerprint(dao.Items.Values
        .OrderBy(item => item.InstanceId).Select(item => new
        {
            item.InstanceId, item.ContainerType, item.ContainerInstance, item.ContainerPlacement,
            item.ItemType, item.LowId, item.HighId, item.Quality, item.StackCount, item.Source
        }).ToArray());

    static string Fingerprint<T>(T value) => Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, FingerprintOptions)));

    readonly record struct DatabaseOperations(int OfferReads, int QuestReads, int GeneratedReads, int KeyLookupReads,
        int AcceptCommits, int InventoryMutations, int MissionTransactions, int MissionSnapshotReads)
    {
        internal static DatabaseOperations Capture(Fixture fixture, AuthoredMissionTestDao dao) => new(
            fixture.Offers.CurrentOfferReads, fixture.Store.QuestReads, fixture.Store.GeneratedReads,
            fixture.Store.KeyLookupReads, fixture.Store.AcceptCalls, fixture.Persistence.MutationCalls, dao.Calls, dao.CharacterReads);
    }

    // These instruments delegate to the same production objective and population owners on both revisions.
    internal sealed class Measurement : IMissionDungeonObjectivePlacement, IMissionDungeonPopulationModule, IDisposable
    {
        readonly Process _process = Process.GetCurrentProcess();
        readonly DungeonObjectivePlacement _objective = new();
        Stamp? _doorsAndTransitions;
        Stamp? _populationPrepared;
        Stamp? _chestsPlanned;

        internal string Phase { get; set; } = "";
        internal List<Sample> Samples { get; } = [];

        internal T Measure<T>(string stage, Func<T> call)
        {
            Stamp before = TakeStamp();
            T result = call();
            End(stage, before);
            return result;
        }

        public bool TrySelect(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
            out int roomIndex, out AoVector3 position, Func<int, Vector3, string?>? validatePoint = null)
        {
            Stamp before = TakeStamp();
            bool result = _objective.TrySelect(dungeon, layout, districts, out roomIndex, out position, validatePoint);
            End("objective_selection_with_physical_check", before);
            return result;
        }

        public Func<int, Vector3, string?> CreatePhysicalValidator(DungeonWorldLayout dungeon, DungeonLayout layout,
            PlayfieldDistrictsData? districts, PlayfieldWorldSimulation world, CollisionSphereData? player,
            MissionChestPlacementPolicy? policy)
            => _objective.CreatePhysicalValidator(dungeon, layout, districts, world, player, policy);

        public bool ValidateFrozen(DungeonWorldLayout dungeon, DungeonLayout layout, PlayfieldDistrictsData? districts,
            int roomIndex, Vector3 position, Func<int, Vector3, string?> validatePoint, out string failure)
        {
            Stamp before = TakeStamp();
            bool result = _objective.ValidateFrozen(dungeon, layout, districts, roomIndex, position, validatePoint, out failure);
            End("frozen_objective_validation", before);
            _doorsAndTransitions = TakeStamp();
            return result;
        }

        public void Populate(QuestDungeonPlayfield world, DungeonWorldLayout dungeon, PlayfieldDistrictsData? districts,
            MissionNpcObjectiveBinding? objectiveNpc, bool spawnNpcObjective, IGameData gameData, IZoneLogger logger,
            int[] roomDistances)
        {
            if (_doorsAndTransitions is { } doors)
            {
                End("door_and_floor_transition_construction", doors);
                _doorsAndTransitions = null;
            }

            Stamp before = TakeStamp();
            MissionDungeonPopulationModule.Shared.Populate(world, dungeon, districts, objectiveNpc, spawnNpcObjective,
                gameData, logger, roomDistances);
            End("npc_population_preparation", before);
            _populationPrepared = TakeStamp();
        }

        internal void ObserveInfo(string message)
        {
            if (message.StartsWith("Mission chest population planned ", StringComparison.Ordinal))
                _chestsPlanned = TakeStamp();
            else if (message.StartsWith("Mission chests loaded ", StringComparison.Ordinal))
            {
                if (_populationPrepared is { } population)
                    End("objective_registration_and_chest_initialization", population);
                if (_chestsPlanned is { } planned)
                    End("chest_seal_and_materialization_after_plan", planned);
                _populationPrepared = null;
                _chestsPlanned = null;
            }
        }

        Stamp TakeStamp() => new(Stopwatch.GetTimestamp(), _process.TotalProcessorTime, GC.GetAllocatedBytesForCurrentThread());

        void End(string stage, Stamp before)
        {
            double wall = Stopwatch.GetElapsedTime(before.Timestamp).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before.ThreadBytes;
            double cpu = (_process.TotalProcessorTime - before.Cpu).TotalMilliseconds;
            _process.Refresh();
            Samples.Add(new(Phase, stage, wall, cpu, allocated, _process.WorkingSet64, _process.PeakWorkingSet64));
        }

        public void Dispose() => _process.Dispose();

        readonly record struct Stamp(long Timestamp, TimeSpan Cpu, long ThreadBytes);
        internal readonly record struct Sample(string Phase, string Stage, double WallMs, double CpuMs,
            long CurrentThreadAllocatedBytes, long ProcessWorkingSetBytes, long ProcessPeakWorkingSetBytes);
    }
}
