namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
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
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class AccumulatorTests
    {
        const int FirstPerkId = 100;
        const int FirstPerkItemId = 210830;
        const int FirstProcNanoId = 210941;
        const int TraderProfession = 7;
        const int StartingNano = 1000;

        // items.dat 210830–210839 and 210941–210950: ToWear level, slot chance, drain amount, return percentage.
        static readonly (int Level, int Chance, int Drain, int ReturnPercent)[] Ranks =
        [
            (10, 17, 20, 50), (20, 17, 41, 52), (40, 18, 64, 55), (60, 18, 104, 57),
            (90, 19, 220, 60), (110, 19, 268, 63), (130, 20, 300, 66), (160, 20, 387, 70),
            (190, 21, 488, 75), (202, 25, 624, 81)
        ];

        static readonly Lazy<PerkCatalog> Perks = new(LoadPerkCatalog);

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(10)]
        public void EachTrainedRankUsesAuthoredChanceAndDrainsOnlyTheHitTarget(int rank)
        {
            using var world = new ProcWorld(rank);
            NpcCharacter otherOpponent = world.CreateOpponent(3);
            world.Trader.SetFightingTarget(otherOpponent.Identity);
            var (_, chance, drain, returnPercent) = Ranks[rank - 1];

            Assert.AreEqual(FirstProcNanoId + rank - 1, world.Trader.Stats.GetOrZero(CharacterStat.ProcNano1));
            Assert.AreEqual(chance, world.Trader.Stats.GetOrZero(CharacterStat.ProcChance1));

            ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, roll: () => chance);
            Assert.AreEqual(StartingNano, Nano(world.Target));

            int rolls = 0;
            ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, roll: () =>
            {
                rolls++;
                return chance - 1;
            });

            Assert.AreEqual(1, rolls);
            Assert.AreEqual(StartingNano - drain, Nano(world.Target));
            Assert.AreEqual(StartingNano, Nano(otherOpponent));
            Assert.AreEqual(StartingNano + drain * returnPercent / 100, Nano(world.Trader));
            Assert.AreEqual(4000 - drain, world.Target.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(1000 + drain * returnPercent / 100, world.Trader.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void UntrainedIneligibleAndMeleeAttackersCannotTriggerAccumulator()
        {
            using var untrained = new ProcWorld(rank: 0);
            using var underLevel = new ProcWorld(rank: 1, level: 9);
            using var otherProfession = new ProcWorld(rank: 1, profession: 1);
            using var missingRankPrerequisite = new ProcWorld(rank: 2, trainedIds: [101]);
            using var wrongWeapon = new ProcWorld(rank: 1, initiative: CharacterStat.MeleeInit);

            foreach (ProcWorld world in new[] { untrained, underLevel, otherProfession, missingRankPrerequisite, wrongWeapon })
            {
                ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, () => 0);
                Assert.AreEqual(StartingNano, Nano(world.Target));
                Assert.AreEqual(StartingNano, Nano(world.Trader));
            }
        }

        [TestMethod]
        public void RegisteredAndStatDefinedProcWithTheSameNanoRollOnlyOnce()
        {
            using var world = new ProcWorld(rank: 1);
            world.Weapon.Definition.SpellList[EventType.OnHit] =
            [
                new ItemSpell { FunctionType = (int)FunctionType.AddOffProc, Arguments = [100, FirstProcNanoId] }
            ];

            int rolls = 0;
            ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, () => rolls++);
            Assert.AreEqual(1, rolls);
            Assert.AreEqual(StartingNano - 20, Nano(world.Target));
        }

        [DataTestMethod]
        [DataRow(0, 1000, 1000)]
        [DataRow(13, 1000, 1010)]
        [DataRow(13, 3997, 4000)]
        [DataRow(13, 4000, 4000)]
        public void NanoReturnedUsesActualLossAndRespectsBothPoolLimits(int victimNano, int traderNano, int expectedNano)
        {
            using var world = new ProcWorld(rank: 10);
            world.Target.Stats.Set(CharacterStat.CurrentNano, victimNano);
            world.Trader.Stats.Set(CharacterStat.CurrentNano, traderNano);

            ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, () => 0);

            Assert.AreEqual(0, Nano(world.Target));
            Assert.AreEqual(expectedNano, Nano(world.Trader));
        }

        [TestMethod]
        public void AnEvadedRangedSwingDoesNotRunItsStatDefinedProc()
        {
            using var world = new ProcWorld(rank: 1, evading: true);
            world.Trader.Stats.Set(CharacterStat.ProcChance1, 100, StatDetail.Bonus);
            var weapon = new CharacterWeapon { Item = world.Weapon };
            world.Trader.SetWeapon(WeaponSlot.MainHand, weapon);
            world.Trader.SetFightingTarget(world.Target.Identity);
            weapon.ResetAttack();

            Assert.IsTrue(weapon.Tick(weapon.AttackSpeed));
            Assert.AreEqual(StartingNano, Nano(world.Target));
            Assert.AreEqual(StartingNano, Nano(world.Trader));
        }

        [TestMethod]
        public void AWeaponHitCannotDrainItsOwnUserOrAnUnattackableTarget()
        {
            using var world = new ProcWorld(rank: 1);
            world.Target.Attackable = false;
            ProcRules.OnWeaponHit(world.Trader, world.Target, world.Weapon, () => 0);
            ProcRules.OnWeaponHit(world.Trader, world.Trader, world.Weapon, () => 0);

            Assert.AreEqual(StartingNano, Nano(world.Target));
            Assert.AreEqual(StartingNano, Nano(world.Trader));
        }

        static int Nano(Character character) => character.Stats.GetOrZero(CharacterStat.CurrentNano);

        static PerkCatalog LoadPerkCatalog()
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string data = Path.Combine(dir.FullName, "AORebirth", "GameData");
                if (File.Exists(Path.Combine(data, "Perks.json")))
                    return PerkCatalog.Load(data);
            }

            throw new FileNotFoundException("The tracked AORebirth/GameData/Perks.json was not found.");
        }

        static ItemTemplate CreatePerk(int index)
        {
            var (level, chance, _, _) = Ranks[index];
            int nextRank = index < Ranks.Length - 1 ? FirstPerkId + index + 1 : 0;
            var wearRequirements = new List<ItemRequirement>
            {
                Requirement(Operator.EqualTo, TraderProfession, CharacterStat.Profession),
                Requirement(Operator.GreaterThan, level - 1, CharacterStat.Level),
                Requirement(Operator.And)
            };
            if (index > 0)
            {
                wearRequirements.Add(Requirement(Operator.HasPerk, FirstPerkId + index - 1));
                wearRequirements.Add(Requirement(Operator.And));
            }

            return new ItemTemplate
            {
                Id = FirstPerkItemId + index,
                Actions = [new ItemAction { ActionType = (int)ActionType.ToWear, Requirements = wearRequirements }],
                SpellList = new()
                {
                    [EventType.OnWear] =
                    [
                        ConditionalProcStat(CharacterStat.ProcNano1, FirstProcNanoId + index, nextRank),
                        ProcStat(CharacterStat.ProcInitiative1, (int)WeaponFlags.Ranged),
                        ConditionalProcStat(CharacterStat.ProcChance1, chance, nextRank)
                    ]
                }
            };
        }

        static ItemRequirement Requirement(Operator op, int value = 0, CharacterStat stat = 0)
            => new() { Operator = (int)op, Value = value, StatNumber = (int)stat };

        static ItemSpell ProcStat(CharacterStat stat, int value)
            => new() { FunctionType = (int)FunctionType.ChangeVariable, Target = (int)ItemTarget.Wearer, Arguments = [(int)stat, value] };

        static ItemSpell ConditionalProcStat(CharacterStat stat, int value, int nextRank)
        {
            ItemSpell spell = ProcStat(stat, value);
            spell.Requirements = [Requirement(Operator.HasPerk, nextRank), Requirement(Operator.Not)];
            return spell;
        }

        static ItemSpell Drain(CharacterStat stat, int amount, int returnPercent, int ac = 0)
            => new()
            {
                FunctionType = (int)FunctionType.DrainHit,
                Target = (int)ItemTarget.Target,
                Arguments = [(int)stat, -amount, -amount, ac, returnPercent]
            };

        sealed class ProcWorld : IDisposable
        {
            readonly StubItemBuilder _items = new();
            readonly DynelRegistry _registry = new();
            readonly ServiceProvider _services;
            readonly Playfield _playfield;

            public Player Trader { get; }
            public NpcCharacter Target { get; }
            public Item Weapon { get; }

            public ProcWorld(int rank, int level = 220, int profession = TraderProfession,
                CharacterStat initiative = CharacterStat.RangedInit, int[]? trainedIds = null, bool evading = false)
            {
                for (int index = 0; index < Ranks.Length; index++)
                {
                    var (_, _, drain, returnPercent) = Ranks[index];
                    _items.Add(CreatePerk(index));
                    _items.Add(TestNanos.Create(FirstProcNanoId + index, durationCentiseconds: 0, can: CanFlags.ApplyOnHostile,
                        modifiers:
                        [
                            Drain(CharacterStat.Health, drain, returnPercent, (int)CharacterStat.MeleeAC),
                            Drain(CharacterStat.CurrentNano, drain, returnPercent)
                        ]));
                }

                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton<WorldSimulationAccess>()
                    .AddSingleton<IItemBuilder>(_items)
                    .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                    .BuildServiceProvider();
                _playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_playfield, _services);
                typeof(Playfield).GetField("<FightModes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_playfield, DistrictFightModes.None);
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_playfield, new ConcurrentDictionary<Character, byte>());

                Trader = new Player(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 }, new StubLogger(), _items)
                {
                    Playfield = _playfield,
                    PerkCatalog = Perks.Value,
                    PerkActionCatalog = PerkActionCatalog.Empty
                };
                Trader.Inventory.Apply(new CharacterHydrationResult(), 1, _items);
                Trader.Stats.Set(CharacterStat.Profession, profession);
                Trader.Stats.Set(CharacterStat.Level, level);
                Trader.LoadTrainedPerks(trainedIds ?? Enumerable.Range(FirstPerkId, rank));
                Trader.RebaseStats();
                SetVitals(Trader, health: 1000);
                _registry.Register(Trader);

                Target = CreateOpponent(2, evading);
                Weapon = TestWorld.CreateItem();
                Weapon.Definition.Stats[CharacterStat.InitiativeType] = (int)initiative;
                Weapon.Definition.Stats[CharacterStat.AttackRange] = 10;
            }

            public NpcCharacter CreateOpponent(int id, bool evading = false)
            {
                var opponent = new ProcOpponent(new Identity { Type = IdentityType.CanbeAffected, Instance = id }, _items, evading)
                {
                    Playfield = _playfield,
                    Attackable = true
                };
                SetVitals(opponent, health: 4000);
                _registry.Register(opponent);
                return opponent;
            }

            static void SetVitals(Character character, int health)
            {
                character.Stats.Set(CharacterStat.MaxHealth, 4000);
                character.Stats.Set(CharacterStat.Health, health);
                character.Stats.Set(CharacterStat.MaxNanoEnergy, 4000);
                character.Stats.Set(CharacterStat.CurrentNano, StartingNano);
            }

            public void Dispose() => _services.Dispose();
        }

        sealed class ProcOpponent(Identity identity, IItemBuilder items, bool evading) : NpcCharacter(identity, items)
        {
            public override bool IsEvading => evading;
        }
    }
}
