namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    [TestClass]
    public sealed class FightingOpponentTests
    {
        [TestMethod]
        public void OpponentCountTracksWhoIsFightingTheCharacter()
        {
            using ServiceProvider services = new ServiceCollection().AddSingleton(new DynelRegistry()).BuildServiceProvider();
            Playfield playfield = Playfield(services);
            Player player = TestWorld.CreatePlayer(18);
            NpcCharacter first = Npc(1);
            NpcCharacter second = Npc(2);
            Place(playfield, services, player, first, second);

            Assert.AreEqual(0, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.IsFalse(CombatText.MeetsRequirements(player.Stats));

            first.SetFightingTarget(player.Identity);
            first.SetFightingTarget(player.Identity);
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.IsTrue(CombatText.MeetsRequirements(player.Stats));

            second.SetFightingTarget(player.Identity);
            Assert.AreEqual(2, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));

            first.SetFightingTarget(second.Identity);
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.AreEqual(1, second.Stats.Get(CharacterStat.NumberOfFightingOpponents));

            second.SetFightingTarget(Identity.None);
            Assert.AreEqual(0, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.IsFalse(CombatText.MeetsRequirements(player.Stats));
        }

        [TestMethod]
        public void UnregisterClearsAttackersAndAllowsReengage()
        {
            using ServiceProvider services = new ServiceCollection().AddSingleton(new DynelRegistry()).BuildServiceProvider();
            Playfield playfield = Playfield(services);
            DynelRegistry registry = services.GetRequiredService<DynelRegistry>();
            Player player = TestWorld.CreatePlayer(18);
            NpcCharacter npc = Npc(1);
            Place(playfield, services, player, npc);

            npc.SetFightingTarget(player.Identity);
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));

            registry.Unregister(player.Identity);
            Assert.AreEqual(Identity.None, npc.FightingTarget);
            Assert.AreEqual(0, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.IsFalse(CombatText.MeetsRequirements(player.Stats));

            registry.Register(player);
            npc.StartFighting(player.Identity, 0);
            Assert.AreEqual(player.Identity, npc.FightingTarget);
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
        }

        [TestMethod]
        public void ExternalOpponentStatWriteSnapsBackToAttackerCount()
        {
            using ServiceProvider services = new ServiceCollection().AddSingleton(new DynelRegistry()).BuildServiceProvider();
            Playfield playfield = Playfield(services);
            Player player = TestWorld.CreatePlayer(18);
            NpcCharacter npc = Npc(1);
            Place(playfield, services, player, npc);

            player.Stats.Set(CharacterStat.NumberOfFightingOpponents, 4, StatDetail.Base);
            Assert.AreEqual(0, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));

            npc.SetFightingTarget(player.Identity);
            player.Stats.Set(CharacterStat.NumberOfFightingOpponents, 9, StatDetail.Base);
            Assert.AreEqual(1, player.Stats.Get(CharacterStat.NumberOfFightingOpponents));
            Assert.IsTrue(CombatText.MeetsRequirements(player.Stats));
        }

        [TestMethod]
        public void SnapshotDoesNotPersistOpponentCount()
        {
            var characters = new SnapshotCharacters();
            var snapshots = new CharacterSnapshotService(characters, characters, new StubLogger());
            using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
            Playfield playfield = Playfield(services);
            Player player = TestWorld.CreatePlayer(18);
            player.Playfield = playfield;
            player.Stats.Set(CharacterStat.Level, 5, StatDetail.Base);
            player.Stats.Set(CharacterStat.NumberOfFightingOpponents, 3, StatDetail.Base);

            snapshots.Commit(player);

            Assert.AreEqual(5, characters.Stats.Single(stat => stat.StatId == (int)CharacterStat.Level).StatValue);
            Assert.IsFalse(characters.Stats.Any(stat => stat.StatId == (int)CharacterStat.NumberOfFightingOpponents));
        }

        static ItemSpell CombatText { get; } = new()
        {
            FunctionType = (int)FunctionType.Text,
            Requirements =
            [
                new ItemRequirement
                {
                    StatNumber = (int)CharacterStat.NumberOfFightingOpponents,
                    Operator = (int)Operator.EqualTo,
                    Value = 0,
                    ChildOperator = (int)Operator.And
                },
                new ItemRequirement
                {
                    StatNumber = 0,
                    Operator = (int)Operator.Not,
                    Value = 0,
                    ChildOperator = (int)Operator.And
                }
            ]
        };

        static NpcCharacter Npc(int instance)
            => new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = instance },
                new StubItemBuilder());

        static void Place(Playfield playfield, ServiceProvider services, params Character[] characters)
        {
            DynelRegistry registry = services.GetRequiredService<DynelRegistry>();
            foreach (Character character in characters)
            {
                character.Playfield = playfield;
                registry.Register(character);
            }
        }

        static Playfield Playfield(ServiceProvider services)
        {
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new Identity { Type = IdentityType.Playfield2, Instance = 800 });
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, services);
            return playfield;
        }

        sealed class SnapshotCharacters : ICharacterRepository, IStatRepository
        {
            public List<StatRecord> Stats { get; } = [];

            public CharacterRecord? GetById(int id) => null;

            public void SetOnline(int id)
            {
            }

            public void SetOffline(int id)
            {
            }

            public void SaveLocation(CharacterRecord character, int online)
            {
            }

            public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats)
                => Stats.AddRange(stats);

            public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats)
            {
                Stats.AddRange(stats);
                return true;
            }

            public IReadOnlyList<StatRecord> GetForCharacter(int id) => [];

            public void UpsertForCharacter(int id, IReadOnlyList<StatRecord> stats)
            {
            }
        }
    }
}
