namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

[TestClass]
public sealed class TeamWarpNanoTests
{

    [TestMethod]
    public void Exact_restrictions_requirements_upload_and_catalog_graph_fail_without_cost()
    {
        foreach (int restricted in new[] { 127, 647, 1931, 4000, 4999 })
        {
            using var f = new Fixture(); var a = f.World.World(500); var b = f.World.World(restricted);
            var caster = f.Player(a, 1); var member = f.Player(b, 2); f.Join(caster, member);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
            Assert.IsFalse(f.Service.TryCast(member, 154914, caster.Identity));
            Assert.AreEqual(0, f.Store.Commits);
        }
        using (var f = new Fixture())
        {
            var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
            caster.Stats.Set((CharacterStat)130, 421); Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
            caster.Stats.Set((CharacterStat)130, 1000); caster.Stats.Set((CharacterStat)531, 1);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); caster.Stats.Set((CharacterStat)531, 0);
            f.Nano(154914).Template.SpellList[EventType.OnUse][0].Arguments.Add(1);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); Assert.AreEqual(0, f.Store.Commits);
        }
    }

    [TestMethod]
    public void Invalid_caster_geometry_is_rejected_without_guessing_a_heading_or_publishing_nan()
    {
        using var f = new Fixture(); var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
        foreach (var heading in new[] { new Quaternion(0, 0, 0, 0), new Quaternion(double.NaN, 0, 0, 1), new Quaternion(0, double.PositiveInfinity, 0, 1) })
        { caster.Rotation = heading; Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); }
        caster.Rotation = new Quaternion(0, 0, 0, 1); caster.Position = new Vector3(double.NaN, 0, 0);
        Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(5000, caster.Stats.GetOrZero(CharacterStat.CurrentNano));
        caster.Position = new Vector3(1, 0, 0);
    }

    sealed class Fixture : IDisposable
    {
        internal readonly PlayfieldTransferTests.Fixture World = new();
        internal readonly Store Store = new();
        internal readonly NanoCatalog Catalog = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
        internal readonly NanoService Service;
        long _time;
        internal Fixture()
        {
            Service = new(Catalog, Store, [],
                utcNow: () => new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(_time), monotonicMilliseconds: () => _time);
            typeof(PlayfieldManager).GetField("<Nanos>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(World.Manager, Service);
        }
        internal NanoDefinition Nano(int id) { Assert.IsTrue(Catalog.TryGet(id, out var definition)); return definition; }
        internal Player Player(Playfield world, int id)
        {
            var p = World.Player(world, id); p.Name = "Member" + id; p.Rotation = new Quaternion(0, 0, 0, 1);
            foreach (int stat in new[] { 130, 131, 127 }) p.Stats.Set((CharacterStat)stat, 1000);
            p.Stats.Set((CharacterStat)531, 0); p.Stats.Set(CharacterStat.Profession, 3); p.Stats.Set(CharacterStat.Level, 60);
            p.Stats.Set(CharacterStat.CurrentNano, 5000); p.Stats.Set(CharacterStat.MaxNanoEnergy, 5000);
            p.Stats.Set(CharacterStat.MaxNCU, 0); p.Stats.Set(CharacterStat.AggDef, 25); p.Stats.Set(CharacterStat.NanoCInit, 0);
            p.TryAddUploadedNano(154914); p.TryAddUploadedNano(154913);
            World.Owner(world, () =>
            {
                Assert.IsTrue(Service.AttachPlayer(p)); p.NanoRuntime = Service; World.Manager.Teams.AttachPlayer(p);
                world.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(p);
            });
            return p;
        }
        internal void Join(Player a, Player b)
        {
            World.Owner(a.Playfield!, () => World.Manager.Teams.TryHandle(a, new CharacterActionMessage { Identity = a.Identity, Target = b.Identity, Action = CharacterActionType.TeamRequestInvite }));
            World.Owner(b.Playfield!, () => World.Manager.Teams.TryHandle(b, new CharacterActionMessage { Identity = b.Identity, Target = a.Identity, Action = CharacterActionType.TeamRequestReply, Parameter2 = 1 }));
            World.Drain(a.Playfield!); if (!ReferenceEquals(a.Playfield, b.Playfield)) World.Drain(b.Playfield!);
            Assert.IsTrue(World.Manager.Teams.AreTeammates(a, b)); Clear(a, b);
        }
        internal void Advance(Player caster, int milliseconds) { _time += milliseconds; World.Owner(caster.Playfield!, () => Service.Tick(caster)); }
        internal void Cast(Player caster, int id, Identity target)
        { World.Owner(caster.Playfield!, () => Assert.IsTrue(Service.TryCast(caster, id, target))); Advance(caster, Nano(id).AttackCentiseconds * 10); }
        internal MessageBody[] Bodies(Player p) => p.Session is ZoneSession s ? World.Packets(s).Select(bytes => new ZoneMessageCodec().Deserialize(bytes)!.Body).ToArray() : [];
        internal void Clear(params Player[] players) { foreach (var p in players) Bodies(p); }
        public void Dispose() => World.Dispose();
    }
    sealed class Store : IActiveNanoRepository
    {
        internal int Commits; internal bool Fail, Unknown; internal IReadOnlyList<NanoCharacterWrite> Last = [];
        public IReadOnlyList<ActiveNanoRecord> Load(int id) => [];
        public void Commit(IReadOnlyList<NanoCharacterWrite> writes)
        {
            if (Unknown) throw new DatabaseCommitOutcomeUnknownException(new InvalidOperationException("fixture unknown"));
            if (Fail) throw new InvalidOperationException("fixture rollback");
            Commits++; Last = writes;
        }
    }
}
