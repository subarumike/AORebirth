using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Nanos;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class DelmusMergeIntegrationTests
{
    [TestMethod]
    public void MissingNpcTemplatesCatalogRejectsAllHashes()
    {
        string root = CreateRoot();
        var data = new GameDataStore(new StubLogger(), null, root);
        Assert.AreEqual(0, data.MobTemplateCount);
        Assert.IsFalse(data.TryResolveMobTemplate("KEEP", 5, out _));
        Assert.IsFalse(data.TryResolveMobTemplate("UNKNOWN", 5, out _));
    }

    [TestMethod]
    public void NpcTemplatesCatalogIsTheOnlyTemplateAndStatSource()
    {
        string root = CreateRoot();
        File.WriteAllText(Path.Combine(root, "NpcTemplates.json"), """
            {
              "KEEP":{"Templates":[{"Level":5,"Name":"Accepted","Stats":{"54":5}}]},
              "NEW":{"Templates":[
                {"Level":5,"Name":"New","Stats":{"54":5,"1":100}},
                {"Level":15,"Name":"New","Stats":{"54":15,"1":200}}]}
            }
            """);
        File.WriteAllText(Path.Combine(root, "MonsterWeapons.json"), "{\"LEW1\":[120910,120911]}");
        var data = new GameDataStore(new StubLogger(), null, root);
        Assert.IsTrue(data.TryResolveMobTemplate("KEEP", 5, out var existing));
        Assert.AreEqual("Accepted", existing.Name);
        Assert.AreEqual(5, data.ComposeNpcStats(existing, 5)[(int)CharacterStat.Level]);
        Assert.IsTrue(data.TryResolveMobTemplate("NEW", 10, out var added));
        Assert.AreEqual(150, data.ComposeNpcStats(added, 10)[1]);
        Assert.AreEqual(10, data.ComposeNpcStats(added, 10)[(int)CharacterStat.Level]);
        Assert.IsTrue(data.TryGetMonsterWeapon("LEW1", out var weapon));
        CollectionAssert.AreEqual(new[] { 120910, 120911 }, weapon);
    }

    [TestMethod]
    public void ActorLocalNanosCannotBypassPlayerDaoOwnership()
    {
        Player player = TestWorld.CreatePlayer(901);
        var spell = TestNanos.Create(1000, durationCentiseconds: 1000);
        Assert.ThrowsExactly<InvalidOperationException>(() => player.TryApplyBuff(spell, player.Identity, DateTime.UtcNow, out _, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() => player.TryRestoreBuff(spell, player.Identity, 1, DateTime.UtcNow.AddSeconds(10)));
        Assert.ThrowsExactly<InvalidOperationException>(() => NanoRuntime.TryStartCast(player, 1000, player.Identity, DateTime.UtcNow));
        Assert.AreEqual(0, player.Buffs.Count);
        Assert.IsFalse(player.IsCastingNano);
    }

    [TestMethod]
    public void ChargeUpdatesReachTheDaoWithoutResettingLocationOnlyCounts()
    {
        var charged = SharedCharacterPersistence.Map(new ItemLocationUpdate(1, (int)IdentityType.Inventory, 901, 64, 3));
        var moved = SharedCharacterPersistence.Map(new ItemLocationUpdate(1, (int)IdentityType.Inventory, 901, 65));
        var retired = SharedCharacterPersistence.Map(new ItemLocationUpdate(1, (int)IdentityType.None, 901, 1, 0));
        Assert.AreEqual(3, charged.StackCount);
        Assert.IsNull(moved.StackCount);
        Assert.AreEqual(1, retired.StackCount);
    }

    static string CreateRoot()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "catalog-merge-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
