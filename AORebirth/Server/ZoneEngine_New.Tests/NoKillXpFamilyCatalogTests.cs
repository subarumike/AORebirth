namespace ZoneEngine_New.Tests;

using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;

/// <summary>Xp.json NoKillXpFamilies: the NPC families whose kills give no regular XP, reloadable.</summary>
[TestClass]
public sealed class NoKillXpFamilyCatalogTests
{
    const string LevelRow = "\"1\": { \"KillAward\": 109, \"NextLevelXp\": 1450, \"XPKillRange\": 4 }";

    [TestMethod]
    public void AFamilyInTheFileGivesNoKillXpAndOneNotInItDoes()
    {
        using var root = new TempRoot();
        root.WriteXp("\"NoKillXpFamilies\": [ { \"Family\": 0, \"Note\": \"Guards\" }, { \"Family\": 219, \"Note\": \"Unicorns\" } ], " + LevelRow);
        var catalog = new NoKillXpFamilyCatalog(root.Path, [0]);

        Assert.IsTrue(catalog.FromFile);
        Assert.AreEqual(0, catalog.LoadProblems.Count);
        // No regular XP at all, so not the grey 1 XP either: AwardRegularXp returns before the pool.
        Assert.IsTrue(Character.GivesNoKillXp(catalog, new GameplayXp(), 219));
        Assert.IsTrue(Character.GivesNoKillXp(catalog, new GameplayXp(), 0));
        // Any other family keeps KillAward + XP stat.
        Assert.IsFalse(Character.GivesNoKillXp(catalog, new GameplayXp(), 42));
    }

    [TestMethod]
    public void AnUnsetNpcFamilyIsNotFamilyZero()
    {
        using var root = new TempRoot();
        root.WriteXp("\"NoKillXpFamilies\": [ { \"Family\": 0 } ], " + LevelRow);
        var catalog = new NoKillXpFamilyCatalog(root.Path, [0]);

        Assert.IsFalse(Character.GivesNoKillXp(catalog, new GameplayXp(), (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.Unset));
    }

    [TestMethod]
    public void AnAbsentListOrFileFallsBackToGameplayJson()
    {
        using var root = new TempRoot();
        var noFile = new NoKillXpFamilyCatalog(root.Path, [0, 7]);
        Assert.IsFalse(noFile.FromFile);
        Assert.IsTrue(noFile.GivesNoKillXp(7));

        root.WriteXp(LevelRow);
        var noKey = new NoKillXpFamilyCatalog(root.Path, [0, 7]);
        Assert.IsFalse(noKey.FromFile);
        Assert.IsTrue(noKey.GivesNoKillXp(7));
        Assert.IsFalse(noKey.GivesNoKillXp(219));
    }

    [TestMethod]
    public void ABadFileKeepsTheOldSetAndReportsWhy()
    {
        using var root = new TempRoot();
        root.WriteXp("\"NoKillXpFamilies\": [ { \"Family\": 219 } ], " + LevelRow);
        var catalog = new NoKillXpFamilyCatalog(root.Path, [0]);

        root.WriteXp("\"NoKillXpFamilies\": [ { \"Note\": \"no family\" } ], " + LevelRow);
        var missingFamily = catalog.Reload();
        Assert.IsFalse(missingFamily.Ok);
        Assert.AreEqual(1, missingFamily.Problems.Count);
        Assert.IsTrue(catalog.GivesNoKillXp(219));

        File.WriteAllText(root.XpPath, "{ not json");
        var unparsable = catalog.Reload();
        Assert.IsFalse(unparsable.Ok);
        Assert.IsTrue(catalog.GivesNoKillXp(219));
    }

    [TestMethod]
    public void AReloadChangesTheNextKill()
    {
        using var root = new TempRoot();
        root.WriteXp("\"NoKillXpFamilies\": [ { \"Family\": 0 } ], " + LevelRow);
        var catalog = new NoKillXpFamilyCatalog(root.Path, [0]);
        var reload = new NoKillXpFamiliesReload(catalog);
        Assert.AreEqual(NoKillXpFamilyCatalog.ReloadName, reload.Name);
        Assert.IsFalse(Character.GivesNoKillXp(catalog, new GameplayXp(), 219));

        root.WriteXp("\"NoKillXpFamilies\": [ { \"Family\": 0 }, { \"Family\": 219 } ], " + LevelRow);
        var result = reload.Reload();

        Assert.IsTrue(result.Ok);
        Assert.AreEqual(2, result.Entries);
        Assert.IsTrue(Character.GivesNoKillXp(catalog, new GameplayXp(), 219));
    }

    sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nokillxp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string XpPath => System.IO.Path.Combine(Path, NoKillXpFamilyCatalog.FileName);

        public void WriteXp(string body) => File.WriteAllText(XpPath, "{ " + body + " }");

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
