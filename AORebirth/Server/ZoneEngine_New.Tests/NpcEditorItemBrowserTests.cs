namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using AORebirth.Core.GameData;
using AORebirth.Enums;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Inventory.Dat;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorItemBrowserTests
{
    [TestMethod]
    public void PartialNamesPageInStableOrderAndExactIdsHonorFilters()
    {
        var catalog = new Catalog(
            Item(23, "Pistol", 80, ItemClass.Weapon), Item(21, "pistol", 45, ItemClass.Weapon),
            Item(25, "Pistol Kit", 10, ItemClass.Misc),
            new ItemTemplate { Id = 24, Name = "Nano Formula", Quality = 42, DynelType = (int)IdentityType.NanoProgram });
        var browser = new NpcEditorItemBrowser(catalog, new StubGameData(HashItemCatalog.Parse(null)));

        NpcEditorItemPage first = browser.Search("PIST", "weapon", 40, 90, 0, 1, false, CancellationToken.None);
        Assert.AreEqual(21, first.Rows.Single().Id);
        Assert.IsTrue(first.HasMore);
        NpcEditorItemPage second = browser.Search("PIST", "weapon", 40, 90, first.NextOffset, 1, false, CancellationToken.None);
        Assert.AreEqual(23, second.Rows.Single().Id);
        Assert.IsFalse(second.HasMore);
        Assert.AreEqual(24, browser.Search("24", "Nano", 40, 45, 0, 20, false, CancellationToken.None).Rows.Single().Id);
        Assert.AreEqual(0, browser.Search("24", "Misc", null, null, 0, 20, false, CancellationToken.None).Rows.Length);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            browser.Search(null, null, 60, 40, 0, 20, false, CancellationToken.None)).StatusCode);
    }

    [TestMethod]
    public void FamilySearchPreviewsMatchingQualityAndPreservesNativeIds()
    {
        var catalog = new Catalog(Item(21, "Small Pistol", 45, ItemClass.Weapon), Item(23, "Large Pistol", 80, ItemClass.Weapon));
        var data = new StubGameData(HashItemCatalog.Parse("""
            {"PIST":{"Templates":[21,23]},"MISS":{"Templates":[23,99]},"POOL":{"Children":["PIST"]}}
            """));
        var browser = new NpcEditorItemBrowser(catalog, data);

        NpcEditorItemRow match = browser.Search("pistol", "Weapon", 75, 90, 0, 40, true, CancellationToken.None).Rows.Single();
        Assert.AreEqual("PIST", match.Hash);
        Assert.AreEqual(23, match.Id);
        CollectionAssert.AreEqual(new[] { 21, 23 }, match.TemplateIds);
        Assert.AreEqual(45, match.MinQuality);
        Assert.AreEqual(80, match.MaxQuality);
        Assert.AreEqual(21, browser.Search("21", null, null, null, 0, 40, true, CancellationToken.None).Rows.Single().Id);
    }

    [TestMethod]
    public void PreviewUsesTheDescriptionAndStatsAlreadyInTheRdbRecord()
    {
        ItemTemplate item = DatItemMapper.ToTemplate(new DatItemTemplate
        {
            ID = 21,
            Quality = 45,
            Rdb = new DatRdbTemplate { Description = "Catalog description" },
            Stats = { [(int)CharacterStat.ItemClass] = (int)ItemClass.Weapon, [(int)CharacterStat.AttackRange] = 20 }
        }, "Pistol");
        var browser = new NpcEditorItemBrowser(new Catalog(item), new StubGameData(HashItemCatalog.Parse(null)));

        NpcEditorItemDetails preview = browser.Get(21);
        Assert.AreEqual("Catalog description", preview.Description);
        Assert.AreEqual(20, preview.Stats.Single(stat => stat.Id == (int)CharacterStat.AttackRange).Value);
        Assert.AreEqual(404, Assert.ThrowsExactly<NpcEditorRuntimeException>(() => browser.Get(999)).StatusCode);
    }

    [TestMethod]
    public void SearchesARealAoItemInTheSelectedReadOnlyCatalog()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            Assert.Inconclusive("Set AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT to an existing RDB-derived GameData root.");
        Assert.IsTrue(File.Exists(Path.Combine(root!, GameDataPaths.ItemsFileName)), "The selected RDB-derived catalog is unavailable.");
        var data = new StubGameData(HashItemCatalog.Parse(null), rootPath: root);
        var browser = new NpcEditorItemBrowser(new ItemTemplateCatalog(data, new StubLogger()), data);

        const int PeacekeeperHelmet = 286563;
        NpcEditorItemRow row = browser.Search(PeacekeeperHelmet.ToString(CultureInfo.InvariantCulture),
            null, null, null, 0, 20, false, CancellationToken.None).Rows.Single();
        Assert.AreEqual(PeacekeeperHelmet, row.Id);
        Assert.IsTrue(row.Name.Contains("Peacekeeper Helmet", StringComparison.OrdinalIgnoreCase), row.Name);
        Assert.IsTrue(browser.Search("Peacekeeper Helmet", null, null, null, 0, 100, false, CancellationToken.None)
            .Rows.Any(item => item.Id == PeacekeeperHelmet));
    }

    static ItemTemplate Item(int id, string name, int quality, ItemClass category)
        => new() { Id = id, Name = name, Quality = quality, Stats = { [CharacterStat.ItemClass] = (int)category } };

    sealed class Catalog(params ItemTemplate[] templates) : IItemTemplateCatalog
    {
        readonly Dictionary<int, ItemTemplate> _byId = templates.ToDictionary(item => item.Id);
        public IReadOnlyCollection<ItemTemplate> Templates => _byId.Values;
        public bool TryGet(int aoid, out ItemTemplate template) => _byId.TryGetValue(aoid, out template!);
        public ItemTemplate Require(int aoid) => _byId[aoid];
    }
}
