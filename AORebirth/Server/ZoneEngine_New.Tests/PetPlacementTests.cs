namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using AORebirth.Core.GameData;
using AORebirth.World.Collision;
using AORebirth.World.Pathfinding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Ai;
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
        using var scene = new Scene([Floor(0)]);
        Vector3 wanted = PetFormation.PointFor(scene.Owner, 0);

        Assert.IsNull(scene.Owner.Playfield!.Pathfinder);
        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out Vector3 at, out int index));
        Assert.AreEqual(0, index);
        Assert.AreEqual(wanted.x, at.x, 0.001);
        Assert.AreEqual(0, at.y, 0.001);
        Assert.AreEqual(wanted.z, at.z, 0.001);
    }

    [TestMethod]
    public void CollisionOnlyPlacementUsesAnOppositeFormationPointWhenRearSlotsHaveNoFloor()
    {
        using var scene = new Scene([Floor(0, minZ: 15.9f)]);
        Assert.IsNull(scene.Owner.Playfield!.Pathfinder);
        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out Vector3 at, out int index));
        Assert.IsTrue(at.z > scene.Owner.Position.z);

        Vector3 followPoint = PetFormation.PointFor(scene.Owner, index);
        Assert.AreEqual(followPoint.x, at.x, 0.001);
        Assert.AreEqual(followPoint.z, at.z, 0.001);
    }

    [TestMethod]
    public void OppositeFormationSlotsUseDistinctAttackOffsets()
    {
        using var scene = new Scene([Floor(0)]);
        NpcCharacter pet = scene.AddPet(physicalSlot: 0, reservedSlot: 0);
        Vector3 rear = PetFormation.AttackPoint(pet, scene.Owner, 0, 1);
        Vector3 opposite = PetFormation.AttackPoint(pet, scene.Owner, PetFormation.OppositeSlot(0), 1);

        Assert.IsTrue(Vector3.Abs(rear - opposite) > 0.001);
    }

    [TestMethod]
    public void CollisionOnlyPlacementRejectsInsufficientHeadroom()
    {
        using var scene = new Scene([Floor(0), Floor(0.75f)]);

        Assert.IsTrue(scene.World.TrySnapToFloor(scene.Owner.Position, out _));
        Assert.IsFalse(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out _));
    }

    [TestMethod]
    public void CollisionOnlyPlacementSkipsBothReservedAndPhysicallyOccupiedPoints()
    {
        using var scene = new Scene([Floor(0)]);
        NpcCharacter pet = scene.AddPet(physicalSlot: 0, reservedSlot: 1);

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out int index));
        Assert.AreEqual(2, index);
        Assert.AreEqual(1, scene.Owner.OwnedPets.FormationIndex(pet));

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, pet, out _, out int replacementIndex));
        Assert.AreEqual(0, replacementIndex);
    }

    [TestMethod]
    public void NavmeshPlacementUsesAValidSnappedPoint()
    {
        using var scene = new Scene([Floor(0)], navigationFloor: Floor(0, minX: 14.5f));
        Vector3 wanted = PetFormation.PointFor(scene.Owner, 0);
        Assert.IsTrue(scene.Finder!.TrySnap(ToNumerics(wanted), PetAi.FollowSnapMeters, out NumVector3 snapped));
        Assert.IsTrue(snapped.X > wanted.x + 0.1, "The bake must move the first candidate horizontally.");

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out Vector3 at, out int index));
        Assert.AreEqual(0, index);
        Assert.AreEqual(snapped.X, at.x, 0.001);
        Assert.AreEqual(0, at.y, 0.001);
        Assert.AreEqual(snapped.Z, at.z, 0.001);
    }

    [TestMethod]
    public void LoadedNavmeshWithoutNearbyPolygonsFallsBackToCollisionFloor()
    {
        using var scene = new Scene([Floor(0)], navigationFloor: Floor(0, 80, 112, 80, 112));
        Vector3 wanted = PetFormation.PointFor(scene.Owner, 0);
        Assert.IsFalse(scene.Finder!.TrySnap(ToNumerics(wanted), PetAi.FollowSnapMeters, out _));

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out Vector3 at, out int index));
        Assert.AreEqual(0, index);
        Assert.AreEqual(wanted.x, at.x, 0.001);
        Assert.AreEqual(0, at.y, 0.001);
        Assert.AreEqual(wanted.z, at.z, 0.001);
    }

    [TestMethod]
    public void NavmeshPlacementRejectsOffCenterObstructionInLiveCollision()
    {
        // The live scene has a low ledge intersecting the edge of slot zero, absent from the bake.
        using var scene = new Scene([Floor(0), Floor(0.75f, 14.6f, 14.8f, 14, 15)], navigationFloor: Floor(0));
        Vector3 wanted = PetFormation.PointFor(scene.Owner, 0);
        Assert.IsTrue(scene.Finder!.TrySnap(ToNumerics(wanted), PetAi.FollowSnapMeters, out NumVector3 snapped));
        Assert.AreEqual(wanted.x, snapped.X, 0.001);
        Assert.AreEqual(wanted.z, snapped.Z, 0.001);
        Assert.IsTrue(scene.World.HasLineOfSight(new Vector3(wanted.x, 0.05, wanted.z), new Vector3(wanted.x, 1.8, wanted.z)));

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out int index));
        Assert.AreEqual(1, index);
    }

    [TestMethod]
    public void PetDeathReleasesItsFormationPoint()
    {
        using var scene = new Scene([Floor(0)]);
        NpcCharacter pet = scene.AddPet(physicalSlot: 0, reservedSlot: 0);

        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out int occupied));
        Assert.AreEqual(1, occupied);

        scene.Service.OnPetDied(pet);

        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        Assert.IsTrue(scene.Service.TryFindSpawnPosition(scene.Owner, null, out _, out int released));
        Assert.AreEqual(0, released);
    }

    static NumVector3 ToNumerics(Vector3 point) => new(point.xf, point.yf, point.zf);

    static CollisionTriangleMesh Floor(float y, float minX = 0, float maxX = 32, float minZ = 0, float maxZ = 32)
        => new(
            [new NumVector3(minX, y, minZ), new NumVector3(maxX, y, minZ),
                new NumVector3(minX, y, maxZ), new NumVector3(maxX, y, maxZ)],
            [new CollisionTriangle(0, 2, 1), new CollisionTriangle(1, 2, 3)]);

    sealed class Scene : IDisposable
    {
        readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aorebirth-pet-placement-");
        readonly ServiceProvider _services;
        public PlayfieldWorldSimulation World { get; }
        public NavMeshPathfinder? Finder { get; }
        public Player Owner { get; }
        public PetService Service { get; }

        public Scene(CollisionTriangleMesh[] surfaces, CollisionTriangleMesh? navigationFloor = null)
        {
            var logger = new StubLogger();
            string gameDataRoot = Directory.CreateDirectory(Path.Combine(_root.FullName, "GameData")).FullName;
            var data = new GameDataStore(logger, null, gameDataRoot);
            var geometry = new PlayfieldGeometryData { Collision = new PlayfieldCollisionSet(PlayfieldId, surfaces, null) };
            World = PlayfieldWorldSimulation.Create(PlayfieldId, geometry, null, new DestinationsCatalog(), data, logger);
            _services = new ServiceCollection()
                .AddSingleton(new WorldSimulationAccess { Instance = World })
                .BuildServiceProvider();

            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, _services);
            if (navigationFloor != null)
            {
                string config = Directory.CreateDirectory(Path.Combine(_root.FullName, "Config")).FullName;
                File.WriteAllText(Path.Combine(config, NavMeshBuildSettings.ConfigFileName), "{}");
                NavMeshBakeResult mesh = NavMeshBaker.Bake(new PlayfieldCollisionSet(PlayfieldId, [navigationFloor], null));
                NavMeshFile.Write(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(PlayfieldId)), mesh);
                Assert.IsTrue(NavMeshPathfinder.TryLoad(gameDataRoot, PlayfieldId, out NavMeshPathfinder? finder, out string? failure), failure);
                Finder = finder;
                typeof(Playfield).GetField("<Pathfinder>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(playfield, finder);
            }
            Owner = TestWorld.CreatePlayer(1);
            Owner.Position = new Vector3(16, 0, 16);
            Owner.Rotation = new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1);
            Owner.Playfield = playfield;
            Service = new PetService(playfield, data, logger);
        }

        public NpcCharacter AddPet(int physicalSlot, int reservedSlot)
        {
            var pet = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 2 }, new StubItemBuilder())
            {
                Playfield = Owner.Playfield,
                Position = PetFormation.PointFor(Owner, physicalSlot)
            };
            pet.Stats.Set(CharacterStat.MaxHealth, 100);
            pet.Stats.Set(CharacterStat.Health, 100);
            pet.BindPet(new PetController(Owner, PetTypes.Attack, "TEST", 1, null));
            Owner.OwnedPets.Add(pet, reservedSlot);
            return pet;
        }

        public void Dispose()
        {
            Finder?.Dispose();
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
