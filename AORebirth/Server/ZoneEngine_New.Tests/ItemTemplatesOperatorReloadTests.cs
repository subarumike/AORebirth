namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ZoneEngine_New.Core.DailyLogin;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Quests;

[TestClass]
public sealed class ItemTemplatesOperatorReloadTests
{
    const string Token = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    const string Route = "/v1/item-templates";
    const string Before = """
        {"NEST":{"Children":["ITEM"]},"ITEM":{"Templates":[1,2]}}
        """;
    const string After = """
        {"NEST":{"Children":["ITEM"]},"ITEM":{"Templates":[3]},"ADDD":{"Templates":[3]}}
        """;

    [TestMethod]
    public async Task AuthenticatedValidationReloadAndRollbackUseTheRealCatalogAndCaches()
    {
        using var fixture = new Fixture();
        await using var host = await fixture.Start();
        using var client = new HttpClient { BaseAddress = new Uri(host.Address) };
        using (HttpResponseMessage unauthorized = await client.GetAsync(Route))
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        Assert.AreEqual(Digest(Before), await Loaded(client));
        Assert.IsTrue(fixture.Minter.HasScalingFamily("NEST"));
        Assert.IsFalse(fixture.Data.CanResolveItemHash("ADDD"));

        using (HttpResponseMessage checkedCatalog = await Validate(client, After))
        {
            Assert.AreEqual(HttpStatusCode.OK, checkedCatalog.StatusCode, await checkedCatalog.Content.ReadAsStringAsync());
            Assert.AreEqual(Digest(After), await ReadSha(checkedCatalog));
        }
        Assert.AreEqual(Digest(Before), await Loaded(client));
        Assert.AreEqual(Before, File.ReadAllText(fixture.Catalog));

        fixture.Activate(After);
        using (HttpResponseMessage mismatch = await Reload(client, Before))
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        Assert.AreEqual(Digest(Before), await Loaded(client));

        using (HttpResponseMessage updated = await Reload(client, After))
        {
            Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode, await updated.Content.ReadAsStringAsync());
            Assert.AreEqual(Digest(After), await ReadSha(updated));
        }
        Assert.IsFalse(fixture.Minter.HasScalingFamily("NEST"));
        Assert.IsTrue(fixture.Data.TryGetHashInstance("ITEM", out HashInstance after));
        CollectionAssert.AreEqual(new[] { 3 }, after.TemplateIds);
        Assert.AreEqual(Digest(After), await Loaded(client));
        fixture.Activate(Before);
        using (HttpResponseMessage restored = await Reload(client, Before))
            Assert.AreEqual(HttpStatusCode.OK, restored.StatusCode, await restored.Content.ReadAsStringAsync());
        Assert.AreEqual(Digest(Before), await Loaded(client));
        Assert.IsTrue(fixture.Minter.HasScalingFamily("NEST"));
        Assert.IsFalse(fixture.Data.CanResolveItemHash("ADDD"));
    }

    [DataTestMethod]
    [DataRow("{ invalid json")]
    [DataRow("{\"NEST\":{\"Children\":[\"NOPE\"]},\"ITEM\":{\"Templates\":[1,2]}}")]
    [DataRow("{\"NEST\":{\"Children\":[\"ITEM\"]},\"ITEM\":{\"Templates\":[999]}}")]
    [DataRow("{\"NEST\":{\"Children\":[\"NEST\"]},\"ITEM\":{\"Templates\":[1,2]}}")]
    [DataRow("{\"NEST\":{\"Children\":[\"ITEM\"]},\"ITEM\":{\"Templates\":[1]},\"ITEM\":{\"Templates\":[3]}}")]
    public async Task InvalidCatalogPreservesLoadedBytesAndCaches(string invalid)
    {
        using var fixture = new Fixture();
        await using var host = await fixture.Start();
        using var client = OperatorClient(host);
        Assert.IsTrue(fixture.Minter.HasScalingFamily("NEST"));
        using (HttpResponseMessage result = await Validate(client, invalid))
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        Assert.AreEqual(Before, File.ReadAllText(fixture.Catalog));
        fixture.Activate(invalid);
        using (HttpResponseMessage result = await Reload(client, invalid))
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        Assert.AreEqual(Digest(Before), await Loaded(client));
        Assert.IsTrue(fixture.Minter.HasScalingFamily("NEST"));
    }

    [TestMethod]
    public async Task DependentValidationFailureAndConcurrentRequestsPreserveCoherentState()
    {
        using var fixture = new Fixture();
        await using var host = await fixture.Start();
        using var client = OperatorClient(host);
        string scripts = Directory.CreateDirectory(Path.Combine(fixture.Root, "Knubot")).FullName;
        string broken = Path.Combine(scripts, "broken.json");
        File.WriteAllText(broken, "{");
        fixture.Activate(After);
        using (HttpResponseMessage refused = await Reload(client, After))
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.AreEqual(Digest(Before), await Loaded(client));
        Assert.AreEqual(0, fixture.Knubot.NpcCount);
        File.Delete(broken);

        HttpResponseMessage[] concurrent = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Reload(client, After)));
        foreach (HttpResponseMessage result in concurrent)
        {
            using (result)
            {
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode, await result.Content.ReadAsStringAsync());
                Assert.AreEqual(Digest(After), await ReadSha(result));
            }
        }
        Assert.AreEqual(Digest(After), await Loaded(client));
        fixture.Activate(Before);
        using HttpResponseMessage restored = await Reload(client, Before);
        Assert.AreEqual(HttpStatusCode.OK, restored.StatusCode, await restored.Content.ReadAsStringAsync());
        Assert.AreEqual(Digest(Before), await Loaded(client));
    }

    [TestMethod]
    public void ExactInheritedMissingEdgesStayScopedToUnchangedRows()
    {
        const string inherited = """
            {"NEST":{"Children":["ITEM","GAP"]},"ITEM":{"Templates":[1]}}
            """;
        using var fixture = new Fixture(inherited);
        var same = fixture.Registry.VerifyItemTemplates(Digest(inherited), Encoding.UTF8.GetBytes(inherited), fixture.Items, out _);
        Assert.IsTrue(same.Ok, string.Join(" ", same.Problems));
        string touched = inherited.Replace("\"ITEM\",\"GAP\"", "\"GAP\",\"ITEM\"", StringComparison.Ordinal);
        var changed = fixture.Registry.VerifyItemTemplates(Digest(touched), Encoding.UTF8.GetBytes(touched), fixture.Items, out var status);
        Assert.IsFalse(changed.Ok);
        Assert.AreEqual(Digest(inherited), status.Sha256);
    }

    [TestMethod]
    public void UnboundedHashDepthAndUnavailableItemIndexFailClosed()
    {
        using var fixture = new Fixture();
        var rows = Enumerable.Range(0, 256).ToDictionary(i => "LEVEL" + i,
            i => (object)new { Children = new[] { i == 255 ? "ITEM" : "LEVEL" + (i + 1) } });
        rows.Add("NEST", new { Children = new[] { "LEVEL0" } });
        rows.Add("ITEM", new { Templates = new[] { 1, 2 } });
        string deep = JsonSerializer.Serialize(rows);
        var excessive = fixture.Registry.VerifyItemTemplates(Digest(deep), Encoding.UTF8.GetBytes(deep), fixture.Items, out _);
        Assert.IsFalse(excessive.Ok);
        StringAssert.Contains(excessive.Problems.Single(), "exceed");

        var unavailable = fixture.Registry.VerifyItemTemplates(Digest(After), Encoding.UTF8.GetBytes(After), new StubCatalog(), out var status);
        Assert.IsFalse(unavailable.Ok);
        StringAssert.Contains(unavailable.Problems.Single(), "items.dat");
        Assert.AreEqual(Digest(Before), status.Sha256);
    }

    [TestMethod]
    public void QuestBindingsFailClosedAndDailyLoginReusesOnlyTheCurrentFamilyCache()
    {
        using var fixture = new Fixture();
        var rewards = DailyLoginRewardCatalog.Parse("""
            {"Days":{"1":{"ItemId":1,"QualityMode":"characterLevel"}}}
            """);
        var selector = new DailyLoginRewardSelector(fixture.Items, rewards, fixture.Data);
        Assert.IsTrue(selector.TrySelect(1, 50, 1, new Random(1), out var original, out _));
        Assert.AreEqual(2, original!.HighId);

        string path = Path.Combine(fixture.Root, "Quests.json");
        File.WriteAllText(path, """
            [{"Hash":"QUEST","StartItems":[{"Hash":"ITEM","Quality":50}]}]
            """);
        Assert.IsTrue(fixture.Quests.Reload().Ok);
        var blocked = fixture.Registry.VerifyItemTemplates(Digest(After), Encoding.UTF8.GetBytes(After), fixture.Items, out _);
        Assert.IsFalse(blocked.Ok);
        StringAssert.Contains(blocked.Problems.Single(), "QUEST");

        File.Delete(path);
        Assert.IsTrue(fixture.Quests.Reload().Ok);
        fixture.Activate(After);
        var activated = fixture.Registry.VerifyItemTemplates(Digest(After), null, fixture.Items, out _);
        Assert.IsTrue(activated.Ok, string.Join(" ", activated.Problems));
        Assert.IsTrue(selector.TrySelect(1, 50, 1, new Random(1), out var changed, out _));
        Assert.AreEqual(1, changed!.HighId);
    }

    [TestMethod]
    public void EquipmentPoolActivatesThroughTheSameCatalogUsedForLoot()
    {
        const string initial = """
            {"ITEM":{"Templates":[1]},"EQUIP":{"Templates":[2],"EquipmentLootOnly":true},
             "POOL":{"Children":["EQUIP"],"EquipmentLoot":{"EQUIP":{"Weight":1,"MinCenter":50,"MaxCenter":200}}}}
            """;
        using var fixture = new Fixture(initial);
        Assert.AreEqual(1, fixture.Minter.GetEquipmentLootMembers("POOL")!.Single().Weight);
        string next = initial.Replace("\"Weight\":1", "\"Weight\":4", StringComparison.Ordinal)
            .Replace("[2]", "[3]", StringComparison.Ordinal);
        fixture.Activate(next);
        var activated = fixture.Registry.VerifyItemTemplates(Digest(next), null, fixture.Items, out _);
        Assert.IsTrue(activated.Ok, string.Join(" ", activated.Problems));
        EquipmentLootMember member = fixture.Minter.GetEquipmentLootMembers("POOL")!.Single();
        Assert.AreEqual(4, member.Weight);
        CollectionAssert.AreEqual(new[] { 3 }, member.Family.TemplateIds);
        Assert.IsFalse(fixture.Data.CanResolveItemHash("EQUIP"));
    }

    static HttpClient OperatorClient(ItemTemplatesReloadHost host)
    {
        var client = new HttpClient { BaseAddress = new Uri(host.Address) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return client;
    }

    static Task<HttpResponseMessage> Reload(HttpClient client, string json)
        => client.PostAsync(Route, new StringContent(JsonSerializer.Serialize(new { sha256 = Digest(json) }), Encoding.UTF8, "application/json"));

    static async Task<HttpResponseMessage> Validate(HttpClient client, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route + "/validate")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-AORebirth-Content-Sha256", Digest(json));
        return await client.SendAsync(request);
    }

    static async Task<string?> Loaded(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(Route);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return await ReadSha(response);
    }

    static async Task<string?> ReadSha(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
        return document.RootElement.GetProperty("sha256").GetString();
    }

    static string Digest(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    sealed class Fixture : IDisposable
    {
        internal Fixture(string initial = Before)
        {
            Root = Directory.CreateTempSubdirectory("ao-itemtemplates-operator-").FullName;
            Catalog = Path.Combine(Root, "ItemTemplates.json");
            File.WriteAllText(Catalog, initial);
            var logger = new StubLogger();
            Data = new GameDataStore(logger, null, Root);
            Items = new StubCatalog().Add(1, 1).Add(2, 200).Add(3, 80);
            Minter = new HashItemMinter(Data, Items, new StubItemBuilder());
            Quests = new QuestCatalog(Data, logger, Minter);
            Knubot = new KnubotCatalog(Data, Quests, Items, logger);
            Registry = new GameDataReloadRegistry([new ItemTemplatesReload(Data, Knubot, Minter)]);
        }

        internal string Root { get; }
        internal string Catalog { get; }
        internal GameDataStore Data { get; }
        internal StubCatalog Items { get; }
        internal HashItemMinter Minter { get; }
        internal QuestCatalog Quests { get; }
        internal KnubotCatalog Knubot { get; }
        internal GameDataReloadRegistry Registry { get; }

        internal Task<ItemTemplatesReloadHost> Start() => ItemTemplatesReloadHost.StartAsync(Registry, Items, 0, Token);

        internal void Activate(string json)
        {
            string staged = Catalog + ".staged";
            File.WriteAllText(staged, json);
            File.Move(staged, Catalog, overwrite: true);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
