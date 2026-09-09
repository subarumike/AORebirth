namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class MongoNanoTests
{
    [TestMethod]
    public void Real_catalog_commits_cost_heal_and_duration_then_one_point_taunt_not_magnitude_damage()
    {
        using var f = new Fixture(); var npc = f.Npc(2); npc.SetFightingTarget(new() { Type = IdentityType.CanbeAffected, Instance = 99 });
        f.Begin(); Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(100, npc.Stats.GetOrZero(CharacterStat.Health));
        f.Advance(999); Assert.AreEqual(0, f.Store.Commits); f.Advance(1);
        Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(62, f.Player.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(90, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.AreEqual(99, npc.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(f.Player.Identity, npc.FightingTarget);
        Assert.AreEqual(1, npc.Engagements); Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
        Assert.AreEqual(100198, f.Service.GetActive(f.Player).Single().NanoId);
        Assert.IsTrue(f.Store.Last!.Single().BaseStats.Any(s => s.StatId == (int)CharacterStat.Health && s.StatValue == 62));
        f.Advance(2000); Assert.AreEqual(62, f.Player.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(1, npc.Engagements); // No invented periodic heal/taunt.
        f.Advance(18000); Assert.AreEqual(0, f.Service.GetActive(f.Player).Count);
    }

    [TestMethod]
    public void Area_is_twenty_meters_in_two_dimensions_and_never_grants_template_based_combat()
    {
        using var f = new Fixture(); var edge = f.Npc(20); edge.Position.y = 1000;
        var outside = f.Npc(20.01); var social = f.Npc(2, combat: false); var dead = f.Npc(2); dead.OnDeath();
        var otherPlayer = f.World.Player(f.Pf, 2);
        var findPerson = new GeneratedMissionNpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 999 }, new StubItemBuilder(), null!);
        Assert.IsFalse(findPerson.AcceptsPlayerCombatNanos);
        f.Begin(); f.Advance(1000);
        Assert.AreEqual(99, edge.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(100, outside.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(100, social.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(0, dead.Engagements); Assert.AreEqual(100, otherPlayer.Stats.GetOrZero(CharacterStat.Health));
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void Failed_or_unknown_commit_has_no_heal_damage_or_taunt(bool unknown)
    {
        using var f = new Fixture(); var npc = f.Npc(2); f.Store.Fail = true; f.Store.Unknown = unknown;
        f.Begin(); f.Advance(1000);
        Assert.AreEqual(50, f.Player.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(100, npc.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(0, npc.Engagements); Assert.AreEqual(0, f.Service.GetActive(f.Player).Count);
        Assert.AreEqual(unknown, f.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    [DataRow("disconnect")] [DataRow("death")] [DataRow("zone")] [DataRow("replacement")] [DataRow("shutdown")]
    public void Pending_cast_cannot_survive_owner_lifecycle_change(string mode)
    {
        using var f = new Fixture(); var npc = f.Npc(2); f.Begin();
        switch (mode)
        {
            case "disconnect": f.Player.Session!.State = SessionState.Closed; break;
            case "death": f.Player.OnDeath(); break;
            case "zone": f.Player.Playfield = f.World.World(501); break;
            case "replacement": var session = f.World.Session(); session.BindPlayer(f.Player); f.Player.Session = session; break;
            case "shutdown": f.Service.DetachPlayer(f.Player); break;
        }
        f.Advance(1000); Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, npc.Engagements);
    }

    [TestMethod]
    public void Postcommit_recipient_replacement_is_fenced_and_duplicate_completion_cannot_replay()
    {
        using var f = new Fixture(); var npc = f.Npc(2); TestNpc? replacement = null;
        f.Store.Before = () => { replacement = f.Npc(2, identity: npc.Identity); };
        f.Begin(); f.Advance(1000); f.Advance(0);
        Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(0, npc.Engagements);
        Assert.AreEqual(0, replacement!.Engagements); Assert.AreEqual(100, replacement.Stats.GetOrZero(CharacterStat.Health));
    }

    [TestMethod]
    public void Restore_keeps_duration_without_replaying_heal_or_taunt()
    {
        using var f = new Fixture(); var npc = f.Npc(2); f.Begin(); f.Advance(1000);
        var rows = f.Service.GetActive(f.Player).ToArray(); f.Service.DetachPlayer(f.Player); f.Store.Rows = rows;
        Assert.IsTrue(f.Service.AttachPlayer(f.Player)); f.Service.RefreshPlayer(f.Player);
        Assert.AreEqual(62, f.Player.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(1, npc.Engagements);
        Assert.AreEqual(1, f.Service.GetActive(f.Player).Count);
    }

    [TestMethod]
    public void Requirements_and_child_graph_are_validated_before_cost_or_success()
    {
        using var f = new Fixture(); f.Player.Stats.Set((CharacterStat)129, 9);
        Assert.IsFalse(f.Service.TryCast(f.Player, 100198, Identity.None)); f.Player.Stats.Set((CharacterStat)129, 10);
        Assert.IsTrue(f.Catalog.TryGet(100194, out var child));
        child.Template.SpellList[AORebirth.Enums.EventType.OnUse][0].Arguments[0] = 2001;
        Assert.IsFalse(f.Service.TryCast(f.Player, 100198, Identity.None)); Assert.AreEqual(0, f.Store.Commits);
    }

    sealed class TestNpc(Identity identity, bool combat) : NpcCharacter(identity, new StubItemBuilder())
    {
        internal int Engagements;
        public override bool AcceptsPlayerCombatNanos => combat;
        public override void StartFighting(Identity target, byte action) { Engagements++; SetFightingTarget(target); }
    }
    sealed class Fixture : IDisposable
    {
        internal readonly PlayfieldTransferTests.Fixture World = new();
        internal readonly Store Store = new();
        internal readonly NanoCatalog Catalog = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
        internal readonly NanoService Service;
        internal readonly Playfield Pf;
        internal readonly Player Player;
        long _time;
        internal Fixture()
        {
            Service = new(Catalog, Store, [new MongoNanoSpecialization(Catalog)],
                utcNow: () => new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(_time), monotonicMilliseconds: () => _time);
            typeof(PlayfieldManager).GetField("<Nanos>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(World.Manager, Service);
            Pf = World.World(500); Player = World.Player(Pf, 1);
            Player.Stats.Set(CharacterStat.CurrentNano, 100); Player.Stats.Set(CharacterStat.MaxNanoEnergy, 100);
            Player.Stats.Set(CharacterStat.Health, 50); Player.Stats.Set(CharacterStat.MaxHealth, 100);
            Player.Stats.Set(CharacterStat.AggDef, 25); Player.Stats.Set(CharacterStat.NanoCInit, 0);
            Player.Stats.Set((CharacterStat)128, 10); Player.Stats.Set((CharacterStat)129, 10); Player.Stats.Set((CharacterStat)368, 9);
            Player.TryAddUploadedNano(100198);
            World.Owner(Pf, () => { Assert.IsTrue(Service.AttachPlayer(Player)); Player.NanoRuntime = Service; });
        }
        internal TestNpc Npc(double distance, bool combat = true, Identity? identity = null)
        {
            var registry = Pf.GetRequiredService<DynelRegistry>();
            var npc = new TestNpc(identity ?? registry.AllocateNpcIdentity(), combat) { Playfield = Pf };
            npc.Position = new(Player.Position.x + distance, Player.Position.y, Player.Position.z);
            npc.Stats.Set(CharacterStat.Health, 100); npc.Stats.Set(CharacterStat.MaxHealth, 100);
            registry.Register(npc); return npc;
        }
        internal void Begin() => World.Owner(Pf, () => Assert.IsTrue(Service.TryCast(Player, 100198, Identity.None)));
        internal void Advance(int milliseconds) { _time += milliseconds; World.Owner(Pf, () => Service.Tick(Player)); }
        public void Dispose() => World.Dispose();
    }
    sealed class Store : IActiveNanoRepository
    {
        internal int Commits; internal bool Fail, Unknown; internal Action? Before;
        internal IReadOnlyList<ActiveNanoRecord> Rows = [];
        internal IReadOnlyList<NanoCharacterWrite>? Last;
        public IReadOnlyList<ActiveNanoRecord> Load(int id) => Rows;
        public void Commit(IReadOnlyList<NanoCharacterWrite> writes)
        {
            Before?.Invoke();
            if (Unknown) throw new DatabaseCommitOutcomeUnknownException(new InvalidOperationException("test unknown"));
            if (Fail) throw new InvalidOperationException("test rollback");
            Last = writes; Commits++;
        }
    }
}
