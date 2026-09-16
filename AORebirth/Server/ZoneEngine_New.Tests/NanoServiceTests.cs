namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;
    using ZoneEngine_New.Core.Helpers;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class NanoServiceTests
    {

        [TestMethod]
        public void Unknown_unuploaded_insufficient_mana_and_ncu_fail_before_cast_packet()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(upload: false);
            Assert.IsFalse(f.Service.TryCast(p, 99, p.Identity));
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            p.TryAddUploadedNano(10); p.Stats.Set(CharacterStat.CurrentNano, 9);
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            p.Stats.Set(CharacterStat.CurrentNano, 100); p.Stats.Set(CharacterStat.MaxNCU, 4);
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            Assert.IsTrue(f.Session(p).Bodies.All(body => body is ChatTextMessage)); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Unsupported_compound_periodic_hostile_or_function_graph_is_atomic_rejection()
        {
            foreach (int mode in Enumerable.Range(0, 4))
            {
                var nano = Buff(10);
                if (mode == 0) nano.Template.SpellList[EventType.OnUse].Add(new ItemSpell { FunctionType = (int)FunctionType.AreaHit });
                if (mode == 1) nano.Template.SpellList[EventType.OnUse][0].TickCount = 2;
                if (mode == 2) nano.Template.Defend[CharacterStat.NanoResist] = 100;
                if (mode == 3) nano.Template.Actions.Add(new ItemAction { ActionType = (int)ActionType.ToUse,
                    Requirements = [new ItemRequirement { ChildOperator = (int)Operator.And,
                        Operator = (int)Operator.HasMaster, Target = (int)ItemTarget.Self }] });
                var f = new Fixture(nano); var p = f.Player();
                Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity), "mode=" + mode);
                Assert.IsTrue(f.Session(p).Bodies.All(body => body is ChatTextMessage)); Assert.AreEqual(0, f.Store.Commits);
                Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength));
            }
        }

        [TestMethod]
        public void Expiration_reverses_once_and_persists_removal_before_packet()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10);
            f.Advance(10000); f.Service.Tick(p); f.Service.Tick(p);
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength)); Assert.AreEqual(0, f.Service.GetActive(p).Count);
            Assert.AreEqual(0, f.Store.Rows[p.Identity.Instance].Count); Assert.AreEqual(1, f.Store.Commits);
            Assert.AreEqual(1, f.Session(p).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Removal_failure_preserves_active_contribution_and_no_removal_packet()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10); f.Store.Failure = new IOException();
            Assert.IsFalse(f.Service.Remove(p, 10)); Assert.AreEqual(105, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(1, f.Service.GetActive(p).Count); Assert.AreEqual(0, f.Session(p).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Restore_uses_durable_expiry_and_identity_without_replaying_instant_heal()
        {
            var nano = Buff(10); nano.Template.SpellList[EventType.OnUse].Add(Heal(35));
            var f = new Fixture(nano);
            f.Store.Rows[1] = [new ActiveNanoRecord(10, 7, 51, 1000, f.Utc.AddSeconds(4).Ticks)];
            var p = f.Player(); Assert.AreEqual(250, p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(105, p.Stats.GetOrZero(CharacterStat.Strength));
            f.Service.RefreshPlayer(p); var duration = f.Session(p).Bodies.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(400, duration.Parameter2); Assert.AreEqual(51, f.Service.GetActive(p).Single().NanoInstance);
            Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Unknown_persisted_nano_rejects_hydration_without_pruning_rows()
        {
            var f = new Fixture(Buff(10)); f.Store.Rows[1] = [new ActiveNanoRecord(99, 7, 1, 1000, f.Utc.AddSeconds(4).Ticks)];
            var p = f.Player(attach: false); Assert.IsFalse(f.Service.AttachPlayer(p));
            Assert.AreEqual(99, f.Store.Rows[1].Single().NanoId); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Expired_rows_are_removed_and_zero_instance_normalized_without_recasting()
        {
            var f = new Fixture(Buff(10), Buff(11, strain: 8));
            f.Store.Rows[1] = [new ActiveNanoRecord(10, 7, 0, 1000, f.Utc.AddSeconds(4).Ticks),
                new ActiveNanoRecord(11, 8, 42, 1000, f.Utc.AddSeconds(-1).Ticks)];
            var p = f.Player(); Assert.AreEqual(43, f.Service.GetActive(p).Single().NanoInstance);
            Assert.AreEqual(1, f.Store.Rows[1].Count); Assert.AreEqual(1, f.Store.Commits);
            Assert.AreEqual(0, f.Session(p).Bodies.Count);
        }

        [TestMethod]
        public void Duplicate_persisted_strain_fails_closed_instead_of_arbitrary_replacement()
        {
            var f = new Fixture(Buff(10), Buff(11));
            f.Store.Rows[1] = [new ActiveNanoRecord(10, 7, 1, 1000, f.Utc.AddSeconds(4).Ticks),
                new ActiveNanoRecord(11, 7, 2, 1000, f.Utc.AddSeconds(4).Ticks)];
            var p = f.Player(attach: false); Assert.IsFalse(f.Service.AttachPlayer(p)); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Legacy_duration_only_row_gets_explicit_deadline_without_invented_default()
        {
            var f = new Fixture(Buff(10)); f.Store.Rows[1] = [new ActiveNanoRecord(10, 7, 1, 450, 0)];
            var p = f.Player(); Assert.AreEqual(f.Utc.AddMilliseconds(4500).Ticks, f.Service.GetActive(p).Single().ExpiresAtUtcTicks);
            Assert.AreEqual(450, f.Service.GetActive(p).Single().DurationCentiseconds); Assert.AreEqual(1, f.Store.Commits);
            f.Advance(4500); f.Service.Tick(p); Assert.AreEqual(0, f.Service.GetActive(p).Count);
        }

        [TestMethod]
        public void Removal_addresses_active_instance_but_never_guesses_the_single_remaining_buff()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10);
            Assert.IsFalse(f.Service.TryRemove(p, new CharacterActionMessage { Action = CharacterActionType.RemoveFriendlyNano, Target = Identity.None }));
            Assert.IsTrue(f.Service.TryRemove(p, new CharacterActionMessage { Action = CharacterActionType.RemoveFriendlyNano,
                Target = Identity.None, Parameter1 = f.Service.GetActive(p).Single().NanoInstance }));
            Assert.AreEqual(0, f.Service.GetActive(p).Count);
        }

        [TestMethod]
        public void Equipment_rebase_reapplies_only_current_nano_bonus()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10);
            p.Stats.ClearBonuses(); p.Stats.AddBonus(CharacterStat.Strength, 9); f.Service.ReapplyBonusesAfterRebase(p);
            Assert.AreEqual(114, p.Stats.GetOrZero(CharacterStat.Strength));
            f.Service.Remove(p, 10); Assert.AreEqual(109, p.Stats.GetOrZero(CharacterStat.Strength));
        }

        [TestMethod]
        public void Overview_login_clears_stale_unlock_without_buff_remove_and_restores_only_active_mask()
        {
            var f = new Fixture([new ActiveStatOverlayNanoSpecialization()], Buff(223767));
            var p = f.Player(attach: false); p.Stats.Set(CharacterStat.MapsC, 403669119);
            Assert.IsTrue(f.Service.AttachPlayer(p)); Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.MapsC));
            Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            f.Service.DetachPlayer(p);
            f.Store.Rows[1] = [new ActiveNanoRecord(223767, 7, 8, 1000, f.Utc.AddSeconds(4).Ticks)];
            var restored = f.Player(); Assert.AreEqual(403669119, restored.Stats.GetOrZero(CharacterStat.MapsC));
            f.Service.RefreshPlayer(restored); Assert.AreEqual(0, f.Session(restored).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Body_development_and_nano_pool_derive_bonus_maxima_without_baking_into_snapshot_base()
        {
            var nano = Buff(10, modifier: 20);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.BodyDevelopment;
            nano.Template.SpellList[EventType.OnUse].Add(new ItemSpell { FunctionType = (int)FunctionType.Modify,
                Target = (int)ItemTarget.Target, Arguments = [(int)CharacterStat.NanoPool, 15] });
            var f = new Fixture(nano); var p = f.Player();
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            f.Restore(p, 10);
            Assert.AreEqual(560, p.Stats.GetOrZero(CharacterStat.MaxHealth)); Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(145, p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy)); Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            p.NanoRuntime = f.Service; p.Rebase();
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", 1, 1, 1, 1, 10), p.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", 1, 1, 1, 1, 30), p.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", 1, 1, 1, 1, 10), p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", 1, 1, 1, 1, 25), p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
            Assert.IsTrue(f.Service.Remove(p, 10));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base), p.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base), p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
        }

        [TestMethod]
        public void Independent_body_buffs_recompute_owned_derived_delta_when_one_expires()
        {
            var first = Buff(10, modifier: 20); var second = Buff(11, modifier: 10, strain: 8);
            first.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.BodyDevelopment;
            second.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.BodyDevelopment;
            var f = new Fixture(first, second); var p = f.Player(); p.Stats.Set(CharacterStat.BodyDevelopment, 10);
            f.Restore(p, 10); f.Advance(300); f.Restore(p, 11); Assert.AreEqual(590, p.Stats.GetOrZero(CharacterStat.MaxHealth));
            f.Service.Remove(p, 10); Assert.AreEqual(530, p.Stats.GetOrZero(CharacterStat.MaxHealth));
            f.Service.Remove(p, 11); Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        [TestMethod]
        public void Scaling_modify_uses_exact_existing_flat_contribution_and_reverses_once()
        {
            var nano = Buff(10, modifier: 240);
            nano.Template.SpellList[EventType.OnUse][0].FunctionType = (int)FunctionType.ScalingModify;
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.RunSpeed;
            var f = new Fixture(nano); var p = f.Player(); p.Stats.Set(CharacterStat.RunSpeed, 100);
            f.Restore(p, 10); Assert.AreEqual(340, p.Stats.GetOrZero(CharacterStat.RunSpeed));
            f.Service.Remove(p, 10); Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.IsFalse(f.Service.Remove(p, 10)); Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Bonus));
        }

        [TestMethod]
        public void Prospective_nano_projection_uses_future_stats_without_mutating_actor_store_or_wire()
        {
            var f = new Fixture(ResourceBonusNano()); var p = f.Player();
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            f.Restore(p, 10); p.NanoRuntime = f.Service; p.Rebase();
            var before = p.Stats.GetEntries().ToArray(); int messages = f.Session(p).Bodies.Count, commits = f.Store.Commits;
            var future = new StatCollection();
            foreach (var entry in p.Stats.GetEntries()) future.Set(entry.Stat, entry.Base);
            future.Set(CharacterStat.Level, 15); future.Set(CharacterStat.TitleLevel, 2);
            p.Inventory.ApplyWearBonuses(future);
            Assert.IsTrue(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.TryVital("health", future, out int baseHealth));
            Assert.IsTrue(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.TryVital("nano", future, out int baseNano));
            future.Set(CharacterStat.MaxHealth, baseHealth); future.Set(CharacterStat.MaxNanoEnergy, baseNano);
            f.Service.ProjectBonusesAfterRebase(p, future);
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", 1, 1, 2, 15, 30) + 40, future.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", 1, 1, 2, 15, 25) + 30, future.GetOrZero(CharacterStat.MaxNanoEnergy));
            Assert.AreEqual(baseHealth, future.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(baseNano, future.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            Assert.AreEqual(5, future.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            Assert.AreEqual(7, future.GetOrZero(CharacterStat.CurrentNano, StatDetail.Bonus));
            Assert.IsTrue(before.SequenceEqual(p.Stats.GetEntries()));
            Assert.AreEqual(messages, f.Session(p).Bodies.Count); Assert.AreEqual(commits, f.Store.Commits);
            Assert.ThrowsExactly<ArgumentException>(() => f.Service.ProjectBonusesAfterRebase(p, p.Stats));
            Assert.ThrowsExactly<InvalidOperationException>(() => f.Service.ProjectBonusesAfterRebase(TestWorld.CreatePlayer(p.Identity.Instance), new()));
        }

        private static NanoDefinition ResourceBonusNano()
        {
            var nano = Buff(10, modifier: 20);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.BodyDevelopment;
            foreach (var pair in new[] { (CharacterStat.NanoPool, 15), (CharacterStat.MaxHealth, 40),
                (CharacterStat.MaxNanoEnergy, 30), (CharacterStat.Health, 5), (CharacterStat.CurrentNano, 7) })
                nano.Template.SpellList[EventType.OnUse].Add(new() { FunctionType = (int)FunctionType.Modify,
                    Target = (int)ItemTarget.Target, Arguments = [(int)pair.Item1, pair.Item2] });
            return nano;
        }

        [TestMethod]
        public void Attribute_nano_trickle_uses_floor_of_total_not_floor_of_delta_and_survives_restore()
        {
            var nano = AbilityBuff(10, CharacterStat.Agility, 1);
            var f = new Fixture(nano); var p = f.Player();
            foreach (var stat in AbilityStats) p.Stats.Set(stat, 0);
            p.Stats.Set(CharacterStat.Agility, 6); p.Stats.Set((CharacterStat)108, 50);
            f.Restore(p, 10);
            // Skill108 is 20% Strength +60% Agility +20% Sense. floor(4.2/4)-floor(3.6/4)=1.
            Assert.AreEqual(51, p.Stats.GetOrZero((CharacterStat)108));
            Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108, StatDetail.Base));
            f.Service.DetachPlayer(p); Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108));
            f.Session(p).Bodies.Clear(); Assert.IsTrue(f.Service.AttachPlayer(p));
            Assert.AreEqual(51, p.Stats.GetOrZero((CharacterStat)108)); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            Assert.AreEqual(0, f.Store.Commits); Assert.IsTrue(f.Service.Remove(p, 10));
            Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108));
        }

        [TestMethod]
        public void Multiple_attribute_nanos_share_fractional_trickle_without_double_apply_or_reverse()
        {
            var first = AbilityBuff(10, CharacterStat.Stamina, 2);
            var second = AbilityBuff(11, CharacterStat.Stamina, 2); second.Template.Stats[(CharacterStat)75] = 8;
            var f = new Fixture(first, second); var p = f.Player(); p.Stats.Set(CharacterStat.Stamina, 2);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10);
            f.Restore(p, 10); Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            f.Advance(300); f.Restore(p, 11);
            Assert.AreEqual(6, p.Stats.GetOrZero(CharacterStat.Stamina));
            Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.IsTrue(f.Service.Remove(p, 10));
            Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.IsTrue(f.Service.Remove(p, 11)); Assert.IsFalse(f.Service.Remove(p, 11));
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        private static readonly CharacterStat[] AbilityStats = [CharacterStat.Strength, CharacterStat.Agility,
            CharacterStat.Stamina, CharacterStat.Intelligence, CharacterStat.Sense, CharacterStat.Psychic];
        private static NanoDefinition AbilityBuff(int id, CharacterStat stat, int amount)
        {
            var nano = Buff(id, modifier: amount);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)stat;
            return nano;
        }

        [TestMethod]
        public void Cast_refusal_does_not_mutate_saved_nanos_stats_or_dao_and_stale_session_is_silent()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10);
            var stats = p.Stats.GetEntries().ToArray(); var rows = f.Store.Rows[1].ToArray();
            for (int i = 0; i < 2; i++) Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            Assert.IsTrue(stats.SequenceEqual(p.Stats.GetEntries()));
            CollectionAssert.AreEqual(rows, f.Store.Rows[1]); Assert.AreEqual(0, f.Store.Attempts);
            Assert.AreEqual(2, f.Session(p).Bodies.Count);
            Assert.IsTrue(f.Session(p).Bodies.All(b => b is ChatTextMessage));
            var session = f.Session(p); session.Bodies.Clear(); session.State = SessionState.Connected;
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity)); Assert.AreEqual(0, session.Bodies.Count);
            session.State = SessionState.InPlay; session.UnbindPlayer();
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity)); Assert.AreEqual(0, session.Bodies.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Saved_nano_removal_commit_failure_preserves_projection_and_quarantines_unknown_outcome(bool unknown)
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Restore(p, 10);
            var rows = f.Store.Rows[1].ToArray();
            f.Store.Failure = unknown ? new DatabaseCommitOutcomeUnknownException(new IOException("transport")) : new IOException("rollback");
            Assert.IsFalse(f.Service.Remove(p, 10));
            CollectionAssert.AreEqual(rows, f.Store.Rows[1]); Assert.AreEqual(105, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(1, f.Service.GetActive(p).Count); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            Assert.AreEqual(1, f.Store.Attempts); Assert.AreEqual(0, f.Store.Commits);
            Assert.AreEqual(unknown, p.IsPersistenceQuarantined);
            if (unknown) { Assert.AreEqual(SessionState.Closed, f.Session(p).State); f.Service.Tick(p); Assert.AreEqual(1, f.Store.Attempts); }
        }

        [TestMethod]
        public void Saved_retired_area_effect_restores_duration_without_replaying_healing_or_modifiers()
        {
            var nano = Buff(100198);
            var f = new Fixture([new SavedDurationNanoSpecialization()], nano);
            f.Store.Rows[1] = [new ActiveNanoRecord(nano.Id, nano.Strain, 42, 1000, f.Utc.AddSeconds(10).Ticks)];
            var p = f.Player();
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(250, p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(42, f.Service.GetActive(p).Single().NanoInstance);
            Assert.AreEqual(0, f.Store.Attempts); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            Assert.IsTrue(f.Service.Remove(p, nano.Id)); Assert.AreEqual(0, f.Store.Rows[1].Count);
        }

        private static string Describe(MessageBody body) => body is CharacterActionMessage action ? action.Action.ToString() : body.GetType().Name;
        private static NanoDefinition Buff(int id, int modifier = 5, int ncu = 5, int duration = 1000, int strain = 7)
            => new(new ItemTemplate { Id = id, Stats = new() { [(CharacterStat)8] = duration, [(CharacterStat)54] = ncu,
                [(CharacterStat)75] = strain, [(CharacterStat)210] = 30, [(CharacterStat)287] = 20,
                [(CharacterStat)294] = 20, [(CharacterStat)407] = 10 }, SpellList = new() { [EventType.OnUse] =
                [new ItemSpell { FunctionType = (int)FunctionType.Modify, Target = (int)ItemTarget.Target,
                    Arguments = [(int)CharacterStat.Strength, modifier] }] } });
        private static ItemSpell Heal(int amount, int? maximum = null) => new() { FunctionType = (int)FunctionType.Hit,
            Target = (int)ItemTarget.Target, Arguments = maximum.HasValue
                ? [(int)CharacterStat.Health, amount, maximum.Value] : [(int)CharacterStat.Health, amount] };

        private sealed class Fixture
        {
            public readonly NanoService Service; public readonly Store Store = new();
            public DateTime Utc = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc); public long Milliseconds;
            public int RandomCalls; public (int, int) LastRange; private readonly NanoDefinition[] _nanos;
            public int? InterruptionCode;
            public Fixture(params NanoDefinition[] nanos) : this([], nanos) { }
            public Fixture(INanoSpecialization[] specialties, params NanoDefinition[] nanos)
            {
                _nanos = nanos;
                Service = new NanoService(new NanoCatalog(nanos), Store, specialties, utcNow: () => Utc,
                    monotonicMilliseconds: () => Milliseconds, next: (min, max) => { RandomCalls++; LastRange = (min, max); return max - 1; },
                    interruptionCode: _ => InterruptionCode);
            }
            public Player Player(int id = 1, bool upload = true, bool attach = true)
            {
                var player = TestWorld.CreatePlayer(id);
                player.Stats.Set(CharacterStat.Strength, 100); player.Stats.Set(CharacterStat.CurrentNano, 100);
                player.Stats.Set(CharacterStat.MaxNanoEnergy, 100); player.Stats.Set(CharacterStat.Health, 250);
                player.Stats.Set(CharacterStat.MaxHealth, 500); player.Stats.Set(CharacterStat.MaxNCU, 30);
                player.Stats.Set(CharacterStat.AggDef, 25); player.Stats.Set(CharacterStat.NanoCInit, 0);
                if (upload) foreach (NanoDefinition nano in _nanos) player.TryAddUploadedNano(nano.Id);
                ReplaceSession(player); if (attach) Assert.IsTrue(Service.AttachPlayer(player));
                player.Stats.DrainDirty(); return player;
            }
            public Session Session(Player player) => (Session)player.Session!;
            public void ReplaceSession(Player player) { var session = new Session(); session.BindPlayer(player); player.Session = session; }
            public void Advance(int ms) { Milliseconds += ms; Utc = Utc.AddMilliseconds(ms); }
            public void Restore(Player player, int nanoId)
            {
                var nano = _nanos.Single(n => n.Id == nanoId);
                var rows = Service.GetActive(player).ToList();
                Service.DetachPlayer(player);
                rows.Add(new ActiveNanoRecord(nanoId, nano.Strain, nanoId, nano.DurationCentiseconds,
                    Utc.AddMilliseconds(nano.DurationCentiseconds * 10L).Ticks));
                Store.Rows[player.Identity.Instance] = rows;
                Assert.IsTrue(Service.AttachPlayer(player));
            }
        }
        private sealed class Store : IActiveNanoRepository
        {
            public readonly Dictionary<int, List<ActiveNanoRecord>> Rows = new();
            public Exception? Failure; public int Attempts; public int Commits; public IReadOnlyList<NanoCharacterWrite> Last = [];
            public Action<IReadOnlyList<NanoCharacterWrite>>? BeforeCommit;
            public IReadOnlyList<ActiveNanoRecord> Load(int characterId) => Rows.GetValueOrDefault(characterId) ?? [];
            public void Commit(IReadOnlyList<NanoCharacterWrite> characters)
            {
                Attempts++; if (Failure != null) throw Failure; BeforeCommit?.Invoke(characters);
                Last = characters; foreach (var character in characters) Rows[character.CharacterId] = character.ActiveNanos.ToList();
                Commits++;
            }
        }
        private sealed class Session : IZoneSession
        {
            public readonly List<MessageBody> Bodies = new(); public SessionState State { get; set; } = SessionState.InPlay;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player; public void UnbindPlayer() => Player = null;
            public void Close() => State = SessionState.Closed;
            public void Send(byte[] packet) => throw new NotSupportedException(); public void Send(Message message) => Send(message.Body);
            public void Send(MessageBody body) => Bodies.Add(body); public void Send(MessageBody body, int sender, int receiver) => Send(body);
            public void SendInitiateCompression() { } public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
        }
    }
}
