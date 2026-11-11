namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.Core.Encryption;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Shops;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Accounts;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Metrics;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.NpcEditor;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Teams;
using ZoneEngine_New.Core.Trade;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class NpcEditorConnectedTests
{
    const string TemplateHash = "EDTR";
    const int ArmorId = 1301;
    const int DropId = 1302;
    const int Quality = 20;

    [TestMethod]
    public async Task AuthenticatedRoutesApplyLiveArmorAndSaveLootAndEquipmentForFutureSpawns()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        CancellationToken ct = deadline.Token;
        string temp = Directory.CreateTempSubdirectory("aorebirth-npc-editor-connected-").FullName;
        try
        {
            string root = Path.Combine(temp, "GameData");
            string overlay = Path.Combine(temp, "Overlay");
            Directory.CreateDirectory(Path.Combine(root, GameDataPaths.PlayfieldsFolderName));
            Directory.CreateDirectory(overlay);
            string templates = Path.Combine(root, GameDataPaths.NpcTemplatesFileName);
            string authored = $$$"""
                {"{{{TemplateHash}}}":{"Templates":[{"Name":"Editor fixture","Level":{{{Quality}}},
                 "Stats":{"{{{(int)CharacterStat.MaxHealth}}}":100,"{{{(int)CharacterStat.Health}}}":100}}]}}
                """;
            File.WriteAllText(templates, authored);
            File.WriteAllText(Path.Combine(root, GameDataPaths.ItemTemplatesFileName), $$$"""
                {"DROP":{"Templates":[{{{DropId}}}]}}
                """);

            var logger = new StubLogger();
            var data = new GameDataStore(logger, null, root, overlay);
            var catalog = new StubCatalog().Add(ArmorId, Quality).Add(DropId, Quality);
            catalog.Require(ArmorId).Stats[CharacterStat.ItemClass] = (int)ItemClass.Armor;
            catalog.Require(ArmorId).Stats[CharacterStat.DefaultPos] = 2;
            var ids = new StubInstanceIdAllocator();
            var items = new ItemBuilder(catalog, ids, logger);
            var minter = new HashItemMinter(data, catalog, items);
            var writes = new RejectPersistence();
            PlayfieldManager? manager = null;
            using var flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager!), writes, logger);
            using var snapshots = new CharacterSnapshotService(writes, writes, logger);
            using var world = new PlayfieldManager(logger, new MessageRouter([], logger), new PlayerHydrator(items, data),
                data, items, minter, new StubInventoryRepository(), ids,
                new InventoryMoveService(logger, flush, new InventoryActionService(writes, flush, ids, logger)),
                flush, new TradeService(logger, data, catalog, minter, ids, flush, writes), snapshots,
                new PlayfieldMetricsRegistry(), new TeamService(),
                new AuthoredQuestService(null!, flush, items, catalog, ids, new AuthoredQuestCatalog(new InteractionContent()), logger),
                catalog, new EmptyShops());
            manager = world;
            Playfield field = world.GetOrCreate(777);
            SpawnService spawns = field.GetRequiredService<SpawnService>();
            NpcCharacter first = await OnTick(field, () => spawns.Spawn(TemplateHash, new Vector3(0, 0, 0)), ct);
            NpcCharacter second = await OnTick(field, () => spawns.Spawn(TemplateHash, new Vector3(5, 0, 0)), ct);

            const string user = "editor-fixture";
            const string password = "fixture-only";
            var account = new MySqlAccountSanctionRepository.EditorCredentials(1, user, PasswordHash.CreateHash(password), 511);
            var sessions = new NpcEditorSessions(_ => account, TimeProvider.System, 1, 100, true, true);
            using var certificate = NpcEditorHost.CreateCertificate();
            await using var app = NpcEditorHost.Build(0, certificate, sessions, logger);
            NpcEditorHost.MapEditorRoutes(app, new NpcEditorRuntime(world), new NpcEditorLoot(data, catalog, minter),
                new NpcEditorEquipment(data, catalog), new NpcEditorItemBrowser(catalog, data));
            await app.StartAsync(ct);
            byte[] fingerprint = certificate.GetCertHash(HashAlgorithmName.SHA256);
            using var http = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, actual, _, _) => actual != null
                    && CryptographicOperations.FixedTimeEquals(actual.GetCertHash(HashAlgorithmName.SHA256), fingerprint)
            }) { BaseAddress = new Uri(app.Urls.Single()) };

            var session = await Post<NpcEditorSessionReply>(http, "/npc-editor/session", new { username = user, password }, ct);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            var rows = (await http.GetFromJsonAsync<NpcEditorNpc[]>("/npc-editor/npcs?q=Editor", ct))!;
            Assert.AreEqual(2, rows.Length);
            NpcEditorIdentity selected = rows.Single(row => row.Identity.InstanceId == first.Identity.Instance).Identity;
            string path = $"/npc-editor/npcs/{selected.PlayfieldId}/{selected.IdentityType}/{selected.InstanceId}/{selected.Generation:D}";
            var stats = (await http.GetFromJsonAsync<NpcEditorStat[]>(path + "/stats", ct))!;
            Assert.IsTrue(stats.Any(stat => stat.StatId == (int)CharacterStat.Level && stat.RuntimeValue == Quality));
            var configuredEquipment = (await http.GetFromJsonAsync<NpcEditorEquipmentState>(path + "/equipment", ct))!;
            var configuredLoot = (await http.GetFromJsonAsync<NpcEditorLootView>(path + "/loot", ct))!;
            Assert.AreEqual(2, configuredEquipment.EstimatedAffectedNpcs);
            Assert.AreEqual(2, configuredLoot.Saved.EstimatedAffectedNpcs);

            var worn = await Post<NpcEditorNpcDetails>(http, path + "/equipment", new NpcEditorEquipmentEdit(-1, 0, ArmorId, Quality), ct);
            Assert.AreEqual(ArmorId, worn.Equipment.Single().LowId);
            Assert.AreEqual(0, await OnTick(field, () => second.Equipment.Content.Count, ct));

            var equipment = await Post<NpcEditorEquipmentState>(http, path + "/equipment/save",
                new NpcEditorEquipmentSave(0, -1, 0, 0, ArmorId, Quality), ct);
            Assert.AreEqual(user, equipment.History.Single().Actor);
            var entry = new NpcEditorLootEntry("DROP", 100, 1, 0, Quality, Quality);
            var liveLoot = await Post<NpcEditorLootView>(http, path + "/loot/live", new NpcEditorLootLiveEdit(0, null, entry), ct);
            var simulation = await Post<NpcEditorLootSimulationResult>(http, path + "/loot/simulate", new NpcEditorLootSimulationRequest(10_000), ct);
            Assert.AreEqual(0, simulation.EmptyCorpses);
            Assert.AreEqual(10_000, simulation.Items.Single().TotalDrops);
            Assert.AreEqual(0, await OnTick(field, () => second.ActiveLootTable?.Count ?? 0, ct));
            await Post<NpcEditorLootView>(http, path + "/loot/live/revert",
                new NpcEditorLootLiveRevert(liveLoot.Live.Revision, liveLoot.Live.History.Single().ChangeId), ct);
            Assert.AreEqual(0, await OnTick(field, () => first.ActiveLootTable?.Count ?? 0, ct));

            var loot = await Post<NpcEditorLootState>(http, path + "/loot",
                new NpcEditorLootSave(0, null, entry, Scope: "templateBand"), ct);
            Assert.AreEqual(user, loot.History.Single().Actor);

            var generated = await OnTick(field, () =>
            {
                var fresh = spawns.Spawn(TemplateHash, new Vector3(10, 0, 0));
                var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 77701 }, fresh, data);
                corpse.ResolveLoot(minter, qualityVariancePercent: 0);
                return (Worn: fresh.Equipment.Content.Values.Single().LowId, Drop: corpse.Loot.Content.Values.Single());
            }, ct);
            Assert.AreEqual(ArmorId, generated.Worn);
            Assert.AreEqual(DropId, generated.Drop.LowId);
            Assert.AreEqual(Quality, generated.Drop.Quality);
            Assert.AreEqual(authored, File.ReadAllText(templates));
            await app.StopAsync(ct);

            File.WriteAllText(Path.Combine(overlay, NpcEditorLootJournalFile.FileName), "{");
            var reopened = new GameDataStore(logger, null, root, overlay);
            var restarted = new NpcCharacter(first.Identity, items) { MobTemplate = reopened.RequireMobTemplate(TemplateHash) };
            restarted.Stats.Set(CharacterStat.Level, Quality);
            restarted.FillEquipment(reopened, logger, catalog);
            Assert.AreEqual(ArmorId, restarted.Equipment.Content.Values.Single().LowId);
            Assert.AreEqual(0, restarted.MobTemplate.ItemTable.Count);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    static async Task<T> Post<T>(HttpClient http, string path, object body, CancellationToken ct) where T : class
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(path, body, ct);
        Assert.IsTrue(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
    }

    static Task<T> OnTick<T>(Playfield field, Func<T> work, CancellationToken ct)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(field.Dispatch(() =>
        {
            try { result.SetResult(work()); }
            catch (Exception ex) { result.SetException(ex); }
        }));
        return result.Task.WaitAsync(ct);
    }

    sealed class EmptyShops : IShopDao
    {
        public IList<ShopVendorData> ListForPlayfield(int playfieldId) => [];
    }

    sealed class RejectPersistence : ICharacterRepository, IStatRepository, ICharacterCoalesceCommit,
        IInventoryMutationPersistence, ITradePersistence
    {
        public CharacterRecord? GetById(int characterId) => throw new AssertFailedException("Unexpected character lookup.");
        public void SetOnline(int characterId) => throw new AssertFailedException("Unexpected character lease.");
        public void SetOffline(int characterId) => throw new AssertFailedException("Unexpected character lease.");
        public void SaveLocation(CharacterRecord character, int online) => throw new AssertFailedException("Unexpected character save.");
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) => throw new AssertFailedException("Unexpected character save.");
        public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats)
            => throw new AssertFailedException("Unexpected character save.");
        public IReadOnlyList<StatRecord> GetForCharacter(int characterId) => throw new AssertFailedException("Unexpected character lookup.");
        public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats) => throw new AssertFailedException("Unexpected stat save.");
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates,
            int characterId, IReadOnlyList<int> nanos, IReadOnlyList<ActiveNanoRecord>? activeNanos, IReadOnlyList<SkillLockRecord>? skillLocks)
            => throw new AssertFailedException("Unexpected inventory save.");
        public void Persist(InventoryMutationBatch batch) => throw new AssertFailedException("Unexpected inventory mutation.");
        public void Persist(TradePersistenceBatch batch) => throw new AssertFailedException("Unexpected trade save.");
    }
}
