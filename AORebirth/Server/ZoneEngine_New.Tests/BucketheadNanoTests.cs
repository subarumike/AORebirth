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
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class BucketheadNanoTests
{
    [TestMethod]
    public void Real_catalog_cast_commits_once_then_publishes_exact_vendor_without_ncu()
    {
        using var f = new Fixture();
        Assert.IsTrue(f.Nanos.TryGet(300439, out var nano));
        Assert.AreEqual(1, nano.NanoCost); Assert.AreEqual(100, nano.AttackCentiseconds);
        Assert.AreEqual(100, nano.RechargeCentiseconds);
        f.Begin(); Assert.AreEqual(0, f.Summons.Count); Assert.AreEqual(0, f.Store.Commits);
        f.Advance(999); Assert.AreEqual(0, f.Summons.Count);
        f.Advance(1);
        var npc = f.Summons.ForOwner(f.Player)!;
        Assert.IsNotNull(npc); Assert.AreEqual(1, f.Store.Commits);
        Assert.AreEqual(99, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.AreEqual(0, f.Service.GetActive(f.Player).Count);
        Assert.AreEqual(0, f.Player.Stats.GetOrZero(CharacterStat.CurrentNCU));
        Assert.AreEqual(43352, npc.Stats.GetOrZero(CharacterStat.MonsterData));
        Assert.AreEqual(220, npc.Stats.GetOrZero(CharacterStat.Level));
        Assert.AreEqual(101861, npc.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(271061505, npc.Stats.GetOrZero(CharacterStat.Flags));
        Assert.AreEqual(f.Player.Position.x + 1, npc.Position.x);
        Assert.AreEqual(99566, npc.Shop!.Template.Id); Assert.IsTrue(npc.Shop.Stock.IsAcceptedSnapshot);
        Assert.AreEqual(46, npc.Shop.Stock.Slots.Count);
        foreach (var source in ZoneEngine.Core.Playfields.CapturedBucketheadTechnodealerContentProvider.Stock)
        {
            var slot = npc.Shop.Stock.Slots[source.Slot];
            Assert.AreEqual(source.LowId, slot.LowId); Assert.AreEqual(source.HighId, slot.HighId);
            Assert.AreEqual(source.Quality, slot.Quality);
        }
        Assert.AreEqual(f.Items.Require(99566).Stats[CharacterStat.BuyModifier], npc.Shop.BuyModifier);
        Assert.AreEqual(f.Items.Require(99566).Stats[CharacterStat.SellModifier], npc.Shop.SellModifier);
        var bodies = f.World.Packets((ZoneSession)f.Player.Session!).Select(p => new ZoneMessageCodec().Deserialize(p)!.Body).ToArray();
        int scfu = Array.FindIndex(bodies, p => p is SimpleCharFullUpdateMessage s && s.Identity == npc.Identity);
        int vmfu = Array.FindIndex(bodies, p => p is VendingMachineFullUpdateMessage v && v.NpcIdentity == npc.Identity);
        Assert.IsTrue(scfu >= 0 && vmfu > scfu);
        var shopPacket = (VendingMachineFullUpdateMessage)bodies[vmfu];
        Assert.AreEqual(0x0b, (int)shopPacket.TypeIdentifier);
        Assert.AreEqual(99566u, shopPacket.Stats.Single(s => s.Value1 == CharacterStat.ACGItemTemplateID).Value2);
        Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Failed_or_unknown_cost_commit_never_spawns_or_replaces(bool unknown)
    {
        using var f = new Fixture(); f.Cast(); var old = f.Summons.ForOwner(f.Player);
        f.Advance(1000); f.Store.Fail = true; f.Store.Unknown = unknown;
        f.Cast();
        Assert.AreSame(old, f.Summons.ForOwner(f.Player)); Assert.AreEqual(1, f.Store.Commits);
        Assert.AreEqual(99, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.AreEqual(unknown, f.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void Replacement_and_exact_ten_minute_expiry_withdraw_old_registry_and_shop()
    {
        using var f = new Fixture(); f.Cast(); var old = f.Summons.ForOwner(f.Player)!;
        var shop = old.Shop!; f.Advance(1000); f.Cast();
        var current = f.Summons.ForOwner(f.Player)!;
        Assert.AreNotEqual(old.Identity, current.Identity); Assert.IsNull(old.Playfield); Assert.IsNull(shop.Playfield);
        Assert.IsFalse(f.Registry.TryGet(old.Identity, out _));
        f.Time += 599999; f.TickSummons(); Assert.AreSame(current, f.Summons.ForOwner(f.Player));
        f.Time++; f.TickSummons(); Assert.AreEqual(0, f.Summons.Count);
        Assert.IsFalse(f.Registry.TryGet(current.Identity, out _)); Assert.IsNull(current.Playfield);
    }

    [TestMethod]
    [DataRow("disconnect")]
    [DataRow("death")]
    [DataRow("zone")]
    [DataRow("replacement")]
    [DataRow("npc_death")]
    [DataRow("shutdown")]
    public void Source_world_cleans_up_when_owner_or_summon_lifecycle_ends(string mode)
    {
        using var f = new Fixture(); f.Cast(); var npc = f.Summons.ForOwner(f.Player)!; var shop = npc.Shop!;
        switch (mode)
        {
            case "disconnect": f.Player.Session!.Close(); break;
            case "death": f.World.Owner(f.Pf, () => f.Player.OnDeath()); break;
            case "zone": f.Player.Playfield = null; break;
            case "replacement": f.Registry.UnregisterExact(f.Player); break;
            case "npc_death": f.World.Owner(f.Pf, () => npc.OnDeath()); break;
            case "shutdown": f.World.Owner(f.Pf, f.Summons.Shutdown); break;
        }
        f.TickSummons(); Assert.AreEqual(0, f.Summons.Count); Assert.IsNull(npc.Playfield); Assert.IsNull(shop.Playfield);
        Assert.IsFalse(f.Registry.TryGet(npc.Identity, out _));
        if (mode == "zone") f.Player.Playfield = f.Pf;
    }

    [TestMethod]
    public void Deferred_callback_is_one_shot_and_rechecks_owner_authority()
    {
        using var f = new Fixture(); Action publish = null!;
        f.World.Owner(f.Pf, () => Assert.IsTrue(f.Summons.TryPrepare(f.Player, () => true, out publish)));
        f.World.Owner(f.Pf, () => { publish(); publish(); }); Assert.AreEqual(1, f.Summons.Count);
        var old = f.Summons.ForOwner(f.Player);
        bool current = true;
        f.World.Owner(f.Pf, () => Assert.IsTrue(f.Summons.TryPrepare(f.Player, () => current, out publish)));
        current = false; f.World.Owner(f.Pf, publish); Assert.AreSame(old, f.Summons.ForOwner(f.Player));
    }

    [TestMethod]
    public void Unsupported_graph_and_missing_real_stock_fail_before_cost()
    {
        using var f = new Fixture();
        var unsupported = new BucketheadSummonService(f.Pf, f.Registry,
            f.Pf.GetRequiredService<PlayfieldLocality>(), f.Pf.GetRequiredService<AcceptedNpcActivationService>(),
            new StubItemBuilder(), new StubCatalog());
        Assert.IsFalse(unsupported.TryPrepare(f.Player, () => true, out _));
        Assert.IsTrue(f.Nanos.TryGet(300439, out var nano));
        nano.Template.SpellList[AORebirth.Enums.EventType.OnUse][0].Arguments.Add(1);
        Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Summons.Count);
    }

    [TestMethod]
    public void Invalid_geometry_and_changed_criteria_never_charge_without_invented_playfield_stats()
    {
        using var f = new Fixture();
        var position = f.Player.Position; var rotation = f.Player.Rotation;
        foreach (double x in new[] { double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            f.Player.Position = new(x, 0, 0);
            Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
        }
        f.Player.Position = position;
        f.Player.Rotation = new(0, 0, 0, 0);
        Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
        f.Player.Rotation = rotation;
        Assert.IsTrue(f.Nanos.TryGet(300439, out var nano));
        Assert.IsTrue(StatCollection.IsUnset(f.Player.Stats.Get(CharacterStat.PlayfieldType)));
        Assert.IsTrue(BucketheadNanoSpecialization.ActionRequirements(f.Player, f.Player, nano));
        // The accepted dedicated Legacy path does not read a persisted/stale PF type.
        f.Player.Stats.Set(CharacterStat.PlayfieldType, 2);
        Assert.IsTrue(BucketheadNanoSpecialization.ActionRequirements(f.Player, f.Player, nano));
        nano.Template.Actions[0].Requirements[0].Value = 3;
        Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Summons.Count);
    }

    [TestMethod]
    [DataRow(300439)]
    [DataRow(300440)]
    public void Previously_persisted_crystal_alias_resolves_without_uploading_or_rewriting_rows(int request)
    {
        using var f = new Fixture(uploaded: 300440);
        f.World.Owner(f.Pf, () => Assert.IsTrue(f.Service.TryCast(f.Player, request, f.Player.Identity)));
        f.Advance(1000); Assert.AreEqual(1, f.Summons.Count); Assert.AreEqual(1, f.Store.Commits);
        Assert.IsFalse(f.Player.UploadedNanoIds.Contains(300439)); Assert.IsTrue(f.Player.UploadedNanoIds.Contains(300440));
    }

    [TestMethod]
    public void Pending_disconnect_cancels_before_commit_and_does_not_publish()
    {
        using var f = new Fixture(); f.Begin(); f.Player.Session!.Close(); f.Advance(1000);
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Summons.Count);
        Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
    }

    sealed class Fixture : IDisposable
    {
        internal readonly PlayfieldTransferTests.Fixture World = new();
        internal readonly Store Store = new();
        internal readonly NanoCatalog Nanos = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
        internal readonly ItemTemplateCatalog Items;
        internal readonly NanoService Service;
        internal readonly Playfield Pf;
        internal readonly Player Player;
        internal long Time;
        internal BucketheadSummonService Summons => Pf.GetRequiredService<BucketheadSummonService>();
        internal DynelRegistry Registry => Pf.GetRequiredService<DynelRegistry>();
        internal Fixture(int uploaded = 300439)
        {
            var data = new StubGameData(HashItemCatalog.Parse("{}", "{}")) { RootPath = Path.Combine(AppContext.BaseDirectory, "GameData") };
            Items = new(new NoNames(), data, new StubLogger());
            Service = new(Nanos, Store, [new BucketheadNanoSpecialization()],
                utcNow: () => new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Time), monotonicMilliseconds: () => Time);
            typeof(PlayfieldManager).GetField("<Nanos>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(World.Manager, Service);
            Pf = World.World(500, Items, new ItemBuilder(Items, new StubLogger()), () => Time);
            Player = World.Player(Pf, 1); Player.Rotation = new(0, 0, 0, 1);
            Player.Stats.Set(CharacterStat.CurrentNano, 100); Player.Stats.Set(CharacterStat.MaxNanoEnergy, 100);
            Player.Stats.Set(CharacterStat.MaxNCU, 0); Player.Stats.Set(CharacterStat.AggDef, 25); Player.Stats.Set(CharacterStat.NanoCInit, 0);
            Player.TryAddUploadedNano(uploaded);
            World.Owner(Pf, () => { Assert.IsTrue(Service.AttachPlayer(Player)); Player.NanoRuntime = Service;
                Pf.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(Player); });
            World.Packets((ZoneSession)Player.Session!);
        }
        internal void Begin() => World.Owner(Pf, () =>
        {
            Assert.IsTrue(Nanos.TryGet(300439, out var nano));
            Assert.IsTrue(new BucketheadNanoSpecialization().TryPrepare(Player, Player, nano, out _), "Exact spell graph");
            Assert.IsTrue(BucketheadNanoSpecialization.ActionRequirements(Player, Player, nano), "Exact action criteria");
            Assert.IsTrue(Summons.TryPrepare(Player, () => true, out _), "World and stock admission");
            Assert.IsTrue(Service.TryCast(Player, 300439, Player.Identity), "Nano cast admission");
        });
        internal void Advance(int milliseconds) { Time += milliseconds; World.Owner(Pf, () => Service.Tick(Player)); }
        internal void Cast() { Begin(); Advance(1000); }
        internal void TickSummons() => World.Owner(Pf, Summons.Tick);
        public void Dispose() => World.Dispose();
    }
    sealed class Store : IActiveNanoRepository
    {
        internal int Commits; internal bool Fail, Unknown;
        public IReadOnlyList<ActiveNanoRecord> Load(int id) => [];
        public void Commit(IReadOnlyList<NanoCharacterWrite> writes)
        {
            if (Unknown) throw new DatabaseCommitOutcomeUnknownException(new InvalidOperationException("test unknown commit"));
            if (Fail) throw new InvalidOperationException("test rollback");
            Commits++;
        }
    }
    sealed class NoNames : IItemNameRepository
    {
        public bool TryGetName(int aoid, out string name) { name = string.Empty; return false; }
        public IReadOnlyDictionary<int, string> GetAllNames() => new Dictionary<int, string>();
    }
}
