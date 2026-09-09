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
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Missions;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class NanoServiceTests
    {
        [TestMethod]
        public void Casting_is_nonblocking_and_completion_order_is_cast_finish_stat_duration()
        {
            var f = new Fixture(Buff(10)); var p = f.Player();
            Assert.IsTrue(f.Service.TryCast(p, 10, Identity.None));
            Assert.AreEqual(1, f.Session(p).Bodies.Count);
            Assert.IsInstanceOfType<CastNanoSpellMessage>(f.Session(p).Bodies[0]);
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.CurrentNano));
            f.Advance(199); f.Service.Tick(p); Assert.AreEqual(0, f.Store.Commits);
            f.Advance(1); f.Service.Tick(p);
            CollectionAssert.AreEqual(new[] { "CastNanoSpellMessage", "FinishNanoCasting", "StatMessage", "SetNanoDuration" },
                f.Session(p).Bodies.Select(Describe).ToArray());
            Assert.AreEqual(90, p.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(105, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Base));
            Assert.AreEqual(5, p.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(1, f.Store.Commits);
            var duration = (CharacterActionMessage)f.Session(p).Bodies.Last();
            Assert.AreEqual(0, duration.Unknown); Assert.AreEqual(10, duration.Target.Instance);
            Assert.AreEqual(p.Identity.Instance, duration.Parameter1); Assert.AreEqual(1000, duration.Parameter2);
        }

        [TestMethod]
        public void Pending_and_recharge_reject_duplicate_requests_without_reapplying_cost()
        {
            var f = new Fixture(Buff(10)); var p = f.Player();
            Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity));
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            f.Advance(200); f.Service.Tick(p); f.Service.Tick(p);
            Assert.AreEqual(1, f.Store.Commits); Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            f.Advance(300); Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity));
        }

        [TestMethod]
        public void Ordinary_cast_wire_body_matches_existing_typed_contract_not_triggered_npc_fields()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity));
            var body = f.Session(p).Bodies.Single();
            var resolver = new SerializerResolverBuilder<MessageBody>().Build(); var serializer = resolver.GetSerializer(body.GetType());
            using var stream = new MemoryStream();
            using var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(stream);
            serializer.Serialize(writer, new SerializationContext(resolver), body);
            CollectionAssert.AreEqual(Convert.FromHexString("25314D6D0000C35000000001000000000A0000C35000000001000000000000C35000000001"), stream.ToArray());
        }

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
            Assert.AreEqual(0, f.Session(p).Bodies.Count); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Cost_and_requirements_are_rechecked_at_completion()
        {
            var nano = Buff(10); nano.Template.Actions.Add(new ItemAction { ActionType = (int)ActionType.ToUse,
                Requirements = [new ItemRequirement { Target = (int)ItemTarget.User, StatNumber = (int)CharacterStat.Level,
                    Operator = (int)Operator.GreaterThan, Value = 10 }] });
            var f = new Fixture(nano); var p = f.Player();
            p.Stats.Set(CharacterStat.Level, 5); Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity));
            p.Stats.Set(CharacterStat.Level, 20); Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity));
            p.Stats.Set(CharacterStat.Level, 5); f.Advance(200); f.Service.Tick(p);
            Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.CurrentNano));
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
                Assert.AreEqual(0, f.Session(p).Bodies.Count); Assert.AreEqual(0, f.Store.Commits);
                Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength));
            }
        }

        [TestMethod]
        public void Self_refresh_does_not_duplicate_modifier_and_preserves_unrelated_bonus()
        {
            var f = new Fixture(Buff(10)); var p = f.Player();
            p.Stats.AddBonus(CharacterStat.Strength, 7);
            f.Cast(p, 10); int instance = f.Service.GetActive(p).Single().NanoInstance;
            f.Advance(300); f.Cast(p, 10);
            Assert.AreEqual(112, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(instance, f.Service.GetActive(p).Single().NanoInstance);
            Assert.IsTrue(f.Service.Remove(p, 10)); Assert.AreEqual(107, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.IsFalse(f.Service.Remove(p, 10)); Assert.AreEqual(0, f.Service.GetActive(p).Count);
        }

        [TestMethod]
        public void Same_strain_replacement_uses_net_ncu_and_removes_only_old_contribution()
        {
            var f = new Fixture(Buff(10), Buff(11, modifier: 9, ncu: 6)); var p = f.Player();
            p.Stats.Set(CharacterStat.MaxNCU, 6); f.Cast(p, 10); f.Advance(300); f.Cast(p, 11);
            Assert.AreEqual(109, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(6, p.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(11, f.Service.GetActive(p).Single().NanoId);
            Assert.AreEqual(10, f.Session(p).Bodies.OfType<BuffMessage>().Single().NanoProgram.Instance);
        }

        [TestMethod]
        public void Expiration_reverses_once_and_persists_removal_before_packet()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Cast(p, 10);
            f.Advance(10000); f.Service.Tick(p); f.Service.Tick(p);
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength)); Assert.AreEqual(0, f.Service.GetActive(p).Count);
            Assert.AreEqual(0, f.Store.Rows[p.Identity.Instance].Count); Assert.AreEqual(2, f.Store.Commits);
            Assert.AreEqual(1, f.Session(p).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Persistence_failure_does_not_apply_cost_buff_or_success_packets()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Store.Failure = new IOException("rollback"); f.Cast(p, 10);
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.Strength)); Assert.IsFalse(p.IsPersistenceQuarantined);
            Assert.AreEqual(1, f.Session(p).Bodies.Count); Assert.AreEqual(0, f.Service.GetActive(p).Count);
        }

        [TestMethod]
        public void Indeterminate_commit_quarantines_closes_and_never_retries_memory_projection()
        {
            var f = new Fixture(Buff(10)); var p = f.Player();
            f.Store.Failure = new DatabaseCommitOutcomeUnknownException(new IOException("transport")); f.Cast(p, 10);
            Assert.IsTrue(p.IsPersistenceQuarantined); Assert.AreEqual(SessionState.Closed, f.Session(p).State);
            Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity)); f.Service.Tick(p); Assert.AreEqual(1, f.Store.Attempts);
        }

        [TestMethod]
        public void Removal_failure_preserves_active_contribution_and_no_removal_packet()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Cast(p, 10); f.Store.Failure = new IOException();
            Assert.IsFalse(f.Service.Remove(p, 10)); Assert.AreEqual(105, p.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(1, f.Service.GetActive(p).Count); Assert.AreEqual(0, f.Session(p).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Changed_session_cannot_complete_old_cast_and_stale_close_does_not_cancel_new_one()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); var old = f.Session(p);
            Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity)); f.ReplaceSession(p);
            f.Advance(200); f.Service.Tick(p); Assert.AreEqual(0, f.Store.Commits);
            Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity)); f.Service.Cancel(p, old);
            f.Advance(200); f.Service.Tick(p); Assert.AreEqual(1, f.Store.Commits);
        }

        [TestMethod]
        public void Jump_cancellation_is_idempotent_and_old_actor_cannot_mutate_new_owner()
        {
            var f = new Fixture(Buff(10)); var p = f.Player();
            Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity)); p.InterruptTimedActions(TimedActionInterrupt.Jump);
            f.Advance(200); f.Service.Tick(p); Assert.AreEqual(0, f.Store.Commits);
            f.Service.DetachPlayer(p); var replacement = f.Player();
            Assert.IsFalse(f.Service.TryCast(p, 10, p.Identity)); f.Service.DetachPlayer(p);
            Assert.IsTrue(f.Service.TryCast(replacement, 10, replacement.Identity));
        }

        [TestMethod]
        public void Death_or_playfield_change_after_start_cannot_land_a_cast()
        {
            foreach (bool dead in new[] { false, true })
            {
                var f = new Fixture(Buff(10)); var p = f.Player();
                Assert.IsTrue(f.Service.TryCast(p, 10, p.Identity));
                if (dead) p.OnDeath();
                else p.Playfield = (Playfield)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                f.Advance(200); f.Service.Tick(p); Assert.AreEqual(0, f.Store.Commits);
            }
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
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Cast(p, 10);
            Assert.IsFalse(f.Service.TryRemove(p, new CharacterActionMessage { Action = CharacterActionType.RemoveFriendlyNano, Target = Identity.None }));
            Assert.IsTrue(f.Service.TryRemove(p, new CharacterActionMessage { Action = CharacterActionType.RemoveFriendlyNano,
                Target = Identity.None, Parameter1 = f.Service.GetActive(p).Single().NanoInstance }));
            Assert.AreEqual(0, f.Service.GetActive(p).Count);
        }

        [TestMethod]
        public void Equipment_rebase_reapplies_only_current_nano_bonus()
        {
            var f = new Fixture(Buff(10)); var p = f.Player(); f.Cast(p, 10);
            p.Stats.ClearBonuses(); p.Stats.AddBonus(CharacterStat.Strength, 9); f.Service.ReapplyBonusesAfterRebase(p);
            Assert.AreEqual(114, p.Stats.GetOrZero(CharacterStat.Strength));
            f.Service.Remove(p, 10); Assert.AreEqual(109, p.Stats.GetOrZero(CharacterStat.Strength));
        }

        [TestMethod]
        public void Instant_heal_uses_declared_inclusive_range_and_caps_without_duration_row()
        {
            var nano = Buff(10, duration: 0); nano.Template.SpellList[EventType.OnUse] = [Heal(300, 350)];
            var f = new Fixture(nano); var p = f.Player(); f.Cast(p, 10);
            Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(0, f.Service.GetActive(p).Count);
            Assert.AreEqual(1, f.RandomCalls); Assert.AreEqual((300, 351), f.LastRange);
            Assert.AreEqual(0, f.Session(p).Bodies.OfType<CharacterActionMessage>().Count(a => a.Action == CharacterActionType.SetNanoDuration));
        }

        [TestMethod]
        public void Mana_heal_is_planned_after_cost_in_the_same_durable_write()
        {
            var nano = Buff(10, duration: 0); var heal = Heal(5); heal.Arguments[0] = (int)CharacterStat.CurrentNano;
            nano.Template.SpellList[EventType.OnUse] = [heal]; var f = new Fixture(nano); var p = f.Player(); f.Cast(p, 10);
            Assert.AreEqual(95, p.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(95, f.Store.Last.Single().BaseStats.Single(s => s.StatId == (int)CharacterStat.CurrentNano).StatValue);
        }

        [TestMethod]
        public void Ordered_modifier_then_heal_uses_new_maximum_and_refresh_does_not_double_old_bonus()
        {
            var nano = Buff(10, modifier: 100);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.MaxHealth;
            nano.Template.SpellList[EventType.OnUse].Add(Heal(100));
            var f = new Fixture(nano); var p = f.Player(); p.Stats.Set(CharacterStat.Health, 500);
            f.Cast(p, 10); Assert.AreEqual(600, p.Stats.GetOrZero(CharacterStat.Health));
            f.Advance(300); f.Cast(p, 10);
            Assert.AreEqual(600, p.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(600, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        [TestMethod]
        public void Attack_timing_uses_existing_init_softcap_aggdef_and_declared_cap()
        {
            var nano = Buff(10); nano.Template.Stats[(CharacterStat)294] = 600;
            nano.Template.Stats[(CharacterStat)523] = 100;
            Assert.IsTrue(nano.TryCalculateAttackTime(25, 0, out int zero)); Assert.AreEqual(600, zero);
            Assert.IsTrue(nano.TryCalculateAttackTime(25, 600, out int init)); Assert.AreEqual(300, init);
            Assert.IsTrue(nano.TryCalculateAttackTime(100, 1200, out int capped)); Assert.AreEqual(100, capped);
            nano.Template.Stats[(CharacterStat)294] = 2000;
            Assert.IsTrue(nano.TryCalculateAttackTime(25, 1500, out int soft)); Assert.AreEqual(1350, soft);
            nano.Template.Stats[(CharacterStat)407] = -1; Assert.IsFalse(nano.TryCalculateAttackTime(25, 0, out _));
        }

        [TestMethod]
        public void Same_playfield_target_cast_commits_both_owners_under_ordered_snapshot_gates()
        {
            var f = new Fixture(Buff(10)); var caster = f.Player(2); var target = f.Player(1);
            using var services = new ServiceCollection().AddSingleton(new PlayfieldLocality(4582, null))
                .AddSingleton(new WorldSimulationAccess()).BuildServiceProvider();
            var playfield = (Playfield)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, services);
            caster.Playfield = target.Playfield = playfield; target.Position = new Vector3(5, 0, 0);
            f.Store.BeforeCommit = writes =>
            {
                Assert.IsTrue(Monitor.IsEntered(caster.PersistenceGate)); Assert.IsTrue(Monitor.IsEntered(target.PersistenceGate));
                CollectionAssert.AreEqual(new[] { 1, 2 }, writes.Select(w => w.CharacterId).ToArray());
                Assert.AreEqual(100, caster.Stats.GetOrZero(CharacterStat.CurrentNano));
                Assert.AreEqual(100, target.Stats.GetOrZero(CharacterStat.Strength));
            };
            Assert.IsTrue(f.Service.TryCast(caster, 10, target.Identity)); f.Advance(200); f.Service.Tick(caster);
            Assert.AreEqual(90, caster.Stats.GetOrZero(CharacterStat.CurrentNano)); Assert.AreEqual(100, target.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(105, target.Stats.GetOrZero(CharacterStat.Strength)); Assert.AreEqual(100, caster.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(2, f.Store.Last.Count);
        }

        [TestMethod]
        public void Cross_playfield_out_of_range_and_target_ownership_change_fail_closed()
        {
            var f = new Fixture(Buff(10)); var caster = f.Player(1); var target = f.Player(2);
            using var services = new ServiceCollection().AddSingleton(new PlayfieldLocality(4582, null))
                .AddSingleton(new WorldSimulationAccess()).BuildServiceProvider();
            var playfield = (Playfield)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, services);
            caster.Playfield = playfield;
            Assert.IsFalse(f.Service.TryCast(caster, 10, target.Identity));
            target.Playfield = playfield; target.Position = new Vector3(21, 0, 0);
            Assert.IsFalse(f.Service.TryCast(caster, 10, target.Identity));
            target.Position = new Vector3(20, 0, 0); Assert.IsTrue(f.Service.TryCast(caster, 10, target.Identity));
            f.ReplaceSession(target); f.Advance(200); f.Service.Tick(caster); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Overview_map_flag_is_committed_with_ncu_and_clears_only_after_actual_removal()
        {
            var f = new Fixture([new OverviewMapNanoSpecialization()], Buff(OverviewMapNanoSpecialization.NanoId));
            var p = f.Player(); f.Cast(p, OverviewMapNanoSpecialization.NanoId);
            Assert.AreEqual(OverviewMapNanoSpecialization.MapsMask, p.Stats.GetOrZero(CharacterStat.MapsC));
            Assert.AreEqual(OverviewMapNanoSpecialization.MapsMask,
                f.Store.Last.Single().BaseStats.Single(s => s.StatId == (int)CharacterStat.MapsC).StatValue);
            StatMessage map = f.Session(p).Bodies.OfType<StatMessage>().Single(s => s.Stats.Any(t => t.Value1 == CharacterStat.MapsC));
            Assert.AreEqual(0, map.Unknown); Assert.AreEqual(1, map.Stats.Length);
            f.Session(p).Bodies.Clear(); Assert.IsTrue(f.Service.Remove(p, OverviewMapNanoSpecialization.NanoId));
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.MapsC));
            Assert.AreEqual(0, f.Store.Last.Single().ActiveNanos.Count);
            Assert.AreEqual(0, f.Store.Last.Single().BaseStats.Single(s => s.StatId == (int)CharacterStat.MapsC).StatValue);
            Assert.IsInstanceOfType<BuffMessage>(f.Session(p).Bodies[0]);
            var clear = (StatMessage)f.Session(p).Bodies.Last(); Assert.AreEqual(0, clear.Unknown);
            Assert.AreEqual(CharacterStat.MapsC, clear.Stats.Single().Value1); Assert.AreEqual(0u, clear.Stats.Single().Value2);
        }

        [TestMethod]
        public void Overview_login_clears_stale_unlock_without_buff_remove_and_restores_only_active_mask()
        {
            var f = new Fixture([new OverviewMapNanoSpecialization()], Buff(OverviewMapNanoSpecialization.NanoId));
            var p = f.Player(attach: false); p.Stats.Set(CharacterStat.MapsC, OverviewMapNanoSpecialization.MapsMask);
            Assert.IsTrue(f.Service.AttachPlayer(p)); Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.MapsC));
            Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            f.Service.DetachPlayer(p);
            f.Store.Rows[1] = [new ActiveNanoRecord(OverviewMapNanoSpecialization.NanoId, 7, 8, 1000, f.Utc.AddSeconds(4).Ticks)];
            var restored = f.Player(); Assert.AreEqual(OverviewMapNanoSpecialization.MapsMask, restored.Stats.GetOrZero(CharacterStat.MapsC));
            f.Service.RefreshPlayer(restored); Assert.AreEqual(0, f.Session(restored).Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Overview_transaction_failure_never_unlocks_map_in_memory_or_on_wire()
        {
            var f = new Fixture([new OverviewMapNanoSpecialization()], Buff(OverviewMapNanoSpecialization.NanoId));
            var p = f.Player(); f.Store.Failure = new IOException(); f.Cast(p, OverviewMapNanoSpecialization.NanoId);
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.MapsC)); Assert.AreEqual(0, f.Service.GetActive(p).Count);
            Assert.AreEqual(0, f.Session(p).Bodies.OfType<StatMessage>().Count());
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
            f.Cast(p, 10);
            Assert.AreEqual(560, p.Stats.GetOrZero(CharacterStat.MaxHealth)); Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(145, p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy)); Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            p.NanoRuntime = f.Service; p.Rebase();
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 1, 10), p.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 1, 30), p.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(MaxNanoCalculator.Compute(1, 1, 1, 1, 10), p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            Assert.AreEqual(MaxNanoCalculator.Compute(1, 1, 1, 1, 25), p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
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
            f.Cast(p, 10); f.Advance(300); f.Cast(p, 11); Assert.AreEqual(590, p.Stats.GetOrZero(CharacterStat.MaxHealth));
            f.Service.Remove(p, 10); Assert.AreEqual(530, p.Stats.GetOrZero(CharacterStat.MaxHealth));
            f.Service.Remove(p, 11); Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        [TestMethod]
        public void Body_development_then_heal_uses_projected_derived_maximum()
        {
            var nano = Buff(10, modifier: 20);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.BodyDevelopment;
            nano.Template.SpellList[EventType.OnUse].Add(Heal(100));
            var f = new Fixture(nano); var p = f.Player(); p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.Health, 500);
            f.Cast(p, 10); Assert.AreEqual(560, p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(560, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        [TestMethod]
        public void Scaling_modify_uses_exact_existing_flat_contribution_and_reverses_once()
        {
            var nano = Buff(10, modifier: 240);
            nano.Template.SpellList[EventType.OnUse][0].FunctionType = (int)FunctionType.ScalingModify;
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)CharacterStat.RunSpeed;
            var f = new Fixture(nano); var p = f.Player(); p.Stats.Set(CharacterStat.RunSpeed, 100);
            f.Cast(p, 10); Assert.AreEqual(340, p.Stats.GetOrZero(CharacterStat.RunSpeed));
            f.Service.Remove(p, 10); Assert.AreEqual(100, p.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.IsFalse(f.Service.Remove(p, 10)); Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Bonus));
        }

        [TestMethod]
        public void Prospective_nano_projection_uses_future_stats_without_mutating_actor_store_or_wire()
        {
            var f = new Fixture(ResourceBonusNano()); var p = f.Player();
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            f.Cast(p, 10); p.NanoRuntime = f.Service; p.Rebase();
            var before = p.Stats.GetEntries().ToArray(); int messages = f.Session(p).Bodies.Count, commits = f.Store.Commits;
            var future = new StatCollection();
            foreach (var entry in p.Stats.GetEntries()) future.Set(entry.Stat, entry.Base);
            future.Set(CharacterStat.Level, 15); future.Set(CharacterStat.TitleLevel, 2);
            p.Inventory.ApplyWearBonuses(future);
            Assert.IsTrue(MaxHealthCalculator.TryCompute(future, out int baseHealth));
            Assert.IsTrue(MaxNanoCalculator.TryCompute(future, out int baseNano));
            future.Set(CharacterStat.MaxHealth, baseHealth); future.Set(CharacterStat.MaxNanoEnergy, baseNano);
            f.Service.ProjectBonusesAfterRebase(p, future);
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 2, 15, 30) + 40, future.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(MaxNanoCalculator.Compute(1, 1, 2, 15, 25) + 30, future.GetOrZero(CharacterStat.MaxNanoEnergy));
            Assert.AreEqual(baseHealth, future.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
            Assert.AreEqual(baseNano, future.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base));
            Assert.AreEqual(5, future.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            Assert.AreEqual(7, future.GetOrZero(CharacterStat.CurrentNano, StatDetail.Bonus));
            Assert.IsTrue(before.SequenceEqual(p.Stats.GetEntries()));
            Assert.AreEqual(messages, f.Session(p).Bodies.Count); Assert.AreEqual(commits, f.Store.Commits);
            Assert.ThrowsExactly<ArgumentException>(() => f.Service.ProjectBonusesAfterRebase(p, p.Stats));
            Assert.ThrowsExactly<InvalidOperationException>(() => f.Service.ProjectBonusesAfterRebase(TestWorld.CreatePlayer(p.Identity.Instance), new()));
        }

        [TestMethod]
        public void Direct_xp_refill_durable_plan_equals_published_real_active_bonus_actor()
        {
            var f = new Fixture(ResourceBonusNano()); var p = f.Player();
            p.Stats.Set(CharacterStat.Level, 1); p.Stats.Set(CharacterStat.XP, 0); p.Stats.Set(CharacterStat.IP, 1500);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            f.Cast(p, 10); p.NanoRuntime = f.Service; p.Rebase();
            var plan = DirectXpRewardPlan.Create(p, 1500);
            Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.Level));
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 2, 10), plan.Stats[CharacterStat.MaxHealth]);
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 2, 30) + 40 - 5, plan.Stats[CharacterStat.Health]);
            Assert.AreEqual(MaxNanoCalculator.Compute(1, 1, 1, 2, 25) + 30 - 7, plan.Stats[CharacterStat.CurrentNano]);
            plan.PublishAfterCommit(p);
            foreach (CharacterStat stat in new[] { CharacterStat.MaxHealth, CharacterStat.MaxNanoEnergy,
                CharacterStat.Health, CharacterStat.CurrentNano })
                Assert.AreEqual(plan.Stats[stat], p.Stats.GetOrZero(stat, StatDetail.Base), stat.ToString());
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxHealth), p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy), p.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(5, p.Stats.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            Assert.AreEqual(7, p.Stats.GetOrZero(CharacterStat.CurrentNano, StatDetail.Bonus));
        }

        [TestMethod]
        public void Direct_xp_plans_equipment_conditions_at_prospective_level_before_durable_refill()
        {
            var p = TestWorld.CreatePlayer(41); p.Stats.Set(CharacterStat.Level, 1); p.Stats.Set(CharacterStat.XP, 0);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            var worn = TestWorld.CreateItem(instanceId: 77);
            worn.SpellList[EventType.OnWear] = new[] { (CharacterStat.BodyDevelopment, 10), (CharacterStat.MaxHealth, 40),
                (CharacterStat.Health, 5), (CharacterStat.NanoPool, 15), (CharacterStat.MaxNanoEnergy, 30), (CharacterStat.CurrentNano, 7) }
                .Select(pair => new ItemSpell { FunctionType = (int)FunctionType.Modify, Target = (int)ItemTarget.Wearer,
                    Arguments = [(int)pair.Item1, pair.Item2], Requirements = [new() { Target = (int)ItemTarget.Self,
                        StatNumber = (int)CharacterStat.Level, Operator = (int)Operator.GreaterThan, Value = 1 }] }).ToList();
            Assert.IsTrue(p.Inventory.Armor.Add(p.Inventory.Armor.Offset, worn)); p.Rebase();
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            var plan = DirectXpRewardPlan.Create(p, 1500);
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 2, 20), plan.Stats[CharacterStat.MaxHealth]);
            Assert.AreEqual(plan.Stats[CharacterStat.MaxHealth] + 40 - 5, plan.Stats[CharacterStat.Health]);
            plan.PublishAfterCommit(p);
            foreach (CharacterStat stat in new[] { CharacterStat.MaxHealth, CharacterStat.MaxNanoEnergy,
                CharacterStat.Health, CharacterStat.CurrentNano })
                Assert.AreEqual(plan.Stats[stat], p.Stats.GetOrZero(stat, StatDetail.Base), stat.ToString());
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxHealth), p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy), p.Stats.GetOrZero(CharacterStat.CurrentNano));
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
            f.Cast(p, 10);
            // Skill108 is 20% Strength +60% Agility +20% Sense. floor(4.2/4)-floor(3.6/4)=1.
            Assert.AreEqual(51, p.Stats.GetOrZero((CharacterStat)108));
            Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108, StatDetail.Base));
            f.Service.DetachPlayer(p); Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108));
            f.Session(p).Bodies.Clear(); Assert.IsTrue(f.Service.AttachPlayer(p));
            Assert.AreEqual(51, p.Stats.GetOrZero((CharacterStat)108)); Assert.AreEqual(0, f.Session(p).Bodies.Count);
            Assert.AreEqual(1, f.Store.Commits); Assert.IsTrue(f.Service.Remove(p, 10));
            Assert.AreEqual(50, p.Stats.GetOrZero((CharacterStat)108));
        }

        [TestMethod]
        public void Multiple_attribute_nanos_share_fractional_trickle_without_double_apply_or_reverse()
        {
            var first = AbilityBuff(10, CharacterStat.Stamina, 2);
            var second = AbilityBuff(11, CharacterStat.Stamina, 2); second.Template.Stats[(CharacterStat)75] = 8;
            var f = new Fixture(first, second); var p = f.Player(); p.Stats.Set(CharacterStat.Stamina, 2);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10);
            f.Cast(p, 10); Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            f.Advance(300); f.Cast(p, 11);
            Assert.AreEqual(6, p.Stats.GetOrZero(CharacterStat.Stamina));
            Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.IsTrue(f.Service.Remove(p, 10));
            Assert.AreEqual(1, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.IsTrue(f.Service.Remove(p, 11)); Assert.IsFalse(f.Service.Remove(p, 11));
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            Assert.AreEqual(500, p.Stats.GetOrZero(CharacterStat.MaxHealth));
        }

        [TestMethod]
        public void Attribute_then_heal_uses_owned_body_trickle_and_expiry_preserves_equipment_baseline()
        {
            var nano = AbilityBuff(10, CharacterStat.Stamina, 1);
            nano.Template.SpellList[EventType.OnUse].Add(Heal(100));
            var f = new Fixture(nano); var p = f.Player();
            p.Stats.Set(CharacterStat.Stamina, 2); p.Stats.AddBonus(CharacterStat.Stamina, 1);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.Health, 500);
            f.Cast(p, 10);
            Assert.AreEqual(503, p.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(503, p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(503, f.Store.Last.Single().BaseStats.Single(s => s.StatId == (int)CharacterStat.Health).StatValue);
            p.Stats.ClearBonuses(); p.Stats.AddBonus(CharacterStat.Stamina, 2);
            f.Service.ReapplyBonusesAfterRebase(p);
            Assert.AreEqual(5, p.Stats.GetOrZero(CharacterStat.Stamina));
            Assert.AreEqual(0, p.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Bonus));
            // Only the nano delta is recomputed; this does not claim baseline equipment trickle ownership.
            f.Advance(10000); f.Service.Tick(p); f.Service.Tick(p);
            Assert.AreEqual(4, p.Stats.GetOrZero(CharacterStat.Stamina));
            Assert.AreEqual(2, p.Stats.GetOrZero(CharacterStat.Stamina, StatDetail.Bonus));
            Assert.AreEqual(10, p.Stats.GetOrZero(CharacterStat.BodyDevelopment));
        }

        [TestMethod]
        public void Attribute_trickle_downstream_health_nano_and_prospective_xp_match_published_actor()
        {
            var nano = AbilityBuff(10, CharacterStat.Stamina, 4);
            nano.Template.SpellList[EventType.OnUse].Add(new() { FunctionType = (int)FunctionType.Modify,
                Target = (int)ItemTarget.Target, Arguments = [(int)CharacterStat.Psychic, 4] });
            var f = new Fixture(nano); var p = f.Player();
            foreach (var stat in AbilityStats) p.Stats.Set(stat, 3);
            p.Stats.Set(CharacterStat.BodyDevelopment, 10); p.Stats.Set(CharacterStat.NanoPool, 10);
            p.Stats.Set(CharacterStat.Level, 1); p.Stats.Set(CharacterStat.XP, 0); p.Stats.Set(CharacterStat.IP, 1500);
            f.Cast(p, 10); p.NanoRuntime = f.Service; p.Rebase();
            Assert.AreEqual(11, p.Stats.GetOrZero(CharacterStat.BodyDevelopment));
            Assert.AreEqual(11, p.Stats.GetOrZero(CharacterStat.NanoPool));
            int commits = f.Store.Commits; var before = p.Stats.GetEntries().ToArray();
            var plan = DirectXpRewardPlan.Create(p, 1500);
            Assert.IsTrue(before.SequenceEqual(p.Stats.GetEntries())); Assert.AreEqual(commits, f.Store.Commits);
            Assert.AreEqual(MaxHealthCalculator.Compute(1, 1, 1, 2, 11), plan.Stats[CharacterStat.Health]);
            Assert.AreEqual(MaxNanoCalculator.Compute(1, 1, 1, 2, 11), plan.Stats[CharacterStat.CurrentNano]);
            plan.PublishAfterCommit(p);
            Assert.AreEqual(plan.Stats[CharacterStat.Health], p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxHealth), p.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(p.Stats.GetOrZero(CharacterStat.MaxNanoEnergy), p.Stats.GetOrZero(CharacterStat.CurrentNano));
        }

        private static readonly CharacterStat[] AbilityStats = [CharacterStat.Strength, CharacterStat.Agility,
            CharacterStat.Stamina, CharacterStat.Intelligence, CharacterStat.Sense, CharacterStat.Psychic];
        private static NanoDefinition AbilityBuff(int id, CharacterStat stat, int amount)
        {
            var nano = Buff(id, modifier: amount);
            nano.Template.SpellList[EventType.OnUse][0].Arguments[0] = (int)stat;
            return nano;
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
            public Fixture(params NanoDefinition[] nanos) : this([], nanos) { }
            public Fixture(INanoSpecialization[] specialties, params NanoDefinition[] nanos)
            {
                _nanos = nanos;
                Service = new NanoService(new NanoCatalog(nanos), Store, specialties, utcNow: () => Utc,
                    monotonicMilliseconds: () => Milliseconds, next: (min, max) => { RandomCalls++; LastRange = (min, max); return max - 1; });
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
            public void Cast(Player player, int nano) { Assert.IsTrue(Service.TryCast(player, nano, player.Identity)); Advance(200); Service.Tick(player); }
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
