namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using AORebirth.World.Collision;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Pets;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.WorldSimulation;

using NumVector3 = System.Numerics.Vector3;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class PetPlacementTests
{
    const int PlayfieldId = 910001;

    [TestMethod]
    public void CollisionOnlyPlayfieldPlacesPetAtTheFirstFormationPoint()
    {
        using var scene = new Scene(Floor(0));
        Vector3 wanted = PetFormation.PointFor(scene.Owner, 0);

        Assert.IsNull(scene.Owner.Playfield!.Pathfinder);
        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out Vector3 at, out int index));
        Assert.AreEqual(0, index);
        Assert.AreEqual(wanted.x, at.x, 0.001);
        Assert.AreEqual(0, at.y, 0.001);
        Assert.AreEqual(wanted.z, at.z, 0.001);
    }

    [TestMethod]
    public void CollisionOnlyPlacementRejectsInsufficientHeadroom()
    {
        using var scene = new Scene(Floor(0), Floor(0.75f));

        Assert.IsTrue(scene.World.TrySnapToFloor(scene.Owner.Position, out _));
        Assert.IsFalse(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out _));
    }

    [TestMethod]
    public void CollisionOnlyPlacementSkipsBothReservedAndPhysicallyOccupiedPoints()
    {
        using var scene = new Scene(Floor(0));
        var pet = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 2 }, new StubItemBuilder())
        {
            Playfield = scene.Owner.Playfield,
            Position = PetFormation.PointFor(scene.Owner, 0)
        };
        pet.Stats.Set(CharacterStat.MaxHealth, 100);
        pet.Stats.Set(CharacterStat.Health, 100);
        scene.Owner.OwnedPets.Add(pet, formationIndex: 1);

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out int index));
        Assert.AreEqual(2, index);
        Assert.AreEqual(1, scene.Owner.OwnedPets.FormationIndex(pet));

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, pet, out _, out int replacementIndex));
        Assert.AreEqual(0, replacementIndex);
    }

    static CollisionTriangleMesh Floor(float y)
        => new(
            [new NumVector3(0, y, 0), new NumVector3(32, y, 0),
                new NumVector3(0, y, 32), new NumVector3(32, y, 32)],
            [new CollisionTriangle(0, 2, 1), new CollisionTriangle(1, 2, 3)]);

    sealed class Scene : IDisposable
    {
        readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aorebirth-pet-placement-");
        readonly ServiceProvider _services;
        public PlayfieldWorldSimulation World { get; }
        public Player Owner { get; }
        public PetService Service { get; }

        public Scene(params CollisionTriangleMesh[] surfaces)
        {
            var logger = new StubLogger();
            var data = new GameDataStore(logger, null, _root.FullName);
            var geometry = new PlayfieldGeometryData { Collision = new PlayfieldCollisionSet(PlayfieldId, surfaces, null) };
            World = PlayfieldWorldSimulation.Create(PlayfieldId, geometry, null, new DestinationsCatalog(), data, logger);
            _services = new ServiceCollection()
                .AddSingleton(new WorldSimulationAccess { Instance = World })
                .BuildServiceProvider();

            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, _services);
            Owner = TestWorld.CreatePlayer(1);
            Owner.Position = new Vector3(16, 0, 16);
            Owner.Playfield = playfield;
            Service = new PetService(playfield, data, logger);
        }

        public void Dispose()
        {
            _services.Dispose();
            World.Dispose();
            _root.Delete(recursive: true);
        }
    }
}

[TestClass]
public sealed class MonsterCombatRulePolicyTests
{
    [TestMethod]
    [DataRow("""{ "NanoId": 1, "HealthBelowPercent": 50, "ChancePercent": 100, "CooldownSeconds": 94 }""")]
    [DataRow("""{ "NanoId": 1, "HealthBelowPercent": 50, "ChancePercent": 100, "MaxCastsPerFight": null }""")]
    public void TriggeredNanoRejectsAnOmittedCastLimitOrCooldown(string trigger)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-combat-rule-");
        try
        {
            WriteTrigger(root, trigger);
            Assert.IsFalse(MonsterCombatRulesCatalog.Reload(root.FullName).Ok);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void TriggeredNanoAcceptsAnExplicitUnlimitedCastLimitAndCooldown()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-combat-rule-");
        try
        {
            WriteTrigger(root, """
                { "NanoId": 1, "HealthBelowPercent": 50, "ChancePercent": 100, "MaxCastsPerFight": null, "CooldownSeconds": 94 }
                """);
            Assert.IsTrue(MonsterCombatRulesCatalog.Reload(root.FullName).Ok);
            Assert.IsTrue(MonsterCombatRulesCatalog.For(root.FullName).TryGet("TEST", out MonsterCombatRules rules));
            Assert.IsNotNull(rules.TriggeredSelfNanos);
            Assert.IsNull(rules.TriggeredSelfNanos[0].MaxCastsPerFight);
            Assert.AreEqual(94, rules.TriggeredSelfNanos[0].CooldownSeconds);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    static void WriteTrigger(DirectoryInfo root, string trigger)
        => File.WriteAllText(Path.Combine(root.FullName, MonsterCombatRulesCatalog.FileName),
            "{ \"TEST\": { \"TriggeredSelfNanos\": [" + trigger + "] } }");
}
