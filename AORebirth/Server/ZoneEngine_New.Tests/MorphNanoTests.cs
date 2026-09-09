namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class MorphNanoTests
    {
        [TestMethod]
        [DataRow(270542, 270497, 150, 360000)]
        [DataRow(288546, 288538, 150, 1440000)]
        [DataRow(281569, 281568, 1000, 360000)]
        [DataRow(82835, 30365, 1200, 1440000)]
        public void Real_catalog_morph_commits_declared_lifetime_and_owned_modifiers(int id, int shape, int speed, int duration)
        {
            var f = new Fixture(id); f.Cast();
            Assert.AreEqual(shape, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData, StatDetail.Base));
            Assert.AreEqual(100 + speed, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.AreEqual(duration, f.Store.Rows.Single().DurationCentiseconds);
            Assert.AreEqual(f.Nano.NcuCost, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(1000 - f.Nano.NanoCost, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.IsTrue(f.Service.IsFightingRestricted(f.Player));
            Assert.AreEqual(1, f.Store.Commits);
            Assert.IsTrue(f.Session.Bodies.OfType<CharacterActionMessage>().Any(m => m.Action == CharacterActionType.FinishNanoCasting));
            Assert.IsTrue(f.Session.Bodies.Last() is CharacterActionMessage { Action: CharacterActionType.SetNanoDuration });
        }

        [TestMethod]
        public void Real_catalog_requirement_failure_and_unknown_head_branch_do_not_begin_cast()
        {
            var f = new Fixture(270542);
            f.Player.Stats.Set((CharacterStat)166, 80); // Exact GreaterThan 80, not >=.
            Assert.IsFalse(f.Service.TryCast(f.Player, f.Nano.Id, f.Player.Identity));
            f.Player.Stats.Set((CharacterStat)166, 81); f.Player.Stats.Set((CharacterStat)12, 999);
            Assert.IsFalse(f.Service.TryCast(f.Player, f.Nano.Id, f.Player.Identity));
            Assert.AreEqual(0, f.Session.Bodies.Count); Assert.AreEqual(0, f.Store.Commits);
        }

        [TestMethod]
        public void Failed_transaction_does_not_publish_morph_modifiers_or_completion()
        {
            var f = new Fixture(270542); f.Store.Fail = true; f.Cast();
            Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.AreEqual(1000, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.IsFalse(f.Service.IsFightingRestricted(f.Player));
            Assert.AreEqual(1, f.Session.Bodies.Count); Assert.IsInstanceOfType<CastNanoSpellMessage>(f.Session.Bodies[0]);
        }

        [TestMethod]
        public void Remove_reverses_only_owned_overlay_and_preserves_original_base()
        {
            var f = new Fixture(270542); f.Player.Stats.Set(CharacterStat.CATMesh, 111);
            f.Player.Stats.Set(CharacterStat.DisplayCATMesh, 222); f.Cast(); f.Session.Bodies.Clear();
            Assert.IsTrue(f.Service.Remove(f.Player, f.Nano.Id));
            Assert.AreEqual(111, f.Player.Stats.GetOrZero(CharacterStat.CATMesh));
            Assert.AreEqual(222, f.Player.Stats.GetOrZero(CharacterStat.DisplayCATMesh));
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.IsFalse(f.Service.IsFightingRestricted(f.Player)); Assert.AreEqual(0, f.Store.Rows.Count);
            var clears = f.Session.Bodies.OfType<StatMessage>().Where(m => m.Stats.Length == 1).Take(3).ToArray();
            CollectionAssert.AreEqual(new[] { CharacterStat.MonsterData, CharacterStat.IsVehicle, CharacterStat.Mesh },
                clears.Select(m => m.Stats[0].Value1).ToArray());
            Assert.IsTrue(clears.All(m => m.Unknown == 0 && m.Stats[0].Value2 == 0));
            Assert.AreEqual(1, f.Session.Bodies.OfType<BuffMessage>().Count());
            Assert.IsFalse(f.Service.Remove(f.Player, f.Nano.Id));
        }

        [TestMethod]
        public void Actual_declared_expiry_reverses_morph_and_flight_once()
        {
            var f = new Fixture(281569); f.Cast(); f.Player.Motor.ApplyAction(MovementAction.SwitchToFly);
            f.Now = f.Now.AddMilliseconds((long)f.Nano.DurationCentiseconds * 10); f.Session.Bodies.Clear();
            f.Service.Tick(f.Player); f.Service.Tick(f.Player);
            Assert.AreEqual(0, f.Store.Rows.Count); Assert.AreEqual(2, f.Store.Commits);
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            Assert.AreEqual(MovementState.Run, f.Player.Motor.State);
            Assert.AreEqual(1, f.Session.Bodies.OfType<BuffMessage>().Count());
        }

        [TestMethod]
        public void Rebase_reapplies_overlay_then_removal_exposes_latest_equipment_bonus()
        {
            var f = new Fixture(270542); f.Cast(); f.Player.Stats.ClearBonuses();
            f.Player.Stats.AddBonus(CharacterStat.CATMesh, 55); f.Player.Stats.AddBonus(CharacterStat.RunSpeed, 20);
            f.Service.ReapplyBonusesAfterRebase(f.Player);
            Assert.AreEqual(270497, f.Player.Stats.GetOrZero(CharacterStat.CATMesh));
            Assert.AreEqual(270, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            f.Service.Remove(f.Player, f.Nano.Id);
            Assert.AreEqual(55, f.Player.Stats.GetOrZero(CharacterStat.CATMesh));
            Assert.AreEqual(120, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
        }

        [TestMethod]
        public void Hoverboard_flight_refresh_obeys_current_expansion_authority_without_fake_spelllist()
        {
            var f = new Fixture(281569); f.Cast(); Assert.AreEqual(1, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            f.Player.Stats.Set((CharacterStat)531, 1); f.Service.RefreshPlayer(f.Player);
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            f.Player.Stats.Set((CharacterStat)531, 0); f.Service.RefreshPlayer(f.Player);
            Assert.AreEqual(1, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            Assert.AreEqual(0, f.Session.Raw.Count); Assert.AreEqual(1, f.Store.Commits);
        }

        [TestMethod]
        public void Fly_request_cannot_grant_authority_and_removal_demotes_to_previous_walk_mode()
        {
            var f = new Fixture(281569); f.Player.Motor.ApplyAction(MovementAction.SwitchToWalk);
            f.Player.Motor.ApplyAction(MovementAction.SwitchToFly); Assert.AreEqual(MovementState.Walk, f.Player.Motor.State);
            f.Cast(); f.Player.Motor.ApplyAction(MovementAction.SwitchToFly); Assert.AreEqual(MovementState.Fly, f.Player.Motor.State);
            f.Player.Motor.ApplyAction(MovementAction.ElevateUpStart);
            Assert.IsTrue(f.Service.Remove(f.Player, f.Nano.Id));
            Assert.AreEqual(MovementState.Walk, f.Player.Motor.State);
            Assert.AreEqual(0, (int)(f.Player.Motor.MovementFlags & (MovementFlags.ElevateUp | MovementFlags.ElevateDown)));
            Assert.AreEqual((int)MovementState.Walk, f.Player.Stats.GetOrZero(CharacterStat.CurrentMovementMode));
        }

        [TestMethod]
        public void Temporary_rebase_clear_does_not_demote_valid_flight_but_sl_does()
        {
            var f = new Fixture(281569); f.Cast(); f.Player.Motor.ApplyAction(MovementAction.SwitchToFly);
            f.Player.Stats.ClearBonuses(); f.Service.ReapplyBonusesAfterRebase(f.Player);
            f.Player.Motor.RefreshFlightAuthority(); Assert.AreEqual(MovementState.Fly, f.Player.Motor.State);
            f.Service.RefreshPlayer(f.Player); Assert.AreEqual(MovementState.Fly, f.Player.Motor.State);
            f.Player.Stats.Set((CharacterStat)531, 1); f.Service.RefreshPlayer(f.Player);
            Assert.AreEqual(MovementState.Run, f.Player.Motor.State);
            f.Player.Motor.ApplyAction(MovementAction.SwitchToFly); Assert.AreEqual(MovementState.Run, f.Player.Motor.State);
        }

        [TestMethod]
        public void Restore_projects_before_full_without_replaying_cast_or_charging_mana()
        {
            var f = new Fixture(270542, attach: false);
            f.Store.Rows.Add(new(f.Nano.Id, f.Nano.Strain, 9, f.Nano.DurationCentiseconds,
                f.Now.AddMilliseconds((long)f.Nano.DurationCentiseconds * 10).Ticks));
            Assert.IsTrue(f.Service.AttachPlayer(f.Player));
            Assert.AreEqual(270497, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(1000, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(0, f.Session.Bodies.Count); Assert.AreEqual(0, f.Store.Commits);
            f.Service.DetachPlayer(f.Player);
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(1, f.Store.Rows.Count); Assert.IsFalse(f.Service.IsFightingRestricted(f.Player));
        }

        [TestMethod]
        public void Ambiguous_legacy_persisted_morph_base_is_preserved_not_cleared()
        {
            var f = new Fixture(270542, attach: false); f.Player.Stats.Set(CharacterStat.MonsterData, 270497);
            f.Store.Rows.Add(new(f.Nano.Id, f.Nano.Strain, 9, f.Nano.DurationCentiseconds,
                f.Now.AddMilliseconds((long)f.Nano.DurationCentiseconds * 10).Ticks));
            Assert.IsFalse(f.Service.AttachPlayer(f.Player)); Assert.AreEqual(0, f.Store.Commits);
            Assert.AreEqual(270497, f.Player.Stats.GetOrZero(CharacterStat.MonsterData, StatDetail.Base));
            Assert.AreEqual(1, f.Store.Rows.Count);
        }

        [TestMethod]
        public void Multiple_durable_vehicle_authorities_and_stale_actor_are_rejected()
        {
            var f = new Fixture(270542, attach: false);
            Assert.IsFalse(f.Morph.IsValidActiveSet(f.Player, [new(270542, 0, 1, 1000, 1), new(281569, 914, 2, 1000, 1)]));
            Assert.IsTrue(f.Service.AttachPlayer(f.Player));
            var other = TestWorld.CreatePlayer(f.Player.Identity.Instance);
            Assert.IsFalse(f.Service.AttachPlayer(other)); Assert.IsFalse(f.Service.IsFightingRestricted(other));
            Assert.IsTrue(f.Morph.Handles(82835)); // Its nested child is now explicitly planned and tested.
        }

        [TestMethod]
        public void Sparrow_low_ncu_child_finishes_once_without_legacy_ghost_duration()
        {
            var f = new Fixture(82835); f.Cast(); f.Service.Tick(f.Player);
            Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(82835, f.Store.Rows.Single().NanoId);
            CollectionAssert.AreEqual(new[] { 82835, 273292 }, f.Session.Bodies.OfType<CastNanoSpellMessage>().Select(m => m.NanoId).ToArray());
            var finished = f.Session.Bodies.OfType<CharacterActionMessage>().Where(m => m.Action == CharacterActionType.FinishNanoCasting).ToArray();
            CollectionAssert.AreEqual(new[] { 82835, 273292 }, finished.Select(m => m.Parameter2).ToArray());
            Assert.IsTrue(finished.All(m => m.Parameter1 == 1 && m.Target == Identity.None && m.Unknown == 0));
            var duration = f.Session.Bodies.OfType<CharacterActionMessage>().Single(m => m.Action == CharacterActionType.SetNanoDuration);
            Assert.AreEqual(82835, duration.Target.Instance); Assert.AreEqual(8, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(989, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.IsFalse(f.Service.TryCast(f.Player, 82835, f.Player.Identity)); // Parent recharge, not child's zero recharge.
        }

        [TestMethod]
        public void Sparrow_high_ncu_child_and_parent_commit_together_then_child_expires_without_termination_script()
        {
            var f = new Fixture(82835); f.Player.Stats.Set(CharacterStat.MaxNCU, 1007); f.Cast();
            Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(2, f.Store.Rows.Count);
            Assert.AreEqual(1007, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
            var child = f.Store.Rows.Single(r => r.NanoId == 273292);
            Assert.AreEqual(1, child.DurationCentiseconds); Assert.AreEqual(0, child.Strain);
            Assert.AreNotEqual(child.NanoInstance, f.Store.Rows.Single(r => r.NanoId == 82835).NanoInstance);
            var durations = f.Session.Bodies.OfType<CharacterActionMessage>().Where(m => m.Action == CharacterActionType.SetNanoDuration).ToArray();
            CollectionAssert.AreEqual(new[] { 273292, 82835 }, durations.Select(m => m.Target.Instance).ToArray());
            Assert.AreEqual(1, durations[0].Parameter2);
            f.Now = f.Now.AddMilliseconds(10); f.Session.Bodies.Clear(); f.Service.Tick(f.Player); f.Service.Tick(f.Player);
            Assert.AreEqual(2, f.Store.Commits); Assert.AreEqual(82835, f.Store.Rows.Single().NanoId);
            Assert.AreEqual(8, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(30365, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(1, f.Session.Bodies.OfType<BuffMessage>().Count());
            Assert.AreEqual(273292, f.Session.Bodies.OfType<BuffMessage>().Single().NanoProgram.Instance);
            Assert.AreEqual(0, f.Session.Bodies.OfType<CastNanoSpellMessage>().Count());
            f.Session.Bodies.Clear(); Assert.IsTrue(f.Service.Remove(f.Player, 82835));
            Assert.AreEqual(0, f.Session.Bodies.OfType<CastNanoSpellMessage>().Count()); // Parent OnTerminate is not invented either.
        }

        [TestMethod]
        public void Sparrow_parent_and_child_rollback_or_uncertain_commit_publish_neither()
        {
            foreach (bool uncertain in new[] { false, true })
            {
                var f = new Fixture(82835); f.Player.Stats.Set(CharacterStat.MaxNCU, 1007);
                f.Store.Fail = !uncertain; f.Store.Unknown = uncertain; f.Cast(); f.Service.Tick(f.Player);
                Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Store.Rows.Count);
                Assert.AreEqual(1000, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
                Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
                Assert.AreEqual(0, f.Session.Bodies.OfType<CharacterActionMessage>().Count());
                Assert.AreEqual(1, f.Session.Bodies.OfType<CastNanoSpellMessage>().Count());
                Assert.AreEqual(uncertain, f.Player.IsPersistenceQuarantined);
            }
        }

        [TestMethod]
        public void Sparrow_detach_and_restore_keep_real_rows_without_replaying_nested_effects()
        {
            var f = new Fixture(82835); f.Player.Stats.Set(CharacterStat.MaxNCU, 1007); f.Cast();
            f.Service.DetachPlayer(f.Player); f.Session.Bodies.Clear();
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.IsTrue(f.Service.AttachPlayer(f.Player));
            Assert.AreEqual(30365, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(1300, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            Assert.AreEqual(2, f.Store.Rows.Count); Assert.AreEqual(1, f.Store.Commits);
            Assert.AreEqual(0, f.Session.Bodies.Count); Assert.AreEqual(989, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            f.Service.DetachPlayer(f.Player); f.Service.DetachPlayer(f.Player);
            Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.RunSpeed));
            var replacement = TestWorld.CreatePlayer(f.Player.Identity.Instance);
            Assert.IsTrue(f.Service.AttachPlayer(replacement)); f.Service.Tick(f.Player);
            Assert.IsFalse(f.Service.TryCast(f.Player, 82835, f.Player.Identity));
        }

        [TestMethod]
        public void Sparrow_cast_interrupts_cannot_complete_child_against_replaced_session_or_dead_owner()
        {
            foreach (string reason in new[] { "disconnect", "zoning", "death", "replacement" })
            {
                var f = new Fixture(82835); Assert.IsTrue(f.Service.TryCast(f.Player, 82835, f.Player.Identity));
                switch (reason)
                {
                    case "disconnect": f.Service.Cancel(f.Player, f.Session); f.Session.Close(); break;
                    case "zoning": f.Player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield); break;
                    case "death": f.Player.OnDeath(); break;
                    case "replacement": var replacement = new Session(); replacement.BindPlayer(f.Player); f.Player.Session = replacement; break;
                }
                f.Milliseconds += 1000; f.Now = f.Now.AddSeconds(1); f.Service.Tick(f.Player);
                Assert.AreEqual(0, f.Store.Commits, reason); Assert.AreEqual(0, f.Store.Rows.Count, reason);
                Assert.AreEqual(0, f.Session.Bodies.OfType<CharacterActionMessage>().Count(), reason);
            }
        }

        [TestMethod]
        public void Sparrow_cast_in_sl_retains_morph_and_reacquires_only_proven_flight_after_zone_refresh()
        {
            var f = new Fixture(82835); f.Player.Stats.Set((CharacterStat)531, 1); f.Cast();
            Assert.AreEqual(30365, f.Player.Stats.GetOrZero(CharacterStat.MonsterData));
            Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            f.Player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
            f.Player.Stats.Set((CharacterStat)531, 0); f.Service.RefreshPlayer(f.Player);
            Assert.AreEqual(1, f.Player.Stats.GetOrZero(CharacterStat.IsVehicle));
            Assert.AreEqual(1, f.Store.Commits);
            Assert.AreEqual(1, f.Session.Bodies.OfType<CastNanoSpellMessage>().Count(m => m.NanoId == 273292));
        }

        [TestMethod]
        public void Existing_action_and_event_requirement_paths_remain_distinct_and_unknown_enforced_ops_fail()
        {
            var p = TestWorld.CreatePlayer(1); p.Stats.Set(CharacterStat.Strength, 10);
            ItemRequirement falseOr = new() { ChildOperator = (int)Operator.Or, Operator = (int)Operator.EqualTo,
                Target = (int)ItemTarget.Self, StatNumber = (int)CharacterStat.Strength, Value = 20 };
            Assert.IsTrue(NanoRequirements.Action(p, p, [falseOr])); // Accepted Actions ignores OR links.
            Assert.IsTrue(NanoRequirements.TryEvent(p, p, [falseOr], out bool met)); Assert.IsFalse(met);
            ItemRequirement trueOr = new() { ChildOperator = (int)Operator.Or, Operator = (int)Operator.EqualTo,
                Target = (int)ItemTarget.Self, StatNumber = (int)CharacterStat.Strength, Value = 10 };
            Assert.IsTrue(NanoRequirements.TryEvent(p, p, [falseOr, trueOr], out met)); Assert.IsTrue(met);
            var unsupported = new ItemRequirement { ChildOperator = (int)Operator.And, Operator = (int)Operator.HasMaster,
                Target = (int)ItemTarget.Self };
            Assert.IsFalse(NanoRequirements.Action(p, p, [unsupported]));
            Assert.IsFalse(NanoRequirements.TryEvent(p, p, [unsupported], out _));
        }

        [TestMethod]
        public void Legacy_flying_allowed_operator_does_not_override_expansion_criterion_or_server_flight_authority()
        {
            var f = new Fixture(281569);
            var criterion = new ItemRequirement { ChildOperator = (int)Operator.And, Operator = (int)Operator.FlyingAllowed,
                Target = (int)ItemTarget.Self };
            Assert.IsTrue(NanoRequirements.Action(f.Player, f.Player, [criterion]));
            f.Player.Stats.Set((CharacterStat)531, 1);
            Assert.IsFalse(f.Service.TryCast(f.Player, f.Nano.Id, f.Player.Identity));
            f.Player.Stats.Set(CharacterStat.IsVehicle, 1);
            f.Player.Motor.ApplyAction(MovementAction.SwitchToFly);
            Assert.AreEqual(MovementState.Run, f.Player.Motor.State);
            Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Session.Bodies.Count);
        }

        private sealed class Fixture
        {
            public readonly Player Player = TestWorld.CreatePlayer(1);
            public readonly Session Session = new(); public readonly Store Store = new();
            public readonly MorphNanoSpecialization Morph = new(); public readonly NanoService Service;
            public readonly NanoDefinition Nano; private readonly NanoCatalog _catalog;
            public DateTime Now = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc); public long Milliseconds;
            public Fixture(int id, bool attach = true)
            {
                var catalog = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
                _catalog = catalog;
                Assert.IsTrue(catalog.TryGet(id, out var nano)); Nano = nano;
                foreach (ItemRequirement requirement in nano.Template.Actions.Where(a => a.ActionType == (int)ActionType.ToUse)
                    .SelectMany(a => a.Requirements).Where(r => r.ChildOperator == (int)Operator.And && r.StatNumber != 0))
                    Player.Stats.Set((CharacterStat)requirement.StatNumber, (Operator)requirement.Operator switch
                    { Operator.EqualTo => requirement.Value, Operator.GreaterThan => requirement.Value + 1, Operator.NotBitAnd => 0,
                        _ => throw new AssertFailedException("Unhandled fixture requirement " + requirement.Operator) });
                Player.Stats.Set((CharacterStat)12, 5907); Player.Stats.Set(CharacterStat.MonsterData, 0);
                Player.Stats.Set(CharacterStat.RunSpeed, 100); Player.Stats.Set(CharacterStat.CurrentNano, 1000);
                Player.Stats.Set(CharacterStat.MaxNanoEnergy, 1000); Player.Stats.Set(CharacterStat.Health, 100);
                Player.Stats.Set(CharacterStat.MaxHealth, 100); Player.Stats.Set(CharacterStat.MaxNCU, 100);
                Player.Stats.Set(CharacterStat.AggDef, 25); Player.Stats.Set(CharacterStat.NanoCInit, 0);
                Player.TryAddUploadedNano(id); Session.BindPlayer(Player); Player.Session = Session;
                Service = new(catalog, Store, [Morph, new SparrowChildNanoSpecialization()], utcNow: () => Now, monotonicMilliseconds: () => Milliseconds);
                if (attach) Assert.IsTrue(Service.AttachPlayer(Player)); Player.Stats.DrainDirty();
            }
            public void Cast()
            {
                bool prepared = Morph.TryPrepare(Player, Player, Nano, out _);
                bool childPrepared = _catalog.TryGet(273292, out var child)
                    && new SparrowChildNanoSpecialization().TryPrepare(Player, Player, child, out _);
                Assert.IsTrue(Service.TryCast(Player, Nano.Id, Player.Identity),
                    $"MorphPrepared={prepared}; ActionRequirements={NanoEffectPlan.ActionRequirements(Player, Player, Nano)}; "
                    + $"ChildPrepared={childPrepared}; ParentDefend={Nano.Template.Defend.Count}; ChildDefend={child?.Template.Defend.Count}");
                int delay = Nano.AttackCentiseconds * 10; Milliseconds += delay; Now = Now.AddMilliseconds(delay); Service.Tick(Player);
            }
        }
        private sealed class Store : IActiveNanoRepository
        {
            public List<ActiveNanoRecord> Rows = new(); public int Commits; public bool Fail; public bool Unknown;
            public IReadOnlyList<ActiveNanoRecord> Load(int characterId) => Rows.ToArray();
            public void Commit(IReadOnlyList<NanoCharacterWrite> characters)
            {
                if (Fail) throw new InvalidOperationException("Known rollback");
                if (Unknown) throw new ZoneEngine_New.Core.Data.DatabaseCommitOutcomeUnknownException(new IOException("Lost commit reply"));
                Rows = characters.Single().ActiveNanos.ToList(); Commits++;
            }
        }
        private sealed class Session : IZoneSession
        {
            public List<MessageBody> Bodies = new(); public List<byte[]> Raw = new();
            public SessionState State { get; set; } = SessionState.InPlay; public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player; public void UnbindPlayer() => Player = null;
            public void Close() => State = SessionState.Closed;
            public void Send(byte[] packet) => Raw.Add(packet); public void Send(Message message) => Send(message.Body);
            public void Send(MessageBody body) => Bodies.Add(body); public void Send(MessageBody body, int sender, int receiver) => Send(body);
            public void SendInitiateCompression() { } public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
        }
    }
}
