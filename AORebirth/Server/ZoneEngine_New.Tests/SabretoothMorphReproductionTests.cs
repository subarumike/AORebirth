namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class SabretoothMorphReproductionTests
{
    const int Sabretooth = 85050;
    const int AnatomySabretooth = 217678;
    const int GrowlingPredator = 217680;
    const int ReturnToForm = 301835;
    const int ConflictingHostileMorph = 259764;
    const int UnrelatedHostile = 700001;
    const int SabretoothModel = 30356;
    const int PredatorModel = 209022;
    static readonly StubGameData GameData = new(HashItemCatalog.Parse("{}"), rootPath: GameDataPaths.ResolveRuntimeRoot())
    {
        AllowMissingXpLevels = true,
    };
    static readonly Lazy<ItemTemplateCatalog> Catalog = new(() => new ItemTemplateCatalog(GameData, new StubLogger()));

    [DataTestMethod]
    [DataRow(Sabretooth, 60, SabretoothModel)]
    [DataRow(85052, 60, SabretoothModel)]
    [DataRow(85057, 60, SabretoothModel)]
    [DataRow(85061, 60, SabretoothModel)]
    [DataRow(85063, 60, SabretoothModel)]
    [DataRow(215053, 60, SabretoothModel)]
    [DataRow(217676, 60, SabretoothModel)]
    [DataRow(AnatomySabretooth, 60, SabretoothModel)]
    [DataRow(85070, 240, SabretoothModel)]
    [DataRow(GrowlingPredator, 240, PredatorModel)]
    public void LandedFormsKeepTheirShapeUntilTheirAuthoredDeadline(int form, int minutes, int model)
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, form);
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, player, form, items, now));
        DateTime expiry = now.AddMinutes(minutes);
        Assert.IsTrue(player.TryGetBuff(form, out Buff? buff));
        Assert.AreEqual(expiry, buff!.ExpiresAtUtc);

        NanoRuntime.Tick(player, now.AddSeconds(12));
        NanoRuntime.Tick(player, expiry.AddMilliseconds(-1));
        VerifyForm(player, form, model);
        NanoRuntime.Tick(player, expiry);
        VerifyNoForm(player, form);
    }

    [DataTestMethod]
    [DataRow(Sabretooth, SabretoothModel)]
    [DataRow(GrowlingPredator, PredatorModel)]
    public void CombatStartDamageAndAnUnrelatedHostileNanoKeepTheActiveForm(int form, int model)
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, form);
        items.Add(TestNanos.Create(UnrelatedHostile, strain: 731, can: CanFlags.ApplyOnHostile,
            flags: NanoFlags.IsHostile | NanoFlags.IsBuff, modifiers: [TestNanos.Modify(CharacterStat.RunSpeed, -10)]));
        NpcCharacter mob = SpawnOpponent(world, items, player);
        DateTime now = DateTime.UtcNow;

        world.Locality.ActivatePlayerVisibility(player);
        world.Locality.ActivatePlayerVisibility(world.Second);
        Assert.IsTrue(Land(player, player, form, items, now));
        VerifyForm(player, form, model);
        Assert.IsTrue(world.FirstSession.Sent.OfType<StatMessage>().Any(packet => PublishesShape(packet, player, model)));
        Assert.IsTrue(world.SecondSession.Sent.OfType<StatMessage>().Any(packet => PublishesShape(packet, player, model)));

        player.StartFighting(mob.Identity, 1);
        Assert.AreEqual(mob.Identity, player.FightingTarget);
        Assert.IsTrue(player.IsInCombat);
        player.RollBuffBreaks(BuffBreakCause.Attack, mob);
        int health = player.Stats.GetOrZero(CharacterStat.Health);
        player.ApplyDamage(mob, 10, HitType.Normal);
        Assert.AreEqual(health - 10, player.Stats.GetOrZero(CharacterStat.Health));
        player.RollBuffBreaks(BuffBreakCause.SpellAttack, mob);
        Assert.IsTrue(Land(mob, player, UnrelatedHostile, items, now.AddSeconds(5)));
        Assert.IsTrue(player.TryGetBuff(UnrelatedHostile, out _));

        NanoRuntime.Tick(player, now.AddSeconds(12));
        VerifyForm(player, form, model);
        Assert.IsFalse(world.FirstSession.Sent.OfType<BuffMessage>().Any(packet =>
            packet.Action == 0 && packet.NanoProgram.Instance == form));
    }

    [TestMethod]
    public void HigherRankReplacesTheOldFormAndExplicitDispelStillWorks()
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, Sabretooth, AnatomySabretooth, ReturnToForm);
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, player, Sabretooth, items, now));
        Assert.IsTrue(Land(player, player, AnatomySabretooth, items, now.AddSeconds(1)));
        Assert.IsFalse(player.TryGetBuff(Sabretooth, out _));
        Assert.IsFalse(Land(player, player, Sabretooth, items, now.AddSeconds(2)));
        VerifyForm(player, AnatomySabretooth, SabretoothModel);

        Assert.IsTrue(Land(player, player, ReturnToForm, items, now.AddSeconds(3)));
        VerifyNoForm(player, AnatomySabretooth);
    }

    [TestMethod]
    public void AuthoredHostileMorphCanReplaceSabretoothDuringCombat()
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, Sabretooth, ConflictingHostileMorph);
        NpcCharacter mob = SpawnOpponent(world, items, player);
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, player, Sabretooth, items, now));
        player.StartFighting(mob.Identity, 1);
        Assert.IsTrue(Land(mob, player, ConflictingHostileMorph, items, now.AddSeconds(5)));
        Assert.IsFalse(player.TryGetBuff(Sabretooth, out _));
        Assert.IsTrue(player.TryGetBuff(ConflictingHostileMorph, out _));
        Assert.IsTrue(world.FirstSession.Sent.OfType<BuffMessage>().Any(packet =>
            packet.Action == 0 && packet.NanoProgram.Instance == Sabretooth));
    }

    [TestMethod]
    public void PlayshiftAndRehydrationKeepTheFormAndItsExpiry()
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, GrowlingPredator);
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, player, GrowlingPredator, items, now));
        Assert.AreEqual(0, NanoRuntime.StripPlayshiftFailures(player));
        VerifyForm(player, GrowlingPredator, PredatorModel);

        player.SnapshotActiveNanosForTransfer();
        List<ActiveNanoRecord> snapshot = player.TakeDirtyActiveNanos()!;
        ActiveNanoRecord record = snapshot.Single(nano => nano.NanoId == GrowlingPredator);
        Assert.AreEqual(GrowlingPredator, record.NanoId);
        Assert.AreEqual(now.AddHours(4).Ticks, record.ExpiresAtUtcTicks);

        using var destination = new TriggerTestWorld();
        Player arrived = Ready(destination.First);
        new PlayerHydrator(items, GameData).Apply(arrived, new CharacterHydrationResult
        {
            Character = new CharacterRecord { Id = arrived.Identity.Instance, Name = "Morph transfer", Playfield = 1, HeadingW = 1 },
            Stats = SpawnStats(),
            ActiveNanos = snapshot,
        });
        Assert.IsTrue(arrived.TryGetBuff(GrowlingPredator, out Buff? restored));
        Assert.AreEqual(record.ExpiresAtUtcTicks, restored!.ExpiresAtUtc.Ticks);
        Assert.AreEqual(record.NanoInstance, restored.NanoInstance);
        VerifyForm(arrived, GrowlingPredator, PredatorModel);
    }

    [TestMethod]
    public void LinkDeadReconnectKeepsTheFormAndDeathClearsIt()
    {
        using var world = new TriggerTestWorld();
        Player player = Ready(world.First);
        StubItemBuilder items = RuntimeItems(world, Sabretooth);
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, player, Sabretooth, items, now));
        world.FirstSession.UnbindPlayer();
        player.EnterLinkDead(TimeSpan.FromSeconds(60));
        NanoRuntime.Tick(player, now.AddSeconds(12));
        var reconnect = new RecordingZoneSession { State = SessionState.InPlay };
        player.EnterOnline(reconnect);
        Assert.AreSame(player, reconnect.Player);
        VerifyForm(player, Sabretooth, SabretoothModel);

        player.OnDeath();
        Assert.IsTrue(player.IsDead);
        VerifyNoForm(player, Sabretooth);
    }

    static Player Ready(Player player)
    {
        var data = (StubGameData)player.Playfield!.GetRequiredService<IGameData>();
        data.AllowMissingXpLevels = true;
        foreach (StatRecord stat in SpawnStats())
            player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue);
        player.Stats.Set(CharacterStat.MaxNCU, 250);
        return player;
    }

    static StatRecord[] SpawnStats()
        => new Dictionary<CharacterStat, int>
        {
            [CharacterStat.Flags] = 0x00081241, [CharacterStat.Breed] = 1, [CharacterStat.Sex] = 2,
            [CharacterStat.Profession] = (int)Profession.Adventurer, [CharacterStat.Fatness] = 0,
            [CharacterStat.Race] = 1, [CharacterStat.HeadMesh] = 40683, [CharacterStat.VisualFlags] = 31,
            [CharacterStat.Scale] = 100, [CharacterStat.Level] = 160, [CharacterStat.TitleLevel] = 5,
            [CharacterStat.Side] = 0, [CharacterStat.Expansion] = 171, [CharacterStat.Strength] = 150,
            [CharacterStat.Agility] = 150, [CharacterStat.Stamina] = 150, [CharacterStat.Intelligence] = 150,
            [CharacterStat.Sense] = 150, [CharacterStat.Psychic] = 150,
            [CharacterStat.BodyDevelopment] = 350, [CharacterStat.NanoPool] = 200,
            [CharacterStat.Health] = 2000, [CharacterStat.MaxHealth] = 3000,
            [CharacterStat.MaxNanoEnergy] = 1800, [CharacterStat.CurrentNano] = 1250,
            [CharacterStat.RunSpeed] = 450,
        }.Select(entry => new StatRecord { StatId = (int)entry.Key, StatValue = entry.Value }).ToArray();

    static StubItemBuilder RuntimeItems(TriggerTestWorld world, params int[] forms)
    {
        var items = (StubItemBuilder)world.Playfield.GetRequiredService<IItemBuilder>();
        foreach (int id in forms.Append(273292))
            items.Add(Catalog.Value.Require(id));
        return items;
    }

    static NpcCharacter SpawnOpponent(TriggerTestWorld world, StubItemBuilder items, Player player)
    {
        var mob = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 50000 }, items)
        {
            Attackable = true, Playfield = world.Playfield, Position = player.Position,
        };
        mob.Stats.Set(CharacterStat.Health, 2000);
        mob.Stats.Set(CharacterStat.MaxHealth, 2000);
        world.Registry.Register(mob);
        world.Locality.RegisterDynel(mob);
        return mob;
    }

    static bool Land(Character source, Character target, int nanoId, IItemBuilder items, DateTime now)
        => NanoRuntime.TryApplyImmediate(source, target, nanoId, items, new StubInventoryRepository(), now);

    static bool PublishesShape(StatMessage packet, Player player, int model)
        => packet.Identity == player.Identity && packet.Stats.Any(value =>
            value.Value1 == CharacterStat.MonsterData && value.Value2 == (uint)model);

    static void VerifyForm(Player player, int nanoId, int model)
    {
        player.RebaseStats();
        Assert.IsTrue(player.TryGetBuff(nanoId, out _));
        Assert.AreEqual(model, player.Stats.GetOrZero(CharacterStat.MonsterData));
        SimpleCharFullUpdateMessage packet = player.BuildSpawnMessage();
        Assert.AreEqual((uint)model, packet.MonsterData);
        Assert.IsTrue(packet.ActiveNanos.Any(nano => nano.NanoIdentity.Instance == nanoId));
    }

    static void VerifyNoForm(Player player, int nanoId)
    {
        player.RebaseStats();
        Assert.IsFalse(player.TryGetBuff(nanoId, out _));
        Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.MonsterData));
    }
}
