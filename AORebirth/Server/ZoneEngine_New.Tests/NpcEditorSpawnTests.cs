namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
public sealed class NpcEditorSpawnTests
{
    const int PlayfieldId = 777;
    const string OriginalHash = "SRC1";
    const string NewHash = "DST1";
    const string Actor = "FixtureGM";

    [TestMethod]
    public void SavedPlacementSurvivesNativeReconstructionAndSchedulesItsNewRespawn()
    {
        using var fixture = new Content();
        string source = File.ReadAllText(fixture.PlacementPath);
        using var live = new World(fixture);
        NpcCharacter original = live.NpcAt(0);
        NpcEditorSpawnTarget target = live.Editor.CaptureTarget(original);
        NpcEditorSpawnSettings before = target.View.Configured!;
        var after = before with
        {
            X = 30, Y = 4, Z = 40, FacingDegrees = 90, Hash = NewHash,
            MinLevel = 35, MaxLevel = 35, RespawnSeconds = 91, RespawnChance = 68
        };

        NpcEditorSpawnView saved = live.Editor.Save(target, new(0, after), Actor);
        Assert.AreEqual(1, saved.Revision);
        Assert.AreEqual(Actor, saved.History.Single().Actor);
        Assert.IsTrue(saved.RequiresPlayfieldReload);
        Assert.AreEqual(OriginalHash, original.MobTemplate!.Hash);
        Assert.AreEqual(source, File.ReadAllText(fixture.PlacementPath));

        using var rebuilt = new World(fixture);
        NpcCharacter changed = rebuilt.NpcAt(0);
        NpcCharacter unrelated = rebuilt.NpcAt(1);
        HashSpawnPoint changedPoint = rebuilt.PointAt(0);
        Assert.AreEqual(OriginalHash, changedPoint.Source.HashText);
        Assert.AreEqual(NewHash, changed.MobTemplate!.Hash);
        Assert.AreEqual(35, changed.Stats.GetOrZero(CharacterStat.Level));
        Assert.AreEqual(30f, changed.Position.xf);
        Assert.AreEqual(40f, changed.Position.zf);
        Assert.AreEqual(90, changedPoint.Sites[0].FacingDegrees);
        Assert.AreEqual(68, changedPoint.RespawnChance);
        Assert.AreEqual(original.SpawnPointKey, changed.SpawnPointKey);
        Assert.AreEqual(OriginalHash, unrelated.MobTemplate!.Hash);
        Assert.AreEqual(60f, unrelated.Position.xf);
        Assert.IsFalse(rebuilt.Editor.Describe(changed).RequiresPlayfieldReload);

        NpcEditorSpawnTarget restoredTarget = rebuilt.Editor.CaptureTarget(changed);
        DateTime minimum = DateTime.UtcNow.AddSeconds(91);
        rebuilt.OnTick(changed.DespawnAsDeath);
        Assert.AreEqual(HashSpawnState.Dead, changedPoint.State);
        Assert.IsTrue(changedPoint.NextSpawnTime >= minimum && changedPoint.NextSpawnTime <= DateTime.UtcNow.AddSeconds(91));
        Assert.AreEqual(1, rebuilt.SpawnSystem.CountAuthoredSpawnNpcs(1));
        Assert.AreEqual(before, rebuilt.Editor.Restore(restoredTarget, new(1, 0), Actor).Configured);

        using var reopened = new World(fixture);
        Assert.AreEqual(OriginalHash, reopened.NpcAt(0).MobTemplate!.Hash);
        Assert.AreEqual(10f, reopened.NpcAt(0).Position.xf);
        Assert.AreEqual(30, reopened.PointAt(0).RespawnTimeSeconds);
    }

    [TestMethod]
    public void InvalidAndStaleChangesAreRejectedBeforeWritingAndBatchPreviewDoesNotWrite()
    {
        using var fixture = new Content();
        using var world = new World(fixture);
        NpcEditorSpawnTarget target = world.Editor.CaptureTarget(world.NpcAt(0));
        NpcEditorSpawnSettings before = target.View.Configured!;
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            world.Editor.Save(target, new(0, before with { MinLevel = 42, MaxLevel = 12 }), Actor)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            world.Editor.Save(target, new(0, before with { Hash = "MISS" }), Actor)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            world.Editor.PrepareRespawn(target, 1.5, null)).StatusCode);
        NpcEditorSpawnRespawnPlan preview = world.Editor.PrepareRespawn(target, 65, 0);
        Assert.AreEqual(30, preview.Before.RespawnSeconds);
        Assert.AreEqual(65, preview.After.RespawnSeconds);
        Assert.AreEqual(0, preview.After.RespawnChance);
        Assert.IsFalse(File.Exists(fixture.OverlayPath));

        world.Editor.Save(target, new(preview.ExpectedRevision, preview.After), Actor);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            world.Editor.Save(target, new(0, before with { RespawnSeconds = 16 }), Actor)).StatusCode);
        var source = JsonSerializer.Deserialize<PlayfieldSpawnsData>(File.ReadAllText(fixture.PlacementPath))!;
        source.Spawns[0].Position = [1, 2, 3];
        File.WriteAllText(fixture.PlacementPath, JsonSerializer.Serialize(source));
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            world.Editor.Restore(target, new(1, 0), Actor)).StatusCode);
        using var rebuilt = new World(fixture);
        Assert.AreEqual(1f, rebuilt.NpcAt(0).Position.xf);
        Assert.IsFalse(rebuilt.Editor.Describe(rebuilt.NpcAt(0)).CanSave);
    }

    [TestMethod]
    public void LivePositionPreviewUsesNativeWarpAndUndoWithoutChangingSpawnSettings()
    {
        using var fixture = new Content();
        using var world = new World(fixture);
        NpcCharacter first = world.NpcAt(0);
        NpcCharacter second = world.NpcAt(1);
        NpcEditorSpawnView preview = world.OnTick(() => world.Editor.Preview(first, new(16, 0, 23), Actor));
        Assert.AreEqual(16f, first.Position.xf);
        Assert.AreEqual(60f, second.Position.xf);
        Assert.AreEqual(10f, preview.Runtime!.X);
        Assert.AreEqual(Actor, preview.LivePreview!.History.Single().Actor);
        Guid undo = preview.LivePreview.ChangeId!.Value;
        world.OnTick(() => world.Editor.UndoPreview(first, new(undo), Actor));
        Assert.AreEqual(10f, first.Position.xf);
        Assert.IsFalse(File.Exists(fixture.OverlayPath));
    }

    [TestMethod]
    public void DormantZeroChancePointCanBeInspectedAndRestoredWithoutAnNpc()
    {
        using var fixture = new Content();
        using var live = new World(fixture);
        NpcEditorSpawnTarget target = live.Editor.CaptureTarget(live.NpcAt(0));
        live.Editor.Save(target, new(0, target.View.Configured! with { RespawnChance = 0 }), Actor);

        using var rebuilt = new World(fixture);
        NpcEditorSpawnView dormant = rebuilt.Editor.Describe(rebuilt.Field, 0);
        Assert.AreEqual("Dead", dormant.State);
        Assert.AreEqual(0, dormant.LoadedNpcs);
        Assert.AreEqual(0, dormant.Runtime!.RespawnChance);
        Assert.AreEqual(2, rebuilt.Editor.List(rebuilt.Field, 0, 25).Total);
        Assert.IsNull(dormant.LivePreview);
        var restored = rebuilt.Editor.Restore(rebuilt.Editor.CaptureTarget(rebuilt.Field, 0), new(1, 0), Actor);
        Assert.AreEqual(100, restored.Configured!.RespawnChance);
        using var reopened = new World(fixture);
        Assert.AreEqual(OriginalHash, reopened.NpcAt(0).MobTemplate!.Hash);
    }

    sealed class World : IDisposable
    {
        readonly Lock _tick = new();
        readonly ServiceProvider _services;
        internal Playfield Field { get; }
        internal HashSpawnSystem SpawnSystem { get; }
        internal NpcEditorSpawn Editor { get; }

        internal World(Content fixture)
        {
            var logger = new StubLogger();
            var data = new GameDataStore(logger, null, fixture.Root, fixture.Overlay);
            Playfield field = Field = Blank<Playfield>();
            Set(field, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = PlayfieldId });
            Set(field, "<ResourceId>k__BackingField", PlayfieldId);
            Set(field, "_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            Set(field, "_lifecycleSync", new Lock());
            Set(field, "_tickSync", _tick);
            var registry = new DynelRegistry();
            var locality = new PlayfieldLocality(PlayfieldId, null);
            var items = new StubItemBuilder();
            var minter = new HashItemMinter(data, new StubCatalog(), items);
            var moves = new InventoryMoveService(logger, Blank<InventoryFlushService>(), Blank<InventoryActionService>());
            _services = new ServiceCollection().AddSingleton(registry).AddSingleton(locality)
                .AddSingleton<IGameData>(data).AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(new WorldSimulationAccess()).AddSingleton(moves).AddSingleton(new GameplaySettings())
                .AddSingleton(new NpcContentActivationService(field, registry, locality, items, new StubCatalog(), data))
                .AddSingleton(services => new SpawnService(services, registry, logger, field, Blank<PlayfieldManager>(), data,
                    items, minter, Blank<InventoryFlushService>(), Blank<TradeService>(), Blank<CharacterSnapshotService>()))
                .AddSingleton(services => new HashSpawnSystem(field, services.GetRequiredService<SpawnService>(), data, logger))
                .BuildServiceProvider();
            Set(field, "_serviceProvider", _services);
            SpawnSystem = _services.GetRequiredService<HashSpawnSystem>();
            SpawnSystem.Initialize(locality);
            Editor = new(data);
            MethodInfo tick = typeof(HashSpawnSystem).GetMethod("OnIndoorSpawnTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            OnTick(() => { tick.Invoke(SpawnSystem, null); tick.Invoke(SpawnSystem, null); });
        }

        internal HashSpawnPoint PointAt(int index)
            => ((System.Collections.Generic.List<HashSpawnPoint>)typeof(HashSpawnSystem)
                .GetField("_allPoints", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(SpawnSystem)!)
                .Single(point => point.AuthoredSpawnIndex == index);

        internal NpcCharacter NpcAt(int index) => PointAt(index).MembersSnapshot().OfType<NpcCharacter>().Single();

        internal void OnTick(Action work) { lock (_tick) work(); }

        internal T OnTick<T>(Func<T> work) { lock (_tick) return work(); }

        static T Blank<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        static void Set(Playfield field, string name, object value) => typeof(Playfield)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(field, value);

        public void Dispose() => _services.Dispose();
    }

    sealed class Content : IDisposable
    {
        readonly string _folder = Directory.CreateTempSubdirectory("aorebirth-npc-spawn-").FullName;
        internal string Root => Path.Combine(_folder, "GameData");
        internal string Overlay => Path.Combine(_folder, "Overlay");
        internal string PlacementPath => Path.Combine(Root, GameDataPaths.PlayfieldSpawnsRelativePath(PlayfieldId));
        internal string OverlayPath => Path.Combine(Overlay, NpcEditorSpawnStore.FileName);

        internal Content()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlacementPath)!);
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(Path.Combine(Root, GameDataPaths.NpcTemplatesFileName), """
                {"SRC1":{"Templates":[{"Name":"Original fixture","Level":20}]},
                 "DST1":{"Templates":[{"Name":"Reassigned fixture","Level":35}]}}
                """);
            File.WriteAllText(PlacementPath, JsonSerializer.Serialize(new PlayfieldSpawnsData
            {
                SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                PlayfieldId = PlayfieldId,
                Spawns = [Placement(10), Placement(60)]
            }));
        }

        static PlayfieldSpawnEntry Placement(float x) => new()
        {
            HashText = OriginalHash, Position = [x, 0, 20], Radius = 0,
            MinLevel = 20, MaxLevel = 20, RespawnTime = 30, RespawnChance = 100
        };

        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }
}
