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
            Assert.IsFalse(f.Morph.Handles(82835)); // Nested child script has not been silently omitted.
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
            public readonly NanoDefinition Nano;
            public DateTime Now = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc); public long Milliseconds;
            public Fixture(int id, bool attach = true)
            {
                var catalog = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
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
                Service = new(catalog, Store, [Morph], utcNow: () => Now, monotonicMilliseconds: () => Milliseconds);
                if (attach) Assert.IsTrue(Service.AttachPlayer(Player)); Player.Stats.DrainDirty();
            }
            public void Cast()
            {
                Assert.IsTrue(Service.TryCast(Player, Nano.Id, Player.Identity));
                int delay = Nano.AttackCentiseconds * 10; Milliseconds += delay; Now = Now.AddMilliseconds(delay); Service.Tick(Player);
            }
        }
        private sealed class Store : IActiveNanoRepository
        {
            public List<ActiveNanoRecord> Rows = new(); public int Commits; public bool Fail;
            public IReadOnlyList<ActiveNanoRecord> Load(int characterId) => Rows.ToArray();
            public void Commit(IReadOnlyList<NanoCharacterWrite> characters)
            {
                if (Fail) throw new InvalidOperationException("Known rollback");
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
