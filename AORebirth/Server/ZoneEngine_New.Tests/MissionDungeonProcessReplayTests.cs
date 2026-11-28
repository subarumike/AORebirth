namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Database.Domain.Quests;
using AORebirth.Interfaces.Persistence.Missions;
using AORebirth.World.Collision;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests.Dungeons;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using Fixture = QuestDungeonDestinationTests.Fixture;
using ObjectiveRun = MissionChestObjectiveIntegrationTests.ObjectiveRun;

[TestClass]
[DoNotParallelize]
public sealed class DungeonProcessReplayTests
{
    const int Owner = 111;
    const int Format = 1;
    const int MaxSnapshotBytes = 4 * 1024 * 1024;
    static readonly FieldInfo SpawnPointsField = typeof(HashSpawnSystem).GetField("_allPoints",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The existing fixture spawn-point field is unavailable.");
    static readonly JsonSerializerOptions HashOptions = new() { IncludeFields = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MissionDungeonProcessReplayCapture")]
    [DataRow(324, 1, 9, 125, false)]
    [DataRow(341, 2, 42, 22, true)]
    [DataRow(321, 1, 1, 37, false)]
    [DataRow(351, 1, 2, 160, false)]
    public void CaptureAcceptedDungeon(int style, int floors, int seed, int quality, bool multiFloorProfile)
    {
        string root = RequiredDirectory("AO_REBIRTH_DUNGEON_TEST_ROOT");
        string path = SnapshotPath(style);
        using var performance = new DungeonColdPathPerformanceTests.Measurement();
        var generator = MissionChestObjectiveIntegrationTests.CreateRetainedGenerator(root);
        using var run = new ObjectiveRun(2, retainedLayouts: generator, performance: performance, seed: seed,
            quality: quality, useMultiFloorProfile: multiFloorProfile);
        QuestDungeonParameters accepted = run.Fixture.Parameters();
        ValidateCase(accepted, style, floors, seed, quality);
        Assert.IsNotNull(run.World.ChestPopulation);
        Assert.IsNull(run.World.ChestPopulation.DeferredReason);
        MissionChestObjectiveIntegrationTests.AssertObjectiveBinding(run.World, accepted);
        var kill = SeedDurableOrdinaryKill(run.Dao, run.World, accepted.Seed);
        var snapshot = new Snapshot(
            Format, Environment.ProcessId, run.Fixture.Store.Generated!, run.Fixture.Store.Character!,
            run.Fixture.Store.Keys.ToArray(), run.Fixture.Store.Links,
            AuthoredMissionTestDao.CloneCharacterQuest(run.Dao.CharacterQuest)!,
            run.Dao.Flags.Values.ToArray(), run.Dao.Objectives.Values.ToArray(), run.Dao.Observations.ToArray(),
            run.Dao.Items.Values.OrderBy(item => item.InstanceId).ToArray(), kill,
            FingerprintWorld(run.World), FingerprintPopulation(run.World),
            MissionChestObjectiveIntegrationTests.ChestBodies(run.World));
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        Assert.IsTrue(bytes.Length <= MaxSnapshotBytes, "The disposable replay snapshot must stay bounded.");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            file.Write(bytes);
        TestContext.WriteLine("MISSION_DUNGEON_PROCESS_CAPTURE " + JsonSerializer.Serialize(new
        {
            style, floors, seed, quality, snapshot.CapturePid, sha256 = Digest(bytes), bytes = bytes.Length,
            snapshot.Killed, chests = snapshot.ChestBodies.Length
        }));
    }

    [TestMethod]
    [TestCategory("MissionDungeonProcessReplay")]
    [DataRow(324, 1, 9, 125)]
    [DataRow(341, 2, 42, 22)]
    [DataRow(321, 1, 1, 37)]
    [DataRow(351, 1, 2, 160)]
    public void RestoreAcceptedDungeonInFreshProcess(int style, int floors, int seed, int quality)
    {
        string root = RequiredDirectory("AO_REBIRTH_DUNGEON_TEST_RESTART_ROOT");
        string path = SnapshotPath(style);
        Assert.IsTrue(new FileInfo(path).Length is > 0 and <= MaxSnapshotBytes);
        byte[] bytes = File.ReadAllBytes(path);
        Snapshot? snapshot = JsonSerializer.Deserialize<Snapshot>(bytes);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(Format, snapshot.Schema);
        Assert.AreNotEqual(snapshot.CapturePid, Environment.ProcessId, "Replay must run in a separate test-host process.");
        Assert.IsTrue(QuestDungeonParameters.TryParse(snapshot.Generated.AcgBuildingGeneratorJson, out var accepted));
        ValidateCase(accepted, style, floors, seed, quality);
        Assert.IsTrue(snapshot.Generated.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks, "Use a fresh capture.");
        Assert.AreEqual(Owner, snapshot.Assignment.CharacterId, "Only the fixture player can be replayed.");
        var dao = new AuthoredMissionTestDao
        {
            CharacterQuest = snapshot.Completion,
            Flags = snapshot.Flags.ToDictionary(flag => new MissionKeyData(flag.CharacterId, flag.QuestId) + "|" + flag.FlagKey),
            Objectives = snapshot.Objectives.ToDictionary(row => new MissionObjectiveKeyData(
                new MissionKeyData(row.CharacterId, row.QuestId), row.ObjectiveId)),
            Observations = new HashSet<string>(snapshot.Observations, StringComparer.Ordinal),
            Items = snapshot.Inventory.ToDictionary(item => item.InstanceId)
        };

        using var measure = new DungeonColdPathPerformanceTests.Measurement { Phase = "separate_process_replay" };
        var templates = MissionChestPlacementTests.Templates.Value;
        var generator = measure.Measure("generator_startup", () => MissionChestObjectiveIntegrationTests.CreateRetainedGenerator(root));
        var ids = new StubInstanceIdAllocator();
        using var fixture = new Fixture(dao, new ItemBuilder(templates, ids, new StubLogger()), templates: templates,
            inventory: new MissionChestObjectiveIntegrationTests.ObjectiveInventory(dao), ids: ids,
            playerId: Owner, enableMissionPopulationPersistence: true, layouts: generator, performance: measure);
        fixture.Store.Generated = snapshot.Generated;
        fixture.Store.Character = snapshot.Assignment;
        fixture.Store.Keys.AddRange(snapshot.Keys);
        foreach (var link in snapshot.Links)
            fixture.Store.Links.Add(link.Key, link.Value);
        string items = FingerprintInventory(snapshot.Inventory);

        var result = measure.Measure("full_reconstruction", () => fixture.Service.ResolveLogin(accepted.DungeonPlayfield));
        Assert.IsInstanceOfType<QuestDungeonPlayfield>(result.Playfield);
        var world = (QuestDungeonPlayfield)result.Playfield;
        Assert.AreEqual(accepted.Content, world.Layout.Content);
        Assert.AreEqual(snapshot.World, FingerprintWorld(world));
        Assert.AreEqual(snapshot.Population, FingerprintPopulation(world));
        MissionChestObjectiveIntegrationTests.AssertObjectiveBinding(world, accepted);
        Assert.IsTrue(world.TokenProgress!.IsKilled(snapshot.Killed.Point, snapshot.Killed.Ordinal));
        HashSpawnPoint killedPoint = Points(world).Single(point => point.InitialTransformSeed == snapshot.Killed.TransformSeed);
        Assert.IsTrue(killedPoint.IsBranchKilled!(snapshot.Killed.Ordinal));
        Assert.IsFalse(killedPoint.RespawnsAfterDeath);
        if (killedPoint.FrozenTemplates!.Length == 1)
            Assert.IsTrue(killedPoint.Retired, "A wholly killed point must be retired on reconstruction.");
        byte[][] actualChests = MissionChestObjectiveIntegrationTests.ChestBodies(world);
        Assert.AreEqual(snapshot.ChestBodies.Length, actualChests.Length);
        for (int i = 0; i < actualChests.Length; i++)
            CollectionAssert.AreEqual(snapshot.ChestBodies[i], actualChests[i]);
        Assert.AreEqual(items, FingerprintInventory(dao.Items.Values));
        Assert.AreEqual(0, fixture.Store.AcceptCalls);
        Assert.AreEqual(0, fixture.Persistence.MutationCalls);
        fixture.AssertNoErrors();

        // Checking the replacement revision after the timed replay keeps its catalog parse out of the cold path.
        var newAcceptance = new QuestDungeonParameters
        {
            Seed = seed, Quality = quality, DungeonPlayfield = accepted.DungeonPlayfield,
            EntranceInstance = accepted.EntranceInstance
        };
        Assert.IsTrue(generator.ConfigureNewDungeon(newAcceptance, accepted.IsTeam));
        Assert.AreNotEqual(accepted.Content, newAcceptance.Content, "The active retained revision must actually differ.");

        TestContext.WriteLine("MISSION_DUNGEON_PROCESS_REPLAY " + JsonSerializer.Serialize(new
        {
            style, floors, seed, quality, snapshot.CapturePid, replayPid = Environment.ProcessId,
            snapshotSha256 = Digest(bytes), snapshot.World, snapshot.Population, snapshot.Killed,
            chests = actualChests.Length, dao.Calls, dao.CharacterReads, fixture.Store.AcceptCalls,
            fixture.Store.QuestReads, fixture.Store.GeneratedReads, samples = measure.Samples
        }));
    }

    // The snapshot is fixture-only data. Use the existing test DAO transaction to seed one already
    // recorded ordinary kill, so replay covers both persisted observations and suppression.
    static KilledBranch SeedDurableOrdinaryKill(AuthoredMissionTestDao dao, QuestDungeonPlayfield world, int seed)
    {
        string prefix = "mission-token:" + dao.CharacterQuest!.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture) + ":";
        string slot = dao.Flags.Values
            .Where(flag => flag.FlagKey.StartsWith(prefix + "spawn:r", StringComparison.Ordinal))
            .OrderBy(flag => flag.FlagKey, StringComparer.Ordinal).First().FlagKey[(prefix.Length + "spawn:".Length)..];
        int separator = slot.LastIndexOf(':');
        string point = slot[..separator];
        int ordinal = int.Parse(slot[(separator + 1)..], CultureInfo.InvariantCulture);
        int pointSeparator = point.IndexOf('p');
        int room = int.Parse(point.AsSpan(1, pointSeparator - 1), CultureInfo.InvariantCulture);
        int pointIndex = int.Parse(point.AsSpan(pointSeparator + 1), CultureInfo.InvariantCulture);
        int transformSeed = unchecked((((seed * 397) ^ room) * 397) ^ pointIndex);
        Assert.IsTrue(Points(world).Any(spawn => spawn.InitialTransformSeed == transformSeed));
        var mission = new MissionKeyData(Owner, dao.CharacterQuest.QuestId);
        var progressKey = new MissionObjectiveKeyData(mission, prefix + "population");
        dao.Execute(Owner, tx =>
        {
            Assert.IsTrue(tx.TryAddObservation(new MissionObjectiveObservationData
            {
                CharacterId = Owner, QuestId = mission.QuestId, ObjectiveId = prefix + "population",
                ObservationKey = slot, EventType = "MissionNpcKilled"
            }));
            MissionObjectiveProgressData progress = tx.GetObjective(progressKey);
            Assert.AreEqual(0, progress.Progress);
            progress.Progress = 1;
            progress.LastObservationKey = slot;
            tx.SaveObjective(progressKey, progress);
            tx.SaveFlag(mission, new MissionFlagData
            {
                CharacterId = Owner, QuestId = mission.QuestId, FlagKey = prefix + "dead:" + slot, Value = "1"
            });
            return true;
        });
        return new KilledBranch(point, ordinal, transformSeed);
    }

    static string FingerprintWorld(QuestDungeonPlayfield world)
    {
        var dynels = world.GetRequiredService<DynelRegistry>().Dynels();
        var doors = dynels.OfType<Door>().OrderBy(door => door.Identity.Instance).Select(door => new
        {
            door.Identity.Instance, door.TemplateId, door.RoomLink, door.IsOpen, door.IsLocked, door.LockDifficulty,
            position = Coordinates(door.Position)
        }).ToArray();
        var transitions = dynels.OfType<DungeonFloorTransitionDynel>().OrderBy(transition => transition.Identity.Instance)
            .Select(transition => new { transition.Identity.Instance, position = Coordinates(transition.Position), landing = Coordinates(transition.Landing) })
            .ToArray();
        return Fingerprint(new { world.Layout.GeneratorPayload, world.Layout.Content, doors, transitions,
            collision = FingerprintCollision(world.Layout.Geometry!.Collision) });
    }

    static string FingerprintCollision(PlayfieldCollisionSet geometry)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> metadata = stackalloc byte[12];
        foreach (var mesh in geometry.SurfaceMeshes)
        {
            BinaryPrimitives.WriteInt32LittleEndian(metadata, mesh.CellId ?? -1);
            BinaryPrimitives.WriteInt32LittleEndian(metadata[4..], mesh.Vertices.Length);
            BinaryPrimitives.WriteInt32LittleEndian(metadata[8..], mesh.Triangles.Length);
            hash.AppendData(metadata);
            hash.AppendData(MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()));
            hash.AppendData(MemoryMarshal.AsBytes(mesh.Triangles.AsSpan()));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static IEnumerable<HashSpawnPoint> Points(QuestDungeonPlayfield world)
        => (List<HashSpawnPoint>)SpawnPointsField.GetValue(world.GetRequiredService<HashSpawnSystem>())!;

    static string FingerprintPopulation(QuestDungeonPlayfield world) => Fingerprint(Points(world)
        .Where(point => point.InitialTransformSeed.HasValue).Select(point => new
        {
            point.HashText, point.InitialTransformSeed, point.CellId, point.RespawnsAfterDeath,
            templates = point.FrozenTemplates?.Select(template => template.Hash).ToArray(),
            sites = point.Sites.Select(site => new { centre = Coordinates(site.Centre), site.FacingDegrees,
                site.WidthDegrees, site.Radius }).ToArray()
        }).ToArray());

    static string FingerprintInventory(IEnumerable<MissionItemInstanceData> items) =>
        Fingerprint(items.OrderBy(item => item.InstanceId).ToArray());

    static (float, float, float) Coordinates(AoVector3 point) => (point.xf, point.yf, point.zf);

    static string Fingerprint<T>(T value) => Digest(JsonSerializer.SerializeToUtf8Bytes(value, HashOptions));

    static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    static string RequiredDirectory(string variable)
    {
        string? configured = Environment.GetEnvironmentVariable(variable);
        Assert.IsNotNull(configured, "Set " + variable + " to the owned ignored test directory.");
        string root = Path.GetFullPath(configured);
        Assert.IsTrue(Directory.Exists(root), "The specified test directory must already exist.");
        return root;
    }

    static string SnapshotPath(int style) => Path.Combine(RequiredDirectory("AO_REBIRTH_DUNGEON_REPLAY_SNAPSHOT_ROOT"),
        "replay-" + style.ToString(CultureInfo.InvariantCulture) + ".json");

    static void ValidateCase(QuestDungeonParameters accepted, int style, int floors, int seed, int quality)
    {
        Assert.AreEqual(style, accepted.LayoutPlan!.StyleId);
        Assert.AreEqual(floors, accepted.LayoutPlan.FloorCount);
        Assert.AreEqual(seed, accepted.Seed);
        Assert.AreEqual(quality, accepted.Quality);
        Assert.IsNotNull(accepted.Content);
        Assert.IsNotNull(accepted.ObjectiveItem);
    }

    public sealed record KilledBranch(string Point, int Ordinal, int TransformSeed);
    public sealed record Snapshot(int Schema, int CapturePid, GeneratedQuestRow Generated, CharacterQuestRow Assignment,
        QuestDungeonKeyItemRow[] Keys, Dictionary<int, string> Links, CharacterQuestCompletionSnapshotData Completion,
        MissionFlagData[] Flags, MissionObjectiveProgressData[] Objectives, string[] Observations,
        MissionItemInstanceData[] Inventory, KilledBranch Killed, string World, string Population, byte[][] ChestBodies);
}
