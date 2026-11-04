namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Pets;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Teams;

[TestClass]
public sealed class ChestCombatTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OwnCombatRejectsNewOpenBeforeRefreshingLoot(bool incoming)
    {
        using var fixture = new Fixture();
        fixture.BeginCombat(incoming);
        Assert.IsTrue(fixture.Player.IsInCombat);
        if (incoming)
            Assert.AreEqual(Identity.None, fixture.Player.FightingTarget);

        Assert.IsFalse(fixture.Chest.TryUse(fixture.Player));
        Assert.IsFalse(fixture.Chest.IsOpen);
        Assert.AreEqual(0, fixture.Refreshes);
        Assert.AreEqual(0, fixture.World.Persistence.Calls);

        fixture.EndCombat(incoming);
        Assert.IsFalse(fixture.Player.IsInCombat);
        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        Assert.AreEqual(1, fixture.Refreshes);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OwnCombatRejectsActualChestTransferUntilCombatEnds(bool incoming)
    {
        using var fixture = new Fixture();
        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        var originalRow = fixture.World.Persistence.Rows[fixture.Item.InstanceId];
        fixture.BeginCombat(incoming);
        fixture.World.Session.Messages.Clear();

        fixture.TakeItem();

        Assert.AreEqual(0, fixture.World.Persistence.Calls);
        Assert.AreSame(fixture.Item, fixture.Chest.Loot.Content[0]);
        Assert.IsFalse(fixture.Player.Inventory.Inventory.Content.ContainsKey(64));
        Assert.AreSame(originalRow, fixture.World.Persistence.Rows[fixture.Item.InstanceId]);
        Assert.IsFalse(fixture.World.Session.Messages.OfType<ContainerAddItemMessage>().Any());

        fixture.EndCombat(incoming);
        fixture.AssertSuccessfulTransfer();
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OwnCombatRejectsChestDeletionUntilCombatEnds(bool incoming)
    {
        using var fixture = new Fixture();
        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        var originalRow = fixture.World.Persistence.Rows[fixture.Item.InstanceId];
        var request = new CharacterActionMessage
        {
            Identity = fixture.Player.Identity,
            Action = CharacterActionType.DeleteItem,
            Target = new Identity { Type = IdentityType.Backpack, Instance = fixture.Chest.InventoryHandle << 16 },
            Parameter1 = fixture.Item.LowId,
            Parameter2 = fixture.Item.HighId
        };
        fixture.BeginCombat(incoming);
        fixture.World.Session.Messages.Clear();

        Assert.IsFalse(fixture.World.Actions.TryDelete(fixture.Player, request.Target, fixture.Item.InstanceId));
        fixture.World.Actions.Handle(fixture.Player, request);

        Assert.AreEqual(0, fixture.World.Persistence.Calls);
        Assert.AreSame(fixture.Item, fixture.Chest.Loot.Content[0]);
        Assert.AreEqual(3, fixture.Item.StackCount);
        Assert.AreEqual(0, fixture.Player.Inventory.Inventory.Content.Count);
        Assert.AreSame(originalRow, fixture.World.Persistence.Rows[fixture.Item.InstanceId]);
        Assert.IsFalse(fixture.World.Session.Messages.OfType<CharacterActionMessage>()
            .Any(message => message.Action == CharacterActionType.DeleteItem));

        fixture.EndCombat(incoming);
        fixture.World.Persistence.BeforeCommit = _ =>
        {
            Assert.AreSame(fixture.Item, fixture.Chest.Loot.Content[0]);
            Assert.IsFalse(fixture.World.Session.Messages.OfType<CharacterActionMessage>()
                .Any(message => message.Action == CharacterActionType.DeleteItem));
        };
        fixture.World.Actions.Handle(fixture.Player, request);

        Assert.AreEqual(1, fixture.World.Persistence.Calls);
        Assert.IsFalse(fixture.Chest.Loot.Content.ContainsKey(0));
        Assert.AreEqual(0, fixture.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual((int)IdentityType.None, fixture.World.Persistence.Rows[fixture.Item.InstanceId].ContainerType);
        Assert.AreEqual(fixture.Item.InstanceId, fixture.World.Persistence.Rows[fixture.Item.InstanceId].ContainerPlacement);
        CharacterActionMessage acknowledgement = fixture.World.Session.Messages.OfType<CharacterActionMessage>()
            .Single(message => message.Action == CharacterActionType.DeleteItem);
        Assert.AreEqual(request.Target, acknowledgement.Target);
        Assert.AreEqual(0, acknowledgement.Parameter1);
        Assert.AreEqual(0, acknowledgement.Parameter2);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OwnCombatStillPermitsClosingAnOpenChest(bool incoming)
    {
        using var fixture = new Fixture();
        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        fixture.BeginCombat(incoming);

        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        Assert.IsFalse(fixture.Chest.IsOpen);
        Assert.AreEqual(1, fixture.Refreshes);
        Assert.AreEqual(0, fixture.World.Persistence.Calls);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TeammateOrPetCombatAlonePermitsOpeningAndActualTransfer(bool pet)
    {
        using var fixture = new Fixture();
        if (pet)
        {
            NpcCharacter ownedPet = fixture.Npc(445);
            ownedPet.BindPet(new PetController(fixture.Player, 1, "synthetic-pet", 60, null));
            fixture.Player.OwnedPets.Add(ownedPet);
            ownedPet.SetFightingTarget(fixture.Opponent.Identity);
        }
        else
        {
            Player teammate = fixture.Teammate();
            teammate.SetFightingTarget(fixture.Opponent.Identity);
        }

        Assert.IsFalse(fixture.Player.IsInCombat);
        Assert.IsTrue(fixture.Player.IsAlliedInCombat());
        Assert.IsTrue(fixture.Chest.TryUse(fixture.Player));
        fixture.AssertSuccessfulTransfer();
    }

    [TestMethod]
    public void OwnCombatDoesNotChangeCorpseOpeningRules()
    {
        using var fixture = new Fixture();
        NpcCharacter dead = fixture.Npc(446);
        var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 447 }, dead,
            new StubGameData(HashItemCatalog.Parse("{}")));
        fixture.Registry.Register(corpse);
        fixture.BeginCombat(incoming: false);

        Assert.IsTrue(fixture.Player.IsInCombat);
        Assert.IsTrue(corpse.TryUse(fixture.Player));
        Assert.IsTrue(corpse.IsOpen);
    }

    sealed class Fixture : IDisposable
    {
        public readonly InventoryActionTests.World World = new();
        public readonly DynelRegistry Registry = new();
        readonly TeamService teams = new(dispatchOnOwner: (_, action) => action());
        readonly ServiceProvider services;
        public Player Player => World.Player;
        public readonly Chest Chest;
        public readonly Item Item;
        public readonly NpcCharacter Opponent;
        public int Refreshes;

        public Fixture()
        {
            services = new ServiceCollection().AddSingleton(Registry).AddSingleton(teams)
                .AddSingleton(World.Flush).AddSingleton(new PlayfieldLocality(4582, null)).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, services);
            typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, 117);
            typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, new ConcurrentDictionary<Character, byte>());
            Player.Stats.Set(CharacterStat.Level, 60);
            Registry.Register(Player);
            Opponent = Npc(444);
            Chest = new Chest(new Identity { Type = IdentityType.Container, Instance = 90001 })
            {
                Playfield = Player.Playfield,
                Position = Player.Position,
                SpawnDefinition = new ChestSpawnDefinition
                {
                    StateMachineType = 1000015, StateMachineInstance = 7,
                    Stats = new[]
                    {
                        new ChestSpawnStat { StatId = (int)CharacterStat.Flags, Value = 0x20001841 },
                        new ChestSpawnStat { StatId = (int)CharacterStat.StaticInstance, Value = 162447 },
                        new ChestSpawnStat { StatId = (int)CharacterStat.ACGItemLevel, Value = 1 },
                        new ChestSpawnStat { StatId = (int)CharacterStat.ACGItemTemplateID, Value = 162447 },
                        new ChestSpawnStat { StatId = (int)CharacterStat.ACGItemTemplateID2, Value = 162447 },
                        new ChestSpawnStat { StatId = (int)CharacterStat.MultipleCount, Value = 1 }
                    }
                },
                RefreshLoot = _ => { Refreshes++; return true; }
            };
            Registry.Register(Chest);
            Item = TestWorld.CreateItem(instanceId: 902, stackCount: 3);
            Assert.IsTrue(Chest.Loot.Add(0, Item));
            World.Persistence.Rows[Item.InstanceId] = Item.ToRecord(Chest.Loot.Identity, 0, Item.StackCount);
        }

        public NpcCharacter Npc(int id)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = id }, new StubItemBuilder())
                { Playfield = Player.Playfield, Position = Player.Position };
            Registry.Register(npc);
            return npc;
        }

        public Player Teammate()
        {
            teams.AttachPlayer(Player);
            Player teammate = TestWorld.CreatePlayer(222);
            teammate.Playfield = Player.Playfield;
            teammate.Position = Player.Position;
            teammate.Stats.Set(CharacterStat.Level, 60);
            var session = new InventoryActionTests.Session();
            session.BindPlayer(teammate);
            teammate.Session = session;
            Registry.Register(teammate);
            teams.AttachPlayer(teammate);
            teams.TryHandle(Player, new CharacterActionMessage
                { Identity = Player.Identity, Action = CharacterActionType.TeamRequestInvite, Target = teammate.Identity });
            teams.TryHandle(teammate, new CharacterActionMessage
            {
                Identity = teammate.Identity, Action = CharacterActionType.TeamRequestReply,
                Target = Player.Identity, Parameter2 = 1
            });
            Assert.IsTrue(teams.AreTeammates(Player, teammate));
            return teammate;
        }

        public void BeginCombat(bool incoming)
        {
            if (incoming)
                Opponent.SetFightingTarget(Player.Identity);
            else
                Player.SetFightingTarget(Opponent.Identity);
        }

        public void EndCombat(bool incoming)
        {
            if (incoming)
                Opponent.SetFightingTarget(Identity.None);
            else
                Player.SetFightingTarget(Identity.None);
        }

        public void TakeItem() => new InventoryMoveService(new StubLogger(), World.Flush, World.Actions).Handle(Player,
            new ClientMoveItemToInventoryMessage
            {
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = Chest.InventoryHandle << 16 },
                TargetPlacement = 64
            });

        public void AssertSuccessfulTransfer()
        {
            World.Session.Messages.Clear();
            World.Persistence.BeforeCommit = _ =>
            {
                Assert.AreSame(Item, Chest.Loot.Content[0]);
                Assert.IsFalse(World.Session.Messages.OfType<ContainerAddItemMessage>().Any());
            };
            TakeItem();
            Assert.AreEqual(1, World.Persistence.Calls);
            Assert.IsFalse(Chest.Loot.Content.ContainsKey(0));
            Assert.AreSame(Item, Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(64, World.Persistence.Rows[Item.InstanceId].ContainerPlacement);
            Assert.AreEqual(Player.Identity.Instance, World.Persistence.Rows[Item.InstanceId].ContainerInstance);
            Assert.AreEqual(3, World.Persistence.Rows[Item.InstanceId].StackCount);
            Assert.AreEqual(64, World.Session.Messages.OfType<ContainerAddItemMessage>().Single().TargetPlacement);
        }

        public void Dispose()
        {
            services.Dispose();
            World.Dispose();
        }
    }
}
