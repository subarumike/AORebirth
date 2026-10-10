namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    [TestClass]
    public sealed class BuffRepairIntegrationTests
    {
        const int ParentId = 31001;
        const int ChildId = 31002;

        [TestMethod]
        public void SkillFunctionAppliesThroughThePassiveModifierPath()
        {
            var stats = new NpcCharacter(IdentityFor(1), new StubItemBuilder()).Stats;
            stats.Set(CharacterStat.Strength, 100);

            StatModifierSpells.Apply([Skill(CharacterStat.Strength, 17)], stats);

            Assert.AreEqual(117, stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(17, stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
            Assert.AreEqual(100, stats.GetOrZero(CharacterStat.Strength, StatDetail.Base));
        }

        [TestMethod]
        public void SkillFunctionAppliesThroughTheOnUseDispatchPath()
        {
            var target = new NpcCharacter(IdentityFor(1), new StubItemBuilder());
            target.Stats.Set(CharacterStat.Strength, 100);
            NanoSpell instant = TestNanos.Create(ParentId, durationCentiseconds: 0,
                modifiers: [Skill(CharacterStat.Strength, 17)]);

            Assert.IsTrue(instant.ExecuteOnUseSpells(target, new StubInventoryRepository(), new StubItemBuilder()));

            Assert.AreEqual(117, target.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(17, target.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
        }

        [TestMethod]
        public void BuffSkillAndDamageBonusesApplyOnceAcrossLandingRebaseAndCancellation()
        {
            var player = new Player(IdentityFor(1), new StubLogger(), new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.Stats.Set(CharacterStat.MaxHealth, 1000);
            player.Stats.Set(CharacterStat.Health, 1000);
            player.Stats.Set(CharacterStat.Strength, 100);
            player.Stats.Set(CharacterStat.ProjectileDamageModifier, 5);
            NanoSpell spell = TestNanos.Create(ParentId, modifiers:
                [Skill(CharacterStat.Strength, 17), Skill(CharacterStat.ProjectileDamageModifier, 25)]);
            var items = new StubItemBuilder().Add(spell);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(player, player, ParentId, items,
                new StubInventoryRepository(), DateTime.UtcNow));
            Assert.AreEqual(17, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
            Assert.AreEqual(30, player.Stats.GetOrZero(CharacterStat.ProjectileDamageModifier));

            player.RebaseStats();
            player.RebaseStats();

            Assert.AreEqual(17, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
            Assert.AreEqual(25, player.Stats.GetOrZero(CharacterStat.ProjectileDamageModifier, StatDetail.Bonus));
            Assert.AreEqual(30, player.Stats.GetOrZero(CharacterStat.ProjectileDamageModifier));
            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(player, ParentId));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
            Assert.AreEqual(5, player.Stats.GetOrZero(CharacterStat.ProjectileDamageModifier));
        }

        [DataTestMethod]
        [DataRow((int)FunctionType.Hit)]
        [DataRow((int)FunctionType.CastNano)]
        [DataRow((int)FunctionType.TeamCastNano)]
        [DataRow((int)FunctionType.AreaCastNano)]
        [DataRow((int)FunctionType.PlayfieldNano)]
        [DataRow((int)FunctionType.CastNanoIfPossible)]
        [DataRow((int)FunctionType.NpcCastNanoIfPossible)]
        [DataRow((int)FunctionType.CastNanoIfPossibleOnFightTarget)]
        [DataRow((int)FunctionType.NpcCastNanoIfPossibleOnFightTarget)]
        public void PeriodicFunctionsHonorConfiguredCountAndCentisecondInterval(int function)
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ItemSpell effect = Periodic((FunctionType)function, tickCount: 3, interval: 50);
            Buff buff = Buff.Create(TestNanos.Create(ParentId, modifiers: [effect]), IdentityFor(1), 1, start);
            var due = new List<ItemSpell>();

            buff.CollectDueTicks(start.AddMilliseconds(499), due);
            Assert.AreEqual(0, due.Count);
            buff.CollectDueTicks(start.AddMilliseconds(500), due);
            Assert.AreEqual(1, due.Count);
            Assert.AreSame(effect, due[0]);
            due.Clear();
            buff.CollectDueTicks(start.AddSeconds(1), due);
            Assert.AreEqual(1, due.Count);
            due.Clear();
            buff.CollectDueTicks(start.AddMinutes(1), due);
            Assert.AreEqual(0, due.Count, "Landing plus two later applications exhausts TickCount=3.");
        }

        [DataTestMethod]
        [DataRow(1, 100)]
        [DataRow(3, 0)]
        public void NonRepeatingFunctionsDoNotScheduleLaterApplications(int count, int interval)
        {
            var due = new List<ItemSpell>();
            DateTime start = DateTime.UtcNow;
            Buff buff = Buff.Create(TestNanos.Create(ParentId,
                modifiers: [Periodic(FunctionType.CastNano, count, (uint)interval)]), IdentityFor(1), 1, start);

            buff.CollectDueTicks(start.AddSeconds(10), due);

            Assert.AreEqual(0, due.Count);
        }

        [TestMethod]
        public void ChildAuraRefreshesOnlyAtConfiguredIntervalsAndStopsAfterConfiguredCount()
        {
            NanoSpell parent = TestNanos.Create(ParentId,
                modifiers: [Periodic(FunctionType.CastNano, 3, 100)]);
            NanoSpell child = TestNanos.Create(ChildId, durationCentiseconds: 1000);
            var items = new StubItemBuilder().Add(parent).Add(child);
            using var world = new PeriodicWorld(items);
            NpcCharacter owner = world.Character(1);
            DateTime start = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(owner, owner, ParentId, items,
                new StubInventoryRepository(), start));
            Buff first = RequireBuff(owner, ChildId);
            Assert.AreEqual(2, owner.Buffs.Count);
            Assert.AreEqual(20, owner.UsedNcu);
            NanoRuntime.Tick(owner, start.AddMilliseconds(999));
            Assert.AreSame(first, RequireBuff(owner, ChildId));

            NanoRuntime.Tick(owner, start.AddSeconds(1));
            Buff second = RequireBuff(owner, ChildId);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.NanoInstance + 1, second.NanoInstance);
            NanoRuntime.Tick(owner, start.AddSeconds(2));
            Buff third = RequireBuff(owner, ChildId);
            Assert.AreNotSame(second, third);
            Assert.AreEqual(second.NanoInstance + 1, third.NanoInstance);
            NanoRuntime.Tick(owner, start.AddSeconds(3));

            Assert.AreSame(third, RequireBuff(owner, ChildId));
            Assert.AreEqual(2, owner.Buffs.Count);
            Assert.AreEqual(20, owner.UsedNcu);
        }

        [TestMethod]
        public void CancellingAuraStopsRefreshAndLetsItsExistingChildExpire()
        {
            NanoSpell parent = TestNanos.Create(ParentId,
                modifiers: [Periodic(FunctionType.CastNano, 10, 100)]);
            var items = new StubItemBuilder().Add(parent).Add(TestNanos.Create(ChildId, durationCentiseconds: 1000));
            using var world = new PeriodicWorld(items);
            NpcCharacter owner = world.Character(1);
            DateTime start = DateTime.UtcNow;
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(owner, owner, ParentId, items,
                new StubInventoryRepository(), start));
            NanoRuntime.Tick(owner, start.AddSeconds(1));
            Buff child = RequireBuff(owner, ChildId);

            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(owner, ParentId));
            NanoRuntime.Tick(owner, start.AddSeconds(2));
            Assert.AreSame(child, RequireBuff(owner, ChildId));
            Assert.AreEqual(10, owner.UsedNcu);
            NanoRuntime.Tick(owner, child.ExpiresAtUtc);
            Assert.IsFalse(owner.TryGetBuff(ChildId, out _));
            Assert.AreEqual(0, owner.UsedNcu);
            NanoRuntime.Tick(owner, child.ExpiresAtUtc.AddMinutes(1));
            Assert.AreEqual(0, owner.Buffs.Count);
        }

        [TestMethod]
        public void ReplacedBuffInstanceCannotExecuteTicksAlreadyCollectedForIt()
        {
            NanoSpell refresher = TestNanos.Create(ParentId,
                modifiers: [Periodic(FunctionType.CastNano, 2, 100)]);
            NanoSpell child = TestNanos.Create(ChildId, modifiers: [HealthHit(10, 2, 100)]);
            var items = new StubItemBuilder().Add(refresher).Add(child);
            using var world = new PeriodicWorld(items);
            NpcCharacter owner = world.Character(1);
            owner.Stats.Set(CharacterStat.Health, 100);
            DateTime start = DateTime.UtcNow;
            Assert.AreEqual(BuffApplyDecision.Apply,
                owner.TryApplyBuff(refresher, owner.Identity, start, out _, out _));
            Assert.AreEqual(BuffApplyDecision.Apply,
                owner.TryApplyBuff(child, owner.Identity, start, out Buff? oldChild, out _));

            NanoRuntime.Tick(owner, start.AddSeconds(1));

            Assert.AreNotSame(oldChild, RequireBuff(owner, ChildId));
            Assert.AreEqual(110, owner.Stats.GetOrZero(CharacterStat.Health),
                "The replacement's landing heals once; the already collected old-instance tick must be ignored.");
        }

        [TestMethod]
        public void PeriodicFunctionsReadCasterRankAndTargetNanoStateWhileApplyingToCaster()
        {
            const int trainedRank = 810001;
            const int nextRank = 810002;
            ItemSpell heal = HealthHit(7, 3, 100);
            heal.Target = (int)ItemTarget.User;
            heal.Requirements =
            [
                new() { Operator = (int)Operator.OnCaster },
                new() { Operator = (int)Operator.HasPerk, Value = trainedRank },
                new() { Operator = (int)Operator.HasNotPerk, Value = nextRank },
                new() { Operator = (int)Operator.And },
                new() { Operator = (int)Operator.OnTarget },
                new() { Operator = (int)Operator.HasRunningNano, Value = ChildId },
                new() { Operator = (int)Operator.And },
            ];
            NanoSpell parent = TestNanos.Create(ParentId, modifiers: [heal]);
            var items = new StubItemBuilder().Add(parent);
            using var world = new PeriodicWorld(items);
            NpcCharacter owner = world.Character(1);
            var caster = new Player(IdentityFor(2), new StubLogger(), items);
            caster.TrainedPerks.Restore([trainedRank]);
            caster.Stats.Set(CharacterStat.MaxHealth, 100);
            caster.Stats.Set(CharacterStat.Health, 40);
            world.Attach(caster);
            DateTime start = DateTime.UtcNow;
            Assert.AreEqual(BuffApplyDecision.Apply,
                owner.TryApplyBuff(TestNanos.Create(ChildId), owner.Identity, start, out _, out _));

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, owner, ParentId, items,
                new StubInventoryRepository(), start));
            Assert.AreEqual(47, caster.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(owner, start.AddSeconds(1));
            Assert.AreEqual(54, caster.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(1000, owner.Stats.GetOrZero(CharacterStat.Health));

            Assert.AreEqual(BuffRemovalOutcome.Removed,
                owner.TryRemoveBuff(ChildId, BuffRemovalReason.Cancelled, out _));
            NanoRuntime.Tick(owner, start.AddSeconds(2));
            Assert.AreEqual(54, caster.Stats.GetOrZero(CharacterStat.Health),
                "The next tick must re-evaluate the event target's running-nano resolver.");
            Assert.AreEqual(1000, owner.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void DamageOverTimeKeepsInitialAddDamageAndUnmodifiedLaterTicks()
        {
            NanoSpell dot = TestNanos.Create(ParentId, modifiers: [HealthHit(-10, 3, 100)],
                can: CanFlags.ApplyOnHostile);
            var items = new StubItemBuilder().Add(dot);
            using var world = new PeriodicWorld(items);
            NpcCharacter caster = world.Character(1);
            NpcCharacter target = world.Character(2);
            caster.Stats.Set(CharacterStat.ProjectileDamageModifier, 50);
            DateTime start = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, target, ParentId, items,
                new StubInventoryRepository(), start));
            Assert.AreEqual(940, target.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(target, start.AddMilliseconds(999));
            Assert.AreEqual(940, target.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(target, start.AddSeconds(2));
            Assert.AreEqual(920, target.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(target, start.AddSeconds(3));
            Assert.AreEqual(920, target.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void HealOverTimeDropsTicksAtItsExpiryDeadline()
        {
            NanoSpell hot = TestNanos.Create(ParentId, durationCentiseconds: 200,
                modifiers: [HealthHit(7, 4, 100)]);
            var items = new StubItemBuilder().Add(hot);
            using var world = new PeriodicWorld(items);
            NpcCharacter owner = world.Character(1);
            owner.Stats.Set(CharacterStat.Health, 40);
            DateTime start = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(owner, owner, ParentId, items,
                new StubInventoryRepository(), start));
            Assert.AreEqual(47, owner.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(owner, start.AddSeconds(1));
            Assert.AreEqual(54, owner.Stats.GetOrZero(CharacterStat.Health));
            NanoRuntime.Tick(owner, start.AddSeconds(2));
            Assert.AreEqual(54, owner.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(0, owner.Buffs.Count);
            NanoRuntime.Tick(owner, start.AddMinutes(1));
            Assert.AreEqual(54, owner.Stats.GetOrZero(CharacterStat.Health));
        }

        static Identity IdentityFor(int instance)
            => new() { Type = IdentityType.CanbeAffected, Instance = instance };

        static ItemSpell Skill(CharacterStat stat, int amount)
            => new() { FunctionType = (int)FunctionType.Skill, Arguments = [(int)stat, amount] };

        static ItemSpell Periodic(FunctionType function, int tickCount, uint interval)
            => new()
            {
                FunctionType = (int)function,
                Arguments = [ChildId],
                TickCount = tickCount,
                TickInterval = interval,
            };

        static ItemSpell HealthHit(int amount, int tickCount, uint interval)
            => new()
            {
                FunctionType = (int)FunctionType.Hit,
                Arguments = [(int)CharacterStat.Health, amount, amount, (int)CharacterStat.ProjectileAC],
                TickCount = tickCount,
                TickInterval = interval,
            };

        static Buff RequireBuff(Character owner, int id)
        {
            Assert.IsTrue(owner.TryGetBuff(id, out Buff? buff));
            Assert.IsNotNull(buff);
            return buff;
        }

        sealed class PeriodicWorld : IDisposable
        {
            readonly ServiceProvider _services;
            readonly DynelRegistry _registry = new();
            readonly StubItemBuilder _items;
            readonly Playfield _playfield;

            public PeriodicWorld(StubItemBuilder items)
            {
                _items = items;
                _playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton<IItemBuilder>(items)
                    .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_playfield, _services);
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_playfield, new ConcurrentDictionary<Character, byte>());
            }

            public NpcCharacter Character(int id)
            {
                var character = new NpcCharacter(IdentityFor(id), _items)
                {
                    Attackable = true,
                    Playfield = _playfield,
                };
                character.Stats.Set(CharacterStat.MaxNCU, 60);
                character.Stats.Set(CharacterStat.MaxHealth, 1000);
                character.Stats.Set(CharacterStat.Health, 1000);
                _registry.Register(character);
                return character;
            }

            public void Attach(Character character)
            {
                character.Playfield = _playfield;
                _registry.Register(character);
            }

            public void Dispose() => _services.Dispose();
        }
    }
}
