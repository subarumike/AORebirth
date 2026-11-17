namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

using AORebirth.Enums;
using AORebirth.World.Collision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Pets;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;

using NumVector3 = System.Numerics.Vector3;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class NpcPetSummonsTests
{
    const int CreationTemplateId = 910002;
    const string OwnerHash = "TEST";
    static readonly string[] PetHashes = ["PETA", "PETB", "PETC", "PETD", "PETE"];
    static readonly DateTime FightStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void StableItemBindingIgnoresDisplayNameAndRejectsUnboundEquipment()
    {
        using var valid = new Scene();
        MonsterCombatRulesCatalog.For(valid.Data.RootPath).ValidateEncounterReferences(valid.Catalog, valid.Data);
        Assert.IsNotNull(NpcPetSummons.TryCreate(valid.Owner, valid.Data));

        using var unbound = new Scene(CreationTemplateId + 1, "NPC Pet Creation Item");
        var error = Assert.ThrowsExactly<InvalidDataException>(() => NpcPetSummons.TryCreate(unbound.Owner, unbound.Data));
        StringAssert.Contains(error.Message, "no bound creation item");
    }

    [TestMethod]
    public void LoaderChecksEverySummonOperationAndRejectsUnknownTargets()
    {
        using var scene = new Scene();
        List<ItemSpell> operations = scene.Template.SpellList[EventType.OnUse];
        operations.Add(Summon("NONE", Require(CharacterStat.LastRnd, Operator.GreaterThan, 100)));

        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MonsterCombatRulesCatalog.For(scene.Data.RootPath).ValidateEncounterReferences(scene.Catalog, scene.Data));
        StringAssert.Contains(error.Message, "NONE");
    }

    [TestMethod]
    public void ConditionalSelectionRespectsOneOfEachTypeAndFourLivingPets()
    {
        using var scene = new Scene();
        NpcPetSummons summons = scene.AttachBrain();

        summons.Tick(true, FightStart);
        summons.Tick(true, FightStart.AddSeconds(1));
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);

        summons.Tick(true, FightStart.AddSeconds(2));
        summons.Tick(true, FightStart.AddSeconds(4));
        Assert.AreEqual(1, scene.Owner.OwnedPets.Count);
        Assert.AreEqual(PetHashes[0], scene.Owner.OwnedPets.All[0].SpawnHash);

        for (int choice = 2; choice <= PetHashes.Length; choice++)
        {
            scene.Owner.Stats.Set(CharacterStat.Level, choice);
            summons.Tick(true, FightStart.AddSeconds(2 + 2 * choice));
        }

        CollectionAssert.AreEquivalent(PetHashes.Take(4).ToArray(),
            scene.Owner.OwnedPets.All.Select(pet => pet.SpawnHash).ToArray());
        foreach (NpcCharacter pet in scene.Owner.OwnedPets.All)
        {
            Assert.AreSame(scene.Owner, pet.Pet!.Owner);
            Assert.AreEqual(scene.Owner.Identity.Instance, pet.Stats.GetOrZero(CharacterStat.PetMaster));
            Assert.IsTrue(scene.Registry.TryGet(pet.Identity, out Dynel? registered));
            Assert.AreSame(pet, registered);
        }
    }

    [TestMethod]
    public void DeathReplacementUsesItsDelayAndTheVacatedFormationPosition()
    {
        using var scene = new Scene(maxPets: 1);
        NpcPetSummons summons = scene.AttachBrain();
        summons.Tick(true, FightStart);
        summons.Tick(true, FightStart.AddSeconds(2));
        NpcCharacter first = scene.Owner.OwnedPets.All.Single();
        int formationIndex = scene.Owner.OwnedPets.FormationIndex(first);

        first.Stats.Set(CharacterStat.Health, 0);
        scene.Spawns.DespawnNpc(first);
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        Assert.IsFalse(scene.Registry.TryGet(first.Identity, out _));

        summons.Tick(true, FightStart.AddSeconds(3));
        summons.Tick(true, FightStart.AddSeconds(9));
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        summons.Tick(true, FightStart.AddSeconds(10));
        NpcCharacter replacement = scene.Owner.OwnedPets.All.Single();
        Assert.AreEqual(first.SpawnHash, replacement.SpawnHash);
        Assert.AreNotEqual(first.Identity, replacement.Identity);
        Assert.AreEqual(formationIndex, scene.Owner.OwnedPets.FormationIndex(replacement));
    }

    [TestMethod]
    public void ImmediateReaggroAfterResetStartsFreshAndOwnerDeathDismissesPets()
    {
        using var scene = new Scene(maxPets: 1);
        NpcPetSummons summons = scene.AttachBrain();
        summons.Tick(true, FightStart);
        summons.Tick(true, FightStart.AddSeconds(2));
        NpcCharacter first = scene.Owner.OwnedPets.All.Single();

        scene.Owner.OnReset();
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        Assert.IsFalse(scene.Registry.TryGet(first.Identity, out _));

        summons.Tick(true, FightStart.AddSeconds(3));
        summons.Tick(true, FightStart.AddSeconds(4));
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        summons.Tick(true, FightStart.AddSeconds(5));
        NpcCharacter next = scene.Owner.OwnedPets.All.Single();

        scene.Owner.Stats.Set(CharacterStat.Health, 0);
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        Assert.IsFalse(scene.Registry.TryGet(next.Identity, out _));
        Assert.IsNull(next.Playfield);
    }

    static ItemRequirement Require(CharacterStat stat, Operator comparison, int value)
        => new() { StatNumber = (int)stat, Operator = (int)comparison, Value = value };

    static ItemSpell Summon(string hash, params ItemRequirement[] requirements)
        => new()
        {
            FunctionType = (int)FunctionType.SummonPet,
            Target = (int)ItemTarget.Wearer,
            Arguments = [hash, 1, 0],
            Requirements = [.. requirements]
        };

    sealed class CreationCatalog(ItemTemplate template) : IItemTemplateCatalog
    {
        public IReadOnlyCollection<ItemTemplate> Templates => [template];

        public bool TryGet(int aoid, out ItemTemplate result)
        {
            result = aoid == template.Id ? template : null!;
            return result != null;
        }

        public ItemTemplate Require(int aoid)
            => TryGet(aoid, out ItemTemplate item) ? item : throw new KeyNotFoundException($"Missing fixture item {aoid}.");
    }

    sealed class Scene : IDisposable
    {
        const int PlayfieldId = 910003;
        readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aorebirth-npc-pets-");
        readonly ServiceProvider _services;
        readonly PlayfieldWorldSimulation _world;

        public DynelRegistry Registry { get; } = new();
        public NpcCharacter Owner { get; }
        public GameDataStore Data { get; }
        public IItemTemplateCatalog Catalog { get; }
        public ItemTemplate Template { get; }
        public SpawnService Spawns => _services.GetRequiredService<SpawnService>();

        public Scene(int equippedTemplateId = CreationTemplateId, string itemName = "Renamed fixture", int maxPets = 4)
        {
            WriteGameData(equippedTemplateId, maxPets);
            Template = new ItemTemplate { Id = equippedTemplateId, Name = itemName, Quality = 1 };
            // Rnd is always 1..100. The first entry consumes one roll but cannot select a pet.
            // Every later entry can pass only if it sees that same event's roll and the fixture's active level.
            var operations = new List<ItemSpell> { Summon(PetHashes[^1], Require(CharacterStat.Rnd, Operator.LessThan, 1)) };
            for (int i = 0; i < PetHashes.Length; i++)
                operations.Add(Summon(PetHashes[i], Require(CharacterStat.LastRnd, Operator.GreaterThan, 0),
                    Require(CharacterStat.Level, Operator.EqualTo, i + 1)));
            Template.SpellList.Add(EventType.OnUse, operations);
            Catalog = new CreationCatalog(Template);

            var logger = new StubLogger();
            var itemBuilder = new StubItemBuilder().Add(Template);
            Data = new GameDataStore(logger, null, _root.FullName);
            var playfield = Blank<Playfield>();
            Field(playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = PlayfieldId });
            Field(playfield, "<ResourceId>k__BackingField", PlayfieldId);
            Field(playfield, "_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            Field(playfield, "_lifecycleSync", new Lock());
            var locality = new PlayfieldLocality(PlayfieldId, null);
            var floor = new CollisionTriangleMesh(
                [new NumVector3(0, 0, 0), new NumVector3(32, 0, 0), new NumVector3(0, 0, 32), new NumVector3(32, 0, 32)],
                [new CollisionTriangle(0, 2, 1), new CollisionTriangle(1, 2, 3)]);
            var geometry = new PlayfieldGeometryData { Collision = new PlayfieldCollisionSet(PlayfieldId, [floor], null) };
            _world = PlayfieldWorldSimulation.Create(PlayfieldId, geometry, null, new DestinationsCatalog(), Data, logger);
            var minter = new HashItemMinter(Data, Catalog, itemBuilder);
            var flush = Blank<InventoryFlushService>();
            var moves = new InventoryMoveService(logger, flush, Blank<InventoryActionService>());
            _services = new ServiceCollection().AddSingleton<IGameData>(Data).AddSingleton(Registry).AddSingleton(locality)
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(new WorldSimulationAccess { Instance = _world })
                .AddSingleton(moves)
                .AddSingleton(new PetService(playfield, Data, logger))
                .AddSingleton(new NpcContentActivationService(playfield, Registry, locality, itemBuilder, Catalog, Data))
                .AddSingleton<SpawnService>(services => new SpawnService(services, Registry, logger, playfield,
                    Blank<PlayfieldManager>(), Data, itemBuilder, minter, flush,
                    Blank<TradeService>(), Blank<CharacterSnapshotService>()))
                .BuildServiceProvider();
            Field(playfield, "_serviceProvider", _services);
            Assert.IsTrue(Data.TryResolveMobTemplate(OwnerHash, 1, out MobTemplate template));
            Owner = Spawns.SpawnMob(template, new Vector3(16, 0, 16), spawnHash: OwnerHash, attachDefaultBrain: false);
        }

        public NpcPetSummons AttachBrain()
        {
            NpcBrain brain = NpcBrain.Create(Owner, Owner.Position);
            return (NpcPetSummons)typeof(NpcBrain).GetField("_petSummons", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(brain)!;
        }

        void WriteGameData(int equippedTemplateId, int maxPets)
        {
            var policy = new MonsterPetRule
            {
                MaxPets = maxPets, MaxPetsPerType = 1, SummonIntervalSeconds = 2, RespawnDelaySeconds = 7, RetryDelaySeconds = 3
            };
            File.WriteAllText(Path.Combine(_root.FullName, MonsterCombatRulesCatalog.PetSummonsFileName),
                JsonSerializer.Serialize(new NpcPetSummonProfile { Version = 1, CreationItemTemplateIds = [CreationTemplateId], Policy = policy }));
            File.WriteAllText(Path.Combine(_root.FullName, MonsterCombatRulesCatalog.FileName),
                JsonSerializer.Serialize(new Dictionary<string, MonsterCombatRules> { [OwnerHash] = new() { Pets = policy } }));
            var templates = PetHashes.ToDictionary(hash => hash, hash => new
            {
                Templates = new[] { new { Name = hash, Level = 1, Stats = FixtureStats(), Equipment = Array.Empty<int[]>() } }
            });
            templates[OwnerHash] = new
            {
                Templates = new[] { new { Name = OwnerHash, Level = 1, Stats = FixtureStats(), Equipment = new[] { new[] { equippedTemplateId, equippedTemplateId } } } }
            };
            File.WriteAllText(Path.Combine(_root.FullName, "NpcTemplates.json"), JsonSerializer.Serialize(templates));
        }

        static Dictionary<int, int> FixtureStats() => new()
        {
            [(int)CharacterStat.Level] = 1,
            [(int)CharacterStat.MaxHealth] = 100,
            [(int)CharacterStat.Health] = 100
        };

        static T Blank<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        static void Field<T>(Playfield playfield, string name, T value)
            => typeof(Playfield).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, value);

        public void Dispose()
        {
            _services.Dispose();
            _world.Dispose();
            _root.Delete(recursive: true);
        }
    }
}
