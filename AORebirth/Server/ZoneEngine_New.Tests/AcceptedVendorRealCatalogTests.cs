namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Trade;

[TestClass]
public sealed class AcceptedVendorRealCatalogTests
{
    [TestMethod]
    public void PackagedRealCatalogResolvesEveryAcceptedShopAndPreservesItsTemplatePricing()
    {
        string dataRoot = Path.Combine(AppContext.BaseDirectory, "GameData");
        string path = Path.Combine(dataRoot, "items.dat");
        Assert.IsTrue(File.Exists(path), "The repository GameData/items.dat must be packaged; no synthetic catalog is permitted.");
        var data = new StubGameData(HashItemCatalog.Parse("{}", "{}")) { RootPath = dataRoot };
        // No SQL/name rows are supplied: the real catalog cannot turn missing templates into name-only stubs.
        var catalog = new ItemTemplateCatalog(new NoNames(), data, new StubLogger());
        var items = new ItemBuilder(catalog, new StubLogger());
        var endpoints = Endpoints();
        var missing = new List<string>();
        var pricing = new List<object>();
        foreach (var endpoint in endpoints)
        {
            bool resolved = catalog.TryGet(endpoint.TemplateId, out var template);
            if (!resolved) missing.Add(endpoint.Source + " vendorTemplate=" + endpoint.TemplateId);
            int? buy = resolved && template.Stats.TryGetValue(CharacterStat.BuyModifier, out int b) ? b : null;
            int? sell = resolved && template.Stats.TryGetValue(CharacterStat.SellModifier, out int s) ? s : null;
            pricing.Add(new { endpoint.Source, endpoint.TemplateId, BuyModifier426 = buy, SellModifier427 = sell,
                StockRows = endpoint.Stock.Count });
            for (int slot = 0; slot < endpoint.Stock.Count; slot++)
            {
                var row = endpoint.Stock[slot];
                if (!catalog.TryGet(row.LowId, out _)) missing.Add(endpoint.Source + " slot=" + slot + " lowId=" + row.LowId);
                if (!catalog.TryGet(row.HighId, out _)) missing.Add(endpoint.Source + " slot=" + slot + " highId=" + row.HighId);
            }
        }
        using (var source = File.OpenRead(path))
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "accepted-vendors-items.audit.json"),
                JsonSerializer.Serialize(new
                {
                    Authority = "Packaged repository GameData/items.dat; production ItemTemplateCatalog; no SQL/name fallback",
                    SourceSha256 = Convert.ToHexString(SHA256.HashData(source)),
                    NpcEndpoints = 19, StandaloneEndpoints = 3, Endpoints = pricing,
                    Missing = missing.Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.AreEqual(22, endpoints.Count);
        Assert.AreEqual(0, missing.Count, string.Join(Environment.NewLine, missing));

        var definitions = AcceptedSocialNpcCatalog.Definitions.Where(definition => definition.Binding.HasVendor).ToArray();
        Assert.AreEqual(19, definitions.Length);
        foreach (var definition in definitions)
        {
            var npc = definition.Create(items);
            bool attached;
            string failure;
            if (npc is AcceptedSubwayMerchantCatalog.AcceptedSubwayMerchantCharacter)
                attached = AcceptedSubwayMerchantCatalog.TryAttachShop(npc, items, catalog, out failure);
            else if (npc is AcceptedAreteVendorCatalog.AcceptedAreteVendorCharacter)
                attached = AcceptedAreteVendorCatalog.TryAttachShop(npc, items, catalog, out failure);
            else
                attached = AcceptedGardenVendorCatalog.TryAttachShop(npc, items, catalog, out failure);
            Assert.IsTrue(attached, definition.Binding.ContentNpcIdentity + ": " + failure);
            AssertTemplatePricing(npc.Shop!, catalog);
        }
        using var world = new AuthoredQuestTests.World(6553);
        foreach (var definition in AcceptedAreteVendorCatalog.StandaloneDefinitions)
        {
            Assert.IsTrue(AcceptedAreteVendorCatalog.TryCreateStandaloneShop(definition, world.Player.Playfield!, catalog,
                out var shop, out var failure), definition.SourceIdentity + ": " + failure);
            AssertTemplatePricing(shop, catalog);
        }
    }

    static void AssertTemplatePricing(VendingMachine shop, IItemTemplateCatalog catalog)
    {
        var source = catalog.Require(shop.Template.Id);
        // The exact Legacy captured-vendor int-template constructor copies these same stats;
        // it does not use the separate hash/DAO vendor-pricing override.
        Assert.IsTrue(source.Stats.TryGetValue(CharacterStat.BuyModifier, out int buy), "Missing item stat426: " + source.Id);
        Assert.IsTrue(source.Stats.TryGetValue(CharacterStat.SellModifier, out int sell), "Missing item stat427: " + source.Id);
        Assert.AreEqual(buy, shop.BuyModifier); Assert.AreEqual(sell, shop.SellModifier);
        Assert.IsTrue(sell > 0, "Nonpositive actual sell modifier requires explicit review: " + source.Id + " value=" + sell);
        Assert.IsTrue(buy >= 0, "Negative actual buy modifier requires explicit review: " + source.Id + " value=" + buy);
        Assert.IsTrue(shop.Stock.IsAcceptedSnapshot);
    }

    sealed record Endpoint(string Source, int TemplateId, IReadOnlyList<ShopStockSlot> Stock);
    static List<Endpoint> Endpoints()
    {
        var result = new List<Endpoint>();
        foreach (var row in CapturedSubwayVendorContentProvider.Definitions)
            result.Add(new("npc:" + row.SourceNpcInstance.ToString("X8"), row.VendorTemplateId,
                row.Stock.Select(stock => new ShopStockSlot(stock.LowId, stock.HighId, stock.Quality)).ToArray()));
        result.Add(new("npc:" + CapturedAreteMarcoSpidaVendorContentProvider.SourceNpcInstance.ToString("X8"),
            CapturedAreteMarcoSpidaVendorContentProvider.CaptureVendorTemplateId,
            CapturedAreteMarcoSpidaVendorContentProvider.Stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray()));
        result.Add(new("npc:" + CapturedAreteLoreleiVendorContentProvider.SourceNpcInstance.ToString("X8"),
            CapturedAreteLoreleiVendorContentProvider.CaptureVendorTemplateId,
            CapturedAreteLoreleiVendorContentProvider.Stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray()));
        foreach (var row in AcceptedGardenVendorCatalog.Placements)
            result.Add(new("npc:" + row.SourceNpcInstance.ToString("X8"), row.VendorTemplateId,
                row.Stock.Select(stock => new ShopStockSlot(stock.LowId, stock.HighId, stock.Quality)).ToArray()));
        foreach (var row in AcceptedAreteVendorCatalog.StandaloneDefinitions)
            result.Add(new("machine:" + row.SourceVendorInstance.ToString("X8"), row.Content.TemplateId,
                row.Content.Stock.Select(stock => new ShopStockSlot(stock.LowId, stock.HighId, stock.Quality)).ToArray()));
        return result;
    }

    sealed class NoNames : IItemNameRepository
    {
        public bool TryGetName(int aoid, out string name) { name = string.Empty; return false; }
        public IReadOnlyDictionary<int, string> GetAllNames() => new Dictionary<int, string>();
    }
}
