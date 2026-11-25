namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Pets;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class EngineerPetGuardTests
{
    const string Predator = "PDM3";
    const string Mindreaver = "DEMD";
    const string Yidira = "URTX";

    [TestMethod]
    public void DogGuardsItsOwnerAndReturnsToGuardAfterAttack()
    {
        using var scene = new Scene();
        NpcCharacter dog = scene.AddPet(Predator);
        NpcCharacter hostile = scene.AddNpc(3);
        hostile.SetFightingTarget(scene.Owner.Identity);

        scene.Command(PetCommandCode.Follow, dog);
        Assert.AreEqual(PetMode.Follow, dog.Pet!.Mode);
        Assert.IsFalse(PetAi.PickEnemy(dog.Brain!));

        scene.Command(PetCommandCode.Guard, dog);
        Assert.AreEqual(PetMode.Guard, dog.Pet.Mode);
        Assert.IsTrue(PetAi.PickEnemy(dog.Brain!));

        scene.Owner.SetTarget(hostile.Identity);
        scene.Command(PetCommandCode.Attack, dog);
        Assert.AreEqual(PetMode.Attack, dog.Pet.Mode);
        Assert.AreEqual(hostile.Identity, dog.Pet.AttackTarget);
        hostile.SetFightingTarget(Identity.None);
        scene.Registry.Unregister(hostile.Identity);
        Assert.IsFalse(PetAi.PickEnemy(dog.Brain!));
        Assert.AreEqual(PetMode.Guard, dog.Pet.Mode);
    }

    [TestMethod]
    [DataRow(Mindreaver)]
    [DataRow(Yidira)]
    public void MezzPetStaysPassiveOnGuardAndObeysExplicitAttack(string hash)
    {
        using var scene = new Scene();
        NpcCharacter pet = scene.AddPet(hash);
        NpcCharacter hostile = scene.AddNpc(3);
        hostile.SetFightingTarget(scene.Owner.Identity);

        scene.Command(PetCommandCode.Guard, pet);
        Assert.AreEqual(PetMode.Follow, pet.Pet!.Mode);
        Assert.IsFalse(PetAi.PickEnemy(pet.Brain!));

        scene.Owner.SetTarget(hostile.Identity);
        scene.Command(PetCommandCode.Attack, pet);
        Assert.AreEqual(hostile.Identity, pet.Pet.AttackTarget);
        Assert.IsTrue(PetAi.PickEnemy(pet.Brain!));
        hostile.SetFightingTarget(Identity.None);
        scene.Registry.Unregister(hostile.Identity);
        Assert.IsFalse(PetAi.PickEnemy(pet.Brain!));
        Assert.AreEqual(PetMode.Follow, pet.Pet.Mode);
    }

    [TestMethod]
    public void CommandsRetainSeparateModesAndRejectAnotherOwnersPet()
    {
        using var scene = new Scene();
        NpcCharacter dog = scene.AddPet(Predator);
        NpcCharacter healer = scene.AddPet("HEAL");
        NpcCharacter attacker = scene.AddPet("ATCK");
        NpcCharacter hostile = scene.AddNpc(3);
        Player ally = scene.AddPlayer(4);
        NpcCharacter foreignPet = scene.AddPet(Mindreaver, ally);

        scene.Command(PetCommandCode.Follow, dog, healer, attacker);
        scene.Service.Command(ally, PetCommandCode.Wait, [foreignPet.Identity]);
        scene.Service.Command(scene.Owner, PetCommandCode.Guard, [foreignPet.Identity]);
        Assert.AreEqual(PetMode.Wait, foreignPet.Pet!.Mode, "A command cannot affect an unrelated owner's pet.");

        scene.Owner.SetTarget(ally.Identity);
        scene.Command(PetCommandCode.Heal, healer);
        Assert.AreEqual(ally.Identity, healer.Pet!.HealTarget);
        scene.Owner.SetTarget(hostile.Identity);
        scene.Command(PetCommandCode.Attack, attacker, healer);
        Assert.AreEqual(hostile.Identity, attacker.Pet!.AttackTarget);
        Assert.AreEqual(PetMode.Guard, healer.Pet.Mode);
        Assert.AreEqual(ally.Identity, healer.Pet.HealTarget);
        Assert.AreEqual(PetMode.Follow, dog.Pet!.Mode);

        scene.Command(PetCommandCode.Guard, healer);
        Assert.AreEqual(PetMode.Guard, healer.Pet.Mode);
        Assert.AreEqual(Identity.None, healer.Pet.HealTarget);
        scene.Command(PetCommandCode.Follow, healer);
        Assert.AreEqual(PetMode.Follow, healer.Pet.Mode);
        Assert.AreEqual(PetMode.Attack, attacker.Pet.Mode);
    }

    [TestMethod]
    public void PetDropsAnOldAttackOrderOnZoningAndKeepsGuardAcrossOwnerReclaim()
    {
        using var scene = new Scene();
        NpcCharacter dog = scene.AddPet(Predator);
        NpcCharacter hostile = scene.AddNpc(3);
        scene.Owner.SetTarget(hostile.Identity);
        scene.Command(PetCommandCode.Guard, dog);
        scene.Command(PetCommandCode.Attack, dog);
        Assert.AreEqual(PetMode.Attack, dog.Pet!.Mode);

        // The playfield transfer calls this lifecycle hook before moving the same pet to its next playfield.
        dog.Pet.LeavePlayfield();
        Assert.AreEqual(PetMode.Guard, dog.Pet.Mode);
        Assert.AreEqual(Identity.None, dog.Pet.AttackTarget);

        scene.Service.AwaitOwnerReturn(scene.Owner);
        Assert.IsTrue(dog.Pet.IsAwaitingOwner);
        Assert.AreEqual(0, scene.Owner.OwnedPets.Count);
        Player returningOwner = scene.AddPlayer(scene.Owner.Identity.Instance);
        scene.Service.ReclaimAwaitingPets(returningOwner);
        Assert.AreSame(returningOwner, dog.Pet.Owner);
        Assert.IsFalse(dog.Pet.IsAwaitingOwner);
        Assert.AreSame(dog, returningOwner.OwnedPets.InSlotOf(PetTypes.Support));
        Assert.AreEqual(PetMode.Guard, dog.Pet.Mode);
    }

    [TestMethod]
    [DataRow(null, PetMode.Follow)]
    [DataRow("[]", PetMode.Guard)]
    public void GuardDistinguishesOlderContentFromAnExplicitlyEmptyRoleList(string? mezzPets, PetMode expected)
    {
        using var scene = new Scene(mezzPets);
        NpcCharacter dog = scene.AddPet(Predator);
        scene.Command(PetCommandCode.Guard, dog);
        Assert.AreEqual(expected, dog.Pet!.Mode);
    }

    [TestMethod]
    [DataRow("\"ATCK\"")]
    [DataRow("\"MISSING\"")]
    [DataRow("\"DEMD\", \"demd\"")]
    public void CatalogRejectsInvalidMezzRoleEntries(string roleEntries)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-pet-catalog-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, PetTypeCatalog.FileName),
                "{\"Types\":{\"DEMD\":2001,\"ATCK\":1},\"MezzPets\":[" + roleEntries + "]}");
            Assert.ThrowsExactly<InvalidDataException>(() => PetTypeCatalog.For(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    sealed class Scene : IDisposable
    {
        readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aorebirth-pet-command-");
        readonly ServiceProvider _services;
        readonly Playfield _playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        readonly PetTypeCatalog _types;
        int _nextNpcId = 1_000_000;

        public Player Owner { get; }
        public PetService Service { get; }
        public DynelRegistry Registry { get; } = new();

        public Scene(string? mezzPets = """["DEMD", "URTX"]""")
        {
            const string types = """{"Types": { "PDM3": 2001, "DEMD": 2001, "URTX": 2001, "HEAL": 1001, "ATCK": 1 }""";
            string roles = mezzPets == null ? string.Empty : ",\"MezzPets\":" + mezzPets;
            File.WriteAllText(Path.Combine(_root.FullName, PetTypeCatalog.FileName), types + roles + "}");
            var data = new StubGameData(HashItemCatalog.Parse(null), rootPath: _root.FullName);
            _types = PetTypeCatalog.For(data.RootPath);
            _services = new ServiceCollection().AddSingleton(Registry).AddSingleton<IGameData>(data)
                .AddSingleton(new PlayfieldLocality(444, null)).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_playfield, _services);
            typeof(Playfield).GetField("<FightModes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_playfield, DistrictFightModes.None);
            Service = new PetService(_playfield, data, new StubLogger());
            Owner = AddPlayer(1);
        }

        public void Command(PetCommandCode command, params NpcCharacter[] pets)
            => Service.Command(Owner, command, Array.ConvertAll(pets, pet => pet.Identity));

        public Player AddPlayer(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Playfield = _playfield;
            player.Position = new Vector3(10, 0, 10);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 100);
            Registry.Register(player);
            return player;
        }

        public NpcCharacter AddNpc(int offset)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = ++_nextNpcId },
                new StubItemBuilder()) { Playfield = _playfield, Position = new Vector3(10 + offset, 0, 10) };
            npc.Stats.Set(CharacterStat.MaxHealth, 100);
            npc.Stats.Set(CharacterStat.Health, 100);
            Registry.Register(npc);
            return npc;
        }

        public NpcCharacter AddPet(string hash, Player? owner = null)
        {
            owner ??= Owner;
            NpcCharacter pet = AddNpc(owner.OwnedPets.Count);
            var controller = new PetController(owner, _types.TypeOf(hash), hash, 1, null);
            pet.BindPet(controller);
            NpcBrain.CreatePet(pet, controller);
            owner.OwnedPets.Add(pet);
            return pet;
        }

        public void Dispose()
        {
            _services.Dispose();
            _root.Delete(recursive: true);
        }
    }
}
