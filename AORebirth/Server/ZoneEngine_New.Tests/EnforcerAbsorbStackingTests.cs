namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Nanos;

[TestClass]
public sealed class EnforcerAbsorbStackingTests
{
    const int Ward = 117680;
    const int ShrugAbsorb = 266820;
    const int CocoonMarker = 209802;
    const int Cocoon = 215576;
    static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static readonly Lazy<ItemBuilder> Items = new(() => new ItemBuilder(
        new ItemTemplateCatalog(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: GameDataPaths.ResolveRuntimeRoot()),
            new StubLogger()),
        new StubLogger()));

    [DataTestMethod]
    [DataRow(Ward, ShrugAbsorb)]
    [DataRow(ShrugAbsorb, Ward)]
    public void WardAndShrugStackInBothOrdersAndRefreshIndependently(int first, int second)
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.MaxNCU, 200);
        Assert.IsTrue(Land(player, first, Now));
        Buff original = RequireBuff(player, first);
        Assert.IsTrue(Land(player, second, Now.AddSeconds(1)));
        Assert.AreSame(original, RequireBuff(player, first));
        Assert.AreEqual(1, original.StackingOrder);
        Assert.AreEqual(1, RequireBuff(player, second).StackingOrder);
        Assert.AreEqual(560, Pool(player));

        Assert.AreEqual(0, player.TakeOffShields(player, 380, (int)CharacterStat.MeleeAC));
        Assert.AreEqual(180, RequireBuff(player, second).AbsorbLeft(CharacterStat.AbsorbMeleeAC));
        Assert.AreEqual(0, original.AbsorbLeft(CharacterStat.AbsorbMeleeAC));
        Assert.AreEqual(560, player.Stats.GetOrZero(CharacterStat.AbsorbProjectileAC));

        Assert.IsTrue(Land(player, second, Now.AddSeconds(2)));
        Assert.AreEqual(280, Pool(player));
        Assert.AreSame(original, RequireBuff(player, first));
        Assert.IsTrue(Land(player, first, Now.AddSeconds(3)));
        Assert.AreEqual(560, Pool(player));
        Assert.AreEqual(2, player.Buffs.Count);

        Assert.IsTrue(NanoRuntime.StripBuff(player, first));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(second, out _));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CocoonReplacesWardAndCoexistsWithShrugInBothOrders(bool shrugFirst)
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.MaxNCU, 200);
        Assert.IsTrue(Land(player, Ward, Now));
        if (shrugFirst)
            Assert.IsTrue(Land(player, ShrugAbsorb, Now));

        Assert.IsTrue(Land(player, CocoonMarker, Now.AddSeconds(1)));
        Assert.IsTrue(Land(player, Cocoon, Now.AddSeconds(1)));
        Assert.IsFalse(player.TryGetBuff(Ward, out _));
        Assert.IsFalse(Land(player, Ward, Now.AddSeconds(2)));
        if (!shrugFirst)
            Assert.IsTrue(Land(player, ShrugAbsorb, Now.AddSeconds(2)));

        CollectionAssert.AreEquivalent(new[] { ShrugAbsorb, CocoonMarker, Cocoon },
            player.Buffs.Select(buff => buff.Id).ToArray());
        Assert.AreEqual(10_280, Pool(player));
        Assert.IsTrue(Land(player, ShrugAbsorb, Now.AddSeconds(3)));
        Assert.IsTrue(Land(player, CocoonMarker, Now.AddSeconds(4)));
        Assert.IsTrue(Land(player, Cocoon, Now.AddSeconds(4)));
        Assert.AreEqual(10_280, Pool(player));

        NanoRuntime.Tick(player, Now.AddSeconds(34));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(ShrugAbsorb, out _));
        Assert.IsTrue(Land(player, Ward, Now.AddSeconds(35)));
        Assert.AreEqual(560, Pool(player));

        NanoRuntime.Tick(player, Now.AddSeconds(63));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(Ward, out _));
        Assert.IsFalse(player.TryGetBuff(ShrugAbsorb, out _));
    }

    static bool Land(Player player, int nanoId, DateTime now)
        => NanoRuntime.TryApplyImmediate(player, player, nanoId, Items.Value, new StubInventoryRepository(), now);

    static Buff RequireBuff(Player player, int id)
    {
        Assert.IsTrue(player.TryGetBuff(id, out Buff? buff), $"Expected nano {id} to be active.");
        return buff!;
    }

    static int Pool(Player player) => player.Stats.GetOrZero(CharacterStat.AbsorbMeleeAC);
}
