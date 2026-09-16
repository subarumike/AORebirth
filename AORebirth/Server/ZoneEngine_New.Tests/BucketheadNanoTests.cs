namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
    public void Edited_summon_template_choice_and_lifetime_are_used_after_file_reload_without_rebuild()
    {
        using var f = new Fixture();
        var content = f.Pf.GetRequiredService<IGameData>().WorldContent;
        var rule = content.Summons.Single();
        var alternative = JsonSerializer.Deserialize<WorldNpcDefinition>(JsonSerializer.Serialize(content.Npcs.Single(x => x.Key == rule.NpcDefinitionKey)))!;
        alternative.Key = "fixture:alternate-summon"; alternative.Name = "Editable summon fixture";
        content.Npcs = content.Npcs.Append(alternative).ToArray(); rule.NpcDefinitionKey = alternative.Key;
        rule.RelativeOffset = [2, 0, 0]; rule.LifetimeSeconds = 3;
        string root = Path.Combine(Path.GetTempPath(), "aor-summon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        SummonService? service = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "WorldContent.json"), JsonSerializer.Serialize(content));
            var data = new StubGameData(HashItemCatalog.Parse("{}", "{}")) { RootPath = root };
            service = new(f.Pf, f.Registry, f.Pf.GetRequiredService<PlayfieldLocality>(), f.Pf.GetRequiredService<NpcContentActivationService>(),
                new ItemBuilder(f.Items, new StubLogger()), f.Items, data, () => f.Time);
            f.World.Owner(f.Pf, () =>
            {
                Assert.IsTrue(service.TryPrepare(f.Player, data.WorldContent.Summons.Single(), () => true, out var publish));
                publish();
                Assert.AreEqual("Editable summon fixture", service.ForOwner(f.Player)!.Name);
                Assert.AreEqual(f.Player.Position.x + 2, service.ForOwner(f.Player)!.Position.x);
                f.Time += 3000; service.Tick(); Assert.AreEqual(0, service.Count);
            });
        }
        finally { if (service != null) f.World.Owner(f.Pf, service.Shutdown); Directory.Delete(root, true); }
    }

    [TestMethod]
    public void Replacement_and_exact_ten_minute_expiry_withdraw_old_registry_and_shop()
    {
        using var f = new Fixture(); f.Spawn(); var old = f.Summons.ForOwner(f.Player)!;
        var shop = old.Shop!; f.Advance(1000); f.Spawn();
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
        using var f = new Fixture(); f.Spawn(); var npc = f.Summons.ForOwner(f.Player)!; var shop = npc.Shop!;
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
        f.World.Owner(f.Pf, () => Assert.IsTrue(f.Summons.TryPrepare(f.Player, f.Pf.GetRequiredService<IGameData>().WorldContent.Summons.Single(x => x.NanoId == 300439), () => true, out publish)));
        f.World.Owner(f.Pf, () => { publish(); publish(); }); Assert.AreEqual(1, f.Summons.Count);
        var old = f.Summons.ForOwner(f.Player);
        bool current = true;
        f.World.Owner(f.Pf, () => Assert.IsTrue(f.Summons.TryPrepare(f.Player, f.Pf.GetRequiredService<IGameData>().WorldContent.Summons.Single(x => x.NanoId == 300439), () => current, out publish)));
        current = false; f.World.Owner(f.Pf, publish); Assert.AreSame(old, f.Summons.ForOwner(f.Player));
    }

    [TestMethod]
    public void Unsupported_graph_and_missing_real_stock_fail_before_cost()
    {
        using var f = new Fixture();
        var unsupported = new SummonService(f.Pf, f.Registry,
            f.Pf.GetRequiredService<PlayfieldLocality>(), f.Pf.GetRequiredService<NpcContentActivationService>(),
            new StubItemBuilder(), new StubCatalog(), f.Pf.GetRequiredService<IGameData>());
        Assert.IsFalse(unsupported.TryPrepare(f.Player, f.Pf.GetRequiredService<IGameData>().WorldContent.Summons.Single(x => x.NanoId == 300439), () => true, out _));
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
        Assert.IsTrue(new SummonNanoSpecialization().ActionRequirements(f.Player, f.Player, nano));
        // The accepted dedicated Legacy path does not read a persisted/stale PF type.
        f.Player.Stats.Set(CharacterStat.PlayfieldType, 2);
        Assert.IsTrue(new SummonNanoSpecialization().ActionRequirements(f.Player, f.Player, nano));
        nano.Template.Actions[0].Requirements[0].Value = 3;
        Assert.IsFalse(f.Service.TryCast(f.Player, 300439, f.Player.Identity));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(0, f.Summons.Count);
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
        internal SummonService Summons => Pf.GetRequiredService<SummonService>();
        internal DynelRegistry Registry => Pf.GetRequiredService<DynelRegistry>();
        internal Fixture(int uploaded = 300439)
        {
            var data = new StubGameData(HashItemCatalog.Parse("{}", "{}")) { RootPath = Path.Combine(AppContext.BaseDirectory, "GameData") };
            Items = new(new NoNames(), data, new StubLogger());
            Service = new(Nanos, Store, [new SummonNanoSpecialization()],
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
        internal void Advance(int milliseconds) { Time += milliseconds; World.Owner(Pf, () => Service.Tick(Player)); }
        internal void Spawn() => World.Owner(Pf, () =>
        {
            var rule = Pf.GetRequiredService<IGameData>().WorldContent.Summons.Single(x => x.NanoId == 300439);
            Assert.IsTrue(Summons.TryPrepare(Player, rule, () => true, out var publish)); publish();
        });
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
