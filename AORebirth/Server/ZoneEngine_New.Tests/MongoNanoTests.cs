namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AORebirth.Enums;
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
    public void Requirements_and_child_graph_are_validated_before_cost_or_success()
    {
        using var f = new Fixture(); f.Player.Stats.Set((CharacterStat)129, 9);
        Assert.IsFalse(f.Service.TryCast(f.Player, 100198, Identity.None)); f.Player.Stats.Set((CharacterStat)129, 10);
        Assert.IsTrue(f.Catalog.TryGet(100194, out var child));
        child.Template.SpellList[EventType.OnUse][0].FunctionType = (int)FunctionType.Hit;
        Assert.IsFalse(f.Service.TryCast(f.Player, 100198, Identity.None)); Assert.AreEqual(0, f.Store.Commits);
    }

    [TestMethod]
    [DataRow("target")] [DataRow("operation")] [DataRow("requirement-target")]
    public void Unsupported_child_taunt_graph_is_rejected_before_cost(string invalid)
    {
        using var f = new Fixture(); Assert.IsTrue(f.Catalog.TryGet(100194, out var child));
        var spell = child.Template.SpellList[EventType.OnUse][0];
        if (invalid == "target") spell.Target = (int)ItemTarget.User;
        if (invalid == "operation") spell.Requirements[0].Operator = (int)Operator.HasMaster;
        if (invalid == "requirement-target") spell.Requirements[0].Target = int.MaxValue;
        Assert.IsFalse(f.Service.TryCast(f.Player, 100198, Identity.None));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
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
            Service = new(Catalog, Store, [],
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
