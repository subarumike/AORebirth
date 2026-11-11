namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
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
using ZoneEngine_New.Core.Logging;
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
    public async Task AuthenticatedRoutesEditLiveNpcsAndSaveStatsLootAndEquipmentForFutureSpawns()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
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
                {"PARENT":{"Children":["{{{TemplateHash}}}"]},
                 "{{{TemplateHash}}}":{"Templates":[{"Name":"Editor fixture","Level":{{{Quality}}},
                 "Stats":{"{{{(int)CharacterStat.MaxHealth}}}":100,"{{{(int)CharacterStat.Health}}}":100,
                          "{{{(int)CharacterStat.BreedHostility}}}":0}}]}}
                """;
            File.WriteAllText(templates, authored);
            File.WriteAllText(Path.Combine(root, GameDataPaths.ItemTemplatesFileName), $$$"""
                {"DROP":{"Templates":[{{{DropId}}}]}}
                """);

            var logger = new ConnectedLogger();
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
            var namedLoot = new NpcEditorNamedLoot(data, catalog);
            var lootEvidence = new NpcEditorLootEvidence(root, overlay, logger);
            NpcEditorHost.MapEditorRoutes(app, new NpcEditorRuntime(world, data), new NpcEditorLoot(data, catalog, minter),
                new NpcEditorEquipment(data, catalog), new NpcEditorItemBrowser(catalog, data),
                new NpcEditorStats(data), new NpcEditorSpawn(data), namedLoot, lootEvidence,
                new NpcEditorLootCoverage(data, catalog, namedLoot, lootEvidence));
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
            NpcEditorIdentity other = rows.Single(row => row.Identity.InstanceId == second.Identity.Instance).Identity;
            CollectionAssert.AreEqual(new[] { "PARENT" }, rows[0].FamilyHashes);
            Assert.AreEqual(2, (await http.GetFromJsonAsync<NpcEditorNpc[]>("/npc-editor/npcs?q=PARENT", ct))!.Length);
            string path = NpcPath(selected);
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

            var configuredStats = (await http.GetFromJsonAsync<NpcEditorStatsState>(path + "/stats/saved", ct))!;
            Assert.AreEqual(2, configuredStats.EstimatedAffectedNpcs);
            var savedStats = await Post<NpcEditorStatsState>(http, path + "/stats/save",
                new NpcEditorStatsSave(0, (int)CharacterStat.MaxHealth, 150), ct);
            Assert.AreEqual(user, savedStats.History.Single().Actor);
            Assert.AreEqual(100, await OnTick(field, () => first.Stats.GetOrZero(CharacterStat.MaxHealth), ct));

            await VerifyLivePhaseThree(http, field, first, second, selected, other, logger, ct);
            await VerifyRespawnBatchAndDormantPlacement(http, world, root, overlay, user, logger, ct);
            await VerifyScopedStats(http, world, selected, ct);

            var generated = await OnTick(field, () =>
            {
                var fresh = spawns.Spawn(TemplateHash, new Vector3(10, 0, 0));
                var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 77701 }, fresh, data);
                corpse.ResolveLoot(minter, qualityVariancePercent: 0);
                return (Worn: fresh.Equipment.Content.Values.Single().LowId, Drop: corpse.Loot.Content.Values.Single(),
                    Maximum: fresh.Stats.GetOrZero(CharacterStat.MaxHealth), Current: fresh.Stats.GetOrZero(CharacterStat.Health));
            }, ct);
            Assert.AreEqual(ArmorId, generated.Worn);
            Assert.AreEqual(DropId, generated.Drop.LowId);
            Assert.AreEqual(Quality, generated.Drop.Quality);
            Assert.AreEqual(150, generated.Maximum);
            Assert.AreEqual(150, generated.Current);
            account = account with { GmLevel = 1 };
            using HttpResponseMessage denied = await http.PostAsJsonAsync(path + "/stats/save",
                new NpcEditorStatsSave(1, (int)CharacterStat.MaxHealth, 175), ct);
            Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.AreEqual(authored, File.ReadAllText(templates));
            await app.StopAsync(ct);

            File.WriteAllText(Path.Combine(overlay, NpcEditorLootJournalFile.FileName), "{");
            var reopened = new GameDataStore(logger, null, root, overlay);
            var restarted = new NpcCharacter(first.Identity, items) { MobTemplate = reopened.RequireMobTemplate(TemplateHash) };
            restarted.Stats.Set(CharacterStat.Level, Quality);
            restarted.FillEquipment(reopened, logger, catalog);
            Assert.AreEqual(ArmorId, restarted.Equipment.Content.Values.Single().LowId);
            Assert.AreEqual(0, restarted.MobTemplate.ItemTable.Count);
            Assert.AreEqual(150, restarted.MobTemplate.Stats[(int)CharacterStat.MaxHealth]);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    static string NpcPath(NpcEditorIdentity selected)
        => $"/npc-editor/npcs/{selected.PlayfieldId}/{selected.IdentityType}/{selected.InstanceId}/{selected.Generation:D}";

    static async Task VerifyLivePhaseThree(HttpClient http, Playfield field, NpcCharacter first, NpcCharacter second,
        NpcEditorIdentity selected, NpcEditorIdentity other, ConnectedLogger logger, CancellationToken ct)
    {
        string path = NpcPath(selected);
        int statId = (int)CharacterStat.MaxHealth;
        using HttpResponseMessage stale = await http.PostAsJsonAsync(
            NpcPath(selected with { Generation = Guid.NewGuid() }) + "/stats", new { statId, expectedBase = 100, value = 140 }, ct);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        var resolved = await http.GetFromJsonAsync<NpcEditorIdentity>(
            $"/npc-editor/target?playfieldId={field.ClientPlayfieldId}&identityType={selected.IdentityType}&instanceId={selected.InstanceId}", ct);
        Assert.AreEqual(selected, resolved);
        using HttpResponseMessage stalePresence = await http.GetAsync(NpcPath(selected with { Generation = Guid.NewGuid() }) + "/presence", ct);
        Assert.AreEqual(HttpStatusCode.Conflict, stalePresence.StatusCode);

        var behavior = (await http.GetFromJsonAsync<NpcEditorBehaviorView>(path + "/behavior", ct))!;
        var patrol = new[] { new NpcEditorPoint(0, 0, 0), new NpcEditorPoint(5, 0, 0) };
        var edited = await Post<NpcEditorBehaviorView>(http, path + "/behavior",
            new NpcEditorBehaviorEdit(behavior.Version, 75, true, true, patrol), ct, logger);
        Assert.AreEqual(75, edited.BreedHostility);
        Assert.IsTrue(edited.PatrolEnabled && edited.PatrolRun);
        Assert.AreEqual(2, edited.Waypoints.Length);
        Assert.AreEqual(0, await OnTick(field, () => second.Stats.GetOrZero(CharacterStat.BreedHostility), ct));
        Assert.IsFalse(await OnTick(field, () => second.Brain?.PatrolEnabled == true, ct));
        var undone = await Post<NpcEditorBehaviorView>(http, path + "/behavior/undo",
            new NpcEditorBehaviorUndo(edited.Version, edited.History.Single().ChangeId), ct);
        Assert.AreEqual(behavior.BreedHostility, undone.BreedHostility);
        Assert.AreEqual(behavior.PatrolEnabled, undone.PatrolEnabled);

        using HttpResponseMessage mixed = await http.PostAsJsonAsync("/npc-editor/batch/preview",
            new { operation = "stat", targets = new[] { selected, other with { PlayfieldId = selected.PlayfieldId + 1 } },
                statId, value = 140 }, ct);
        Assert.AreEqual(HttpStatusCode.BadRequest, mixed.StatusCode);
        var preview = await Post<NpcEditorBatchPreview>(http, "/npc-editor/batch/preview",
            new { operation = "stat", targets = new[] { selected, other }, statId, value = 140 }, ct);
        Assert.IsNotNull(preview.PlanId);
        Assert.AreEqual(2, preview.Rows.Length);
        await Post<NpcEditorNpcDetails>(http, NpcPath(other) + "/stats", new { statId, expectedBase = 100, value = 130 }, ct);
        var applied = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/apply", new { planId = preview.PlanId }, ct);
        Assert.IsTrue(applied.Rows[0].Applied);
        Assert.IsFalse(applied.Rows[1].Applied);
        Assert.IsNotNull(applied.Rows[1].Error);
        Assert.AreEqual(140, await OnTick(field, () => first.Stats.GetOrZero(CharacterStat.MaxHealth), ct));
        var rolledBack = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/undo", new { applied.BatchId }, ct);
        Assert.IsTrue(rolledBack.Rows[0].Applied);
        Assert.AreEqual(100, await OnTick(field, () => first.Stats.GetOrZero(CharacterStat.MaxHealth), ct));
        Assert.AreEqual(130, await OnTick(field, () => second.Stats.GetOrZero(CharacterStat.MaxHealth), ct));

        await ApplyAndUndoBatch(http, new { operation = "aggression", targets = new[] { selected, other }, value = 45 },
            () => OnTick(field, () => first.Stats.GetOrZero(CharacterStat.BreedHostility), ct), 45, 0, ct);
        await ApplyAndUndoBatch(http, new { operation = "armor", targets = new[] { other }, itemId = ArmorId, quality = Quality },
            () => OnTick(field, () => second.Equipment.Content.Count, ct), 1, 0, ct);
        await ApplyAndUndoBatch(http, new { operation = "loot", targets = new[] { selected, other },
                lootEntry = new NpcEditorLootEntry("DROP", 50, 1, 0, Quality, Quality) },
            () => OnTick(field, () => second.ActiveLootTable?.Count ?? 0, ct), 1, 0, ct);
    }

    static async Task VerifyScopedStats(HttpClient http, PlayfieldManager world, NpcEditorIdentity selected, CancellationToken ct)
    {
        string path = NpcPath(selected) + "/stats";
        const string family = "?scope=family&familyHash=PARENT";
        const string playfield = "?scope=playfield";
        const int statId = (int)CharacterStat.AMSModifier;
        var familyState = (await http.GetFromJsonAsync<NpcEditorStatsState>(path + "/saved" + family, ct))!;
        Assert.AreEqual("family", familyState.Scope);
        Assert.AreEqual(5, familyState.EstimatedAffectedNpcs);
        await Post<NpcEditorStatsState>(http, path + "/save" + family, new NpcEditorStatsSave(0, statId, 120), ct);
        var fieldState = (await http.GetFromJsonAsync<NpcEditorStatsState>(path + "/saved" + playfield, ct))!;
        Assert.AreEqual("playfield", fieldState.Scope);
        Assert.AreEqual(2, fieldState.EstimatedAffectedNpcs);
        await Post<NpcEditorStatsState>(http, path + "/save" + playfield, new NpcEditorStatsSave(0, statId, 250), ct);
        Assert.AreEqual(250, await NewSpawnBase(777));
        Assert.AreEqual(120, await NewSpawnBase(778));

        await Post<NpcEditorStatsState>(http, path + "/save", new NpcEditorStatsSave(1, statId, 180), ct);
        Assert.AreEqual(250, await NewSpawnBase(777));
        Assert.AreEqual(180, await NewSpawnBase(778));

        Task<int> NewSpawnBase(int resourceId)
        {
            Playfield field = world.GetOrCreate(resourceId);
            return OnTick(field, () => field.GetRequiredService<SpawnService>().Spawn(TemplateHash, new Vector3(5, 0, 5))
                .Stats.GetOrZero(CharacterStat.AMSModifier, StatDetail.Base), ct);
        }
    }

    static async Task ApplyAndUndoBatch(HttpClient http, object request, Func<Task<int>> read, int after, int before, CancellationToken ct)
    {
        var preview = await Post<NpcEditorBatchPreview>(http, "/npc-editor/batch/preview", request, ct);
        Assert.IsNotNull(preview.PlanId, string.Join(" / ", preview.Rows.Select(row => row.Error)));
        var applied = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/apply", new { planId = preview.PlanId }, ct);
        Assert.IsTrue(applied.CanUndo && applied.Rows.All(row => row.Applied), string.Join(" / ", applied.Rows.Select(row => row.Error)));
        Assert.AreEqual(after, await read());
        var undone = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/undo", new { applied.BatchId }, ct);
        Assert.IsFalse(undone.CanUndo);
        Assert.IsTrue(undone.Rows.All(row => row.Applied), string.Join(" / ", undone.Rows.Select(row => row.Error)));
        Assert.AreEqual(before, await read());
    }

    static async Task VerifyRespawnBatchAndDormantPlacement(HttpClient http, PlayfieldManager world, string root,
        string overlay, string actor, ConnectedLogger logger, CancellationToken ct)
    {
        const int playfieldId = 778;
        string source = Path.Combine(root, GameDataPaths.PlayfieldSpawnsRelativePath(playfieldId));
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        string authored = JsonSerializer.Serialize(new PlayfieldSpawnsData
        {
            SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
            PlayfieldId = playfieldId,
            Spawns = Enumerable.Range(0, 4).Select(index => new PlayfieldSpawnEntry
            {
                HashText = TemplateHash, Position = [10 + index * 10, 0, 20],
                MinLevel = Quality, MaxLevel = Quality, RespawnTime = 30 + index,
                RespawnChance = index == 3 ? 0 : 100
            }).ToArray()
        });
        File.WriteAllText(source, authored);
        Playfield field = world.GetOrCreate(playfieldId);
        var spawns = field.GetRequiredService<HashSpawnSystem>();
        int active = 0;
        for (int attempt = 0; attempt < 12 && active < 3; attempt++)
            active = await OnTick(field, () => Enumerable.Range(0, 3).Sum(spawns.CountAuthoredSpawnNpcs), ct);
        Assert.AreEqual(3, active, logger.LastException?.ToString());

        string pointsPath = $"/npc-editor/playfields/{playfieldId}/spawns";
        var list = (await http.GetFromJsonAsync<NpcEditorSpawnList>(pointsPath + "?offset=1&limit=2", ct))!;
        Assert.AreEqual(4, list.Total);
        CollectionAssert.AreEqual(new int?[] { 1, 2 }, list.Points.Select(point => point.AuthoredIndex).ToArray());
        var npcs = (await http.GetFromJsonAsync<NpcEditorNpc[]>($"/npc-editor/npcs?q={playfieldId}", ct))!;
        var targets = new Dictionary<int, NpcEditorIdentity>();
        foreach (NpcEditorNpc npc in npcs.Where(npc => npc.Identity.PlayfieldId == playfieldId))
        {
            var point = (await http.GetFromJsonAsync<NpcEditorSpawnView>(NpcPath(npc.Identity) + "/spawn", ct))!;
            targets.Add(point.AuthoredIndex!.Value, npc.Identity);
        }
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, targets.Keys.ToArray());

        var preview = await Post<NpcEditorBatchPreview>(http, "/npc-editor/batch/preview",
            new { operation = "respawn", targets = new[] { targets[0], targets[1] }, respawnSeconds = 75, respawnChance = 55 }, ct);
        Assert.IsNotNull(preview.PlanId, string.Join(" / ", preview.Rows.Select(row => row.Error)));
        Assert.AreEqual(2, preview.Rows.Length);
        Assert.IsFalse(File.Exists(Path.Combine(overlay, NpcEditorSpawnStore.FileName)));
        var applied = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/apply", new { planId = preview.PlanId }, ct);
        Assert.IsTrue(applied.CanUndo && applied.Rows.All(row => row.Applied), string.Join(" / ", applied.Rows.Select(row => row.Error)));
        for (int index = 0; index < 2; index++)
        {
            NpcEditorSpawnView changed = await ReadPoint(index);
            Assert.AreEqual(75, changed.Configured!.RespawnSeconds);
            Assert.AreEqual(55, changed.Configured.RespawnChance);
            Assert.AreEqual(30 + index, changed.Runtime!.RespawnSeconds);
            Assert.AreEqual(actor, changed.History.Single().Actor);
            Assert.IsTrue(changed.RequiresPlayfieldReload);
        }
        Assert.AreEqual(0, (await ReadPoint(2)).Revision);

        var undone = await Post<NpcEditorBatchResult>(http, "/npc-editor/batch/undo", new { applied.BatchId }, ct);
        Assert.IsFalse(undone.CanUndo);
        Assert.IsTrue(undone.Rows.All(row => row.Applied), string.Join(" / ", undone.Rows.Select(row => row.Error)));
        for (int index = 0; index < 2; index++)
        {
            NpcEditorSpawnView restored = await ReadPoint(index);
            Assert.AreEqual(restored.Original, restored.Configured);
            Assert.AreEqual(2, restored.Revision);
            Assert.AreEqual("restore", restored.History[^1].Action);
        }

        NpcEditorSpawnView dormant = await ReadPoint(3);
        Assert.AreEqual(0, dormant.Revision);
        Assert.AreEqual(0, dormant.LoadedNpcs);
        Assert.AreEqual("Dead", dormant.State);
        Assert.IsNotNull(dormant.AuthoredSignature);
        Assert.IsNull(dormant.LivePreview);
        NpcEditorSpawnSettings settings = dormant.Configured! with { RespawnSeconds = 125 };
        string dormantPath = pointsPath + "/3";
        using HttpResponseMessage stale = await http.PostAsJsonAsync(dormantPath + "/save",
            new { authoredSignature = new string('0', 64), expectedRevision = 0, settings }, ct);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        var savedDormant = await Post<NpcEditorSpawnView>(http, dormantPath + "/save",
            new { dormant.AuthoredSignature, expectedRevision = 0, settings }, ct);
        Assert.AreEqual(125, savedDormant.Configured!.RespawnSeconds);
        Assert.AreEqual(0, savedDormant.Configured.RespawnChance);
        var restoredDormant = await Post<NpcEditorSpawnView>(http, dormantPath + "/restore",
            new { dormant.AuthoredSignature, expectedRevision = savedDormant.Revision, revision = 0 }, ct);
        Assert.AreEqual(dormant.Original, restoredDormant.Configured);
        Assert.AreEqual(0, (await ReadPoint(2)).Revision);
        Assert.AreEqual(authored, File.ReadAllText(source));

        await VerifyNamedLootAssignment(http, actor, targets[0], targets[1], await ReadPoint(0), logger, ct);

        async Task<NpcEditorSpawnView> ReadPoint(int index)
            => (await http.GetFromJsonAsync<NpcEditorSpawnView>(pointsPath + "/" + index, ct))!;
    }

    static async Task VerifyNamedLootAssignment(HttpClient http, string actor, NpcEditorIdentity selected,
        NpcEditorIdentity neighbor, NpcEditorSpawnView point, ConnectedLogger logger, CancellationToken ct)
    {
        const string tables = "/npc-editor/loot-tables";
        var table = new NamedLootTable("connected-table", "Connected named loot", "Fixture-only authored loot", "ordinaryNpc",
            [new NamedLootGroup("guaranteed", "GUARANTEED_ALL", 1, 10_000, 0, null,
                [new NamedLootEntry("item", DropId, DropId, Quality, Quality, false, 1, 1, 10_000, 0, false)])]);
        var saved = await Post<NamedLootRegistryState>(http, tables + "/save", new NamedLootTableSave(0, table), ct, logger);
        Assert.AreEqual(1, saved.Revision);
        Assert.AreEqual(actor, saved.History.Single().Actor);
        Assert.AreEqual(table.Id, saved.Tables.Single().Id);

        var targets = (await http.GetFromJsonAsync<LootTargets>(tables + "/targets?playfieldId=" + point.ResourcePlayfieldId, ct))!;
        LootTargetSpawn exact = targets.Spawns.Single(spawn => spawn.AuthoredIndex == point.AuthoredIndex);
        Assert.AreEqual(point.AuthoredSignature, exact.AuthoredSignature);
        var assignment = new NamedLootAssignment("connected-placement", "authoredPoint", 0, "append", table.Id,
            PlayfieldId: exact.PlayfieldId, AuthoredIndex: exact.AuthoredIndex, AuthoredSignature: exact.AuthoredSignature);
        var edit = new NamedLootAssignmentSave(saved.Revision, assignment);
        using (HttpResponseMessage previewResponse = await http.PostAsJsonAsync(tables + "/assignments/preview", edit, ct))
        {
            Assert.IsTrue(previewResponse.IsSuccessStatusCode, await previewResponse.Content.ReadAsStringAsync(ct));
            JsonElement preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.AreEqual(1, preview.GetProperty("loadedMatches").GetInt32());
        }
        Assert.AreEqual(0, (await http.GetFromJsonAsync<NamedLootRegistryState>(tables, ct))!.Assignments.Length);

        var assigned = await Post<NamedLootRegistryState>(http, tables + "/assignments/save", edit, ct, logger);
        Assert.AreEqual(2, assigned.Revision);
        Assert.AreEqual(assignment, assigned.Assignments.Single());
        var selectedLoot = (await http.GetFromJsonAsync<NpcEditorEffectiveLootView>(NpcPath(selected) + "/loot/effective", ct))!;
        var neighborLoot = (await http.GetFromJsonAsync<NpcEditorEffectiveLootView>(NpcPath(neighbor) + "/loot/effective", ct))!;
        Assert.AreEqual(assignment.Id, selectedLoot.Resolution.Groups.Single().AssignmentId);
        Assert.AreEqual(exact.AuthoredIndex, selectedLoot.Resolution.Context.AuthoredIndex);
        Assert.AreEqual(DropId, selectedLoot.Candidates.Single(candidate => candidate.Eligible).LowId);
        Assert.AreEqual(0, neighborLoot.Resolution.Groups.Length);

        var simulation = await Post<NpcEditorLootSimulationResult>(http, NpcPath(selected) + "/loot/simulate",
            new NpcEditorLootSimulationRequest(24, 713), ct, logger);
        Assert.AreEqual(713, simulation.Seed);
        Assert.AreEqual(24, simulation.Groups.Single().ItemsGenerated);

        using HttpResponseMessage stale = await http.PostAsJsonAsync(tables + "/save",
            new NamedLootTableSave(saved.Revision, table), ct);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        using HttpResponseMessage referenced = await http.PostAsJsonAsync(tables + "/delete",
            new NamedLootDelete(assigned.Revision, table.Id), ct);
        Assert.AreEqual(HttpStatusCode.Conflict, referenced.StatusCode);

        var restored = await Post<NamedLootRegistryState>(http, tables + "/restore",
            new NamedLootRestore(assigned.Revision, saved.Revision), ct, logger);
        Assert.AreEqual(3, restored.Revision);
        Assert.AreEqual("restore", restored.History[^1].Action);
        Assert.AreEqual(table.Id, restored.Tables.Single().Id);
        Assert.AreEqual(0, restored.Assignments.Length);
        var afterRestore = (await http.GetFromJsonAsync<NpcEditorEffectiveLootView>(NpcPath(selected) + "/loot/effective", ct))!;
        Assert.AreEqual(0, afterRestore.Resolution.Groups.Length);
    }

    static async Task<T> Post<T>(HttpClient http, string path, object body, CancellationToken ct, ConnectedLogger? logger = null) where T : class
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(path, body, ct);
        Assert.IsTrue(response.IsSuccessStatusCode,
            $"{path} responded {(int)response.StatusCode}. {await response.Content.ReadAsStringAsync(ct)}\n{logger?.LastException}");
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

    sealed class ConnectedLogger : IZoneLogger
    {
        internal volatile Exception? LastException;
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(Exception exception, string message) => LastException = exception;
        public IZoneLogger CreateForPlayfield(int playfieldId) => this;
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
